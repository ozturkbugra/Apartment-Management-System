using ApartmanAidatTakip.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class TopluMakbuzController : Controller
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
                Session["DonemSorgu"] = "0";
            }
            else
            {
                ViewBag.DonemSorgu = true;
                Session["DonemSorgu"] = "1";
            }
        }
        public ActionResult Index(int? daireno)
        {
            Sabit();
            DonemEklendiMi();

            Session["Aktif"] = "TopluMakbuz";

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            if (daireno != null)
            {
                ViewBag.DaireNo = daireno;
                ViewBag.EkBorclar = db.Eks.Where(x => x.DaireNo == daireno && x.BinaID == BinaID && x.Durum == "A").ToList();
                ViewBag.AidatBorclar = db.Aidats.Where(x => x.DaireNo == daireno && x.BinaID == BinaID && x.Durum == "A").ToList();
                ViewBag.Borc = db.Dairelers.Where(x => x.DaireNo == daireno && x.BinaID == BinaID).Select(x => x.Borc).FirstOrDefault();
                var dairebilgi = db.Dairelers.Where(x => x.DaireNo == daireno && x.BinaID == BinaID).FirstOrDefault();
                ViewBag.b = dairebilgi;
                ViewBag.Makbuzlar = db.Makbuzs.Where(x => x.DaireID == dairebilgi.DaireID && x.BinaID == BinaID).OrderByDescending(x=> x.MakbuzID).ToList();
            }

            return View();
        }


        [HttpPost]
        public ActionResult Olustur(int[] SecilenAidatlar, int[] SecilenEkler, int daireID)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            // Aidat yada ek boşsa hata ver
            if ((SecilenAidatlar == null || !SecilenAidatlar.Any()) &&
                (SecilenEkler == null || !SecilenEkler.Any()))
            {
                TempData["Mesaj"] = "Lütfen en az bir borç seçiniz.";
                return RedirectToAction("Index");
            }
            var dairesorgu = db.Dairelers.Where(x => x.DaireID == daireID).FirstOrDefault();

            // İşlemleri transaction ile yap
            using (var tran = db.Database.BeginTransaction())
            {
                try
                {
                    decimal toplamTutar = 0;

                    if (SecilenAidatlar != null && SecilenAidatlar.Any())
                        toplamTutar += db.Aidats
                            .Where(x => SecilenAidatlar.Contains(x.AidatID))
                            .Sum(x => (decimal?)x.AidatTutar) ?? 0;

                    if (SecilenEkler != null && SecilenEkler.Any())
                        toplamTutar += db.Eks
                            .Where(x => SecilenEkler.Contains(x.EkID))
                            .Sum(x => (decimal?)x.EkTutar) ?? 0;

                    // Dairenin borcunu güncelle
                    dairesorgu.Borc = dairesorgu.Borc - toplamTutar;

                    var sonmakbuz = db.Makbuzs.OrderByDescending(x => x.MakbuzID).FirstOrDefault(x=> x.BinaID == BinaID && x.Durum == "A");
                    
                    // Yeni makbuz numarasını belirleme
                    var yenino = sonmakbuz.MakbuzNo + 1;

                    //makbuz ekle
                    Makbuz yeni = new Makbuz
                    {
                        MakbuzNo = yenino, 
                        BinaID = BinaID,
                        DaireID = daireID,
                        MabuzTutar = toplamTutar,
                        MakbuzTarihi = DateTime.Now,
                        Durum = "A",
                        OnayliMi = false
                    };

                    db.Makbuzs.Add(yeni);
                    db.SaveChanges();

                   // aidat satırlarını ekle
                    if (SecilenAidatlar != null)
                    {
                        var aidatlar = db.Aidats.Where(x => SecilenAidatlar.Contains(x.AidatID)).ToList();
                        foreach (var a in aidatlar)
                        {
                            a.Durum = "P"; 

                            db.MakbuzSatirs.Add(new MakbuzSatir
                            {
                                MakbuzID = yeni.MakbuzID,
                                AyAdi = a.AidatAy,
                                YilAdi = a.AidatYil,
                                Tutar = a.AidatTutar,
                                DaireID = dairesorgu.DaireID,
                                BinaID = a.BinaID,
                                Durum = "A",
                                EkMiAidatMi = "A"
                            });
                        }
                    }

                    //ek satırlarını ekle
                    if (SecilenEkler != null)
                    {
                        var ekler = db.Eks.Where(x => SecilenEkler.Contains(x.EkID)).ToList();
                        foreach (var e in ekler)
                        {
                            e.Durum = "P"; 

                            db.MakbuzSatirs.Add(new MakbuzSatir
                            {
                                MakbuzID = yeni.MakbuzID,
                                AyAdi = e.EkAy,
                                YilAdi = e.EkYil,
                                Tutar = e.EkTutar,
                                DaireID = dairesorgu.DaireID,
                                BinaID = e.BinaID,
                                Durum = "A",
                                EkMiAidatMi = "E"
                            });
                        }
                    }

                    db.SaveChanges();
                    // başarılıysa commit et
                    tran.Commit();

                    TempData["Basarili"] = "Toplu makbuz başarıyla oluşturuldu.";
                }
                catch (Exception ex)
                {
                    // hata varsa rollback yap
                    tran.Rollback();
                    TempData["Hata"] = "Hata oluştu: " + ex.Message;
                }
            }

            return RedirectToAction("Index", "TopluMakbuz", new { DaireNo = dairesorgu.DaireNo});
        }


    }
}