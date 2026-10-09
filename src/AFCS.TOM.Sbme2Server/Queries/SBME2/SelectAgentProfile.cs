namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _agentProfiles = @"SELECT Id, Description, LanguageId
FROM 
(
SELECT A.AgentId Id, AR.Description, CDD.LanguageId
FROM AGENTROLES AR 
INNER JOIN AGENT_AGENTROLE AAR ON AR.AgentRole = AAR.AgentRole
INNER JOIN AGENTS A ON A.AgentId = AAR.AgentId
INNER JOIN CONFDBDESCRIPTIONS CDD ON CDD.DescId = AR.RoleDescId AND CDD.DescClass = 21 
WHERE A.AgentId > 0 AND A.AgentId < 256) P ";
    }
}
