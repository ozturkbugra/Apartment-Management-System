using ApartmanAidatTakip.Helpers;
using ApartmanAidatTakip.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Ajax.Utilities;
using OfficeOpenXml; // EPPlus kütüphanesi
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class AnaSayfaController : Controller
    {
        AptVTEntities db = new AptVTEntities();

        public void Sabit()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var tarih = db.Binalars.Where(x => x.BinaID == BinaID).FirstOrDefault();
            DateTime Lisans = Convert.ToDateTime(tarih.SozlesmeBitisTarihi);
            DateTime Bugun = DateTime.Now.Date;
            // Tarihleri çıkararak farkı hesapla

            ViewBag.LisansTarih = tarih.SozlesmeBitisTarihi.Value.ToString("dd/MM/yyyy");

            // Lisans süresi ile bugünkü tarih arasındaki farkı hesapla
            TimeSpan fark = Lisans - Bugun;

            // Toplam süre 365 gün, bu yüzden kalan gün sayısını hesapla
            int kalanGun = fark.Days;

            // Eğer kalan gün 365'i geçerse, minimum 0 olacak şekilde ayarlanır
            if (kalanGun < 0)
            {
                kalanGun = 0;
            }

            // Progress bar'a kalan gün sayısını ve doluluk oranını gönder
            ViewBag.KalanGun = kalanGun;
            double percent = (kalanGun / 365.0) * 100;


            ViewBag.Percent = Math.Round(percent);

            var duyurular = System.Web.HttpRuntime.Cache["Duyurular_Aktif"] as System.Collections.Generic.List<ApartmanAidatTakip.Models.Duyurular>;
            if (duyurular == null)
            {
                duyurular = db.Duyurulars.AsNoTracking().Where(x => x.Durum == "A").OrderByDescending(x => x.ID).ToList();
                System.Web.HttpRuntime.Cache.Insert("Duyurular_Aktif", duyurular, null,
                    DateTime.Now.AddMinutes(5), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            ViewBag.Duyurular = duyurular;

        }
        public void DonemEklendiMi()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int Yil = DateTime.Now.Year;
            int Ay = DateTime.Now.Month;
            var donemeklendimi = db.Kasas.Where(x => x.BinaID == BinaID && x.KasaYil == Yil && x.AyKodu == Ay).FirstOrDefault();
            if (donemeklendimi == null)
            {
                ViewBag.DonemSorgu = false;
                Session["DonemSorgu"] = "1";
            }
            else
            {
                ViewBag.DonemSorgu = true;
                Session["DonemSorgu"] = "1";
            }
        }
        public ActionResult Login()
        {
            DateTime simdi = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult Login(string Parola, int? BinaID, string KullaniciAdi, bool? remember)
        {
            string ipKey = Request.UserHostAddress ?? "unknown";
            int kalanKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
            if (kalanKilit > 0)
            {
                ViewBag.Uyari = LoginRateLimiter.KilitMesaji(kalanKilit);
                DateTime kilitSimdi = DateTime.Now.Date;
                ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= kilitSimdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();
                return View();
            }

            string p = Crypto.Hash(Parola, "MD5");
            var a = db.KullanicilarViews.Where(x => x.KullaniciAdi == KullaniciAdi && x.Parola == p && x.BinaID == BinaID && x.KullaniciDurumu == "A").FirstOrDefault();
            if (a != null)
            {
                LoginRateLimiter.Sifirla(ipKey);
                // İki adımlı doğrulama aktif mi? (Google Authenticator)
                bool ikiAdimAktif = db.Database.SqlQuery<bool>(
                    "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                    a.KullaniciID).FirstOrDefault();

                if (ikiAdimAktif)
                {
                    // Parola doğru; henüz oturum açmıyoruz. Kod doğrulama ekranına yönlendiriyoruz.
                    Session["Pending2FA_KullaniciID"] = a.KullaniciID;
                    Session["Pending2FA_Remember"] = (remember != null);
                    return RedirectToAction("LoginDogrula", "AnaSayfa");
                }

                GirisCereziOlustur(a, remember != null);
                return RedirectToAction("Index", "AnaSayfa");
            }
            else
            {
                LoginRateLimiter.HataKaydet(ipKey);
                int yeniKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
                ViewBag.Uyari = yeniKilit > 0
                    ? LoginRateLimiter.KilitMesaji(yeniKilit)
                    : "Kullanıcı Adı, Şifre veya bina yanlış";
                DateTime simdi = DateTime.Now.Date;
                ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();

                return View();
            }

        }

        // Başarılı giriş sonrası kullanıcı çerezini oluşturur (normal giriş ve 2FA sonrası ortak kullanılır).
        private void GirisCereziOlustur(KullanicilarView a, bool remember)
        {
            var tarih = a.SozlesmeBitisTarihi;

            HttpCookie userCookie = new HttpCookie("KullaniciBilgileri");
            userCookie.Values["KullaniciID"] = a.KullaniciID.ToString();
            userCookie.Values["AdSoyad"] = HttpUtility.UrlEncode(a.AdSoyad.ToString());
            userCookie.Values["KullaniciAdi"] = HttpUtility.UrlEncode(a.KullaniciAdi.ToString());
            userCookie.Values["BinaID"] = a.BinaID.ToString();
            userCookie.Values["BinaAdi"] = HttpUtility.UrlEncode(a.BinaAdi.ToString());
            userCookie.Values["BinaAdres"] = HttpUtility.UrlEncode(a.Adres.ToString());
            userCookie.Values["Parola"] = HttpUtility.UrlEncode(a.Parola.ToString());
            userCookie.Values["LisansTarih"] = HttpUtility.UrlEncode(tarih.Value.ToString("dd/MM/yyyy"));

            userCookie.Expires = remember ? DateTime.Now.AddDays(365) : DateTime.Now.AddDays(1);

            Response.Cookies.Add(userCookie);
        }

        // 2FA aktif kullanıcılar için kod giriş ekranı.
        public ActionResult LoginDogrula()
        {
            if (Session["Pending2FA_KullaniciID"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            return View();
        }

        [HttpPost]
        public ActionResult LoginDogrula(string kod)
        {
            if (Session["Pending2FA_KullaniciID"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            string ipKey = Request.UserHostAddress ?? "unknown";
            int kalanKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
            if (kalanKilit > 0)
            {
                ViewBag.Uyari = LoginRateLimiter.KilitMesaji(kalanKilit);
                return View();
            }

            int kullaniciID = Convert.ToInt32(Session["Pending2FA_KullaniciID"]);
            bool remember = Session["Pending2FA_Remember"] != null && (bool)Session["Pending2FA_Remember"];

            string secret = db.Database.SqlQuery<string>(
                "SELECT TwoFactorSecret FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();

            // Önce Authenticator kodu, olmadıysa tek kullanımlık yedek kod denenir.
            bool dogru = TwoFactorHelper.ValidateCode(secret, kod);
            if (!dogru)
            {
                dogru = YedekKoduTuket(kullaniciID, kod);
            }

            if (!dogru)
            {
                LoginRateLimiter.HataKaydet(ipKey);
                int yeniKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
                ViewBag.Uyari = yeniKilit > 0
                    ? LoginRateLimiter.KilitMesaji(yeniKilit)
                    : "Doğrulama kodu hatalı veya süresi dolmuş. Lütfen tekrar deneyin.";
                return View();
            }

            LoginRateLimiter.Sifirla(ipKey);

            // Kod doğru: kullanıcıyı yeniden çekip çerezi oluştur ve oturumu başlat.
            var a = db.KullanicilarViews.FirstOrDefault(x => x.KullaniciID == kullaniciID && x.KullaniciDurumu == "A");
            if (a == null)
            {
                Session.Remove("Pending2FA_KullaniciID");
                Session.Remove("Pending2FA_Remember");
                return RedirectToAction("Login", "AnaSayfa");
            }

            GirisCereziOlustur(a, remember);
            Session.Remove("Pending2FA_KullaniciID");
            Session.Remove("Pending2FA_Remember");
            return RedirectToAction("Index", "AnaSayfa");
        }

        // --- ŞİFREMİ UNUTTUM (self-servis, 2FA ile kimlik doğrulamalı) ---
        public ActionResult SifremiUnuttum()
        {
            DateTime simdi = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult SifremiUnuttum(int? BinaID, string KullaniciAdi)
        {
            var a = db.KullanicilarViews.FirstOrDefault(x => x.KullaniciAdi == KullaniciAdi && x.BinaID == BinaID && x.KullaniciDurumu == "A");
            if (a != null)
            {
                bool ikiAdimAktif = db.Database.SqlQuery<bool>(
                    "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                    a.KullaniciID).FirstOrDefault();

                if (ikiAdimAktif)
                {
                    // Kimlik, bir sonraki adımda Authenticator kodu / yedek kod ile doğrulanacak.
                    Session["Reset_KullaniciID"] = a.KullaniciID;
                    return RedirectToAction("SifreYenile", "AnaSayfa");
                }

                ViewBag.Uyari = "Bu hesapta iki adımlı doğrulama aktif olmadığı için kendiniz şifre sıfırlayamazsınız. Lütfen sistem yöneticinize başvurun.";
            }
            else
            {
                ViewBag.Uyari = "Kullanıcı adı veya bina hatalı.";
            }

            DateTime simdi = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();
            return View();
        }

        public ActionResult SifreYenile()
        {
            if (Session["Reset_KullaniciID"] == null)
            {
                return RedirectToAction("SifremiUnuttum", "AnaSayfa");
            }
            return View();
        }

        [HttpPost]
        public ActionResult SifreYenile(string kod, string Parola, string Parola2)
        {
            if (Session["Reset_KullaniciID"] == null)
            {
                return RedirectToAction("SifremiUnuttum", "AnaSayfa");
            }

            int kullaniciID = Convert.ToInt32(Session["Reset_KullaniciID"]);

            // Önce parolaları kontrol et (yanlışsa yedek kodu boşuna tüketmemek için).
            if (string.IsNullOrWhiteSpace(Parola) || Parola != Parola2)
            {
                ViewBag.Uyari = "Şifreler boş olamaz ve birbiriyle uyuşmalıdır.";
                return View();
            }

            // Kimlik doğrulama: önce Authenticator kodu, olmadıysa yedek kod (tek kullanımlık).
            string secret = db.Database.SqlQuery<string>(
                "SELECT TwoFactorSecret FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();

            bool dogru = TwoFactorHelper.ValidateCode(secret, kod);
            if (!dogru)
            {
                dogru = YedekKoduTuket(kullaniciID, kod);
            }

            if (!dogru)
            {
                ViewBag.Uyari = "Doğrulama kodu hatalı veya süresi dolmuş. Lütfen tekrar deneyin.";
                return View();
            }

            var k = db.Kullanicilars.FirstOrDefault(x => x.KullaniciID == kullaniciID);
            if (k == null)
            {
                Session.Remove("Reset_KullaniciID");
                return RedirectToAction("Login", "AnaSayfa");
            }

            k.Parola = Crypto.Hash(Parola, "MD5");
            db.SaveChanges();

            db.Hareketlers.Add(new Hareketler()
            {
                BinaID = k.BinaID ?? 0,
                KullaniciID = kullaniciID,
                OlayAciklama = "Kullanıcı iki adımlı doğrulama ile şifresini sıfırladı",
                Tarih = DateTime.Now,
                Tur = "Güncelleme",
            });
            db.SaveChanges();

            Session.Remove("Reset_KullaniciID");
            TempData["Basarili"] = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction("Login", "AnaSayfa");
        }

        // Girilen kod bir yedek koda uyuyorsa onu tüketir (bir daha kullanılamaz) ve true döner.
        private bool YedekKoduTuket(int kullaniciID, string kod)
        {
            string kodlar = db.Database.SqlQuery<string>(
                "SELECT TwoFactorRecoveryCodes FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();

            if (string.IsNullOrEmpty(kodlar))
                return false;

            var hashListesi = kodlar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string girilenHash = TwoFactorHelper.HashRecoveryCode(kod);

            if (!hashListesi.Contains(girilenHash))
                return false;

            hashListesi.Remove(girilenHash); // tek kullanımlık: listeden çıkar
            string yeni = string.Join(";", hashListesi);
            object yeniParam = string.IsNullOrEmpty(yeni) ? (object)DBNull.Value : yeni;

            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                yeniParam, kullaniciID);

            return true;
        }

        public class MakbuzGrupModel
        {
            public int MakbuzID { get; set; }
            public int MakbuzNo { get; set; }
            public int DaireNo { get; set; }
            public DateTime? Tarih { get; set; }
            public decimal Toplam { get; set; }
            public bool VarAidat { get; set; }
            public bool VarEk { get; set; }
        }

        public ActionResult Index()
        {
            // --- 1. GİRİŞ KONTROLLERİ ---
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            // AsNoTracking ile sadece okuma (HIZLI)
            var aktifmi = db.KullanicilarViews.AsNoTracking().FirstOrDefault(x => x.KullaniciID == KullaniciID);

            Session["Aktif"] = "Anasayfa";
            Sabit();
            Session["DaireID"] = "0";

            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            DateTime nowtime = DateTime.Now;
            DateTime licence = DateTime.Parse(userCookie.Values["LisansTarih"]);

            if (licence < nowtime)
            {
                return RedirectToAction("Logout", "AnaSayfa");
            }

            // --- 2. TARİH DEĞİŞKENLERİ ---
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;
            int previousMonth = (currentMonth == 1) ? 12 : currentMonth - 1;
            int previousYear = (currentMonth == 1) ? currentYear - 1 : currentYear;

            // --- 3. BU AYIN VERİLERİ (RAM DOSTU - AsNoTracking) ---
            var buAyGiderListesi = db.GiderViews.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih.Value.Year == currentYear && x.GiderTarih.Value.Month == currentMonth)
                .OrderByDescending(x => x.GiderID).ToList();

            // Mevcut bu satırın ALTINA ekle:
            var buAyMakbuzSatirListesi = db.MakbuzSatirViews.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.MakbuzSatirDurum == "A"
                         && x.MakbuzTarihi.Value.Year == currentYear
                         && x.MakbuzTarihi.Value.Month == currentMonth)
                .OrderByDescending(x => x.MakbuzID)
                .ToList();

            // MakbuzID bazında grupla
            var grupluMakbuzlar = buAyMakbuzSatirListesi
            .GroupBy(x => x.MakbuzID)
            .Select(g => new MakbuzGrupModel
            {
                MakbuzID = g.Key ?? 0,
                MakbuzNo = g.First().MakbuzNo ?? 0,
                DaireNo = g.First().DaireNo ?? 0,
                Tarih = g.First().MakbuzTarihi,
                Toplam = g.Sum(x => x.Tutar ?? 0),
                VarAidat = g.Any(x => x.EkMiAidatMi == "A"),
                VarEk = g.Any(x => x.EkMiAidatMi == "E")
            })
            .OrderByDescending(x => x.MakbuzID)
            .ToList();

            ViewBag.MakbuzSatirlar = grupluMakbuzlar;


            var buAyTahsilatListesi = db.Tahsilats.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == currentYear && x.TahsilatTarih.Value.Month == currentMonth)
                .OrderByDescending(x => x.TahsilatID).ToList();

            // --- 4. HESAPLAMALAR ---
            decimal aygider = buAyGiderListesi.Sum(x => x.GiderTutar) ?? 0;
            decimal makbuzgelir = buAyMakbuzSatirListesi.Sum(x => x.Tutar ?? 0);
            decimal tahsilatgelir = buAyTahsilatListesi.Sum(x => x.TahsilatTutar) ?? 0;

            ViewBag.aygelir = makbuzgelir + tahsilatgelir;
            ViewBag.aygider = aygider;
            ViewBag.Giderler = buAyGiderListesi.ToList(); // Ekrana sadece son 10 taneyi bas, hepsini değil
            ViewBag.Tahsilatlar = buAyTahsilatListesi.ToList();

            // TOPLAM ALACAK (HIZLI COUNT)
            // Tüm daireleri çekmeye gerek yok, sadece borcu topla
            decimal toplamBorc = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).Sum(x => (decimal?)x.Borc) ?? 0;
            ViewBag.alacak = toplamBorc;
            ViewBag.ToplamAlacak = toplamBorc;

            // --- 5. YILLIK HESAPLAMALAR (OPTIMIZE EDİLDİ) ---
            // Join kullanarak tek sorguda çekiyoruz (N+1 engellendi)

            decimal yilMakbuz = (from ms in db.MakbuzSatirs
                                 join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                 where m.BinaID == BinaID && m.Durum == "A" && m.MakbuzTarihi.Value.Year == currentYear
                                 select ms.Tutar).Sum() ?? 0;

            decimal yilTahsilatDemirbas = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == currentYear && x.DemirbasMi == true).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
            decimal yilTahsilatAidat = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == currentYear && x.DemirbasMi == false).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
            decimal yilGider = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih.Value.Year == currentYear).Sum(x => (decimal?)x.GiderTutar) ?? 0;

            ViewBag.ToplamGelir = yilMakbuz + yilTahsilatDemirbas + yilTahsilatAidat;
            ViewBag.ToplamGider = yilGider;

            // --- 6. KASA HESABI (FİNAL DÜZELTME - DEVİR KONTROLLÜ VE AYRIŞTIRMALI) ---
            var acilis = db.AcilisBakiyes.AsNoTracking().FirstOrDefault(x => x.BinaID == BinaID);
            decimal acilisbakiye = acilis?.ToplamTutar ?? 0;
            decimal ekacilis = acilis?.EkTutar ?? 0;

            // Bu aya ait (Örn: Ocak 2026) kapatılmış kasa var mı?
            var son_kasa = db.Kasas.AsNoTracking().FirstOrDefault(x => x.KasaYil == currentYear && x.AyKodu == currentMonth && x.BinaID == BinaID);

            if (son_kasa != null)
            {
                // Bu ayın kasası zaten kapatılmış.
                // NOT: MakbuzView üzerinden detay (E/A) gelmediği için burada da makbuz detayına inmemiz lazım.
                // Ancak pratiklik açısından, son kasa kapandıysa üzerine eklenen makbuzları detaylı sorgulayalım:

                var buAyEkMakbuzTutar = (from ms in db.MakbuzSatirs
                                         join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                         where m.BinaID == BinaID && m.Durum == "A"
                                         && m.MakbuzTarihi.Value.Year == currentYear
                                         && m.MakbuzTarihi.Value.Month == currentMonth
                                         && ms.EkMiAidatMi == "E"
                                         select ms.Tutar).Sum() ?? 0;

                decimal demirbasgider = buAyGiderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;

                ViewBag.Kasa = (son_kasa.KasaToplam + makbuzgelir + tahsilatgelir) - aygider;

                // DÜZELTME: Ek Bakiyeye "buAyEkMakbuzTutar" eklendi
                ViewBag.EkBakiye = (buAyTahsilatListesi.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) + buAyEkMakbuzTutar + son_kasa.KasaEk) - demirbasgider;

                ViewBag.AidatBakiye = (decimal)ViewBag.Kasa - (decimal)ViewBag.EkBakiye;
            }
            else
            {
                // Bu ayın kasası yok. Geçmişteki EN SON kasayı bul.
                var bironcekikasa = db.Kasas.AsNoTracking()
                    .Where(x => x.BinaID == BinaID)
                    .OrderByDescending(x => x.KasaYil)
                    .ThenByDescending(x => x.AyKodu)
                    .FirstOrDefault();

                if (bironcekikasa == null)
                {
                    // HİÇ KASA YOKSA: Her şeyi baştan sona topla.
                    decimal tummakbuz = (from ms in db.MakbuzSatirs
                                         join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                         where m.BinaID == BinaID && m.Durum == "A"
                                         select ms.Tutar).Sum() ?? 0;

                    // Burada değişken ismin tumtahsilatEk kalmış ama sorgu MakbuzSatir'dan yapılıyor, DOĞRU.
                    decimal tumMakbuzEk = (from ms in db.MakbuzSatirs
                                           join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                           where m.BinaID == BinaID && m.Durum == "A" && ms.EkMiAidatMi == "E"
                                           select ms.Tutar).Sum() ?? 0;

                    decimal gider2 = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;

                    var tumTahsilatlar = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A").Select(x => new { x.TahsilatTutar, x.DemirbasMi }).ToList();
                    decimal tahsilat3 = tumTahsilatlar.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;
                    decimal aidattahsilat = tumTahsilatlar.Where(x => x.DemirbasMi == false).Sum(x => x.TahsilatTutar) ?? 0;
                    decimal demirbasgider = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTuruID == 6).Sum(x => (decimal?)x.GiderTutar) ?? 0;

                    ViewBag.Kasa = (acilisbakiye + tummakbuz + tahsilat3 + aidattahsilat) - gider2;
                    // DÜZELTME: tumtahsilatEk (aslında makbuz ek) mantığı doğruydu, isim karmaşası vardı, kontrol edildi.
                    ViewBag.EkBakiye = (tahsilat3 + ekacilis + tumMakbuzEk) - demirbasgider;
                    ViewBag.AidatBakiye = (decimal)ViewBag.Kasa - (decimal)ViewBag.EkBakiye;
                }
                else
                {
                    // --- KRİTİK DÜZELTME BURADA ---
                    int sonKasaYil = bironcekikasa.KasaYil ?? 0;
                    int sonKasaAy = bironcekikasa.AyKodu ?? 0;

                    bool hemenOncekiAyMi = (currentYear == sonKasaYil && currentMonth - 1 == sonKasaAy) || (currentYear == sonKasaYil + 1 && currentMonth == 1 && sonKasaAy == 12);

                    // 1. MAKBUZLAR (ARTIK DETAYLI ÇEKİYORUZ)
                    // Select kısmına 'ms.EkMiAidatMi' eklendi.
                    var makbuzSorgu = from ms in db.MakbuzSatirs
                                      join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                      where m.BinaID == BinaID && m.Durum == "A"
                                      select new { m.MakbuzTarihi, ms.Tutar, ms.EkMiAidatMi };

                    decimal araDonemMakbuzToplam = 0;
                    decimal araDonemMakbuzEk = 0; // Demirbaş Makbuzları için yeni değişken

                    // Sorguyu listeye çekip RAM'de işlem yapalım (LINQ to SQL karmaşasını önlemek için)
                    // Tarih filtresini önce uyguluyoruz
                    var filtrelenmisMakbuzlar = hemenOncekiAyMi
                        ? makbuzSorgu.Where(x => x.MakbuzTarihi.Value.Year > sonKasaYil || (x.MakbuzTarihi.Value.Year == sonKasaYil && x.MakbuzTarihi.Value.Month >= sonKasaAy)).ToList()
                        : makbuzSorgu.Where(x => x.MakbuzTarihi.Value.Year > sonKasaYil || (x.MakbuzTarihi.Value.Year == sonKasaYil && x.MakbuzTarihi.Value.Month > sonKasaAy)).ToList();

                    // Şimdi Ayrıştırıyoruz
                    araDonemMakbuzToplam = filtrelenmisMakbuzlar.Sum(x => (decimal?)x.Tutar) ?? 0;
                    araDonemMakbuzEk = filtrelenmisMakbuzlar.Where(x => x.EkMiAidatMi == "E").Sum(x => (decimal?)x.Tutar) ?? 0;

                    // 2. GİDERLER
                    var giderQuery = db.GiderViews.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A");
                    List<GiderView> araDonemGiderListesi;

                    if (hemenOncekiAyMi)
                        araDonemGiderListesi = giderQuery.Where(x => x.GiderTarih.Value.Year > sonKasaYil || (x.GiderTarih.Value.Year == sonKasaYil && x.GiderTarih.Value.Month >= sonKasaAy)).ToList();
                    else
                        araDonemGiderListesi = giderQuery.Where(x => x.GiderTarih.Value.Year > sonKasaYil || (x.GiderTarih.Value.Year == sonKasaYil && x.GiderTarih.Value.Month > sonKasaAy)).ToList();

                    decimal araDonemGiderToplam = araDonemGiderListesi.Sum(x => x.GiderTutar) ?? 0;
                    decimal araDonemDemirbasGider = araDonemGiderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;

                    // 3. TAHSİLATLAR
                    var tahsilatQuery = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A");
                    List<Tahsilat> araDonemTahsilatListesi;

                    if (hemenOncekiAyMi)
                        araDonemTahsilatListesi = tahsilatQuery.Where(x => x.TahsilatTarih.Value.Year > sonKasaYil || (x.TahsilatTarih.Value.Year == sonKasaYil && x.TahsilatTarih.Value.Month >= sonKasaAy)).ToList();
                    else
                        araDonemTahsilatListesi = tahsilatQuery.Where(x => x.TahsilatTarih.Value.Year > sonKasaYil || (x.TahsilatTarih.Value.Year == sonKasaYil && x.TahsilatTarih.Value.Month > sonKasaAy)).ToList();

                    decimal araDonemTahsilatToplam = araDonemTahsilatListesi.Sum(x => x.TahsilatTutar) ?? 0;
                    decimal araDonemDemirbasTahsilat = araDonemTahsilatListesi.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;

                    // HESAPLAMA (DÜZELTİLDİ)
                    ViewBag.Kasa = (bironcekikasa.KasaToplam + araDonemMakbuzToplam + araDonemTahsilatToplam) - araDonemGiderToplam;

                    // BURAYA DİKKAT: araDonemMakbuzEk eklendi!
                    ViewBag.EkBakiye = (bironcekikasa.KasaEk + araDonemDemirbasTahsilat + araDonemMakbuzEk) - araDonemDemirbasGider;

                    ViewBag.AidatBakiye = (decimal)ViewBag.Kasa - (decimal)ViewBag.EkBakiye;
                }
            }

            // --- 7. DEĞİŞİM GRAFİKLERİ (AsNoTracking ile) ---
            // Tek tek Sum çekmek yerine hızlıca hallediyoruz.
            decimal eskimakbuz = db.Makbuzs.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.MakbuzTarihi.Value.Month == previousMonth && x.MakbuzTarihi.Value.Year == previousYear).Sum(x => (decimal?)x.MabuzTutar) ?? 0;
            decimal eskitahsilat = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Month == previousMonth && x.TahsilatTarih.Value.Year == previousYear).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
            decimal eskigider = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih.Value.Month == previousMonth && x.GiderTarih.Value.Year == previousYear).Sum(x => (decimal?)x.GiderTutar) ?? 0;

            decimal eskigelir = eskimakbuz + eskitahsilat;
            decimal yenigelir = makbuzgelir + tahsilatgelir;
            decimal yenigider = aygider;

            // Yüzde hesapları aynı kalıyor...
            decimal yuzdeDegisim;
            if (eskigelir == 0) yuzdeDegisim = (yenigelir > 0) ? 100 : 0;
            else yuzdeDegisim = Math.Round(((yenigelir - eskigelir) / eskigelir) * 100, 2);

            ViewBag.Degisim = yuzdeDegisim < 0 ? 0 : 1;
            ViewBag.DegisimTutar = Math.Abs(yuzdeDegisim);

            decimal gideryuzdedegisim;
            if (eskigider == 0) gideryuzdedegisim = (yenigider > 0) ? 100 : 0;
            else gideryuzdedegisim = Math.Round(((yenigider - eskigider) / eskigider) * 100, 2);

            ViewBag.GiderDegisim = gideryuzdedegisim < 0 ? 0 : 1;
            ViewBag.GiderDegisimTutar = Math.Abs(gideryuzdedegisim);

            decimal kasadegisim = yuzdeDegisim - gideryuzdedegisim;
            ViewBag.KasaDegisim = kasadegisim < 0 ? 0 : 1;
            ViewBag.KasaDegisimTutar = Math.Abs(kasadegisim);

            // --- BORÇLU DAİRE SAYISI (HIZLI COUNT) ---
            // Tüm daireleri çekip RAM'e atmak yerine, sadece Borc kolonunu sorguluyoruz.
            var borclar = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).Select(x => x.Borc).ToList();

            ViewBag.BorcuOlmayanlar = borclar.Count(x => x <= 0);
            ViewBag.BorcuOlanlar = borclar.Count(x => x > 0);

            return View();
        }

        public ActionResult Sakinler()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }

            Sabit();
            Session["Aktif"] = "Sakinler";
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var eklenensakinsayisi = db.Dairelers.Where(x => x.BinaID == BinaID).Count();
            var a = db.Binalars.Where(x => x.BinaID == BinaID).FirstOrDefault();
            var olmasıgerekensakinsayisi = a.DaireSayisi;

            if (eklenensakinsayisi < olmasıgerekensakinsayisi)
            {
                ViewBag.Durum = true;
            }
            else
            {
                ViewBag.Durum = false;
            }

            ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();

            return View();
        }

        [HttpPost]
        public ActionResult SakinEkle(Daireler daireler)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            try
            {
                var eklenensakinsayisi = db.Dairelers.Where(x => x.BinaID == BinaID).Count();
                var a = db.Binalars.Where(x => x.BinaID == BinaID).FirstOrDefault();
                var olmasıgerekensakinsayisi = a.DaireSayisi;

                if (eklenensakinsayisi <= olmasıgerekensakinsayisi)
                {
                    if (eklenensakinsayisi == 0)
                    {
                        eklenensakinsayisi = 0;
                    }

                    int sayi = eklenensakinsayisi + 1;

                    int daireno = sayi;
                    daireler.DaireNo = daireno;
                    daireler.BinaID = BinaID;
                    daireler.Borc = 0;
                    db.Dairelers.Add(daireler);
                    db.SaveChanges();
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = sayi + " numaralı daire eklendi",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();
                    TempData["Basarili"] = "Sakin Başarıyla Eklendi.";

                }
                else
                {
                    TempData["Hata"] = "Fazla Daire Sakini Eklemeye Çalıştınız.";

                }
            }
            catch (Exception)
            {

                TempData["Hata"] = "Bir Hata Oluştu !";
            }


            ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();

            return RedirectToAction("Sakinler", "AnaSayfa");
        }


        [HttpPost]
        public ActionResult SakinEkleExcel(HttpPostedFileBase file)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var sorgu = db.Dairelers.Where(x => x.BinaID == BinaID).FirstOrDefault();

            if (sorgu != null)
            {
                TempData["Hata"] = "Bu binaya ait daire kaydı yapılmıştır. Bu yüzden toplu halde daire ekleyemezsiniz. Tek tek daireleri tanımlamanız gerekmektedir.";
                return RedirectToAction("Sakinler", "AnaSayfa");

            }

            try
            {
                if (file != null && file.ContentLength > 0)
                {
                    using (var package = new ExcelPackage(file.InputStream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        int rowCount = worksheet.Dimension.Rows;

                        var eklenensakinsayisi = db.Dairelers.Count(x => x.BinaID == BinaID);
                        var bina = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);
                        int olmasıgerekensakinsayisi = bina?.DaireSayisi ?? 0;

                        if (eklenensakinsayisi + (rowCount - 1) > olmasıgerekensakinsayisi)
                        {
                            TempData["Hata"] = "Fazla daire sakini eklemeye çalıştınız.";
                            return RedirectToAction("Sakinler", "AnaSayfa");
                        }

                        List<Daireler> daireListesi = new List<Daireler>();


                        int dairesayisi = 1;

                        for (int row = 2; row <= rowCount; row++) // 1. satır başlık olduğu için 2'den başlıyoruz
                        {

                            eklenensakinsayisi++; // Her yeni eklemede artır

                            var daire = new Daireler
                            {
                                DaireNo = dairesayisi, // Excel'den DaireNo al
                                AdSoyad = worksheet.Cells[row, 1].Value?.ToString() ?? "", // Excel'den AdSoyad al
                                BinaID = BinaID,
                                Telefon = worksheet.Cells[row, 2].Value?.ToString() ?? "", // Sabit değer
                                TC = worksheet.Cells[row, 3].Value?.ToString() ?? "", // Sabit değer
                                DaireDurum = worksheet.Cells[row, 4].Value?.ToString() ?? "", // Sabit değer
                                Borc = 0, // Sabit değer
                                YonetimdeMi = "H" // Sabit değer
                            };

                            daireListesi.Add(daire);
                            dairesayisi++;

                        }

                        db.Dairelers.AddRange(daireListesi);
                        db.SaveChanges();

                        // Hareketler Tablosuna Kayıt
                        Hareketler hareketler = new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = $"{daireListesi.Count} adet daire sakini eklendi",
                            Tarih = DateTime.Now,
                            Tur = "Toplu Ekleme",
                        };
                        db.Hareketlers.Add(hareketler);
                        db.SaveChanges();

                        TempData["Basarili"] = "Toplu sakin ekleme başarılı.";
                    }
                }
            }
            catch (Exception ex)
            {
                string errmsg = ex.Message;
                TempData["Hata"] = errmsg;
            }

            return RedirectToAction("Sakinler", "AnaSayfa");
        }


        public ActionResult SakinDuzenle(int id)
        {
            Sabit();
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            var a = db.Dairelers.Where(x => x.DaireID == id && x.BinaID == BinaID).FirstOrDefault();
            if (a == null)
            {
                return RedirectToAction("Sakinler", "AnaSayfa");
            }
            else
            {
                ViewBag.s = a;
                ViewBag.EkSakinler = db.DigerDaireSakinleris.Where(x => x.DaireID == id).ToList();
                return View();
            }
        }


        [HttpPost]
        public ActionResult EkSakinEkle(int DaireID, string AdSoyad, string Telefon, string DaireDurum, string TC)
        {
            try
            {
                // Senin belirttiğin kolon yapısına birebir uygun nesne oluşturuluyor
                var yeniSakin = new DigerDaireSakinleri()
                {
                    DaireID = DaireID,
                    AdSoyad = AdSoyad,
                    Telefon = Telefon,
                    DaireDurum = DaireDurum, // "Ev Sahibi", "Eş", "Çocuk" vb. formdan ne seçilirse
                    TC = TC
                };

                db.DigerDaireSakinleris.Add(yeniSakin);
                db.SaveChanges();
                //TempData["Basarili"] = "Ek sakin/bilgi başarıyla eklendi.";
            }
            catch (Exception)
            {
                TempData["Hata"] = "Kişi eklenirken bir hata oluştu !";
            }

            return RedirectToAction("SakinDuzenle", "AnaSayfa", new { id = DaireID });
        }

        public ActionResult EkSakinSil(int id, int daireId)
        {
            var kisi = db.DigerDaireSakinleris.FirstOrDefault(x => x.ID == id && x.DaireID == daireId);
            if (kisi != null)
            {
                db.DigerDaireSakinleris.Remove(kisi);
                db.SaveChanges();
                TempData["Basarili"] = "Kişi kaydı silindi.";
            }
            return RedirectToAction("SakinDuzenle", "AnaSayfa", new { id = daireId });
        }

        [HttpPost]
        public ActionResult SakinDuzenle(Daireler daireler, int DaireID)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            var a = db.Dairelers.Where(x => x.DaireID == DaireID && x.BinaID == BinaID).FirstOrDefault();
            if (a == null)
            {
                TempData["Hata"] = "Bir Hata Oluştu";
                return RedirectToAction("Sakinler", "AnaSayfa");
            }
            else
            {
                int yil = DateTime.Now.Year;
                string ay = DateTime.Now.ToString("MMMM");
                var aidattutar = db.Aidats.FirstOrDefault(x => x.AidatYil == yil && x.AidatAy == ay && x.BinaID == BinaID);
                if (aidattutar != null)
                {
                    if (a.YonetimdeMi == "E" && daireler.YonetimdeMi == "H")
                    {
                        // ARAYA GİREN KONTROL: Bu adamın zaten o ay borcu var mı?
                        bool zatenBorcluMu = db.Aidats.Any(x => x.DaireNo == a.DaireNo && x.AidatYil == yil && x.AidatAy == ay && x.BinaID == BinaID && x.Durum == "A");

                        // Eğer borcu yoksa ekle (Varsa elleme, mükerrer olmasın)
                        if (!zatenBorcluMu)
                        {
                            Aidat aidat = new Aidat()
                            {
                                AidatAy = ay,
                                AidatYil = yil,
                                AidatTutar = aidattutar.AidatTutar,
                                DaireNo = a.DaireNo,
                                BinaID = BinaID,
                                ZamEklendiMi = "H",
                                Durum = "A"
                            };
                            db.Aidats.Add(aidat);
                            a.Borc += aidattutar.AidatTutar;
                            db.SaveChanges();
                        }
                    }

                    if (a.YonetimdeMi == "H" && daireler.YonetimdeMi == "E")
                    {
                        var aidatsorgu = db.Aidats.FirstOrDefault(x => x.DaireNo == a.DaireNo && x.AidatYil == yil && x.AidatAy == ay && x.BinaID == BinaID && x.Durum == "A");
                        if (aidatsorgu != null)
                        {
                            db.Aidats.Remove(aidatsorgu);
                            a.Borc -= aidattutar.AidatTutar;
                            db.SaveChanges();
                        }
                    }
                }

                a.Aciklama = daireler.Aciklama;
                a.AdSoyad = daireler.AdSoyad;
                a.DaireDurum = daireler.DaireDurum;
                a.TC = daireler.TC;
                a.Telefon = daireler.Telefon;
                a.YonetimdeMi = daireler.YonetimdeMi;
                db.SaveChanges();
                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = a.DaireNo + " numaralı daire'nin bilgileri güncellendi",
                    Tarih = DateTime.Now,
                    Tur = "Güncelleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();
                TempData["Basarili"] = "Sakin Başarıyla Güncellendi.";
                return RedirectToAction("Sakinler", "AnaSayfa");
            }
        }

        public ActionResult Password()
        {

            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Sabit();
            return View();
        }

        [HttpPost]
        public ActionResult Password(string eskiparola, string Parola, string Parola2)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            var a = db.Kullanicilars.Where(x => x.KullaniciID == KullaniciID).FirstOrDefault();
            var eskisifre = Crypto.Hash(eskiparola, "MD5");
            if (a.Parola == eskisifre)
            {
                if (Parola == Parola2)
                {

                    a.Parola = Crypto.Hash(Parola, "MD5");
                    db.SaveChanges();
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = "Kullanıcı şifresini güncellendi",
                        Tarih = DateTime.Now,
                        Tur = "Güncelleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();
                    TempData["Basarili"] = "Şifreniz Başarıyla Güncellendi.";

                }
                else
                {
                    TempData["Hata"] = "Şifreleriniz Birbiriyle Uyuşmuyor.";

                }
            }
            else
            {
                TempData["Hata"] = "Eski Şifreniz Hatalı.";
            }

            return View();

        }
        public ActionResult Logout()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }

            if (Request.Cookies["KullaniciBilgileri"] != null)
            {
                // Cookie'nin süresini geçmiş bir zamana ayarla
                HttpCookie userCookie = new HttpCookie("KullaniciBilgileri");
                userCookie.Expires = DateTime.Now.AddDays(-1);
                Response.Cookies.Add(userCookie);
            }
            return RedirectToAction("Index", "AnaSayfa");

        }

        public ActionResult DaireBorclandir()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            Session["Aktif"] = "DaireBorclandir";
            Sabit();
            return View();
        }

        [HttpPost]
        public ActionResult DaireBorclandir(Aidat aidat)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            try
            {

                //buğra
                int yeniAyKodu;
                if (aidat.AidatAy == "Ocak")
                {
                    yeniAyKodu = 1;
                }
                else if (aidat.AidatAy == "Şubat")
                {
                    yeniAyKodu = 2;
                }
                else if (aidat.AidatAy == "Mart")
                {
                    yeniAyKodu = 3;
                }
                else if (aidat.AidatAy == "Nisan")
                {
                    yeniAyKodu = 4;
                }
                else if (aidat.AidatAy == "Mayıs")
                {
                    yeniAyKodu = 5;
                }
                else if (aidat.AidatAy == "Haziran")
                {
                    yeniAyKodu = 6;
                }
                else if (aidat.AidatAy == "Temmuz")
                {
                    yeniAyKodu = 7;
                }
                else if (aidat.AidatAy == "Ağustos")
                {
                    yeniAyKodu = 8;
                }
                else if (aidat.AidatAy == "Eylül")
                {
                    yeniAyKodu = 9;
                }
                else if (aidat.AidatAy == "Ekim")
                {
                    yeniAyKodu = 10;
                }
                else if (aidat.AidatAy == "Kasım")
                {
                    yeniAyKodu = 11;
                }
                else if (aidat.AidatAy == "Aralık")
                {
                    yeniAyKodu = 12;
                }
                else
                {
                    yeniAyKodu = 0; // Hatalı ay girişi durumu için
                }

                // Şu anki ay ve yılı alıyoruz
                int buAy = DateTime.Now.Month;
                int buYil = DateTime.Now.Year;

                // Gelecek dönem eklenmemesi kontrolü
                if (aidat.AidatYil > buYil || (aidat.AidatYil == buYil && yeniAyKodu > buAy))
                {
                    ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();

                    TempData["Hata"] = "Vakti gelmemiş dönemi ekleyemezsiniz.";
                    return View();
                }


                var sonKasaDonemi = db.Kasas
                              .Where(x => x.BinaID == BinaID)
                              .OrderByDescending(x => x.KasaYil)
                              .ThenByDescending(x => x.AyKodu)
                              .FirstOrDefault();

                // Eğer daha önce bir dönem eklendiyse ve yeni dönem eski bir dönemse hata döndür
                if (sonKasaDonemi != null && (aidat.AidatYil < sonKasaDonemi.KasaYil || (aidat.AidatYil == sonKasaDonemi.KasaYil && yeniAyKodu < sonKasaDonemi.AyKodu)))
                {
                    ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
                    TempData["Hata"] = "Önceki aylara dönem ekleyemezsiniz!";
                    return View();
                }


                var aidatvarmi = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo && x.AidatYil == aidat.AidatYil && x.AidatAy == aidat.AidatAy && x.Durum == "A").FirstOrDefault();
                if (aidatvarmi == null)
                {
                    aidat.BinaID = BinaID;
                    aidat.Durum = "A";
                    aidat.ZamEklendiMi = "H";
                    db.Aidats.Add(aidat);
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = aidat.DaireNo + " dairesine " + aidat.AidatTutar + " tutarında " + aidat.AidatAy + " - " + aidat.AidatYil + " borçlandırılmıştır.",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    var dairesorgu = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo).FirstOrDefault();
                    dairesorgu.Borc += aidat.AidatTutar;
                    db.SaveChanges();
                    TempData["Basarili"] = "Daire Başarıyla Borçlandırıldı.";
                }
                else
                {
                    TempData["Hata"] = "Bu Daireye Aynı Aidatı Zaten Eklediniz !";
                }
                ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu !";
                ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            }



            return View();
        }


        [HttpPost]
        public ActionResult EkstraAidatEkEkle(Aidat aidat, string Tur)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            try
            {
                string ayadi = DateTime.Now.ToString("MMMM");
                int yil = DateTime.Now.Year;

                if (Tur == "1")
                {

                    var aidatsayi = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo && x.AidatAy.Contains(ayadi) && x.AidatYil == yil).Count();

                    aidatsayi++;

                    aidat.AidatAy = ayadi + "-" + aidatsayi;
                    aidat.AidatYil = yil;
                    aidat.BinaID = BinaID;
                    aidat.Durum = "A";
                    aidat.ZamEklendiMi = "H";
                    db.Aidats.Add(aidat);

                    var dairesorgu = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo).FirstOrDefault();
                    dairesorgu.Borc += aidat.AidatTutar;
                    db.SaveChanges();
                    TempData["Basarili"] = "Ekstra Aidat Eklenmiştir";


                }
                else
                {
                    var eksayi = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo && x.EkAy.Contains(ayadi) && x.EkYil == yil).Count();
                    eksayi++;

                    Ek yeniek = new Ek()
                    {
                        EkAy = ayadi + "-" + eksayi,
                        EkYil = yil,
                        BinaID = BinaID,
                        DaireNo = aidat.DaireNo,
                        Durum = "A",
                        EkTutar = aidat.AidatTutar

                    };
                    db.Eks.Add(yeniek);

                    var dairesorgu = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == aidat.DaireNo).FirstOrDefault();
                    dairesorgu.Borc += aidat.AidatTutar;
                    db.SaveChanges();
                    TempData["Basarili"] = "Ekstra Ek Eklenmiştir";


                }

                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = aidat.DaireNo + " ekstra borçlandırılmıştır",
                    Tarih = DateTime.Now,
                    Tur = "Ekleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();

            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu !";
                ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            }



            return RedirectToAction("DaireBorclandir", "AnaSayfa");
        }


        // ======================================================================
        // TOPLU GEÇMİŞE DÖNÜK BORÇLANDIRMA
        // Seçilen ay/yıl için, seçili (checkbox'u işaretli) her daireye ayrı tutar
        // ve ayrı tür (Aidat / Demirbaş) ile borç yazar. Dönem (Kasa) oluşturmaz;
        // bireysel DaireBorclandir gibi çalışır ama birden çok daireyi tek seferde
        // ve geçmiş aylara da işleyebilir. Gelecek ay engellenir, geçmiş serbesttir.
        // ======================================================================
        public ActionResult TopluGecmisBorclandir()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            Session["Aktif"] = "TopluGecmisBorclandir";
            Sabit();
            return View();
        }

        [HttpPost]
        public ActionResult TopluGecmisBorclandir(string AidatAy, int AidatYil, int[] DaireNolar, string[] Tutarlar, string[] Turler)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            Session["Aktif"] = "TopluGecmisBorclandir";

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    int yeniAyKodu = AyKoduBul(AidatAy);
                    if (yeniAyKodu == 0)
                    {
                        TempData["Hata"] = "Geçersiz ay seçimi.";
                        ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
                        Sabit();
                        return View();
                    }

                    // Gelecek dönem engeli (geçmiş serbest, bu özelliğin amacı geçmişe dönük borçlandırmadır)
                    int buAy = DateTime.Now.Month;
                    int buYil = DateTime.Now.Year;
                    if (AidatYil > buYil || (AidatYil == buYil && yeniAyKodu > buAy))
                    {
                        TempData["Hata"] = "Vakti gelmemiş (gelecek) bir döneme borçlandırma yapamazsınız.";
                        ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
                        Sabit();
                        return View();
                    }

                    if (DaireNolar == null || DaireNolar.Length == 0)
                    {
                        TempData["Hata"] = "Borçlandırılacak daire seçmediniz.";
                        ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
                        Sabit();
                        return View();
                    }

                    var daireler = db.Dairelers.Where(x => x.BinaID == BinaID).ToList();

                    // MÜKERRER YÖNETİMİ (sıralı ek numara):
                    // Bu ay/yıl için (ödenmiş/ödenmemiş fark etmeksizin) daha önce HERHANGİ bir kayıt
                    // (aidat veya ek) varsa, bütün parti tek tip olacak şekilde ay adına sıralı bir
                    // numara eklenir (ör. "Mayıs - 2"). Böylece geçmişe dönük ekleme ile asıl dönem
                    // birbirine karışmaz ve iki ayrı "Mayıs 2026" mükerrer gibi görünmez.
                    // Numara, mevcut en büyük numaranın bir fazlasıdır; hiç kayıt yoksa düz ay adı kullanılır.
                    string baseAy = AidatAy;
                    string ekOnek = baseAy + " - "; // "Mayıs - "

                    var mevcutAidatAylar = db.Aidats.AsNoTracking()
                        .Where(x => x.BinaID == BinaID && x.AidatYil == AidatYil
                                    && (x.AidatAy == baseAy || x.AidatAy.StartsWith(ekOnek)))
                        .Select(x => x.AidatAy).ToList();
                    var mevcutEkAylar = db.Eks.AsNoTracking()
                        .Where(x => x.BinaID == BinaID && x.EkYil == AidatYil
                                    && (x.EkAy == baseAy || x.EkAy.StartsWith(ekOnek)))
                        .Select(x => x.EkAy).ToList();

                    var tumEtiketler = mevcutAidatAylar.Concat(mevcutEkAylar).ToList();

                    // Mevcut en büyük sıra numarasını bul (düz "Mayıs" = 1 sayılır).
                    int mevcutMaxNo = 0;
                    foreach (var et in tumEtiketler)
                    {
                        if (et == baseAy)
                        {
                            if (mevcutMaxNo < 1) mevcutMaxNo = 1;
                        }
                        else if (et.StartsWith(ekOnek))
                        {
                            int no;
                            if (int.TryParse(et.Substring(ekOnek.Length).Trim(), out no) && no > mevcutMaxNo)
                                mevcutMaxNo = no;
                        }
                    }

                    // Hiç kayıt yoksa düz ay adı; varsa bir sonraki numara ile etiketli ay.
                    string efektifAy = (mevcutMaxNo == 0) ? baseAy : (ekOnek + (mevcutMaxNo + 1));
                    bool etiketlendi = (mevcutMaxNo > 0);

                    int eklenenSayisi = 0;
                    int atlananSayisi = 0;

                    for (int i = 0; i < DaireNolar.Length; i++)
                    {
                        int daireNo = DaireNolar[i];
                        string turHam = (Turler != null && i < Turler.Length) ? Turler[i] : "aidat";
                        decimal tutar = TutarParse((Tutarlar != null && i < Tutarlar.Length) ? Tutarlar[i] : null);

                        if (tutar <= 0)
                        {
                            atlananSayisi++;
                            continue; // tutar girilmemiş daire atlanır
                        }

                        var daire = daireler.FirstOrDefault(x => x.DaireNo == daireNo);
                        if (daire == null) { atlananSayisi++; continue; }

                        bool demirbasMi = (turHam == "demirbas" || turHam == "2");

                        if (demirbasMi)
                        {
                            db.Eks.Add(new Ek()
                            {
                                EkAy = efektifAy,
                                EkYil = AidatYil,
                                EkTutar = tutar,
                                DaireNo = daireNo,
                                BinaID = BinaID,
                                Durum = "A",
                            });
                        }
                        else
                        {
                            db.Aidats.Add(new Aidat()
                            {
                                AidatAy = efektifAy,
                                AidatYil = AidatYil,
                                AidatTutar = tutar,
                                DaireNo = daireNo,
                                BinaID = BinaID,
                                Durum = "A",
                                ZamEklendiMi = "H",
                            });
                        }

                        daire.Borc += tutar;
                        eklenenSayisi++;
                    }

                    if (eklenenSayisi == 0)
                    {
                        transaction.Rollback();
                        TempData["Hata"] = "Hiçbir daire borçlandırılmadı. (Hiçbir daireye tutar girilmemiş olabilir.)";
                        ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
                        Sabit();
                        return View();
                    }

                    // Tüm aidat/ek eklemeleri ve borç güncellemeleri tek SaveChanges ile
                    db.Hareketlers.Add(new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = efektifAy + " - " + AidatYil + " dönemi için " + eklenenSayisi + " daire toplu (geçmişe dönük) borçlandırılmıştır.",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    });

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["Basarili"] = eklenenSayisi + " daire \"" + efektifAy + " " + AidatYil + "\" dönemi için borçlandırıldı." +
                        (etiketlendi ? " Bu dönem daha önce mevcut olduğundan geçmiş borçlanma \"" + efektifAy + "\" olarak işaretlendi." : "") +
                        (atlananSayisi > 0 ? " (" + atlananSayisi + " daire tutar girilmediği için atlandı.)" : "");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Hata"] = "Bir hata oluştu! İşlemler geri alındı. Detay: " + ex.Message;
                }
            }

            ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
            Sabit();
            return View();
        }

        // Ay adını ay koduna çevirir (0 = geçersiz). Yardımcı.
        private int AyKoduBul(string ay)
        {
            switch (ay)
            {
                case "Ocak": return 1;
                case "Şubat": return 2;
                case "Mart": return 3;
                case "Nisan": return 4;
                case "Mayıs": return 5;
                case "Haziran": return 6;
                case "Temmuz": return 7;
                case "Ağustos": return 8;
                case "Eylül": return 9;
                case "Ekim": return 10;
                case "Kasım": return 11;
                case "Aralık": return 12;
                default: return 0;
            }
        }

        // "1.500" / "1.250,50" / "1500" gibi girişleri decimal'e çevirir (binlik nokta ayracı temizlenir).
        private decimal TutarParse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Replace(".", "").Replace(" ", "").Trim();
            decimal d;
            if (decimal.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;
            return 0;
        }


        public ActionResult DonemEkle()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DonemEkle";
            Sabit();

            // Özellik 1: En son eklenen dönemin aidat kayıtlarından EN SIK kullanılan (mod)
            // tutarı aidat kutusuna öneri olarak taşı. Tek bir düşük daire tutarının yanlışlıkla
            // referans alınmasını önlemek için "son kayıt" değil "en çok tekrar eden" tutar seçilir.
            HttpCookie onericookie = Request.Cookies["KullaniciBilgileri"];
            int oneriBinaID = Convert.ToInt32(onericookie.Values["BinaID"]);

            // Öncelik: Aidat Tanımlama'da bu ay için girilmiş tutar. Yoksa son dönemin modu.
            var buAyTanim = AidatTanimBul(oneriBinaID, DateTime.Now.Year, DateTime.Now.Month);
            if (buAyTanim.HasValue)
            {
                ViewBag.OnerilenAidat = TutarYaz(buAyTanim.Value);
                ViewBag.OneriKaynak = "tanim";
                return View();
            }

            var sonDonem = db.Kasas.AsNoTracking()
                .Where(x => x.BinaID == oneriBinaID)
                .OrderByDescending(x => x.KasaYil).ThenByDescending(x => x.AyKodu)
                .FirstOrDefault();

            if (sonDonem != null)
            {
                var sonAyAidatlari = db.Aidats.AsNoTracking()
                    .Where(x => x.BinaID == oneriBinaID && x.Durum == "A"
                                && x.AidatAy == sonDonem.KasaAy && x.AidatYil == sonDonem.KasaYil
                                && x.AidatTutar != null)
                    .Select(x => x.AidatTutar)
                    .ToList();

                if (sonAyAidatlari.Count > 0)
                {
                    decimal modTutar = sonAyAidatlari
                        .GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .ThenByDescending(g => g.Key)
                        .First().Key.Value;

                    ViewBag.OnerilenAidat = TutarYaz(modTutar);
                    ViewBag.OneriKaynak = "mod";
                }
            }

            return View();

        }

        // Binlik ayracı nokta, ondalık yoksa gösterme (ör. 1.500 veya 1.250,50)
        private static string TutarYaz(decimal tutar)
        {
            var tr = new System.Globalization.CultureInfo("tr-TR");
            return (tutar == Math.Floor(tutar)) ? tutar.ToString("#,##0", tr) : tutar.ToString("#,##0.##", tr);
        }

        // Aidat Tanımlama (AidatTanim) tablosundan ilgili bina/yıl/ay için tanımlı aidat; yoksa null.
        private decimal? AidatTanimBul(int BinaID, int yil, int ay)
        {
            return db.AidatTanims.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.AidatYil == yil && x.AidatAy == ay)
                .Select(x => (decimal?)x.Tutar)
                .FirstOrDefault();
        }


        [HttpPost]
        public ActionResult DonemEkle(Aidat aidat, Ek ek)
        {
            // Transaction işlemini en başta başlatıyoruz.
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                    int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                    int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

                    // 1. VALIDASYONLAR (KONTROLLER)
                    // Bina ayarlarını (YoneticiAidatEkleme bit alanı için) en başta çekiyoruz
                    var binaAyar = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);

                    // Bu dönem daha önce eklenmiş mi kontrolü (Aidat için)
                    var donemeklendimi = db.Aidats.FirstOrDefault(x => x.BinaID == BinaID && x.AidatAy == aidat.AidatAy && x.AidatYil == aidat.AidatYil && x.Durum == "A");

                    // Açılış bakiyesi kontrolü
                    var acilisbakiyesieklendimi = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);
                    if (acilisbakiyesieklendimi == null)
                    {
                        TempData["Hata"] = "Açılış bakiyesi eklemediğiniz için dönem eklenemedi. Açılış bakiyeniz yoksa 0 olarak ekleyiniz";
                        return View();
                    }

                    // Daire sayısı kontrolü
                    var tds = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);
                    int? tanimlanandairesayisi = tds.DaireSayisi ?? 0;
                    int? eds = db.Dairelers.Count(x => x.BinaID == BinaID);

                    if (tanimlanandairesayisi != eds)
                    {
                        TempData["Hata"] = "Eksik Daire Sakini Tanımlaması nedeniyle dönem ekleme işlemi gerçekleştirilemedi. Eklemeniz Gereken Daire Sayısı: " + tanimlanandairesayisi;
                        return View();
                    }

                    // Ay Kodu Belirleme
                    int yeniAyKodu = 0;
                    switch (aidat.AidatAy)
                    {
                        case "Ocak": yeniAyKodu = 1; break;
                        case "Şubat": yeniAyKodu = 2; break;
                        case "Mart": yeniAyKodu = 3; break;
                        case "Nisan": yeniAyKodu = 4; break;
                        case "Mayıs": yeniAyKodu = 5; break;
                        case "Haziran": yeniAyKodu = 6; break;
                        case "Temmuz": yeniAyKodu = 7; break;
                        case "Ağustos": yeniAyKodu = 8; break;
                        case "Eylül": yeniAyKodu = 9; break;
                        case "Ekim": yeniAyKodu = 10; break;
                        case "Kasım": yeniAyKodu = 11; break;
                        case "Aralık": yeniAyKodu = 12; break;
                    }

                    // Tarih Kontrolü
                    int buyil = DateTime.Now.Year;
                    int buay = DateTime.Now.Month;

                    if (aidat.AidatYil != buyil || yeniAyKodu != buay)
                    {
                        string cumle = DateTime.Now.ToString("MMMM") + " ayındayız bu ay haricinde dönem ekleyemezsiniz. Ekleyemediğiniz Dönem varsa tüm daireleri tek tek borçlandırmalısınız";
                        TempData["Hata"] = cumle;
                        return View();
                    }

                    // Önceki dönem kontrolü
                    var sonKasaDonemi = db.Kasas.Where(x => x.BinaID == BinaID).OrderByDescending(x => x.KasaYil).ThenByDescending(x => x.AyKodu).FirstOrDefault();
                    if (sonKasaDonemi != null && (aidat.AidatYil < sonKasaDonemi.KasaYil || (aidat.AidatYil == sonKasaDonemi.KasaYil && yeniAyKodu < sonKasaDonemi.AyKodu)))
                    {
                        TempData["Hata"] = "Önceki aylara dönem ekleyemezsiniz!";
                        return View();
                    }

                    // 2. BORÇLANDIRILACAK AYLARI BELİRLE (Özellik 3: atlanan ara ayları da doldur)
                    // En son açılan dönem ile hedef (içinde bulunulan) ay arasındaki TÜM eksik aylar
                    // sırayla borçlandırılır. İlk dönem ise yalnızca hedef ay işlenir.
                    var trKultur = new System.Globalization.CultureInfo("tr-TR");
                    var borclanacakAylar = new List<Tuple<int, int>>(); // (yil, ayKodu)

                    if (sonKasaDonemi == null)
                    {
                        borclanacakAylar.Add(Tuple.Create((int)aidat.AidatYil, yeniAyKodu));
                    }
                    else
                    {
                        var iter = new DateTime(sonKasaDonemi.KasaYil.Value, sonKasaDonemi.AyKodu.Value, 1).AddMonths(1);
                        var hedef = new DateTime((int)aidat.AidatYil, yeniAyKodu, 1);
                        while (iter <= hedef)
                        {
                            borclanacakAylar.Add(Tuple.Create(iter.Year, iter.Month));
                            iter = iter.AddMonths(1);
                        }
                    }

                    // 3. AİDAT EKLEME İŞLEMİ (Peşin Ödeyen Kontrollü, çok aylı)
                    bool aidatEklendi = false;
                    if (aidat.AidatTutar != null && borclanacakAylar.Count > 0)
                    {
                        aidatEklendi = true;
                        var daireler = db.Dairelers.Where(x => x.BinaID == BinaID).ToList();

                        // Peşin ödeyenleri ilgili tüm yıllar için tek seferde çekiyoruz (Performans için)
                        var ilgiliYillar = borclanacakAylar.Select(t => t.Item1).Distinct().ToList();

                        // YILLIK peşin ödeme: tüm yıl ödenmiş sayılır, o yıla hiç aidat eklenmez (mevcut davranış).
                        var pesinOdeyenlerListesi = db.PesinOdemelers
                                                        .Where(x => x.BinaID == BinaID && ilgiliYillar.Contains(x.Yil))
                                                        .Select(x => new { x.DaireID, x.Yil })
                                                        .ToList();

                        // AYLIK peşin ödeme: yalnızca seçili ay(lar) ödenmiş. O aylar için aidat YİNE eklenir
                        // ama borç artmaz; karşılığında anında (ödenmiş/onaylı) makbuz kesilir.
                        var aylikPesinListesi = db.AylikPesinOdemelers
                                                    .Where(x => x.BinaID == BinaID && ilgiliYillar.Contains(x.Yil))
                                                    .Select(x => new { x.DaireID, x.Yil, x.Ay })
                                                    .ToList();

                        // Aylık peşin ödeyen daire başına kesilecek makbuz satırlarını biriktiriyoruz (DaireID -> (ayAdi, yil, tutar)).
                        var pesinMakbuzKalemleri = new Dictionary<int, List<Tuple<string, int, decimal>>>();

                        // AİDAT TANIMLARI (AidatTanim): ara aylar kendi tanımlı tutarıyla borçlandırılır
                        // (tanım yoksa girilen tutar). Hedef ayda girilen tutar esastır. Kullanılan tutar
                        // tanım tablosuna da yazılır (yoksa eklenir, farklıysa güncellenir).
                        var aidatTanimlari = db.AidatTanims
                                                .Where(x => x.BinaID == BinaID && ilgiliYillar.Contains(x.AidatYil))
                                                .ToList();
                        var kullanilanTutarlar = new List<decimal>();

                        foreach (var ayTuple in borclanacakAylar)
                        {
                            int dYil = ayTuple.Item1;
                            int dAyKodu = ayTuple.Item2;
                            string dAyAdi = new DateTime(dYil, dAyKodu, 1).ToString("MMMM", trKultur);

                            bool hedefAyMi = (dYil == aidat.AidatYil && dAyKodu == yeniAyKodu);
                            var ayTanim = aidatTanimlari.FirstOrDefault(t => t.AidatYil == dYil && t.AidatAy == dAyKodu);
                            decimal ayTutar = (!hedefAyMi && ayTanim != null) ? ayTanim.Tutar : aidat.AidatTutar.Value;
                            kullanilanTutarlar.Add(ayTutar);

                            if (ayTanim == null)
                            {
                                db.AidatTanims.Add(new AidatTanim { BinaID = BinaID, AidatYil = dYil, AidatAy = dAyKodu, Tutar = ayTutar });
                            }
                            else if (ayTanim.Tutar != ayTutar)
                            {
                                ayTanim.Tutar = ayTutar;
                            }

                            // O ayın Kasa (devir) satırını oluştur. Devir zinciri için her ay kendi içinde kaydeder.
                            DonemKasaOlustur(dYil, dAyKodu, BinaID, acilisbakiyesieklendimi);

                            // Aylık peşin ödeyen daireler ÖNCE işlensin (makbuzları ilk sırada kesilsin).
                            var siraliDaireler = daireler
                                .OrderByDescending(d => aylikPesinListesi.Any(p => p.DaireID == d.DaireID && p.Yil == dYil && p.Ay == dAyKodu))
                                .ToList();

                            foreach (var item in siraliDaireler)
                            {
                                // Yönetici muafiyeti
                                if (item.YonetimdeMi == "E" && (binaAyar?.YoneticiAidatEkleme != true))
                                    continue;

                                // YILLIK Peşin Ödeyen Kontrolü (ilgili yıl bazında) — hiç aidat eklenmez
                                if (pesinOdeyenlerListesi.Any(p => p.DaireID == item.DaireID && p.Yil == dYil)) continue;

                                // AYLIK Peşin Ödeyen Kontrolü (yıl + ay bazında)
                                bool aylikPesinMi = aylikPesinListesi.Any(p => p.DaireID == item.DaireID && p.Yil == dYil && p.Ay == dAyKodu);

                                Aidat aidat1 = new Aidat()
                                {
                                    AidatAy = dAyAdi,
                                    AidatYil = dYil,
                                    AidatTutar = ayTutar,
                                    DaireNo = item.DaireNo,
                                    BinaID = BinaID,
                                    ZamEklendiMi = "H",
                                    // Aylık peşin ödenen ay doğrudan "P" (ödenmiş) açılır; borca eklenmez.
                                    Durum = aylikPesinMi ? "P" : "A",
                                };

                                db.Aidats.Add(aidat1);

                                if (aylikPesinMi)
                                {
                                    // Borç artmaz; bu ay için makbuz satırı biriktir.
                                    if (!pesinMakbuzKalemleri.ContainsKey(item.DaireID))
                                        pesinMakbuzKalemleri[item.DaireID] = new List<Tuple<string, int, decimal>>();
                                    pesinMakbuzKalemleri[item.DaireID].Add(Tuple.Create(dAyAdi, dYil, ayTutar));
                                }
                                else
                                {
                                    // Daire borcunu artır
                                    item.Borc += ayTutar;
                                }
                            }
                        }

                        // Tüm ayların aidatlarını ve borç güncellemelerini tek seferde kaydet.
                        db.SaveChanges();

                        // AYLIK PEŞİN ÖDEME MAKBUZLARI: her daire için tek makbuz (tüm peşin ayları satır olarak).
                        if (pesinMakbuzKalemleri.Count > 0)
                        {
                            var pesinMakbuzlar = new Dictionary<int, Makbuz>();
                            foreach (var kv in pesinMakbuzKalemleri)
                            {
                                decimal toplamTutar = kv.Value.Sum(t => t.Item3);
                                Makbuz pesinMakbuz = new Makbuz()
                                {
                                    BinaID = BinaID,
                                    DaireID = kv.Key,
                                    MakbuzTarihi = DateTime.Now.Date,
                                    MabuzTutar = toplamTutar,
                                    Durum = "A",
                                    OnayliMi = true,
                                    Aciklama = "Peşin Ödeme",
                                };
                                db.Makbuzs.Add(pesinMakbuz);
                                pesinMakbuzlar[kv.Key] = pesinMakbuz;
                            }
                            db.SaveChanges(); // MakbuzID'ler üretilir

                            foreach (var kv in pesinMakbuzKalemleri)
                            {
                                var pesinMakbuz = pesinMakbuzlar[kv.Key];
                                foreach (var kalem in kv.Value)
                                {
                                    db.MakbuzSatirs.Add(new MakbuzSatir()
                                    {
                                        MakbuzID = pesinMakbuz.MakbuzID,
                                        AyAdi = kalem.Item1,
                                        YilAdi = kalem.Item2,
                                        Tutar = kalem.Item3,
                                        DaireID = kv.Key,
                                        BinaID = BinaID,
                                        Durum = "A",
                                        EkMiAidatMi = "A",
                                    });
                                }
                            }
                            db.SaveChanges();
                            MakbuzNoDuzenle();
                        }

                        // Hareket Kaydı (tek veya çok aylı özet)
                        var ilk = borclanacakAylar.First();
                        var son = borclanacakAylar.Last();
                        string ilkAyAdi = new DateTime(ilk.Item1, ilk.Item2, 1).ToString("MMMM", trKultur);
                        string sonAyAdi = new DateTime(son.Item1, son.Item2, 1).ToString("MMMM", trKultur);
                        bool tekTutar = kullanilanTutarlar.Distinct().Count() == 1;
                        string olayAciklama = borclanacakAylar.Count == 1
                            ? aidat.AidatTutar + " TL tutarında " + sonAyAdi + " - " + son.Item1 + " Dönemi Eklenmiştir."
                            : (tekTutar ? aidat.AidatTutar + " TL tutarında " : "Aidat tanımlarındaki aylık tutarlarla (" + aidat.AidatTutar + " TL hedef ay) ")
                              + ilkAyAdi + " " + ilk.Item1 + " - " + sonAyAdi + " " + son.Item1 + " arası " + borclanacakAylar.Count + " dönem eklenmiştir.";

                        Hareketler hareketler = new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = olayAciklama,
                            Tarih = DateTime.Now,
                            Tur = "Ekleme",
                        };
                        db.Hareketlers.Add(hareketler);

                        // Bekleyen Makbuzları Onaylama
                        var onaylanmayanmakbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.OnayliMi == false).ToList();
                        foreach (var makbuz in onaylanmayanmakbuzlar)
                        {
                            makbuz.OnayliMi = true;
                        }

                        db.SaveChanges(); // Hareket ve Makbuz onaylarını kaydet
                    }

                    // 4. EK (DEMİRBAŞ) EKLEME İŞLEMİ
                    var donemeklendimi2 = db.Eks.FirstOrDefault(x => x.BinaID == BinaID && x.EkAy == aidat.AidatAy && x.EkYil == aidat.AidatYil && x.Durum == "A");

                    if (ek.EkTutar != null)
                    {
                        if (donemeklendimi2 == null)
                        {
                            var daireler2 = db.Dairelers.Where(x => x.BinaID == BinaID).ToList();

                            foreach (var item in daireler2)
                            {
                                // GÜNCELLEME: Aynı yönetim kontrolünü Ek/Demirbaş döngüsüne de uyguluyoruz.
                                // Eğer YoneticiAidatEkleme pasifse (true değilse) yöneticileri demirbaşlardan da muaf tutar.
                                if (item.YonetimdeMi == "E" && (binaAyar?.YoneticiAidatEkleme != true))
                                    continue;

                                // Ek'te peşin ödeyen muafiyeti yok, devam ediyoruz.

                                var daireno = item.DaireNo;

                                Ek ek1 = new Ek()
                                {
                                    EkAy = aidat.AidatAy,
                                    EkYil = aidat.AidatYil,
                                    EkTutar = ek.EkTutar,
                                    DaireNo = daireno,
                                    BinaID = BinaID,
                                    Durum = "A",
                                };

                                db.Eks.Add(ek1);

                                // Daire borcunu artır
                                item.Borc += ek.EkTutar;
                            }

                            // Ekler ve borç güncellemelerini toplu kaydet
                            db.SaveChanges();

                            Hareketler hareketler1 = new Hareketler()
                            {
                                BinaID = BinaID,
                                KullaniciID = KullaniciID,
                                OlayAciklama = ek.EkTutar + " TL tutarında " + aidat.AidatAy + " - " + aidat.AidatYil + " Ek/Demirbaş Dönemi Eklenmiştir.",
                                Tarih = DateTime.Now,
                                Tur = "Ekleme",
                            };
                            db.Hareketlers.Add(hareketler1);
                            db.SaveChanges();
                        }
                    }

                    // 5. SONUÇ VE TRANSACTION COMMIT
                    transaction.Commit();

                    bool ekEklendi = (ek.EkTutar != null && donemeklendimi2 == null);

                    if (!aidatEklendi && ekEklendi)
                    {
                        TempData["Basarili"] = "Aidat Daha Önce Eklendiği İçin Sadece Ek Eklendi";
                    }
                    else if (aidatEklendi && !ekEklendi)
                    {
                        TempData["Basarili"] = borclanacakAylar.Count > 1
                            ? borclanacakAylar.Count + " Dönem Başarıyla Eklendi"
                            : "Dönem Başarıyla Eklendi";
                    }
                    else if (aidatEklendi && ekEklendi)
                    {
                        TempData["Basarili"] = "Dönem Başarıyla Eklendi";
                    }
                    else
                    {
                        TempData["Hata"] = "Bu Dönem Daha Önce Eklendiği İçin İşlem Başarısız Oldu!";
                    }

                }
                catch (Exception ex)
                {
                    // Herhangi bir hata olursa yapılan TÜM işlemleri geri al.
                    transaction.Rollback();
                    TempData["Hata"] = "Bir Hata Oluştu! İşlemler geri alındı. Hata Detayı: " + ex.Message;
                }
            }

            Sabit();
            return View();
        }

        // Belirtilen ay/yıl için devir bakiyesini hesaplayıp o aya ait Kasa satırını oluşturur ve kaydeder.
        // Devir = bir önceki ayın kasası (yoksa açılış bakiyesi/son kasa) + o önceki ayın tahsilat/makbuz
        // gelirleri - giderleri. DonemEkle içindeki tek-ay mantığının çok aylı backfill için parametreli hali.
        private void DonemKasaOlustur(int yil, int ay, int BinaID, AcilisBakiye acilis)
        {
            var trKultur = new System.Globalization.CultureInfo("tr-TR");
            DateTime bugun = new DateTime(yil, ay, 1);
            DateTime oncekiAy = bugun.AddMonths(-1);
            int oncekiYil = oncekiAy.Year;
            int oncekiAyKodu = oncekiAy.Month;

            var son_kasa = db.Kasas.FirstOrDefault(x => x.KasaYil == oncekiYil && x.AyKodu == oncekiAyKodu && x.BinaID == BinaID);

            // Önceki ayın kasası yoksa devir bakiyesi olarak oluştur.
            if (son_kasa == null)
            {
                var oncekiAyAdi = new DateTime(oncekiYil, oncekiAyKodu, 1).ToString("MMMM", trKultur);
                var eklenensonkasa = db.Kasas.Where(x => x.BinaID == BinaID).OrderByDescending(x => x.KasaID).FirstOrDefault();

                decimal eklenecekaidat, eklenecekek;
                if (eklenensonkasa != null)
                {
                    eklenecekaidat = Convert.ToDecimal(eklenensonkasa.KasaAidat);
                    eklenecekek = Convert.ToDecimal(eklenensonkasa.KasaEk);
                }
                else
                {
                    eklenecekaidat = Convert.ToDecimal(acilis.AidatTutar);
                    eklenecekek = Convert.ToDecimal(acilis.EkTutar);
                }

                var yeniKasa = new Kasa
                {
                    KasaYil = oncekiYil,
                    AyKodu = oncekiAyKodu,
                    BinaID = BinaID,
                    KasaEk = eklenecekek,
                    KasaAidat = eklenecekaidat,
                    KasaAy = oncekiAyAdi,
                    KasaToplam = eklenecekek + eklenecekaidat
                };

                db.Kasas.Add(yeniKasa);
                db.SaveChanges();
            }

            var son_kasa2 = db.Kasas.FirstOrDefault(x => x.KasaYil == oncekiYil && x.AyKodu == oncekiAyKodu && x.BinaID == BinaID);
            decimal kasaek = Convert.ToDecimal(son_kasa2.KasaEk);
            decimal kasaaidat = Convert.ToDecimal(son_kasa2.KasaAidat);

            int oy = oncekiAy.Year;
            int oa = oncekiAy.Month;

            var makbuzIDListesi = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.MakbuzTarihi.Value.Year == oy && x.MakbuzTarihi.Value.Month == oa).Select(x => x.MakbuzID).ToList();

            var makbuzToplam = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == "A" && makbuzIDListesi.Contains((int)x.MakbuzID)).Sum(x => (decimal?)x.Tutar) ?? 0;
            var ektoplam2 = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == "E" && makbuzIDListesi.Contains((int)x.MakbuzID)).Sum(x => (decimal?)x.Tutar) ?? 0;
            var ektoplam1 = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == oy && x.TahsilatTarih.Value.Month == oa && x.DemirbasMi == true).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
            var giderektoplam = db.Giders.Where(x => x.GiderTuruID == 6 && x.Durum == "A" && x.BinaID == BinaID && x.GiderTarih.Value.Year == oy && x.GiderTarih.Value.Month == oa).Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var gidertoplam = db.Giders.Where(x => x.GiderTuruID != 6 && x.Durum == "A" && x.BinaID == BinaID && x.GiderTarih.Value.Year == oy && x.GiderTarih.Value.Month == oa).Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var aidattoplam3 = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == oy && x.TahsilatTarih.Value.Month == oa && x.DemirbasMi == false).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

            var aidattoplam = (makbuzToplam + kasaaidat + aidattoplam3) - gidertoplam;
            var ektoplam = (ektoplam1 + ektoplam2 + kasaek) - giderektoplam;
            var fulltoplam = aidattoplam + ektoplam;

            var ayAdi = new DateTime(yil, ay, 1).ToString("MMMM", trKultur);
            Kasa kasa = new Kasa()
            {
                KasaAy = ayAdi,
                KasaYil = yil,
                KasaAidat = aidattoplam,
                KasaEk = ektoplam,
                KasaToplam = fulltoplam,
                BinaID = BinaID,
                AyKodu = ay
            };
            db.Kasas.Add(kasa);
            db.SaveChanges();
        }

        // Makbuz belge numaralarını yeniden sıralar (AnaSayfa'ya özel kopya; MakbuzController ile aynı mantık).
        public void MakbuzNoDuzenle()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var makbuzliste = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderBy(x => x.MakbuzID).ToList();

            int mno = 0;
            foreach (var item in makbuzliste)
            {
                item.MakbuzNo = mno + 1;
                mno++;
            }
            db.SaveChanges();
        }

        // ================== DÖNEM İPTAL (Son dönemi sıfırlama) ==================
        // Yanlış tutarla açılan SON dönemi geri alır: o ay/yıla ait makbuz+satır, gider, tahsilat,
        // aidat, ek ve kasa kayıtlarını siler, daire borçlarını yeniden hesaplar.
        // Güvenlik: yöneticinin parolası + (2FA açıksa) Google Authenticator kodu gerekir.
        public ActionResult DonemIptal()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DonemIptal";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            // İptal edilebilecek son dönem
            var sonDonem = db.Kasas.AsNoTracking()
                .Where(x => x.BinaID == BinaID)
                .OrderByDescending(x => x.KasaYil).ThenByDescending(x => x.AyKodu)
                .FirstOrDefault();

            ViewBag.SonDonem = sonDonem;

            // 2FA açık mı?
            bool ikiAdimAktif = db.Database.SqlQuery<bool>(
                "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                KullaniciID).FirstOrDefault();
            ViewBag.IkiAdimAktif = ikiAdimAktif;

            return View();
        }

        [HttpPost]
        public ActionResult DonemIptal(string parola, string kod)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            // Güvenlik doğrulaması: parola
            var kullanici = db.Kullanicilars.FirstOrDefault(x => x.KullaniciID == KullaniciID);
            if (kullanici == null)
            {
                TempData["Hata"] = "Kullanıcı bulunamadı.";
                return RedirectToAction("DonemIptal");
            }

            string girilenHash = Crypto.Hash(parola ?? "", "MD5");
            if (!string.Equals(girilenHash, kullanici.Parola, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Hata"] = "Parola hatalı. Dönem iptali yapılmadı.";
                return RedirectToAction("DonemIptal");
            }

            // 2FA açıksa Google Authenticator kodu da zorunlu
            bool ikiAdimAktif = db.Database.SqlQuery<bool>(
                "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                KullaniciID).FirstOrDefault();

            if (ikiAdimAktif)
            {
                string secret = db.Database.SqlQuery<string>(
                    "SELECT TwoFactorSecret FROM Kullanicilar WHERE KullaniciID = @p0",
                    KullaniciID).FirstOrDefault();

                if (string.IsNullOrWhiteSpace(kod) || !TwoFactorHelper.ValidateCode(secret, kod))
                {
                    TempData["Hata"] = "İki adımlı doğrulama kodu hatalı. Dönem iptali yapılmadı.";
                    return RedirectToAction("DonemIptal");
                }
            }

            // İptal edilecek SON dönem
            var sonDonem = db.Kasas
                .Where(x => x.BinaID == BinaID)
                .OrderByDescending(x => x.KasaYil).ThenByDescending(x => x.AyKodu)
                .FirstOrDefault();

            if (sonDonem == null)
            {
                TempData["Hata"] = "İptal edilecek bir dönem bulunamadı.";
                return RedirectToAction("DonemIptal");
            }

            int donemYil = sonDonem.KasaYil ?? 0;
            int donemAyKodu = sonDonem.AyKodu ?? 0;
            string donemAyAdi = sonDonem.KasaAy;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Bu dönemden etkilenen daireleri (borç yeniden hesabı için) önceden topla.
                    var etkilenenDaireIdler = db.Dairelers.Where(x => x.BinaID == BinaID).Select(x => x.DaireID).ToList();

                    // 1. MakbuzSatir + Makbuz (o ay/yıl)
                    var makbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID
                        && x.MakbuzTarihi.Value.Year == donemYil && x.MakbuzTarihi.Value.Month == donemAyKodu).ToList();
                    var makbuzIdler = makbuzlar.Select(x => x.MakbuzID).ToList();

                    var makbuzSatirlar = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && makbuzIdler.Contains((int)x.MakbuzID)).ToList();

                    // ÖNEMLİ: İptal edilen döneme ait makbuzlar, GEÇMİŞ dönem aidat/ek borçlarının
                    // ödemesini de içerebilir. O geçmiş aidat/ek kayıtları ödendiği için Durum="P" olmuştur.
                    // Makbuz satırları silinmeden önce, iptal edilen dönem DIŞINDAKİ bir aidat/ek'i kapatan
                    // satırların ilgili kaydını tekrar "A" (ödenmemiş) yap ki borç yeniden hesapta doğru doğsun.
                    // (İptal edilen dönemin kendi aidat/ek'i zaten aşağıda tamamen silineceği için ona dokunulmaz.)
                    var daireNoMap = db.Dairelers.Where(x => x.BinaID == BinaID).ToDictionary(x => x.DaireID, x => x.DaireNo);
                    foreach (var satir in makbuzSatirlar)
                    {
                        bool ayniDonem = (satir.AyAdi == donemAyAdi && satir.YilAdi == donemYil);
                        if (ayniDonem) continue;

                        if (!satir.DaireID.HasValue || !daireNoMap.ContainsKey(satir.DaireID.Value)) continue;
                        int? dNo = daireNoMap[satir.DaireID.Value];

                        if (satir.EkMiAidatMi == "A")
                        {
                            var aidatGeri = db.Aidats.FirstOrDefault(x => x.BinaID == BinaID && x.DaireNo == dNo && x.AidatAy == satir.AyAdi && x.AidatYil == satir.YilAdi && x.Durum == "P");
                            if (aidatGeri != null) aidatGeri.Durum = "A";
                        }
                        else if (satir.EkMiAidatMi == "E")
                        {
                            var ekGeri = db.Eks.FirstOrDefault(x => x.BinaID == BinaID && x.DaireNo == dNo && x.EkAy == satir.AyAdi && x.EkYil == satir.YilAdi && x.Durum == "P");
                            if (ekGeri != null) ekGeri.Durum = "A";
                        }
                    }

                    db.MakbuzSatirs.RemoveRange(makbuzSatirlar);
                    db.Makbuzs.RemoveRange(makbuzlar);

                    // 2. Gider (o ay/yıl)
                    var giderler = db.Giders.Where(x => x.BinaID == BinaID
                        && x.GiderTarih.Value.Year == donemYil && x.GiderTarih.Value.Month == donemAyKodu).ToList();
                    db.Giders.RemoveRange(giderler);

                    // 3. Tahsilat (o ay/yıl)
                    var tahsilatlar = db.Tahsilats.Where(x => x.BinaID == BinaID
                        && x.TahsilatTarih.Value.Year == donemYil && x.TahsilatTarih.Value.Month == donemAyKodu).ToList();
                    db.Tahsilats.RemoveRange(tahsilatlar);

                    // 4. Aidat (bu dönem)
                    var aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.AidatAy == donemAyAdi && x.AidatYil == donemYil).ToList();
                    db.Aidats.RemoveRange(aidatlar);

                    // 5. Ek (bu dönem)
                    var ekler = db.Eks.Where(x => x.BinaID == BinaID && x.EkAy == donemAyAdi && x.EkYil == donemYil).ToList();
                    db.Eks.RemoveRange(ekler);

                    // 6. Kasa (bu dönem)
                    var kasalar = db.Kasas.Where(x => x.BinaID == BinaID && x.KasaYil == donemYil && x.AyKodu == donemAyKodu).ToList();
                    db.Kasas.RemoveRange(kasalar);

                    db.SaveChanges();

                    // 7. Belge numaralarını yeniden sırala
                    MakbuzNoDuzenle();
                    GiderNoDuzenle();
                    TahsilatNoDuzenle();

                    // 8. Daire borçlarını yeniden hesapla (aidat + ek, aktif kayıtlar üzerinden)
                    foreach (var daireId in etkilenenDaireIdler)
                    {
                        var daire = db.Dairelers.FirstOrDefault(x => x.BinaID == BinaID && x.DaireID == daireId);
                        if (daire == null) continue;

                        var aidatToplam = db.Aidats.Where(x => x.Durum == "A" && x.DaireNo == daire.DaireNo && x.BinaID == BinaID).Sum(x => (decimal?)x.AidatTutar) ?? 0;
                        var ekToplam = db.Eks.Where(x => x.Durum == "A" && x.DaireNo == daire.DaireNo && x.BinaID == BinaID).Sum(x => (decimal?)x.EkTutar) ?? 0;
                        daire.Borc = aidatToplam + ekToplam;
                    }

                    // 9. Hareket log'u
                    Hareketler hareket = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = donemAyAdi + " - " + donemYil + " dönemi iptal edildi (makbuz, gider, tahsilat, aidat, ek ve kasa kayıtları silindi).",
                        Tarih = DateTime.Now,
                        Tur = "İptal",
                    };
                    db.Hareketlers.Add(hareket);

                    db.SaveChanges();
                    transaction.Commit();

                    TempData["Basarili"] = donemAyAdi + " - " + donemYil + " dönemi başarıyla iptal edildi.";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Hata"] = "Dönem iptali sırasında hata oluştu, işlemler geri alındı. Hata: " + ex.Message;
                }
            }

            return RedirectToAction("DonemIptal");
        }

        public ActionResult EklenenAidatlar()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "EklenenAidatlar";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.Aidatlar = db.AidatViews.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.AidatID).ToList();
            return View();

        }

        public ActionResult AidatDuzenle(int? id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var Aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.AidatID == id).FirstOrDefault();
            if (id == null || Aidatlar == null)
            {
                return RedirectToAction("EklenenAidatlar", "AnaSayfa");
            }
            else
            {
                ViewBag.a = Aidatlar;
                return View();
            }
        }

        [HttpPost]
        public ActionResult AidatDuzenle(Aidat aidat, int AidatID)
        {
            try
            {
                if (aidat.AidatTutar != null)
                {
                    HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                    int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                    int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                    var Aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.AidatID == AidatID).FirstOrDefault();
                    Aidatlar.AidatTutar = aidat.AidatTutar;
                    db.SaveChanges();

                    var daire = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo).FirstOrDefault();
                    var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                    var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;

                    daire.Borc = dairetoplam + ektoplam;
                    db.SaveChanges();

                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = daire.DaireNo + " nolu dairenin " + Aidatlar.AidatAy + " - " + Aidatlar.AidatYil + " aidatı " + aidat.AidatTutar + " tl olarak güncellenmiştir.",
                        Tarih = DateTime.Now,
                        Tur = "Güncelleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();

                    TempData["Basarili"] = "Aidat Başarıyla Güncellendi";
                }
                else
                {
                    TempData["Hata"] = "Tutarı Boş Bırakamazsınız onun yerine 0 yazınız!";

                }

            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }

            return RedirectToAction("EklenenAidatlar", "AnaSayfa");
        }


        public ActionResult AidatSil(int? id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                var Aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.AidatID == id).FirstOrDefault();

                if (id != null && Aidatlar != null)
                {
                    var DaireNo2 = Aidatlar.DaireNo;

                    var DaireIDsorgu = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo2).FirstOrDefault();
                    var DaireID = DaireIDsorgu.DaireID;
                    var makbuzvarmi = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.AyAdi == Aidatlar.AidatAy && x.YilAdi == Aidatlar.AidatYil && x.Durum == "A" && x.DaireID == DaireID).FirstOrDefault();
                    if (makbuzvarmi == null)
                    {
                        Aidatlar.Durum = "S";
                        db.SaveChanges();
                        var daire = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo).FirstOrDefault();
                        var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                        var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == Aidatlar.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;

                        daire.Borc = dairetoplam + ektoplam;
                        db.SaveChanges();

                        Hareketler hareketler = new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = daire.DaireNo + " nolu dairenin " + Aidatlar.AidatAy + " - " + Aidatlar.AidatYil + " aidatı " + Aidatlar.AidatTutar + " tl tutarındaki aidatı silinmiştir.",
                            Tarih = DateTime.Now,
                            Tur = "Silme",
                        };
                        db.Hareketlers.Add(hareketler);
                        db.SaveChanges();

                        TempData["Basarili"] = "Aidat Başarıyla Silindi";
                    }
                    else
                    {
                        TempData["Hata"] = "Bu Aidatla İlgili Makbuz Oluşturulmuştur Önce Makbuzu Silmelisiniz.!";
                    }
                }


            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }

            return RedirectToAction("EklenenAidatlar", "AnaSayfa");
        }

        // Toplu aidat silme: seçili AidatID'leri soft-delete eder (Durum="S").
        // Makbuzu olan aidatlar atlanır. Etkilenen dairelerin borcu yeniden hesaplanır.
        [HttpPost]
        public ActionResult AidatTopluSil(int[] ids)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            if (ids == null || ids.Length == 0)
            {
                TempData["Hata"] = "Silinecek kayıt seçmediniz.";
                return RedirectToAction("EklenenAidatlar", "AnaSayfa");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.Durum == "A" && ids.Contains(x.AidatID)).ToList();

                    // Daire No -> DaireID eşlemesi
                    var daireMap = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID)
                        .ToDictionary(x => x.DaireNo, x => x.DaireID);

                    // Makbuz satırı olan (Ay+Yıl+DaireID) kombinasyonları (tek sorgu)
                    var makbuzSet = new HashSet<string>(
                        db.MakbuzSatirs.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A")
                            .Select(x => x.AyAdi + "|" + x.YilAdi + "|" + x.DaireID).ToList());

                    int silinen = 0, atlanan = 0;
                    var etkilenenDaireNolar = new HashSet<int?>();

                    foreach (var a in aidatlar)
                    {
                        int? daireID = (a.DaireNo != null && daireMap.ContainsKey(a.DaireNo)) ? daireMap[a.DaireNo] : (int?)null;
                        string anahtar = a.AidatAy + "|" + a.AidatYil + "|" + daireID;

                        if (daireID == null || makbuzSet.Contains(anahtar))
                        {
                            atlanan++;
                            continue; // makbuzu olan atlanır
                        }

                        a.Durum = "S";
                        etkilenenDaireNolar.Add(a.DaireNo);
                        silinen++;
                    }

                    if (silinen > 0)
                    {
                        db.SaveChanges(); // durum değişiklikleri DB'ye yansısın ki borç hesabı doğru olsun

                        foreach (var dno in etkilenenDaireNolar)
                        {
                            var daire = db.Dairelers.FirstOrDefault(x => x.BinaID == BinaID && x.DaireNo == dno);
                            if (daire == null) continue;
                            var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == dno && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                            var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == dno && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;
                            daire.Borc = dairetoplam + ektoplam;
                        }

                        db.Hareketlers.Add(new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = silinen + " aidat kaydı toplu olarak silinmiştir.",
                            Tarih = DateTime.Now,
                            Tur = "Silme",
                        });

                        db.SaveChanges();
                    }

                    transaction.Commit();

                    if (silinen > 0)
                        TempData["Basarili"] = silinen + " aidat kaydı silindi." + (atlanan > 0 ? " (" + atlanan + " kayıt makbuzu olduğu için atlandı.)" : "");
                    else
                        TempData["Hata"] = atlanan > 0 ? "Seçilen kayıtların tamamının makbuzu olduğu için hiçbiri silinmedi." : "Silinecek uygun kayıt bulunamadı.";
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["Hata"] = "Bir Hata Oluştu! İşlemler geri alındı.";
                }
            }

            return RedirectToAction("EklenenAidatlar", "AnaSayfa");
        }

        public ActionResult EklenenEkler()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "EklenenEkler";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.Ekler = db.EkViews.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.EkID).ToList();
            return View();

        }
        public ActionResult EkDuzenle(int? id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var Ekler = db.Eks.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkID == id).FirstOrDefault();
            if (id == null || Ekler == null)
            {
                return RedirectToAction("EklenenAidatlar", "AnaSayfa");
            }
            else
            {
                ViewBag.a = Ekler;
                return View();
            }
        }

        [HttpPost]
        public ActionResult EkDuzenle(Ek ek, int EkID)
        {
            try
            {
                if (ek.EkTutar != null)
                {
                    HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                    int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                    int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                    var Ekler = db.Eks.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkID == EkID).FirstOrDefault();
                    Ekler.EkTutar = ek.EkTutar;
                    db.SaveChanges();

                    var daire = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo).FirstOrDefault();
                    var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                    var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;

                    daire.Borc = dairetoplam + ektoplam;
                    db.SaveChanges();

                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = daire.DaireNo + " nolu dairenin " + Ekler.EkAy + " - " + Ekler.EkYil + " eki " + ek.EkTutar + " tl olarak güncellenmiştir.",
                        Tarih = DateTime.Now,
                        Tur = "Güncelleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();

                    TempData["Basarili"] = "Ek Başarıyla Güncellendi";
                }
                else
                {
                    TempData["Hata"] = "Tutarı Boş Bırakamazsınız onun yerine 0 yazınız!";

                }

            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }

            return RedirectToAction("EklenenEkler", "AnaSayfa");
        }


        public ActionResult EkSil(int? id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                var Ekler = db.Eks.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkID == id).FirstOrDefault();




                if (id != null && Ekler != null)
                {
                    var DaireNo2 = Ekler.DaireNo;

                    var DaireIDsorgu = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo2).FirstOrDefault();
                    var DaireID = DaireIDsorgu.DaireID;
                    var makbuzvarmi = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.AyAdi == Ekler.EkAy && x.YilAdi == Ekler.EkYil && x.DaireID == DaireID && x.Durum == "A").FirstOrDefault();
                    if (makbuzvarmi == null)
                    {
                        Ekler.Durum = "S";
                        db.SaveChanges();
                        var daire = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo).FirstOrDefault();
                        var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                        var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == Ekler.DaireNo && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;

                        daire.Borc = dairetoplam + ektoplam;
                        db.SaveChanges();

                        Hareketler hareketler = new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = daire.DaireNo + " nolu dairenin " + Ekler.EkAy + " - " + Ekler.EkYil + " aidatı " + Ekler.EkTutar + " tl tutarındaki aidatı silinmiştir.",
                            Tarih = DateTime.Now,
                            Tur = "Silme",
                        };
                        db.Hareketlers.Add(hareketler);
                        db.SaveChanges();

                        TempData["Basarili"] = "Ek Başarıyla Silindi";
                    }
                    else
                    {
                        TempData["Hata"] = "Bu Ekle İlgili Makbuz Oluşturulmuştur Önce Makbuzu Silmelisiniz.!";
                    }
                }


            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }

            return RedirectToAction("EklenenEkler", "AnaSayfa");
        }

        // Toplu ek/demirbaş silme: seçili EkID'leri soft-delete eder (Durum="S").
        // Makbuzu olan ekler atlanır. Etkilenen dairelerin borcu yeniden hesaplanır.
        [HttpPost]
        public ActionResult EkTopluSil(int[] ids)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            if (ids == null || ids.Length == 0)
            {
                TempData["Hata"] = "Silinecek kayıt seçmediniz.";
                return RedirectToAction("EklenenEkler", "AnaSayfa");
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var ekler = db.Eks.Where(x => x.BinaID == BinaID && x.Durum == "A" && ids.Contains(x.EkID)).ToList();

                    var daireMap = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID)
                        .ToDictionary(x => x.DaireNo, x => x.DaireID);

                    var makbuzSet = new HashSet<string>(
                        db.MakbuzSatirs.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A")
                            .Select(x => x.AyAdi + "|" + x.YilAdi + "|" + x.DaireID).ToList());

                    int silinen = 0, atlanan = 0;
                    var etkilenenDaireNolar = new HashSet<int?>();

                    foreach (var e in ekler)
                    {
                        int? daireID = (e.DaireNo != null && daireMap.ContainsKey(e.DaireNo)) ? daireMap[e.DaireNo] : (int?)null;
                        string anahtar = e.EkAy + "|" + e.EkYil + "|" + daireID;

                        if (daireID == null || makbuzSet.Contains(anahtar))
                        {
                            atlanan++;
                            continue;
                        }

                        e.Durum = "S";
                        etkilenenDaireNolar.Add(e.DaireNo);
                        silinen++;
                    }

                    if (silinen > 0)
                    {
                        db.SaveChanges();

                        foreach (var dno in etkilenenDaireNolar)
                        {
                            var daire = db.Dairelers.FirstOrDefault(x => x.BinaID == BinaID && x.DaireNo == dno);
                            if (daire == null) continue;
                            var dairetoplam = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == dno && x.Durum == "A").Sum(x => (decimal?)x.AidatTutar) ?? 0;
                            var ektoplam = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == dno && x.Durum == "A").Sum(x => (decimal?)x.EkTutar) ?? 0;
                            daire.Borc = dairetoplam + ektoplam;
                        }

                        db.Hareketlers.Add(new Hareketler()
                        {
                            BinaID = BinaID,
                            KullaniciID = KullaniciID,
                            OlayAciklama = silinen + " ek/demirbaş kaydı toplu olarak silinmiştir.",
                            Tarih = DateTime.Now,
                            Tur = "Silme",
                        });

                        db.SaveChanges();
                    }

                    transaction.Commit();

                    if (silinen > 0)
                        TempData["Basarili"] = silinen + " demirbaş kaydı silindi." + (atlanan > 0 ? " (" + atlanan + " kayıt makbuzu olduğu için atlandı.)" : "");
                    else
                        TempData["Hata"] = atlanan > 0 ? "Seçilen kayıtların tamamının makbuzu olduğu için hiçbiri silinmedi." : "Silinecek uygun kayıt bulunamadı.";
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    TempData["Hata"] = "Bir Hata Oluştu! İşlemler geri alındı.";
                }
            }

            return RedirectToAction("EklenenEkler", "AnaSayfa");
        }

        public ActionResult Giderler()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "Giderler";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            int ay = DateTime.Now.Month;
            int yil = DateTime.Now.Year;
            ViewBag.Giderler = db.GiderViews.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil).OrderByDescending(x => x.GiderID).ToList();
            ViewBag.SilinenGiderler = db.GiderViews.Where(x => x.BinaID == BinaID && x.Durum == "P").OrderByDescending(x => x.GiderID).ToList();
            // Sabit (tekrar eden) gider şablonları
            ViewBag.SabitGiderler = (from s in db.SabitGiders.AsNoTracking()
                                     join t in db.GiderTurus on s.GiderTuruID equals t.GiderTuruID into tg
                                     from t in tg.DefaultIfEmpty()
                                     where s.BinaID == BinaID && s.Durum == "A"
                                     orderby s.SabitGiderID descending
                                     select new SabitGiderListe
                                     {
                                         SabitGiderID = s.SabitGiderID,
                                         GiderAciklama = s.GiderAciklama,
                                         GiderTuruID = s.GiderTuruID,
                                         GiderTuruAdi = t.GiderTuruAdi,
                                         GiderTutar = s.GiderTutar
                                     }).ToList();
            DonemEklendiMi();
            ViewBag.GiderTuru = db.GiderTurus.OrderBy(x => x.GiderTuruAdi).ToList();
            return View();

        }


        [HttpPost]

        public ActionResult GiderEkle(Gider gider, string GiderTutar)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            try
            {
                // --- PARA BİRİMİ ÇEVİRME ---
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(GiderTutar))
                {
                    // Türk formatına göre (nokta binlik, virgül kuruş) çevir
                    parsedTutar = decimal.Parse(GiderTutar, new CultureInfo("tr-TR"));
                }
                gider.GiderTutar = parsedTutar;
                // ---------------------------

                var songider = db.Giders.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.GiderNo).FirstOrDefault();
                int gno = songider?.GiderNo ?? 0;

                int songiderno = gno + 1;
                gider.GiderNo = songiderno;

                gider.BinaID = BinaID;
                gider.GiderTarih = DateTime.Now.Date;
                gider.Durum = "A";

                db.Giders.Add(gider);
                db.SaveChanges();

                GiderNoDuzenle(); // Bu metodun varsa çalışır

                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    // Burada gider.GiderTutar kullanıyoruz (decimal hali)
                    OlayAciklama = gider.GiderTutar + " Tutarında " + gider.GiderNo + " numaralı gider eklendi.",
                    Tarih = DateTime.Now,
                    Tur = "Ekleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                // Eklenen giderin makbuzunu Giderler sayfasında yeni sekmede aç
                TempData["YeniGiderMakbuzID"] = gider.GiderID;
                TempData["Basarili"] = "Gider Başarıyla Eklendi";
            }
            catch (Exception ex) // Hata detayını görmek için ex ekledim
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
            }

            // Viewbag doldurma kısımların...
            ViewBag.Giderler = db.GiderViews.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.GiderID).ToList();
            ViewBag.SilinenGiderler = db.GiderViews.Where(x => x.BinaID == BinaID && x.Durum == "P").OrderByDescending(x => x.GiderID).ToList();
            ViewBag.GiderTuru = db.GiderTurus.OrderBy(x => x.GiderTuruAdi).ToList();

            return RedirectToAction("Giderler", "AnaSayfa");
        }

        // ============================================================
        // SABİT (TEKRAR EDEN) GİDER ŞABLONLARI
        // ============================================================

        [HttpPost]
        public ActionResult SabitGiderEkle(SabitGider sabitGider, string GiderTutar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            try
            {
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(GiderTutar))
                {
                    parsedTutar = decimal.Parse(GiderTutar, new CultureInfo("tr-TR"));
                }
                sabitGider.GiderTutar = parsedTutar;
                sabitGider.BinaID = BinaID;
                sabitGider.Durum = "A";

                db.SabitGiders.Add(sabitGider);
                db.SaveChanges();

                TempData["Basarili"] = "Sabit Gider Başarıyla Eklendi";
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
            }

            return RedirectToAction("Giderler", "AnaSayfa");
        }

        public ActionResult SabitGiderSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            try
            {
                var sabit = db.SabitGiders.FirstOrDefault(x => x.SabitGiderID == id && x.BinaID == BinaID);
                if (sabit != null)
                {
                    sabit.Durum = "P";
                    db.SaveChanges();
                    TempData["Basarili"] = "Sabit Gider Silindi";
                }
                else
                {
                    TempData["Hata"] = "Sabit gider bulunamadı!";
                }
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
            }

            return RedirectToAction("Giderler", "AnaSayfa");
        }

        [HttpPost]
        public ActionResult SabitGiderGuncelle(SabitGider sabitGider, string GiderTutar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            try
            {
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(GiderTutar))
                {
                    parsedTutar = decimal.Parse(GiderTutar, new CultureInfo("tr-TR"));
                }

                var mevcut = db.SabitGiders.FirstOrDefault(x => x.SabitGiderID == sabitGider.SabitGiderID && x.BinaID == BinaID);
                if (mevcut == null)
                {
                    TempData["Hata"] = "Sabit gider bulunamadı!";
                    return RedirectToAction("Giderler", "AnaSayfa");
                }

                mevcut.GiderTuruID = sabitGider.GiderTuruID;
                mevcut.GiderAciklama = sabitGider.GiderAciklama;
                mevcut.GiderTutar = parsedTutar;
                db.SaveChanges();

                TempData["Basarili"] = "Sabit Gider Başarıyla Güncellendi";
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
            }

            return RedirectToAction("Giderler", "AnaSayfa");
        }

        // Seçili sabit giderden gerçek bir Gider kaydı oluşturur ve makbuzunu açar.
        public ActionResult SabitGiderOlustur(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            // Dönem açık değilse gider eklenemez (GiderEkle ile aynı kural)
            DonemEklendiMi();
            if (ViewBag.DonemSorgu != true)
            {
                TempData["Hata"] = "Dönem kapalı. Önce dönemi başlatmalısınız.";
                return RedirectToAction("Giderler", "AnaSayfa");
            }

            var sabit = db.SabitGiders.AsNoTracking().FirstOrDefault(x => x.SabitGiderID == id && x.BinaID == BinaID && x.Durum == "A");
            if (sabit == null)
            {
                TempData["Hata"] = "Sabit gider bulunamadı!";
                return RedirectToAction("Giderler", "AnaSayfa");
            }

            try
            {
                var songider = db.Giders.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.GiderNo).FirstOrDefault();
                int songiderno = (songider?.GiderNo ?? 0) + 1;

                Gider gider = new Gider()
                {
                    GiderAciklama = sabit.GiderAciklama,
                    GiderTuruID = sabit.GiderTuruID,
                    GiderTutar = sabit.GiderTutar,
                    GiderNo = songiderno,
                    BinaID = BinaID,
                    GiderTarih = DateTime.Now.Date,
                    Durum = "A"
                };

                db.Giders.Add(gider);

                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = gider.GiderTutar + " Tutarında sabit giderden " + gider.GiderNo + " numaralı gider oluşturuldu.",
                    Tarih = DateTime.Now,
                    Tur = "Ekleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                GiderNoDuzenle();

                // Oluşturulan giderin makbuzunu (PDF) aç
                return RedirectToAction("GiderMakbuz", "AnaSayfa", new { GiderID = gider.GiderID });
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
                return RedirectToAction("Giderler", "AnaSayfa");
            }
        }

        public ActionResult GiderMakbuz(int? GiderID)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            if (GiderID == null)
            {
                return RedirectToAction("Index", "AnaSayfa");
            }

            // Gider ve bina bilgilerini al
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var gider = db.Giders.FirstOrDefault(x => x.GiderID == GiderID && x.BinaID == BinaID);

            if (gider == null)
            {
                return RedirectToAction("Index", "AnaSayfa");
            }

            string binaAdi2 = HttpUtility.UrlDecode(userCookie["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(userCookie["BinaAdres"]);

            MemoryStream workStream = new MemoryStream();
            Document document = new Document(PageSize.A4, 50f, 50f, 20f, 10f);
            PdfWriter.GetInstance(document, workStream).CloseStream = false;
            document.Open();

            // Fontlar
            string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
            Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
            Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);
            Font baslik = new Font(bfArialTurkish, 12, Font.BOLD);
            Font vukFont = new Font(bfArialTurkish, 8, Font.NORMAL); // Sadece bu eklendi

            // Üst Bilgi (Logo vs)
            string logoPath = Server.MapPath("~/Content/Admin/assets/img/binamakbuzlogo.png");
            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
            logo.ScaleAbsolute(100f, 100f);

            PdfPTable headerTable = new PdfPTable(3);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 20, 50, 30 });

            PdfPCell logoCell = new PdfPCell(logo);
            logoCell.Border = PdfPCell.NO_BORDER;
            headerTable.AddCell(logoCell);

            PdfPCell buildingInfoCell = new PdfPCell();
            buildingInfoCell.Border = PdfPCell.NO_BORDER;
            buildingInfoCell.AddElement(new Paragraph(binaAdi2.ToUpper(), titleFont));
            buildingInfoCell.AddElement(new Paragraph(binaAdres, subTitleFont));
            headerTable.AddCell(buildingInfoCell);

            PdfPCell receiptInfoCell = new PdfPCell();
            receiptInfoCell.Border = PdfPCell.NO_BORDER;
            receiptInfoCell.AddElement(new Paragraph("Tarih: " + (gider.GiderTarih.HasValue ? gider.GiderTarih.Value.ToString("dd/MM/yyyy") : ""), subTitleFont));
            receiptInfoCell.AddElement(new Paragraph("Makbuz No: " + gider.GiderNo, subTitleFont));
            receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            headerTable.AddCell(receiptInfoCell);

            document.Add(headerTable);

            // Başlık
            Paragraph title = new Paragraph("GİDER MAKBUZU", titleFont);
            title.SpacingBefore = 1f; // Boşluğu düzelttik
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);

            // Tablo
            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;
            table.SpacingBefore = 7f;
            table.SetWidths(new float[] { 70, 30 });

            table.AddCell(new PdfPCell(new Phrase("GİDER AÇIKLAMA", baslik)));
            table.AddCell(new PdfPCell(new Phrase("TUTAR", baslik)));

            table.AddCell(new PdfPCell(new Phrase(gider.GiderAciklama, tableFont)));
            table.AddCell(new PdfPCell(new Phrase(gider.GiderTutar.HasValue ? gider.GiderTutar.Value.ToString("C2") : "0,00 TL", tableFont)));

            PdfPCell totalCell = new PdfPCell(new Phrase("TOPLAM", baslik));
            totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(totalCell);
            table.AddCell(new PdfPCell(new Phrase(gider.GiderTutar.HasValue ? gider.GiderTutar.Value.ToString("C2") : "0,00 TL", tableFont)));

            document.Add(table);

            // Alt Kısım (Senin orijinal 2 sütunlu yapın)
            PdfPTable footerTable = new PdfPTable(2);
            footerTable.WidthPercentage = 100;
            footerTable.SetWidths(new float[] { 3, 1 });
            footerTable.SpacingBefore = 5f;

            // Sol taraf: Aldım yazısı
            PdfPCell reminderCell = new PdfPCell(new Paragraph(
                binaAdi2 + "'dan #" + (gider.GiderTutar.HasValue ? gider.GiderTutar.Value.ToString("N2") : "0,00") + "# TL aldım.",
                new Font(bfArialTurkish, 12, Font.ITALIC, BaseColor.BLACK)
            ));
            reminderCell.Border = PdfPCell.NO_BORDER;
            footerTable.AddCell(reminderCell);



            // Sağ taraf: AD SOYAD ve İMZA (Blok halinde sağa yaslı ve kendi içinde aynı hizada)
            PdfPCell imzaCell = new PdfPCell();
            imzaCell.Border = PdfPCell.NO_BORDER;
            imzaCell.PaddingRight = 50f;

            // İç tablo oluşturuyoruz ki metinler blok olarak aynı hizadan başlasın
            PdfPTable icTablo = new PdfPTable(1);
            icTablo.HorizontalAlignment = Element.ALIGN_RIGHT; // Tabloyu sağa yasla
            icTablo.WidthPercentage = 100;

            // 1. Satır: AD SOYAD
            PdfPCell cAdSoyad = new PdfPCell(new Paragraph("AD SOYAD", new Font(bfArialTurkish, 12, Font.BOLD, BaseColor.BLACK)));
            cAdSoyad.Border = PdfPCell.NO_BORDER;
            cAdSoyad.HorizontalAlignment = Element.ALIGN_RIGHT; // Metni sağa yasla
            cAdSoyad.PaddingBottom = 5f;
            icTablo.AddCell(cAdSoyad);

            // 2. Satır: İMZA
            PdfPCell cImza = new PdfPCell(new Paragraph("İMZA", new Font(bfArialTurkish, 12, Font.BOLD, BaseColor.BLACK)));
            cImza.Border = PdfPCell.NO_BORDER;
            cImza.HorizontalAlignment = Element.ALIGN_RIGHT; // Metni sağa yasla
            icTablo.AddCell(cImza);

            imzaCell.AddElement(icTablo);
            footerTable.AddCell(imzaCell);

            document.Add(footerTable);

            // VUK Notu (İmza alanından sonra boşluklu)
            Paragraph vukNotu = new Paragraph("Bu belge 213 sayılı Vergi Usul Kanunu hükümlerine tabi değildir. Sadece apartman içi kayıtların tutulması amacıyla düzenlenmiştir.", vukFont);
            vukNotu.SpacingBefore = 60f;
            vukNotu.Alignment = Element.ALIGN_CENTER;
            document.Add(vukNotu);

            document.Close();
            byte[] byteInfo = workStream.ToArray();
            workStream.Position = 0;

            Response.AppendHeader("Content-Disposition", "inline; filename=GiderMakbuz.pdf");
            return File(workStream, "application/pdf");
        }

        [HttpPost]
        // DİKKAT: Parametreye 'string GiderTutar' ekledik.
        public ActionResult GiderGuncelle(Gider gider, string GiderTutar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            try
            {
                // --- 1. PARA BİRİMİ DÖNÜŞTÜRME (STRING -> DECIMAL) ---
                // Gelen "10.000,50" verisini 10000.50 decimal sayısına çeviriyoruz.
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(GiderTutar))
                {
                    parsedTutar = decimal.Parse(GiderTutar, new CultureInfo("tr-TR"));
                }
                // -----------------------------------------------------

                // Güncellenecek kaydı bul
                var mevcutGider = db.Giders.FirstOrDefault(x => x.GiderID == gider.GiderID && x.BinaID == BinaID);

                if (mevcutGider == null)
                {
                    TempData["Hata"] = "Kayıt bulunamadı!";
                    return RedirectToAction("Giderler", "AnaSayfa");
                }

                // Tarih Kontrolü (Geçmiş dönem düzenlenemesin)
                int AyKontrol = DateTime.Now.Month;
                int YilKontrol = DateTime.Now.Year;

                if (mevcutGider.GiderTarih.Value.Month != AyKontrol || mevcutGider.GiderTarih.Value.Year != YilKontrol)
                {
                    TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri düzenleyemezsiniz!";
                    return RedirectToAction("Giderler", "AnaSayfa");
                }

                // Güncelleme İşlemi
                mevcutGider.GiderTuruID = gider.GiderTuruID;
                mevcutGider.GiderAciklama = gider.GiderAciklama;

                // --- 2. ÇEVİRDİĞİMİZ TUTARI ATIYORUZ ---
                mevcutGider.GiderTutar = parsedTutar;
                // ---------------------------------------

                db.SaveChanges();

                // Hareket Logu Ekle
                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    // Log mesajında da parsedTutar kullanıyoruz
                    OlayAciklama = $"{mevcutGider.GiderNo} numaralı gider güncellendi. (Yeni Tutar: {parsedTutar.ToString("N2")})",
                    Tarih = DateTime.Now,
                    Tur = "Guncelleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                TempData["Basarili"] = "Gider Başarıyla Güncellendi";
            }
            catch (Exception ex)
            {
                // Hata mesajını görmek için ex.Message ekledim, istersen kaldırabilirsin.
                TempData["Hata"] = "Güncelleme sırasında bir hata oluştu! " + ex.Message;
            }

            return RedirectToAction("Giderler", "AnaSayfa");
        }

        public ActionResult BorcluDairelerPDF()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            // Bina bilgilerini al
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            string kullaniciAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["KullaniciAdi"]);
            string adSoyad = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["AdSoyad"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);

            var borcluDaireler = db.Dairelers.Where(x => x.BinaID == BinaID && x.Borc > 0).OrderBy(x => x.DaireNo).ToList();
            var toplamAlacak = borcluDaireler.Sum(x => x.Borc);

            MemoryStream workStream = new MemoryStream();
            Document document = new Document(PageSize.A4, 50f, 50f, 20f, 10f);
            PdfWriter.GetInstance(document, workStream).CloseStream = false;
            document.Open();

            // Türkçe font
            string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
            Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
            Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);
            Font baslik = new Font(bfArialTurkish, 12, Font.BOLD);

            // Logo ve bina adı
            string logoPath = Server.MapPath("~/Content/Admin/assets/img/binamakbuzlogo.png");
            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
            logo.ScaleAbsolute(100f, 100f);
            logo.Alignment = iTextSharp.text.Image.ALIGN_LEFT;

            PdfPTable headerTable = new PdfPTable(3);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 30, 50, 20 });

            PdfPCell logoCell = new PdfPCell(logo);
            logoCell.Border = PdfPCell.NO_BORDER;
            headerTable.AddCell(logoCell);

            PdfPCell buildingInfoCell = new PdfPCell();
            buildingInfoCell.Border = PdfPCell.NO_BORDER;
            buildingInfoCell.AddElement(new Paragraph(binaAdi.ToUpper(), titleFont));
            buildingInfoCell.AddElement(new Paragraph("" + binaAdres, subTitleFont));
            headerTable.AddCell(buildingInfoCell);

            PdfPCell receiptInfoCell = new PdfPCell();
            receiptInfoCell.Border = PdfPCell.NO_BORDER;
            receiptInfoCell.AddElement(new Paragraph("Tarih: " + DateTime.Now.ToString("dd/MM/yyyy"), subTitleFont));
            receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            headerTable.AddCell(receiptInfoCell);

            document.Add(headerTable);

            // Başlık: Borçlular
            Paragraph title = new Paragraph("BORÇLULAR", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);

            // Borçlu daireler tablosu
            PdfPTable table = new PdfPTable(4);
            table.WidthPercentage = 100;
            table.SpacingBefore = 20f;
            table.SetWidths(new float[] { 15, 40, 15, 15 });

            //table.SetWidths(new float[] { 30, 50, 20 });

            table.AddCell(new PdfPCell(new Phrase("DAİRE NO", baslik)));
            table.AddCell(new PdfPCell(new Phrase("AD SOYAD", baslik)));
            table.AddCell(new PdfPCell(new Phrase("DURUM", baslik)));
            table.AddCell(new PdfPCell(new Phrase("BORÇ", baslik)));

            foreach (var daire in borcluDaireler)
            {
                table.AddCell(new PdfPCell(new Phrase(daire.DaireNo.ToString(), tableFont)));
                table.AddCell(new PdfPCell(new Phrase(daire.AdSoyad, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(daire.DaireDurum, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(daire.Borc.HasValue ? daire.Borc.Value.ToString("C2") : "0,00 TL", tableFont)));
            }

            // Toplam alacak kısmı
            PdfPCell totalCell = new PdfPCell(new Phrase("TOPLAM ALACAK", baslik));
            totalCell.Colspan = 3;
            totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(totalCell);
            table.AddCell(new PdfPCell(new Phrase(toplamAlacak.HasValue ? toplamAlacak.Value.ToString("C2") : "0,00 TL", tableFont)));

            document.Add(table);

            document.Close();

            byte[] byteInfo = workStream.ToArray();
            workStream.Write(byteInfo, 0, byteInfo.Length);
            workStream.Position = 0;

            Response.AppendHeader("Content-Disposition", "inline; filename=BorcluDaireler.pdf");
            return File(workStream, "application/pdf");
        }

        public ActionResult BorcluDairelerExcel()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            // Bina bilgilerini al
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            string binaAdi = HttpUtility.UrlDecode(userCookie.Values["BinaAdi"]);

            var borcluDaireler = db.Dairelers
                .Where(x => x.BinaID == BinaID && x.Borc > 0)
                .OrderBy(x => x.DaireNo)
                .ToList();
            var toplamAlacak = borcluDaireler.Sum(x => x.Borc);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Borçlu Daireler");

                // Başlık
                worksheet.Cell(1, 1).Value = $"{binaAdi.ToUpper()} - BORÇLU DAİRELER " + DateTime.Now.ToString("dd/MM/yyyy");
                worksheet.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Sütun başlıkları
                worksheet.Cell(3, 1).Value = "DAİRE NO";
                worksheet.Cell(3, 2).Value = "AD SOYAD";
                worksheet.Cell(3, 3).Value = "DURUM";
                worksheet.Cell(3, 4).Value = "BORÇ";

                var row = 4;
                foreach (var daire in borcluDaireler)
                {
                    worksheet.Cell(row, 1).Value = daire.DaireNo;
                    worksheet.Cell(row, 2).Value = daire.AdSoyad;
                    worksheet.Cell(row, 3).Value = daire.DaireDurum;
                    worksheet.Cell(row, 4).Value = daire.Borc.HasValue ? daire.Borc.Value.ToString("C2") : "0,00 TL";
                    row++;
                }

                // Toplam alacak
                worksheet.Cell(row, 3).Value = "TOPLAM ALACAK";
                worksheet.Cell(row, 3).Style.Font.Bold = true;
                worksheet.Cell(row, 4).Value = toplamAlacak.HasValue ? toplamAlacak.Value.ToString("C2") : "0,00 TL";
                worksheet.Cell(row, 4).Style.Font.Bold = true;

                // Sütun genişliklerini içeriğe göre ayarla
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    byte[] content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BorcluDaireler.xlsx");
                }
            }
        }


        public ActionResult GiderSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            var gidervarmi = db.Giders.Where(x => x.BinaID == BinaID && x.GiderID == id).FirstOrDefault();

            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (gidervarmi.GiderTarih.Value.Month != AyKontrol || gidervarmi.GiderTarih.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri silemezsiniz";
                return RedirectToAction("Giderler", "AnaSayfa");
            }


            if (gidervarmi != null)
            {
                gidervarmi.Durum = "P";
                db.SaveChanges();
                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = gidervarmi.GiderTutar + " Tutarında " + gidervarmi.GiderNo + " numaralı gider silindi.",
                    Tarih = DateTime.Now,
                    Tur = "Silme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();
                GiderNoDuzenle();
                TempData["Basarili"] = "Gider Başarıyla Silindi";

            }
            else
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }
            return RedirectToAction("Giderler", "AnaSayfa");
        }

        public ActionResult Tahsilat()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "Tahsilat";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            int ay = DateTime.Now.Month;
            int yil = DateTime.Now.Year;
            ViewBag.Tahsilatlar = db.TahsilatViews.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Month == ay && x.TahsilatTarih.Value.Year == yil).OrderByDescending(x => x.TahsilatID).ToList();
            ViewBag.SilinenTahsilatlar = db.TahsilatViews.Where(x => x.BinaID == BinaID && x.Durum == "P").OrderByDescending(x => x.TahsilatID).ToList();
            DonemEklendiMi();
            return View();

        }

        [HttpPost]
        public ActionResult TahsilatEkle(Tahsilat tahsilat, string TahsilatTutar)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            try
            {
                // --- PARA BİRİMİ DÖNÜŞTÜRME (1.000,50 -> Decimal) ---
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(TahsilatTutar))
                {
                    parsedTutar = decimal.Parse(TahsilatTutar, new CultureInfo("tr-TR"));
                }
                tahsilat.TahsilatTutar = parsedTutar;
                // ----------------------------------------------------

                var sontahsilat = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.TahsilatNo).FirstOrDefault();
                int tno = sontahsilat?.TahsilatNo ?? 0;

                int sontahsilatno = tno + 1;
                tahsilat.TahsilatNo = sontahsilatno;

                tahsilat.BinaID = BinaID;
                tahsilat.TahsilatTarih = DateTime.Now.Date;
                tahsilat.Durum = "A";

                db.Tahsilats.Add(tahsilat);
                db.SaveChanges();

                TempData["Basarili"] = "Tahsilat Başarıyla Eklendi";

                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    // Log mesajında da parsedTutar kullanıyoruz
                    OlayAciklama = parsedTutar.ToString("N2") + " Tutarında " + tahsilat.TahsilatNo + " numaralı tahsilat eklendi.",
                    Tarih = DateTime.Now,
                    Tur = "Ekleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                TahsilatNoDuzenle(); // Varsa çalışır
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir Hata Oluştu! " + ex.Message;
            }

            ViewBag.Tahsilatlar = db.TahsilatViews.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.TahsilatID).ToList();
            ViewBag.SilinenTahsilatlar = db.TahsilatViews.Where(x => x.BinaID == BinaID && x.Durum == "P").OrderByDescending(x => x.TahsilatID).ToList();

            return RedirectToAction("Tahsilat", "AnaSayfa");
        }

        [HttpPost]
        public ActionResult TahsilatGuncelle(Tahsilat tahsilat, string TahsilatTutar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            try
            {
                // --- PARA BİRİMİ DÖNÜŞTÜRME ---
                decimal parsedTutar = 0;
                if (!string.IsNullOrEmpty(TahsilatTutar))
                {
                    parsedTutar = decimal.Parse(TahsilatTutar, new CultureInfo("tr-TR"));
                }
                // ------------------------------

                var mevcutTahsilat = db.Tahsilats.FirstOrDefault(x => x.TahsilatID == tahsilat.TahsilatID && x.BinaID == BinaID);

                if (mevcutTahsilat == null)
                {
                    TempData["Hata"] = "Kayıt bulunamadı!";
                    return RedirectToAction("Tahsilat", "AnaSayfa");
                }

                int AyKontrol = DateTime.Now.Month;
                int YilKontrol = DateTime.Now.Year;

                if (mevcutTahsilat.TahsilatTarih.Value.Month != AyKontrol || mevcutTahsilat.TahsilatTarih.Value.Year != YilKontrol)
                {
                    TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri düzenleyemezsiniz!";
                    return RedirectToAction("Tahsilat", "AnaSayfa");
                }

                // Güncelleme
                mevcutTahsilat.TahsilatAciklama = tahsilat.TahsilatAciklama;
                // Çevrilen tutarı atıyoruz
                mevcutTahsilat.TahsilatTutar = parsedTutar;
                mevcutTahsilat.DemirbasMi = tahsilat.DemirbasMi;

                db.SaveChanges();

                // Loglama
                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = $"{mevcutTahsilat.TahsilatNo} numaralı tahsilat güncellendi. (Yeni Tutar: {parsedTutar.ToString("N2")})",
                    Tarih = DateTime.Now,
                    Tur = "Guncelleme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();

                TempData["Basarili"] = "Tahsilat Başarıyla Güncellendi";
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Güncelleme sırasında bir hata oluştu! " + ex.Message;
            }

            return RedirectToAction("Tahsilat", "AnaSayfa");
        }

        public ActionResult TahsilatMakbuz(int? TahsilatID)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            if (TahsilatID == null)
            {
                return RedirectToAction("Index", "AnaSayfa");
            }

            // Tahsilat ve bina bilgilerini al
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var tahsilat = db.Tahsilats.FirstOrDefault(x => x.TahsilatID == TahsilatID);
            var binaAdi = userCookie.Values["BinaAdi"].ToString();

            var kontrol = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatID == TahsilatID).FirstOrDefault();

            if (kontrol == null)
            {
                return RedirectToAction("Index", "AnaSayfa");
            }

            string kullaniciAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["KullaniciAdi"]);
            string adSoyad = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["AdSoyad"]);
            string binaAdi2 = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);


            MemoryStream workStream = new MemoryStream();
            // Yalnızca A4 kağıdının üst yarısını kullan
            Document document = new Document(PageSize.A4, 50f, 50f, 20f, 10f); // Sağdan ve soldan boşluklar 50f, üstten 20f, alttan 10f
            PdfWriter.GetInstance(document, workStream).CloseStream = false;
            document.Open();

            // Türkçe font
            string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
            Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
            Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);
            Font baslik = new Font(bfArialTurkish, 12, Font.BOLD);
            // Logo ve bina adı
            string logoPath = Server.MapPath("~/Content/Admin/assets/img/binamakbuzlogo.png");
            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
            logo.ScaleAbsolute(100f, 100f);
            logo.Alignment = iTextSharp.text.Image.ALIGN_LEFT;

            PdfPTable headerTable = new PdfPTable(3);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 20, 50, 30 });

            PdfPCell logoCell = new PdfPCell(logo);
            logoCell.Border = PdfPCell.NO_BORDER;
            headerTable.AddCell(logoCell);

            PdfPCell buildingInfoCell = new PdfPCell();
            buildingInfoCell.Border = PdfPCell.NO_BORDER;
            buildingInfoCell.AddElement(new Paragraph(binaAdi2.ToUpper(), titleFont));
            buildingInfoCell.AddElement(new Paragraph("" + binaAdres, subTitleFont));
            headerTable.AddCell(buildingInfoCell);

            PdfPCell receiptInfoCell = new PdfPCell();
            receiptInfoCell.Border = PdfPCell.NO_BORDER;
            receiptInfoCell.AddElement(new Paragraph("Tarih: " + kontrol.TahsilatTarih.Value.ToString("dd/MM/yyyy"), subTitleFont));
            receiptInfoCell.AddElement(new Paragraph("Makbuz No: " + tahsilat.TahsilatNo, subTitleFont));
            receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            headerTable.AddCell(receiptInfoCell);

            document.Add(headerTable);

            // Tahsilat Makbuzu Başlığı
            Paragraph title = new Paragraph("TAHSİLAT MAKBUZU", titleFont);
            title.SpacingBefore = -20f; // Negatif boşluk ile yukarı çekiyoruz
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);

            // Tahsilat bilgilerini içeren tablo
            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;
            table.SpacingBefore = 20f;
            table.SetWidths(new float[] { 70, 30 });

            table.AddCell(new PdfPCell(new Phrase("TAHSİLAT AÇIKLAMA", baslik)));
            table.AddCell(new PdfPCell(new Phrase("TUTAR", baslik)));

            table.AddCell(new PdfPCell(new Phrase(tahsilat.TahsilatAciklama, tableFont)));
            table.AddCell(new PdfPCell(new Phrase(tahsilat.TahsilatTutar.HasValue ? tahsilat.TahsilatTutar.Value.ToString("C2") : "0,00 TL", tableFont)));

            // Toplam Tutar
            PdfPCell totalCell = new PdfPCell(new Phrase("TOPLAM", baslik));
            totalCell.Colspan = 1;
            totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(totalCell);
            table.AddCell(new PdfPCell(new Phrase(tahsilat.TahsilatTutar.HasValue ? tahsilat.TahsilatTutar.Value.ToString("C2") : "0,00 TL", tableFont)));

            document.Add(table);

            // Not ve İmza kısmı için tablo
            PdfPTable footerTable = new PdfPTable(2);
            footerTable.WidthPercentage = 100;
            footerTable.SetWidths(new float[] { 3, 1 }); // %75 - %25 oranı

            // Not Kısmı (%75)
            PdfPCell reminderCell = new PdfPCell(new Paragraph(
               "",
                new Font(bfArialTurkish, 12, Font.ITALIC, BaseColor.BLACK)
            ));
            reminderCell.HorizontalAlignment = Element.ALIGN_LEFT;
            reminderCell.Border = PdfPCell.NO_BORDER;


            // İmza Kısmı (%25)
            PdfPCell imzaCell = new PdfPCell(new Paragraph("KAŞE - İMZA", new Font(bfArialTurkish, 12, Font.BOLD, BaseColor.BLACK)));
            imzaCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            imzaCell.Border = PdfPCell.NO_BORDER;
            imzaCell.PaddingRight = 50f; // Sağdan boşluk


            // Hücreleri tabloya ekle
            footerTable.AddCell(reminderCell);
            footerTable.AddCell(imzaCell);

            // Tabloyu belgeye ekle
            document.Add(footerTable);


            Font vukFont = new Font(bfArialTurkish, 8, Font.NORMAL);

            Paragraph vukNotu = new Paragraph("Bu belge 213 sayılı Vergi Usul Kanunu hükümlerine tabi değildir. Sadece apartman içi kayıtların tutulması amacıyla düzenlenmiştir.", vukFont);

            vukNotu.SpacingBefore = 60f; // İmza ve bilgilerden sonra aşağıya itiyoruz
            vukNotu.Alignment = Element.ALIGN_CENTER;


            document.Add(vukNotu);


            document.Close();

            byte[] byteInfo = workStream.ToArray();
            workStream.Write(byteInfo, 0, byteInfo.Length);
            workStream.Position = 0;

            Response.AppendHeader("Content-Disposition", "inline; filename=TahsilatMakbuz.pdf");
            return File(workStream, "application/pdf");
        }

        public ActionResult TahsilatSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            var tahsilatvarmi = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatID == id).FirstOrDefault();



            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (tahsilatvarmi.TahsilatTarih.Value.Month != AyKontrol || tahsilatvarmi.TahsilatTarih.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri silemezsiniz";
                return RedirectToAction("Tahsilat", "AnaSayfa");
            }

            if (tahsilatvarmi != null)
            {
                tahsilatvarmi.Durum = "P";
                db.SaveChanges();
                Hareketler hareketler = new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = tahsilatvarmi.TahsilatTutar + " Tutarında " + tahsilatvarmi.TahsilatNo + " numaralı tahsilat silindi.",
                    Tarih = DateTime.Now,
                    Tur = "Silme",
                };
                db.Hareketlers.Add(hareketler);
                db.SaveChanges();
                TahsilatNoDuzenle();
                TempData["Basarili"] = "Tahsilat Başarıyla Silindi";

            }
            else
            {
                TempData["Hata"] = "Bir Hata Oluştu!";

            }
            return RedirectToAction("Tahsilat", "AnaSayfa");
        }

        public ActionResult BorcluDaireler()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "BorcluDaireler";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.BorcluDaireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID && x.Borc > 0).OrderBy(x => x.DaireNo).ToList();
            return View();
        }

        public void GiderNoDuzenle()
        {

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var makbuzliste = db.Giders.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderBy(x => x.GiderID).ToList();

            int mno = 0;
            foreach (var item in makbuzliste)
            {

                item.GiderNo = mno + 1;
                mno++;
            }
            db.SaveChanges();
        }

        public void TahsilatNoDuzenle()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var makbuzliste = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderBy(x => x.TahsilatID).ToList();

            int mno = 0;
            foreach (var item in makbuzliste)
            {

                item.TahsilatNo = mno + 1;
                mno++;
            }
            db.SaveChanges();
        }


        public ActionResult AcilisBakiye()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var bakiyeler = db.AcilisBakiyes.AsNoTracking().Where(x => x.BinaID == BinaID).ToList();
            var a = bakiyeler.Count;

            Session["Aktif"] = "AcilisBakiye";
            Sabit();
            if (a > 1)
            {
                ViewBag.Durum = false;
            }

            if (a == 0)
            {
                ViewBag.Durum = true;
            }

            ViewBag.Bakiye = bakiyeler;

            return View();
        }

        [HttpPost]
        public ActionResult AcilisBakiyeEkle(AcilisBakiye acilisBakiye)
        {

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var a = db.AcilisBakiyes.Where(x => x.BinaID == BinaID).FirstOrDefault();

            if (a == null)
            {
                decimal ektutar = Convert.ToDecimal(acilisBakiye.EkTutar);
                decimal aidattutar = Convert.ToDecimal(acilisBakiye.AidatTutar);
                decimal toplam = ektutar + aidattutar;

                acilisBakiye.ToplamTutar = toplam;
                acilisBakiye.BinaID = BinaID;

                db.AcilisBakiyes.Add(acilisBakiye);
                db.SaveChanges();

                TempData["Basarili"] = "Açılış Bakiyesi Başarıyla Eklendi";
            }
            else
            {
                TempData["Hata"] = "Bir Hata Oluştu";
            }



            return RedirectToAction("AcilisBakiye", "AnaSayfa");
        }


        public ActionResult DaireSorgu(int? DaireNo)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            Session["Aktif"] = "DaireSorgu";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            if (DaireNo == null)
            {
                ViewBag.DaireNo = null;
            }

            if (DaireNo != null)
            {
                var varmi = db.Dairelers.Where(x => x.DaireNo == DaireNo && x.BinaID == BinaID).FirstOrDefault();

                if (varmi == null)
                {
                    TempData["Hata"] = "Daire Bulunamadı";
                    return View();
                }

                ViewBag.b = varmi;

                // AİDATLAR: Hem Ödenen (P) Hem Ödenmeyen (A) gelsin, ID'ye göre tersten sıralansın
                ViewBag.Aidat = db.Aidats.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .OrderByDescending(x => x.AidatID)
                    .ToList();

                // DEMİRBAŞLAR: Hem Ödenen (P) Hem Ödenmeyen (A) gelsin, ID'ye göre tersten sıralansın
                ViewBag.Ek = db.Eks.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .OrderByDescending(x => x.EkID)
                    .ToList();

                ViewBag.DaireNo = DaireNo;
            }

            return View();
        }


        public ActionResult DaireOdemeDurumu(int? DaireNo)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            Session["Aktif"] = "DaireOdemeDurumu";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.DaireNo = null;

            if (DaireNo != null)
            {
                var daire = db.Dairelers.AsNoTracking()
                    .FirstOrDefault(x => x.DaireNo == DaireNo && x.BinaID == BinaID);

                if (daire == null)
                {
                    TempData["Hata"] = "Daire Bulunamadı";
                    return View();
                }

                ViewBag.b = daire;
                ViewBag.DaireNo = DaireNo;

                // Aidat dönemleri (ödenen P / ödenmeyen A) - aylık durum ızgarası için
                var aidatlar = db.Aidats.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .Select(x => new { x.AidatYil, x.AidatAy, x.Durum, x.AidatTutar })
                    .ToList();

                // Demirbaş dönemleri
                var ekler = db.Eks.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .Select(x => new { x.EkYil, x.EkAy, x.Durum, x.EkTutar })
                    .ToList();

                // Fiili ödeme günleri (makbuz tarihleri) - takvimde işaretlenecek
                var makbuzlar = db.Makbuzs.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.DaireID == daire.DaireID && x.Durum == "A" && x.MakbuzTarihi != null)
                    .Select(x => new { x.MakbuzTarihi, x.MabuzTutar, x.MakbuzNo })
                    .ToList();

                var odemeGunleri = makbuzlar
                    .Where(x => x.MakbuzTarihi.HasValue)
                    .Select(x => new
                    {
                        yil = x.MakbuzTarihi.Value.Year,
                        ay = x.MakbuzTarihi.Value.Month,
                        gun = x.MakbuzTarihi.Value.Day,
                        tutar = x.MabuzTutar ?? 0,
                        no = x.MakbuzNo
                    })
                    .OrderBy(x => x.yil).ThenBy(x => x.ay).ThenBy(x => x.gun)
                    .ToList();

                ViewBag.AidatJson = Newtonsoft.Json.JsonConvert.SerializeObject(aidatlar);
                ViewBag.EkJson = Newtonsoft.Json.JsonConvert.SerializeObject(ekler);
                ViewBag.OdemeJson = Newtonsoft.Json.JsonConvert.SerializeObject(odemeGunleri);
            }

            return View();
        }


        [HttpPost]
        public ActionResult GecikmeZammı(Aidat aidat, string tutar, bool? durum)
        {

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            decimal tl = Decimal.Parse(tutar);
            int yil = DateTime.Now.Year;
            string ay = DateTime.Now.ToString("MMMM");


            List<Aidat> oa;

            if (durum == null)
            {
                oa = db.Aidats
                .Where(x => x.Durum == "A" && x.ZamEklendiMi == "H" && x.BinaID == BinaID && !(x.AidatYil == yil && x.AidatAy == ay))
                .ToList();
            }
            else
            {
                oa = db.Aidats
                 .Where(x => x.Durum == "A" && x.BinaID == BinaID && !(x.AidatYil == yil && x.AidatAy == ay))
                 .ToList();
            }

            if (oa == null || oa.Count == 0)
            {
                TempData["Hata"] = "Ödenmeyen geçmiş aidat dönemi bulunamadı";
                return RedirectToAction("DaireBorclandir", "AnaSayfa");

            }

            foreach (var item in oa)
            {
                int DaireNo = Convert.ToInt32(item.DaireNo);
                var dsh = db.Dairelers.Where(x => x.DaireNo == DaireNo && x.BinaID == BinaID).FirstOrDefault();

                int DaireID2 = dsh.DaireID;

                item.AidatTutar += tl;
                item.ZamEklendiMi = "E";
                db.SaveChanges();

                borcduzenle(DaireID2);

            }

            Hareketler hareketler = new Hareketler()
            {
                BinaID = BinaID,
                KullaniciID = KullaniciID,
                OlayAciklama = tl + " tutarında gecikme zammı eklendi",
                Tarih = DateTime.Now,
                Tur = "Ekleme",
            };
            db.Hareketlers.Add(hareketler);
            db.SaveChanges();


            TempData["Basarili"] = "Zamanı Geçmiş Aidat Borçlarına Gecikme Zammı Eklendi";

            return RedirectToAction("DaireBorclandir", "AnaSayfa");
        }


        public void borcduzenle(int DaireID)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var borc = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireID == DaireID).FirstOrDefault();

            var aidat = db.Aidats.Where(x => x.Durum == "A" && x.DaireNo == borc.DaireNo && x.BinaID == BinaID).Sum(x => (decimal?)x.AidatTutar) ?? 0;
            var ek = db.Eks.Where(x => x.Durum == "A" && x.DaireNo == borc.DaireNo && x.BinaID == BinaID).Sum(x => (decimal?)x.EkTutar) ?? 0;
            decimal toplam = aidat + ek;

            borc.Borc = toplam;
            db.SaveChanges();

        }

        public ActionResult Notlar()
        {
            Session["Aktif"] = "Notlar";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.n = db.Notlars.Where(x => x.BinaID == BinaID).FirstOrDefault();
            return View();
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult NotEkle(Notlar not)
        {


            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

                var sorgu = db.Notlars.Where(x => x.BinaID == BinaID).FirstOrDefault();
                if (sorgu == null)
                {
                    not.BinaID = BinaID;
                    db.Notlars.Add(not);
                    db.SaveChanges();
                    TempData["Basarili"] = "Not Başarıyla Eklendi.";
                }
                else
                {
                    sorgu.Aciklama = not.Aciklama;
                    sorgu.BorcAciklama = not.BorcAciklama;
                    db.SaveChanges();
                    TempData["Basarili"] = "Not Başarıyla Güncellendi.";
                }


            }
            catch (Exception)
            {

                TempData["Hata"] = "Bir Hata Oluştu";

            }


            return RedirectToAction("Notlar", "AnaSayfa");
        }


        public ActionResult PesinOdemeler()
        {
            Session["Aktif"] = "PesinOdemeler";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.List = db.PesinOdemelerViews.AsNoTracking().Where(x => x.BinaID == BinaID).ToList();
            ViewBag.Daireler = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();

            // AYLIK peşin ödemeler: daire + yıl bazında grupla, daire bilgisiyle birleştir (salt-okunur DTO).
            var aylikRaw = db.AylikPesinOdemelers.AsNoTracking().Where(x => x.BinaID == BinaID).ToList();
            var daireMap = db.Dairelers.AsNoTracking().Where(x => x.BinaID == BinaID).ToList();
            var aylikList = aylikRaw
                .GroupBy(x => new { x.DaireID, x.Yil })
                .Select(g =>
                {
                    var d = daireMap.FirstOrDefault(dd => dd.DaireID == g.Key.DaireID);
                    return new AylikPesinOdemeListe
                    {
                        DaireID = g.Key.DaireID,
                        Yil = g.Key.Yil,
                        DaireNo = d?.DaireNo ?? 0,
                        AdSoyad = d?.AdSoyad,
                        Aylar = g.Select(z => z.Ay).OrderBy(z => z).ToList()
                    };
                })
                .OrderBy(x => x.DaireNo)
                .ToList();
            ViewBag.AylikList = aylikList;
            return View();
        }

        [HttpPost]
        public ActionResult PesinOdemeEkle(PesinOdemeler pesinOdemeler)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            int daireid = pesinOdemeler.DaireID;

            var kontrol = db.PesinOdemelers.Where(x => x.BinaID == BinaID && x.DaireID == daireid && x.Yil == DateTime.Now.Year).FirstOrDefault();
            if (kontrol != null)
            {
                TempData["Hata"] = "Bu Daire İçin Peşin Ödeme Zaten Eklenmiş";
                return RedirectToAction("PesinOdemeler", "AnaSayfa");
            }

            pesinOdemeler.DaireID = daireid;
            pesinOdemeler.BinaID = BinaID;
            pesinOdemeler.Yil = DateTime.Now.Year;
            db.PesinOdemelers.Add(pesinOdemeler);
            db.SaveChanges();
            TempData["Basarili"] = "Peşin Ödeme Başarıyla Eklendi";

            return RedirectToAction("PesinOdemeler");
        }

        //PesinOdemeSil

        public ActionResult PesinOdemeSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var pesinodeme = db.PesinOdemelers.Where(x => x.BinaID == BinaID && x.ID == id).FirstOrDefault();
            if (pesinodeme != null)
            {
                db.PesinOdemelers.Remove(pesinodeme);
                db.SaveChanges();
                TempData["Basarili"] = "Peşin Ödeme Başarıyla Silindi";
            }
            else
            {
                TempData["Hata"] = "Bir Hata Oluştu!";
            }
            return RedirectToAction("PesinOdemeler", "AnaSayfa");
        }

        // =====================================================================
        // AYLIK PEŞİN ÖDEME (ay bazlı) — AylikPesinOdemeler tablosu
        // Her ödenen ay için bir satır (DaireID + Yil + Ay). Dönem eklenirken
        // o aylara aidat yine eklenir ama borç artmaz; anında makbuz kesilir.
        // =====================================================================

        [HttpPost]
        public ActionResult AylikPesinOdemeEkle(int DaireID, int[] Aylar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int Yil = DateTime.Now.Year;

            if (Aylar == null || Aylar.Length == 0)
            {
                TempData["Hata"] = "En az bir ay seçmelisiniz.";
                return RedirectToAction("PesinOdemeler", "AnaSayfa");
            }

            // Yıllık peşin ödemesi olan daireye ayrıca aylık eklenmez (çakışma).
            var yillikVar = db.PesinOdemelers.Any(x => x.BinaID == BinaID && x.DaireID == DaireID && x.Yil == Yil);
            if (yillikVar)
            {
                TempData["Hata"] = "Bu daire için bu yıl zaten YILLIK peşin ödeme var. Aylık eklenemez.";
                return RedirectToAction("PesinOdemeler", "AnaSayfa");
            }

            // Mevcut ayları çek, sadece eksik olanları ekle (çift kayıt önleme).
            var mevcutAylar = db.AylikPesinOdemelers
                .Where(x => x.BinaID == BinaID && x.DaireID == DaireID && x.Yil == Yil)
                .Select(x => x.Ay)
                .ToList();

            foreach (var ay in Aylar.Distinct())
            {
                if (ay < 1 || ay > 12) continue;
                if (mevcutAylar.Contains(ay)) continue;
                db.AylikPesinOdemelers.Add(new AylikPesinOdemeler
                {
                    DaireID = DaireID,
                    Yil = Yil,
                    Ay = ay,
                    BinaID = BinaID,
                });
            }
            db.SaveChanges();
            TempData["Basarili"] = "Aylık peşin ödeme başarıyla kaydedildi.";
            return RedirectToAction("PesinOdemeler", "AnaSayfa");
        }

        [HttpPost]
        public ActionResult AylikPesinOdemeGuncelle(int DaireID, int Yil, int[] Aylar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            if (Aylar == null || Aylar.Length == 0)
            {
                TempData["Hata"] = "En az bir ay seçmelisiniz. Tümünü kaldırmak için kaydı silin.";
                return RedirectToAction("PesinOdemeler", "AnaSayfa");
            }

            // İlgili daire+yıl grubunun eski kayıtlarını kaldır, yeni seçimi ekle.
            var eskiler = db.AylikPesinOdemelers
                .Where(x => x.BinaID == BinaID && x.DaireID == DaireID && x.Yil == Yil)
                .ToList();
            if (eskiler.Any())
                db.AylikPesinOdemelers.RemoveRange(eskiler);

            foreach (var ay in Aylar.Distinct())
            {
                if (ay < 1 || ay > 12) continue;
                db.AylikPesinOdemelers.Add(new AylikPesinOdemeler
                {
                    DaireID = DaireID,
                    Yil = Yil,
                    Ay = ay,
                    BinaID = BinaID,
                });
            }
            db.SaveChanges();
            TempData["Basarili"] = "Aylık peşin ödeme güncellendi.";
            return RedirectToAction("PesinOdemeler", "AnaSayfa");
        }

        public ActionResult AylikPesinOdemeSil(int DaireID, int Yil)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            var kayitlar = db.AylikPesinOdemelers
                .Where(x => x.BinaID == BinaID && x.DaireID == DaireID && x.Yil == Yil)
                .ToList();
            if (kayitlar.Any())
            {
                db.AylikPesinOdemelers.RemoveRange(kayitlar);
                db.SaveChanges();
                TempData["Basarili"] = "Aylık peşin ödeme kaydı silindi.";
            }
            else
            {
                TempData["Hata"] = "Kayıt bulunamadı!";
            }
            return RedirectToAction("PesinOdemeler", "AnaSayfa");
        }

        // ============================================================
        //  AİDAT HESAPLAMA / TAHMİNİ AİDAT BELİRLEME
        //  Bir yılın giderlerini gider türüne göre gruplayıp (demirbaş ayrı),
        //  toplanan aidat geliriyle ve geçmiş yılla kıyaslar; daire başına
        //  gereken aylık aidatı ve enflasyon/artış oranıyla tahmini aidatı hesaplar.
        // ============================================================

        // Bir yıl için toplanan aidat ve demirbaş gelirini hesaplar
        // (makbuz satırları + tahsilatlar) — DetayliGelirGider ile aynı mantık.
        private void AidatGelirHesapla(int BinaID, int yil, out decimal toplananAidat, out decimal toplananDemirbas)
        {
            var tahsilatlar = db.Tahsilats.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.TahsilatTarih.Value.Year == yil && x.Durum == "A")
                .Select(x => new { x.DemirbasMi, x.TahsilatTutar })
                .ToList();
            decimal tahAidat = tahsilatlar.Where(x => x.DemirbasMi == false).Sum(x => x.TahsilatTutar) ?? 0;
            decimal tahDemirbas = tahsilatlar.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;

            var makbuzIdleri = db.Makbuzs.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.MakbuzTarihi.Value.Year == yil && x.Durum == "A")
                .Select(x => x.MakbuzID)
                .ToList();

            decimal makbuzAidat = 0;
            decimal makbuzDemirbas = 0;
            if (makbuzIdleri.Any())
            {
                var satirlar = db.MakbuzSatirs.AsNoTracking()
                    .Where(x => makbuzIdleri.Contains(x.MakbuzID ?? 0) && x.Durum == "A")
                    .Select(x => new { x.EkMiAidatMi, x.Tutar })
                    .ToList();
                makbuzAidat = satirlar.Where(x => x.EkMiAidatMi == "A").Sum(x => x.Tutar) ?? 0;
                makbuzDemirbas = satirlar.Where(x => x.EkMiAidatMi == "E").Sum(x => x.Tutar) ?? 0;
            }

            toplananAidat = tahAidat + makbuzAidat;
            toplananDemirbas = tahDemirbas + makbuzDemirbas;
        }

        // Bir gider türü adının "demirbaş" grubuna girip girmediğini belirler.
        private static bool DemirbasMi(string turAdi)
        {
            if (string.IsNullOrWhiteSpace(turAdi)) return false;
            string ad = turAdi.Trim().ToLowerInvariant().Replace("ş", "s").Replace("i̇", "i");
            return ad.Contains("demirba");
        }

        public ActionResult AidatHesaplama(int? yil, bool demirbasDahil = false)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            Session["Aktif"] = "AidatHesaplama";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            // Seçilebilir yıllar: giderlerin geçtiği yıllar + içinde bulunduğumuz yıl
            var giderYillari = db.Giders.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih != null)
                .Select(x => x.GiderTarih.Value.Year)
                .Distinct()
                .ToList();
            if (!giderYillari.Contains(DateTime.Now.Year))
                giderYillari.Add(DateTime.Now.Year);
            giderYillari = giderYillari.OrderByDescending(x => x).ToList();

            int seciliYil = yil ?? DateTime.Now.Year;

            ViewBag.Yillar = giderYillari;
            ViewBag.Yil = seciliYil;
            ViewBag.DemirbasDahil = demirbasDahil;

            // Aktif daire sayısı (daire başına bölmek için)
            int daireSayisi = db.Dairelers.AsNoTracking().Count(x => x.BinaID == BinaID);
            ViewBag.DaireSayisi = daireSayisi;

            // Kıyasta adil olmak için: içinde bulunduğumuz yıl kısmi ise geçen ay sayısı,
            // tam geçmiş yıllar için 12 ay.
            int aySayisi = (seciliYil == DateTime.Now.Year) ? DateTime.Now.Month : 12;
            ViewBag.AySayisi = aySayisi;

            // --- Seçili yılın giderleri, türe göre gruplanmış ---
            var giderler = (from g in db.Giders.AsNoTracking()
                            join t in db.GiderTurus on g.GiderTuruID equals t.GiderTuruID
                            where g.BinaID == BinaID && g.Durum == "A" && g.GiderTarih.Value.Year == seciliYil
                            select new { t.GiderTuruID, t.GiderTuruAdi, g.GiderTutar }).ToList();

            var gruplar = giderler
                .GroupBy(x => new { x.GiderTuruID, x.GiderTuruAdi })
                .Select(x => new Models.AidatHesaplamaGrup
                {
                    Ad = x.Key.GiderTuruAdi,
                    Tutar = x.Sum(y => y.GiderTutar ?? 0),
                    // Demirbaş gideri aidata girmez: yerleşik tür (ID 6) veya adı "demirbaş" olan türler
                    Demirbas = x.Key.GiderTuruID == 6 || DemirbasMi(x.Key.GiderTuruAdi)
                })
                .OrderByDescending(x => x.Tutar)
                .ToList();

            ViewBag.Gruplar = gruplar;

            decimal demirbasGider = gruplar.Where(x => x.Demirbas).Sum(x => x.Tutar);
            decimal aidatGider = gruplar.Where(x => !x.Demirbas).Sum(x => x.Tutar);
            decimal toplamGider = demirbasGider + aidatGider;

            ViewBag.DemirbasGider = demirbasGider;
            ViewBag.AidatGider = aidatGider;
            ViewBag.ToplamGider = toplamGider;

            // --- Toplanan gelir (seçili yıl) ---
            decimal toplananAidat, toplananDemirbas;
            AidatGelirHesapla(BinaID, seciliYil, out toplananAidat, out toplananDemirbas);
            ViewBag.ToplananAidat = toplananAidat;
            ViewBag.ToplananDemirbas = toplananDemirbas;

            // --- Geçmiş yıl kıyası ---
            int oncekiYil = seciliYil - 1;
            var oncekiGiderler = (from g in db.Giders.AsNoTracking()
                                  join t in db.GiderTurus on g.GiderTuruID equals t.GiderTuruID
                                  where g.BinaID == BinaID && g.Durum == "A" && g.GiderTarih.Value.Year == oncekiYil
                                  select new { t.GiderTuruID, t.GiderTuruAdi, g.GiderTutar }).ToList();

            System.Func<int, string, bool> oncekiDemirbasMi = (id, ad) => id == 6 || DemirbasMi(ad);
            decimal oncekiDemirbasGider = oncekiGiderler.Where(x => oncekiDemirbasMi(x.GiderTuruID, x.GiderTuruAdi)).Sum(x => x.GiderTutar ?? 0);
            decimal oncekiAidatGider = oncekiGiderler.Where(x => !oncekiDemirbasMi(x.GiderTuruID, x.GiderTuruAdi)).Sum(x => x.GiderTutar ?? 0);
            decimal oncekiToplamGider = oncekiDemirbasGider + oncekiAidatGider;

            decimal oncekiToplananAidat, oncekiToplananDemirbas;
            AidatGelirHesapla(BinaID, oncekiYil, out oncekiToplananAidat, out oncekiToplananDemirbas);

            ViewBag.OncekiYil = oncekiYil;
            ViewBag.OncekiAidatGider = oncekiAidatGider;
            ViewBag.OncekiToplamGider = oncekiToplamGider;
            ViewBag.OncekiToplananAidat = oncekiToplananAidat;
            ViewBag.OncekiVarMi = oncekiGiderler.Any() || oncekiToplananAidat > 0;

            // --- Ay bazında kırılım (işletme gideri = demirbaş hariç) ---
            // Hangi tür ID'leri demirbaş sayılır (ID 6 veya adı "demirbaş")
            var turler = db.GiderTurus.AsNoTracking()
                .Select(x => new { x.GiderTuruID, x.GiderTuruAdi }).ToList();
            var demirbasIdSet = new System.Collections.Generic.HashSet<int>(
                turler.Where(t => t.GiderTuruID == 6 || DemirbasMi(t.GiderTuruAdi)).Select(t => t.GiderTuruID));

            var ayliklar = db.Giders.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTarih != null
                    && (x.GiderTarih.Value.Year == seciliYil || x.GiderTarih.Value.Year == oncekiYil))
                .Select(x => new { Yil = x.GiderTarih.Value.Year, Ay = x.GiderTarih.Value.Month, x.GiderTuruID, x.GiderTutar })
                .ToList();

            decimal[] aylikThis = new decimal[13]; // 1..12 kullanılır
            decimal[] aylikPrev = new decimal[13];
            foreach (var x in ayliklar)
            {
                if (x.Ay < 1 || x.Ay > 12) continue;
                if (demirbasIdSet.Contains(x.GiderTuruID ?? 0)) continue; // demirbaş hariç
                decimal tut = x.GiderTutar ?? 0;
                if (x.Yil == seciliYil) aylikThis[x.Ay] += tut;
                else aylikPrev[x.Ay] += tut;
            }

            // Aktif ay sayısı = o yıl içinde herhangi bir gideri olan aylar
            int ayThis = ayliklar.Where(x => x.Yil == seciliYil).Select(x => x.Ay).Distinct().Count();
            int ayPrev = ayliklar.Where(x => x.Yil == oncekiYil).Select(x => x.Ay).Distinct().Count();

            // Aylık ortalama işletme gideri (adil kıyas tabanı)
            decimal avgThis = ayThis > 0 ? aidatGider / ayThis : 0;
            decimal avgPrev = ayPrev > 0 ? oncekiAidatGider / ayPrev : 0;

            ViewBag.AylikThis = aylikThis;
            ViewBag.AylikPrev = aylikPrev;
            ViewBag.AyThis = ayThis;
            ViewBag.AyPrev = ayPrev;
            ViewBag.OrtakAy = Math.Min(ayThis, ayPrev);
            ViewBag.AylarEsit = (ayThis == ayPrev);
            ViewBag.AvgThis = avgThis;
            ViewBag.AvgPrev = avgPrev;

            // Otomatik artış oranı: aylık ortalama üzerinden (eksik ay kıyası bozmaz)
            decimal otoArtis = 0;
            if (avgPrev > 0)
                otoArtis = Math.Round(((avgThis - avgPrev) / avgPrev) * 100, 1);
            ViewBag.OtoArtis = otoArtis;

            // --- Mevcut aidat (zam bunun üzerine yapılır) ---
            // Toplanan gelir herkes düzenli ödemediği için güvenilmez; belirlenen aidatı
            // referans alırız: en son açılan dönemin Aidat kayıtlarında EN SIK tekrar
            // eden tutar (mod). DonemEkle'deki öneri mantığının aynısı (ödenen+ödenmeyen).
            // Öncelik: Aidat Tanımlama (AidatTanim) tablosunda içinde bulunulan aya kadar
            // tanımlanmış EN SON ayın tutarı. Tanım yoksa eski mod mantığına düşülür.
            decimal mevcutAidat = 0;
            string mevcutAidatDonem = "";
            string mevcutAidatKaynak = "";
            // Seçili yıl içinde aranır: bu yıl ise içinde bulunulan aya kadar, diğer yıllarda yılın son tanımlı ayı.
            int sonAy = (seciliYil == DateTime.Now.Year) ? DateTime.Now.Month : 12;
            var sonTanim = db.AidatTanims.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.AidatYil == seciliYil && x.AidatAy <= sonAy)
                .OrderByDescending(x => x.AidatAy)
                .FirstOrDefault();
            var sonDonem = sonTanim != null ? null : db.Kasas.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.KasaYil == seciliYil)
                .OrderByDescending(x => x.AyKodu)
                .FirstOrDefault();
            if (sonTanim != null)
            {
                mevcutAidat = sonTanim.Tutar;
                mevcutAidatDonem = new DateTime(sonTanim.AidatYil, sonTanim.AidatAy, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")) + " " + sonTanim.AidatYil;
                mevcutAidatKaynak = "tanim";
            }
            else if (sonDonem != null)
            {
                var sonAyAidatlari = db.Aidats.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && (x.Durum == "A" || x.Durum == "P")
                                && x.AidatAy == sonDonem.KasaAy && x.AidatYil == sonDonem.KasaYil
                                && x.AidatTutar != null)
                    .Select(x => x.AidatTutar)
                    .ToList();

                if (sonAyAidatlari.Count > 0)
                {
                    mevcutAidat = sonAyAidatlari
                        .GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .ThenByDescending(g => g.Key)
                        .First().Key.Value;
                    mevcutAidatDonem = sonDonem.KasaAy + " " + sonDonem.KasaYil;
                    mevcutAidatKaynak = "mod";
                }
            }
            ViewBag.MevcutAidat = mevcutAidat;
            ViewBag.MevcutAidatDonem = mevcutAidatDonem;
            ViewBag.MevcutAidatKaynak = mevcutAidatKaynak;

            return View();
        }

        // ============================================================
        //  AİDAT TANIMLAMA — AidatTanim tablosu (bina + yıl + ay → tutar)
        //  Yıl seçilir, 12 ayın aidatı girilip kaydedilir/güncellenir.
        //  DonemEkle önerisi/ara ay borçlandırması ve Aidat Hesaplama'daki
        //  "mevcut aidat" buradan beslenir. Açılmış dönemlerin Aidat
        //  kayıtlarını DEĞİŞTİRMEZ; yalnızca tanımdır.
        // ============================================================

        public ActionResult AidatTanimlama(int? yil)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            Session["Aktif"] = "AidatTanimlama";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int buYil = DateTime.Now.Year;
            int seciliYil = (yil.HasValue && yil.Value >= 2000 && yil.Value <= 2100) ? yil.Value : buYil;

            // Seçilebilir yıllar: tanımı olan yıllar + dönem açılmış yıllar + bu yıl ve gelecek yıl
            var yillar = db.AidatTanims.AsNoTracking().Where(x => x.BinaID == BinaID).Select(x => x.AidatYil).Distinct().ToList();
            yillar.AddRange(db.Kasas.AsNoTracking().Where(x => x.BinaID == BinaID && x.KasaYil != null).Select(x => x.KasaYil.Value).Distinct().ToList());
            yillar.Add(buYil);
            yillar.Add(buYil + 1);
            yillar.Add(seciliYil);
            ViewBag.Yillar = yillar.Distinct().OrderByDescending(x => x).ToList();
            ViewBag.Yil = seciliYil;

            // Seçili yılın tanımları (index 1..12)
            var tanimlar = new decimal?[13];
            foreach (var t in db.AidatTanims.AsNoTracking().Where(x => x.BinaID == BinaID && x.AidatYil == seciliYil).ToList())
            {
                if (t.AidatAy >= 1 && t.AidatAy <= 12) tanimlar[t.AidatAy] = t.Tutar;
            }
            ViewBag.Tanimlar = tanimlar;

            // Bilgi amaçlı: o ay fiilen borçlandırılan en sık tutar (silinmiş ve "Mayıs - 2" partileri hariç)
            var uygulanan = new decimal?[13];
            var yilAidatlari = db.Aidats.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.AidatYil == seciliYil && (x.Durum == "A" || x.Durum == "P") && x.AidatTutar > 0)
                .Select(x => new { x.AidatAy, x.AidatTutar })
                .ToList();
            foreach (var g in yilAidatlari.GroupBy(x => AyKoduBul((x.AidatAy ?? "").Trim())).Where(g => g.Key > 0))
            {
                uygulanan[g.Key] = g.GroupBy(x => x.AidatTutar)
                    .OrderByDescending(x => x.Count()).ThenByDescending(x => x.Key)
                    .First().Key;
            }
            ViewBag.Uygulanan = uygulanan;

            // Dönemi açılmış aylar
            var acikAylar = db.Kasas.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.KasaYil == seciliYil && x.AyKodu != null)
                .Select(x => x.AyKodu.Value)
                .ToList();
            ViewBag.AcikAylar = acikAylar;

            // "Tüm aylara uygula" kutusu için öneri: en son tanımlanan tutar
            var sonTanim = db.AidatTanims.AsNoTracking()
                .Where(x => x.BinaID == BinaID)
                .OrderByDescending(x => x.AidatYil).ThenByDescending(x => x.AidatAy)
                .Select(x => (decimal?)x.Tutar)
                .FirstOrDefault();
            ViewBag.SonTanim = sonTanim.HasValue ? TutarYaz(sonTanim.Value) : "";

            return View();
        }

        [HttpPost]
        public ActionResult AidatTanimKaydet(int Yil, string[] Tutarlar)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

            if (Yil < 2000 || Yil > 2100 || Tutarlar == null || Tutarlar.Length != 12)
            {
                TempData["Hata"] = "Geçersiz istek.";
                return RedirectToAction("AidatTanimlama", new { yil = Yil });
            }

            // Geçmiş yıllar yalnızca görüntülenir, değiştirilemez.
            if (Yil < DateTime.Now.Year)
            {
                TempData["Hata"] = "Geçmiş yılların aidat tanımları değiştirilemez.";
                return RedirectToAction("AidatTanimlama", new { yil = Yil });
            }

            try
            {
                var mevcutlar = db.AidatTanims.Where(x => x.BinaID == BinaID && x.AidatYil == Yil).ToList();
                int eklenen = 0, guncellenen = 0, silinen = 0;

                // Dönemi açılmış aylar kilitli: gönderilen değer yok sayılır, tanıma dokunulmaz.
                var acikAylar = new HashSet<int>(db.Kasas.AsNoTracking()
                    .Where(x => x.BinaID == BinaID && x.KasaYil == Yil && x.AyKodu != null)
                    .Select(x => x.AyKodu.Value)
                    .ToList());

                for (int ay = 1; ay <= 12; ay++)
                {
                    if (acikAylar.Contains(ay)) continue;

                    decimal tutar = TutarParse(Tutarlar[ay - 1]);
                    var kayit = mevcutlar.FirstOrDefault(x => x.AidatAy == ay);

                    if (tutar <= 0)
                    {
                        // Boş bırakılan ayın tanımı kaldırılır
                        if (kayit != null) { db.AidatTanims.Remove(kayit); silinen++; }
                    }
                    else if (kayit == null)
                    {
                        db.AidatTanims.Add(new AidatTanim { BinaID = BinaID, AidatYil = Yil, AidatAy = ay, Tutar = tutar });
                        eklenen++;
                    }
                    else if (kayit.Tutar != tutar)
                    {
                        kayit.Tutar = tutar;
                        guncellenen++;
                    }
                }

                if (eklenen + guncellenen + silinen == 0)
                {
                    TempData["Basarili"] = "Değişiklik yok; " + Yil + " aidat tanımları zaten güncel.";
                    return RedirectToAction("AidatTanimlama", new { yil = Yil });
                }

                db.Hareketlers.Add(new Hareketler
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = Yil + " yılı aidat tanımları güncellendi (" + eklenen + " eklendi, " + guncellenen + " güncellendi, " + silinen + " kaldırıldı).",
                    Tarih = DateTime.Now,
                    Tur = "Güncelleme",
                });

                db.SaveChanges();
                TempData["Basarili"] = Yil + " yılı aidat tanımları kaydedildi. (" + eklenen + " eklendi, " + guncellenen + " güncellendi, " + silinen + " kaldırıldı)";
            }
            catch (Exception ex)
            {
                TempData["Hata"] = "Bir hata oluştu! Detay: " + ex.Message;
            }

            return RedirectToAction("AidatTanimlama", new { yil = Yil });
        }
    }
}