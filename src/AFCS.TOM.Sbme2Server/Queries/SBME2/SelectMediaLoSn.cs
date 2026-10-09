namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _mediaLoSn = @"SELECT SMARTCARDSN as SerialNumber, ISSUINGSALEDEVICEID as SaleDeviceId FROM #SCHEME_SBME2_GESTOWN#.TSCDOCUMENTS WHERE TSCSERIALNO=:serial AND (:shortcardmodel = 0 OR SHORTCARDMODEL=:shortcardmodel)";
    }
}
