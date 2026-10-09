namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        // not used
        private static string _profileRequestID { get; } = @"SELECT * FROM TARIFFOWN.PROFILEREQUESTCODES";
        
        private static string _profileRequestsWithMaps { get; } =
            @"SELECT A.profilerequestcodeid, A.profilerequestcodedescr, B.sex, B.holderprofileid, B.profilerequestcodetype
            FROM TARIFFOWN.profilerequestCODES A
            INNER JOIN TARIFFOWN.profilerequestMAP B
                ON A.profilerequestcodeid = b.profilerequestcodeid
            ORDER BY B.holderprofileid";
    }
}
