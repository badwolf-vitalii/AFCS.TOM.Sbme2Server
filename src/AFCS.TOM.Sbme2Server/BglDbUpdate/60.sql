-- 2024y 11m 14d 17:41:50

SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

-- CREATE INDEX ----------------------------------------------------------------
CREATE INDEX index_GroupUID ON VtSellCommitInfo (ID)
CREATE INDEX index_ID ON CscContractRefundArticleInfo (Article_ID)
CREATE INDEX index_ID ON CscContractArticleInfo (ID)
CREATE INDEX index_ID ON Article (ID)
CREATE INDEX index_ID ON PaymentDetail (ID)
CREATE INDEX index_ID ON SaleTransaction (ID)
CREATE INDEX index_ID ON DeviceShift (ID)
CREATE INDEX index_ID ON AgentShift (ID)
CREATE INDEX index_ID ON AccountingPeriod (ID)
--------------------------------------------------------------------------------

INSERT INTO [dbo].[DatabaseInfo]
           ([Version]
           ,[LastModified]
           ,[ChangeLog])
     VALUES
           (60
           ,CONVERT(datetime, '20241114 17:41:50')
           ,'Created some indices.')