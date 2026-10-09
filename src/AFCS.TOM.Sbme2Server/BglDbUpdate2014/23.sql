[OPTIONAL]
-- 2023y 06m 12d 21:09:22

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE AgentShift -----------------------------------------------------------
ALTER TABLE [dbo].[AgentShift]  WITH CHECK ADD  CONSTRAINT [FK_AgentShift_SellingDataSendStatus] FOREIGN KEY([SendingStatusCode])
REFERENCES [dbo].[SellingDataSendStatus] ([Code])

ALTER TABLE [dbo].[AgentShift] CHECK CONSTRAINT [FK_AgentShift_SellingDataSendStatus]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (23
           ,CONVERT(datetime, '20230621 21:09:22')
           ,'Added a foreign key: AgentShift-SellingDataSendStatus.')