using ApartmanAidatTakip.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
namespace ApartmanAidatTakip.Controllers
{
    public class RaporlarController : Controller
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

        public ActionResult GelirGider(DateTime? ilk, DateTime? son)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "GelirGider";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            if (ilk != null && son != null)
            {
                ViewBag.tarihdeger1 = ilk.Value.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = son.Value.ToString("yyyy-MM-dd");
                if (ilk.HasValue)
                {
                    ilk = ilk.Value.Date; // Saat kısmını 00:00:00 yapar
                }

                // tarih2'nin saatini 23:59:59 olarak ayarla
                if (son.HasValue)
                {
                    son = son.Value.Date.AddDays(1).AddTicks(-1); // Saat kısmını 23:59:59 yapar
                }
                ViewBag.MakbuzGelir = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= ilk && x.MakbuzTarihi <= son && x.Durum == "A").OrderBy(X => X.MakbuzID).ToList();
                ViewBag.TahsilatGelir = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A").OrderBy(X => X.TahsilatID).ToList();
                ViewBag.Gider = db.GiderViews.Where(x => x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").OrderBy(X => X.GiderID).ToList();

            }
            else
            {
                ViewBag.tarihdeger1 = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = DateTime.Now.ToString("yyyy-MM-dd");
            }
            return View();
        }

        public ActionResult GelirPDF(DateTime? ilk, DateTime? son)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var makbuzGelir = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= ilk && x.MakbuzTarihi <= son && x.Durum == "A").OrderBy(x => x.MakbuzID).ToList();
            var tahsilatGelir = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A").OrderBy(x => x.TahsilatID).ToList();

            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 0, 30);
                MemoryStream workStream = new MemoryStream();
                PdfWriter.GetInstance(document, workStream).CloseStream = false;

                document.Open();

                // Türkçe karakter desteği için Unicode font ekleme
                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bf, 14, Font.BOLD);
                Font tableFont = new Font(bf, 9);
                Font boldTableFont = new Font(bf, 10, Font.BOLD);

                // Sağ üst köşeye tarih ve saat ekleme
                string currentDateTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                Paragraph dateTimeParagraph = new Paragraph($"ÇIKTI TARİHİ: {currentDateTime}", tableFont)
                {
                    Alignment = Element.ALIGN_RIGHT
                };
                document.Add(dateTimeParagraph);

                // Başlık
                Paragraph title = new Paragraph("Gelir Raporu", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(title);

                // Tarih Aralığı
                Paragraph dateRange = new Paragraph($"Tarih Aralığı: {ilk?.ToString("dd/MM/yyyy")} - {son?.ToString("dd/MM/yyyy")}", tableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(dateRange);

                document.Add(new Paragraph("\n"));

                // Makbuz Geliri Tablosu
                Paragraph makbuzGelirHeader = new Paragraph("Makbuz Geliri", boldTableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(makbuzGelirHeader);
                document.Add(new Paragraph("\n"));

                PdfPTable makbuzGelirTable = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };
                makbuzGelirTable.SetWidths(new float[] { 2f, 2f, 2f, 2f }); // Otomatik genişlik ayarlama

                string[] makbuzHeader = { "Makbuz No", "Tarih", "Daire No", "Tutar" };
                foreach (string header in makbuzHeader)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, boldTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    makbuzGelirTable.AddCell(cell);
                }

                decimal makbuzToplam = 0;
                foreach (var item in makbuzGelir)
                {
                    makbuzGelirTable.AddCell(new PdfPCell(new Phrase(item.MakbuzNo.ToString(), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    makbuzGelirTable.AddCell(new PdfPCell(new Phrase(item.MakbuzTarihi.Value.ToString("dd/MM/yyyy"), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    makbuzGelirTable.AddCell(new PdfPCell(new Phrase(item.DaireNo.ToString(), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    makbuzGelirTable.AddCell(new PdfPCell(new Phrase(item.MabuzTutar.Value.ToString("N2"), tableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                    makbuzToplam += (decimal)item.MabuzTutar;
                }

                PdfPCell toplamCell = new PdfPCell(new Phrase("Toplam", boldTableFont))
                {
                    Colspan = 3,
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                makbuzGelirTable.AddCell(toplamCell);
                makbuzGelirTable.AddCell(new PdfPCell(new Phrase(makbuzToplam.ToString("N2"), boldTableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });

                document.Add(makbuzGelirTable);
                document.Add(new Paragraph("\n"));

                // Tahsilat Geliri Tablosu
                Paragraph tahsilatGelirHeader = new Paragraph("Tahsilat Geliri", boldTableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(tahsilatGelirHeader);
                document.Add(new Paragraph("\n"));

                PdfPTable tahsilatGelirTable = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };
                tahsilatGelirTable.SetWidths(new float[] { 10f, 10f, 50f, 15f });

                string[] tahsilatHeader = { "Tahsilat No", "Tarih", "Açıklama", "Tutar" };
                foreach (string header in tahsilatHeader)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, boldTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    tahsilatGelirTable.AddCell(cell);
                }

                decimal tahsilatToplam = 0;
                foreach (var item in tahsilatGelir)
                {
                    tahsilatGelirTable.AddCell(new PdfPCell(new Phrase(item.TahsilatNo.ToString(), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    tahsilatGelirTable.AddCell(new PdfPCell(new Phrase(item.TahsilatTarih.Value.ToString("dd/MM/yyyy"), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    tahsilatGelirTable.AddCell(new PdfPCell(new Phrase(item.TahsilatAciklama, tableFont)) { HorizontalAlignment = Element.ALIGN_LEFT });
                    tahsilatGelirTable.AddCell(new PdfPCell(new Phrase(item.TahsilatTutar.Value.ToString("N2"), tableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                    tahsilatToplam += (decimal)item.TahsilatTutar;
                }

                PdfPCell tahsilatToplamCell = new PdfPCell(new Phrase("Toplam", boldTableFont))
                {
                    Colspan = 3,
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                tahsilatGelirTable.AddCell(tahsilatToplamCell);
                tahsilatGelirTable.AddCell(new PdfPCell(new Phrase(tahsilatToplam.ToString("N2"), boldTableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });

                document.Add(tahsilatGelirTable);


                
                decimal total = tahsilatToplam + makbuzToplam;
                Paragraph toplam = new Paragraph($"TOPLAM GELİR: {total.ToString("N2")}", boldTableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(toplam);

                var aaa = db.AcilisBakiyes.Where(x => x.BinaID == BinaID).FirstOrDefault();
                var acilisbakiyesi1 = aaa.ToplamTutar;

                var bbb = db.Makbuzs.Where(x => x.MakbuzTarihi <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.MabuzTutar);
                if(bbb == null)
                {
                    bbb = 0;
                }

                var ccc = db.Tahsilats.Where(x => x.TahsilatTarih <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.TahsilatTutar);
                if (ccc == null)
                {
                    ccc = 0;
                }

                var gelir1 = bbb + ccc;

                var gider1 = db.Giders.Where(x => x.GiderTarih <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.GiderTutar);

                if (gider1 == null)
                {
                    gider1 = 0;
                }

                var toplam1 = (acilisbakiyesi1 + gelir1) - gider1;

                Paragraph Kasa = new Paragraph($"KASA: {toplam1.Value.ToString("N2")}", boldTableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(Kasa);


                document.Close();

                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=GelirRaporu.pdf");
                return File(workStream, "application/pdf");
            }
        }


        public ActionResult GiderPDF(DateTime? ilk, DateTime? son)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            var giderler = db.GiderViews.Where(x => x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").OrderBy(x => x.GiderID).ToList();

            

            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 0, 30);
                MemoryStream workStream = new MemoryStream();
                PdfWriter.GetInstance(document, workStream).CloseStream = false;
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // Türkçe karakter desteği için Unicode font ekliyoruz
                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                // Font boyutlarını ayarla
                Font titleFont = new Font(bf, 14, Font.BOLD); // Başlık fontunu küçülttüm
                Font tableFont = new Font(bf, 8); // İçerik fontlarını küçülttüm
                Font boldTableFont = new Font(bf, 9, Font.BOLD); // Tablo başlıklarını küçülttüm

                // Sağ üst köşeye tarih ve saat ekleme
                string currentDateTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                Paragraph dateTimeParagraph = new Paragraph($"ÇIKTI TARİHİ: {currentDateTime}", tableFont)
                {
                    Alignment = Element.ALIGN_RIGHT,
                    SpacingBefore = -10f // Tarih kısmını yukarı çekmek için
                };
                document.Add(dateTimeParagraph);

                // Başlık
                Paragraph title = new Paragraph("Gider Raporu", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(title);

                // Tarih aralığı
                Paragraph dateRange = new Paragraph($"Tarih Aralığı: {ilk?.ToString("dd/MM/yyyy")} - {son?.ToString("dd/MM/yyyy")}", tableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(dateRange);

                document.Add(new Paragraph("\n"));

                // Giderler Tablosu
                PdfPTable giderTable = new PdfPTable(5); // 5 sütun olmalı
                giderTable.WidthPercentage = 100;
                giderTable.SetWidths(new float[] { 7f, 15f, 50f, 10f, 13f }); // "Gider No", "Gider Türü" ve "Tutar" sütunlarını küçülttüm

                // Başlıklar
                string[] giderHeader = { "No", "Türü", "Gider Açıklama", "Tarih", "Tutar" };
                foreach (string header in giderHeader)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, boldTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };
                    giderTable.AddCell(cell);
                }

                // Veriler
                decimal giderToplam = 0;
                foreach (var item in giderler)
                {
                    giderTable.AddCell(new PdfPCell(new Phrase(item.GiderNo.ToString(), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    giderTable.AddCell(new PdfPCell(new Phrase(item.GiderTuruAdi, tableFont)) { HorizontalAlignment = Element.ALIGN_LEFT });
                    giderTable.AddCell(new PdfPCell(new Phrase(item.GiderAciklama, tableFont)) { HorizontalAlignment = Element.ALIGN_LEFT });
                    giderTable.AddCell(new PdfPCell(new Phrase(item.GiderTarih.Value.ToString("dd/MM/yyyy"), tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    giderTable.AddCell(new PdfPCell(new Phrase(item.GiderTutar.Value.ToString("N2"), tableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                    giderToplam += (decimal)item.GiderTutar;
                }

                // Toplam Satırı
                PdfPCell giderToplamCell = new PdfPCell(new Phrase("Toplam", boldTableFont))
                {
                    Colspan = 4, // Toplam satırı 4 sütunu kapsıyor
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };
                giderTable.AddCell(giderToplamCell);
                giderTable.AddCell(new PdfPCell(new Phrase(giderToplam.ToString("N2"), boldTableFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });

                document.Add(giderTable);

                var aaa = db.AcilisBakiyes.Where(x => x.BinaID == BinaID).FirstOrDefault();
                var acilisbakiyesi1 = aaa.ToplamTutar;

                var bbb = db.Makbuzs.Where(x => x.MakbuzTarihi <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.MabuzTutar);
                if (bbb == null)
                {
                    bbb = 0;
                }

                var ccc = db.Tahsilats.Where(x => x.TahsilatTarih <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.TahsilatTutar);
                if (ccc == null)
                {
                    ccc = 0;
                }

                var gelir1 = bbb + ccc;

                var gider1 = db.Giders.Where(x => x.GiderTarih <= son && x.Durum == "A" && x.BinaID == BinaID).Sum(x => x.GiderTutar);

                if (gider1 == null)
                {
                    gider1 = 0;
                }

                var toplam1 = (acilisbakiyesi1 + gelir1) - gider1;

                Paragraph Kasa = new Paragraph($"KASA: {toplam1.Value.ToString("N2")}", boldTableFont)
                {
                    Alignment = Element.ALIGN_CENTER
                };
                document.Add(Kasa);


                document.Close();


                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=GiderRaporu.pdf");
                return File(workStream, "application/pdf");
            }
        }

        public ActionResult DetayliGelirGider(int? ay, int? yil)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DetayliGelirGider";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            

            if (ay != null && yil != null)
            {
                ViewBag.Ay = ay;
                ViewBag.Yil = yil;
                string ayadi;

                switch (ay)
                {
                    case 1:
                        ayadi = "Ocak";
                        break;
                    case 2:
                        ayadi = "Şubat";
                        break;
                    case 3:
                        ayadi = "Mart";
                        break;
                    case 4:
                        ayadi = "Nisan";
                        break;
                    case 5:
                        ayadi = "Mayıs";
                        break;
                    case 6:
                        ayadi = "Haziran";
                        break;
                    case 7:
                        ayadi = "Temmuz";
                        break;
                    case 8:
                        ayadi = "Ağustos";
                        break;
                    case 9:
                        ayadi = "Eylül";
                        break;
                    case 10:
                        ayadi = "Ekim";
                        break;
                    case 11:
                        ayadi = "Kasım";
                        break;
                    case 12:
                        ayadi = "Aralık";
                        break;
                    default:
                        ayadi = "Geçersiz Ay";
                        break;
                }
                ViewBag.Yazi = ayadi + " - " + yil + " Dönemi Gelir Gider Raporu";

                var elektrik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 1 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var su = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 2 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var bakimonarim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 3 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var maas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 4 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var ssk = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 5 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var demirbas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 6 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yonetim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 7 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var temizlik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 8 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var diger = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 9 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yakitisinma = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 10 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var toplamgider = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var eskikasa = db.Kasas.Where(x => x.BinaID == BinaID && x.AyKodu == ay && x.KasaYil == yil).FirstOrDefault();

                var makbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzTarihi.Value.Month == ay && x.MakbuzTarihi.Value.Year == yil && x.Durum == "A").ToList();
                var tahsilatlar = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih.Value.Month == ay && x.TahsilatTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

                decimal aidattoplam = 0;
                decimal ektoplam = 0;
                foreach (var item in makbuzlar)
                {
                    var aidatmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "A" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;

                    aidattoplam += aidatmakbuz;

                }
                foreach (var item in makbuzlar)
                {
                    var ekmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "E" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;
                    ektoplam += ekmakbuz;
                }



                decimal? eskikasatoplami = 0;
                decimal? eskikasatoplamiek = 0;
                decimal? eskikasatoplamiaidat = 0;
                if (eskikasa != null)
                {
                    eskikasatoplami = eskikasa.KasaToplam;
                    eskikasatoplamiek = eskikasa.KasaEk;
                    eskikasatoplamiaidat = eskikasa.KasaAidat;
                }

                var yenikasa = (aidattoplam + ektoplam + eskikasatoplami + tahsilatlar) - toplamgider;

                var yenikasaek = (eskikasatoplamiek + ektoplam + tahsilatlar) - demirbas;
                var yenikasaaidat = yenikasa - yenikasaek;

                ViewBag.yenikasa = yenikasa;
                ViewBag.yenikasaek = yenikasaek;
                ViewBag.yenikasaaidat = yenikasaaidat;


                ViewBag.aidat = aidattoplam;
                ViewBag.ek = ektoplam + tahsilatlar;
                ViewBag.gelirtoplam = aidattoplam + ektoplam + tahsilatlar;

                ViewBag.eskikasa = eskikasatoplami;
                ViewBag.eskikasaek = eskikasatoplamiek;
                ViewBag.eskikasaaidat = eskikasatoplamiaidat;



                ViewBag.elektrik = elektrik;
                ViewBag.su = su;
                ViewBag.bakimonarim = bakimonarim;
                ViewBag.maas = maas;
                ViewBag.ssk = ssk;
                ViewBag.demirbas = demirbas;
                ViewBag.yonetim = yonetim;
                ViewBag.temizlik = temizlik;
                ViewBag.diger = diger;
                ViewBag.yakitisinma = yakitisinma;
                ViewBag.toplamgider = toplamgider;



            }
            else
            {
                return View();

            }



            return View();

        }

        public ActionResult DetayliGelirGiderPDF(int? ay, int? yil)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            string kullaniciAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["KullaniciAdi"]);
            string adSoyad = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["AdSoyad"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);
            // ... (Veri çekme işlemleri)
            ViewBag.Ay = ay;
            ViewBag.Yil = yil;
            string ayadi;

            switch (ay)
            {
                case 1:
                    ayadi = "Ocak";
                    break;
                case 2:
                    ayadi = "Şubat";
                    break;
                case 3:
                    ayadi = "Mart";
                    break;
                case 4:
                    ayadi = "Nisan";
                    break;
                case 5:
                    ayadi = "Mayıs";
                    break;
                case 6:
                    ayadi = "Haziran";
                    break;
                case 7:
                    ayadi = "Temmuz";
                    break;
                case 8:
                    ayadi = "Ağustos";
                    break;
                case 9:
                    ayadi = "Eylül";
                    break;
                case 10:
                    ayadi = "Ekim";
                    break;
                case 11:
                    ayadi = "Kasım";
                    break;
                case 12:
                    ayadi = "Aralık";
                    break;
                default:
                    ayadi = "Geçersiz Ay";
                    break;
            }
            ViewBag.Yazi = ayadi + " - " + yil + " Dönemi Gelir Gider Raporu";

            var elektrik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 1 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var su = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 2 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var bakimonarim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 3 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var maas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 4 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var ssk = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 5 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var demirbas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 6 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var yonetim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 7 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var temizlik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 8 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var diger = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 9 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var yakitisinma = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 10 && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var toplamgider = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTarih.Value.Month == ay && x.GiderTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            var eskikasa = db.Kasas.Where(x => x.BinaID == BinaID && x.AyKodu == ay && x.KasaYil == yil).FirstOrDefault();

            var makbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzTarihi.Value.Month == ay && x.MakbuzTarihi.Value.Year == yil && x.Durum == "A").ToList();
            var tahsilatlar = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih.Value.Month == ay && x.TahsilatTarih.Value.Year == yil && x.Durum == "A").Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

            decimal aidattoplam = 0;
            decimal ektoplam = 0;
            foreach (var item in makbuzlar)
            {
                var aidatmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "A" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;

                aidattoplam += aidatmakbuz;

            }
            foreach (var item in makbuzlar)
            {
                var ekmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "E" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;
                ektoplam += ekmakbuz;
            }



            decimal? eskikasatoplami = 0;
            decimal? eskikasatoplamiek = 0;
            decimal? eskikasatoplamiaidat = 0;
            if (eskikasa != null)
            {
                eskikasatoplami = eskikasa.KasaToplam;
                eskikasatoplamiek = eskikasa.KasaEk;
                eskikasatoplamiaidat = eskikasa.KasaAidat;
            }

            var yenikasa = (aidattoplam + ektoplam + eskikasatoplami + tahsilatlar) - toplamgider;

            var yenikasaek = (eskikasatoplamiek + ektoplam + tahsilatlar) - demirbas;
            var yenikasaaidat = yenikasa - yenikasaek;

            ViewBag.yenikasa = yenikasa;
            ViewBag.yenikasaek = yenikasaek;
            ViewBag.yenikasaaidat = yenikasaaidat;


            ViewBag.aidat = aidattoplam;
            ViewBag.ek = ektoplam + tahsilatlar;
            ViewBag.gelirtoplam = aidattoplam + ektoplam + tahsilatlar;

            ViewBag.eskikasa = eskikasatoplami;
            ViewBag.eskikasaek = eskikasatoplamiek;
            ViewBag.eskikasaaidat = eskikasatoplamiaidat;



            ViewBag.elektrik = elektrik;
            ViewBag.su = su;
            ViewBag.bakimonarim = bakimonarim;
            ViewBag.maas = maas;
            ViewBag.ssk = ssk;
            ViewBag.demirbas = demirbas;
            ViewBag.yonetim = yonetim;
            ViewBag.temizlik = temizlik;
            ViewBag.diger = diger;
            ViewBag.yakitisinma = yakitisinma;
            ViewBag.toplamgider = toplamgider;

            // PDF belgesi oluşturma
            Document document = new Document();
            MemoryStream workStream = new MemoryStream();
            PdfWriter.GetInstance(document, workStream).CloseStream = false;

            using (MemoryStream stream = new MemoryStream())
            {
                

                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font arialFont = new Font(baseFont, 14); // Font boyutu 14


                string arialFontPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont2 = BaseFont.CreateFont(arialFontPath2, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                
                Font arialFontBold = new Font(baseFont2, 16, Font.BOLD); // Kalın ve 16 punto

                BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
                Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
                Font tableHeaderFont = new Font(bfArialTurkish, 10, Font.BOLD);
                Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);

                PdfWriter.GetInstance(document, stream);
                document.Open();

                

                // Giderler

                // Load logo image
                string logoPath = Server.MapPath("~/Content/Admin/assets/img/binamakbuzlogo.png");
                iTextSharp.text.Image logo = iTextSharp.text.Image.GetInstance(logoPath);
                logo.ScaleAbsolute(100f, 100f); // Adjust size as needed
                logo.Alignment = iTextSharp.text.Image.ALIGN_LEFT;


                // Create a table to organize the header layout (3 columns: left for logo, middle for building info, right for date/receipt number)
                PdfPTable headerTable = new PdfPTable(3);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 20, 50, 30 }); // Adjust the column widths as needed

                // Left Column: Logo
                PdfPCell logoCell = new PdfPCell(logo);
                logoCell.Border = PdfPCell.NO_BORDER;
                headerTable.AddCell(logoCell);

                // Middle Column: Building Information
                PdfPCell buildingInfoCell = new PdfPCell();
                buildingInfoCell.Border = PdfPCell.NO_BORDER;
                buildingInfoCell.AddElement(new Paragraph("" + binaAdi.ToString().ToUpper(), titleFont));
                buildingInfoCell.AddElement(new Paragraph("" + binaAdres, subTitleFont));
                headerTable.AddCell(buildingInfoCell);

                // Right Column: Date and Receipt Number
                PdfPCell receiptInfoCell = new PdfPCell();
                receiptInfoCell.Border = PdfPCell.NO_BORDER;
                receiptInfoCell.AddElement(new Paragraph("Tarih: " + DateTime.Now.ToString("dd/MM/yyyy"), subTitleFont));
              
                receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                headerTable.AddCell(receiptInfoCell);

                document.Add(headerTable);

                // Başlık
                Paragraph titleParagraph = new Paragraph(ViewBag.Yazi, arialFontBold);
                titleParagraph.Alignment = Element.ALIGN_CENTER; // Ortala
                document.Add(titleParagraph);
                document.Add(new Paragraph(" ")); // Boşluk

                document.Add(new Paragraph($"Elektrik: {ViewBag.elektrik.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Su: {ViewBag.su.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Yakıt - Isınma Gideri: {ViewBag.yakitisinma.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Yönetim Gideri: {ViewBag.yonetim.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Temizlik Gideri: {ViewBag.temizlik.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Bakım Onarım Gideri: {ViewBag.bakimonarim.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Demirbaş Gideri: {ViewBag.demirbas.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Görevli Maaş Gideri: {ViewBag.maas.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Görevli SSK Gideri: {ViewBag.ssk.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Diğer Gider: {ViewBag.diger.ToString("N2")} TL", arialFont));

                // Toplam Gideri ortala
                Paragraph toplamGiderParagraph = new Paragraph($"Toplam Gider: {ViewBag.toplamgider.ToString("N2")} TL", arialFontBold);
                toplamGiderParagraph.Alignment = Element.ALIGN_LEFT; // Ortala
                document.Add(toplamGiderParagraph);
                document.Add(new Paragraph(" ")); // Boşluk

                // Gelirler
                document.Add(new Paragraph($"Aidat Geliri: {ViewBag.aidat.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Ek Gelir: {ViewBag.ek.ToString("N2")} TL", arialFont));

                // Toplam Geliri ortala
                Paragraph toplamGelirParagraph = new Paragraph($"Toplam Gelir: {ViewBag.gelirtoplam.ToString("N2")} TL", arialFontBold);
                toplamGelirParagraph.Alignment = Element.ALIGN_LEFT; // Ortala
                document.Add(toplamGelirParagraph);
                document.Add(new Paragraph(" ")); // Boşluk

                // Kasa Devir
                Paragraph kasaDevirParagraph = new Paragraph($"Kasa Devir: {ViewBag.eskikasa.ToString("N2")} TL", arialFontBold);
                kasaDevirParagraph.Alignment = Element.ALIGN_LEFT; // Ortala
                document.Add(kasaDevirParagraph);

                // Kasa Devir ayrıntıları ortala
                Paragraph eskikasaAyrintiParagraph = new Paragraph($"(Aidat: {ViewBag.eskikasaaidat.ToString("N2")} TL | Ek: {ViewBag.eskikasaek.ToString("N2")} TL)", arialFont);
                eskikasaAyrintiParagraph.Alignment = Element.ALIGN_LEFT; // Ortala
                document.Add(eskikasaAyrintiParagraph);

                document.Add(new Paragraph(" ")); // Boşluk

                // Toplam Kasayı ortala
                Paragraph toplamKasaParagraph = new Paragraph($"Toplam Kasa: {ViewBag.yenikasa.ToString("N2")} TL", arialFontBold);
                toplamKasaParagraph.Alignment = Element.ALIGN_CENTER; // Ortala
                document.Add(toplamKasaParagraph);

                // Toplam Kasa ayrıntıları ortala
                Paragraph yenikasaAyrintiParagraph = new Paragraph($"(Aidat: {ViewBag.yenikasaaidat.ToString("N2")} TL | Ek: {ViewBag.yenikasaek.ToString("N2")} TL)", arialFont);
                yenikasaAyrintiParagraph.Alignment = Element.ALIGN_CENTER; // Ortala
                document.Add(yenikasaAyrintiParagraph);

                document.Close();

                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=DetayliGelirGiderRaporu.pdf");
                return File(workStream, "application/pdf");
            }

        }

        public ActionResult DenetciRaporu(DateTime? ilk, DateTime? son)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DenetciRaporu";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            if (ilk != null && son != null)
            {
                ViewBag.tarihdeger1 = ilk.Value.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = son.Value.ToString("yyyy-MM-dd");
                if (ilk.HasValue)
                {
                    ilk = ilk.Value.Date; // Saat kısmını 00:00:00 yapar
                }

                // tarih2'nin saatini 23:59:59 olarak ayarla
                if (son.HasValue)
                {
                    son = son.Value.Date.AddDays(1).AddTicks(-1); // Saat kısmını 23:59:59 yapar
                }

                int devirtarih = Convert.ToInt32(ilk.Value.Month);
                int devirtarihyil = ilk.Value.Year;

                var kasa = db.Kasas.Where(x => x.BinaID == BinaID && x.AyKodu == devirtarih && x.KasaYil == devirtarihyil).FirstOrDefault();

                if (kasa != null)
                {
                    ViewBag.KasaToplam = kasa.KasaToplam;
                    ViewBag.KasaAidat = kasa.KasaAidat;
                    ViewBag.KasaEk = kasa.KasaEk;
                }
                else
                {
                    ViewBag.KasaToplam = 0;
                    ViewBag.KasaAidat = 0;
                    ViewBag.KasaEk =0;
                }


                ViewBag.Yazi = ilk + " - " + son + " tarihleri arası gelir gider";
                var elektrik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 1 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var su = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 2 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var bakimonarim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 3 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var maas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 4 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var ssk = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 5 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var demirbas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 6 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yonetim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 7 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var temizlik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 8 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var diger = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 9 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yakitisinma = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 10 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var toplamgider = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;

                var makbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= ilk && x.MakbuzTarihi <= son && x.Durum == "A").ToList();
                var tahsilatlar = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

                decimal aidattoplam = 0;
                decimal ektoplam = 0;
                foreach (var item in makbuzlar)
                {
                    var aidatmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "A" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;

                    aidattoplam += aidatmakbuz;

                }
                foreach (var item in makbuzlar)
                {
                    var ekmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "E" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;
                    ektoplam += ekmakbuz;
                }

                var toplamgelir = aidattoplam + ektoplam + tahsilatlar;
                
                ViewBag.aidat = aidattoplam;
                ViewBag.ek = ektoplam + tahsilatlar;
                ViewBag.toplamgelir = toplamgelir;
                ViewBag.elektrik = elektrik;
                ViewBag.su = su;
                ViewBag.bakimonarim = bakimonarim;
                ViewBag.maas = maas;
                ViewBag.ssk = ssk;
                ViewBag.demirbas = demirbas;
                ViewBag.yonetim = yonetim;
                ViewBag.temizlik = temizlik;
                ViewBag.diger = diger;
                ViewBag.yakitisinma = yakitisinma;
                ViewBag.toplamgider = toplamgider;

                ViewBag.toplamkasa = toplamgelir + ViewBag.KasaToplam - ViewBag.toplamgider;
                ViewBag.toplamkasaek = ViewBag.ek + ViewBag.KasaEk - ViewBag.demirbas;
                ViewBag.toplamkasaaidat = ViewBag.toplamkasa - ViewBag.toplamkasaek;
            }
            else
            {
                ViewBag.tarihdeger1 = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = DateTime.Now.ToString("yyyy-MM-dd");
            }
            return View();
        }

        public ActionResult DenetciRaporuPDF(DateTime? ilk, DateTime? son)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            string kullaniciAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["KullaniciAdi"]);
            string adSoyad = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["AdSoyad"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);


            ViewBag.Baslik = binaAdi + " Denetim Kurulu Raporu";

            ViewBag.Paragraf = binaAdi + " yönetiminin " + ilk.Value.ToShortDateString() + " - " + son.Value.ToShortDateString() + " tarihleri arasındaki döneme ait çalışmaları ve faaliyetleri denetlenmiş, Gelir ve Gider dökümanı aşağıdaki gibidir.";
            ViewBag.Baslik2 = "Mali Yönden Yapılan İnceleme";
            ViewBag.Paragraf2 = "1-Belirlenen Döneme ait gelir ve giderler aşağıdaki gibi tespit edilmiştir.";
            ViewBag.Paragraf3 = "2-Gelir ve Gider arasındaki farkın aşağıdaki gibi olduğu tespit edilmiştir.";

            if (ilk != null && son != null)
            {
                ViewBag.tarihdeger1 = ilk.Value.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = son.Value.ToString("yyyy-MM-dd");
                if (ilk.HasValue)
                {
                    ilk = ilk.Value.Date; // Saat kısmını 00:00:00 yapar
                }

                // tarih2'nin saatini 23:59:59 olarak ayarla
                if (son.HasValue)
                {
                    son = son.Value.Date.AddDays(1).AddTicks(-1); // Saat kısmını 23:59:59 yapar
                }

                int devirtarih = Convert.ToInt32(ilk.Value.Month);
                int devirtarihyil = ilk.Value.Year;


                var kasa = db.Kasas.Where(x => x.BinaID == BinaID && x.AyKodu == devirtarih && x.KasaYil == devirtarihyil).FirstOrDefault();

                if (kasa != null)
                {
                    ViewBag.KasaToplam = kasa.KasaToplam;
                    ViewBag.KasaAidat = kasa.KasaAidat;
                    ViewBag.KasaEk = kasa.KasaEk;
                }
                else
                {
                    ViewBag.KasaToplam = 0;
                    ViewBag.KasaAidat = 0;
                    ViewBag.KasaEk = 0;
                }

                


                var elektrik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 1 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var su = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 2 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var bakimonarim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 3 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var maas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 4 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var ssk = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 5 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var demirbas = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 6 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yonetim = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 7 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var temizlik = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 8 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var diger = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 9 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var yakitisinma = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTuruID == 10 && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
                var toplamgider = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;

                var makbuzlar = db.Makbuzs.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= ilk && x.MakbuzTarihi <= son && x.Durum == "A").ToList();
                var tahsilatlar = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

                decimal aidattoplam = 0;
                decimal ektoplam = 0;
                foreach (var item in makbuzlar)
                {
                    var aidatmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "A" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;

                    aidattoplam += aidatmakbuz;

                }
                foreach (var item in makbuzlar)
                {
                    var ekmakbuz = db.MakbuzSatirs.Where(x => x.MakbuzID == item.MakbuzID && x.EkMiAidatMi == "E" && x.Durum == "A").Sum(x => (decimal?)x.Tutar) ?? 0;
                    ektoplam += ekmakbuz;
                }

                var toplamgelir = aidattoplam + ektoplam + tahsilatlar;

                ViewBag.aidat = aidattoplam;
                ViewBag.ek = ektoplam + tahsilatlar;
                ViewBag.toplamgelir = toplamgelir;
                ViewBag.elektrik = elektrik;
                ViewBag.su = su;
                ViewBag.bakimonarim = bakimonarim;
                ViewBag.maas = maas;
                ViewBag.ssk = ssk;
                ViewBag.demirbas = demirbas;
                ViewBag.yonetim = yonetim;
                ViewBag.temizlik = temizlik;
                ViewBag.diger = diger;
                ViewBag.yakitisinma = yakitisinma;
                ViewBag.toplamgider = toplamgider;

                ViewBag.Mevcut = toplamgelir - toplamgider;

                ViewBag.toplamkasa = toplamgelir + ViewBag.KasaToplam - ViewBag.toplamgider;
                ViewBag.toplamkasaek = ViewBag.ek + ViewBag.KasaEk - ViewBag.demirbas;
                ViewBag.toplamkasaaidat = ViewBag.toplamkasa - ViewBag.toplamkasaek;
            }


            // PDF belgesi oluşturma
            Document document = new Document();
            MemoryStream workStream = new MemoryStream();
            PdfWriter.GetInstance(document, workStream).CloseStream = false;

            using (MemoryStream stream = new MemoryStream())
            {


                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font arialFont = new Font(baseFont, 14); // Font boyutu 14


                string arialFontPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont2 = BaseFont.CreateFont(arialFontPath2, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font arialFontBold = new Font(baseFont2, 16, Font.BOLD); // Kalın ve 16 punto

                BaseFont bfArialTurkish = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bfArialTurkish, 18, Font.BOLD);
                Font subTitleFont = new Font(bfArialTurkish, 12, Font.NORMAL);
                Font tableHeaderFont = new Font(bfArialTurkish, 10, Font.BOLD);
                Font tableFont = new Font(bfArialTurkish, 10, Font.NORMAL);

                PdfWriter.GetInstance(document, stream);
                document.Open();



             
                // Başlık
                Paragraph titleParagraph = new Paragraph(ViewBag.Baslik, arialFontBold);
                titleParagraph.Alignment = Element.ALIGN_CENTER; // Ortala
                document.Add(titleParagraph);
                document.Add(new Paragraph(" ")); // Boşluk


                document.Add(new Paragraph(ViewBag.Paragraf, arialFont));
                document.Add(new Paragraph(" ")); // Boşluk
                document.Add(new Paragraph(" ")); // Boşluk

                Paragraph titleParagraph2 = new Paragraph(ViewBag.Baslik2, arialFontBold);
                titleParagraph2.Alignment = Element.ALIGN_CENTER; // Ortala
                document.Add(titleParagraph2);
                document.Add(new Paragraph(" ")); // Boşluk

                document.Add(new Paragraph(ViewBag.Paragraf2, arialFont));
                document.Add(new Paragraph(" ")); // Boşluk
                document.Add(new Paragraph("GİDERLER", arialFontBold));

                document.Add(new Paragraph($"Elektrik: {ViewBag.elektrik.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Su: {ViewBag.su.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Yakıt - Isınma Gideri: {ViewBag.yakitisinma.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Yönetim Gideri: {ViewBag.yonetim.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Temizlik Gideri: {ViewBag.temizlik.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Bakım Onarım Gideri: {ViewBag.bakimonarim.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Demirbaş Gideri: {ViewBag.demirbas.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Görevli Maaş Gideri: {ViewBag.maas.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Görevli SSK Gideri: {ViewBag.ssk.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Diğer Gider: {ViewBag.diger.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"TOPLAM: {ViewBag.toplamgider.ToString("N2")} TL", arialFont));

                // Gelirler
                document.Add(new Paragraph(" ")); // Boşluk
                document.Add(new Paragraph("GELİRLER", arialFontBold));
                document.Add(new Paragraph($"Aidat Geliri: {ViewBag.aidat.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Ek Gelir: {ViewBag.ek.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph(" ")); // Boşluk
                document.Add(new Paragraph(ViewBag.Paragraf3, arialFont));
                document.Add(new Paragraph(" ")); // Boşluk
                document.Add(new Paragraph($"GELİR: {ViewBag.toplamgelir.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"GİDER: {ViewBag.toplamgider.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph(" ")); // Boşluk
                Paragraph paragraph = new Paragraph();
                paragraph.Add(new Chunk($"MEVCUT: {ViewBag.Mevcut.ToString("N2")} TL (BİR ÖNCEKİ AYDAN {ViewBag.KasaToplam.ToString("N2")} TL KALAN PARA İLE ", arialFont));

                // Kalın yazılacak bölüm
                paragraph.Add(new Chunk($"{ViewBag.toplamkasa.ToString("N2")} TL", arialFontBold));

                // Paragrafın kalan kısmı
                paragraph.Add(new Chunk(" )", arialFont));

                // PDF'ye paragrafı ekleme
                document.Add(paragraph);


                document.Close();

                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=DenetciRaporu.pdf");
                return File(workStream, "application/pdf");
            }

        }

        public ActionResult TureGoreGiderler(DateTime? ilk, DateTime? son, int? GiderTuruID)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "TureGoreGiderler";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.GiderTuru = db.GiderTurus.OrderBy(x => x.GiderTuruAdi).ToList();
            if (ilk != null && son != null && GiderTuruID != null)
            {
                ViewBag.tarihdeger1 = ilk.Value.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = son.Value.ToString("yyyy-MM-dd");
                ViewBag.giderturudeger = GiderTuruID;
                if (ilk.HasValue)
                {
                    ilk = ilk.Value.Date; // Saat kısmını 00:00:00 yapar
                }

                // tarih2'nin saatini 23:59:59 olarak ayarla
                if (son.HasValue)
                {
                    son = son.Value.Date.AddDays(1).AddTicks(-1); // Saat kısmını 23:59:59 yapar
                }

                ViewBag.Gider = db.GiderViews.Where(x => x.GiderTuruID == GiderTuruID && x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum=="A").ToList();
                ViewBag.Toplam = db.GiderViews.Where(x => x.GiderTuruID == GiderTuruID && x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum == "A").Sum(x => (decimal?)x.GiderTutar) ?? 0;
            }
            else
            {
                ViewBag.tarihdeger1 = DateTime.Now.ToString("yyyy-MM-dd");
                ViewBag.tarihdeger2 = DateTime.Now.ToString("yyyy-MM-dd");
            }
            return View();
        }

        public ActionResult DevirBakiyeleri()
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {

                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DevirBakiyeleri";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            ViewBag.Bakiyeler = db.Kasas.Where(x=> x.BinaID == BinaID).OrderBy(x => x.KasaID).ToList();
            return View();
        }
    }
}