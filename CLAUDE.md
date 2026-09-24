# CLAUDE.md — ApartmanAidatTakip (Apartman/Site Aidat Takip Sistemi)

Bu dosya projenin mimarisini, veri tabanı yapısını, iş akışını ve dosya tasarımını tek yerde toplar. Amaç: aynı yerlere tekrar tekrar bakmadan geliştirmeye devam edebilmek. Tüm domain terimleri **Türkçe**'dir.

> Geliştirici: Buğra Öztürk · Dil: Türkçe UI · Model: ASP.NET MVC 5 (.NET Framework 4.7.2)

---

## 1. Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| Framework | ASP.NET MVC 5 (`Microsoft.AspNet.Mvc` 5.2.7), Razor (.cshtml) |
| Veri erişimi | Entity Framework 6 **Database First** — model `Models/Model1.edmx`'ten (T4 `.tt`) üretilir |
| Veri tabanı | Microsoft SQL Server. Connection string adı `AptVTEntities`; yerel: `data source=BUGRA;initial catalog=aptmhs25122025` |
| PDF | iTextSharp 5 |
| Excel | ClosedXML + EPPlus |
| Frontend | Bootstrap, jQuery 3.4.1, NiceAdmin teması (`Content/Admin/assets`: apexcharts, echarts, quill, tinymce, simple-datatables, boxicons, remixicon) |

