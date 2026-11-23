using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>SteamGridDB'de ad arayıp ikon seçtiren pencere. Sonuç: indirilmiş ikon verisi.</summary>
    public class IconPickerForm : Form
    {
        private readonly SteamGrid _sg;
        private readonly int _count;
        private readonly TextBox _search = Theme.TextBox();
        private readonly ComboBox _matches = new ComboBox();
        private readonly Label _status = Theme.Label("", Theme.Small, Theme.Muted);
        private readonly FlowLayoutPanel _tiles = new FlowLayoutPanel { AutoScroll = true };
        private readonly Button _ok = Theme.Button("Seç", true);
        private int _loadId;

        public byte[] Result { get; private set; }

        public IconPickerForm(string term)
        {
            var settings = AppSettings.Load();
            _sg = new SteamGrid(settings.SteamGridKey);
            _count = Math.Max(settings.IconCount, 20);

            Text = "SteamGridDB ikon ara";
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, 470);

            _search.SetBounds(16, 16, 440, 26);
            _search.Text = term ?? "";
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Search(); } };
            var find = Theme.Button("Ara");
            find.SetBounds(464, 15, 100, 28);
            find.Click += async (s, e) => await Search();

            _matches.DropDownStyle = ComboBoxStyle.DropDownList;
            _matches.FlatStyle = FlatStyle.Flat;
            _matches.BackColor = Theme.Panel;
            _matches.ForeColor = Theme.Text;
            _matches.Font = Theme.Base;
            _matches.SetBounds(16, 52, 548, 26);
            _matches.SelectedIndexChanged += async (s, e) => await LoadIcons();

            _status.AutoSize = false;
            _status.SetBounds(16, 84, 548, 18);
            _tiles.SetBounds(16, 106, 556, 300);

            _ok.SetBounds(444, 418, 120, 38);
            _ok.Enabled = false;
            _ok.Click += async (s, e) => await Accept();
            var cancel = Theme.Button("Vazgeç");
            cancel.SetBounds(316, 418, 120, 38);
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { _search, find, _matches, _status, _tiles, _ok, cancel });
            Shown += async (s, e) => await Search();
        }

        private void Status(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        private async Task Search()
        {
            var term = _search.Text.Trim();
            if (term.Length == 0) return;
            Status($"\"{term}\" aranıyor...", Theme.Muted);
            try
            {
                var games = await _sg.SearchAsync(term);
                _matches.Items.Clear();
                if (games.Count == 0)
                {
                    Status($"\"{term}\" için sonuç yok. Adı değiştirip tekrar deneyin.", Theme.Warn);
                    return;
                }
                _matches.Items.AddRange(games.Cast<object>().ToArray());
                _matches.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Status(ex.Message, Theme.Bad);
            }
        }

        private async Task LoadIcons()
        {
            if (!(_matches.SelectedItem is SgGame game)) return;
            var id = ++_loadId;
            foreach (Control c in _tiles.Controls.Cast<Control>().ToList()) c.Dispose();
            _tiles.Controls.Clear();
            _ok.Enabled = false;
            Status($"{game.Name}: ikonlar alınıyor...", Theme.Muted);
            try
            {
                var icons = await _sg.IconsAsync(game.Id, _count);
                if (id != _loadId) return;
                if (icons.Count == 0)
                {
                    Status($"{game.Name} için ikon yok. Listeden başka bir eşleşme seçin.", Theme.Warn);
                    return;
                }
                Status($"{game.Name}: {icons.Count} ikon. Tıklayıp seçin (çift tıklama = seç ve kapat).", Theme.Ok);
                var tiles = icons.Select(icon =>
                {
                    var t = new ImageTile { Size = new Size(76, 76), Tag = icon };
                    t.Click += (s, e) => Select(t);
                    t.DoubleClick += async (s, e) => { Select(t); await Accept(); };
                    _tiles.Controls.Add(t);
                    return t;
                }).ToList();
                await Task.WhenAll(tiles.Select(async t =>
                {
                    try
                    {
                        var data = await _sg.DownloadAsync(((SgImage)t.Tag).Url);
                        if (id == _loadId && !t.IsDisposed) t.Image = ImageTools.Load(data);
                    }
                    catch { if (!t.IsDisposed) { t.Placeholder = "hata"; t.Invalidate(); } }
                }));
            }
            catch (Exception ex)
            {
                if (id == _loadId) Status(ex.Message, Theme.Bad);
            }
        }

        private void Select(ImageTile tile)
        {
            foreach (var t in _tiles.Controls.OfType<ImageTile>()) t.Selected = t == tile;
            _ok.Enabled = true;
        }

        private async Task Accept()
        {
            var tile = _tiles.Controls.OfType<ImageTile>().FirstOrDefault(t => t.Selected);
            if (tile == null) return;
            _ok.Enabled = false;
            Status("İkon indiriliyor...", Theme.Muted);
            try
            {
                Result = await _sg.DownloadAsync(((SgImage)tile.Tag).Url);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                Status("İndirilemedi: " + ex.Message, Theme.Bad);
                _ok.Enabled = true;
            }
        }
    }
}
