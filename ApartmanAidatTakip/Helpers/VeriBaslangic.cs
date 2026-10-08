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
        }
    }
}
