using System.Collections.Generic;

namespace ApartmanAidatTakip.Models
{
    // Aylık peşin ödemeleri ekrana taşırken daire bilgisiyle birleştiren salt-okunur DTO.
    // (DB view değil; controller'da Daireler ile join'lenerek doldurulur — SabitGiderListe deseni.)
    public class AylikPesinOdemeListe
    {
        public int DaireID { get; set; }
        public int DaireNo { get; set; }
        public string AdSoyad { get; set; }
        public int Yil { get; set; }
        public List<int> Aylar { get; set; }
    }
}
