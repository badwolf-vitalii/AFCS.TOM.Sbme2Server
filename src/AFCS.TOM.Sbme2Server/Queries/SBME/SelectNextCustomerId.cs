namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _nextCustomerId = @"SELECT holderid_seq.NEXTVAL FROM GESTOWN.HOLDERS WHERE ROWNUM <= 1";
    }
}
