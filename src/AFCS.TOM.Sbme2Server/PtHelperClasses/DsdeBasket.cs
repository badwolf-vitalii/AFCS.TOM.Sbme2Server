using AFCS.TOM.BLogic.Interfaces;
using AFCS.TOM.Plugin.Interface.Models;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.ATMApiModels;
using AFCS.TOM.SbmeModels.Basket;
using AFCS.TOM.SbmeModels.Basket.ArticleInfos;
using AFCS.TOM.SbmeModels.Enums;
using Newtonsoft.Json;
using System.Xml.Serialization;

namespace AFCS.TOM.BLogic
{
    [Serializable]
    public class DsdeBasket
    {

        [Serializable]
        public class SimplifiedBasket
        {
            public decimal TotalPriceClear { get; set; }
            public decimal TotalPrice => TotalPriceClear - DiscountAmount;
            public decimal? DiscountPct { get; set; }
            public decimal DiscountAmount => DiscountPct.HasValue ? DiscountPct.Value / 100M * TotalPriceClear : 0M;
            public decimal Refund { get; set; }
            public decimal Remaining => TotalPrice - TotalPaid;
            public Dictionary<PaymentMethod, decimal> PaidEach { get; set; } = new Dictionary<PaymentMethod, decimal>();
            public decimal TotalPaid => PaidEach?.Values?.Sum() ?? 0;
            public decimal Change
            {
                get
                {
                    if (PaidEach?.ContainsKey(PaymentMethod.Cash) ?? false)
                    {
                        var notCash = TotalPaid - Math.Max(0, PaidEach[PaymentMethod.Cash]);
                        var cash = PaidEach[PaymentMethod.Cash];
                        var diff = cash - (TotalPrice /*- Refund*/ - notCash);
                        return Math.Min(cash, Math.Max(0, diff));
                    }
                    return 0;
                }
            }
            public string PosTransactionId { get; set; }
            public bool RefundInProgress { get; set; }
            public PaymentMethod RefundPaymentMethod { get; set; } = PaymentMethod.None;

            public SimplifiedBasket()
            {
            }

            public SimplifiedBasket(decimal total) => TotalPriceClear = total;

            public void Clear()
            {
                TotalPriceClear = 0;
                PaidEach?.Clear();
                PaidEach = null;
            }
        }

        public Guid SessionId { get; set; }
        public Guid SaleTransactionId { get; set; }
        public string? VtsTransactionId { get; set; }
        public string? SbmeTempTranId { get; set; }
        public string PosTransactionId { get; set; }
        public DateTime? ConfirmationTime { get; set; }
        public IArticlesSerializationRulesType ArticlesSerializationRules { get; set; }
        public List<Article> Articles { get; set; } = new List<Article>();
        public UniqueItemList<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t> ExternalProposals { get; set; } = new UniqueItemList<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t>();
        public UniqueItemList<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t> ExternalProposalRefunds { get; set; } = new UniqueItemList<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t>();
        public RequestedTicketsIssuing RequestedTicketsIssuing { get; set; } = null;
        public List<ulong> JammedTickets { get; set; } = new List<ulong>();
        public List<SbmeModels.BglDataLayer.UndonableContract> UndonableContracts { get; set; } = new List<SbmeModels.BglDataLayer.UndonableContract>();
        public List<PosDetail> PosDetails { get; set; } = new List<PosDetail>();
        public string LastPosOperationName { get; set; }
        public string? PtInvoiceClientCode { get; set; }
        public string? PtInvoiceClientBusinessName { get; set; }
        public AbiCodeResponse? PtBankTransferAbi { get; set; }
        public CabCodeResponse? PtBankTransferCab { get; set; }
        public decimal? PtDiscount { get; set; }
        public string? PtDiscountNotes { get; set; }
        public string? BankTransferNumber { get; set; }
        public PosOut LastPosOperationResult { get; set; }
        private SimplifiedBasket _simplified { get; set; }
        public SimplifiedBasket Simplified
        {
            get
            {
                if (_simplified != null) return _simplified;
                var total = TotalPrice();
                _simplified = new SimplifiedBasket(total);
                return _simplified;
            }
            set
            {
                _simplified = value;
                UpdateSimplifiedBasketRefund(null, null);
            }
        }

