-- 2024y 07m 16d 12:28:19

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE Article --------------------------------------------------------------
ALTER TABLE [dbo].[Article]
ADD [BillingPrices] nvarchar(MAX) NULL;

ALTER TABLE [dbo].[Article]
ADD [NumSalePeriodUnits] int NULL;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (45
           ,CONVERT(datetime, '20240716 12:28:19')
           ,'Added new columns to Article: BillingPrices and NumSalePeriodUnits.')