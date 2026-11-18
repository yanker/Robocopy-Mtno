using System;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.Tests
{
    public static class SettingsTests
    {
        [Test]
        public static void RoundTrip()
        {
            string dir = Tmp.Create();
            try
            {
                Settings s = new Settings();
                s.LastSource = @"E:\Usuarios\Alejandro=raro";
                s.LastDestination = @"D:\Respaldo ñ";
                s.TechnicianName = "Técnico Uno";
                s.LastProfile = Profile.Rapida;
                s.Save(dir);
                Settings t = Settings.Load(dir);
                Check.Equal(s.LastSource, t.LastSource);
                Check.Equal(s.LastDestination, t.LastDestination);
                Check.Equal(s.TechnicianName, t.TechnicianName);
                Check.Equal(Profile.Rapida, t.LastProfile);
            }
            finally { Tmp.Delete(dir); }
        }

        [Test]
        public static void CorruptFile_Defaults()
        {
            string dir = Tmp.Create();
            try
            {
                File.WriteAllBytes(Path.Combine(dir, "ajustes.txt"), new byte[] { 0, 1, 2, 255, 254, 0, 61, 61, 10, 13 });
                Settings t = Settings.Load(dir);
                Check.Equal(Profile.Segura, t.LastProfile);
                Check.Equal("", t.LastSource);
                Check.Equal("", Settings.Load(Path.Combine(dir, "no_existe")).LastDestination);
            }
            finally { Tmp.Delete(dir); }
        }

        [Test]
        public static void ResolveDataDir_Writable_UsesDatosFolder()
        {
            string exeDir = Tmp.Create();
            try
            {
                bool fb;
                string d = Settings.ResolveDataDir(exeDir, out fb);
                Check.True(!fb);
                Check.Equal(Path.Combine(exeDir, "datos"), d);
                Check.True(Directory.Exists(d));
            }
            finally { Tmp.Delete(exeDir); }
        }

        [Test]
        public static void ReadOnlyExeDir_FallsBackToTemp()
        {
            string exeDir = Tmp.Create();
            try
            {
                Tmp.Cmd("icacls \"" + exeDir + "\" /deny \"" + Environment.UserName + ":(OI)(CI)(W)\"");
                bool fb;
                string d = Settings.ResolveDataDir(exeDir, out fb);
                Check.True(fb, "debia usar el respaldo");
                Check.True(d.StartsWith(Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase), d);
                Check.True(Directory.Exists(d));
            }
            finally
            {
                Tmp.Cmd("icacls \"" + exeDir + "\" /remove:d \"" + Environment.UserName + "\"");
                Tmp.Delete(exeDir);
            }
        }
    }
}
