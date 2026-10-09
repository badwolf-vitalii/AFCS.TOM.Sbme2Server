namespace AFCS.TOM.Sbme2Server.SBME
{
    static partial class Queries
    {
        private static string _ps(string query) => QueryHelper.PutSchemes(query);

        public static string GetCustomerByCF() => _ps(string.Format(_customerByCF));
        public static string InsertHolder() => _ps(string.Format(_insertHolder));
        public static string InsertTscRequest() => _ps(string.Format(_insertTscRequest));
        public static string UpdateHolder() => _ps(string.Format(_updateHolder));
        public static string UpdateCardStatus() => _ps(_updateCardStatus);
        public static string UpdateHolderStatus() => _ps(string.Format(_updateHolderStatus));
        public static string UpdateTscRequestStatus() => _ps(string.Format(_updateTscRequestStatus));
        public static string UpdateTscRequestCheckCode() => _ps(string.Format(_updateTscRequestCheckCode));
        public static string UpdateTscRequestCheckCodeWithStatus() => _ps(string.Format(_updateTscRequestCheckCodeWithStatus));
        public static string UpdateTscRequestHolderProfiles(string profiles) => _ps(string.Format(_updateTscRequestHolderProfiles, profiles));
        public static string UpdateCardEvd() => _ps(_updateCardEvd);
        public static string DeleteHolder() => _ps(string.Format(_deleteHolder));
        public static string GetNextCustomerId() => _ps(_nextCustomerId);
        public static string GetCustomerByName() => _ps(string.Format(_customerByName));
        public static string GetCustomerByFilter(string whereCondition) => _ps($"{_customerByFilter}{whereCondition} ORDER BY HOLDERID");
        public static string GetCustomerByHolderID() => _ps(_customerByHolderID);
        public static string GetHolderProfiles() => _ps(_getHolderProfiles);
        public static string GetAllHolderProfilesDescriptions() => _ps(_getAllHolderProfilesDescriptions);
        public static string GetProfileRequestID() => _ps(_profileRequestID);
        public static string GetProfileRequestWithMaps() => _ps(_profileRequestsWithMaps);
        public static string GetCard() => _ps(_card);
        public static string GetCardsByHolderID() => _ps(_cardsByHolderID);
        public static string ProfileRequestMap() => _ps(_profileRequestMap);
        public static string ProfileRequestMapByProfileId() => _ps(_profileRequestMapByProfileId);
        public static string Layouts() => _ps(_layouts);
        public static string GetLayoutsU() => _ps(_layoutsU);
        public static string GetShortCardModel() => _ps(_shortCardModel);
        public static string GetTSCDates() => _ps(_tscDates);
        public static string GetProfileValidityEndDate() => _ps(_profileValidityEndDate);
        public static string GetCustomerIdByLogicalSerialNumber() => _ps(_customerIdByLogicalSerialNumber);
        public static string GetCustomerIdByPhysicalSerialNumber() => _ps(_customerIdByPhysicalSerialNumber);
        public static string GetProfileRequestByProviderId() => _ps(_profileRequestByProviderId);
        public static string GetProfileRequestsByProviderIdWithMaps() => _ps(_profileRequestsByProviderIdWithMaps);
        public static string GetTDSDEContracts() => _ps(_getTDSDEContracts);
        public static string GetParametersSV_RV() => _ps(_getParametersSV_RV);
        public static string GetParametersDV() => _ps(_getParametersDV);
        public static string ClearTDSDEContracts() => _ps(_clearTDSDEContracts);
        public static string ClearParamsDv() => _ps(_clearParamsDv);
        public static string ClearParamsSvRv() => _ps(_clearParamsSvRv);
    }
}