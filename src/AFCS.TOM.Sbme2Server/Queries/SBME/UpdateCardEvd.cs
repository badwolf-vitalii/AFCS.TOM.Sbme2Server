namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private const string _updateCardEvd = @"UPDATE TSC_DOCUMENTS SET LASTUPDATE = SYSDATE #SPLIT# WHERE SHORTCARDMODELID = :SHORTCARDMODELID AND TSCSERIALNO = :TSCSERIALNO";
    }
}
