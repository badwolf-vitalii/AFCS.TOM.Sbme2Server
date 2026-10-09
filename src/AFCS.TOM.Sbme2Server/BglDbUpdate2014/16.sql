-- 2023y 05m 05d 18:12:13

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE SaleDevice -----------------------------------------------------------
CREATE TABLE [dbo].[SaleDevice](
	[ID] [int] NOT NULL,
	[TomSwVer] [nvarchar](32) NOT NULL,
	[VtsSrvSwVer] [nvarchar](32) NULL,
	[PluginVer] [nvarchar](32) NULL,
	[LocalDsdeSrvSwVer] [nvarchar](32) NULL,
	[RemoteDsdeSrvSwVer] [nvarchar](32) NULL,
	[TpfVer] [int] NULL,
	[LocalDbVer] [int] NULL,
	[StartUp] [datetime] NOT NULL,
	[ActiveAgentShift] [uniqueidentifier] NULL,
	[Description] [nvarchar](50) NULL,
	[NodeId] [int] NULL,
	[NodeDescription] [nvarchar](50) NULL,
	[OperatorId] [tinyint] NULL,
	[DeviceClass] [tinyint] NULL,
	[DeviceCode] [int] NULL,
	[LastUpdate] [datetime] NOT NULL,
 CONSTRAINT [PK_SaleDevice] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

-- CREATE SaleDeviceConfiguration ----------------------------------------------
CREATE TABLE [dbo].[SaleDeviceConfiguration](
	[ID] [uniqueidentifier] NOT NULL,
	[SaleDeviceId] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_SaleDeviceConfiguration] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[SaleDeviceConfiguration]  WITH CHECK ADD  CONSTRAINT [FK_SaleDeviceConfiguration_SaleDevice] FOREIGN KEY([SaleDeviceId])
REFERENCES [dbo].[SaleDevice] ([ID])

ALTER TABLE [dbo].[SaleDeviceConfiguration] CHECK CONSTRAINT [FK_SaleDeviceConfiguration_SaleDevice]
--------------------------------------------------------------------------------

-- CREATE DsdePeriferalDevice --------------------------------------------------
CREATE TABLE [dbo].[DsdePeriferalDevice](
	[Type] [int] NOT NULL,
	[SubType] [int] NOT NULL,
	[SaleDeviceId] [int] NOT NULL,
	[Enabled] [tinyint] NOT NULL,
	[SwVer] [nvarchar](32) NULL,
	[Description] [nvarchar](50) NULL,
	[LastUpdate] [datetime] NOT NULL,
 CONSTRAINT [PK_DsdePeriferalDevice] PRIMARY KEY CLUSTERED 
(
	[Type] ASC,
	[SubType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[DsdePeriferalDevice]  WITH CHECK ADD  CONSTRAINT [FK_DsdePeriferalDevice_SaleDevice] FOREIGN KEY([SaleDeviceId])
REFERENCES [dbo].[SaleDevice] ([ID])

ALTER TABLE [dbo].[DsdePeriferalDevice] CHECK CONSTRAINT [FK_DsdePeriferalDevice_SaleDevice]
--------------------------------------------------------------------------------

-- CREATE DsdePeriferalDeviceModule --------------------------------------------
CREATE TABLE [dbo].[DsdePeriferalDeviceModule](
	[Type] [int] NOT NULL,
	[SubType] [int] NOT NULL,
	[DeviceType] [int] NOT NULL,
	[DeviceSubType] [int] NOT NULL,
	[Enabled] [tinyint] NOT NULL,
	[Description] [nvarchar](50) NULL,
 CONSTRAINT [PK_DsdePeriferalDeviceModule] PRIMARY KEY CLUSTERED 
(
	[Type] ASC,
	[SubType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[DsdePeriferalDeviceModule]  WITH CHECK ADD  CONSTRAINT [FK_DsdePeriferalDeviceModule_DsdePeriferalDevice] FOREIGN KEY([DeviceType], [DeviceSubType])
REFERENCES [dbo].[DsdePeriferalDevice] ([Type], [SubType])

ALTER TABLE [dbo].[DsdePeriferalDeviceModule] CHECK CONSTRAINT [FK_DsdePeriferalDeviceModule_DsdePeriferalDevice]
--------------------------------------------------------------------------------

-- CREATE DsdePeriferalDeviceModuleAlarm ---------------------------------------
CREATE TABLE [dbo].[DsdePeriferalDeviceModuleAlarm](
	[ID] [uniqueidentifier] NOT NULL,
	[ModuleType] [int] NOT NULL,
	[ModuleSubType] [int] NOT NULL,
	[Code] [tinyint] NOT NULL,
	[Description] [nvarchar](50) NULL,
	[Gravity] [tinyint] NOT NULL,
 CONSTRAINT [PK_DsdePeriferalDeviceModuleAlarm] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[DsdePeriferalDeviceModuleAlarm]  WITH CHECK ADD  CONSTRAINT [FK_DsdePeriferalDeviceModuleAlarm_DsdePeriferalDeviceModule] FOREIGN KEY([ModuleType], [ModuleSubType])
REFERENCES [dbo].[DsdePeriferalDeviceModule] ([Type], [SubType])

ALTER TABLE [dbo].[DsdePeriferalDeviceModuleAlarm] CHECK CONSTRAINT [FK_DsdePeriferalDeviceModuleAlarm_DsdePeriferalDeviceModule]
--------------------------------------------------------------------------------

-- CREATE DsdePeriferalDeviceConfiguration -------------------------------------
CREATE TABLE [dbo].[DsdePeriferalDeviceConfiguration](
	[ID] [uniqueidentifier] NOT NULL,
	[DeviceType] [int] NOT NULL,
	[DeviceSubType] [int] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_DsdePeriferalDeviceConfiguration] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[DsdePeriferalDeviceConfiguration]  WITH CHECK ADD  CONSTRAINT [FK_DsdePeriferalDeviceConfiguration_DsdePeriferalDevice] FOREIGN KEY([DeviceType], [DeviceSubType])
REFERENCES [dbo].[DsdePeriferalDevice] ([Type], [SubType])

ALTER TABLE [dbo].[DsdePeriferalDeviceConfiguration] CHECK CONSTRAINT [FK_DsdePeriferalDeviceConfiguration_DsdePeriferalDevice]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (16
           ,CONVERT(datetime, '20230505 18:12:13')
           ,'Added tables: SaleDevice, SaleDeviceConfiguration, DsdePeriferalDevice, DsdePeriferalDeviceModule, DsdePeriferalDeviceModuleAlarm, DsdePeriferalDeviceConfiguration')