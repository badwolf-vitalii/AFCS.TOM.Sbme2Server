namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    [Serializable]
    public class ContractBlackList
    {
        public BlackList BlackList { get; set; }
        public DateTime IssuingDate { get; set; }
        public int SaleDeviceId { get; set; }
    }
}
