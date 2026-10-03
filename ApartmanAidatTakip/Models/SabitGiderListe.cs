namespace ApartmanAidatTakip.Models
{
    using System;

    // Sabit gider listesini (gider türü adıyla birlikte) ekrana taşımak için
    // kullanılan salt-okunur görünüm nesnesi. DB'ye yazılmaz.
    public class SabitGiderListe
    {
        public int SabitGiderID { get; set; }
        public string GiderAciklama { get; set; }
        public Nullable<int> GiderTuruID { get; set; }
        public string GiderTuruAdi { get; set; }
        public Nullable<decimal> GiderTutar { get; set; }
    }
}
