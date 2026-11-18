using System;
using System.Diagnostics;
using System.IO;

namespace CopiaSegura.Tests
{
    // Utilidades de prueba: carpetas temporales y comandos auxiliares.
    public static class Tmp
    {
        public static string Create()
        {
            string p = Path.Combine(Path.GetTempPath(), "cs_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(p);
            return p;
        }

        public static void WriteFile(string path, long size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] buf = new byte[size];
            new Random((int)(size % 9973) + 1).NextBytes(buf);
            File.WriteAllBytes(path, buf);
        }

        // "ruta relativa:hash" de cada archivo, ordenado: sirve para comparar dos arboles.
        public static string HashTree(string root)
        {
            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
            foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                using (FileStream fs = File.OpenRead(f))
                using (System.Security.Cryptography.SHA256Managed sha = new System.Security.Cryptography.SHA256Managed())
                {
                    lines.Add(f.Substring(root.Length).ToLowerInvariant() + ":" + BitConverter.ToString(sha.ComputeHash(fs)));
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines.ToArray());
        }

        public static void CopyTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
            foreach (string d in Directory.GetDirectories(src)) CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        public static int Cmd(string commandLine)
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + commandLine);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        public static void Delete(string path)
        {
            try { Cmd("rmdir /s /q \"\\\\?\\" + path + "\""); } catch { }
        }
    }
}
