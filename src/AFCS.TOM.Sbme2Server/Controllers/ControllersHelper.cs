using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels.Enums;
using System.ComponentModel;
using System.Reflection;

namespace AFCS.TOM.Sbme2Server.Controllers
{
    public static class ControllersHelper
    {
        public static string GetConnectionString(IConfiguration configuration, ConnectionString connection)
        {
            var fi = connection.GetType().GetField(connection.ToString());
            var attributes = fi?.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            var name = (attributes?.Any() ?? false) ? attributes.First().Description : connection.ToString();
            return configuration.GetConnectionString(name);
        }

        public static string AssemblyVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly()?.GetName()?.Version;
                if (version == null) return "Unknown";
                var result = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

                if (NodeId > 0)
                    result = result + $" [{NodeId}]";

                return result;
            }
        }

        private static int? _nodeId;
        public static int NodeId
        {
            get
            {
                if (_nodeId.HasValue) return _nodeId.Value;
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                var launchSettings = new LaunchSettings();
                config.GetSection("LaunchSettings").Bind(launchSettings);
                _nodeId = launchSettings.NodeId ?? 0;
                return _nodeId.Value;
            }
        }
    }
}
