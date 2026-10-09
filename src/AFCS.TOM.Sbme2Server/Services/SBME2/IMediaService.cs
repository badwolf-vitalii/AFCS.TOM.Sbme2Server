using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeModels.SBME2;

namespace AFCS.TOM.Sbme2Server.Services.SBME2
{
    public interface IMediaService
    {
        public Task<IList<Media>> GetMediaByHolderId(string connectionStringGestown, string connectionStringTariffown, RequestBase<HolderIdRequest> filters);
        public Task<IList<string>> GetMediaByProfileAndBlReason(string connectionStringGestown, int? profile, int? blreason);
        public Task ResetMediaBl(string connectionStringGestown, string tscSerial, int shortcardmodel);
        public Task UpdateMediaStatus(string connectionStringGestown, string tscSerial, int shortcardmodel, byte status);
        public Task CreateMedia(string connectionString, RequestBase<Media> media);
        public Task<int> GetShortCardModel(string connectionString, decimal manufacturedId);
        public Task<SbmeModels.SBME2.PhysicalMediaInfo?> GetMediaPhysicalSerialNumber(string connectionString, int saleDeviceId, uint logicalSerialNumber);
        public Task<LogicalMediaInfo?> GetMediaLogicalSerialNumber(string connectionString, uint phisicalSerialNumber, decimal manufacturerId, int shortCardModel);
        public Task<bool> BlackListContract(string connectionString, bool createHistory, ContractBlackList bcl);
        public Task<bool> BlackListMedia(string connectionString, bool createHistory, BlackList bl);
        public Task<bool> CheckBlackListMedia(string connectionString, int shortCardModel, string serialNumber);
        public Task<bool> DeleteBlackListMedia(string connectionString, BlackListKey blk);

        /* Waiting final documentation */
        public Task<bool> BlackListContractV2(string connectionString, bool createHistory, ContractBlackList bcl);
        public Task<bool> BlackListMediaV2(string connectionString, bool createHistory, BlackList bl);
        public Task<bool> UpdateTSCProofDoc(string connectionString, string proofDocSn, int shortCardModel, string lastSerialNo);
    }

    public class EmptyMediaService : IMediaService
    {
        public Task<bool> BlackListContract(string connectionString, bool createHistory, ContractBlackList bcl)
        {
            throw new NotImplementedException();
        }

        public Task<bool> BlackListContractV2(string connectionString, bool createHistory, ContractBlackList bcl)
        {
            throw new NotImplementedException();
        }

        public Task<bool> BlackListMedia(string connectionString, bool createHistory, BlackList bl)
        {
            throw new NotImplementedException();
        }

        public Task<bool> BlackListMediaV2(string connectionString, bool createHistory, BlackList bl)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckBlackListMedia(string connectionString, int shortCardModel, string serialNumber)
        {
            throw new NotImplementedException();
        }

        public Task CreateMedia(string connectionString, RequestBase<Media> media)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteBlackListMedia(string connectionString, BlackListKey blk)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Media>> GetMediaByHolderId(string connectionStringGestown, string connectionStringTariffown, RequestBase<HolderIdRequest> filters)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetMediaByProfileAndBlReason(string connectionStringGestown, int? profile, int? blreason)
        {
            throw new NotImplementedException();
        }

        public Task ResetMediaBl(string connectionStringGestown, string tscSerial, int shortcardmodel)
        {
            throw new NotImplementedException();
        }

        public Task UpdateMediaStatus(string connectionStringGestown, string tscSerial, int shortcardmodelst, byte status)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetShortCardModel(string connectionString, decimal manufacturedId)
        {
            throw new NotImplementedException();
        }

        public Task<SbmeModels.SBME2.PhysicalMediaInfo?> GetMediaPhysicalSerialNumber(string connectionString, int saleDeviceId, uint logicalSerialNumber)
        {
            throw new NotImplementedException();
        }

        public Task<LogicalMediaInfo?> GetMediaLogicalSerialNumber(string connectionString, uint phisicalSerialNumber, decimal manufacturerId, int shortCardModel)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateTSCProofDoc(string connectionString, string proofDocSn, int shortCardModel, string lastSerialNo)
        {
            throw new NotImplementedException();
        }
    }
}