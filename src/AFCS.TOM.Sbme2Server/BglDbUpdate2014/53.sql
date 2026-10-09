-- 2024y 10m 04d 23:08:26

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PtConfirmTransaction -------------------------------------------------
ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [PtDiscount] [numeric](5, 0) NULL;

ALTER TABLE [dbo].[PtConfirmTransaction]
ADD [PtDiscountNotes] [nvarchar](240) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (53
           ,CONVERT(datetime, '20241004 23:08:26')
           ,'Added new columns to PtConfirmTransaction: PtDiscount and PtDiscountNotes.')