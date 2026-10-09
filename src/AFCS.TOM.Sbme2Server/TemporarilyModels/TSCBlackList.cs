namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    [Serializable]
    public class TSCBlackList
    {
        public int ShortCardModel { get; set; }
        public string LastTscSerialNo { get; set; }
        public int SaleOperatorId { get; set; }
        public string FirstTscSerialNo { get; set; }
        public DateTime InsertDate { get; set; }
        public int ReasonCode { get; set; }
        public int? ImpSessionId { get; set; }
        public int? ToBeBurnedCounter { get; set; }
        public string? TpfStatus { get; set; }
        public int? BlackListAddedBy { get; set; }
        public int? BlackListSuspended { get; set; }
        public int? BlackListBurnOperatorId { get; set; }
        public int? BlackListBurnDeviceClassId { get; set; }
        public int? BlackListBurnDeviceClassCode { get; set; }
        public DateTime? BlackListBurnDateTime { get; set; }
        public int? Indistribution { get; set; }
        public int? ActivityId { get; set; }
    }

}
