using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.SBME.InputParameters;

namespace AFCS.TOM.Sbme2Server.Exceptions
{
    public static class ExHelper
    {
        const int InternalServerErrorCode = 5;

        public static bool AllowExceptionContainers { get; set; }

        public static void ThrowExceptionContainer(Exception ex, string description)
        {
            if (AllowExceptionContainers) throw new ExceptionContainer(ex, InternalServerErrorCode, description);
        }

        public static void ThrowExceptionContainer(Exception ex, string description, string additionalInfo)
        {
            if (AllowExceptionContainers) throw new ExceptionContainer(ex, InternalServerErrorCode, description, additionalInfo);
        }

        public static void ThrowExceptionContainer(Exception ex, int code, string description = "")
        {
            if (AllowExceptionContainers) throw new ExceptionContainer(ex, code, description);
        }

        public static void ThrowExceptionContainer(Exception ex, int code, string description, string additionalInfo)
        {
            if (AllowExceptionContainers) throw new ExceptionContainer(ex, code, description, additionalInfo);
        }
    }

    // 1000
    public class ServiceNotAvailableException : ExceptionContainer
    {
        private const string _message = "Service {0} is not available in the current installation.";

        public ServiceNotAvailableException(string serviceName)
        {
            Code = (int)DsdeSrvExceptionCode.ServiceNotAvailable;
            Description = string.Format(_message, serviceName);
        }
    }

    // 1001
    public class DBConnectionOpeningException : ExceptionContainer
    {
        private const string _message1 = "Failed to open the DB connection.";
        private const string _message2 = "Failed to open the DB connection ({0}).";

        private static string? GetDataSource(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
            var start = connectionString.Replace("data source", "datasource").ToLower().IndexOf("datasource");
            start = connectionString.IndexOf("=", start + 1);
            if (start >= 0)
            {
                var end = connectionString.IndexOf(";", start + 1);
                if (end >= 0)
                    return connectionString.Substring(start, end - start).Trim();
            }
            return null;
        }

        public DBConnectionOpeningException(string? connectionString = null)
        {
            Code = (int)DsdeSrvExceptionCode.DBConnectionOpening;
            Description = string.IsNullOrWhiteSpace(connectionString) ? _message1 : string.Format(_message2, GetDataSource(connectionString));
        }
    }

    // 1002
    public class DBConnectionClosingException : ExceptionContainer
    {
        private const string _message = "Failed to close the DB connection.";

        public DBConnectionClosingException()
        {
            Code = (int)DsdeSrvExceptionCode.DBConnectionClosing;
            Description = _message;
        }
    }

    // 1003
    public class CreateCustomerException : ExceptionContainer
    {
        private const string _message = "Failed to create new customer.";

        public CreateCustomerException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.CreateCustomer;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1004
    public class GetCustomersByFilterException : ExceptionContainer
    {
        private const string _message = "Failed to get a customer by filter.";

        public GetCustomersByFilterException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetCustomersByFilter;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1005
    public class GetAgentsByFilterException : ExceptionContainer
    {
        private const string _message = "Failed to get an agent by filter.";

        public GetAgentsByFilterException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetAgentsByFilter;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1006
    public class GetMediasByHolderIdException : ExceptionContainer
    {
        private const string _message = "Failed to get a media by HolderId.";

        public GetMediasByHolderIdException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetMediasByHolderId;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1007
    public class MediaDeliveryException : ExceptionContainer
    {
        private const string _message = "Failed to delivery a new media";

        public MediaDeliveryException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.MediaDelivery;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1008
    public class NoShortCardModelAssociatedToTheManufacturedId : ExceptionContainer
    {
        private const string _message = "No ShortCardModel associated to the ManufacturedId {0}.";

        public NoShortCardModelAssociatedToTheManufacturedId(ulong cardManufacturedId, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.NoShortCardModelAssociatedToTheManufacturedId;
            Description = string.Format(_message, cardManufacturedId);
            Exception = innerException;
        }
    }

    // 1009
    public class ChecksForCardIssuingException : ExceptionContainer
    {
        private const string _message = "Failed to check card for Issuing.";

        public ChecksForCardIssuingException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.ChecksForCardIssuing;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1010
    public class GetContractsException : ExceptionContainer
    {
        private const string _message = "Failed to get contracts for the card {0}-{1}.";

        public GetContractsException(GetContractsParameters parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetContracts;
            Description = string.Format(_message, parameters?.ShortCardModel ?? 0, parameters?.TscSerialNumber ?? string.Empty);
            Exception = innerException;
        }

        public GetContractsException(int shortCardModel, int tscSerialNumber, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetContracts;
            Description = string.Format(_message, shortCardModel, tscSerialNumber);
            Exception = innerException;
        }
    }

