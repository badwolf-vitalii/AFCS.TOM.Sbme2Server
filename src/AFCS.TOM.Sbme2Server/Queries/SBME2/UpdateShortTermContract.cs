namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        public const string _updateShortTermContracts = @"UPDATE SHORTTERMCONTRACTS SET 
            BLINSERTDATE = SYSDATE,
            BLADDEDBY = :Agentid, 
            BLREASONCODE = :Reasoncode,
            BLSUSPENDED = :Blsuspended  
		WHERE ISSUINGDATE = :Issuingdate
			AND SALEOPERATORID = :Saleoperatorid
			AND SALEDEVICEID = :Saledeviceid 
			AND LASTSERIALNO BETWEEN :Lastserialno AND :Firstserialno";
    }
}
