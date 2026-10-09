-- 2024y 05m 23d 16:48:53

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PtItemArticleInfo -----------------------------------------------------
ALTER TABLE [dbo].[PtItemArticleInfo]
ALTER COLUMN SerialStart numeric(20, 0) NOT NULL;

ALTER TABLE [dbo].[PtItemArticleInfo]
ALTER COLUMN SerialEnd numeric(20, 0) NOT NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (42
           ,CONVERT(datetime, '20240523 16:48:53')
           ,'PtItemArticleInfo: columns to SerialStart and SerialEnd type changed to numeric(20, 0)')