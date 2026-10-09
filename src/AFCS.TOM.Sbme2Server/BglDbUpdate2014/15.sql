-- 2023y 05m 05d 18:02:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE CanceledCscContract --------------------------------------------------
CREATE TABLE [dbo].[CanceledCscContract](
	[ID] [nvarchar](16) NOT NULL,
	[DeviceShiftId] [uniqueidentifier] NOT NULL,
	[CardSerialNumber] [nvarchar](10) NOT NULL,
	[CardShortCardModel] [int] NOT NULL,
	[ContractSerialNumber] [nvarchar](16) NOT NULL,
	[ContractSaleDeviceId] [int] NOT NULL,
	[ContractSaleOperatorId] [tinyint] NOT NULL,
	[ContractPosition] [tinyint] NULL,
	[TariffId] [int] NOT NULL,
	[TariffDescription] [nvarchar](32) NULL,
	[SaleType] [tinyint] NULL,
	[OriginId] [smallint] NULL,
	[OriginDescription] [nvarchar](50) NULL,
	[DestinationId] [smallint] NULL,
	[DestinationDescription] [nvarchar](50) NULL,
	[Via1] [smallint] NULL,
	[Via1Description] [nvarchar](50) NULL,
	[Via2] [smallint] NULL,
	[Via2Description] [nvarchar](50) NULL,
	[PassengerClass] [tinyint] NULL,
	[TotalRides] [smallint] NULL,
	[RidesToGo] [smallint] NULL,
	[MetroRidesToGo] [smallint] NULL,
	[MinutesToGo] [smallint] NULL,
	[RideMaxDuration] [smallint] NULL,
	[JourneyDistance] [smallint] NULL,
	[KilometerDistance] [smallint] NULL,
	[ResidualQuantity] [tinyint] NULL,
	[RequireInputDate] [tinyint] NULL,
	[SVD] [datetime] NULL,
	[EVD] [datetime] NULL,
	[LVD] [datetime] NULL,
	[SellingDateTime] [datetime] NULL,
	[CancellationDateTime] [datetime] NOT NULL,
	[CancellationReasonCode] [tinyint] NOT NULL,
	[IsMaster] [tinyint] NOT NULL,
	[MasterContractId] [nvarchar](16) NULL,
	[Receipt] [varbinary](max) NULL,
 CONSTRAINT [PK_CanceledCscContract] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[CanceledCscContract]  WITH CHECK ADD  CONSTRAINT [FK_CanceledCscContract_CanceledCscContract] FOREIGN KEY([MasterContractId])
REFERENCES [dbo].[CanceledCscContract] ([ID])

ALTER TABLE [dbo].[CanceledCscContract] CHECK CONSTRAINT [FK_CanceledCscContract_CanceledCscContract]

ALTER TABLE [dbo].[CanceledCscContract]  WITH CHECK ADD  CONSTRAINT [FK_CanceledCscContract_DeviceShift] FOREIGN KEY([DeviceShiftId])
REFERENCES [dbo].[DeviceShift] ([ID])

ALTER TABLE [dbo].[CanceledCscContract] CHECK CONSTRAINT [FK_CanceledCscContract_DeviceShift]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (15
           ,CONVERT(datetime, '20230505 18:02:43')
           ,'Added a table: CanceledCscContract')