using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MetroKafe.Core;

namespace MetroKafe.UI
{
    /// <summary>Hazırlama aracı ayarları: atlanacak klasörler, SteamGridDB.</summary>
    public class SettingsPage : Panel
    {
        public SettingsPage()
        {
            var s = AppSettings.Load();

            var title = Theme.Label("Ayarlar", Theme.Subtitle);
            Controls.Add(title);

            // ---------------- klasör
            var h1 = Theme.Label("Oyun klasörünü bulma", Theme.Bold, Theme.Accent);
            h1.Location = new Point(0, 40);
            var d1 = Theme.Label("Exe bu klasörlerden birinin içindeyse bir üst klasöre çıkılır (her satıra bir ad).\n" +
                                 "Örnek: ets2\\bin\\win_x64\\eurotrucks2.exe → oyun klasörü: ets2", Theme.Small, Theme.Muted);
            d1.Location = new Point(0, 64);

            var skip = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.Base,
                Text = string.Join(Environment.NewLine, s.SkipFolders),
            };
            skip.SetBounds(0, 104, 260, 330);

            var reset = Theme.Button("Varsayılana dön");
            reset.SetBounds(0, 442, 160, 34);
            reset.Click += (o, e) => skip.Text = string.Join(Environment.NewLine, AppSettings.DefaultSkipFolders);

            var unreal = Theme.CheckBox("Unreal Engine oyunlarını tanı", s.DetectUnreal);
            unreal.Location = new Point(280, 104);
            var ud = Theme.Label("Oyun\\Proje\\Binaries\\Win64\\x.exe gibi yapılarda,\nyanında \"Engine\" klasörü olan üst klasörü\noyun klasörü olarak alır.", Theme.Small, Theme.Muted);
            ud.Location = new Point(300, 130);

            // ---------------- SteamGridDB
            const int x2 = 610;
            var h2 = Theme.Label("SteamGridDB", Theme.Bold, Theme.Accent);
            h2.Location = new Point(x2, 40);

            var lk = Theme.Label("API anahtarı", Theme.Small, Theme.Muted);
            lk.Location = new Point(x2, 70);
            var key = Theme.TextBox(s.SteamGridKey);
            key.SetBounds(x2, 90, 270, 26);
            key.UseSystemPasswordChar = true;
            var show = Theme.CheckBox("Göster", false);
            show.Location = new Point(x2 + 280, 92);
            show.CheckedChanged += (o, e) => key.UseSystemPasswordChar = !show.Checked;

            var link = new LinkLabel
            {
                Text = "Anahtar al: steamgriddb.com → Preferences → API",
                Font = Theme.Small,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.AccentHover,
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            link.Location = new Point(x2, 122);
            link.LinkClicked += (o, e) => Process.Start("https://www.steamgriddb.com/profile/preferences/api");

            var useName = Theme.CheckBox("Oyun adını SteamGridDB'deki adla doldur", s.UseSteamGridName);
            useName.Location = new Point(x2, 152);
            var und = Theme.Label("Örn. klasör \"ets2\" ise ad \"Euro Truck Simulator 2\" olur.\nAdı elle değiştirirseniz dokunulmaz.", Theme.Small, Theme.Muted);
            und.Location = new Point(x2 + 20, 178);

            var lg = Theme.Label("Kapak adedi", Theme.Small, Theme.Muted);
            lg.Location = new Point(x2, 224);
            var grids = Number(s.GridCount);
            grids.Location = new Point(x2 + 100, 220);
            var li = Theme.Label("İkon adedi", Theme.Small, Theme.Muted);
            li.Location = new Point(x2, 258);
            var icons = Number(s.IconCount);
            icons.Location = new Point(x2 + 100, 254);

            var test = Theme.Button("Anahtarı Test Et");
            test.SetBounds(x2, 296, 160, 34);
            var testResult = Theme.Label("", Theme.Small);
            testResult.Location = new Point(x2 + 170, 305);
            test.Click += async (o, e) =>
            {
                test.Enabled = false;
                testResult.ForeColor = Theme.Muted;
                testResult.Text = "Deneniyor...";
                try
                {
                    var r = await new SteamGrid(key.Text).SearchAsync("Portal");
                    testResult.ForeColor = Theme.Ok;
                    testResult.Text = "Çalışıyor (" + r.Count + " sonuç).";
                }
                catch (Exception ex)
                {
                    testResult.ForeColor = Theme.Bad;
                    testResult.Text = ex.Message;
                }
                finally { test.Enabled = true; }
            };

            // ---------------- kaydet
            var status = Theme.Label("", Theme.Small);
            status.Location = new Point(170, 560);
            var save = Theme.Button("Kaydet", true);
            save.SetBounds(0, 550, 150, 40);
            save.Click += (o, e) =>
            {
                s.SkipFolders = skip.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                s.DetectUnreal = unreal.Checked;
                s.SteamGridKey = key.Text.Trim();
                s.UseSteamGridName = useName.Checked;
                s.GridCount = (int)grids.Value;
                s.IconCount = (int)icons.Value;
                try
                {
                    s.Save();
                    status.ForeColor = Theme.Ok;
                    status.Text = "Kaydedildi. Yeni ayarlar bir sonraki sürüklemede kullanılır.";
                }
                catch (Exception ex)
                {
                    status.ForeColor = Theme.Bad;
                    status.Text = "Kaydedilemedi: " + ex.Message;
                }
            };

            Controls.AddRange(new Control[]
            {
                h1, d1, skip, reset, unreal, ud,
                h2, lk, key, show, link, useName, und, lg, grids, li, icons, test, testResult,
                save, status,
            });
        }

        private static NumericUpDown Number(int value) => new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Value = value,
            Width = 70,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Base,
        };
    }
}
