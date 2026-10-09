namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _getParametersSV_RV = @"SELECT * FROM PARAMETERSV_RV WHERE SERIALNO = :SERIALNO AND SALEDEVICEID = :SALEDEVICEID ORDER BY CODE";// AND DEVICE = :DEVICE ORDER BY CODE";
    }
}
