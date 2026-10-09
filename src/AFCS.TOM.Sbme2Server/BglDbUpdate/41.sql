-- 2024y 04m 26d 11:33:26

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PtItemArticleInfo ----------------------------------------------------
ALTER TABLE [dbo].[PtItemArticleInfo]
ADD [SaleDeviceId] int NOT NULL DEFAULT 0;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (41
           ,CONVERT(datetime, '20240426 11:33:26')
           ,'Added a new column to PtItemArticleInfo: SaleDeviceId.')