using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Customer;
using AFCS.TOM.SbmeModels.SBME;
using AFCS.TOM.SbmeModels.SBME.InputParameters;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public interface ICustomerService
    {
        public Task CardIssuingConfirmAsync(MediaDelivery mediaDelivery, SFTPConfirmTSCRequestConfiguration sftpConfig);
        
        public Task ProfileRenewalConfirmAsync(ProfileRenewal profileRenewal, SFTPConfirmTSCRequestConfiguration sftpConfig);

        public Task<ResultForIssuingChecks> ChecksForCardIssuing(Dictionary<ConnectionString, string> connectionStrings, SFTPConfirmTSCRequestConfiguration sftpConfig, TscIssuingParameters parameters);

        public Task<ProfileInfo[]> CalculateProfileInfos(string connectionString, CalculateProfileInfosParameters parameters, DateTime[]? oldPevds = null, CardIssuingFlag issuingFlag = CardIssuingFlag.JustIssued);

        public Task<bool> UpdateCardState(string connectionString, UpdateCardStateParameters parameters);

        public Task<bool> UpdateTscRequestState(string connectionString, UpdateTscRequestStateParameters parameters);

        public Task<bool> UnlockHolder(string connectionString, uint holderId);
        
        public Task<bool> LockHolder(string connectionString, uint holderId);

        public Task<IList<HolderProfile>> GetHolderProfiles(string connectionString, short profile1, short profile2, short profile3, byte? providerId = null);
        
        public Task<IList<HolderProfileDescription>> GetAllHolderProfilesDescriptions(string connectionString);

        public Task<IList<CardLayout>> GetProfileLayoutAsync(string connectionString, int profileId);

        public Task<IList<ProfileRequestMap>> GetProfileRequestMapAsync(string connectionString, int profileReqId);
        
        public Task<IList<ProfileRequestMap>> GetProfileRequestMapByProfileIdAsync(string connectionString, int profile1, int profile2, int profile3, char gender);

        public Task<IList<Customer>> GetCustomersByFilterAsync(string connectionString, string[] customerPar);

        public Task<Customer> GetCustomerByHolderIDAsync(string connectionString, uint holderId);

        public Task<Card> GetCardAsync(string connectionString, int shortCardModel, uint chipId);

        public Task<IList<Card>> GetCardsByHolderIDAsync(string connectionString, uint holderId);

        public Task<IList<ProfileRequestCode>> GetProfileRequestCodeAsync(string tarifownConnectionString, string confownConnectionString, char gender, byte providerId);

        public Task<bool> DeleteCustomerAsync(string connectionString, uint id);
        
        public Task<uint> CreateCustomerAsync(string connectionString, Customer customer);

        public Task UpdateCustomerAsync(string connectionString, uint holderId, List<PredicateFilter> filter);

        public Task<int> GetShortCardModel(string connectionString, decimal manufacturedId);

        public Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null);

        public Task<List<GetContractsResult>> GetContracts(string connectionString, GetContractsParameters parameters);

        public Task ClearTDSDEContracts(string connectionString);
        
        public Task<GetProfileExtensionDetails> GetProfileExtension(string connectionString, GetProfileExtensionParameters parameters);

        public Task<int> BlackListCard(string connectionString, StolenLoastParameters parameters);

        public Task<bool> UpdateCardExpirationDates(string connectionString, UpdateCardExpirationDatesParameters parameters);
    }

    public class EmptyCustomerService : ICustomerService
    {
        public Task<ProfileInfo[]> CalculateProfileInfos(string connectionString, CalculateProfileInfosParameters parameters, DateTime[]? oldPevds = null, CardIssuingFlag issuingFlag = CardIssuingFlag.JustIssued)
        {
            throw new NotImplementedException();
        }

        public Task CardIssuingConfirmAsync(MediaDelivery mediaDelivery, SFTPConfirmTSCRequestConfiguration sftpConfig)
        {
            throw new NotImplementedException();
        }

        public Task<ResultForIssuingChecks> ChecksForCardIssuing(Dictionary<ConnectionString, string> connectionStrings, SFTPConfirmTSCRequestConfiguration sftpConfig, TscIssuingParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task<uint> CreateCustomerAsync(string connectionString, Customer customer)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteCustomerAsync(string connectionString, uint id)
        {
            throw new NotImplementedException();
        }

        public Task<Card> GetCardAsync(string connectionString, int shortCardModel, uint chipId)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Card>> GetCardsByHolderIDAsync(string connectionString, uint holderId)
        {
            throw new NotImplementedException();
        }

        public Task<List<GetContractsResult>> GetContracts(string connectionString, GetContractsParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task ClearTDSDEContracts(string connectionString)
        {
            throw new NotImplementedException();
        }

        public Task<Customer> GetCustomerByHolderIDAsync(string connectionString, uint holderId)
        {
            throw new NotImplementedException();
        }

        public Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Customer>> GetCustomersByFilterAsync(string connectionString, string[] customerPar)
        {
            throw new NotImplementedException();
        }

        public Task<IList<HolderProfile>> GetHolderProfiles(string connectionString, short profile1, short profile2, short profile3, byte? providerId = null)
        {
            throw new NotImplementedException();
        }

        public Task<IList<HolderProfileDescription>> GetAllHolderProfilesDescriptions(string connectionString)
        {
            throw new NotImplementedException();
        }

        public Task<GetProfileExtensionDetails> GetProfileExtension(string connectionString, GetProfileExtensionParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task<IList<CardLayout>> GetProfileLayoutAsync(string connectionString, int profileId)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ProfileRequestCode>> GetProfileRequestCodeAsync(string tarifownConnectionString, string confownConnectionString, char gender, byte providerId)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ProfileRequestMap>> GetProfileRequestMapAsync(string connectionString, int profileReqId)
        {
            throw new NotImplementedException();
        }

        public Task<IList<ProfileRequestMap>> GetProfileRequestMapByProfileIdAsync(string connectionString, int profile1, int profile2, int profile3, char gender)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetShortCardModel(string connectionString, decimal manufacturedId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LockHolder(string connectionString, uint holderId)
        {
            throw new NotImplementedException();
        }

        public Task ProfileRenewalConfirmAsync(ProfileRenewal profileRenewal, SFTPConfirmTSCRequestConfiguration sftpConfig)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UnlockHolder(string connectionString, uint holderId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateCardState(string connectionString, UpdateCardStateParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCustomerAsync(string connectionString, uint holderId, List<PredicateFilter> filter)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateTscRequestState(string connectionString, UpdateTscRequestStateParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task<int> BlackListCard(string connectionString, StolenLoastParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateCardExpirationDates(string connectionString, UpdateCardExpirationDatesParameters parameters)
        {
            throw new NotImplementedException();
        }
    }
}
