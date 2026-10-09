namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _mediaPhSn = @"SELECT TSCSERIALNO as SerialNumber, SHORTCARDMODEL as ShortCardModel FROM #SCHEME_SBME2_GESTOWN#.TSCDOCUMENTS WHERE SMARTCARDSN=:serial AND (:saledevice = 0 OR ISSUINGSALEDEVICEID=:saledevice)";
    }
}
