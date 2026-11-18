using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CopiaSegura.Core
{
    // Ajustes portables: viven en <carpeta del exe>\datos\ajustes.txt (formato clave=valor).
    public sealed class Settings
    {
        const string FileName = "ajustes.txt";

        public string LastSource = "";
        public string LastDestination = "";
        public string TechnicianName = "";
        public Profile LastProfile = Profile.Segura;

        public static string ResolveDataDir(string exeDir, out bool usedFallback)
        {
            try
            {
                string dir = Path.Combine(exeDir, "datos");
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                usedFallback = false;
                return dir;
            }
            catch (Exception)
            {
                string fallback = Path.Combine(Path.GetTempPath(), "CopiaSegura_datos");
                Directory.CreateDirectory(fallback);
                usedFallback = true;
                return fallback;
            }
        }

        public static Settings Load(string dataDir)
        {
            Settings s = new Settings();
            try
            {
                string path = Path.Combine(dataDir, FileName);
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string key = line.Substring(0, i).Trim();
                    string val = line.Substring(i + 1);
                    if (key == "LastSource") s.LastSource = val;
                    else if (key == "LastDestination") s.LastDestination = val;
                    else if (key == "TechnicianName") s.TechnicianName = val;
                    else if (key == "LastProfile")
                    {
                        try { s.LastProfile = (Profile)Enum.Parse(typeof(Profile), val, false); }
                        catch (ArgumentException) { }
                    }
                }
            }
            catch (Exception)
            {
                return new Settings();
            }
            return s;
        }

        public void Save(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            List<string> lines = new List<string>();
            lines.Add("LastSource=" + Clean(LastSource));
            lines.Add("LastDestination=" + Clean(LastDestination));
            lines.Add("TechnicianName=" + Clean(TechnicianName));
            lines.Add("LastProfile=" + LastProfile);
            File.WriteAllLines(Path.Combine(dataDir, FileName), lines.ToArray(), Encoding.UTF8);
        }

        static string Clean(string v)
        {
            return (v ?? "").Replace("\r", "").Replace("\n", "");
        }
    }
}
