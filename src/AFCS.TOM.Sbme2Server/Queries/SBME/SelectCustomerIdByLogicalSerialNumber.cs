namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _customerIdByLogicalSerialNumber = @"SELECT A.holderid FROM GESTOWN.holders A INNER JOIN GESTOWN.tsc_documents B ON A.HolderID = B.holderid WHERE B.saledeviceid = :saledeviceid AND B.serialno = :serial AND (shortcardmodelid = :shortcardmodelid OR :shortcardmodelid = 0) AND rownum = 1 ORDER BY HOLDERID";
    }
}
