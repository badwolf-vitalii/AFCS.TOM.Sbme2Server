-- 2023y 03m 25d 15:46:16

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE CardAnomalyType ------------------------------------------------------
CREATE TABLE [dbo].[CardAnomalyType](
	[Code] [numeric](5, 0) NOT NULL,
	[Name] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK_CardAnomalyType] PRIMARY KEY CLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

--------------------------------------------------------------------------------

-- INSERT INTO CardAnomalyType -------------------------------------------------
INSERT INTO [dbo].[CardAnomalyType]
           ([Code]
           ,[Name])
     VALUES
           (1
           ,'Partial writing')
--------------------------------------------------------------------------------

-- CREATE CardAnomaly ----------------------------------------------------------
CREATE TABLE [dbo].[CardAnomaly](
	[ID] [uniqueidentifier] NOT NULL,
	[DeviceShift_ID] [uniqueidentifier] NOT NULL,
	[CardSerialNumberPh] [nvarchar](10) NOT NULL,
	[CardSerialNumberLo] [nvarchar](10) NOT NULL,
	[ShortCardModel] [int] NOT NULL,
	[AnomalyTime] [datetime] NOT NULL,
	[AnomalyType] [numeric](5, 0) NOT NULL,
	[ResolvedTime] [datetime] NULL,
 CONSTRAINT [PK_CardAnomaly] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[CardAnomaly]  WITH CHECK ADD  CONSTRAINT [FK_CardAnomaly_DeviceShiftID] FOREIGN KEY([DeviceShift_ID])
REFERENCES [dbo].[DeviceShift] ([ID])

ALTER TABLE [dbo].[CardAnomaly]  WITH CHECK ADD  CONSTRAINT [FK_CardAnomaly_CardAnomalyType] FOREIGN KEY([AnomalyType])
REFERENCES [dbo].[CardAnomalyType] ([Code])

ALTER TABLE [dbo].[CardAnomaly] CHECK CONSTRAINT [FK_CardAnomaly_CardAnomalyType]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (12
           ,CONVERT(datetime, '20230325 15:46:16')
           ,'Created tables CardAnomalyType and CardAnomaly')