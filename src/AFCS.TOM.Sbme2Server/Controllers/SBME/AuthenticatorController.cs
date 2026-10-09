using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.SBME;
using AFCS.TOM.SbmeModels.Enums;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.SBME
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AuthenticatorController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Sbme1.Authenticator");
        
        private IConfiguration _configuration { get; set; }
        private IAuthenticatorService _authenticatorService { get; set; }
        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private bool _isServiceEnabled { get; }

        public AuthenticatorController(IConfiguration configuration, IAuthenticatorService authenticatorService)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var launchSettings = new LaunchSettings();
            config.GetSection("LaunchSettings").Bind(launchSettings);
            _isServiceEnabled = launchSettings.BglServicesEnabled;

            _configuration = configuration;
            _authenticatorService = authenticatorService;
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

        [HttpGet("GetAuthenticationByCredential")]
        [HelperClasses.QueryStringConstraint("userId", true)]
        [HelperClasses.QueryStringConstraint("password", true)]
        [Produces(typeof(TOM.SbmeModels.SBME.AuthenticationToken))]
        public async Task<IActionResult> GetAuthenticationByCredentialAsync([FromQuery] string userId, string password)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            // http://localhost:5000/api/authenticator/?userid=mil&password=pippo
            var token = await _authenticatorService.AutheticateByCredentialAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN /*TODO: check*/), userId, password);
            return Ok(token);
        }
        
        [HttpGet("GetAuthenticationByCardPinCode")]
        [HelperClasses.QueryStringConstraint("cardSerialNumber", true)]
        [Produces(typeof(TOM.SbmeModels.SBME.AuthenticationToken))]
        public async Task<IActionResult> GetAuthenticationByCardPinCodeAsync([FromQuery] string cardSerialNumber)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            // http://localhost:5000/api/authenticator/?cardSerialNumber=123
            var token = await _authenticatorService.AutheticateByCardPinCodeAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN /*TODO: check*/), cardSerialNumber);
            return Ok(token);
        }

    }
}