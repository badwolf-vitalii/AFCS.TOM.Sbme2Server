-- 2023y 11m 29d 16:23:29

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CanceledCscContract --------------------------------------------------
ALTER TABLE [dbo].[Article]
ADD [LongDescription] [nvarchar](250) NULL;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (29
           ,CONVERT(datetime, '20231129 16:23:29')
           ,'Added a new column to Article: LongDescription.')