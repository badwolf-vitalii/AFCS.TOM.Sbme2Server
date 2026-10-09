using AFCS.TOM.BLogic;
using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.TemporarilyModels;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.ATMApiModels;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using static System.Net.WebRequestMethods;
using Basket = AFCS.TOM.SbmeModels.Basket;
using BDL = AFCS.TOM.SbmeModels.BglDataLayer;
using DL = AFCS.TOM.SbmeDataLayer;
using Enums = AFCS.TOM.SbmeModels.Enums;
using Sales = AFCS.TOM.SbmeModels.OutputParameters.Sales;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public partial class SalesService : ISalesService
    {
        private Logger _logger = LogManager.GetLogger("Sbme2Server");
        private DL.DataLayerContext _context { get; }
        public bool IsServiceEnabled { get; }

        public SalesService(IConfiguration configuration, DL.DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            IsServiceEnabled = launchSettings.BglServicesEnabled;
            _context = context;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<Guid> CommitSaleTransaction(SaleTransaction transaction)
        {
            switch (BglDataLayerConfiguration.Instance.BasketVersion)
            {
                case 1: return await CommitSaleTransactionB1(transaction);
                case 2: return await CommitSaleTransactionB2(transaction);
                default: return Guid.Empty;
            }
        }
        
        public async Task<Guid> CommitSaleTransactionB1(SaleTransaction transaction)
        {
            if (transaction == null || (transaction.DeviceShift == null && transaction.DeviceShiftId.Equals(Guid.Empty)) || transaction.PaymentDetails == null) return Guid.Empty;

            // A successful commit may be retried after the response is lost.
            // Preserve the original transaction rather than inserting it again.
            if (transaction.Id != Guid.Empty &&
                await _context.SaleTransactions.AnyAsync(p => p.Id == transaction.Id))
                return transaction.Id;

            if (transaction.Id.Equals(Guid.Empty))
                transaction.Id = Guid.NewGuid();

            if (transaction.DeviceShift == null)
            {
                if (transaction.DeviceShiftId.Equals(Guid.Empty))
                    throw new MissingShiftInformationException();
                else
                {
                    transaction.DeviceShift = _context.DeviceShifts.FirstOrDefault(p => p.Id.Equals(transaction.DeviceShiftId));
                    if (transaction.DeviceShift == null)
                        throw new MissingShiftInformationException();
                }
            }
            transaction.DeviceShiftId = transaction.DeviceShift.Id;

            var articles = (transaction.Articles?.Count ?? 0) > 0
                ? transaction.Articles
                    ?.OrderBy(p => p.Position)
                : transaction.PaymentDetails
                    ?.Where(p => p.Article != null && (p.Article.SaleTransactionId == null || p.Article.SaleTransactionId == transaction.Id))
                    ?.OrderBy(p => p.Article.Position).Select(p => p.Article);
            var position = 1; // 0 - resered and is not used
            if (articles?.Any() ?? false)
            {
                // Enumerate a snapshot because duplicate articles may be removed from
                // transaction.Articles while processing the sale.
                foreach (var article in articles.ToList())
                {
                    article.Position = position++;
                    article.SaleTransactionId = article.SaleTransactionId ?? transaction.Id;
                    var empty = article.Id.Equals(Guid.Empty);
                    var add = empty || !_context.Articles.Any(p => p.Id.Equals(article.Id));
                    if (empty) article.Id = Guid.NewGuid();
                    if (add)
                    {
                        if (article.ContactlessCardArticleInfos != null)
                            foreach (var info in article.ContactlessCardArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.CscContractArticleInfos != null)
                            foreach (var info in article.CscContractArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.CscContractRefundArticleInfo != null)
                        {
                            article.CscContractRefundArticleInfo.ArticleId = article.Id;
                            if (article.CscContractRefundArticleInfo.SaleTransactionId == Guid.Empty)
                                article.CscContractRefundArticleInfo.SaleTransactionId = transaction.Id;
                        }
                        if (article.MagneticArticleInfos != null)
                            foreach (var info in article.MagneticArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.ProfileRenewalArticleInfos != null)
                            foreach (var info in article.ProfileRenewalArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        _context.Articles.Add(article);
                    }
                    else
                    {
                        transaction.Articles?.Remove(article);
                    }
                }
            }

            var details = transaction.PaymentDetails;
            transaction.PaymentDetails = null;

            var posDetails = transaction.PosDetails;
            transaction.PosDetails = null;

            _context.SaleTransactions.Add(transaction);

            if (details != null)
            {
                foreach (var detail in details)
                {
                    if (detail.Article == null && detail.ArticleId.HasValue && detail.ArticleId != Guid.Empty && !_context.Articles.Any(p => p.Id == detail.ArticleId)) continue; // for fake undo of not existing articles
                    var empty = detail.Id.Equals(Guid.Empty);
                    var add = empty || !_context.PaymentDetails.Any(p => p.Id.Equals(detail.Id));
                    if (empty) detail.Id = Guid.NewGuid();
                    if (detail.Article != null)
                        detail.ArticleId = detail.Article.Id;
                    detail.PaymentMethodNavigation = _context.PaymentMethods.FirstOrDefault(p => p.Code == detail.PaymentMethod);
                    detail.SaleTransactionId = (detail.Article != null ? detail.Article.SaleTransactionId : detail.SaleTransactionId) ?? transaction.Id;
                    if (detail.SaleTransactionId == transaction.Id)
                        detail.SaleTransaction = transaction;
                    else
                    {
                        detail.Article = _context.Articles.FirstOrDefault(p => p.Id.Equals(detail.ArticleId));
                        detail.SaleTransaction = _context.SaleTransactions.FirstOrDefault(p => p.Id.Equals(detail.SaleTransactionId));
                    }
                    if (add) _context.PaymentDetails.Add(detail);
                }
            }

            if (posDetails != null)
            {
                foreach (var detail in posDetails)
                {
                    var empty = detail.Id.Equals(Guid.Empty);
                    var add = empty || !_context.PosDetails.Any(p => p.Id.Equals(detail.Id));
                    if (empty) detail.Id = Guid.NewGuid();
                    detail.SaleTransactionId = transaction.Id;
                    detail.SaleTransaction = transaction;
                    if (add) _context.PosDetails.Add(detail);
                }
            }

            return transaction.Id;
        }

        public async Task<Guid> CommitSaleTransactionB2(SaleTransaction transaction)
        {
            if (transaction == null || (transaction.DeviceShift == null && transaction.DeviceShiftId.Equals(Guid.Empty)) || transaction.PaymentDetails == null) return Guid.Empty;

            // A successful commit may be retried after the response is lost.
            // Preserve the original transaction rather than inserting it again.
            if (transaction.Id != Guid.Empty &&
                await _context.SaleTransactions.AnyAsync(p => p.Id == transaction.Id))
                return transaction.Id;

            if (transaction.Id.Equals(Guid.Empty))
                transaction.Id = Guid.NewGuid();

            if (transaction.DeviceShift == null)
            {
                if (transaction.DeviceShiftId.Equals(Guid.Empty))
                    throw new MissingShiftInformationException();
                else
                {
                    transaction.DeviceShift = _context.DeviceShifts.FirstOrDefault(p => p.Id.Equals(transaction.DeviceShiftId));
                    if (transaction.DeviceShift == null)
                        throw new MissingShiftInformationException();
                }
            }
            transaction.DeviceShiftId = transaction.DeviceShift.Id;

            var articles = (transaction.Articles?.Count ?? 0) > 0
                ? transaction.Articles
                    ?.OrderBy(p => p.Position)
                : transaction.PaymentDetails
                    ?.Where(p => p.Article != null && (p.Article.SaleTransactionId == null || p.Article.SaleTransactionId == transaction.Id))
                    ?.OrderBy(p => p.Article.Position).Select(p => p.Article);
            var position = 1; // 0 - reserved and not used
            var anyArticleAdded = false;
            var existingTransactionId = Guid.Empty;
            if (articles?.Any() ?? false)
            {
                // Enumerate a snapshot because duplicate articles may be removed from
                // transaction.Articles while processing the sale.
                foreach (var article in articles.ToList())
                {
                    article.Position = position++;
                    article.SaleTransactionId = article.SaleTransactionId ?? transaction.Id;
                    var empty = article.Id.Equals(Guid.Empty);
                    var add = empty || !_context.Articles.Any(p => p.Id.Equals(article.Id));
                    if (empty) article.Id = Guid.NewGuid();
                    if (add)
                    {
                        if (article.ContactlessCardArticleInfos != null)
                            foreach (var info in article.ContactlessCardArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.CscContractArticleInfos != null)
                            foreach (var info in article.CscContractArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.CscContractRefundArticleInfo != null)
                        {
                            article.CscContractRefundArticleInfo.ArticleId = article.Id;
                            if (article.CscContractRefundArticleInfo.SaleTransactionId == Guid.Empty)
                                article.CscContractRefundArticleInfo.SaleTransactionId = transaction.Id;
                        }
                        if (article.MagneticArticleInfos != null)
                            foreach (var info in article.MagneticArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.MagneticRefundArticleInfo != null)
                        {
                            article.MagneticRefundArticleInfo.ArticleId = article.Id;
                            if (article.MagneticRefundArticleInfo.SaleTransactionId == Guid.Empty)
                                article.MagneticRefundArticleInfo.SaleTransactionId = transaction.Id;
                        }
                        if (article.ProfileRenewalArticleInfos != null)
                            foreach (var info in article.ProfileRenewalArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.PtItemArticleInfos != null)
                            foreach (var info in article.PtItemArticleInfos)
                            {
                                if (info.Id.Equals(Guid.Empty))
                                    info.Id = Guid.NewGuid();
                                info.ArticleId = article.Id;
                            }
                        if (article.PtItemRefundArticleInfo != null)
                        {
                            article.PtItemRefundArticleInfo.ArticleId = article.Id;
                            if (article.PtItemRefundArticleInfo.SaleTransactionId == Guid.Empty)
                                article.PtItemRefundArticleInfo.SaleTransactionId = transaction.Id;
                        }
                        _context.Articles.Add(article);
                        anyArticleAdded = true;
                    }
                    else
                    {
                        transaction.Articles?.Remove(article);
                        if (!anyArticleAdded && existingTransactionId.Equals(Guid.Empty))
                        {
                            existingTransactionId = _context.Articles.FirstOrDefault(p => p.Id.Equals(article.Id))?.SaleTransactionId ?? existingTransactionId;
                        }
                    }
                }
            }
            else
            {
                anyArticleAdded = true;
            }

            if (!anyArticleAdded)
            {
                return existingTransactionId;
            }

            var details = transaction.PaymentDetails;
            transaction.PaymentDetails = null;

            var posDetails = transaction.PosDetails;
            transaction.PosDetails = null;

            _context.SaleTransactions.Add(transaction);

            if (details != null)
            {
                foreach (var detail in details)
                {
                    if (detail.Article == null && detail.ArticleId.HasValue && detail.ArticleId != Guid.Empty && !_context.Articles.Any(p => p.Id == detail.ArticleId)) continue; // for fake undo of not existing articles
                    var empty = detail.Id.Equals(Guid.Empty);
                    var add = empty || !_context.PaymentDetails.Any(p => p.Id.Equals(detail.Id));
                    if (empty) detail.Id = Guid.NewGuid();
                    if (detail.Article != null)
                        detail.ArticleId = detail.Article.Id;
                    detail.PaymentMethodNavigation = _context.PaymentMethods.FirstOrDefault(p => p.Code == detail.PaymentMethod);
                    detail.SaleTransactionId = (detail.Article != null ? detail.Article.SaleTransactionId : detail.SaleTransactionId) ?? transaction.Id;
                    if (detail.SaleTransactionId == transaction.Id)
                        detail.SaleTransaction = transaction;
                    else
                    {
                        detail.Article = _context.Articles.FirstOrDefault(p => p.Id.Equals(detail.ArticleId));
                        detail.SaleTransaction = _context.SaleTransactions.FirstOrDefault(p => p.Id.Equals(detail.SaleTransactionId));
                    }
                    if (add) _context.PaymentDetails.Add(detail);
                }
            }

            if (posDetails != null)
            {
                foreach (var detail in posDetails)
                {
                    var empty = detail.Id.Equals(Guid.Empty);
                    var add = empty || !_context.PosDetails.Any(p => p.Id.Equals(detail.Id));
                    if (empty) detail.Id = Guid.NewGuid();
                    detail.SaleTransactionId = transaction.Id;
                    detail.SaleTransaction = transaction;
                    if (add) _context.PosDetails.Add(detail);
                }
            }

            return transaction.Id;
        }

        public async Task<Guid> CommitPtTransaction(PtConfirmTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);

            // Check both the persisted ID and the business key to handle client retries.
            var existingId = await FindExistingPtTransactionId(transaction);
            if (existingId.HasValue)
                return existingId.Value;

            var products = transaction.PtConfirmTransactionProducts;
            var payments = transaction.PtConfirmTransactionPayments;
            if (transaction.Id.Equals(Guid.Empty))
            {
                transaction.Id = Guid.NewGuid();
            }
            transaction.PtConfirmTransactionProducts = null;
            transaction.PtConfirmTransactionPayments = null;
            await _context.PtConfirmTransactions.AddAsync(transaction);
            if (products?.Any() ?? false)
            {
                foreach (var product in products)
                {
                    if (product.Id.Equals(Guid.Empty))
                    {
                        product.Id = Guid.NewGuid();
                    }
                    product.PtConfirmTransactionId = transaction.Id;
                }
                await _context.PtConfirmTransactionProducts.AddRangeAsync(products);
            }
            if (payments?.Any() ?? false)
            {
                foreach (var payment in payments)
                {
                    if (payment.Id.Equals(Guid.Empty))
                    {
                        payment.Id = Guid.NewGuid();
                    }
                    payment.PtConfirmTransactionId = transaction.Id;
                }
                await _context.PtConfirmTransactionPayments.AddRangeAsync(payments);
            }
            return transaction.Id;
        }

        public async Task<bool> CheckIfPtTransactionExists(PtConfirmTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            return (await FindExistingPtTransactionId(transaction)).HasValue;
        }

        private async Task<Guid?> FindExistingPtTransactionId(PtConfirmTransaction transaction)
        {
            if (transaction.Id != Guid.Empty)
            {
                var matchingId = await _context.PtConfirmTransactions
                    .Where(p => p.Id == transaction.Id)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync();
                if (matchingId.HasValue)
                    return matchingId;
            }

            return await _context.PtConfirmTransactions
                .Where(p => p.TransactionNumber == transaction.TransactionNumber &&
                    p.CodiceEsattoria == transaction.CodiceEsattoria &&
                    p.CodiceRivendita == transaction.CodiceRivendita)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();
        }

        public IEnumerable<Basket.Article> GetSoldArticles(string serialNumber, bool includeVTokens, int top)
        {
            var infos = _context.CscContractArticleInfos.Where(p => p.CardSerialNumber != null && p.CardSerialNumber.Equals(serialNumber));
            var articles = infos == null ? null : _context.Articles.Where(p => infos.Any(q => q.ArticleId.Equals(p.Id)));

            var selection = new List<Basket.Article>();
            foreach (var info in infos)
            {
                var article = articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                var newArticle = new Basket.Article
                {
                    Id = article.Id,
                    Description = article.Description,
                    ArticleType = (Enums.ArticleType)article.ArticleType,
                    QuantityIssued = (uint)article.QuantityIssued,
                    QuantityRequired = (uint)article.QuantityRequired,
                    UniquePrice = article.UniquePrice,
                    PaymentDetails = new List<Basket.PaymentDetail>(),
                    Info = new Basket.ArticleInfos.CscContractArticleInfo
                    {
                        CardSerialNumber = info.CardSerialNumber,
                        CardInfoBefore = includeVTokens ? info.VtokenBefore : null,
                        CardInfoAfter = includeVTokens ? info.VtokenAfter : null
                    }
                };
                if (article.PaymentDetails != null)
                    foreach (var pd in article.PaymentDetails)
                    {
                        newArticle.PaymentDetails.Add(new Basket.PaymentDetail
                        {
                            Id = pd.Id,
                            PaymentMethod = (Enums.PaymentMethod)pd.PaymentMethod,
                            PaymentTime = pd.PaymentTime,
                            Amount = pd.Amount,
                        });
                    }
                selection.Add(newArticle);
            }
            var result = selection.OrderBy(p => (p.PaymentDetails?.Count() ?? 0) > 0 ? p.PaymentDetails.FirstOrDefault().PaymentTime : DateTime.MaxValue).AsEnumerable();
            return result;
        }

        public async Task<List<Guid>?> AddCscContractArticleInfo(List<CscContractArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.CscContractArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.Id.Equals(Guid.Empty)) info.Id = Guid.NewGuid();
                _context.CscContractArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddCscContractRefundArticleInfo(List<CscContractRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                var first = await _context.CscContractRefundArticleInfos.FirstOrDefaultAsync(p => p.ArticleId.Equals(info.ArticleId));
                if (first != null)
                {
                    if (first.UndoReceiptId.Equals(Guid.Empty))
                    {
                        first.UndoReceiptId = Guid.NewGuid();
                    }
                    guids.Add(first.UndoReceiptId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.UndoReceiptId.Equals(Guid.Empty))
                {
                    info.UndoReceiptId = Guid.NewGuid();
                }
                _context.CscContractRefundArticleInfos.Add(info);
                guids.Add(info.UndoReceiptId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddMagneticTicketArticleInfo(List<MagneticArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.MagneticArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.Id.Equals(Guid.Empty)) info.Id = Guid.NewGuid();
                _context.MagneticArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddMagneticRefundArticleInfo(List<MagneticRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.MagneticRefundArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                _context.MagneticRefundArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddContactlessCardArticleInfo(List<ContactlessCardArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.ContactlessCardArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.Id.Equals(Guid.Empty)) info.Id = Guid.NewGuid();
                _context.ContactlessCardArticleInfos.Add(info);
                guids.Add(info.ArticleId); InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddProfileRenewalArticleInfo(List<ProfileRenewalArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.ProfileRenewalArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.Id.Equals(Guid.Empty)) info.Id = Guid.NewGuid();
                _context.ProfileRenewalArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddPtItemArticleInfo(List<PtItemArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.PtItemArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                if (info.Id.Equals(Guid.Empty)) info.Id = Guid.NewGuid();
                _context.PtItemArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        public async Task<List<Guid>?> AddPtItemRefundArticleInfo(List<PtItemRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            var guids = new List<Guid>();
            for (var i = 0; i < infos.Count(); ++i)
            {
                var info = infos[i];
                var article = _context.Articles.FirstOrDefault(p => p.Id.Equals(info.ArticleId));
                if (article == null) continue;
                if (_context.PtItemRefundArticleInfos.Any(p => p.ArticleId.Equals(info.ArticleId)))
                {
                    guids.Add(info.ArticleId);
                    InsertPtArticleInfo(i, article, apti);
                    continue;
                }
                _context.PtItemRefundArticleInfos.Add(info);
                guids.Add(info.ArticleId);
                InsertPtArticleInfo(i, article, apti);
            }
            return guids.Count > 0 ? guids : null;
        }

        private void InsertPtArticleInfo(int i, Article? article, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            if (apti != null && article != null)
            {
                var ptInfo = apti.Skip(i).FirstOrDefault();
                if (ptInfo != null)
                {
                    article.BillingPrices = ptInfo.UniqueBillingPriceCent?.ToString();
                    article.NumSalePeriodUnits = ptInfo.NumSalePeriodUnits;
                }
            }
        }

        public async Task AddSpoiledMagneticTicketArticleInfo(Article article, Guid deviceShiftId, DateTime time)
        {
            if (article.MagneticArticleInfos.Count > 1) throw new Exception("Article must contain only one MagnetickArticleInfo");

            var first = article.MagneticArticleInfos.FirstOrDefault();
            if (first == null || _context.MagneticArticleInfos.Any(p => p.Id == first.Id))
                return;

            var st = new SaleTransaction {
                Id = Guid.NewGuid(),
                DeviceShiftId = deviceShiftId,
                TransactionTime = time,
                VtTransactionId = string.Empty
            };
            _context.SaleTransactions.Add(st);

            if (!_context.Articles.Any(p => p.Id.Equals(article.Id)))
            {
                article.SaleTransactionId = st.Id;
                _context.Articles.Add(article);
            }

            _context.MagneticArticleInfos.Add(first);
        }

        public async Task<Guid[]> AddWhiteListContactlessCardArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time)
        {
            var st = new SaleTransaction {
                Id = Guid.NewGuid(),
                DeviceShiftId = deviceShiftId,
                TransactionTime = time,
                VtTransactionId = string.Empty
            };
            _context.SaleTransactions.Add(st);

            var result = new Guid[articles.Count];
            var arts = articles.ToArray();
            for (var i = 0; i < arts.Length; ++i)
            {
                var article = arts[i];
                if (article.CscContractArticleInfos.Count > 1)
                    throw new Exception($"Article #{i + 1} must contain only one CscContractArticleInfo");
                
                if (article.Id.Equals(Guid.Empty)) article.Id = Guid.NewGuid();

                var first = article.CscContractArticleInfos.FirstOrDefault();
                if (first == null)
                {
                    result[i] = Guid.Empty;
                    continue;
                }
                if (first.Id.Equals(Guid.Empty)) first.Id = Guid.NewGuid();
                if (_context.CscContractArticleInfos.Any(p => p.Id == first.Id))
                {
                    result[i] = first.ArticleId;
                    continue;
                }
                if (first.VtContractGroupId == null) first.VtContractGroupId = string.Empty;
                if (first.VtokenBefore == null) first.VtokenBefore = new byte[0];
                if (first.VtokenAfter == null) first.VtokenAfter = new byte[0];

                if (!_context.Articles.Any(p => p.Id.Equals(article.Id)))
                {
                    article.SaleTransactionId = st.Id;
                    _context.Articles.Add(article);
                }

                result[i] = article.Id;
                _context.CscContractArticleInfos.Add(first);
            }
            return result;
        }

        public async Task<Guid[]> AddContactlessCardExpirationExtensionArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time)
        {
            var st = new SaleTransaction {
                Id = Guid.NewGuid(),
                DeviceShiftId = deviceShiftId,
                TransactionTime = time,
                VtTransactionId = string.Empty
            };
            _context.SaleTransactions.Add(st);

            var result = new Guid[articles.Count];
            var arts = articles.ToArray();
            for (var i = 0; i < arts.Length; ++i)
            {
                var article = arts[i];
                if (article.ContactlessCardExpirationExtensionArticleInfos.Count > 1)
                    throw new Exception($"Article #{i + 1} must contain only one ContactlessCardExpirationExtensionArticleInfo");
                
                if (article.Id.Equals(Guid.Empty)) article.Id = Guid.NewGuid();

                var first = article.ContactlessCardExpirationExtensionArticleInfos.FirstOrDefault();
                if (first == null)
                {
                    result[i] = Guid.Empty;
                    continue;
                }
                if (first.Id.Equals(Guid.Empty)) first.Id = Guid.NewGuid();
                if (_context.ContactlessCardExpirationExtensionArticleInfos.Any(p => p.Id == first.Id))
                {
                    result[i] = first.ArticleId;
                    continue;
                }

                if (!_context.Articles.Any(p => p.Id.Equals(article.Id)))
                {
                    article.SaleTransactionId = st.Id;
                    _context.Articles.Add(article);
                }

                result[i] = article.Id;
                _context.ContactlessCardExpirationExtensionArticleInfos.Add(first);
            }
            return result;
        }

        public async Task RegisterCanceledCscContracts(List<CanceledCscContract>? contracts)
        {
            if (!(contracts?.Any() ?? false)) return;
            _context.CanceledCscContracts.AddRange(contracts.Where(p => p != null));
        }

        public async Task AddPosDetails(List<PosDetail> details)
        {
            foreach (var detail in details)
            {
                if (detail.Id.Equals(Guid.Empty))
                    detail.Id = Guid.NewGuid();
                else
                    if (_context.PosDetails.Any(p => p.Id.Equals(detail.Id))) continue;
                _context.PosDetails.Add(detail);
            }
        }

        public async Task AddPtBankTransferInfo(PtBankTransferInfo info)
        {
            if (info.Id.Equals(Guid.Empty))
                info.Id = Guid.NewGuid();
            else
                if (_context.PtBankTransferInfos.Any(p => p.Id.Equals(info.Id))) return;
            _context.PtBankTransferInfos.Add(info);
        }

        public List<PosDetail>? GetPosDetails(Guid transactionId)
        {
            if (transactionId == Guid.Empty) return null;
            var pd = _context.PosDetails.Where(p => p.SaleTransactionId.Equals(transactionId));
            return pd.ToList();
        }

        public Sales.PosRefundDetails? GetPosRefundDetails(Guid transactionId)
        {
            if (transactionId == Guid.Empty)
            {
                return new Sales.PosRefundDetails {
                    ReasonCode = 1 // transaction not specified
                };
            }

            var last = _context.PosDetails.OrderByDescending(p => p.TimeOnPc).FirstOrDefault();
            var refundable = last?.SaleTransactionId.Equals(transactionId) ?? false;

            var reasonCode = 0;

            if (!refundable)
            {
                return new Sales.PosRefundDetails {
                    ReasonCode = 2 // POS payment not found
                };
            }

            var tr = _context.SaleTransactions
                .Include("Articles")
                .Include("PaymentDetails")
                .FirstOrDefault(p => p.Id.Equals(transactionId));

            if ((tr?.Articles?.Count ?? 0) == 0)
            {
                return new Sales.PosRefundDetails {
                    ReasonCode = 3 // Transaction not found or is empty
                };
            }

            const int SOLD = 1;
            const int UNDONE = 7;
            const int POS = 2;

            refundable &= tr!.PaymentDetails?.All(p => p.PaymentMethod == POS) ?? false;

            if (!refundable)
            {
                return new Sales.PosRefundDetails {
                    ReasonCode = 4 // Not all (or no one of) used payment methods are POS
                };
            }

            if (tr!.Articles.Any(p => p.ArticleType != SOLD && p.ArticleType != UNDONE))
            {
                refundable = false;
                reasonCode = 5; // Transaction contains not only contract reloadings
            }
            else if (refundable)
            {
                var a = tr!.Articles.Where(p => p.ArticleType == SOLD).Select(p => p.Id).ToArray();
                var b = tr!.Articles.Where(p => p.ArticleType == UNDONE).Select(p => p.Id).ToArray();
                var c = a.Where(p => !b.Contains(p)).ToArray();
                refundable = c.Length == 1;
                if (!refundable)
                {
                    reasonCode = 6; // Transaction contains multiple articles
                }
            }

            var pd = _context.PosDetails.Where(p => p.SaleTransactionId.Equals(transactionId));
            if (pd?.Any() ?? false)
            {
                var serialized = JsonConvert.SerializeObject(pd.ToList());
                var deserialized = JsonConvert.DeserializeObject<List<BDL.PosDetail>>(serialized);
                return new Sales.PosRefundDetails {
                    PosDetails = deserialized,
                    IsRefundable = refundable,
                    ReasonCode = reasonCode
                };
            }
            else
            {
                return new Sales.PosRefundDetails {
                    ReasonCode = 7 // No POS payment details
                };
            }
        }

        public Sales.CashRefundDetails? GetCashRefundDetails(Guid transactionId)
        {
            if (transactionId == Guid.Empty)
            {
                return new Sales.CashRefundDetails {
                    ReasonCode = 0b_0000_0001 // transaction not specified
                };
            }

            var tr = _context.SaleTransactions
                .Include("Articles")
                .Include("PaymentDetails")
                .FirstOrDefault(p => p.Id.Equals(transactionId));

            if ((tr?.Articles?.Count ?? 0) == 0)
            {
                return new Sales.CashRefundDetails {
                    ReasonCode = 0b_0000_0010 // Transaction not found or is empty
                };
            }

            const int SOLD = 1;
            const int UNDONE = 7;
            const int CASH = 1;

            var refundable = tr!.PaymentDetails?.All(p => p.PaymentMethod == CASH) ?? false;

            if (!refundable)
            {
                return new Sales.CashRefundDetails {
                    ReasonCode = 0b_0000_0100 // Not all (or no one of) used payment methods are CASH
                };
            }

            var reasonCode = 0b_0000_0000;

            if (tr!.Articles.Any(p => p.ArticleType != SOLD && p.ArticleType != UNDONE))
            {
                refundable = false;
                reasonCode |= 0b_0000_1000; // Transaction contains not only contract reloadings
            }

            var a = tr!.Articles.Where(p => p.ArticleType == SOLD).Select(p => p.Id).ToArray();
            var b = tr!.Articles.Where(p => p.ArticleType == UNDONE).Select(p => p.Id).ToArray();
            var c = a.Where(p => !b.Contains(p)).ToArray();

            if (c.Any())
            {
                b = _context.CscContractRefundArticleInfos.Where(p => c.Contains(p.ArticleId)).Select(p => p.ArticleId).ToArray();
                c = a.Where(p => !b.Contains(p)).ToArray();
            }

            refundable = c.Length == 1;
            if (!refundable)
            {
                reasonCode |= 0b_0001_0000; // Transaction contains multiple articles
            }

            var pd = _context.PosDetails.OrderBy(p => p.TimeOnPc).Where(p => p.SaleTransactionId.Equals(transactionId));
            var last = pd.LastOrDefault();
            if (last != null && last.IsRefund == 0 && last.KODescription == null)
            {
                return new Sales.CashRefundDetails {
                    ReasonCode = reasonCode | 0b_0010_0000 // There are present POS payment details
                };
            }
            else
            {
                return new Sales.CashRefundDetails {
                    IsRefundable = refundable,
                    ReasonCode = reasonCode
                };
            }
        }

        public BDL.Article? GetArticleByItsCscContractInfo(Guid cscContractInfoId)
        {
            var info = _context.CscContractArticleInfos.FirstOrDefault(p => p.Id == cscContractInfoId);
            if (info == null) return null;
            var article = _context.Articles.FirstOrDefault(p => p.Id == info.ArticleId);
            if (article == null) return null;
            //article.CscContractArticleInfos = article.CscContractArticleInfos ?? new List<CscContractArticleInfo>();
            //var infos = _context.CscContractArticleInfos
            //    .Where(p => p.ArticleId == article.Id && !article.CscContractArticleInfos.Any(q => q.Id == p.Id)).ToList();
            //infos.ForEach(p => article.CscContractArticleInfos.Add(p));
            var serialized = JsonConvert.SerializeObject(article);
            return JsonConvert.DeserializeObject<BDL.Article>(serialized);
        }

        public List<BDL.CscContractArticleInfo>? GetCscContractArticleInfos(Guid cscContractInfoId)
        {
            var res = _context.CscContractArticleInfos.FirstOrDefault(p => p.ArticleId == cscContractInfoId);
            if (res == null) return null;
            var serialized = JsonConvert.SerializeObject(res);
            return JsonConvert.DeserializeObject<List<BDL.CscContractArticleInfo>>(serialized);
        }

        public List<CscContractArticleDetails> GetCscContractArticles(string cardSerialNumber)
        {
            var infos = _context.CscContractArticleInfos
                .Where(p => p.CardSerialNumber == cardSerialNumber)
                .Include("Article")
                .Include("Article.PaymentDetails")
                .Where(p => !_context.CscContractRefundArticleInfos.Any(q => q.ArticleId.Equals(p.ArticleId)))
                .OrderBy(p => p.Article.Position)
                .Join(_context.SaleTransactions, i => i.Article.SaleTransactionId, s => s.Id, (info, transaction) => new CscContractArticleDetails
                {
                    Info = info,
                    Article = info.Article,
                    SaleTransaction = transaction
                })
                .OrderByDescending(p => p.SaleTransaction.TransactionTime)
                .ToList();
            return infos;
        }

        public BDL.UndonableContract? GetUndonableContract(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom)
            => GetUndonableContractsEnumerable(cardSerialNumber, shortCardModel, deviceShiftId, startFrom)?.FirstOrDefault();

        public List<BDL.UndonableContract>? GetUndonableContracts(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom, bool undone = false)
            => GetUndonableContractsEnumerable(cardSerialNumber, shortCardModel, deviceShiftId, startFrom, undone)?.ToList();

        private IEnumerable<BDL.UndonableContract>? GetUndonableContractsEnumerable(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom, bool undone = false)
        {
            var trArtInf = _context.SaleTransactions
                .Where(p => !deviceShiftId.HasValue || p.DeviceShift.Id == deviceShiftId.Value)
                .Join(_context.Articles, st => st.Id, a => a.SaleTransactionId, (st, a) => new {
                    SaleTransactionId = st.Id,
                    TransactionTime = st.TransactionTime,
                    PtInvoiceClientCode = st.PtInvoiceClientCode,
                    PtInvoiceClientBusinessName = st.PtInvoiceClientBusinessName,
                    Article = a,
                    Undone = _context.CscContractRefundArticleInfos.Any(p => p.ArticleId == a.Id),
                })
                .Where(p => p.Undone == undone)
                .Join(_context.CscContractArticleInfos, ta => ta.Article.Id, ci => ci.ArticleId, (ta, ci) => new BDL.UndonableContract {
                    Undone = ta.Undone,
                    PositionInBasket = ta.Article.Position,
                    ShortCardModel = ci.ShortCardModel,
                    CardSerialNumber = ci.CardSerialNumber,
                    VTokenBefore = ci.VtokenBefore,
                    VTokenAfter = ci.VtokenAfter,
                    VtContractGroupId = ci.VtContractGroupId,
                    UniquePrice = ta.Article.UniquePrice,
                    QuantityIssued = ta.Article.QuantityIssued,
                    TariffId = ci.TariffId,
                    NbAreas = ci.NbAreas,
                    Description = ta.Article.Description + (string.IsNullOrWhiteSpace(ci.AreaExtension) ? string.Empty : $" {ci.AreaExtension}"),
                    VtContractId = ci.VtContractId,
                    VtSlaveContractsId = ci.VtSlaveContractsId,
                    VtSlaveContractsTariffId = ci.VtSlaveContractsTariffId,
                    TransactionTime = ta.TransactionTime,
                    TransactionId = ta.SaleTransactionId,
                    ArticleId = ta.Article.Id,
                    ContractInfoID = ci.Id,
                    ZoneList = ci.ZoneList,
                    UnitsMultiplier = ci.NumberOfUnits,
                    BillingPrices = ta.Article.BillingPrices,
                    NumSalePeriodUnits = ta.Article.NumSalePeriodUnits,
                    SoldInSubstitution = ta.Article.SoldInSubstitution,
                    PaidByEmployee = ci.PaidByEmployee,
                    PtInvoiceClientCode = ta.PtInvoiceClientCode,
                    PtInvoiceClientBusinessName = ta.PtInvoiceClientBusinessName,
                    WithInvoice = ta.Article.WithInvoice.HasValue && ta.Article.WithInvoice == 1
                })
                .Where(p => p.CardSerialNumber == cardSerialNumber && p.ShortCardModel == shortCardModel && p.PositionInBasket > 0)
                .OrderByDescending(p => p.PositionInBasket)
                .ToList();

            trArtInf.Sort((a, b) =>
            {
                var dif = (a.TransactionTime - b.TransactionTime).Ticks;
                var direct = dif > 0
                    ? 1 : dif < 0 ? -1
                    : a.PositionInBasket - b.PositionInBasket > 0
                        ? 1
                        : -1;
                var inverse = -direct;
                return inverse;
            });

            var toBeDeleted = new List<BDL.UndonableContract>();
            foreach (var tr0 in trArtInf)
            {
                var inArticle = _context.PaymentDetails.FirstOrDefault(p => p.ArticleId == tr0.ArticleId);

                if (inArticle != null)
                {
                    tr0.PaymentDetail = JsonConvert.DeserializeObject<BDL.PaymentDetail>(JsonConvert.SerializeObject(inArticle));
                    tr0.PaidInArticle = true;
                }
                else
                {
                    if (tr0.UniquePrice == 0)
                    {
                        tr0.PaymentDetail = new BDL.PaymentDetail
                        {
                            Id = Guid.Empty,
                            SaleTransactionId = tr0.TransactionId,
                            ArticleId = tr0.ArticleId,
                            PaymentTime = tr0.TransactionTime,
                            PaymentMethod = (int)Enums.PaymentMethod.Cash,
                            Amount = 0
                        };
                        tr0.PaidInTransaction = true;
                    }
                    else
                    {
                        var inTransaction = _context.PaymentDetails.FirstOrDefault(p => p.SaleTransactionId == tr0.TransactionId);
                        tr0.PaymentDetail = JsonConvert.DeserializeObject<BDL.PaymentDetail>(JsonConvert.SerializeObject(inTransaction));
                        if (tr0.PaymentDetail != null)
                            tr0.PaidInTransaction = true;
                        else
                            toBeDeleted.Add(tr0);
                    }
                }
            }
            toBeDeleted.ForEach(p => trArtInf.Remove(p));
            toBeDeleted.Clear();

            for (var i = 1; i < trArtInf.Count; ++i)
                if (trArtInf.Take(i).Any(p => p.PaymentDetail == null || trArtInf[i].PaymentDetail == null
                    || (p.PaidInArticle
                        && p.PaymentDetail.ArticleId == trArtInf[i].PaymentDetail.ArticleId)
                    || (p.PaidInTransaction
                        && p.PaymentDetail.SaleTransactionId == trArtInf[i].PaymentDetail.SaleTransactionId
                        && p.ArticleId == trArtInf[i].ArticleId)))
                    toBeDeleted.Add(trArtInf[i]);
            toBeDeleted.ForEach(p => trArtInf.Remove(p));
            toBeDeleted.Clear();

            if (!startFrom.HasValue) return trArtInf.AsQueryable();

            var first = trArtInf.FirstOrDefault(p => p.ArticleId == startFrom.Value);
            if (first == null) return null;
            var start = trArtInf.IndexOf(first);
            return trArtInf.Skip(start + 1).AsQueryable();
        }

        public Sales.MostlyUsedTariffs? GetMostlyUsedTariffs(Enums.ArticleType articleType, byte periodInDays)
        {
            var version = BglDataLayerConfiguration.Instance?.GetMostlyUsedTariffsVersion ?? 1;
            switch (version)
            {
                case 2:
                    return GetMostlyUsedTariffsV2(articleType, periodInDays);
                case 1:
                default:
                    return GetMostlyUsedTariffsV1(articleType, periodInDays);
            }
        }

        public Sales.MostlyUsedTariffs? GetMostlyUsedTariffsV1(Enums.ArticleType articleType, byte periodInDays)
        {
            if (!(articleType == Enums.ArticleType.Contract || articleType == Enums.ArticleType.MagneticTicket)) return null;
            var from = DateTime.Today.AddDays(-periodInDays);
            var infoTableName = articleType == Enums.ArticleType.Contract ? "CscContractArticleInfos" : "MagneticArticleInfos";
            
            IEnumerable<Article>? counts0 = null;
            try
            {
                counts0 = _context.Articles
                    ?.Include("SaleTransaction")
                    ?.Include(infoTableName)
                    ?.Where(p => p.ArticleType == (int)articleType && p.SaleTransaction.TransactionTime >= from)
                    ?.AsEnumerable();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, $"Error during fetching of the articles of type {articleType}: " + ex.Message);
                throw;
            }

            List<Sales.MostlyUsedTariffs.TariffCount>? counts = null;
            var groupedBy = false;
            var sw = Stopwatch.StartNew();
            try
            {
                var counts1 = articleType == Enums.ArticleType.Contract
                    ? counts0?.GroupBy(p => new {
                        TariffId = p.CscContractArticleInfos.FirstOrDefault()?.TariffId ?? 0
                    })
                    : counts0?.GroupBy(p => new {
                        TariffId = p.MagneticArticleInfos.FirstOrDefault()?.TariffId ?? 0
                    });

                sw.Restart();
                counts = counts1
                    ?.DistinctBy(p => p.Key.TariffId)
                    ?.Take(100)
                    ?.Select(p => new Sales.MostlyUsedTariffs.TariffCount {
                        TariffId = p.Key.TariffId,
                        Count = p.Count(),
                    })
                    ?.OrderByDescending(p => p.Count)
                    ?.ToList();
            }
            catch (Exception ex)
            {
                if (groupedBy)
                {
                    LogHelper.Error(_logger, $"Error during fetching the tariffs ({sw.ElapsedMilliseconds}ms): " + ex.Message);
                }
                else
                {
                    LogHelper.Error(_logger, $"Error during grouping by ({sw.ElapsedMilliseconds}ms): " + ex.Message);
                }
                throw;
            }
            sw.Reset();

            if (counts != null)
                return new Sales.MostlyUsedTariffs {
                    TariffCounts = counts
                };
            return null;
        }

        public Sales.MostlyUsedTariffs? GetMostlyUsedTariffsV2(Enums.ArticleType articleType, byte periodInDays)
        {
            if (!(articleType == Enums.ArticleType.Contract || articleType == Enums.ArticleType.MagneticTicket)) return null;
            var from = DateTime.Today.AddDays(-periodInDays);
            var infoTableName = articleType == Enums.ArticleType.Contract ? "CscContractArticleInfos" : "MagneticArticleInfos";

            List<Guid>? transactions = null;
            try
            {
                transactions = _context.SaleTransactions.Where(p => p.TransactionTime >= from).OrderByDescending(p => p.TransactionTime).Select(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, $"Error during fetching of the transactions from {from:yyyy/MM/dd HH:mm}: " + ex.Message);
                throw;
            }

            IEnumerable<Article>? counts0 = null;
            try
            {
                counts0 = _context.Articles
                    .Include(infoTableName)
                    .Where(p => p.SaleTransactionId.HasValue && transactions.Contains(p.SaleTransactionId.Value));
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, $"Error during fetching of the articles of type {articleType}: " + ex.Message);
                throw;
            }

            List<Sales.MostlyUsedTariffs.TariffCount>? counts = null;
            var groupedBy = false;
            var sw = Stopwatch.StartNew();
            try
            {
                var counts1 = articleType == Enums.ArticleType.Contract
                    ? counts0?.GroupBy(p => new {
                        TariffId = p.CscContractArticleInfos.FirstOrDefault()?.TariffId ?? 0
                    })
                    : counts0?.GroupBy(p => new {
                        TariffId = p.MagneticArticleInfos.FirstOrDefault()?.TariffId ?? 0
                    });

                sw.Restart();
                counts = counts1
                    ?.DistinctBy(p => p.Key.TariffId)
                    ?.Take(50)
                    ?.Select(p => new Sales.MostlyUsedTariffs.TariffCount {
                        TariffId = p.Key.TariffId,
                        Count = p.Count(),
                    })
                    ?.OrderByDescending(p => p.Count)
                    ?.ToList();
            }
            catch (Exception ex)
            {
                if (groupedBy)
                {
                    LogHelper.Error(_logger, $"Error during fetching the tariffs ({sw.ElapsedMilliseconds}ms): " + ex.Message);
                }
                else
                {
                    LogHelper.Error(_logger, $"Error during grouping by ({sw.ElapsedMilliseconds}ms): " + ex.Message);
                }
                throw;
            }
            sw.Reset();

            if (counts != null)
                return new Sales.MostlyUsedTariffs {
                    TariffCounts = counts
                };
            return null;
        }

        public int GetTheLongestPeriodOfArticles(Enums.ArticleType articleType)
        {
            var res = _context.Articles
                .Where(p => p.SaleTransaction != null && p.ArticleType == (int)articleType)
                .Select(p => (DateTime?)p.SaleTransaction.TransactionTime)
                .Min();
            if (res.HasValue)
            {
                var diff = DateTime.Today - TrimTime(res.Value);
                return diff.Days;
            }
            return 0;
        }

        private static DateTime ParsePtDt(string value)
        {
            var res = DateTime.Parse(value, CultureInfo.InvariantCulture);
            return res;
        }

        public List<SaleTransaction> GetMissingPtTransactions()
        {
            // A sale is missing its PT confirmation when its transaction number
            // is absent from the confirmation table.
            var confirmedTransactionNumbers = _context.PtConfirmTransactions
                .Select(p => p.TransactionNumber)
                .ToList()
                .Select(number => number.ToString())
                .ToHashSet();
            var transactions = _context.SaleTransactions
                .Where(p => p.SentDateTime.HasValue && p.SentDateTime.Value < DateTime.Today && !string.IsNullOrWhiteSpace(p.VtTransactionId))
                .Where(p => !confirmedTransactionNumbers.Contains(p.VtTransactionId))
                .OrderBy(p => p.TransactionTime)
                .ToList();
            return transactions;
        }

        public async Task<SaleTransaction?> GetSaleTransaction(Guid transactionId)
        {
            if (transactionId.Equals(Guid.Empty)) return null;
            var tr = await _context
                .SaleTransactions
                .Include("PaymentDetails")
                .Include("PaymentDetails.PaymentMethodNavigation")
                .Include("PosDetails")
                .Include("PtBankTransferInfos")
                .Include("Articles")
                .Include("Articles.MagneticRefundArticleInfo")
                .Include("Articles.PtItemRefundArticleInfo")
                .Include("Articles.ContactlessCardArticleInfos")
                .Include("Articles.ContactlessCardExpirationExtensionArticleInfos")
                .Include("Articles.CscContractArticleInfos")
                .Include("Articles.MagneticArticleInfos")
                .Include("Articles.ProfileRenewalArticleInfos")
                .Include("Articles.PtItemArticleInfos")
                .FirstOrDefaultAsync(p => p.Id.Equals(transactionId));
            if (tr != null)
            {
                var undone = _context.CscContractRefundArticleInfos
                    .Include("Article")
                    .Include("Article.CscContractArticleInfos")
                    .Include("Article.SaleTransaction")
                    .Where(p => p.SaleTransactionId.Equals(transactionId));
                if (undone?.Any() ?? false)
                {
                    if (tr.Articles == null)
                    {
                        tr.Articles = new List<Article>();
                    }
                    foreach (var undo in undone)
                    {
                        tr.Articles.Add(undo.Article);
                    }
                }
            }
            return tr;
        }

        public void GetTransactionBasket(SaleTransaction tr)
        {
            if (tr == null) return;
            var last = _context.PtConfirmTransactions.OrderByDescending(p => p.TransactionNumber).FirstOrDefault();
            List<PaymentDetail> pd = _context.PaymentDetails
                .Where(p => p.SaleTransactionId.Equals(tr.Id))
                .OrderBy(p => p.PaymentTime)
                .ToList();
            var dsh = _context.DeviceShifts.Where(p => p.Id == tr.DeviceShiftId).FirstOrDefault();
            var ash = _context.AgentShifts.Where(p => p.Id == dsh.AgentShiftId).FirstOrDefault();
            var basket = new DsdeBasket
            {
                PtDiscount = pd.FirstOrDefault()?.PtDiscount,
                PtDiscountNotes = pd.FirstOrDefault()?.PtDiscountNotes,
                VtsTransactionId = tr.VtTransactionId,
            };
            var confirmTransactionRequest = new ConfirmTransactionRequest {
                SupplierID = last.SupplierId,
                AgentID = ash.AgentId,
                ShiftNumber = ash.ShiftNumber,
                NbShiftDay = 120, // PORCA: ???
                NbShiftAgent = 10, // PORCA: ???
                LocalityID = last?.LocalityId ?? 0,
                TransactionNumber = Convert.ToInt32(basket.VtsTransactionId),
                CodiceRivendita = last?.CodiceRivendita, // ???
                CodiceEsattoria = last?.CodiceEsattoria ?? 0,
                DtaVendita = tr.TransactionTime.ToString("o"),
                //CodiceClienteFatturaElettronica = basket?.PtInvoiceClientCode, // ???
                NamePC = last?.NamePc ?? string.Empty,
                DtaVersamento = GetMidnight(tr.TransactionTime).ToString("o"),
                Sconto = basket?.PtDiscount ?? 0,
                NoteSconto = basket?.PtDiscountNotes,
                Products = new List<ConfirmTransactionProduct>(),
                Payments = new List<ConfirmTransactionPayment>()
            };
        }

        private static DateTime GetMidnight(DateTime dt) => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);

        private static Enums.ArticleType GetArticleType(int code) => (Enums.ArticleType)code;

        private static bool CmpArticleTypes(int code, Enums.ArticleType articleType) => GetArticleType(code) == articleType;

        private static DateTime? TrimTime(DateTime? dt) => TrimTime(dt, null);

        private static DateTime TrimTime(DateTime? dt, DateTime defaultValue) => dt.HasValue
            ? TrimTime(dt.Value)
            : defaultValue;

        private static DateTime? TrimTime(DateTime? dt, DateTime? defaultValue) => dt.HasValue
            ? TrimTime(dt.Value)
            : defaultValue;

        private static DateTime TrimTime(DateTime dt) => DateOnly.FromDateTime(dt).ToDateTime(TimeOnly.MinValue);
    }
}
