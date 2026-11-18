using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CopiaSegura.Core
{
    public enum VerifyMode { Quick, Sha256 }

    public sealed class VerifyResult
    {
        public long FilesChecked;
        public List<string> Missing = new List<string>();
        public List<string> SizeMismatch = new List<string>();
        public List<string> HashMismatch = new List<string>();
        public List<string> Unreadable = new List<string>();
        // Archivos que se pidió no comprobar: quedan SIN verificar, nunca cuentan como correctos.
        public List<string> Skipped = new List<string>();
        public bool Cancelled;

        public bool Ok
        {
            get
            {
                return !Cancelled && Missing.Count == 0 && SizeMismatch.Count == 0
                    && HashMismatch.Count == 0 && Unreadable.Count == 0 && Skipped.Count == 0;
            }
        }
    }

    public static class Verifier
    {
        // Rutas relativas al origen. progress(hechos, total, archivoActual).
        public static VerifyResult Verify(string source, string destination, VerifyMode mode, bool skipJunctions,
            ICollection<string> skipRelativePaths, Func<bool> cancel, Action<long, long, string> progress)
        {
            return Verify(source, destination, mode, skipJunctions, skipRelativePaths, cancel, progress, true);
        }

        // compareDates: la comprobación rápida también exige la misma fecha de modificación
        // (un archivo interrumpido puede tener ya el tamaño final pero otra fecha).
        public static VerifyResult Verify(string source, string destination, VerifyMode mode, bool skipJunctions,
            ICollection<string> skipRelativePaths, Func<bool> cancel, Action<long, long, string> progress, bool compareDates)
        {
            VerifyResult r = new VerifyResult();
            List<string> files = new List<string>();
            ScanResult scan = TreeScanner.Scan(source, skipJunctions, cancel, null, files);
            r.Unreadable.AddRange(scan.Unreadable);
            if (cancel != null && cancel()) { r.Cancelled = true; return r; }

            HashSet<string> skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (skipRelativePaths != null) foreach (string s in skipRelativePaths) skip.Add(s);

            string srcRoot = source.TrimEnd('\\', '/');
            long done = 0;
            foreach (string f in files)
            {
                if (cancel != null && cancel()) { r.Cancelled = true; return r; }
                string rel = f.Substring(srcRoot.Length).TrimStart('\\', '/');
                done++;
                if (progress != null) progress(done, files.Count, rel);
                if (skip.Contains(rel)) { r.Skipped.Add(rel); continue; }
                r.FilesChecked++;
                string d = Path.Combine(destination, rel);
                try
                {
                    if (!File.Exists(d)) { r.Missing.Add(rel); continue; }
                    FileInfo fi = new FileInfo(f);
                    FileInfo di = new FileInfo(d);
                    if (fi.Length != di.Length) { r.SizeMismatch.Add(rel); continue; }
                    if (compareDates && Math.Abs((fi.LastWriteTimeUtc - di.LastWriteTimeUtc).TotalSeconds) > 2) { r.SizeMismatch.Add(rel); continue; }
                    if (mode == VerifyMode.Sha256 && HashOf(f) != HashOf(d)) r.HashMismatch.Add(rel);
                }
                catch (Exception e)
                {
                    if (e is IOException || e is UnauthorizedAccessException) r.Unreadable.Add(rel);
                    else throw;
                }
            }
            return r;
        }

        // Las lineas "ERROR n (0x..) Copying File <ruta>" de robocopy -> rutas relativas al origen.
        public static List<string> RelativePathsFromErrors(IEnumerable<string> errorLines, string sourceRoot)
        {
            List<string> result = new List<string>();
            string root = sourceRoot.TrimEnd('\\', '/');
            foreach (string line in errorLines)
            {
                int i = line.IndexOf(root, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string rest = line.Substring(i + root.Length).TrimStart('\\', '/').Trim();
                if (rest.Length > 0) result.Add(rest);
            }
            return result;
        }

        static string HashOf(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
            using (SHA256Managed sha = new SHA256Managed())
            {
                return BitConverter.ToString(sha.ComputeHash(fs));
            }
        }
    }
}
