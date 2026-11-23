using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>Kurulum ekranı: disk seçimi, kopyalama, kurulum sonrası adımlar.</summary>
    public class InstallView : Panel
    {
        private readonly MainForm _main;
        private readonly GameConfig _cfg;
        private readonly Label _info;
        private readonly Label _hint;
        private readonly FlowLayoutPanel _disks;
        private readonly TextBox _log;
        private readonly Panel _progressPanel;
        private readonly Label _currentFile;
        private readonly Label _stats;
        private readonly FlatProgress _bar;
        private readonly Button _install;
        private readonly Button _cancel;
        private readonly Button _play;

        private long _size = -1;
        private int _fileCount;
        private DiskButton _selected;
        private CancellationTokenSource _cts;
        private string _target;

        public InstallView(MainForm main, GameConfig cfg)
        {
            _main = main;
            _cfg = cfg;

            var title = Theme.Label(cfg.Name, Theme.Title);
            title.Location = new Point(0, 0);
            title.MaximumSize = new Size(580, 0);

            _info = Theme.Label("Oyun boyutu hesaplanıyor...", Theme.Base, Theme.Muted);
            _info.Location = new Point(2, 44);

            _hint = Theme.Label("Kurulacak diski seçin  (klavyeden 1, 2, 3... ve Enter)", Theme.Bold);
            _hint.Location = new Point(2, 84);

            _disks = new FlowLayoutPanel
            {
                Location = new Point(0, 112),
                Size = new Size(584, 250),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };

            _log = new TextBox
            {
                Location = new Point(0, 112),
                Size = new Size(584, 250),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                Font = Theme.Mono,
                Visible = false,
            };

            _progressPanel = new Panel { Location = new Point(0, 372), Size = new Size(584, 64), Visible = false };
            _currentFile = Theme.Label("", Theme.Small, Theme.Muted);
            _currentFile.AutoSize = false;
            _currentFile.Size = new Size(584, 18);
            _currentFile.AutoEllipsis = true;
            _bar = new FlatProgress { Location = new Point(0, 22), Size = new Size(584, 12) };
            _stats = Theme.Label("", Theme.Small, Theme.Text);
            _stats.Location = new Point(0, 40);
            _progressPanel.Controls.AddRange(new Control[] { _currentFile, _bar, _stats });

            _install = Theme.Button("Kur", true);
            _install.Size = new Size(160, 40);
            _install.Location = new Point(424, 448);
            _install.Enabled = false;
            _install.Click += async (s, e) => await StartInstall();

            _cancel = Theme.Button("İptal");
            _cancel.Size = new Size(120, 40);
            _cancel.Location = new Point(296, 448);
            _cancel.Visible = false;
            _cancel.Click += (s, e) => _cts?.Cancel();

            _play = Theme.Button("Oyunu Başlat", true);
            _play.Size = new Size(160, 40);
            _play.Location = new Point(424, 448);
            _play.Visible = false;
            _play.Click += (s, e) => PlayAndClose();

            Controls.AddRange(new Control[] { title, _info, _hint, _disks, _log, _progressPanel, _install, _cancel, _play });

            HandleCreated += async (s, e) =>
            {
                var form = FindForm();
                form.KeyPreview = true;
                form.KeyDown += FormKeyDown;
                await LoadSize();
            };
        }

        protected override void Dispose(bool disposing)
        {
            var form = FindForm();
            if (form != null) form.KeyDown -= FormKeyDown;
            base.Dispose(disposing);
        }

        private async Task LoadSize()
        {
            try
            {
                var files = await Task.Run(() => HashList.ListFiles(_cfg.Folder, false));
                _fileCount = files.Count;
                _size = await Task.Run(() => files.Sum(f => new FileInfo(Path.Combine(_cfg.Folder, f)).Length));
                _info.Text = $"Boyut: {SystemOps.FormatSize(_size)}   ·   {_fileCount:N0} dosya";
            }
            catch (Exception ex)
            {
                _info.Text = "Boyut hesaplanamadı: " + ex.Message;
                _size = 0;
            }
            FillDisks();
        }

        private void FillDisks()
        {
            _disks.Controls.Clear();
            var sourceRoot = Path.GetPathRoot(_cfg.Folder).ToUpperInvariant();
            var sourceIsDisc = DriveInfo.GetDrives().Any(d => d.Name.ToUpperInvariant() == sourceRoot && d.DriveType == DriveType.CDRom);
            int i = 1;
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable)) continue;
                    // ISO'nun kendisi listelenmez; kaynak sabit diskteyse (test) aynı disk de seçilebilir.
                    if (sourceIsDisc && d.RootDirectory.FullName.ToUpperInvariant() == sourceRoot) continue;
                    var btn = new DiskButton(i, d, _size) { Width = 562 };
                    btn.Click += (s, e) => Select((DiskButton)s);
                    btn.DoubleClick += async (s, e) => { Select((DiskButton)s); await StartInstall(); };
                    _disks.Controls.Add(btn);
                    i++;
                }
                catch { /* hazır olmayan disk */ }
            }
            if (i == 1)
                _hint.Text = "Kurulabilecek disk bulunamadı.";
            else if (!_disks.Controls.OfType<DiskButton>().Any(b => b.Fits))
                _hint.Text = "Hiçbir diskte yeterli boş alan yok!";
        }

        private void Select(DiskButton b)
        {
            if (_main.Busy || !b.Fits) return;
            foreach (var d in _disks.Controls.OfType<DiskButton>()) d.Selected = d == b;
            _selected = b;
            _install.Enabled = true;
            _install.Text = $"{b.Drive.Name.TrimEnd('\\')} diskine kur";
        }

        private async void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (_main.Busy || !_disks.Visible) return;
            int n = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) n = e.KeyCode - Keys.D0;
            else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9) n = e.KeyCode - Keys.NumPad0;
            if (n > 0)
            {
                var b = _disks.Controls.OfType<DiskButton>().FirstOrDefault(x => x.Number == n);
                if (b != null) Select(b);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && _selected != null)
            {
                e.Handled = true;
                await StartInstall();
            }
        }

        private void AddLog(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AddLog), line); return; }
            _log.AppendText(line + Environment.NewLine);
        }

        private async Task StartInstall()
        {
            if (_main.Busy || _selected == null) return;
            _target = Installer.TargetFolder(_cfg, _selected.Drive);

            if (GameConfig.Exists(_target) || (Directory.Exists(_target) && Directory.EnumerateFileSystemEntries(_target).Any()))
            {
                var r = MessageBox.Show(FindForm(), $"Bu oyun {_target} klasöründe zaten var.\n\nÜzerine yazılsın mı? (Kayıtlar Belgelerim'de olduğu için genelde silinmez.)",
                    "Oyun zaten kurulu", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            _main.Busy = true;
            _disks.Visible = false;
            _log.Visible = true;
            _progressPanel.Visible = true;
            _install.Visible = false;
            _cancel.Visible = true;
            _hint.Text = "Kopyalanıyor... Bitene kadar bilgisayarı kapatmayın.";

            Directory.CreateDirectory(_target);
            var log = new Log(_target);
            log.Line += AddLog;
            log.Info($"Kurulum başladı: {_cfg.Folder} → {_target}");

            _cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            var progress = new Progress<ProgressInfo>(p => ShowProgress(p, sw));
            CopyResult result;
            try
            {
                var expected = Installer.ExpectedHashes(_cfg.Folder);
                if (expected == null) log.Info("MD5 listesi yok, kopyalama doğrulanmadan yapılacak.");
                result = await Task.Run(() => Installer.Copy(_cfg.Folder, _target, null, expected, progress, log, _cts.Token));
            }
            catch (OperationCanceledException)
            {
                log.Fail("Kurulum iptal edildi.");
                _main.Busy = false;
                _cancel.Visible = false;
                _hint.Text = "Kurulum iptal edildi.";
                if (MessageBox.Show(FindForm(), "Yarım kalan dosyalar silinsin mi?", "İptal", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try { Directory.Delete(_target, true); } catch (Exception ex) { AddLog("Silinemedi: " + ex.Message); }
                }
                return;
            }

            _cancel.Visible = false;
            log.Info($"Kopyalama bitti ({sw.Elapsed:hh\\:mm\\:ss}). Hatalı dosya: {result.Failed.Count}");

            if (result.Failed.Count > 0)
            {
                var r = MessageBox.Show(FindForm(),
                    $"{result.Failed.Count} dosya hatalı kopyalandı veya okunamadı.\nISO / disk bozuk olabilir.\n\nYine de kurulum tamamlansın mı?",
                    "Hatalı dosyalar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes)
                {
                    _main.Busy = false;
                    _hint.Text = "Kurulum yarıda kaldı.";
                    return;
                }
            }

            _hint.Text = "Ayarlar yapılıyor (kayıtlar, kısayollar, Defender)...";
            _currentFile.Text = "";
            try
            {
                var targetCfg = GameConfig.Load(_target);
                await Task.Run(() => Installer.PostInstall(targetCfg, _cfg.Folder, log));
            }
            catch (Exception ex)
            {
                log.Fail("Kurulum sonrası adımlar tamamlanamadı: " + ex.Message);
            }

            _main.Busy = false;
            _hint.Text = result.Failed.Count == 0 ? "Kurulum tamamlandı. İyi oyunlar!" : "Kurulum hatalarla tamamlandı.";
            _hint.ForeColor = result.Failed.Count == 0 ? Theme.Ok : Theme.Warn;
            _bar.BarColor = result.Failed.Count == 0 ? Theme.Ok : Theme.Warn;
            _bar.Invalidate();
            _play.Visible = true;
        }

        private void ShowProgress(ProgressInfo p, Stopwatch sw)
        {
            _bar.Value = p.Percent;
            _currentFile.Text = p.CurrentFile;
            var secs = Math.Max(sw.Elapsed.TotalSeconds, 0.1);
            var speed = p.DoneBytes / secs;
            var eta = speed > 0 ? TimeSpan.FromSeconds((p.TotalBytes - p.DoneBytes) / speed) : TimeSpan.Zero;
            _stats.Text = $"%{p.Percent:0}   ·   {SystemOps.FormatSize(p.DoneBytes)} / {SystemOps.FormatSize(p.TotalBytes)}   ·   " +
                          $"{speed / (1 << 20):0} MB/sn   ·   Kalan: {eta:hh\\:mm\\:ss}   ·   {p.DoneFiles:N0}/{p.TotalFiles:N0} dosya";
        }

        private void PlayAndClose()
        {
            try
            {
                SystemOps.LaunchGame(GameConfig.Load(_target));
                FindForm().Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "Oyun başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>Disk seçme kartı.</summary>
    public class DiskButton : Control
    {
        public readonly int Number;
        public readonly DriveInfo Drive;
        public readonly bool Fits;
        private bool _selected;
        private bool _hover;

        public DiskButton(int number, DriveInfo drive, long required)
        {
            Number = number;
            Drive = drive;
            Fits = drive.AvailableFreeSpace > required + (512L << 20);
            Height = 58;
            Margin = new Padding(0, 0, 0, 8);
            Cursor = Fits ? Cursors.Hand : Cursors.No;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public bool Selected { get => _selected; set { _selected = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Back);
            var bg = _selected ? Theme.PanelHover : (_hover && Fits ? Theme.PanelHover : Theme.Panel);
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, 0, 0, Width, Height);
            if (_selected) using (var p = new Pen(Theme.Accent, 2)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);

            var dim = Fits ? Theme.Text : Theme.Muted;
            using (var badge = new SolidBrush(Fits ? Theme.Accent : Theme.Border))
                g.FillEllipse(badge, 14, 15, 28, 28);
            TextRenderer.DrawText(g, Number.ToString(), Theme.Bold, new Rectangle(14, 15, 28, 28), Color.FromArgb(20, 20, 20),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var label = string.IsNullOrEmpty(Drive.VolumeLabel) ? "Yerel Disk" : Drive.VolumeLabel;
            TextRenderer.DrawText(g, $"{Drive.Name.TrimEnd('\\')}  {label}", Theme.Bold, new Point(56, 9), dim);

            var free = Drive.AvailableFreeSpace;
            var total = Math.Max(Drive.TotalSize, 1);
            var text = $"Boş: {SystemOps.FormatSize(free)} / {SystemOps.FormatSize(total)}" + (Fits ? "" : "   ·   Yetersiz alan");
            TextRenderer.DrawText(g, text, Theme.Small, new Point(56, 33), Fits ? Theme.Muted : Theme.Bad);

            var barRect = new Rectangle(Width - 180, 25, 160, 8);
            using (var b = new SolidBrush(Theme.Border)) g.FillRectangle(b, barRect);
            var used = (int)(barRect.Width * (1 - free / (double)total));
            using (var b = new SolidBrush(free < total / 10 ? Theme.Bad : Theme.Accent)) g.FillRectangle(b, barRect.X, barRect.Y, used, barRect.Height);
        }
    }
}
