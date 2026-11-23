using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>Kurulu oyun ekranı: başlat, MD5 doğrula, onar, kısayolları yenile, kaldır.</summary>
    public class VerifyView : Panel
    {
        private readonly MainForm _main;
        private readonly GameConfig _cfg;
        private readonly Label _status;
        private readonly DataGridView _grid;
        private readonly FlatProgress _bar;
        private readonly Label _progressText;
        private readonly Button _verify;
        private readonly Button _repair;
        private readonly Button _share;
        private readonly Button _shortcuts;
        private readonly Button _uninstall;
        private CancellationTokenSource _cts;
        private VerifyResult _last;

        public VerifyView(MainForm main, GameConfig cfg)
        {
            _main = main;
            _cfg = cfg;

            var title = Theme.Label(cfg.Name, Theme.Title);
            title.MaximumSize = new Size(580, 0);
            var info = Theme.Label($"Kurulum yeri: {cfg.Folder}\nKurulum tarihi: {cfg.InstallDate}", Theme.Small, Theme.Muted);
            info.Location = new Point(2, 44);

            var play = Theme.Button("Oyunu Başlat", true);
            play.SetBounds(0, 90, 180, 44);
            play.Click += (s, e) => Play();

            _verify = Theme.Button("Dosyaları Doğrula");
            _verify.SetBounds(190, 90, 150, 44);
            _verify.Click += async (s, e) => await RunVerify();

            _shortcuts = Theme.Button("Kısayolları Yenile");
            _shortcuts.SetBounds(350, 90, 140, 44);
            _shortcuts.Click += (s, e) => RecreateShortcuts();

            _uninstall = Theme.Button("Kaldır");
            _uninstall.SetBounds(500, 90, 84, 44);
            _uninstall.ForeColor = Theme.Bad;
            _uninstall.Click += (s, e) => Uninstall();

            _status = Theme.Label("Oyun dosyalarında sorun varsa \"Dosyaları Doğrula\" ile kontrol edin.", Theme.Base, Theme.Muted);
            _status.Location = new Point(2, 150);

            _bar = new FlatProgress { Location = new Point(0, 178), Size = new Size(584, 10), Visible = false };
            _progressText = Theme.Label("", Theme.Small, Theme.Muted);
            _progressText.AutoSize = false;
            _progressText.AutoEllipsis = true;
            _progressText.SetBounds(0, 192, 584, 18);

            _grid = new DataGridView
            {
                Location = new Point(0, 214),
                Size = new Size(584, 226),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Visible = false,
            };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("durum", "Durum");
            _grid.Columns.Add("dosya", "Dosya");
            _grid.Columns[0].FillWeight = 18;
            _grid.Columns[1].FillWeight = 82;

            _repair = Theme.Button("Onar", true);
            _repair.SetBounds(424, 448, 160, 40);
            _repair.Visible = false;
            _repair.Click += async (s, e) => await Repair();

            _share = Theme.Button("WhatsApp ile Gönder");
            _share.SetBounds(244, 448, 170, 40);
            _share.Visible = false;
            _share.Click += (s, e) => Share();

            Controls.AddRange(new Control[] { title, info, play, _verify, _shortcuts, _uninstall, _status, _bar, _progressText, _grid, _repair, _share });
        }

        private void Play()
        {
            try
            {
                SystemOps.LaunchGame(_cfg);
                FindForm().Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "Oyun başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetBusy(bool busy)
        {
            _main.Busy = busy;
            foreach (var b in new[] { _shortcuts, _uninstall, _repair, _share }) b.Enabled = !busy;
            _verify.Text = busy ? "Durdur" : "Dosyaları Doğrula";
        }

        private async Task RunVerify()
        {
            if (_main.Busy) { _cts?.Cancel(); return; }
            if (!File.Exists(_cfg.HashPath))
            {
                _status.Text = $"{GameConfig.HashFileName} bulunamadı, doğrulama yapılamıyor.";
                _status.ForeColor = Theme.Bad;
                return;
            }

            SetBusy(true);
            _grid.Visible = false;
            _repair.Visible = _share.Visible = false;
            _bar.Visible = true;
            _bar.BarColor = Theme.Accent;
            _status.ForeColor = Theme.Text;
            _status.Text = "Dosyalar kontrol ediliyor...";
            _cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            try
            {
                var progress = new Progress<ProgressInfo>(p =>
                {
                    _bar.Value = p.Percent;
                    var speed = p.DoneBytes / Math.Max(sw.Elapsed.TotalSeconds, 0.1) / (1 << 20);
                    _progressText.Text = $"%{p.Percent:0}  ·  {p.DoneFiles:N0}/{p.TotalFiles:N0}  ·  {speed:0} MB/sn  ·  {p.CurrentFile}";
                });
                _last = await Task.Run(() => Verifier.Verify(_cfg.Folder, progress, _cts.Token));
                ShowResult();
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Doğrulama durduruldu.";
            }
            catch (Exception ex)
            {
                _status.Text = "Doğrulama yapılamadı: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowResult()
        {
            var bad = _last.Items.Count(i => i.Status == VerifyStatus.Bad);
            var missing = _last.Items.Count(i => i.Status == VerifyStatus.Missing);
            _bar.Value = 100;
            _progressText.Text = "";
            new Log(_cfg.Folder).Info($"Doğrulama: {_last.Ok} doğru, {bad} hatalı, {missing} eksik");

            if (_last.AllOk)
            {
                _bar.BarColor = Theme.Ok;
                _status.Text = $"Tüm dosyalar sağlam ({_last.Ok:N0} dosya).";
                _status.ForeColor = Theme.Ok;
                return;
            }

            _bar.BarColor = Theme.Bad;
            _status.Text = $"Doğru: {_last.Ok:N0}   Hatalı: {bad:N0}   Eksik: {missing:N0}";
            _status.ForeColor = Theme.Bad;
            _grid.Rows.Clear();
            foreach (var item in _last.Problems)
            {
                var i = _grid.Rows.Add(item.Status == VerifyStatus.Bad ? "HATALI" : "EKSİK", item.RelativePath);
                _grid.Rows[i].Cells[0].Style.ForeColor = item.Status == VerifyStatus.Bad ? Theme.Bad : Theme.Warn;
            }
            _grid.ClearSelection();
            _grid.Visible = true;
            _repair.Visible = _share.Visible = true;
        }

        /// <summary>Hatalı/eksik dosyaları kaynaktan (ISO veya seçilen klasör) yeniden kopyalar.</summary>
        private async Task Repair()
        {
            var source = FindSource();
            if (source == null) return;

            var files = _last.Problems.Select(p => p.RelativePath).ToList();
            var log = new Log(_cfg.Folder);
            SetBusy(true);
            _status.Text = $"{files.Count} dosya {source} konumundan yeniden kopyalanıyor...";
            _status.ForeColor = Theme.Text;
            _bar.BarColor = Theme.Accent;
            _cts = new CancellationTokenSource();
            try
            {
                var progress = new Progress<ProgressInfo>(p => { _bar.Value = p.Percent; _progressText.Text = p.CurrentFile; });
                var expected = Installer.ExpectedHashes(_cfg.Folder);
                var result = await Task.Run(() => Installer.Copy(source, _cfg.Folder, files, expected, progress, log, _cts.Token));
                log.Info($"Onarım: {files.Count - result.Failed.Count} dosya düzeltildi, {result.Failed.Count} dosya düzeltilemedi.");
            }
            catch (OperationCanceledException) { }
            finally { SetBusy(false); }
            await RunVerify();
        }

        private string FindSource()
        {
            bool Usable(string dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, GameConfig.HashFileName))
                                       && !AppInfo.SamePath(dir, _cfg.Folder);

            if (Usable(_cfg.SourcePath)) return _cfg.SourcePath;

            // ISO başka harfle takılmış olabilir: CD/DVD sürücülerinde aynı oyunu ara.
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.CDRom && d.IsReady))
            {
                var root = d.RootDirectory.FullName;
                if (Usable(root) && GameConfig.Exists(root) && GameConfig.Load(root).Name == _cfg.Name) return root;
            }

            MessageBox.Show(FindForm(), "Oyunun ISO'su bulunamadı. ISO'yu takıp (çift tıklayıp) içindeki klasörü seçin.", "Kaynak gerekli",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            using (var dlg = new FolderBrowserDialog { Description = "Oyunun orijinal klasörünü (ISO'yu) seçin" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return null;
                if (Usable(dlg.SelectedPath)) return dlg.SelectedPath;
                MessageBox.Show(FindForm(), "Seçilen klasörde " + GameConfig.HashFileName + " yok.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void Share()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"*{_cfg.Name}* - dosya kontrolü");
            sb.AppendLine($"Hatalı: {_last.Items.Count(i => i.Status == VerifyStatus.Bad)}, Eksik: {_last.Items.Count(i => i.Status == VerifyStatus.Missing)}");
            foreach (var p in _last.Problems.Take(40))
                sb.AppendLine((p.Status == VerifyStatus.Bad ? "HATALI | " : "EKSİK | ") + p.RelativePath);
            if (_last.Problems.Count() > 40) sb.AppendLine("...");
            Process.Start("https://wa.me/?text=" + Uri.EscapeDataString(sb.ToString()));
        }

        private void RecreateShortcuts()
        {
            var lines = new StringBuilder();
            var log = new Log(_cfg.Folder);
            log.Line += l => lines.AppendLine(l.Substring(l.IndexOf(']') + 2));
            SystemOps.CreateShortcuts(_cfg, log);
            MessageBox.Show(FindForm(), lines.ToString(), "Kısayollar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Uninstall()
        {
            var folder = _cfg.Folder.TrimEnd('\\');
            if (Path.GetPathRoot(folder).TrimEnd('\\') == folder)
            {
                MessageBox.Show(FindForm(), "Oyun bir diskin kökünde görünüyor, güvenlik için kaldırma yapılmadı.", "Kaldır",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var r = MessageBox.Show(FindForm(),
                $"{_cfg.Name} kaldırılacak:\n\n• {folder} klasörü silinecek\n• Masaüstü kısayolları silinecek\n" +
                (_cfg.DefenderExclusion ? "• Defender istisnası kaldırılacak\n" : "") +
                "\nBelgelerim'deki kayıtlar silinmez. Program kapandıktan sonra klasör silinir.\nDevam edilsin mi?",
                "Oyunu kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            // Klasör, bu program kapandıktan birkaç saniye sonra silinir.
            Environment.CurrentDirectory = Path.GetTempPath();
            Installer.Uninstall(_cfg, new Log(null));
            _main.Busy = false;
            FindForm().Close();
        }
    }
}
