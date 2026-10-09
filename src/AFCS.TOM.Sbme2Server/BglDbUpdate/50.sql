-- 2024y 10m 02d 13:24:25

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE PtBankTransferInfo ---------------------------------------------------
CREATE TABLE [dbo].[PtBankTransferInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[SaleTransaction_ID] [uniqueidentifier] NOT NULL,
	[BankTransferNumber] [nvarchar](15) NOT NULL,
	[Amount] [decimal](9, 0) NOT NULL,
	[Abi] [nvarchar](15) NOT NULL,
	[Cab] [nvarchar](15) NOT NULL,
	[AbiCompanyName] [nvarchar](100) NULL,
	[AbiDescription] [nvarchar](100) NULL,
	[CabCompany] [nvarchar](100) NULL,
	[CabAddress] [nvarchar](100) NULL,
	[CabLocality] [nvarchar](100) NULL,
	[CabCity] [nvarchar](100) NULL,
	[CabZip] [nvarchar](100) NULL,
	[CabProvince] [nvarchar](100) NULL,
 CONSTRAINT [PK_PtBankTransferInfo] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PtBankTransferInfo]  WITH CHECK ADD  CONSTRAINT [FK_PtBankTransferInfo_SaleTransaction] FOREIGN KEY([SaleTransaction_ID])
REFERENCES [dbo].[SaleTransaction] ([ID])

ALTER TABLE [dbo].[PtBankTransferInfo] CHECK CONSTRAINT [FK_PtBankTransferInfo_SaleTransaction]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (50
           ,CONVERT(datetime, '20241002 13:24:25')
           ,'Added a new table: PtBankTransferInfo.')