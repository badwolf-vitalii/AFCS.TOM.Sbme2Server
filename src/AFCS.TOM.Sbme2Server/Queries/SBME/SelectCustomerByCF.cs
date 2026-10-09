namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _customerByCF = @"SELECT * FROM GESTOWN.HOLDERS WHERE HOLDERFISCALCODE Like :HOLDERFISCALCODE ORDER BY HOLDERID";
    }
}
