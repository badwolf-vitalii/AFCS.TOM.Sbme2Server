-- 2023y 03m 07d 11:23:49

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE Article --------------------------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ALTER COLUMN [VtContractId] [nvarchar](16);
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (10
           ,CONVERT(datetime, '20230307 11:23:49')
           ,'CscContractArticleInfo.VtContractId max length changed to 16')