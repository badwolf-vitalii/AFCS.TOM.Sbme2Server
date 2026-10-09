-- 2023y 10m 27d 13:14:13

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE CanceledCscContract --------------------------------------------------
ALTER TABLE [dbo].[CanceledCscContract]
ADD [History] [nvarchar](MAX) NULL;

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (28
           ,CONVERT(datetime, '20231027 13:14:13')
           ,'Added a new column to CanceledCscContract: History.')