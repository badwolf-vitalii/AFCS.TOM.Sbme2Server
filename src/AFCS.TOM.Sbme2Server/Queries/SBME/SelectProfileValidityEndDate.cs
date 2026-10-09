namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _profileValidityEndDate = @"SELECT
            PROFILEVALIDITYENDDATE,
            HolderProfileID,
            HolderProfileAux1ID,
            HolderProfileAux2ID,
            PROFILEAUX1VALIDITYENDDATE,
            PROFILEAUX2VALIDITYENDDATE
            FROM GESTOWN.TSC_REQUESTS
            WHERE TSCReqID = :TSCReqID";
    }
}
