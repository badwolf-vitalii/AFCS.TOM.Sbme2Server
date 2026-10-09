-- 2023y 02m 05d 21:53:01

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE AgentShift -----------------------------------------------------------
ALTER TABLE [dbo].[AgentShift]
ADD [VtsShiftID] [int] NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (2
           ,CONVERT(datetime, '20230205 21:53:01')
           ,'Added a column to AgentShift: VtsShiftID')