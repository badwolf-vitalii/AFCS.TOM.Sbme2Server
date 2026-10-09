namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateCardStatus = @"UPDATE GESTOWN.TSC_DOCUMENTS SET STATUS = :STATUS WHERE SHORTCARDMODELID = :SHORTCARDMODELID AND TSCSERIALNO = :TSCSERIALNO AND HOLDERID = :HOLDERID";
    }
}
