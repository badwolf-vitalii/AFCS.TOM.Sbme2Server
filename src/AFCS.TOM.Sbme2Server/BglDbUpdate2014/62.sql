-- 2025y 07m 11d 16:43:53

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE DeviceShift ----------------------------------------------------------
ALTER TABLE [dbo].[DeviceShift]
ADD [PtCodiceRivendita] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [PtCodiceEsattoria] [int] NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [AppVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [DsdeVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [VtsServerVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [PluginVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [VTokenStreamerVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [BootstrapperVersion] [nvarchar](50) NULL;

ALTER TABLE [dbo].[DeviceShift]
ADD [TpfVersion] [nvarchar](50) NULL;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (62
           ,CONVERT(datetime, '20250711 16:43:53')
           ,'Added new columns to DeviceShift: PtCodiceRivendita, PtCodiceEsattoria, AppVersion, DsdeVersion, VtsServerVersion, PluginVersion, VTokenStreamerVersion, BootstrapperVersion, TpfVersion.')