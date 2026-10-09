-- 2023y 02m 13d 16:59:09

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- INSERT INTO ArticleType -----------------------------------------------------
INSERT INTO [dbo].[ArticleType] ([Code], [Name])
VALUES (8, 'UndoneMagneticTicket')

INSERT INTO [dbo].[ArticleType] ([Code], [Name])
VALUES (9, 'PtItem')

INSERT INTO [dbo].[ArticleType] ([Code], [Name])
VALUES (10, 'UndonePtItem')

INSERT INTO [dbo].[ArticleType] ([Code], [Name])
VALUES (11, 'JammedTicket')
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (7
           ,CONVERT(datetime, '20230213 16:59:09')
           ,'Registred new ArticleTypes')