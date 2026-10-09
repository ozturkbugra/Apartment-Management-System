using ApartmanAidatTakip.Models;
using System;
using System.Data.Entity;
using System.Text;

namespace ApartmanAidatTakip.Helpers
{
    /// <summary>
    /// Uygulama ilk çalıştığında zorunlu başlangıç (seed) verilerini oluşturur.
    /// Gider türleri (GiderTuru) sabit ID'lerle (1-10) programın her yerinde
    /// kullanıldığından (ör. DetayliGelirGider, AidatHesaplama), tablo yoksa
    /// oluşturulur ve tablo BOŞSA türler eklenir. Tabloda veri varsa hiçbir
    /// şey yapılmaz (her açılışta tekrar çalışmaz).
    /// </summary>
    public static class VeriBaslangic
    {
        // GiderTuruID -> Ad. ID'ler sabittir; raporlar/hesaplamalar bu ID'lere bağlıdır.
        private static readonly (int Id, string Ad)[] GiderTurleri =
        {
            (1,  "ELEKTRİK FATURASI"),
            (2,  "SU FATURASI"),
            (3,  "BAKIM ONARIM"),
            (4,  "GÖREVLİ MAAŞ"),
            (5,  "GÖREVLİ SSK"),
            (6,  "DEMİRBAŞ"),
            (7,  "YÖNETİM"),
            (8,  "TEMİZLİK"),
            (9,  "DİĞER"),
            (10, "YAKIT ISINMA"),
        };

        public static void Calistir()
        {
            try
            {
                using (var db = new AptVTEntities())
                {
                    // Veritabanına erişilemiyorsa (ör. tasarım zamanı) sessizce çık.
                    if (!db.Database.Exists())
                        return;

                    var sb = new StringBuilder();

                    // 1) Tablo yoksa oluştur
                    sb.AppendLine("IF OBJECT_ID('dbo.GiderTuru', 'U') IS NULL");
                    sb.AppendLine("BEGIN");
                    sb.AppendLine("    CREATE TABLE dbo.GiderTuru (");
                    sb.AppendLine("        GiderTuruID int IDENTITY(1,1) NOT NULL PRIMARY KEY,");
                    sb.AppendLine("        GiderTuruAdi nvarchar(255) NULL,");
                    sb.AppendLine("        BinaID int NULL");
                    sb.AppendLine("    );");
                    sb.AppendLine("END");

                    // 2) Tablo BOŞSA sabit türleri ekle (doluysa dokunma)
                    sb.AppendLine("IF NOT EXISTS (SELECT 1 FROM dbo.GiderTuru)");
                    sb.AppendLine("BEGIN");
                    sb.AppendLine("    SET IDENTITY_INSERT dbo.GiderTuru ON;");
                    foreach (var t in GiderTurleri)
                    {
                        // Tek tırnakları kaçır (ad sabit olsa da güvenli olsun)
                        string ad = t.Ad.Replace("'", "''");
                        sb.AppendLine(
                            "    INSERT INTO dbo.GiderTuru (GiderTuruID, GiderTuruAdi, BinaID) VALUES (" +
                            t.Id + ", N'" + ad + "', 0);");
                    }
                    sb.AppendLine("    SET IDENTITY_INSERT dbo.GiderTuru OFF;");
                    sb.AppendLine("END");

                    db.Database.ExecuteSqlCommand(sb.ToString());
                }
            }
            catch
            {
                // Başlangıç verisi oluşturulamazsa uygulamanın açılışını engelleme.
                // (Bağlantı/izin sorunları başlatmayı düşürmemeli.)
            }

            AidatTanimHazirla();
        }

        /// <summary>
        /// AidatTanim (aylık aidat tanımı) tablosu yoksa oluşturur; tablo BOŞSA geçmiş
        /// Aidat kayıtlarından her bina + yıl + ay için EN SIK tekrar eden tutarı (mod;
        /// eşitlikte büyük tutar) aktarır. Doluysa dokunmaz. (AidatTanim_tablo.sql ile aynı.)
        /// </summary>
        private static void AidatTanimHazirla()
        {
            try
            {
                using (var db = new AptVTEntities())
                {
                    if (!db.Database.Exists())
                        return;

                    // 1) Tablo yoksa oluştur (+ bina/yıl/ay tekil indeks)
                    db.Database.ExecuteSqlCommand(@"
IF OBJECT_ID('dbo.AidatTanim', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AidatTanim (
        ID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BinaID int NOT NULL,
        AidatYil int NOT NULL,
        AidatAy int NOT NULL,
        Tutar decimal(18,2) NOT NULL
    );
    CREATE UNIQUE INDEX UX_AidatTanim_Bina_Yil_Ay ON dbo.AidatTanim (BinaID, AidatYil, AidatAy);
END");

                    // 2) Tablo BOŞSA geçmiş aidatlardan doldur (silinmiş ve 'Mayıs - 2' gibi ek partiler hariç)
                    db.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.AidatTanim)
BEGIN
    ;WITH Kayitlar AS (
        SELECT BinaID, AidatYil, AidatTutar,
               CASE LTRIM(RTRIM(AidatAy))
                    WHEN N'Ocak' THEN 1 WHEN N'Şubat' THEN 2 WHEN N'Mart' THEN 3
                    WHEN N'Nisan' THEN 4 WHEN N'Mayıs' THEN 5 WHEN N'Haziran' THEN 6
                    WHEN N'Temmuz' THEN 7 WHEN N'Ağustos' THEN 8 WHEN N'Eylül' THEN 9
                    WHEN N'Ekim' THEN 10 WHEN N'Kasım' THEN 11 WHEN N'Aralık' THEN 12
               END AS Ay
        FROM dbo.Aidat
        WHERE Durum IN (N'A', N'P') AND AidatTutar > 0
          AND BinaID IS NOT NULL AND AidatYil IS NOT NULL
    ), Sayim AS (
        SELECT BinaID, AidatYil, Ay, AidatTutar,
               ROW_NUMBER() OVER (PARTITION BY BinaID, AidatYil, Ay
                                  ORDER BY COUNT(*) DESC, AidatTutar DESC) AS Sira
        FROM Kayitlar
        WHERE Ay IS NOT NULL
        GROUP BY BinaID, AidatYil, Ay, AidatTutar
    )
    INSERT INTO dbo.AidatTanim (BinaID, AidatYil, AidatAy, Tutar)
    SELECT BinaID, AidatYil, Ay, AidatTutar
    FROM Sayim
    WHERE Sira = 1
    ORDER BY BinaID, AidatYil, Ay;
END");
                }
            }
            catch
            {
                // Açılışı engelleme (bağlantı/izin sorunları başlatmayı düşürmemeli).
            }
        }
    }
}
