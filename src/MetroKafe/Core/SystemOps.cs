using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MetroKafe.Core
{
    /// <summary>Kurulum sonrası sistem işlemleri: Belgelerim, kayıt defteri, Defender, kısayollar.</summary>
    public static class SystemOps
    {
        public static int Run(string exe, string args, out string output)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                output = (o.Result + e.Result).Trim();
                return p.ExitCode;
            }
        }

        private static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";

        private static int PowerShell(string command, out string output) =>
            Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"", out output);

        public static void AddDefenderExclusion(string folder, Log log)
        {
            PowerShell("Add-MpPreference -ExclusionPath " + PsQuote(folder), out var o1);
            PowerShell("(Get-MpPreference).ExclusionPath", out var list);
            if (list.Split('\n').Any(l => string.Equals(l.Trim().TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                log.Ok("Windows Defender istisnası eklendi.");
            else
                log.Fail("Windows Defender istisnası eklenemedi (Defender kapalı veya başka antivirüs olabilir). " + o1);
        }

        public static void RemoveDefenderExclusion(string folder, Log log)
        {
            var code = PowerShell("Remove-MpPreference -ExclusionPath " + PsQuote(folder), out var o);
            if (code == 0) log.Ok("Windows Defender istisnası kaldırıldı.");
            else log.Fail("Windows Defender istisnası kaldırılamadı. " + o);
        }

        public static void ImportReg(string regPath, Log log)
        {
            var code = Run("reg.exe", "import \"" + regPath + "\"", out var o);
            if (code == 0) log.Ok("Kayıt defteri dosyası yüklendi: " + Path.GetFileName(regPath));
            else log.Fail("Kayıt defteri dosyası yüklenemedi: " + o);
        }

        /// <summary>
        /// Oyunun Belgelerim'e giden dosyalarını kopyalar. Klasörler birleştirilir; Belgelerim'de
        /// zaten olan ve farklı olan dosyalar önce Belgelerim\MetroKafeYedek\tarih altına yedeklenir.
        /// (Eski bat tüm üst klasörü taşıyordu; "My Games" gibi ortak klasörlerde diğer oyunların
        /// kayıtları da kayboluyordu.)
        /// </summary>
        public static void CopyDocuments(string source, Log log, string docs = null)
        {
            docs = docs ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var backupRoot = Path.Combine(docs, "MetroKafeYedek", DateTime.Now.ToString("yyyy.MM.dd-HH-mm"));
            var prefix = source.TrimEnd('\\') + "\\";
            int copied = 0, backedUp = 0, failed = 0;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(prefix.Length);
                var target = Path.Combine(docs, rel);
                try
                {
                    if (File.Exists(target))
                    {
                        if (SameContent(file, target)) continue;
                        var backup = Path.Combine(backupRoot, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.SetAttributes(target, FileAttributes.Normal);
                        File.Move(target, backup);
                        backedUp++;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, false);
                    File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
                    copied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    log.Fail($"Belgelerim'e kopyalanamadı: {rel} - {ex.Message}");
                }
            }
            if (backedUp > 0) log.Info($"Belgelerim'deki {backedUp} eski dosya yedeklendi: {backupRoot}");
            if (failed == 0) log.Ok($"Belgelerim dosyaları kopyalandı ({copied} dosya).");
        }

        private static bool SameContent(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return HashList.ComputeFile(a, null, System.Threading.CancellationToken.None) ==
                   HashList.ComputeFile(b, null, System.Threading.CancellationToken.None);
        }

        public static string DesktopFolder => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        private static string LnkPath(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "");
            return Path.Combine(DesktopFolder, name + ".lnk");
        }

        public static void CreateShortcuts(GameConfig cfg, Log log)
        {
            CreateOne(cfg, cfg.Name, cfg.Exe, cfg.Arguments, cfg.Icon, log);
            foreach (var s in cfg.ExtraShortcuts)
                CreateOne(cfg, s.Name, s.Target, s.Arguments, s.Icon, log);
        }

        private static void CreateOne(GameConfig cfg, string name, string target, string args, string icon, Log log)
        {
            try
            {
                var targetPath = cfg.ResolvePath(target);
                if (!File.Exists(targetPath)) log.Fail($"Kısayol hedefi bulunamadı: {target}");
                var iconPath = cfg.ResolvePath(icon);
                if (string.IsNullOrEmpty(icon) || !File.Exists(iconPath)) iconPath = targetPath;
                Shortcut.Create(LnkPath(name), targetPath, args, Path.GetDirectoryName(targetPath), iconPath);
                log.Ok($"Masaüstü kısayolu oluşturuldu: {name}");
            }
            catch (Exception ex)
            {
                log.Fail($"Kısayol oluşturulamadı ({name}): {ex.Message}");
            }
        }

        public static void RemoveShortcuts(GameConfig cfg, Log log)
        {
            foreach (var name in new[] { cfg.Name }.Concat(cfg.ExtraShortcuts.Select(s => s.Name)))
            {
                try
                {
                    var p = LnkPath(name);
                    if (File.Exists(p)) { File.Delete(p); log.Ok("Kısayol silindi: " + name); }
                }
                catch (Exception ex) { log.Fail($"Kısayol silinemedi ({name}): {ex.Message}"); }
            }
        }

        public static void LaunchGame(GameConfig cfg)
        {
            var path = cfg.ResolvePath(cfg.Exe);
            Process.Start(new ProcessStartInfo(path, cfg.Arguments ?? "")
            {
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = true,
            });
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0") + " MB";
            return (bytes / 1024.0).ToString("0") + " KB";
        }
    }
}
