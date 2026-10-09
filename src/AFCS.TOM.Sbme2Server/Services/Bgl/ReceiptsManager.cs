using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.Basket.ArticleInfos;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.VtsModels.Responses;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using Article = AFCS.TOM.SbmeDataLayer.Article;
using ArticleInfoBasket = AFCS.TOM.SbmeModels.Basket.ArticleInfos.CscContractArticleInfo;
using CardInfo = AFCS.TOM.SbmeModels.VtsWrapper.Responses.GetInfoCard.Response;
using Enums = AFCS.TOM.SbmeModels.Enums;
using MagneticArticleInfo = AFCS.TOM.SbmeDataLayer.MagneticArticleInfo;
using PaymentDetail = AFCS.TOM.SbmeDataLayer.PaymentDetail;
using PtItemArticleInfo = AFCS.TOM.SbmeDataLayer.PtItemArticleInfo;
using SaleTransaction = AFCS.TOM.SbmeDataLayer.SaleTransaction;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class ReceiptsManager : IBglServiceBase
    {
        private DataLayerContext _context { get; }
        private ReceiptData _receiptData { get; }
        public bool IsServiceEnabled { get; }

        public ReceiptsManager(IConfiguration configuration, DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            var receiptData = new ReceiptData();
            configuration.GetSection("ReceiptData").Bind(receiptData);
            _receiptData = receiptData;
            IsServiceEnabled = launchSettings.BglServicesEnabled;
            _context = context;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        private int UpdateWrongContactlessCardArticleInfos()
        {
            var wrong = _context.ContactlessCardArticleInfos
                .Where(p => !p.ReissuingReasonCode.HasValue &&
                !(string.IsNullOrWhiteSpace(p.ReissuingCscSerialLo) || string.IsNullOrWhiteSpace(p.ReissuingCscSerialPh)));
            if (wrong != null && wrong.Any())
            {
                foreach (var wr in wrong)
                {
                    wr.ReissuingReasonCode = 255;
                }
            }
            return wrong?.Count() ?? 0;
        }

        public byte[]? CreateCloseShiftReceipt(Guid agentShiftId, ReceiptTemplateType templateType, PaymentMethodReport[]? paymentMethodReports = null)
        {
            if (templateType == ReceiptTemplateType.NotSpecified) return null;
            var agentShift = _context.AgentShifts.Where(p => p.Id == agentShiftId).Include("DeviceShifts").FirstOrDefault();
            var deviceShifts = agentShift?.DeviceShifts.Select(p => p.Id.ToString().ToLower()).ToArray();
            if ((deviceShifts?.Length ?? 0) == 0) return null;

            if (paymentMethodReports != null && paymentMethodReports.Length == 0)
            {
                paymentMethodReports = null;
            }

            var updated = UpdateWrongContactlessCardArticleInfos();
            if (updated > 0)
            {
                SaveChanges();
            }

            var transactions = _context.SaleTransactions
                .Include("DeviceShift")
                .Include("Articles")
                .Include("Articles.MagneticArticleInfos")
                .Where(p => deviceShifts.Any(q => q.Equals(p.DeviceShiftId.ToString().ToLower())));

            var articleTypes = new[]
            {
                Enums.ArticleType.Contract,
                Enums.ArticleType.MagneticTicket,
                Enums.ArticleType.ContactlessCardIssued,
                Enums.ArticleType.ContactlessCardReissued,
                Enums.ArticleType.ProfileRenewal,
                Enums.ArticleType.PtItem
            };

            var undonePtArticles = _context.PtItemRefundArticleInfos
                .Where(p => transactions.Any(q => q.Id == p.SaleTransactionId))
                .Select(p => p.ArticleId)
                .ToList();

            var allArticles = transactions
                .SelectMany(p => p.Articles)
                .Where(p => articleTypes.Contains((Enums.ArticleType)p.ArticleType))
                .Include("ContactlessCardArticleInfos")
                .Include("CscContractArticleInfos")
                .Include("CscContractRefundArticleInfo")
                .Include("PtItemArticleInfos")
                .Include("PtItemRefundArticleInfo")
                .Include("MagneticRefundArticleInfo")
                .Include("MagneticArticleInfos")
                .Include("ProfileRenewalArticleInfos")
                .Where(p => (p.FromWhiteList ?? 0) == 0 &&
                    (p.MagneticArticleInfos == null || !p.MagneticArticleInfos.Any(q => q.ResultIssuingCode == 6)) &&
                    (p.PtItemArticleInfos == null || !undonePtArticles.Contains(p.Id)))
                .ToList();

            var undoingArticles = _context.CscContractRefundArticleInfos
                .Include("Article")
                .Where(p => transactions.Any(q => q.Id == p.SaleTransactionId))
                .Select(p => p.ArticleId)
                .ToList();

            var allUndoneContracts = _context.Articles
                .Where(p => undoingArticles.Contains(p.Id))
                .Include("CscContractArticleInfos")
                .ToList();

            undoingArticles = _context.MagneticRefundArticleInfos
                .Include("Article")
                .Where(p => transactions.Any(q => q.Id == p.SaleTransactionId) && transactions.Any(q => q.Id == p.Article.SaleTransactionId))
                .Select(p => p.ArticleId)
                .ToList();

            var allUndoneTickets = _context.Articles
                .Where(p => undoingArticles.Contains(p.Id))
                .Include("MagneticArticleInfos")
                .ToList();

            int getPtItemCode(PtItemArticleInfo? info)
            {
                var itemType = (Enums.PtItemType)(info?.ItemType ?? 1);
                var itemCode = info?.ItemCode ?? 0;
                switch (itemType)
                {
                    case Enums.PtItemType.Sale: return itemCode;
                    case Enums.PtItemType.Substitution: return -itemCode;
                    default: return 0;
                }
            }

            bool CmpTariffId(Article article, int tariffId) =>
                GetArticleType(article.ArticleType) == Enums.ArticleType.MagneticTicket
                    ? article.MagneticArticleInfos.FirstOrDefault()?.TariffId == tariffId
                    : CmpArticleTypes(article.ArticleType, Enums.ArticleType.Contract)
                        ? article.CscContractArticleInfos.FirstOrDefault()?.TariffId == tariffId
                        : CmpArticleTypes(article.ArticleType, Enums.ArticleType.PtItem)
                            ? getPtItemCode(article.PtItemArticleInfos.FirstOrDefault()) == tariffId
                            : CmpArticleTypes(article.ArticleType, Enums.ArticleType.ProfileRenewal)
                                ? article.ProfileRenewalArticleInfos.FirstOrDefault()?.HolderProfile == tariffId
                                : CmpArticleTypes(article.ArticleType, Enums.ArticleType.ContactlessCardIssued) ||
                                  CmpArticleTypes(article.ArticleType, Enums.ArticleType.ContactlessCardReissued)
                                    ? -article.ArticleType == tariffId
                                    : false;

            var articles = allArticles
                .GroupBy(p => new {
                    TariffId = CmpArticleTypes(p.ArticleType, Enums.ArticleType.MagneticTicket)
                        ? p.MagneticArticleInfos.FirstOrDefault()?.TariffId ?? 0
                        : CmpArticleTypes(p.ArticleType, Enums.ArticleType.Contract)
                            ? p.CscContractArticleInfos.FirstOrDefault()?.TariffId ?? 0
                            : CmpArticleTypes(p.ArticleType, Enums.ArticleType.PtItem)
                                ? getPtItemCode(p.PtItemArticleInfos.FirstOrDefault())
                                : CmpArticleTypes(p.ArticleType, Enums.ArticleType.ProfileRenewal)
                                    ? p.ProfileRenewalArticleInfos.FirstOrDefault()?.HolderProfile ?? 0
                                    : -p.ArticleType,
                    p.ArticleType
                })
                .Where(p => !(p.Key.ArticleType == (int)Enums.ArticleType.ProfileRenewal && p.Key.TariffId == -(int)Enums.ArticleType.ProfileRenewal))
                .DistinctBy(p => new {
                    p.Key.TariffId,
                    p.Key.ArticleType
                })
                .Where(p => p != null)
                .Select(p => new SoldArticle {
                    TariffId = p.Key.TariffId,
                    ArticleType = (Enums.ArticleType)p.Key.ArticleType,
                    Count = p.Count(),
                    QuantityRequired = allArticles
                        .Where(q => CmpTariffId(q, p.Key.TariffId))
                        .Sum(q => q.QuantityRequired),
                    QuantityIssued = allArticles
                        .Where(q => CmpTariffId(q, p.Key.TariffId))
                        .Sum(q => q.QuantityIssued),
                    TotalPrice = allArticles
                        .Where(q => CmpTariffId(q, p.Key.TariffId))
                        .Sum(q => q.UniquePrice * q.QuantityIssued),
                    Articles = p.ToList() //p.Key.ArticleType == (int)Enums.ArticleType.MagneticTicket ? p.ToList() : new List<Article>()
                })
                .OrderBy(p => p.TariffId)
                .ToList();

            var spoiledTickets = transactions
                .ToArray()
                .SelectMany(p => p.Articles)
                .Where(p => p.ArticleType == (int)Enums.ArticleType.MagneticTicket)
                .Select(p => p.MagneticArticleInfos.Cast<MagneticArticleInfo>())
                .Where(p => p != null && p.Count() > 0 && p.Any(q => q.ResultIssuingCode == 6))
                .OrderBy(p => p.FirstOrDefault().FirstSerialNumber)
                .ToList();

            var undoneArticles = allUndoneContracts
                .GroupBy(p => new {
                    p.ArticleType,
                    TariffId = CmpArticleTypes(p.ArticleType, Enums.ArticleType.MagneticTicket)
                        ? p.MagneticArticleInfos.First().TariffId
                        : CmpArticleTypes(p.ArticleType, Enums.ArticleType.Contract)
                            ? p.CscContractArticleInfos.First().TariffId
                            : CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardIssued) ||
                              CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardReissued)
                                ? -p.ArticleType
                                : p.ProfileRenewalArticleInfos.First().HolderProfile
                })
                .DistinctBy(p => new {
                    p.Key.TariffId
                })
                .Select(p => new {
                    p.Key.ArticleType,
                    p.Key.TariffId,
                    QuantityIssued = p.Sum(q => q.QuantityIssued),
                    Price = p.Sum(q => q.UniquePrice)
                })
                .OrderBy(p => p.TariffId)
                .ToList();

            var soldTickets = articles.Where(p => p.ArticleType == Enums.ArticleType.MagneticTicket).ToList();
            foreach (var sold in soldTickets)
            {
                sold.Articles = sold.Articles?.Where(p => !allUndoneTickets.Any(q => q.Id == p.Id)).ToList() ?? new List<Article>();
                sold.Count = sold.Articles.Count();
                sold.QuantityRequired = sold.Articles.Sum(p => p.QuantityRequired);
                sold.QuantityIssued = sold.Articles.Sum(p => p.QuantityIssued);
                sold.TotalPrice = sold.Articles.Sum(p => p.UniquePrice * p.QuantityIssued);
            }
            soldTickets = soldTickets.Where(p => p.Articles.Count() > 0).ToList();

            var totalCardsExtendedValue = transactions.SelectMany(p => p.Articles)
                ?.Where(p => p.ArticleType == (int)Enums.ArticleType.CardExpirationExtension)
                ?.Count() ?? 0;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\AgentShiftReceiptTemplate.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string tickets_tag = "#tickets#";
            const string tickets_total_tag = "#tickets_total#";
            const string cancelled_tickets_tag = "#cancelled_tickets#";
            const string spoiled_tickets_tag = "#spoiled_tickets#";
            const string tickets_spoiled_tag = "#tickets_spoiled#";
            const string contracts_tag = "#contracts#";
            const string contracts_total_tag = "#contracts_total#";
            const string cancelled_contracts_tag = "#cancelled_contracts#";
            const string cancelled_contracts_total_tag = "#cancelled_contracts_total#";
            const string issued_cards_tag = "#issued_cards#";
            const string reissued_cards_tag = "#reissued_cards#";
            const string renewed_profiles_tag = "#renewed_profiles#";
            const string renewed_profiles_total_tag = "#renewed_profiles_total#";
            const string pt_items_tag = "#pt_items#";
            const string pt_items_total_tag = "#pt_items_total#";
            const string pt_items_subst_tag = "#pt_items_subst#";
            const string pt_items_subst_total_tag = "#pt_items_subst_total_tag#";
            const string cards_extended_tag = "#cards_extended#";
            const string total_money_tag = "#total#";
            const string total_discount_tag = "#total_discount#";
            const string report_id_tag = "#report_id#";
            const string payment_methods_tag = "#payment_methods#";
            const string remove_tag = "#REM#";
            string? totalMoneyLabelArgs = null;
            string? totalDiscountLabelArgs = null;
            string? reportIdLabelArgs = null;
            var skp_company_name = false;
            var ticketsArgs = new[] { 0, 0, 0 };
            var ticketsTotalArgs = new[] { 0, 0 };
            var ticketsSpoiledArgs = new[] { 0, 0 };
            var cancelledTicketsArgs = new[] { 0, 0 };
            var spoiledTicketsArgs = new[] { 0 };
            var contractsArgs = new[] { 0, 0, 0 };
            var contractsTotalArgs = new[] { 0, 0 };
            var cancelledContractsArgs = new[] { 0, 0, 0 };
            var cancelledContractsTotalArgs = new[] { 0, 0 };
            var ptItemsArgs = new[] { 0, 0, 0 };
            var ptItemsTotalArgs = new[] { 0, 0 };
            var ptItemsSubstArgs = new[] { 0, 0, 0 };
            var ptItemsSubstTotalArgs = new[] { 0, 0 };
            var issuedCardsArgs = new[] { 0, 0 };
            var reissuedCardsArgs = new[] { 0, 0, 0 };
            var renewedProfilesArgs = new[] { 0, 0, 0 };
            var renewedProfilesTotalArgs = new[] { 0, 0 };
            var totalCardsExtended = new[] { 0 };
            var totalMoneyArgs = new[] { 0 };
            var totalDiscountArgs = new[] { 0 };
            var reportIdArgs = new[] { 0 };
            List<string>? paymentMethods = null;
            PaymentMethodReport[]? pms = paymentMethodReports;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    var sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string[] values = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //var value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //values = new[] { value };
                    //break;
                    case 1: values = new[] { NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size) }; break;
                    case 2: values = new[] { agentShift.DeviceShifts.FirstOrDefault().SaleDeviceId.PadLeft(5, '0') }; break;
                    case 3: values = new[] { agentShift.ShiftNumber.ToString() }; break;
                    case 4: values = new[] { agentShift.AgentId.ToString().PadLeft(5, '0') }; break;
                    case 5: values = new[] { agentShift.StartDate.ToString("dd/MM/yyyy HH:mm") }; break;
                    case 6:
                        values = new[] { agentShift.EndDate.HasValue
                                       ? agentShift.EndDate.Value.ToString("dd/MM/yyyy HH:mm")
                                       : string.Empty
                                   }; break;

                    case 7:
                        ticketsArgs[0] = size;
                        break;
                    case 8:
                        ticketsArgs[1] = size;
                        break;
                    case 9:
                        ticketsArgs[2] = size;
                        output.Add(tickets_tag);
                        continue;
                    case 10:
                        ticketsTotalArgs[0] = size;
                        break;
                    case 11:
                        ticketsTotalArgs[1] = size;
                        output.Add(tickets_total_tag);
                        continue;

                    case 12:
                        cancelledTicketsArgs[0] = size;
                        break;
                    case 13:
                        cancelledTicketsArgs[1] = size;
                        output.Add(cancelled_tickets_tag);
                        continue;

                    case 14:
                        spoiledTicketsArgs[0] = size;
                        output.Add(spoiled_tickets_tag);
                        continue;

                    case 15:
                        contractsArgs[0] = size;
                        break;
                    case 16:
                        contractsArgs[1] = size;
                        break;
                    case 17:
                        contractsArgs[2] = size;
                        output.Add(contracts_tag);
                        continue;
                    case 18:
                        contractsTotalArgs[0] = size;
                        break;
                    case 19:
                        contractsTotalArgs[1] = size;
                        output.Add(contracts_total_tag);
                        continue;

                    case 20:
                        cancelledContractsArgs[0] = size;
                        break;
                    case 21:
                        cancelledContractsArgs[1] = size;
                        break;
                    case 22:
                        cancelledContractsArgs[2] = size;
                        output.Add(cancelled_contracts_tag);
                        continue;
                    case 23:
                        cancelledContractsTotalArgs[0] = size;
                        break;
                    case 24:
                        cancelledContractsTotalArgs[1] = size;
                        output.Add(cancelled_contracts_total_tag);
                        continue;

                    case 25:
                        ticketsSpoiledArgs[0] = size;
                        break;
                    case 26:
                        ticketsSpoiledArgs[1] = size;
                        output.Add(tickets_spoiled_tag);
                        continue;

                    case 27:
                        issuedCardsArgs[0] = size;
                        break;
                    case 28:
                        issuedCardsArgs[1] = size;
                        output.Add(issued_cards_tag);
                        continue;

                    case 29:
                        reissuedCardsArgs[0] = size;
                        break;
                    case 30:
                        reissuedCardsArgs[1] = size;
                        break;
                    case 31:
                        reissuedCardsArgs[2] = size;
                        output.Add(reissued_cards_tag);
                        continue;

                    case 32:
                        renewedProfilesArgs[0] = size;
                        break;
                    case 33:
                        renewedProfilesArgs[1] = size;
                        break;
                    case 34:
                        renewedProfilesArgs[2] = size;
                        output.Add(renewed_profiles_tag);
                        continue;
                    case 35:
                        renewedProfilesTotalArgs[0] = size;
                        break;
                    case 36:
                        renewedProfilesTotalArgs[1] = size;
                        output.Add(renewed_profiles_total_tag);
                        continue;

                    case 37:
                        ptItemsArgs[0] = size;
                        break;
                    case 38:
                        ptItemsArgs[1] = size;
                        break;
                    case 39:
                        ptItemsArgs[2] = size;
                        output.Add(pt_items_tag);
                        continue;
                    case 40:
                        ptItemsTotalArgs[0] = size;
                        break;
                    case 41:
                        ptItemsTotalArgs[1] = size;
                        output.Add(pt_items_total_tag);
                        continue;

                    case 42:
                        ptItemsSubstArgs[0] = size;
                        break;
                    case 43:
                        ptItemsSubstArgs[1] = size;
                        break;
                    case 44:
                        ptItemsSubstArgs[2] = size;
                        output.Add(pt_items_subst_tag);
                        continue;
                    case 45:
                        ptItemsSubstTotalArgs[0] = size;
                        break;
                    case 46:
                        ptItemsSubstTotalArgs[1] = size;
                        output.Add(pt_items_subst_total_tag);
                        continue;

                    case 47:
                        totalCardsExtended[0] = size;
                        output.Add(cards_extended_tag);
                        output.Add(fileData[i]);
                        continue;

                    case 48:
                        totalMoneyArgs[0] = size;
                        totalMoneyLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                        output.Add(total_money_tag);
                        continue;

                    case 49:
                        totalDiscountArgs[0] = size;
                        totalDiscountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                        output.Add(total_discount_tag);
                        continue;

                    case 50:
                        {
                            pms = pms ?? GetAgentShiftPaymentMethods(agentShiftId)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (pms[j].Amount / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;

                    case 51:
                        reportIdArgs[0] = size;
                        reportIdLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                        output.Add(report_id_tag);
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (values != null)
                    foreach (var value in values)
                    {
                        var insert = size == 0
                        ? value
                        : value.PadLeft(size, ' ');
                        if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                        fileData[i] = $"{beginning}{insert}{ending}";
                    }
                --i;
            }

            var totalMoney = 0.0M;
            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(tickets_tag);
            if (pos >= 0)
            {
                var at2 = soldTickets.Where(p => p.ArticleType == Enums.ArticleType.MagneticTicket && p.TariffId != 0).ToArray();
                if (at2.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at2)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(ticketsArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ticketsArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ticketsArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(tickets_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at2.Sum(p => p.QuantityIssued),
                            at2.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ticketsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ticketsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(tickets_spoiled_tag);
            if (pos >= 0)
                output.RemoveRange(pos - 2, 4);
            pos = output.IndexOf(cancelled_tickets_tag);
            if (pos >= 0)
            {
                var ua = allUndoneTickets.ToArray();
                if (ua.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in ua)
                    {
                        var info = a.MagneticArticleInfos?.FirstOrDefault();
                        if (info == null) continue;
                        var args = new List<string>
                        {
                            ExtractContractSerial(info.FirstSerialNumber ?? 0).ToString().PadLeft(10, '0').PadRight(cancelledTicketsArgs[0], ' ') + " " +
                            a.QuantityIssued.ToString().PadLeft(cancelledTicketsArgs[1], ' ')
                        };
                        args = args.Where(p => !string.IsNullOrWhiteSpace(p) && !p.Contains(remove_tag)).ToList();
                        output.InsertRange(pos, args);
                        pos += args.Count;
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(spoiled_tickets_tag);
            if (pos >= 0)
            {
                var st = spoiledTickets.ToArray();
                if (st.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var t in st)
                    {
                        var args = new List<string>
                        {
                            ExtractContractSerial(t.FirstOrDefault().FirstSerialNumber ?? 0).ToString().PadLeft(10, '0').PadRight(spoiledTicketsArgs[0], ' ')
                        };
                        args = args.Where(p => !string.IsNullOrWhiteSpace(p) && !p.Contains(remove_tag)).ToList();
                        output.InsertRange(pos, args);
                        pos += args.Count;
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(contracts_tag);
            if (pos >= 0)
            {
                var at1 = articles.Where(p => p.ArticleType == Enums.ArticleType.Contract && p.TariffId != 0).ToArray();
                if (at1.Length > 0)
                {
                    output.RemoveAt(pos);
                    var qTotal = 0;
                    foreach (var a in at1)
                    {
                        var args = new string[0];
                        if (a.ArticleType == Enums.ArticleType.Contract)
                        {
                            var numberOfUnits = a.Articles.Select(p => {
                                var info = p.CscContractArticleInfos?.FirstOrDefault();
                                if (info != null)
                                {
                                    return (info.NumberOfUnits ?? 1) * (info.Article?.QuantityIssued ?? 0);
                                }
                                else
                                {
                                    return 0;
                                }
                            });
                            var sum = numberOfUnits.Sum();
                            qTotal += sum;
                            args = new[]
                            {
                                a.TariffId.ToString().PadRight(contractsArgs[0], ' '),
                                sum.ToString().PadLeft(contractsArgs[1], ' '),
                                (a.TotalPrice / 100.0M).ToString("F2").PadLeft(contractsArgs[2], ' ')
                            };
                        }
                        else
                        {
                            qTotal += a.QuantityIssued;
                            args = new[]
                            {
                                a.TariffId.ToString().PadRight(contractsArgs[0], ' '),
                                a.QuantityIssued.ToString().PadLeft(contractsArgs[1], ' '),
                                (a.TotalPrice / 100.0M).ToString("F2").PadLeft(contractsArgs[2], ' ')
                            };
                        }
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(contracts_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            //at1.Sum(p => p.QuantityIssued),
                            qTotal,
                            at1.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(contractsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(contractsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(cancelled_contracts_tag);
            if (pos >= 0)
            {
                var ua = undoneArticles.Where(p => p.ArticleType == (int)Enums.ArticleType.Contract).ToArray();
                if (ua.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in ua)
                    {
                        var args = new List<string>
                        {
					        //a.ContractId.ToString().PadRight(cancelledContractsArgs[0], ' ') + " " + (a.Price / 100.0M).ToString("F2").PadLeft(cancelledContractsArgs[1], ' ')
					        a.TariffId.ToString().PadRight(cancelledContractsArgs[0], ' ') + " " +
                            a.QuantityIssued.ToString().PadLeft(cancelledContractsArgs[1], ' ') + " " +
                            (a.Price / 100.0M).ToString("F2").PadLeft(cancelledContractsArgs[2], ' ')
                        };
                        args = args.Where(p => !string.IsNullOrWhiteSpace(p) && !p.Contains(remove_tag)).ToList();
                        output.InsertRange(pos, args);
                        pos += args.Count;
                    }

                    pos = output.IndexOf(cancelled_contracts_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            ua.Sum(p => p.QuantityIssued),
                            ua.Sum(p => p.Price)
                        };
                        totalMoney -= total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(cancelledContractsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(cancelledContractsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(pt_items_tag);
            if (pos >= 0)
            {
                var at9 = articles.Where(p => p.ArticleType == Enums.ArticleType.PtItem && p.TariffId > 0).ToArray();
                if (at9.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at9)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(ptItemsArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ptItemsArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ptItemsArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(pt_items_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at9.Sum(p => p.QuantityIssued),
                            at9.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ptItemsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ptItemsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(pt_items_subst_tag);
            if (pos >= 0)
            {
                var at9 = articles.Where(p => p.ArticleType == Enums.ArticleType.PtItem && p.TariffId < 0).ToArray();
                if (at9.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at9)
                    {
                        var args = new[]
                        {
                            Math.Abs(a.TariffId).ToString().PadRight(ptItemsSubstArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ptItemsSubstArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ptItemsSubstArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(pt_items_subst_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at9.Sum(p => p.QuantityIssued),
                            at9.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ptItemsSubstTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ptItemsSubstTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(issued_cards_tag);
            if (pos >= 0)
            {
                var at3 = articles.Where(p => p.ArticleType == Enums.ArticleType.ContactlessCardIssued && p.TariffId != 0).ToArray();
                if (at3.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at3)
                    {
                        totalMoney += a.TotalPrice;
                        var args = new[]
                        {
                            a.Count.ToString().PadRight(issuedCardsArgs[0], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(issuedCardsArgs[1], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(reissued_cards_tag);
            if (pos >= 0)
            {
                var at4 = articles.Where(p => p.ArticleType == Enums.ArticleType.ContactlessCardReissued && p.TariffId != 0).ToArray();
                if (at4.Length > 0)
                {
                    output.RemoveAt(pos);

                    var ids = at4.SelectMany(p => p.Articles)?.Select(p => p.Id)?.Distinct()?.ToArray();
                    var ok = false;

                    if (ids?.Any() ?? false)
                    {
                        var methods = _context.ContactlessCardReissuingReasons.ToDictionary(key => key.Code, value => value.Name);
                        var group = _context.ContactlessCardArticleInfos
                            ?.Where(p => ids.Contains(p.ArticleId) && p.ReissuingReasonCode.HasValue)
                            ?.Include("Article")
                            ?.GroupBy(p => new {
                                ReasonCode = p.ReissuingReasonCode
                            })
                            ?.Select(p => new {
                                Reason = methods.ContainsKey(p.Key.ReasonCode!.Value) ? methods[p.Key.ReasonCode.Value] : p.Key.ReasonCode.ToString(),
                                TotalPrice = p.Sum(q => q.Article == null ? 0M : q.Article.UniquePrice * q.Article.QuantityIssued),
                                Count = p.Count()
                            })
                            ?.ToArray();

                        if (group?.Any() ?? false)
                        {
                            totalMoney += group.Sum(p => p.TotalPrice);
                            foreach (var item in group)
                            {
                                var args = new List<string>();
                                var reason = (item.Reason?.ToString() ?? "?").PadRight(reissuedCardsArgs[0], ' ').ToUpper();
                                if (reason.Length > reissuedCardsArgs[0])
                                {
                                    reason = reason.Substring(0, reissuedCardsArgs[0]);
                                }
                                var qty = item.Count.ToString().PadLeft(reissuedCardsArgs[1], ' ');
                                var total = (item.TotalPrice / 100.0M).ToString("F2").PadLeft(reissuedCardsArgs[2], ' ');
                                args.Add(reason!);
                                args.Add(qty);
                                args.Add(total);
                                var line = string.Join(' ', args);
                                output.Insert(pos++, line);
                            }
                            ok = true;
                        }
                    }
                    
                    if (!ok)
                    {
                        foreach (var a in at4)
                        {
                            totalMoney += a.TotalPrice;
                            var reason = "SCADENZA".PadRight(reissuedCardsArgs[0], ' ').ToUpper();
                            if (reason.Length > reissuedCardsArgs[0])
                            {
                                reason = reason.Substring(0, reissuedCardsArgs[0]);
                            }
                            var args = new[]
                            {
                                reason,
                                a.Count.ToString().PadRight(reissuedCardsArgs[1], ' '),
                                (a.TotalPrice / 100.0M).ToString("F2").PadLeft(reissuedCardsArgs[2], ' ')
                            };
                            var line = string.Join(' ', args);
                            output.Insert(pos++, line);
                        }
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(renewed_profiles_tag);
            if (pos >= 0)
            {
                var at6 = articles.Where(p => p.ArticleType == Enums.ArticleType.ProfileRenewal && p.TariffId != 0).ToArray();
                if (at6.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at6)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(renewedProfilesArgs[0], ' '),
                            a.Count.ToString().PadLeft(renewedProfilesArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(renewedProfilesArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(renewed_profiles_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at6.Sum(p => p.QuantityIssued),
                            at6.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(renewedProfilesTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(renewedProfilesTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(cards_extended_tag);
            if (pos >= 0)
            {
                if (totalCardsExtendedValue == 0)
                {
                    output.RemoveRange(pos, 3);
                }
                else
                {
                    output.RemoveAt(pos);
                    var line = output[pos];
                    var pos2 = line.LastIndexOf('#');
                    pos2 = line.LastIndexOf('#', pos2 - 1);
                    line = line.Substring(0, pos2).TrimEnd() + " " + $"{totalCardsExtendedValue}".PadLeft(totalCardsExtended[0], ' ');
                    output[pos] = line;
                }
            }
            pos = output.IndexOf(total_money_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                totalMoneyLabelArgs = totalMoneyLabelArgs?.PadRight(maxLength - totalMoneyArgs[0], ' ');

                var pds = _context.SaleTransactions
                    .Where(p => deviceShifts.Any(q => q.Equals(p.DeviceShiftId.ToString().ToLower())))
                    //.Where(p => p.DeviceShiftId.Equals(ds.Id))
                    .Include("PaymentDetails")
                    .SelectMany(p => p.PaymentDetails)
                    .ToArray();
                var where = pds.Where(p => p.FullPrice.HasValue);
                var discount = (where?.Any() ?? false) ? where?.Select(p => p.FullPrice - p.Amount)?.Sum() : null;

                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0)
                {
                    output.RemoveAt(pos2);
                }
                if (discount.HasValue)
                {
                    totalMoney -= discount.Value;
                }

                var args = new[]
                {
                    totalMoneyLabelArgs + (totalMoney / 100.0M).ToString("F2").ToString().PadLeft(totalMoneyArgs[0], ' ')
                };
                var line = string.Join(' ', args);
                output.Insert(pos++, line);

                if (pos2 >= 0 && discount.HasValue)
                {
                    args = new[]
                    {
                        totalDiscountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(totalDiscountArgs[0], ' ')
                    };
                    line = string.Join(' ', args);
                    output.Insert(pos++, line);
                }
            }
            pos = output.IndexOf(report_id_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                reportIdLabelArgs = reportIdLabelArgs?.PadRight(maxLength - reportIdArgs[0], ' ');
                var reportId = (agentShift?.ReportId ?? 0) > 200
                    ? agentShift!.ReportId.ToString()
                    : "***";
                var args = new[]
                {
                    reportIdLabelArgs + reportId.PadLeft(reportIdArgs[0], ' ')
                };
                var line = string.Join(' ', args);
                output.Insert(pos++, line);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if ((paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines($@"D:\receipts\es.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            return res;
        }

        private static void ManageInvoice(bool withInvoice, ref List<string> output, bool single = true)
        {
            var invoice_tag = single ? "~INVOICE1~" : "~INVOICE2~";
            var pos = output.IndexOf(invoice_tag);
            if (pos >= 0)
            {
                if (!withInvoice)
                {
                    output.RemoveAt(pos);
                    output.RemoveAt(pos);
                }
                else
                {
                    var maxLength = GetLineMaxLength(output);
                    var text = single
                        ? "PRODOTTO OGGETTO DI FATTURA"
                        : "PRODOTTI OGGETTI DI FATTURA";
                    text = text.Replace("\\n", "\n").Replace("\\r", "\r");
                    text = ReceiptData.GetStrinAllignToCenter(text, maxLength);
                    output[pos] = text;
                }
            }
        }

        public async Task<byte[]?> CreateTransactionReceipt(Guid transactionId, ReceiptTemplateType templateType)
        {
            var transaction = _context.SaleTransactions
                .Include("DeviceShift")
                .Include("Articles")
                .Where(p => transactionId.Equals(p.Id));

            var tran = transaction.FirstOrDefault();
            var receipt = tran?.Receipt;

            var articleTypes = new[]
            {
                Enums.ArticleType.Contract,
                Enums.ArticleType.MagneticTicket,
                Enums.ArticleType.ContactlessCardIssued,
                Enums.ArticleType.ContactlessCardReissued,
                Enums.ArticleType.ProfileRenewal,
                Enums.ArticleType.PtItem
            };

            var undonePtArticles = _context.PtItemRefundArticleInfos
                .Where(p => p.SaleTransactionId == transactionId)
                .Select(p => p.ArticleId)
                .ToList();

            var allArticles = transaction
                .SelectMany(p => p.Articles)
                .Where(p => articleTypes.Contains((Enums.ArticleType)p.ArticleType))
                .Include("ContactlessCardArticleInfos")
                .Include("CscContractArticleInfos")
                .Include("CscContractRefundArticleInfo")
                .Include("PtItemArticleInfos")
                .Include("PtItemRefundArticleInfo")
                .Include("MagneticRefundArticleInfo")
                .Include("MagneticArticleInfos")
                .Include("ProfileRenewalArticleInfos")
                .Where(p => (p.CscContractRefundArticleInfo == null || p.MagneticRefundArticleInfo != null) &&
                    (p.MagneticArticleInfos == null || !p.MagneticArticleInfos.Any(q => q.ResultIssuingCode == 6)) &&
                    (p.PtItemArticleInfos == null || !undonePtArticles.Contains(p.Id)))
                .ToList();

            var undoingArticles = _context.CscContractRefundArticleInfos
                .Include("Article")
                .Where(p => p.SaleTransactionId == transactionId && p.Article.SaleTransactionId != transactionId)
                .Select(p => p.ArticleId)
                .ToList();

            var allUndoneContracts = _context.Articles
                .Where(p => undoingArticles.Contains(p.Id))
                .Include("CscContractArticleInfos")
                .ToList();

            undoingArticles = _context.MagneticRefundArticleInfos
                .Include("Article")
                .Where(p => p.SaleTransactionId == transactionId && p.Article.SaleTransactionId == transactionId)
                .Select(p => p.ArticleId)
                .ToList();

            var allUndoneTickets = _context.Articles
                .Where(p => undoingArticles.Contains(p.Id))
                .Include("MagneticArticleInfos")
                .ToList();

            int getPtItemCode(PtItemArticleInfo? info)
            {
                var itemType = (Enums.PtItemType)(info?.ItemType ?? 1);
                var itemCode = info?.ItemCode ?? 0;
                switch (itemType)
                {
                    case Enums.PtItemType.Sale: return itemCode;
                    case Enums.PtItemType.Substitution: return -itemCode;
                    default: return 0;
                }
            }

            bool CmpTariffId(Article article, int tariffId) =>
                CmpArticleTypes(article.ArticleType, Enums.ArticleType.MagneticTicket)
                    ? (article.MagneticArticleInfos.First()?.TariffId ?? 0) == tariffId
                    : CmpArticleTypes(article.ArticleType, Enums.ArticleType.Contract)
                        ? (article.CscContractArticleInfos.First()?.TariffId ?? 0) == tariffId
                        : CmpArticleTypes(article.ArticleType, Enums.ArticleType.PtItem)
                            ? getPtItemCode(article.PtItemArticleInfos.FirstOrDefault()) == tariffId
                            : CmpArticleTypes(article.ArticleType, Enums.ArticleType.ContactlessCardIssued) ||
                              CmpArticleTypes(article.ArticleType, Enums.ArticleType.ContactlessCardReissued)
                                ? -article.ArticleType == tariffId
                                : (article.ProfileRenewalArticleInfos.First()?.HolderProfile ?? 0) == tariffId;

            var articles = allArticles
                .GroupBy(p => new {
                    TariffId = CmpArticleTypes(p.ArticleType, Enums.ArticleType.MagneticTicket)
                        ? p.MagneticArticleInfos.First().TariffId
                        : CmpArticleTypes(p.ArticleType, Enums.ArticleType.Contract)
                            ? p.CscContractArticleInfos.First().TariffId
                            : CmpArticleTypes(p.ArticleType, Enums.ArticleType.PtItem)
                                ? getPtItemCode(p.PtItemArticleInfos.FirstOrDefault())
                                : CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardIssued) ||
                                  CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardReissued)
                                    ? -p.ArticleType
                                    : p.ProfileRenewalArticleInfos.First().HolderProfile,
                    p.ArticleType
                })
                .DistinctBy(p => new {
                    p.Key.TariffId,
                    p.Key.ArticleType
                })
                .Select(p => new SoldArticle {
                    TariffId = p.Key.TariffId,
                    ArticleType = (Enums.ArticleType)p.Key.ArticleType,
                    Count = p.Count(),
                    QuantityRequired = p.Sum(q => q.QuantityRequired),
                    QuantityIssued = p.Sum(q => q.QuantityIssued),
                    TotalPrice = p.Sum(q => q.UniquePrice * q.QuantityIssued),
                    Articles = p.ToList()
                })
                .OrderBy(p => p.TariffId)
                .ToList();

            var undoneArticles = allUndoneContracts
                .GroupBy(p => new {
                    p.ArticleType,
                    TariffId = CmpArticleTypes(p.ArticleType, Enums.ArticleType.MagneticTicket)
                        ? p.MagneticArticleInfos.First().TariffId
                        : CmpArticleTypes(p.ArticleType, Enums.ArticleType.Contract)
                            ? p.CscContractArticleInfos.First().TariffId
                            : CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardIssued) ||
                              CmpArticleTypes(p.ArticleType, Enums.ArticleType.ContactlessCardReissued)
                                ? -p.ArticleType
                                : p.ProfileRenewalArticleInfos.First().HolderProfile,
                    p.Description,
                    ContractId = CmpArticleTypes(p.ArticleType, Enums.ArticleType.Contract)
                        ? $"{p.CscContractArticleInfos.First().VtContractId}|{p.CscContractArticleInfos.First().CardSerialNumber}"
                        : null,
                    TotalPrice = p.UniquePrice * p.QuantityIssued
                })
                .Select(p => new {
                    p.Key.ArticleType,
                    p.Key.TariffId,
                    ContractId = p.Key.ContractId ?? string.Empty,
                    p.Key.Description,
                    p.Key.TotalPrice
                })
                .OrderBy(p => p.TariffId)
                .ToList();

            var soldTickets = articles.Where(p => p.ArticleType == Enums.ArticleType.MagneticTicket).ToList();
            foreach (var sold in soldTickets)
            {
                sold.Articles = sold.Articles?.Where(p => !allUndoneTickets.Any(q => q.Id == p.Id)).ToList() ?? new List<Article>();
                sold.Count = sold.Articles.Count();
                sold.QuantityRequired = sold.Articles.Sum(p => p.QuantityRequired);
                sold.QuantityIssued = sold.Articles.Sum(p => p.QuantityIssued);
                sold.TotalPrice = sold.Articles.Sum(p => p.UniquePrice * p.QuantityIssued);
            }
            soldTickets = soldTickets.Where(p => p.Articles.Count() > 0).ToList();

            tran.DeviceShift.AgentShift = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(tran.DeviceShift.AgentShiftId));

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\SaleTransactionReceiptTemplate.txt");
            var output = new List<string>();
            var code = 0;
            PaymentMethodReport[]? pms = null;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string tickets_tag = "#tickets#";
            const string tickets_total_tag = "#tickets_total#";
            const string contracts_tag = "#contracts#";
            const string contracts_total_tag = "#contracts_total#";
            const string cancelled_contracts_tag = "#cancelled_contracts#";
            const string issued_cards_tag = "#issued_cards#";
            const string reissued_cards_tag = "#reissued_cards#";
            const string renewed_profiles_tag = "#renewed_profiles#";
            const string renewed_profiles_total_tag = "#renewed_profiles_total#";
            const string pt_items_tag = "#pt_items#";
            const string pt_items_total_tag = "#pt_items_total#";
            const string pt_items_subst_tag = "#pt_items_subst#";
            const string pt_items_subst_total_tag = "#pt_items_subst_total#";
            const string payment_methods_tag = "#payment_methods#";
            const string total_money_tag = "#total#";
            const string total_discount_pct_tag = "#discount_pct#";
            const string total_discount_tag = "#discount#";
            const string remove_tag = "#REM#";
            string? totalMoneyLabelArgs = null;
            string? discountPctLabelArgs = null;
            string? discountLabelArgs = null;
            var skp_company_name = false;
            var ticketsArgs = new[] { 0, 0, 0 };
            var ticketsTotalArgs = new[] { 0, 0 };
            var contractsArgs = new[] { 0, 0, 0 };
            var contractsTotalArgs = new[] { 0, 0 };
            var ptItemsArgs = new[] { 0, 0, 0 };
            var ptItemsTotalArgs = new[] { 0, 0 };
            var ptItemsSubstArgs = new[] { 0, 0, 0 };
            var ptItemsSubstTotalArgs = new[] { 0, 0 };
            var contractsLabelsArgs = new[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };
            var contractsDetailsArgs = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            var cancelledContractsArgs = new[] { 0, 0, 0, 0 };
            var issuedCardsArgs = new[] { 0, 0 };
            var reissuedCardsArgs = new[] { 0, 0 };
            var renewedProfilesArgs = new[] { 0, 0, 0 };
            var renewedProfilesTotalArgs = new[] { 0, 0 };
            var totalMoneyArgs = new[] { 0 };
            var discountArgs = new[] { 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    var sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string[] values = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    /*{
                        // TODO: https://jira.easyticketing.it/browse/CAR-2122
                        var value = _receiptData.CompanyNameFormatted;
                        //                            var value = @"
                        //ATM S.P.A.
                        //Azienda Trasporti
                        //Milanese
                        //Foro Buonaparte 61
                        //20121 Milano

                        //P.I 12883390150
                        //R.1. 97230720159

                        //Infoline ATM
                        //02.48.607.607
                        //www.atm.it
                        //";
                        if (size > 0)
                        {
                            var padLeft = (size - value.Length) / 2;
                            var padRight = size - value.Length - padLeft;
                            if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                            if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                        }
                        values = new[] { value };
                    }
                    break;*/
                    case 1: values = new[] { NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size) }; break;
                    case 2: values = new[] { tran.DeviceShift.SaleDeviceId.PadLeft(5, '0') }; break;
                    case 3: values = new[] { tran.DeviceShift.AgentShift.ShiftNumber.ToString() }; break;
                    case 4: values = new[] { tran.DeviceShift.AgentShift.AgentId.ToString().PadLeft(5, '0') }; break;

                    case 5:
                        ticketsArgs[0] = size;
                        break;
                    case 6:
                        ticketsArgs[1] = size;
                        break;
                    case 7:
                        ticketsArgs[2] = size;
                        output.Add(tickets_tag);
                        continue;
                    case 8:
                        ticketsTotalArgs[0] = size;
                        break;
                    case 9:
                        ticketsTotalArgs[1] = size;
                        output.Add(tickets_total_tag);
                        continue;

                    case 10:
                        contractsArgs[0] = size;
                        break;
                    case 11:
                        contractsArgs[1] = size;
                        break;
                    case 12:
                        contractsArgs[2] = size;
                        output.Add(contracts_tag);
                        continue;
                    case 13:
                        contractsTotalArgs[0] = size;
                        break;
                    case 14:
                        contractsTotalArgs[1] = size;
                        output.Add(contracts_total_tag);
                        continue;

                    case 15:
                        cancelledContractsArgs[0] = size;
                        break;
                    case 16:
                        cancelledContractsArgs[1] = size;
                        continue;
                    case 17:
                        cancelledContractsArgs[2] = size;
                        break;
                    case 18:
                        cancelledContractsArgs[3] = size;
                        output.Add(cancelled_contracts_tag);
                        continue;

                    case 19:
                        issuedCardsArgs[0] = size;
                        break;
                    case 20:
                        issuedCardsArgs[1] = size;
                        output.Add(issued_cards_tag);
                        continue;

                    case 21:
                        reissuedCardsArgs[0] = size;
                        break;
                    case 22:
                        reissuedCardsArgs[1] = size;
                        output.Add(reissued_cards_tag);
                        continue;

                    case 23:
                        renewedProfilesArgs[0] = size;
                        break;
                    case 24:
                        renewedProfilesArgs[1] = size;
                        break;
                    case 25:
                        renewedProfilesArgs[2] = size;
                        output.Add(renewed_profiles_tag);
                        continue;
                    case 26:
                        renewedProfilesTotalArgs[0] = size;
                        break;
                    case 27:
                        renewedProfilesTotalArgs[1] = size;
                        output.Add(renewed_profiles_total_tag);
                        continue;

                    case 28:
                        ptItemsArgs[0] = size;
                        break;
                    case 29:
                        ptItemsArgs[1] = size;
                        break;
                    case 30:
                        ptItemsArgs[2] = size;
                        output.Add(pt_items_tag);
                        continue;
                    case 31:
                        ptItemsTotalArgs[0] = size;
                        break;
                    case 32:
                        ptItemsTotalArgs[1] = size;
                        output.Add(pt_items_total_tag);
                        continue;

                    case 33:
                        ptItemsSubstArgs[0] = size;
                        break;
                    case 34:
                        ptItemsSubstArgs[1] = size;
                        break;
                    case 35:
                        ptItemsSubstArgs[2] = size;
                        output.Add(pt_items_subst_tag);
                        continue;
                    case 36:
                        ptItemsSubstTotalArgs[0] = size;
                        break;
                    case 37:
                        ptItemsSubstTotalArgs[1] = size;
                        output.Add(pt_items_subst_total_tag);
                        continue;

                    case 38:
                        {
                            //pms = pms ?? GetTransactionPaymentMethods(context, tran.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            //var total = pms?.Select(p => p.Amount)?.Sum() ?? 0M;
                            //var value = (total / 100.0M).ToString("F2").PadLeft(size, ' ');
                            //values = new[] { value };
                            totalMoneyArgs[0] = size;
                            totalMoneyLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_money_tag);
                        }
                        break;

                    case 39:
                        {
                            discountArgs[0] = size;
                            discountPctLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_pct_tag);
                        }
                        break;
                    case 40:
                        {
                            discountArgs[1] = size;
                            discountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_tag);
                        }
                        break;

                    case 41:
                        {
                            // TODO: temporarily disabled
                            //pms = pms ?? GetTransactionPaymentMethods(tran.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            //if ((pms?.Length ?? 0) > 0)
                            //{
                            //    paymentMethods = new List<string>();
                            //    for (var j = 0; j < pms.Length; ++j)
                            //    {
                            //        if (pms[j].Description == null) continue;
                            //        var ins = (pms[j].Amount / 100.0M).ToString("F2").PadLeft(size, ' ');
                            //        ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                            //        paymentMethods.Add(ins);
                            //    }
                            //}
                            output.Add(payment_methods_tag);
                        }
                        break;

                    case 42:
                        {
                            var value = tran.VtTransactionId;
                            if (size > 0)
                            {
                                var padLeft = (size - value.Length) / 2;
                                var padRight = size - value.Length - padLeft;
                                if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                                if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                            }
                            values = new[] { value };
                        }
                        break;

                    case 43:
                        {
                            var value = tran.TransactionTime.ToString("dd/MM/yyyy HH:mm");
                            if (size > 0)
                            {
                                var padLeft = (size - value.Length) / 2;
                                var padRight = size - value.Length - padLeft;
                                if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                                if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                            }
                            values = new[] { value };
                        }
                        break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (values != null)
                    foreach (var value in values)
                    {
                        var insert = size == 0
                        ? value
                        : value.PadLeft(size, ' ');
                        if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                        fileData[i] = $"{beginning}{insert}{ending}";
                    }
                --i;
            }

            var totalMoney = 0.0M;
            var maxLength = GetLineMaxLength(output);

            var printPaymentMethods = false;

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(tickets_tag);
            if (pos >= 0)
            {
                var at2 = soldTickets.ToArray();
                if (at2.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at2)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(ticketsArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ticketsArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ticketsArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(tickets_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at2.Sum(p => p.QuantityIssued),
                            at2.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ticketsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ticketsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(contracts_tag);
            if (pos >= 0)
            {
                var at1 = articles.Where(p => p.ArticleType == Enums.ArticleType.Contract).ToArray();
                if (at1.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at1)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(contractsArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(contractsArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(contractsArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(contracts_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at1.Sum(p => p.QuantityIssued),
                            at1.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(contractsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(contractsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(cancelled_contracts_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var ua = undoneArticles.Where(p => p.ArticleType == (int)Enums.ArticleType.Contract).ToArray();
                if (ua.Length > 0)
                {
                    var isFirst = true;
                    foreach (var a in ua)
                    {
                        var description = a.Description.PadLeft(cancelledContractsArgs[3], ' ');
                        if (description.Length > cancelledContractsArgs[3])
                            description = description.Substring(0, cancelledContractsArgs[3]);
                        totalMoney -= a.TotalPrice;
                        var split = a.ContractId?.Split('|') ?? new string?[] { null, null };
                        var args = new List<string>
                        {
                            split[1] ?? remove_tag,
                            (split[0]?.ToString().PadRight(cancelledContractsArgs[0], ' ') ?? remove_tag) + " " + ((a.TotalPrice / 100.0M).ToString("F2").PadLeft(cancelledContractsArgs[1], ' ') ?? remove_tag),
                            a.TariffId.ToString().PadRight(cancelledContractsArgs[2], ' ') + " " + (description.PadLeft(cancelledContractsArgs[3], ' ') ?? remove_tag),
                        };
                        args = args.Where(p => !string.IsNullOrWhiteSpace(p) && !p.Contains(remove_tag)).ToList();
                        if (!isFirst) args.Insert(0, GetAsterisks(maxLength));
                        output.InsertRange(pos, args);
                        pos += args.Count;
                        isFirst = false;
                    }
                }
                else
                    output.RemoveRange(pos - 1, 2);
            }
            pos = output.IndexOf(pt_items_tag);
            if (pos >= 0)
            {
                var at9 = articles.Where(p => p.ArticleType == Enums.ArticleType.PtItem && p.TariffId > 0).ToArray();
                if (at9.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at9)
                    {
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(ptItemsArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ptItemsArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ptItemsArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(pt_items_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at9.Sum(p => p.QuantityIssued),
                            at9.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ptItemsTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ptItemsTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(pt_items_subst_tag);
            if (pos >= 0)
            {
                var at9 = articles.Where(p => p.ArticleType == Enums.ArticleType.PtItem && p.TariffId < 0).ToArray();
                if (at9.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at9)
                    {
                        var args = new[]
                        {
                            Math.Abs(a.TariffId).ToString().PadRight(ptItemsSubstArgs[0], ' '),
                            a.QuantityIssued.ToString().PadLeft(ptItemsSubstArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(ptItemsSubstArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(pt_items_subst_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at9.Sum(p => p.QuantityIssued),
                            at9.Sum(p => p.TotalPrice)
                        };
                        totalMoney += total[1];
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(ptItemsSubstTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(ptItemsSubstTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(issued_cards_tag);
            if (pos >= 0)
            {
                var at3 = articles.Where(p => p.ArticleType == Enums.ArticleType.ContactlessCardIssued).ToArray();
                if (at3.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at3)
                    {
                        totalMoney += a.TotalPrice;
                        var args = new[]
                        {
                            a.Count.ToString().PadRight(issuedCardsArgs[0], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(issuedCardsArgs[1], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(reissued_cards_tag);
            if (pos >= 0)
            {
                var at4 = articles.Where(p => p.ArticleType == Enums.ArticleType.ContactlessCardReissued).ToArray();
                if (at4.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at4)
                    {
                        totalMoney += a.TotalPrice;
                        var args = new[]
                        {
                            a.Count.ToString().PadRight(reissuedCardsArgs[0], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(reissuedCardsArgs[1], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 4);
            }
            pos = output.IndexOf(renewed_profiles_tag);
            if (pos >= 0)
            {
                var at6 = articles.Where(p => p.ArticleType == Enums.ArticleType.ProfileRenewal).ToArray();
                if (at6.Length > 0)
                {
                    output.RemoveAt(pos);
                    foreach (var a in at6)
                    {
                        totalMoney += a.TotalPrice;
                        var args = new[]
                        {
                            a.TariffId.ToString().PadRight(renewedProfilesArgs[0], ' '),
                            a.Count.ToString().PadLeft(renewedProfilesArgs[1], ' '),
                            (a.TotalPrice / 100.0M).ToString("F2").PadLeft(renewedProfilesArgs[2], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }

                    pos = output.IndexOf(renewed_profiles_total_tag);
                    if (pos >= 0)
                    {
                        output.RemoveAt(pos);
                        var total = new[]
                        {
                            at6.Sum(p => p.QuantityIssued),
                            at6.Sum(p => p.TotalPrice)
                        };
                        var args = new[]
                        {
                            total[0].ToString().PadLeft(renewedProfilesTotalArgs[0], ' '),
                            (total[1] / 100.0M).ToString("F2").PadLeft(renewedProfilesTotalArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos, line);
                    }
                }
                else
                    output.RemoveRange(pos - 2, 6);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (printPaymentMethods && (paymentMethods?.Count ?? 0) > 0)
                {
                    output.RemoveAt(pos);
                    output.InsertRange(pos, paymentMethods);
                }
                else
                    output.RemoveRange(pos - 1, 3);
            }
            pos = output.IndexOf(total_money_tag);
            if (pos >= 0)
            {
                output.RemoveRange(pos, 2);
                totalMoneyLabelArgs = totalMoneyLabelArgs?.PadRight(maxLength - totalMoneyArgs[0], ' ');
                var args = new[]
                {
                    totalMoneyLabelArgs + (totalMoney / 100.0M).ToString("F2").ToString().PadLeft(totalMoneyArgs[0], ' ')
                };
                var line = string.Join(' ', args);
                output.Insert(pos++, line);
            }
            pos = output.IndexOf(total_discount_pct_tag);
            if (pos >= 0)
            {
                var n = 3;
                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0) ++n;
                output.RemoveRange(pos, n);
                var first = _context.PaymentDetails.Where(p => p.SaleTransactionId.Equals(transactionId))?.FirstOrDefault(p => p.PtDiscount.HasValue);
                var discountPct = first != null && first.PtDiscount.HasValue ? first.PtDiscount.Value / 100M : 0M;
                var discount = first?.Amount;
                if (discountPct > 0)
                {
                    discountPctLabelArgs = discountPctLabelArgs?.PadRight(maxLength - discountArgs[0], ' ');
                    discountLabelArgs = discountLabelArgs?.PadRight(maxLength - discountArgs[1], ' ');
                    var args = new List<string>
                    {
                        string.Empty,
                        discountPctLabelArgs + (discountPct.ToString("F2").ToString().TrimEnd('0').TrimEnd(',').TrimEnd('.')+"%").PadLeft(discountArgs[0], ' '),
                        discount.HasValue
                            ? discountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(discountArgs[1], ' ')
                            : remove_tag
                    };
                    args.RemoveAll(p => p.Equals(remove_tag));
                    output.InsertRange(pos, args);
                    pos += args.Count;
                }
            }
            else
            {
                pos = output.IndexOf(total_discount_tag);
                if (pos >= 0)
                {
                    output.RemoveRange(pos, 1);
                }
            }

            var hasInvoice = tran.WithInvoice.HasValue && tran.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, false);

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\st.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            tran.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCscContractReceiptImmediately(ArticleInfoBasket articleInfo, Guid agentId, ReceiptTemplateType templateType, bool inclidingTime, decimal? contractPrice = null, decimal? contractDiscount = null)
        {
            if (articleInfo == null) return null;

            var agentShift = _context.AgentShifts.Include("DeviceShifts").FirstOrDefault(s => s.Id.Equals(agentId));
            var deviceShift = agentShift?.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (deviceShift == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractReloadInstant.txt").ToList();
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string price_remove_tag = "#price_remove_tag#";
            const string discount_remove_tag = "#discount_remove_tag#";
            const string company_name_tag = "#company_name_tag#";
            const string payment_methods_tag = "#payment_methods#";
            const string date_time_now_tag = "#DateTimeNow#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                //var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string value = string.Empty;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = deviceShift.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(articleInfo.CardSerialNumber.ToString()) ?? remove_tag; break;
                    case 6:
                        value = articleInfo.HolderId.HasValue
                            ? articleInfo.HolderId.Value.ToString()
                            : remove_tag;
                        break;
                    case 7:
                        value = articleInfo.HolderBirthday.HasValue
                            ? articleInfo.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;
                    case 8: value = articleInfo.TariffId.ToString(); break;
                    case 9:
                        if (string.IsNullOrWhiteSpace(articleInfo.TariffDescription))
                            value = remove_tag;
                        else
                            value = articleInfo.TariffDescription.PadRight(size, ' ');
                        break;
                    case 10: value = articleInfo.VtContractId.ToString() ?? remove_tag; break;
                    case 11:
                        value = articleInfo.PassengerClass.HasValue && articleInfo.PassengerClass.Value > 0
                            ? articleInfo.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = articleInfo.OriginDescription ?? (articleInfo.Origin.HasValue
                            ? articleInfo.Origin.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = articleInfo.DestinationDescription ?? (articleInfo.Destination.HasValue
                            ? articleInfo.Destination.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = articleInfo.Via1Description ?? (articleInfo.ViaPoint1.HasValue
                            ? articleInfo.ViaPoint1.Value.ToString()
                            : remove_tag); break;
                    case 15:
                        value = articleInfo.Via2Description ?? (articleInfo.ViaPoint2.HasValue
                            ? articleInfo.ViaPoint2.Value.ToString()
                            : remove_tag); break;
                    case 16:
                        value = articleInfo.DtSvd.HasValue
                            ? articleInfo.DtSvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        value = articleInfo.DtEvd.HasValue
                            ? articleInfo.DtEvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 18:
                        value = articleInfo.DtLvd.HasValue
                            ? articleInfo.DtLvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 19:
                        if (string.IsNullOrWhiteSpace(articleInfo.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = articleInfo.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 20:
                        value = articleInfo.NbJourney.HasValue && articleInfo.NbJourney < 1023
                            ? articleInfo.NbJourney.Value.ToString()
                            : remove_tag; break;
                    case 21:
                        if (contractPrice.HasValue)
                        {
                            value = (contractPrice.Value / 100.0M).ToString("F2");
                        }
                        else
                        {
                            output.Add(price_remove_tag);
                            value = remove_tag;
                        }
                        break;
                    case 22:
                        if (contractPrice.HasValue && contractDiscount.HasValue)
                        {
                            value = (contractDiscount.Value / 100.0M).ToString("F2");
                        }
                        else
                        {
                            output.Add(discount_remove_tag);
                            value = remove_tag;
                        }
                        break;
                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }
            pos = output.IndexOf(discount_remove_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
            }
            pos = output.IndexOf(price_remove_tag);
            if (pos >= 0)
            {
                var minus = output[pos - 1].StartsWith("==") ? 0 : 1;
                output.RemoveRange(pos - minus, 2 + minus);
            }
            if (!inclidingTime)
            {
                var first = output.FirstOrDefault(p => p.StartsWith(date_time_now_tag));
                if (first != null)
                {
                    pos = output.IndexOf(first);
                    output.RemoveRange(pos, 2);
                }
            }

            NormalizeOutput(maxLength, ref output, articleInfo.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cr.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            return res;
        }

        public async Task<byte[]?> CreateCscContractReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("CscContractArticleInfos")
                .Include("PaymentDetails")
                .FirstOrDefault(p => p.Id == articleId);
            var info = article?.CscContractArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractReload.txt").ToList();
            var output = new List<string>();
            var code = 0;

            if (article.FromWhiteList.HasValue && article.FromWhiteList.Value == 1)
                fileData.RemoveAt(2);
            else
                fileData.RemoveAt(3);

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string payment_methods_tag = "#payment_methods#";
            const string total_discount_pct_tag = "#discount_pct#";
            const string total_discount_tag = "#discount#";
            const string remove_tag = "#REM#";
            string? discountPctLabelArgs = null;
            string? discountLabelArgs = null;
            var discountArgs = new[] { 0, 0 };
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                //var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(info.CardSerialNumber) ?? remove_tag; break;
                    case 6:
                        value = info.HolderId.HasValue
                            ? info.HolderId.Value.ToString()
                            : remove_tag;
                        break;
                    case 7:
                        value = info.HolderBirthday.HasValue
                            ? info.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;
                    case 8: value = info.TariffId.ToString(); break;
                    case 9:
                        if (string.IsNullOrWhiteSpace(info.TariffDescription))
                            value = remove_tag;
                        else
                            value = info.TariffDescription.PadRight(size, ' ');
                        break;
                    case 10: value = info.VtContractId.ToString() ?? remove_tag; break;
                    case 11:
                        value = info.PassengerClass.HasValue && info.PassengerClass.Value > 0
                            ? info.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = info.OriginDescription ?? (info.Origin.HasValue
                            ? info.Origin.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = info.DestinationDescription ?? (info.Destination.HasValue
                            ? info.Destination.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = info.Via1Description ?? (info.ViaPoint1.HasValue
                            ? info.ViaPoint1.Value.ToString()
                            : remove_tag); break;
                    case 15:
                        value = info.Via2Description ?? (info.ViaPoint2.HasValue
                            ? info.ViaPoint2.Value.ToString()
                            : remove_tag); break;
                    case 16:
                        value = info.DtSvd.HasValue
                            ? info.DtSvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        value = info.DtEvd.HasValue
                            ? info.DtEvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 18:
                        value = info.DtLvd.HasValue
                            ? info.DtLvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 19:
                        if (string.IsNullOrWhiteSpace(info.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = info.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 20:
                        value = info.NbJourney.HasValue && info.NbJourney < 1023
                            ? info.NbJourney.Value.ToString()
                            : remove_tag; break;
                    case 21:
                        value = article.FromWhiteList.HasValue && article.FromWhiteList.Value == 1
                            ? remove_tag
                            : (article.UniquePrice / 100.0M).ToString("F2"); break;
                    case 22:
                        value = article.DiscountApplied.HasValue && article.DiscountApplied.Value > 0
                            ? (article.DiscountApplied.Value / 100.0M).ToString("F2")
                            : remove_tag;
                        break;

                    case 23:
                        discountArgs[0] = size;
                        discountPctLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                        output.Add(total_discount_pct_tag);
                        break;
                    case 24:
                        discountArgs[1] = size;
                        discountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                        output.Add(total_discount_tag);
                        break;

                    case 25:
                        {
                            var pms = GetArticlePaymentMethods(article.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (pms[j].Amount / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;
                    case 26:
                        value = article.Id.ToString().Replace("-", string.Empty);
                        if (size > 0)
                        {
                            var padLeft = (size - value.Length) / 2;
                            var padRight = size - value.Length - padLeft;
                            if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                            if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                        }
                        break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }

            pos = output.IndexOf(total_discount_pct_tag);
            if (pos >= 0)
            {
                var n = 1;
                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0) ++n;
                output.RemoveRange(pos, n);
                var transactionId = _context.Articles.First(p => p.Id.Equals(articleId)).SaleTransactionId;
                var first = _context.PaymentDetails.Where(p => p.SaleTransactionId.Equals(transactionId))?.FirstOrDefault(p => p.PtDiscount.HasValue);
                var discountPct = first != null && first.PtDiscount.HasValue ? first.PtDiscount.Value / 100M : 0M;
                var discount = first?.Amount;
                if (discountPct > 0)
                {
                    discountPctLabelArgs = discountPctLabelArgs?.PadRight(maxLength - discountArgs[0], ' ');
                    discountLabelArgs = discountLabelArgs?.PadRight(maxLength - discountArgs[1], ' ');
                    var args = new List<string>
                    {
                        string.Empty,
                        discountPctLabelArgs + (discountPct.ToString("F2").ToString().TrimEnd('0').TrimEnd(',').TrimEnd('.')+"%").PadLeft(discountArgs[0], ' '),
                        discount.HasValue
                            ? discountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(discountArgs[1], ' ')
                            : remove_tag
                    };
                    args.RemoveAll(p => p.Equals(remove_tag));
                    output.InsertRange(pos, args);
                    pos += args.Count;
                }
            }

            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output, info.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cr.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            info.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCscContractUndoReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("CscContractRefundArticleInfo")
                .Include("CscContractArticleInfos")
                .Include("SaleTransaction")
                .Include("SaleTransaction.PaymentDetails")
                .Include("PaymentDetails")
                .Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.Id == articleId);
            var targetInfo = article?.CscContractRefundArticleInfo;
            var info = article?.CscContractArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractUndo.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string payment_methods_tag = "#payment_methods#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(info.CardSerialNumber) ?? remove_tag; break;
                    case 6:
                        value = info.HolderId.HasValue
                            ? info.HolderId.Value.ToString()
                            : remove_tag;
                        break;
                    case 7:
                        value = info.HolderBirthday.HasValue
                            ? info.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;
                    case 8: value = info.TariffId.ToString(); break;
                    case 9:
                        if (string.IsNullOrWhiteSpace(info.TariffDescription))
                            value = remove_tag;
                        else
                            value = info.TariffDescription.PadRight(size, ' ');
                        break;
                    case 10: value = info.VtContractId.ToString() ?? remove_tag; break;
                    case 11:
                        value = info.PassengerClass.HasValue && info.PassengerClass.Value > 0
                            ? info.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = info.OriginDescription ?? (info.Origin.HasValue
                            ? info.Origin.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = info.DestinationDescription ?? (info.Destination.HasValue
                            ? info.Destination.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = info.Via1Description ?? (info.ViaPoint1.HasValue
                       ? info.ViaPoint1.Value.ToString()
                       : remove_tag); break;
                    case 15:
                        value = info.Via2Description ?? (info.ViaPoint2.HasValue
                            ? info.ViaPoint2.Value.ToString()
                            : remove_tag); break;
                    case 16:
                        value = info.DtSvd.HasValue
                            ? info.DtSvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        value = info.DtEvd.HasValue
                            ? info.DtEvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 18:
                        value = info.DtLvd.HasValue
                            ? info.DtLvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 19:
                        if (string.IsNullOrWhiteSpace(info.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = info.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 20:
                        value = info.NbJourney.HasValue && info.NbJourney < 1023
                            ? info.NbJourney.Value.ToString()
                            : remove_tag; break;
                    case 21:
                        {
                            var apd = article.PaymentDetails
                                ?.OrderByDescending(p => p.SaleTransaction?.TransactionTime ?? DateTime.MinValue)
                                ?.FirstOrDefault();
                            if (apd != null && apd.Amount == 0 && apd.Article != null)
                                apd.Amount = apd.Article.UniquePrice * apd.Article.QuantityIssued;
                            var amount = apd?.Amount ?? article.UniquePrice * article.QuantityIssued;
                            value = (Math.Abs(amount) / 100.0M).ToString("F2");
                        }
                        break;
                    case 22:
                        {
                            var pms = GetArticlePaymentMethods(article.Id, true)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (Math.Abs(pms[j].Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;
                    //{
                    //    var pm = article?.PaymentDetails?.FirstOrDefault()?.PaymentMethod ?? 0;
                    //    if (pm <= 0) value = remove_tag;
                    //    else
                    //    {
                    //        var pmDescr = context.PaymentMethods.FirstOrDefault(p => p.Code == pm)?.Name;
                    //        value = pmDescr?.ToUpper() ?? remove_tag;
                    //    }
                    //}
                    //break;
                    case 23:
                        value = article.Id.ToString().Replace("-", string.Empty);
                        if (size > 0)
                        {
                            var padLeft = (size - value.Length) / 2;
                            var padRight = size - value.Length - padLeft;
                            if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                            if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                        }
                        break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output, info.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cu.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            targetInfo.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCscContractUndoReceiptImmediately(CscContractArticleInfoWithPaymentMethods articleInfoWithPaymentMethods, Guid agentId, ReceiptTemplateType templateType, bool inclidingTime, decimal? contractPrice = null)
        {
            var articleInfo = articleInfoWithPaymentMethods?.ArticleInfo;
            var pms = articleInfoWithPaymentMethods?.PaymentMethods;
            if (articleInfo == null) return null;

            var agentShift = _context.AgentShifts.Include("DeviceShifts").FirstOrDefault(s => s.Id.Equals(agentId));
            var deviceShift = agentShift?.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (deviceShift == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractUndo.txt").ToList();
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string price_remove_tag = "#price_remove_tag#";
            const string company_name_tag = "#company_name_tag#";
            const string payment_methods_tag = "#payment_methods#";
            const string date_time_now_tag = "#DateTimeNow#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string value = string.Empty;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = deviceShift.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(articleInfo.CardSerialNumber.ToString()) ?? remove_tag; break;
                    case 6:
                        value = articleInfo.HolderId.HasValue
                            ? articleInfo.HolderId.Value.ToString()
                            : remove_tag;
                        break;
                    case 7:
                        value = articleInfo.HolderBirthday.HasValue
                            ? articleInfo.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;
                    case 8: value = articleInfo.TariffId.ToString(); break;
                    case 9:
                        if (string.IsNullOrWhiteSpace(articleInfo.TariffDescription))
                            value = remove_tag;
                        else
                            value = articleInfo.TariffDescription.PadRight(size, ' ');
                        break;
                    case 10: value = articleInfo.VtContractId.ToString() ?? remove_tag; break;
                    case 11:
                        value = articleInfo.PassengerClass.HasValue && articleInfo.PassengerClass.Value > 0
                            ? articleInfo.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = articleInfo.OriginDescription ?? (articleInfo.Origin.HasValue
                            ? articleInfo.Origin.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = articleInfo.DestinationDescription ?? (articleInfo.Destination.HasValue
                            ? articleInfo.Destination.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = articleInfo.Via1Description ?? (articleInfo.ViaPoint1.HasValue
                            ? articleInfo.ViaPoint1.Value.ToString()
                            : remove_tag); break;
                    case 15:
                        value = articleInfo.Via2Description ?? (articleInfo.ViaPoint2.HasValue
                            ? articleInfo.ViaPoint2.Value.ToString()
                            : remove_tag); break;
                    case 16:
                        value = articleInfo.DtSvd.HasValue
                            ? articleInfo.DtSvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        value = articleInfo.DtEvd.HasValue
                            ? articleInfo.DtEvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 18:
                        value = articleInfo.DtLvd.HasValue
                            ? articleInfo.DtLvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 19:
                        if (string.IsNullOrWhiteSpace(articleInfo.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = articleInfo.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 20:
                        value = articleInfo.NbJourney.HasValue && articleInfo.NbJourney < 1023
                            ? articleInfo.NbJourney.Value.ToString()
                            : remove_tag; break;
                    case 21:
                        if (contractPrice.HasValue)
                        {
                            value = (contractPrice.Value / 100.0M).ToString("F2");
                        }
                        else
                        {
                            output.Add(price_remove_tag);
                            value = remove_tag;
                        }
                        break;
                    case 22:
                        {
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (Math.Abs(pms[j].Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;
                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }
            pos = output.IndexOf(price_remove_tag);
            if (pos >= 0)
            {
                var minus = output[pos - 1].StartsWith("==") ? 0 : 1;
                output.RemoveRange(pos - minus, 2 + minus);
            }
            if (!inclidingTime)
            {
                var first = output.FirstOrDefault(p => p.StartsWith(date_time_now_tag));
                if (first != null)
                {
                    pos = output.IndexOf(first);
                    output.RemoveRange(pos, 2);
                }
            }

            var hasInvoice = (articleInfoWithPaymentMethods?.WithInvoice.HasValue ?? false) && articleInfoWithPaymentMethods.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output, articleInfo.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cu.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            return res;
        }

        public async Task<byte[]?> CreateCscIssuingReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("ContactlessCardArticleInfos")
                .Include("PaymentDetails")
                .Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.Id == articleId);
            var info = article?.ContactlessCardArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\CardIssuing.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string profiles_tag = "#profiles#";
            const string payment_methods_tag = "#payment_methods#";
            const string total_discount_pct_tag = "#discount_pct#";
            const string total_discount_tag = "#discount#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            var profilesArgs = new[] { 0, 0, 0 };
            string? discountPctLabelArgs = null;
            string? discountLabelArgs = null;
            var discountArgs = new[] { 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(info.CardSerialNumberPh) ?? remove_tag; break;
                    case 6: value = info.CardSerialNumberLo ?? remove_tag; break;
                    case 7: value = info.EndValidityDate.ToString("dd/MM/yyyy"); break;
                    case 8: value = info.HolderId.ToString(); break;
                    case 9:
                        value = info.HolderBirthday.HasValue
                            ? info.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;

                    case 10:
                        profilesArgs[0] = size;
                        --i;
                        continue;
                    case 11:
                        profilesArgs[1] = size;
                        continue;
                    case 12:
                        profilesArgs[2] = size;
                        output.Add(profiles_tag);
                        output.Add(fileData[i]);
                        continue;

                    case 13:
                        {
                            var apd = article.PaymentDetails
                                .OrderByDescending(p => p.SaleTransaction?.TransactionTime ?? DateTime.MinValue)
                                .FirstOrDefault();
                            var amount = apd?.Amount ?? article.UniquePrice * article.QuantityIssued;
                            value = (amount / 100.0M).ToString("F2");
                        }
                        break;

                    case 14:
                        {
                            discountArgs[0] = size;
                            discountPctLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_pct_tag);
                        }
                        break;
                    case 15:
                        {
                            discountArgs[1] = size;
                            discountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_tag);
                        }
                        break;

                    case 16:
                        {
                            var pms = GetArticlePaymentMethods(article.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (Math.Abs(pms[j].Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(profiles_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos); // remove: '#08:3#                   #09:28#'
                var end = output[pos].IndexOf("#"); // get the position of '#10:15#'
                var descrLine = output[pos].Substring(0, end).Trim(); // cut out the '#10:15#' and leave only the 'Data di Scadenza'
                descrLine = descrLine.PadRight(maxLength, ' ');
                output.RemoveAt(pos); // remove 'Data di Scadenza         #10:15#'

                var defaultDate = new DateTime(1997, 1, 1);
                var profiles = new List<Tuple<int, string, DateTime>>();
                profiles.Add(new Tuple<int, string, DateTime>(info.ProfileId, info.ProfileDescription, info.ProfileEndValidityDate));
                if (info.ProfileAux1Id.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.ProfileAux1Id.Value, info.ProfileAux1Description,
                        info.ProfileAux1EndValidityDate.HasValue
                            ? info.ProfileAux1EndValidityDate.Value
                            : defaultDate));
                if (info.ProfileAux2Id.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.ProfileAux2Id.Value, info.ProfileAux2Description,
                        info.ProfileAux1EndValidityDate.HasValue
                            ? info.ProfileAux1EndValidityDate.Value
                            : defaultDate));

                var profileCounter = 0;
                foreach (var profile in profiles)
                {
                    if (profileCounter > 0)
                        output.Insert(pos++, GetAsterisks(maxLength));

                    var args = new[]
                    {
                        profile.Item1.ToString().PadRight(profilesArgs[0], ' '),
                        profile.Item2.PadLeft(profilesArgs[1], ' ')
                    };
                    var line = string.Join(' ', args);
                    output.Insert(pos++, line);

                    if (profile.Item3 > defaultDate)
                    {
                        var date = profile.Item3.ToString("dd/MM/yyyy").PadLeft(profilesArgs[2], ' ');
                        var descr = descrLine.Substring(0, descrLine.Length - profilesArgs[2]) + date;
                        output.Insert(pos++, descr);
                    }
                    ++profileCounter;
                }
            }
            pos = output.IndexOf(total_discount_pct_tag);
            if (pos >= 0)
            {
                var n = 1;
                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0) ++n;
                output.RemoveRange(pos, n);
                var first = _context.PaymentDetails.Where(p => p.SaleTransactionId.Equals(st.Id))?.FirstOrDefault(p => p.PtDiscount.HasValue);
                var discountPct = first != null && first.PtDiscount.HasValue ? first.PtDiscount.Value / 100M : 0M;
                var discount = first?.Amount;
                if (discountPct > 0)
                {
                    discountPctLabelArgs = discountPctLabelArgs?.PadRight(maxLength - discountArgs[0], ' ');
                    discountLabelArgs = discountLabelArgs?.PadRight(maxLength - discountArgs[1], ' ');
                    var args = new List<string>
                    {
                        string.Empty,
                        discountPctLabelArgs + (discountPct.ToString("F2").ToString().TrimEnd('0').TrimEnd(',').TrimEnd('.')+"%").PadLeft(discountArgs[0], ' '),
                        discount.HasValue
                            ? discountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(discountArgs[1], ' ')
                            : remove_tag
                    };
                    args.RemoveAll(p => p.Equals(remove_tag));
                    output.InsertRange(pos, args);
                    pos += args.Count;
                }
            }
            else
            {
                pos = output.IndexOf(total_discount_tag);
                if (pos >= 0)
                {
                    output.RemoveRange(pos, 1);
                }
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\is.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            info.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCscReissuingReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("ContactlessCardArticleInfos")
                .Include("ContactlessCardArticleInfos.ReissuingReasonCodeNavigation")
                .Include("PaymentDetails")
                .Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.Id == articleId);
            var info = article?.ContactlessCardArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\CardReissuing.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string profiles_tag = "#profiles#";
            const string payment_methods_tag = "#payment_methods#";
            const string total_discount_pct_tag = "#discount_pct#";
            const string total_discount_tag = "#discount#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            var profilesArgs = new[] { 0, 0, 0 };
            string? discountPctLabelArgs = null;
            string? discountLabelArgs = null;
            var discountArgs = new[] { 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            if (info.ReissuingReasonCode.HasValue)
                            {
                                value = info.ReissuingReasonCodeNavigation?.Name?.ToUpper() ?? remove_tag;
                            }
                            else
                                value = remove_tag;
                        }
                        break;
                    case 2:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 3: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 4: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 5: value = agentShift.ShiftNumber.ToString(); break;
                    case 6: value = HexToDec(info.CardSerialNumberPh) ?? remove_tag; break;
                    case 7: value = info.CardSerialNumberLo ?? remove_tag; break;
                    case 8: value = info.EndValidityDate.ToString("dd/MM/yyyy"); break;
                    case 9: value = info.HolderId.ToString(); break;
                    case 10:
                        value = info.HolderBirthday.HasValue
                            ? info.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;

                    case 11:
                        profilesArgs[0] = size;
                        --i;
                        continue;
                    case 12:
                        profilesArgs[1] = size;
                        continue;
                    case 13:
                        profilesArgs[2] = size;
                        output.Add(profiles_tag);
                        output.Add(fileData[i]);
                        continue;

                    case 14:
                        {
                            var apd = article.PaymentDetails
                                .OrderByDescending(p => p.SaleTransaction?.TransactionTime ?? DateTime.MinValue)
                                .FirstOrDefault();
                            var amount = apd?.Amount ?? article.UniquePrice * article.QuantityIssued;
                            value = (amount / 100.0M).ToString("F2");
                        }
                        break;

                    case 15:
                        {
                            discountArgs[0] = size;
                            discountPctLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_pct_tag);
                        }
                        break;
                    case 16:
                        {
                            discountArgs[1] = size;
                            discountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_tag);
                        }
                        break;

                    case 17:
                        {
                            var pms = GetArticlePaymentMethods(article.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (Math.Abs(pms[j].Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(profiles_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos); // remove: '#08:3#                   #09:28#'
                var end = output[pos].IndexOf("#"); // get the position of '#10:15#'
                var descrLine = output[pos].Substring(0, end).Trim(); // cut out the '#10:15#' and leave only the 'Data di Scadenza'
                descrLine = descrLine.PadRight(maxLength, ' ');
                output.RemoveAt(pos); // remove 'Data di Scadenza         #10:15#'

                var defaultDate = new DateTime(1997, 1, 1);
                var profiles = new List<Tuple<int, string, DateTime>>();
                profiles.Add(new Tuple<int, string, DateTime>(info.ProfileId, info.ProfileDescription, info.ProfileEndValidityDate));
                if (info.ProfileAux1Id.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.ProfileAux1Id.Value, info.ProfileAux1Description,
                        info.ProfileAux1EndValidityDate.HasValue
                            ? info.ProfileAux1EndValidityDate.Value
                            : defaultDate));
                if (info.ProfileAux2Id.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.ProfileAux2Id.Value, info.ProfileAux2Description,
                        info.ProfileAux1EndValidityDate.HasValue
                            ? info.ProfileAux1EndValidityDate.Value
                            : defaultDate));

                var profileCounter = 0;
                foreach (var profile in profiles)
                {
                    if (profileCounter > 0)
                        output.Insert(pos++, GetAsterisks(maxLength));

                    var args = new[]
                    {
                        profile.Item1.ToString().PadRight(profilesArgs[0], ' '),
                        profile.Item2.PadLeft(profilesArgs[1], ' ')
                    };
                    var line = string.Join(' ', args);
                    output.Insert(pos++, line);

                    if (profile.Item3 > defaultDate)
                    {
                        var date = profile.Item3.ToString("dd/MM/yyyy").PadLeft(profilesArgs[2], ' ');
                        var descr = descrLine.Substring(0, descrLine.Length - profilesArgs[2]) + date;
                        output.Insert(pos++, descr);
                    }
                    ++profileCounter;
                }
            }
            pos = output.IndexOf(total_discount_pct_tag);
            if (pos >= 0)
            {
                var n = 1;
                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0) ++n;
                output.RemoveRange(pos, n);
                var first = _context.PaymentDetails.Where(p => p.SaleTransactionId.Equals(st.Id))?.FirstOrDefault(p => p.PtDiscount.HasValue);
                var discountPct = first != null && first.PtDiscount.HasValue ? first.PtDiscount.Value / 100M : 0M;
                var discount = first?.Amount;
                if (discountPct > 0)
                {
                    discountPctLabelArgs = discountPctLabelArgs?.PadRight(maxLength - discountArgs[0], ' ');
                    discountLabelArgs = discountLabelArgs?.PadRight(maxLength - discountArgs[1], ' ');
                    var args = new List<string>
                    {
                        string.Empty,
                        discountPctLabelArgs + (discountPct.ToString("F2").ToString().TrimEnd('0').TrimEnd(',').TrimEnd('.')+"%").PadLeft(discountArgs[0], ' '),
                        discount.HasValue
                            ? discountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(discountArgs[1], ' ')
                            : remove_tag
                    };
                    args.RemoveAll(p => p.Equals(remove_tag));
                    output.InsertRange(pos, args);
                    pos += args.Count;
                }
            }
            else
            {
                pos = output.IndexOf(total_discount_tag);
                if (pos >= 0)
                {
                    output.RemoveRange(pos, 1);
                }
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\re.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            info.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateProfileRenewalReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("ContactlessCardArticleInfos")
                .Include("ProfileRenewalArticleInfos")
                .Include("ContactlessCardArticleInfos.ReissuingReasonCodeNavigation")
                .Include("PaymentDetails")
                .Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.Id == articleId);
            var info = article?.ProfileRenewalArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ProfileRenewal.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string profiles_tag = "#profiles#";
            const string payment_methods_tag = "#payment_methods#";
            const string total_discount_pct_tag = "#discount_pct#";
            const string total_discount_tag = "#discount#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            var profilesArgs = new[] { 0, 0, 0 };
            string? discountPctLabelArgs = null;
            string? discountLabelArgs = null;
            var discountArgs = new[] { 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;

                    case 5: value = info.CardSerialNumber ?? remove_tag; break;
                    case 6: value = info.HolderId.ToString(); break;
                    case 7:
                        value = info.HolderBirthday.HasValue
                            ? info.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;

                    case 8:
                        profilesArgs[0] = size;
                        --i;
                        continue;
                    case 9:
                        profilesArgs[1] = size;
                        continue;
                    case 10:
                        profilesArgs[2] = size;
                        output.Add(profiles_tag);
                        output.Add(fileData[i]);
                        continue;

                    case 11:
                        {
                            var apd = article.PaymentDetails
                                ?.OrderByDescending(p => p.SaleTransaction?.TransactionTime ?? DateTime.MinValue)
                                ?.FirstOrDefault();
                            var amount = apd?.Amount ?? article.UniquePrice * article.QuantityIssued;
                            value = (amount / 100.0M).ToString("F2");
                        }
                        break;

                    case 12:
                        {
                            discountArgs[0] = size;
                            discountPctLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_pct_tag);
                        }
                        break;
                    case 13:
                        {
                            discountArgs[1] = size;
                            discountLabelArgs = beginning + (string.IsNullOrWhiteSpace(beginning) ? string.Empty : " ");
                            output.Add(total_discount_tag);
                        }
                        break;

                    case 14:
                        {
                            var pms = GetArticlePaymentMethods(article.Id)?.Where(p => p.Amount != 0M)?.ToArray();
                            if ((pms?.Length ?? 0) > 0)
                            {
                                paymentMethods = new List<string>();
                                for (var j = 0; j < pms.Length; ++j)
                                {
                                    if (pms[j].Description == null) continue;
                                    var ins = (Math.Abs(pms[j].Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                    ins = pms[j].Description.ToUpper() + ins.Remove(0, pms[j].Description.Length);
                                    paymentMethods.Add(ins);
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(profiles_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos); // remove: '#08:3#                   #09:28#'
                var end = output[pos].IndexOf("#"); // get the position of '#10:15#'
                var descrLine = output[pos].Substring(0, end).Trim(); // cut out the '#10:15#' and leave only the 'Data di Scadenza'
                descrLine = descrLine.PadRight(maxLength, ' ');
                output.RemoveAt(pos); // remove 'Data di Scadenza         #10:15#'

                var defaultDate = new DateTime(1997, 1, 1);
                var profiles = new List<Tuple<int, string, DateTime>>();
                profiles.Add(new Tuple<int, string, DateTime>(info.HolderProfile, info.HolderProfileDescription, info.HolderProfileLimitDate));
                if (info.HolderProfile2.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.HolderProfile2.Value, info.HolderProfileDescription2,
                        info.HolderProfile2LimitDate.HasValue
                            ? info.HolderProfile2LimitDate.Value
                            : defaultDate));
                if (info.HolderProfile3.HasValue)
                    profiles.Add(new Tuple<int, string, DateTime>(info.HolderProfile3.Value, info.HolderProfileDescription3,
                        info.HolderProfile3LimitDate.HasValue
                            ? info.HolderProfile3LimitDate.Value
                            : defaultDate));

                var profileCounter = 0;
                foreach (var profile in profiles)
                {
                    if (profileCounter > 0)
                        output.Insert(pos++, GetAsterisks(maxLength));

                    var args = new[]
                    {
                        profile.Item1.ToString().PadRight(profilesArgs[0], ' '),
                        profile.Item2.PadLeft(profilesArgs[1], ' ')
                    };
                    var line = string.Join(' ', args);
                    output.Insert(pos++, line);

                    if (profile.Item3 > defaultDate)
                    {
                        var date = profile.Item3.ToString("dd/MM/yyyy").PadLeft(profilesArgs[2], ' ');
                        var descr = descrLine.Substring(0, descrLine.Length - profilesArgs[2]) + date;
                        output.Insert(pos++, descr);
                    }
                    ++profileCounter;
                }
            }
            pos = output.IndexOf(total_discount_pct_tag);
            if (pos >= 0)
            {
                var n = 1;
                var pos2 = output.IndexOf(total_discount_tag);
                if (pos2 >= 0) ++n;
                output.RemoveRange(pos, n);
                var first = _context.PaymentDetails.Where(p => p.SaleTransactionId.Equals(st.Id))?.FirstOrDefault(p => p.PtDiscount.HasValue);
                var discountPct = first != null && first.PtDiscount.HasValue ? first.PtDiscount.Value / 100M : 0M;
                var discount = first?.Amount;
                if (discountPct > 0)
                {
                    discountPctLabelArgs = discountPctLabelArgs?.PadRight(maxLength - discountArgs[0], ' ');
                    discountLabelArgs = discountLabelArgs?.PadRight(maxLength - discountArgs[1], ' ');
                    var args = new List<string>
                    {
                        string.Empty,
                        discountPctLabelArgs + (discountPct.ToString("F2").ToString().TrimEnd('0').TrimEnd(',').TrimEnd('.')+"%").PadLeft(discountArgs[0], ' '),
                        discount.HasValue
                            ? discountLabelArgs + (discount.Value / 100.0M).ToString("F2").ToString().PadLeft(discountArgs[1], ' ')
                            : remove_tag
                    };
                    args.RemoveAll(p => p.Equals(remove_tag));
                    output.InsertRange(pos, args);
                    pos += args.Count;
                }
            }
            else
            {
                pos = output.IndexOf(total_discount_tag);
                if (pos >= 0)
                {
                    output.RemoveRange(pos, 1);
                }
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\pr.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            info.Receipt = res;

            return res;
        }

        public async Task<byte[]> CreateContactlessCardExpirationExtensionReceipt(Guid articleId, ReceiptTemplateType templateType)
        {
            var article = _context.Articles
                .Include("ContactlessCardExpirationExtensionArticleInfos")
                //.Include("PaymentDetails")
                //.Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.Id == articleId);
            var info = article?.ContactlessCardExpirationExtensionArticleInfos?.FirstOrDefault();
            var st = article == null
                ? null
                : _context.SaleTransactions
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id == article.SaleTransactionId);
            var ds = st?.DeviceShift;
            var agentShift = st?.DeviceShift?.AgentShift;

            if (article == null || info == null || st == null || ds == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\CardExpirationExtension.txt");
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string profiles_tag = "#profiles#";
            const string payment_methods_tag = "#payment_methods#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            var profilesArgs = new[] { 0, 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Length; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(info.CardSerialNumber) ?? remove_tag; break;
                    case 6: value = info.CardLogicalSerialNumber ?? remove_tag; break;
                    case 7: value = info.NewCardExpirationNullable?.ToString("dd/MM/yyyy") ?? remove_tag; break;
                    case 8: value = info.HolderId?.ToString() ?? remove_tag; break;
                    case 9: value = info.HolderBirthday?.ToString("dd/MM/yyyy") ?? remove_tag; break;

                    case 10:
                        profilesArgs[0] = size;
                        --i;
                        continue;
                    case 11:
                        profilesArgs[1] = size;
                        continue;
                    case 12:
                        profilesArgs[2] = size;
                        output.Add(profiles_tag);
                        output.Add(fileData[i]);
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(profiles_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos); // remove: '#08:3#                   #09:28#'
                var end = output[pos].IndexOf("#"); // get the position of '#10:15#'
                var descrLine = output[pos].Substring(0, end).Trim(); // cut out the '#10:15#' and leave only the 'Data di Scadenza'
                descrLine = descrLine.PadRight(maxLength, ' ');
                output.RemoveAt(pos); // remove 'Data di Scadenza         #10:15#'

                var defaultDate = new DateTime(1997, 1, 1);
                var profiles = new List<Tuple<int, string, DateTime?>>();
                if (info.NewProfileId1.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.NewProfileId1.Value, info.NewProfile1Description, info.NewProfile1EndValidityDate ?? defaultDate));
                }
                else if (info.OldProfileId1.HasValue && info.OldProfile1EndValidityDate.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.OldProfileId1.Value, info.OldProfile1Description, info.OldProfile1EndValidityDate ?? defaultDate));
                }
                if (info.NewProfileId2.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.NewProfileId2.Value, info.NewProfile2Description, info.NewProfile2EndValidityDate ?? defaultDate));
                }
                else if (info.OldProfileId2.HasValue && info.OldProfile2EndValidityDate.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.OldProfileId2.Value, info.OldProfile2Description, info.OldProfile2EndValidityDate ?? defaultDate));
                }
                if (info.NewProfileId3.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.NewProfileId3.Value, info.NewProfile3Description, info.NewProfile3EndValidityDate ?? defaultDate));
                }
                else if (info.OldProfileId3.HasValue && info.OldProfile3EndValidityDate.HasValue)
                {
                    profiles.Add(new Tuple<int, string, DateTime?>(info.OldProfileId3.Value, info.OldProfile3Description, info.OldProfile3EndValidityDate ?? defaultDate));
                }

                var profileCounter = 0;
                if ((profiles?.Count ?? 0) > 0)
                {
                    foreach (var profile in profiles!)
                    {
                        if (profileCounter > 0)
                            output.Insert(pos++, GetAsterisks(maxLength));

                        var args = new[]
                        {
                            profile.Item1.ToString().PadRight(profilesArgs[0], ' '),
                            profile.Item2.PadLeft(profilesArgs[1], ' ')
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);

                        if (profile.Item3.HasValue && profile.Item3.Value > defaultDate)
                        {
                            var date = profile.Item3.Value.ToString("dd/MM/yyyy").PadLeft(profilesArgs[2], ' ');
                            var descr = descrLine.Substring(0, descrLine.Length - profilesArgs[2]) + date;
                            output.Insert(pos++, descr);
                        }
                        ++profileCounter;
                    }
                }
                else
                {
                    output.RemoveAt(pos - 1);
                    output.RemoveAt(pos - 1);
                }
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if (false && (paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\ex.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            info.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCanceledCscContractReceipt(string contractId, ReceiptTemplateType templateType)
        {
            var master = _context.CanceledCscContracts
                .Include("DeviceShift")
                .Include("DeviceShift.AgentShift")
                .FirstOrDefault(p => p.Id.Equals(contractId));

            if (master == null) return null;

            var slaves = _context.CanceledCscContracts
                .Where(p => !string.IsNullOrWhiteSpace(p.MasterContractId) && p.MasterContractId.Equals(contractId));

            var ds = master.DeviceShift;
            var agentShift = master.DeviceShift?.AgentShift;

            if (ds == null || agentShift == null) return null;

            bool isOd(byte? saleType) => (SbmeModels.VtsModels.Responses.Enums.SaleType_t)(saleType ?? 0) == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD;

            var od = isOd(master.SaleType)
                ? master
                : slaves.AsEnumerable().FirstOrDefault(p => isOd(p.SaleType)) ?? master;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractRemoval.txt").ToList();
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string history_tag = "#history#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            var historyArgs = new[] { 0, 0 };
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                //var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = ds.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(master.CardSerialNumber) ?? remove_tag; break;
                    case 6: value = master.TariffId.ToString(); break;
                    case 7:
                        if (string.IsNullOrWhiteSpace(master.TariffDescription))
                            value = remove_tag;
                        else
                            value = master.TariffDescription.PadRight(size, ' ');
                        break;
                    case 8: value = master.ContractSerialNumber ?? master.Id ?? remove_tag; break;
                    case 9:
                        value = od.PassengerClass.HasValue && od.PassengerClass.Value > 0
                            ? od.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 10:
                        value = od.OriginDescription ?? (od.OriginId.HasValue
                            ? od.OriginId.Value.ToString()
                            : remove_tag); break;
                    case 11:
                        value = od.DestinationDescription ?? (od.DestinationId.HasValue
                            ? od.DestinationId.Value.ToString()
                            : remove_tag); break;
                    case 12:
                        value = od.Via1Description ?? (od.Via1.HasValue
                            ? od.Via1.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = od.Via2Description ?? (od.Via2.HasValue
                            ? od.Via2.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = master.Svd.HasValue
                            ? master.Svd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 15:
                        value = master.Evd.HasValue
                            ? master.Evd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 16:
                        value = master.Lvd.HasValue
                            ? master.Lvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        var rides = od.TotalRides ?? master.TotalRides;
                        value = rides.HasValue
                            ? rides.Value.ToString()
                            : remove_tag; break;

                    case 18:
                        historyArgs[0] = size;
                        --i;
                        break;
                    case 19:
                        historyArgs[1] = size;
                        output.Add(history_tag);
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != null && value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(history_tag);
            if (pos >= 0)
            {
                var history = !string.IsNullOrWhiteSpace(master.History)
                    ? JsonConvert.DeserializeObject<List<CanceledContractHistoryElement>>(master.History)
                    : null;
                
                if (history == null)
                {
                    output.RemoveRange(pos - 2, 4);
                }
                else
                {
                    output.RemoveAt(pos);
                    foreach (var element in history)
                    {
                        var dates = new StringBuilder();
                        if (element.Start.HasValue && element.End.HasValue && !Helper.GetMidnight(element.Start.Value).Equals(Helper.GetMidnight(element.End.Value)))
                        {
                            dates.Append(element.Start.Value.ToString("dd/MM/yyyy"));
                            dates.Append(" ");
                            dates.Append(element.End.Value.ToString("dd/MM/yyyy"));
                        }
                        else if (element.Start.HasValue && !element.Limit.HasValue && !element.End.HasValue
                            || (element.Start.HasValue && element.End.HasValue && Helper.GetMidnight(element.Start.Value).Equals(Helper.GetMidnight(element.End.Value))))
                        {
                            dates.Append("Data utilizzo: ");
                            dates.Append(element.Start.Value.ToString("dd/MM/yyyy"));
                        }
                        else if (!element.Start.HasValue && element.Limit.HasValue && !element.End.HasValue)
                        {
                            dates.Append("Da val. entro: ");
                            dates.Append(element.Limit.Value.ToString("dd/MM/yyyy"));
                        }
                        else if (!element.Start.HasValue && element.End.HasValue)
                        {
                            dates.Append("Scade il ");
                            dates.Append(element.End.Value.ToString("dd/MM/yyyy"));
                        }
                        var args = new[]
                        {
                            dates.ToString().PadRight(historyArgs[0], ' '),
                            ((element.Price ?? 0) / 100.0M).ToString("F2").PadLeft(historyArgs[1], ' '),
                        };
                        var line = string.Join(' ', args);
                        output.Insert(pos++, line);
                    }
                }
            }

            var hasInvoice = master.WithInvoice.HasValue && master.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cd.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            master.Receipt = res;

            return res;
        }

        public async Task<byte[]?> CreateCanceledCscContractRefundFailedReceipt(Guid articleId, Guid agentId, ReceiptTemplateType templateType)
        {
            var agentShift = _context.AgentShifts.Include("DeviceShifts").FirstOrDefault(s => s.Id.Equals(agentId));
            var deviceShift = agentShift?.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (deviceShift == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\ContractUndoRefundFailed.txt").ToList();
            var output = new List<string>();
            var code = 0;

            var article = _context.Articles
                .Include("CscContractArticleInfos")
                .Include("SaleTransaction")
                .Include("SaleTransaction.PaymentDetails")
                .Include("PaymentDetails")
                .Include("PaymentDetails.SaleTransaction")
                .FirstOrDefault(p => p.ArticleType == (int)Enums.ArticleType.Contract && p.Id.Equals(articleId));
            var articleInfo = article?.CscContractArticleInfos?.FirstOrDefault();

            if (articleInfo == null) return null;

            const string skp_tag = "~SKP~";
            const string company_name_tag = "#company_name_tag#";
            const string payment_methods_tag = "#payment_methods#";
            const string remove_tag = "#REM#";
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);
                //var codeArg = fileData[i].Substring(openingPos + 1, (lengthPos >= 0 ? lengthPos : closingPos) - openingPos - 1);
                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string value = string.Empty;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    //value = _receiptData.CompanyNameFormatted;
                    //if (size > 0)
                    //{
                    //    var padLeft = (size - value.Length) / 2;
                    //    var padRight = size - value.Length - padLeft;
                    //    if (padLeft > 0) value = value.PadLeft(padLeft + value.Length, ' ');
                    //    if (padRight > 0) value = value.PadRight(padRight + value.Length, ' ');
                    //}
                    //break;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = deviceShift.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(articleInfo.CardSerialNumber.ToString()) ?? remove_tag; break;
                    case 6:
                        value = articleInfo.HolderId.HasValue
                            ? articleInfo.HolderId.Value.ToString()
                            : remove_tag;
                        break;
                    case 7:
                        value = articleInfo.HolderBirthday.HasValue
                            ? articleInfo.HolderBirthday.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        break;
                    case 8: value = articleInfo.TariffId.ToString(); break;
                    case 9:
                        if (string.IsNullOrWhiteSpace(articleInfo.TariffDescription))
                            value = remove_tag;
                        else
                            value = articleInfo.TariffDescription.PadRight(size, ' ');
                        break;
                    case 10: value = articleInfo.VtContractId.ToString() ?? remove_tag; break;
                    case 11:
                        value = articleInfo.PassengerClass.HasValue && articleInfo.PassengerClass.Value > 0
                            ? articleInfo.PassengerClass.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = articleInfo.OriginDescription ?? (articleInfo.Origin.HasValue
                            ? articleInfo.Origin.Value.ToString()
                            : remove_tag); break;
                    case 13:
                        value = articleInfo.DestinationDescription ?? (articleInfo.Destination.HasValue
                            ? articleInfo.Destination.Value.ToString()
                            : remove_tag); break;
                    case 14:
                        value = articleInfo.Via1Description ?? (articleInfo.ViaPoint1.HasValue
                            ? articleInfo.ViaPoint1.Value.ToString()
                            : remove_tag); break;
                    case 15:
                        value = articleInfo.Via2Description ?? (articleInfo.ViaPoint2.HasValue
                            ? articleInfo.ViaPoint2.Value.ToString()
                            : remove_tag); break;
                    case 16:
                        value = articleInfo.DtSvd.HasValue
                            ? articleInfo.DtSvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 17:
                        value = articleInfo.DtEvd.HasValue
                            ? articleInfo.DtEvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 18:
                        value = articleInfo.DtLvd.HasValue
                            ? articleInfo.DtLvd.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 19:
                        if (string.IsNullOrWhiteSpace(articleInfo.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = articleInfo.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 20:
                        value = articleInfo.NbJourney.HasValue
                            ? articleInfo.NbJourney.Value.ToString()
                            : "***"; break;
                    case 21:
                        {
                            var apd = article.PaymentDetails
                                ?.OrderByDescending(p => p.SaleTransaction?.TransactionTime ?? DateTime.MinValue)
                                ?.FirstOrDefault();
                            if (apd != null && apd.Amount == 0 && apd.Article != null)
                                apd.Amount = apd.Article.UniquePrice * apd.Article.QuantityIssued;
                            var amount = apd?.Amount ?? article.UniquePrice * article.QuantityIssued;
                            value = (Math.Abs(amount) / 100.0M).ToString("F2");
                        }
                        break;
                    case 22:
                        {
                            var descr = _context.PaymentMethods.ToDictionary(key => key.Code, value => value.Name);
                            var pms = article.SaleTransaction.PaymentDetails.Select(p => p.PaymentMethod)
                                .Select(p => new PaymentMethodReport {
                                    MethodId = p,
                                    Description = descr.ContainsKey(p) ? descr[p] : "???",
                                    Amount = 0
                                });
                            if (pms?.Any() ?? false)
                            {
                                var trpd = article.SaleTransaction?.PaymentDetails;
                                if (trpd?.Any() ?? false)
                                {
                                    paymentMethods = new List<string>();
                                    foreach (var pm in pms)
                                    {
                                        pm.Amount = trpd.Where(p => p.PaymentMethod == pm.MethodId).Sum(p => p.Amount);
                                        var ins = (Math.Abs(pm.Amount) / 100.0M).ToString("F2").PadLeft(size, ' ');
                                        ins = pm.Description.ToUpper() + ins.Remove(0, pm.Description.Length);
                                        paymentMethods.Add(ins);
                                    }
                                }
                            }
                            output.Add(payment_methods_tag);
                        }
                        continue;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }
            pos = output.IndexOf(payment_methods_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                if ((paymentMethods?.Count ?? 0) > 0)
                    output.InsertRange(pos, paymentMethods);
                else
                    output.RemoveRange(pos - 1, 2);
            }

            var hasInvoice = article.WithInvoice.HasValue && article.WithInvoice == 1;
            ManageInvoice(hasInvoice, ref output, true);

            NormalizeOutput(maxLength, ref output, articleInfo.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\crf.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            return res;
        }

        public async Task<byte[]?> CreateCardInfoReceipt(CardInfo cardInfo, Guid agentId, ReceiptTemplateType templateType)
        {
            if (cardInfo == null) return null;

            var agentShift = _context.AgentShifts.Include("DeviceShifts").FirstOrDefault(s => s.Id.Equals(agentId));
            var deviceShift = agentShift?.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (deviceShift == null || agentShift == null) return null;

            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var fileData = File.ReadAllLines($@"{startupPath}\ReceiptTemplates\{(int)templateType}\CardInfo.txt").ToList();
            var output = new List<string>();
            var code = 0;

            const string skp_tag = "~SKP~";
            const string cliente_separation_tag = "~CLT~";
            const string profili_separation_tag = "~PRF~";
            const string profile2_separation_tag = "~PRF2~";
            const string profile3_separation_tag = "~PRF3~";
            const string contracs_tag = "#Contracts#";
            const string validations_tag = "#Validations#";
            const string company_name_tag = "#company_name_tag#";
            const string remove_tag = "#REM#";
            bool profile1specified = cardInfo.VTokenPayload.Environment.HolderProfileIdSpecified;
            bool skipProfile2 = false;
            bool skipProfile3 = false;
            var skp_company_name = false;
            List<string>? paymentMethods = null;

            for (var i = 0; i < fileData.Count; ++i)
            {
                if (fileData[i].Contains(profile2_separation_tag))
                {
                    if (!cardInfo.VTokenPayload.Environment.HolderProfile2IdSpecified)
                    {
                        skipProfile2 = true;
                        continue;
                    }
                    else
                    {
                        var sep = fileData[i].Replace(profile2_separation_tag, "");
                        output.Add(sep);
                        continue;
                    }
                }

                if (fileData[i].Contains(profile3_separation_tag))
                {
                    if (!cardInfo.VTokenPayload.Environment.HolderProfile3IdSpecified)
                    {
                        skipProfile3 = true;
                        continue;
                    }
                    else
                    {
                        var sep = fileData[i].Replace(profile3_separation_tag, "");
                        output.Add(sep);
                        continue;
                    }
                }

                // Add contracts
                if (fileData[i].Contains(contracs_tag))
                {
                    var lineCliente = output.FirstOrDefault(p => p.Contains(cliente_separation_tag, StringComparison.InvariantCultureIgnoreCase));
                    var posCliente = string.IsNullOrWhiteSpace(lineCliente) ? -1 : output.LastIndexOf(lineCliente);
                    if (!string.IsNullOrWhiteSpace(lineCliente) &&
                        posCliente >= 0)
                    {
                        output[posCliente] = lineCliente.Replace(cliente_separation_tag, "");
                        if (!cardInfo.VTokenPayload.Environment.HolderIdNullable.HasValue &&
                            !cardInfo.VTokenPayload.Environment.HolderBirthDateSpecified)
                        {
                            output.RemoveAt(posCliente);
                            output.RemoveAt(posCliente);
                            output.RemoveAt(posCliente);
                        }
                    }

                    var lineProfili = output.FirstOrDefault(p => p.Contains(profili_separation_tag, StringComparison.InvariantCultureIgnoreCase));
                    var posProfili = string.IsNullOrWhiteSpace(lineProfili) ? -1 : output.LastIndexOf(lineProfili);
                    if (posProfili >= 0)
                    {
                        output[posProfili] = lineProfili.Replace(profili_separation_tag, "");
                        if (profile1specified && (skipProfile2 || skipProfile3))
                        {
                            //
                        }
                        else if (!profile1specified && skipProfile2 && skipProfile3)
                        {
                            if (!(cardInfo.VTokenPayload?.ContractsList?.Any() ?? false))
                            {
                                //
                            }
                            output.RemoveAt(posProfili);
                            output.RemoveAt(posProfili);
                            output.RemoveAt(posProfili);
                        }
                    }

                    if (cardInfo.VTokenPayload?.ContractsList?.Any() ?? false)
                    {
                        var lContracts = cardInfo.VTokenPayload.ContractsList!.ToList();
                        var contractToAdd = new List<string>();
                        int index = 1;

                        foreach (var contract in lContracts)
                        {
                            contractToAdd = GetContractListFromCard(contract, templateType, index);
                            output.AddRange(contractToAdd);
                            index++;
                        }
                        continue;
                    }
                    else
                    {
                        output.RemoveAt(output.Count - 1);
                        output.RemoveAt(output.Count - 1);
                        ++i;
                        continue;
                    }
                }

                // Add validations
                if (fileData[i].Contains(validations_tag))
                {
                    if (cardInfo.VTokenPayload?.ContractsList?.Any() ?? false)
                    {
                        output.RemoveAt(output.Count - 3);
                    }

                    var contracts = cardInfo.VTokenPayload.ContractsList;
                    if (cardInfo.VTokenPayload?.ValidationsList?.Any(p => p.ValidationType1Specified) ?? false)
                    {
                        var lValidations = cardInfo.VTokenPayload.ValidationsList!.ToList();
                        var validationToAdd = new List<string>();
                        int index = 1;

                        foreach (var validation in lValidations)
                        {
                            validationToAdd = GetValidationsListFromCard(validation, contracts, templateType, index);
                            output.AddRange(validationToAdd);
                            index++;
                        }
                        continue;
                    }
                    else
                    {
                        output.RemoveAt(output.Count - 1);
                        output.RemoveAt(output.Count - 1);
                        continue;
                    }
                }

                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);

                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        output.Add(company_name_tag);
                        skp_company_name = fileData[i].Contains(skp_tag);
                        continue;
                    case 1:
                        {
                            value = NormalizeDeviceClass(_receiptData.DeviceClass ?? "DSDE", size);
                            var rpl = tag + ":" + sizeArg + "#";
                            fileData[i] = fileData[i].Replace(rpl, value);
                            --i;
                            continue;
                        }
                    case 2: value = deviceShift.SaleDeviceId.PadLeft(5, '0'); break;
                    case 3: value = agentShift.AgentId.ToString().PadLeft(5, '0'); break;
                    case 4: value = agentShift.ShiftNumber.ToString(); break;
                    case 5: value = HexToDec(cardInfo.VTokenPayload?.PhysicalDocInfo?.TSCSerialNumber) ?? remove_tag; break;
                    case 6:
                        if (cardInfo.VTokenPayload.Environment.CardIssueDateNullable.HasValue)
                        {
                            value = cardInfo.VTokenPayload.Environment.CardIssueDateNullable.Value.ToString("dd/MM/yyyy");
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 7:
                        if (cardInfo.VTokenPayload.Environment.CardValLimitDateNullable.HasValue)
                        {
                            value = cardInfo.VTokenPayload.Environment.CardValLimitDateNullable.Value.ToString("dd/MM/yyyy");
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 8:
                        value = remove_tag; //cardInfo.VTokenPayload.PhysicalDocInfo.ObjectType.ToString();
                        break;
                    case 9:
                        if (cardInfo.VTokenPayload.PhysicalDocInfo.CardValidityStatusNullable.HasValue)
                        {
                            value = cardInfo.VTokenPayload.PhysicalDocInfo.CardValidityStatusNullable.Value.ToString();
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 10:
                        if (cardInfo.VTokenPayload.Environment.HolderIdNullable.HasValue)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderIdNullable.Value.ToString();
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 11:
                        if (cardInfo.VTokenPayload.Environment.HolderBirthDateSpecified)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderBirthDate.ToString("dd/MM/yyyy");
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 12:
                        if (profile1specified)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfileIdNullable.HasValue
                                ? cardInfo.VTokenPayload.Environment.HolderProfileIdNullable.Value.ToString()
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 13:
                        if (profile1specified)
                        {
                            value = !string.IsNullOrEmpty(cardInfo.VTokenPayload.Environment.HolderProfileDescription)
                                ? cardInfo.VTokenPayload.Environment.HolderProfileDescription
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 14:
                        if (profile1specified)
                        {
                            value = cardInfo.VTokenPayload.Environment.CardIssueDateNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.CardIssueDateNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 15:
                        if (profile1specified)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfileValidityLimitDateTimeNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.HolderProfileValidityLimitDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 16:
                        if (!skipProfile2)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfile2IdNullable.HasValue
                                ? cardInfo.VTokenPayload.Environment.HolderProfile2IdNullable.Value.ToString()
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 17:
                        if (!skipProfile2)
                        {
                            value = !string.IsNullOrEmpty(cardInfo.VTokenPayload.Environment.HolderProfile2Description)
                                ? cardInfo.VTokenPayload.Environment.HolderProfile2Description
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 18:
                        if (!skipProfile2)
                        {
                            value = cardInfo.VTokenPayload.Environment.CardIssueDateNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.CardIssueDateNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 19:
                        if (!skipProfile2)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfile2ValidityLimitDateTimeNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.HolderProfile2ValidityLimitDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 20:
                        if (!skipProfile3)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfile3IdNullable.HasValue
                                ? cardInfo.VTokenPayload.Environment.HolderProfile3IdNullable.Value.ToString()
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 21:
                        if (!skipProfile3)
                        {
                            value = !string.IsNullOrEmpty(cardInfo.VTokenPayload.Environment.HolderProfile3Description)
                                ? cardInfo.VTokenPayload.Environment.HolderProfile3Description
                                : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 22:
                        if (!skipProfile3)
                        {
                            value = cardInfo.VTokenPayload.Environment.CardIssueDateNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.CardIssueDateNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;
                    case 23:
                        if (!skipProfile3)
                        {
                            value = cardInfo.VTokenPayload.Environment.HolderProfile3ValidityLimitDateTimeNullable.HasValue
                            ? cardInfo.VTokenPayload.Environment.HolderProfile3ValidityLimitDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag;
                        }
                        else
                        {
                            value = remove_tag;
                        }
                        break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
            }

            var maxLength = GetLineMaxLength(output);

            var pos = output.IndexOf(company_name_tag);
            if (pos >= 0)
            {
                output.RemoveAt(pos);
                var companyName = _receiptData.GetCompanyNameAllignedToCenter(maxLength);
                if (companyName != null)
                {
                    if (skp_company_name)
                        if (companyName.Length > 0)
                            companyName[0] = skp_tag + companyName[0];
                        else
                            companyName = new[] { skp_tag };
                    output.InsertRange(0, companyName);
                }
                else
                    output.RemoveAt(pos);
            }

            NormalizeOutput(maxLength, ref output, cardInfo.VTokenPayload.PhysicalDocInfo.ShortCardModel == 2);
            var data = string.Join(Environment.NewLine, output);
            //File.WriteAllLines(@"D:\receipts\cd.txt", output);
            var res = new byte[data.Length];
            for (var i = 0; i < data.Length; ++i) res[i] = (byte)data[i];

            return res;
        }

        private List<string> GetContractListFromCard(ContractDetailType contract, ReceiptTemplateType templateType, int index)
        {
            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var filePath = Path.Combine(startupPath, "ReceiptTemplates", $"{(int)templateType}", "Contract.txt");
            var fileData = File.ReadAllLines(filePath).ToList();
            var output = new List<string>();
            var code = 0;

            const string remove_tag = "#REM#";
            const string spaces_tag = "#SPACES#";

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);

                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0: value = index.ToString(); break;
                    case 1:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var slaves = contract.SlaveContracts.Select(p => p.TariffId);
                                value = contract.TariffId.ToString() + $" ({string.Join(", ", slaves)})";
                            }
                            else
                            {
                                value = contract.TariffId.ToString();
                            }
                        }
                        break;
                    case 2:
                        if (string.IsNullOrWhiteSpace(contract.TariffDescription))
                            value = remove_tag;
                        else
                            value = contract.TariffDescription.PadRight(size, ' ');
                        break;
                    case 3:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var slaves = contract.SlaveContracts.Select(p => p.ContractSerialNumber);
                                value = contract.ContractSerialNumber.ToString() + $"\r\n{string.Join("\r\n", slaves)}";
                            }
                            else
                            {
                                value = contract.ContractSerialNumber.ToString();
                            }
                        }
                        break;
                    case 4:
                        value = contract.SellingDateTimeNullable.HasValue
                            ? contract.SellingDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 5:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                value = contract.SlaveContracts.Any(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD)
                                    ? SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD.ToString()
                                    : contract.SaleType.ToString();
                            }
                            else
                            {
                                value = contract.SaleType.ToString();
                            }
                        }
                        break;
                    case 6:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var ctr = contract.SlaveContracts.FirstOrDefault(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD);
                                if (ctr != null && ctr.PassengerClassNullable.HasValue && ctr.PassengerClassNullable.Value > 0)
                                {
                                    value = ctr.PassengerClassNullable.Value.ToString();
                                }
                                else
                                {
                                    value = contract.PassengerClassNullable.HasValue && contract.PassengerClassNullable.Value > 0
                                        ? contract.PassengerClassNullable.Value.ToString()
                                        : remove_tag;
                                }
                            }
                            else
                            {
                                value = contract.PassengerClassNullable.HasValue && contract.PassengerClassNullable.Value > 0
                                    ? contract.PassengerClassNullable.Value.ToString()
                                    : remove_tag;
                            }
                        }
                        break;
                    case 7:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var ctr = contract.SlaveContracts.FirstOrDefault(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD);
                                if (ctr != null && ctr.OriginUidNullable.HasValue)
                                {
                                    value = ctr.OriginDescription ?? ctr.OriginUidNullable.Value.ToString();
                                }
                                else
                                {
                                    value = contract.OriginDescription ?? (contract.OriginUidNullable.HasValue
                                        ? contract.OriginUidNullable.Value.ToString()
                                        : remove_tag);
                                }
                            }
                            else
                            {
                                value = contract.OriginDescription ?? (contract.OriginUidNullable.HasValue
                                    ? contract.OriginUidNullable.Value.ToString()
                                    : remove_tag);
                            }
                        }
                        break;
                    case 8:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var ctr = contract.SlaveContracts.FirstOrDefault(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD);
                                if (ctr != null && ctr.DestinationUidNullable.HasValue)
                                {
                                    value = ctr.DestinationDescription ?? ctr.DestinationUidNullable.Value.ToString();
                                }
                                else
                                {
                                    value = contract.DestinationDescription ?? (contract.DestinationUidNullable.HasValue
                                        ? contract.DestinationUidNullable.Value.ToString()
                                        : remove_tag);
                                }
                            }
                            else
                            {
                                value = contract.DestinationDescription ?? (contract.DestinationUidNullable.HasValue
                                    ? contract.DestinationUidNullable.Value.ToString()
                                    : remove_tag);
                            }
                        }
                        break;
                    case 9:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var ctr = contract.SlaveContracts.FirstOrDefault(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD);
                                if (ctr != null && ctr.ViaNode1Nullable.HasValue)
                                {
                                    value = ctr.viaNode1LangDescription?.FirstOrDefault()?.NodeDescription ?? ctr.ViaNode1Nullable.Value.ToString();
                                }
                                else
                                {
                                    value = contract.viaNode1LangDescription?.FirstOrDefault()?.NodeDescription ?? (contract.ViaNode1Nullable.HasValue
                                        ? contract.ViaNode1Nullable.Value.ToString()
                                        : remove_tag);
                                }
                            }
                            else
                            {
                                value = contract.viaNode1LangDescription?.FirstOrDefault()?.NodeDescription ?? (contract.ViaNode1Nullable.HasValue
                                    ? contract.ViaNode1Nullable.Value.ToString()
                                    : remove_tag);
                            }
                        }
                        break;
                    case 10:
                        {
                            if (contract.SlaveContracts?.Any() ?? false)
                            {
                                var ctr = contract.SlaveContracts.FirstOrDefault(p => p.SaleType == SbmeModels.VtsModels.Responses.Enums.SaleType_t.OD);
                                if (ctr != null && ctr.ViaNode2Nullable.HasValue)
                                {
                                    value = ctr.viaNode2LangDescription?.FirstOrDefault()?.NodeDescription ?? ctr.ViaNode2Nullable.Value.ToString();
                                }
                                else
                                {
                                    value = contract.viaNode2LangDescription?.FirstOrDefault()?.NodeDescription ?? (contract.ViaNode2Nullable.HasValue
                                        ? contract.ViaNode2Nullable.Value.ToString()
                                        : remove_tag);
                                }
                            }
                            else
                            {
                                value = contract.viaNode2LangDescription?.FirstOrDefault()?.NodeDescription ?? (contract.ViaNode2Nullable.HasValue
                                    ? contract.ViaNode2Nullable.Value.ToString()
                                    : remove_tag);
                            }
                        }
                        break;
                    case 11:
                        value = contract.ContractValidityStatusNullable.HasValue
                            ? contract.ContractValidityStatusNullable.Value.ToString()
                            : remove_tag; break;
                    case 12:
                        value = contract.StartValidityDateTimeNullable.HasValue
                            ? contract.StartValidityDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 13:
                        value = contract.EndValidityDateTimeNullable.HasValue
                            ? contract.EndValidityDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 14:
                        value = contract.ValidityLimitDateTimeNullable.HasValue
                            ? contract.ValidityLimitDateTimeNullable.Value.ToString("dd/MM/yyyy")
                            : remove_tag; break;
                    case 15:
                        if (string.IsNullOrWhiteSpace(contract.ZoneList))
                        {
                            value = remove_tag;
                            break;
                        }
                        var split = contract.ZoneList.Split(':');
                        if (split.Length > 1)
                        {
                            var first = split.First();
                            var last = split.Last();
                            value = $"MI{first}-MI{last}";
                        }
                        else
                        {
                            value = split.FirstOrDefault();
                        }
                        break;
                    case 16:
                        value = contract.ResidualQuantityNullable.HasValue
                            ? contract.ResidualQuantityNullable.Value.ToString()
                            : remove_tag; break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (value != remove_tag)
                {
                    var split = value.Split("\r\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    value = split[0];
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                    if (split.Length > 1)
                    {
                        for (var j = 1; j < split.Length; ++j)
                        {
                            output.Add(spaces_tag + split[j]);
                        }
                    }
                }
                if (i == 0 && code == 1) // pessimo, lo so!
                {
                    output = new List<string>();
                    i--;
                }
            }

            return output;
        }

        private List<string> GetValidationsListFromCard(ValidationType validation, ContractDetailType[]? contracts, ReceiptTemplateType templateType, int index)
        {
            if (contracts == null) contracts = new ContractDetailType[0];
            var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
            var filePath = Path.Combine(startupPath, "ReceiptTemplates", $"{(int)templateType}", "Validation.txt");
            var fileData = File.ReadAllLines(filePath).ToList();
            var output = new List<string>();
            var code = 0;

            const string remove_tag = "#REM#";
            const string remove_tag2 = "#REM2#";

            string? tariff = null;
            string? tariffDescr = null;
            string? ctrSn = null;
            var ctrs = contracts;
            var ctr = contracts.FirstOrDefault(p => p.ContractUid?.Equals(validation.ContractUid) ?? false);
            if (ctr != null)
            {
                tariff = ctr.TariffId.ToString();
                tariffDescr = ctr.TariffDescription;
                ctrSn = ctr.ContractSerialNumber.ToString();
            }

            for (var i = 0; i < fileData.Count; ++i)
            {
                var tag = $"#{code.ToString().PadLeft(2, '0')}";
                var openingPos = fileData[i].IndexOf(tag);
                if (openingPos < 0)
                {
                    output.Add(fileData[i]);
                    continue;
                }
                var beginning = fileData[i].Substring(0, openingPos).TrimEnd();
                var lengthPos = fileData[i].IndexOf(':', openingPos + 1);
                var closingPos = fileData[i].IndexOf('#', openingPos + 1);

                var size = 0;
                var sizeArg = string.Empty;
                if (lengthPos > 0 && lengthPos < closingPos)
                {
                    sizeArg = fileData[i].Substring(lengthPos + 1, closingPos - lengthPos - 1);
                    if (!int.TryParse(sizeArg, out size)) size = 0;
                }
                var ending = fileData[i].Substring(closingPos + 1, fileData[i].Length - closingPos - 1);

                string? value = null;
                switch (code++)
                {
                    case 0:
                        if (string.IsNullOrWhiteSpace(tariff))
                            value = remove_tag2;
                        else
                            value = tariff;
                        break;
                    case 1:
                        if (string.IsNullOrWhiteSpace(tariffDescr))
                            value = remove_tag;
                        else
                            value = tariffDescr;
                        break;
                    case 2:
                        if (string.IsNullOrWhiteSpace(ctrSn))
                            value = remove_tag2;
                        else
                            value = ctrSn;
                        break;
                    case 3:
                        if (!validation.FirstValidationDateTimeSpecified || validation.FirstValidationDateTimeNullable == null)
                            value = remove_tag2;
                        else
                            value = validation.FirstValidationDateTime.ToString("dd/MM/yyyy HH:mm");
                        break;
                    case 4:
                        if (!validation.ValidationDateTimeSpecified || validation.ValidationDateTimeNullable == null)
                            value = remove_tag2;
                        else
                            value = validation.ValidationDateTime.ToString("dd/MM/yyyy HH:mm");
                        break;
                    case 5: value = validation.NodeId.ToString(); break;
                    case 6:
                        if (string.IsNullOrWhiteSpace(validation.NodeDescription))
                            value = remove_tag;
                        else
                            value = validation.NodeDescription;
                        break;
                    case 7:
                        if (!validation.ValidationType1Specified || validation.ValidationType1Nullable == null)
                            value = remove_tag2;
                        else
                            value = validation.ValidationType1.ToString();
                        break;
                    case 8:
                        if (!validation.ValidationResultSpecified || validation.ValidationResultNullable == null)
                            value = remove_tag2;
                        else
                            value = validation.ValidationResult.ToString();
                        break;
                    case 9:
                        if (!validation.TripExpirationDateTimeSpecified || validation.TripExpirationDateTimeNullable == null)
                            value = remove_tag2;
                        else
                            value = validation.TripExpirationDateTime.ToString("dd/MM/yyyy HH:mm");
                        break;
                    case 10:
                        if (string.IsNullOrWhiteSpace(validation.RideDescription))
                            value = remove_tag;
                        else
                            value = validation.RideDescription;
                        break;
                    case 11:
                        if (!validation.PassengerCountSpecified || validation.PassengerCountNullable == null)
                            value = remove_tag2;
                        else
                            value = validation.PassengerCount.ToString();
                        break;

                    default:
                        output.Add(fileData[i]);
                        continue;
                }

                if (!(value.Equals(remove_tag) || value.Equals(remove_tag2)))
                {
                    var insert = size == 0 ? value : value.PadLeft(size, ' ');
                    if (!string.IsNullOrWhiteSpace(beginning)) insert = $" {insert}";
                    fileData[i] = $"{beginning}{insert}{ending}";
                    output.Add(fileData[i]);
                }
                if (value.Equals(remove_tag))
                {
                    output.RemoveAt(output.Count - 1);
                }
                if (i == 0 && code == 1) // pessimo, lo so!
                {
                    output = new List<string>();
                    i--;
                }
            }

            return output;
        }

        private string GetAsterisks(int length)
        {
            var separator = "  ";
            var asterisk = "*";
            var asterisks = separator + asterisk + separator;
            var side = false;
            while (asterisks.Length < length)
            {
                asterisks = (side ? $"{separator}{asterisk}" : string.Empty) + asterisks + (!side ? $"{asterisk}{separator}" : string.Empty);
                side = !side;
            }
            return asterisks;
        }

        private static int GetLineMaxLength(IEnumerable<string> lines)
        {
            var result = lines.Select(p => p.ToUpper().Replace("~SKP~", string.Empty).Length).Max();
            if (lines.Where(p => p.Length == result).Count() == 1)
            {
                var newMaxLength = lines.Select(p => {
                    if (p.Count(q => q == '#') < 2) return p;
                    var pos1 = p.IndexOf('#');
                    var pos2 = p.IndexOf('#', pos1 + 1);
                    return p.Remove(pos1, pos2 - pos1 + 1);
                }).Where(p => p.Length < result).Select(p => p.Length).Max();
                if (newMaxLength > 0) result = newMaxLength;
            }
            return result;
        }

        private static void NormalizeOutput(int maxLength, ref List<string> output, bool mifareUl = false)
        {
            if (output == null) return;
            const string skp_tag = "~SKP~";
            const string spaces_tag = "#SPACES#";
            var a = "CONTRATTO TSC";
            var b = "CONTRATTO BSC";
            var len = a.Length;
            var remove = new List<int>();
            for (var i = 0; i < output.Count; ++i)
            {
                var outputI = output[i].Replace(skp_tag, string.Empty);
                if (mifareUl && outputI.Length >= len && outputI.Contains(a))
                {
                    outputI = outputI.Replace(a, b);
                }
                if (outputI.Length > maxLength)
                {
                    var ins = outputI.Substring(maxLength);
                    outputI = outputI.Substring(0, maxLength);
                    output.Insert(i + 1, ins);
                }
                if (!string.IsNullOrWhiteSpace(outputI))
                {
                    if (i > 0 && outputI.StartsWith('=') && output[i - 1].StartsWith('-'))
                    {
                        var all1 = true;
                        for (var j = 0; all1 && j < outputI.Length; ++j)
                        {
                            all1 = outputI[j] == '=';
                        }
                        var all2 = true;
                        for (var j = 0; all2 && j < output[i-1].Length; ++j)
                        {
                            all1 = output[i-1][j] == '-';
                        }
                        if (all1 && all2)
                        {
                            remove.Add(i - 1);
                        }
                    }
                }
                if (outputI.StartsWith(spaces_tag))
                {
                    outputI = outputI.Replace(spaces_tag, string.Empty);
                    var spaces = string.Empty.PadLeft(maxLength - outputI.Length, ' ');
                    outputI = spaces + outputI;
                }
                output[i] = outputI;
            }
            if (remove.Any())
            {
                remove.Reverse();
                foreach (var i in remove)
                {
                    output.RemoveAt(i);
                }
            }
        }
        
        private static string NormalizeDeviceClass(string value, int length) => value.PadRight(length, ' ');

        private Enums.ArticleType GetArticleType(int code) => (Enums.ArticleType)code;

        private bool CmpArticleTypes(int code, Enums.ArticleType articleType) => GetArticleType(code) == articleType;

        private DateTime? TrimTime(DateTime? dt) => TrimTime(dt, null);

        private DateTime TrimTime(DateTime? dt, DateTime defaultValue) => dt.HasValue
            ? TrimTime(dt.Value)
            : defaultValue;

        private DateTime? TrimTime(DateTime? dt, DateTime? defaultValue) => dt.HasValue
            ? TrimTime(dt.Value)
            : defaultValue;

        private DateTime TrimTime(DateTime dt) => DateOnly.FromDateTime(dt).ToDateTime(TimeOnly.MinValue);

        private string? HexToDec(string? value)
        {
            if (!ulong.TryParse(value ?? string.Empty, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)) return value;
            return result.ToString();
        }

        private ulong ExtractContractSerial(decimal serial) => (ulong)serial & 0xFFFF_FFFF;

        public PaymentMethodReport[]? GetAgentShiftPaymentMethods(Guid agentShiftId)
        {
            switch (BglDataLayerConfiguration.Instance.BasketVersion)
            {
                case 1: return GetAgentShiftPaymentMethodsB1(agentShiftId);
                case 2: return GetAgentShiftPaymentMethodsB2(agentShiftId);
                default: return null;
            }
        }

        public PaymentMethodReport[]? GetAgentShiftPaymentMethodsB2(Guid agentShiftId)
        {
            try
            {
                var enums = Enum.GetValues(typeof(Enums.PaymentMethod)).Cast<Enums.PaymentMethod>();
                var dict = new Dictionary<Enums.PaymentMethod, decimal>();
                foreach (var en in enums)
                {
                    dict.Add(en, 0M);
                }
                var dss = _context.DeviceShifts.Where(p => p.AgentShiftId.Equals(agentShiftId));
                foreach (var ds in dss)
                {
                    var tran = _context.SaleTransactions
                        .Where(p => p.DeviceShiftId.Equals(ds.Id))
                        .Include("PaymentDetails");

                    var bt = tran
                        .SelectMany(p => p.PaymentDetails)
                        .Where(p => p != null && p.PaymentMethod == (int)Enums.PaymentMethod.BankTransfer)
                        .Sum(p => p.Amount);
                    dict[Enums.PaymentMethod.BankTransfer] += bt;

                    var pos = tran
                        .SelectMany(p => p.PaymentDetails)
                        .Where(p => p != null && p.PaymentMethod == (int)Enums.PaymentMethod.POS)
                        .Sum(p => p.Amount);
                    dict[Enums.PaymentMethod.POS] += pos;

                    var cash = tran
                        .SelectMany(p => p.PaymentDetails)
                        .Where(p => p != null && p.PaymentMethod == (int)Enums.PaymentMethod.Cash)
                        .Sum(p => p.Amount);
                    dict[Enums.PaymentMethod.Cash] += cash;
                }
                var ret = dict.Where(p => p.Value > 0).Select(p => new PaymentMethodReport {
                    MethodId = (int)p.Key,
                    Amount = p.Value,
                    Description = _context.PaymentMethods.FirstOrDefault(q => q.Code == (int)p.Key)?.Name
                }).ToArray();
                return ret;
            }
            catch (Exception ex)
            {
                throw;
            }
            try
            {
                var aShifts = _context.AgentShifts
                    .Include("DeviceShifts")
                    .Include("DeviceShifts.SaleTransactions")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails.Article")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails")
                    //.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.SaleTransaction")
                    //.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.Article")
                    //.Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Where(p => p.Id == agentShiftId)
                    .ToList();
                if (aShifts == null) return null;

                var transactions = new List<SaleTransaction>();
                foreach (var aShift in aShifts.Where(p => p.DeviceShifts != null))
                    foreach (var dShift in aShift.DeviceShifts.Where(p => p.SaleTransactions != null))
                        foreach (var tr in dShift.SaleTransactions.Where(p => !transactions.Any(q => q.Id == p.Id)))
                            transactions.Add(tr);

                var allPaymentMethods = new Dictionary<int, PaymentMethodReport>();
                foreach (var transaction in transactions)
                {
                    var paymentMethods = GetTransactionPaymentMethods(transaction.Id, true);
                    if (paymentMethods != null)
                    {
                        foreach (var paymentMethod in paymentMethods)
                        {
                            if (allPaymentMethods.ContainsKey(paymentMethod.MethodId))
                                allPaymentMethods[paymentMethod.MethodId].Amount += paymentMethod.Amount;
                            else
                                allPaymentMethods.Add(paymentMethod.MethodId, paymentMethod);
                        }
                    }
                }

                var result = allPaymentMethods.Select(p => p.Value).ToArray();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }
            return null;
        }

        public PaymentMethodReport[]? GetAgentShiftPaymentMethodsB1(Guid agentShiftId)
        {
            try
            {
                var aShifts = _context.AgentShifts
                    .Include("DeviceShifts")
                    .Include("DeviceShifts.SaleTransactions")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails.Article")
                    .Include("DeviceShifts.SaleTransactions.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails")
                    .Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.SaleTransaction")
                    .Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.Article")
                    .Include("DeviceShifts.SaleTransactions.Articles.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Where(p => p.Id == agentShiftId)
                    .ToList();
                if (aShifts == null) return null;

                var transactions = new List<SaleTransaction>();
                foreach (var aShift in aShifts.Where(p => p.DeviceShifts != null))
                    foreach (var dShift in aShift.DeviceShifts.Where(p => p.SaleTransactions != null))
                        foreach (var tr in dShift.SaleTransactions.Where(p => !transactions.Any(q => q.Id == p.Id)))
                            transactions.Add(tr);

                var allPaymentMethods = new Dictionary<int, PaymentMethodReport>();
                foreach (var transaction in transactions)
                {
                    var paymentMethods = GetTransactionPaymentMethods(transaction.Id, true);
                    if (paymentMethods != null)
                        foreach (var paymentMethod in paymentMethods)
                            if (allPaymentMethods.ContainsKey(paymentMethod.MethodId))
                                allPaymentMethods[paymentMethod.MethodId].Amount += paymentMethod.Amount;
                            else
                                allPaymentMethods.Add(paymentMethod.MethodId, paymentMethod);
                }

                var result = allPaymentMethods.Select(p => p.Value).ToArray();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }
            return null;
        }

        public PaymentMethodReport[]? GetTransactionPaymentMethods(Guid transactionId, bool endShift = false)
        {
            switch (BglDataLayerConfiguration.Instance.BasketVersion)
            {
                case 1: return GetTransactionPaymentMethodsB1(transactionId, endShift);
                case 2: return GetTransactionPaymentMethodsB2(transactionId, endShift);
                default: return null;
            }
        }

        public PaymentMethodReport[]? GetTransactionPaymentMethodsB2(Guid transactionId, bool endShift = false)
        {
            try
            {
                var pd = _context.PaymentDetails.FirstOrDefault(p => p.SaleTransactionId.HasValue && p.SaleTransactionId.Value.Equals(transactionId));
                if (pd != null)
                {
                    return new[] {
                        new PaymentMethodReport {
                            MethodId = pd.PaymentMethod,
                            Amount = pd.Amount,
                            Description = _context.PaymentMethods.FirstOrDefault(p => p.Code == pd.PaymentMethod)?.Name
                        }
                    };
                }
            }
            catch (Exception)
            {
                throw;
            }

            try
            {
                var transaction = _context.SaleTransactions
                    .Include("PaymentDetails")
                    .Include("PaymentDetails.Article")
                    .Include("PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("Articles.PaymentDetails")
                    .Include("Articles.PaymentDetails.SaleTransaction")
                    .Include("Articles.PaymentDetails.Article")
                    .Include("Articles.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("Articles.PaymentDetails.Article.MagneticRefundArticleInfo")
                    .FirstOrDefault(p => transactionId.Equals(p.Id));

                if (transaction == null) return null;

                var undoneArticles = _context.CscContractRefundArticleInfos
                    .Include("Article")
                    .Include("Article.SaleTransaction")
                    .Include("Article.PaymentDetails")
                    .Include("Article.PaymentDetails.SaleTransaction")
                    .Include("Article.PaymentDetails.Article")
                    .Include("Article.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("Article.PaymentDetails.Article.MagneticRefundArticleInfo")
                    .Where(p => p.SaleTransactionId == transactionId)
                    .Select(p => p.Article);

                var hasArticles = transaction.Articles?.Any() ?? false;
                if (!hasArticles && !(undoneArticles?.Any() ?? false))
                {
                    return null;
                }

                var paymentDetails = new List<PaymentDetail>();
                if ((transaction.PaymentDetails?.Count ?? 0) > 0)
                {
                    var tpd = transaction.PaymentDetails
                        ?.Where(p =>
                            p.Article?.CscContractRefundArticleInfo == null &&
                            //(p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= transaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id));
                    if ((tpd?.Count() ?? 0) > 0)
                        paymentDetails.AddRange(tpd);
                }
                foreach (var article in transaction.Articles)
                {
                    var apd = article.PaymentDetails
                        ?.Where(p =>
                            p.Article?.CscContractRefundArticleInfo == null &&
                            //(p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= transaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id));
                    if ((apd?.Count() ?? 0) > 0)
                        paymentDetails.AddRange(apd);
                }
                if (undoneArticles != null)
                    foreach (var article in undoneArticles)
                    {
                        var apd = article?.PaymentDetails
                            ?.Where(p => p.SaleTransaction.TransactionTime < transaction.TransactionTime)
                            ?.OrderByDescending(p => p.PaymentTime)
                            ?.FirstOrDefault();
                        if (apd != null)
                            if (endShift)
                            {
                                var sum = apd.Article?.PaymentDetails?.Select(p => p.Article?.PaymentDetails?.Select(q => q.Amount)?.Sum() ?? 0)?.Sum() ?? 0;
                                apd.Amount = sum;
                                paymentDetails.Add(apd);
                            }
                            else
                                if (!paymentDetails.Any(q => q.Id == apd.Id))
                            {
                                apd.Amount = -Math.Abs(apd.Amount);
                                paymentDetails.Add(apd);
                            }
                            else if (article.SaleTransactionId != transactionId && article.SaleTransaction.TransactionTime < transaction.TransactionTime)
                            {
                                var maxCash = paymentDetails.Where(p => p.PaymentMethod == (int)Enums.PaymentMethod.Cash).Sum(p => p.Amount);
                                var amount = article.UniquePrice * article.QuantityIssued;
                                //if (maxCash >= amount)
                                //    paymentDetails.Add(new PaymentDetail
                                //    {
                                //        Amount = -amount,
                                //        PaymentMethod = (int)Enums.PaymentMethod.Cash
                                //    });
                            }
                    }

                var paymentMethods = paymentDetails.GroupBy(p => p.PaymentMethod).Select(p => p.Key).ToArray();
                var result = new PaymentMethodReport[paymentMethods.Length];
                var index = 0;
                foreach (var method in paymentMethods)
                {
                    var sum = paymentDetails.Where(p => p.PaymentMethod == method).Sum(p => p.Amount);
                    result[index++] = new PaymentMethodReport {
                        MethodId = method,
                        Amount = sum,
                        Description = _context.PaymentMethods.FirstOrDefault(p => p.Code == method)?.Name
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
            return null;
        }

        public PaymentMethodReport[]? GetTransactionPaymentMethodsB1(Guid transactionId, bool endShift = false)
        {
            try
            {
                var transaction = _context.SaleTransactions
                    .Include("PaymentDetails")
                    .Include("PaymentDetails.Article")
                    .Include("PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Include("Articles.PaymentDetails")
                    .Include("Articles.PaymentDetails.SaleTransaction")
                    .Include("Articles.PaymentDetails.Article")
                    .Include("Articles.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .FirstOrDefault(p => transactionId.Equals(p.Id));

                if (transaction == null) return null;

                var undoneArticles = _context.CscContractRefundArticleInfos
                    .Include("Article")
                    .Include("Article.PaymentDetails")
                    .Include("Article.PaymentDetails.SaleTransaction")
                    .Include("Article.PaymentDetails.Article")
                    .Include("Article.PaymentDetails.Article.CscContractRefundArticleInfo")
                    .Where(p => p.SaleTransactionId == transactionId)
                    .Select(p => p.Article);

                var paymentDetails = new List<PaymentDetail>();
                if ((transaction.PaymentDetails?.Count ?? 0) > 0)
                {
                    var tpd = transaction.PaymentDetails
                        ?.Where(p =>
                            p.Article?.CscContractRefundArticleInfo == null &&
                            //(p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= transaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id));
                    if ((tpd?.Count() ?? 0) > 0)
                        paymentDetails.AddRange(tpd);
                }
                foreach (var article in transaction.Articles)
                {
                    var apd = article.PaymentDetails
                        ?.Where(p =>
                            p.Article?.CscContractRefundArticleInfo == null &&
                            //(p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= transaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id));
                    if ((apd?.Count() ?? 0) > 0)
                        paymentDetails.AddRange(apd);
                }
                if (undoneArticles != null)
                    foreach (var article in undoneArticles)
                    {
                        var apd = article?.PaymentDetails
                        ?.Where(p => p.SaleTransaction.TransactionTime < transaction.TransactionTime)
                        ?.OrderByDescending(p => p.PaymentTime)
                        ?.FirstOrDefault();
                        if (apd != null)
                            if (endShift)
                            {
                                var sum = apd.Article?.PaymentDetails?.Select(p => p.Article?.PaymentDetails?.Select(q => q.Amount)?.Sum() ?? 0)?.Sum() ?? 0;
                                apd.Amount = sum;
                                paymentDetails.Add(apd);
                            }
                            else
                                if (!paymentDetails.Any(q => q.Id == apd.Id))
                            {
                                apd.Amount = -Math.Abs(apd.Amount);
                                paymentDetails.Add(apd);
                            }
                    }

                var paymentMethods = paymentDetails.GroupBy(p => p.PaymentMethod).Select(p => p.Key).ToArray();
                var result = new PaymentMethodReport[paymentMethods.Length];
                var index = 0;
                foreach (var method in paymentMethods)
                {
                    var sum = paymentDetails.Where(p => p.PaymentMethod == method).Sum(p => p.Amount);
                    result[index++] = new PaymentMethodReport {
                        MethodId = method,
                        Amount = sum,
                        Description = _context.PaymentMethods.FirstOrDefault(p => p.Code == method)?.Name
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }
            return null;
        }

        public PaymentMethodReport[]? GetArticlePaymentMethods(Guid articleId, bool undone = false)
        {
            return null;
            switch (BglDataLayerConfiguration.Instance.BasketVersion)
            {
                case 1: return GetArticlePaymentMethodsB1(articleId, undone);
                case 2: return GetArticlePaymentMethodsB2(articleId, undone);
                default: return null;
            }
        }

        public PaymentMethodReport[]? GetArticlePaymentMethodsB2(Guid articleId, bool undone = false)
        {
            try
            {
                var article = _context.Articles
                    .Include("SaleTransaction")
                    .Include("SaleTransaction.PaymentDetails")
                    .Include("CscContractRefundArticleInfo")
                    .Include("PaymentDetails")
                    .Include("PaymentDetails.SaleTransaction")
                    .Include("PaymentDetails.Article")
                    .Include("PaymentDetails.Article.SaleTransaction")
                    .Include("PaymentDetails.Article.CscContractRefundArticleInfo")
                    .FirstOrDefault(p => articleId.Equals(p.Id));

                if (article == null) return null;

                var paymentDetails = new List<PaymentDetail>();
                List<PaymentDetail>? apd = null;
                if ((article.PaymentDetails?.Count ?? 0) > 0)
                {
                    apd = article.PaymentDetails
                        ?.Where(p =>
                            (p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= article.SaleTransaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id)).ToList();
                    if ((apd?.Count() ?? 0) > 0)
                        paymentDetails = apd;
                }

                if (undone &&
                    (apd?.Count() ?? 0) == 0 &&
                    (article.PaymentDetails?.Count() ?? 0) > 0 &&
                    article.PaymentDetails.First().Amount == 0 &&
                    article.PaymentDetails.First().Article != null)
                {
                    var pd = article.PaymentDetails.First();
                    var ar = pd.Article;
                    pd.Amount = ar.UniquePrice * ar.QuantityIssued;
                    paymentDetails?.Add(pd);
                }

                var paymentMethods = paymentDetails?.GroupBy(p => p.PaymentMethod)?.Select(p => p.Key)?.ToArray();
                var result = new PaymentMethodReport[paymentMethods?.Length ?? 0];
                var index = 0;
                if (paymentMethods != null)
                    foreach (var method in paymentMethods)
                    {
                        var sum = paymentDetails?.Where(p => p.PaymentMethod == method)?.Sum(p => p.Amount) ?? 0;
                        result[index++] = new PaymentMethodReport {
                            MethodId = method,
                            Amount = sum,
                            Description = _context.PaymentMethods.FirstOrDefault(p => p.Code == method)?.Name
                        };
                    }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }
            return null;
        }

        public PaymentMethodReport[]? GetArticlePaymentMethodsB1(Guid articleId, bool undone = false)
        {
            try
            {
                var article = _context.Articles
                    .Include("SaleTransaction")
                    .Include("SaleTransaction.PaymentDetails")
                    .Include("CscContractRefundArticleInfo")
                    .Include("PaymentDetails")
                    .Include("PaymentDetails.SaleTransaction")
                    .Include("PaymentDetails.Article")
                    .Include("PaymentDetails.Article.SaleTransaction")
                    .Include("PaymentDetails.Article.CscContractRefundArticleInfo")
                    .FirstOrDefault(p => articleId.Equals(p.Id));

                if (article == null) return null;

                var paymentDetails = new List<PaymentDetail>();
                List<PaymentDetail>? apd = null;
                if ((article.PaymentDetails?.Count ?? 0) > 0)
                {
                    apd = article.PaymentDetails
                        ?.Where(p =>
                            (p.Article?.CscContractRefundArticleInfo == null || p.Amount > 0) &&
                            p.SaleTransaction.TransactionTime <= article.SaleTransaction.TransactionTime &&
                            !paymentDetails.Any(q => q.Id == p.Id)).ToList();
                    if ((apd?.Count() ?? 0) > 0)
                        paymentDetails = apd;
                }

                if (undone &&
                    (apd?.Count() ?? 0) == 0 &&
                    (article.PaymentDetails?.Count() ?? 0) > 0 &&
                    article.PaymentDetails.First().Amount == 0 &&
                    article.PaymentDetails.First().Article != null)
                {
                    var pd = article.PaymentDetails.First();
                    var ar = pd.Article;
                    pd.Amount = ar.UniquePrice * ar.QuantityIssued;
                    paymentDetails?.Add(pd);
                }

                var paymentMethods = paymentDetails?.GroupBy(p => p.PaymentMethod)?.Select(p => p.Key)?.ToArray();
                var result = new PaymentMethodReport[paymentMethods?.Length ?? 0];
                var index = 0;
                if (paymentMethods != null)
                    foreach (var method in paymentMethods)
                    {
                        var sum = paymentDetails?.Where(p => p.PaymentMethod == method)?.Sum(p => p.Amount) ?? 0;
                        result[index++] = new PaymentMethodReport {
                            MethodId = method,
                            Amount = sum,
                            Description = _context.PaymentMethods.FirstOrDefault(p => p.Code == method)?.Name
                        };
                    }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
            }
            return null;
        }
    }
}
