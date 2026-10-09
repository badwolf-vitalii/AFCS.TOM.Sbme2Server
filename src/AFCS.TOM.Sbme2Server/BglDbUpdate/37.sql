-- 2024y 02m 28d 12:49:56

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE StaticVariablesList ---------------------------------------------------
ALTER TABLE [dbo].[StaticVariablesList]
ADD [AdminBlockUnlockDateTime] [datetime] NULL;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [ThresholdBlockUnlockDateTime] [datetime] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (37
           ,CONVERT(datetime, '20240228 12:49:56')
           ,'Added new columns StaticVariablesList: AdminBlockUnlockDateTime, ThresholdBlockUnlockDateTime')