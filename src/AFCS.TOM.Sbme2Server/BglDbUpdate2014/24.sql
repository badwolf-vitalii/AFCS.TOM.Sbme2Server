-- 2023y 07m 09d 00:52:08

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE ContactlessCardExpirationExtensionArticleInfo ------------------------
ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo]
ADD [HolderBirthday] [datetime] NULL;

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo]
ADD [CardLogicalSerialNumber] [nvarchar](10) NULL;
--------------------------------------------------------------------------------

-- UPDATE CscContractRefundArticleInfo -----------------------------------------
ALTER TABLE [dbo].[CscContractRefundArticleInfo]
ADD [VtsReceiptId] [uniqueidentifier] NOT NULL DEFAULT newid();
--------------------------------------------------------------------------------

-- UPDATE MagneticRefundArticleInfo --------------------------------------------
ALTER TABLE [dbo].[MagneticRefundArticleInfo]
ADD [VtsReceiptId] [uniqueidentifier] NOT NULL DEFAULT newid();
--------------------------------------------------------------------------------

-- UPDATE PtItemRefundArticleInfo ----------------------------------------------
ALTER TABLE [dbo].[PtItemRefundArticleInfo]
ADD [VtsReceiptId] [uniqueidentifier] NOT NULL DEFAULT newid();
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (24
           ,CONVERT(datetime, '20230709 00:52:08')
           ,'Added columns to ContactlessCardExpirationExtensionArticleInfo: HolderBirthday, CardLogicalSerialNumber. Added a column to CscContractRefundArticleInfo, MagneticRefundArticleInfo and PtItemRefundArticleInfo: VtsReceiptId.')