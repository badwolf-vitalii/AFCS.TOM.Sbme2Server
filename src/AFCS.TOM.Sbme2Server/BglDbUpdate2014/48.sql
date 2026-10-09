-- 2024y 07m 25d 10:45:32

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE SaleTransaction ------------------------------------------------------
ALTER TABLE [dbo].[SaleTransaction]
ADD [PtInvoiceClientCode] nvarchar(50) NULL;

ALTER TABLE [dbo].[SaleTransaction]
ADD [PtInvoiceClientBusinessName] nvarchar(100) NULL;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (48
           ,CONVERT(datetime, '20240725 10:45:32')
           ,'Added new columns to SaleTransaction: PtInvoiceClientCode and PtInvoiceClientBusinessName.')