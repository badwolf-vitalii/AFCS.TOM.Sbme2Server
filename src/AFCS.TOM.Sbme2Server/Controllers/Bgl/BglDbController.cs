using AFCS.TOM.CustomFTP;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.VtCashFlow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SqlServer.Dac.Model;
using Newtonsoft.Json;
using NLog;
using DL = AFCS.TOM.SbmeDataLayer;
using TM = AFCS.TOM.SbmeModels.BglDataLayer;

namespace AFCS.TOM.Sbme2Server.Controllers.Bgl
{
    [Route("api/[controller]")]
    [ApiController]
    public class BglDbController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("BglDbService");
        protected static BacpacBackgroundServiceNotReadyException BacpacBackgroundServiceNotReady { get; } = new BacpacBackgroundServiceNotReadyException();

        private Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");

        private IBglDbService _bglDbService { get; }
        private KeysManager _keysManager { get; }

        public BglDbController(IBglDbService bglDbService, KeysManager keysManager)
        {
            _bglDbService = bglDbService;
            _keysManager = keysManager;
        }

        private bool CheckServiceAvailability(bool throwable = false) => CheckServiceAvailability(out _, throwable);

        private bool CheckServiceAvailability(out byte errorCode, bool throwable = false)
        {
            errorCode = 0;
            if (!_bglDbService.IsServiceEnabled)
            {
                errorCode = 1;
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            if (!BacpacBackgroundService.IsReady)
            {
                errorCode = 2;
                if (throwable)
                    throw BacpacBackgroundServiceNotReady;
                else
                    return false;
            }
            return true;
        }

        [HttpPost("GenerateBacpac")]
        public async Task<IActionResult> GenerateBacpac()
        {
            try
            {
                _bglDbService.GenerateBacpac();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Bacpac generation failed.";
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex));
                return BadRequest($"Bacpac generation failed.");
            }
            return Ok();
        }

        [HttpGet("IsGeneratingBacpac")]
        public async Task<IActionResult> IsGeneratingBacpac() => Ok(BglDbService.IsBacpacGenerationActive);

        [HttpGet("GetLatestBacpacFileName")]
        public async Task<IActionResult> GetLatestBacpacFileName()
        {
            try
            {
                var latest = _bglDbService.GetLatestBacpacFileName();
                if (string.IsNullOrWhiteSpace(latest)) return NoContent();
                return Ok(latest);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting Bacpac file name failed.";
                LogHelper.Error(_logger, new GettingBacpacException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GettingBacpacException(ex));
                return BadRequest($"Getting Bacpac file name failed.");
            }
        }

        [HttpGet("DownloadLatestBacpac")]
        public async Task<IActionResult> DownloadLatestBacpac()
        {
            try
            {
                var bacpac = _bglDbService.DownloadLatestBacpac();
                if ((bacpac?.Length ?? 0) == 0) return NoContent();
                return Ok(bacpac);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Bacpac download failed.";
                LogHelper.Error(_logger, new GettingBacpacException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GettingBacpacException(ex));
                return BadRequest($"Bacpac download failed.");
            }
        }

        /// <summary>
        /// **[GET]** Gets version
        /// </summary>
        /// <returns>Major.Minor.Build.Revision</returns>
        [HttpGet("GetVersion")]
        [Produces(typeof(string))]
        public async Task<IActionResult> GetVersion()
        {
            try
            {
                var startup = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var kitVer = Path.Combine(startup!, "kit.ver");
                if (System.IO.File.Exists(kitVer))
                {
                    var ver = System.IO.File.ReadAllText(kitVer).Trim();
                    if (!string.IsNullOrWhiteSpace(ver))
                    {
                        return Ok(ver);
                    }
                }
            }
            catch
            {
            }
            return Ok(ControllersHelper.AssemblyVersion);
        }

        /// <summary>
        /// **[GET]** Check connection speed
        /// </summary>
        /// <param name="requestStartTimeTicks">Request start time ticks in UTC</param>
        /// <returns></returns>
        [HttpGet("CheckConnectionSpeed")]
        [Produces(typeof(string))]
        public async Task<IActionResult> CheckConnectionSpeed([FromQuery] long requestStartTimeTicks)
        {
            var received = DateTime.UtcNow;
            var requestStart = new DateTime(requestStartTimeTicks, DateTimeKind.Utc);
            return Ok(new StartEnd
            {
                Start = requestStart,
                End = received
            });
        }

        #region DB
        [HttpGet("GetDatabaseKeyId")]
        [Produces(typeof(byte))]
        public async Task<IActionResult> GetDatabaseKeyId()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            return Ok(KeysManager.LocalDbKeyIndex);
        }

        [HttpGet("GetDatabaseName")]
        [Produces(typeof(string))]
        public async Task<IActionResult> GetDatabaseName()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            return Ok(Configurations.BglDataLayerConfiguration.Instance?.Database);
        }

        [HttpPost("AddDbVersionChangeLog")]
        public async Task<IActionResult> AddDbVersionChangeLog(int dbVersion, string? changeLog)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            bool result;
            try
            {
                result = await _bglDbService.AddDbVersionChangeLog(dbVersion, changeLog);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("UpdateDbVersionDateTime")]
        public async Task<IActionResult> UpdateDbVersionDateTime(int dbVersion, DateTime? dateTime)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            bool result;
            try
            {
                result = await _bglDbService.UpdateDbVersionDateTime(dbVersion, dateTime);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetDatabaseInfo")]
        public async Task<IActionResult> GetDatabaseInfo(int dbVersion)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            DatabaseInfo? result = null;
            try
            {
                result = await _bglDbService.GetDatabaseInfo(dbVersion);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetLastDatabaseInfo")]
        public async Task<IActionResult> GetLastDatabaseInfo()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            DatabaseInfo? result = null;
            try
            {
                result = await _bglDbService.GetLastDatabaseInfo();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetNewDatabaseInfo")]
        public async Task<IActionResult> GetNewDatabaseInfo(int dbVersion)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            DatabaseInfo? result = null;
            try
            {
                result = await _bglDbService.GetNewDatabaseInfo(dbVersion);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("UpdateDatabase")]
        public async Task<IActionResult> UpdateDatabase(bool oneStepUpdate, int? dbVersion)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var result = false;
            try
            {
                result = await _bglDbService.UpdateDatabase(oneStepUpdate, dbVersion);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("QuickUpdateDb")]
        public async Task<IActionResult> QuickUpdateDb()
        {
            if (!CheckServiceAvailability(out var errorCode)) return Helper.I_am_a_teapot(this, errorCode == 2 ? BacpacBackgroundServiceNotReady : ServiceNotAvailable);
            var result = false;
            try
            {
                result = await _bglDbService.UpdateDatabase(false, null);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }
        #endregion

        #region Accounting Period
        [HttpPost("GetAccountingPeriod")]
        public async Task<IActionResult> GetAccountingPeriod()
        {
            if (!CheckServiceAvailability(out var errorCode)) return Helper.I_am_a_teapot(this, errorCode == 2 ? BacpacBackgroundServiceNotReady : ServiceNotAvailable);
            SbmeModels.OutputParameters.Sales.GetAccountingPeriodResponse? result;
            TemporarilyModels.GetAccountingPeriodResponse? data = null;
            var saved = false;
            try
            {
                data = await _bglDbService.GetAccountingPeriod();
                await _bglDbService.SaveChangesAsync();
                saved = true;
                var serialized = JsonConvert.SerializeObject(data);
                result = JsonConvert.DeserializeObject<SbmeModels.OutputParameters.Sales.GetAccountingPeriodResponse>(serialized);
            }
            catch (Exception ex)
            {
                if (data != null)
                {
                    if (saved)
                    {
                        LogHelper.Error(_logger, "Error during serialization");
                    }
                    else
                    {
                        LogHelper.Error(_logger, "Error during saving");
                    }
                }
                else
                {
                    LogHelper.Error(_logger, "Error during getting an accounting period");
                }
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }
        #endregion

        #region Agent Shift
        [HttpPost("GetAgentShiftNumber")]
        public async Task<IActionResult> GetAgentShiftNumber(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte res;
            try
            {
                res = await _bglDbService.GetAgentShiftNumber(parameters.AgentId, parameters.CompanyId, parameters.DeviceIdentifier, parameters.AccountingPeriodId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("GetAgentShift")]
        public async Task<IActionResult> GetAgentShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            TM.AgentShift? result;
            try
            {
                var data = await _bglDbService.GetAgentShift(parameters.AgentId, parameters.CompanyId, parameters.AccountingPeriodId);
                var serialized = JsonConvert.SerializeObject(data);
                result = JsonConvert.DeserializeObject<TM.AgentShift>(serialized);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("AddAgentShift")]
        public async Task<IActionResult> AddAgentShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            TM.AgentShift? result;
            try
            {
                var serialized = JsonConvert.SerializeObject(parameters.AgentShift);
                var insertion = JsonConvert.DeserializeObject<DL.AgentShift>(serialized);
                var data = await _bglDbService.AddAgentShift(insertion, parameters.AccountingPeriodId);
                await _bglDbService.SaveChangesAsync();
                serialized = JsonConvert.SerializeObject(data);
                result = JsonConvert.DeserializeObject<TM.AgentShift>(serialized);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("OpenAgentShift")]
        public async Task<IActionResult> OpenAgentShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                res = await _bglDbService.OpenAgentShift(parameters.AgentShiftId.Value, parameters.ServiceType, parameters.SellingRegion);
                await _bglDbService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Open shift failed.";
                LogHelper.Error(_logger, ex.Exception);
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("CloseAgentShift")]
        public async Task<IActionResult> CloseAgentShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                var receiptTemplate = parameters.ReceiptTemplate.HasValue ? parameters.ReceiptTemplate.Value : ReceiptTemplateType.NotSpecified;
                res = parameters.ClosingAgentId > 0
                    ? await _bglDbService.CloseAgentShift(parameters.AgentShiftId.Value, parameters.VtsShiftId ?? 0, receiptTemplate, parameters.ClosingAgentId, parameters.PaymentMethods)
                    : await _bglDbService.CloseAgentShift(parameters.AgentShiftId.Value, parameters.VtsShiftId ?? 0, receiptTemplate, parameters.PaymentMethods, parameters.ClosedAutomatically);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("LockAgentShift")]
        public async Task<IActionResult> LockAgentShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            bool res = false;
            try
            {
                res = await _bglDbService.LockAgentShift(parameters.AgentShiftId.Value);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("SetAgentVtsShift")]
        public async Task<IActionResult> SetAgentVtsShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                res = await _bglDbService.SetAgentVtsShift(parameters.AgentShiftId.Value, parameters.VtsShiftId ?? 0);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }
        #endregion

        #region Device Shift
        [HttpPost("GetDeviceShift")]
        public async Task<IActionResult> GetDeviceShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            TM.DeviceShift? result;
            try
            {
                var data = string.IsNullOrWhiteSpace(parameters.DeviceIdentifier)
                    ? parameters.DeviceShiftId.HasValue
                        ? await _bglDbService.GetDeviceShift(parameters.DeviceShiftId.Value)
                        : await _bglDbService.GetDeviceShiftOfAgent(parameters.AgentShiftId.Value)
                    : await _bglDbService.GetDeviceShift(parameters.DeviceIdentifier);
                var serialized = JsonConvert.SerializeObject(data);
                result = JsonConvert.DeserializeObject<TM.DeviceShift>(serialized);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("AddDeviceShift")]
        public async Task<IActionResult> AddDeviceShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            TM.DeviceShift? result;
            try
            {
                var serialized = JsonConvert.SerializeObject(parameters.DeviceShift);
                var insertion = JsonConvert.DeserializeObject<DL.DeviceShift>(serialized);
                var data = await _bglDbService.AddDeviceShift(insertion, parameters.AgentShiftId.Value);
                await _bglDbService.SaveChangesAsync();
                serialized = JsonConvert.SerializeObject(data);
                result = JsonConvert.DeserializeObject<TM.DeviceShift>(serialized);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("OpenDeviceShift")]
        public async Task<IActionResult> OpenDeviceShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                res = await _bglDbService.OpenDeviceShift(parameters.DeviceShiftId.Value);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("CloseDeviceShift")]
        public async Task<IActionResult> CloseDeviceShift(BglDbControllerParameters parameters)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                res = await _bglDbService.CloseDeviceShift(parameters.DeviceShiftId.Value, parameters.DeviceShiftClosingReason, parameters.DeviceShiftClosingReasonDescription);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("UpdateDeviceShift")]
        public async Task<IActionResult> UpdateDeviceShift(DeviceShift shift)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var res = false;
            try
            {
                res = await _bglDbService.UpdateDeviceShift(shift);
                if (res)
                {
                    await _bglDbService.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(res);
        }

        [HttpPost("GetMifareKeys")]
        public async Task<IActionResult> GetMifareKeys([FromBody] Guid deviceShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? keys = null;
            try
            {
                keys = await _keysManager.GetMifareKeys(deviceShiftId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(keys);
        }

        [HttpPost("GetMifareKeysV2")]
        public async Task<IActionResult> GetMifareKeysV2([FromBody] Guid deviceShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            string? keys = null;
            try
            {
                keys = await _keysManager.GetMifareKeysV2(deviceShiftId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(keys);
        }

        [HttpPost("GetSamKeys")]
        public async Task<IActionResult> GetSamKeys([FromBody] Guid deviceShiftId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? keys = null;
            try
            {
                keys = await _keysManager.GetSamKeys(deviceShiftId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(keys);
        }

        [HttpPost("RegisterCardAnomaly")]
        public async Task<IActionResult> RegisterCardAnomaly([FromBody] CardAnomaly anomaly)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var result = Guid.Empty;
            try
            {
                result = await _bglDbService.RegisterCardAnomaly(anomaly);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result.Equals(Guid.Empty)) return NoContent();
            return Ok(result);
        }

        [HttpPost("ResolveCardAnomaly")]
        public async Task<IActionResult> ResolveCardAnomaly([FromBody] CardAnomaly anomaly)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.ResolveCardAnomaly(anomaly);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }
        #endregion

        #region Device Status
        [HttpPost("SetDeviceStatus")]
        public async Task<IActionResult> SetDeviceStatus([FromBody] TM.SaleDevice saleDevice)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            if (saleDevice == null)
            {
                LogHelper.Warning(_logger, $"SaleDevice is null.");
                return NoContent();
            }

            try
            {
                var now = DateTime.Now;
                saleDevice.LastUpdate = now;
                if (saleDevice.DsdePeriferalDevices.Any())
                    foreach (var device in saleDevice.DsdePeriferalDevices.Where(p => p != null))
                        device.LastUpdate = now;
                var serialized = JsonConvert.SerializeObject(saleDevice);
                var insertion = JsonConvert.DeserializeObject<SaleDevice>(serialized);
                await _bglDbService.SetDeviceStatus(insertion);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok();
        }

        [HttpPost("CleanUpDeviceStatus")]
        public async Task<IActionResult> CleanUpDeviceStatus([FromBody] int saleDeviceId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);

            try
            {
                await _bglDbService.CleanUpDeviceStatus(saleDeviceId);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok();
        }
        #endregion

        [HttpPost("RegisterApplicationShutdown")]
        public async Task<IActionResult> RegisterApplicationShutdown([FromBody] ApplicationShutdown shutdown)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            var result = Guid.Empty;
            try
            {
                result = await _bglDbService.RegisterApplicationShutdown(shutdown);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result.Equals(Guid.Empty)) return NoContent();
            return Ok(result);
        }

        [HttpPost("GetApplicationSnapshot")]
        public async Task<IActionResult> GetApplicationSnapshot([FromQuery] string id, [FromQuery] bool downloadHugeFile = false)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            byte[]? result = null;
            try
            {
                result = _bglDbService.GetApplicationSnapshot(id);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("GetApplicationSnapshotFromPath")]
        public async Task<IActionResult> GetApplicationSnapshotFromPath([FromQuery] string path, [FromQuery] long offset, [FromQuery] int blockSize = 10240)
        {
            var result = CFTPBlock.FileDoesNotExist;
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
                path = Path.Combine(startupPath, "snap", path);
                if (!System.IO.File.Exists(path)) throw new FileNotFoundException(path);
                result = Manager.GetFileBlock(offset, path, blockSize);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("DownloadApplicationShutdownContext")]
        public async Task<IActionResult> DownloadApplicationShutdownContext([FromQuery] string id, [FromQuery] string fileName)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (string.IsNullOrWhiteSpace(fileName)) throw new Exception("File name not specified.");
                var data = _bglDbService.DownloadApplicationShutdownContext(id, fileName);
                if (data == null) return NoContent();
                System.IO.File.WriteAllBytes(fileName, data);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("GetSbmeProfilePriceMapping")]
        [Produces(typeof(SbmeProfilePriceMapping))]
        public async Task<IActionResult> GetSbmeProfilePriceMapping([FromQuery] int profileId, [FromQuery] short? issuingReasonCode)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            SbmeProfilePriceMapping? result = null;
            try
            {
                result = _bglDbService.GetSbmeProfilePriceMapping(profileId, issuingReasonCode ?? 0);
                if (result == null) return NoContent();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("RegisterNewDevice")]
        public async Task<IActionResult> RegisterNewDevice([FromQuery] string deviceIdentifier)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.RegisterNewDevice(deviceIdentifier);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("GetStaticVariables")]
        public async Task<IActionResult> GetStaticVariables([FromQuery] string deviceIdentifier)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            StaticVariablesList? result;
            try
            {
                result = await _bglDbService.GetStaticVariables(deviceIdentifier);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("SetStaticVariables")]
        public async Task<IActionResult> SetStaticVariables([FromBody] StaticVariablesList variables)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.SetStaticVariables(variables);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("DeclareAdminBlockUnlockManaged")]
        public async Task<IActionResult> DeclareAdminBlockUnlockManaged([FromQuery] string deviceIdentifier)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.DeclareAdminBlockUnlockManaged(deviceIdentifier);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("DeclareThresholdBlockUnlockManaged")]
        public async Task<IActionResult> DeclareThresholdBlockUnlockManaged([FromQuery] string deviceIdentifier)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.DeclareThresholdBlockUnlockManaged(deviceIdentifier);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("DeclareOfflineBlockUnlockManaged")]
        public async Task<IActionResult> DeclareOfflineBlockUnlockManaged([FromQuery] string deviceIdentifier)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.DeclareOfflineBlockUnlockManaged(deviceIdentifier);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("LoadPersonalization")]
        public async Task<IActionResult> LoadPersonalization([FromQuery] int? agentId, [FromQuery] short? companyId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (!agentId.HasValue)
                {
                    ExHelper.ThrowExceptionContainer(new Exception("Missing parameter: agentId"), "LoadPersonalization");
                }
                if (!companyId.HasValue)
                {
                    ExHelper.ThrowExceptionContainer(new Exception("Missing parameter: companyId"), "LoadPersonalization");
                }

                var personalization = _bglDbService.LoadPersonalization(agentId.Value, companyId.Value);
                return Ok(personalization);
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("SavePersonalization")]
        public async Task<IActionResult> SavePersonalization([FromBody] string personalization, [FromQuery] int? agentId, [FromQuery] short? companyId)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                if (!agentId.HasValue)
                {
                    ExHelper.ThrowExceptionContainer(new Exception("Missing parameter: agentId"), "LoadPersonalization");
                }
                if (!companyId.HasValue)
                {
                    ExHelper.ThrowExceptionContainer(new Exception("Missing parameter: companyId"), "LoadPersonalization");
                }

                _bglDbService.SavePersonalization(personalization, agentId.Value, companyId.Value);
                await _bglDbService.SaveChangesAsync();
            }
            catch (ExceptionContainer ex)
            {
                LogHelper.Error(_logger, new BacpacGenerationException(ex.Exception));
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

        [HttpPost("RegisterSellContract")]
        public async Task<IActionResult> RegisterSellContract([FromBody] VtSellContractInfo info)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.RegisterSellContract(info);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        private static SemaphoreSlim _registerSellCommitSemaphore { get; } = new SemaphoreSlim(1);

        [HttpPost("RegisterSellCommit")]
        public async Task<IActionResult> RegisterSellCommit([FromBody] VtSellCommitInfo info)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _registerSellCommitSemaphore.WaitAsync();
                await _bglDbService.RegisterSellCommit(info);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            finally
            {
                _registerSellCommitSemaphore.Release();
            }
            return Ok();
        }

        [HttpPost("RegisterSellCommitPamentType")]
        public async Task<IActionResult> RegisterSellCommitPamentType([FromBody] VtSellCommitPaymentTypeInfo info)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.RegisterSellCommitPamentType(info);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("RegisterSaleTransactionPamentType")]
        public async Task<IActionResult> RegisterSaleTransactionPamentType([FromBody] VtSellTransactionPaymentTypeInfo info)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.RegisterSaleTransactionPamentType(info);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("GetVtSellCommit")]
        public async Task<IActionResult> GetVtSellCommit([FromQuery] string groupUid)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            SbmeModels.VtCashFlow.VtSellCommitShortInfo? result = null;
            try
            {
                result = await _bglDbService.GetVtSellCommit(groupUid);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpGet("GetVtTransactionCashFlow")]
        public async Task<IActionResult> GetVtTransactionCashFlow([FromQuery] string transactionUid)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            List<VtTransactionCashFlow>? result = null;
            try
            {
                result = await _bglDbService.GetVtTransactionCashFlow(transactionUid);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result ?? new List<VtTransactionCashFlow>());
        }

        [HttpGet("GetMinAppVersione")]
        public async Task<IActionResult> GetMinAppVersione()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            string? result = null;
            try
            {
                result = await _bglDbService.GetMinAppVersione();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpGet("GetMinVtsVersione")]
        public async Task<IActionResult> GetMinVtsVersione()
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            string? result = null;
            try
            {
                result = await _bglDbService.GetMinVtsVersione();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("SetMinAppVersione")]
        public async Task<IActionResult> SetMinAppVersione([FromBody] string? version)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.SetMinAppVersione(version);
                await _bglDbService.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("SetMinVtsVersione")]
        public async Task<IActionResult> SetMinVtsVersione([FromBody] string? version)
        {
            if (!CheckServiceAvailability()) return Helper.I_am_a_teapot(this, ServiceNotAvailable);
            try
            {
                await _bglDbService.SetMinVtsVersione(version);
                await _bglDbService.SaveChangesAsync();
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
