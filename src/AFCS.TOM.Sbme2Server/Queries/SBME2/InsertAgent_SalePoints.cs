namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _insertAgent_SalePoints = @"INSERT INTO #SCHEME_SBME2_CONFOWN#.AGENT_SALEPOINTS (
            AGENTID, 
            SALEPOINTID
            )             
            VALUES 
            (
            :AgentId,
            :SalePointId
            )";
    }
}
