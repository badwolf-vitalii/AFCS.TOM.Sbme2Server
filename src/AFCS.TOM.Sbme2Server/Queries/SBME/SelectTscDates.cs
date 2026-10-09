namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _tscDates = @"SELECT IssuingDate, TSCValidityEndDate FROM GESTOWN.TSC_REQUESTS WHERE TSCReqID = :TSCReqID";
    }
}
