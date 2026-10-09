namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    [Serializable]
    public class BlackList
    {      
        public int SaleOperatorId { get; set; }
        public string FirstSerialNo { get; set; }
        public string LastSerialNo { get; set; }
        public int IsShortTermContract { get; set; }
        public int? ReasonCode { get; set; }
        public int BlackListSuspended { get; set; }
        public DateTime BlackListInsertDate { get; set; }
        public int ShortCardModel { get; set; }
        public int AgentId { get; set; }
        public int Indistribution { get; set; }
    }
}
