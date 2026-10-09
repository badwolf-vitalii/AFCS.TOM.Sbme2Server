namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _deleteAgent_SalePoints = @"DELETE FROM #SCHEME_SBME2_CONFOWN#.AGENT_SALEPOINTS 
            WHERE AGENTID = :AgentId, 
              AND SALEPOINTID = :SalePointId";
    }
}
