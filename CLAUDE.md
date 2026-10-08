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
- **Git Commit Yasaktır:** Bu projede hiçbir zaman `git commit` veya otomatik commit komutu atılmamalıdır.
- **`.csproj` Kaydı:** Yeni bir sayfa, component veya kod dosyası oluşturulduğunda/eklendiğinde, bu dosya mutlaka `ApartmanAidatTakip/ApartmanAidatTakip.csproj` dosyasına (örn. `<Content Include="..." />` veya `<Compile Include="..." />`) kaydedilmelidir. Aksi halde `publish` (yayınlama) aşamasında projeye dahil edilmemektedir.
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

**PesinOdemeler** — daire bazlı peşin **yıl** ödemesi: `ID PK`, `DaireID`, `Yil`, `BinaID`. Bir dairenin tüm yılı peşin ödediği durum; dönem eklenirken o daireye o yıl hiç aidat eklenmez (atlanır).

**AylikPesinOdemeler** — daire bazlı peşin **ay** ödemesi: `ID PK`, `DaireID`, `Yil`, `Ay` (1–12), `BinaID`. Seçili ayları peşin ödeme; **her ödenen ay için bir satır**. EDMX'e elle eklendi (SSDL+CSDL+mapping); entity `Models/AylikPesinOdemeler.cs`, DbSet `AylikPesinOdemelers`, tablo `AylikPesinOdemeler_tablo.sql`. Ekrana taşırken daire+yıl bazında gruplayan salt-okunur DTO `Models/AylikPesinOdemeListe.cs` kullanılır (DB view değil). **Yıllık peşinden farkı:** dönem eklenirken o aylara aidat **yine eklenir** (ama borç artmaz) ve karşılığında anında ödenmiş/onaylı makbuz kesilir.

### 3.3 Tahsilat / Gider / Makbuz

**Makbuz** — makbuz başlığı: `MakbuzID PK`, `MakbuzNo`, `BinaID`, `DaireID`, `MabuzTutar` (dikkat: kolon adı yazım olarak "Mabuz"), `MakbuzTarihi`, `Aciklama`, `Durum`, `OnayliMi` (bool?).

**MakbuzSatir** — makbuz satırı (aidat/ek kalemleri): `MakbuzSatirID PK`, `MakbuzID` (FK), `AyAdi`, `YilAdi`, `Tutar`, `DaireID`, `BinaID`, `Durum`, `EkMiAidatMi` (satırın Ek mi Aidat mı olduğunu ayırır).

**Tahsilat** — genel tahsilat/gelir kaydı: `TahsilatID PK`, `TahsilatAciklama`, `TahsilatTutar`, `TahsilatTarih`, `BinaID`, `Durum`, `TahsilatNo`, `DemirbasMi` (bool?).

**Gider** — gider kaydı: `GiderID PK`, `GiderAciklama`, `GiderTuruID` (FK), `GiderTutar`, `GiderTarih`, `BinaID`, `Durum`, `GiderNo`.

**GiderTuru** — gider tipi sözlüğü: `GiderTuruID PK`, `GiderTuruAdi`, `BinaID`.

