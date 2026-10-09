namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateTscRequestCheckCode = @"UPDATE GESTOWN.TSC_REQUESTS SET CHECKCODE = :CHECKCODE, ISSUEERRORCODE = :ISSUEERRORCODE WHERE TSCREQID = :TSCREQID";
    }
}
