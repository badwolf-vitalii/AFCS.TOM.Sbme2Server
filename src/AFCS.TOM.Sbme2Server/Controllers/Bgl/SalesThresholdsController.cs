using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.SalesThresholds;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesThresholdsController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("SalesThresholdsService");

        private Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");

        private ISalesThresholdsService _salesThresholdsService { get; }

        public SalesThresholdsController(ISalesThresholdsService salesThresholdsService) => _salesThresholdsService = salesThresholdsService;

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!_salesThresholdsService.IsServiceEnabled)
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gives the maximum threshold level and the threshold's alarm level
        /// </summary>
        [HttpGet("ThresholdsInfo")]
        [Produces(typeof(ThresholdsInfo))]
        public async Task<IActionResult> GetSalesThresholds()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                var result = await _salesThresholdsService.GetSalesThresholds();
                return Ok(result);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "GetSalesThresholds() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("GetSalesThresholds() failed.");
            }
        }

        /// <summary>
        /// Gives the current residual value
        /// </summary>
        [HttpGet("GetCurrentResidual")]
        [Produces(typeof(decimal))]
        public async Task<IActionResult> GetCurrentResidual()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                var result = await _salesThresholdsService.GetCurrentResidual();
                return Ok(result);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "GetCurrentResidual() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("GetCurrentResidual() failed.");
            }
        }

        /// <summary>
        /// Gives the maximum threshold level, 
        /// the threshold's alarm level, 
        /// the residual and device activiity states
        /// </summary>
        [HttpGet("GetFullInfo")]
        [Produces(typeof(FullInfo))]
        public async Task<IActionResult> GetFullInfo()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                var result = await _salesThresholdsService.GetFullInfo();
                return Ok(result);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "GetFullInfo() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("GetFullInfo() failed.");
            }
        }

        /// <summary>
        /// Set up threshold's maximum level
        /// </summary>
        [HttpPost("SetSalesMaxThreshold")]
        [Produces(typeof(void))]
        public async Task<IActionResult> SetSalesMaxThreshold([FromBody] SetSalesMaxThresholdRequest parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (!parameters.Amount.HasValue) new MandatoryFieldNotSpecifiedException("Amount");
                await _salesThresholdsService.SetSalesMaxThreshold(parameters);
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "SetSalesMaxThreshold() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("SetSalesMaxThreshold() failed.");
            }
        }

        /// <summary>
        /// Set up threshold's allarm level
        /// </summary>
        [HttpPost("SetSalesAlarmThreshold")]
        [Produces(typeof(void))]
        public async Task<IActionResult> SetSalesAlarmThreshold([FromBody] SetSalesAlarmThresholdRequest parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (!parameters.Amount.HasValue) new MandatoryFieldNotSpecifiedException("Amount");
                await _salesThresholdsService.SetSalesAlarmThreshold(parameters);
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "SetSalesAlarmThreshold() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("SetSalesAlarmThreshold() failed.");
            }
        }

        /// <summary>
        /// Set up a precise velue of the current residual
        /// </summary>
        [HttpPost("SetExactCurrentResidual")]
        [Produces(typeof(void))]
        public async Task<IActionResult> SetExactCurrentResidual([FromBody] SetExactCurrentResidualRequest parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (!parameters.Amount.HasValue) new MandatoryFieldNotSpecifiedException("Amount");
                await _salesThresholdsService.SetExactCurrentResidual(parameters);
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "SetExactCurrentResidual() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("SetExactCurrentResidual() failed.");
            }
        }

        /// <summary>
        /// Resets the current residual setting it the maximum threshold value
        /// </summary>
        [HttpPost("ResetCurrentResidual")]
        [Produces(typeof(void))]
        public async Task<IActionResult> ResetCurrentResidual()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _salesThresholdsService.ResetCurrentResidual();
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "ResetCurrentResidual() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("ResetCurrentResidual() failed.");
            }
        }

        /// <summary>
        /// Block the device for administrative reasons
        /// </summary>
        [HttpPost("BlockSaleOperations")]
        [Produces(typeof(void))]
        public async Task<IActionResult> BlockSaleOperations([FromBody] BlockSaleOperationsRequest parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _salesThresholdsService.BlockSaleOperations(parameters);
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "BlockSaleOperations() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("BlockSaleOperations() failed.");
            }
        }

        /// <summary>
        /// Unlock the device that has been blocked for administrative reasons
        /// </summary>
        [HttpPost("UnlockSaleOperations")]
        [Produces(typeof(void))]
        public async Task<IActionResult> UnlockSaleOperations([FromBody] UnlockSaleOperationsRequest parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _salesThresholdsService.UnlockSaleOperations(parameters);
                await _salesThresholdsService.SaveChangesAsync();
                return Ok();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = "UnlockSaleOperations() failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest("UnlockSaleOperations() failed.");
            }
        }
    }
}
