-- 2024y 11m 14d 17:48:04

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE VtSellTransactionPaymentTypeInfo -------------------------------------
CREATE TABLE [dbo].[VtSellTransactionPaymentTypeInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[InsertDate] [datetime] NOT NULL,
	[SaleTransactionId] [uniqueidentifier] NULL,
	[VtTransactionUid] [int] NULL,
	[AmountEuroCent] [int] NULL,
	[FlowType] [int] NULL,
	[PaymentType1] [int] NULL,
 CONSTRAINT [PK_VtSellTransactionPaymentTypeInfo] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (59
           ,CONVERT(datetime, '20241114 17:48:04')
           ,'Created VtSellTransactionPaymentTypeInfo.')