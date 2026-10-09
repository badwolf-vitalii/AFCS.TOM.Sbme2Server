
using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Sales;
using Microsoft.EntityFrameworkCore;
using DL = AFCS.TOM.SbmeDataLayer;
namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public partial class ReceiptsService : IReceiptsService
    {
        private DL.DataLayerContext _context { get; }
        public bool IsServiceEnabled { get; }

        public ReceiptsService(IConfiguration configuration, DL.DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            IsServiceEnabled = launchSettings.BglServicesEnabled;
            _context = context;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public AgentShift? GetAgentShift(Guid shiftId, bool includeSaleTransactions = false)
        {
            var ordered = _context?.AgentShifts
                ?.Where(p => p.Id == shiftId)
                ?.OrderByDescending(p => p.StartDate)
                ?.AsQueryable();
            if (includeSaleTransactions)
            {
                ordered = ordered
                    ?.Include("AccountingPeriod")
                    ?.Include("CashDrawers")
                    ?.Include("DeviceShifts")
                    ?.Include("DeviceShifts.SaleTransactions")
                    ?.Include("DeviceShifts.SaleTransactions.PaymentDetails")
                    ?.Include("DeviceShifts.SaleTransactions.Articles")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ContactlessCardArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractRefundArticleInfo")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.MagneticArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ProfileRenewalArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails");
            }
            var result = ordered?.FirstOrDefault();
            return result;
        }

        public List<AgentShift>? GetAgentShifts(int agentId, DateTime from, DateTime to, bool includeSaleTransactions = false)
        {
            var ordered = _context?.AgentShifts
                ?.Where(p =>
                    p.AgentId == agentId &&
                    p.StartDate >= from &&
                    (p.EndDate ?? DateTime.Today) <= to &&
                    p.DeviceShifts.Count > 0)
                ?.OrderByDescending(p => p.StartDate)
                ?.AsQueryable();
            if (includeSaleTransactions)
            {
                ordered = ordered
                    ?.Include("AccountingPeriod")
                    ?.Include("CashDrawers")
                    ?.Include("DeviceShifts")
                    ?.Include("DeviceShifts.SaleTransactions")
                    ?.Include("DeviceShifts.SaleTransactions.PaymentDetails")
                    ?.Include("DeviceShifts.SaleTransactions.Articles")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ContactlessCardArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractRefundArticleInfo")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.MagneticArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ProfileRenewalArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails");
            }
            var result = ordered?.ToList();
            return result;
        }

        public List<AgentShift>? GetAllAgentsShifts(DateTime from, DateTime to, bool includeSaleTransactions = false)
        {
            var ordered = _context?.AgentShifts
                ?.Where(p =>
                    p.StartDate >= from &&
                    (p.EndDate ?? new DateTime(9999, 1, 1)) <= to &&
                    p.DeviceShifts.Count > 0)
                ?.OrderByDescending(p => p.StartDate)
                ?.Include("AccountingPeriod")
                ?.Include("CashDrawers")
                ?.Include("DeviceShifts");
            if (includeSaleTransactions)
            {
                ordered = ordered
                    ?.Include("DeviceShifts.SaleTransactions")
                    ?.Include("DeviceShifts.SaleTransactions.PaymentDetails")
                    ?.Include("DeviceShifts.SaleTransactions.Articles")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ContactlessCardArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.CscContractRefundArticleInfo")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.MagneticArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.ProfileRenewalArticleInfos")
                    ?.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails");
            }
            var result = ordered?.Take(90)?.ToList();
            return result;
        }

        public byte[]? GetTransactionReceipt(Guid transactionId) =>
            _context.SaleTransactions.FirstOrDefault(p => p.Id == transactionId)?.Receipt;

        public byte[]? GetEndShiftReceipt(Guid agentShiftId) =>
            _context.AgentShifts.FirstOrDefault(p => p.Id == agentShiftId)?.Receipt;

        public List<ReceiptDateAndContent>? GetReceipts(Guid agentShiftId, ReceiptTypeMask mask, StartEnd? startEnd = null, int rowsPerPage = 50, int pageNumber = 0)
        {
            if (agentShiftId.Equals(Guid.Empty) || mask == ReceiptTypeMask.None) return null;

            var result = new List<ReceiptDateAndContent>();
            
            var shifts = _context.DeviceShifts
                .Where(p => p.AgentShiftId.Equals(agentShiftId))
                .Select(p => p.Id);
            if (shifts == null || !shifts.Any()) return null;
            
            var transactions = _context.SaleTransactions
                .Where(p => p.Receipt != null && shifts.Contains(p.DeviceShiftId))
                .Select(p => new {
                    TransactionId = p.Id,
                    TransactionTime = p.TransactionTime,
                    Receipt = p.Receipt
                });

            if (transactions != null && transactions.Any())
            {
                var articles0 = _context.Articles
                    .Join(transactions, ar => ar.SaleTransactionId, st => st.TransactionId, (ar, st) => new {
                        TransactionTime = st.TransactionTime,
                        ArticleId = ar.Id,
                        FromWhiteList = ar.FromWhiteList
                    });

                if (ReceiptTypeMask.ContractReloading == (mask & ReceiptTypeMask.ContractReloading))
                {
                    var articles = articles0.Where(p => !p.FromWhiteList.HasValue || p.FromWhiteList == 0);
                    var receipts = _context.CscContractArticleInfos
                        .Where(p => p.Receipt != null)
                        .Join(articles, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                            TransactionTime = ar.TransactionTime,
                            ArticleId = inf.ArticleId,
                            Receipt = inf.Receipt
                        })
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.ArticleId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.ContractReloading
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }

                if (ReceiptTypeMask.ContractUndo == (mask & ReceiptTypeMask.ContractUndo))
                {
                    var receipts = _context.CscContractRefundArticleInfos
                        .Where(p => p.Receipt != null)
                        .Join(articles0, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                            TransactionTime = ar.TransactionTime,
                            ArticleId = inf.ArticleId,
                            Receipt = inf.Receipt
                        })
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.ArticleId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.ContractUndo
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }

                if (ReceiptTypeMask.ProfileRenewal == (mask & ReceiptTypeMask.ProfileRenewal))
                {
                    var receipts = _context.ProfileRenewalArticleInfos
                        .Where(p => p.Receipt != null)
                        .Join(articles0, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                            TransactionTime = ar.TransactionTime,
                            ArticleId = inf.ArticleId,
                            Receipt = inf.Receipt
                        })
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.ArticleId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.ProfileRenewal
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }

                if (ReceiptTypeMask.CardIssuing == (mask & ReceiptTypeMask.CardIssuing))
                {
                    var receipts = _context.ContactlessCardArticleInfos
                        .Where(p => p.Receipt != null && !(p.ReissuingReasonCode.HasValue || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialPh) || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialLo)))
                        .Join(articles0, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                            TransactionTime = ar.TransactionTime,
                            ArticleId = inf.ArticleId,
                            Receipt = inf.Receipt
                        })
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.ArticleId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.CardIssuing
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }

                if (ReceiptTypeMask.CardReissuing == (mask & ReceiptTypeMask.CardReissuing))
                {
                    var receipts = _context.ContactlessCardArticleInfos
                        .Where(p => p.Receipt != null && (p.ReissuingReasonCode.HasValue || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialPh) || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialLo)))
                        .Join(articles0, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                            TransactionTime = ar.TransactionTime,
                            ArticleId = inf.ArticleId,
                            Receipt = inf.Receipt
                        })
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.ArticleId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.CardReissuing
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }

                if (ReceiptTypeMask.SaleTransaction == (mask & ReceiptTypeMask.SaleTransaction))
                {
                    var receipts = transactions
                        .Where(p => p.Receipt != null)
                        .Select(p => new ReceiptDateAndContent {
                            ArticleId = p.TransactionId.ToString(),
                            Content = p.Receipt,
                            Date = p.TransactionTime,
                            Type = ReceiptType.SaleTransactionReport
                        });
                    if (receipts != null && receipts.Any())
                        result.AddRange(receipts);
                }
            }

            if (ReceiptTypeMask.ContractReloadingWL == (mask & ReceiptTypeMask.ContractReloadingWL))
            {
                var transactionsNoReceipts = _context.SaleTransactions
                    .Where(p => p.Receipt == null && shifts.Contains(p.DeviceShiftId))
                    .Select(p => new {
                        TransactionId = p.Id,
                        TransactionTime = p.TransactionTime
                    });

                var articles0 = _context.Articles
                    .Join(transactionsNoReceipts, ar => ar.SaleTransactionId, st => st.TransactionId, (ar, st) => new {
                        TransactionTime = st.TransactionTime,
                        ArticleId = ar.Id,
                        FromWhiteList = ar.FromWhiteList
                    });

                var articles = articles0.Where(p => p.FromWhiteList.HasValue && p.FromWhiteList != 0);
                var receipts = _context.CscContractArticleInfos
                    .Where(p => p.Receipt != null)
                    .Join(articles, inf => inf.ArticleId, ar => ar.ArticleId, (inf, ar) => new {
                        TransactionTime = ar.TransactionTime,
                        ArticleId = inf.ArticleId,
                        Receipt = inf.Receipt
                    })
                    .Select(p => new ReceiptDateAndContent {
                        ArticleId = p.ArticleId.ToString(),
                        Content = p.Receipt,
                        Date = p.TransactionTime,
                        Type = ReceiptType.WhiteListContractReload
                    });
                if (receipts != null && receipts.Any())
                    result.AddRange(receipts);
            }

            if (ReceiptTypeMask.ContractRemoval == (mask & ReceiptTypeMask.ContractRemoval))
            {
                var receipts = _context.CanceledCscContracts
                    .Join(shifts, canc => canc.DeviceShiftId, sh => sh, (canc, sh) => new {
                        ContractId = canc.Id,
                        CancellationTime = canc.CancellationDateTime,
                        Receipt = canc.Receipt
                    })
                    .Where(p => p.Receipt != null)
                    .Select(p => new ReceiptDateAndContent {
                        ArticleId = p.ContractId,
                        Content = p.Receipt,
                        Date = p.CancellationTime,
                        Type = ReceiptType.ContractRemoval
                    });
                if (receipts != null && receipts.Any())
                    result.AddRange(receipts);
            }

            if (!result.Any()) return null;

            if (startEnd != null)
            {
                var que = result.AsQueryable();
                if (startEnd.Start.HasValue)
                {
                    que = que.Where(p => p.Date >= startEnd.Start.Value);
                }
                if (startEnd.End.HasValue)
                {
                    que = que.Where(p => p.Date < startEnd.End.Value);
                }
                return que.Skip(rowsPerPage * pageNumber).Take(rowsPerPage).ToList();
            }
            else
            {
                return result.Skip(rowsPerPage * pageNumber).Take(rowsPerPage).ToList();
            }
        }

        public List<ReceiptDateAndContent>? GetReceiptsForMedia(string cardSerialNumber, int shortCardModel, ReceiptTypeMask mask, StartEnd? startEnd = null, int rowsPerPage = 50, int pageNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(cardSerialNumber) || mask == ReceiptTypeMask.None) return null;

            var result = new List<ReceiptDateAndContent>();

            var reloadNorm = ReceiptTypeMask.ContractReloading == (mask & ReceiptTypeMask.ContractReloading);
            var reloadWl = ReceiptTypeMask.ContractReloadingWL == (mask & ReceiptTypeMask.ContractReloadingWL);
            var cancel = ReceiptTypeMask.ContractRemoval == (mask & ReceiptTypeMask.ContractRemoval);
            var undo = ReceiptTypeMask.ContractUndo == (mask & ReceiptTypeMask.ContractUndo);
            
            if (reloadNorm || reloadWl)
            {
                var receipts = _context.CscContractArticleInfos
                    .Where(p => p.Receipt != null && p.CardSerialNumber.Equals(cardSerialNumber.ToUpper()) && (shortCardModel == 0 || p.ShortCardModel == shortCardModel))
                    .Join(_context.Articles, inf => inf.ArticleId, ar => ar.Id, (inf, ar) => new {
                        ArticleId = inf.ArticleId,
                        Receipt = inf.Receipt,
                        TransactionId = ar.SaleTransactionId,
                        FromWhiteList = ar.FromWhiteList ?? 0
                    })
                    .Join(_context.SaleTransactions, inf => inf.TransactionId, tr => tr.Id, (inf, tr) => new {
                        ArticleId = inf.ArticleId,
                        Receipt = inf.Receipt,
                        FromWhiteList = inf.FromWhiteList,
                        TransactionTime = tr.TransactionTime
                    });
                
                var receiptsNorm = receipts.Where(p => p.Receipt != null && p.FromWhiteList == 0).Select(p => new ReceiptDateAndContent {
                    ArticleId = p.ArticleId.ToString(),
                    Content = p.Receipt,
                    Date = p.TransactionTime,
                    Type = ReceiptType.ContractReloading
                });
                if (receiptsNorm != null && receiptsNorm.Any())
                    result.AddRange(receiptsNorm);

                var receiptsWl = receipts.Where(p => p.Receipt != null && p.FromWhiteList != 0).Select(p => new ReceiptDateAndContent {
                    ArticleId = p.ArticleId.ToString(),
                    Content = p.Receipt,
                    Date = p.TransactionTime,
                    Type = ReceiptType.WhiteListContractReload
                });

                if (receiptsWl != null && receiptsWl.Any())
                    result.AddRange(receiptsWl);
            }

            if (undo)
            {
                var receipts = _context.CscContractArticleInfos
                    .Where(p => p.Receipt != null && p.CardSerialNumber.Equals(cardSerialNumber.ToUpper()) && (shortCardModel == 0 || p.ShortCardModel == shortCardModel))
                    .Join(_context.Articles, inf => inf.ArticleId, ar => ar.Id, (inf, ar) => new {
                        ArticleId = inf.ArticleId,
                        TransactionId = ar.SaleTransactionId
                    })
                    .Join(_context.CscContractRefundArticleInfos, inf => inf.ArticleId, refund => refund.ArticleId, (inf, refund) => new {
                        ArticleId = inf.ArticleId,
                        Receipt = refund.Receipt,
                        TransactionId = inf.TransactionId
                    })
                    .Join(_context.SaleTransactions, inf => inf.TransactionId, tr => tr.Id, (inf, tr) => new ReceiptDateAndContent {
                        ArticleId = inf.ArticleId.ToString(),
                        Content = inf.Receipt,
                        Date = tr.TransactionTime,
                        Type = ReceiptType.ContractUndo
                    });
                if (receipts != null && receipts.Any())
                    result.AddRange(receipts);
            }

            if (cancel)
            {
                var receipts = _context.CanceledCscContracts
                    .Where(p => p.Receipt != null && p.CardSerialNumber.Equals(cardSerialNumber.ToUpper()) && (shortCardModel == 0 || p.CardShortCardModel == shortCardModel))
                    .Select(p => new ReceiptDateAndContent {
                        ArticleId = p.Id,
                        Content = p.Receipt,
                        Date = p.CancellationDateTime,
                        Type = ReceiptType.ContractRemoval
                    });
                if (receipts != null && receipts.Any())
                    result.AddRange(receipts);
            }

            if (!result.Any()) return null;

            if (startEnd != null)
            {
                var que = result.AsQueryable();
                if (startEnd.Start.HasValue)
                {
                    que = que.Where(p => p.Date >= startEnd.Start.Value);
                }
                if (startEnd.End.HasValue)
                {
                    que = que.Where(p => p.Date < startEnd.End.Value);
                }
                return que.Skip(rowsPerPage * pageNumber).Take(rowsPerPage).ToList();
            }
            else
            {
                return result.Skip(rowsPerPage * pageNumber).Take(rowsPerPage).ToList();
            }
        }

        public byte[]? GetReceiptByTypeAndId(string id, ReceiptTypeMask mask)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var reloadNorm = ReceiptTypeMask.ContractReloading == (mask & ReceiptTypeMask.ContractReloading);
            var reloadWl = ReceiptTypeMask.ContractReloadingWL == (mask & ReceiptTypeMask.ContractReloadingWL);
            var cancel = ReceiptTypeMask.ContractRemoval == (mask & ReceiptTypeMask.ContractRemoval);
            var undo = ReceiptTypeMask.ContractUndo == (mask & ReceiptTypeMask.ContractUndo);
            var profile = ReceiptTypeMask.ProfileRenewal == (mask & ReceiptTypeMask.ProfileRenewal);
            var issuing = ReceiptTypeMask.CardIssuing == (mask & ReceiptTypeMask.CardIssuing);
            var reissuing = ReceiptTypeMask.CardReissuing == (mask & ReceiptTypeMask.CardReissuing);
            var expirationExtension = ReceiptTypeMask.CardExpirationExtension == (mask & ReceiptTypeMask.CardExpirationExtension);
            var saleTransaction = ReceiptTypeMask.SaleTransaction == (mask & ReceiptTypeMask.SaleTransaction);

            if (reloadNorm)
            {
                var artId = new Guid(id);
                var result = _context.CscContractArticleInfos
                    .Include("Article")
                    .Where(p => p.ArticleId.Equals(artId) && (p.Article.FromWhiteList == null || p.Article.FromWhiteList == 0))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (reloadWl)
            {
                var artId = new Guid(id);
                var result = _context.CscContractArticleInfos
                    .Include("Article")
                    .Where(p => p.ArticleId.Equals(artId) && (p.Article.FromWhiteList == null || p.Article.FromWhiteList == 1))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (undo)
            {
                var artId = new Guid(id);
                var result = _context.CscContractRefundArticleInfos
                    .Where(p => p.ArticleId.Equals(artId))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (cancel)
            {
                var result = _context.CanceledCscContracts
                    .Where(p => p.Id.Equals(id))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (profile)
            {
                var artId = new Guid(id);
                var result = _context.ProfileRenewalArticleInfos
                    .Where(p => p.ArticleId.Equals(artId))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (issuing)
            {
                var artId = new Guid(id);
                var result = _context.ContactlessCardArticleInfos
                    .Where(p => p.ArticleId.Equals(artId) && !(p.ReissuingReasonCode.HasValue || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialPh) || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialLo)))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (reissuing)
            {
                var artId = new Guid(id);
                var result = _context.ContactlessCardArticleInfos
                    .Where(p => p.ArticleId.Equals(artId) && (p.ReissuingReasonCode.HasValue || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialPh) || !string.IsNullOrWhiteSpace(p.ReissuingCscSerialLo)))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (expirationExtension)
            {
                var artId = new Guid(id);
                var result = _context.ContactlessCardExpirationExtensionArticleInfos
                    .Where(p => p.ArticleId.Equals(artId))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }
            if (saleTransaction)
            {
                var trId = new Guid(id);
                var result = _context.SaleTransactions
                    .Where(p => p.Id.Equals(trId))
                    ?.FirstOrDefault()
                    ?.Receipt;
                if (result != null) return result;
            }

            return null;
        }

        public Guid? GetUndoneCscContractVtsReceiptId(Guid articleId) =>
            _context.CscContractRefundArticleInfos.FirstOrDefault(p => p.ArticleId.Equals(articleId))?.VtsReceiptId;

        public Guid? GetUndoneTicketVtsReceiptId(Guid articleId) =>
            _context.MagneticRefundArticleInfos.FirstOrDefault(p => p.ArticleId.Equals(articleId))?.VtsReceiptId;

        public Guid? GetUndonePtItemVtsReceiptId(Guid articleId) =>
            _context.PtItemRefundArticleInfos.FirstOrDefault(p => p.ArticleId.Equals(articleId))?.VtsReceiptId;
    }
}
