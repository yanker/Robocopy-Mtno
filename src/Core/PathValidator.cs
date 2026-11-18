using System;
using System.Collections.Generic;
using System.IO;

namespace CopiaSegura.Core
{
    public enum Severity { Ok, Warning, Error }

    public sealed class CheckResult
    {
        public Severity Severity;
        public string Message;

        public CheckResult(Severity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class PathValidator
    {
        static string Normalize(string p)
        {
            string full = Path.GetFullPath(p.Trim());
            if (full.Length > 3) full = full.TrimEnd('\\', '/');
            return full;
        }

        public static List<CheckResult> Validate(string source, string destination, long sourceBytes, long destFreeBytes)
        {
            List<CheckResult> r = new List<CheckResult>();
            if (string.IsNullOrEmpty(source) || source.Trim().Length == 0)
            {
                r.Add(new CheckResult(Severity.Error, "Elige la carpeta de origen."));
                return r;
            }
            if (string.IsNullOrEmpty(destination) || destination.Trim().Length == 0)
            {
                r.Add(new CheckResult(Severity.Error, "Elige dónde guardar la copia."));
                return r;
            }

            string src, dst;
            try
            {
                src = Normalize(source);
                dst = Normalize(destination);
            }
            catch (Exception)
            {
                r.Add(new CheckResult(Severity.Error, "Alguna de las rutas no es válida."));
                return r;
            }

            if (!Directory.Exists(src))
            {
                r.Add(new CheckResult(Severity.Error, "La carpeta de origen no existe o no se puede leer."));
                return r;
            }
            r.Add(new CheckResult(Severity.Ok, "El origen existe y se puede leer."));

            string srcWithSlash = src.EndsWith("\\") ? src : src + "\\";
            string dstWithSlash = dst.EndsWith("\\") ? dst : dst + "\\";
            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
            {
                r.Add(new CheckResult(Severity.Error, "El destino no puede ser la misma carpeta que el origen."));
                return r;
            }
            if (dstWithSlash.StartsWith(srcWithSlash, StringComparison.OrdinalIgnoreCase))
            {
                r.Add(new CheckResult(Severity.Error, "El destino no puede estar dentro del origen."));
                return r;
            }
            if (srcWithSlash.StartsWith(dstWithSlash, StringComparison.OrdinalIgnoreCase))
                r.Add(new CheckResult(Severity.Warning, "El origen está dentro del destino. Revisa que sea lo que quieres."));

            string dstRoot = Path.GetPathRoot(dst);
            if (!Directory.Exists(dstRoot))
            {
                r.Add(new CheckResult(Severity.Error, "La unidad del destino no está disponible."));
                return r;
            }

            if (string.Equals(Path.GetPathRoot(src), dstRoot, StringComparison.OrdinalIgnoreCase))
                r.Add(new CheckResult(Severity.Warning, "Origen y destino están en la misma unidad: no será una copia de seguridad independiente."));
            else
                r.Add(new CheckResult(Severity.Ok, "Origen y destino están en unidades distintas."));

            if (sourceBytes >= 0 && destFreeBytes >= 0)
            {
                if (destFreeBytes < sourceBytes)
                    r.Add(new CheckResult(Severity.Error, "No hay espacio suficiente en el destino."));
                else if (destFreeBytes < (long)(sourceBytes * 1.1))
                    r.Add(new CheckResult(Severity.Warning, "El espacio libre en el destino es justo."));
                else
                    r.Add(new CheckResult(Severity.Ok, "Hay espacio de sobra en el destino."));
            }
            return r;
        }

        // true si "child" es la misma carpeta que "parent" o está dentro de ella.
        public static bool IsInside(string child, string parent)
        {
            try
            {
                string c = Normalize(child);
                string p = Normalize(parent);
                if (!c.EndsWith("\\")) c += "\\";
                if (!p.EndsWith("\\")) p += "\\";
                return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Espacio que aún hace falta: lo que ya hay en el destino (copia anterior cancelada) no se vuelve a contar.
        public static long BytesStillNeeded(string destination, long sourceBytes, Func<bool> cancel)
        {
            try
            {
                if (sourceBytes <= 0 || !Directory.Exists(destination)) return sourceBytes;
                long already = TreeScanner.Scan(destination, true, cancel, null).Bytes;
                long needed = sourceBytes - already;
                return needed < 0 ? 0 : needed;
            }
            catch (Exception)
            {
                return sourceBytes;
            }
        }

        public static long GetFreeBytes(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(root)) return -1;
                if (root.StartsWith("\\\\")) return -1;
                return new DriveInfo(root.Substring(0, 1)).AvailableFreeSpace;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