        [XmlIgnore] [field: NonSerialized] [JsonIgnore] public bool IsClosed
        {
            get
            {
                if (Articles?.Any() ?? false) return false;
                if (ExternalProposals?.Any() ?? false) return false;
                if (ExternalProposalRefunds?.Any() ?? false) return false;
                if (JammedTickets?.Any() ?? false) return false;
                if (UndonableContracts?.Any() ?? false) return false;
                if (PosDetails?.Any() ?? false) return false;
                if (RequestedTicketsIssuing?.Contracts?.Any() ?? false) return false;
                return true;
            }
        }

        //public DsdeBasket()
        //{
        //}

        private void UpdateSimplifiedBasketRefund(object sender, EventArgs e)
        {
            if (Simplified == null || (Articles?.Count ?? 0) == 0) return;
            var undo = GetUndoneTypeArticles()
                ?.Where(p => p.Info != null && p.Info is CscContractRefundArticleInfo)
                ?.Select(p => (p.Info as CscContractRefundArticleInfo).ArticleId);
            var undone = GetArticles(undo)?.Where(p => p.OldTransactionId.HasValue);
            var refund = undone?.Sum(p => p.TotalPrice) ?? 0;
            Simplified.Refund = refund;
        }

        public int Size(bool includeUndone = false) => GetArticles(includeUndone)?.Count() ?? 0;
        public int SizeExcludingPtSubstitutions() => GetArticles(false)
            ?.Where(p => p.ArticleType != ArticleType.PtItem || (p.Info is PtItemArticleInfo info) && info.ItemType != PtItemType.Substitution)
            ?.Count() ?? 0;

        public IEnumerable<Article> GetUndoneTypeArticles() => Articles?.Where(p => !IsSpoiledTicket(p) && IsUndone(p));

        public IEnumerable<Article> GetArticles(IEnumerable<Guid> ids) => Articles?.Where(p => ids.Any(q => q == p.Id));
        public IEnumerable<Article> GetArticles(IEnumerable<string> ids) => Articles?.Where(p => ids.Any(q => q == p.Id.ToString().Replace("-", string.Empty)));
        public IEnumerable<Article> GetArticles(bool includeUndone, bool? paid = null)
        {
            if (includeUndone)
                return Articles.Where(p => !IsSpoiledTicket(p) && !IsUndone(p.ArticleType) && (!paid.HasValue || p.Paid == paid.Value));

            var articles = Articles.Where(p => !IsSpoiledTicket(p) && (!IsUndone(p.ArticleType) && (!paid.HasValue || p.Paid == paid.Value)));
            var undone = GetUndoneTypeArticles()
                ?.Select(p => (p.Info is CscContractRefundArticleInfo info1)
                    ? info1?.ArticleId
                    : (p.Info is MagneticRefundArticleInfo info2)
                        ? info2?.ArticleId
                        : (p.Info is PtItemRefundArticleInfo info3)
                            ? info3?.ArticleId
                            : Guid.Empty)
                ?.Where(p => !(p?.Equals(Guid.Empty) ?? true))
                ?.Distinct();
            return articles?.Where(p => p.Paid || !(undone?.Contains(p?.Id) ?? true));
        }
        public IEnumerable<Article> GetArticles(bool includeUndone, ArticleType type, bool? paid = null) =>
            GetArticles(includeUndone, paid).Where(p => !IsSpoiledTicket(p) && p.ArticleType == type);
        public IEnumerable<Article> GetArticles(bool includeUndone, ArticleType[] types, bool? paid = null) =>
            GetArticles(includeUndone, paid).Where(p => !IsSpoiledTicket(p) && types.Contains(p.ArticleType));
        public IEnumerable<Article> GetArticles(bool includeUndone, PaymentMethod method, bool? paid = null) =>
            GetArticles(includeUndone, paid).Where(p => !IsSpoiledTicket(p) && p.SinglePaymentMethod && p.PaymentDetails.Any(q => q.PaymentMethod == method));
        public IEnumerable<Article> GetArticles(bool includeUndone, PaymentMethod[] methods, bool? paid = null) =>
            GetArticles(includeUndone, paid).Where(p => !IsSpoiledTicket(p) && p.SinglePaymentMethod && p.PaymentDetails.Any(q => methods.Contains(q.PaymentMethod)));
        public IEnumerable<Article> GetArticles(bool includeUndone, ArticleType type, PaymentMethod method, bool? paid = null) =>
            GetArticles(includeUndone, type, paid).Where(p => !IsSpoiledTicket(p) && p.SinglePaymentMethod && p.PaymentDetails.Any(q => q.PaymentMethod == method));
        public IEnumerable<Article> GetArticles(bool includeUndone, ArticleType type, PaymentMethod[] methods, bool? paid = null) =>
            GetArticles(includeUndone, type, paid).Where(p => !IsSpoiledTicket(p) && p.SinglePaymentMethod && p.PaymentDetails.Any(q => methods.Contains(q.PaymentMethod)));

