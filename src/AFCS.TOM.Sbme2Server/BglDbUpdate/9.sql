-- 2023y 03m 06d 12:58:29

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE Article --------------------------------------------------------------
ALTER TABLE [dbo].[Article]
ADD [DiscountApplied] [decimal](10, 0);

ALTER TABLE [dbo].[Article]
ADD [FromWhiteList] [tinyint];
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (9
           ,CONVERT(datetime, '20230306 12:58:29')
           ,'Added columns to Article: DiscountApplied and FromWhiteList')