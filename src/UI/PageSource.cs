using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    // Fila "etiqueta + cuadro de ruta + Examinar… + ayuda".
    public sealed class PathRow : Panel, IFittable
    {
        public readonly TextBox Box = new TextBox();
        public readonly Button Browse = new Button();
        readonly Label caption;
        readonly WrapLabel hint;

        public PathRow(string captionText, string hintText)
        {
            caption = new Label();
            caption.Text = captionText;
            caption.Font = UiKit.Bold;
            caption.AutoSize = true;
            caption.Location = new Point(0, 0);
            Box.Font = UiKit.Normal;
            Browse.Text = "Examinar…";
            Browse.Font = UiKit.Normal;
            hint = UiKit.Text(hintText, UiKit.Small, UiKit.Muted);
            Controls.AddRange(new Control[] { caption, Box, Browse, hint });
            Margin = new Padding(0, 6, 0, 6);
        }

        public void FitTo(int width)
        {
            Width = width;
            Box.SetBounds(0, 24, width - 112, 26);
            Browse.SetBounds(width - 104, 23, 104, 28);
            hint.Location = new Point(0, 54);
            hint.FitTo(width);
            Height = 54 + hint.Height + 4;
        }
    }

    public sealed class ScanOutcome
    {
        public ScanResult Scan;
        public List<CheckResult> Checks;
    }

    public sealed class PageSource : UserControl, IWizardPage
    {
        readonly FlowLayoutPanel flow = UiKit.NewFlow();
        readonly PathRow rowSrc = new PathRow("Carpeta a copiar (origen)",
            "Por ejemplo la carpeta de usuario de un equipo, un disco entero o Documentos. El origen solo se lee: nunca se modifica.");
        readonly PathRow rowDst = new PathRow("Guardar en (destino)",
            "Se creará una carpeta nueva si no existe. Si ya existe, se añaden los archivos que falten y los que tengan el mismo nombre se sustituyen por los del origen. No se borra nada que no esté en el origen.");
        readonly FlowLayoutPanel checks = new FlowLayoutPanel();
        readonly WrapLabel status = UiKit.Text("", UiKit.Normal, UiKit.Muted);
        readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer();
        WizardState state;
        volatile int generation;
        bool scanning;
        bool ready;
        bool loading;

        public event EventHandler StateChanged;

        public PageSource()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            WrapLabel title = UiKit.Text("¿Qué quieres copiar y dónde?", UiKit.Title, UiKit.Ink);
            WrapLabel sub = UiKit.Text("Elige la carpeta de origen y el destino. El programa comprobará todo antes de seguir.", UiKit.Normal, UiKit.Muted);
            checks.FlowDirection = FlowDirection.TopDown;
            checks.WrapContents = false;
            checks.AutoSize = true;
            checks.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            checks.Margin = new Padding(0);
            flow.Controls.Add(title);
            flow.Controls.Add(sub);
            flow.Controls.Add(rowSrc);
            flow.Controls.Add(rowDst);
            flow.Controls.Add(UiKit.Section("Comprobaciones automáticas"));
            flow.Controls.Add(status);
            flow.Controls.Add(checks);
            Controls.Add(flow);

            debounce.Interval = 700;
            debounce.Tick += delegate { debounce.Stop(); StartScan(); };
            EventHandler changed = delegate
            {
                if (loading) return;
                ready = false;
                scanning = false;
                generation++;
                debounce.Stop();
                debounce.Start();
                Raise();
            };
            rowSrc.Box.TextChanged += changed;
            rowDst.Box.TextChanged += changed;
            rowSrc.Browse.Click += delegate { Browse(rowSrc.Box); };
            rowDst.Browse.Click += delegate { Browse(rowDst.Box); };
        }

        public string StepTitle { get { return "Origen y destino"; } }

        void Raise()
        {
            EventHandler h = StateChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        void Browse(TextBox box)
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.ShowNewFolderButton = true;
                if (box.Text.Length > 0 && Directory.Exists(box.Text)) d.SelectedPath = box.Text;
                if (d.ShowDialog(this) == DialogResult.OK) box.Text = d.SelectedPath;
            }
        }

        public void SetPaths(string source, string destination)
        {
            loading = true;
            rowSrc.Box.Text = source;
            rowDst.Box.Text = destination;
            loading = false;
            ready = false;
        }

        public bool CanAdvance(out string reason)
        {
            reason = null;
            if (scanning || !ready) { reason = "Espera a que termine el análisis."; return false; }
            foreach (CheckResult c in state.Checks)
                if (c.Severity == Severity.Error) { reason = "Corrige los avisos en rojo antes de seguir."; return false; }
            return true;
        }

        public void OnEnter(WizardState s)
        {
            state = s;
            if (rowSrc.Box.Text.Length == 0 && rowDst.Box.Text.Length == 0)
                SetPaths(s.Settings.LastSource ?? "", s.Settings.LastDestination ?? "");
            if (!ready && rowSrc.Box.Text.Trim().Length > 0 && rowDst.Box.Text.Trim().Length > 0) StartScan();
        }

        public void OnLeave(WizardState s)
        {
            s.SourcePath = rowSrc.Box.Text.Trim();
            s.DestPath = rowDst.Box.Text.Trim();
            s.Settings.LastSource = s.SourcePath;
            s.Settings.LastDestination = s.DestPath;
        }

        // Análisis síncrono (lo usa la prueba de humo).
        public void ValidateNow()
        {
            ScanOutcome o = Analyze(rowSrc.Box.Text.Trim(), rowDst.Box.Text.Trim(), null);
            Apply(o);
        }

        void StartScan()
        {
            int mine = ++generation;
            string src = rowSrc.Box.Text.Trim();
            string dst = rowDst.Box.Text.Trim();
            scanning = true;
            status.Text = "Analizando el origen…";
            Raise();
            ThreadPool.QueueUserWorkItem(delegate
            {
                ScanOutcome o = Analyze(src, dst, delegate { return mine != generation; });
                if (mine != generation) return;
                try
                {
                    BeginInvoke(new MethodInvoker(delegate { if (mine == generation) Apply(o); }));
                }
                catch (InvalidOperationException) { }
            });
        }

        void Apply(ScanOutcome o)
        {
            state.Scan = o.Scan;
            state.Checks = o.Checks;
            UiKit.ClearAndDispose(checks.Controls);
            foreach (CheckResult c in o.Checks) checks.Controls.Add(UiKit.Badge(c.Severity, c.Message));
            status.Text = o.Scan == null ? "" : "Origen: " + o.Scan.Files.ToString("N0") + " archivos · " + ReportWriter.FormatBytes(o.Scan.Bytes);
            scanning = false;
            ready = true;
            UiKit.FitAll(flow);
            FitChecks();
            Raise();
        }

        void FitChecks()
        {
            int w = flow.ClientSize.Width - flow.Padding.Horizontal;
            checks.Width = w;
            foreach (Control c in checks.Controls)
            {
                IFittable f = c as IFittable;
                if (f != null) f.FitTo(w - c.Margin.Horizontal);
            }
        }

        public static ScanOutcome Analyze(string src, string dst, Func<bool> cancel)
        {
            ScanOutcome o = new ScanOutcome();
            bool srcOk = false;
            try { srcOk = src.Length > 0 && Directory.Exists(src); } catch (Exception) { }
            long free = -1;
            if (srcOk)
            {
                o.Scan = TreeScanner.Scan(src, true, cancel, null);
                if (dst.Length > 0) free = PathValidator.GetFreeBytes(dst);
            }
            // El espacio necesario descuenta lo que ya hay en el destino (p. ej. una copia anterior cancelada).
            long needed = o.Scan == null ? -1 : PathValidator.BytesStillNeeded(dst, o.Scan.Bytes, cancel);
            o.Checks = PathValidator.Validate(src, dst, needed, free);
            if (o.Scan != null)
            {
                if (o.Scan.Files == 0)
                    o.Checks.Add(new CheckResult(Severity.Warning, "El origen está vacío: no hay nada que copiar."));
                if (o.Scan.Unreadable.Count > 0)
                    o.Checks.Add(new CheckResult(Severity.Warning, o.Scan.Unreadable.Count + " carpeta(s) o archivo(s) no se pueden leer; aparecerán como error en la copia."));
                try
                {
                    string root = Path.GetPathRoot(Path.GetFullPath(src));
                    if (!string.IsNullOrEmpty(root) && !root.StartsWith("\\\\") && new DriveInfo(root.Substring(0, 1)).DriveType == DriveType.Removable)
                        o.Checks.Add(new CheckResult(Severity.Warning, "El origen es una unidad extraíble. Si va lenta, prueba otro puerto USB (los puertos azules suelen ser USB 3.0)."));
                }
                catch (Exception) { }
            }
            return o;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            FitChecks();
        }
    }
}
