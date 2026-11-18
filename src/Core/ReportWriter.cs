using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CopiaSegura.Core
{
    public sealed class ReportData
    {
        public CopyOptions Options;
        public string Computer;
        public string Technician;
        public string CommandLine;
        public DateTime Start;
        public DateTime End;
        public long Files;
        public long Bytes;
        public RunResult Run;
        public VerifyResult Verify;          // null = no verificada
        public VerifyMode VerifyMode;
    }

    public static class ReportWriter
    {
        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            if (u == 0) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            return v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " " + units[u];
        }

        static string E(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        static void Row(StringBuilder sb, string label, string value)
        {
            sb.Append("<tr><th>").Append(E(label)).Append("</th><td>").Append(E(value)).Append("</td></tr>\n");
        }

        static void AppendList(StringBuilder sb, string label, System.Collections.Generic.List<string> items)
        {
            foreach (string s in items)
                sb.Append("<li>").Append(E(label)).Append(": <code>").Append(E(s)).Append("</code></li>\n");
        }

        public static string BuildHtml(ReportData d)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<!doctype html>\n<html lang=\"es\"><head><meta charset=\"utf-8\">");
            sb.Append("<title>Informe de copia segura</title>");
            sb.Append("<style>body{font:14px Segoe UI,Arial,sans-serif;max-width:800px;margin:24px auto;color:#1d2733}");
            sb.Append("table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:6px 10px;border-bottom:1px solid #d8dee6;vertical-align:top}");
            sb.Append("th{width:200px;color:#5d6b7a}.ok{color:#1a7f37}.bad{color:#c62828}.warn{color:#b35900}code{word-break:break-all}</style></head><body>\n");
            sb.Append("<h1>Informe de copia segura</h1>\n<table>\n");
            Row(sb, "Equipo", d.Computer);
            Row(sb, "Técnico", d.Technician);
            Row(sb, "Inicio", d.Start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Row(sb, "Fin", d.End.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Row(sb, "Origen", d.Options.Source);
            Row(sb, "Destino", d.Options.Destination);
            Row(sb, "Archivos", d.Files.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.'));
            Row(sb, "Tamaño", FormatBytes(d.Bytes));
            sb.Append("</table>\n");

            bool runOk = d.Run != null && d.Run.Success;
            sb.Append("<h2>Resultado</h2>\n<p class=\"").Append(runOk ? "ok" : "bad").Append("\"><b>")
              .Append(E(d.Run == null ? "Sin ejecutar" : d.Run.Describe())).Append("</b></p>\n");
            if (d.Run != null && d.Run.ErrorLines.Count > 0)
            {
                sb.Append("<h3>Errores (").Append(d.Run.ErrorLines.Count).Append(")</h3><ul>\n");
                foreach (string l in d.Run.ErrorLines) sb.Append("<li><code>").Append(E(l)).Append("</code></li>\n");
                sb.Append("</ul>\n");
            }

            sb.Append("<h2>Verificación</h2>\n");
            if (d.Verify == null)
            {
                sb.Append("<p class=\"warn\"><b>Copia NO verificada.</b> Terminar la copia no garantiza que los datos sean idénticos.</p>\n");
            }
            else
            {
                string mode = d.VerifyMode == VerifyMode.Sha256 ? "profunda (SHA-256)" : "rápida (cantidad y tamaño)";
                if (d.Verify.Ok)
                    sb.Append("<p class=\"ok\"><b>Verificada</b> — comprobación ").Append(mode).Append(": ")
                      .Append(d.Verify.FilesChecked).Append(" archivos coinciden.</p>\n");
                else
                {
                    sb.Append("<p class=\"bad\"><b>Verificación con diferencias</b> — comprobación ").Append(mode)
                      .Append(d.Verify.Cancelled ? " (cancelada antes de terminar)" : "")
                      .Append(". Faltan: ").Append(d.Verify.Missing.Count)
                      .Append(", tamaño o fecha distintos: ").Append(d.Verify.SizeMismatch.Count)
                      .Append(", contenido distinto: ").Append(d.Verify.HashMismatch.Count)
                      .Append(", ilegibles: ").Append(d.Verify.Unreadable.Count)
                      .Append(", sin verificar: ").Append(d.Verify.Skipped.Count).Append(".</p>\n<ul>\n");
                    AppendList(sb, "FALTA", d.Verify.Missing);
                    AppendList(sb, "TAMAÑO O FECHA DISTINTOS", d.Verify.SizeMismatch);
                    AppendList(sb, "CONTENIDO DISTINTO", d.Verify.HashMismatch);
                    AppendList(sb, "ILEGIBLE", d.Verify.Unreadable);
                    AppendList(sb, "SIN VERIFICAR", d.Verify.Skipped);
                    sb.Append("</ul>\n");
                }
            }

            sb.Append("<h2>Comando ejecutado</h2>\n<p><code>").Append(E(d.CommandLine)).Append("</code></p>\n");
            sb.Append("</body></html>\n");
            return sb.ToString();
        }

        public static void Save(string path, ReportData d)
        {
            File.WriteAllText(path, BuildHtml(d), new UTF8Encoding(true));
        }
    }
}
