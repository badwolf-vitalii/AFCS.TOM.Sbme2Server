namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateTscRequestCheckCodeWithStatus = @"UPDATE GESTOWN.TSC_REQUESTS SET CHECKCODE = :CHECKCODE, ISSUEERRORCODE = :ISSUEERRORCODE, OUTPUTSTATUS = :OUTPUTSTATUS WHERE TSCREQID = :TSCREQID";
    }
}
