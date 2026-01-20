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
            string p = Crypto.Hash(Parola, "MD5");
            var a = db.KullanicilarViews.Where(x => x.KullaniciAdi == KullaniciAdi && x.Parola == p && x.BinaID == BinaID && x.KullaniciDurumu == "A").FirstOrDefault();
            if (a != null)
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

                // Cookie'nin geçerlilik süresini belirleyin (örneğin 1 gün)
                if (remember != null)
                {
                    userCookie.Expires = DateTime.Now.AddDays(365); // 1 ay
                }
                else
                {
                    userCookie.Expires = DateTime.Now.AddDays(1); // 1 gün
                }


                // Cookie'yi ekle
                Response.Cookies.Add(userCookie);
                return RedirectToAction("Index", "AnaSayfa");
            }
            else
            {
                ViewBag.Uyari = "Kullanıcı Adı, Şifre veya bina yanlış";
                DateTime simdi = DateTime.Now.Date;
                ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();

                return View();
            }

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

            var buAyMakbuzListesi = db.MakbuzViews.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.MakbuzTarihi.Value.Year == currentYear && x.MakbuzTarihi.Value.Month == currentMonth)
                .OrderByDescending(x => x.MakbuzID).ToList();

            var buAyTahsilatListesi = db.Tahsilats.AsNoTracking()
                .Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == currentYear && x.TahsilatTarih.Value.Month == currentMonth)
                .OrderByDescending(x => x.TahsilatID).ToList();

            // --- 4. HESAPLAMALAR ---
            decimal aygider = buAyGiderListesi.Sum(x => x.GiderTutar) ?? 0;
            decimal makbuzgelir = buAyMakbuzListesi.Sum(x => x.MabuzTutar) ?? 0;
            decimal tahsilatgelir = buAyTahsilatListesi.Sum(x => x.TahsilatTutar) ?? 0;

            ViewBag.aygelir = makbuzgelir + tahsilatgelir;
            ViewBag.aygider = aygider;
            ViewBag.Giderler = buAyGiderListesi.ToList(); // Ekrana sadece son 10 taneyi bas, hepsini değil
            ViewBag.Makbuzlar = buAyMakbuzListesi.ToList();
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

            // --- 6. KASA HESABI (FALLBACK HIZLANDIRMA) ---
            // --- 6. KASA HESABI (FİNAL DÜZELTME - DEVİR KONTROLLÜ) ---
            var acilis = db.AcilisBakiyes.AsNoTracking().FirstOrDefault(x => x.BinaID == BinaID);
            decimal acilisbakiye = acilis?.ToplamTutar ?? 0;
            decimal ekacilis = acilis?.EkTutar ?? 0;

            // Bu aya ait (Örn: Ocak 2026) kapatılmış kasa var mı?
            var son_kasa = db.Kasas.AsNoTracking().FirstOrDefault(x => x.KasaYil == currentYear && x.AyKodu == currentMonth && x.BinaID == BinaID);

            if (son_kasa != null)
            {
                // Bu ayın kasası zaten kapatılmış, direkt onu göster.
                decimal demirbasgider = buAyGiderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;

                ViewBag.Kasa = (son_kasa.KasaToplam + makbuzgelir + tahsilatgelir) - aygider;
                ViewBag.EkBakiye = (buAyTahsilatListesi.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) + son_kasa.KasaEk) - demirbasgider;
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

                    decimal tumtahsilatEk = (from ms in db.MakbuzSatirs
                                             join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                             where m.BinaID == BinaID && m.Durum == "A" && ms.EkMiAidatMi == "E"
                                             select ms.Tutar).Sum() ?? 0;

                    decimal gider2 = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;

                    var tumTahsilatlar = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A").Select(x => new { x.TahsilatTutar, x.DemirbasMi }).ToList();
                    decimal tahsilat3 = tumTahsilatlar.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;
                    decimal aidattahsilat = tumTahsilatlar.Where(x => x.DemirbasMi == false).Sum(x => x.TahsilatTutar) ?? 0;
                    decimal demirbasgider = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.Durum == "A" && x.GiderTuruID == 6).Sum(x => (decimal?)x.GiderTutar) ?? 0;

                    ViewBag.Kasa = (acilisbakiye + tummakbuz + tahsilat3 + aidattahsilat) - gider2;
                    ViewBag.EkBakiye = (tahsilat3 + ekacilis + tumtahsilatEk) - demirbasgider;
                    ViewBag.AidatBakiye = (decimal)ViewBag.Kasa - (decimal)ViewBag.EkBakiye;
                }
                else
                {
                    // --- KRİTİK DÜZELTME BURADA ---
                    int sonKasaYil = bironcekikasa.KasaYil ?? 0;
                    int sonKasaAy = bironcekikasa.AyKodu ?? 0;

                    // Mantık: Eğer bulunan son kasa "Hemen Bir Önceki Ay" ise (Örn: Ocak'tayız, son kasa Aralık),
                    // devir yapılmamış olabilir. O yüzden O AYI DA (Aralık) hesaplamaya dahil et (>=).
                    // Ama çok eski bir aysa (Örn: Ocak'tayız, son kasa Temmuz), o kesin kapanmıştır, üzerine ekle (>).

                    bool hemenOncekiAyMi = (currentYear == sonKasaYil && currentMonth - 1 == sonKasaAy) || (currentYear == sonKasaYil + 1 && currentMonth == 1 && sonKasaAy == 12);

                    // Eğer hemen önceki aysa ">=" (dahil et), değilse ">" (hariç tut)
                    // Bu sayede Aralık ayının içi boş olsa bile (devir yapılmadığı için), Aralık hareketlerini de toplayıp ekleriz.

                    // 1. MAKBUZLAR
                    var makbuzSorgu = from ms in db.MakbuzSatirs
                                      join m in db.Makbuzs on ms.MakbuzID equals m.MakbuzID
                                      where m.BinaID == BinaID && m.Durum == "A"
                                      select new { m.MakbuzTarihi, ms.Tutar };

                    decimal araDonemMakbuz;
                    if (hemenOncekiAyMi)
                        araDonemMakbuz = makbuzSorgu.Where(x => x.MakbuzTarihi.Value.Year > sonKasaYil || (x.MakbuzTarihi.Value.Year == sonKasaYil && x.MakbuzTarihi.Value.Month >= sonKasaAy)).Sum(x => (decimal?)x.Tutar) ?? 0;
                    else
                        araDonemMakbuz = makbuzSorgu.Where(x => x.MakbuzTarihi.Value.Year > sonKasaYil || (x.MakbuzTarihi.Value.Year == sonKasaYil && x.MakbuzTarihi.Value.Month > sonKasaAy)).Sum(x => (decimal?)x.Tutar) ?? 0;


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

                    // HESAPLAMA
                    ViewBag.Kasa = (bironcekikasa.KasaToplam + araDonemMakbuz + araDonemTahsilatToplam) - araDonemGiderToplam;
                    ViewBag.EkBakiye = (bironcekikasa.KasaEk + araDonemDemirbasTahsilat) - araDonemDemirbasGider;
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
                return View();
            }
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
                if (sonKasaDonemi != null && aidat.AidatYil < sonKasaDonemi.KasaYil && yeniAyKodu < sonKasaDonemi.AyKodu)
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
                    db.SaveChanges();
                    Hareketler hareketler = new Hareketler()
                    {
                        BinaID = BinaID,
                        KullaniciID = KullaniciID,
                        OlayAciklama = aidat.DaireNo + " dairesine " + aidat.AidatTutar + " tutarında " + aidat.AidatAy + " - " + aidat.AidatYil + " borçlandırılmıştır.",
                        Tarih = DateTime.Now,
                        Tur = "Ekleme",
                    };
                    db.Hareketlers.Add(hareketler);
                    db.SaveChanges();
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
                    db.SaveChanges();

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
                    db.SaveChanges();

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


        public ActionResult DonemEkle()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DonemEkle";
            Sabit();
            return View();

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
                    // Bu kısım veritabanına yazma yapmadığı için hızlıca kontrol edip dönebiliriz.

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

                    // 2. KASA OLUŞTURMA VE DEVİR İŞLEMLERİ (Veritabanı Yazma Başlıyor)

                    DateTime bugun = DateTime.Now;
                    DateTime oncekiAy = bugun.AddMonths(-1);
                    int oncekiYil = oncekiAy.Year;
                    int oncekiAyKodu = oncekiAy.Month;

                    var son_kasa = db.Kasas.FirstOrDefault(x => x.KasaYil == oncekiYil && x.AyKodu == oncekiAyKodu && x.BinaID == BinaID);

                    // Eğer önceki ayın kasası yoksa oluştur (Devir Bakiyesi Oluşturma)
                    if (son_kasa == null)
                    {
                        var ayAdi = new DateTime(oncekiYil, oncekiAyKodu, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR"));
                        var eklenensonkasa = db.Kasas.Where(x => x.BinaID == BinaID).OrderByDescending(x => x.KasaID).FirstOrDefault();

                        decimal eklenecekaidat, eklenecekek;
                        if (eklenensonkasa != null)
                        {
                            eklenecekaidat = Convert.ToDecimal(eklenensonkasa.KasaAidat);
                            eklenecekek = Convert.ToDecimal(eklenensonkasa.KasaEk);
                        }
                        else
                        {
                            eklenecekaidat = Convert.ToDecimal(acilisbakiyesieklendimi.AidatTutar);
                            eklenecekek = Convert.ToDecimal(acilisbakiyesieklendimi.EkTutar);
                        }

                        var yeniKasa = new Kasa
                        {
                            KasaYil = oncekiYil,
                            AyKodu = oncekiAyKodu,
                            BinaID = BinaID,
                            KasaEk = eklenecekek,
                            KasaAidat = eklenecekaidat,
                            KasaAy = ayAdi,
                            KasaToplam = eklenecekek + eklenecekaidat
                        };

                        db.Kasas.Add(yeniKasa);
                        db.SaveChanges(); // Buradaki SaveChanges kasayı oluşturmak için zorunlu, transaction içinde olduğu için güvenli.
                    }

                    // Mevcut ayın kasası ve toplamlarını hesaplama
                    var son_kasa2 = db.Kasas.FirstOrDefault(x => x.KasaYil == oncekiYil && x.AyKodu == oncekiAyKodu && x.BinaID == BinaID);
                    decimal kasaek = Convert.ToDecimal(son_kasa2.KasaEk);
                    decimal kasaaidat = Convert.ToDecimal(son_kasa2.KasaAidat);

                    int yil = oncekiAy.Year;
                    int ay = oncekiAy.Month;

                    var makbuzIDListesi = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.MakbuzTarihi.Value.Year == yil && x.MakbuzTarihi.Value.Month == ay).Select(x => x.MakbuzID).ToList();

                    var makbuzToplam = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == "A" && makbuzIDListesi.Contains((int)x.MakbuzID)).Sum(x => (decimal?)x.Tutar) ?? 0;
                    var ektoplam2 = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == "E" && makbuzIDListesi.Contains((int)x.MakbuzID)).Sum(x => (decimal?)x.Tutar) ?? 0;
                    var ektoplam1 = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == yil && x.TahsilatTarih.Value.Month == ay && x.DemirbasMi == true).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
                    var giderektoplam = db.Giders.Where(x => x.GiderTuruID == 6 && x.Durum == "A" && x.BinaID == BinaID && x.GiderTarih.Value.Year == yil && x.GiderTarih.Value.Month == ay).Sum(x => (decimal?)x.GiderTutar) ?? 0;
                    var gidertoplam = db.Giders.Where(x => x.GiderTuruID != 6 && x.Durum == "A" && x.BinaID == BinaID && x.GiderTarih.Value.Year == yil && x.GiderTarih.Value.Month == ay).Sum(x => (decimal?)x.GiderTutar) ?? 0;
                    var aidattoplam3 = db.Tahsilats.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.TahsilatTarih.Value.Year == yil && x.TahsilatTarih.Value.Month == ay && x.DemirbasMi == false).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

                    var aidattoplam = (makbuzToplam + kasaaidat + aidattoplam3) - gidertoplam;
                    var ektoplam = (ektoplam1 + ektoplam2 + kasaek) - giderektoplam;
                    var fulltoplam = aidattoplam + ektoplam;


                    // 3. AİDAT EKLEME İŞLEMİ (Peşin Ödeyen Kontrollü)
                    if (aidat.AidatTutar != null)
                    {
                        if (donemeklendimi == null)
                        {
                            var daireler = db.Dairelers.Where(x => x.BinaID == BinaID).ToList();

                            // Peşin ödeyenleri döngü öncesi tek seferde çekiyoruz (Performans için)
                            var pesinOdeyenlerListesi = db.PesinOdemelers
                                                          .Where(x => x.Yil == aidat.AidatYil && x.BinaID == BinaID)
                                                          .Select(x => x.DaireID)
                                                          .ToList();

                            foreach (var item in daireler)
                            {
                                // Yönetici Kontrolü
                                if (item.YonetimdeMi == "E") continue;

                                // Peşin Ödeyen Kontrolü
                                if (pesinOdeyenlerListesi.Contains(item.DaireID)) continue;

                                var daireno = item.DaireNo;

                                Aidat aidat1 = new Aidat()
                                {
                                    AidatAy = aidat.AidatAy,
                                    AidatYil = aidat.AidatYil,
                                    AidatTutar = aidat.AidatTutar,
                                    DaireNo = daireno,
                                    BinaID = BinaID,
                                    ZamEklendiMi = "H",
                                    Durum = "A",
                                };

                                db.Aidats.Add(aidat1);

                                // Daire borcunu artır
                                item.Borc += aidat.AidatTutar;
                            }

                            // Döngü bitti, tüm aidatları ve borç güncellemelerini tek seferde kaydediyoruz.
                            db.SaveChanges();

                            // Kasa Kaydı
                            Kasa kasa = new Kasa()
                            {
                                KasaAy = aidat.AidatAy,
                                KasaYil = aidat.AidatYil,
                                KasaAidat = aidattoplam,
                                KasaEk = ektoplam,
                                KasaToplam = fulltoplam,
                                BinaID = BinaID,
                                AyKodu = DateTime.Now.Month
                            };
                            db.Kasas.Add(kasa);

                            // Hareket Kaydı
                            Hareketler hareketler = new Hareketler()
                            {
                                BinaID = BinaID,
                                KullaniciID = KullaniciID,
                                OlayAciklama = aidat.AidatTutar + " TL tutarında " + aidat.AidatAy + " - " + aidat.AidatYil + " Dönemi Eklenmiştir.",
                                Tarih = DateTime.Now,
                                Tur = "Ekleme",
                            };
                            db.Hareketlers.Add(hareketler);

                            // Bekleyen Makbuzları Onaylama
                            var onaylanmayanmakbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.OnayliMi == false).ToList();
                            if (onaylanmayanmakbuzlar.Count > 0)
                            {
                                foreach (var makbuz in onaylanmayanmakbuzlar)
                                {
                                    makbuz.OnayliMi = true;
                                }
                            }

                            db.SaveChanges(); // Kasa, Hareket ve Makbuz onaylarını kaydet
                        }
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
                                if (item.YonetimdeMi == "E") continue;

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
                    // Buraya kadar hata almadan geldiysek her şeyi kalıcı olarak veritabanına işle.
                    transaction.Commit();

                    if (ek.EkTutar != null && donemeklendimi != null && donemeklendimi2 == null)
                    {
                        TempData["Basarili"] = "Aidat Daha Önce Eklendiği İçin Sadece Ek Eklendi";
                    }
                    else if (donemeklendimi == null && donemeklendimi2 != null)
                    {
                        TempData["Basarili"] = "Ek Daha Önce Eklendiği İçin Sadece Aidat Eklendi";
                    }
                    else if (donemeklendimi == null && donemeklendimi2 == null)
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
                    // Herhangi bir hata olursa (elektrik kesintisi, veri hatası vb.) yapılan TÜM işlemleri geri al.
                    transaction.Rollback();
                    TempData["Hata"] = "Bir Hata Oluştu! İşlemler geri alındı. Hata Detayı: " + ex.Message;
                }
            }

            Sabit();
            return View();
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
            ViewBag.BorcluDaireler = db.Dairelers.Where(x => x.BinaID == BinaID && x.Borc > 0).OrderBy(x => x.DaireNo).ToList();
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
                db.SaveChanges();
            }
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
                db.SaveChanges();
            }
        }


        public ActionResult AcilisBakiye()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var a = db.AcilisBakiyes.Where(x => x.BinaID == BinaID).Count();

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

            ViewBag.Bakiye = db.AcilisBakiyes.Where(x => x.BinaID == BinaID).ToList();

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
                ViewBag.Aidat = db.Aidats
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .OrderByDescending(x => x.AidatID)
                    .ToList();

                // DEMİRBAŞLAR: Hem Ödenen (P) Hem Ödenmeyen (A) gelsin, ID'ye göre tersten sıralansın
                ViewBag.Ek = db.Eks
                    .Where(x => x.BinaID == BinaID && x.DaireNo == DaireNo && (x.Durum == "A" || x.Durum == "P"))
                    .OrderByDescending(x => x.EkID)
                    .ToList();

                ViewBag.DaireNo = DaireNo;
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
            ViewBag.List = db.PesinOdemelerViews.Where(x => x.BinaID == BinaID).ToList();
            ViewBag.Daireler = db.Dairelers.Where(x => x.BinaID == BinaID).OrderBy(x => x.DaireNo).ToList();
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
    }
}