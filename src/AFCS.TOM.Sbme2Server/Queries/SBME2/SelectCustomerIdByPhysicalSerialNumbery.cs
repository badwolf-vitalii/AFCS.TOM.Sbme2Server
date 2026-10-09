namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _customerIdByPhysicalSerialNumber = @"SELECT A.holderid FROM #SCHEME_SBME2_GESTOWN#.holders A INNER JOIN #SCHEME_SBME2_GESTOWN#.tscdocuments B ON A.HolderID = B.holderid WHERE (B.TSCSTATUS IS NULL OR B.TSCSTATUS != 4) AND B.TSCSERIALNO = :serial AND (SHORTCARDMODEL = :shortcardmodelid OR :shortcardmodelid = 0) AND rownum = 1";
    }
}
