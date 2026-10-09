-- 2024y 10m 10d 13:18:53

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE ContactlessCardReissuingReason ---------------------------------------
UPDATE [dbo].[ContactlessCardReissuingReason]
SET Name = 'Smarrimento'
WHERE Code IN (1, 102)
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (55
           ,CONVERT(datetime, '20241010 13:18:53')
           ,'ContactlessCardReissuingReason: Smarimento changed to Smarrimento.')