### Kritik kurallar
- **Üretilen dosyaları elle düzenleme:** `Model1.Context.cs`, `Model1.Designer.cs`, `Model1.cs`. Değişiklik gerekiyorsa EDMX'ten yeniden üret.
- **Yeni `.cs` / `.cshtml` dosyaları UTF-8 (BOM'lu) kaydedilmeli**, yoksa Türkçe karakterler bozulur.
- **Mevcut açık (light) tasarım hiç bozulmadan** korunur; dark mode ayrı katman olarak eklenmiştir.
- Yalnızca `Content/Admin/assets` okunur; `Content/Admin` altındaki boş şablon HTML dosyaları atlanır.
- Her controller kendi `AptVTEntities db = new AptVTEntities();` alanını açar (DI yok).

---

## 2. Proje Düzeni

```
ApartmanAidatTakip.sln
└── ApartmanAidatTakip/
    ├── App_Start/        RouteConfig (default controller = AnaSayfa/Index), BundleConfig, FilterConfig
    ├── Controllers/      İş mantığı (aşağıda)
    ├── Models/           EF entity'leri + DB view entity'leri (*View) + Model1.edmx
    ├── Views/            Admin/ AnaSayfa/ Ayarlar/ Daire/ Makbuz/ Raporlar/ TopluMakbuz/ Shared/
    ├── Scripts/ Content/  İstemci varlıkları
    └── Web.config        Connection string + ayarlar
```

Shared layout'lar: `_Layout`, `_AdminLayout`, `_DaireLayout`.

---

## 3. Veri Tabanı Yapısı

EF Database First. Aşağıdaki tablolar `Models/*.cs` entity'lerinden birebir çıkarılmıştır. Neredeyse tüm tablolar `BinaID` ile bir binaya (tenant) bağlıdır — **tüm sorgular giriş yapılan binaya göre filtrelenir**.

### 3.1 Ana tablolar

**Binalar** — bina/site kaydı (tenant kökü)
| Kolon | Tip | Not |
|-------|-----|-----|
| BinaID | int PK | |
| BinaAdi, Adres, VergiNo | string | |
| DaireSayisi | int? | |
| Durum | string | soft-delete / aktiflik |
| SozlesmeBaslamaTarihi, SozlesmeBitisTarihi | datetime? | lisans/sözleşme süresi |
| BinaKullaniciAdi | string | |
| MakbuzOnayKaldir, YoneticiAidatEkleme | bool? | Ayarlar bit bayrakları |

**Daireler** — daire/kullanım birimi
| Kolon | Tip | Not |
|-------|-----|-----|
| DaireID | int PK | |
| DaireNo, BinaID | int? | |
| AdSoyad, Telefon, TC | string | asıl sakin |
| DaireDurum, YonetimdeMi, Aciklama | string | |
| Borc | decimal? | güncel bakiye borç (`borcduzenle` ile hesaplanır) |

**Kullanicilar** — yönetici/panel kullanıcıları
| Kolon | Tip | Not |
|-------|-----|-----|
| KullaniciID | int PK · BinaID int? | |
| KullaniciAdi, AdSoyad, Parola, Telefon | string | Parola MD5 hash |
| Yetki | string | `"1"` = admin/superadmin |
| Durum | string | |

### 3.2 Aidat / Ek / Dönem

**Aidat** — dönemsel aidat borcu: `AidatID PK`, `AidatAy` (string), `AidatYil` (int?), `AidatTutar` (decimal?), `DaireNo`, `BinaID`, `Durum`, `ZamEklendiMi` (gecikme zammı işlendi mi).

**Ek** — daireye ek/ekstra ücret: `EkID PK`, `EkAy`, `EkYil`, `EkTutar`, `DaireNo`, `BinaID`, `Durum`.

**Kasa** — bina bazlı aylık dönem/kasa satırı: `KasaID PK`, `KasaAy`, `KasaYil`, `KasaAidat`, `KasaEk`, `KasaToplam`, `BinaID`, `AyKodu`. Bir dönemin açılıp açılmadığı bu tablodan `DonemEklendiMi()` ile kontrol edilir.

**AcilisBakiye** — bina açılış bakiyesi: `AcilisBakiyeID PK`, `BinaID`, `EkTutar`, `AidatTutar`, `ToplamTutar`.

**PesinOdemeler** — daire bazlı peşin yıl ödemesi: `ID PK`, `DaireID`, `Yil`, `BinaID`.

### 3.3 Tahsilat / Gider / Makbuz

**Makbuz** — makbuz başlığı: `MakbuzID PK`, `MakbuzNo`, `BinaID`, `DaireID`, `MabuzTutar` (dikkat: kolon adı yazım olarak "Mabuz"), `MakbuzTarihi`, `Aciklama`, `Durum`, `OnayliMi` (bool?).

**MakbuzSatir** — makbuz satırı (aidat/ek kalemleri): `MakbuzSatirID PK`, `MakbuzID` (FK), `AyAdi`, `YilAdi`, `Tutar`, `DaireID`, `BinaID`, `Durum`, `EkMiAidatMi` (satırın Ek mi Aidat mı olduğunu ayırır).

**Tahsilat** — genel tahsilat/gelir kaydı: `TahsilatID PK`, `TahsilatAciklama`, `TahsilatTutar`, `TahsilatTarih`, `BinaID`, `Durum`, `TahsilatNo`, `DemirbasMi` (bool?).

**Gider** — gider kaydı: `GiderID PK`, `GiderAciklama`, `GiderTuruID` (FK), `GiderTutar`, `GiderTarih`, `BinaID`, `Durum`, `GiderNo`.

**GiderTuru** — gider tipi sözlüğü: `GiderTuruID PK`, `GiderTuruAdi`, `BinaID`.

### 3.4 Yardımcı tablolar
- **Hareketler** — denetim/işlem günlüğü: `HareketID PK`, `BinaID`, `KullaniciID`, `OlayAciklama`, `Tarih`, `Tur`.
- **Duyurular** — duyurular: `ID PK`, `Baslik`, `Aciklama`, `Tarih`, `Durum`.
- **Notlar** — bina notları: `ID PK`, `Aciklama`, `BinaID`, `BorcAciklama`.
- **DigerDaireSakinleri** — bir dairenin ek sakinleri: `ID PK`, `DaireID`, `AdSoyad`, `Telefon`, `DaireDurum`, `TC`.

### 3.5 Read-only DB View'ları (`*View` entity'leri)
Liste ve rapor ekranlarını besleyen, SQL view'larına eşlenmiş salt-okunur entity'ler:
`AidatView`, `EkView`, `GiderView`, `HareketView`, `KasaView`, `MakbuzView`, `MakbuzSatirView`, `KullanicilarView`, `TahsilatView`, `PesinOdemelerView`.
Bunlara yazılmaz; join'li/özet verileri hazır sunarlar (ör. `KullanicilarView` giriş doğrulamasında kullanılır).

### 3.6 Numara sıralama (belge no) mantığı
`MakbuzNo`, `GiderNo`, `TahsilatNo` uygulama tarafından yönetilir; silme/ekleme sonrası `MakbuzNoDuzenle()` / `GiderNoDuzenle()` / `TahsilatNoDuzenle()` ile yeniden sıralanır.

---

## 4. Controller'lar ve Sorumluluklar

| Controller | Rol |
|-----------|-----|
| **AnaSayfa** (~2900 satır) | Ana yönetici uygulaması: aidat (DaireBorclandir, DonemEkle, AidatDuzenle/Sil), ekler (Ek*), giderler (Giderler, GiderEkle/Guncelle/Sil, GiderMakbuz), tahsilatlar (Tahsilat*), sakinler (Sakinler, SakinEkle, SakinEkleExcel, SakinDuzenle, EkSakinEkle/Sil), açılış bakiyesi, notlar, peşin ödemeler, borçlu daireler (+PDF/Excel), daire sorgu (DaireSorgu), gecikme zammı (GecikmeZammı) |
| **Admin** | Superadmin paneli: Binalar ve Kullanicilar yönetimi, soft-delete + geri alma (BinaSil/BinaGeriAl/BinaTamamenSil, Kullanici eşdeğerleri), Duyurular, binalar arası Hareketler, şifre değişimi. Google Authenticator + rate limit + şifre sıfırlama içerir |
| **Makbuz** | Tek makbuz yaşam döngüsü: Olustur/Ekle, satır ekleme (AidatSatirEkle, EkSatirEkle), SatirCikar, MakbuzSil, GeneratePdf, Ara |
| **TopluMakbuz** | Bir daire için seçili aidat/eklerden toplu makbuz üretimi |
| **Raporlar** | Her raporun ekran + `*PDF` aksiyonu: GelirGider, DetayliGelirGider, DenetciRaporu, TureGoreGiderler, DevirBakiyeleri, TureGoreGelirGiderTarihBazli |
| **Daire** | Sakin (kiracı) tarafı: giriş + kendi makbuzlarını görüntüleme |
| **Ayarlar** | Bina ayarları: `AyarlariGuncelle` `Binalar` üzerindeki bit bayraklarını (MakbuzOnayKaldir, YoneticiAidatEkleme) JSON ile açar/kapatır |
| **MobilApi** | Tek `[HttpPost] GetBorcluDaireler` JSON ucu; auth = **sabit paylaşılan şifre** (`bugraozturk1905`), cookie/session değil |

---

## 5. Kimlik Doğrulama Modeli (ÜÇ ayrı şema)

1. **Yönetici uygulaması** (AnaSayfa, Ayarlar, Makbuz, Raporlar, TopluMakbuz) — **cookie tabanlı**.
   `AnaSayfa.Login`, `KullanicilarView`'e karşı doğrular (parola `Crypto.Hash(Parola,"MD5")`), sonra **şifresiz `KullaniciBilgileri` HttpCookie**'sine KullaniciID, BinaID, BinaAdi, KullaniciAdi, Parola, LisansTarih vb. yazar. Alt kod `Request.Cookies["KullaniciBilgileri"].Values["BinaID"]` okuyarak tüm sorguları binaya göre daraltır. Cookie 1 gün (beni hatırla: 365 gün).
2. **Admin paneli** (AdminController) — **Session tabanlı**. `Admin.Login`, `Kullanicilar`'ı `Yetki=="1"` ile kontrol eder ve `Session["AdminID"]` / `Session["KullaniciAdi"]` atar. Google Authenticator 2FA + rate limit destekli.
3. **MobilApi** — sabit şifre (yukarıda).

> Güvenlik notu (mevcut durum, istenmedikçe "düzeltilmez"): parolalar MD5; auth cookie düz metin; mobil API sabit şifre. Bunlar kod tabanının olduğu gibi gerçekleridir.

---

## 6. Ortak Yardımcı Metotlar (her controller'da KOPYALANMIŞ)

Bunlar merkezi değildir — ihtiyaç duyan her controller'a kopyalanmıştır. **Birini değiştirirken kardeşlerinin de aynı düzeltmeye ihtiyacı var mı diye bak.**

- `Sabit()` — ortak ViewBag verisi (kalan lisans günü `KalanGun`/`Percent`, aktif `Duyurular`) cookie'deki BinaID'den; çoğu GET aksiyonunun başında çağrılır.
- `DonemEklendiMi()` — geçerli ayın döneminin (BinaID+yıl+ay için Kasa satırı) var olup olmadığını kontrol eder; `ViewBag.DonemSorgu` atar.
- `MakbuzNoDuzenle()` / `GiderNoDuzenle()` / `TahsilatNoDuzenle()` — belge numaralarını yeniden sıralar.
- `borcduzenle(int DaireID)` — makbuz/aidat değişimi sonrası dairenin `Borc` değerini yeniden hesaplar.
- `bosmakbuzsil()` — boş makbuzları siler.

---

## 7. Temel İş Akışları

**Dönem açma (aylık):** Yönetici `DonemEkle` ile ilgili ay/yıl için `Kasa` satırı oluşturur → `DaireBorclandir` her daireye o dönemin `Aidat` (ve varsa `Ek`) kaydını yazar → `Daireler.Borc` güncellenir.

**Tahsilat / Makbuz kesme:** `Makbuz.Olustur` başlık açar → `AidatSatirEkle`/`EkSatirEkle` ile `MakbuzSatir` kalemleri eklenir (`EkMiAidatMi` ayırır) → makbuz tutarı toplanır, `borcduzenle` ile dairenin borcu düşer → `MakbuzOnayKaldir` ayarına göre `OnayliMi` durumu. `TopluMakbuz` bunu bir dairenin birden çok borcu için tek seferde yapar.

**Gider:** `GiderEkle` bir `GiderTuru`'ne bağlı `Gider` yazar; gelir-gider raporlarında `Tahsilat` (gelir) ile karşılaştırılır.

**Raporlama:** Her Raporlar aksiyonu ekranda gösterir; eş `*PDF` aksiyonu iTextSharp ile PDF, borçlu listeleri ClosedXML/EPPlus ile Excel üretir.

---

## 8. Konvansiyonlar

- **Çift gönderim koruması:** Kayıt oluşturan formlarda submit butonu kilitlenerek çift kayıt önlenir.
- **Dark mode:** Ayrı katman olarak eklenir; mevcut light tasarım asla değiştirilmez.
- Route: tek default route, varsayılan controller `AnaSayfa`, action `Index`.
- Çok kiracılı (multi-tenant) izolasyon tamamen `BinaID` filtresine dayanır — yeni sorgu yazarken BinaID filtresini atlamayın.

### Performans kuralları
- **Döngü içinde `SaveChanges()` çağırma.** `foreach` içinde her kayıt için ayrı `SaveChanges()`, satır sayısı kadar DB gidiş-dönüşü demektir. Değişiklikleri EF'e biriktir, döngü bittikten sonra **tek** `SaveChanges()` çağır. Aynı şekilde tek bir aksiyonda ardışık `SaveChanges()`'leri birleştir — hem hızlı hem atomik olur.
- **Salt-okunur sorgulara `AsNoTracking()` ekle, yazma sorgularına asla.** Yalnızca ekrana basılıp sonradan değiştirilmeyen listelere (ViewBag'e atanan `*View` ve liste sorguları) `AsNoTracking()` uygulanır; EF change-tracking yükünü kaldırır. Kaydı çekip üzerinde değişiklik yapıp `SaveChanges()` edilen sorgulara **eklenmez**, yoksa güncelleme kaydedilmez.
- **Nadir değişen global veriyi cache'le ve değiştiren yerde temizle.** `Duyurular` gibi seyrek değişen veri her sayfa açılışında sorgulanmaz; `HttpRuntime.Cache` ile kısa süreli (ör. 5 dk) cache'lenir. Veriyi değiştiren aksiyonda (ekleme/pasife alma/aktife alma) `Cache.Remove(...)` ile cache boşaltılır ki değişiklik anında yansısın.
