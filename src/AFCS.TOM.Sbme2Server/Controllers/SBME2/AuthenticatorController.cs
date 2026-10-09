using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.SBME2;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.SBME2
{
    [ApiController]
    [Route("api/v2/[controller]")]
    public class AuthenticatorController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Sbme2.Authenticator");

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
            _isServiceEnabled = launchSettings.Sbme2ServicesEnabled;

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

        [HttpPost("UserLogin")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UserLogin()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
        
        [HttpPost("UserRegistration")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UserRegistration()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
        
        [HttpPost("UserChangePassword")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UserChangePassword()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
    }
}
