namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private static string _ps(string query) => QueryHelper.PutSchemes(query);

        public static string GetCustomerByFilter(string whereCondition, bool withPhoto = true) => _ps($"{string.Format(_customerByFilter, withPhoto ? "photo," : string.Empty)}{whereCondition}");
        public static string GetCustomerByHolderID(bool withPhoto = true) => _ps(string.Format(_customerByHolderID, withPhoto ? "photo," : string.Empty));
        public static string GetCustomerIdByLogicalSerialNumber() => _ps(_customerIdByLogicalSerialNumber);
        public static string GetCustomerIdByPhysicalSerialNumber() => _ps(_customerIdByPhysicalSerialNumber);
        public static string InsertCustomer(bool holderIdSpecified = true) => _ps(string.Format(_insertCustomer, holderIdSpecified ? ":HolderId" : "holderid_seq.NEXTVAL"));
        public static string InsertHolderSignature() => _ps(_insertHolderSignature);
        public static string UpdateHolder() => _ps(string.Format(_updateHolder));
        public static string UpdateHolderSignature() => _ps(string.Format(_updateHolderSignature));
        public static string GetHolderSignatureId() => _ps(string.Format(_signatureIdByHolderID));
        public static string GetHolderSignature() => _ps(string.Format(_signature));
        public static string GetRelatedCustomers(string whereCondition) => _ps($"{_relatedcustomerByHolderId}{whereCondition}");
        public static string InsertCustomerAttachment() => _ps(string.Format(_insertAttachment));
        public static string GetMediaByHolderId(string whereCondition) => _ps($"{_mediaByHolderId}{whereCondition}");
        public static string GetMediaByProfileAndBlReason() => _ps(_cardByProfileAndBlReason);
        public static string InsertMedia() => _ps(string.Format(_insertMedia));
        public static string GetAgentByFilter(string whereCondition) => _ps($"{_agentByFilter}{whereCondition}");
        //public static string InsertAgent(bool agentIdSpecified = true) => _ps(string.Format(_insertAgent, agentIdSpecified ? ":AgentId" : AgentIdSeqNextVal()));
        public static string InsertAgent(bool agentIdSpecified = true) => _ps(string.Format(_insertAgent, agentIdSpecified ? ":AgentId" : "AGENTID_SEQ.NEXTVAL"));
        public static string AgentIdSeqNextVal() => _ps(string.Format(_selectNextSeqVal, "AgentId", "ATMPCONFOWN", "AGENTS"));
        public static string GetNextAgentId() => _ps(_nextAgentId);
        public static string GetAgentProfiles(string whereCondition) => _ps($"{_agentProfiles}{whereCondition}");
        public static string GetAgentRoles(string whereCondition) => _ps($"{_agentRoles}{whereCondition}");
        public static string InsertAgent_AgentRole() => _ps(_insertAgent_AgentRole);
        public static string DeleteAgent_AgentRole(string whereConditionAgentId, string whereConditionRoleId) => _ps($"{_deleteAgent_AgentRole}{whereConditionAgentId}{whereConditionRoleId}");
        public static string InsertAgent_SalePoints() => _ps(_insertAgent_SalePoints);
        public static string DeleteAgent_SalePoints(string whereConditionAgentId, string whereConditionSalePointId) => _ps($"{_deleteAgent_SalePoints}{whereConditionAgentId}{whereConditionSalePointId}");
        public static string InsertAgent_Devices() => _ps(_insertAgent_Devices);
        public static string DeleteAgent_Devices(string whereConditionAgentId, string whereConditionDeviceId) => _ps($"{_deleteAgent_Devices}{whereConditionAgentId}{whereConditionDeviceId}");
        public static string UpdateAgent() => _ps(_updateAgent);
        public static string AllHolderProfilesDescriptions() => _ps(_allHolderProfilesDescriptions);

        #region Media
        public static string InsertTSCDocumentInHistory() => _ps(_insertTSCDocumentInHistory);
        public static string InsertTSCDocumentInHistoryDetail() => _ps(_insertTSCDocumentInHistoryDetail);
        public static string ResetMediaBl() => _ps(_resetMediaBl);
        public static string ResetMediaStatus() => _ps(_updateMediaStatus);
        public static string UpdateTSCDocument() => _ps(_updateTSCDocument);
        public static string UpdateTSCDocumentProofDoc() => _ps(_updateTSCDocumentProofDoc);
        public static string SelectTSCDocument() => _ps(_selectTSCDocument);
        public static string InsertTSCDocumentInBlackList() => _ps(_insertTSCDocumentInBlackList);
        public static string InsertContractInBlackList() => _ps(_insertContractInBlackList);
        public static string InsertLongTermContractInHistory() => _ps(_insertLongTermContractInHistory);
        public static string InsertShortTermContractInHistory() => _ps(_insertShortTermContractInHistory);
        public static string UpdateLongTermContracts() => _ps(_updateLongTermContracts);
        public static string UpdateShortTermContracts() => _ps(_updateShortTermContracts);
        public static string SelectReasonCode() => _ps(_selectReasonCode);
        public static string DeleteCardFromBlackList() => _ps(_deleteCardFromBlackList);
        public static string GetShortCardModel() => _ps(_shortCardModel);
        public static string GetMediaLoSn() => _ps(_mediaLoSn);
        public static string GetMediaPhSn() => _ps(_mediaPhSn);
        #endregion
    }
}
