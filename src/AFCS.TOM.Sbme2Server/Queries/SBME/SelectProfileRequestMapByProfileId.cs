namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _profileRequestMapByProfileId = @"SELECT
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
            WHERE (profilerequestmap.holderprofileid = :profile1 OR profilerequestmap.holderprofileid = :profile2 OR profilerequestmap.holderprofileid = :profile3) AND (profilerequestmap.sex = :sex OR profilerequestmap.sex = 'X' OR :sex = 'X')";
    }
}
