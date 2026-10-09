-- 2023y 02m 05d 21:53:03

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE PtItemRefundArticleInfo ----------------------------------------------
CREATE TABLE [dbo].[PtItemRefundArticleInfo](
	[Article_ID] [uniqueidentifier] NOT NULL,
	[SaleTransaction_ID] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_PtItemRefundArticleInfo] PRIMARY KEY CLUSTERED 
(
	[Article_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[PtItemRefundArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_PtItemRefundArticleInfo_Article] FOREIGN KEY([Article_ID])
REFERENCES [dbo].[Article] ([ID])

ALTER TABLE [dbo].[PtItemRefundArticleInfo] CHECK CONSTRAINT [FK_PtItemRefundArticleInfo_Article]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (4
           ,CONVERT(datetime, '20230205 21:53:03')
           ,'Created PtItemRefundArticleInfo')