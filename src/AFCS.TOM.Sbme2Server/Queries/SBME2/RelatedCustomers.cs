namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _relatedcustomerByHolderId = @"SELECT A.RelatedHolderId, A.RelationType, A.CreationDate, A.Remarks, B.* FROM #SCHEME_SBME2_GESTOWN#.HOLDERRELATIONS A inner join #SCHEME_SBME2_GESTOWN#.HOLDERS B on A.relatedholderid = B.holderid ";
    }
}
