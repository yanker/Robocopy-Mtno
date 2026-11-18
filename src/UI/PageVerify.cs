using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public sealed class PageVerify : UserControl, IWizardPage
    {
        readonly FlowLayoutPanel flow = UiKit.NewFlow();
        readonly WrapLabel headline = UiKit.Text("Copia terminada", UiKit.Title, UiKit.Ink);
        readonly WrapLabel result = UiKit.Badge(Severity.Ok, "");
        readonly WrapLabel counts = UiKit.Text("", UiKit.Normal, UiKit.Ink);
        readonly RadioButton rdoQuick = new RadioButton();
        readonly RadioButton rdoSha = new RadioButton();
        readonly ProgressBar bar = new ProgressBar();
        readonly WrapLabel verifyStatus = UiKit.Text("", UiKit.Normal, UiKit.Muted);
        readonly ListBox problems = new ListBox();
        readonly Button btnVerify = UiKit.Btn("Verificar ahora", true);
        readonly Button btnRetry = UiKit.Btn("Reintentar solo los fallidos", false);
        readonly Button btnReport = UiKit.Btn("Guardar informe…", false);
        readonly TextBox txtTech = new TextBox();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly object gate = new object();

        WizardState state;
        volatile bool busy;
        volatile bool workDone;
        bool cancelVerify;
        long vDone, vTotal;
        string vCurrent = "";
        VerifyResult vResult;
        RunResult retryResult;
        RobocopyRunner retryRunner;
        Exception failure;
        bool isRetry;

        public PageVerify()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            rdoQuick.Text = "Rápida: comparar cantidad y tamaño (segundos). Detecta archivos que faltan o están cortados.";
            rdoSha.Text = "Profunda: SHA-256. Lee origen y destino y compara el contenido real. Lenta, pero es la prueba definitiva para datos irremplazables.";
            foreach (RadioButton r in new RadioButton[] { rdoQuick, rdoSha })
            {
                r.Font = UiKit.Normal;
                r.AutoSize = false;
                r.Height = 40;
                r.Margin = new Padding(0, 2, 0, 2);
            }
            rdoQuick.Checked = true;
            bar.Maximum = 1000;
            bar.Height = 18;
            problems.Height = 110;
            problems.Font = UiKit.Mono;
            problems.HorizontalScrollbar = true;
            txtTech.Font = UiKit.Normal;
            txtTech.Width = 240;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Controls.AddRange(new Control[] { btnVerify, btnRetry, btnReport });
            FlowLayoutPanel techRow = new FlowLayoutPanel();
            techRow.AutoSize = true;
            Label tl = new Label();
            tl.Text = "Nombre del técnico (para el informe):";
            tl.AutoSize = true;
            tl.Font = UiKit.Normal;
            tl.Margin = new Padding(0, 6, 8, 0);
            techRow.Controls.Add(tl);
            techRow.Controls.Add(txtTech);

            flow.Controls.Add(headline);
            flow.Controls.Add(UiKit.Text("Terminar no significa verificado. Elige cómo comprobarla.", UiKit.Normal, UiKit.Muted));
            flow.Controls.Add(result);
            flow.Controls.Add(counts);
            flow.Controls.Add(UiKit.Section("Archivos con problemas"));
            flow.Controls.Add(problems);
            flow.Controls.Add(UiKit.Section("Verificación"));
            flow.Controls.Add(rdoQuick);
            flow.Controls.Add(rdoSha);
            flow.Controls.Add(bar);
            flow.Controls.Add(verifyStatus);
            flow.Controls.Add(buttons);
            flow.Controls.Add(techRow);
            flow.Controls.Add(UiKit.Note("Conserva el origen hasta verificar y haz una segunda copia en otro soporte."));
            Controls.Add(flow);

            timer.Interval = 300;
            timer.Tick += delegate { Tick(); };
            btnVerify.Click += delegate { StartVerify(); };
            btnRetry.Click += delegate { StartRetry(); };
            btnReport.Click += delegate { SaveReportDialog(); };
        }

        public string StepTitle { get { return "Verificar y terminar"; } }

        // Con "Mover" el origen se borra al copiar: no hay contra qué verificar.
        public bool CanVerify
        {
            get { return state != null && state.Options != null && !state.Options.Move && !busy; }
        }

        public bool CanAdvance(out string reason)
        {
            reason = null;
            return false;
        }

        public void OnEnter(WizardState s)
        {
            state = s;
            txtTech.Text = s.Settings.TechnicianName ?? "";
            bar.Value = 0;
            verifyStatus.Text = "";
            Refresh_();
        }

        public void OnLeave(WizardState s)
        {
            s.Settings.TechnicianName = txtTech.Text;
            try { s.Settings.Save(s.DataDir); } catch (Exception) { }
            timer.Stop();
        }

        void Refresh_()
        {
            RunResult r = state.Run;
            // Verde solo si fue limpia (códigos 0 o 1); completada con avisos (2-7) = ámbar; fallo o detenida = rojo.
            Severity sev = r == null || !r.Success ? Severity.Error : (r.IsClean && r.ErrorCount == 0 ? Severity.Ok : Severity.Warning);
            result.Text = (sev == Severity.Ok ? "✔ " : sev == Severity.Warning ? "⚠ " : "✖ ") + (r == null ? "Sin resultado" : r.Describe());
            result.ForeColor = sev == Severity.Ok ? UiKit.OkFore : sev == Severity.Warning ? UiKit.WarnFore : UiKit.BadFore;
            result.BackColor = sev == Severity.Ok ? UiKit.OkBack : sev == Severity.Warning ? UiKit.WarnBack : UiKit.BadBack;
            long files = state.Scan == null ? 0 : state.Scan.Files;
            long bytes = state.Scan == null ? 0 : state.Scan.Bytes;
            counts.Text = files.ToString("N0") + " archivos (" + ReportWriter.FormatBytes(bytes) + ") en el origen · "
                + (r == null ? 0 : r.ErrorCount) + " error(es) · Verificado: " + (state.Verify == null ? "todavía no" : state.Verify.Ok ? "sí, todo coincide" : "con diferencias");
            problems.Items.Clear();
            if (r != null) foreach (string l in r.ErrorLines) problems.Items.Add(l);
            if (state.Verify != null)
            {
                foreach (string f in state.Verify.Missing) problems.Items.Add("FALTA: " + f);
                foreach (string f in state.Verify.SizeMismatch) problems.Items.Add("TAMAÑO O FECHA DISTINTOS: " + f);
                foreach (string f in state.Verify.HashMismatch) problems.Items.Add("CONTENIDO DISTINTO: " + f);
                foreach (string f in state.Verify.Unreadable) problems.Items.Add("ILEGIBLE: " + f);
                foreach (string f in state.Verify.Skipped) problems.Items.Add("SIN VERIFICAR: " + f);
            }
            btnRetry.Enabled = r != null && !busy && (r.ErrorLines.Count > 0 || !r.Success);
            btnVerify.Enabled = CanVerify;
            if (state.Options != null && state.Options.Move)
                verifyStatus.Text = "Con «Mover» el origen ya no existe: no se puede verificar contra él. Comprueba el destino a mano; el informe dirá que la copia NO está verificada.";
            UiKit.FitAll(flow);
        }

        // ---- Verificación ----
        void StartVerify()
        {
            if (busy || !CanVerify) return;
            busy = true; workDone = false; isRetry = false; cancelVerify = false; failure = null; vResult = null;
            VerifyMode mode = rdoSha.Checked ? VerifyMode.Sha256 : VerifyMode.Quick;
            state.VerifyMode = mode;
            verifyStatus.Text = "Verificando…";
            btnVerify.Enabled = false;
            btnRetry.Enabled = false;
            // Se verifica TODO (también lo que robocopy marcó con error): si falta, saldrá como FALTA.
            string src = state.Options.Source, dst = state.Options.Destination;
            bool junc = state.Options.SkipJunctions;
            bool dates = state.Options.PreserveDates;
            Thread th = new Thread(delegate()
            {
                try
                {
                    vResult = Verifier.Verify(src, dst, mode, junc, null, delegate { return cancelVerify; },
                        delegate(long d, long t, string c) { lock (gate) { vDone = d; vTotal = t; vCurrent = c; } }, dates);
                }
                catch (Exception e) { failure = e; }
                finally { workDone = true; }
            });
            th.IsBackground = true;
            th.Start();
            timer.Start();
        }

        // Verificación síncrona (prueba de humo).
        public VerifyResult VerifySync(VerifyMode mode)
        {
            state.VerifyMode = mode;
            state.Verify = Verifier.Verify(state.Options.Source, state.Options.Destination, mode, state.Options.SkipJunctions, null, null, null, state.Options.PreserveDates);
            return state.Verify;
        }

        // Al cerrar el programa: una verificación se cancela sola; un reintento de copia pide confirmación.
        public bool ConfirmCloseWhileBusy(IWin32Window owner)
        {
            if (!busy) return true;
            if (!isRetry) { cancelVerify = true; return true; }
            DialogResult r = MessageBox.Show(owner, "Se está reintentando la copia. ¿Seguro que quieres salir? La copia se detendrá.",
                "Copia en curso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return false;
            if (retryRunner != null) retryRunner.Stop();
            return true;
        }

        // ---- Reintento ----
        void StartRetry()
        {
            if (busy) return;
            busy = true; workDone = false; isRetry = true; failure = null; retryResult = null;
            verifyStatus.Text = "Reintentando la copia (solo se copia lo que falta)…";
            btnVerify.Enabled = false;
            btnRetry.Enabled = false;
            CopyOptions o = state.Options;
            long total = state.Scan == null ? 0 : state.Scan.Bytes;
            retryRunner = new RobocopyRunner(null);
            RobocopyRunner rr = retryRunner;
            Thread th = new Thread(delegate()
            {
                try { retryResult = rr.Run(o, total); }
                catch (Exception e) { failure = e; }
                finally { workDone = true; }
            });
            th.IsBackground = true;
            th.Start();
            timer.Start();
        }

        void Tick()
        {
            if (!workDone)
            {
                if (!isRetry)
                {
                    long d, t; string c;
                    lock (gate) { d = vDone; t = vTotal; c = vCurrent; }
                    if (t > 0) { bar.Value = (int)Math.Min(1000, d * 1000 / t); verifyStatus.Text = "Verificando " + d + " de " + t + ": " + c; }
                }
                return;
            }
            timer.Stop();
            busy = false;
            if (failure != null)
            {
                verifyStatus.Text = "Error: " + failure.Message;
            }
            else if (isRetry)
            {
                state.Run = retryResult;
                state.Verify = null;
                verifyStatus.Text = retryResult.Describe();
            }
            else
            {
                state.Verify = vResult;
                bar.Value = 1000;
                verifyStatus.Text = vResult.Ok ? "Verificación correcta." : "La verificación encontró diferencias (ver la lista).";
            }
            Refresh_();
        }

        // ---- Informe ----
        public ReportData BuildReportData()
        {
            ReportData d = new ReportData();
            d.Options = state.Options;
            d.Computer = Environment.MachineName;
            d.Technician = txtTech.Text.Length > 0 ? txtTech.Text : (state.Settings.TechnicianName ?? "");
            d.CommandLine = "robocopy " + CommandBuilder.BuildArguments(state.Options);
            d.Start = state.Start;
            d.End = state.End;
            d.Files = state.Scan == null ? 0 : state.Scan.Files;
            d.Bytes = state.Scan == null ? 0 : state.Scan.Bytes;
            d.Run = state.Run;
            d.Verify = state.Verify;
            d.VerifyMode = state.VerifyMode;
            return d;
        }

        public void SaveReport(string path)
        {
            ReportWriter.Save(path, BuildReportData());
        }

        void SaveReportDialog()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Informe HTML (*.html)|*.html";
                dlg.FileName = "Informe_copia_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".html";
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    SaveReport(dlg.FileName);
                    MessageBox.Show(FindForm(), "Informe guardado en:\r\n" + dlg.FileName + "\r\n\r\nPuedes abrirlo en el navegador e imprimirlo o guardarlo como PDF.",
                        "Informe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e)
                {
                    MessageBox.Show(FindForm(), "No se pudo guardar el informe: " + e.Message, "Informe", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
