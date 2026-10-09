using AFCS.TOM.SbmeModels.SBME2;

namespace AFCS.TOM.Sbme2Server.Services.SBME2
{
    public class CustomerService : ICustomerService
    {
        public async Task<uint> CreateCustomer(string connectionString, RequestBase<Customer> customer)
        {
            //TODO
            return await DBOracleManager2.CreateCustomer(connectionString, customer);
        }

        public async Task<IList<Customer>> GetCustomersByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            //TODO
            return await DBOracleManager2.GetCustomersByFilter(connectionString, filters).ConfigureAwait(false);
        }

        public async Task<Customer> GetCustomerByHolderID(string connectionString, uint holderID, bool withPhoto = true) =>
            await DBOracleManager2.GetCustomerByHoldderIDAsync(connectionString, holderID, withPhoto);

        public async Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null) =>
            await DBOracleManager2.GetCustomerIdByCardSerialNumber(connectionString, sn, shortCardModel, saleDeviceid);
        
        public async Task<IList<RelatedCustomer>> GetRelatedCustomers(string connectionString, RequestBase<HolderIdRequest> filters)
        {
            //TODO
            return await DBOracleManager2.GetRelatedCustomers(connectionString, filters).ConfigureAwait(false);
        }

        public async Task<uint> CreateCustomerAttachment(string connectionString, RequestBase<CustomerAttachment> attachment)
        {
            //TODO
            return await DBOracleManager2.CreateCustomerAttachment(connectionString, attachment);
        }

        public async Task<bool> UpdateCustomer(string connectionString, uint holderId, RequestBase<IList<SearchFilter>> attachment) =>
            await DBOracleManager2.UpdateCustomer(connectionString, holderId, attachment);

        public async Task<short> CreateAgent(string connectionString, RequestBase<Agent> agent)
        {
            //TODO
            return await DBOracleManager2.CreateAgent(connectionString, agent);
        }
        public async Task<IList<Agent>> GetAgentsByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            //TODO
            return await DBOracleManager2.GetAgentsByFilter(connectionString, filters).ConfigureAwait(false);
        }

        public async Task<bool> UpdateAgent(string connectionString, Agent agent, RequestBase<IList<SearchFilter>> filters)
        {
            //TODO
            return await DBOracleManager2.UpdateAgent(connectionString, agent, filters);
        }

        public async Task<int> GetAgentId(string connectionString)
        {
            //TODO
            return await DBOracleManager2.GetAgentId(connectionString);
        }

        public async Task<List<AgentProfile>> GetAgentRoles(string connectionString, RequestBase<AgentProfileIdRequest> filters)
        {
            //TODO
            return await DBOracleManager2.GetAgentRoles(connectionString, filters);
        }

        public async Task<List<HolderProfile>> GetAllHolderProfilesDescriptions(string connectionString)
        {
            //TODO
            return await DBOracleManager2.GetAllHolderProfilesDescriptions(connectionString);
        }
    }
}
