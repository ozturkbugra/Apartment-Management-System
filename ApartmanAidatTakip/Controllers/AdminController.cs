using ApartmanAidatTakip.Helpers;
using ApartmanAidatTakip.Models;
using Microsoft.Ajax.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class AdminController : Controller
    {
        AptVTEntities db = new AptVTEntities();
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(Kullanicilar kullanicilar, string Parola)
        {
            string ipKey = Request.UserHostAddress ?? "unknown";
            int kalanKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
            if (kalanKilit > 0)
            {
                ViewBag.Uyari = LoginRateLimiter.KilitMesaji(kalanKilit);
                return View();
            }

            var s = Crypto.Hash(Parola, "MD5");
            var l = db.Kullanicilars.FirstOrDefault(x => x.KullaniciAdi == kullanicilar.KullaniciAdi && x.Yetki == "1" && x.Durum=="A");

            if (l != null && l.Parola == s)
            {
                LoginRateLimiter.Sifirla(ipKey);

                // İki adımlı doğrulama aktifse önce kod istenir.
                if (Ad_TwoFactorEnabled(l.KullaniciID))
                {
                    Session["Pending2FA_AdminID"] = l.KullaniciID;
                    Session["Pending2FA_AdminAd"] = l.KullaniciAdi;
                    return RedirectToAction("LoginDogrula", "Admin");
                }

                Session["AdminID"] = l.KullaniciID;
                Session["KullaniciAdi"] = l.KullaniciAdi;
                return RedirectToAction("Index", "Admin");
            }

            LoginRateLimiter.HataKaydet(ipKey);
            int yeniKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
            ViewBag.Uyari = yeniKilit > 0 ? LoginRateLimiter.KilitMesaji(yeniKilit) : "Kullanıcı adı veya şifre yanlış";
            return View();
        }

        // ================== ADMIN 2FA ORTAK YARDIMCILAR ==================
        private bool Ad_TwoFactorEnabled(int kullaniciID)
        {
            return db.Database.SqlQuery<bool>(
                "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();
        }

        private string Ad_TwoFactorSecret(int kullaniciID)
        {
            return db.Database.SqlQuery<string>(
                "SELECT TwoFactorSecret FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();
        }

        // Girilen kod bir yedek koda uyuyorsa tüketir (tek kullanımlık) ve true döner.
        private bool Ad_YedekKoduTuket(int kullaniciID, string kod)
        {
            string kodlar = db.Database.SqlQuery<string>(
                "SELECT TwoFactorRecoveryCodes FROM Kullanicilar WHERE KullaniciID = @p0",
                kullaniciID).FirstOrDefault();
            if (string.IsNullOrEmpty(kodlar)) return false;

            var liste = kodlar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string hash = TwoFactorHelper.HashRecoveryCode(kod);
            if (!liste.Contains(hash)) return false;

            liste.Remove(hash);
            string yeni = string.Join(";", liste);
            object yeniParam = string.IsNullOrEmpty(yeni) ? (object)DBNull.Value : yeni;
            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                yeniParam, kullaniciID);
            return true;
        }

        // ================== ADMIN GİRİŞ 2FA DOĞRULAMA ==================
        public ActionResult LoginDogrula()
        {
            if (Session["Pending2FA_AdminID"] == null)
                return RedirectToAction("Login", "Admin");
            return View();
        }

        [HttpPost]
        public ActionResult LoginDogrula(string kod)
        {
            if (Session["Pending2FA_AdminID"] == null)
                return RedirectToAction("Login", "Admin");

            string ipKey = Request.UserHostAddress ?? "unknown";
            int kalanKilit = LoginRateLimiter.KalanKilitSaniye(ipKey);
            if (kalanKilit > 0)
            {
                ViewBag.Uyari = LoginRateLimiter.KilitMesaji(kalanKilit);
                return View();
            }

            int kullaniciID = Convert.ToInt32(Session["Pending2FA_AdminID"]);
            string secret = Ad_TwoFactorSecret(kullaniciID);

            bool dogru = TwoFactorHelper.ValidateCode(secret, kod);
            if (!dogru) dogru = Ad_YedekKoduTuket(kullaniciID, kod);

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
            Session["AdminID"] = kullaniciID;
            Session["KullaniciAdi"] = Session["Pending2FA_AdminAd"];
            Session.Remove("Pending2FA_AdminID");
            Session.Remove("Pending2FA_AdminAd");
            return RedirectToAction("Index", "Admin");
        }

        // ================== ADMIN GÜVENLİK (2FA KURULUM) ==================
        public ActionResult Guvenlik()
        {
            if (Session["AdminID"] == null)
                return RedirectToAction("Login", "Admin");

            int kullaniciID = Convert.ToInt32(Session["AdminID"]);
            bool aktif = Ad_TwoFactorEnabled(kullaniciID);
            ViewBag.IkiAdimAktif = aktif;

            if (aktif)
            {
                string kodlar = db.Database.SqlQuery<string>(
                    "SELECT TwoFactorRecoveryCodes FROM Kullanicilar WHERE KullaniciID = @p0",
                    kullaniciID).FirstOrDefault();
                ViewBag.IkiAdimYedekKalan = string.IsNullOrEmpty(kodlar)
                    ? 0 : kodlar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }
            else
            {
                string secret = Ad_TwoFactorSecret(kullaniciID);
                if (string.IsNullOrEmpty(secret))
                {
                    secret = TwoFactorHelper.GenerateSecret();
                    db.Database.ExecuteSqlCommand(
                        "UPDATE Kullanicilar SET TwoFactorSecret = @p0 WHERE KullaniciID = @p1",
                        secret, kullaniciID);
                }
                string ad = Session["KullaniciAdi"]?.ToString() ?? "admin";
                ViewBag.IkiAdimSecret = secret;
                ViewBag.IkiAdimOtpUri = TwoFactorHelper.GetOtpAuthUri(secret, ad, "Apartman Aidat - Admin");
            }
            return View();
        }

        [HttpPost]
        public JsonResult IkiAdimAktiflestir(string kod)
        {
            if (Session["AdminID"] == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });
            int kullaniciID = Convert.ToInt32(Session["AdminID"]);
            string secret = Ad_TwoFactorSecret(kullaniciID);

            if (string.IsNullOrEmpty(secret))
                return Json(new { success = false, message = "Gizli anahtar bulunamadı, sayfayı yenileyin." });
            if (Ad_TwoFactorEnabled(kullaniciID))
                return Json(new { success = true, message = "İki adımlı doğrulama zaten aktif." });
            if (!TwoFactorHelper.ValidateCode(secret, kod))
                return Json(new { success = false, message = "Girdiğiniz kod hatalı veya süresi dolmuş." });

            var yedekKodlar = TwoFactorHelper.GenerateRecoveryCodes(8);
            string hashler = string.Join(";", yedekKodlar.Select(TwoFactorHelper.HashRecoveryCode));
            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorEnabled = 1, TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                hashler, kullaniciID);

            return Json(new { success = true, message = "İki adımlı doğrulama aktifleştirildi.", recoveryCodes = yedekKodlar });
        }

        [HttpPost]
        public JsonResult IkiAdimYedekYenile()
        {
            if (Session["AdminID"] == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });
            int kullaniciID = Convert.ToInt32(Session["AdminID"]);
            if (!Ad_TwoFactorEnabled(kullaniciID))
                return Json(new { success = false, message = "İki adımlı doğrulama aktif değil." });

            var yedekKodlar = TwoFactorHelper.GenerateRecoveryCodes(8);
            string hashler = string.Join(";", yedekKodlar.Select(TwoFactorHelper.HashRecoveryCode));
            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorRecoveryCodes = @p0 WHERE KullaniciID = @p1",
                hashler, kullaniciID);
            return Json(new { success = true, recoveryCodes = yedekKodlar });
        }

        [HttpPost]
        public JsonResult IkiAdimKapat()
        {
            if (Session["AdminID"] == null) return Json(new { success = false, message = "Oturum süresi dolmuş." });
            int kullaniciID = Convert.ToInt32(Session["AdminID"]);
            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorEnabled = 0, TwoFactorSecret = NULL, TwoFactorRecoveryCodes = NULL WHERE KullaniciID = @p0",
                kullaniciID);
            return Json(new { success = true, message = "İki adımlı doğrulama kapatıldı." });
        }

        // ================== ADMIN ŞİFREMİ UNUTTUM ==================
        public ActionResult SifremiUnuttum()
        {
            return View();
        }

        [HttpPost]
        public ActionResult SifremiUnuttum(string KullaniciAdi)
        {
            var l = db.Kullanicilars.FirstOrDefault(x => x.KullaniciAdi == KullaniciAdi && x.Yetki == "1" && x.Durum == "A");
            if (l != null)
            {
                if (Ad_TwoFactorEnabled(l.KullaniciID))
                {
                    Session["AdminReset_KullaniciID"] = l.KullaniciID;
                    return RedirectToAction("SifreYenile", "Admin");
                }
                ViewBag.Uyari = "Bu hesapta iki adımlı doğrulama aktif olmadığı için şifre sıfırlanamıyor.";
            }
            else
            {
                ViewBag.Uyari = "Yönetici kullanıcı adı hatalı.";
            }
            return View();
        }

        public ActionResult SifreYenile()
        {
            if (Session["AdminReset_KullaniciID"] == null)
                return RedirectToAction("SifremiUnuttum", "Admin");
            return View();
        }

        [HttpPost]
        public ActionResult SifreYenile(string kod, string Parola, string Parola2)
        {
            if (Session["AdminReset_KullaniciID"] == null)
                return RedirectToAction("SifremiUnuttum", "Admin");

            int kullaniciID = Convert.ToInt32(Session["AdminReset_KullaniciID"]);

            if (string.IsNullOrWhiteSpace(Parola) || Parola != Parola2)
            {
                ViewBag.Uyari = "Şifreler boş olamaz ve birbiriyle uyuşmalıdır.";
                return View();
            }

            string secret = Ad_TwoFactorSecret(kullaniciID);
            bool dogru = TwoFactorHelper.ValidateCode(secret, kod);
            if (!dogru) dogru = Ad_YedekKoduTuket(kullaniciID, kod);

            if (!dogru)
            {
                ViewBag.Uyari = "Doğrulama kodu hatalı veya süresi dolmuş. Lütfen tekrar deneyin.";
                return View();
            }

            var k = db.Kullanicilars.FirstOrDefault(x => x.KullaniciID == kullaniciID);
            if (k == null)
            {
                Session.Remove("AdminReset_KullaniciID");
                return RedirectToAction("Login", "Admin");
            }

            k.Parola = Crypto.Hash(Parola, "MD5");
            db.SaveChanges();

            db.Hareketlers.Add(new Hareketler()
            {
                BinaID = k.BinaID ?? 0,
                KullaniciID = kullaniciID,
                OlayAciklama = "Yönetici iki adımlı doğrulama ile şifresini sıfırladı",
                Tarih = DateTime.Now,
                Tur = "Güncelleme",
            });
            db.SaveChanges();

            Session.Remove("AdminReset_KullaniciID");
            TempData["Basarili"] = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction("Login", "Admin");
        }
        public ActionResult Index()
        {
            DateTime today = DateTime.Today;
            DateTime sevenDaysFromNow = today.AddDays(7);

            var Son7gun = db.Binalars.Where(e => e.SozlesmeBitisTarihi <= sevenDaysFromNow && e.SozlesmeBitisTarihi >= today && e.Durum == "A").ToList();
            var Biten = db.Binalars.Where(e => e.SozlesmeBitisTarihi < today && e.Durum == "A").ToList();

            ViewBag.Son7gun = Son7gun;
            ViewBag.Biten = Biten;
            return View();
        }

        public ActionResult Binalar()
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x=> x.Durum== "A" && x.SozlesmeBitisTarihi >= Tarih).ToList();
            return View();
        }

        public ActionResult BinaEkle(Binalar binalar)
        {
            var binavarmi = db.Binalars.Where(x => x.BinaKullaniciAdi == binalar.BinaKullaniciAdi).FirstOrDefault();
            if(binavarmi == null && binalar.BinaKullaniciAdi != null)
            {
                binalar.SozlesmeBaslamaTarihi = DateTime.Now.Date;
                binalar.Durum = "A";
                db.Binalars.Add(binalar);
                db.SaveChanges();
                TempData["Basarili"] = "Bina Ekleme İşlemi Başarılı.";
            }
            else
            {
                TempData["Hata"] = "Bu Bina Adında Bir Bina Zaten Kayıtlı!";
            }

            return RedirectToAction("Binalar","Admin");
        }

        public ActionResult BinaDuzenle(int id)
        {
            ViewBag.b = db.Binalars.Where(x => x.BinaID == id).FirstOrDefault();
            return View();
        }

        [HttpPost]
        public ActionResult BinaDuzenle(int BinaID, Binalar binalar)
        {
            var eskibina = db.Binalars.Where(x => x.BinaID == BinaID).FirstOrDefault();
            var binavarmi = db.Binalars.Where(x => x.BinaKullaniciAdi == binalar.BinaKullaniciAdi && x.BinaID != binalar.BinaID).FirstOrDefault();

            if(binavarmi == null && binalar.BinaKullaniciAdi != null)
            {
                eskibina.BinaAdi = binalar.BinaAdi;
                eskibina.BinaKullaniciAdi = binalar.BinaKullaniciAdi;
                eskibina.Adres = binalar.Adres;
                eskibina.VergiNo = binalar.VergiNo;
                eskibina.DaireSayisi = binalar.DaireSayisi;
                eskibina.SozlesmeBitisTarihi = binalar.SozlesmeBitisTarihi;
                db.SaveChanges();
                TempData["Basarili"] = "Bina Güncelleme İşlemi Başarılı.";
            }
            else
            {
                TempData["Hata"] = "Bu Bina Adında Bir Bina Zaten Kayıtlı!";
            }


            return RedirectToAction("Binalar","Admin");
        }

        public ActionResult BinaSil(int id)
        {
            var bina = db.Binalars.Where(x => x.BinaID == id).FirstOrDefault();
            bina.Durum = "P";
            db.SaveChanges();
            TempData["Basarili"] = "Bina Silme İşlemi Başarılı.";
            return RedirectToAction("Binalar", "Admin");
        }

        public ActionResult SilinenBinalar()
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.Durum == "P").ToList();
            return View();
        }

        public ActionResult BinaGeriAl(int id)
        {
            var bina = db.Binalars.Where(x => x.BinaID == id).FirstOrDefault();
            bina.Durum = "A";
            db.SaveChanges();
            TempData["Basarili"] = "Bina Geri Alma İşlemi Başarılı.";
            return RedirectToAction("SilinenBinalar", "Admin");
        }
        public ActionResult BinaTamamenSil(int id)
        {
            var bina = db.Binalars.Where(x => x.BinaID == id).FirstOrDefault();
            db.Binalars.Remove(bina);
            db.SaveChanges();
            TempData["Basarili"] = "Bina Silme İşlemi Başarılı.";
            return RedirectToAction("SilinenBinalar", "Admin");
        }

        public ActionResult Kullanicilar(int? BinaID)
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.Durum == "A" && x.SozlesmeBitisTarihi >= Tarih).ToList();
            if (BinaID == null)
            {
                ViewBag.Kullanicilar = db.KullanicilarViews.Where(x=> x.KullaniciDurumu == "A" && x.BinaDurumu == "A" && x.SozlesmeBitisTarihi >= Tarih).ToList();
            }
            else
            {
                ViewBag.Kullanicilar = db.KullanicilarViews.Where(x => x.BinaID == BinaID && x.KullaniciDurumu == "A").ToList();
            }
            
            return View();
        }

        public ActionResult KullaniciEkle(Kullanicilar kullanicilar)
        {
            var kullanicivarmi = db.Kullanicilars.Where(x => x.KullaniciAdi == kullanicilar.KullaniciAdi && x.BinaID == kullanicilar.BinaID).FirstOrDefault();
            if(kullanicivarmi == null)
            {
                string Sifre = "123456";
                kullanicilar.Durum = "A";
                if(kullanicilar.Yetki == "1")
                {
                    kullanicilar.BinaID = null;
                }
                kullanicilar.Parola = Crypto.Hash(Sifre, "MD5");
                db.Kullanicilars.Add(kullanicilar);
                db.SaveChanges();
                TempData["Basarili"] = "Kullanıcı Ekleme İşlemi Başarılı.";

            }
            else
            {
                TempData["Hata"] = "Bu Kullanıcı Zaten Kayıtlı!";

            }


            return RedirectToAction("Kullanicilar","Admin", new { BinaID = kullanicilar.BinaID });
        }

        public ActionResult KullaniciSil(int id)
        {
            var kullanici = db.Kullanicilars.Where(x => x.KullaniciID == id).FirstOrDefault();
            kullanici.Durum = "P";
            db.SaveChanges();
            TempData["Basarili"] = "Kullanıcı Silme İşlemi Başarılı.";
            return RedirectToAction("Kullanicilar", "Admin");
        }

        public ActionResult KullaniciDuzenle(int id)
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.Durum == "A" && x.SozlesmeBitisTarihi >= Tarih).ToList();
            ViewBag.b = db.Kullanicilars.Where(x => x.KullaniciID == id).FirstOrDefault();
            ViewBag.IkiAdimAktif = db.Database.SqlQuery<bool>(
                "SELECT ISNULL(TwoFactorEnabled, 0) FROM Kullanicilar WHERE KullaniciID = @p0", id).FirstOrDefault();
            return View();
        }

        // Admin, bir kullanıcının şifresini yeniler (kullanıcı giriş yapamadığında).
        [HttpPost]
        public ActionResult SifreSifirla(int KullaniciID, string YeniParola, string YeniParola2)
        {
            var kullanici = db.Kullanicilars.FirstOrDefault(x => x.KullaniciID == KullaniciID);
            if (kullanici == null)
            {
                TempData["Hata"] = "Kullanıcı bulunamadı.";
                return RedirectToAction("Kullanicilar", "Admin");
            }

            if (string.IsNullOrWhiteSpace(YeniParola) || YeniParola != YeniParola2)
            {
                TempData["Hata"] = "Şifreler boş olamaz ve birbiriyle uyuşmalıdır.";
                return RedirectToAction("KullaniciDuzenle", "Admin", new { id = KullaniciID });
            }

            kullanici.Parola = Crypto.Hash(YeniParola, "MD5");
            db.SaveChanges();

            db.Hareketlers.Add(new Hareketler()
            {
                BinaID = kullanici.BinaID ?? 0,
                KullaniciID = KullaniciID,
                OlayAciklama = "Yönetici tarafından kullanıcı şifresi sıfırlandı",
                Tarih = DateTime.Now,
                Tur = "Güncelleme",
            });
            db.SaveChanges();

            TempData["Basarili"] = "Kullanıcının şifresi başarıyla güncellendi.";
            return RedirectToAction("KullaniciDuzenle", "Admin", new { id = KullaniciID });
        }

        // Kullanıcı Authenticator'a ve yedek kodlarına erişimini tamamen kaybederse,
        // yöneticinin (admin) 2FA'yı sıfırlaması için yedek kapı.
        public ActionResult IkiAdimSifirla(int id)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE Kullanicilar SET TwoFactorEnabled = 0, TwoFactorSecret = NULL, TwoFactorRecoveryCodes = NULL WHERE KullaniciID = @p0",
                id);
            TempData["Basarili"] = "Kullanıcının iki adımlı doğrulaması sıfırlandı. Kullanıcı tekrar kurulum yapabilir.";
            return RedirectToAction("KullaniciDuzenle", "Admin", new { id = id });
        }

        [HttpPost]
        public ActionResult KullaniciDuzenle(int KullaniciID, Kullanicilar kullanicilar)
        {
            var eskikullanici = db.Kullanicilars.Where(x => x.KullaniciID == KullaniciID).FirstOrDefault();
            var kullanicivarmi = db.Kullanicilars.Where(x => x.KullaniciAdi == kullanicilar.KullaniciAdi && x.KullaniciID != kullanicilar.KullaniciID).FirstOrDefault();

            if (kullanicivarmi == null)
            {
                eskikullanici.KullaniciAdi = kullanicilar.KullaniciAdi;
                eskikullanici.BinaID = kullanicilar.BinaID;
                eskikullanici.AdSoyad = kullanicilar.AdSoyad;
                eskikullanici.Telefon = kullanicilar.Telefon;
                
                db.SaveChanges();
                TempData["Basarili"] = "Kullanıcı Güncelleme İşlemi Başarılı.";
            }
            else
            {
                TempData["Hata"] = "Bu Kullanıcı Zaten Kayıtlı!";
            }


            return RedirectToAction("Kullanicilar", "Admin",new { BinaID=kullanicilar.BinaID });
        }


        public ActionResult SilinenKullanicilar()
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Kullanicilar = db.KullanicilarViews.Where(x => x.KullaniciDurumu == "P").ToList();
            return View();
        }

        public ActionResult KullaniciGeriAl(int id)
        {
            var kullanici = db.Kullanicilars.Where(x => x.KullaniciID == id).FirstOrDefault();
            kullanici.Durum = "A";
            db.SaveChanges();
            TempData["Basarili"] = "Kullanıcı Geri Alma İşlemi Başarılı.";
            return RedirectToAction("SilinenKullanicilar", "Admin");
        }
        public ActionResult KullaniciTamamenSil(int id)
        {
            var kullanici = db.Kullanicilars.Where(x => x.KullaniciID == id).FirstOrDefault();
            db.Kullanicilars.Remove(kullanici);
            db.SaveChanges();
            TempData["Basarili"] = "Kullanici Silme İşlemi Başarılı.";
            return RedirectToAction("SilinenKullanicilar", "Admin");
        }
        public ActionResult Logout()
        {
            Session["AdminID"] = null;
            Session["KullaniciAdi"] = null;
            Session.Abandon();
            return RedirectToAction("Login", "Admin");

        }
        public ActionResult Password()
        {
            
            return View();

        }
        
        [HttpPost]
        public ActionResult Password(string eskiparola, string Parola, string Parola2)
        {
            int KullaniciID = Convert.ToInt32(Session["AdminID"]);
            var a = db.Kullanicilars.Where(x => x.KullaniciID == KullaniciID).FirstOrDefault();
            var eskisifre = Crypto.Hash(eskiparola, "MD5");
            if (a.Parola == eskisifre)
            {
                if(Parola == Parola2)
                {

                    a.Parola = Crypto.Hash(Parola, "MD5");
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
        public ActionResult Hareketler(int? BinaID, DateTime? tarih1, DateTime? tarih2)
        {
            DateTime Tarih = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.Durum == "A" && x.SozlesmeBitisTarihi >= Tarih).OrderBy(x=> x.BinaAdi).ToList();
            
            if (BinaID == null && tarih1 == null && tarih2==null)
            {
                ViewBag.tarihdeger1 = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.Hareketler = db.HareketViews.OrderByDescending(x => x.HareketID).ToList();
            }
            else
            {
                ViewBag.tarihdeger1 = tarih1.Value.ToString("yyyy-MM-dd"); 
                ViewBag.tarihdeger2 = tarih2.Value.ToString("yyyy-MM-dd"); 
                // tarih1'in saatini 00:00:00 olarak ayarla
                if (tarih1.HasValue)
                {
                    tarih1 = tarih1.Value.Date; // Saat kısmını 00:00:00 yapar
                }

                // tarih2'nin saatini 23:59:59 olarak ayarla
                if (tarih2.HasValue)
                {
                    tarih2 = tarih2.Value.Date.AddDays(1).AddTicks(-1); // Saat kısmını 23:59:59 yapar
                }
                if(BinaID == null)
                {
                    ViewBag.Hareketler = db.HareketViews
                                        .Where(x => x.Tarih >= tarih1 && x.Tarih <= tarih2)
                                        .OrderByDescending(x => x.HareketID)
                                        .ToList();
                }
                else
                {
                    ViewBag.Hareketler = db.HareketViews
                                        .Where(x => x.BinaID == BinaID && x.Tarih >= tarih1 && x.Tarih <= tarih2)
                                        .OrderByDescending(x => x.HareketID)
                                        .ToList();
                    ViewBag.BinaID2 = BinaID;
                }
                
                
            }
            return View();
        }


        public ActionResult Duyurular()
        {
            ViewBag.Duyurular = db.Duyurulars.OrderByDescending(x => x.ID).ToList();
            return View();

        }

        [HttpPost]
        public ActionResult DuyuruEkle(Duyurular duyurular)
        {
            duyurular.Tarih = DateTime.Now.Date;
            duyurular.Durum = "A";
            db.Duyurulars.Add(duyurular);
            db.SaveChanges();
            TempData["Basarili"] = "Duyuru başarıyla eklendi";
            ViewBag.Duyurular = db.Duyurulars.OrderByDescending(x => x.ID).ToList();
            return RedirectToAction("Duyurular","Admin");

        }

        public ActionResult DuyuruPasifeAl(int id)
        {
            var varmi = db.Duyurulars.Where(x => x.ID == id).FirstOrDefault();
            varmi.Durum = "P";
            db.SaveChanges();
            TempData["Basarili"] = "Duyuru başarıyla pasife alındı";
            return RedirectToAction("Duyurular", "Admin");
        }

        public ActionResult DuyuruAktifeAl(int id)
        {
            var varmi = db.Duyurulars.Where(x => x.ID == id).FirstOrDefault();
            varmi.Durum = "A";
            db.SaveChanges();
            TempData["Basarili"] = "Duyuru başarıyla aktife alındı";
            return RedirectToAction("Duyurular", "Admin");
        }

    }
}