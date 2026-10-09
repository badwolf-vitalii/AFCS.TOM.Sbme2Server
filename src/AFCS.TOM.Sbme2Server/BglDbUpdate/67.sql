-- 2025y 12m 15d 10:27:43

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE StaticVariablesList ---------------------------------------------------
UPDATE [dbo].[StaticVariablesList]
SET [MinAppVersioneAllowed] = '2.8.1.153'

UPDATE [dbo].[StaticVariablesList]
SET [MinVtsVersioneAllowed] = '2.2.0.219'
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (67
           ,CONVERT(datetime, '20251215 10:27:43')
           ,'Setting up the MinAppVersioneAllowed and MinVtsVersioneAllowed.')