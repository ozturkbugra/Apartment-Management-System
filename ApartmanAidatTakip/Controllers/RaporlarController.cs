using ApartmanAidatTakip.Models;
using DocumentFormat.OpenXml.Office2021.DocumentTasks;
using iTextSharp.text;
using iTextSharp.text.pdf;
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
                    makbuzGelirTable.AddCell(new PdfPCell(new Phrase(item.DaireNo.ToString() + " ( " + item.AdSoyad.ToString() + " )", tableFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
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

        public ActionResult DetayliGelirGider(int? ay, int? yil, bool yillik = false)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }
            Session["Aktif"] = "DetayliGelirGider";
            Sabit();
            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.Yillik = yillik;

            if (yil != null && (ay != null || yillik))
            {
                ViewBag.Ay = ay;
                ViewBag.Yil = yil;

                // --- 1. BAŞLIK AYARI ---
                string raporBasligi = "";
                if (yillik)
                    raporBasligi = yil + " Yılı Genel (Kümülatif) Gelir Gider Raporu";
                else
                {
                    string ayadi = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(ay.Value);
                    raporBasligi = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ayadi) + " - " + yil + " Dönemi Gelir Gider Raporu";
                }
                ViewBag.Yazi = raporBasligi;

                // --- 2. GİDERLER (HIZLANDIRILDI - TEK SORGU) ---
                // Veritabanına 10 kere gitmek yerine 1 kere gidip listeyi RAM'e alıyoruz.
                var giderQuery = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.GiderTarih.Value.Year == yil && x.Durum == "A");

                if (!yillik)
                    giderQuery = giderQuery.Where(x => x.GiderTarih.Value.Month == ay);

                // Sadece lazım olan kolonları çekiyoruz (Select kullanımı performansı artırır)
                var giderListesi = giderQuery.Select(x => new { x.GiderTuruID, x.GiderTutar }).ToList();

                // Hesaplamalar RAM üzerinde yapılıyor (Çok hızlı)
                ViewBag.elektrik = giderListesi.Where(x => x.GiderTuruID == 1).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.su = giderListesi.Where(x => x.GiderTuruID == 2).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.bakimonarim = giderListesi.Where(x => x.GiderTuruID == 3).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.maas = giderListesi.Where(x => x.GiderTuruID == 4).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.ssk = giderListesi.Where(x => x.GiderTuruID == 5).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.demirbas = giderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.yonetim = giderListesi.Where(x => x.GiderTuruID == 7).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.temizlik = giderListesi.Where(x => x.GiderTuruID == 8).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.diger = giderListesi.Where(x => x.GiderTuruID == 9).Sum(x => x.GiderTutar) ?? 0;
                ViewBag.yakitisinma = giderListesi.Where(x => x.GiderTuruID == 10).Sum(x => x.GiderTutar) ?? 0;

                decimal toplamgider = giderListesi.Sum(x => x.GiderTutar) ?? 0;
                decimal demirbasgider = giderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;


                // --- 3. GELİRLER (HIZLANDIRILDI - JOIN KULLANIMI) ---
                var makbuzQuery = db.Makbuzs.AsNoTracking().Where(x => x.BinaID == BinaID && x.MakbuzTarihi.Value.Year == yil && x.Durum == "A");
                var tahsilatQuery = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.TahsilatTarih.Value.Year == yil && x.Durum == "A");

                if (!yillik)
                {
                    makbuzQuery = makbuzQuery.Where(x => x.MakbuzTarihi.Value.Month == ay);
                    tahsilatQuery = tahsilatQuery.Where(x => x.TahsilatTarih.Value.Month == ay);
                }

                // Tahsilatları çek
                var tahsilatListesi = tahsilatQuery.Select(x => new { x.DemirbasMi, x.TahsilatTutar }).ToList();
                decimal tahsilatlardemirbas = tahsilatListesi.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;
                decimal tahsilatlaraidat = tahsilatListesi.Where(x => x.DemirbasMi == false).Sum(x => x.TahsilatTutar) ?? 0;

                // Makbuzları Hızlı Çek (N+1 Çözümü)
                decimal aidatmakbuztoplam = 0;
                decimal ekmakbuztoplam = 0;

                // İlgili makbuzların ID'lerini al
                var makbuzIdListesi = makbuzQuery.Select(x => x.MakbuzID).ToList();

                if (makbuzIdListesi.Any())
                {
                    // TEK SORGU ile tüm detayları al
                    var detaylar = db.MakbuzSatirs.AsNoTracking()
                                     .Where(x => makbuzIdListesi.Contains(x.MakbuzID ?? 0) && x.Durum == "A")
                                     .Select(x => new { x.EkMiAidatMi, x.Tutar })
                                     .ToList();

                    aidatmakbuztoplam = detaylar.Where(x => x.EkMiAidatMi == "A").Sum(x => x.Tutar) ?? 0;
                    ekmakbuztoplam = detaylar.Where(x => x.EkMiAidatMi == "E").Sum(x => x.Tutar) ?? 0;
                }

                decimal donemGelirAidat = aidatmakbuztoplam + tahsilatlaraidat;
                decimal donemGelirEk = ekmakbuztoplam + tahsilatlardemirbas;
                decimal donemToplamGelir = donemGelirAidat + donemGelirEk;


                // --- 4. DEVİR / AÇILIŞ MANTIĞI (SENİN İSTEDİĞİN YAPI) ---

                // Önce AcilisBakiye'yi hazırda tut (Yedek Plan)
                var acilis = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);

                // Kasa Tablosuna Bak (Asıl Plan)
                // Eğer yıllık ise: Ocak ayının kasası o yılın başıdır.
                // Eğer aylık ise: O ayın kasası o ayın başıdır.
                int sorguAy = yillik ? 1 : ay.Value;
                var devirKasa = db.Kasas.FirstOrDefault(x => x.BinaID == BinaID && x.AyKodu == sorguAy && x.KasaYil == yil);

                decimal devirToplam = 0;
                decimal devirEk = 0;
                decimal devirAidat = 0;

                if (devirKasa != null)
                {
                    // SENARYO A: Kasa bulundu. O kayıttaki değer "Devir"dir.
                    devirToplam = devirKasa.KasaToplam ?? 0;
                    devirEk = devirKasa.KasaEk ?? 0;
                    devirAidat = devirKasa.KasaAidat ?? 0;
                }
                else
                {
                    // SENARYO B: Kasa kaydı YOK. AcilisBakiye'ye git.
                    if (acilis != null)
                    {
                        devirToplam = acilis.ToplamTutar ?? 0;
                        devirEk = acilis.EkTutar ?? 0;
                        devirAidat = acilis.AidatTutar ?? 0;
                    }
                }

                // --- 5. SONUÇLARI HESAPLA ---
                // Yeni Kasa = Devir + Gelir - Gider

                var yenikasa = (devirToplam + donemToplamGelir) - toplamgider;
                var yenikasaek = (devirEk + donemGelirEk) - demirbasgider;
                var yenikasaaidat = yenikasa - yenikasaek;

                // --- VIEWBAG ATAMALARI ---
                ViewBag.yenikasa = yenikasa;
                ViewBag.yenikasaek = yenikasaek;
                ViewBag.yenikasaaidat = yenikasaaidat;

                ViewBag.aidat = donemGelirAidat;
                ViewBag.ek = donemGelirEk;
                ViewBag.gelirtoplam = donemToplamGelir;

                ViewBag.eskikasa = devirToplam; // Devir Bakiyesi
                ViewBag.eskikasaek = devirEk;
                ViewBag.eskikasaaidat = devirAidat;

                ViewBag.toplamgider = toplamgider;
            }
            else
            {
                return View();
            }

            return View();
        }

        public ActionResult DetayliGelirGiderPDF(int? ay, int? yil, bool yillik = false)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);
            string binaAdres = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdres"]);

            ViewBag.Ay = ay;
            ViewBag.Yil = yil;

            // --- BAŞLIK ---
            string raporBasligi = "";
            if (yillik)
                raporBasligi = yil + " Yılı Genel (Kümülatif) Gelir Gider Raporu";
            else
            {
                string ayadi = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(ay.Value);
                raporBasligi = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ayadi) + " - " + yil + " Dönemi Gelir Gider Raporu";
            }
            ViewBag.Yazi = raporBasligi;

            // --- GİDERLER (HIZLI) ---
            var giderQuery = db.Giders.AsNoTracking().Where(x => x.BinaID == BinaID && x.GiderTarih.Value.Year == yil && x.Durum == "A");
            if (!yillik) giderQuery = giderQuery.Where(x => x.GiderTarih.Value.Month == ay);

            var giderListesi = giderQuery.Select(x => new { x.GiderTuruID, x.GiderTutar }).ToList();

            ViewBag.elektrik = giderListesi.Where(x => x.GiderTuruID == 1).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.su = giderListesi.Where(x => x.GiderTuruID == 2).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.bakimonarim = giderListesi.Where(x => x.GiderTuruID == 3).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.maas = giderListesi.Where(x => x.GiderTuruID == 4).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.ssk = giderListesi.Where(x => x.GiderTuruID == 5).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.demirbas = giderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.yonetim = giderListesi.Where(x => x.GiderTuruID == 7).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.temizlik = giderListesi.Where(x => x.GiderTuruID == 8).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.diger = giderListesi.Where(x => x.GiderTuruID == 9).Sum(x => x.GiderTutar) ?? 0;
            ViewBag.yakitisinma = giderListesi.Where(x => x.GiderTuruID == 10).Sum(x => x.GiderTutar) ?? 0;

            decimal toplamgider = giderListesi.Sum(x => x.GiderTutar) ?? 0;
            decimal demirbasgider = giderListesi.Where(x => x.GiderTuruID == 6).Sum(x => x.GiderTutar) ?? 0;

            // --- GELİRLER (HIZLI) ---
            var makbuzQuery = db.Makbuzs.AsNoTracking().Where(x => x.BinaID == BinaID && x.MakbuzTarihi.Value.Year == yil && x.Durum == "A");
            var tahsilatQuery = db.Tahsilats.AsNoTracking().Where(x => x.BinaID == BinaID && x.TahsilatTarih.Value.Year == yil && x.Durum == "A");

            if (!yillik)
            {
                makbuzQuery = makbuzQuery.Where(x => x.MakbuzTarihi.Value.Month == ay);
                tahsilatQuery = tahsilatQuery.Where(x => x.TahsilatTarih.Value.Month == ay);
            }

            var tahsilatListesi = tahsilatQuery.Select(x => new { x.DemirbasMi, x.TahsilatTutar }).ToList();
            decimal tahsilatlardemirbas = tahsilatListesi.Where(x => x.DemirbasMi == true).Sum(x => x.TahsilatTutar) ?? 0;
            decimal tahsilatlaraidat = tahsilatListesi.Where(x => x.DemirbasMi == false).Sum(x => x.TahsilatTutar) ?? 0;

            decimal aidatmakbuztoplam = 0;
            decimal ekmakbuztoplam = 0;

            var makbuzIdListesi = makbuzQuery.Select(x => x.MakbuzID).ToList();
            if (makbuzIdListesi.Any())
            {
                var detaylar = db.MakbuzSatirs.AsNoTracking()
                                 .Where(x => makbuzIdListesi.Contains(x.MakbuzID ?? 0) && x.Durum == "A")
                                 .Select(x => new { x.EkMiAidatMi, x.Tutar })
                                 .ToList();

                aidatmakbuztoplam = detaylar.Where(x => x.EkMiAidatMi == "A").Sum(x => x.Tutar) ?? 0;
                ekmakbuztoplam = detaylar.Where(x => x.EkMiAidatMi == "E").Sum(x => x.Tutar) ?? 0;
            }

            decimal donemGelirAidat = aidatmakbuztoplam + tahsilatlaraidat;
            decimal donemGelirEk = ekmakbuztoplam + tahsilatlardemirbas;
            decimal donemToplamGelir = donemGelirAidat + donemGelirEk;

            // --- DEVİR HESABI (DOĞRU MANTIK) ---
            var acilis = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);

            int sorguAy = yillik ? 1 : ay.Value;
            var devirKasa = db.Kasas.FirstOrDefault(x => x.BinaID == BinaID && x.AyKodu == sorguAy && x.KasaYil == yil);

            decimal devirToplam = 0;
            decimal devirEk = 0;
            decimal devirAidat = 0;

            if (devirKasa != null)
            {
                devirToplam = devirKasa.KasaToplam ?? 0;
                devirEk = devirKasa.KasaEk ?? 0;
                devirAidat = devirKasa.KasaAidat ?? 0;
            }
            else
            {
                if (acilis != null)
                {
                    devirToplam = acilis.ToplamTutar ?? 0;
                    devirEk = acilis.EkTutar ?? 0;
                    devirAidat = acilis.AidatTutar ?? 0;
                }
            }

            var yenikasa = (devirToplam + donemToplamGelir) - toplamgider;
            var yenikasaek = (devirEk + donemGelirEk) - demirbasgider;
            var yenikasaaidat = yenikasa - yenikasaek;

            ViewBag.toplamgider = toplamgider;
            ViewBag.aidat = donemGelirAidat;
            ViewBag.ek = donemGelirEk;
            ViewBag.gelirtoplam = donemToplamGelir;

            ViewBag.eskikasa = devirToplam;
            ViewBag.eskikasaek = devirEk;
            ViewBag.eskikasaaidat = devirAidat;

            ViewBag.yenikasa = yenikasa;
            ViewBag.yenikasaek = yenikasaek;
            ViewBag.yenikasaaidat = yenikasaaidat;

            // --- PDF OLUŞTURMA (Standart) ---
            Document document = new Document();
            MemoryStream workStream = new MemoryStream();
            PdfWriter.GetInstance(document, workStream).CloseStream = false;

            using (MemoryStream stream = new MemoryStream())
            {
                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font arialFont = new Font(baseFont, 14);
                BaseFont baseFont2 = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font arialFontBold = new Font(baseFont2, 16, Font.BOLD);
                Font titleFont = new Font(baseFont, 18, Font.BOLD);
                Font subTitleFont = new Font(baseFont, 12, Font.NORMAL);

                PdfWriter.GetInstance(document, stream);
                document.Open();

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
                buildingInfoCell.AddElement(new Paragraph("" + binaAdi.ToString().ToUpper(), titleFont));
                buildingInfoCell.AddElement(new Paragraph("" + binaAdres, subTitleFont));
                headerTable.AddCell(buildingInfoCell);

                PdfPCell receiptInfoCell = new PdfPCell();
                receiptInfoCell.Border = PdfPCell.NO_BORDER;
                receiptInfoCell.AddElement(new Paragraph("Tarih: " + DateTime.Now.ToString("dd/MM/yyyy"), subTitleFont));
                receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                headerTable.AddCell(receiptInfoCell);

                document.Add(headerTable);

                Paragraph titleParagraph = new Paragraph(ViewBag.Yazi, arialFontBold);
                titleParagraph.Alignment = Element.ALIGN_CENTER;
                document.Add(titleParagraph);
                document.Add(new Paragraph(" "));

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

                Paragraph toplamGiderParagraph = new Paragraph($"Toplam Gider: {ViewBag.toplamgider.ToString("N2")} TL", arialFontBold);
                toplamGiderParagraph.Alignment = Element.ALIGN_LEFT;
                document.Add(toplamGiderParagraph);
                document.Add(new Paragraph(" "));

                document.Add(new Paragraph($"Aidat Geliri: {ViewBag.aidat.ToString("N2")} TL", arialFont));
                document.Add(new Paragraph($"Demirbaş Gelir: {ViewBag.ek.ToString("N2")} TL", arialFont));

                Paragraph toplamGelirParagraph = new Paragraph($"Toplam Gelir: {ViewBag.gelirtoplam.ToString("N2")} TL", arialFontBold);
                toplamGelirParagraph.Alignment = Element.ALIGN_LEFT;
                document.Add(toplamGelirParagraph);
                document.Add(new Paragraph(" "));

                string devirBasligi = yillik ? "Geçen Yıldan Devir" : "Geçen Aydan Devir";
                Paragraph kasaDevirParagraph = new Paragraph($"{devirBasligi}: {ViewBag.eskikasa.ToString("N2")} TL", arialFontBold);
                kasaDevirParagraph.Alignment = Element.ALIGN_LEFT;
                document.Add(kasaDevirParagraph);

                Paragraph eskikasaAyrintiParagraph = new Paragraph($"(Aidat: {ViewBag.eskikasaaidat.ToString("N2")} TL | Demirbaş: {ViewBag.eskikasaek.ToString("N2")} TL)", arialFont);
                eskikasaAyrintiParagraph.Alignment = Element.ALIGN_LEFT;
                document.Add(eskikasaAyrintiParagraph);
                document.Add(new Paragraph(" "));

                Paragraph toplamKasaParagraph = new Paragraph($"Toplam Kasa: {ViewBag.yenikasa.ToString("N2")} TL", arialFontBold);
                toplamKasaParagraph.Alignment = Element.ALIGN_CENTER;
                document.Add(toplamKasaParagraph);

                Paragraph yenikasaAyrintiParagraph = new Paragraph($"(Aidat: {ViewBag.yenikasaaidat.ToString("N2")} TL | Demirbaş: {ViewBag.yenikasaek.ToString("N2")} TL)", arialFont);
                yenikasaAyrintiParagraph.Alignment = Element.ALIGN_CENTER;
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
                var tahsilatlarek = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A" && x.DemirbasMi == true).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
                var tahsilatlaraidat = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A" && x.DemirbasMi == false).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

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

                var toplamgelir = aidattoplam + ektoplam + tahsilatlarek + tahsilatlaraidat;
                
                ViewBag.aidat = aidattoplam + tahsilatlaraidat;
                ViewBag.ek = ektoplam + tahsilatlarek;
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
                var tahsilatlarek = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A" && x.DemirbasMi == true).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;
                var tahsilatlaraidat = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= ilk && x.TahsilatTarih <= son && x.Durum == "A" && x.DemirbasMi == false).Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

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

                var toplamgelir = aidattoplam + ektoplam + tahsilatlarek + tahsilatlaraidat;

                ViewBag.aidat = aidattoplam + tahsilatlaraidat;
                ViewBag.ek = ektoplam + tahsilatlarek;
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







                ViewBag.Mevcut = toplamgelir - toplamgider;

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

                ViewBag.Gider = db.GiderViews.Where(x => x.GiderTuruID == GiderTuruID && x.BinaID == BinaID && x.GiderTarih >= ilk && x.GiderTarih <= son && x.Durum=="A").OrderByDescending(x=> x.GiderID).ToList();
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
            ViewBag.Bakiyeler = db.Kasas.Where(x=> x.BinaID == BinaID).OrderByDescending(x => x.KasaID).ToList();
            return View();
        }


        public ActionResult TureGoreGelirGiderTarihBazli(DateTime? ilk, DateTime? son, string raporturu)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null)
            {
                return RedirectToAction("Login", "AnaSayfa");
            }

            Session["Aktif"] = "TureGoreGelirGider";
            Sabit();

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);

            ViewBag.tarihdeger1 = ilk?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.tarihdeger2 = son?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.raporturu = raporturu;

            if (ilk != null && son != null && !string.IsNullOrEmpty(raporturu))
            {
                DateTime baslangic = ilk.Value.Date;
                DateTime bitis = son.Value.Date.AddDays(1).AddTicks(-1);

                // --- 1. LİSTELEME SORGULARI (Sadece Seçilen Aralık) ---
                var giderQuery = db.GiderViews.Where(x => x.BinaID == BinaID && x.GiderTarih >= baslangic && x.GiderTarih <= bitis && x.Durum == "A");
                var tahsilatQuery = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= baslangic && x.TahsilatTarih <= bitis && x.Durum == "A");

                string satirTuru = (raporturu == "Demirbas") ? "E" : "A";

                if (raporturu == "Demirbas")
                {
                    giderQuery = giderQuery.Where(x => x.GiderTuruID == 6);
                    tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == true);
                }
                else // Aidat
                {
                    giderQuery = giderQuery.Where(x => x.GiderTuruID != 6);
                    tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == false);
                }

                ViewBag.Gider = giderQuery.OrderBy(x => x.GiderID).ToList();
                ViewBag.TahsilatGelir = tahsilatQuery.OrderBy(x => x.TahsilatID).ToList();

                var uygunMakbuzIDleri = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == satirTuru).Select(x => x.MakbuzID).Distinct().ToList();
                ViewBag.MakbuzGelir = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= baslangic && x.MakbuzTarihi <= bitis && x.Durum == "A" && uygunMakbuzIDleri.Contains(x.MakbuzID)).OrderBy(x => x.MakbuzID).ToList();


                // --- 2. KASA MEVCUDU HESAPLAMA (DOĞRU MANTIK: İLK TARİHİN KASASI) ---

                decimal baslangicBakiyesi = 0;

                // "ilk" tarih hangi yıl ve aysa, o ayın KASA kaydını bul.
                // Çünkü o kayıttaki "KasaEk/KasaAidat" sütunu, o ayın BAŞLANGIÇ (Devir) bakiyesidir.
                var baslangicAyKasa = db.Kasas.FirstOrDefault(x => x.BinaID == BinaID && x.KasaYil == ilk.Value.Year && x.AyKodu == ilk.Value.Month);

                if (baslangicAyKasa != null)
                {
                    // SENARYO 1: Seçilen ayın Kasa kaydı var.
                    // O ayın başlangıç devrini alıyoruz.
                    if (raporturu == "Demirbas") baslangicBakiyesi = baslangicAyKasa.KasaEk ?? 0;
                    else baslangicBakiyesi = baslangicAyKasa.KasaAidat ?? 0;
                }
                else
                {
                    // SENARYO 2: Seçilen ayın Kasa kaydı YOK (Henüz oluşmamış veya ilk ay).
                    // O zaman en başa, ACILISBAKIYE tablosuna dönüyoruz.
                    var acilis = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);
                    if (acilis != null)
                    {
                        if (raporturu == "Demirbas") baslangicBakiyesi = acilis.EkTutar ?? 0;
                        else baslangicBakiyesi = acilis.AidatTutar ?? 0;
                    }
                }

                // --- HAREKETLERİ HESAPLA (Sadece Seçilen Aralık) ---
                // Başlangıç bakiyesini bulduk, şimdi üstüne bu aralıktaki hareketleri ekleyip çıkaracağız.

                // Tahsilat Toplamı (Seçilen Aralık)
                decimal aralikTahsilat = ((List<Tahsilat>)ViewBag.TahsilatGelir).Sum(x => x.TahsilatTutar ?? 0);

                // Makbuz Toplamı (Seçilen Aralık)
                decimal aralikMakbuz = ((List<MakbuzView>)ViewBag.MakbuzGelir).Sum(x => x.MabuzTutar ?? 0);

                // Gider Toplamı (Seçilen Aralık)
                decimal aralikGider = ((List<GiderView>)ViewBag.Gider).Sum(x => x.GiderTutar ?? 0);

                // --- SONUÇ: BAŞLANGIÇ + (GELİR - GİDER) ---
                decimal kasaMevcudu = (baslangicBakiyesi + aralikTahsilat + aralikMakbuz) - aralikGider;

                ViewBag.KasaBaslik = (raporturu == "Demirbas" ? "DEMİRBAŞ" : "AİDAT") + " KASA MEVCUDU";
                ViewBag.KasaTutar = kasaMevcudu;

                // View'da kullanmak için toplamları da gönderelim
                ViewBag.AralikGiderToplam = aralikGider;
                ViewBag.AralikMakbuzToplam = aralikMakbuz;
                ViewBag.AralikTahsilatToplam = aralikTahsilat;
            }

            return View();
        }

        public ActionResult TureGoreGelirPDF(DateTime? ilk, DateTime? son, string raporturu)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null) return RedirectToAction("Login", "AnaSayfa");

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);

            DateTime baslangic = ilk.Value.Date;
            DateTime bitis = son.Value.Date.AddDays(1).AddTicks(-1);

            // --- LİSTELEME SORGULARI ---
            var tahsilatQuery = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= baslangic && x.TahsilatTarih <= bitis && x.Durum == "A");
            string satirTuru = (raporturu == "Demirbas") ? "E" : "A";

            if (raporturu == "Demirbas") tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == true);
            else tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == false);

            var uygunMakbuzIDleri = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.Durum == "A" && x.EkMiAidatMi == satirTuru).Select(x => x.MakbuzID).Distinct().ToList();
            var makbuzGelir = db.MakbuzViews.Where(x => x.BinaID == BinaID && x.MakbuzTarihi >= baslangic && x.MakbuzTarihi <= bitis && x.Durum == "A" && uygunMakbuzIDleri.Contains(x.MakbuzID)).OrderBy(x => x.MakbuzID).ToList();
            var tahsilatListesi = tahsilatQuery.OrderBy(x => x.TahsilatID).ToList();

            // --- KASA HESAPLAMA (DOĞRU MANTIK) ---
            decimal baslangicBakiyesi = 0;

            // 1. "ilk" tarihin ayındaki Kasa kaydına bak
            var baslangicAyKasa = db.Kasas.FirstOrDefault(x => x.BinaID == BinaID && x.KasaYil == ilk.Value.Year && x.AyKodu == ilk.Value.Month);

            if (baslangicAyKasa != null)
            {
                if (raporturu == "Demirbas") baslangicBakiyesi = baslangicAyKasa.KasaEk ?? 0;
                else baslangicBakiyesi = baslangicAyKasa.KasaAidat ?? 0;
            }
            else
            {
                // 2. Yoksa AcilisBakiye'ye git
                var acilis = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);
                if (acilis != null)
                {
                    if (raporturu == "Demirbas") baslangicBakiyesi = acilis.EkTutar ?? 0;
                    else baslangicBakiyesi = acilis.AidatTutar ?? 0;
                }
            }

            // Seçilen aralıktaki toplamları hesapla (ViewBag'deki listelerden değil, yeniden sorgudan)
            decimal aralikTahsilat = tahsilatListesi.Sum(x => x.TahsilatTutar ?? 0);
            decimal aralikMakbuz = makbuzGelir.Sum(x => x.MabuzTutar ?? 0);

            // Gideri de hesaplamamız lazım ki kasayı bulabilelim (Listelemesek bile)
            var giderQuery = db.Giders.Where(x => x.BinaID == BinaID && x.GiderTarih >= baslangic && x.GiderTarih <= bitis && x.Durum == "A");
            if (raporturu == "Demirbas") giderQuery = giderQuery.Where(x => x.GiderTuruID == 6);
            else giderQuery = giderQuery.Where(x => x.GiderTuruID != 6);
            decimal aralikGider = giderQuery.Sum(x => (decimal?)x.GiderTutar) ?? 0;

            decimal kasaMevcudu = (baslangicBakiyesi + aralikTahsilat + aralikMakbuz) - aralikGider;

            // --- PDF OLUŞTURMA ---
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 25, 25);
                MemoryStream workStream = new MemoryStream();
                PdfWriter.GetInstance(document, workStream).CloseStream = false;
                document.Open();

                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bf, 14, Font.BOLD);
                Font tableFont = new Font(bf, 9);
                Font boldTableFont = new Font(bf, 10, Font.BOLD);
                Font kasaFont = new Font(bf, 12, Font.BOLD);

                string baslikTur = (raporturu == "Demirbas") ? "DEMİRBAŞ GELİR" : "AİDAT GELİR";
                Paragraph title = new Paragraph($"{binaAdi} - {baslikTur} RAPORU", titleFont) { Alignment = Element.ALIGN_CENTER };
                document.Add(title);
                document.Add(new Paragraph($"Tarih Aralığı: {ilk?.ToString("dd/MM/yyyy")} - {son?.ToString("dd/MM/yyyy")}", tableFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));

                // MAKBUZ TABLOSU
                if (makbuzGelir.Count > 0)
                {
                    document.Add(new Paragraph("Makbuz Gelirleri", boldTableFont));
                    PdfPTable table = new PdfPTable(4) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 2f, 3f, 2f, 2f });
                    table.AddCell(new PdfPCell(new Phrase("Makbuz No", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tarih", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Daire", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tutar", boldTableFont)));
                    foreach (var item in makbuzGelir)
                    {
                        table.AddCell(new PdfPCell(new Phrase(item.MakbuzNo.ToString(), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.MakbuzTarihi.Value.ToString("dd/MM/yyyy"), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.DaireNo.ToString(), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.MabuzTutar.Value.ToString("N2"), tableFont)));
                    }
                    document.Add(table);
                    document.Add(new Paragraph($"Makbuz Toplam: {aralikMakbuz:N2} TL", boldTableFont) { Alignment = Element.ALIGN_RIGHT });
                    document.Add(new Paragraph("\n"));
                }

                // TAHSİLAT TABLOSU
                if (tahsilatListesi.Count > 0)
                {
                    document.Add(new Paragraph("Tahsilat Gelirleri", boldTableFont));
                    PdfPTable table = new PdfPTable(4) { WidthPercentage = 100 };
                    table.AddCell(new PdfPCell(new Phrase("No", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tarih", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Açıklama", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tutar", boldTableFont)));
                    foreach (var item in tahsilatListesi)
                    {
                        table.AddCell(new PdfPCell(new Phrase(item.TahsilatNo.ToString(), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.TahsilatTarih.Value.ToString("dd/MM/yyyy"), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.TahsilatAciklama, tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.TahsilatTutar.Value.ToString("N2"), tableFont)));
                    }
                    document.Add(table);
                    document.Add(new Paragraph($"Tahsilat Toplam: {aralikTahsilat:N2} TL", boldTableFont) { Alignment = Element.ALIGN_RIGHT });
                    document.Add(new Paragraph("\n"));
                }

                // KASA BİLGİSİ
                PdfPTable kasaTable = new PdfPTable(1) { WidthPercentage = 100 };
                string kasaBaslik = (raporturu == "Demirbas" ? "DEMİRBAŞ" : "AİDAT") + " KASA MEVCUDU";
                PdfPCell cellKasa = new PdfPCell(new Phrase($"{kasaBaslik}: {kasaMevcudu:N2} TL", kasaFont));
                cellKasa.HorizontalAlignment = Element.ALIGN_CENTER;
                cellKasa.BackgroundColor = iTextSharp.text.BaseColor.LIGHT_GRAY;
                cellKasa.Padding = 10;
                kasaTable.AddCell(cellKasa);
                document.Add(kasaTable);

                document.Close();
                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=GelirRaporu.pdf");
                return File(workStream, "application/pdf");
            }
        }

        public ActionResult TureGoreGiderPDF(DateTime? ilk, DateTime? son, string raporturu)
        {
            if (Request.Cookies["KullaniciBilgileri"] == null) return RedirectToAction("Login", "AnaSayfa");

            HttpCookie userCookie = Request.Cookies["KullaniciBilgileri"];
            int BinaID = Convert.ToInt32(userCookie.Values["BinaID"]);
            string binaAdi = HttpUtility.UrlDecode(Request.Cookies["KullaniciBilgileri"]["BinaAdi"]);

            DateTime baslangic = ilk.Value.Date;
            DateTime bitis = son.Value.Date.AddDays(1).AddTicks(-1);

            // --- GİDER SORGUSU ---
            var giderQuery = db.GiderViews.Where(x => x.BinaID == BinaID && x.GiderTarih >= baslangic && x.GiderTarih <= bitis && x.Durum == "A");
            string satirTuru = (raporturu == "Demirbas") ? "E" : "A";

            if (raporturu == "Demirbas") giderQuery = giderQuery.Where(x => x.GiderTuruID == 6);
            else giderQuery = giderQuery.Where(x => x.GiderTuruID != 6);

            var giderListesi = giderQuery.OrderBy(x => x.GiderID).ToList();

            // --- KASA HESAPLAMA (DOĞRU MANTIK) ---
            decimal baslangicBakiyesi = 0;
            var baslangicAyKasa = db.Kasas.FirstOrDefault(x => x.BinaID == BinaID && x.KasaYil == ilk.Value.Year && x.AyKodu == ilk.Value.Month);

            if (baslangicAyKasa != null)
            {
                if (raporturu == "Demirbas") baslangicBakiyesi = baslangicAyKasa.KasaEk ?? 0;
                else baslangicBakiyesi = baslangicAyKasa.KasaAidat ?? 0;
            }
            else
            {
                var acilis = db.AcilisBakiyes.FirstOrDefault(x => x.BinaID == BinaID);
                if (acilis != null)
                {
                    if (raporturu == "Demirbas") baslangicBakiyesi = acilis.EkTutar ?? 0;
                    else baslangicBakiyesi = acilis.AidatTutar ?? 0;
                }
            }

            // Aralıktaki Giderler
            decimal aralikGider = giderListesi.Sum(x => x.GiderTutar ?? 0);

            // Aralıktaki Gelirleri de hesaplamalıyız (Listelemesek bile)
            var tahsilatQuery = db.Tahsilats.Where(x => x.BinaID == BinaID && x.TahsilatTarih >= baslangic && x.TahsilatTarih <= bitis && x.Durum == "A");
            if (raporturu == "Demirbas") tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == true);
            else tahsilatQuery = tahsilatQuery.Where(x => x.DemirbasMi == false);
            decimal aralikTahsilat = tahsilatQuery.Sum(x => (decimal?)x.TahsilatTutar) ?? 0;

            decimal aralikMakbuz = (from m in db.Makbuzs
                                    join ms in db.MakbuzSatirs on m.MakbuzID equals ms.MakbuzID
                                    where m.BinaID == BinaID && m.Durum == "A"
                                          && m.MakbuzTarihi >= baslangic && m.MakbuzTarihi <= bitis
                                          && ms.Durum == "A" && ms.EkMiAidatMi == satirTuru
                                    select ms.Tutar).Sum() ?? 0;

            decimal kasaMevcudu = (baslangicBakiyesi + aralikTahsilat + aralikMakbuz) - aralikGider;

            // --- PDF OLUŞTURMA (Standart) ---
            using (MemoryStream ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 25, 25, 25, 25);
                MemoryStream workStream = new MemoryStream();
                PdfWriter.GetInstance(document, workStream).CloseStream = false;
                document.Open();

                string arialFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(arialFontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font titleFont = new Font(bf, 14, Font.BOLD);
                Font tableFont = new Font(bf, 9);
                Font boldTableFont = new Font(bf, 10, Font.BOLD);
                Font kasaFont = new Font(bf, 12, Font.BOLD);

                string baslikTur = (raporturu == "Demirbas") ? "DEMİRBAŞ GİDER" : "AİDAT GİDER";
                Paragraph title = new Paragraph($"{binaAdi} - {baslikTur} RAPORU", titleFont) { Alignment = Element.ALIGN_CENTER };
                document.Add(title);
                document.Add(new Paragraph($"Tarih Aralığı: {ilk?.ToString("dd/MM/yyyy")} - {son?.ToString("dd/MM/yyyy")}", tableFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));

                // GİDER TABLOSU
                if (giderListesi.Count > 0)
                {
                    document.Add(new Paragraph("Giderler", boldTableFont));
                    PdfPTable table = new PdfPTable(4) { WidthPercentage = 100 };
                    table.AddCell(new PdfPCell(new Phrase("No", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tür", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tarih", boldTableFont)));
                    table.AddCell(new PdfPCell(new Phrase("Tutar", boldTableFont)));
                    foreach (var item in giderListesi)
                    {
                        table.AddCell(new PdfPCell(new Phrase(item.GiderNo.ToString(), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.GiderTuruAdi, tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.GiderTarih.Value.ToString("dd/MM/yyyy"), tableFont)));
                        table.AddCell(new PdfPCell(new Phrase(item.GiderTutar.Value.ToString("N2"), tableFont)));
                    }
                    document.Add(table);
                    document.Add(new Paragraph($"Gider Toplam: {aralikGider:N2} TL", boldTableFont) { Alignment = Element.ALIGN_RIGHT });
                    document.Add(new Paragraph("\n"));
                }

                // KASA BİLGİSİ
                PdfPTable kasaTable = new PdfPTable(1) { WidthPercentage = 100 };
                string kasaBaslik = (raporturu == "Demirbas" ? "DEMİRBAŞ" : "AİDAT") + " KASA MEVCUDU";
                PdfPCell cellKasa = new PdfPCell(new Phrase($"{kasaBaslik}: {kasaMevcudu:N2} TL", kasaFont));
                cellKasa.HorizontalAlignment = Element.ALIGN_CENTER;
                cellKasa.BackgroundColor = iTextSharp.text.BaseColor.LIGHT_GRAY;
                cellKasa.Padding = 10;
                kasaTable.AddCell(cellKasa);
                document.Add(kasaTable);

                document.Close();
                byte[] byteInfo = workStream.ToArray();
                workStream.Write(byteInfo, 0, byteInfo.Length);
                workStream.Position = 0;

                Response.AppendHeader("Content-Disposition", "inline; filename=GiderRaporu.pdf");
                return File(workStream, "application/pdf");
            }
        }



    }
}