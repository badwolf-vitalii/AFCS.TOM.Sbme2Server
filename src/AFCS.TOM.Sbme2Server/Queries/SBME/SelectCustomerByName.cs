namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _customerByName = @"SELECT * FROM GESTOWN.HOLDERS WHERE HolderLastName Like :HolderLastName AND HolderFirstNme Like :HolderFirstNme ORDER BY HOLDERID";
    }
}
