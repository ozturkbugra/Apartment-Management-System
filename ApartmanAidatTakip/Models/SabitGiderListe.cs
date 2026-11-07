namespace ApartmanAidatTakip.Models
{
    using System;

    // Sabit gider listesini (gider türü adı ve aylık ödeme/vade durumuyla birlikte)
    // ekrana taşımak için kullanılan salt-okunur görünüm nesnesi. DB'ye yazılmaz.
    public class SabitGiderListe
    {
        public int SabitGiderID { get; set; }
        public string GiderAciklama { get; set; }
        public Nullable<int> GiderTuruID { get; set; }
        public string GiderTuruAdi { get; set; }
        public Nullable<decimal> GiderTutar { get; set; }
        public Nullable<int> SonOdemeGunu { get; set; }

        // Bu ayki ödeme durumu
        public bool OdendiMi { get; set; }
        public int OdemeAdedi { get; set; }
        public Nullable<decimal> OdenenTutar { get; set; }
        public Nullable<DateTime> OdemeTarihi { get; set; }
        public Nullable<int> OdenenGiderID { get; set; }
        public Nullable<int> OdenenGiderNo { get; set; }

        // Vade / Yaklaşan ödeme tarihi hesapları
        public Nullable<DateTime> SonOdemeTarihi { get; set; }
        public int KalanGun { get; set; }
        // "odendi", "bugun", "yaklasti", "gecikti", "bekliyor"
        public string DurumKodu { get; set; }
        public string DurumMesaji { get; set; }
    }
}
