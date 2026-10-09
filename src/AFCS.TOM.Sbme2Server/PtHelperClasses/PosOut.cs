namespace AFCS.TOM.Plugin.Interface.Models;

[Serializable]
public class PosOut
{
    [Serializable]
    public enum CardTypes : byte
    {
        CreditCard = 1,
        Bancomat,
        Others
    }

    [Serializable]
    public struct TPOSData
    {
        public string TerminalId { get; set; }
        public string InfoRelease { get; set; }
        public string StatoPos { get; set; }
        public string Trk1 { get; set; }
        public string EsitoLetturaTrk1 { get; set; }
        public string Trk2 { get; set; }
        public string EsitoLetturaTrk2 { get; set; }
        public string Ticket { get; set; }
        public string AmountEcho { get; set; }
        public string DataTrs { get; set; }
        public string ActionCode { get; set; }
        public string PreauthorizationCode { get; set; }
        public string AmountAuth { get; set; }
        public string OperationNumber { get; set; }
        public string AuthorizationCode { get; set; }
        public string PAN { get; set; }
        public string BankBalance { get; set; }
        public string POSBalance { get; set; }
        public string STAN { get; set; }
        public string CardType { get; set; }
        public string KODescription { get; set; }
        public string TransactionResult { get; set; }
        public string TransactionType { get; set; }
        public string AcquirerId { get; set; }
        public string DatiAggiuntiviTagDaGt { get; set; }
    }

    public byte CommErrorCode { get; set; }
    public bool TransactionResult { get; set; }
    public string TransactionResultCode { get; set; }
    public string TransactionResultErrorDescription { get; set; }
    public CardTypes CardType { get; set; }
    public string TokenToSaveInDB { get; set; }
    public TPOSData PosData { get; set; }
    public string PosVersion { get; set; }
}