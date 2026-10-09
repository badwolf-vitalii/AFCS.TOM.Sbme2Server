namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        // not used
        private const string _profileRequestByProviderId =
            @"SELECT A.*
            FROM profilerequestcodes A
            INNER JOIN profilerequestmap B
            ON A.profilerequestcodeid = B.profilerequestcodeid
                INNER JOIN holderprofiles C
                ON B.holderprofileid = C.holderprofileid
                WHERE C.providerid = :providerid
            ORDER BY A.profilerequestcodedescr";

        private static string _profileRequestsByProviderIdWithMaps { get; } =
            @"SELECT A.profilerequestcodeid, A.profilerequestcodedescr, B.holderprofileid, B.profilerequestcodetype, C.SALEAGENTLISTID
            FROM TARIFFOWN.profilerequestCODES A
            INNER JOIN TARIFFOWN.profilerequestMAP B
                ON A.profilerequestcodeid = b.profilerequestcodeid
            INNER JOIN holderprofiles C
                ON B.holderprofileid = C.holderprofileid
            WHERE (B.SEX = :SEX OR B.SEX = 'X' OR B.SEX = 'x') AND (B.profilerequestcodetype = 'P' OR B.profilerequestcodetype = 'p') AND C.PROFILETYPE = 'U'
            ORDER BY B.holderprofileid";
    }
}
