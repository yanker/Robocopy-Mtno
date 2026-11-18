using System;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class ReportWriterTests
    {
        static ReportData Sample()
        {
            ReportData d = new ReportData();
            d.Options = CopyOptions.ForProfile(Profile.Segura, @"E:\A&B", @"D:\Destino");
            d.Computer = "PC-CLIENTE";
            d.Technician = "Técnico";
            d.CommandLine = "robocopy ...";
            d.Start = new DateTime(2026, 11, 18, 10, 0, 0);
            d.End = new DateTime(2026, 11, 18, 11, 20, 0);
            d.Files = 212430;
            d.Bytes = 148L * 1024 * 1024 * 1024;
            d.Run = new RunResult();
            d.Run.ExitCode = 1;
            return d;
        }

        [Test]
        public static void EscapesAmpersandInPaths()
        {
            string html = ReportWriter.BuildHtml(Sample());
            Check.True(html.Contains(@"E:\A&amp;B"), "ruta escapada");
            Check.True(html.Contains("PC-CLIENTE"));
            Check.True(html.Contains("charset=utf-8") || html.Contains("charset=\"utf-8\""));
        }

        [Test]
        public static void NullVerify_SaysNotVerified()
        {
            ReportData d = Sample();
            d.Verify = null;
            Check.True(ReportWriter.BuildHtml(d).Contains("NO verificada"));
        }

        [Test]
        public static void OkVerify_SaysVerified()
        {
            ReportData d = Sample();
            d.Verify = new VerifyResult();
            d.VerifyMode = VerifyMode.Sha256;
            string html = ReportWriter.BuildHtml(d);
            Check.True(html.Contains("Verificada") && html.Contains("SHA-256"), html);
            Check.True(!html.Contains("NO verificada"));
        }

        [Test]
        public static void SkippedOrUnreadable_NeverReportedAsVerified()
        {
            ReportData d = Sample();
            d.Verify = new VerifyResult();
            d.Verify.Skipped.Add("x.bin");
            d.Verify.Unreadable.Add("y.bin");
            d.VerifyMode = VerifyMode.Quick;
            string html = ReportWriter.BuildHtml(d);
            Check.True(!html.Contains("<b>Verificada</b>"), "no puede decir Verificada");
            Check.True(html.Contains("sin verificar: 1"), html);
            Check.True(html.Contains("ilegibles: 1"), html);
            Check.True(html.Contains("x.bin") && html.Contains("y.bin"), "lista los archivos afectados");
        }

        [Test]
        public static void ListsErrorsEscaped()
        {
            ReportData d = Sample();
            d.Run.ExitCode = 9;
            d.Run.ErrorLines.Add("ERROR 5 (0x00000005) Copying File E:\\<x>.tmp");
            string html = ReportWriter.BuildHtml(d);
            Check.True(html.Contains("&lt;x&gt;.tmp"), "error escapado");
            Check.True(html.Contains("FALLÓ"));
        }

        [Test]
        public static void Save_WritesUtf8File()
        {
            string dir = Tmp.Create();
            try
            {
                string path = Path.Combine(dir, "informe.html");
                ReportWriter.Save(path, Sample());
                Check.True(File.ReadAllText(path, System.Text.Encoding.UTF8).Contains("Técnico"));
            }
            finally { Tmp.Delete(dir); }
        }

        [Test]
        public static void FormatBytes_Readable()
        {
            Check.Equal("148,0 GB", ReportWriter.FormatBytes(148L * 1024 * 1024 * 1024));
            Check.Equal("0 B", ReportWriter.FormatBytes(0));
            Check.Equal("1,5 MB", ReportWriter.FormatBytes(1572864));
        }
    }
}
