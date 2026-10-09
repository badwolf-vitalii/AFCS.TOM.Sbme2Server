-- 2024y 10m 02d 13:40:31

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PtConfirmTransaction -------------------------------------------------
ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [BankTransferNumber] [nvarchar](15) NULL;

ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [BankTransferAmount] [decimal](9, 0) NULL;

ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [Abi] [nvarchar](15) NULL;

ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [Cab] [nvarchar](15) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (51
           ,CONVERT(datetime, '20241002 13:40:31')
           ,'Added new columns to PtConfirmTransaction: BankTransferNumber, BankTransferAmount, Abi and Cab.')