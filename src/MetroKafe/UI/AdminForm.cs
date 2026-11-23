using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>Admin paneli: oyun hazırlama, güncelleme, şifre değiştirme.</summary>
    public class AdminForm : Form
    {
        private readonly Panel _pages = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 16) };
        private readonly List<Button> _navButtons = new List<Button>();
        private bool _busy;

        public AdminForm()
        {
            Text = "Admin Paneli - " + AppInfo.VersionText;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 640);

            var nav = new Panel { Dock = DockStyle.Left, Width = 180, BackColor = Theme.Panel, Padding = new Padding(10, 16, 10, 10) };
            var head = Theme.Label("ADMİN", Theme.Bold, Theme.Accent);
            head.Location = new Point(14, 14);
            nav.Controls.Add(head);

            AddNav(nav, "Oyun Hazırla", () => new PreparePage(this));
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

            Shown += (s, e) => _navButtons[0].PerformClick();
        }

        public bool Busy
        {
            get => _busy;
            set { _busy = value; foreach (var b in _navButtons) b.Enabled = !value; }
        }

        private void AddNav(Panel nav, string text, Func<Control> page)
        {
            var b = Theme.Button(text);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.SetBounds(10, 48 + _navButtons.Count * 46, 160, 40);
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Theme.Panel;
            b.Click += (s, e) =>
            {
                foreach (var x in _navButtons) x.ForeColor = Theme.Text;
                b.ForeColor = Theme.Accent;
                foreach (Control c in _pages.Controls) c.Dispose();
                _pages.Controls.Clear();
                var p = page();
                p.Dock = DockStyle.Fill;
                _pages.Controls.Add(p);
            };
            _navButtons.Add(b);
            nav.Controls.Add(b);
        }
    }

    // ------------------------------------------------------------------ Oyun hazırla

    public class PreparePage : Panel
    {
        private readonly AdminForm _owner;
        private readonly TextBox _folder = Theme.TextBox();
        private readonly TextBox _name = Theme.TextBox();
        private readonly ComboBox _exe = new ComboBox();
        private readonly TextBox _args = Theme.TextBox();
        private readonly TextBox _icon = Theme.TextBox();
        private readonly TextBox _cover = Theme.TextBox();
        private readonly TextBox _docs = Theme.TextBox();
        private readonly TextBox _reg = Theme.TextBox();
        private readonly CheckBox _defender = Theme.CheckBox("Defender istisnası ekle", true);
        private readonly DataGridView _shortcuts = new DataGridView();
        private readonly CheckBox _doMd5 = Theme.CheckBox("MD5 listesi oluştur", true);
        private readonly CheckBox _doCleanup = Theme.CheckBox("Eski bat sistemi dosyalarını sil", false);
        private readonly CheckBox _doIso = Theme.CheckBox("ISO oluştur:", false);
        private readonly TextBox _isoPath = Theme.TextBox();
        private readonly Button _go = Theme.Button("Hazırla", true);
        private readonly FlatProgress _bar = new FlatProgress();
        private readonly Label _status = Theme.Label("", Theme.Small, Theme.Muted);
        private CancellationTokenSource _cts;

        public PreparePage(AdminForm owner)
        {
            _owner = owner;
            _exe.DropDownStyle = ComboBoxStyle.DropDown;
            _exe.FlatStyle = FlatStyle.Flat;
            _exe.BackColor = Theme.Panel;
            _exe.ForeColor = Theme.Text;
            _exe.Font = Theme.Base;

            var title = Theme.Label("Oyun Hazırla", Theme.Subtitle);
            Controls.Add(title);

            int y = 36;
            Row("Oyun klasörü", _folder, y, 500);
            var browse = Theme.Button("Seç");
            browse.SetBounds(640, y - 1, 64, 28);
            browse.Click += (s, e) => Browse();
            Controls.Add(browse);

            y += 36; Row("Oyun adı", _name, y, 300);
            Row("Exe", _exe, y, 202, 490, 440);
            y += 36; Row("Argüman", _args, y, 300);
            Row("İkon", _icon, y, 202, 490, 440);
            y += 36; Row("Belgelerim kaynağı", _docs, y, 300);
            Row("Kapak", _cover, y, 202, 490, 440);
            y += 36; Row("Reg dosyası", _reg, y, 300);
            _defender.Location = new Point(490, y + 2);
            Controls.Add(_defender);

            y += 40;
            var sl = Theme.Label("Ek masaüstü kısayolları (ana kısayol yukarıdaki Exe'dir)", Theme.Small, Theme.Muted);
            sl.Location = new Point(0, y);
            Controls.Add(sl);
            _shortcuts.SetBounds(0, y + 20, 692, 110);
            Theme.StyleGrid(_shortcuts);
            _shortcuts.Columns.Add("ad", "Kısayol adı");
            _shortcuts.Columns.Add("hedef", "Hedef (klasöre göre)");
            _shortcuts.Columns.Add("arg", "Argüman");
            _shortcuts.Columns.Add("ikon", "İkon");
            Controls.Add(_shortcuts);

            y += 142;
            _doMd5.Location = new Point(0, y);
            _doCleanup.Location = new Point(180, y);
            y += 28;
            _doIso.Location = new Point(0, y);
            _isoPath.SetBounds(112, y - 2, 580, 26);
            Controls.AddRange(new Control[] { _doMd5, _doCleanup, _doIso, _isoPath });

            y += 40;
            _bar.SetBounds(0, y, 692, 10);
            _status.AutoSize = false;
            _status.AutoEllipsis = true;
            _status.SetBounds(0, y + 14, 520, 40);
            _go.SetBounds(532, y + 18, 160, 40);
            _go.Click += async (s, e) => await Run();
            Controls.AddRange(new Control[] { _bar, _status, _go });

            _folder.Text = AppInfo.ExeFolder;
            LoadFolder();
            _folder.Leave += (s, e) => LoadFolder();
        }

        private void Row(string label, Control box, int y, int width, int x = 130, int labelX = 0)
        {
            var l = Theme.Label(label, Theme.Small, Theme.Muted);
            l.Location = new Point(labelX, y + 4);
            box.SetBounds(x, y, width, 26);
            Controls.Add(l);
            Controls.Add(box);
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Oyun klasörünü seçin", SelectedPath = _folder.Text })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _folder.Text = dlg.SelectedPath;
                LoadFolder();
            }
        }

        private string Folder => _folder.Text.Trim().TrimEnd('\\');

        private void LoadFolder()
        {
            if (!Directory.Exists(Folder)) return;
            GameConfig cfg;
            try
            {
                cfg = GameConfig.Exists(Folder) ? GameConfig.Load(Folder) : new GameConfig { Folder = Folder, Name = Path.GetFileName(Folder) };
            }
            catch (Exception ex)
            {
                _status.Text = "ini okunamadı: " + ex.Message;
                return;
            }

            // Exe listesi: kök ve bir alt klasördeki exe/bat dosyaları (MetroKafe dosyaları hariç)
            var candidates = Directory.EnumerateFiles(Folder, "*.exe", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Folder, "*.bat", SearchOption.TopDirectoryOnly))
                .Select(f => f.Substring(Folder.Length + 1))
                .Where(f => f.Count(c => c == '\\') <= 2 && !GameConfig.IsManagedFile(f)
                            && !f.ToLowerInvariant().Contains("redist") && !f.ToLowerInvariant().Contains("unins"))
                .OrderBy(f => f.Count(c => c == '\\')).ThenBy(f => f)
                .Take(60).ToList();
            _exe.Items.Clear();
            _exe.Items.AddRange(candidates.Cast<object>().ToArray());

            _name.Text = cfg.Name;
            _exe.Text = cfg.Exe.Length > 0 ? cfg.Exe : candidates.FirstOrDefault() ?? "";
            _args.Text = cfg.Arguments;
            _icon.Text = cfg.Icon;
            _cover.Text = cfg.Cover;
            _docs.Text = cfg.DocumentsSource;
            _reg.Text = cfg.RegFile;
            _defender.Checked = cfg.DefenderExclusion;
            _shortcuts.Rows.Clear();
            foreach (var s in cfg.ExtraShortcuts) _shortcuts.Rows.Add(s.Name, s.Target, s.Arguments, s.Icon);
            _isoPath.Text = Path.Combine(Path.GetDirectoryName(Folder) ?? Folder, cfg.FolderName + ".iso");
            _status.Text = GameConfig.Exists(Folder) ? "Mevcut " + GameConfig.FileName + " yüklendi." : "Yeni oyun: bilgileri kontrol edip Hazırla'ya basın.";
        }

        private GameConfig BuildConfig()
        {
            var cfg = new GameConfig
            {
                Folder = Folder,
                Name = _name.Text.Trim(),
                Exe = _exe.Text.Trim(),
                Arguments = _args.Text.Trim(),
                Icon = _icon.Text.Trim(),
                Cover = _cover.Text.Trim(),
                DocumentsSource = _docs.Text.Trim(),
                RegFile = _reg.Text.Trim(),
                DefenderExclusion = _defender.Checked,
                Installed = false,
            };
            foreach (DataGridViewRow r in _shortcuts.Rows)
            {
                if (r.IsNewRow) continue;
                var s = new ShortcutInfo
                {
                    Name = (r.Cells[0].Value as string ?? "").Trim(),
                    Target = (r.Cells[1].Value as string ?? "").Trim(),
                    Arguments = (r.Cells[2].Value as string ?? "").Trim(),
                    Icon = (r.Cells[3].Value as string ?? "").Trim(),
                };
                if (s.Name.Length > 0 && s.Target.Length > 0) cfg.ExtraShortcuts.Add(s);
            }
            return cfg;
        }

        private async Task Run()
        {
            if (_owner.Busy) { _cts?.Cancel(); return; }
            if (!Directory.Exists(Folder)) { _status.Text = "Klasör bulunamadı."; return; }
            var cfg = BuildConfig();
            if (cfg.Name.Length == 0) { _status.Text = "Oyun adı boş olamaz."; return; }
            if (cfg.Exe.Length == 0 || !File.Exists(cfg.ResolvePath(cfg.Exe)))
            {
                if (MessageBox.Show(FindForm(), $"Exe bulunamadı: {cfg.Exe}\nYine de devam edilsin mi?", "Uyarı",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            _owner.Busy = true;
            _go.Text = "Durdur";
            _bar.BarColor = Theme.Accent;
            _cts = new CancellationTokenSource();
            var progress = new Progress<ProgressInfo>(p => { _bar.Value = p.Percent; _status.Text = $"%{p.Percent:0}  {p.CurrentFile}"; });
            try
            {
                if (_doCleanup.Checked) CleanupLegacy();

                cfg.Save();
                var ownTarget = Path.Combine(Folder, GameConfig.ExeFileName);
                if (!AppInfo.SamePath(ownTarget, AppInfo.ExePath))
                {
                    if (File.Exists(ownTarget)) File.SetAttributes(ownTarget, FileAttributes.Normal);
                    File.Copy(AppInfo.ExePath, ownTarget, true);
                }

                if (_doMd5.Checked)
                {
                    _status.Text = "MD5 hesaplanıyor...";
                    var list = await Task.Run(() => HashList.Build(Folder, progress, _cts.Token));
                    HashList.Save(cfg.HashPath, list);
                }

                if (_doIso.Checked)
                {
                    var iso = _isoPath.Text.Trim();
                    if (iso.StartsWith(Folder + "\\", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("ISO dosyası oyun klasörünün içine kaydedilemez.");
                    _status.Text = "ISO hazırlanıyor...";
                    _bar.Value = 0;
                    await Task.Run(() => IsoBuilder.Build(Folder, iso, cfg.Name, progress, _cts.Token));
                }

                _bar.Value = 100;
                _bar.BarColor = Theme.Ok;
                _status.Text = "Hazır." + (_doIso.Checked ? " ISO: " + _isoPath.Text : "");
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Durduruldu.";
            }
            catch (Exception ex)
            {
                _bar.BarColor = Theme.Bad;
                _status.Text = "Hata: " + ex.Message;
            }
            finally
            {
                _owner.Busy = false;
                _go.Text = "Hazırla";
            }
        }

        /// <summary>Eski bat tabanlı sistemden kalan dosyaları siler (sadece oyun klasörünün kökünde).</summary>
        private void CleanupLegacy()
        {
            var names = new[]
            {
                "MetroKafeBat.bat", "MetroKafeBat.exe", "MetroKafeKisayol.bat", "MetroKafeKisayol.exe",
                "MetroKafeKurulum.bat", "MetroKafeKurulum.exe", "MetroKafeLauncher.bat", "MetroKafeLauncher.exe",
                "MetroKafeMD5.bat", "MetroKafeMD5Kontrol.bat", "MetroKafeMD5Kontrol.exe", "MetroKafeMD5Kontrol.css",
                "MetroKafeMD5Sonuc.html", "MetroKafeRAR.bat", "MetroKafeRAR.rar", "MetroKafeFC.exe", GameConfig.LogFileName,
            };
            foreach (var n in names)
            {
                var p = Path.Combine(Folder, n);
                if (!File.Exists(p)) continue;
                File.SetAttributes(p, FileAttributes.Normal);
                File.Delete(p);
            }
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
            _bar.SetBounds(0, 174, 692, 10);

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
            _notes.SetBounds(0, 196, 692, 300);

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
            var info = Theme.Label("Şifre bu bilgisayarda saklanır (%ProgramData%\\MetroKafe).", Theme.Small, Theme.Muted);
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
