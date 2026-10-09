-- 2024y 04m 12d 11:59:29

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE AgentShift -----------------------------------------------------------
ALTER TABLE [dbo].[AgentShift]
ADD [ReportId] [numeric](10, 0) NOT NULL DEFAULT 0;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (40
           ,CONVERT(datetime, '20240412 11:59:29')
           ,'Added a column ReportId to AgentShift')