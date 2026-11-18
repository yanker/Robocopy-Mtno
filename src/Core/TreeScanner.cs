using System;
using System.Collections.Generic;
using System.IO;

namespace CopiaSegura.Core
{
    public sealed class ScanResult
    {
        public long Files;
        public long Bytes;
        public List<string> Unreadable = new List<string>();
    }

    public static class TreeScanner
    {
        public static ScanResult Scan(string root, bool skipJunctions, Func<bool> cancel, Action<long, long> progress)
        {
            return Scan(root, skipJunctions, cancel, progress, null);
        }

        // Si "collect" no es null, recibe la ruta completa de cada archivo encontrado.
        public static ScanResult Scan(string root, bool skipJunctions, Func<bool> cancel, Action<long, long> progress, List<string> collect)
        {
            ScanResult r = new ScanResult();
            bool excludeRootDirs = CommandBuilder.IsDriveRoot(root);
            ScanDir(root, excludeRootDirs, skipJunctions, cancel, progress, r, collect);
            return r;
        }

        static bool Cancelled(Func<bool> cancel)
        {
            return cancel != null && cancel();
        }

        static void ScanDir(string dir, bool isExcludingRoot, bool skipJunctions, Func<bool> cancel, Action<long, long> progress, ScanResult r, List<string> collect)
        {
            if (Cancelled(cancel)) return;
            string[] files;
            string[] subdirs;
            try
            {
                files = Directory.GetFiles(dir);
                subdirs = Directory.GetDirectories(dir);
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException || e is PathTooLongException || e is IOException || e is NotSupportedException || e is ArgumentException)
                {
                    r.Unreadable.Add(dir);
                    return;
                }
                throw;
            }

            foreach (string f in files)
            {
                try
                {
                    r.Bytes += new FileInfo(f).Length;
                    r.Files++;
                    if (collect != null) collect.Add(f);
                }
                catch (Exception)
                {
                    r.Unreadable.Add(f);
                }
            }
            if (progress != null) progress(r.Files, r.Bytes);

            foreach (string d in subdirs)
            {
                if (Cancelled(cancel)) return;
                string name = Path.GetFileName(d);
                if (isExcludingRoot && IsRootExcluded(name)) continue;
                if (skipJunctions)
                {
                    try
                    {
                        if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch (Exception)
                    {
                        r.Unreadable.Add(d);
                        continue;
                    }
                }
                ScanDir(d, false, skipJunctions, cancel, progress, r, collect);
            }
        }

        static bool IsRootExcluded(string name)
        {
            foreach (string x in CommandBuilder.RootExcludedDirs)
                if (string.Equals(x, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
