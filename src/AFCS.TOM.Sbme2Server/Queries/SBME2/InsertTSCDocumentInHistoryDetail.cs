namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _insertTSCDocumentInHistoryDetail = @"INSERT INTO TscDocumentDetailsHistory 
        (
        ModificationDate,
        ReasonCode,
        ShortCardModel,
        TSCSerialNo,
        SaleOperatorId, 
        AttributeCode,
        AttribCharValue,
        AttribIntValue,
        AttribDateValue
        ) 
        SELECT SYSDATE, :reasonCode ,ShortCardModel, TSCSerialNo, SaleOperatorId, 
            AttributeCode, AttribCharValue, AttribIntValue, AttribDateValue  
        FROM TscDocumentDetails
        WHERE SHORTCARDMODEL = :shortCardModel AND TSCSERIALNO = :tscSerialNo";
    }
}
