-- 2024y 05m 24d 15:45:58

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo -----------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [NumberOfUnits] int NULL DEFAULT 1;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (43
           ,CONVERT(datetime, '20240524 15:45:58')
           ,'Added a new column to CscContractArticleInfo: NumberOfUnits.')