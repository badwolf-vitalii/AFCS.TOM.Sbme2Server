namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _cardsByHolderID = @"SELECT * FROM GESTOWN.TSC_DOCUMENTS WHERE HOLDERID = :holderID ORDER BY SERIALNO DESC";
    }
}
