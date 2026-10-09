-- 2023y 07m 14d 10:14:25

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE SbmeProfilePriceMapping ----------------------------------------------
CREATE TABLE [dbo].[SbmeProfilePriceMapping](
	[ProfileId] [int] NOT NULL,
	[IssuingReasonCode] [numeric](3, 0) NOT NULL,
	[IssuingPrice] [decimal](10, 0) NOT NULL,
 CONSTRAINT [PK_SbmeProfilePriceMapping] PRIMARY KEY CLUSTERED 
(
	[ProfileId] ASC,
	[IssuingReasonCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[SbmeProfilePriceMapping] ADD  CONSTRAINT [DF_SbmeProfilePriceMapping_IssuingReasonCode]  DEFAULT ((0)) FOR [IssuingReasonCode]
--------------------------------------------------------------------------------

-- INSERT INTO SbmeProfilePriceMapping -----------------------------------------
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (0, 1, 1500)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (0, 2, 1500)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (0, 3, 0)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (2, 1, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (2, 2, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (69, 1, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (69, 2, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (70, 1, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (70, 2, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (98, 1, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (98, 2, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (100, 1, 1000)
INSERT INTO [dbo].[SbmeProfilePriceMapping] ([ProfileId], [IssuingReasonCode], [IssuingPrice])
VALUES (100, 2, 1000)
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (25
           ,CONVERT(datetime, '20230714 10:14:25')
           ,'Created a table: SbmeProfilePriceMapping.')