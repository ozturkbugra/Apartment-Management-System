using ApartmanAidatTakip.Models;
using System;
using System.Collections.Generic;
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
            }
            else
            {
                // Cookie bulunamazsa giriş sayfasına yönlendirebilir veya boş bir obje geçebilirsiniz
                return RedirectToAction("Login", "Hesap");
            }

            Sabit();
            return View();
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