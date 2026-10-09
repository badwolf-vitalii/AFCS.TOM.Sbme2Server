namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _insertTSCDocumentInBlackList = @"INSERT INTO TSCBlacklist 
			(
            SHORTCARDMODEL,
            SALEOPERATORID,
            LASTTSCSERIALNO,
            FIRSTTSCSERIALNO,
            INSERTDATE,
            REASONCODE,
            BLADDEDBY,
            BLSUSPENDED,
            INDISTRIBUTION
            ) 
			VALUES 
            (
            :shortCardModel,
            :saleOperatorID,
            :lastSerialNo,
            :firstSerialNo,
            SYSDATE,
            :reasonCode,
            :agentID,
            :suspended,
            :insertDistr
            )";
    }
}
