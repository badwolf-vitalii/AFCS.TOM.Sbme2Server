-- 2024y 01m 23d 11:17:47

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PosDetail -------------------------------------------------------------
ALTER TABLE [dbo].[PosDetail]
ADD [TimeOnPC] [datetime] NOT NULL DEFAULT getdate();

ALTER TABLE [dbo].[PosDetail]
ADD [IsRefund] [tinyint] NOT NULL DEFAULT 0;

ALTER TABLE [dbo].[PosDetail]
ADD [POSTransactionId] [nvarchar](MAX) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (33
           ,CONVERT(datetime, '20240123 11:17:47')
           ,'Added new columns to PosDetail: TimeOnPC, IsRefund, POSTransactionId')