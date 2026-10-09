using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/bgl/[controller]")]
    [ApiController]
    public class ReceiptsDebugController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Bgl.Receipts & ReceiptsDebugController");

        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IReceiptsService _receiptsService { get; }
        private ReceiptsManager _receiptsManager { get; }
        private bool _receiptsDebugControllerEnabled { get; }

        public ReceiptsDebugController(ReceiptsManager receiptsManager, IReceiptsService receiptsService, IConfiguration configuration)
        {
            var debuggingConfiguration = new DebuggingConfiguration();
            configuration.GetSection("DebuggingConfiguration").Bind(debuggingConfiguration);
            _receiptsDebugControllerEnabled = debuggingConfiguration.ReceiptsDebugControllerEnabled ?? false;

            _receiptsService = receiptsService;
            _receiptsManager = receiptsManager;
        }

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!(_receiptsDebugControllerEnabled && _receiptsService.IsServiceEnabled))
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        [HttpGet("TestAgentShiftPaymentMethods")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestAgentShiftPaymentMethods([FromQuery] Guid? agentShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!agentShiftId.HasValue)
            //    agentShiftId = _context.AgentShifts.OrderByDescending(p => p.StartDate).FirstOrDefault()?.Id;

            if (!agentShiftId.HasValue)
                return BadRequest();

            var result = _receiptsManager.GetAgentShiftPaymentMethods(agentShiftId.Value);
            return Ok(result);
        }

        [HttpGet("TestCreateCloseShiftReceipt")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCreateCloseShiftReceipt([FromQuery] Guid? agentShiftId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!agentShiftId.HasValue)
            //    agentShiftId = _context.AgentShifts.OrderByDescending(p => p.StartDate).FirstOrDefault()?.Id;

            //agentShiftId = new Guid("927c0217-9e2e-4f14-9859-8879b9cb807b");
            //templateType = ReceiptTemplateType.CP260C;

            if (!agentShiftId.HasValue)
                return BadRequest();

            var result = _receiptsManager.CreateCloseShiftReceipt(agentShiftId.Value, templateType);
            return Ok(result);
        }

        [HttpGet("TestCreateTransactionReceipt")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCreateTransactionReceipt([FromQuery] Guid? transactionId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!transactionId.HasValue)
            //    transactionId = _context.SaleTransactions.OrderByDescending(p => p.TransactionTime).FirstOrDefault()?.Id;

            //transactionId = new Guid("9d285a08-4e05-4b3c-8758-4822c0a97f62");
            templateType = ReceiptTemplateType.CP260C;

            if (!transactionId.HasValue)
                return BadRequest();

            var result = _receiptsManager.CreateTransactionReceipt(transactionId.Value, templateType);
            await _receiptsService.SaveChangesAsync();
            return Ok(result);
        }

        [HttpGet("TestCreateContactlessCardExpirationExtensionReceipt")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCreateContactlessCardExpirationExtensionReceipt([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!transactionId.HasValue)
            //    transactionId = _context.SaleTransactions.OrderByDescending(p => p.TransactionTime).FirstOrDefault()?.Id;

            //transactionId = new Guid("9d285a08-4e05-4b3c-8758-4822c0a97f62");
            templateType = ReceiptTemplateType.CP260C;

            if (!articleId.HasValue)
            {
                articleId = new Guid("30f53a9b-3fcb-4bbd-9e85-68388b3d3eb3");
            }

            var result = _receiptsManager.CreateContactlessCardExpirationExtensionReceipt(articleId.Value, templateType);
            await _receiptsService.SaveChangesAsync();
            return Ok(result);
        }

        [HttpGet("TestCscContract")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCscContract([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!articleId.HasValue)
            //{
            //    var undone = _context.CscContractArticleInfos
            //        .Include("Article")
            //        .Include("Article.SaleTransaction")
            //        .OrderByDescending(p => p.Article.Position)
            //        .OrderByDescending(p => p.Article.SaleTransaction.TransactionTime);
            //    articleId = undone.FirstOrDefault()?.ArticleId;
            //}

            if (!articleId.HasValue)
                return BadRequest();

            articleId = new Guid("fd5c0e21-59cd-4758-bc7a-b97795176171");
            templateType = ReceiptTemplateType.CP260C;

            var result = await _receiptsManager.CreateCscContractReceipt(articleId.Value, templateType);
            return Ok(result);
        }

        [HttpGet("TestCscContractUndo")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCscContractUndo([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!articleId.HasValue)
            //{
            //    var undone = _context.CscContractRefundArticleInfos
            //        .Include("Article")
            //        .Include("Article.SaleTransaction")
            //        .OrderByDescending(p => p.Article.Position)
            //        .OrderByDescending(p => p.Article.SaleTransaction.TransactionTime);
            //    articleId = undone.FirstOrDefault()?.ArticleId;
            //}

            articleId = new Guid("b87fbe5c-a1a9-41fe-9f89-03190cd4db3c");
            templateType = ReceiptTemplateType.CP260C;

            if (!articleId.HasValue)
                return BadRequest();

            var result = await _receiptsManager.CreateCscContractUndoReceipt(articleId.Value, templateType);
            return Ok(result);
        }

        [HttpGet("TestCscIssuing")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCscIssuing([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!articleId.HasValue)
            //{
            //    var undone = _context.ContactlessCardArticleInfos
            //        .Include("Article")
            //        .Include("Article.SaleTransaction")
            //        .Where(p => !string.IsNullOrEmpty(p.ReissuingCscSerialLo))
            //        .OrderByDescending(p => p.Article.Position)
            //        .OrderByDescending(p => p.Article.SaleTransaction.TransactionTime);
            //    articleId = undone.FirstOrDefault()?.ArticleId;
            //}

            //articleId = new Guid("17ea6a93-4a17-4829-9d60-1dbf05dda5bb");
            templateType = ReceiptTemplateType.CP260C;

            if (!articleId.HasValue)
                return BadRequest();

            var result = await _receiptsManager.CreateCscIssuingReceipt(articleId.Value, templateType);
            await _receiptsService.SaveChangesAsync();
            return Ok(result);
        }

        [HttpGet("TestCscReissuing")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCscReissuing([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!articleId.HasValue)
            //{
            //    var undone = _context.ContactlessCardArticleInfos
            //        .Include("Article")
            //        .Include("Article.SaleTransaction")
            //        .Where(p => !string.IsNullOrEmpty(p.ReissuingCscSerialLo))
            //        .OrderByDescending(p => p.Article.Position)
            //        .OrderByDescending(p => p.Article.SaleTransaction.TransactionTime);
            //    articleId = undone.FirstOrDefault()?.ArticleId;
            //}

            //articleId = new Guid("17ea6a93-4a17-4829-9d60-1dbf05dda5bb");
            templateType = ReceiptTemplateType.CP260C;

            if (!articleId.HasValue)
                return BadRequest();

            var result = await _receiptsManager.CreateCscReissuingReceipt(articleId.Value, templateType);
            await _receiptsService.SaveChangesAsync();
            return Ok(result);
        }

        [HttpGet("TestProfileRenewal")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestProfileRenewal([FromQuery] Guid? articleId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            //if (!articleId.HasValue)
            //{
            //    var undone = _context.ProfileRenewalArticleInfos
            //        .Include("Article")
            //        .Include("Article.SaleTransaction")
            //        .OrderByDescending(p => p.Article.Position)
            //        .OrderByDescending(p => p.Article.SaleTransaction.TransactionTime);
            //    articleId = undone.FirstOrDefault()?.ArticleId;
            //}

            if (!articleId.HasValue)
                return BadRequest();

            var result = await _receiptsManager.CreateProfileRenewalReceipt(articleId.Value, templateType);
            await _receiptsService.SaveChangesAsync();
            return Ok(result);
        }

        [HttpGet("TestCanceledCscContractReceipt")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> TestCanceledCscContractReceipt([FromQuery] string? contractId, [FromQuery] ReceiptTemplateType templateType)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);

            //if (string.IsNullOrWhiteSpace(contractId))
            //    contractId = _context.CanceledCscContracts.OrderByDescending(p => p.CancellationDateTime).FirstOrDefault(p => p.IsMaster == 1)?.Id;

            if (string.IsNullOrWhiteSpace(contractId)) return BadRequest();

            var result = await _receiptsManager.CreateCanceledCscContractReceipt(contractId, templateType);
            return Ok(result);
        }
    }
}
