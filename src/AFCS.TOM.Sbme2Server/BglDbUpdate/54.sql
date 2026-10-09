-- 2024y 10m 04d 23:15:58

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PaymentDetail --------------------------------------------------------
ALTER TABLE [dbo].[PaymentDetail]
ADD [PtDiscount] [numeric](5, 0) NULL;

ALTER TABLE [dbo].[PaymentDetail]
ADD [PtDiscountNotes] [nvarchar](240) NULL;

ALTER TABLE [dbo].[PaymentDetail]
ADD [FullPrice] [decimal](9, 0) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (54
           ,CONVERT(datetime, '20241004 23:15:58')
           ,'Added new columns to PaymentDetail: PtDiscount, PtDiscountNotes, FullPrice.')