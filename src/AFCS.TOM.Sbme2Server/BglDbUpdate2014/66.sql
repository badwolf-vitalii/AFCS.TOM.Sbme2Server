-- 2025y 12m 15d 10:27:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE StaticVariablesList ---------------------------------------------------
ALTER TABLE [dbo].[StaticVariablesList]
ADD [MinAppVersioneAllowed] [nvarchar](20) NULL;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [MinVtsVersioneAllowed] [nvarchar](20) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (66
           ,CONVERT(datetime, '20251215 10:27:43')
           ,'Added new columns to StaticVariablesList: MinAppVersioneAllowed, MinVtsVersioneAllowed.')