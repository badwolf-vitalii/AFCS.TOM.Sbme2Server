-- 2023y 02m 06d 10:45:00

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE MagneticArticleInfo --------------------------------------------------
ALTER TABLE [dbo].[MagneticArticleInfo]
ADD [VtContractGroupId] [nvarchar](MAX) NOT NULL DEFAULT '';
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (6
           ,CONVERT(datetime, '20230207 11:55:00')
           ,'Added a column to MagneticArticleInfo: VtContractGroupId')