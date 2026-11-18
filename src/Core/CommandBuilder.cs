using System;
using System.Text;

namespace CopiaSegura.Core
{
    public static class CommandBuilder
    {
        public static readonly string[] RootExcludedDirs = new string[] { "System Volume Information", "$RECYCLE.BIN" };

        public static bool IsDriveRoot(string path)
        {
            if (path == null) return false;
            string p = path.Trim();
            if (p.Length == 2 && p[1] == ':') return true;
            return p.Length == 3 && p[1] == ':' && (p[2] == '\\' || p[2] == '/');
        }

        // Una barra final antes de la comilla de cierre rompe el analisis de robocopy ("E:\"):
        // se anade un punto para que la ruta siga siendo la misma carpeta.
        public static string Quote(string path)
        {
            if (path == null) throw new ArgumentException("Ruta vacia");
            if (path.IndexOf('"') >= 0) throw new ArgumentException("La ruta contiene comillas");
            if (path.EndsWith("\\")) path += ".";
            return "\"" + path + "\"";
        }

        public static string BuildArguments(CopyOptions o)
        {
            if (o.Mirror || o.Move) o.EnsureSourceNotInsideDestination();
            // Rutas absolutas y normalizadas: una ruta como "/MIR" nunca llega a robocopy como si fuera una opcion.
            string source = FullPath(o.Source);
            StringBuilder sb = new StringBuilder();
            sb.Append(Quote(source)).Append(' ').Append(Quote(FullPath(o.Destination)));
            if (o.IncludeSubfolders) sb.Append(" /E");
            if (o.Restartable) sb.Append(" /Z");
            sb.Append(o.PreserveDates ? " /COPY:DAT /DCOPY:DAT" : " /COPY:D /DCOPY:D");
            if (o.SkipJunctions) sb.Append(" /XJ");
            if (IsDriveRoot(source))
            {
                sb.Append(" /XD");
                foreach (string d in RootExcludedDirs) sb.Append(" \"").Append(d).Append('"');
            }
            sb.Append(" /R:").Append(o.Retries);
            sb.Append(" /W:").Append(o.WaitSeconds);
            sb.Append(" /MT:").Append(o.Threads);
            sb.Append(" /TEE /BYTES /NDL");
            if (o.Mirror) sb.Append(" /MIR");
            if (o.Move) sb.Append(" /MOVE");
            // /UNILOG+: registro Unicode (conserva ñ y acentos) que se AÑADE al existente al reanudar.
            if (o.WriteLog) sb.Append(" /UNILOG+:").Append(Quote(FullPath(o.LogPath)));
            return sb.ToString();
        }

        static string FullPath(string path)
        {
            try { return System.IO.Path.GetFullPath(path.Trim()); }
            catch (Exception e)
            {
                if (e is ArgumentException || e is NotSupportedException || e is System.IO.PathTooLongException)
                    throw new ArgumentException("Ruta no válida: " + path, e);
                throw;
            }
        }
    }
}
