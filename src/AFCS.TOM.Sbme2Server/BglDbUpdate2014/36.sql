-- 2024y 02m 16d 17:06:04

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- UPDATE StaticVariablesList ---------------------------------------------------
ALTER TABLE [dbo].[StaticVariablesList]
ADD [AdminBlock] [tinyint] NOT NULL DEFAULT 0;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [MaxSalesThreshold] [decimal](10, 0) NOT NULL DEFAULT 2000000;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [WarningSalesThreshold] [decimal](10, 0) NOT NULL DEFAULT 100000;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [CurrentResidual] [decimal](10, 0) NOT NULL DEFAULT 0;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [IsResidualVirgin] [tinyint] NOT NULL DEFAULT 1;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [AutoCloseShiftWhenBlockingSaleOperations] [tinyint] NOT NULL DEFAULT 1;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [AutoCloseBasketWhenBlockingSaleOperations] [tinyint] NOT NULL DEFAULT 1;

ALTER TABLE [dbo].[StaticVariablesList]
ADD [DaysOfflineBeforeAutoblockingSaleOperations] [tinyint] NOT NULL DEFAULT 10;
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (36
           ,CONVERT(datetime, '20240216 17:06:04')
           ,'Added new columns to StaticVariablesList: AdminBlock, MaxSalesThreshold, WarningSalesThreshold, CurrentResidual, IsResidualVirgin, AutoCloseShiftWhenBlockingSaleOperations, AutoCloseBasketWhenBlockingSaleOperations, DaysOfflineBeforeAutoblockingSaleOperations')