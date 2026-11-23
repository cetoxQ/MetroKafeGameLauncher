using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MetroKafe.Core;

// Çekirdek mantığın uçtan uca testi: hazırla → MD5 → ISO → kopyala → doğrula → bozulma → onar.
// Sistem ayarlarına (Defender, kayıt defteri, masaüstü) dokunmaz.
// Kullanım: MetroKafe.Tests.exe [test klasörü]
internal static class Program
{
    private static int _fail;

    private static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "  OK   " : "  FAIL ") + name);
        if (!ok) _fail++;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint load);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr h);

    /// <summary>Windows'un (masaüstü/kısayol) ikonu istenen boyutta yükleyip yükleyemediği.</summary>
    private static int WinIconWidth(string path, int size)
    {
        var h = LoadImage(IntPtr.Zero, path, 1 /*IMAGE_ICON*/, size, size, 0x10 /*LR_LOADFROMFILE*/);
        if (h == IntPtr.Zero) return 0;
        try { using (var icon = Icon.FromHandle(h)) return icon.Width; }
        finally { DestroyIcon(h); }
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var root = Path.Combine(args.Length > 0 ? args[0] : Path.GetTempPath(), "mk-test");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        var game = Path.Combine(root, "Assassin's Creed & Co!");
        Directory.CreateDirectory(Path.Combine(game, "bin", "x64"));
        Directory.CreateDirectory(Path.Combine(game, @"gamepath\CSIDL_MYDOCUMENTS\My Games\Test"));
        File.WriteAllBytes(Path.Combine(game, "bin", "x64", "Game!.exe"), Enumerable.Range(0, 5_000_000).Select(i => (byte)(i * 7)).ToArray());
        File.WriteAllText(Path.Combine(game, "Türkçe ğüşıöç %PATH%.txt"), "merhaba", Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(game, "bos.dat"), new byte[0]);
        File.WriteAllText(Path.Combine(game, @"gamepath\CSIDL_MYDOCUMENTS\My Games\Test\save.sav"), "save");
        var hidden = Path.Combine(game, "gizli.cfg");
        File.WriteAllText(hidden, "hidden");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        File.WriteAllText(Path.Combine(game, "MetroKafeKisayol.bat"), "eski");
        using (var bmp = new Bitmap(300, 400)) bmp.Save(Path.Combine(game, "MetroKafeCover.png"));

        Console.WriteLine("ini");
        var cfg = new GameConfig { Folder = game, Name = "Assassin's Creed & Co!", Exe = @"bin\x64\Game!.exe", Arguments = "-windowed" };
        cfg.ExtraShortcuts.Add(new ShortcutInfo { Name = "Ek Mod", Target = @"bin\x64\Game!.exe", Arguments = "-mod" });
        cfg.Save();
        var back = GameConfig.Load(game);
        Check(back.Name == cfg.Name && back.Exe == cfg.Exe && back.Arguments == "-windowed", "kaydet/oku");
        Check(back.ExtraShortcuts.Count == 1 && back.ExtraShortcuts[0].Arguments == "-mod", "ek kısayol");
        Check(!back.Installed, "Kurulu=0");
        Check(back.FolderName == "Assassin's Creed & Co!", "klasör adı");

        Console.WriteLine("ini ANSI (1254)");
        var ansiDir = Path.Combine(root, "ansi");
        Directory.CreateDirectory(ansiDir);
        File.WriteAllText(Path.Combine(ansiDir, GameConfig.FileName), "[Oyun]\r\nAd=Şehir Oyunu\r\n", Encoding.GetEncoding(1254));
        Check(GameConfig.Load(ansiDir).Name == "Şehir Oyunu", "Türkçe ANSI ini");

        Console.WriteLine("MD5");
        File.Copy(typeof(Program).Assembly.Location, Path.Combine(game, GameConfig.ExeFileName));
        var list = HashList.Build(game, null, CancellationToken.None);
        HashList.Save(cfg.HashPath, list);
        var names = list.Select(e => e.RelativePath).ToList();
        Check(names.Contains(@"bin\x64\Game!.exe"), "! içeren dosya");
        Check(names.Contains("Türkçe ğüşıöç %PATH%.txt"), "Türkçe ve % içeren dosya");
        Check(names.Contains("gizli.cfg"), "gizli dosya");
        Check(names.Contains("bos.dat") && list.First(e => e.RelativePath == "bos.dat").Hash == "d41d8cd98f00b204e9800998ecf8427e", "boş dosya");
        Check(!names.Any(n => n.StartsWith("MetroKafe", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".png")), "MetroKafe dosyaları hariç");
        Check(HashList.Load(cfg.HashPath).Count == list.Count, "md5 dosyası geri okunuyor");
        Check((File.GetAttributes(cfg.HashPath) & FileAttributes.Hidden) != 0, "md5 gizli");
        HashList.Save(cfg.HashPath, list);
        Check(HashList.Load(cfg.HashPath).Count == list.Count, "md5 ikinci kez yazılınca çiftlenmiyor");

        Console.WriteLine("ISO");
        var iso = Path.Combine(root, "test.iso");
        IsoBuilder.Build(game, iso, cfg.Name, null, CancellationToken.None);
        Check(File.Exists(iso) && new FileInfo(iso).Length > 5_000_000, "ISO oluştu (" + new FileInfo(iso).Length / 1024 + " KB)");

        Console.WriteLine("kopyala + doğrula");
        var target = Path.Combine(root, "kurulu", cfg.FolderName);
        var log = new Log(null);
        log.Line += l => Console.WriteLine("        " + l);
        var res = Installer.Copy(game, target, null, Installer.ExpectedHashes(game), null, log, CancellationToken.None);
        Check(res.Failed.Count == 0, "kopyalama hatasız");
        Check(File.Exists(Path.Combine(target, GameConfig.FileName)) && File.Exists(Path.Combine(target, GameConfig.ExeFileName)), "ini ve exe kopyalandı");
        Check((File.GetAttributes(Path.Combine(target, "gizli.cfg")) & FileAttributes.Hidden) != 0, "gizli öznitelik korundu");
        var v = Verifier.Verify(target, null, CancellationToken.None);
        Check(v.AllOk && v.Ok == list.Count, "doğrulama: hepsi sağlam");

        Console.WriteLine("bozulma + onarım");
        File.WriteAllText(Path.Combine(target, "Türkçe ğüşıöç %PATH%.txt"), "bozuk");
        File.SetAttributes(Path.Combine(target, "gizli.cfg"), FileAttributes.Normal);
        File.Delete(Path.Combine(target, "gizli.cfg"));
        v = Verifier.Verify(target, null, CancellationToken.None);
        Check(v.Items.Count(i => i.Status == VerifyStatus.Bad) == 1 && v.Items.Count(i => i.Status == VerifyStatus.Missing) == 1, "1 hatalı, 1 eksik bulundu");
        res = Installer.Copy(game, target, v.Problems.Select(p => p.RelativePath), Installer.ExpectedHashes(target), null, log, CancellationToken.None);
        Check(res.Failed.Count == 0 && Verifier.Verify(target, null, CancellationToken.None).AllOk, "onarım sonrası sağlam");

        Console.WriteLine("bozuk kaynak tespiti");
        var expected = Installer.ExpectedHashes(game);
        expected["bos.dat"] = "00000000000000000000000000000000";
        res = Installer.Copy(game, Path.Combine(root, "kurulu2"), null, expected, null, new Log(null), CancellationToken.None);
        Check(res.Failed.SequenceEqual(new[] { "bos.dat" }), "MD5 tutmayan kaynak dosya yakalandı");

        Console.WriteLine("Belgelerim");
        var docs = Path.Combine(root, "Belgeler");
        Directory.CreateDirectory(Path.Combine(docs, @"My Games\Diger"));
        Directory.CreateDirectory(Path.Combine(docs, @"My Games\Test"));
        File.WriteAllText(Path.Combine(docs, @"My Games\Diger\keep.sav"), "baska oyun");
        File.WriteAllText(Path.Combine(docs, @"My Games\Test\save.sav"), "eski kayit");
        SystemOps.CopyDocuments(Path.Combine(target, @"gamepath\CSIDL_MYDOCUMENTS"), log, docs);
        Check(File.ReadAllText(Path.Combine(docs, @"My Games\Diger\keep.sav")) == "baska oyun", "başka oyunun kaydı yerinde");
        Check(File.ReadAllText(Path.Combine(docs, @"My Games\Test\save.sav")) == "save", "oyunun dosyası kopyalandı");
        Check(Directory.GetFiles(Path.Combine(docs, "MetroKafeYedek"), "save.sav", SearchOption.AllDirectories)
            .Select(File.ReadAllText).SingleOrDefault() == "eski kayit", "eski dosya yedeklendi");

        Console.WriteLine("kısayol");
        var lnk = Path.Combine(root, "test.lnk");
        Shortcut.Create(lnk, Path.Combine(target, @"bin\x64\Game!.exe"), "-a 'b'", target, "");
        Check(File.Exists(lnk), "tırnaklı/ünlemli kısayol");

        Console.WriteLine("kurulum durumu");
        var tc = GameConfig.Load(target);
        tc.Installed = true;
        tc.InstallDate = "2026-11-24 10:00";
        tc.SourcePath = game;
        tc.Save();
        tc = GameConfig.Load(target);
        Check(tc.Installed && tc.SourcePath == game && tc.ExtraShortcuts.Count == 1, "Kurulu=1 kaydedildi, diğer alanlar korundu");
        Check(Verifier.Verify(target, null, CancellationToken.None).AllOk, "ini değişince doğrulama bozulmuyor");

        Console.WriteLine("oyun klasörünü bulma");
        var settings = new AppSettings();
        string Exe(string rel)
        {
            var p = Path.Combine(root, "oyunlar", rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, "");
            return p;
        }
        string Name(string exe) => Path.GetFileName(GameLocator.FindGameFolder(exe, settings));
        Check(Name(Exe(@"ets2\bin\win_x64\eurotrucks2.exe")) == "ets2", @"ets2\bin\win_x64 → ets2");
        Check(Name(Exe(@"The Witcher 3\bin\x64\witcher3.exe")) == "The Witcher 3", @"bin\x64 → The Witcher 3");
        Check(Name(Exe(@"UT2004\System\UT2004.exe")) == "UT2004", @"System → UT2004");
        Check(Name(Exe(@"Hades\Hades.exe")) == "Hades", "kökteki exe");
        Directory.CreateDirectory(Path.Combine(root, "oyunlar", @"Hogwarts Legacy\Engine"));
        Check(Name(Exe(@"Hogwarts Legacy\Phoenix\Binaries\Win64\HogwartsLegacy.exe")) == "Hogwarts Legacy", "Unreal: Engine klasörlü üst klasör");
        settings.DetectUnreal = false;
        Check(Name(Exe(@"Hogwarts Legacy\Phoenix\Binaries\Win64\HogwartsLegacy.exe")) == "Phoenix", "Unreal tanıma kapalı");
        settings.SkipFolders.Add("Oyun Dosyalari");
        Check(Name(Exe(@"Ozel\Oyun Dosyalari\a.exe")) == "Ozel", "ayarlardan eklenen klasör");

        Console.WriteLine("ad temizleme");
        Check(GameLocator.CleanName("Euro Truck Simulator 2 [FitGirl Repack]") == "Euro Truck Simulator 2", "[FitGirl Repack]");
        Check(GameLocator.CleanName("Hades (v1.38290)") == "Hades", "(v1.38290)");
        Check(GameLocator.CleanName("Elden Ring - RUNE") == "Elden Ring", "- RUNE");
        Check(GameLocator.CleanName("Red.Dead.Redemption.2") == "Red Dead Redemption 2", "noktalı ad");
        Check(GameLocator.CleanName("ets2") == "ets2", "kısa ad aynı kalır");

        Console.WriteLine("ikon");
        using (var bmp = new Bitmap(300, 200))
        {
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Orange);
            var ico = ImageTools.BuildIco(bmp);
            var icoPath = Path.Combine(root, "test.ico");
            File.WriteAllBytes(icoPath, ico);
            using (var big = ImageTools.Load(ico)) Check(big.Width == 256, "ico 256 boyutu");
            Check(WinIconWidth(icoPath, 256) == 256 && WinIconWidth(icoPath, 32) == 32, "Windows ico'yu 256 ve 32 olarak okuyor");
            using (var small = new Icon(icoPath, 16, 16)) Check(small.Width == 16, "ico 16 boyutu");
            var hiddenIco = Path.Combine(root, "gizli.ico");
            File.WriteAllBytes(hiddenIco, new byte[] { 1 });
            File.SetAttributes(hiddenIco, FileAttributes.Hidden);
            using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); ImageTools.SaveIco(ms.ToArray(), hiddenIco); }
            Check(ImageTools.IsIco(File.ReadAllBytes(hiddenIco)), "gizli eski ikonun üzerine yazıldı");
        }

        var key = AppSettings.Load().SteamGridKey;
        if (key.Length > 0)
        {
            Console.WriteLine("SteamGridDB (canlı)");
            var sg = new SteamGrid(key);
            Check(sg.SearchAsync("ets2").Result.Count == 0, "\"ets2\" sonuç vermiyor (yedek arama gerekli)");
            var games = sg.SearchAsync("Euro Truck Simulator 2").Result;
            Check(games.Count > 0 && games[0].Name == "Euro Truck Simulator 2", "Euro Truck Simulator 2 bulundu");
            var grids = sg.GridsAsync(games[0].Id, 10).Result;
            var icons = sg.IconsAsync(games[0].Id, 10).Result;
            Check(grids.Count == 10, "10 kapak");
            Check(icons.Count > 0, icons.Count + " ikon");
            using (var img = ImageTools.Load(sg.DownloadAsync(grids[0].Url).Result))
                Check(img.Width == 600 && img.Height == 900, "kapak 600x900");
            var coverPath = Path.Combine(root, "MetroKafeCover.png");
            ImageTools.SaveCoverPng(sg.DownloadAsync(grids[1].Url).Result, coverPath);
            Check(File.Exists(coverPath), "kapak PNG kaydedildi");
            foreach (var ic in icons.Take(3))
            {
                var p = Path.Combine(root, "sg.ico");
                ImageTools.SaveIco(sg.DownloadAsync(ic.Url).Result, p);
                using (var i = new Icon(p, 256, 256)) Check(i.Width > 0, "ikon kaydedildi (" + ic.Mime + ")");
            }
        }
        else Console.WriteLine("SteamGridDB testi atlandı (API anahtarı yok)");

        Console.WriteLine(_fail == 0 ? "\nTÜM TESTLER GEÇTİ" : $"\n{_fail} TEST BAŞARISIZ");
        return _fail;
    }
}
