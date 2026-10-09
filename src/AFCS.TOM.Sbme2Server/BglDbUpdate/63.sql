-- 2025y 10m 06d 10:39:00

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE SaleTransaction ------------------------------------------------------
ALTER TABLE [dbo].[SaleTransaction]
ADD [WithInvoice] [tinyint] NULL;

-- UPDATE Article --------------------------------------------------------------
ALTER TABLE [dbo].[Article]
ADD [WithInvoice] [tinyint] NULL;

-- UPDATE CanceledCscContract --------------------------------------------------
ALTER TABLE [dbo].[CanceledCscContract]
ADD [WithInvoice] [tinyint] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (63
           ,CONVERT(datetime, '20251006 10:39:00')
           ,'Added new columns to SaleTransaction, Article and CanceledCscContract: WithInvoice.')