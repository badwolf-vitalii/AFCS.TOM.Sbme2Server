-- 2025y 05m 28d 20:18:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE AgentShift -----------------------------------------------------------
ALTER TABLE [dbo].[AgentShift]
ADD [HolderId] [int] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (61
           ,CONVERT(datetime, '20250528 20:18:43')
           ,'Added a new column to AgentShift: HolderId.')