**SabitGider** — sabit (her ay tekrar eden) gider şablonu: `SabitGiderID PK`, `GiderAciklama` (nvarchar 500), `GiderTuruID`, `GiderTutar` (decimal 18,2), `BinaID`, `Durum` (`A`/`P` soft-delete). DB'ye yazılır; bir **şablondur**, gerçek gider değildir — `SabitGiderOlustur` ile `Gider` kaydına kopyalanır. EDMX'e elle eklendi (SSDL+CSDL+mapping); entity `Models/SabitGider.cs`, DbSet `SabitGiders`. Ekrana taşırken tür adıyla join'li salt-okunur DTO `Models/SabitGiderListe.cs` kullanılır (DB view değil).

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
| **AnaSayfa** (~3000 satır) | Ana yönetici uygulaması: aidat (DaireBorclandir, **TopluGecmisBorclandir**, DonemEkle, **DonemIptal**, AidatDuzenle/Sil, **AidatTopluSil**), ekler (Ek*, **EkTopluSil**), giderler (Giderler, GiderEkle/Guncelle/Sil, GiderMakbuz), sabit giderler (SabitGiderEkle/Guncelle/Sil, SabitGiderOlustur), tahsilatlar (Tahsilat*), sakinler (Sakinler, SakinEkle, SakinEkleExcel, SakinDuzenle, EkSakinEkle/Sil), açılış bakiyesi, notlar, peşin ödemeler, borçlu daireler (+PDF/Excel), daire sorgu (DaireSorgu), **daire ödeme durumu takvimi (DaireOdemeDurumu)**, gecikme zammı (GecikmeZammı) |
| **Admin** | Superadmin paneli: Binalar ve Kullanicilar yönetimi, soft-delete + geri alma (BinaSil/BinaGeriAl/BinaTamamenSil, Kullanici eşdeğerleri), Duyurular, binalar arası Hareketler, şifre değişimi. Google Authenticator + rate limit + şifre sıfırlama içerir |
| **Makbuz** | Tek makbuz yaşam döngüsü: Olustur/Ekle, satır ekleme (AidatSatirEkle, EkSatirEkle), SatirCikar, MakbuzSil, GeneratePdf, Ara |
| **TopluMakbuz** | Bir daire için seçili aidat/eklerden toplu makbuz üretimi |
| **Raporlar** | Her raporun ekran + `*PDF` aksiyonu: GelirGider, DetayliGelirGider, DenetciRaporu, TureGoreGiderler, **YillikTureGoreGiderler**, DevirBakiyeleri, TureGoreGelirGiderTarihBazli |
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
- `borcduzenle(int DaireID)` — makbuz/aidat değişimi sonrası dairenin `Borc` değerini yeniden hesaplar (`Durum=="A"` aidat + ek toplamı).
- `bosmakbuzsil()` — boş makbuzları siler.
- `DonemKasaOlustur(yil, ay, BinaID, acilis)` (AnaSayfa'ya özel, private) — verilen ay/yıl için devir bakiyesini (önceki ay kasası/açılış bakiyesi + önceki ayın tahsilat/makbuz gelirleri − giderleri) hesaplayıp o aya ait `Kasa` satırını oluşturur. `DonemEkle`'nin çok-aylı backfill'inde her ay için çağrılır; devir zinciri için kendi içinde `SaveChanges` yapar.

---

## 7. Temel İş Akışları

**Dönem açma (aylık):** Yönetici `DonemEkle` ile ilgili ay/yıl için `Kasa` satırı oluşturur → `DaireBorclandir` her daireye o dönemin `Aidat` (ve varsa `Ek`) kaydını yazar → `Daireler.Borc` güncellenir.

- **Aidat tutarı önerisi (DonemEkle GET):** Aidat kutusuna, en son açılan dönemin aidat kayıtlarından **en sık tekrar eden tutar (mod)** öneri olarak basılır (`ViewBag.OnerilenAidat`) — tek bir düşük daire tutarının yanlış referans olmasını önler. View'de kutuya yazıldıkça **binlik ayracı nokta** uygulanır (`formatThousands`); submit öncesi noktalar temizlenip (`stripSeparators`) sunucuya ham tam sayı gider. **Demirbaş/Ek kutusuna öneri yapılmaz**, boş kalır (değişken ve zorunlu olmayan alan).
- **Atlanan ara ayları doldurma (DonemEkle POST):** Hedef ay yine içinde bulunulan ay olmalıdır; ancak en son `Kasa` dönemi ile hedef ay arasında atlanan aylar varsa (ör. son=Mayıs, hedef=Ekim → Haziran…Ekim) **tüm eksik aylar** sırayla borçlandırılır. Her ay için `DonemKasaOlustur` ile Kasa (devir) oluşturulur ve daireler **girilen aidat tutarıyla** borçlandırılır (peşin ödeyen + yönetici muafiyeti her ay için geçerli; peşin ödeme yıl bazlıdır). **Ek/Demirbaş yalnızca hedef aya** eklenir, ara aylara eklenmez. Tüm işlem tek transaction; aidat/borç toplu tek `SaveChanges`.

**Aylık peşin ödeme (ay bazlı):** `PesinOdemeler` ekranında yıllık peşinin altında ayrı bir **"Aylık Peşin Ödemeler"** bölümü vardır. Yönetici bir daire için o yılın **seçili aylarını** peşin işaretler (`AylikPesinOdemeEkle`; her ay için bir `AylikPesinOdemeler` satırı). Kayıt **düzenlenebilir** (`AylikPesinOdemeGuncelle` — eski satırları silip yeni seçimi yazar) ve silinebilir (`AylikPesinOdemeSil`). View'de ay seçimi ızgara checkbox'larla yapılır; seçilen aylar **ardışık değilse** (arada ay atlanmışsa) SweetAlert ile uyarı verip çift onay ister (`aylikSubmitKontrol`). `DonemEkle` o aya geldiğinde: yıllık peşinli daire atlanır (aidat yok); **aylık** peşinli daire için aidat **`Durum="P"` olarak yine eklenir** (borç artmaz), o dairenin tüm peşin ayları **tek makbuzda** (`OnayliMi=true`, `MakbuzTarihi=bugün`, `EkMiAidatMi="A"`) toplanır. Aylık peşinli daireler döngüde **önce** işlenir; makbuz satırları döngü dışında tek `SaveChanges` ile yazılıp `MakbuzNoDuzenle` çağrılır.

**Dönem iptali (son dönemi sıfırlama — `DonemIptal`):** Yanlış tutarla açılan **yalnızca en son açılan dönemi** (Kasa'daki en büyük yıl+ay) geri alır; daha eski aylar iptal edilemez. **Güvenlik:** yöneticinin parolası (`Crypto.Hash(…,"MD5")` ile karşılaştırılır) + 2FA açıksa Google Authenticator kodu (`TwoFactorHelper.ValidateCode`) zorunlu; doğrulama başarısızsa hiçbir şey silinmez. Tek transaction içinde o ay/yıla ait **Makbuz + MakbuzSatir, Gider, Tahsilat, Aidat, Ek ve Kasa** kayıtları silinir, `MakbuzNoDuzenle`/`GiderNoDuzenle`/`TahsilatNoDuzenle` ile belge noları yeniden sıralanır, tüm daire borçları yeniden hesaplanır, `Hareketler`'e log yazılır. **Kritik:** Makbuz satırları silinmeden önce, iptal edilen dönem **dışındaki** bir aidat/ek'i kapatan satırların ilgili `Aidat`/`Ek` kaydı tekrar `Durum="A"` yapılır (geçmiş dönem ödemesinin borç yeniden hesapta geri doğması için) — iptal edilen dönemin kendi aidat/ek'i zaten tamamen silindiği için ona dokunulmaz. View'de SweetAlert ile çift onay; form `onsubmit="return false"` ile Enter'a basınca onaysız/spam gönderim engellenir (programatik `form.submit()` çalışmaya devam eder).

**Toplu geçmişe dönük borçlandırma (`TopluGecmisBorclandir`):** Ayrı bir ekranda seçilen **ay/yıl** için birden çok daireyi tek seferde borçlandırır — bireysel `DaireBorclandir` gibi **Kasa/dönem oluşturmaz**, sadece daire bazlı `Aidat`/`Ek` + `Daireler.Borc` yazar. Her dairenin yanında checkbox + ayrı **tutar** + ayrı **tür (Aidat/Demirbaş)** vardır; üstteki **"Toplu Doldur"** kutusu seçili tüm satırlara tutar+tür basar (sonradan tek tek değiştirilebilir). View'de **shift ile aralık seçme** desteklenir. Yalnızca işaretli ve tutarı >0 olan satırlar işlenir. **Geçmiş serbest, gelecek ay engellidir** (amaç geçmişe dönük/atlanan ayları doldurmak). POST parallel array ile gelir (`int[] DaireNolar, string[] Tutarlar, string[] Turler`, `string AidatAy, int AidatYil`); tek transaction + döngü dışında **tek `SaveChanges`**. **Mükerrer yönetimi (sıralı ek numara):** o ay/yıl için (ödenmiş/ödenmemiş fark etmez) daha önce **herhangi** bir `Aidat`/`Ek` kaydı varsa, iki ayrı "Mayıs 2026" karışmasın diye bütün parti tek tip olacak şekilde ay adına sıralı numara eklenir (düz `"Mayıs"`=1 sayılır → `"Mayıs - 2"`, tekrar `"Mayıs - 3"`). Hiç kayıt yoksa düz ay adı kullanılır. Numara aidat+ek için **ortak** hesaplanır (tek tiplik). Menü: **Yönetim İşlemleri → Toplu Geçmiş Borçlandır**.

**Toplu aidat/ek silme (`AidatTopluSil` / `EkTopluSil`):** `EklenenAidatlar` ve `EklenenEkler` listelerinde sol checkbox ile seçili kayıtları toplu soft-delete eder (`Durum="S"`). **Makbuzu olan kayıtlar atlanır** (makbuz satırları `AyAdi|YilAdi|DaireID` anahtarıyla tek sorguda `HashSet`'e çekilir, döngü içinde sorgu yok). Etkilenen dairelerin borcu yeniden hesaplanır; tek transaction + toplu `SaveChanges` + `Hareketler` log. POST `int[] ids` alır. Sonuç mesajı "X silindi (Y makbuzlu atlandı)" biçimindedir.

**İstemci tarafı liste deseni (EklenenAidatlar / EklenenEkler / Sakinler):** Bu üç listede tema `.datatable` (simple-datatables) **kullanılmaz** — çünkü sayfa dışı satırları DOM'dan kaldırıp toplu seçimi bozar. Yerine view içinde kendi hafif **arama + sayfalama + (gerekli yerde) checkbox seçimi** JS'i kuruludur: tüm satırlar DOM'da kalır, sadece `display:none` ile gizlenir (böylece farklı sayfalardaki seçimler bile silme formuna dahil olur). Arama satır `data-search` attribute'una göre yapılır; `.row-check` tıklamalarında **shift ile aralık seçme** vardır; sayfa başına seçimi + "« 1 2 3 »" sayfalama + "X-Y / toplam" bilgisi bulunur. **Sakinler:** sadece arama+sayfalama (checkbox yok), arama **yalnızca daire no + ad soyad + Ev Sahibi/Kiracı** üzerinden (telefon/TC/borç hariç), sayfa başına 10/20/30/40/50 (varsayılan 10). **EklenenAidatlar/EklenenEkler:** checkbox + toplu sil dahil, sayfa başına 25/50/100/250/Tümü (varsayılan 100). Her sayfanın kendi `<style>` bloğunda `html.dark-mode` override'ları vardır (light tasarım korunur).

**Tahsilat / Makbuz kesme:** `Makbuz.Olustur` başlık açar → `AidatSatirEkle`/`EkSatirEkle` ile `MakbuzSatir` kalemleri eklenir (`EkMiAidatMi` ayırır) → makbuz tutarı toplanır, `borcduzenle` ile dairenin borcu düşer → `MakbuzOnayKaldir` ayarına göre `OnayliMi` durumu. `TopluMakbuz` bunu bir dairenin birden çok borcu için tek seferde yapar.

**Gider:** `GiderEkle` bir `GiderTuru`'ne bağlı `Gider` yazar; gelir-gider raporlarında `Tahsilat` (gelir) ile karşılaştırılır. `GiderEkle` sonrası, eklenen giderin ID'si `TempData["YeniGiderMakbuzID"]` ile view'a taşınır ve makbuz PDF'i yeni sekmede otomatik açılır.

**Sabit gider:** Giderler ekranında `SabitGider` şablonları listelenir (eklenir/güncellenir/soft-delete edilir). Her satırdaki **"Gider Makbuzu Oluştur"** butonu `SabitGiderOlustur` ile şablonu sıralı `GiderNo` + bugünün tarihiyle gerçek bir `Gider` kaydına kopyalar (Hareket log'u yazar), dönem kapalıysa engeller, sonra makbuz PDF'ine yönlendirir (yeni sekmede). DB tablosu `SabitGider_tablo.sql` ile oluşturulur.

**Makbuz PDF içeriği (GeneratePdf — MakbuzController + DaireController'da KOPYALANMIŞ):** Üst blok iki sütunlu tablo: solda **Daire No / Ad Soyad / Toplam Borç / NOT** (ve varsa `Notlar.BorcAciklama`), sağ köşede **KAŞE - İMZA**. Tutarlar `tr-TR` kültürüyle `#,##0.##` (binlik nokta ayracı, ör. `1.000 TL`). Tablonun altında, **VUK notunun ("Bu belge 213 sayılı...") hemen üstünde**, tam genişlikte, **altı çizili** ve küçük punto (9pt) ile **"Ödenmeyen Dönemler"** satırı: dairenin açık (`Durum="A"`) `Aidat` + `Ek` kayıtlarından `Yıl - Ay Tutar TL` biçiminde yan yana; demirbaşlar `(Demirbaş)` ile işaretlenir (ör. `2026 - Şubat 1.000 TL, 2026 - Ocak 500 TL (Demirbaş)`). Daire panelinde (DaireController) VUK notu olmadığından ödenmeyen dönemler en altta durur. **İki controller da aynı mantığı taşır — birini değiştirince diğerini de güncelle.**

**Daire ödeme durumu takvimi (DaireOdemeDurumu):** Daire no ile sorgulanır; dairenin ödeme geçmişini görsel takvimle sunar. Veri controller'da JSON olarak (Newtonsoft) view'a aktarılır, takvim JS ile çizilir. İki ayrı veri birlikte gösterilir: (1) **ödeme günü** = `Makbuz.MakbuzTarihi` (makbuzun kesildiği gün) → ilgili ayın mini takviminde yeşil işaretli gün, tıklanınca makbuz no + tutar; (2) **ay başlığı rengi** = o ayın `Aidat` durumu (P=yeşil "ÖDENDİ" / A=kırmızı "ÖDENMEDİ" / kayıt yok=gri). **Önemli nüans:** geçmiş aylar ileri bir tarihte toplu ödenirse (ör. Mayıs+Haziran+Temmuz hepsi Temmuz'da) yeşil gün yalnızca **ödemenin yapıldığı Temmuz takviminde** çıkar; Mayıs/Haziran'da yeşil gün olmaz ama o ayların başlıkları `Aidat` durumu `P` olduğu için yine yeşil "ÖDENDİ" olur. Yıl sekmeleri + özet kutuları (toplam ödeme adedi, tahsil edilen tutar, ödenen/ödenmeyen aidat ayı) içerir. Menü: **Diğer → Ödeme Durumu**.

**Yıllık Kategori Giderleri Raporu (YillikTureGoreGiderler / YillikTureGoreGiderlerPDF):** Seçilen yıla ait tüm aktif (`Durum="A"`) giderleri veritabanında tanımlı `GiderTuru` bazında gruplayarak listeler. Web ekranında Bootstrap Card + Tablo yapısında S.No, Açıklama, Tarih ve Tutar kolonlarıyla sunulur. `YillikTureGoreGiderlerPDF` aksiyonu, iTextSharp `PdfPTable` ile gruplandırılmış, sayfa kesintilerinde tablo başlıklarını otomatik koruyan (`HeaderRows=2`), inline önizlenebilir yeni sekme PDF çıktısı verir.

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