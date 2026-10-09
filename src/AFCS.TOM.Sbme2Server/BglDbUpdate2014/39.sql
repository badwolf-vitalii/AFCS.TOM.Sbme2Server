-- 2023y 03m 26d 15:44:08

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE ContactlessCardArticleInfo -------------------------------------------
ALTER TABLE [dbo].[ContactlessCardArticleInfo]
ADD [PaidByEmployee] [numeric](10) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (39
           ,CONVERT(datetime, '20230325 15:44:08')
           ,'Added a column to ContactlessCardArticleInfo: PaidByEmployee')