-- 2023y 03m 08d 07:48:39

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
           (11
           ,CONVERT(datetime, '20230308 07:48:39')
           ,'CscContractArticleInfo.VtContractId max length changed back to 10')