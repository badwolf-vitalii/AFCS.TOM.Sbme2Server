namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _insertContractInBlackList = @"INSERT INTO CONTRACTBLDISTRLIST 
        (
        ISSUINGDATE,
        SALEOPERATORID,
        SALEDEVICEID,
        FIRSTSERIALNO,
        LASTSERIALNO,
        ISSHORTTERMCONTRACT,
        REASONCODE,
        BLSUSPENDED,
        BLINSERTDATE,
        BLADDEDBY,
        INDISTRIBUTION
        )
        VALUES
        (
        :Issuingdate,
        :Saleoperatorid,
        :Saledeviceid,
        :Firstserialno,
        :Lastserialno,
        :Isshorttermcontract,
        :Reasoncode,
        :Blsuspended,
        :Blinsertdate,
        :Bladdedby,
        :Indistribution
        )";
    }
}
