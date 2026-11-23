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

        Console.WriteLine(_fail == 0 ? "\nTÜM TESTLER GEÇTİ" : $"\n{_fail} TEST BAŞARISIZ");
        return _fail;
    }
}
