using AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.TemporarilyModels
{
    [Serializable]
    public class CscContractArticleDetails
    {
        public CscContractArticleInfo Info { get; set; }
        public Article Article { get; set; }
        public SaleTransaction SaleTransaction { get; set; }
    }
}