        public List<ulong> GetJammedTickets() => JammedTickets.ToList(); // gets a copy of the list

        public IEnumerable<Article> GetUnpaidArticles()
        {
            var articles = Articles.Where(p => !IsSpoiledTicket(p) && !IsUndone(p.ArticleType));
            var undone = GetUndoneTypeArticles()
                ?.Select(p => (p.Info is CscContractRefundArticleInfo info1)
                    ? info1?.ArticleId
                    : (p.Info is MagneticRefundArticleInfo info2)
                        ? info2?.ArticleId
                        : (p.Info is PtItemRefundArticleInfo info3)
                            ? info3?.ArticleId
                            : Guid.Empty)
                ?.Where(p => !(p?.Equals(Guid.Empty) ?? true))
                ?.Distinct();
            var undone2 = Articles
                .Where(p => (undone?.Contains(p?.Id) ?? true) && p.OldTransactionId == null)
                .Select(p => p.Id);
            return articles?.Where(p => !(undone2?.Contains(p?.Id) ?? true));
        }

        public IEnumerable<Article> GetSpoiledTicketsArticles() => Articles?.Where(p => p.ArticleType == ArticleType.MagneticTicket && (p.Info is MagneticTicketArticleInfo info) && info.ResultIssuingCode == 6);

        public decimal TotalPrice(bool? paid = null) => GetUnpaidArticles().Sum(p => p.TotalPrice);
        public decimal TotalPrice(ArticleType type, bool? paid = null) => GetArticles(false, type, paid).Sum(p => p.TotalPrice);
        public decimal TotalPrice(PaymentMethod method, bool? paid = null) => GetArticles(false, method, paid).Sum(p => p.TotalPrice);
        public decimal TotalPrice(ArticleType type, PaymentMethod method, bool? paid = null) => GetArticles(false, type, method, paid).Sum(p => p.TotalPrice);
        public decimal TotalPriceExcludingPtSubstitutions() => GetUnpaidArticles()
            .Where(p => p.ArticleType != ArticleType.PtItem || (p.Info is PtItemArticleInfo info) && info.ItemType != PtItemType.Substitution)
            .Sum(p => p.TotalPrice);

        public void AddArticle(Article article)
        {
            if (article == null) return;
            UpdateSaleTransactionId();
            article.Position = GetNewArticleCounter();
            Articles.Add(article);
        }

        public Article AddEmptyArticle(ArticleType type = ArticleType.Unknown, ArticleInfo info = null)
        {
            UpdateSaleTransactionId();
            var article = new Article
            {
                Id = Guid.NewGuid(),
                Position = GetNewArticleCounter(),
                ArticleType = type,
                Info = info
            };
            Articles.Add(article);
            return article;
        }

