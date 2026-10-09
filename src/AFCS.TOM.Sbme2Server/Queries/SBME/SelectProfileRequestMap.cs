namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _profileRequestMap = @"SELECT
            profilerequestmap.profilerequestcodeid,
            profilerequestmap.sex,
            profilerequestmap.holderprofileid,
            profilerequestmap.profilerequestcodetype,
            holderprofiles.holderprofilelongname,
            holderprofiles.providerid,
            holderprofiles.saleagentlistid,
            holderprofiles.tscvalidityduration,
            holderprofiles.tscvaliditylimit,
            holderprofiles.profilevalidityduration,
            holderprofiles.profilevaliditylimit,
            holderprofiles.profilevaliditylimitage,
            holderprofiles.profileyoungvaldur,
            holderprofiles.profileyoungage,
            holderprofiles.issueprice,
            holderprofiles.renewprice
            FROM TARIFFOWN.profilerequestmap INNER JOIN TARIFFOWN.holderprofiles ON profilerequestmap.holderprofileid = holderprofiles.holderprofileid 
            WHERE profilerequestmap.profilerequestcodeid = :profilerequestcodeid ORDER BY profilerequestmap.profilerequestcodetype";
    }
}
