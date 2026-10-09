-- 2024y 06m 12d 06:52:50

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE SaleTransaction ------------------------------------------------------
ALTER TABLE [dbo].[SaleTransaction]
ADD [ClosedAutomatically] tinyint NULL DEFAULT 0;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (44
           ,CONVERT(datetime, '20240612 06:52:50')
           ,'Added a new column to SaleTransaction: ClosedAutomatically.')