    // 1011
    public class GetProfileExtensionException : ExceptionContainer
    {
        private const string _message = "Failed to get profile extension for the card {0} and profiles: {1}, {2}, {3}.";

        public GetProfileExtensionException(GetProfileExtensionParameters parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileExtension;
            Description = string.Format(_message, parameters?.TSCSerialNo ?? 0, parameters?.HolderProfile1 ?? 0, parameters?.HolderProfile2 ?? 0, parameters?.HolderProfile3 ?? 0);
            Exception = innerException;
        }

        public GetProfileExtensionException(long tscSerialNo, int holderProfile, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileExtension;
            Description = string.Format(_message, tscSerialNo, holderProfile);
            Exception = innerException;
        }

        public GetProfileExtensionException(long tscSerialNo, int holderProfile1, int holderProfile2, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileExtension;
            Description = string.Format(_message, tscSerialNo, holderProfile1, holderProfile2);
            Exception = innerException;
        }

        public GetProfileExtensionException(long tscSerialNo, int holderProfile1, int holderProfile2, int holderProfile3, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileExtension;
            Description = string.Format(_message, tscSerialNo, holderProfile1, holderProfile2, holderProfile3);
            Exception = innerException;
        }
    }

    // 1012
    public class BlackListCardException : ExceptionContainer
    {
        private const string _message = "Failed to blacklist the card {0}.";

        public BlackListCardException(StolenLoastParameters parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.BlackListCard;
            Description = string.Format(_message, parameters?.TSCSerialNo ?? 0);
            Exception = innerException;
        }

        public BlackListCardException(long tscSerialNo, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.BlackListCard;
            Description = string.Format(_message, tscSerialNo);
            Exception = innerException;
        }
    }

    // 1013
    public class UpdateCardExpirationDatesException : ExceptionContainer
    {
        private const string _message = "Failed to update EV dates of the card {0}-{1} to {2} ({3}, {4}, {5}).";

        public UpdateCardExpirationDatesException(UpdateCardExpirationDatesParameters? parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateCardExpirationDates;
            Description = string.Format(_message,
                parameters?.ShortCardModel ?? 0,
                parameters?.CardSerialNumber ?? 0,
                parameters?.CardEvd?.ToString("dd/MM/yyyy") ?? "-/-/-",
                parameters?.Profile1Evd?.ToString("dd/MM/yyyy") ?? "-/-/-",
                parameters?.Profile2Evd?.ToString("dd/MM/yyyy") ?? "-/-/-",
                parameters?.Profile3Evd?.ToString("dd/MM/yyyy") ?? "-/-/-");
            Exception = innerException;
        }
    }

    // 1014
    public class CardIssuingConfirmException : ExceptionContainer
    {
        private const string _message = "Failed to confirm card for issuing.";

        public CardIssuingConfirmException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.CardIssuingConfirm;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1015
    public class ProfileRenewalConfirmException : ExceptionContainer
    {
        private const string _message = "Failed to confirm profile renewal.";

        public ProfileRenewalConfirmException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.ProfileRenewalConfirm;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1016
    public class UpdateCardStateAsyncException : ExceptionContainer
    {
        private const string _message = "Failed to update card's ({0}-{1}) state '{2}' of the holder {3}.";

        public UpdateCardStateAsyncException(UpdateCardStateParameters parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateCardStateAsync;
            Description = string.Format(_message, parameters.ShortCardModel, parameters.CardSerialNo, parameters.NewState, parameters.HolderId);
            Exception = innerException;
        }

        public UpdateCardStateAsyncException(int shortCardModel, ulong cardSerialNo, string newState, int holderId, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateCardStateAsync;
            Description = string.Format(_message, shortCardModel, cardSerialNo, newState, holderId);
            Exception = innerException;
        }
    }

    // 1017
    public class UpdateTscRequestStateAsyncException : ExceptionContainer
    {
        private const string _message = "Failed to update TscRequest's ({0}) state ('{1}').";

        public UpdateTscRequestStateAsyncException(UpdateTscRequestStateParameters parameters, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateTscRequestStateAsync;
            Description = string.Format(_message, parameters.RequestId, parameters.NewState);
            Exception = innerException;
        }

        public UpdateTscRequestStateAsyncException(int tscRequestId, string newState, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateTscRequestStateAsync;
            Description = string.Format(_message, tscRequestId, newState);
            Exception = innerException;
        }
    }

    // 1018
    public class UpdateHolderStateAsyncException : ExceptionContainer
    {
        private const string _message = "Failed to update Holder's ({0}) state ('{1}').";

