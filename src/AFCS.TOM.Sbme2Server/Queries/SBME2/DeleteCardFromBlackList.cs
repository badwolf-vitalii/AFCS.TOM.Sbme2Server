namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _deleteCardFromBlackList = @"DELETE FROM TSCBlacklist  
            WHERE SHORTCARDMODEL = :SHORTCARDMODEL 
            AND LASTTSCSERIALNO = :LASTSERIALNO";
    }
}
