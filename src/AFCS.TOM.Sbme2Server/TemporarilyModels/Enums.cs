namespace AFCS.TOM.Sbme2Server.TemporarilyModels.Enums
{
    public enum ReasonCode
    {
        t2c_BLResCode_NONE = 0,
        t2c_BLResCode_Stolen = 1, /* The card has been stolen */
        t2c_BLResCode_Lost = 2, /* The card has been lost */
        t2c_BLResCode_Broken = 3, /* The card has been broken */
        t2c_BLResCode_Renewal = 4, /* The card has been renewed */
        t2c_BLResCode_Antifraud = 5 /* Result of antifraud checks */
    }

    public enum CTRReasonCode
    {
        t2c_CtrBLReasCode_NONE = 0,
        t2c_CtrBLReasCode_SAMStolen = 1, /* The SAM that issued the contract was stolen */
        t2c_CtrBLReasCode_Antifraud = 5 /* Result of antifraud checks */
    }
}
