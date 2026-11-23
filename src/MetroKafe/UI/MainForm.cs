using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>
    /// Ana pencere: solda kapak resmi, sağda ini durumuna göre kurulum / doğrulama ekranı,
    /// sol altta sürüm numarası (3 kez tıklayınca admin girişi).
    /// </summary>
    public class MainForm : Form
    {
        private readonly Panel _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 16) };
        private readonly PictureBox _cover = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, BackColor = Theme.Panel };
        private readonly Label _version;
        private int _clicks;
        private int _lastClick;

        public bool Busy { get; set; }

        public MainForm()
        {
            Text = "Metro Internet Kafe - Oyun Kurulum";
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 580);
            try { Icon = Icon.ExtractAssociatedIcon(AppInfo.ExePath); } catch { }

            var side = new Panel { Dock = DockStyle.Left, Width = 300, BackColor = Theme.Panel, Padding = new Padding(0) };
            side.Controls.Add(_cover);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Back };
            _version = Theme.Label(AppInfo.VersionText, Theme.Small, Theme.Muted);
            _version.Location = new Point(10, 7);
            _version.Cursor = Cursors.Default;
            _version.MouseDown += VersionMouseDown;
            var brand = Theme.Label("Metro Internet Kafe", Theme.Small, Theme.Muted);
            brand.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            footer.Controls.Add(_version);
            footer.Controls.Add(brand);
            footer.Layout += (s, e) => brand.Location = new Point(footer.Width - brand.Width - 12, 7);

            Controls.Add(_content);
            Controls.Add(side);
            Controls.Add(footer);

            FormClosing += (s, e) =>
            {
                if (Busy && MessageBox.Show(this, "İşlem devam ediyor. Yine de kapatılsın mı?", Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    e.Cancel = true;
            };

            Reload();
        }

        /// <summary>ini dosyasını okuyup uygun ekranı gösterir.</summary>
        public void Reload()
        {
            var folder = AppInfo.ExeFolder;
            Control view;
            if (!GameConfig.Exists(folder))
            {
                SetCover(null);
                view = new MessageView("Oyun bilgisi bulunamadı",
                    $"Bu klasörde {GameConfig.FileName} dosyası yok.\n\nOyunu hazırlamak için sol alttaki sürüm numarasına 3 kez tıklayıp admin paneline girin.");
            }
            else
            {
                GameConfig cfg;
                try { cfg = GameConfig.Load(folder); }
                catch (Exception ex)
                {
                    view = new MessageView("Oyun bilgisi okunamadı", ex.Message);
                    ShowView(view);
                    return;
                }
                Text = cfg.Name + " - Metro Internet Kafe";
                SetCover(cfg.ResolvePath(cfg.Cover));
                view = cfg.Installed ? (Control)new VerifyView(this, cfg) : new InstallView(this, cfg);
            }
            ShowView(view);
        }

        private void ShowView(Control view)
        {
            foreach (Control c in _content.Controls) c.Dispose();
            _content.Controls.Clear();
            view.Dock = DockStyle.Fill;
            _content.Controls.Add(view);
        }

        private void SetCover(string path)
        {
            var old = _cover.Image;
            _cover.Image = null;
            old?.Dispose();
            if (path == null || !File.Exists(path)) return;
            try
            {
                // Dosyayı kilitlememek için belleğe al.
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var img = Image.FromStream(ms))
                    _cover.Image = new Bitmap(img);
            }
            catch { }
        }

        private void VersionMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var now = Environment.TickCount;
            _clicks = now - _lastClick < 600 ? _clicks + 1 : 1;
            _lastClick = now;
            if (_clicks < 3) return;
            _clicks = 0;
            if (Busy) return;
            using (var login = new LoginForm())
            {
                if (login.ShowDialog(this) != DialogResult.OK) return;
            }
            using (var admin = new AdminForm())
                admin.ShowDialog(this);
            Reload();
        }
    }

    /// <summary>Basit bilgi ekranı.</summary>
    public class MessageView : Panel
    {
        public MessageView(string title, string message)
        {
            var t = Theme.Label(title, Theme.Title);
            t.Location = new Point(0, 0);
            var m = Theme.Label(message, Theme.Base, Theme.Muted);
            m.AutoSize = false;
            m.Location = new Point(2, 50);
            m.Size = new Size(560, 300);
            Controls.Add(t);
            Controls.Add(m);
        }
    }
}
