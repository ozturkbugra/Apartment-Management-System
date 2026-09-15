using ApartmanAidatTakip.Helpers;
using ApartmanAidatTakip.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class AyarlarController : Controller
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

            ViewBag.Duyurular = db.Duyurulars.Where(x => x.Durum == "A").OrderByDescending(x => x.ID).ToList();

        }
        public ActionResult Index()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];

            if (userCookie != null && userCookie.Values["BinaID"] != null)
            {
                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);

                // Eşleşen bina bulunamazsa null referans hatası almamak için önlem alıyoruz
                var binaAyar = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);
                ViewBag.Ayar = binaAyar;

                // --- İKİ ADIMLI DOĞRULAMA (Google Authenticator) DURUMU ---
                bool ikiAdimAktif = TwoFactorEnabled(KullaniciID);
                ViewBag.IkiAdimAktif = ikiAdimAktif;

                if (ikiAdimAktif)
                {
                    // Kalan (kullanılmamış) yedek kod sayısı
                    string kodlar = db.Database.SqlQuery<string>(
                        "SELECT TwoFactorRecoveryCodes FROM Kullanicilar WHERE KullaniciID = @p0",
                        KullaniciID).FirstOrDefault();
                    ViewBag.IkiAdimYedekKalan = string.IsNullOrEmpty(kodlar)
                        ? 0
                        : kodlar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length;
                }

                if (!ikiAdimAktif)
                {
                    // Henüz aktifleştirilmemiş: kalıcı bir bekleyen gizli anahtar hazırla
                    // (sayfa yenilendiğinde QR kodun değişmemesi için DB'de saklanır, doğrulanınca aktif edilir).
                    string secret = TwoFactorSecret(KullaniciID);
                    if (string.IsNullOrEmpty(secret))
                    {
                        secret = TwoFactorHelper.GenerateSecret();
                        db.Database.ExecuteSqlCommand(
                            "UPDATE Kullanicilar SET TwoFactorSecret = @p0 WHERE KullaniciID = @p1",
                            secret, KullaniciID);
                    }

                    string kullaniciAdi = HttpUtility.UrlDecode(userCookie.Values["KullaniciAdi"] ?? "");
                    string binaAdi = HttpUtility.UrlDecode(userCookie.Values["BinaAdi"] ?? "Apartman");
                    string hesapAdi = string.IsNullOrEmpty(kullaniciAdi) ? "kullanici" : kullaniciAdi;

                    ViewBag.IkiAdimSecret = secret;
                    ViewBag.IkiAdimOtpUri = TwoFactorHelper.GetOtpAuthUri(secret, hesapAdi, "Apartman Aidat - " + binaAdi);
                }
            }
            else
            {
                // Cookie bulunamazsa giriş sayfasına yönlendirebilir veya boş bir obje geçebilirsiniz
                return RedirectToAction("Login", "Hesap");
            }

            Sabit();
            return View();
        }

        // Kullanıcının iki adımlı doğrulamasının aktif olup olmadığını döner.
        private bool TwoFactorEnabled(int kullaniciID)
        {
            return db.Database.SqlQuery<bool>(
                "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();
        }

        // Kullanıcıya ait (bekleyen veya aktif) gizli anahtarı döner.
        private string TwoFactorSecret(int kullaniciID)
        {
            return db.Database.SqlQuery<string>(
                "SELECT TwoFactorSecret FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();
        }

        // Kullanıcının Authenticator uygulamasından girdiği kodu doğrulayıp 2FA'yı aktifleştirir.
        [HttpPost]
        public JsonResult IkiAdimAktiflestir(string kod)
        {
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                if (userCookie == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });

                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                string secret = TwoFactorSecret(KullaniciID);

                if (string.IsNullOrEmpty(secret))
                    return Json(new { success = false, message = "Gizli anahtar bulunamadı, lütfen sayfayı yenileyin." });

                if (TwoFactorEnabled(KullaniciID))
                    return Json(new { success = true, message = "İki adımlı doğrulama zaten aktif." });

                if (!TwoFactorHelper.ValidateCode(secret, kod))
                    return Json(new { success = false, message = "Girdiğiniz kod hatalı veya süresi dolmuş. Lütfen tekrar deneyin." });

                // Tek kullanımlık yedek kodları üret, hash'lerini sakla, düz metinleri bir kez göster.
                var yedekKodlar = TwoFactorHelper.GenerateRecoveryCodes(8);
                string hashler = string.Join(";", yedekKodlar.Select(TwoFactorHelper.HashRecoveryCode));

                db.Database.ExecuteSqlCommand(
                    "UPDATE Kullanicilar SET TwoFactorEnabled = 1, TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                    hashler, KullaniciID);

                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                db.Hareketlers.Add(new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = "İki adımlı doğrulama (Google Authenticator) aktifleştirildi",
                    Tarih = DateTime.Now,
                    Tur = "Güncelleme",
                });
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "İki adımlı doğrulama başarıyla aktifleştirildi.",
                    recoveryCodes = yedekKodlar
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata oluştu: " + ex.Message });
            }
        }

        // Mevcut yedek kodları geçersiz kılıp yeni bir set üretir (yalnızca 2FA aktifse).
        [HttpPost]
        public JsonResult IkiAdimYedekYenile()
        {
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                if (userCookie == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });

                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                if (!TwoFactorEnabled(KullaniciID))
                    return Json(new { success = false, message = "İki adımlı doğrulama aktif değil." });

                var yedekKodlar = TwoFactorHelper.GenerateRecoveryCodes(8);
                string hashler = string.Join(";", yedekKodlar.Select(TwoFactorHelper.HashRecoveryCode));

                db.Database.ExecuteSqlCommand(
                    "UPDATE Kullanicilar SET TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                    hashler, KullaniciID);

                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                db.Hareketlers.Add(new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = "İki adımlı doğrulama yedek kodları yenilendi",
                    Tarih = DateTime.Now,
                    Tur = "Güncelleme",
                });
                db.SaveChanges();

                return Json(new { success = true, recoveryCodes = yedekKodlar });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata oluştu: " + ex.Message });
            }
        }

        // İki adımlı doğrulamayı kapatır ve gizli anahtarı temizler.
        [HttpPost]
        public JsonResult IkiAdimKapat()
        {
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                if (userCookie == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });

                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

                db.Database.ExecuteSqlCommand(
                    "UPDATE Kullanicilar SET TwoFactorEnabled = 0, TwoFactorSecret = NULL, TwoFactorRecoveryCodes = NULL WHERE KullaniciID = @p0",
                    KullaniciID);

                db.Hareketlers.Add(new Hareketler()
                {
                    BinaID = BinaID,
                    KullaniciID = KullaniciID,
                    OlayAciklama = "İki adımlı doğrulama (Google Authenticator) devre dışı bırakıldı",
                    Tarih = DateTime.Now,
                    Tur = "Güncelleme",
                });
                db.SaveChanges();

                return Json(new { success = true, message = "İki adımlı doğrulama kapatıldı." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult AyarlariGuncelle(string alanAdi, bool durum)
        {
            try
            {
                HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
                if (userCookie == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });

                int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                var bina = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);

                if (bina == null) return Json(new { success = false, message = "Bina bulunamadı." });

                // Gelen alan adına göre ilgili bit alanını güncelliyoruz
                if (alanAdi == "MakbuzOnayKaldir")
                {
                    bina.MakbuzOnayKaldir = durum;
                }
                else if (alanAdi == "YoneticiAidatEkleme")
                {
                    bina.YoneticiAidatEkleme = durum;
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Ayarlar başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata oluştu: " + ex.Message });
            }
        }
    }
}