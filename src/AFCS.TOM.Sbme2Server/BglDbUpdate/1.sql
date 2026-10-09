-- 2023y 02m 05d 21:53:00

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE DatabaseInfo ---------------------------------------------------------
CREATE TABLE [dbo].[DatabaseInfo](
	[Version] [int] NOT NULL,
	[LastModified] [datetime] NULL,
	[ChangeLog] [nvarchar](max) NULL,
 CONSTRAINT [PK_DatabaseInfo] PRIMARY KEY CLUSTERED 
(
	[Version] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (1
           ,CONVERT(datetime, '20230205 21:53:00')
           ,'Created DatabaseInfo')