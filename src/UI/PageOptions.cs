using System;
using System.Drawing;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    // Una opción: control de entrada + título + explicación + etiqueta con el parámetro.
    public sealed class OptionRow : Panel, IFittable
    {
        readonly Control input;
        readonly WrapLabel title;
        readonly WrapLabel explanation;
        readonly Label tag = new Label();
        readonly bool inputOnRight;

        public OptionRow(OptionHelp help, Control input, bool inputOnRight, bool danger)
        {
            this.input = input;
            this.inputOnRight = inputOnRight;
            title = UiKit.Text(help.Title, UiKit.Bold, UiKit.Ink);
            explanation = UiKit.Text(help.Explanation, UiKit.Small, UiKit.Muted);
            tag.Text = help.Flag;
            tag.Font = UiKit.Mono;
            tag.ForeColor = UiKit.Muted;
            tag.AutoSize = true;
            tag.BackColor = Color.FromArgb(0xE8, 0xED, 0xF3);
            tag.Padding = new Padding(4, 1, 4, 1);
            tag.Visible = help.Flag.Length > 0;
            BackColor = danger ? UiKit.BadBack : Color.White;
            Margin = new Padding(0, 0, 0, 6);
            Controls.AddRange(new Control[] { input, title, explanation, tag });
        }

        public void FitTo(int width)
        {
            Width = width;
            int left = inputOnRight ? 12 : 40;
            int right = inputOnRight ? 190 : 12;
            if (!inputOnRight) input.Location = new Point(12, 12);
            tag.Location = new Point(width - tag.PreferredWidth - 12, 10);
            title.Location = new Point(left, 8);
            title.FitTo(Math.Max(100, width - left - tag.PreferredWidth - 24));
            explanation.Location = new Point(left, 8 + title.Height);
            explanation.FitTo(Math.Max(100, width - left - right));
            int h = 8 + title.Height + explanation.Height + 8;
            if (inputOnRight)
            {
                input.Width = 160;
                input.Location = new Point(width - 172, 32);
                int minHeight = 32 + input.Height + 10;
                if (h < minHeight) h = minHeight;
            }
            Height = h;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(UiKit.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    public sealed class ProfilePicker : Panel, IFittable
    {
        public readonly RadioButton Segura = new RadioButton();
        public readonly RadioButton Rapida = new RadioButton();
        readonly WrapLabel d1 = UiKit.Text("Para datos importantes o discos dudosos. Suave con el disco de origen y registra todo.", UiKit.Small, UiKit.Muted);
        readonly WrapLabel d2 = UiKit.Text("Disco sano y datos no críticos. Usa más hilos y no reintenta.", UiKit.Small, UiKit.Muted);

        public ProfilePicker()
        {
            Segura.Text = "Copia segura (recomendado)";
            Rapida.Text = "Copia rápida";
            Segura.Font = UiKit.Bold;
            Rapida.Font = UiKit.Bold;
            Segura.AutoSize = true;
            Rapida.AutoSize = true;
            Controls.AddRange(new Control[] { Segura, Rapida, d1, d2 });
            Margin = new Padding(0, 4, 0, 8);
        }

        public void FitTo(int width)
        {
            Width = width;
            int half = (width - 16) / 2;
            Segura.Location = new Point(8, 8);
            d1.Location = new Point(26, 32);
            d1.FitTo(half - 28);
            Rapida.Location = new Point(half + 24, 8);
            d2.Location = new Point(half + 42, 32);
            d2.FitTo(half - 28);
            Height = 32 + Math.Max(d1.Height, d2.Height) + 10;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int half = (Width - 16) / 2;
            using (Pen p = new Pen(Segura.Checked ? UiKit.Primary : UiKit.Line, 2))
                e.Graphics.DrawRectangle(p, 1, 1, half + 6, Height - 3);
            using (Pen p = new Pen(Rapida.Checked ? UiKit.Primary : UiKit.Line, 2))
                e.Graphics.DrawRectangle(p, half + 16, 1, Width - (half + 16) - 3, Height - 3);
        }
    }

    public sealed class PageOptions : UserControl, IWizardPage
    {
        static readonly int[] RetryValues = { 0, 1, 3, 5 };
        static readonly int[] WaitValues = { 0, 2, 5 };
        static readonly int[] ThreadValues = { 1, 2, 4, 8 };

        readonly FlowLayoutPanel flow = UiKit.NewFlow();
        readonly ProfilePicker picker = new ProfilePicker();
        readonly CheckBox chkSub = new CheckBox();
        readonly CheckBox chkDates = new CheckBox();
        readonly CheckBox chkJunc = new CheckBox();
        readonly CheckBox chkRestart = new CheckBox();
        readonly CheckBox chkLog = new CheckBox();
        readonly CheckBox chkDoubtful = new CheckBox();
        readonly ComboBox cmbRetries = new ComboBox();
        readonly ComboBox cmbWait = new ComboBox();
        readonly ComboBox cmbThreads = new ComboBox();
        readonly CheckBox chkMirror = new CheckBox();
        readonly CheckBox chkMove = new CheckBox();
        readonly Button btnUnlock = UiKit.Btn("Desbloquear opciones peligrosas…", false);
        readonly WrapLabel doubtfulNote = UiKit.Note("Disco dudoso activado: se usa 1 hilo y 1 reintento. Si hay ruidos, desconexiones o errores de lectura, quizá convenga clonar el disco antes de copiar archivos.");
        WizardState state;
        bool loading;

        public PageOptions()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            flow.Controls.Add(UiKit.Text("Elige cómo copiar", UiKit.Title, UiKit.Ink));
            flow.Controls.Add(UiKit.Text("Si dudas, deja “Copia segura”. Cada opción se explica; nada peligroso viene activado.", UiKit.Normal, UiKit.Muted));
            flow.Controls.Add(picker);
            flow.Controls.Add(UiKit.Section("Qué se copia"));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("subfolders"), chkSub, false, false));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("dates"), chkDates, false, false));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("junctions"), chkJunc, false, false));
            flow.Controls.Add(UiKit.Section("Seguridad y fiabilidad"));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("restartable"), chkRestart, false, false));
            Fill(cmbRetries, RetryValues, " reintento(s)");
            Fill(cmbWait, WaitValues, " s de espera");
            Fill(cmbThreads, ThreadValues, " hilo(s)");
            flow.Controls.Add(new OptionRow(HelpTexts.Get("retries"), Combine(cmbRetries, cmbWait), true, false));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("threads"), cmbThreads, true, false));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("log"), chkLog, false, false));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("doubtful"), chkDoubtful, false, false));
            flow.Controls.Add(doubtfulNote);
            flow.Controls.Add(UiKit.Section("Opciones peligrosas (bloqueadas)"));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("mirror"), chkMirror, false, true));
            flow.Controls.Add(new OptionRow(HelpTexts.Get("move"), chkMove, false, true));
            flow.Controls.Add(btnUnlock);
            Controls.Add(flow);

            doubtfulNote.Visible = false;
            foreach (CheckBox c in new CheckBox[] { chkSub, chkDates, chkJunc, chkRestart, chkLog, chkDoubtful, chkMirror, chkMove })
            {
                c.AutoSize = false;
                c.Size = new Size(20, 20);
            }
            picker.Segura.CheckedChanged += delegate { if (picker.Segura.Checked) OnProfileChanged(Profile.Segura); picker.Invalidate(); };
            picker.Rapida.CheckedChanged += delegate { if (picker.Rapida.Checked) OnProfileChanged(Profile.Rapida); picker.Invalidate(); };
            chkDoubtful.CheckedChanged += delegate { OnDoubtfulChanged(); };
            btnUnlock.Click += delegate { Unlock(); };
        }

        static void Fill(ComboBox c, int[] values, string suffix)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Font = UiKit.Normal;
            foreach (int v in values) c.Items.Add(v + suffix);
        }

        static Control Combine(ComboBox a, ComboBox b)
        {
            Panel p = new Panel();
            p.Height = 56;
            a.SetBounds(0, 0, 160, 24);
            b.SetBounds(0, 28, 160, 24);
            p.Controls.Add(a);
            p.Controls.Add(b);
            return p;
        }

        public string StepTitle { get { return "Opciones"; } }

        public bool CanAdvance(out string reason)
        {
            reason = null;
            return true;
        }

        public void OnEnter(WizardState s)
        {
            state = s;
            bool pathsChanged = s.Options == null || s.Options.Source != s.SourcePath || s.Options.Destination != s.DestPath;
            if (pathsChanged) s.Options = CopyOptions.ForProfile(s.Profile, s.SourcePath, s.DestPath);
            LoadFromOptions();
        }

        public void OnLeave(WizardState s)
        {
            if (state == null) return;
            ReadToOptions();
        }

        void LoadFromOptions()
        {
            CopyOptions o = state.Options;
            loading = true;
            picker.Segura.Checked = state.Profile == Profile.Segura;
            picker.Rapida.Checked = state.Profile == Profile.Rapida;
            chkSub.Checked = o.IncludeSubfolders;
            chkDates.Checked = o.PreserveDates;
            chkJunc.Checked = o.SkipJunctions;
            chkRestart.Checked = o.Restartable;
            chkLog.Checked = o.WriteLog;
            chkDoubtful.Checked = o.DoubtfulDisk;
            Select(cmbRetries, RetryValues, o.Retries);
            Select(cmbWait, WaitValues, o.WaitSeconds);
            Select(cmbThreads, ThreadValues, o.Threads);
            chkMirror.Enabled = o.DangerousUnlocked;
            chkMove.Enabled = o.DangerousUnlocked;
            chkMirror.Checked = o.Mirror;
            chkMove.Checked = o.Move;
            btnUnlock.Visible = !o.DangerousUnlocked;
            doubtfulNote.Visible = o.DoubtfulDisk;
            loading = false;
            UiKit.FitAll(flow);
        }

        static void Select(ComboBox c, int[] values, int v)
        {
            int idx = Array.IndexOf(values, v);
            c.SelectedIndex = idx >= 0 ? idx : 0;
        }

        void ReadToOptions()
        {
            CopyOptions o = state.Options;
            o.IncludeSubfolders = chkSub.Checked;
            o.PreserveDates = chkDates.Checked;
            o.SkipJunctions = chkJunc.Checked;
            o.Restartable = chkRestart.Checked;
            o.WriteLog = chkLog.Checked;
            o.DoubtfulDisk = chkDoubtful.Checked;
            o.Retries = RetryValues[cmbRetries.SelectedIndex];
            o.WaitSeconds = WaitValues[cmbWait.SelectedIndex];
            o.Threads = ThreadValues[cmbThreads.SelectedIndex];
            if (o.DangerousUnlocked)
            {
                try
                {
                    o.SetMirror(chkMirror.Checked);
                    o.SetMove(chkMove.Checked);
                }
                catch (InvalidOperationException ex)
                {
                    o.SetMirror(false);
                    o.SetMove(false);
                    chkMirror.Checked = false;
                    chkMove.Checked = false;
                    MessageBox.Show(FindForm(), ex.Message, "Opción no permitida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void OnProfileChanged(Profile p)
        {
            if (loading || state == null) return;
            state.Profile = p;
            Rebuild(chkDoubtful.Checked);
        }

        void OnDoubtfulChanged()
        {
            if (loading || state == null) return;
            Rebuild(chkDoubtful.Checked);
        }

        // Vuelve a los valores del perfil (y aplica "disco dudoso" si está marcado).
        void Rebuild(bool doubtful)
        {
            CopyOptions o = CopyOptions.ForProfile(state.Profile, state.SourcePath, state.DestPath);
            if (doubtful) o.ApplyDoubtfulDisk();
            state.Options = o;
            LoadFromOptions();
        }

        void Unlock()
        {
            if (PathValidator.IsInside(state.Options.Source, state.Options.Destination))
            {
                MessageBox.Show(FindForm(), "Espejo y Mover no se pueden usar cuando el origen está dentro del destino: se borraría el propio origen.",
                    "Opción no permitida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string phrase = Dialogs.Prompt(FindForm(), "Opciones peligrosas",
                "Estas opciones pueden BORRAR archivos del destino (espejo) o del origen (mover).\r\n\r\nSi sabes lo que haces, escribe BORRAR (en mayúsculas) para desbloquearlas.");
            if (phrase == null) return;
            if (state.Options.TryUnlockDangerous(phrase))
            {
                LoadFromOptions();
                MessageBox.Show(FindForm(), "Opciones desbloqueadas. Revisa el paso siguiente con mucho cuidado: se indicará si algo puede borrarse.",
                    "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(FindForm(), "No coincide. Las opciones siguen bloqueadas.", "Copia Segura", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
