-- 2024y 07m 17d 16:53:09

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE Article --------------------------------------------------------------
ALTER TABLE [dbo].[Article]
ADD [SoldInSubstitution] tinyint NULL DEFAULT 0;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (46
           ,CONVERT(datetime, '20240717 16:53:09')
           ,'Added a new column to Article: SoldInSubstitution.')