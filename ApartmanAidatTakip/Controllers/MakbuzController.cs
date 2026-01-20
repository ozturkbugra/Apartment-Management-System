using ApartmanAidatTakip.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;

namespace ApartmanAidatTakip.Controllers
{
    public class MakbuzController : Controller
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

        public void bosmakbuzsil()
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            var bosMakbuzlar = db.Makbuzs.Where(x => x.MabuzTutar == 0 && x.BinaID == BinaID && x.Durum=="A").ToList();

            if (bosMakbuzlar.Any())
            {
                db.Makbuzs.RemoveRange(bosMakbuzlar);
                db.SaveChanges();
                MakbuzNoDuzenle();
            }

            

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
        public ActionResult Index()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            bosmakbuzsil();
            Session["Aktif"] = "Makbuz";
            Sabit();
            Session["DaireID"] = "0";
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID && x.Borc > 0).OrderBy(x => x.DaireNo).ToList();

            int ay = DateTime.Now.Month;
            int yil = DateTime.Now.Year;
            ViewBag.Makbuzlar = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.MakbuzTarihi.Value.Month == ay && x.MakbuzTarihi.Value.Year == yil).OrderByDescending(x => x.MakbuzID).ToList();
            ViewBag.SilinenMakbuzlar = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.Durum == "P").OrderByDescending(x => x.MakbuzID).ToList();
            DonemEklendiMi();
           
            return View();
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

        [HttpPost]
        public ActionResult Olustur(Makbuz makbuz)
        {
            var userCookie = Request.Cookies["KullaniciBilgileri"];
            if (userCookie == null || string.IsNullOrEmpty(userCookie.Values["BinaID"]))
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
                      

            if (makbuz == null || makbuz.DaireID == 0)
            {
                return RedirectToAction("Index", "Makbuz");
            }

            // Yeni makbuz bilgilerini ayarla
            makbuz.BinaID = BinaID;
            makbuz.Durum = "A";
            makbuz.OnayliMi = false;

            var s = db.Dairelers
                      .FirstOrDefault(x => x.BinaID == BinaID && x.DaireNo == makbuz.DaireID);

            if (s == null)
            {
                TempData["Hata"] = "Daire bulunamadı!";
                return RedirectToAction("Index", "Makbuz");
            }

            makbuz.DaireID = s.DaireID;
            makbuz.MakbuzTarihi = DateTime.Now.Date;
            makbuz.MabuzTutar = 0;

            db.Makbuzs.Add(makbuz);
            db.SaveChanges();

            var sonmakbuz = db.Makbuzs
                              .Where(x => x.BinaID == BinaID)
                              .OrderByDescending(x => x.MakbuzID)
                              .FirstOrDefault();

            if (sonmakbuz == null)
            {
                TempData["Hata"] = "Makbuz kaydedilemedi!";
                return RedirectToAction("Index", "Makbuz");
            }

            Session["MakbuzID"] = sonmakbuz.MakbuzID;
            Session["DaireID"] = sonmakbuz.DaireID;

            MakbuzNoDuzenle();

            return RedirectToAction("Ekle", "Makbuz");
        }


        public ActionResult Ekle(int? ID)
        {
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int MakbuzID;

            if (ID!= null)
            {
                MakbuzID = (int)ID;
            }
            else
            {
                if (Session["MakbuzID"] == null) {
                    return RedirectToAction("Index", "Makbuz");

                }
                MakbuzID = Convert.ToInt32(Session["MakbuzID"].ToString());

            }
            var sorgulama = db.Makbuzs.Where(x => x.MakbuzID == MakbuzID && x.BinaID == BinaID).FirstOrDefault();
            if(sorgulama == null)
            {
                return RedirectToAction("Index", "Makbuz");
            }

            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            
            else
            {
                Sabit();
                
                
                int DaireID = Convert.ToInt32(Session["DaireID"]);
                if (DaireID == 0)
                {
                    var makbuzsorgu = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
                    DaireID = Convert.ToInt32(makbuzsorgu.DaireID);
                }
                var makbuzvarmi = db.Makbuzs.Where(x => x.DaireID == DaireID && x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
                if (makbuzvarmi == null)
                {
                    return RedirectToAction("Index", "Makbuz");
                }
                int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
                var daire = db.Dairelers.Where(x => x.DaireID == DaireID && x.BinaID == BinaID).FirstOrDefault();

                ViewBag.d = daire;
                Session["DaireNo"] = daire.DaireNo;
                Session["MakbuzID"] = MakbuzID;
                ViewBag.Aidatlar = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == daire.DaireNo && x.Durum == "A");
                ViewBag.Ekler = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == daire.DaireNo && x.Durum == "A");
                ViewBag.MakbuzSatir = db.MakbuzSatirViews.Where(x => x.MakbuzID == MakbuzID && x.MakbuzSatirDurum == "A" && x.DaireNo == daire.DaireNo).ToList();

                Sabit();
                return View();
            }

        }


        public ActionResult AidatSatirEkle(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            int DaireID = Convert.ToInt32(Session["DaireID"]);
            int DaireNo = Convert.ToInt32(Session["DaireNo"]);
            int MakbuzID = Convert.ToInt32(Session["MakbuzID"]);
          

            var ms = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if(ms.OnayliMi == true)
            {
                TempData["Hata"] = "Bu Makbuz Onaylandığı için işlem yapılamaz";
                return RedirectToAction("Ekle", "Makbuz");
            }

            if (ms.MakbuzTarihi.Value.Month != AyKontrol || ms.MakbuzTarihi.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışında veri ekleyemezsiniz";
                return RedirectToAction("Ekle", "Makbuz");
            }

            try
            {
                var aidat = db.Aidats.Where(x => x.AidatID == id && x.DaireNo == DaireNo && x.BinaID == BinaID && x.Durum == "A").FirstOrDefault();
                if (aidat == null)
                {
                    return RedirectToAction("Index", "Makbuz");
                }
                if (DaireID == 0)
                {
                    var makbuzsorguilk = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
                    DaireID = Convert.ToInt32(makbuzsorguilk.DaireID);
                }
                MakbuzSatir makbuzSatir = new MakbuzSatir()
                {
                    MakbuzID = MakbuzID,
                    AyAdi = aidat.AidatAy,
                    YilAdi = aidat.AidatYil,
                    Tutar = aidat.AidatTutar,
                    DaireID = DaireID,
                    BinaID = BinaID,
                    Durum = "A",
                    EkMiAidatMi = "A",
                };
                db.MakbuzSatirs.Add(makbuzSatir);
                db.SaveChanges();
                aidat.Durum = "P";
                db.SaveChanges();
                var makbuz = db.Makbuzs.Where(x => x.MakbuzID == MakbuzID && x.BinaID == BinaID && x.Durum == "A").FirstOrDefault();
                makbuz.MabuzTutar += aidat.AidatTutar;
                db.SaveChanges();
                var dairesec = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireID == makbuz.DaireID).FirstOrDefault();
                dairesec.Borc -= aidat.AidatTutar;
                db.SaveChanges();
                if (makbuz.MakbuzNo == null)
                {
                    // En son makbuzu al
                    var sonmakbuz = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.MakbuzNo).FirstOrDefault();

                    //Eğer son makbuz varsa numarasını al, yoksa 0 olarak başla
                    int mno = sonmakbuz?.MakbuzNo ?? 0; // Nullable int türü için null kontrolü ve varsayılan değer 0

                    // Yeni makbuz numarası oluştur
                    int sonmakbuzno = mno + 1;
                    makbuz.MakbuzNo = sonmakbuzno;
                    db.SaveChanges();
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = sonmakbuzno + " numaralı makbuz eklendi",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();


                }
                MakbuzNoDuzenle();
                TempData["Basarili"] = "Başarıyla Eklendi";

            }
            catch (Exception)
            {
                TempData["Basarili"] = "Bir Hata Oluştu";
            }
            bosmakbuzsil();
            return RedirectToAction("Ekle", "Makbuz");
        }


        public ActionResult EkSatirEkle(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int KullaniciID = Convert.ToInt32(userCookie.Values["KullaniciID"]);
            int DaireID = Convert.ToInt32(Session["DaireID"]);
            int DaireNo = Convert.ToInt32(Session["DaireNo"]);
            int MakbuzID = Convert.ToInt32(Session["MakbuzID"]);

            var ms = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();

            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (ms.OnayliMi == true)
            {
                TempData["Hata"] = "Bu Makbuz Onaylandığı için işlem yapılamaz";
                return RedirectToAction("Ekle", "Makbuz");
            }

            if (ms.MakbuzTarihi.Value.Month != AyKontrol || ms.MakbuzTarihi.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışında veri ekleyemezsiniz";
                return RedirectToAction("Ekle", "Makbuz");
            }
            try
            {
                var ek = db.Eks.Where(x => x.EkID == id && x.DaireNo == DaireNo && x.BinaID == BinaID && x.Durum == "A").FirstOrDefault();
                if (ek == null)
                {
                    return RedirectToAction("Index", "Makbuz");
                }
                if (DaireID == 0)
                {
                    var makbuzsorguilk = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
                    DaireID = Convert.ToInt32(makbuzsorguilk.DaireID);
                }
                MakbuzSatir makbuzSatir = new MakbuzSatir()
                {
                    MakbuzID = MakbuzID,
                    AyAdi = ek.EkAy,
                    YilAdi = ek.EkYil,
                    Tutar = ek.EkTutar,
                    DaireID = DaireID,
                    BinaID = BinaID,
                    Durum = "A",
                    EkMiAidatMi = "E",
                };
                db.MakbuzSatirs.Add(makbuzSatir);
                db.SaveChanges();
                ek.Durum = "P";
                db.SaveChanges();
                var makbuz = db.Makbuzs.Where(x => x.MakbuzID == MakbuzID && x.BinaID == BinaID && x.Durum == "A").FirstOrDefault();
                makbuz.MabuzTutar += ek.EkTutar;
                db.SaveChanges();
                var dairesec = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireID == makbuz.DaireID).FirstOrDefault();
                dairesec.Borc -= ek.EkTutar;
                db.SaveChanges();
                if (makbuz.MakbuzNo == null)
                {
                    // En son makbuzu al
                    var sonmakbuz = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A").OrderByDescending(x => x.MakbuzNo).FirstOrDefault();

                    //Eğer son makbuz varsa numarasını al, yoksa 0 olarak başla
                    int mno = sonmakbuz?.MakbuzNo ?? 0; // Nullable int türü için null kontrolü ve varsayılan değer 0

                    // Yeni makbuz numarası oluştur
                    int sonmakbuzno = mno + 1;
                    makbuz.MakbuzNo = sonmakbuzno;
                    db.SaveChanges();
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = sonmakbuzno + " numaralı makbuz eklendi",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();
                }
                MakbuzNoDuzenle();

                TempData["Basarili"] = "Başarıyla Eklendi";
            }
            catch (Exception)
            {
                TempData["Basarili"] = "Bir Hata Oluştu";

            }
            bosmakbuzsil();
            return RedirectToAction("Ekle", "Makbuz");
        }

        public ActionResult SatirCikar(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int DaireID = Convert.ToInt32(Session["DaireID"]);
            int DaireNo = Convert.ToInt32(Session["DaireNo"]);
            int MakbuzID = Convert.ToInt32(Session["MakbuzID"]);

            var ms = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (ms.MakbuzTarihi.Value.Month != AyKontrol || ms.MakbuzTarihi.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri silemezsiniz";
                return RedirectToAction("Ekle", "Makbuz");
            }

            if (ms.OnayliMi == true)
            {
                TempData["Hata"] = "Bu Makbuz Onaylandığı için işlem yapılamaz";
                return RedirectToAction("Ekle", "Makbuz");
            }

            try
            {
                if (DaireID == 0)
                {
                    var makbuzsorguilk = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
                    DaireID = Convert.ToInt32(makbuzsorguilk.DaireID);
                }
                var satir = db.MakbuzSatirs.Where(x => x.MakbuzSatirID == id && x.BinaID == BinaID && x.DaireID == DaireID).FirstOrDefault();

                satir.Durum = "P";
                db.SaveChanges();
                var makbuzsorgu = db.Makbuzs.Where(x => x.MakbuzID == satir.MakbuzID).FirstOrDefault();
                

                makbuzsorgu.MabuzTutar -= satir.Tutar;
                db.SaveChanges();
                var dairesorguborc = db.Dairelers.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && x.DaireID == DaireID).FirstOrDefault();
                if (satir.EkMiAidatMi == "A")
                {

                    var aidatsorgu = db.Aidats.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && x.AidatAy == satir.AyAdi && x.AidatYil == satir.YilAdi).FirstOrDefault();
                    aidatsorgu.Durum = "A";
                    dairesorguborc.Borc += aidatsorgu.AidatTutar;
                    db.SaveChanges();

                }
                if (satir.EkMiAidatMi == "E")
                {
                    var eksorgu = db.Eks.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && x.EkAy == satir.AyAdi && x.EkYil == satir.YilAdi).FirstOrDefault();
                    eksorgu.Durum = "A";
                    dairesorguborc.Borc += eksorgu.EkTutar;
                    db.SaveChanges();
                }

                TempData["Basarili"] = "Başarıyla Çıkartıldı";
            }
            catch (Exception)
            {
                TempData["Hata"] = "Bir Hata Oluştu";

            }
            return RedirectToAction("Ekle", "Makbuz");
        }

        public ActionResult MakbuzSil(int id)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var makbuz = db.Makbuzs.Where(x => x.MakbuzID == id && x.BinaID == BinaID).FirstOrDefault();

            if (makbuz.OnayliMi == true)
            {
                TempData["Hata"] = "Bu Makbuz Onaylandığı için işlem yapılamaz";
                return RedirectToAction("Index", "Makbuz");
            }

            int AyKontrol = DateTime.Now.Month;
            int YilKontrol = DateTime.Now.Year;

            if (makbuz.MakbuzTarihi.Value.Month != AyKontrol || makbuz.MakbuzTarihi.Value.Year != YilKontrol)
            {
                TempData["Hata"] = "Bulunduğunuz Dönem dışındaki verileri silemezsiniz";
                return RedirectToAction("Index", "Makbuz");
            }
            try
            {
                if (Session["DonemSorgu"].ToString() == "0")
                {
                    TempData["Hata"] = DateTime.Now.ToString("MMMM") + " Dönemini eklemediğiniz için bu işlemi yapamazsınız";
                    return RedirectToAction("Index", "Makbuz");
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
                            aidatsec.Durum = "A";
                        }
                        if (ekmiaidatmi == "E")
                        {
                            var eksec = db.Eks.Where(x => x.DaireNo == daireno && x.EkAy == ayadi && x.EkYil == yiladi && x.BinaID == BinaID && x.Durum == "P").FirstOrDefault();
                            eksec.Durum = "A";
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

            return RedirectToAction("Index", "Makbuz");
        }

        public ActionResult GeneratePdf(int? MakbuzID)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            int DaireID = Convert.ToInt32(Session["DaireID"]);

            string kullaniciAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["KullaniciAdi"]);
            string adSoyad = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["AdSoyad"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);

            var varmi = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
            if (MakbuzID == null || varmi == null)
            {
                return RedirectToAction("Index", "Makbuz");
            }



            if (DaireID == 0)
            {
                var makbuzsorgu = db.Makbuzs.FirstOrDefault(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID);
                DaireID = Convert.ToInt32(makbuzsorgu.DaireID);
            }
            var makbuzsorgu1 = db.Makbuzs.FirstOrDefault(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID);
            var daire = db.Dairelers.FirstOrDefault(x => x.DaireID == DaireID && x.BinaID == BinaID);
            var makbuzSatirList = db.MakbuzSatirViews.Where(x => x.MakbuzID == MakbuzID && x.MakbuzSatirDurum == "A" && x.DaireNo == daire.DaireNo).ToList();

            var makbuzz = db.Makbuzs.Where(x => x.MakbuzID == MakbuzID).FirstOrDefault();
            makbuzz.OnayliMi = true;
            db.SaveChanges();


            MemoryStream workStream = new MemoryStream();
            Document document = new Document();
            PdfWriter.GetInstance(document, workStream).CloseStream = false;
            document.Open();

            // Set Turkish font
            string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
            Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
            Font tableHeaderFont = new Font(bfArialTurkish, 10, Font.BOLD);
            Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);

            // Load logo image
            string logoPath = Server.MapPath("~/Content/Admin/assets/img/binamakbuzlogo.png");
            iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
            logo.ScaleAbsolute(100f, 100f); // Adjust size as needed
            logo.Alignment = iTextSharp.text.Image.ALIGN_LEFT;
            logo.SpacingBefore = -20f;

            // Create a table to organize the header layout (3 columns: left for logo, middle for building info, right for date/receipt number)
            PdfPTable headerTable = new PdfPTable(3);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 20, 50, 30 }); // Adjust the column widths as needed

            // Left Column: Logo
            PdfPCell logoCell = new PdfPCell(logo);
            logoCell.Border = PdfPCell.NO_BORDER;
            logoCell.PaddingTop = -10f; // Yukarıya yaklaştır
            logoCell.VerticalAlignment = Element.ALIGN_TOP; // İçeriği en üste hizala
            headerTable.AddCell(logoCell);

            // Middle Column: Building Information
            PdfPCell buildingInfoCell = new PdfPCell();
            buildingInfoCell.Border = PdfPCell.NO_BORDER;
            buildingInfoCell.PaddingTop = -20f; // Yukarıya yaklaştır
            buildingInfoCell.VerticalAlignment = Element.ALIGN_TOP;
            buildingInfoCell.AddElement(new Paragraph(binaAdi.ToString().ToUpper(), titleFont));
            buildingInfoCell.AddElement(new Paragraph(binaAdres, subTitleFont));
            headerTable.AddCell(buildingInfoCell);

            // Right Column: Date and Receipt Number
            PdfPCell receiptInfoCell = new PdfPCell();
            receiptInfoCell.Border = PdfPCell.NO_BORDER;
            receiptInfoCell.PaddingTop = -20f; // Yukarıya yaklaştır
            receiptInfoCell.VerticalAlignment = Element.ALIGN_TOP;
            receiptInfoCell.AddElement(new Paragraph("Makbuz Tarihi: " + makbuzsorgu1.MakbuzTarihi.Value.ToString("dd/MM/yyyy"), subTitleFont));
            receiptInfoCell.AddElement(new Paragraph("Makbuz No: " + makbuzsorgu1.MakbuzNo, subTitleFont));
            receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            headerTable.AddCell(receiptInfoCell);


            document.Add(headerTable);

            // Center: Receipt Title
            Paragraph title = new Paragraph("MAKBUZ", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingBefore = -20f; // Negatif boşluk ile yukarı çekiyoruz
            document.Add(title);

            // Adding table for the receipt content
            PdfPTable table = new PdfPTable(5); // Add an extra column for row numbers
            table.WidthPercentage = 100;
            table.SpacingBefore = 20f;
            table.SpacingAfter = 20f;

            // Table headers
            table.AddCell(new PdfPCell(new Phrase("NO", tableHeaderFont))); // Row number column
            table.AddCell(new PdfPCell(new Phrase("AY", tableHeaderFont)));
            table.AddCell(new PdfPCell(new Phrase("YIL", tableHeaderFont)));
            table.AddCell(new PdfPCell(new Phrase("TÜRÜ", tableHeaderFont)));
            table.AddCell(new PdfPCell(new Phrase("TUTAR", tableHeaderFont)));

            decimal totalTutar = 0; // Initialize the total amount
            int rowNumber = 1; // Initialize row number

            // Table rows
            foreach (var item in makbuzSatirList)
            {
                string ayAdi = item.AyAdi ?? "N/A"; // Provide a default value for null
                string yilAdi = item.YilAdi?.ToString() ?? "N/A"; // Convert to string and provide a default value for null
                string tutar = item.Tutar?.ToString("C2") ?? "0,00 ₺"; // Provide a default value for null
                string tur = item.EkMiAidatMi;
                if (item.EkMiAidatMi == "A")
                {
                    tur = "Aidat";
                }
                if (item.EkMiAidatMi == "E")
                {
                    tur = "Demirbaş";
                }

                table.AddCell(new PdfPCell(new Phrase(rowNumber.ToString(), tableFont))); // Add row number
                table.AddCell(new PdfPCell(new Phrase(ayAdi, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(yilAdi, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(tur, tableFont)));
                table.AddCell(new PdfPCell(new Phrase(tutar, tableFont)));

                totalTutar += item.Tutar ?? 0; // Sum up the total amount
                rowNumber++; // Increment row number
            }

            // Add total row
            PdfPCell totalCell = new PdfPCell(new Phrase("TOPLAM", tableHeaderFont));
            totalCell.Colspan = 4;
            totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(totalCell);
            table.AddCell(new PdfPCell(new Phrase(totalTutar.ToString("C2"), tableFont)));
            table.SpacingAfter = 0f; // Tablonun altındaki boşluğu kaldır
            document.Add(table);

            // Borçlu bilgilerini içeren tablo oluştur
            PdfPTable debtorTable = new PdfPTable(2);
            debtorTable.WidthPercentage = 100;
            debtorTable.SetWidths(new float[] { 70, 30 });

            // Sol tarafa borçlu bilgileri
            PdfPCell debtorInfoCellLeft = new PdfPCell();
            debtorInfoCellLeft.Border = PdfPCell.NO_BORDER;
            debtorInfoCellLeft.VerticalAlignment = Element.ALIGN_MIDDLE;

            debtorInfoCellLeft.AddElement(new Paragraph("Daire No: " + daire.DaireNo, subTitleFont));
            debtorInfoCellLeft.AddElement(new Paragraph("Ad Soyad: " + daire.AdSoyad, subTitleFont));
            debtorInfoCellLeft.AddElement(new Paragraph("Toplam Borç: " + daire.Borc, subTitleFont));

            if (daire.Borc > 0)
            {
                var borcaciklama = db.Notlars.Where(x => x.BinaID == BinaID).FirstOrDefault();

                debtorInfoCellLeft.AddElement(new Paragraph("NOT: Lütfen borcunuzu zamanında ödeyiniz.",
                                        new Font(bfArialTurkish, 12, Font.ITALIC, BaseColor.BLACK)));

                if (borcaciklama != null)
                {
                    debtorInfoCellLeft.AddElement(new Paragraph(borcaciklama.BorcAciklama,
                                        new Font(bfArialTurkish, 12, Font.ITALIC, BaseColor.BLACK)));
                }
            }
            else
            {
                debtorInfoCellLeft.AddElement(new Paragraph("NOT: Borcunuzu Zamanında Ödediğiniz İçin Teşekkür Ederiz.",
                                    new Font(bfArialTurkish, 12, Font.ITALIC, BaseColor.BLACK)));
            }

            debtorTable.AddCell(debtorInfoCellLeft);

            // Sağ tarafa KAŞE - İMZA
            PdfPCell kaşeImzaCell = new PdfPCell();
            kaşeImzaCell.Border = PdfPCell.NO_BORDER;
            kaşeImzaCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            kaşeImzaCell.VerticalAlignment = Element.ALIGN_TOP;

            Paragraph kaşeImzaParagraph = new Paragraph("KAŞE - İMZA",
                                new Font(bfArialTurkish, 12, Font.BOLD, BaseColor.BLACK));
            kaşeImzaParagraph.Alignment = Element.ALIGN_RIGHT;
            kaşeImzaCell.AddElement(kaşeImzaParagraph);

            debtorTable.AddCell(kaşeImzaCell);

            // Tabloyu PDF'ye ekle
            document.Add(debtorTable);


            Font vukFont = new Font(bfArialTurkish, 8, Font.NORMAL);

            Paragraph vukNotu = new Paragraph("Bu belge 213 sayılı Vergi Usul Kanunu hükümlerine tabi değildir. Sadece apartman içi kayıtların tutulması amacıyla düzenlenmiştir.", vukFont);

            vukNotu.SpacingBefore = 10f; // İmza ve bilgilerden sonra aşağıya itiyoruz
            vukNotu.Alignment = Element.ALIGN_CENTER;


            document.Add(vukNotu);
            // --- VUK Notu Bitti ---

            document.Close();

            // Add reminder if debt is greater than 0


            document.Close();

            byte[] byteInfo = workStream.ToArray();
            workStream.Write(byteInfo, 0, byteInfo.Length);
            workStream.Position = 0;

            string makbuzadi = daire.DaireNo.ToString() + " No'lu Dairenin Makbuzu";

            Response.AppendHeader("Content-Disposition", "inline; filename=" + makbuzadi + ".pdf");
            return File(workStream, "application/pdf");
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

        public ActionResult Ara(int? DaireNo, int? MakbuzNo, int? GiderNo, int? TahsilatNo)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "Ara";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);


            if (MakbuzNo == null)
            {

                ViewBag.MakbuzNo = null;
            }
            if (DaireNo == null)
            {
                ViewBag.DaireNo = null;
            }
            if (GiderNo == null)
            {
                ViewBag.GiderNo = null;
            }
            if (TahsilatNo == null)
            {
                ViewBag.TahsilatNo = null;
            }

            if (DaireNo != null)
            {
                ViewBag.Makbuzlar = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && x.Durum == "A").OrderByDescending(x=> x.MakbuzID).ToList();
                ViewBag.DaireNo = DaireNo;

            }
            if (MakbuzNo != null)
            {
                ViewBag.Makbuzlar = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzNo == MakbuzNo && x.Durum == "A").OrderByDescending(x => x.MakbuzID).ToList();
                ViewBag.MakbuzNo = MakbuzNo;
            }
            if (MakbuzNo != null && DaireNo != null)
            {
                ViewBag.Makbuzlar = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzNo == MakbuzNo && x.DaireNo == DaireNo && x.Durum == "A").OrderByDescending(x => x.MakbuzID).ToList();
                ViewBag.DaireNo = DaireNo;
                ViewBag.MakbuzNo = MakbuzNo;
            }
            if (GiderNo != null)
            {
                ViewBag.GiderMakbuz = db.GiderViews.Where(x => x.BinaID == BinaID && x.GiderNo == GiderNo && x.Durum == "A").ToList();
                ViewBag.GiderNo = GiderNo;
            }

            if (TahsilatNo != null)
            {
                ViewBag.TahsilatMakbuz = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatNo == TahsilatNo && x.Durum == "A").ToList();
                ViewBag.TahsilatNo = TahsilatNo;
            }




            return View();
        }
    }
}