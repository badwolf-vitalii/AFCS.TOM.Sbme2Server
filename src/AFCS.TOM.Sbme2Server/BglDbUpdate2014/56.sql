-- 2024y 11m 14d 17:44:20

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE VtSellContractInfo ---------------------------------------------------
CREATE TABLE [dbo].[VtSellContractInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[InsertDate] [datetime] NOT NULL,
	[SaleTransactionId] [uniqueidentifier] NULL,
	[Result] [int] NULL,
	[VtTransactionUid] [int] NULL,
	[CardInfoIn] [varbinary](max) NULL,
	[TransactionUid] [nvarchar](32) NULL,
	[NumSaleUnits] [int] NULL,
	[PassengerClass] [int] NULL,
	[OriginId] [int] NULL,
	[DestinationId] [int] NULL,
	[LineId] [int] NULL,
	[Distance] [int] NULL,
	[RealDistance] [int] NULL,
	[Via1] [int] NULL,
	[Via2] [int] NULL,
	[DocumentType] [nvarchar](32) NULL,
	[PriceComponents] [nvarchar](32) NULL,
	[LimitValidityDateTime] [datetime] NULL,
	[EndValidityDateTime] [datetime] NULL,
	[StartValidityDateTime] [datetime] NULL,
	[ContractTypeDescription] [nvarchar](32) NULL,
	[ContractDataExpiration] [datetime] NULL,
	[ContractData] [varbinary](max) NULL,
	[ContractUid] [nvarchar](32) NULL,
	[GroupUid] [nvarchar](32) NULL,
	[ReceiptType] [nvarchar](32) NULL,
	[PaymentType] [int] NULL,
	[AmountEuroCent] [int] NULL,
	[SellProposalId] [nvarchar](32) NULL,
 CONSTRAINT [PK_VtSellContractInfo] PRIMARY KEY CLUSTERED 
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
           (56
           ,CONVERT(datetime, '20241114 17:44:20')
           ,'Created VtSellContractInfo.')