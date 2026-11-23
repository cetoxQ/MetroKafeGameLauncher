using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MetroKafe.UI
{
    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(24, 25, 31);
        public static readonly Color Panel = Color.FromArgb(34, 36, 44);
        public static readonly Color PanelHover = Color.FromArgb(46, 49, 60);
        public static readonly Color Border = Color.FromArgb(58, 61, 74);
        public static readonly Color Text = Color.FromArgb(235, 236, 240);
        public static readonly Color Muted = Color.FromArgb(145, 149, 165);
        public static readonly Color Accent = Color.FromArgb(255, 140, 26);
        public static readonly Color AccentHover = Color.FromArgb(255, 163, 71);
        public static readonly Color Ok = Color.FromArgb(76, 195, 120);
        public static readonly Color Bad = Color.FromArgb(235, 87, 87);
        public static readonly Color Warn = Color.FromArgb(240, 190, 70);

        public static readonly Font Base = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 18f);
        public static readonly Font Subtitle = new Font("Segoe UI Semibold", 12f);
        public static readonly Font Mono = new Font("Consolas", 9f);

        public static Label Label(string text, Font font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Font = font ?? Base,
                ForeColor = color ?? Text,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
        }

        public static Button Button(string text, bool primary = false)
        {
            var b = new Button
            {
                Text = text,
                Font = primary ? Bold : Base,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Panel,
                ForeColor = primary ? Color.FromArgb(20, 20, 20) : Text,
                Height = 38,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.FlatAppearance.MouseOverBackColor = primary ? AccentHover : PanelHover;
            b.FlatAppearance.MouseDownBackColor = primary ? Accent : Border;
            b.UseMnemonic = false;
            if (primary)
            {
                b.EnabledChanged += (s, e) =>
                {
                    b.BackColor = b.Enabled ? Accent : Border;
                    b.FlatAppearance.BorderColor = b.Enabled ? Accent : Border;
                };
            }
            return b;
        }

        public static TextBox TextBox(string text = "")
        {
            return new TextBox
            {
                Text = text,
                Font = Base,
                BackColor = Panel,
                ForeColor = Text,
                BorderStyle = BorderStyle.FixedSingle,
            };
        }

        public static CheckBox CheckBox(string text, bool check)
        {
            return new CheckBox { Text = text, Checked = check, Font = Base, ForeColor = Text, AutoSize = true, BackColor = Color.Transparent };
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Panel;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Border;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Back;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            g.ColumnHeadersDefaultCellStyle.Font = Small;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Back;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Muted;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            g.DefaultCellStyle.BackColor = Panel;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = PanelHover;
            g.DefaultCellStyle.SelectionForeColor = Text;
            g.DefaultCellStyle.Font = Base;
            g.RowHeadersVisible = false;
            g.AllowUserToResizeRows = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
    }

    /// <summary>Koyu temaya uygun düz ilerleme çubuğu.</summary>
    public class FlatProgress : Control
    {
        private double _value;
        public Color BarColor = Theme.Accent;

        public FlatProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 10;
            BackColor = Theme.Panel;
        }

        /// <summary>0-100</summary>
        public double Value
        {
            get => _value;
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Back);
            using (var bg = new SolidBrush(BackColor)) FillRounded(e.Graphics, bg, new Rectangle(0, 0, Width - 1, Height - 1));
            var w = (int)((Width - 1) * _value / 100.0);
            if (w > 2)
                using (var fg = new SolidBrush(BarColor)) FillRounded(e.Graphics, fg, new Rectangle(0, 0, w, Height - 1));
        }

        private static void FillRounded(Graphics g, Brush b, Rectangle r)
        {
            int d = Math.Min(r.Height, r.Width);
            using (var path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 90, 180);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }
    }
}
