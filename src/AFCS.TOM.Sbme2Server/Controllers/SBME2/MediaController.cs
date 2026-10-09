using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.SBME2;
using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.SBME2;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace AFCS.TOM.Sbme2Server.Controllers.SBME2
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    [Route("api/v2/[controller]")]
    public class MediaController : ControllerBase
    {
        protected static ServiceNotAvailableException ServiceNotAvailable { get; } = new ServiceNotAvailableException("Sbm2.Media");

        private NLog.Logger _logger { get; } = LogManager.GetLogger("Sbme2Server");
        private IConfiguration _configuration { get; set; }
        private IMediaService _mediaService { get; set; }
        private bool _isServiceEnabled { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="mediaService"></param>
        public MediaController(IConfiguration configuration, IMediaService mediaService)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var launchSettings = new LaunchSettings();
            config.GetSection("LaunchSettings").Bind(launchSettings);
            _isServiceEnabled = launchSettings.Sbme2ServicesEnabled;

            _configuration = configuration;
            _mediaService = mediaService;
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
        /// GetMediasByHolderId
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("GetMediasByHolderId")]
        [Produces(typeof(IList<Media>))]
        public async Task<IActionResult> GetMediasByHolderId([FromBody] RequestBase<HolderIdRequest> parameters)
        {
            IList<Media>? medias;
            try
            {
                CheckServiceAvailability(true);
                medias = await _mediaService.GetMediaByHolderId(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_TARIFFOWN), parameters);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetMediasByHolderIdException(ex));
                return BadRequest(ex.Message);
            }
            if (medias == null) return NoContent();
            Helper.JsonSerializer.SerializeData(medias, "Media");
            return Ok(medias);
        }

        /// <summary>
        /// GetMediaByProfileAndBlReason
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpGet("GetMediaByProfileAndBlReason")]
        [Produces(typeof(IList<string>))]
        public async Task<IActionResult> GetMediaByProfileAndBlReason([FromQuery] int? profile, [FromQuery] int? blreason)
        {
            IList<string>? medias;
            try
            {
                CheckServiceAvailability(true);
                medias = await _mediaService.GetMediaByProfileAndBlReason(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), profile, blreason);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetMediasByHolderIdException(ex));
                return BadRequest(ex.Message);
            }
            if (medias == null) return NoContent();
            Helper.JsonSerializer.SerializeData(medias, "GetMediaByProfileAndBlReason");
            return Ok(medias);
        }

        /// <summary>
        /// UpdateMediaBl
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("ResetMediaBl")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> ResetMediaBl([FromQuery] string tscSerial, [FromQuery] int shortcardmodel)
        {
            var resul = false;
            try
            {
                CheckServiceAvailability(true);
                await _mediaService.ResetMediaBl(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), tscSerial, shortcardmodel);
                resul = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetMediasByHolderIdException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(resul);
        }

        /// <summary>
        /// UpdateMediaWStatus
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("UpdateMediaStatus")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateMediaStatus([FromQuery] string tscSerial, [FromQuery] int shortcardmodel, [FromQuery] byte status)
        {
            var resul = false;
            try
            {
                CheckServiceAvailability(true);
                await _mediaService.UpdateMediaStatus(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), tscSerial, shortcardmodel, status);
                resul = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new GetMediasByHolderIdException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(resul);
        }

        /// <summary>
        /// CreateMedia
        /// </summary>
        /// <param name="media"></param>
        /// <returns></returns>
        [HttpPost("CreateMedia")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> CreateMedia([FromBody] RequestBase<Media> media)
        {
            var returnValue = false;
            try
            {
                CheckServiceAvailability(true);
                await _mediaService.CreateMedia(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), media);
                returnValue = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new MediaDeliveryException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(returnValue);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("GetMediaRequest")]
        //[Produces(typeof(bool))]
        public async Task<IActionResult> GetMediaRequest([FromBody] RequestBase<HolderIdRequest> filters)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        [HttpPost("GetMediaContracts")]
        //[Produces(typeof(bool))]
        public async Task<IActionResult> GetMediaContracts([FromBody] RequestBase<MediaId> filters)
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            return Ok();
        }
        
        /// <summary>
        /// GetNewMediaEVDA
        /// </summary>
        /// <param name="mediaId"></param>
        /// <returns></returns>
        [HttpPost("GetNewMediaEVD")]
        [Produces(typeof(DateTime))]
        public async Task<IActionResult> GetMediaEVD([FromBody] RequestBase<MediaId> mediaId)
        {
            DateTime? newMediaEVD = null;
            try
            {
                CheckServiceAvailability(true);
                //await _mediaService.MediaDelivery(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), media);
                //returnValue = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new MediaDeliveryException(ex));
                return BadRequest(ex.Message);
            }
            if (newMediaEVD == null) return NoContent();
            return Ok(newMediaEVD);
        }
        
        /// <summary>
        /// MediaEVDUpdate
        /// </summary>
        /// <param name="mediaUpdateEVDParameters"></param>
        /// <returns></returns>
        [HttpPost("MediaEVDUpdate")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateEVDMediaEVD([FromBody] RequestBase<MediaUpdateEVDParameters> mediaUpdateEVDParameters)
        {
            var result = true;
            try
            {
                CheckServiceAvailability(true);
                //await _mediaService.MediaDelivery(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), media);
                //returnValue = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new MediaDeliveryException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }
        
        /// <summary>
        /// MediaLock
        /// </summary>
        /// <param name="mediaLockParameter"></param>
        /// <returns></returns>
        [HttpPost("MediaLock")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> MediaLock([FromBody] RequestBase<MediaLockParameter> mediaLockParameter)
        {
            var result = true;
            try
            {
                CheckServiceAvailability(true);
                //await _mediaService.MediaDelivery(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), media);
                //returnValue = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new MediaDeliveryException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }
        
        /// <summary>
        /// MediaUnLock
        /// </summary>
        /// <param name="mediaId"></param>
        /// <returns></returns>
        [HttpPost("MediaUnLock")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> MediaUnLock([FromBody] RequestBase<MediaId> mediaId)
        {
            var result = true;
            try
            {
                CheckServiceAvailability(true);
                //await _mediaService.MediaDelivery(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), media);
                //returnValue = true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, new MediaDeliveryException(ex));
                return BadRequest(ex.Message);
            }
            return Ok(result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost("GetMediaEvents")]
        //[Produces(typeof(IList<Media>))]
        public async Task<IActionResult> GetMediaEvents()
        {
            if (!CheckServiceAvailability()) return BadRequest(ServiceNotAvailable);
            IList<Media> medias = null;
            //try
            //{
            //medias = await _mediaService.GetMediaByHolderId(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), filters);
            //}
            //catch (Exception ex)
            //{
            //    LogHelper.Error(_logger, new GetMediasByHolderIdException(ex));
            //    return BadRequest(ex.Message);
            //}
            //Helper.JsonSerializer.SerializeData(medias, "Media");
            if (medias == null) return NoContent();
            return Ok();
        }

        [HttpGet("GetShortCardModel")]
        public async Task<IActionResult> GetShortCardModel([FromQuery] ulong cardManufacturedId)
        {
            var result = 0;
            try
            {
                CheckServiceAvailability(true);
                result = await _mediaService.GetShortCardModel(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_TARIFFOWN), cardManufacturedId);
            }
            catch (Exception ex)
            {
                ex = new ExceptionContainer(ex, 1, $"No ShortCardModel associated to the ManufacturedId {cardManufacturedId}");
                LogHelper.Error(_logger, new NoShortCardModelAssociatedToTheManufacturedId(cardManufacturedId, ex));
                return BadRequest(ex);
            }
            return Ok(result);
        }

        [HttpGet("GetMediaPhysicalSerialNumber")]
        public async Task<IActionResult> GetMediaPhysicalSerialNumber([FromQuery] uint logicalSerialNumber, [FromQuery] int saleDeviceId)
        {
            SbmeModels.SBME2.PhysicalMediaInfo? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await _mediaService.GetMediaPhysicalSerialNumber(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), saleDeviceId, logicalSerialNumber);
            }
            catch (Exception ex)
            {
                ex = new ExceptionContainer(ex, 1);
                LogHelper.Error(_logger, ex);
                return BadRequest(ex);
            }
            if (result == null) return NoContent();
            return Ok(result);
        }

        [HttpGet("GetMediaLogicalSerialNumber")]
        public async Task<IActionResult> GetMediaLogicalSerialNumber([FromQuery] uint phisicalSerialNumber, [FromQuery] decimal manufacturerId, [FromQuery] int shortCardModel)
        {
            LogicalMediaInfo? result = null;
            try
            {
                CheckServiceAvailability(true);
                result = await _mediaService.GetMediaLogicalSerialNumber(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), phisicalSerialNumber, manufacturerId, shortCardModel);
            }
            catch (Exception ex)
            {
                ex = new ExceptionContainer(ex, 1);
                LogHelper.Error(_logger, ex);
                return BadRequest(ex);
            }
            if (result == null) return NoContent();
            return Ok(result);
        }

        /// <summary>
        /// Insert contract in black list
        /// and in history if createHistory = true
        /// </summary>
        /// <returns></returns>
        [HttpPost("BlackListContract")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> BlackListContract(bool createHistory ,ContractBlackList bcl)
        {
            var insertResult = false;
            try
            {
                CheckServiceAvailability(true);
                insertResult = await _mediaService.BlackListContract(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), createHistory, bcl);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex); // New personal exception must be created
                return BadRequest(ex.Message);
            }

            return Ok(insertResult);
        }

        /// <summary>
        /// Insert card in black list
        /// and in history if createHistory = true
        /// </summary>
        /// <returns></returns>
        [HttpPost("BlackListMedia")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> BlackListMedia(bool createHistory, BlackList bl)
        {
            var insertResult = false;
            try
            {
                CheckServiceAvailability(true);
                insertResult = await _mediaService.BlackListMedia(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), createHistory, bl);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex); // New personal exception must be created
                return BadRequest(ex.Message);
            }

            return Ok(insertResult);
        }

        /// <summary>
        /// Check if a card is in black list
        /// </summary>
        /// <returns></returns>
        [HttpPost("CheckBlackListMedia")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> CheckBlackListMedia(int shortCardModel, string serialNumber)
        {
            var check = false;
            try
            {
                CheckServiceAvailability(true);
                check = await _mediaService.CheckBlackListMedia(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN),shortCardModel,serialNumber);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex); // New personal exception must be created
                return BadRequest(ex.Message);
            }

            return Ok(check);
        }

        /// <summary>
        /// Delete card from black list
        /// </summary>
        /// <returns></returns>
        [HttpPost("DeleteBlackListMedia")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> DeleteBlackListMedia(BlackListKey blk)
        {
            var isDeleted = false;
            try
            {
                CheckServiceAvailability(true);
                isDeleted = await _mediaService.DeleteBlackListMedia(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN),blk);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex); // New personal exception must be created
                return BadRequest(ex.Message);
            }

            return Ok(isDeleted);
        }

        /// <summary>
        /// Update card proof document
        /// </summary>
        /// <returns></returns>
        [HttpPost("UpdateTSCProofDoc")]
        [Produces(typeof(bool))]
        public async Task<IActionResult> UpdateTSCProofDoc([FromQuery] string proofDocSn, [FromQuery] int shortCardModel, [FromQuery] string lastSerialNo)
        {
            var isUpdated = false;
            try
            {
                CheckServiceAvailability(true);
                isUpdated = await _mediaService.UpdateTSCProofDoc(ControllersHelper.GetConnectionString(_configuration, ConnectionString.SBME2_GESTOWN), proofDocSn, shortCardModel, lastSerialNo);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                return BadRequest(ex.Message);
            }

            return Ok(isUpdated);
        }
    }
}
