namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _getParametersDV = @"SELECT * FROM PARAMETERDV WHERE SERIALNO = :SERIALNO AND SALEDEVICEID = :SALEDEVICEID ORDER BY CODE";// AND DEVICE = :DEVICE ORDER BY CODE";
    }
}
