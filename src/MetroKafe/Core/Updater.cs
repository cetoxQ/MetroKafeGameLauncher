using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MetroKafe.Core
{
    public class UpdateInfo
    {
        public Version Version;
        public string Tag;
        public string Notes;
        public string DownloadUrl;
        public bool IsNewer => Version > AppInfo.Version;
    }

    /// <summary>
    /// GitHub Releases üzerinden güncelleme. Sadece admin panelinden elle çağrılır.
    /// Çalışan exe yeniden adlandırılabildiği için: exe → .old, yeni dosya → exe, yeniden başlat.
    /// </summary>
    public static class Updater
    {
        static Updater()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private static WebClient NewClient()
        {
            var wc = new WebClient { Encoding = System.Text.Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "MetroKafe/" + AppInfo.Version;
            return wc;
        }

        public static async Task<UpdateInfo> CheckAsync()
        {
            using (var wc = NewClient())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                string json;
                try
                {
                    json = await wc.DownloadStringTaskAsync($"https://api.github.com/repos/{AppInfo.GitHubRepo}/releases/latest");
                }
                catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("GitHub'da henüz yayınlanmış bir sürüm yok.");
                }
                var data = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
                var tag = data["tag_name"] as string ?? "";
                var info = new UpdateInfo
                {
                    Tag = tag,
                    Version = ParseVersion(tag),
                    Notes = data.TryGetValue("body", out var b) ? b as string ?? "" : "",
                };
                if (data.TryGetValue("assets", out var a) && a is object[] assets)
                {
                    foreach (Dictionary<string, object> asset in assets)
                    {
                        if (string.Equals(asset["name"] as string, AppInfo.ReleaseAssetName, StringComparison.OrdinalIgnoreCase))
                            info.DownloadUrl = asset["browser_download_url"] as string;
                    }
                }
                if (info.DownloadUrl == null) throw new Exception($"{tag} sürümünde {AppInfo.ReleaseAssetName} dosyası yok.");
                return info;
            }
        }

        private static Version ParseVersion(string tag)
        {
            var s = tag.TrimStart('v', 'V');
            return Version.TryParse(s, out var v) ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : new Version(0, 0, 0);
        }

        /// <summary>Yeni exe'yi indirir, mevcut exe ile değiştirir ve programı yeniden başlatır.</summary>
        public static async Task ApplyAsync(UpdateInfo info, IProgress<int> progress)
        {
            var exe = AppInfo.ExePath;
            var newPath = exe + ".new";
            var oldPath = exe + ".old";
            if (File.Exists(newPath)) File.Delete(newPath);

            using (var wc = NewClient())
            {
                wc.DownloadProgressChanged += (s, e) => progress?.Report(e.ProgressPercentage);
                await wc.DownloadFileTaskAsync(info.DownloadUrl, newPath);
            }

            // İndirilen dosya gerçekten bir exe mi?
            var head = new byte[2];
            using (var fs = File.OpenRead(newPath)) fs.Read(head, 0, 2);
            if (head[0] != 'M' || head[1] != 'Z')
            {
                File.Delete(newPath);
                throw new Exception("İndirilen dosya geçerli bir program değil.");
            }

            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(exe, oldPath);
            try { File.Move(newPath, exe); }
            catch
            {
                File.Move(oldPath, exe);
                throw;
            }
            Process.Start(new ProcessStartInfo(exe, "--guncellendi") { UseShellExecute = true, WorkingDirectory = AppInfo.ExeFolder });
        }

        /// <summary>Program açılışında önceki güncellemeden kalan .old dosyasını siler.</summary>
        public static void CleanupOld()
        {
            var old = AppInfo.ExePath + ".old";
            // Eski süreç kapanırken birkaç saniye kilitli kalabilir.
            for (int i = 0; i < 30 && File.Exists(old); i++)
            {
                try { File.Delete(old); }
                catch { Thread.Sleep(300); }
            }
        }
    }
}
