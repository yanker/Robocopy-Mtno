using System;
using System.Drawing;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public sealed class PageReview : UserControl, IWizardPage
    {
        readonly FlowLayoutPanel flow = UiKit.NewFlow();
        readonly WrapLabel summary = UiKit.Note("");
        readonly WrapLabel danger = UiKit.Badge(Severity.Error, "");
        readonly TextBox command = new TextBox();

        public PageReview()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            command.Multiline = true;
            command.ReadOnly = true;
            command.Font = UiKit.Mono;
            command.Height = 80;
            command.ScrollBars = ScrollBars.Vertical;
            flow.Controls.Add(UiKit.Text("Revisa antes de empezar", UiKit.Title, UiKit.Ink));
            flow.Controls.Add(UiKit.Text("Esto es lo que va a ocurrir, en cristiano.", UiKit.Normal, UiKit.Muted));
            flow.Controls.Add(summary);
            flow.Controls.Add(danger);
            flow.Controls.Add(UiKit.Section("Comando que se ejecutará (por si quieres verlo)"));
            flow.Controls.Add(command);
            Controls.Add(flow);
            danger.Visible = false;
        }

        public string StepTitle { get { return "Revisar"; } }

        public string CommandText { get { return command.Text; } }
        public string SummaryText { get { return summary.Text; } }

        public bool CanAdvance(out string reason)
        {
            reason = null;
            return true;
        }

        public void OnEnter(WizardState s)
        {
            CopyOptions o = s.Options;
            long files = s.Scan == null ? 0 : s.Scan.Files;
            long bytes = s.Scan == null ? 0 : s.Scan.Bytes;
            double minutes = bytes / (40.0 * 1024 * 1024) / 60.0;
            string eta = minutes < 1 ? "menos de 1 minuto" : "unos " + Math.Ceiling(minutes).ToString("0") + " minutos";
            string text = "Se copiarán " + files.ToString("N0") + " archivos (" + ReportWriter.FormatBytes(bytes) + ") desde " + o.Source + " hacia " + o.Destination + ".\r\n";
            text += o.Move ? "" : "✔ No se borrará ni modificará nada en el origen.\r\n";
            if (!o.Mirror)
            {
                text += "✔ No se borrarán archivos del destino que no estén en el origen.\r\n";
                text += "⚠ Si en el destino ya hay archivos con el mismo nombre, se sustituyen por los del origen.\r\n";
            }
            if (o.Restartable) text += "✔ Si se corta, se podrá reanudar.\r\n";
            text += "Tiempo estimado: " + eta + " (orientativo, a 40 MB/s).";
            summary.Text = text;

            if (o.Mirror || o.Move)
            {
                string w = "";
                if (o.Mirror) w += "ESPEJO activado: se BORRARÁ en el destino todo lo que no esté en el origen. ";
                if (o.Move) w += "MOVER activado: el origen se BORRARÁ después de copiar. ";
                danger.Text = "✖ " + w + "Si no es lo que quieres, vuelve atrás y desmárcalo.";
                danger.Visible = true;
            }
            else danger.Visible = false;

            command.Text = "robocopy " + CommandBuilder.BuildArguments(o);
            UiKit.FitAll(flow);
        }

        public void OnLeave(WizardState s)
        {
            s.Settings.LastProfile = s.Profile;
            try { s.Settings.Save(s.DataDir); } catch (Exception) { }
        }
    }
}
