using System;
using System.IO;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    // Recorre los 5 pasos sin mostrar ventanas, con una copia real pequeña.
    public static class SmokeTest
    {
        // Guarda una captura PNG de cada paso en "dir" (para revisar el aspecto de la interfaz).
        public static string Shots(string dir)
        {
            // Ruta neutra para que las capturas no muestren el usuario ni el equipo de quien las genera.
            string tmp = @"C:\Temp\CopiaSegura_demo";
            try
            {
                Directory.CreateDirectory(dir);
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                string src = Path.Combine(tmp, "Origen");
                string dst = Path.Combine(tmp, "Respaldo");
                Directory.CreateDirectory(Path.Combine(src, "AppData"));
                File.WriteAllText(Path.Combine(src, "documento.txt"), "hola");
                using (FileStream fs = File.Create(Path.Combine(src, @"AppData\grande.dat"))) fs.SetLength(1500L * 1024 * 1024);

                using (MainForm f = new MainForm())
                {
                    f.Show();
                    Pump(f, 300);
                    f.DemoSetPaths(src, dst);
                    Pump(f, 300);
                    Save(f, Path.Combine(dir, "paso1.png"));
                    f.ShowStep(1); Pump(f, 300); Save(f, Path.Combine(dir, "paso2.png"));
                    f.ShowStep(2); Pump(f, 300); Save(f, Path.Combine(dir, "paso3.png"));
                    f.ShowStep(3); Pump(f, 1200); Save(f, Path.Combine(dir, "paso4.png"));
                    for (int i = 0; i < 300 && f.CurrentStep != 4; i++) Pump(f, 100);
                    Pump(f, 300);
                    Save(f, Path.Combine(dir, "paso5.png"));
                    f.FormClosing += delegate(object s, System.Windows.Forms.FormClosingEventArgs e) { e.Cancel = false; };
                }
                return null;
            }
            catch (Exception e) { return e.ToString(); }
            finally { try { Directory.Delete(tmp, true); } catch (Exception) { } }
        }

        static void Pump(System.Windows.Forms.Form f, int ms)
        {
            DateTime end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        }

        static void Save(System.Windows.Forms.Form f, string path)
        {
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static string Run()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cs_smoke_" + Guid.NewGuid().ToString("N"));
            try
            {
                string src = Path.Combine(tmp, "origen");
                string dst = Path.Combine(tmp, "destino");
                Directory.CreateDirectory(Path.Combine(src, "sub carpeta"));
                File.WriteAllText(Path.Combine(src, "a.txt"), "hola");
                File.WriteAllBytes(Path.Combine(src, @"sub carpeta\b.bin"), new byte[4096]);

                WizardState st = new WizardState();
                st.DataDir = Path.Combine(tmp, "datos");
                st.Settings = new Settings();
                string reason;

                // Paso 1
                PageSource p1 = new PageSource();
                p1.OnEnter(st);
                p1.SetPaths(src, dst);
                p1.ValidateNow();
                if (!p1.CanAdvance(out reason)) return "paso 1 no deja avanzar: " + reason;
                p1.OnLeave(st);
                if (st.Scan == null || st.Scan.Files != 2) return "el escaneo no encontro 2 archivos";

                // Paso 1 con destino dentro del origen: debe bloquear
                p1.SetPaths(src, Path.Combine(src, "dentro"));
                p1.ValidateNow();
                if (p1.CanAdvance(out reason)) return "el destino dentro del origen no se bloqueo";
                p1.SetPaths(src, dst);
                p1.ValidateNow();

                // Paso 2
                PageOptions p2 = new PageOptions();
                p2.OnEnter(st);
                p2.OnLeave(st);
                if (st.Options == null || st.Options.Threads != 2 || st.Options.Retries != 1 || st.Options.Mirror)
                    return "las opciones por defecto no son las del perfil seguro";

                // Paso 3
                PageReview p3 = new PageReview();
                p3.OnEnter(st);
                if (!p3.CommandText.StartsWith("robocopy ")) return "el comando de revision no empieza por robocopy";
                if (!p3.CommandText.Contains("/MT:2")) return "el comando no incluye /MT:2";

                // Paso 4
                PageProgress p4 = new PageProgress();
                p4.RunSync(st);
                if (st.Run == null || !st.Run.Success) return "la copia fallo: " + (st.Run == null ? "sin resultado" : st.Run.Describe());

                // Paso 5
                PageVerify p5 = new PageVerify();
                p5.OnEnter(st);
                VerifyResult v = p5.VerifySync(VerifyMode.Quick);
                if (!v.Ok) return "la verificacion rapida encontro diferencias";
                VerifyResult v2 = p5.VerifySync(VerifyMode.Sha256);
                if (!v2.Ok) return "la verificacion SHA-256 encontro diferencias";
                string report = Path.Combine(tmp, "informe.html");
                p5.SaveReport(report);
                if (!File.ReadAllText(report).Contains(Environment.MachineName)) return "el informe no contiene el nombre del equipo";

                // El resumen no debe prometer que no se sobrescribe nada en el destino
                if (!p3.SummaryText.Contains("sustituyen")) return "el resumen no avisa de que los archivos con el mismo nombre se sustituyen";
                if (p3.SummaryText.Contains("No se borrará nada en el destino")) return "el resumen promete algo falso sobre el destino";

                // Con Mover el origen desaparece: no se puede verificar contra el
                WizardState mv = new WizardState();
                mv.Settings = new Settings();
                mv.Scan = st.Scan;
                mv.Options = CopyOptions.ForProfile(Profile.Segura, src, dst);
                mv.Options.TryUnlockDangerous("BORRAR");
                mv.Options.SetMove(true);
                mv.Run = st.Run;
                PageVerify pm = new PageVerify();
                pm.OnEnter(mv);
                if (pm.CanVerify) return "con Mover activado no debe permitirse verificar";

                // Los botones de paso no deben saltarse la validacion del paso 1
                using (MainForm f = new MainForm())
                {
                    f.DemoSetPaths(src, Path.Combine(src, "dentro"));
                    if (f.TryShowStep(2)) return "se pudo saltar al paso 3 con un destino invalido";
                    f.DemoSetPaths(src, dst);
                    if (!f.TryShowStep(2)) return "no se pudo avanzar con rutas validas";
                }
                return null;
            }
            catch (Exception e)
            {
                return e.ToString();
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch (Exception) { }
            }
        }
    }
}