        public Article AllocEmptyArticle(ArticleType type = ArticleType.Unknown, ArticleInfo info = null)
        {
            UpdateSaleTransactionId();
            return new Article
            {
                Id = Guid.NewGuid(),
                Position = GetNewArticleCounter(),
                ArticleType = type,
                Info = info
            };
        }

        public PosDetail AddPosDetail(uint amountRequested, PosOut posResult, bool isRefund, string operationName)
        {
            var detail = new PosDetail
            {
                Id = Guid.NewGuid(),
                AmountRequested = amountRequested.ToString(),
                StatoPos = posResult.PosData.StatoPos,
                Trk2 = posResult.PosData.Trk2,
                Trk1 = posResult.PosData.Trk1,
                EsitoLetturaTrk2 = posResult.PosData.EsitoLetturaTrk2,
                EsitoLetturaTrk1 = posResult.PosData.EsitoLetturaTrk1,
                AmountEcho = posResult.PosData.AmountEcho,
                DataTrs = posResult.PosData.DataTrs,
                ActionCode = posResult.PosData.ActionCode,
                PreauthorizationCode = posResult.PosData.PreauthorizationCode,
                AmountAuth = posResult.PosData.AmountAuth,
                OperationNumber = posResult.PosData.OperationNumber,
                InfoRelease = posResult.PosData.InfoRelease,
                AuthorizationCode = posResult.PosData.AuthorizationCode,
                BankBalance = posResult.PosData.BankBalance,
                PosBalance = posResult.PosData.POSBalance,
                CardType = posResult.PosData.CardType,
                TransactionType = posResult.PosData.TransactionType,
                KODescription = posResult.PosData.KODescription,
                Stan = posResult.PosData.STAN,
                AcquirerId = posResult.PosData.AcquirerId,
                TerminalId = posResult.PosData.TerminalId,
                TransactionResult = (byte)(bool.TryParse(posResult.PosData.TransactionResult, out var trRes) ? (trRes ? 1 : 0) : 0),
                Pan = posResult.PosData.PAN,
                DatiAggiuntiviTagDaGt = posResult.PosData.DatiAggiuntiviTagDaGt,
                TimeOnPc = DateTime.Now,
                IsRefund = (byte) (isRefund ? 1 : 0),
                PostransactionId = posResult.TokenToSaveInDB
            };
            PosDetails.Add(detail);
            LastPosOperationName = operationName;
            LastPosOperationResult = posResult;
            return detail;
        }

        public void AddExternalProposal(string key, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t proposal)
        {
            if (proposal == null || ExternalProposals.Any(p => p.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))) return;
            ExternalProposals.Add(new UniqueKeyValuePair<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t>(key, proposal));
        }

