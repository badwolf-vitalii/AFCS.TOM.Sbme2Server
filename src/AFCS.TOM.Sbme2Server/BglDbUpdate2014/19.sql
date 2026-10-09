-- 2023y 06m 19d 16:26:18

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- DROP AND CREATE Command -----------------------------------------------------
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Command]') AND type in (N'U'))
DROP TABLE [dbo].[Command]

CREATE TABLE [dbo].[Command](
	[ID] [uniqueidentifier] NOT NULL,
	[DeviceIdentifier] [nvarchar](10) NOT NULL,
	[CommandCode] [numeric](10, 0) NOT NULL,
	[RegistrationTime] [datetime] NOT NULL,
	[ExpirationTime] [datetime] NOT NULL,
	[ExecutionTime] [datetime] NULL,
	[MessageText] [nvarchar](max) NULL,
	[ExecutionResultCode] [numeric](5, 0) NULL,
 CONSTRAINT [PK_Command] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (19
           ,CONVERT(datetime, '20230619 16:26:18')
           ,'Dropped and Created table: Command. Updateed table: CommantAttachment.')