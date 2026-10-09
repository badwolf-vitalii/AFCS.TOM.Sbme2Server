using AFCS.TOM.Sbme2Server.Configurations;

namespace AFCS.TOM.Sbme2Server
{
    public static class QueryHelper
    {
        private static Dictionary<string, string> SchemePlaceholders { get; }

        static QueryHelper()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            var connectionStrings = new ConnectionStrings();
            config.GetSection("ConnectionStrings").Bind(connectionStrings);

            SchemePlaceholders = new Dictionary<string, string>()
            {
                { "#SCHEME_SG_GESTOWN#", GetSchemeName(connectionStrings.SGGESTOWN) },
                { "#SCHEME_SG_CONFOWN#", GetSchemeName(connectionStrings.SGCONFOWN) },

                { "#SCHEME_SBME_GESTOWN#", GetSchemeName(connectionStrings.SBMEGESTOWN) },
                { "#SCHEME_SBME_TARIFFOWN#", GetSchemeName(connectionStrings.SBMETARIFFOWN) },

                { "#SCHEME_SBME2_GESTOWN#", GetSchemeName(connectionStrings.GESTOWN2) },
                { "#SCHEME_SBME2_TARIFFOWN#", GetSchemeName(connectionStrings.TARIFFOWN2) },
                { "#SCHEME_SBME2_CONFOWN#", GetSchemeName(connectionStrings.CONFOWN2) },
                { "#SCHEME_SBME2_TARIFFOWN_CONFOWN#", GetSchemeName(connectionStrings.TARIFFOWN_CONFOWN2) }
            };
        }

        private static string GetSchemeName(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return string.Empty;
            var cs = connectionString
                .Replace(" ", string.Empty)
                .Split(';')
                .Where(p => !string.IsNullOrEmpty(p) && p.IndexOf('=') > 0)
                .Select(p => {
                    var split = p.Split('=');
                    return new KeyValuePair<string, string>(split[0].ToLower(), string.Join('=', split.Skip(1)));
                })
                .ToDictionary(p => p.Key, p => p.Value);
            return cs.TryGetValue("userid", out var value) ? value : string.Empty;
        }

        public static string? PutSchemes(string? query)
        {
            if (string.IsNullOrWhiteSpace(query) || (SchemePlaceholders?.Count ?? 0) == 0) return query;
            foreach (var placeholder in SchemePlaceholders.Keys)
                query = query.Replace(placeholder, SchemePlaceholders[placeholder]);
            return query;
        }
    }
}
