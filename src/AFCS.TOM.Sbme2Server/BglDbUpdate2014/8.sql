-- 2023y 02m 21d 09:56:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- DROP PtItemArticleInfo ------------------------------------------------------
DROP TABLE [dbo].[PtItemArticleInfo]
--------------------------------------------------------------------------------

-- UPDATE PtItemArticleInfo ----------------------------------------------------
CREATE TABLE [dbo].[PtItemArticleInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[Article_ID] [uniqueidentifier] NOT NULL,
	[ItemId] [int] NOT NULL,
	[ItemCode] [int] NOT NULL,
	[GraphicsVersion] [int] NOT NULL,
	[Series] [nvarchar](max) NULL,
	[SerialStart] [numeric](10, 0) NOT NULL,
	[SerialEnd] [numeric](10, 0) NOT NULL,
 CONSTRAINT [PK_PtItemArticleInfo] PRIMARY KEY CLUSTERED 
(
	[ID] ASC,
	[Article_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[PtItemArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_PtItemArticleInfo_Article] FOREIGN KEY([Article_ID])
REFERENCES [dbo].[Article] ([ID])

ALTER TABLE [dbo].[PtItemArticleInfo] CHECK CONSTRAINT [FK_PtItemArticleInfo_Article]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (8
           ,CONVERT(datetime, '20230221 09:56:43')
           ,'PtItemArticleInfo has ben recreated with new columns')