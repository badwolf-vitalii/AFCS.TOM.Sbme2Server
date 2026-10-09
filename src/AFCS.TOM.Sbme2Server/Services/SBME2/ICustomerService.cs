using AFCS.TOM.SbmeModels.SBME2;

namespace AFCS.TOM.Sbme2Server.Services.SBME2
{
    public interface ICustomerService
    {
        public Task<uint> CreateCustomer(string connectionString, RequestBase<Customer> customer);
        public Task<IList<Customer>> GetCustomersByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters);
        public Task<Customer> GetCustomerByHolderID(string connectionString, uint holderId, bool withPhoto = true);
        public Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null);
        public Task<IList<RelatedCustomer>> GetRelatedCustomers(string connectionString, RequestBase<HolderIdRequest> filters);
        public Task<uint> CreateCustomerAttachment(string connectionString, RequestBase<CustomerAttachment> attachment);
        public Task<bool> UpdateCustomer(string connectionString, uint holderId, RequestBase<IList<SearchFilter>> attachment);
        public Task<IList<Agent>> GetAgentsByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters);
        public Task<short> CreateAgent(string connectionString, RequestBase<Agent> agent);
        public Task<List<AgentProfile>> GetAgentRoles(string connectionString, RequestBase<AgentProfileIdRequest> filters);
        public Task<List<HolderProfile>> GetAllHolderProfilesDescriptions(string connectionString);
        public Task<bool> UpdateAgent(string connectionString, Agent agent, RequestBase<IList<SearchFilter>> filters);
        public Task<int> GetAgentId(string connectionString);
    }

    public class EmptyCustomerService : ICustomerService
    {
        public Task<short> CreateAgent(string connectionString, RequestBase<Agent> agent)
        {
            throw new NotImplementedException();
        }

        public Task<uint> CreateCustomer(string connectionString, RequestBase<Customer> customer)
        {
            throw new NotImplementedException();
        }

        public Task<uint> CreateCustomerAttachment(string connectionString, RequestBase<CustomerAttachment> attachment)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetAgentId(string connectionString)
        {
            throw new NotImplementedException();
        }

        public Task<List<AgentProfile>> GetAgentRoles(string connectionString, RequestBase<AgentProfileIdRequest> filters)
        {
            throw new NotImplementedException();
        }

        public Task<List<HolderProfile>> GetAllHolderProfilesDescriptions(string connectionString)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Agent>> GetAgentsByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            throw new NotImplementedException();
        }

        public Task<Customer> GetCustomerByHolderID(string connectionString, uint holderId, bool withPhoto = true)
        {
            throw new NotImplementedException();
        }

        public Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Customer>> GetCustomersByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            throw new NotImplementedException();
        }

        public Task<IList<RelatedCustomer>> GetRelatedCustomers(string connectionString, RequestBase<HolderIdRequest> filters)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateAgent(string connectionString, Agent agent, RequestBase<IList<SearchFilter>> filters)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateCustomer(string connectionString, uint holderId, RequestBase<IList<SearchFilter>> attachment)
        {
            throw new NotImplementedException();
        }
    }
}
