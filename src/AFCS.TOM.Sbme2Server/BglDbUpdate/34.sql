-- 2024y 02m 01d 20:15:52

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE Article ---------------------------------------------------------------
ALTER TABLE [dbo].[Article]
ALTER COLUMN QuantityIssued numeric(4, 0) NOT NULL;

ALTER TABLE [dbo].[Article]
ALTER COLUMN QuantityRequired numeric(4, 0) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (34
           ,CONVERT(datetime, '20240201 20:15:52')
           ,'Added new columns to QuantityIssued and QuantityRequired type changed to numeric(4, 0)')