namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _deleteAgent_AgentRole = @"DELETE FROM #SCHEME_SBME2_CONFOWN#.AGENT_AGENTROLE 
            WHERE AGENTID = :AgentId,
              AND AGENTROLE = :AgentRole";
    }
}
