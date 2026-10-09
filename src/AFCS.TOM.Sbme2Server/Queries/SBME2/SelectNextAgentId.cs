namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _nextAgentId = @"SELECT agentid_seq.NEXTVAL FROM #SCHEME_SBME2_CONFOWN#.AGENTS WHERE ROWNUM <= 1";
    }
}
