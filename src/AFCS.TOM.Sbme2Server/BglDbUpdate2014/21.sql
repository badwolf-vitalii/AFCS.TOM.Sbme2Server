-- 2023y 06m 19d 17:19:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CommandAttachment ----------------------------------------------------
ALTER TABLE [dbo].[CommandAttachment]  WITH CHECK ADD  CONSTRAINT [FK_CommandAttachment_CommandAttachmentType] FOREIGN KEY([Type])
REFERENCES [dbo].[CommandAttachmentType] ([Code])
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (21
           ,CONVERT(datetime, '20230619 17:19:43')
           ,'Created a table: CommantAttachmentType. Added new records to CommandAttachmentType. Altered table: CommandAttachment.')