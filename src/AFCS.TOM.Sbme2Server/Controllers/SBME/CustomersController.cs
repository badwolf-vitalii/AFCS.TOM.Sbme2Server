using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.SBME;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Customer;
using AFCS.TOM.SbmeModels.SBME;
using AFCS.TOM.SbmeModels.SBME.InputParameters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.SBME
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Sbme1.Customers");

        private IConfiguration _configuration { get; set; }
        private SFTPConfirmTSCRequestConfiguration _sftpConfiguration { get; set; }
        private ICustomerService _customerService { get; set; }
        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private bool _isServiceEnabled { get; }

        public CustomersController(IConfiguration configuration, ICustomerService customerService)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var launchSettings = new LaunchSettings();
            config.GetSection("LaunchSettings").Bind(launchSettings);
            _isServiceEnabled = launchSettings.Sbme1ServicesEnabled;

            _configuration = configuration;
            _sftpConfiguration = _configuration.GetSection("SFTPConfirmTSCRequestConfiguration").Get<SFTPConfirmTSCRequestConfiguration>();
            _customerService = customerService;
            if (!Directory.Exists(_sftpConfiguration.LocalRepoPath))
                try
                {
                    Directory.CreateDirectory(_sftpConfiguration.LocalRepoPath);
                }
                catch
                {
                    _sftpConfiguration.LocalRepoPath = string.Empty;
                }
            if (!Directory.Exists(_sftpConfiguration.OldFilesRepoPath))
                try
                {
                    Directory.CreateDirectory(_sftpConfiguration.OldFilesRepoPath);
                }
                catch
                {
                    _sftpConfiguration.OldFilesRepoPath = string.Empty;
                }
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
            return Ok(new CheckConnectionSpeedResult {
                Start = requestStart,
                End = received,
                NodeId = ControllersHelper.NodeId
            });
        }

        #region Cards Actions
        [HttpPost("ChecksForCardIssuing")]
        public async Task<IActionResult> ChecksForCardIssuing([FromBody] TscIssuingParameters parameters)
        {
            ResultForIssuingChecks checkResult = null;
            try
            {
                CheckServiceAvailability(true);
                parameters.ReqCheckDsdeGap = _configuration.GetValue<int>("REQCHECKDSDE_GAP");
                checkResult = await _customerService.ChecksForCardIssuing(new Dictionary<ConnectionString, string>
                {
                    { ConnectionString.SG_GESTOWN, ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN) },
                    { ConnectionString.SBME_GESTOWN, ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN) },
                    { ConnectionString.SBME_TARIFFOWN, ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN) }
                }, _sftpConfiguration, parameters);
                if (!(checkResult?.ResultOfChecks ?? false))
                {
                    LogHelper.Debug(_logger, "ChecksForCardIssuing() failed");
                    var req = JsonConvert.SerializeObject(parameters);
                    LogHelper.Debug(_logger, req);
                    if (checkResult != null)
                    {
                        var res = JsonConvert.SerializeObject(checkResult);
                        LogHelper.Debug(_logger, res);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new ChecksForCardIssuingException(ex));
                return BadRequest(ex);
            }
            HelperClasses.JsonSerializer.SerializeData(checkResult);
            return Ok(checkResult);
        }

        [HttpPost("CalculateProfileInfos")]
        public async Task<IActionResult> CalculateProfileInfos([FromBody] CalculateProfileInfosParameters parameters)
        {
            ProfileInfo[]? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await _customerService.CalculateProfileInfos(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(result, "ProfileInfo");
            return Ok(result);
        }

        [HttpPost("UpdateCardStateAsync")]
        public async Task<IActionResult> UpdateCardStateAsync([FromBody] UpdateCardStateParameters parameters)
        {
            var step = string.Empty;
            try
            {
                CheckServiceAvailability(true);
                if (parameters.ShortCardModel == 0 && parameters.CardManufacturedId != 0)
                {
                    try
                    {
                        parameters.ShortCardModel = await _customerService.GetShortCardModel(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), parameters.CardManufacturedId);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, $"GetShortCardModel() failed: {ex.Message}");
                        throw;
                    }
                }
                // Update the state in the SBMEGESTOWN only in case when it has been successfully updated in the SGGESTOWN
                step = "SG";
                var ok = await _customerService.UpdateCardState(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
                step = "SBME";
                ok = ok && await _customerService.UpdateCardState(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), parameters); // not using the &= operator for not performing the call if the first one has returned false
                return Ok(ok);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(step))
                {
                    LogHelper.Error(_logger, $"UpdateCardState() failed on {step}");
                }
                LogHelper.Error(_logger, new UpdateCardStateAsyncException(parameters, ex));
                if (ex?.InnerException != null)
                {
                    LogHelper.Error(_logger, ex.InnerException);
                }
                return BadRequest(ex?.InnerException?.Message ?? ex?.Message);
            }
        }

        [HttpPost("UpdateTscRequestState")]
        public async Task<IActionResult> UpdateTscRequestState([FromBody] UpdateTscRequestStateParameters parameters)
        {
            try
            {
                CheckServiceAvailability(true);
                // Update the state in the SBMEGESTOWN only in case when it has been successfully updated in the SGGESTOWN
                var ok = await _customerService.UpdateTscRequestState(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
                return Ok(ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateTscRequestStateAsyncException(parameters, ex));
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("UnlockHolder")]
        public async Task<IActionResult> UnlockHolder([FromBody] uint holderId)
        {
            try
            {
                CheckServiceAvailability(true);
                var ok = await _customerService.UnlockHolder(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), holderId);
                return Ok(ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateHolderStateAsyncException(holderId, Constants.HOLDER_STATUS_INSERT));
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("LockHolder")]
        public async Task<IActionResult> LockHolder([FromBody] uint holderId)
        {
            try
            {
                CheckServiceAvailability(true);
                var ok = await _customerService.LockHolder(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), holderId);
                return Ok(ok);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateHolderStateAsyncException(holderId, Constants.HOLDER_STATUS_INSERT));
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("GetCard")]
        [HelperClasses.QueryStringConstraint("shortCardModel", true)]
        [HelperClasses.QueryStringConstraint("chipId", true)]
        public async Task<IActionResult> GetCard([FromQuery] int shortCardModel, [FromQuery] uint chipId)
        {
            Card card = null;
            try
            {
                CheckServiceAvailability(true);
                card = await _customerService.GetCardAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), shortCardModel, chipId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCardsByHolderIDException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(card, "Card");
            return Ok(card);
        }

        [HttpGet("GetCardsByHolderID")]
        [HelperClasses.QueryStringConstraint("CardsHolderID", true)]
        public async Task<IActionResult> GetCardsByHolderID([FromQuery] uint cardsHolderID)
        {
            IList<Card> cards = null;
            try
            {
                CheckServiceAvailability(true);
                cards = await _customerService.GetCardsByHolderIDAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), cardsHolderID);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCardsByHolderIDException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(cards, "Card");
            return Ok(cards);
        }

        [HttpPost("CardIssuingConfirmAsync")]
        public async Task<IActionResult> CardIssuingConfirmAsync([FromBody] MediaDelivery mediaDelivery)
        {
            try
            {
                CheckServiceAvailability(true);
                await _customerService.CardIssuingConfirmAsync(mediaDelivery, _sftpConfiguration);
                return Ok(true);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CardIssuingConfirmException(ex));
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("ProfileRenewalConfirmAsync")]
        public async Task<IActionResult> ProfileRenewalConfirmAsync([FromBody] ProfileRenewal profileRenewal)
        {
            try
            {
                CheckServiceAvailability(true);
                await _customerService.ProfileRenewalConfirmAsync(profileRenewal, _sftpConfiguration);
                return Ok(true);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new ProfileRenewalConfirmException(ex));
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("GetContracts")]
        public async Task<IActionResult> GetContracts([FromBody] GetContractsParameters parameters)
        {
            List<GetContractsResult> checkResult = null;
            try
            {
                CheckServiceAvailability(true);
                checkResult = await _customerService.GetContracts(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetContractsException(parameters, ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(checkResult);
            return Ok(checkResult);
        }

        [HttpPost("ClearTDSDEContracts")]
        public async Task<IActionResult> ClearTDSDEContracts()
        {
            try
            {
                CheckServiceAvailability(true);
                await _customerService.ClearTDSDEContracts(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN));
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpPost("GetProfileExtension")]
        public async Task<IActionResult> GetProfileExtension([FromBody] GetProfileExtensionParameters parameters)
        {
            GetProfileExtensionDetails result = null;
            try
            {
                CheckServiceAvailability(true);
                if (parameters.ShortCardModel == 0 && parameters.CardManufacturedId != 0)
                {
                    try
                    {
                        parameters.ShortCardModel = await _customerService.GetShortCardModel(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), parameters.CardManufacturedId);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, $"GetShortCardModel() failed: {ex.Message}");
                        throw;
                    }
                }
                result = await _customerService.GetProfileExtension(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetProfileExtensionException(parameters, ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(result);
            return Ok(result);
        }

        [HttpPost("BlackListCard")]
        public async Task<IActionResult> BlackListCard([FromBody] StolenLoastParameters parameters)
        {
            var result = -1;
            try
            {
                CheckServiceAvailability(true);
                if (parameters.ShortCardModel == 0 && parameters.CardManufacturedId != 0)
                {
                    try
                    {
                        parameters.ShortCardModel = await _customerService.GetShortCardModel(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), parameters.CardManufacturedId);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, $"GetShortCardModel() failed: {ex.Message}");
                        throw;
                    }
                }
                result = await _customerService.BlackListCard(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new BlackListCardException(parameters, ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(result);
            return Ok(result);
        }

        [HttpGet("GetShortCardModel")]
        public async Task<IActionResult> GetShortCardModel([FromQuery] ulong cardManufacturedId)
        {
            var result = 0;
            try
            {
                CheckServiceAvailability(true);
                result = await _customerService.GetShortCardModel(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), cardManufacturedId);
            }
            catch (Exception ex)
            {
                ex = new ExceptionContainer(ex, 1, $"No ShortCardModel associated to the ManufacturedId {cardManufacturedId}");
                LogHelper.Error(_logger, new ChecksForCardIssuingException(ex));
                return BadRequest(ex);
            }
            return Ok(result);
        }

        #region Commented due to ADE-1116: ATM - DSDE/4 - Gestione tabelle SG/SBME per rinnovo tessere e profili
        [HttpPost("UpdateCardExpirationDates")]
        [Produces(typeof(UpdateCardExpirationDatesResult))]
        public async Task<IActionResult> UpdateCardExpirationDates([FromBody] UpdateCardExpirationDatesParameters parameters)
        {
            return Ok(UpdateCardExpirationDatesResult.OK);
            var result = UpdateCardExpirationDatesResult.Failed;
            var sbmeOk = false;
            var sgOk = false;
            if (parameters == null ||
                (parameters.CardEvd.HasValue &&
                parameters.Profile1Evd.HasValue &&
                parameters.Profile2Evd.HasValue &&
                parameters.Profile3Evd.HasValue))
            {
                var ex = new UpdateCardExpirationDatesException(parameters, new Exception("Nothing changed"));
                LogHelper.Error(_logger, ex);
                return BadRequest(ex);
            }
            try
            {
                CheckServiceAvailability(true);
                sgOk = await _customerService.UpdateCardExpirationDates(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCardExpirationDatesException(parameters, ex));
            }
            try
            {
                CheckServiceAvailability(true);
                sbmeOk = await _customerService.UpdateCardExpirationDates(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCardExpirationDatesException(parameters, ex));
            }
            if (sbmeOk && sgOk) result = UpdateCardExpirationDatesResult.OK;
            else if (sbmeOk && !sgOk) result = UpdateCardExpirationDatesResult.SbmeOnly;
            else if (!sbmeOk && sgOk) result = UpdateCardExpirationDatesResult.SgOnly;
            if (result == UpdateCardExpirationDatesResult.Failed) return BadRequest(new UpdateCardExpirationDatesException(parameters));
            HelperClasses.JsonSerializer.SerializeData(result);
            return Ok(result);
        }
        #endregion

        #endregion

        #region Customers Actions
        [HttpGet("GetCustomersByFilter")]
        [HelperClasses.QueryStringConstraint("customerPar", true)]
        public async Task<IActionResult> GetCustomersByFilter([FromQuery] string[]? customerPar)
        {
            // https://localhost:5001/api/customers/?customerPar=holderlastname&customerPar=zanni
            IList<Customer> customers = null;
            customerPar = customerPar ?? new string[0];
            try
            {
                customers = await _customerService.GetCustomersByFilterAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), customerPar);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCustomersByFilterException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(customers, "Customer"); 
            return Ok(customers);
        }

        [HttpGet("GetCustomerByHolderID")]
        [HelperClasses.QueryStringConstraint("holderID", true)]
        public async Task<IActionResult> GetCustomerByHolderID([FromQuery] uint holderId)
        {
            // https://localhost:5001/api/customers/?holderID=24
            Customer customers = null;
            try
            {
                customers = await _customerService.GetCustomerByHolderIDAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), holderId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCustomersByHolderIDException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(customers, "Customer"); 
            return Ok(customers);
        }

        [HttpGet("GetProfileRequestMapAsync")]
        [HelperClasses.QueryStringConstraint("profileReqId", true)]
        public async Task<IActionResult> GetProfileRequestMapAsync([FromQuery] int profileReqId)
        {
            IList<ProfileRequestMap> maps = null;
            try
            {
                maps = await _customerService.GetProfileRequestMapAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), profileReqId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetProfileRequestMapException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(maps, "ProfileRequestMap"); 
            return Ok(maps);
        }

        [HttpGet("GetProfileRequestMapByProfileIdAsync")]
        [HelperClasses.QueryStringConstraint("profile1", true)]
        [HelperClasses.QueryStringConstraint("profile2", true)]
        [HelperClasses.QueryStringConstraint("profile3", true)]
        public async Task<IActionResult> GetProfileRequestMapByProfileIdAsync([FromQuery] int profile1, [FromQuery] int profile2, [FromQuery] int profile3, [FromQuery] char gender)
        {
            IList<ProfileRequestMap> maps = null;
            try
            {
                maps = await _customerService.GetProfileRequestMapByProfileIdAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), profile1, profile2, profile3, gender);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetProfileRequestMapException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(maps, "ProfileRequestMap");
            return Ok(maps);
        }

        [HttpGet("GetHolderProfiles")]
        public async Task<IActionResult> GetHolderProfiles([FromQuery] short profile1, [FromQuery] short profile2, [FromQuery] short profile3, [FromQuery] string? providerId)
        {
            IList<HolderProfile>? profiles = null;
            providerId = providerId ?? string.Empty;
            try
            {
                byte? arg = null;
                if (byte.TryParse(providerId, out var value)) arg = value;
                profiles = await _customerService.GetHolderProfiles(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), profile1, profile2, profile3, arg);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(profiles, "HolderProfile");
            return Ok(profiles);
        }

        [HttpGet("GetAllHolderProfilesDescriptions")]
        public async Task<IActionResult> GetAllHolderProfilesDescriptions()
        {
            IList<HolderProfileDescription>? profiles = null;
            try
            {
                byte? arg = null;
                profiles = await _customerService.GetAllHolderProfilesDescriptions(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN));
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(profiles, "HolderProfileDescription");
            return Ok(profiles);
        }

        [HttpGet("GetProfileLayoutAsync")]
        [HelperClasses.QueryStringConstraint("profileId", true)]
        public async Task<IActionResult> GetProfileLayoutAsync([FromQuery] int profileId)
        {
            IList<CardLayout> layouts = null;
            try
            {
                layouts = await _customerService.GetProfileLayoutAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_CONFOWN), profileId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetProfileLayoutException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(layouts, "CardLayout");
            return Ok(layouts);
        }

        [HttpGet("GetProfileRequestCode")]
        public async Task<IActionResult> GetProfileRequestCode([FromQuery] string? gender, [FromQuery] string? providerId)
        {
            providerId = providerId ?? string.Empty;
            IList<ProfileRequestCode>? crp = null;
            try
            {
                char? arg1 = (gender?.Length ?? 0) > 0 ? gender[0] : 'X';
                var arg2 = byte.MaxValue;
                if (byte.TryParse(providerId, out var value)) arg2 = value;
                crp = await _customerService.GetProfileRequestCodeAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_TARIFFOWN), ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_CONFOWN), arg1.Value, arg2);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetProfileRequestCodeIDException(ex));
                return BadRequest(ex.Message);
            }
            HelperClasses.JsonSerializer.SerializeData(crp, "ProfileRequestCode"); 
            return Ok(crp);
        }

        // POST: api/Customers
        [HttpPost]
        public async Task<IActionResult> CreateNewCustomer([FromBody] Customer customer)
        {
            var holderId = (uint)0;
            try
            {
                holderId = await _customerService.CreateCustomerAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), customer);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(holderId);
        }

        // PUT: api/Customers/5
        [HttpPut("{holderId}")]
        public async Task<IActionResult> UpdateCustomer(uint holderId, [FromBody] List<PredicateFilter> filter)
        {
            try
            {
                await _customerService.UpdateCustomerAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), holderId, filter);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("GetCustomerIdByCardSerialNumber")]
        public async Task<IActionResult> GetCustomerIdByCardSerialNumber([FromQuery] string? sn, [FromQuery] int? shortCardModel, [FromQuery] string? deviceId)
        {
            sn = sn ?? string.Empty;
            shortCardModel = shortCardModel ?? 0;
            deviceId = deviceId ?? string.Empty;
            var holderId = (uint)0;
            try
            {
                holderId = await _customerService.GetCustomerIdByCardSerialNumber(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), sn, shortCardModel.Value, deviceId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(holderId);
        }

        // DELETE: api/ApiWithActions/5
        [HttpPost("DeleteCustomer")]
        //[HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer([FromBody] uint id)
        {
            var ok = false;
            try
            {
                ok = await _customerService.DeleteCustomerAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), id);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(ok);
        }

        [HttpPost("ForgetTsc")]
        public async Task<IActionResult> ForgetTsc([FromQuery] string? tscSerial, [FromQuery] bool? isHex = false)
        {
            var ok = false;
            if (!string.IsNullOrWhiteSpace(tscSerial))
                try
                {
                    var serial = isHex ?? false
                        ? long.Parse(tscSerial, System.Globalization.NumberStyles.HexNumber)
                        : long.Parse(tscSerial);
                    await DBOracleManager.ForgetTscDocument(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN), ControllersHelper.GetConnectionString(_configuration, ConnectionString.SG_GESTOWN), _configuration.GetConnectionString("SGGESTOWN_UNSAFE"), serial);
                    ok = true;
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, new UpdateCustomerException(ex));
                    return BadRequest(ex.Message);
                }
            return Ok(ok);
        }

        [HttpPost("AddParkingContract")]
        public async Task<IActionResult> AddParkingContract([FromQuery] int shortCardModel, [FromQuery] long tscSerial)
        {
            var result = "OK";
            try
            {
                result = await DBOracleManager.AddParkingContract(
                    ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN),
                    _configuration.GetConnectionString("SGGESTOWN_UNSAFE"),
                    shortCardModel, tscSerial);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        [HttpPost("RemoveParkingContract")]
        public async Task<IActionResult> RemoveParkingContract([FromQuery] int shortCardModel, [FromQuery] long tscSerial)
        {
            var result = "OK";
            try
            {
                result = await DBOracleManager.RemoveParkingContract(
                    ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN),
                    _configuration.GetConnectionString("SGGESTOWN_UNSAFE"),
                    shortCardModel, tscSerial);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }
        #endregion
    }
}
