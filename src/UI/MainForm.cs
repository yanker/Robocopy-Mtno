using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public sealed class MainForm : Form
    {
        const int StepSource = 0, StepOptions = 1, StepReview = 2, StepProgress = 3, StepVerify = 4;

        readonly WizardState state = new WizardState();
        readonly List<IWizardPage> pages = new List<IWizardPage>();
        readonly List<Button> stepButtons = new List<Button>();
        readonly Panel host = new Panel();
        readonly Button btnBack = UiKit.Btn("← Atrás", false);
        readonly Button btnNext = UiKit.Btn("Siguiente →", true);
        readonly Button btnAdmin = UiKit.Btn("Reiniciar como administrador", false);
        readonly Label lblHint = new Label();
        readonly PageSource pageSource = new PageSource();
        readonly PageProgress pageProgress = new PageProgress();
        readonly PageVerify pageVerify = new PageVerify();
        int current = -1;
        int maxReached;

        public MainForm()
        {
            Text = "Copia Segura";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            Font = UiKit.Normal;
            ClientSize = new Size(980, 760);
            MinimumSize = new Size(840, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;

            bool fallback;
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            state.DataDir = Settings.ResolveDataDir(exeDir, out fallback);
            state.Settings = Settings.Load(state.DataDir);
            state.Profile = state.Settings.LastProfile;

            pages.Add(pageSource);
            pages.Add(new PageOptions());
            pages.Add(new PageReview());
            pages.Add(pageProgress);
            pages.Add(pageVerify);

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 34;
            top.BackColor = UiKit.Dark;
            Label t = new Label();
            t.Text = "  Copia Segura  ·  para mantenimientos" + (fallback ? "   (datos guardados en %TEMP%: la carpeta del programa es de solo lectura)" : "");
            t.ForeColor = Color.White;
            t.Dock = DockStyle.Fill;
            t.TextAlign = ContentAlignment.MiddleLeft;
            top.Controls.Add(t);

            TableLayoutPanel steps = new TableLayoutPanel();
            steps.Dock = DockStyle.Top;
            steps.Height = 40;
            steps.ColumnCount = pages.Count;
            steps.BackColor = Color.FromArgb(0xF7, 0xF9, 0xFB);
            for (int i = 0; i < pages.Count; i++)
            {
                steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / pages.Count));
                Button b = new Button();
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.Dock = DockStyle.Fill;
                b.Text = (i + 1) + "  " + pages[i].StepTitle;
                b.Font = UiKit.Normal;
                int idx = i;
                b.Click += delegate { OnStepClick(idx); };
                stepButtons.Add(b);
                steps.Controls.Add(b, i, 0);
            }

            host.Dock = DockStyle.Fill;
            foreach (IWizardPage p in pages)
            {
                Control c = (Control)p;
                c.Visible = false;
                host.Controls.Add(c);
            }

            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 54;
            foot.BackColor = Color.FromArgb(0xF7, 0xF9, 0xFB);
            btnBack.Location = new Point(16, 11);
            btnAdmin.Location = new Point(130, 11);
            btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNext.Location = new Point(foot.Width - 170, 11);
            lblHint.AutoSize = false;
            lblHint.ForeColor = UiKit.BadFore;
            lblHint.TextAlign = ContentAlignment.MiddleRight;
            lblHint.Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left;
            lblHint.SetBounds(420, 11, foot.Width - 420 - 190, 32);
            foot.Controls.AddRange(new Control[] { btnBack, btnAdmin, lblHint, btnNext });
            foot.Resize += delegate
            {
                btnNext.Left = foot.Width - btnNext.Width - 16;
                lblHint.Width = Math.Max(50, btnNext.Left - lblHint.Left - 10);
            };

            Controls.Add(host);
            Controls.Add(foot);
            Controls.Add(steps);
            Controls.Add(top);

            btnAdmin.Visible = !IsAdmin();
            btnBack.Click += delegate { GoTo(current - 1); };
            btnNext.Click += delegate { OnNext(); };
            btnAdmin.Click += delegate { RestartAsAdmin(); };
            pageSource.StateChanged += delegate { UpdateButtons(); };
            pageProgress.Finished += delegate { GoTo(StepVerify); };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!pageProgress.ConfirmCloseWhileRunning(this)) { e.Cancel = true; return; }
                if (!pageVerify.ConfirmCloseWhileBusy(this)) e.Cancel = true;
            };

            GoTo(StepSource);
        }

        // Ganchos para la prueba de humo y las capturas de pantalla.
        public WizardState State { get { return state; } }
        public int CurrentStep { get { return current; } }
        public void ShowStep(int step) { GoTo(step); }
        public void DemoSetPaths(string source, string destination)
        {
            pageSource.SetPaths(source, destination);
            pageSource.ValidateNow();
        }

        static bool IsAdmin()
        {
            try
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception) { return false; }
        }

        void RestartAsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception) { /* el usuario canceló el aviso de permisos */ }
        }

        void OnStepClick(int idx)
        {
            if (current >= StepProgress) return;
            if (idx > StepReview) return;
            TryShowStep(idx);
        }

        // Ir hacia atrás siempre se puede. Ir hacia delante exige que cada paso intermedio sea válido
        // y recorre los pasos uno a uno (así cada paso prepara su estado).
        public bool TryShowStep(int step)
        {
            if (step < current) { GoTo(step); return true; }
            while (current < step)
            {
                string reason;
                if (!pages[current].CanAdvance(out reason))
                {
                    lblHint.Text = reason ?? "";
                    return false;
                }
                GoTo(current + 1);
            }
            return true;
        }

        void OnNext()
        {
            string reason;
            if (!pages[current].CanAdvance(out reason))
            {
                lblHint.Text = reason ?? "";
                return;
            }
            GoTo(current + 1);
        }

        void GoTo(int idx)
        {
            if (idx < 0 || idx >= pages.Count) return;
            if (current >= 0)
            {
                pages[current].OnLeave(state);
                ((Control)pages[current]).Visible = false;
            }
            current = idx;
            if (current > maxReached) maxReached = current;
            Control page = (Control)pages[current];
            page.Visible = true;
            page.BringToFront();
            pages[current].OnEnter(state);
            for (int i = 0; i < stepButtons.Count; i++)
            {
                stepButtons[i].ForeColor = i == current ? UiKit.Primary : UiKit.Muted;
                stepButtons[i].BackColor = i == current ? Color.White : Color.FromArgb(0xF7, 0xF9, 0xFB);
                stepButtons[i].Font = i == current ? UiKit.Bold : UiKit.Normal;
            }
            UpdateButtons();
        }

        void UpdateButtons()
        {
            lblHint.Text = "";
            btnBack.Enabled = current > 0 && current < StepProgress;
            btnBack.Visible = current < StepProgress;
            btnNext.Visible = current < StepProgress;
            // Reiniciar como administrador abriría otra instancia: solo antes de empezar la copia.
            btnAdmin.Visible = !IsAdmin() && current < StepProgress;
            btnNext.Text = current == StepReview ? "▶ Iniciar copia" : "Siguiente →";
            string reason;
            bool ok = current < StepProgress && pages[current].CanAdvance(out reason);
            btnNext.Enabled = ok;
            if (current == StepSource && !ok) lblHint.Text = "";
        }
    }
}
