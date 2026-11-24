using System;

namespace ApartmanAidatTakip.Models
{
    /// <summary>
    /// En sadık aidat ödeyenler raporunda daire bazlı yıllık düzenlilik,
    /// ödeme hızı ve 100 üzerinden sadakat puanını taşıyan salt-okunur DTO.
    /// </summary>
    public class SadikOdeyenListe
    {
        public int Sira { get; set; }
        public int DaireID { get; set; }
        public int DaireNo { get; set; }
        public string AdSoyad { get; set; }
        public string DaireDurum { get; set; }

        public int ToplamAidatSayisi { get; set; }
        public int OdenenSayisi { get; set; }
        public int ZamanindaSayisi { get; set; }
        public int GecikmeliSayisi { get; set; }
        public int OdenmeyenSayisi { get; set; }
        public decimal ToplamOdenenTutar { get; set; }

        public bool YillikPesinMi { get; set; }
        public double? OrtalamaOdemeGunu { get; set; }
        public double Puan { get; set; }
        public string Seviye { get; set; }
        public string HizOzeti { get; set; }
    }
}
