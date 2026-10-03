-- =============================================================
-- AylikPesinOdemeler tablosu — Aylık (ay bazlı) peşin ödeme kayıtları
-- Veritabanı: aptmhs25122025  (SQL Server instance: BUGRA)
-- EDMX (Database First) ile birebir eşleşir. Bu script'i BİR KEZ çalıştırın.
--
-- Yıllık (tüm yıl) peşin ödemeler mevcut PesinOdemeler tablosunda kalır.
-- Bu tablo, bir dairenin SEÇİLİ aylarını peşin ödediği durumları tutar:
-- her ödenen ay için bir satır (DaireID + Yil + Ay).
-- =============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AylikPesinOdemeler')
BEGIN
    CREATE TABLE [dbo].[AylikPesinOdemeler]
    (
        [ID]       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DaireID]  INT               NOT NULL,
        [Yil]      INT               NOT NULL,
        [Ay]       INT               NOT NULL,   -- 1-12 ay kodu
        [BinaID]   INT               NOT NULL
    );
END
GO
