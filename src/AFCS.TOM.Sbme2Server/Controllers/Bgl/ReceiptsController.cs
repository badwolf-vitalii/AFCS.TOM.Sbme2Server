using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Basket.ArticleInfos;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Sales;
using AFCS.TOM.SbmeModels.OutputParameters.ShiftReport;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using ArticleInfoBasket = AFCS.TOM.SbmeModels.Basket.ArticleInfos.CscContractArticleInfo;
using CardInfo = AFCS.TOM.SbmeModels.VtsWrapper.Responses.GetInfoCard.Response;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/bgl/[controller]")]
    [ApiController]
    public class ReceiptsController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Bgl.Receipts");

        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IReceiptsService _receiptsService { get; }
        private ReceiptsManager _receiptsManager { get; }

        public ReceiptsController(ReceiptsManager receiptsManager, IReceiptsService receiptsService)
        {
            _receiptsService = receiptsService;
            _receiptsManager = receiptsManager;
        }

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!_receiptsService.IsServiceEnabled)
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        private async Task<CreateReceiptResponse<T>?> CreateReceipt<T>(Func<T, ReceiptTemplateType, Task<byte[]?>> createFunction, T id, ReceiptTemplateType templateType, Func<T>? idGetter = null)
        {
            var receipt = createFunction != null ? await createFunction(id, templateType) : null;
            if (receipt != null && receipt.Length > 0)
            {
                await _receiptsManager.SaveChangesAsync();
                var result = new CreateReceiptResponse<T> {
                    Receipt = receipt,
                    ReceiptId = idGetter == null ? id : idGetter()
                };
                return result;
            }
            return null;
        }

        [HttpPost("CreateTransactionReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateTransactionReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid transactionId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateTransactionReceipt, transactionId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetTransactionReceipt")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> GetTransactionReceipt([FromBody] Guid transactionId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? Receipt = null;
            try
            {
                CheckServiceAvailability(true);
                Receipt = _receiptsService.GetTransactionReceipt(transactionId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(Receipt);
        }

        [HttpPost("CreateCscContractReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateCscContractReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateCscContractReceipt, articleId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCscContractUndoReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateCscContractUndoReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateCscContractUndoReceipt, articleId, templateType,
                    () => _receiptsService.GetUndoneCscContractVtsReceiptId(articleId) ?? articleId);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCscIssuingReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateCscIssuingReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateCscIssuingReceipt, articleId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCscReissuingReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateCscReissuingReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateCscReissuingReceipt, articleId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateContactlessCardExpirationExtensionReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateContactlessCardExpirationExtensionReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateContactlessCardExpirationExtensionReceipt, articleId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateProfileRenewalReceipt")]
        [Produces(typeof(CreateReceiptResponse<Guid>))]
        public async Task<IActionResult> CreateProfileRenewalReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<Guid>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateProfileRenewalReceipt, articleId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCanceledCscContractReceipt")]
        [Produces(typeof(CreateReceiptResponse<string>))]
        public async Task<IActionResult> CreateCanceledCscContractReceipt([FromQuery] ReceiptTemplateType templateType, [FromBody] string contractId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            CreateReceiptResponse<string>? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await CreateReceipt(_receiptsManager.CreateCanceledCscContractReceipt, contractId, templateType);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCardInfoReceipt")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> CreateCardInfoReceipt([FromQuery] ReceiptTemplateType templateType, [FromQuery] Guid agentId, [FromBody] CardInfo cardInfo)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var receipt = await _receiptsManager.CreateCardInfoReceipt(cardInfo, agentId, templateType);

                if (receipt != null && receipt.Length > 0)
                {
                    await _receiptsManager.SaveChangesAsync();
                    result = receipt;
                }

                if (result == null)
                {
                    return NoContent();
                }

            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCscContractReceiptImmediately")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> CreateCscContractReceiptImmediately([FromQuery] ReceiptTemplateType templateType, [FromQuery] Guid agentId, [FromQuery] bool inclidingTime, [FromQuery] decimal? contractPrice, [FromQuery] decimal? contractDiscount, [FromBody] ArticleInfoBasket articleInfo)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var receipt = await _receiptsManager.CreateCscContractReceiptImmediately(articleInfo, agentId, templateType, inclidingTime, contractPrice, contractDiscount);

                if (receipt != null && receipt.Length > 0)
                {
                    result = receipt;
                }

                if (result == null)
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCscContractUndoReceiptImmediately")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> CreateCscContractUndoReceiptImmediately([FromQuery] ReceiptTemplateType templateType, [FromQuery] Guid agentId, [FromQuery] bool inclidingTime, [FromQuery] decimal? contractPrice, [FromBody] CscContractArticleInfoWithPaymentMethods articleInfo)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var receipt = await _receiptsManager.CreateCscContractUndoReceiptImmediately(articleInfo, agentId, templateType, inclidingTime, contractPrice);

                if (receipt != null && receipt.Length > 0)
                {
                    result = receipt;
                }

                if (result == null)
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("CreateCanceledCscContractRefundFailedReceipt")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> CreateCanceledCscContractRefundFailedReceipt([FromQuery] ReceiptTemplateType templateType, [FromQuery] Guid agentId, [FromQuery] Guid articleId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                var receipt = await _receiptsManager.CreateCanceledCscContractRefundFailedReceipt(articleId, agentId, templateType);

                if (receipt != null && receipt.Length > 0)
                {
                    await _receiptsManager.SaveChangesAsync();
                    result = receipt;
                }

                if (result == null)
                {
                    return NoContent();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetEndShiftReceipt")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> GetEndShiftReceipt([FromBody] Guid agentShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? Receipt = null;
            try
            {
                Receipt = _receiptsService.GetEndShiftReceipt(agentShiftId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(Receipt);
        }

        [HttpPost("DeleteEndShiftReport")]
        [Produces(typeof(void))]
        public async Task<IActionResult> DeleteEndShiftReport([FromQuery] Guid shiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                var shift = _receiptsService.GetAgentShift(shiftId);
                if (shift?.Receipt != null)
                {
                    var serialized = JsonConvert.SerializeObject(shift.Receipt);
                    LogHelper.Info(_logger, $"Receipt {shift.ReportId} '{shiftId}' deleted: {serialized}");
                    shift.Receipt = null;
                    await _receiptsManager.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("GetReportOnAgentShift")]
        [Produces(typeof(List<DL.AgentShift>))]
        public async Task<IActionResult> GetReportOnAgentShift([FromQuery] Guid shiftId, [FromQuery] ReceiptTemplateType? templateType, [FromQuery] bool? withReceiptsContent, [FromBody] PaymentMethodReport[]? paymentMethods = null, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            DL.AgentShift? shift = null;
            try
            {
                if (paymentMethods != null && paymentMethods.Length == 0) paymentMethods = null;
                if (!withReceiptsContent.HasValue) withReceiptsContent = true;
                shift = _receiptsService.GetAgentShift(shiftId, includeSaleTransactions ?? false);
                if (shift != null)
                {
                    if (withReceiptsContent.Value)
                    {
                        if ((shift.Receipt?.Length ?? 0) == 0)
                        {
                            if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                            shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value, paymentMethods);
                            if (shift.EndDate.HasValue)
                            {
                                await _receiptsManager.SaveChangesAsync();
                            }
                        }
                        HelperClasses.JsonSerializer.RemoveSerializationCycle(ref shift);
                    }
                    else
                        shift.Receipt = null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(shift);
        }

        [HttpPost("GetReportOnAgentShifts")]
        [Produces(typeof(List<DL.AgentShift>))]
        public async Task<IActionResult> GetReportOnAgentShifts([FromQuery] int agentId, [FromQuery] ReceiptTemplateType? templateType, [FromBody] StartEnd startEnd, [FromQuery] bool? withReceiptsContent, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<DL.AgentShift>? shifts = null;
            try
            {
                if (!withReceiptsContent.HasValue) withReceiptsContent = true;
                var from = startEnd?.Start ?? new DateTime(1753, 1, 1);
                var to = startEnd?.End ?? new DateTime(9999, 1, 1);
                shifts = _receiptsService.GetAgentShifts(agentId, from, to, includeSaleTransactions ?? false);

                if (shifts != null)
                {
                    foreach (var shift in shifts.Where(p => (p.Receipt?.Length ?? 0) == 0))
                    {
                        if (withReceiptsContent.Value)
                        {
                            if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                            shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value);
                            if (shift.EndDate.HasValue)
                            {
                                await _receiptsManager.SaveChangesAsync();
                            }
                        }
                        else
                            shift.Receipt = null;
                    }
                }

                if (shifts != null) HelperClasses.JsonSerializer.RemoveSerializationCycle(ref shifts);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(shifts);
        }

        [HttpPost("GetReportOnAllAgentsShifts")]
        [Produces(typeof(List<DL.AgentShift>))]
        public async Task<IActionResult> GetReportOnAllAgentsShifts([FromQuery] ReceiptTemplateType? templateType, [FromBody] StartEnd startEnd, [FromQuery] bool? withReceiptsContent, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<DL.AgentShift>? shifts = null;
            try
            {
                if (!withReceiptsContent.HasValue) withReceiptsContent = true;
                var from = startEnd?.Start ?? new DateTime(1753, 1, 1);
                var to = startEnd?.End?.AddDays(1).AddSeconds(-1) ?? new DateTime(9999, 1, 1);
                LogHelper.Debug(_logger, $"From: {from:yyyy-MM-dd}, To: {to:yyyy-MM-dd}");
                shifts = _receiptsService.GetAllAgentsShifts(from, to, includeSaleTransactions ?? false);

                if (shifts != null)
                {
                    foreach (var shift in shifts.Where(p => (p.Receipt?.Length ?? 0) == 0))
                    {
                        if (withReceiptsContent.Value)
                        {
                            if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                            try
                            {
                                shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value);
                                if (shift.EndDate.HasValue)
                                {
                                    await _receiptsManager.SaveChangesAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Error(_logger, ex);
                            }
                        }
                        else
                            shift.Receipt = null;
                    }
                }

                if (shifts != null) HelperClasses.JsonSerializer.RemoveSerializationCycle(ref shifts);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(shifts);
        }

        [HttpPost("GetAgentEndShiftReceipt")]
        [Produces(typeof(GetAgentEndShiftReceiptsResponse))]
        public async Task<IActionResult> GetAgentEndShiftReceipt([FromQuery] Guid shiftId, [FromQuery] ReceiptTemplateType? templateType, [FromBody] PaymentMethodReport[]? paymentMethods = null, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);

            GetAgentEndShiftReceiptsResponse? result = null;
            try
            {
                if (paymentMethods != null && paymentMethods.Length == 0) paymentMethods = null;
                LogHelper.Debug(_logger, shiftId);
                var shift = _receiptsService.GetAgentShift(shiftId, includeSaleTransactions ?? false);
                if (shift != null)
                {
                    if ((shift.Receipt?.Length ?? 0) == 0)
                    {
                        if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                        try
                        {
                            shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value, paymentMethods);
                            if (shift.EndDate.HasValue)
                            {
                                await _receiptsManager.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(_logger, ex);
                        }
                    }
                    result = new GetAgentEndShiftReceiptsResponse {
                        ShiftId = shift.Id,
                        ShiftNumber = shift.ShiftNumber,
                        ServiceType = shift.ServiceType,
                        Start = shift.StartDate,
                        End = shift.EndDate,
                        Receipt = shift.Receipt
                    };
                }
                else
                    return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetAgentEndShiftReceipts")]
        [Produces(typeof(List<GetAgentEndShiftReceiptsResponse>))]
        public async Task<IActionResult> GetAgentEndShiftReceipts([FromQuery] int agentId, [FromQuery] ReceiptTemplateType? templateType, [FromBody] StartEnd startEnd, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<GetAgentEndShiftReceiptsResponse>? result = null;
            try
            {
                var from = startEnd?.Start ?? new DateTime(1753, 1, 1);
                var to = startEnd?.End ?? new DateTime(9999, 1, 1);
                var shifts = _receiptsService.GetAgentShifts(agentId, from, to, includeSaleTransactions ?? false);

                if (shifts != null)
                {
                    foreach (var shift in shifts.Where(p => (p.Receipt?.Length ?? 0) == 0))
                    {
                        if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                        shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value);
                        if (shift.EndDate.HasValue)
                        {
                            await _receiptsManager.SaveChangesAsync();
                        }
                    }

                    result = shifts.Where(p => p.Receipt != null).Select(p => new GetAgentEndShiftReceiptsResponse {
                        ShiftId = p.Id,
                        ShiftNumber = p.ShiftNumber,
                        ServiceType = p.ServiceType,
                        Start = p.StartDate,
                        End = p.EndDate,
                        Receipt = p.Receipt
                    })?.ToList();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetAllAgentsEndShiftReceipts")]
        [Produces(typeof(List<GetAgentEndShiftReceiptsResponse>))]
        public async Task<IActionResult> GetAllAgentsEndShiftReceipts([FromQuery] ReceiptTemplateType? templateType, [FromBody] StartEnd startEnd, [FromQuery] bool? includeSaleTransactions = null)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<GetAgentEndShiftReceiptsResponse>? result = null;
            try
            {
                var from = startEnd?.Start ?? new DateTime(1753, 1, 1);
                var to = startEnd?.End ?? new DateTime(9999, 1, 1);
                var shifts = _receiptsService.GetAllAgentsShifts(from, to, includeSaleTransactions ?? false);

                if (shifts != null)
                {
                    foreach (var shift in shifts.Where(p => (p.Receipt?.Length ?? 0) == 0))
                    {
                        if (!templateType.HasValue) templateType = ReceiptTemplateType.NotSpecified;
                        shift.Receipt = _receiptsManager.CreateCloseShiftReceipt(shift.Id, templateType.Value);
                        if (shift.EndDate.HasValue)
                        {
                            await _receiptsManager.SaveChangesAsync();
                        }
                    }

                    result = shifts.Where(p => p.Receipt != null).Select(p => new GetAgentEndShiftReceiptsResponse {
                        ShiftId = p.Id,
                        ShiftNumber = p.ShiftNumber,
                        ServiceType = p.ServiceType,
                        Start = p.StartDate,
                        End = p.EndDate,
                        Receipt = p.Receipt
                    })?.ToList();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetReceipts")]
        [Produces(typeof(List<ReceiptDateAndContent>))]
        public async Task<IActionResult> GetReceipts([FromBody] Guid agentShiftId, [FromQuery] int mask, [FromQuery] bool? withReceiptsContent, [FromQuery] long? startTicks = null, [FromQuery] long? endTicks = null, [FromQuery] int rowsPerPage = 50, [FromQuery] int pageNumber = 0)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<ReceiptDateAndContent>? result = null;
            try
            {
                if (!withReceiptsContent.HasValue) withReceiptsContent = true;
                StartEnd? startEnd = null;
                if (startTicks.HasValue || endTicks.HasValue)
                {
                    startEnd = new StartEnd();
                    if (startTicks.HasValue)
                    {
                        startEnd.Start = new DateTime(startTicks.Value);
                    }
                    if (endTicks.HasValue)
                    {
                        startEnd.End = new DateTime(endTicks.Value);
                    }
                }
                result = _receiptsService.GetReceipts(agentShiftId, (ReceiptTypeMask)mask, startEnd, rowsPerPage, pageNumber);
                if (!withReceiptsContent.Value)
                    result?.ForEach(p => p.Content = null);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result == null || !result.Any()) return NoContent();
            return Ok(result);
        }

        [HttpPost("GetReceiptsForMedia")]
        [Produces(typeof(List<ReceiptDateAndContent>))]
        public async Task<IActionResult> GetReceiptsForMedia([FromBody] string cardSerialNumber, [FromQuery] int mask, [FromQuery] int? shortCardModel, [FromQuery] bool? withReceiptsContent, [FromQuery] long? startTicks = null, [FromQuery] long? endTicks = null, [FromQuery] int rowsPerPage = 50, [FromQuery] int pageNumber = 0)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<ReceiptDateAndContent>? result = null;
            try
            {
                if (!withReceiptsContent.HasValue) withReceiptsContent = true;
                StartEnd? startEnd = null;
                if (startTicks.HasValue || endTicks.HasValue)
                {
                    startEnd = new StartEnd();
                    if (startTicks.HasValue)
                    {
                        startEnd.Start = new DateTime(startTicks.Value);
                    }
                    if (endTicks.HasValue)
                    {
                        startEnd.End = new DateTime(endTicks.Value);
                    }
                }
                result = _receiptsService.GetReceiptsForMedia(cardSerialNumber, shortCardModel ?? 0, (ReceiptTypeMask)mask, startEnd, rowsPerPage, pageNumber);
                if (!withReceiptsContent.Value)
                    result?.ForEach(p => p.Content = null);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result == null || !result.Any()) return NoContent();
            return Ok(result);
        }

        [HttpGet("GetReceiptByTypeAndId")]
        [Produces(typeof(byte[]))]
        public async Task<IActionResult> GetReceiptByTypeAndId([FromQuery] string id, [FromQuery] int mask)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                result = _receiptsService.GetReceiptByTypeAndId(id, (ReceiptTypeMask)mask);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result == null || !result.Any()) return NoContent();
            return Ok(result);
        }

        [HttpGet("GetAgentShiftPaymentMethods")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> GetAgentShiftPaymentMethods([FromQuery] Guid? agentShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);

            if (!agentShiftId.HasValue)
                return BadRequest();

            var result = _receiptsManager.GetAgentShiftPaymentMethods(agentShiftId.Value);
            return Ok(result);
        }
    }
}
