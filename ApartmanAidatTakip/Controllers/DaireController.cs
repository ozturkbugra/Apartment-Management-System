using ApartmanAidatTakip.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class DaireController : Controller
    {

        AptVTEntities db = new AptVTEntities();
        public ActionResult Index()
        {
            if (Session["DaireID"] == null)
            {
                return RedirectToAction("Login", "Daire");
            }
            int DaireID = Convert.ToInt32(Session["DaireID"]);

            var b = db.Dairelers.Where(x => x.DaireID == DaireID).FirstOrDefault();

            var binaid = b.BinaID;

            var binaadi = db.Binalars.Where(x => x.BinaID == binaid).FirstOrDefault();

            ViewBag.BinaAdi = binaadi.BinaAdi;

            Session["BinaAdi"] = binaadi.BinaAdi;
            Session["BinaAdres"] = binaadi.Adres;

            ViewBag.OdenenMakbuzlar = db.Makbuzs.Where(x => x.DaireID == DaireID && x.Durum == "A" && x.OnayliMi == true).OrderByDescending(x=> x.MakbuzNo).ToList();

            ViewBag.OdenmeyenAidatlar = db.Aidats.Where(x => x.BinaID == binaid && x.DaireNo == b.DaireNo && x.Durum == "A").ToList(); 
            ViewBag.OdenmeyenEkler = db.Eks.Where(x => x.BinaID == binaid && x.DaireNo == b.DaireNo && x.Durum == "A").ToList();
            ViewBag.Borc = b.Borc;

            return View();
        }

        public ActionResult Login()
        {
            DateTime simdi = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();
            return View();
        }

        [HttpPost]
        public ActionResult Login(Daireler daireler)
        {
            var varmi = db.Dairelers.Where(x => x.BinaID == daireler.BinaID && x.TC == daireler.TC && x.DaireNo == daireler.DaireNo && x.Telefon == daireler.Telefon).FirstOrDefault();
            if (varmi != null)
            {
                Session["DaireID"] = varmi.DaireID;
                Session["AdSoyad"] = varmi.AdSoyad;
                Session["DaireNo"] = varmi.DaireNo;
                Session["TC"] = varmi.TC;
                Session["Telefon"] = varmi.Telefon;
                Session["BinaID"] = varmi.BinaID;
                return RedirectToAction("Index", "Daire");
            }
            DateTime simdi = DateTime.Now.Date;
            ViewBag.Binalar = db.Binalars.Where(x => x.SozlesmeBitisTarihi >= simdi && x.Durum == "A").OrderBy(x => x.BinaKullaniciAdi).ToList();

            ViewBag.Uyari = "Hatalı Giriş";
            return View();
        }

        public ActionResult Makbuz(int? MakbuzID)
        {
            if (Session["DaireID"] == null)
            {

                return RedirectToAction("Login", "Daire");
            }
          
            int BinaID = Convert.ToInt32(Session["BinaID"]);
            int DaireID = Convert.ToInt32(Session["DaireID"]);


            string adSoyad = Session["AdSoyad"].ToString();
            string binaAdi = Session["BinaAdi"].ToString();
            string binaAdres = Session["BinaAdres"].ToString();
           

            var varmi = db.MakbuzSatirs.Where(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID).FirstOrDefault();
            if (MakbuzID == null || varmi == null)
            {
                return RedirectToAction("Index", "Daire");
            }



            if (DaireID == 0)
            {
                var makbuzsorgu = db.Makbuzs.FirstOrDefault(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID);
                DaireID = Convert.ToInt32(makbuzsorgu.DaireID);
            }
            var makbuzsorgu1 = db.Makbuzs.FirstOrDefault(x => x.BinaID == BinaID && x.MakbuzID == MakbuzID);
            var daire = db.Dairelers.FirstOrDefault(x => x.DaireID == DaireID && x.BinaID == BinaID);
            var makbuzSatirList = db.MakbuzSatirViews.Where(x => x.MakbuzID == MakbuzID && x.MakbuzSatirDurum == "A" && x.DaireNo == daire.DaireNo).ToList();

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
            receiptInfoCell.AddElement(new Paragraph("Makbuz Tarih: " + DateTime.Now.ToString("dd/MM/yyyy"), subTitleFont));
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
                    tur = "Ek";
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
            debtorInfoCellLeft.AddElement(new Paragraph("Bu makbuz daire panelinden daire sakini tarafından manuel alınmıştır!", subTitleFont));
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


            // Add reminder if debt is greater than 0


            document.Close();

            byte[] byteInfo = workStream.ToArray();
            workStream.Write(byteInfo, 0, byteInfo.Length);
            workStream.Position = 0;

            string makbuzadi = daire.DaireNo.ToString() + " No'lu Dairenin Makbuzu";

            Response.AppendHeader("Content-Disposition", "inline; filename=" + makbuzadi + ".pdf");
            return File(workStream, "application/pdf");
        }

        public ActionResult Logout()
        {
            Session["DaireID"] = null;
            Session["AdSoyad"] = null;
            Session["DaireNo"] = null;
            Session["TC"] = null;
            Session["Telefon"] = null;
            Session["BinaID"] = null;
            Session["BinaAdi"] = null;
            Session["BinaAdres"] = null;
            Session.Abandon();
            return RedirectToAction("Login", "Daire");

        }
    }
}