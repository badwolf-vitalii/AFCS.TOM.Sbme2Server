namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _updateTSCDocument = @"UPDATE TSCDocuments 
		SET BLADDEDBY = :agentID,
            BLREASONCODE = :reasonCode,
		    BLSUSPENDED = :suspended,
		    LASTUPDATE = SYSDATE,
		    BLINSERTDATETIME = SYSDATE
		WHERE SHORTCARDMODEL = :shortCardModel AND TSCSERIALNO = :lastSerialNo";

        private const string _updateTSCDocumentProofDoc = @"UPDATE TSCDocuments 
		SET PROOFDOCSN = :proofDocSn
		WHERE SHORTCARDMODEL = :shortCardModel AND TSCSERIALNO = :lastSerialNo";
    }
}
