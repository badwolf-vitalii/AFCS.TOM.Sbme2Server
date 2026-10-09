using AFCS.TOM.SbmeDataLayer;
using SbmeModelsEnums = AFCS.TOM.SbmeModels.Enums;

namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    public class SoldArticle
    {
        public int TariffId { get; set; }
        public SbmeModelsEnums.ArticleType ArticleType { get; set; }
        public int Count { get; set; }
        public int? QuantityRequired { get; set; }
        public int QuantityIssued { get; set; }
        public decimal TotalPrice { get; set; }
        public List<Article> Articles { get; set; }
    }
}
