-- 2024y 01m 23d 11:28:37

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo ------------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [AreaExtension] [nvarchar](8) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (32
           ,CONVERT(datetime, '20240123 11:28:37')
           ,'Added a column to CscContractArticleInfo: AreaExtension')