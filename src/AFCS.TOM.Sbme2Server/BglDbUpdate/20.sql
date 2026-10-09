-- 2023y 06m 19d 16:50:36

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CommandAttachment ----------------------------------------------------
ALTER TABLE [dbo].[CommandAttachment]
ADD [Type] [numeric](5, 0) NOT NULL DEFAULT 0;
--------------------------------------------------------------------------------

-- CREATE CommandAttachmentType ------------------------------------------------
CREATE TABLE [dbo].[CommandAttachmentType](
	[Code] [numeric](5, 0) NOT NULL,
	[Name] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK_CommandAttachmentType] PRIMARY KEY CLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

-- INSERT INTO CommandAttachmentType -----------------------------------------------
INSERT INTO [dbo].[CommandAttachmentType]
           ([Code]
           ,[Name])
    VALUES
           (0,
           'None')

INSERT INTO [dbo].[CommandAttachmentType]
           ([Code]
           ,[Name])
    VALUES
           (1,
           'Text')
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (20
           ,CONVERT(datetime, '20230619 16:50:36')
           ,'Created a new table: CommantAttachmentType. Added new records to CommandAttachmentType.')