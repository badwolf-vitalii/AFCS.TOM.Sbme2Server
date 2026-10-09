-- 2025y 10m 31d 12:19:57

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE VtSellCommitInfo -----------------------------------------------------
ALTER TABLE [dbo].[VtSellCommitInfo]
ADD [TransactionUndoData] [varbinary](MAX) NULL;

ALTER TABLE [dbo].[VtSellCommitInfo]
ADD [TransactionUndoDataMd5] [nvarchar](32) NULL;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (64
           ,CONVERT(datetime, '20251031 12:19:57')
           ,'Added new columns to VtSellCommitInfo: TransactionUndoData, TransactionUndoDataMd5.')