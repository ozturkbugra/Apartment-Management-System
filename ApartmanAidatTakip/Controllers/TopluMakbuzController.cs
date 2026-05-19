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
                db.SaveChanges();
            }
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

                var dairevarmi = db.Dairelers.Where(x => x.DaireNo == daireno && x.BinaID == BinaID).FirstOrDefault();

                if(dairevarmi == null)
                {
                    TempData["Hata"] = "Geçersiz Daire Numarası";
                    return RedirectToAction("Index", "TopluMakbuz");
                }

                ViewBag.EkBorclar = db.Eks.Where(x => x.DaireNo == daireno && x.BinaID == BinaID && x.Durum == "A").ToList();
                ViewBag.AidatBorclar = db.Aidats.Where(x => x.DaireNo == daireno && x.BinaID == BinaID && x.Durum == "A").ToList();
                ViewBag.Borc = db.Dairelers.Where(x => x.DaireNo == daireno && x.BinaID == BinaID).Select(x => x.Borc).FirstOrDefault();
                var dairebilgi = db.Dairelers.Where(x => x.DaireNo == daireno && x.BinaID == BinaID).FirstOrDefault();
                ViewBag.b = dairebilgi;
                ViewBag.Makbuzlar = db.Makbuzs.Where(x => x.DaireID == dairebilgi.DaireID && x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x=> x.MakbuzID).ToList();
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

        public ActionResult MakbuzSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            // Güvenlik ve Ayar kontrolü için Binalar tablosundaki kaydı da çekiyoruz
            var binaAyar = db.Binalars.FirstOrDefault(x => x.BinaID == BinaID);
            var makbuz = db.Makbuzs.Where(x => x.MakbuzID == id && x.BinaID == BinaID).FirstOrDefault();

            if (makbuz == null)
            {
                TempData["Hata"] = "Makbuz bulunamadı.";
                return RedirectToAction("Index", "TopluMakbuz");
            }

            // YENİ EKLENEN KONTROL ALANI:
            // Makbuz onaylıysa VE (Bina ayarı null veya false ise) silmeye izin verme!
            // Eğer MakbuzOnayKaldir == true ise bu if bloğuna hiç girmeyecek ve silmeye izin verecek.
            if (makbuz.OnayliMi == true && (binaAyar?.MakbuzOnayKaldir != true))
            {
                TempData["Hata"] = "Bu Makbuz Onaylandığı için işlem yapılamaz";
                return RedirectToAction("Index", "TopluMakbuz");
            }

            // --- BUNDAN SONRASI MEVCUT YAPINIZIN BİREBİR AYNISIDIR (HİÇBİR ŞEY BOZULMADI) ---

            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (makbuz.MakbuzTarihi.Value.Month != AyKontrol || makbuz.MakbuzTarihi.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri silemezsiniz";
                return RedirectToAction("Index", "TopluMakbuz");
            }

            try
            {
                if (Session["DonemSorgu"].ToString() == "0")
                {
                    TempData["Hata"] = DateTime.Now.ToString("MMMM") + " Dönemini eklemediğiniz için bu işlemi yapamazsınız";
                    return RedirectToAction("Index", "TopluMakbuz");
                }

                if (makbuz.MabuzTutar != 0)
                {
                    var makbuzsatir = db.MakbuzSatirs.Where(x => x.MakbuzID == id && x.BinaID == BinaID && x.Durum == "A").ToList();
                    int DaireID = Convert.ToInt32(makbuz.DaireID);
                    var daire = db.Dairelers.Where(x => x.DaireID == DaireID && x.BinaID == BinaID).FirstOrDefault();

                    //daire.Borc += makbuz.MabuzTutar;
                    db.SaveChanges();

                    foreach (var item in makbuzsatir)
                    {
                        item.Durum = "P";
                        db.SaveChanges();

                        string ayadi = item.AyAdi;
                        int? yiladi = item.YilAdi;
                        int? daireid = item.DaireID;

                        var dairesec = db.Dairelers.Where(x => x.DaireID == daireid).FirstOrDefault();
                        int? daireno = dairesec.DaireNo;
                        string ekmiaidatmi = item.EkMiAidatMi;

                        if (ekmiaidatmi == "A")
                        {
                            var aidatsec = db.Aidats.Where(x => x.DaireNo == daireno && x.AidatAy == ayadi && x.AidatYil == yiladi && x.BinaID == BinaID && x.Durum == "P").FirstOrDefault();
                            if (aidatsec != null) aidatsec.Durum = "A";
                        }
                        if (ekmiaidatmi == "E")
                        {
                            var eksec = db.Eks.Where(x => x.DaireNo == daireno && x.EkAy == ayadi && x.EkYil == yiladi && x.BinaID == BinaID && x.Durum == "P").FirstOrDefault();
                            if (eksec != null) eksec.Durum = "A";
                        }

                        db.SaveChanges();
                    }

                    makbuz.Durum = "P";
                    db.SaveChanges();
                    MakbuzNoDuzenle();

                    int DaireID2 = Convert.ToInt32(makbuz.DaireID);
                    borcduzenle(DaireID2);
                }
                else
                {
                    makbuz.Durum = "P";
                    db.SaveChanges();
                }

                TempData["Basarili"] = "Makbuz Başarıyla Silindi";
            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu";
            }

            return RedirectToAction("Index", "TopluMakbuz");
        }

    }
}