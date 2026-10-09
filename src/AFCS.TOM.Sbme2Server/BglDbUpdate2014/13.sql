-- 2023y 03m 27d 09:19:01

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE ApplicationShutdownReason --------------------------------------------
CREATE TABLE [dbo].[ApplicationShutdownReason](
	[Code] [numeric](5, 0) NOT NULL,
	[Name] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK_ApplicationShutdownReason] PRIMARY KEY CLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

-- INSERT INTO ApplicationShutdownReason ---------------------------------------
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (0
           ,'Unknown')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (1
           ,'Another')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (2
           ,'Software Update')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (3
           ,'Blocked by Busy Indicator')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (4
           ,'Card detecting slow down')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (5
           ,'Card reading/writing slow down')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (6
           ,'Ticket printer issue')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (7
           ,'Pos terminal issue')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (8
           ,'Receipt printer issue')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (9
           ,'No available tariffs')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (10
           ,'Agent shift issue')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (11
           ,'Basket issue')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (12
           ,'VTS Server unreachable')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (13
           ,'DSDE Server unreachable')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (14
           ,'SBME/AFCS Server unreachable')
INSERT INTO [dbo].[ApplicationShutdownReason]
           ([Code]
           ,[Name])
     VALUES
           (15
           ,'Cavalleri Service unreachable')
--------------------------------------------------------------------------------

-- CREATE ApplicationShutdown --------------------------------------------------
CREATE TABLE [dbo].[ApplicationShutdown](
	[ID] [uniqueidentifier] NOT NULL,
	[DeviceShift_ID] [uniqueidentifier] NULL,
	[Reason] [numeric](5, 0) NOT NULL,
	[Time] [datetime] NOT NULL,
	[Context] [varbinary](max) NULL,
 CONSTRAINT [PK_ApplicationShutdown] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[ApplicationShutdown]  WITH CHECK ADD  CONSTRAINT [FK_ApplicationShutdown_ApplicationShutdownReason] FOREIGN KEY([Reason])
REFERENCES [dbo].[ApplicationShutdownReason] ([Code])

ALTER TABLE [dbo].[ApplicationShutdown] CHECK CONSTRAINT [FK_ApplicationShutdown_ApplicationShutdownReason]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (13
           ,CONVERT(datetime, '20230327 09:19:01')
           ,'Created tables ApplicationShutdownReason and ApplicationShutdown')