using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CopiaSegura.Core
{
    public sealed class RunResult
    {
        public int ExitCode;
        public bool StoppedByUser;
        public int ErrorCount;
        public List<string> ErrorLines = new List<string>();

        public bool Success
        {
            get { return ExitCode >= 0 && ExitCode < 8 && !StoppedByUser; }
        }

        // Suma errores de varios tramos (pausas/reanudaciones) sin repetir los reintentados.
        public void AddErrors(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                string key = ProgressParser.ErrorKey(line);
                bool known = false;
                foreach (string existing in ErrorLines)
                    if (ProgressParser.ErrorKey(existing) == key) { known = true; break; }
                if (!known) ErrorLines.Add(line);
            }
            ErrorCount = ErrorLines.Count;
        }

        // Limpia = nada que copiar o todo copiado. Los codigos 2-7 son "completada" pero con avisos.
        public bool IsClean
        {
            get { return !StoppedByUser && (ExitCode == 0 || ExitCode == 1); }
        }

        public string Describe()
        {
            if (StoppedByUser) return "Detenida por el usuario.";
            if (ExitCode < 0 || ExitCode >= 8) return "FALLÓ: hubo errores, revisa el registro.";
            if (ExitCode == 0) return "Nada que copiar: ya estaba todo.";
            if (ExitCode == 1) return "Copia completada.";
            if (ExitCode == 2 || ExitCode == 3) return "Completada, con archivos extra en el destino.";
            return "Completada con diferencias; revisa el registro.";
        }
    }

    public sealed class RobocopyRunner
    {
        readonly string robocopyPath;
        readonly object gate = new object();
        Process process;
        bool stopRequested;

        public event Action<string> OutputLine;
        public event Action<ProgressTracker> ProgressChanged;

        public RobocopyRunner(string robocopyPath)
        {
            this.robocopyPath = robocopyPath;
        }

        public static string FindRobocopy()
        {
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "robocopy.exe");
            if (!File.Exists(p)) throw new FileNotFoundException("No se encuentra robocopy.exe en este equipo", p);
            return p;
        }

        // /UNILOG+ sobre un archivo nuevo escribe sin Unicode (se pierden ñ y acentos). Si el registro
        // aun no existe se crea vacio con la marca UTF-16, y asi /UNILOG+ anade en Unicode.
        static void EnsureUnicodeLog(CopyOptions o)
        {
            if (!o.WriteLog) return;
            try
            {
                string log = Path.GetFullPath(o.LogPath.Trim());
                if (File.Exists(log)) return;
                string dir = Path.GetDirectoryName(log);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(log, new byte[] { 0xFF, 0xFE });
            }
            catch (Exception) { /* si no se puede, robocopy informara del problema con el registro */ }
        }

        // Quita Oculto y Sistema de la carpeta de DESTINO (nunca toca el origen).
        public static void ClearHiddenSystem(string destinationDir)
        {
            try
            {
                if (!Directory.Exists(destinationDir)) return;
                FileAttributes a = File.GetAttributes(destinationDir);
                File.SetAttributes(destinationDir, a & ~(FileAttributes.Hidden | FileAttributes.System));
            }
            catch (Exception) { }
        }

        public void Stop()
        {
            lock (gate)
            {
                stopRequested = true;
                try
                {
                    if (process != null && !process.HasExited) process.Kill();
                }
                catch (Exception) { }
            }
        }

        public RunResult Run(CopyOptions o, long totalBytes)
        {
            RunResult result = new RunResult();
            ProgressTracker tracker = new ProgressTracker(totalBytes);
            ProcessStartInfo psi = new ProcessStartInfo(robocopyPath ?? FindRobocopy(), CommandBuilder.BuildArguments(o));
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

            // Un ejecutor es de un solo uso: si ya se pidió parar, no se pierde la orden ni se arranca nada.
            lock (gate)
            {
                if (stopRequested)
                {
                    result.StoppedByUser = true;
                    result.ExitCode = -1;
                    return result;
                }
            }
            EnsureUnicodeLog(o);
            Process p = Process.Start(psi);
            lock (gate)
            {
                process = p;
                if (stopRequested) { try { p.Kill(); } catch (Exception) { } }
            }

            StringBuilder line = new StringBuilder();
            int ch;
            while ((ch = p.StandardOutput.Read()) >= 0)
            {
                if (ch == '\r' || ch == '\n')
                {
                    if (line.Length > 0) { HandleLine(line.ToString(), tracker, result); line.Length = 0; }
                }
                else line.Append((char)ch);
            }
            if (line.Length > 0) HandleLine(line.ToString(), tracker, result);

            p.WaitForExit();
            result.ExitCode = p.ExitCode;
            lock (gate)
            {
                result.StoppedByUser = stopRequested;
                process = null;
            }
            p.Dispose();
            // Al copiar la raiz de un disco, robocopy deja la carpeta de destino oculta y de sistema.
            if (CommandBuilder.IsDriveRoot(o.Source)) ClearHiddenSystem(o.Destination);
            if (!result.StoppedByUser && result.ExitCode < 8) tracker.Finish();
            result.ErrorCount = tracker.ErrorCount;
            Action<ProgressTracker> pc = ProgressChanged;
            if (pc != null) pc(tracker);
            return result;
        }

        void HandleLine(string text, ProgressTracker tracker, RunResult result)
        {
            Action<string> ol = OutputLine;
            if (ol != null) ol(text);
            ParsedLine parsed = ProgressParser.Parse(text);
            if (parsed.Kind == LineKind.Other) return;
            if (parsed.Kind == LineKind.Error)
            {
                // Un error reintentado aparece varias veces: se guarda una sola vez.
                string key = ProgressParser.ErrorKey(text);
                bool known = false;
                foreach (string existing in result.ErrorLines)
                    if (ProgressParser.ErrorKey(existing) == key) { known = true; break; }
                if (!known) result.ErrorLines.Add(text);
            }
            tracker.Feed(parsed);
            Action<ProgressTracker> pc = ProgressChanged;
            if (pc != null) pc(tracker);
        }
    }
}
