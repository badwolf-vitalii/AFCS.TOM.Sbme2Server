namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _resetMediaBl = @"
			UPDATE #SCHEME_SBME2_GESTOWN#.TSCDOCUMENTS SET
			BLREASONCODE = NULL,
			BLINSERTDATETIME = NULL
			WHERE NVL(LTRIM(TSCSERIALNO, '0'), '0') =
				  NVL(LTRIM(:sn, '0'), '0')
			AND   SHORTCARDMODEL = :scm";
    }
}
