using AFCS.TOM.SbmeModels.Basket;
using AFCS.TOM.SbmeModels.Basket.ArticleInfos;
using AFCS.TOM.SbmeModels.Enums;

namespace AFCS.TOM.BLogic
{
    [Serializable]
    public class RequestedTicketsIssuing
    {
        [Serializable]
        public class Ticket
        {
            public ulong Serial { get; set; }
            public byte[] VToken { get; set; }
        }

        public Guid? ArticleId { get; set; }
        public int ArticlePosition { get; set; }
        public string ArticleDescription { get; set; }
        public ArticleType ArticleType { get; set; }
        public uint ArticleQuantityRequired { get; set; } = 1;
        public decimal ArticleUniquePrice { get; set; }
        public decimal? ArticleDiscountApplied { get; set; }
        public MagneticTicketArticleInfo ArticleInfo { get; set; }
        public SbmeModels.VtsModels.Responses.ContractType[] Contracts { get; set; }

        public RequestedTicketsIssuing()
        {
        }

        public void Put(Article article, SbmeModels.VtsModels.Responses.ContractType[] contracts)
        {
            ArticleId = article.Id;
            ArticlePosition = article.Position;
            ArticleDescription = article.Description;
            ArticleType = article.ArticleType;
            ArticleQuantityRequired = article.QuantityRequired;
            ArticleUniquePrice = article.UniquePrice;
            ArticleDiscountApplied = article.DiscountApplied;
            ArticleInfo = article.Info as MagneticTicketArticleInfo;
            Contracts = contracts;
        }

        public Article Get() => new Article()
        {
            Id = ArticleId,
            Position = ArticlePosition,
            Description = ArticleDescription,
            ArticleType = ArticleType,
            QuantityIssued = ArticleQuantityRequired,
            QuantityRequired = ArticleQuantityRequired,
            UniquePrice = ArticleUniquePrice,
            DiscountApplied = ArticleDiscountApplied,
            PaymentDetails = new List<PaymentDetail>(),
            FromWhiteList = null,
            OldTransactionId = null,
            Info = ArticleInfo
        };
    }
}
