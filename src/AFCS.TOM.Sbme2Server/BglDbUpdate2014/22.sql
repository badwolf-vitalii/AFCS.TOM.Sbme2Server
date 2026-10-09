-- 2023y 06m 12d 20:01:23

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- INSERT INTO ArticleType -----------------------------------------------------
INSERT INTO [dbo].[ArticleType] ([Code], [Name])
VALUES (12, 'CardExpirationExtension')
--------------------------------------------------------------------------------

-- CREATE ContactlessCardExpirationExtensionArticleInfo ------------------------
CREATE TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[Article_ID] [uniqueidentifier] NOT NULL,
	[CardType] [numeric](3, 0) NOT NULL,
	[CardSerialNumber] [nvarchar](10) NOT NULL,
	[ShortCardModel] [int] NOT NULL,
	[HolderId] [int] NULL,
	[Description] [nvarchar](32) NULL,
	[OldCardExpirationNullable] [datetime] NULL,
	[NewCardExpirationNullable] [datetime] NULL,
	[CardExpirationExtensionData] [varbinary](max) NULL,
	[OldProfile1Description] [nvarchar](32) NULL,
	[OldProfile2Description] [nvarchar](32) NULL,
	[OldProfile3Description] [nvarchar](32) NULL,
	[NewProfile1Description] [nvarchar](32) NULL,
	[NewProfile2Description] [nvarchar](32) NULL,
	[NewProfile3Description] [nvarchar](32) NULL,
	[OldProfileId1] [int] NULL,
	[OldProfileId2] [int] NULL,
	[OldProfileId3] [int] NULL,
	[NewProfileId1] [int] NULL,
	[NewProfileId2] [int] NULL,
	[NewProfileId3] [int] NULL,
	[OldProfile1EndValidityDate] [datetime] NULL,
	[OldProfile2EndValidityDate] [datetime] NULL,
	[OldProfile3EndValidityDate] [datetime] NULL,
	[NewProfile1EndValidityDate] [datetime] NULL,
	[NewProfile2EndValidityDate] [datetime] NULL,
	[NewProfile3EndValidityDate] [datetime] NULL,
	[Receipt] [varbinary](max) NULL,
 CONSTRAINT [PK_ContactlessCardExpirationExtensionArticleInfo_1] PRIMARY KEY CLUSTERED 
(
	[ID] ASC,
	[Article_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo] ADD  CONSTRAINT [DF_ContactlessCardExpirationExtensionArticleInfo_ShortCardModel]  DEFAULT ((0)) FOR [ShortCardModel]

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_ContactlessCardExpirationExtensionArticleInfo_Article] FOREIGN KEY([Article_ID])
REFERENCES [dbo].[Article] ([ID])

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo] CHECK CONSTRAINT [FK_ContactlessCardExpirationExtensionArticleInfo_Article]

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_ContactlessCardExpirationExtensionArticleInfo_CscType] FOREIGN KEY([CardType])
REFERENCES [dbo].[CscType] ([Code])

ALTER TABLE [dbo].[ContactlessCardExpirationExtensionArticleInfo] CHECK CONSTRAINT [FK_ContactlessCardExpirationExtensionArticleInfo_CscType]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (22
           ,CONVERT(datetime, '20230621 20:01:23')
           ,'Registred a new ArticleType: CardExpirationExtension. Created a table: ContactlessCardExpirationExtensionArticleInfo.')