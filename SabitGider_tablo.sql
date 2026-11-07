-- =============================================================
-- SabitGider tablosu — Sabit (tekrar eden) gider şablonları
-- Veritabanı: aptmhs25122025  (SQL Server instance: BUGRA)
-- EDMX (Database First) ile birebir eşleşir. Bu script'i BİR KEZ çalıştırın.
-- =============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SabitGider')
BEGIN
    CREATE TABLE [dbo].[SabitGider]
    (
        [SabitGiderID]  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [GiderAciklama] NVARCHAR(500)     NULL,
        [GiderTuruID]   INT               NULL,
        [GiderTutar]    DECIMAL(18,2)     NULL,
        [BinaID]        INT               NULL,
        [Durum]         NVARCHAR(1)       NULL,  -- 'A' = Aktif, 'P' = Pasif/Silinmiş
        [SonOdemeGunu]  INT               NULL   -- Ayın 1-31 arası son ödeme günü
    );
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.SabitGider', 'SonOdemeGunu') IS NULL
    BEGIN
        ALTER TABLE [dbo].[SabitGider] ADD [SonOdemeGunu] INT NULL;
    END
END
GO
