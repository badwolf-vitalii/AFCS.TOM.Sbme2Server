-- 2024y 02m 01d 20:32:06

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- INSERT INTO PaymentMethod ---------------------------------------------------
INSERT INTO [dbo].[PaymentMethod] ([Code], [Name])
VALUES (7, 'Receipt')
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (35
           ,CONVERT(datetime, '20240201 20:32:06')
           ,'Registred new PaymentMethod')