        public UpdateHolderStateAsyncException(decimal holderId, string newState, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateHolderStateAsync;
            Description = string.Format(_message, holderId, newState);
            Exception = innerException;
        }
    }

    // 1019
    public class GetCardsByHolderIDException : ExceptionContainer
    {
        private const string _message = "Failed to get card for Customer by Holder ID.";

        public GetCardsByHolderIDException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetCardsByHolderID;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1020
    public class GetCustomersByHolderIDException : ExceptionContainer
    {
        private const string _message = "Failed to get a customer by holder ID.";

        public GetCustomersByHolderIDException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetCustomersByHolderID;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1021
    public class GetProfileRequestCodeIDException : ExceptionContainer
    {
        private const string _message = "Failed to get a CRP.";

        public GetProfileRequestCodeIDException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileRequestCodeID;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1022
    public class GetProfileLayoutException : ExceptionContainer
    {
        private const string _message = "Failed to get Profile Layouts.";

        public GetProfileLayoutException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileLayout;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1023
    public class GetProfileRequestMapException : ExceptionContainer
    {
        private const string _message = "Failed to get Profiles MAP.";

        public GetProfileRequestMapException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetProfileRequestMap;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1024
    public class UpdateCustomerException : ExceptionContainer
    {
        private const string _message = "Failed to update customer.";

        public UpdateCustomerException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateCustomer;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1025
    public class GetBirthplacesByFilterException : ExceptionContainer
    {
        private const string _message = "Failed to get a list of birthplaces by filter.";

        public GetBirthplacesByFilterException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetBirthplacesByFilter;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1026
    public class MunicipalityListNotFoundException : ExceptionContainer
    {
        private const string _message = "Municipality list '{0}' not found.";

        public MunicipalityListNotFoundException(string fileName, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.MunicipalityListNotFound;
            Description = string.Format(_message, fileName);
            Exception = innerException;
        }
    }

    // 1027
    public class MissingShiftInformationException : ExceptionContainer
    {
        private const string _message = "Missing information about the shift.";

        public MissingShiftInformationException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.MissingShiftInformation;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1028
    public class UnauthorisedAccessToMifareKeysException : ExceptionContainer
    {
        private const string _message = "Unauthorised access to Mifare Keys prevented.";

        public UnauthorisedAccessToMifareKeysException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UnauthorisedAccessToMifareKeys;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1029
    public class CreateAgentException : ExceptionContainer
    {
        private const string _message = "Failed to create new agent.";

        public CreateAgentException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.CreateAgent;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1030
    public class UpdateAgentException : ExceptionContainer
    {
        private const string _message = "Failed to update new agent.";

        public UpdateAgentException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.UpdateAgent;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1031
    public class GetAgentIdException : ExceptionContainer
    {
        private const string _message = "Failed to get a new AgentId from SBME2.";

        public GetAgentIdException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GetAgentId;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1032
    public class BacpacGenerationInProgressException : ExceptionContainer
    {
        private const string _message = "There is a Bacpac generation process in progress.";

        public BacpacGenerationInProgressException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.BacpacGenerationInProgress;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1033
    public class BacpacGenerationException : ExceptionContainer
    {
        private const string _message = "Failed to generate Bacpac.";

        public BacpacGenerationException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.BacpacGeneration;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1034
    public class GettingBacpacException : ExceptionContainer
    {
        public GettingBacpacException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.GettingBacpac;
            Description = innerException?.Message;
            Exception = innerException;
        }
    }

    // 1035
    public class MandatoryFieldNotSpecifiedException : ExceptionContainer
    {
        private const string _message = "Mandatory field not specified: {0}";

        public MandatoryFieldNotSpecifiedException(string fieldName, Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.MandatoryFieldNotSpecified;
            Description = string.Format(_message, fieldName);
            Exception = innerException;
        }
    }

    // 1036
    public class FilterNotSpecifiedException : ExceptionContainer
    {
        private const string _message = "Filter not specified";

        public FilterNotSpecifiedException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.FilterNotSpecified;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1037
    public class OperatorNotSpecifiedException : ExceptionContainer
    {
        private const string _message = "Operator not specified";

        public OperatorNotSpecifiedException(Exception? innerException = null)
        {
            Code = (int)DsdeSrvExceptionCode.OperatorNotSpecified;
            Description = _message;
            Exception = innerException;
        }
    }

    // 1038
    public class BacpacBackgroundServiceNotReadyException : ExceptionContainer
    {
        private const string _message = "BacpacBackgroundService is not yet ready";

        public BacpacBackgroundServiceNotReadyException()
        {
            Code = (int)DsdeSrvExceptionCode.BacpacBackgroundServiceNotReady;
            Description = _message;
        }
    }
}
