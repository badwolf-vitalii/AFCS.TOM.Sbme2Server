-- 2024y 11m 14d 17:46:52

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE VtSellCommitPaymentTypeInfo ------------------------------------------
CREATE TABLE [dbo].[VtSellCommitPaymentTypeInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[ContractUID] [nvarchar](32) NULL,
	[GroupUID] [nvarchar](32) NULL,
	[AmountEuroCent] [int] NULL,
	[FlowType] [int] NULL,
	[PaymentType1] [int] NULL,
 CONSTRAINT [PK_VtSellCommitPaymentTypeInfo] PRIMARY KEY CLUSTERED 
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
           (58
           ,CONVERT(datetime, '20241114 17:46:52')
           ,'Created VtSellCommitPaymentTypeInfo.')