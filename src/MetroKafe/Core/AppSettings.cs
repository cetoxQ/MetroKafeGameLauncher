using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MetroKafe.Core
{
    /// <summary>
    /// Hazırlama aracının ayarları. Kullanıcı başına %AppData%\MetroKafe\ayarlar.ini içinde saklanır
    /// (API anahtarı bu yüzden programın ve GitHub deposunun içine girmez).
    /// </summary>
    public class AppSettings
    {
        /// <summary>Exe bu adlı klasörlerdeyse oyun klasörü için bir üst klasöre çıkılır.</summary>
        public static readonly string[] DefaultSkipFolders =
        {
            "bin", "bin32", "bin64", "bin_x64", "bin_x86", "binaries",
            "win32", "win64", "win_x64", "win_x86", "x64", "x86", "x32",
            "system", "shipping", "retail", "_retail_", "game",
        };

        public List<string> SkipFolders = DefaultSkipFolders.ToList();
        public bool DetectUnreal = true;
        public bool UseSteamGridName = true;
        public string SteamGridKey = "";
        public int GridCount = 10;
        public int IconCount = 10;

        private static string FilePath => Path.Combine(AppInfo.DataFolder, "ayarlar.ini");

        public static AppSettings Load()
        {
            var s = new AppSettings();
            if (!File.Exists(FilePath)) return s;
            var ini = IniFile.Load(FilePath);
            var skip = ini.Get("Klasor", "Atlanacaklar", null);
            if (skip != null)
                s.SkipFolders = skip.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            s.DetectUnreal = ini.GetBool("Klasor", "UnrealTani", true);
            s.UseSteamGridName = ini.GetBool("SteamGridDB", "AdiKullan", true);
            s.SteamGridKey = ini.Get("SteamGridDB", "ApiAnahtari");
            s.GridCount = Clamp(ini.Get("SteamGridDB", "KapakAdedi"), 10);
            s.IconCount = Clamp(ini.Get("SteamGridDB", "IkonAdedi"), 10);
            return s;
        }

        private static int Clamp(string value, int def) =>
            int.TryParse(value, out var n) ? Math.Max(1, Math.Min(50, n)) : def;

        public void Save()
        {
            var ini = File.Exists(FilePath) ? IniFile.Load(FilePath) : new IniFile();
            ini.Set("Klasor", "Atlanacaklar", string.Join(", ", SkipFolders));
            ini.Set("Klasor", "UnrealTani", DetectUnreal ? "1" : "0");
            ini.Set("SteamGridDB", "ApiAnahtari", SteamGridKey.Trim());
            ini.Set("SteamGridDB", "AdiKullan", UseSteamGridName ? "1" : "0");
            ini.Set("SteamGridDB", "KapakAdedi", GridCount.ToString());
            ini.Set("SteamGridDB", "IkonAdedi", IconCount.ToString());
            ini.Save(FilePath);
        }
    }
}
