namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _insertAgent_Devices = @"INSERT INTO #SCHEME_SBME2_CONFOWN#.AGENT_DEVICES (
            AGENTID, 
            OPERATORID,
            DEVICECLASSID,
            DEVICECODE
            )             
            VALUES 
            (
            :AgentId,
            :OperatorId,
            :DeviceClassId,
            :DeviceCode
            )";
    }
}
