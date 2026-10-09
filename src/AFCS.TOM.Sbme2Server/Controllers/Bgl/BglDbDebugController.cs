using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/[controller]")]
    [ApiController]
    public class BglDbDebugController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("BglDbDebug");
        
        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IBglDbDebugService _bglDbDebugService { get; }

        public BglDbDebugController(IBglDbDebugService bglDbService) => _bglDbDebugService = bglDbService;

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!_bglDbDebugService.IsServiceEnabled)
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        [HttpPost("DeleteAccountingPeriods")]
        public async Task<IActionResult> DeleteAccountingPeriods()
        {
            try
            {
                CheckServiceAvailability(true);
                await _bglDbDebugService.DeleteAccountingPeriods();
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

        [HttpPost("DeleteSaleTransactions")]
        public async Task<IActionResult> DeleteSaleTransactions()
        {
            try
            {
                CheckServiceAvailability(true);
                await _bglDbDebugService.DeleteSaleTransactions();
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
    }
}
