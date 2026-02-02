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
            // ... (Giriş kontrolleri aynı kalsın) ...
            if (Request.Cookies["KullaniciBilgileri"] == null) return RedirectToAction("Login", "AnaSayfa");
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            if ((SecilenAidatlar == null || !SecilenAidatlar.Any()) && (SecilenEkler == null || !SecilenEkler.Any()))
            {
                TempData["Mesaj"] = "Lütfen en az bir borç seçiniz.";
                return RedirectToAction("Index");
            }

            var dairesorgu = db.Dairelers.FirstOrDefault(x => x.DaireID == daireID);
            if (dairesorgu == null) return RedirectToAction("Index");

            using (var tran = db.Database.BeginTransaction())
            {
                try
                {
                    // --- GÜVENLİK KONTROLÜ (ÇİFT TIKLAMA ENGELİ) ---
                    // Seçilen aidatlar veritabanında HALA "A" (Aktif/Ödenmemiş) durumunda mı?
                    // Eğer ilk tıklama işlemi yaptıysa bunlar "P" olmuştur, o yüzden listeyi filtreli çekiyoruz.

                    List<Aidat> aidatListesi = new List<Aidat>();
                    List<Ek> ekListesi = new List<Ek>();

                    if (SecilenAidatlar != null && SecilenAidatlar.Any())
                    {
                        // BURASI ÖNEMLİ: && x.Durum == "A" ekledik.
                        aidatListesi = db.Aidats.Where(x => SecilenAidatlar.Contains(x.AidatID) && x.Durum == "A").ToList();

                        // Eğer seçilen sayı ile veritabanından gelen "ödenmemiş" sayı tutmuyorsa, biri ödenmiş demektir.
                        if (aidatListesi.Count != SecilenAidatlar.Length)
                        {
                            tran.Rollback();
                            TempData["Hata"] = "Seçilen aidatların bazıları zaten ödenmiş veya işlemde. Lütfen sayfayı yenileyiniz.";
                            return RedirectToAction("Index", "TopluMakbuz", new { DaireNo = dairesorgu.DaireNo });
                        }
                    }

                    if (SecilenEkler != null && SecilenEkler.Any())
                    {
                        // BURASI ÖNEMLİ: && x.Durum == "A" ekledik.
                        ekListesi = db.Eks.Where(x => SecilenEkler.Contains(x.EkID) && x.Durum == "A").ToList();

                        if (ekListesi.Count != SecilenEkler.Length)
                        {
                            tran.Rollback();
                            TempData["Hata"] = "Seçilen ek ödemelerin bazıları zaten ödenmiş. Lütfen sayfayı yenileyiniz.";
                            return RedirectToAction("Index", "TopluMakbuz", new { DaireNo = dairesorgu.DaireNo });
                        }
                    }

                    // Eğer iki liste de boşsa (yani bir şekilde hepsi ödenmişse) işlemi durdur.
                    if (!aidatListesi.Any() && !ekListesi.Any())
                    {
                        tran.Rollback();
                        return RedirectToAction("Index", "TopluMakbuz", new { DaireNo = dairesorgu.DaireNo });
                    }

                    // --- 1. TUTARLARI HESAPLA ---
                    decimal toplamAidatTutar = aidatListesi.Sum(x => (decimal?)x.AidatTutar) ?? 0;
                    decimal toplamEkTutar = ekListesi.Sum(x => (decimal?)x.EkTutar) ?? 0;
                    decimal genelToplam = toplamAidatTutar + toplamEkTutar;

                    // --- 2. BORÇ DÜŞME ---
                    dairesorgu.Borc -= genelToplam;

                    // --- 3. MAKBUZ NO BELİRLEME ---
                    var sonmakbuz = db.Makbuzs.OrderByDescending(x => x.MakbuzID).FirstOrDefault(x => x.BinaID == BinaID && x.Durum == "A");
                    int yenino = (sonmakbuz != null) ? (sonmakbuz.MakbuzNo ?? 0) + 1 : 1;

                    // --- 4. MAKBUZ OLUŞTUR ---
                    Makbuz yeni = new Makbuz
                    {
                        MakbuzNo = yenino,
                        BinaID = BinaID,
                        DaireID = daireID,
                        MabuzTutar = genelToplam,
                        MakbuzTarihi = DateTime.Now,
                        Durum = "A",
                        OnayliMi = false
                    };

                    db.Makbuzs.Add(yeni);
                    db.SaveChanges();

                    // --- 5. SATIRLARI HAZIRLA ---
                    List<MakbuzSatir> eklenecekSatirlar = new List<MakbuzSatir>();

                    foreach (var a in aidatListesi)
                    {
                        a.Durum = "P"; // Aidatı Pasife çekiyoruz (ÖDENDİ)
                        eklenecekSatirlar.Add(new MakbuzSatir
                        {
                            MakbuzID = yeni.MakbuzID,
                            AyAdi = a.AidatAy,
                            YilAdi = a.AidatYil,
                            Tutar = a.AidatTutar,
                            DaireID = daireID,
                            BinaID = BinaID,
                            Durum = "A",
                            EkMiAidatMi = "A"
                        });
                    }

                    foreach (var e in ekListesi)
                    {
                        e.Durum = "P"; // Eki Pasife çekiyoruz (ÖDENDİ)
                        eklenecekSatirlar.Add(new MakbuzSatir
                        {
                            MakbuzID = yeni.MakbuzID,
                            AyAdi = e.EkAy,
                            YilAdi = e.EkYil,
                            Tutar = e.EkTutar,
                            DaireID = daireID,
                            BinaID = BinaID,
                            Durum = "A",
                            EkMiAidatMi = "E"
                        });
                    }

                    if (eklenecekSatirlar.Any())
                    {
                        db.MakbuzSatirs.AddRange(eklenecekSatirlar);
                        db.SaveChanges();
                    }

                    tran.Commit(); // İşlemi onayla
                    TempData["Basarili"] = "Makbuz başarıyla oluşturuldu.";
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    TempData["Hata"] = "Hata oluştu: " + ex.Message;
                }
            }

            return RedirectToAction("Index", "TopluMakbuz", new { DaireNo = dairesorgu.DaireNo });
        }


    }
}