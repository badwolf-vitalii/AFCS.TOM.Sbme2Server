-- 2023y 05m 25d 12:23:27

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE PtItemType -----------------------------------------------------------
CREATE TABLE [dbo].[PtItemType](
	[Code] [numeric](5, 0) NOT NULL,
	[Name] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK_PtItemType] PRIMARY KEY CLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
--------------------------------------------------------------------------------

-- INSERT INTO PtItemType ------------------------------------------------------
INSERT INTO [dbo].[PtItemType] ([Code], [Name])
VALUES (1, 'Sale')

INSERT INTO [dbo].[PtItemType] ([Code], [Name])
VALUES (2, 'Substitution')
--------------------------------------------------------------------------------

-- UPDATE PtItemArticleInfo ----------------------------------------------------
ALTER TABLE [dbo].[PtItemArticleInfo]
ADD [ItemType] [numeric](5, 0) NULL;

ALTER TABLE [dbo].[PtItemArticleInfo]  WITH CHECK ADD  CONSTRAINT [FK_PtItemArticleInfo_PtItemType] FOREIGN KEY([ItemType])
REFERENCES [dbo].[PtItemType] ([Code])

ALTER TABLE [dbo].[PtItemArticleInfo] CHECK CONSTRAINT [FK_PtItemArticleInfo_PtItemType]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (17
           ,CONVERT(datetime, '20230525 12:23:27')
           ,'Created a new table: PtItemType. Added a column to PtItemArticleInfo: ItemType.')