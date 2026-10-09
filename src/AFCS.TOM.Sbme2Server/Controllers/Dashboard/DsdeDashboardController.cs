using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Dashboard;
using AFCS.TOM.SbmeModels.DsdeDashboard;
using AFCS.TOM.SbmeModels.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NLog;
using DL = AFCS.TOM.SbmeDataLayer;
using TM = AFCS.TOM.SbmeModels.BglDataLayer;

namespace AFCS.TOM.Sbme2Server.Controllers.Dashboard
{
    [EnableCors("CORSPolicy")]
    [Route("api/[controller]")]
    [ApiController]
    public class DsdeDashboardController : Controller
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("DsdeDashboardService");

        private Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IConfiguration _configuration { get; }
        private IHubContext<MessageHub, IMessageHubClient> _messageHub { get; }
        private BglDataLayerConfiguration _bglDataLayerConfiguration { get; }
        private DL.DataLayerContext _context { get; }

        private IDsdeDashboardService _dsdeDashboardService { get; }
        private bool _isServiceEnabled { get; }

        private IList<DeviceListRecord>? _deviceList { get; set; }
        private IList<DeviceListRecord_old>? _oldDeviceList { get; set; }

        public DsdeDashboardController(IConfiguration configuration, IDsdeDashboardService dsdeDashboardService, IHubContext<MessageHub, IMessageHubClient> messageHub)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var launchSettings = new LaunchSettings();
            config.GetSection("LaunchSettings").Bind(launchSettings);
            _isServiceEnabled = launchSettings.DsdeDashboardEnabled ?? false;
            _messageHub = messageHub;

            _configuration = configuration;
            _bglDataLayerConfiguration = BglDataLayerConfiguration.Instance ?? _configuration.GetSection("BglDataLayerConfiguration").Get<BglDataLayerConfiguration>();
            _context = new DL.DataLayerContext(_bglDataLayerConfiguration.ConnectionString);

            _dsdeDashboardService = dsdeDashboardService;
        }

        private bool CheckServiceAvailability(bool throwable = false)
        {
            if (!_isServiceEnabled)
            {
                if (throwable)
                    throw ServiceNotAvailable;
                else
                    return false;
            }
            return true;
        }

        [HttpGet("GetDeviceList")]
        public async Task<IActionResult> GetDeviceList()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            if ((_deviceList?.Count() ?? 0) > 0) return Ok(_deviceList);

            try
            {
                _deviceList = await _dsdeDashboardService.GetDeviceList(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_TARIFFOWN_CONFOWN));
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if ((_deviceList?.Count() ?? 0) == 0) return NoContent();
            return Ok(_deviceList);
        }


        [HttpPost("SetDeviceStatus")]
        public async Task<IActionResult> SetDeviceStatus(long deviceId, bool status)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);

            try
            {
                await _messageHub.Clients.All.SetDeviceStatus(deviceId, status);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }


        [HttpGet("GetSaleDeviceStatus")]
        public async Task<IActionResult> GetSaleDeviceStatus([FromQuery] int saleDeviceId)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            TM.SaleDevice? result = null;
            try
            {
                var dlResult = await _dsdeDashboardService.GetSaleDeviceStatus(_context, saleDeviceId);
                var serialized = JsonConvert.SerializeObject(dlResult);
                result = JsonConvert.DeserializeObject<TM.SaleDevice>(serialized);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (result == null) return NoContent();
            return Ok(result);
        }
    }
}
