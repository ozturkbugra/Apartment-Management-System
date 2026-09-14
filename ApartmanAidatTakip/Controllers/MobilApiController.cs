using ApartmanAidatTakip.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ApartmanAidatTakip.Controllers
{
    public class MobilIstekModel
    {
        public string BinaKullaniciAdi { get; set; }
        public string Sifre { get; set; }
    }

    public class MobilApiController : Controller
    {
        AptVTEntities db = new AptVTEntities();

        [HttpPost] // GET yerine POST yaptık
        public JsonResult GetBorcluDaireler(MobilIstekModel istek)
        {
            if (istek == null || istek.Sifre != "bugraozturk1905")
            {
                Response.StatusCode = 401;
                return Json(new { success = false, message = "Yetkisiz erişim. Şifre hatalı!" });
                // Artık JsonRequestBehavior.AllowGet yazmamıza gerek yok
            }

            if (string.IsNullOrEmpty(istek.BinaKullaniciAdi))
            {
                return Json(new { success = false, message = "Kullanıcı adı boş olamaz." });
            }

            // Gelen verideki boşlukları temizle
            string arananBina = istek.BinaKullaniciAdi;

            var bina = db.Binalars.FirstOrDefault(x => x.BinaKullaniciAdi == arananBina);

            if (bina == null)
            {
                return Json(new { success = false, message = "Bina bulunamadı." });
            }

            var borcluDaireler = db.Dairelers
                .Where(x => x.BinaID == bina.BinaID && x.Borc > 0)
                .OrderBy(x => x.DaireNo)
                .ToList()
                .Select(x => new
                {
                    DaireNo = x.DaireNo.ToString(),
                    AdSoyad = x.AdSoyad,
                    Borc = x.Borc ?? 0
                }).ToList();

            return Json(new { success = true, data = borcluDaireler });
        }
    }
}