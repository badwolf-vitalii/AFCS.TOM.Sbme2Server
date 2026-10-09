namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _getHolderProfiles = @"SELECT * FROM TARIFFOWN.HOLDERPROFILES WHERE HOLDERPROFILEID = :PROFILE1 OR HOLDERPROFILEID = :PROFILE2 OR HOLDERPROFILEID = :PROFILE3";
    }
}
