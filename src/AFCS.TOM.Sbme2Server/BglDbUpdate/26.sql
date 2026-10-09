-- 2023y 07m 25d 16:40:22

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CommandAttachment ----------------------------------------------------
ALTER TABLE [dbo].[CommandAttachment]
ADD [FileName] [nvarchar](MAX) NULL;
ALTER TABLE [dbo].[CommandAttachment]
ADD [Position] [tinyint] NOT NULL DEFAULT 0;

-- UPDATE Command --------------------------------------------------------------
ALTER TABLE [dbo].[Command]
ADD [ErrorOutput] [nvarchar](MAX) NULL;
ALTER TABLE [dbo].[Command]
ADD [StandardOutput] [nvarchar](MAX) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (26
           ,CONVERT(datetime, '20230725 16:40:22')
           ,'Added columns to CommandAttachment: FileName, Position. Added columns to Command: ErrorOutput, StandardOutput.')