# Metro Kafe Oyun Kurulum

Metro Internet Kafe'de satılan oyunların ISO'dan kurulumu ve kontrolü için tek dosyalık program (`MetroKafe.exe`).
Eski bat dosyalarının (MetroKafeKurulum, Kisayol, MD5, MD5Kontrol, Launcher, RAR, Bat) yerine geçer.

## Nasıl çalışır

Program açıldığında yanındaki `MetroKafe.ini` dosyasına bakar:

| Durum | Açılan ekran |
|---|---|
| `[Durum] Kurulu=0` (ISO içindeki kopya) | **Kurulum ekranı**: disk seçimi (1, 2... + Enter), kopyalama, kayıtlar, kısayollar |
| `[Durum] Kurulu=1` (kurulmuş oyundaki kopya) | **Oyun ekranı**: başlat, dosyaları doğrula, onar, kısayolları yenile, kaldır |
| ini yok | Bilgi ekranı (admin panelinden hazırlanır) |

Sol alttaki sürüm numarasına **3 kez tıklayınca** admin girişi açılır. Varsayılan: `admin` / `12345`
(admin panelinden değiştirilebilir, bilgisayar başına `%ProgramData%\MetroKafe\admin.ini` içinde saklanır).

## Oyun hazırlama (admin paneli → Oyun Hazırla)

1. Programı oyun klasörüne kopyalayıp aç ya da panelden klasörü seç.
2. Oyun adı, exe, ek kısayollar vb. doldur.
3. **Hazırla**: `MetroKafe.ini` yazılır, program klasöre `MetroKafe.exe` olarak kopyalanır, MD5 listesi çıkarılır,
   istenirse ISO oluşturulur (Windows'un kendi IMAPI2 bileşeniyle, UDF, 4 GB üstü dosyalar desteklenir).

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
