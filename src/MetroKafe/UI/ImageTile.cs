using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MetroKafe.UI
{
    /// <summary>Seçilebilir görsel kutusu (kapak / ikon seçimi için).</summary>
    public class ImageTile : Control
    {
        private Image _image;
        private bool _selected;
        private bool _hover;

        /// <summary>Kutunun altında/üstünde gösterilen kısa etiket ("Mevcut" gibi).</summary>
        public string Caption = "";

        /// <summary>Görsel henüz yüklenmediyse gösterilen yazı.</summary>
        public string Placeholder = "...";

        public ImageTile()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 8, 8);
        }

        public Image Image
        {
            get => _image;
            set { _image?.Dispose(); _image = value; Invalidate(); }
        }

        public bool Selected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _image?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(_hover ? Theme.PanelHover : Theme.Panel);
            var inner = new Rectangle(4, 4, Width - 8, Height - 8);
            if (_image != null)
            {
                var scale = Math.Min(inner.Width / (double)_image.Width, inner.Height / (double)_image.Height);
                int w = (int)(_image.Width * scale), h = (int)(_image.Height * scale);
                g.DrawImage(_image, inner.X + (inner.Width - w) / 2, inner.Y + (inner.Height - h) / 2, w, h);
            }
            else
            {
                TextRenderer.DrawText(g, Placeholder, Theme.Small, inner, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            if (Caption.Length > 0)
            {
                var r = new Rectangle(0, Height - 18, Width, 18);
                using (var b = new SolidBrush(Color.FromArgb(200, 20, 20, 20))) g.FillRectangle(b, r);
                TextRenderer.DrawText(g, Caption, Theme.Small, r, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (_selected)
                using (var p = new Pen(Theme.Accent, 3)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        }
    }
}
