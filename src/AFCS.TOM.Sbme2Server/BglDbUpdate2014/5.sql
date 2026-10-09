-- 2023y 02m 06d 10:45:00

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE MagneticArticleInfo --------------------------------------------------
ALTER TABLE [dbo].[MagneticArticleInfo]
ADD [LastTicketErrorCode] [int] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (5
           ,CONVERT(datetime, '20230206 10:45:00')
           ,'Added a column to MagneticArticleInfo: LastTicketErrorCode')