-- 2024y 01m 11d 11:42:41

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE StaticVariablesList --------------------------------------------------
CREATE TABLE [dbo].[StaticVariablesList](
	[DeviceIdentifier] [nvarchar](10) NOT NULL,
	[PreventSaleOperations] [tinyint] NOT NULL,
 CONSTRAINT [PK_StaticVariablesList] PRIMARY KEY CLUSTERED 
(
	[DeviceIdentifier] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

ALTER TABLE [dbo].[StaticVariablesList] ADD  CONSTRAINT [DF_StaticVariables_PreventSaleOperations]  DEFAULT ((0)) FOR [PreventSaleOperations]
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (30
           ,CONVERT(datetime, '20240111 11:42:41')
           ,'Created StaticVariablesList')