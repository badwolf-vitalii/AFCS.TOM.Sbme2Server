namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _selectTSCDocument = @"SELECT SHORTCARDMODEL, SALEOPERATORID, LASTTSCSERIALNO 
		FROM TSCBlacklist 
        WHERE SHORTCARDMODEL = :shortCardModel AND LASTTSCSERIALNO = :lastSerialNo";
    }
}
