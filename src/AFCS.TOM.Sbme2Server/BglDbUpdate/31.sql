-- 2024y 01m 11d 16:56:59

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo ------------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [ZoneList] [nvarchar](32) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (31
           ,CONVERT(datetime, '20240111 16:56:59')
           ,'Added a column to CscContractArticleInfo: ZoneList')