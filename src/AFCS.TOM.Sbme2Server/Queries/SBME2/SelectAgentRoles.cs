namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _agentRoles = @"SELECT AgentRole Id, Description, LanguageId, PswPinDuration PwdExpiration
FROM 
(
SELECT AR.AgentRole, AR.Description, CDD.LanguageId, AR.PswPinDuration, AAR.AgentId ProfileId
FROM AGENTROLES AR 
INNER JOIN AGENT_AGENTROLE AAR ON AR.AgentRole = AAR.AgentRole 
INNER JOIN CONFDBDESCRIPTIONS CDD ON CDD.DescId = AR.RoleDescId AND CDD.DescClass = 21
) T ";    
    }
}
