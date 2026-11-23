# Metro Kafe Oyun Kurulum

Metro Internet Kafe'de satılan oyunların ISO'dan kurulumu ve kontrolü için tek dosyalık program (`MetroKafe.exe`).
Eski bat dosyalarının (MetroKafeKurulum, Kisayol, MD5, MD5Kontrol, Launcher, RAR, Bat) yerine geçer.

## Nasıl çalışır

Program açıldığında yanındaki `MetroKafe.ini` dosyasına bakar:

| Durum | Açılan ekran |
|---|---|
| `[Durum] Kurulu=0` (ISO içindeki kopya) | **Kurulum ekranı**: disk seçimi (1, 2... + Enter), kopyalama, kayıtlar, kısayollar |
| `[Durum] Kurulu=1` (kurulmuş oyundaki kopya) | **Oyun ekranı**: başlat, dosyaları doğrula, onar, kısayolları yenile, kaldır |
| ini yok (kendi klasöründe) | **Hazırlama aracı**: oyun hazırlama, ayarlar, güncelleme |

Kurulum/doğrulama ekranları kendini yönetici olarak yeniden başlatır (Defender, kayıt defteri için).
Hazırlama aracı yönetici izni istemez; Windows, yönetici olarak çalışan pencerelere Explorer'dan sürükle-bırakı engeller.

Kurulum/doğrulama ekranında sol alttaki sürüm numarasına **3 kez tıklayınca** admin girişi açılır.
Varsayılan: `admin` / `12345` (değiştirilebilir, `%AppData%\MetroKafe\admin.ini` içinde saklanır).

## Oyun hazırlama

1. `MetroKafe.exe`'yi kendi klasöründe aç.
2. Oyunun exe dosyasını pencereye **sürükle-bırak** (veya kutuya tıklayıp seç, ya da exe'yi `MetroKafe.exe`'nin üzerine bırak).
   - Oyun klasörü: exe `bin`, `win_x64`, `x64`, `Binaries` gibi klasörlerdeyse yukarı çıkılır
     (`ets2\bin\win_x64\eurotrucks2.exe` → `ets2`). Liste **Ayarlar**'dan değiştirilebilir.
   - Unreal oyunlarında (`Oyun\Proje\Binaries\Win64\x.exe`) yanında `Engine` klasörü olan üst klasör alınır.
   - Klasör adındaki `[FitGirl]`, `(v1.2)`, ` - RUNE` gibi ekler temizlenir.
3. **SteamGridDB**: oyun otomatik aranır (önce klasör adı, bulunamazsa exe'nin ürün adı, sonra exe adı).
   10 adet 2:3 kapak ve 10 ikon gösterilir; tıklayıp seç. Ad, SteamGridDB'deki adla doldurulur.
4. **Hazırla**: seçilen kapak `MetroKafeCover.png`, ikon `ico.ico` (256/64/48/32/16) olarak yazılır, `MetroKafe.ini`
   oluşturulur, program klasöre `MetroKafe.exe` olarak kopyalanır, MD5 listesi çıkarılır, istenirse ISO oluşturulur
   (Windows'un kendi IMAPI2 bileşeniyle, UDF, 4 GB üstü dosyalar desteklenir).

### Ayarlar

`%AppData%\MetroKafe\ayarlar.ini` içinde saklanır: atlanacak klasör adları, Unreal tanıma, SteamGridDB API anahtarı,
kapak/ikon adedi. API anahtarı programın veya bu deponun içinde **yoktur**; ilk kullanımda Ayarlar'dan girilir
(steamgriddb.com → Preferences → API).

### MetroKafe.ini örneği

```ini
[Oyun]
Ad=Grand Theft Auto V
Exe=GTA5 MetroKafe Mod.exe
Arguman=
Ikon=ico.ico
Kapak=MetroKafeCover.png

[Kurulum]
HedefKlasor=MetroKafeOyun
BelgelerKaynak=gamepath\CSIDL_MYDOCUMENTS
RegDosyasi=MetroKafeReg.reg
DefenderIstisnasi=1

; Ek masaüstü kısayolları (isteğe bağlı)
[Kisayol2]
Ad=Half-Life Opposing Force
Hedef=Play_OF.bat
Arguman=
Ikon=ico3.ico

[Durum]
Kurulu=0
KurulumTarihi=
KaynakYol=
```

Kurulum hedefi: `<disk>:\MetroKafeOyun\<Oyun adı>`.

## Güncelleme

Program açılışta güncelleme **kontrol etmez**. Admin paneli → Güncelleme → "Güncellemeleri Kontrol Et" ile
GitHub Releases'teki son sürüme bakılır; "Güncelle" exe'yi indirip kendini değiştirir ve yeniden başlar.

### Yeni sürüm yayınlama

```
yayinla.bat
```

Sürüm numarasını ve değişiklik notunu sorar; sürümü yükseltir, derler, GitHub'a gönderir ve Release oluşturur.

## Geliştirme

- .NET Framework 4.8 (Windows 10/11'de hazır gelir), WinForms, ek paket yok.
- Derleme: `dotnet build src\MetroKafe -c Release` → `src\MetroKafe\bin\Release\net48\MetroKafe.exe`
- Testler: `dotnet run --project tests\MetroKafe.Tests -c Release` (kopyalama, MD5, ISO, onarım, Belgelerim)
