-- 2023y 06m 13d 22:15:21

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- INSERT INTO PaymentMethod ---------------------------------------------------
INSERT INTO [dbo].[PaymentMethod] ([Code], [Name])
VALUES (15, 'PAYROLL')
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (18
           ,CONVERT(datetime, '20230613 22:15:21')
           ,'Registred a new PaymentMethod')