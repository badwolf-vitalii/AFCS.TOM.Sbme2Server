using Oracle.ManagedDataAccess.Client;

namespace AFCS.TOM.Sbme2Server
{
    public static class OracleCommandWrapper
    {
        public static string ToString(OracleCommand command, string query)
        {
            if (command == null || query == null || (command.Parameters?.Count ?? 0) == 0) return query?.ToLower() ?? string.Empty;

            query = query.ToLower();
            foreach (OracleParameter? parameter in command.Parameters)
            {
                if (parameter == null) continue;
                query = query.Replace($":{parameter.ParameterName.ToLower()}", parameter.Value?.ToString()?.ToLower() ?? "null");
            }
            return query;
        }
    }  
}
