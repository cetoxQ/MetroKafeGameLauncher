using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>
    /// Oyun hazırlama: exe sürükle-bırak → oyun klasörü/adı bulunur → SteamGridDB'den kapak ve ikon
    /// seçilir → ini, MD5 ve istenirse ISO oluşturulur.
    /// </summary>
    public class PreparePage : Panel
    {
        private const int ColW = 470;
        private const int RightX = 500;
        private const int RightW = 472;

        private readonly AdminForm _owner;
        private readonly Label _drop;
        private readonly TextBox _folder = Theme.TextBox();
        private readonly TextBox _name = Theme.TextBox();
        private readonly ComboBox _exe = new ComboBox();
        private readonly TextBox _args = Theme.TextBox();
        private readonly TextBox _icon = Theme.TextBox();
        private readonly TextBox _cover = Theme.TextBox();
        private readonly TextBox _docs = Theme.TextBox();
        private readonly TextBox _reg = Theme.TextBox();
        private readonly CheckBox _defender = Theme.CheckBox("Defender istisnası ekle", true);
        private readonly FlowLayoutPanel _extras = new FlowLayoutPanel();
        private readonly Label _extrasHint = Theme.Label("", Theme.Small, Theme.Muted);
        private readonly Button _addExe = Theme.Button("+", true);
        private readonly CheckBox _doMd5 = Theme.CheckBox("MD5 listesi oluştur", true);
        private readonly CheckBox _doCleanup = Theme.CheckBox("Eski bat sistemi dosyalarını sil", false);
        private readonly CheckBox _doIso = Theme.CheckBox("ISO oluştur:", false);
        private readonly TextBox _isoPath = Theme.TextBox();
        private readonly Button _go = Theme.Button("Hazırla", true);
        private readonly FlatProgress _bar = new FlatProgress();
        private readonly Label _status = Theme.Label("", Theme.Small, Theme.Muted);

        private readonly TextBox _search = Theme.TextBox();
        private readonly Button _searchBtn = Theme.Button("Ara");
        private readonly ComboBox _matches = new ComboBox();
        private readonly Label _sgStatus = Theme.Label("", Theme.Small, Theme.Muted);
        private readonly FlowLayoutPanel _covers = new FlowLayoutPanel();
        private readonly FlowLayoutPanel _icons = new FlowLayoutPanel();

        private CancellationTokenSource _cts;
        private int _imageLoadId;
        private bool _settingName;
        private bool _nameEdited;
        private SteamGrid _sg;

        public PreparePage(AdminForm owner)
        {
            _owner = owner;
            foreach (var cb in new[] { _exe, _matches })
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Theme.Panel;
                cb.ForeColor = Theme.Text;
                cb.Font = Theme.Base;
            }
            _exe.DropDownStyle = ComboBoxStyle.DropDown;
            _matches.DropDownStyle = ComboBoxStyle.DropDownList;

            // ---------------- sol sütun
            var title = Theme.Label("Oyun Hazırla", Theme.Subtitle);
            Controls.Add(title);

            _drop = new Label
            {
                Text = "Oyunun exe dosyasını buraya sürükleyin\nveya tıklayıp seçin",
                Font = Theme.Bold,
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.Panel,
                Cursor = Cursors.Hand,
                UseMnemonic = false,
            };
            _drop.SetBounds(0, 34, ColW, 70);
            _drop.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dash })
                    e.Graphics.DrawRectangle(p, 1, 1, _drop.Width - 3, _drop.Height - 3);
            };
            _drop.Click += async (s, e) => await BrowseExe();
            Controls.Add(_drop);

            int y = 116;
            Row("Oyun klasörü", _folder, y, ColW - 120 - 62);
            var browse = Theme.Button("Seç");
            browse.SetBounds(ColW - 56, y - 1, 56, 28);
            browse.Click += async (s, e) => await BrowseFolder();
            Controls.Add(browse);
            y += 34; Row("Oyun adı", _name, y);
            y += 34; Row("Exe", _exe, y, ColW - 120 - 40);
            _addExe.SetBounds(ColW - 34, y - 1, 34, 28);
            _addExe.Font = Theme.Subtitle;
            _addExe.Click += (s, e) => AddExtraExe();
            Controls.Add(_addExe);
            y += 34; Row("Argüman", _args, y);

            y += 34;
            var sl = Theme.Label("Ek exe'ler (her biri masaüstüne ayrı kısayol)", Theme.Small, Theme.Muted);
            sl.Location = new Point(0, y);
            Controls.Add(sl);
            _extras.SetBounds(0, y + 18, ColW, 160);
            _extras.FlowDirection = FlowDirection.TopDown;
            _extras.WrapContents = false;
            _extras.AutoScroll = true;
            _extras.BackColor = Theme.Back;
            _extrasHint.Text = "Ek exe yok. Exe'nin yanındaki + ile ekleyin\nveya ek exe'yi bu alana sürükleyin.";
            _extrasHint.Location = new Point(4, y + 24);
            Controls.Add(_extrasHint);
            Controls.Add(_extras);
            _extrasHint.BringToFront();

            y += 186; Row("İkon", _icon, y);
            y += 34; Row("Kapak", _cover, y);
            y += 34; Row("Belgelerim kaynağı", _docs, y);
            y += 34; Row("Reg dosyası", _reg, y);
            y += 32;
            _defender.Location = new Point(120, y);
            Controls.Add(_defender);

            y += 30;
            _doMd5.Location = new Point(0, y);
            _doCleanup.Location = new Point(180, y);
            y += 28;
            _doIso.Location = new Point(0, y);
            _isoPath.SetBounds(112, y - 2, ColW - 112, 26);
            Controls.AddRange(new Control[] { _doMd5, _doCleanup, _doIso, _isoPath });

            y += 34;
            _bar.SetBounds(0, y, ColW, 10);
            _status.AutoSize = false;
            _status.AutoEllipsis = true;
            _status.SetBounds(0, y + 16, ColW - 150, 44);
            _go.SetBounds(ColW - 140, y + 18, 140, 40);
            _go.Click += async (s, e) => await Run();
            Controls.AddRange(new Control[] { _bar, _status, _go });

            _name.TextChanged += (s, e) => { if (!_settingName) _nameEdited = true; };

            // ---------------- sağ sütun: SteamGridDB
            var sgTitle = Theme.Label("SteamGridDB kapak ve ikon", Theme.Subtitle);
            sgTitle.Location = new Point(RightX, 0);
            _search.SetBounds(RightX, 36, RightW - 80, 26);
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(_search.Text); } };
            _searchBtn.SetBounds(RightX + RightW - 72, 35, 72, 28);
            _searchBtn.Click += async (s, e) => await SearchAsync(_search.Text);
            _matches.SetBounds(RightX, 70, RightW, 26);
            _matches.SelectedIndexChanged += async (s, e) => await MatchSelected();
            _sgStatus.AutoSize = false;
            _sgStatus.SetBounds(RightX, 102, RightW, 18);

            var lc = Theme.Label("Kapak (2:3) - tıklayıp seçin", Theme.Small, Theme.Muted);
            lc.Location = new Point(RightX, 126);
            _covers.SetBounds(RightX, 146, RightW + 8, 290);
            _covers.AutoScroll = true;

            var li = Theme.Label("İkon - tıklayıp seçin", Theme.Small, Theme.Muted);
            li.Location = new Point(RightX, 446);
            _icons.SetBounds(RightX, 466, RightW + 8, 136);
            _icons.AutoScroll = true;

            Controls.AddRange(new Control[] { sgTitle, _search, _searchBtn, _matches, _sgStatus, lc, _covers, li, _icons });

            EnableDrop(this);

            if (GameConfig.Exists(AppInfo.ExeFolder))
                HandleCreated += async (s, e) => await LoadPathAsync(AppInfo.ExeFolder);
        }

        private void Row(string label, Control box, int y, int width = ColW - 120)
        {
            var l = Theme.Label(label, Theme.Small, Theme.Muted);
            l.Location = new Point(0, y + 4);
            box.SetBounds(120, y, width, 26);
            Controls.Add(l);
            Controls.Add(box);
        }

        private string Folder => _folder.Text.Trim().TrimEnd('\\');

        // ------------------------------------------------------------------ sürükle-bırak

        private void EnableDrop(Control c)
        {
            if (c == _extras || c is ExtraExeRow)
            {
                EnableExtraDrop(c);
                return;
            }
            c.AllowDrop = true;
            c.DragEnter += (s, e) =>
            {
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && !_owner.Busy ? DragDropEffects.Copy : DragDropEffects.None;
                if (e.Effect != DragDropEffects.None) _drop.BackColor = Theme.PanelHover;
            };
            c.DragLeave += (s, e) => _drop.BackColor = Theme.Panel;
            c.DragDrop += async (s, e) =>
            {
                _drop.BackColor = Theme.Panel;
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    await LoadPathAsync(files[0]);
            };
            foreach (Control child in c.Controls) EnableDrop(child);
        }

        /// <summary>Ek exe alanına bırakılan dosya oyunu değiştirmez, yeni ek exe satırı ekler.</summary>
        private void EnableExtraDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += (s, e) =>
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && !_owner.Busy && Folder.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            c.DragDrop += (s, e) =>
            {
                if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files)) return;
                foreach (var f in files.Where(File.Exists))
                {
                    var row = AddExtraRow(null);
                    if (!row.IsInGame(f))
                    {
                        RemoveExtraRow(row);
                        Status("Ek exe oyun klasörünün içinde olmalı: " + Path.GetFileName(f), Theme.Bad);
                        continue;
                    }
                    row.SetExe(f);
                }
            };
            foreach (Control child in c.Controls) EnableExtraDrop(child);
        }

        // ------------------------------------------------------------------ ek exe'ler

        private IEnumerable<ExtraExeRow> ExtraRows => _extras.Controls.OfType<ExtraExeRow>();

        private ExtraExeRow AddExtraRow(ShortcutInfo s)
        {
            var row = new ExtraExeRow(() => Folder, () => _name.Text.Trim(), _exe.Items.Cast<object>().ToArray());
            if (s != null) row.Fill(s);
            row.RemoveRequested += RemoveExtraRow;
            _extras.Controls.Add(row);
            EnableExtraDrop(row);
            _extrasHint.Visible = false;
            _extras.ScrollControlIntoView(row);
            return row;
        }

        private void RemoveExtraRow(ExtraExeRow row)
        {
            _extras.Controls.Remove(row);
            row.Dispose();
            _extrasHint.Visible = !ExtraRows.Any();
        }

        private void ClearExtraRows()
        {
            foreach (var r in ExtraRows.ToList()) RemoveExtraRow(r);
        }

        /// <summary>"+" butonu: yeni satır ekler ve exe seçtirir; vazgeçilirse satır kaldırılır.</summary>
        private void AddExtraExe()
        {
            if (_owner.Busy) return;
            if (Folder.Length == 0 || !Directory.Exists(Folder))
            {
                Status("Önce oyunun ana exe dosyasını sürükleyin.", Theme.Bad);
                return;
            }
            var row = AddExtraRow(null);
            if (!row.BrowseExe()) RemoveExtraRow(row);
            else row.NameBox.Focus();
        }

        /// <summary>Ek exe'lerin yeni seçilen ikonlarını ico2.ico, ico3.ico... olarak yazar.</summary>
        private void SaveExtraIcons(GameConfig cfg, List<ExtraExeRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].NewIcon == null) continue;
                var file = "ico" + (i + 2) + ".ico";
                ImageTools.SaveIco(rows[i].NewIcon, cfg.ResolvePath(file));
                rows[i].NewIcon = null;
                rows[i].IconPath = file;
                cfg.ExtraShortcuts[i].Icon = file;
            }
        }

        private async Task BrowseExe()
        {
            if (_owner.Busy) return;
            using (var dlg = new OpenFileDialog { Filter = "Oyun dosyası|*.exe;*.bat|Tüm dosyalar|*.*", Title = "Oyunun exe dosyasını seçin" })
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK) await LoadPathAsync(dlg.FileName);
            }
        }

        private async Task BrowseFolder()
        {
            if (_owner.Busy) return;
            using (var dlg = new FolderBrowserDialog { Description = "Oyun klasörünü seçin", SelectedPath = Folder })
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK) await LoadPathAsync(dlg.SelectedPath);
            }
        }

        // ------------------------------------------------------------------ oyunu yükle

        /// <summary>Exe veya klasör yolundan oyunu yükler.</summary>
        public async Task LoadPathAsync(string path)
        {
            if (_owner.Busy) return;
            var settings = AppSettings.Load();
            string folder, exe = null;
            if (Directory.Exists(path))
            {
                folder = Path.GetFullPath(path).TrimEnd('\\');
            }
            else if (File.Exists(path))
            {
                exe = Path.GetFullPath(path);
                folder = GameLocator.FindGameFolder(exe, settings);
            }
            else return;

            if (Path.GetPathRoot(folder).TrimEnd('\\') == folder)
            {
                _status.Text = "Oyun bir diskin kökünde olamaz, bir klasörün içinde olmalı.";
                _status.ForeColor = Theme.Bad;
                return;
            }

            GameConfig cfg;
            try
            {
                cfg = GameConfig.Exists(folder) ? GameConfig.Load(folder) : new GameConfig { Folder = folder, Name = GameLocator.CleanName(Path.GetFileName(folder)) };
            }
            catch (Exception ex)
            {
                _status.Text = "ini okunamadı: " + ex.Message;
                _status.ForeColor = Theme.Bad;
                return;
            }
            cfg.Folder = folder;
            var hadIni = GameConfig.Exists(folder);
            if (exe != null) cfg.Exe = GameLocator.Relative(folder, exe);

            FillExeList(folder);
            _folder.Text = folder;
            _settingName = true;
            _name.Text = cfg.Name;
            _settingName = false;
            _nameEdited = hadIni;
            _exe.Text = cfg.Exe.Length > 0 ? cfg.Exe : (_exe.Items.Count > 0 ? (string)_exe.Items[0] : "");
            _args.Text = cfg.Arguments;
            _icon.Text = cfg.Icon;
            _cover.Text = cfg.Cover;
            _docs.Text = cfg.DocumentsSource;
            _reg.Text = cfg.RegFile;
            _defender.Checked = cfg.DefenderExclusion;
            ClearExtraRows();
            foreach (var s in cfg.ExtraShortcuts) AddExtraRow(s);
            _isoPath.Text = Path.Combine(Path.GetDirectoryName(folder) ?? folder, cfg.FolderName + ".iso");
            _bar.Value = 0;
            _status.ForeColor = Theme.Muted;
            _status.Text = (hadIni ? "Mevcut " + GameConfig.FileName + " yüklendi. " : "Yeni oyun. ") + "Bilgileri kontrol edip Hazırla'ya basın.";
            _drop.Text = "Oyun: " + Path.GetFileName(folder) + "\nBaşka oyun için exe'yi buraya sürükleyin";

            ShowExisting(cfg);

            var exeForSearch = exe ?? cfg.ResolvePath(_exe.Text);
            await AutoSearchAsync(GameLocator.SearchTerms(folder, File.Exists(exeForSearch) ? exeForSearch : folder), settings);
        }

        private void FillExeList(string folder)
        {
            _exe.Items.Clear();
            try
            {
                var candidates = Directory.EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(folder, "*.bat", SearchOption.TopDirectoryOnly))
                    .Select(f => f.Substring(folder.Length + 1))
                    .Where(f => f.Count(c => c == '\\') <= 3 && !GameConfig.IsManagedFile(f)
                                && !f.ToLowerInvariant().Contains("redist") && !f.ToLowerInvariant().Contains("unins")
                                && !f.ToLowerInvariant().Contains("crashreport"))
                    .OrderBy(f => f.Count(c => c == '\\')).ThenBy(f => f)
                    .Take(60);
                _exe.Items.AddRange(candidates.Cast<object>().ToArray());
            }
            catch { }
        }

        // ------------------------------------------------------------------ kapak / ikon

        /// <summary>Klasörde zaten olan kapak ve ikonu "Mevcut" olarak ilk sıraya koyar.</summary>
        private void ShowExisting(GameConfig cfg)
        {
            ClearTiles(_covers);
            ClearTiles(_icons);
            var cover = cfg.ResolvePath(cfg.Cover);
            if (File.Exists(cover)) AddTile(_covers, "existing", LoadLocal(cover), "Mevcut", true);
            var icon = cfg.ResolvePath(cfg.Icon);
            if (File.Exists(icon)) AddTile(_icons, "existing", LoadLocal(icon), "Mevcut", true);
        }

        private static Image LoadLocal(string path)
        {
            try { return ImageTools.Load(File.ReadAllBytes(path)); }
            catch { return null; }
        }

        private static void ClearTiles(FlowLayoutPanel panel)
        {
            foreach (Control c in panel.Controls.Cast<Control>().ToList()) c.Dispose();
            panel.Controls.Clear();
        }

        private ImageTile AddTile(FlowLayoutPanel panel, object tag, Image img, string caption, bool selected)
        {
            var isCover = panel == _covers;
            var t = new ImageTile
            {
                Size = isCover ? new Size(86, 129) : new Size(50, 50),
                Image = img,
                Caption = isCover ? caption : "",
                Tag = tag,
                Selected = selected,
                Placeholder = isCover ? "yükleniyor" : "...",
            };
            if (isCover) t.Margin = new Padding(0, 0, 6, 8); // 5 kapak + dikey kaydırma çubuğu yan yana sığsın
            if (!isCover && caption.Length > 0) t.Size = new Size(50, 68);
            if (!isCover) t.Caption = caption;
            t.Click += (s, e) =>
            {
                foreach (var x in panel.Controls.OfType<ImageTile>()) x.Selected = x == t;
            };
            panel.Controls.Add(t);
            EnableDrop(t);
            return t;
        }

        private static ImageTile Selected(FlowLayoutPanel panel) => panel.Controls.OfType<ImageTile>().FirstOrDefault(t => t.Selected);

        private async Task AutoSearchAsync(List<string> terms, AppSettings settings)
        {
            _sg = new SteamGrid(settings.SteamGridKey);
            _matches.Items.Clear();
            if (string.IsNullOrWhiteSpace(settings.SteamGridKey))
            {
                SgStatus("SteamGridDB için Ayarlar'dan API anahtarı girin.", Theme.Warn);
                return;
            }
            foreach (var term in terms)
            {
                _search.Text = term;
                var found = await SearchAsync(term, quiet: true);
                if (found != false) return; // bulundu veya hata (hata mesajı ekranda kalsın)
            }
            _search.Text = terms.FirstOrDefault() ?? "";
            SgStatus("SteamGridDB'de bulunamadı. Arama kutusuna oyunun tam adını yazıp Ara'ya basın.", Theme.Warn);
        }

        /// <returns>true: bulundu, false: sonuç yok, null: hata.</returns>
        private async Task<bool?> SearchAsync(string term, bool quiet = false)
        {
            term = (term ?? "").Trim();
            if (term.Length == 0) return false;
            if (_sg == null) _sg = new SteamGrid(AppSettings.Load().SteamGridKey);
            SgStatus($"\"{term}\" aranıyor...", Theme.Muted);
            try
            {
                var games = await _sg.SearchAsync(term);
                if (games.Count == 0)
                {
                    if (!quiet) SgStatus($"\"{term}\" için sonuç yok.", Theme.Warn);
                    return false;
                }
                _matches.Items.Clear();
                _matches.Items.AddRange(games.Cast<object>().ToArray());
                _matches.SelectedIndex = 0;
                return true;
            }
            catch (Exception ex)
            {
                SgStatus(ex.Message, Theme.Bad);
                return null;
            }
        }

        private void SgStatus(string text, Color color)
        {
            _sgStatus.Text = text;
            _sgStatus.ForeColor = color;
        }

        private async Task MatchSelected()
        {
            if (!(_matches.SelectedItem is SgGame game)) return;
            var settings = AppSettings.Load();
            if (settings.UseSteamGridName && !_nameEdited && !string.IsNullOrEmpty(game.Name))
            {
                _settingName = true;
                _name.Text = game.Name;
                _settingName = false;
                _isoPath.Text = Path.Combine(Path.GetDirectoryName(Folder) ?? Folder, new GameConfig { Name = game.Name }.FolderName + ".iso");
            }

            var loadId = ++_imageLoadId;
            foreach (var panel in new[] { _covers, _icons })
                foreach (var t in panel.Controls.OfType<ImageTile>().Where(t => !(t.Tag is string)).ToList())
                {
                    panel.Controls.Remove(t);
                    t.Dispose();
                }
            SgStatus($"{game.Name}: görseller alınıyor...", Theme.Muted);
            try
            {
                var gridsTask = _sg.GridsAsync(game.Id, settings.GridCount);
                var iconsTask = _sg.IconsAsync(game.Id, settings.IconCount);
                var grids = await gridsTask;
                var icons = await iconsTask;
                if (loadId != _imageLoadId) return;

                bool hasCover = Selected(_covers) != null, hasIcon = Selected(_icons) != null;
                var tiles = new List<(ImageTile tile, string url)>();
                for (int i = 0; i < grids.Count; i++)
                    tiles.Add((AddTile(_covers, grids[i], null, "", !hasCover && i == 0), grids[i].Thumb ?? grids[i].Url));
                for (int i = 0; i < icons.Count; i++)
                    tiles.Add((AddTile(_icons, icons[i], null, "", !hasIcon && i == 0), icons[i].Url));

                SgStatus($"{game.Name}: {grids.Count} kapak, {icons.Count} ikon bulundu.", Theme.Ok);
                await Task.WhenAll(tiles.Select(async x =>
                {
                    try
                    {
                        var data = await _sg.DownloadAsync(x.url);
                        if (loadId == _imageLoadId && !x.tile.IsDisposed) x.tile.Image = ImageTools.Load(data);
                    }
                    catch
                    {
                        if (!x.tile.IsDisposed) x.tile.Placeholder = "hata";
                        x.tile.Invalidate();
                    }
                }));
            }
            catch (Exception ex)
            {
                if (loadId == _imageLoadId) SgStatus(ex.Message, Theme.Bad);
            }
        }

        /// <summary>Seçilen SteamGridDB kapak/ikonunu tam çözünürlükte indirip oyun klasörüne yazar.</summary>
        private async Task SaveSelectedImages(GameConfig cfg)
        {
            if (Selected(_covers)?.Tag is SgImage grid)
            {
                if (string.IsNullOrWhiteSpace(cfg.Cover) || !cfg.Cover.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    cfg.Cover = "MetroKafeCover.png";
                _status.Text = "Kapak indiriliyor...";
                ImageTools.SaveCoverPng(await _sg.DownloadAsync(grid.Url), cfg.ResolvePath(cfg.Cover));
                _cover.Text = cfg.Cover;
            }
            if (Selected(_icons)?.Tag is SgImage icon)
            {
                if (string.IsNullOrWhiteSpace(cfg.Icon) || !cfg.Icon.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    cfg.Icon = "ico.ico";
                _status.Text = "İkon indiriliyor...";
                ImageTools.SaveIco(await _sg.DownloadAsync(icon.Url), cfg.ResolvePath(cfg.Icon));
                _icon.Text = cfg.Icon;
            }
        }

        // ------------------------------------------------------------------ hazırla

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
            foreach (var row in ExtraRows)
            {
                var sc = row.ToShortcut();
                if (sc.Name.Length > 0 && sc.Target.Length > 0) cfg.ExtraShortcuts.Add(sc);
            }
            return cfg;
        }

        private async Task Run()
        {
            if (_owner.Busy) { _cts?.Cancel(); return; }
            if (Folder.Length == 0 || !Directory.Exists(Folder)) { Status("Önce oyunun exe dosyasını sürükleyin.", Theme.Bad); return; }
            var cfg = BuildConfig();
            if (cfg.Name.Length == 0) { Status("Oyun adı boş olamaz.", Theme.Bad); return; }
            var extraRows = ExtraRows.Where(r => { var sc = r.ToShortcut(); return sc.Name.Length > 0 && sc.Target.Length > 0; }).ToList();
            if (ExtraRows.Any(r => !r.IsEmpty && !extraRows.Contains(r))) { Status("Ek exe satırlarında ad veya exe eksik.", Theme.Bad); return; }
            var missing = new[] { cfg.Exe }.Concat(cfg.ExtraShortcuts.Select(x => x.Target))
                .Where(x => x.Length == 0 || !File.Exists(cfg.ResolvePath(x))).ToList();
            if (missing.Count > 0)
            {
                if (MessageBox.Show(FindForm(), "Bulunamayan exe:\n" + string.Join("\n", missing.Select(m => m.Length == 0 ? "(boş)" : m)) +
                        "\n\nYine de devam edilsin mi?", "Uyarı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            _owner.Busy = true;
            _go.Text = "Durdur";
            _bar.BarColor = Theme.Accent;
            _bar.Value = 0;
            _status.ForeColor = Theme.Muted;
            _cts = new CancellationTokenSource();
            var progress = new Progress<ProgressInfo>(p => { _bar.Value = p.Percent; _status.Text = $"%{p.Percent:0}  {p.CurrentFile}"; });
            try
            {
                if (_doCleanup.Checked) CleanupLegacy();
                await SaveSelectedImages(cfg);
                SaveExtraIcons(cfg, extraRows);

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
                Status("Hazır." + (_doIso.Checked ? " ISO: " + _isoPath.Text : ""), Theme.Ok);
            }
            catch (OperationCanceledException)
            {
                Status("Durduruldu.", Theme.Warn);
            }
            catch (Exception ex)
            {
                _bar.BarColor = Theme.Bad;
                Status("Hata: " + ex.Message, Theme.Bad);
            }
            finally
            {
                _owner.Busy = false;
                _go.Text = "Hazırla";
            }
        }

        private void Status(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
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
}
