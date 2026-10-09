namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _updateMediaStatus = @"
			UPDATE #SCHEME_SBME2_GESTOWN#.TSCDOCUMENTS SET
			TSCSTATUS = :status
			WHERE NVL(LTRIM(TSCSERIALNO, '0'), '0') =
				  NVL(LTRIM(:sn, '0'), '0')
			AND   SHORTCARDMODEL = :scm";
    }
}
