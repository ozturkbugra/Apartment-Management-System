using System;

namespace ApartmanAidatTakip.Models
{
    /// <summary>
    /// Aidat Hesaplama / Tahmini Aidat ekranında, bir yılın giderlerini
    /// gider türüne göre gruplayıp taşıyan salt-okunur DTO (DB view değil).
    /// Demirbaş grupları <see cref="Demirbas"/> ile işaretlenir; ekranda ayrı tutulur.
    /// </summary>
    public class AidatHesaplamaGrup
    {
        public string Ad { get; set; }
        public decimal Tutar { get; set; }
        public bool Demirbas { get; set; }
    }
}
