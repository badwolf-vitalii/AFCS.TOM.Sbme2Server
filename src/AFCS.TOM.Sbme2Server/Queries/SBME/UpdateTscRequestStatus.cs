namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateTscRequestStatus = @"UPDATE GESTOWN.TSC_REQUESTS SET OUTPUTSTATUS = :STATUS WHERE TSCREQID = :TSCREQID";
    }
}
