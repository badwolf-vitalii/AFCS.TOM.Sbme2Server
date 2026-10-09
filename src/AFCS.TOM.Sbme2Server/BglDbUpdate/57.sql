-- 2024y 11m 14d 17:45:41

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE VtSellCommitInfo -----------------------------------------------------
CREATE TABLE [dbo].[VtSellCommitInfo](
	[ID] [uniqueidentifier] NOT NULL,
	[InsertDate] [datetime] NOT NULL,
	[RequestResult] [int] NULL,
	[CommitResult] [int] NULL,
	[VtTransactionUid] [int] NULL,
	[ContractUID] [nvarchar](32) NULL,
	[ContractData] [varbinary](max) NULL,
	[GroupUID] [nvarchar](32) NULL,
	[VToken] [varbinary](max) NULL,
 CONSTRAINT [PK_VtSellCommitInfo] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (57
           ,CONVERT(datetime, '20241114 17:45:41')
           ,'Created VtSellCommitInfo.')