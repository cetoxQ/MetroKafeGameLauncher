using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MetroKafe.Core
{
    public class SgGame
    {
        public int Id;
        public string Name;
        public override string ToString() => Name;
    }

    public class SgImage
    {
        public string Url;
        public string Thumb;
        public string Mime;
        public bool IsIco => Mime == "image/vnd.microsoft.icon" || Url.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SteamGridDB API (v2): oyun arama, 2:3 kapak (600x900) ve ikon listesi.</summary>
    public class SteamGrid
    {
        private const string Api = "https://www.steamgriddb.com/api/v2/";
        private readonly string _key;

        static SteamGrid()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public SteamGrid(string key)
        {
            _key = key?.Trim() ?? "";
        }

        private WebClient Client(bool auth)
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "MetroKafe/" + AppInfo.Version;
            if (auth) wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + _key;
            return wc;
        }

        private async Task<object[]> GetData(string path)
        {
            if (_key.Length == 0) throw new Exception("SteamGridDB API anahtarı girilmemiş (Ayarlar).");
            string json;
            using (var wc = Client(true))
            {
                try { json = await wc.DownloadStringTaskAsync(Api + path); }
                catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("SteamGridDB API anahtarı geçersiz.");
                }
            }
            var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (!(d.TryGetValue("success", out var ok) && ok is bool b && b))
                throw new Exception("SteamGridDB hata döndürdü: " + json);
            return d.TryGetValue("data", out var data) && data is object[] arr ? arr : new object[0];
        }

        public async Task<List<SgGame>> SearchAsync(string term)
        {
            var data = await GetData("search/autocomplete/" + Uri.EscapeDataString(term.Trim()));
            return data.Cast<Dictionary<string, object>>()
                .Select(x => new SgGame { Id = Convert.ToInt32(x["id"]), Name = x["name"] as string })
                .ToList();
        }

        public async Task<List<SgImage>> GridsAsync(int gameId, int count)
        {
            var data = await GetData($"grids/game/{gameId}?dimensions=600x900&types=static&mimes=image/png,image/jpeg&nsfw=false&humor=false");
            return ToImages(data, count);
        }

        public async Task<List<SgImage>> IconsAsync(int gameId, int count)
        {
            var data = await GetData($"icons/game/{gameId}?types=static&mimes=image/png,image/vnd.microsoft.icon&nsfw=false&humor=false");
            return ToImages(data, count);
        }

        private static List<SgImage> ToImages(object[] data, int count) =>
            data.Cast<Dictionary<string, object>>()
                .Select(x => new SgImage
                {
                    Url = x["url"] as string,
                    Thumb = x.TryGetValue("thumb", out var t) ? t as string : null,
                    Mime = x.TryGetValue("mime", out var m) ? m as string : "",
                })
                .Where(x => !string.IsNullOrEmpty(x.Url))
                .Take(count)
                .ToList();

        /// <summary>Görsel indirir (CDN için API anahtarı gönderilmez).</summary>
        public async Task<byte[]> DownloadAsync(string url)
        {
            using (var wc = Client(false))
                return await wc.DownloadDataTaskAsync(url);
        }
    }
}
