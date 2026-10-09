using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.VtCashFlow;
using Microsoft.EntityFrameworkCore;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public interface IBglDbService : IBglServiceBase
    {
        bool IsServiceEnabled { get; }

        #region DB
        Task<bool> AddDbVersionChangeLog(int dbVersion, string? changeLog);
        Task<bool> UpdateDbVersionDateTime(int dbVersion, DateTime? dateTime);
        Task<DatabaseInfo?> GetDatabaseInfo(int dbVersion);
        Task<DatabaseInfo?> GetLastDatabaseInfo();
        Task<DatabaseInfo?> GetNewDatabaseInfo(int dbVersion);
        Task<bool> UpdateDatabase(bool oneStepUpdate, int? dbVersion);
        void GenerateBacpac();
        string? GetLatestBacpacFileName();
        byte[]? DownloadLatestBacpac();
        SbmeProfilePriceMapping? GetSbmeProfilePriceMapping(int profileId, short issuingReasonCode = 0);
        #endregion

        Task<GetAccountingPeriodResponse?> GetAccountingPeriod(bool includeDeviceShifts = true);
        Task RegisterNewDevice(string deviceIdentifier);
        Task<StaticVariablesList?> GetStaticVariables(string deviceIdentifier);
        Task SetStaticVariables(StaticVariablesList variables);
        Task DeclareAdminBlockUnlockManaged(string deviceIdentifier);
        Task DeclareThresholdBlockUnlockManaged(string deviceIdentifier);
        Task DeclareOfflineBlockUnlockManaged(string deviceIdentifier);

        #region Agent Shift
        Task<AgentShift> AddAgentShift(AgentShift shift, Guid? periodId = null);
        Task<byte> GetAgentShiftNumber(int agentId, short companyId, string deviceIdentifier, Guid? periodId = null);
        Task<AgentShift?> GetAgentShift(int agentId, short companyId, Guid? periodId = null, bool includeDeviceShifts = true);
        Task<bool> OpenAgentShift(Guid shiftId, short serviceType, short sellingRegion);
        Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, PaymentMethodReport[]? paymentMethods = null, bool closedAutomatically = false);
        Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, int closingAgentId, PaymentMethodReport[]? paymentMethods = null);
        Task<bool> SetAgentVtsShift(Guid shiftId, int vtsShiftId);
        /// <summary>
        /// Locks or unlocks the shift
        /// </summary>
        Task<bool> LockAgentShift(Guid shiftId);
        #endregion

        #region Device Shift
        Task<DeviceShift?> GetDeviceShift(string deviceIdentifier, bool includeContainers = true);
        Task<DeviceShift?> GetDeviceShift(Guid deviceShiftId, bool includeContainers = true);
        Task<DeviceShift?> GetDeviceShiftOfAgent(Guid agentShiftId, bool includeContainers = true);
        Task<DeviceShift> AddDeviceShift(DeviceShift shift, Guid agentShiftId);
        Task<bool> OpenDeviceShift(Guid shiftId);
        Task<bool> CloseDeviceShift(Guid shiftId, byte closingReason = 0, string closingReasonDescription = null);
        Task<bool> UpdateDeviceShift(DeviceShift shift);
        Task<bool> IsDeviceOperating(Guid shiftId);
        Task<Guid> RegisterCardAnomaly(CardAnomaly anomaly);
        Task ResolveCardAnomaly(CardAnomaly anomaly);
        Task SetDeviceStatus(SaleDevice saleDevice);
        Task CleanUpDeviceStatus(int saleDeviceId);
        #endregion

        Task<Guid> RegisterApplicationShutdown(ApplicationShutdown shutdown);
        byte[]? GetApplicationSnapshot(string id, bool downloadHugeFile = false);
        byte[]? DownloadApplicationShutdownContext(string id, string fileName);
        string? LoadPersonalization(int agentId, short companyId);
        void SavePersonalization(string? personalization, int agentId, short companyId);

        Task RegisterSellContract(VtSellContractInfo info);
        Task RegisterSellCommit(VtSellCommitInfo info);
        Task RegisterSellCommitPamentType(VtSellCommitPaymentTypeInfo info);
        Task RegisterSaleTransactionPamentType(VtSellTransactionPaymentTypeInfo info);
        Task<VtSellCommitShortInfo?> GetVtSellCommit(string groupUid);
        Task<List<VtTransactionCashFlow>?> GetVtTransactionCashFlow(string transactionUid);
        Task<string?> GetMinAppVersione();
        Task<string?> GetMinVtsVersione();
        Task SetMinAppVersione(string? version);
        Task SetMinVtsVersione(string? version);
    }

    public class EmptyBglDbService : IBglDbService
    {
        public bool IsServiceEnabled { get; }

        public Task<bool> AddDbVersionChangeLog(int dbVersion, string? changeLog)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateDbVersionDateTime(int dbVersion, DateTime? dateTime)
        {
            throw new NotImplementedException();
        }

        public Task<DatabaseInfo?> GetDatabaseInfo(int dbVersion)
        {
            throw new NotImplementedException();
        }

        public Task<DatabaseInfo?> GetLastDatabaseInfo()
        {
            throw new NotImplementedException();
        }

        public Task<DatabaseInfo?> GetNewDatabaseInfo(int dbVersion)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateDatabase(bool oneStepUpdate, int? dbVersion)
        {
            throw new NotImplementedException();
        }

        public Task<AgentShift> AddAgentShift(AgentShift shift, Guid? periodId = null)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceShift> AddDeviceShift(DeviceShift shift, Guid agentShiftId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, bool closedAutomatically = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, int closingAgentId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CloseDeviceShift(Guid shiftId, byte closingReason = 0, string closingReasonDescription = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateDeviceShift(DeviceShift shift)
        {
            throw new NotImplementedException();
        }

        public byte[] CreateCloseShiftReceipt(Guid agentShiftId)
        {
            throw new NotImplementedException();
        }

        public Task<AgentShift?> GetAgentShift(int agentId, short companyId, Guid? periodId = null, bool includeDeviceShifts = true)
        {
            throw new NotImplementedException();
        }

        public Task<AgentShift?> GetAgentShift(Guid shiftId)
        {
            throw new NotImplementedException();
        }

        public Task<byte> GetAgentShiftNumber(int agentId, short companyId, string deviceIdentifier, Guid? periodId = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<AgentShift>?> GetAgentShifts(int agentId, DateTime from, DateTime to)
        {
            throw new NotImplementedException();
        }

        public Task<List<AgentShift>?> GetAllAgentsShifts(DateTime from, DateTime to)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceShift?> GetDeviceShift(string deviceIdentifier, bool includeContainers = true)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceShift?> GetDeviceShift(Guid deviceShiftId, bool includeContainers = true)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceShift?> GetDeviceShiftOfAgent(Guid agentShiftId, bool includeContainers = true)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsDeviceOperating(Guid shiftId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LockAgentShift(Guid shiftId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> OpenAgentShift(Guid shiftId, short serviceType, short sellingRegion)
        {
            throw new NotImplementedException();
        }

        public Task<bool> OpenDeviceShift(Guid shiftId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetAgentVtsShift(Guid shiftId, int vtsShiftId)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> RegisterCardAnomaly(CardAnomaly anomaly)
        {
            throw new NotImplementedException();
        }

        public Task ResolveCardAnomaly(CardAnomaly anomaly)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> RegisterApplicationShutdown(ApplicationShutdown shutdown)
        {
            throw new NotImplementedException();
        }

        public Task SetDeviceStatus(SaleDevice saleDevice)
        {
            throw new NotImplementedException();
        }

        public Task CleanUpDeviceStatus(int saleDeviceId)
        {
            throw new NotImplementedException();
        }

        public byte[]? GetApplicationSnapshot(string id, bool downloadHugeFile = false)
        {
            throw new NotImplementedException();
        }

        public byte[]? DownloadApplicationShutdownContext(string id, string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<GetAccountingPeriodResponse?> GetAccountingPeriod(bool includeDeviceShifts = true)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, PaymentMethodReport[]? paymentMethods = null, bool closedAutomatically = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, int closingAgentId, PaymentMethodReport[]? paymentMethods = null)
        {
            throw new NotImplementedException();
        }

        public void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public Task<int> SaveChangesAsync()
        {
            throw new NotImplementedException();
        }

        public void GenerateBacpac()
        {
            throw new NotImplementedException();
        }

        public string? GetLatestBacpacFileName()
        {
            throw new NotImplementedException();
        }

        public byte[]? DownloadLatestBacpac()
        {
            throw new NotImplementedException();
        }

        public SbmeProfilePriceMapping? GetSbmeProfilePriceMapping(int profileId, short issuingReasonCode = 0)
        {
            throw new NotImplementedException();
        }

        public Task RegisterNewDevice(string deviceIdentifier)
        {
            throw new NotImplementedException();
        }

        public Task<StaticVariablesList?> GetStaticVariables(string deviceIdentifier)
        {
            throw new NotImplementedException();
        }

        public Task SetStaticVariables(StaticVariablesList variables)
        {
            throw new NotImplementedException();
        }

        public Task DeclareAdminBlockUnlockManaged(string deviceIdentifier)
        {
            throw new NotImplementedException();
        }

        public Task DeclareThresholdBlockUnlockManaged(string deviceIdentifier)
        {
            throw new NotImplementedException();
        }

        public Task DeclareOfflineBlockUnlockManaged(string deviceIdentifier)
        {
            throw new NotImplementedException();
        }

        public string? LoadPersonalization(int agentId, short companyId)
        {
            throw new NotImplementedException();
        }

        public void SavePersonalization(string? personalization, int agentId, short companyId)
        {
            throw new NotImplementedException();
        }

        public Task RegisterSellContract(VtSellContractInfo info)
        {
            throw new NotImplementedException();
        }

        public Task RegisterSellCommit(VtSellCommitInfo info)
        {
            throw new NotImplementedException();
        }

        public Task RegisterSellCommitPamentType(VtSellCommitPaymentTypeInfo info)
        {
            throw new NotImplementedException();
        }

        public Task RegisterSaleTransactionPamentType(VtSellTransactionPaymentTypeInfo info)
        {
            throw new NotImplementedException();
        }

        public async Task<VtSellCommitShortInfo?> GetVtSellCommit(string groupUid)
        {
            throw new NotImplementedException();
        }

        public async Task<List<VtTransactionCashFlow>?> GetVtTransactionCashFlow(string transactionUid)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetMinAppVersione()
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetMinVtsVersione()
        {
            throw new NotImplementedException();
        }

        public async Task SetMinAppVersione(string? version)
        {
            throw new NotImplementedException();
        }

        public async Task SetMinVtsVersione(string? version)
        {
            throw new NotImplementedException();
        }
    }
}
