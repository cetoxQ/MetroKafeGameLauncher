using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>
    /// Hazırlama aracı / admin paneli: oyun hazırlama, ayarlar, güncelleme, şifre.
    /// Program bir oyun klasöründe değilse doğrudan bu pencere açılır.
    /// </summary>
    public class AdminForm : Form
    {
        private readonly Panel _pages = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 16) };
        private readonly List<Button> _navButtons = new List<Button>();
        private readonly List<Control> _pageControls = new List<Control>();
        private readonly List<Func<Control>> _pageFactories = new List<Func<Control>>();
        private bool _busy;

        public PreparePage Prepare { get; }

        /// <param name="standalone">true: program kendi başına açıldı (ana pencere).</param>
        /// <param name="initialFile">Komut satırından veya exe üzerine bırakılarak gelen dosya.</param>
        public AdminForm(bool standalone = false, string initialFile = null)
        {
            Text = (standalone ? "Metro Kafe Oyun Hazırlama" : "Admin Paneli") + " - " + AppInfo.VersionText;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = standalone;
            StartPosition = standalone ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            ClientSize = new Size(1200, 760);
            try { Icon = Icon.ExtractAssociatedIcon(AppInfo.ExePath); } catch { }

            var nav = new Panel { Dock = DockStyle.Left, Width = 180, BackColor = Theme.Panel, Padding = new Padding(10, 16, 10, 10) };
            var head = Theme.Label(standalone ? "METRO KAFE" : "ADMİN", Theme.Bold, Theme.Accent);
            head.Location = new Point(14, 14);
            nav.Controls.Add(head);

            var version = Theme.Label(AppInfo.VersionText, Theme.Small, Theme.Muted);
            version.AutoSize = false;
            version.Dock = DockStyle.Bottom;
            version.Height = 24;
            version.Padding = new Padding(4, 0, 0, 0);
            nav.Controls.Add(version);

            Prepare = new PreparePage(this);
            AddNav(nav, "Oyun Hazırla", () => Prepare);
            AddNav(nav, "Ayarlar", () => new SettingsPage());
            AddNav(nav, "Güncelleme", () => new UpdatePage(this));
            AddNav(nav, "Şifre Değiştir", () => new PasswordPage());

            Controls.Add(_pages);
            Controls.Add(nav);

            FormClosing += (s, e) =>
            {
                if (_busy && MessageBox.Show(this, "İşlem devam ediyor. Yine de kapatılsın mı?", Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    e.Cancel = true;
            };

            Shown += async (s, e) =>
            {
                ShowPage(0);
                if (!string.IsNullOrEmpty(initialFile)) await Prepare.LoadPathAsync(initialFile);
            };
        }

        public bool Busy
        {
            get => _busy;
            set { _busy = value; foreach (var b in _navButtons) b.Enabled = !value; }
        }

        private void AddNav(Panel nav, string text, Func<Control> page)
        {
            var index = _navButtons.Count;
            var b = Theme.Button(text);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.SetBounds(10, 48 + index * 46, 160, 40);
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Theme.Panel;
            b.Click += (s, e) => ShowPage(index);
            _navButtons.Add(b);
            _pageFactories.Add(page);
            _pageControls.Add(null);
            nav.Controls.Add(b);
        }

        /// <summary>Sayfalar bir kez oluşturulur; geçişte içerik (bırakılan oyun vb.) kaybolmaz.</summary>
        private void ShowPage(int index)
        {
            for (int i = 0; i < _navButtons.Count; i++) _navButtons[i].ForeColor = i == index ? Theme.Accent : Theme.Text;
            if (_pageControls[index] == null)
            {
                var p = _pageFactories[index]();
                p.Dock = DockStyle.Fill;
                _pageControls[index] = p;
                _pages.Controls.Add(p);
            }
            foreach (var c in _pageControls.Where(c => c != null)) c.Visible = c == _pageControls[index];
        }
    }

    // ------------------------------------------------------------------ Güncelleme

    public class UpdatePage : Panel
    {
        private readonly AdminForm _owner;
        private readonly Label _status;
        private readonly TextBox _notes;
        private readonly Button _check;
        private readonly Button _apply;
        private readonly FlatProgress _bar;
        private UpdateInfo _info;

        public UpdatePage(AdminForm owner)
        {
            _owner = owner;
            var title = Theme.Label("Güncelleme", Theme.Subtitle);
            var current = Theme.Label($"Kurulu sürüm: {AppInfo.VersionText}\nKaynak: github.com/{AppInfo.GitHubRepo}", Theme.Base, Theme.Muted);
            current.Location = new Point(0, 40);

            _check = Theme.Button("Güncellemeleri Kontrol Et", true);
            _check.SetBounds(0, 92, 230, 40);
            _check.Click += async (s, e) => await Check();

            _apply = Theme.Button("Güncelle");
            _apply.SetBounds(240, 92, 150, 40);
            _apply.Visible = false;
            _apply.Click += async (s, e) => await Apply();

            _status = Theme.Label("", Theme.Bold);
            _status.Location = new Point(0, 146);

            _bar = new FlatProgress { Visible = false };
            _bar.SetBounds(0, 174, 700, 10);

            _notes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                Visible = false,
            };
            _notes.SetBounds(0, 196, 700, 300);

            var hint = Theme.Label("Not: Güncelleme sadece bu exe dosyasını değiştirir. Oyun klasörlerindeki eski\nMetroKafe.exe kopyaları, oyunu yeniden hazırlayınca güncellenir.", Theme.Small, Theme.Muted);
            hint.Location = new Point(0, 510);

            Controls.AddRange(new Control[] { title, current, _check, _apply, _status, _bar, _notes, hint });
        }

        private async Task Check()
        {
            _check.Enabled = false;
            _apply.Visible = false;
            _status.ForeColor = Theme.Text;
            _status.Text = "Kontrol ediliyor...";
            try
            {
                _info = await Updater.CheckAsync();
                if (_info.IsNewer)
                {
                    _status.Text = $"Yeni sürüm var: v{_info.Version.ToString(3)}";
                    _status.ForeColor = Theme.Accent;
                    _apply.Visible = true;
                }
                else
                {
                    _status.Text = "Program güncel.";
                    _status.ForeColor = Theme.Ok;
                }
                _notes.Text = (_info.Notes ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
                _notes.Visible = _notes.Text.Length > 0;
            }
            catch (Exception ex)
            {
                _status.Text = "Kontrol edilemedi: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
            finally { _check.Enabled = true; }
        }

        private async Task Apply()
        {
            _owner.Busy = true;
            _check.Enabled = _apply.Enabled = false;
            _bar.Visible = true;
            _status.Text = "İndiriliyor...";
            try
            {
                await Updater.ApplyAsync(_info, new Progress<int>(p => _bar.Value = p));
                _status.Text = "Güncellendi, program yeniden başlatılıyor...";
                _owner.Busy = false;
                Application.Exit();
            }
            catch (Exception ex)
            {
                _status.Text = "Güncellenemedi: " + ex.Message;
                _status.ForeColor = Theme.Bad;
                _owner.Busy = false;
                _check.Enabled = _apply.Enabled = true;
            }
        }
    }

    // ------------------------------------------------------------------ Şifre

    public class PasswordPage : Panel
    {
        public PasswordPage()
        {
            var title = Theme.Label("Şifre Değiştir", Theme.Subtitle);
            var info = Theme.Label("Şifre bu bilgisayarda saklanır (%AppData%\\MetroKafe).", Theme.Small, Theme.Muted);
            info.Location = new Point(0, 36);

            TextBox Field(string label, int y, bool secret)
            {
                var l = Theme.Label(label, Theme.Small, Theme.Muted);
                l.Location = new Point(0, y);
                var t = Theme.TextBox();
                t.SetBounds(0, y + 20, 300, 26);
                t.UseSystemPasswordChar = secret;
                Controls.Add(l);
                Controls.Add(t);
                return t;
            }

            var user = Field("Kullanıcı adı", 70, false);
            user.Text = AdminAuth.CurrentUser;
            var oldPass = Field("Mevcut şifre", 126, true);
            var pass = Field("Yeni şifre", 182, true);
            var pass2 = Field("Yeni şifre (tekrar)", 238, true);
            var status = Theme.Label("", Theme.Small);
            status.Location = new Point(0, 300);

            var save = Theme.Button("Kaydet", true);
            save.SetBounds(0, 326, 140, 40);
            save.Click += (s, e) =>
            {
                status.ForeColor = Theme.Bad;
                if (user.Text.Trim().Length == 0) { status.Text = "Kullanıcı adı boş olamaz."; return; }
                if (pass.Text.Length < 4) { status.Text = "Şifre en az 4 karakter olmalı."; return; }
                if (pass.Text != pass2.Text) { status.Text = "Yeni şifreler aynı değil."; return; }
                if (!AdminAuth.CheckPassword(oldPass.Text)) { status.Text = "Mevcut şifre hatalı."; return; }
                AdminAuth.Change(user.Text, pass.Text);
                status.ForeColor = Theme.Ok;
                status.Text = "Kaydedildi.";
                oldPass.Text = pass.Text = pass2.Text = "";
            };

            Controls.AddRange(new Control[] { title, info, status, save });
        }
    }
}
