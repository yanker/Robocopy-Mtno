using System;
using System.Drawing;
using System.Windows.Forms;
using CopiaSegura.Core;

namespace CopiaSegura.UI
{
    public interface IFittable
    {
        void FitTo(int width);
    }

    // Etiqueta con ajuste de línea y altura calculada (no depende de AutoSize).
    public class WrapLabel : Label, IFittable
    {
        public WrapLabel(string text, Font font, Color color)
        {
            AutoSize = false;
            UseMnemonic = false;
            Text = text;
            Font = font;
            ForeColor = color;
            Margin = new Padding(0, 2, 0, 2);
        }

        public void FitTo(int width)
        {
            Width = width;
            Size sz = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(20, width - Padding.Horizontal), 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            Height = sz.Height + Padding.Vertical + 4;
        }
    }

    public static class UiKit
    {
        public static readonly Color Primary = Color.FromArgb(0x1F, 0x6F, 0xEB);
        public static readonly Color Ink = Color.FromArgb(0x1D, 0x27, 0x33);
        public static readonly Color Muted = Color.FromArgb(0x5D, 0x6B, 0x7A);
        public static readonly Color Line = Color.FromArgb(0xD8, 0xDE, 0xE6);
        public static readonly Color Dark = Color.FromArgb(0x23, 0x30, 0x44);
        public static readonly Color OkFore = Color.FromArgb(0x1A, 0x7F, 0x37);
        public static readonly Color OkBack = Color.FromArgb(0xE6, 0xF4, 0xEA);
        public static readonly Color WarnFore = Color.FromArgb(0xB3, 0x59, 0x00);
        public static readonly Color WarnBack = Color.FromArgb(0xFF, 0xF4, 0xE0);
        public static readonly Color BadFore = Color.FromArgb(0xC6, 0x28, 0x28);
        public static readonly Color BadBack = Color.FromArgb(0xFD, 0xEA, 0xEA);
        public static readonly Color NoteBack = Color.FromArgb(0xEE, 0xF5, 0xFF);

        public static readonly Font Normal = new Font("Segoe UI", 9.5f);
        public static readonly Font Bold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Mono = new Font("Consolas", 9f);

        public static WrapLabel Text(string text, Font font, Color color)
        {
            return new WrapLabel(text, font, color);
        }

        public static WrapLabel Badge(Severity s, string message)
        {
            string prefix = s == Severity.Ok ? "✔ " : (s == Severity.Warning ? "⚠ " : "✖ ");
            WrapLabel l = new WrapLabel(prefix + message, Normal, Ink);
            l.Padding = new Padding(8, 4, 8, 4);
            l.Margin = new Padding(0, 3, 0, 3);
            if (s == Severity.Ok) { l.ForeColor = OkFore; l.BackColor = OkBack; }
            else if (s == Severity.Warning) { l.ForeColor = WarnFore; l.BackColor = WarnBack; }
            else { l.ForeColor = BadFore; l.BackColor = BadBack; }
            return l;
        }

        public static WrapLabel Note(string message)
        {
            WrapLabel l = new WrapLabel(message, Normal, Ink);
            l.Padding = new Padding(10, 6, 10, 6);
            l.BackColor = NoteBack;
            l.Margin = new Padding(0, 8, 0, 8);
            return l;
        }

        public static WrapLabel Section(string text)
        {
            WrapLabel l = new WrapLabel(text.ToUpperInvariant(), new Font("Segoe UI", 8.5f, FontStyle.Bold), Muted);
            l.Margin = new Padding(0, 14, 0, 4);
            return l;
        }

        public static Button Btn(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Font = Normal;
            b.AutoSize = true;
            b.Padding = new Padding(10, 3, 10, 3);
            b.MinimumSize = new Size(90, 32);
            if (primary)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Primary;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Primary;
            }
            return b;
        }

        public static FlowLayoutPanel NewFlow()
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.Dock = DockStyle.Fill;
            f.FlowDirection = FlowDirection.TopDown;
            f.WrapContents = false;
            f.AutoScroll = true;
            f.Padding = new Padding(24, 16, 24, 16);
            f.BackColor = Color.White;
            f.Resize += delegate { FitAll(f); };
            return f;
        }

        public static void FitAll(FlowLayoutPanel flow)
        {
            foreach (Control c in flow.Controls)
            {
                int w = flow.ClientSize.Width - flow.Padding.Horizontal - c.Margin.Horizontal;
                if (w < 200) w = 200;
                IFittable f = c as IFittable;
                if (f != null) f.FitTo(w); else c.Width = w;
            }
        }

        public static void ClearAndDispose(Control.ControlCollection controls)
        {
            while (controls.Count > 0)
            {
                Control c = controls[0];
                controls.RemoveAt(0);
                c.Dispose();
            }
        }
    }

    public static class Dialogs
    {
        // Pide una frase al usuario. Devuelve null si cancela.
        public static string Prompt(IWin32Window owner, string title, string message)
        {
            using (Form f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(460, 190);
                f.Font = UiKit.Normal;
                Label l = new Label();
                l.Text = message;
                l.SetBounds(16, 14, 428, 100);
                TextBox t = new TextBox();
                t.SetBounds(16, 120, 428, 24);
                Button ok = new Button();
                ok.Text = "Aceptar";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(270, 152, 80, 28);
                Button cancel = new Button();
                cancel.Text = "Cancelar";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(360, 152, 84, 28);
                f.Controls.AddRange(new Control[] { l, t, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text : null;
            }
        }
    }
}
