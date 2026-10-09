namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _getTDSDEContracts = @"SELECT * FROM T_DSDE_CONTRACTS WHERE SHORTCARDMODELID = :SHORTCARDMODELID AND TSCSERIALNO = :TSCSERIALNO";// AND DEVICE = :DEVICE";
    }
}
