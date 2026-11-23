using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>
    /// Ek exe satırı (ayrı masaüstü kısayolu): ad, exe, argüman, ikon.
    /// İkon yerel dosyadan veya SteamGridDB'den seçilir; Hazırla'da oyun klasörüne yazılır.
    /// </summary>
    public class ExtraExeRow : Panel
    {
        public const int RowWidth = 450;

        private readonly Func<string> _gameFolder;
        private readonly Func<string> _gameName;
        public readonly TextBox NameBox = Theme.TextBox();
        public readonly ComboBox ExeBox = new ComboBox();
        public readonly TextBox ArgsBox = Theme.TextBox();
        private readonly PictureBox _preview = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Back };

        /// <summary>Oyun klasöründeki mevcut ikon (göreli yol).</summary>
        public string IconPath = "";

        /// <summary>Yeni seçilen ikon (Hazırla'da .ico olarak yazılır).</summary>
        public byte[] NewIcon;

        public event Action<ExtraExeRow> RemoveRequested;

        public ExtraExeRow(Func<string> gameFolder, Func<string> gameName, object[] exeItems)
        {
            _gameFolder = gameFolder;
            _gameName = gameName;
            Size = new Size(RowWidth, 72);
            Margin = new Padding(0, 0, 0, 6);
            BackColor = Theme.Panel;

            NameBox.SetBounds(6, 6, 156, 26);
            Cue(NameBox, "Kısayol adı");

            ExeBox.DropDownStyle = ComboBoxStyle.DropDown;
            ExeBox.FlatStyle = FlatStyle.Flat;
            ExeBox.BackColor = Theme.Back;
            ExeBox.ForeColor = Theme.Text;
            ExeBox.Font = Theme.Base;
            ExeBox.Items.AddRange(exeItems);
            ExeBox.SetBounds(168, 6, 206, 26);
            ExeBox.SelectedIndexChanged += (s, e) => SuggestName();

            var browse = Theme.Button("...");
            browse.SetBounds(378, 5, 32, 28);
            browse.Click += (s, e) => BrowseExe();

            var remove = Theme.Button("✕");
            remove.SetBounds(414, 5, 30, 28);
            remove.ForeColor = Theme.Bad;
            remove.Click += (s, e) => RemoveRequested?.Invoke(this);

            ArgsBox.SetBounds(6, 40, 156, 26);
            Cue(ArgsBox, "Argüman (isteğe bağlı)");

            _preview.SetBounds(168, 38, 30, 30);

            var pick = Theme.Button("İkon seç");
            pick.SetBounds(204, 39, 90, 28);
            pick.Click += (s, e) => PickLocalIcon();

            var sg = Theme.Button("SteamGrid'de ara");
            sg.SetBounds(298, 39, 146, 28);
            sg.Click += (s, e) => PickSteamGridIcon();

            Controls.AddRange(new Control[] { NameBox, ExeBox, browse, remove, ArgsBox, _preview, pick, sg });
        }

        public void Fill(ShortcutInfo s)
        {
            NameBox.Text = s.Name;
            ExeBox.Text = s.Target;
            ArgsBox.Text = s.Arguments;
            IconPath = s.Icon ?? "";
            var full = IconPath.Length > 0 ? Path.Combine(_gameFolder(), IconPath) : "";
            if (File.Exists(full)) SetPreview(File.ReadAllBytes(full));
        }

        public ShortcutInfo ToShortcut() => new ShortcutInfo
        {
            Name = NameBox.Text.Trim(),
            Target = ExeBox.Text.Trim(),
            Arguments = ArgsBox.Text.Trim(),
            Icon = IconPath,
        };

        public bool IsEmpty => NameBox.Text.Trim().Length == 0 && ExeBox.Text.Trim().Length == 0;

        /// <summary>Exe yolunu (tam veya göreli) satıra yazar, ad boşsa exe'nin ürün adını önerir.</summary>
        public void SetExe(string path)
        {
            var folder = _gameFolder();
            ExeBox.Text = Path.IsPathRooted(path) ? GameLocator.Relative(folder, path) : path;
            SuggestName();
        }

        private void SuggestName()
        {
            if (NameBox.Text.Trim().Length > 0) return;
            var full = Path.Combine(_gameFolder(), ExeBox.Text.Trim());
            string name = null;
            try
            {
                var v = FileVersionInfo.GetVersionInfo(full);
                name = !string.IsNullOrWhiteSpace(v.FileDescription) ? v.FileDescription : v.ProductName;
            }
            catch { }
            NameBox.Text = string.IsNullOrWhiteSpace(name) ? GameLocator.CleanName(Path.GetFileNameWithoutExtension(full)) : name.Trim();
        }

        /// <returns>Geçerli bir exe seçildiyse true.</returns>
        public bool BrowseExe()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Oyun dosyası|*.exe;*.bat;*.cmd|Tüm dosyalar|*.*",
                Title = "Ek exe'yi seçin",
                InitialDirectory = _gameFolder(),
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return false;
                if (!IsInGame(dlg.FileName))
                {
                    MessageBox.Show(FindForm(), "Seçilen dosya oyun klasörünün içinde olmalı.", "Ek exe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                SetExe(dlg.FileName);
                return true;
            }
        }

        public bool IsInGame(string path) =>
            Path.GetFullPath(path).StartsWith(_gameFolder().TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

        private void PickLocalIcon()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "İkon veya resim|*.ico;*.png;*.jpg;*.jpeg|Tüm dosyalar|*.*",
                Title = "İkon seçin",
                InitialDirectory = _gameFolder(),
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    var data = File.ReadAllBytes(dlg.FileName);
                    SetPreview(data);
                    NewIcon = data;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(FindForm(), "İkon okunamadı: " + ex.Message, "İkon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PickSteamGridIcon()
        {
            var term = NameBox.Text.Trim().Length > 0 ? NameBox.Text.Trim() : _gameName();
            using (var dlg = new IconPickerForm(term))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK || dlg.Result == null) return;
                SetPreview(dlg.Result);
                NewIcon = dlg.Result;
            }
        }

        private void SetPreview(byte[] data)
        {
            try
            {
                var old = _preview.Image;
                _preview.Image = ImageTools.Load(data);
                old?.Dispose();
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Boş kutuda gri ipucu yazısı.</summary>
        private static void Cue(TextBox box, string text) =>
            box.HandleCreated += (s, e) => SendMessage(box.Handle, 0x1501 /*EM_SETCUEBANNER*/, (IntPtr)1, text);
    }
}
