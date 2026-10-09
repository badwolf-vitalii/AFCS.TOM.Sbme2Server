using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using Basket = AFCS.TOM.SbmeModels.Basket;
using DL = AFCS.TOM.SbmeDataLayer;
using Enums = AFCS.TOM.SbmeModels.Enums;
using Sales = AFCS.TOM.SbmeModels.OutputParameters.Sales;
using TM = AFCS.TOM.SbmeModels.BglDataLayer;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/bgl/[controller]")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Bgl.Sales");
        
        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private BglDataLayerConfiguration _bglDataLayerConfiguration { get; }
        private ISalesService _salesService { get; }
        private ReceiptsManager _receiptsManager { get; }

        public SalesController(ReceiptsManager receiptsManager, ISalesService salesService)
        {
            _salesService = salesService;
            _receiptsManager = receiptsManager;
        }

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!_salesService.IsServiceEnabled)
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        [HttpPost("CommitSaleTransaction")]
        public async Task<IActionResult> CommitSaleTransaction([FromBody] TM.SaleTransaction saleTransaction)
        {
            Guid transactionId = Guid.Empty;
            try
            {
                CheckServiceAvailability(true);
                var serialized = JsonConvert.SerializeObject(saleTransaction);
                var insertion = JsonConvert.DeserializeObject<DL.SaleTransaction>(serialized);

                if ((insertion?.Articles?.Count() ?? 0) > 0)
                {
                    var ins = new List<Article?>();
                    var articles = saleTransaction.Articles?.ToArray() ?? new TM.Article[0];
                    for (var i = 0; ins != null && i < articles.Length; ++i)
                    {
                        if ((articles[i].Description?.Length ?? 0) > 50)
                        {
                            var description = articles[i].Description;
                            articles[i].Description = description.Substring(0, 50);
                            articles[i].LongDescription = description.Substring(0, Math.Min(250, description.Length));
                        }
                        var art = saleTransaction.Articles?.Skip(i)?.FirstOrDefault();
                        if (art?.AdditionalPtInfo != null)
                        {
                            articles[i].BillingPrices = art.AdditionalPtInfo.UniqueBillingPriceCent?.ToString();
                            articles[i].NumSalePeriodUnits = art.AdditionalPtInfo.NumSalePeriodUnits;
                        }
                        var serializedArticle = JsonConvert.SerializeObject(articles[i]);
                        if (serializedArticle != null)
                            ins.Add(JsonConvert.DeserializeObject<DL.Article>(serializedArticle)!);
                    }
                    insertion!.Articles = ins;
                }
                else if ((insertion?.PaymentDetails?.Count ?? 0) > 0 && insertion?.PaymentDetails is List<TM.PaymentDetail> orig)
                {
                    var ins = insertion?.PaymentDetails?.ToList();
                    for (var i = 0; ins != null && i < orig.Count; ++i)
                    {
                        if (orig[i].Article == null) continue;
                        var serializedArticle = JsonConvert.SerializeObject(orig[i].Article);
                        ins[i].Article = JsonConvert.DeserializeObject<DL.Article>(serializedArticle);
                        ins[i].Article.SaleTransactionId = orig[i].Article?.SaleTransactionId;

                        if ((ins[i].Article.Description?.Length ?? 0) > 50)
                        {
                            var description = ins[i].Article.Description;
                            ins[i].Article.Description = description.Substring(0, 50);
                            ins[i].Article.LongDescription = description.Substring(0, Math.Min(250, description.Length));
                        }
                    }
                }

                transactionId = await _salesService.CommitSaleTransaction(insertion);

                if (!transactionId.Equals(Guid.Empty))
                    await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(transactionId);
        }

        [HttpPost("CommitPtTransaction")]
        public async Task<IActionResult> CommitPtTransaction([FromBody] PtConfirmTransaction transaction)
        {
            var transactionId = Guid.Empty;
            try
            {
                CheckServiceAvailability(true);
                transactionId = await _salesService.CommitPtTransaction(transaction);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(transactionId);
        }

        [HttpPost("GetPtTransaction")]
        public async Task<IActionResult> CheckIfPtTransactionExists([FromBody] PtConfirmTransaction transaction)
        {
            var result = false;
            try
            {
                CheckServiceAvailability(true);
                result = await _salesService.CheckIfPtTransactionExists(transaction);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("AddCscContractArticleInfo")]
        public async Task<IActionResult> AddCscContractArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.CscContractArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.CscContractArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    insertion.Add(new DL.CscContractArticleInfo {
                        ArticleId = info.Key?.Equals(Guid.Empty) ?? true ? Guid.NewGuid() : new Guid(info.Key),
                        TariffId = info.Value?.TariffId ?? 0,
                        TariffDescription = info.Value?.TariffDescription,
                        VtContractId = info.Value?.VtContractId,
                        CardSerialNumber = info.Value?.CardSerialNumber ?? string.Empty,
                        VtokenBefore = info.Value?.CardInfoBefore ?? new byte[0],
                        VtokenAfter = info.Value?.CardInfoAfter ?? new byte[0],
                        Origin = info.Value?.Origin,
                        OriginDescription = info.Value?.OriginDescription,
                        Destination = info.Value?.Destination,
                        DestinationDescription = info.Value?.DestinationDescription,
                        ViaPoint1 = info.Value?.ViaPoint1,
                        Via1Description = info.Value?.Via1Description,
                        ViaPoint2 = info.Value?.ViaPoint2,
                        Via2Description = info.Value?.Via2Description,
                        HolderId = (int?)info.Value?.HolderId,
                        HolderBirthday = info.Value?.HolderBirthday,
                        KmDistance = info.Value?.KmDistance,
                        NbAreas = info.Value?.NbAreas,
                        NbJourney = info.Value?.NbJourney,
                        PassengerClass = info.Value?.PassengerClass,
                        ShortCardModel = info.Value?.ShortCardModel ?? 0,
                        DtSvd = info.Value?.DtSvd,
                        DtEvd = info.Value?.DtEvd,
                        DtLvd = info.Value?.DtLvd,
                        VtContractGroupId = info.Value?.VtContractGroupId ?? string.Empty,
                        VtSlaveContractsId = info.Value?.VtSlaveContractsId,
                        VtSlaveContractsTariffId = info.Value?.VtSlaveContractsTariffId,
                        ZoneList = info.Value?.ZoneList,
                        AreaExtension = info.Value?.AreaExtension,
                        PaidByEmployee = info.Value?.PaidByEmployee,
                        NumberOfUnits = info.Value?.NumberOfUnits
                    });
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddCscContractArticleInfo(insertion, arts);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddCscContractRefundArticleInfo")]
        public async Task<IActionResult> AddCscContractRefundArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.CscContractRefundArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.CscContractRefundArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    if (info.Value != null)
                    {
                        insertion.Add(new DL.CscContractRefundArticleInfo {
                            ArticleId = info.Value.ArticleId,
                            SaleTransactionId = info.Value.SaleTransactionId
                        });
                        arts.Add(info.Value?.AdditionalPtInfo);
                    }
                }
                added = await _salesService.AddCscContractRefundArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddMagneticTicketArticleInfo")]
        public async Task<IActionResult> AddMagneticTicketArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.MagneticTicketArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                DateTime? checkDateTime(DateTime? date) => !date.HasValue || date.Value == DateTime.MinValue ? null : date;

                var insertion = new List<DL.MagneticArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    insertion.Add(new DL.MagneticArticleInfo {
                        Id = new Guid(info.Key),
                        TariffId = info.Value.TariffId,
                        ArticleId = new Guid(info.Key),
                        Origin = info.Value.Origin,
                        Destination = info.Value.Destination,
                        ViaPoint1 = info.Value.ViaPoint1,
                        ViaPoint2 = info.Value.ViaPoint2,
                        KmDistance = info.Value.KmDistance,
                        NbAreas = info.Value.NbAreas,
                        NbJourney = info.Value.NbJourney,
                        PassengerClass = info.Value.PassengerClass,
                        StartValidityDate = checkDateTime(info.Value.StartValidityDate),
                        EndValidityDate = checkDateTime(info.Value.EndValidityDate),
                        LimitValidityDate = checkDateTime(info.Value.LimitValidityDate),
                        FirstSerialNumber = (ulong)(info.Value.FirstSerialNumber ?? 0) & 0xFFFF_FFFF,
                        NbInTrash = info.Value.NbInTrash,
                        PermanentCutterValue = info.Value.PermanentCutterValue,
                        ResultIssuingCode = info.Value.ResultIssuingCode,
                        RollCode = info.Value.RollCode,
                        RollNumber = info.Value.RollNumber,
                        Vtoken = info.Value.Vtoken,
                        LastTicketErrorCode = info.Value.LastTicketErrorCode,
                        VtContractGroupId = info.Value.VtContractGroupId
                    });
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddMagneticTicketArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddMagneticRefundArticleInfo")]
        public async Task<IActionResult> AddMagneticRefundArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.MagneticRefundArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.MagneticRefundArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    if (info.Value != null)
                    {
                        insertion.Add(new DL.MagneticRefundArticleInfo {
                            ArticleId = info.Value.ArticleId,
                            SaleTransactionId = info.Value.SaleTransactionId
                        });
                        arts.Add(info.Value?.AdditionalPtInfo);
                    }
                }
                added = await _salesService.AddMagneticRefundArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddContactlessCardArticleInfo")]
        public async Task<IActionResult> AddContactlessCardArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.ContactlessCardArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                DateTime? checkDateTime(DateTime? date) => !date.HasValue || date.Value == DateTime.MinValue ? null : date;

                var insertion = new List<DL.ContactlessCardArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    insertion.Add(new DL.ContactlessCardArticleInfo {
                        ArticleId = new Guid(info.Key),
                        CardSerialNumberLo = info.Value.CardSerialNumberLo,
                        CardSerialNumberPh = info.Value.CardSerialNumberPh,
                        ShortCardModel = info.Value.ShortCardModel,
                        CardType = info.Value.CardType,
                        IssuingDate = info.Value.IssuingDate,
                        EndValidityDate = info.Value.EndValidityDate,
                        HolderId = (int)info.Value.HolderId,
                        HolderBirthday = info.Value.HolderBirthday,
                        HolderSex = info.Value.HolderSex,
                        GraphicLayout = info.Value.GraphicLayout,
                        ProfileId = info.Value.ProfileId,
                        ProfileEndValidityDate = info.Value.ProfileEndValidityDate,
                        ProfileAux1Id = info.Value.ProfileAux1Id,
                        ProfileAux1EndValidityDate = checkDateTime(info.Value.ProfileAux1EndValidityDate),
                        ProfileAux2Id = info.Value.ProfileAux2Id,
                        ProfileAux2EndValidityDate = checkDateTime(info.Value.ProfileAux2EndValidityDate),
                        ProfileDescription = info.Value.ProfileDescription,
                        ProfileAux1Description = info.Value.ProfileAux1Description,
                        ProfileAux2Description = info.Value.ProfileAux2Description,
                        ReissuingCscSerialLo = info.Value.ReissuingCscSerialLo,
                        ReissuingCscSerialPh = info.Value.ReissuingCscSerialPh,
                        ReissuingReasonCode = info.Value.ReissuingReasonCode,
                        PaidByEmployee = info.Value.PaidByEmployee
                    });
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddContactlessCardArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddProfileRenewalArticleInfo")]
        public async Task<IActionResult> AddProfileRenewalArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.ProfileRenewalArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.ProfileRenewalArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    insertion.Add(new DL.ProfileRenewalArticleInfo {
                        ArticleId = new Guid(info.Key),
                        CardSerialNumber = info.Value.CardSerialNumber.ToString(),
                        HolderId = (int)info.Value.HolderId,
                        HolderBirthday = info.Value.HolderBirthday,
                        HolderSex = info.Value.HolderSex,
                        VtGroupId = info.Value.VtGroupId,
                        HolderProfile = info.Value.HolderProfile,
                        HolderProfile2 = info.Value.HolderProfile2,
                        HolderProfile3 = info.Value.HolderProfile3,
                        HolderProfileDescription = info.Value.HolderProfileDescription,
                        HolderProfileDescription2 = info.Value.HolderProfileDescription2,
                        HolderProfileDescription3 = info.Value.HolderProfileDescription3,
                        HolderProfileLimitDate = info.Value.HolderProfileLimitDate,
                        HolderProfile2LimitDate = info.Value.HolderProfile2LimitDate,
                        HolderProfile3LimitDate = info.Value.HolderProfile3LimitDate,
                        OldHolderProfile = info.Value.OldHolderProfile,
                        OldHolderProfile2 = info.Value.OldHolderProfile2,
                        OldHolderProfile3 = info.Value.OldHolderProfile3,
                        OldHolderProfileDescription = info.Value.OldHolderProfileDescription,
                        OldHolderProfileDescription2 = info.Value.OldHolderProfileDescription2,
                        OldHolderProfileDescription3 = info.Value.OldHolderProfileDescription3,
                        OldHolderProfileLimitDate = info.Value.OldHolderProfileLimitDate,
                        OldHolderProfile2LimitDate = info.Value.OldHolderProfile2LimitDate,
                        OldHolderProfile3LimitDate = info.Value.OldHolderProfile3LimitDate,
                        ShortCardModel = info.Value.ShortCardModel
                    });
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddProfileRenewalArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddPtItemArticleInfo")]
        public async Task<IActionResult> AddPtItemArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.PtItemArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.PtItemArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    insertion.Add(new DL.PtItemArticleInfo {
                        ArticleId = new Guid(info.Key),
                        ItemId = info.Value.ItemId,
                        ItemCode = info.Value.ItemCode,
                        GraphicsVersion = info.Value.GraphicsVersion,
                        Series = info.Value.Series,
                        SerialStart = info.Value.SerialStart,
                        SerialEnd = info.Value.SerialEnd,
                        ItemType = (int?)info.Value.ItemType,
                        SaleDeviceId = info.Value.SaleDeviceId
                    });
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddPtItemArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddPtItemRefundArticleInfo")]
        public async Task<IActionResult> AddPtItemRefundArticleInfo([FromBody] List<UniqueKeyValuePair<string, Basket.ArticleInfos.PtItemRefundArticleInfo>> infos)
        {
            List<Guid>? added = null;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.PtItemRefundArticleInfo>();
                var arts = new List<Basket.AdditionalPtInfo?>();
                foreach (var info in infos)
                {
                    if (info.Value != null)
                    {
                        insertion.Add(new DL.PtItemRefundArticleInfo {

                            ArticleId = info.Value.ArticleId,
                            SaleTransactionId = info.Value.SaleTransactionId
                        });
                    }
                    arts.Add(info.Value?.AdditionalPtInfo);
                }
                added = await _salesService.AddPtItemRefundArticleInfo(insertion);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddSpoiledMagneticTicketArticleInfo")]
        public async Task<IActionResult> AddSpoiledMagneticTicketArticleInfo([FromBody] SpoiledMagneticArticleInfo article)
        {
            try
            {
                CheckServiceAvailability(true);
                DateTime? checkDateTime(DateTime? date) => !date.HasValue || date.Value == DateTime.MinValue ? null : date;

                var articleId = Guid.NewGuid();
                var newInfo = new DL.MagneticArticleInfo() {
                    Id = Guid.NewGuid(),
                    ArticleId = articleId,
                    TariffId = article.ArticleInfo.TariffId,
                    Origin = article.ArticleInfo.Origin,
                    Destination = article.ArticleInfo.Destination,
                    ViaPoint1 = article.ArticleInfo.ViaPoint1,
                    ViaPoint2 = article.ArticleInfo.ViaPoint2,
                    KmDistance = article.ArticleInfo.KmDistance,
                    NbAreas = article.ArticleInfo.NbAreas,
                    NbJourney = article.ArticleInfo.NbJourney,
                    PassengerClass = article.ArticleInfo.PassengerClass,
                    StartValidityDate = checkDateTime(article.ArticleInfo.StartValidityDate),
                    EndValidityDate = checkDateTime(article.ArticleInfo.EndValidityDate),
                    LimitValidityDate = checkDateTime(article.ArticleInfo.LimitValidityDate),
                    FirstSerialNumber = (ulong)(article.ArticleInfo.FirstSerialNumber ?? 0) & 0xFFFF_FFFF,
                    NbInTrash = article.ArticleInfo.NbInTrash,
                    PermanentCutterValue = article.ArticleInfo.PermanentCutterValue,
                    ResultIssuingCode = article.ArticleInfo.ResultIssuingCode,
                    RollCode = article.ArticleInfo.RollCode,
                    RollNumber = article.ArticleInfo.RollNumber,
                    Vtoken = article.ArticleInfo.Vtoken,
                    LastTicketErrorCode = article.ArticleInfo.LastTicketErrorCode,
                    VtContractGroupId = article.ArticleInfo.VtContractGroupId
                };
                var insertion = new DL.Article {
                    Id = articleId,
                    ArticleType = (int)Enums.ArticleType.MagneticTicket,
                    Description = article.Description,
                    UniquePrice = article.UniquePrice ?? 0M,
                    MagneticArticleInfos = new List<DL.MagneticArticleInfo> {
                        newInfo
                    }
                };

                await _salesService.AddSpoiledMagneticTicketArticleInfo(insertion, article.DeviceShiftId, article.Time);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("AddWhiteListContactlessCardArticleInfo")]
        public async Task<IActionResult> AddWhiteListContactlessCardArticleInfo([FromBody] TM.AddingArticleInfoParameters parameters)
        {
            Guid[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var serialized = JsonConvert.SerializeObject(parameters.Articles);
                var insertion = JsonConvert.DeserializeObject<ICollection<Article>>(serialized);
                result = await _salesService.AddWhiteListContactlessCardArticleInfo(insertion, parameters.DeviceShiftId, parameters.TransactionTime);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("AddContactlessCardExpirationExtensionArticleInfo")]
        public async Task<IActionResult> AddContactlessCardExpirationExtensionArticleInfo([FromBody] TM.AddingArticleInfoParameters parameters)
        {
            Guid[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var serialized = JsonConvert.SerializeObject(parameters.Articles);
                var insertion = JsonConvert.DeserializeObject<ICollection<Article>>(serialized);
                result = await _salesService.AddContactlessCardExpirationExtensionArticleInfo(insertion, parameters.DeviceShiftId, parameters.TransactionTime);
                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("RegisterCanceledCscContracts")]
        public async Task<IActionResult> RegisterCanceledCscContracts([FromBody] List<CanceledCscContract> contracts, [FromQuery] ReceiptTemplateType? receiptTemplateType)
        {
            byte[]? receipt = null;
            try
            {
                CheckServiceAvailability(true);
                var serialized = JsonConvert.SerializeObject(contracts);
                var insertion = JsonConvert.DeserializeObject<List<DL.CanceledCscContract>>(serialized);
                await _salesService.RegisterCanceledCscContracts(insertion);
                await _salesService.SaveChangesAsync();

                var master = insertion.FirstOrDefault(p => p.IsMaster > 0);

                if (master != null && receiptTemplateType.HasValue)
                {
                    master.Receipt = await _receiptsManager.CreateCanceledCscContractReceipt(master.Id, receiptTemplateType.Value);
                    receipt = master.Receipt;
                }

                await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(receipt);
        }

        [HttpPost("AddPosDetails")]
        public async Task<IActionResult> AddPosDetails([FromBody] List<Basket.PosDetail> posDetails)
        {
            var added = 0;
            try
            {
                CheckServiceAvailability(true);
                var insertion = new List<DL.PosDetail>();
                foreach (var info in posDetails)
                    insertion.Add(new DL.PosDetail {
                        SaleTransactionId = info.SaleTransactionId,
                        TransactionResult = info.TransactionResult,
                        TerminalId = info.TerminalId,
                        AcquirerId = info.AcquirerId,
                        Stan = info.Stan,
                        KODescription = info.KODescription,
                        TransactionType = info.TransactionType,
                        CardType = info.CardType,
                        AmountRequested = info.AmountRequested,
                        PosBalance = info.PosBalance,
                        BankBalance = info.BankBalance,
                        Pan = info.Pan,
                        AuthorizationCode = info.AuthorizationCode,
                        OperationNumber = info.OperationNumber,
                        AmountAuth = info.AmountAuth,
                        PreauthorizationCode = info.PreauthorizationCode,
                        ActionCode = info.ActionCode,
                        DataTrs = info.DataTrs,
                        AmountEcho = info.AmountEcho,
                        EsitoLetturaTrk1 = info.EsitoLetturaTrk1,
                        EsitoLetturaTrk2 = info.EsitoLetturaTrk2,
                        Trk1 = info.Trk1,
                        Trk2 = info.Trk2,
                        StatoPos = info.StatoPos,
                        InfoRelease = info.InfoRelease,
                        DatiAggiuntiviTagDaGt = info.DatiAggiuntiviTagDaGt,
                        TimeOnPc = info.TimeOnPc,
                        IsRefund = info.IsRefund,
                        PostransactionId = info.PostransactionId
                    });

                await _salesService.AddPosDetails(insertion);
                added = await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("AddPtBankTransferInfo")]
        public async Task<IActionResult> AddPtBankTransferInfo([FromBody] DL.PtBankTransferInfo info)
        {
            var added = 0;
            try
            {
                CheckServiceAvailability(true);
                await _salesService.AddPtBankTransferInfo(info);
                added = await _salesService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(added);
        }

        [HttpPost("GetPosDetails")]
        [HelperClasses.QueryStringConstraint("transactionId", true)]
        public async Task<IActionResult> GetPosDetails([FromQuery] string transactionId)
        {
            IEnumerable<PosDetail>? details = null;
            try
            {
                CheckServiceAvailability(true);
                details = _salesService.GetPosDetails(new Guid(transactionId));
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(details);
        }

        [HttpPost("GetPosRefundDetails")]
        [HelperClasses.QueryStringConstraint("transactionId", true)]
        public async Task<IActionResult> GetPosRefundDetails([FromQuery] string transactionId)
        {
            Sales.PosRefundDetails? details = null;
            try
            {
                CheckServiceAvailability(true);
                details = _salesService.GetPosRefundDetails(new Guid(transactionId));
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(details);
        }

        [HttpPost("GetCashRefundDetails")]
        [HelperClasses.QueryStringConstraint("transactionId", true)]
        public async Task<IActionResult> GetCashRefundDetails([FromQuery] string transactionId)
        {
            Sales.CashRefundDetails? details = null;
            try
            {
                CheckServiceAvailability(true);
                details = _salesService.GetCashRefundDetails(new Guid(transactionId));
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(details);
        }

        [HttpPost("GetSoldArticles")]
        [HelperClasses.QueryStringConstraint("serialNumber", true)]
        public async Task<IActionResult> GetSoldArticles([FromQuery] string serialNumber, [FromQuery] bool inlcudeVTokens = true)
        {
            IEnumerable<Basket.Article> articles = null;
            try
            {
                CheckServiceAvailability(true);
                articles = _salesService.GetSoldArticles(serialNumber, inlcudeVTokens, int.MaxValue);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(articles.ToList());
        }

        [HttpPost("GetArticleByItsCscContractInfo")]
        public async Task<IActionResult> GetArticleByItsCscContractInfo([FromQuery] Guid cscContractInfoId)
        {
            TM.Article? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = _salesService.GetArticleByItsCscContractInfo(cscContractInfoId);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpPost("GetCscContractArticleInfos")]
        public async Task<IActionResult> GetCscContractArticleInfos([FromQuery] Guid cscContractInfoId)
        {
            List<TM.CscContractArticleInfo>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = _salesService.GetCscContractArticleInfos(cscContractInfoId);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpPost("GetCscContractArticles")]
        public async Task<IActionResult> GetCscContractArticles([FromQuery] string cardSerialNumber, [FromQuery] int max)
        {
            List<TOM.SbmeModels.OutputParameters.Sales.CscContractArticleDetails> result = null;
            try
            {
                CheckServiceAvailability(true);
                var res = _salesService.GetCscContractArticles(cardSerialNumber);
                if (res != null)
                {
                    var serialized = JsonConvert.SerializeObject(res);
                    result = JsonConvert.DeserializeObject<List<TOM.SbmeModels.OutputParameters.Sales.CscContractArticleDetails>>(serialized);
                }
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(max > 0 ? result?.Take(max)?.ToList() : result);
        }

        [HttpPost("GetUndonableContract")]
        public async Task<IActionResult> GetUndonableContract([FromQuery] string cardSerialNumber, [FromQuery] int shortCardModel, [FromQuery] Guid? deviceShiftId, [FromQuery] Guid? startFrom)
        {
            TM.UndonableContract? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = _salesService.GetUndonableContract(cardSerialNumber, shortCardModel, deviceShiftId, startFrom);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpPost("GetUndonableContracts")]
        public async Task<IActionResult> GetUndonableContracts([FromQuery] string cardSerialNumber, [FromQuery] int shortCardModel, [FromQuery] Guid? deviceShiftId, [FromQuery] Guid? startFrom, [FromQuery] bool? undone)
        {
            List<TM.UndonableContract>? result = null;
            try
            {
                CheckServiceAvailability(true);
                var un = undone.HasValue ? undone.Value : false;
                result = _salesService.GetUndonableContracts(cardSerialNumber, shortCardModel, deviceShiftId, startFrom, un);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpGet("GetMostlyUsedTariffs")]
        public async Task<IActionResult> GetMostlyUsedTariffs([FromQuery] Enums.ArticleType articleType, [FromQuery] byte periodInDays)
        {
            Sales.MostlyUsedTariffs? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = _salesService.GetMostlyUsedTariffs(articleType, periodInDays);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result?.TariffCounts);
        }

        [HttpGet("GetTheLongestPeriodOfArticles")]
        public async Task<IActionResult> GetTheLongestPeriodOfArticles([FromQuery] Enums.ArticleType articleType)
        {
            var result = 0;
            try
            {
                CheckServiceAvailability(true);
                result = _salesService.GetTheLongestPeriodOfArticles(articleType);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpGet("GetMissingPtTransactions")]
        public async Task<IActionResult> GetMissingPtTransactions()
        {
            List<DL.SaleTransaction>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await _salesService.GetMissingPtTransactionsAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }

        [HttpGet("GetSaleTransaction")]
        public async Task<IActionResult> GetSaleTransaction(Guid transactionId)
        {
            DL.SaleTransaction? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await _salesService.GetSaleTransaction(transactionId);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(result);
        }
    }
}
