namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _signature = @"SELECT
            ATTACHMENT
            FROM #SCHEME_SBME2_GESTOWN#.ATTACHMENTS
            WHERE ATTACHEMENTID = :ATTACHEMENTID";
    }
}
