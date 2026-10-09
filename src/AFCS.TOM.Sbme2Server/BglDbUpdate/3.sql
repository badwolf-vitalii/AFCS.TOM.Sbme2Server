-- 2023y 02m 05d 21:53:02

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE PtItemArticleInfo ----------------------------------------------------
CREATE TABLE [dbo].[PtItemArticleInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[Article_ID] [uniqueidentifier] NOT NULL,
	[ItemId] [int] NOT NULL,
	[SerialStart] [numeric](10, 0) NOT NULL,
	[SerialEnd] [numeric](10, 0) NOT NULL,
 CONSTRAINT [PK_PtItemArticleInfo] PRIMARY KEY CLUSTERED 
(
	[ID] ASC,
	[Article_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PtItemArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_PtItemArticleInfo_Article] FOREIGN KEY([Article_ID])
REFERENCES [dbo].[Article] ([ID])

ALTER TABLE [dbo].[PtItemArticleInfo] CHECK CONSTRAINT [FK_PtItemArticleInfo_Article]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (3
           ,CONVERT(datetime, '20230205 21:53:02')
           ,'Created PtItemArticleInfo')