        public void AddExternalProposalRefund(string key, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t proposal)
        {
            if (proposal == null || ExternalProposalRefunds.Any(p => p.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))) return;
            ExternalProposalRefunds.Add(new UniqueKeyValuePair<string, SbmeModels.VtsModels.Responses.ExternalProductProposalItem_t>(key, proposal));
        }

        public void RemoveExternalProposal(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var proposal = ExternalProposals.FirstOrDefault(p => p.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            if (proposal == null) return;
            ExternalProposals.Remove(proposal);
        }

        public void RemoveExternalProposalRefund(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var proposal = ExternalProposalRefunds.FirstOrDefault(p => p.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            if (proposal == null) return;
            ExternalProposalRefunds.Remove(proposal);
        }

        public void RegisterJammedTicket(ulong firstSerial)
        {
            JammedTickets.Add(firstSerial);
        }

        public void AddUndonableContractInfo(SbmeModels.BglDataLayer.UndonableContract contractInfo) => UndonableContracts.Add(contractInfo);

        public SbmeModels.BglDataLayer.UndonableContract GetUndonableContractInfo(Guid guid) => UndonableContracts?.FirstOrDefault(p => p.ArticleId == guid);
        public bool RemoveUndonableContractInfo(Guid guid) => UndonableContracts?.RemoveAll(p => p.ArticleId == guid) > 0;

        public bool IsUndoingInvoice => UndonableContracts.Any(p => !string.IsNullOrWhiteSpace(p.PtInvoiceClientCode));

        public void CancelLastArticle()
        {
            if ((Articles?.Count ?? 0) > 0)
            {
                var last = Articles?.Last();
                Articles?.Remove(last);
                if (last.Info is MagneticTicketArticleInfo info)
                    JammedTickets.Remove((ulong)(info.FirstSerialNumber + last.QuantityIssued - 1));
            }
        }

        public bool RemoveArticle(Article article)
        {
            if (Articles.Count == 0 || article == null) return false;
            try
            {
                var result = Articles.Contains(article);
                if (result)
                {
                    Articles.Remove(article);
                    if (article.Info is MagneticTicketArticleInfo info)
                        JammedTickets.Remove((ulong)(info.FirstSerialNumber + article.QuantityIssued - 1));
                }
                else if (article.Id.HasValue)
                {
                    var articles = Articles.Where(p => p.Id.Equals(article.Id));
                    result = Articles.RemoveAll(p => articles.Contains(p)) > 0;
                    foreach (var a in articles)
                        if (a.Info is MagneticTicketArticleInfo info)
                            JammedTickets.Remove((ulong)(info.FirstSerialNumber + a.QuantityIssued - 1));
                }
                return result;
            }
            catch
            {
                return false;
            }
        }

        public bool AddRequestedTicketsIssuing(Article article, SbmeModels.VtsModels.Responses.ContractType[] contracts)
        {
            if (RequestedTicketsIssuing != null)
            {
                return false;
            }
            RequestedTicketsIssuing = new RequestedTicketsIssuing();
            RequestedTicketsIssuing.Put(article, contracts);
            return true;
        }

        public bool ClearRequestedTicketsIssuing()
        {
            if (RequestedTicketsIssuing == null)
            {
                return false;
            }
            RequestedTicketsIssuing = null;
            return true;
        }

        public void Clear()
        {
            Articles?.Clear();
            ExternalProposals?.Clear();
            ExternalProposalRefunds?.Clear();
            JammedTickets?.Clear();
            UndonableContracts?.Clear();
            PosDetails.Clear();
            RequestedTicketsIssuing = null;
            LastPosOperationName = null;
            LastPosOperationResult = null;
            Simplified?.Clear();
            Simplified = null;
            PosTransactionId = null;
            ConfirmationTime = null;
            PtInvoiceClientCode = null;
            PtBankTransferCab = null;
            PtDiscount = null;
            PtDiscountNotes = null;
            PtInvoiceClientBusinessName = null;
            UpdateSaleTransactionId();
        }

        private int GetNewArticleCounter() => (Articles?.Count ?? 0) > 0
            ? Articles?.Max(p => p.Position + 1) ?? 0
            : 0;

        private void UpdateSaleTransactionId()
        {
            if ((Articles?.Count ?? 0) == 0)
                SaleTransactionId = Guid.Empty;
        }

        public static bool IsUndone(ArticleType type) => new[]
        {
            ArticleType.UndoneContract,
            ArticleType.UndoneMagneticTicket,
            ArticleType.UndonePtItem
        }.Contains(type);

        public bool HasSubstitutions() => Articles?.Any(p => p.ArticleType == ArticleType.PtItem && p.UniquePrice < 0) ?? false;

        public static bool IsSpoiledTicket(Article article) => (article?.Info is MagneticTicketArticleInfo info)
            ? info.ResultIssuingCode == 6
            : false;

        public static bool IsUndone(Article article)
        {
            if (article == null || IsSpoiledTicket(article)) return false;
            switch (article.ArticleType)
            {
                case ArticleType.UndoneContract:
                    return article.Info is CscContractRefundArticleInfo;
                case ArticleType.UndoneMagneticTicket:
                    return article.Info is MagneticRefundArticleInfo;
                case ArticleType.UndonePtItem:
                    return article.Info is PtItemRefundArticleInfo;
                default:
                    return false;
            }
        }
    }
}
