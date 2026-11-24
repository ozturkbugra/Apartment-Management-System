using System;
using System.Collections.Generic;

namespace ApartmanAidatTakip.Models
{
    // Borçlu daire listesini ekrana, PDF ve Excel'e taşımak için
    // daire bilgisi ile geciken ay sayısı ve borçlu dönemleri birleştiren DTO.
    public class BorcluDaireListe
    {
        public int DaireID { get; set; }
        public Nullable<int> DaireNo { get; set; }
        public string AdSoyad { get; set; }
        public string Telefon { get; set; }
        public string TC { get; set; }
        public string DaireDurum { get; set; }
        public decimal Borc { get; set; }

        public int GecikenAySayisi { get; set; }
        public int GecikenEkSayisi { get; set; }
        public List<string> BorcluAylar { get; set; }
        public List<string> BorcluEkler { get; set; }

        public bool CokluBorcluMu { get; set; }
    }
}
