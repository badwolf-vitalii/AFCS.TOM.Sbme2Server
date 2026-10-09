-- 2024y 10m 03d 19:47:17

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CscContractRefundArticleInfo -----------------------------------------
ALTER TABLE [dbo].[CscContractRefundArticleInfo]
ADD [UndoReceiptId] [uniqueidentifier] NOT NULL DEFAULT NEWID();
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (52
           ,CONVERT(datetime, '20241003 19:47:17')
           ,'Added a new column to CscContractRefundArticleInfo: UndoReceiptId.')