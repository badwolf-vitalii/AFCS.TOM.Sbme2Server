-- 2023y 03m 25d 06:21:50

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo -----------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [PaidByEmployee] [numeric](10) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (38
           ,CONVERT(datetime, '20230325 06:21:50')
           ,'Added a column to CscContractArticleInfo: PaidByEmployee')