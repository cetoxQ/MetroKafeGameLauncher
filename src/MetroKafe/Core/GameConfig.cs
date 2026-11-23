using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MetroKafe.Core
{
    public class ShortcutInfo
    {
        public string Name = "";
        public string Target = "";
        public string Arguments = "";
        public string Icon = "";
    }

    /// <summary>
    /// Oyun klasöründeki MetroKafe.ini dosyasının karşılığı.
    /// </summary>
    public class GameConfig
    {
        public const string FileName = "MetroKafe.ini";
        public const string HashFileName = "MetroKafeHash.md5";
        public const string LogFileName = "MetroKafeReport.log";
        public const string ExeFileName = "MetroKafe.exe";

        public string Folder;

        public string Name = "";
        public string Exe = "";
        public string Arguments = "";
        public string Icon = "ico.ico";
        public string Cover = "MetroKafeCover.png";

        public string InstallRoot = "MetroKafeOyun";
        public string DocumentsSource = @"gamepath\CSIDL_MYDOCUMENTS";
        public string RegFile = "MetroKafeReg.reg";
        public bool DefenderExclusion = true;

        public List<ShortcutInfo> ExtraShortcuts = new List<ShortcutInfo>();

        public bool Installed;
        public string InstallDate = "";
        public string SourcePath = "";

        public string IniPath => Path.Combine(Folder, FileName);
        public string HashPath => Path.Combine(Folder, HashFileName);

        /// <summary>Kurulumda oluşturulacak klasör adı (geçersiz karakterler temizlenir).</summary>
        public string FolderName
        {
            get
            {
                var n = Name;
                foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "");
                n = n.Trim().TrimEnd('.');
                return n.Length == 0 ? "Oyun" : n;
            }
        }

        public static bool Exists(string folder) => File.Exists(Path.Combine(folder, FileName));

        public static GameConfig Load(string folder)
        {
            var ini = IniFile.Load(Path.Combine(folder, FileName));
            var c = new GameConfig { Folder = folder };
            c.Name = ini.Get("Oyun", "Ad", new DirectoryInfo(folder).Name);
            c.Exe = ini.Get("Oyun", "Exe");
            c.Arguments = ini.Get("Oyun", "Arguman");
            c.Icon = ini.Get("Oyun", "Ikon", c.Icon);
            c.Cover = ini.Get("Oyun", "Kapak", c.Cover);

            c.InstallRoot = ini.Get("Kurulum", "HedefKlasor", c.InstallRoot);
            c.DocumentsSource = ini.Get("Kurulum", "BelgelerKaynak", c.DocumentsSource);
            c.RegFile = ini.Get("Kurulum", "RegDosyasi", c.RegFile);
            c.DefenderExclusion = ini.GetBool("Kurulum", "DefenderIstisnasi", true);

            foreach (var sec in ini.SectionNames.Where(s => s.StartsWith("Kisayol", StringComparison.OrdinalIgnoreCase)).OrderBy(s => s))
            {
                var sc = new ShortcutInfo
                {
                    Name = ini.Get(sec, "Ad"),
                    Target = ini.Get(sec, "Hedef"),
                    Arguments = ini.Get(sec, "Arguman"),
                    Icon = ini.Get(sec, "Ikon"),
                };
                if (sc.Name.Length > 0 && sc.Target.Length > 0) c.ExtraShortcuts.Add(sc);
            }

            c.Installed = ini.GetBool("Durum", "Kurulu");
            c.InstallDate = ini.Get("Durum", "KurulumTarihi");
            c.SourcePath = ini.Get("Durum", "KaynakYol");
            return c;
        }

        public void Save()
        {
            var ini = File.Exists(IniPath) ? IniFile.Load(IniPath) : new IniFile();
            ini.Set("Oyun", "Ad", Name);
            ini.Set("Oyun", "Exe", Exe);
            ini.Set("Oyun", "Arguman", Arguments);
            ini.Set("Oyun", "Ikon", Icon);
            ini.Set("Oyun", "Kapak", Cover);

            ini.Set("Kurulum", "HedefKlasor", InstallRoot);
            ini.Set("Kurulum", "BelgelerKaynak", DocumentsSource);
            ini.Set("Kurulum", "RegDosyasi", RegFile);
            ini.Set("Kurulum", "DefenderIstisnasi", DefenderExclusion ? "1" : "0");

            foreach (var sec in ini.SectionNames.Where(s => s.StartsWith("Kisayol", StringComparison.OrdinalIgnoreCase)).ToList())
                ini.RemoveSection(sec);
            for (int i = 0; i < ExtraShortcuts.Count; i++)
            {
                var sec = "Kisayol" + (i + 2);
                ini.Set(sec, "Ad", ExtraShortcuts[i].Name);
                ini.Set(sec, "Hedef", ExtraShortcuts[i].Target);
                ini.Set(sec, "Arguman", ExtraShortcuts[i].Arguments);
                ini.Set(sec, "Ikon", ExtraShortcuts[i].Icon);
            }

            ini.Set("Durum", "Kurulu", Installed ? "1" : "0");
            ini.Set("Durum", "KurulumTarihi", InstallDate);
            ini.Set("Durum", "KaynakYol", SourcePath);
            ini.Save(IniPath);
        }

        /// <summary>MD5 listesine girmeyecek dosyalar (kurulumla değişen veya programın kendisi).</summary>
        public static bool IsManagedFile(string relativePath)
        {
            if (relativePath.Contains("\\")) return false;
            var n = relativePath.ToLowerInvariant();
            return n == FileName.ToLowerInvariant()
                || n == HashFileName.ToLowerInvariant()
                || n == LogFileName.ToLowerInvariant()
                || n == ExeFileName.ToLowerInvariant()
                || n == AppInfo.ExeName.ToLowerInvariant()
                || n.EndsWith(".exe.old") || n.EndsWith(".exe.new")
                // eski bat sisteminden kalan dosyalar
                || (n.StartsWith("metrokafe") && (n.EndsWith(".bat") || n.EndsWith(".css") || n.EndsWith(".html") || n.EndsWith(".rar") || n.EndsWith(".exe")));
        }

        public string ResolvePath(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return "";
            return Path.IsPathRooted(relative) ? relative : Path.Combine(Folder, relative);
        }
    }
}
