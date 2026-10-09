using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeModels.SBME2;

namespace AFCS.TOM.Sbme2Server.Services.SBME2
{
    public class MediaService : IMediaService
    {
        // Temporarily ... we will have RequestBase<SomethingNew> from Vitalii
        int reasonCode;

        public async Task<IList<Media>> GetMediaByHolderId(string connectionStringGestown, string connectionStringTariffown, RequestBase<HolderIdRequest> filters) =>
            await DBOracleManager2.GetMediaByHolderId(connectionStringGestown, connectionStringTariffown, filters).ConfigureAwait(false);

        public async Task<IList<string>> GetMediaByProfileAndBlReason(string connectionStringGestown, int? profile, int? blreason) =>
            await DBOracleManager2.GetMediaByProfileAndBlReason(connectionStringGestown, profile, blreason).ConfigureAwait(false);

        public async Task ResetMediaBl(string connectionStringGestown, string tscSerial, int shortcardmodel) =>
            await DBOracleManager2.ResetMediaBl(connectionStringGestown, tscSerial, shortcardmodel).ConfigureAwait(false);

        public async Task UpdateMediaStatus(string connectionStringGestown, string tscSerial, int shortcardmodel, byte status) =>
            await DBOracleManager2.ResetMediaStatus(connectionStringGestown, tscSerial, shortcardmodel, status).ConfigureAwait(false);

        public async Task CreateMedia(string connectionString, RequestBase<Media> media) =>
            await DBOracleManager2.CreateMedia(connectionString, media).ConfigureAwait(false);

        public async Task<int> GetShortCardModel(string connectionString, decimal manufacturedId) =>
            await DBOracleManager2.GetShortCardModel(connectionString, manufacturedId);

        public async Task<SbmeModels.SBME2.PhysicalMediaInfo?> GetMediaPhysicalSerialNumber(string connectionString, int saleDeviceId, uint logicalSerialNumber) =>
            await DBOracleManager2.GetMediaPhysicalSerialNumber(connectionString, saleDeviceId, logicalSerialNumber);

        public async Task<LogicalMediaInfo?> GetMediaLogicalSerialNumber(string connectionString, uint phisicalSerialNumber, decimal manufacturerId, int shortCardModel) =>
            await DBOracleManager2.GetMediaLogicalSerialNumber(connectionString, phisicalSerialNumber, manufacturerId, shortCardModel);

        public async Task<bool> BlackListContract(string connectionString, bool createHistory, ContractBlackList bcl) =>
            await DBOracleManager2.BlackListContract(connectionString, bcl, createHistory).ConfigureAwait(false);

        public async Task<bool> BlackListMedia(string connectionString, bool createHistory, BlackList bl) =>
            await DBOracleManager2.BlackListMedia(connectionString, bl, createHistory).ConfigureAwait(false);

        public async Task<bool> CheckBlackListMedia(string connectionString, int shortCardModel, string serialNumber) =>
            await DBOracleManager2.CheckCardInBlackList(shortCardModel, serialNumber, connectionString).ConfigureAwait(false);

        public async Task<bool> DeleteBlackListMedia(string connectionString, BlackListKey blk) =>
            await DBOracleManager2.DeleteCardFromBlackList(blk,connectionString).ConfigureAwait(false);

        /* Waiting final documentation */
        public async Task<bool> BlackListContractV2(string connectionString, bool createHistory, ContractBlackList bcl) =>
            await DBOracleManager2.BlackListContractV2(connectionString, bcl, createHistory).ConfigureAwait(false);

        public async Task<bool> BlackListMediaV2(string connectionString, bool createHistory, BlackList bl) =>
            await DBOracleManager2.BlackListMediaV2(connectionString, reasonCode, bl, createHistory).ConfigureAwait(false);

        public async Task<bool> UpdateTSCProofDoc(string connectionString, string proofDocSn, int shortCardModel, string lastSerialNo) =>
            await DBOracleManager2.UpdateTSCProofDoc(connectionString, proofDocSn, shortCardModel, lastSerialNo).ConfigureAwait(false);
    }
}
