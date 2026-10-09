-- 2023y 09m 19d 10:53:56

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo -----------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ALTER COLUMN CardSerialNumber nvarchar(20)

-- UPDATE CanceledCscContract --------------------------------------------------
ALTER TABLE [dbo].[CanceledCscContract]
ALTER COLUMN CardSerialNumber nvarchar(20)

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (27
           ,CONVERT(datetime, '20230919 10:53:56')
           ,'CardSerialNumber length increased in: CanceledCscContract & CscContractArticleInfo.')