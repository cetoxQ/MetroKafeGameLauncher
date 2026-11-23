using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MetroKafe.Core
{
    /// <summary>Sürüklenen exe'den oyun klasörünü ve adını bulur.</summary>
    public static class GameLocator
    {
        /// <summary>
        /// Exe'nin klasöründen başlayıp "bin", "win_x64" gibi klasörleri atlayarak yukarı çıkar.
        /// Örn. ets2\bin\win_x64\eurotrucks2.exe → ets2
        /// Unreal oyunlarında (Oyun\Proje\Binaries\Win64\x.exe) yanında Engine klasörü olan üst klasör alınır.
        /// </summary>
        public static string FindGameFolder(string exePath, AppSettings settings)
        {
            var skip = new HashSet<string>(settings.SkipFolders, StringComparer.OrdinalIgnoreCase);
            var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(exePath)));
            while (dir.Parent != null && dir.Parent.Parent != null && skip.Contains(dir.Name))
                dir = dir.Parent;

            if (settings.DetectUnreal && dir.Parent != null && dir.Parent.Parent != null
                && !Directory.Exists(Path.Combine(dir.FullName, "Engine"))
                && Directory.Exists(Path.Combine(dir.Parent.FullName, "Engine")))
                dir = dir.Parent;

            return dir.FullName;
        }

        /// <summary>Klasör adındaki [FitGirl], (v1.2), " - RUNE" gibi ekleri temizler.</summary>
        public static string CleanName(string folderName)
        {
            var s = Regex.Replace(folderName ?? "", @"\[.*?\]|\(.*?\)|\{.*?\}", "");
            s = Regex.Replace(s, @"\s+-\s+.*$", "");
            if (!s.Contains(" ")) s = s.Replace('.', ' ').Replace('_', ' ');
            s = Regex.Replace(s, @"\s+", " ").Trim(' ', '-', '.');
            return s.Length == 0 ? folderName : s;
        }

        /// <summary>SteamGridDB'de sırayla denenecek arama kelimeleri.</summary>
        public static List<string> SearchTerms(string gameFolder, string exePath)
        {
            var list = new List<string> { CleanName(Path.GetFileName(gameFolder.TrimEnd('\\'))) };
            try
            {
                var v = FileVersionInfo.GetVersionInfo(exePath);
                list.Add(v.ProductName);
                list.Add(v.FileDescription);
            }
            catch { }
            list.Add(CleanName(Path.GetFileNameWithoutExtension(exePath)));
            return list.Select(x => (x ?? "").Trim())
                .Where(x => x.Length > 1 && !x.Equals("Unreal Engine", StringComparison.OrdinalIgnoreCase)
                            && !x.StartsWith("BootstrapPackagedGame", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string Relative(string folder, string path)
        {
            var prefix = folder.TrimEnd('\\') + "\\";
            var full = Path.GetFullPath(path);
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full.Substring(prefix.Length) : full;
        }
    }
}
