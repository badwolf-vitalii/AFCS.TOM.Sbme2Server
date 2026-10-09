-- 2023y 04m 19d 14:31:10

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractArticleInfo ----------------------------------------------
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [VtSlaveContractsId] [nvarchar](max) NULL;
ALTER TABLE [dbo].[CscContractArticleInfo]
ADD [VtSlaveContractsTariffId] [nvarchar](max) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (14
           ,CONVERT(datetime, '20230419 14:31:10')
           ,'Added columns to CscContractArticleInfo: VtSlaveContractsId and VtSlaveContractsTariffId')