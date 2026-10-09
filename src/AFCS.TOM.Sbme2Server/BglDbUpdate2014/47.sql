-- 2024y 07m 17d 21:15:24

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE PtConfirmTransaction -------------------------------------------------
CREATE TABLE [dbo].[PtConfirmTransaction](
	[ID] [uniqueidentifier] NOT NULL,
	[RequestFailed] [tinyint] NOT NULL,
	[RequestSendTime] [datetime] NOT NULL,
	[SupplierID] [nvarchar](50) NOT NULL,
	[AgentID] [int] NOT NULL,
	[ShiftNumber] [int] NOT NULL,
	[NbShiftDay] [int] NULL,
	[NbShiftAgent] [int] NULL,
	[LocalityID] [int] NOT NULL,
	[TransactionNumber] [int] NOT NULL,
	[CodiceRivendita] [nvarchar](50) NULL,
	[CodiceEsattoria] [int] NOT NULL,
	[DtaVendita] [nvarchar](50) NOT NULL,
	[CodiceClienteFatturaElettronica] [nvarchar](50) NULL,
	[NamePC] [nvarchar](50) NULL,
	[DtaVersamento] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_PtConfirmTransaction] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

-- CREATE PtConfirmTransactionPayment ------------------------------------------
CREATE TABLE [dbo].[PtConfirmTransactionPayment](
	[ID] [uniqueidentifier] NOT NULL,
	[PtConfirmTransaction_ID] [uniqueidentifier] NOT NULL,
	[PaymentTypePT] [int] NULL,
	[TipoAssegno] [nvarchar](50) NULL,
	[NAssegno] [nvarchar](50) NULL,
	[NBonifico] [nvarchar](50) NULL,
	[Banca] [nvarchar](50) NULL,
	[Piazza] [nvarchar](50) NULL,
	[EuroCentAmount] [int] NOT NULL,
	[CdValuta] [nvarchar](50) NULL,
	[NContoCorrente] [nvarchar](50) NULL,
	[Abi] [nvarchar](50) NULL,
	[Cab] [nvarchar](50) NULL,
 CONSTRAINT [PK_PtConfirmTransactionPayment] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PtConfirmTransactionPayment]  WITH CHECK ADD  CONSTRAINT [FK_PtConfirmTransactionPayment_PtConfirmTransaction] FOREIGN KEY([PtConfirmTransaction_ID])
REFERENCES [dbo].[PtConfirmTransaction] ([ID])

ALTER TABLE [dbo].[PtConfirmTransactionPayment] CHECK CONSTRAINT [FK_PtConfirmTransactionPayment_PtConfirmTransaction]
--------------------------------------------------------------------------------

-- CREATE PtConfirmTransactionProduct ------------------------------------------
CREATE TABLE [dbo].[PtConfirmTransactionProduct](
	[ID] [uniqueidentifier] NOT NULL,
	[PtConfirmTransaction_ID] [uniqueidentifier] NOT NULL,
	[MatricolaFamiliare] [int] NULL,
	[ProductPaymentTypePT] [nvarchar](50) NOT NULL,
	[TypeOfSale] [int] NOT NULL,
	[MasterTariff] [int] NULL,
	[Tariff_Sbme] [int] NULL,
	[Tariff_PT] [int] NULL,
	[NumeroDiSerie] [nvarchar](50) NULL,
	[CdVersioneGrafica] [int] NULL,
	[NumberOfSemizones] [int] NULL,
	[TarifficationType] [int] NULL,
	[ReasonCode] [int] NULL,
	[ShortCardModelID] [int] NULL,
	[SaleDeviceID] [int] NOT NULL,
	[SerialNo] [numeric](20, 0) NULL,
	[TscSerialNo] [numeric](20, 0) NULL,
	[FirstSerialNo] [numeric](20, 0) NULL,
	[LastSerialNo] [numeric](20, 0) NULL,
	[NumberOfUnits] [int] NULL,
	[EuroCentPrice] [int] NOT NULL,
	[TotalEuroCentPrice] [int] NOT NULL,
	[CdValuta] [nvarchar](50) NULL,
	[EuroCentBonus] [int] NULL,
	[TSCValidityEndDate] [nvarchar](50) NULL,
	[ValidityStartDate] [nvarchar](50) NULL,
	[ValidityEndDate] [nvarchar](50) NULL,
	[ValidityLimitDate] [nvarchar](50) NULL,
	[IssuingDate] [nvarchar](50) NULL,
 CONSTRAINT [PK_PtConfirmTransactionProduct] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PtConfirmTransactionProduct]  WITH CHECK ADD  CONSTRAINT [FK_PtConfirmTransactionProduct_PtConfirmTransaction] FOREIGN KEY([PtConfirmTransaction_ID])
REFERENCES [dbo].[PtConfirmTransaction] ([ID])

ALTER TABLE [dbo].[PtConfirmTransactionProduct] CHECK CONSTRAINT [FK_PtConfirmTransactionProduct_PtConfirmTransaction]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (47
           ,CONVERT(datetime, '20240717 21:15:24')
           ,'Added new tables: PtConfirmTransaction, PtConfirmTransactionPayment, PtConfirmTransactionProduct.')