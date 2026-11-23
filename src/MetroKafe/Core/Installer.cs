using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace MetroKafe.Core
{
    public class CopyResult
    {
        public List<string> Failed = new List<string>();
    }

    /// <summary>Oyunu kaynaktan (ISO) hedefe kopyalar ve kurulum sonrası adımları çalıştırır.</summary>
    public static class Installer
    {
        public static string TargetFolder(GameConfig cfg, DriveInfo drive) =>
            Path.Combine(drive.RootDirectory.FullName, cfg.InstallRoot, cfg.FolderName);

        /// <summary>Kopyalanacak toplam boyut (program dosyaları dahil).</summary>
        public static long SourceSize(string source) =>
            HashList.ListFiles(source, false).Sum(f => new FileInfo(Path.Combine(source, f)).Length);

        /// <summary>
        /// Tüm dosyaları kopyalar. Okunan veri aynı anda MD5'ten geçirilir ve listedeki değerle
        /// karşılaştırılır; böylece bozuk ISO / okunamayan disk anında yakalanır.
        /// </summary>
        public static CopyResult Copy(string source, string target, IEnumerable<string> onlyFiles,
            Dictionary<string, string> expected, IProgress<ProgressInfo> progress, Log log, CancellationToken ct)
        {
            var files = (onlyFiles ?? HashList.ListFiles(source, false)).ToList();
            var info = new ProgressInfo
            {
                TotalFiles = files.Count,
                TotalBytes = files.Sum(f => { var p = Path.Combine(source, f); return File.Exists(p) ? new FileInfo(p).Length : 0; }),
            };
            var result = new CopyResult();
            var buf = new byte[HashList.BufferSize];
            int lastReport = 0;

            foreach (var rel in files)
            {
                ct.ThrowIfCancellationRequested();
                info.CurrentFile = rel;
                var src = Path.Combine(source, rel);
                var dst = Path.Combine(target, rel);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
                    string hash;
                    using (var md5 = MD5.Create())
                    using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, buf.Length, FileOptions.SequentialScan))
                    using (var output = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length))
                    {
                        output.SetLength(input.Length);
                        int n;
                        while ((n = input.Read(buf, 0, buf.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            md5.TransformBlock(buf, 0, n, null, 0);
                            output.Write(buf, 0, n);
                            info.DoneBytes += n;
                            if (Environment.TickCount - lastReport > 100) { lastReport = Environment.TickCount; progress?.Report(info); }
                        }
                        md5.TransformFinalBlock(buf, 0, 0);
                        hash = HashList.ToHex(md5.Hash);
                    }
                    // Kaynak ISO'da salt okunur gelir; kurulu oyunda kaldırılır, gizli öznitelik korunur.
                    var attrs = File.GetAttributes(src) & ~FileAttributes.ReadOnly;
                    File.SetAttributes(dst, attrs == 0 ? FileAttributes.Normal : attrs);
                    File.SetLastWriteTime(dst, File.GetLastWriteTime(src));

                    if (expected != null && expected.TryGetValue(rel.ToLowerInvariant(), out var exp) && exp != hash)
                    {
                        result.Failed.Add(rel);
                        log.Fail("MD5 tutmadı (kaynak bozuk olabilir): " + rel);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    result.Failed.Add(rel);
                    log.Fail($"Kopyalanamadı: {rel} - {ex.Message}");
                }
                info.DoneFiles++;
            }
            progress?.Report(info);
            return result;
        }

        public static Dictionary<string, string> ExpectedHashes(string folder)
        {
            var path = Path.Combine(folder, GameConfig.HashFileName);
            if (!File.Exists(path)) return null;
            var d = new Dictionary<string, string>();
            foreach (var e in HashList.Load(path)) d[e.RelativePath.ToLowerInvariant()] = e.Hash;
            return d;
        }

        /// <summary>Kopyalama bittikten sonra hedef klasörde yapılacak işler.</summary>
        public static void PostInstall(GameConfig target, string sourcePath, Log log)
        {
            var docs = target.ResolvePath(target.DocumentsSource);
            if (!string.IsNullOrEmpty(target.DocumentsSource) && Directory.Exists(docs))
                SystemOps.CopyDocuments(docs, log);

            if (target.DefenderExclusion)
                SystemOps.AddDefenderExclusion(target.Folder, log);

            var reg = target.ResolvePath(target.RegFile);
            if (!string.IsNullOrEmpty(target.RegFile) && File.Exists(reg))
                SystemOps.ImportReg(reg, log);

            SystemOps.CreateShortcuts(target, log);

            target.Installed = true;
            target.InstallDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            target.SourcePath = sourcePath;
            target.Save();
            log.Ok("Kurulum tamamlandı.");
        }

        /// <summary>
        /// Oyunu kaldırır. Program oyun klasöründen çalıştığı için klasör, program kapandıktan
        /// sonra arka planda silinir.
        /// </summary>
        public static void Uninstall(GameConfig cfg, Log log)
        {
            SystemOps.RemoveShortcuts(cfg, log);
            if (cfg.DefenderExclusion) SystemOps.RemoveDefenderExclusion(cfg.Folder, log);
            var folder = cfg.Folder.TrimEnd('\\');
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
                $"/c ping 127.0.0.1 -n 4 >nul & rmdir /s /q \"{folder}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
        }
    }
}
