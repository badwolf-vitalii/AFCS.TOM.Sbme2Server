-- 2025y 11m 13d 16:53:44

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE StaticVariablesList ---------------------------------------------------
ALTER TABLE [dbo].[StaticVariablesList]
ADD [OfflineBlock] [tinyint] NOT NULL DEFAULT 0;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [OfflineBlockUnlockDateTime] [datetime] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (65
           ,CONVERT(datetime, '20251113 16:53:44')
           ,'Added new columns to StaticVariablesList: OfflineBlock, OfflineBlockUnlockDateTime.')