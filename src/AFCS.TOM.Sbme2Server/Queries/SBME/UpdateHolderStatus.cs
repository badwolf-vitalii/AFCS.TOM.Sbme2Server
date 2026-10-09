namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateHolderStatus = @"UPDATE GESTOWN.HOLDERS SET STATUS = :STATUS WHERE HOLDERID = :HOLDERID AND STATUS != :STATUS";
    }
}
