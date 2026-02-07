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

            var s = Crypto.Hash(Parola, "MD5");
            var l = db.Kullanicilars.FirstOrDefault(x => x.KullaniciAdi == kullanicilar.KullaniciAdi && x.Yetki == "1" && x.Durum=="A");

            if (l != null && l.Parola == s)
            {
                Session["AdminID"] = l.KullaniciID;
                Session["KullaniciAdi"] = l.KullaniciAdi;
                return RedirectToAction("Index", "Admin");
            }
            ViewBag.Uyari = "Kullanıcı adı veya şifre yanlış";
            return View();
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
            return View();
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