-- 2024y 10m 02d 13:14:28

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- INSERT INTO PaymentMethod ---------------------------------------------------
INSERT INTO [dbo].[PaymentMethod] ([Code], [Name])
VALUES (9, 'Bonifico')
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (49
           ,CONVERT(datetime, '20241002 13:14:28')
           ,'Registred new PaymentMethod.')