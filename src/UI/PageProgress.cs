using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public sealed class StatsPanel : Panel, IFittable
    {
        public readonly Label[] Values = new Label[4];
        readonly Label[] captions = new Label[4];

        public StatsPanel(string[] captionTexts)
        {
            for (int i = 0; i < 4; i++)
            {
                Values[i] = new Label();
                Values[i].Font = new Font("Segoe UI", 13f, FontStyle.Bold);
                Values[i].TextAlign = ContentAlignment.MiddleCenter;
                Values[i].Text = "—";
                captions[i] = new Label();
                captions[i].Font = UiKit.Small;
                captions[i].ForeColor = UiKit.Muted;
                captions[i].TextAlign = ContentAlignment.MiddleCenter;
                captions[i].Text = captionTexts[i];
                Controls.Add(Values[i]);
                Controls.Add(captions[i]);
            }
            Height = 64;
            Margin = new Padding(0, 10, 0, 10);
            BackColor = Color.FromArgb(0xF4, 0xF6, 0xF9);
        }

        public void FitTo(int width)
        {
            Width = width;
            int w = width / 4;
            for (int i = 0; i < 4; i++)
            {
                Values[i].SetBounds(i * w, 6, w, 30);
                captions[i].SetBounds(i * w, 38, w, 20);
            }
        }
    }

    public sealed class PageProgress : UserControl, IWizardPage
    {
        sealed class Snap
        {
            public double Total, Current;
            public long Processed, FileSize;
            public string File = "";
            public int Errors;
        }

        readonly FlowLayoutPanel flow = UiKit.NewFlow();
        readonly WrapLabel headline = UiKit.Text("Copiando…", UiKit.Title, UiKit.Ink);
        readonly WrapLabel lblTotal = UiKit.Text("Progreso total: 0 %", UiKit.Bold, UiKit.Ink);
        readonly ProgressBar barTotal = new ProgressBar();
        readonly StatsPanel stats = new StatsPanel(new string[] { "copiado", "velocidad", "restante", "errores (ver registro)" });
        readonly WrapLabel lblFile = UiKit.Text("", UiKit.Normal, UiKit.Ink);
        readonly ProgressBar barFile = new ProgressBar();
        readonly Button btnPause = UiKit.Btn("Pausar (reanudable)", false);
        readonly Button btnCancel = UiKit.Btn("Cancelar…", false);
        readonly Button btnLog = UiKit.Btn("Ver registro en vivo", false);
        readonly TextBox txtLog = new TextBox();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly object gate = new object();
        readonly List<string> pendingLog = new List<string>();
        readonly List<KeyValuePair<DateTime, long>> samples = new List<KeyValuePair<DateTime, long>>();
        RunResult accumulated = new RunResult();
        readonly StringBuilder logText = new StringBuilder();

        WizardState state;
        RobocopyRunner runner;
        Snap snap = new Snap();
        volatile bool done;
        bool paused;
        bool cancelled;
        bool running;
        RunResult lastResult;
        Exception failure;

        public event EventHandler Finished;

        public PageProgress()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            barTotal.Maximum = 1000;
            barTotal.Height = 22;
            barFile.Maximum = 1000;
            barFile.Height = 14;
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = UiKit.Mono;
            txtLog.Height = 170;
            txtLog.Visible = false;
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Controls.AddRange(new Control[] { btnPause, btnCancel, btnLog });

            flow.Controls.Add(headline);
            flow.Controls.Add(UiKit.Text("Puedes dejarlo trabajando. No cierres el programa ni desconectes ningún disco.", UiKit.Normal, UiKit.Muted));
            flow.Controls.Add(lblTotal);
            flow.Controls.Add(barTotal);
            flow.Controls.Add(stats);
            flow.Controls.Add(lblFile);
            flow.Controls.Add(barFile);
            flow.Controls.Add(UiKit.Note("Los archivos muy grandes avanzan despacio; es normal. Si la velocidad baja a 0 durante unos minutos, no canceles: el disco puede estar reintentando."));
            flow.Controls.Add(buttons);
            flow.Controls.Add(txtLog);
            Controls.Add(flow);

            timer.Interval = 500;
            timer.Tick += delegate { Tick(); };
            btnPause.Click += delegate { TogglePause(); };
            btnCancel.Click += delegate { AskCancel(); };
            btnLog.Click += delegate { txtLog.Visible = !txtLog.Visible; UiKit.FitAll(flow); };
        }

        public string StepTitle { get { return "Copiando"; } }

        public bool IsRunning { get { return running && !paused; } }

        public bool CanAdvance(out string reason)
        {
            reason = "La copia está en curso.";
            return false;
        }

        public void OnEnter(WizardState s)
        {
            state = s;
            state.Start = DateTime.Now;
            state.Run = null;
            state.Verify = null;
            accumulated = new RunResult();
            samples.Clear();
            logText.Length = 0;
            txtLog.Text = "";
            cancelled = false;
            paused = false;
            lastResult = null;
            headline.Text = "Copiando…";
            btnPause.Text = "Pausar (reanudable)";
            btnPause.Enabled = true;
            btnCancel.Enabled = true;
            UiKit.FitAll(flow);
            StartRun();
            timer.Start();
        }

        public void OnLeave(WizardState s)
        {
            timer.Stop();
        }

        void StartRun()
        {
            done = false;
            failure = null;
            running = true;
            runner = new RobocopyRunner(null);
            runner.OutputLine += delegate(string line) { lock (gate) { pendingLog.Add(line); } };
            runner.ProgressChanged += delegate(ProgressTracker t)
            {
                Snap n = new Snap();
                n.Total = t.TotalPercent;
                n.Current = t.CurrentPercent;
                n.Processed = t.ProcessedBytes;
                n.FileSize = t.CurrentFileSize;
                n.File = t.CurrentFile ?? "";
                n.Errors = t.ErrorCount;
                lock (gate) { snap = n; }
            };
            Thread th = new Thread(delegate()
            {
                try { lastResult = runner.Run(state.Options, state.Scan == null ? 0 : state.Scan.Bytes); }
                catch (Exception e) { failure = e; }
                finally { done = true; }
            });
            th.IsBackground = true;
            th.Start();
        }

        // Versión síncrona para la prueba de humo.
        public void RunSync(WizardState s)
        {
            state = s;
            s.Start = DateTime.Now;
            RobocopyRunner r = new RobocopyRunner(null);
            s.Run = r.Run(s.Options, s.Scan == null ? 0 : s.Scan.Bytes);
            s.End = DateTime.Now;
        }

        void TogglePause()
        {
            if (!paused)
            {
                paused = true;
                btnPause.Enabled = false;
                headline.Text = "Pausando…";
                runner.Stop();
            }
            else
            {
                paused = false;
                headline.Text = "Copiando…";
                btnPause.Text = "Pausar (reanudable)";
                StartRun();
            }
        }

        void AskCancel()
        {
            DialogResult r = MessageBox.Show(FindForm(),
                "¿Seguro que quieres cancelar la copia?\r\n\r\nLo ya copiado se queda en el destino y no se borra nada. Podrás volver a copiar más tarde y continuará donde se quedó.",
                "Cancelar copia", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;
            cancelled = true;
            if (running) runner.Stop();
            else FinishWith(lastResult ?? NewStopped());   // en pausa: FinishWith suma los errores de todos los tramos
        }

        public bool ConfirmCloseWhileRunning(IWin32Window owner)
        {
            if (!IsRunning) return true;
            DialogResult r = MessageBox.Show(owner, "Hay una copia en curso. ¿Seguro que quieres salir? La copia se detendrá.",
                "Copia en curso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r == DialogResult.Yes) { cancelled = true; if (running) runner.Stop(); return true; }
            return false;
        }

        static RunResult NewStopped()
        {
            RunResult r = new RunResult();
            r.StoppedByUser = true;
            r.ExitCode = 1;
            return r;
        }

        void Tick()
        {
            Snap s;
            List<string> lines;
            lock (gate)
            {
                s = snap;
                lines = new List<string>(pendingLog);
                pendingLog.Clear();
            }
            if (lines.Count > 0)
            {
                foreach (string l in lines) logText.Append(l).Append("\r\n");
                if (logText.Length > 60000) logText.Remove(0, logText.Length - 40000);
                if (txtLog.Visible) { txtLog.Text = logText.ToString(); txtLog.SelectionStart = txtLog.TextLength; txtLog.ScrollToCaret(); }
            }
            UpdateView(s);
            if (running && done) OnRunEnded();
        }

        void UpdateView(Snap s)
        {
            long total = state.Scan == null ? 0 : state.Scan.Bytes;
            long fileCount = state.Scan == null ? 0 : state.Scan.Files;
            if (fileCount == 0) lblTotal.Text = "No hay nada que copiar.";
            else if (total == 0) lblTotal.Text = "Copiando archivos vacíos (sin contenido)…";
            else lblTotal.Text = "Progreso total: " + s.Total.ToString("0") + " %";
            barTotal.Value = (int)Math.Max(0, Math.Min(1000, s.Total * 10));
            lblFile.Text = s.File.Length == 0 ? "" : "Archivo actual: " + s.File + " (" + ReportWriter.FormatBytes(s.FileSize) + ") — " + s.Current.ToString("0") + " %";
            barFile.Value = (int)Math.Max(0, Math.Min(1000, s.Current * 10));

            DateTime now = DateTime.Now;
            samples.Add(new KeyValuePair<DateTime, long>(now, s.Processed));
            while (samples.Count > 1 && (now - samples[0].Key).TotalSeconds > 10) samples.RemoveAt(0);
            double speed = 0;
            if (samples.Count > 1)
            {
                double secs = (samples[samples.Count - 1].Key - samples[0].Key).TotalSeconds;
                if (secs >= 1) speed = (samples[samples.Count - 1].Value - samples[0].Value) / secs;
            }
            stats.Values[0].Text = ReportWriter.FormatBytes(s.Processed) + " / " + ReportWriter.FormatBytes(total);
            stats.Values[1].Text = speed > 0 ? ReportWriter.FormatBytes((long)speed) + "/s" : "—";
            if (speed > 0 && total > s.Processed)
            {
                TimeSpan left = TimeSpan.FromSeconds((total - s.Processed) / speed);
                stats.Values[2].Text = left.TotalHours >= 1 ? (int)left.TotalHours + " h " + left.Minutes + " min" : Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)) + " min";
            }
            else stats.Values[2].Text = "—";
            stats.Values[3].Text = s.Errors.ToString();
        }

        void OnRunEnded()
        {
            running = false;
            if (failure != null)
            {
                RunResult f = new RunResult();
                f.ExitCode = 16;
                f.ErrorLines.Add("No se pudo ejecutar robocopy: " + failure.Message);
                FinishWith(f);
                return;
            }
            accumulated.AddErrors(lastResult.ErrorLines);
            if (paused && !cancelled)
            {
                headline.Text = "Copia en pausa";
                btnPause.Text = "Reanudar";
                btnPause.Enabled = true;
                return;
            }
            FinishWith(lastResult);
        }

        void FinishWith(RunResult r)
        {
            timer.Stop();
            if (cancelled) r.StoppedByUser = true;
            // Los errores de todos los tramos (pausas incluidas), sin repetir los reintentados.
            accumulated.AddErrors(r.ErrorLines);
            r.ErrorLines = new List<string>(accumulated.ErrorLines);
            r.ErrorCount = accumulated.ErrorCount;
            state.Run = r;
            state.End = DateTime.Now;
            EventHandler h = Finished;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
