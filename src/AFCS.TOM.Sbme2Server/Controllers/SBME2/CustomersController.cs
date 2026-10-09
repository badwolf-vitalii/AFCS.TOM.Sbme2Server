using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.SBME2;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.SBME2;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.SBME2
{
    [ApiController]
    [Route("api/v2/[controller]")]
    public class CustomersController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Sbme2.Customers");

        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IConfiguration _configuration { get; set; }
        private ICustomerService _customerService { get; set; }
        private bool _isServiceEnabled { get; }
        private bool _isCustomerIdInheritedFromSbme1 { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="customerService"></param>
        public CustomersController(IConfiguration configuration, ICustomerService customerService)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                var launchSettings = new LaunchSettings();
                config.GetSection("LaunchSettings").Bind(launchSettings);
                _isServiceEnabled = launchSettings.Sbme2ServicesEnabled;
                _isCustomerIdInheritedFromSbme1 = launchSettings.CustomerIdInheritedFromSbme1;
            }
            catch
            {
                LogHelper.Error(_logger, "CustomersController(), reading LaunchSettings failed");
            }
            _configuration = configuration;
            _customerService = customerService;

            if (_configuration == null)
                LogHelper.Error(_logger, "CustomersController(), configuration is null");
            if (_customerService == null)
                LogHelper.Error(_logger, "CustomersController(), customerService is null");
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

        /// <summary>
        /// CreateNewCustomer
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("CreateCustomer")]
        [Produces(typeof(uint))]
        public async Task<IActionResult> CreateCustomer([FromBody] RequestBase<Customer> customer)
        {
            var holderId = (uint)0;
            try
            {
                CheckServiceAvailability(true);

                if (customer.AuthenticationToken?.Equals("I Love Chewbacca") ?? false)
                {
                    try
                    {
                        var id = await DBOracleManager.GetNextCustomerIdAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN));
                        return Ok(id);
                    }
                    catch (ExceptionContainer ex)
                    {
                        if (string.IsNullOrWhiteSpace(ex.Description))
                            ex.Description = $"GetNextCustomerIdAsync failed.";
                        LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                        var serializedEx = JsonConvert.SerializeObject(ex);
                        return Helper.I_am_a_teapot(this, serializedEx);
                    }
                }

                if (_isCustomerIdInheritedFromSbme1)
                {
                    var nextCustomerId = await DBOracleManager.GetNextCustomerIdAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN));
                    customer.Body.HolderId = nextCustomerId;
                }
                else
                    customer.Body.HolderId = 0;
                holderId = await _customerService.CreateCustomer(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), customer);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Adding a customer failed: {customer?.Body?.HolderId ?? 0}.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateCustomerException(ex));
                return BadRequest($"Adding HolderId Failed: {customer?.Body?.HolderId ?? 0}. {ex.Message}");
            }
            return Ok(holderId);
        }

        /// <summary>
        /// GetNextCustomerId
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("GetNextCustomerId")]
        [Produces(typeof(string))]
        public async Task<IActionResult> GetNextCustomerId([FromQuery] string? password)
        {
            CheckServiceAvailability(true);

            if (password?.Equals("ILoveChewbacca") ?? false)
            {
                try
                {
                    var id = await DBOracleManager.GetNextCustomerIdAsync(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME_GESTOWN));
                    return Ok(id.ToString());
                }
                catch (ExceptionContainer ex)
                {
                    if (string.IsNullOrWhiteSpace(ex.Description))
                        ex.Description = $"GetNextCustomerIdAsync failed.";
                    LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                    var serializedEx = JsonConvert.SerializeObject(ex);
                    return Helper.I_am_a_teapot(this, serializedEx);
                }
            }

            return Ok("Say password");
        }

        /// <summary>
        /// TBD....
        /// </summary>
        /// <returns></returns>
        [HttpPost("UpdateCustomer")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateCustomer([FromQuery] uint holderId, [FromBody] RequestBase<IList<SearchFilter>> filters)
        {
            var ok = false;
            try
            {
                CheckServiceAvailability(true);
                ok = await _customerService.UpdateCustomer(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), holderId, filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Updating a customer failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(ok);
        }

        /// <summary>
        /// CreateCustomerContacts
        /// </summary>
        /// <param name="customer"></param>
        /// <returns></returns>
        [HttpPost("CreateCustomerContacts")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> CreateCustomerContacts()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }

        /// <summary>
        /// ModifyCustomerContact
        /// </summary>
        /// <returns></returns>
        [HttpPost("UpdateCustomerContact")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateCustomerContact()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
        
        /// <summary>
        /// GetCustomersByFilters
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("GetCustomersByFilter")]
        [Produces(typeof(IList<Customer>))]
        public async Task<IActionResult> GetCustomersByFilter([FromBody] RequestBase<IList<SearchFilter>> filters)
        {
            IList<Customer> customers = null;
            try
            {
                if (filters?.Body == null || !filters.Body.Any())
                {
                    var serialized = JsonConvert.SerializeObject(filters);
                    ExHelper.ThrowExceptionContainer(new FilterNotSpecifiedException(), 1036, "GetCustomersByFilter() - filter not specified", serialized);
                }
                CheckServiceAvailability(true);
                customers = await _customerService.GetCustomersByFilter(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting customers by filter failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCustomersByFilterException(ex));
                return BadRequest(ex.Message);
            }
            if (customers == null) return NoContent();
            Helper.JsonSerializer.SerializeData(customers, "Customer");
            return Ok(customers);
        }

        [HttpGet("GetCustomerByHolderID")]
        [HelperClasses.QueryStringConstraint("holderID", true)]
        public async Task<IActionResult> GetCustomerByHolderID([FromQuery] uint holderId, [FromQuery] bool? withPhoto)
        {
            // https://localhost:5001/api/customers/?holderID=24
            Customer customers = null;
            try
            {
                CheckServiceAvailability(true);
                customers = await _customerService.GetCustomerByHolderID(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), holderId, withPhoto.HasValue ? withPhoto.Value : true);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting a customer by HolderId failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCustomersByHolderIDException(ex));
                return BadRequest(ex.Message);
            }
            if (customers == null) return NoContent();
            HelperClasses.JsonSerializer.SerializeData(customers, "Customer");
            return Ok(customers);
        }

        [HttpPost("GetCustomerIdByCardSerialNumber")]
        public async Task<IActionResult> GetCustomerIdByCardSerialNumber([FromQuery] string sn, [FromQuery] int shortCardModel, [FromBody] string deviceId)
        {
            var holderId = (uint)0;
            try
            {
                CheckServiceAvailability(true);
                holderId = await _customerService.GetCustomerIdByCardSerialNumber(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), sn, shortCardModel, deviceId);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting customer ID by its card SN failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateCustomerException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(holderId);
        }

        /// <summary>
        /// GetRelatedCustomers
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("GetRelatedCustomers")]
        [Produces(typeof(IList<RelatedCustomer>))]
        public async Task<IActionResult> GetRelatedCustomers([FromBody] RequestBase<HolderIdRequest> filters)
        {
            IList<RelatedCustomer> customers = null;
            try
            {
                CheckServiceAvailability(true);
                customers = await _customerService.GetRelatedCustomers(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting related customers failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetCustomersByFilterException(ex));
                return BadRequest(ex.Message);
            }
            if (customers == null) return NoContent();
            Helper.JsonSerializer.SerializeData(customers, "Customer");
            return Ok(customers);
        }
        
        /// <summary>
        /// TBD....
        /// </summary>
        /// <returns></returns>
        [HttpPost("CreateCustomerRelation")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> CreateCustomerRelation()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
        
        /// <summary>
        /// TBD....
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("GetCustomerAttachments")]
        [Produces(typeof(IList<CustomerAttachment>))]
        public async Task<IActionResult> GetCustomerAttachment([FromBody] RequestBase<HolderIdRequest> filters)
        {
            IList<CustomerAttachment> attachments = null;
            try
            {
                CheckServiceAvailability(true);
                //attachments = await _customerService.GetRelatedCustomers(Helper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting customers attachments failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }
            if (attachments == null) return NoContent();
            Helper.JsonSerializer.SerializeData(attachments, "CustomerAttachment");
            return Ok(attachments);
        }
        
        /// <summary>
        /// TBD....
        /// </summary>
        /// <returns></returns>
        [HttpPost("CreateCustomerAttachment")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> CreateCustomerAttachment()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }
        
        /// <summary>
        /// TBD....
        /// </summary>
        /// <returns></returns>
        [HttpPost("DeleteCustomerAttachment")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> DeleteCustomerAttachment()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok(true);
        }

        /// <summary>
        /// GetAgentsByFilters
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("GetAgentsByFilter")]
        [Produces(typeof(IList<Agent>))]
        public async Task<IActionResult> GetAgentsByFilter([FromBody] RequestBase<IList<SearchFilter>> filters)
        {
            IList<Agent> agents = null;
            try
            {
                if (filters?.Body == null || !filters.Body.Any())
                {
                    var serialized = JsonConvert.SerializeObject(filters);
                    ExHelper.ThrowExceptionContainer(new FilterNotSpecifiedException(), 1036, "GetAgentsByFilter() - filter not specified", serialized);
                }
                CheckServiceAvailability(true);
                agents = await _customerService.GetAgentsByFilter(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_CONFOWN), filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting agents by filter failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetAgentsByFilterException(ex));
                return BadRequest(ex.Message);
            }
            if (agents == null) return NoContent();
            Helper.JsonSerializer.SerializeData(agents, "Agent");
            return Ok(agents);
        }

        /// <summary>
        /// CreateNewAgent
        /// Create a new agent entry in configuration.
        /// The agent refers to an existing Holder.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("CreateAgent")]
        [Produces(typeof(short))]
        public async Task<IActionResult> CreateAgent([FromBody] RequestBase<Agent> agent)
        {
            var agentId = (short)0;            

            try
            {
                CheckServiceAvailability(true);
                agentId = await _customerService.CreateAgent(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_CONFOWN), agent);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Creating an agent failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateAgentException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(agentId);
        }

        /// <summary>
        /// Update agent 
        /// </summary>
        /// <returns></returns>
        [HttpPost("UpdateAgent")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateAgent([FromQuery] Agent agent, [FromBody] RequestBase<IList<SearchFilter>> filters)
        {
            var ok = false;
            try
            {
                CheckServiceAvailability(true);
                ok = await _customerService.UpdateAgent(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_CONFOWN), agent, filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Updating an agent failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new UpdateAgentException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(ok);
        }

        /// <summary>
        /// GetAgentProfiles
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost("GetAgentRoles")]
        [Produces(typeof(List<AgentProfile>))]
        public async Task<IActionResult> GetAgentRoles([FromBody] RequestBase<AgentProfileIdRequest> filters)
        {
            IList<AgentProfile> profiles = null;
            try
            {
                CheckServiceAvailability(true);
                profiles = await _customerService.GetAgentRoles(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_CONFOWN), filters);
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting agent roles failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateAgentException(ex));
                return BadRequest(ex.Message);
            }
            if (profiles == null) return NoContent();
            return Ok(profiles);
        }

        /// <summary>
        /// GetAllHolderProfilesDescriptions
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpGet("GetAllHolderProfilesDescriptions")]
        [Produces(typeof(List<HolderProfile>))]
        public async Task<IActionResult> GetAllHolderProfilesDescriptions()
        {
            IList<HolderProfile> profiles = null;
            try
            {
                CheckServiceAvailability(true);
                profiles = await _customerService.GetAllHolderProfilesDescriptions(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_TARIFFOWN));
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting holder profiles failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new CreateAgentException(ex));
                return BadRequest(ex.Message);
            }
            if (profiles == null) return NoContent();
            return Ok(profiles);
        }

        /// <summary>
        /// GetAgentId
        /// Get a new agentId from SBME2
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpGet("GetAgentId")]
        public async Task<IActionResult> GetAgentId()
        {
            // https://localhost:5001/api/customers/?holderID=24
            int? agentId = null;
            try
            {
                CheckServiceAvailability(true);
                agentId = await _customerService.GetAgentId(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_CONFOWN));
            }
            catch (ExceptionContainer ex)
            {
                if (string.IsNullOrWhiteSpace(ex.Description))
                    ex.Description = $"Getting an agent ID failed.";
                LogHelper.Error(_logger, new CreateCustomerException(ex.Exception));
                var serializedEx = JsonConvert.SerializeObject(ex);
                return Helper.I_am_a_teapot(this, serializedEx);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetAgentIdException(ex));
                return BadRequest(ex.Message);
            }
            if (agentId == null) return NoContent();
            HelperClasses.JsonSerializer.SerializeData(agentId, "AgentId");
            return Ok(agentId);
        }

        [HttpPost("ForgetTsc")]
        public async Task<IActionResult> ForgetTsc([FromQuery] string? tscSerial)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            var ok = false;
            if (!string.IsNullOrWhiteSpace(tscSerial))
                try
                {
                    await DBOracleManager2.ForgetTscDocument(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), tscSerial);
                    ok = true;
                }
                catch (ExceptionContainer ex)
                {
                    if (string.IsNullOrWhiteSpace(ex.Description))
                        ex.Description = $"Forget TSC failed.";
                    LogHelper.Error(_logger, ex.Exception);
                    var serializedEx = JsonConvert.SerializeObject(ex);
                    return Helper.I_am_a_teapot(this, serializedEx);
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, ex);
                    return BadRequest(ex.Message);
                }
            return Ok(ok);
        }
    }
}