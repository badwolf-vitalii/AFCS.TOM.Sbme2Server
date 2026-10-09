using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Reflection;

namespace AFCS.TOM.Sbme2Server
{
    /// <summary>
    /// 
    /// </summary>
    public class Helper
    {
        public static class JsonSerializer
        {
            private static DebuggingConfiguration _debugConfig { get; set; }
            public static DebuggingConfiguration DebugConfig
            {
                get => _debugConfig ?? DebuggingConfiguration.Default;
                set => _debugConfig = value;
            }

            //#if DEBUG
            private static double _lastUsedSeconds { get; set; }

            public static void SerializeData<T>(T data, string typeName = null)
            {
                if (!DebugConfig.SerializeData) return;

                try
                {
                    #region Generate file name
                    var root = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;

                    var serializationDirectory = $@"{root}\Serialization";
                    if (!Directory.Exists(serializationDirectory)) Directory.CreateDirectory(serializationDirectory);
                    var type = typeName ?? data.GetType().ToString();
                    var date = DateTime.Today.ToString("dd-MM-yyyy");
                    var seconds = (DateTime.Now - DateTime.Today).TotalSeconds;
                    if (seconds == _lastUsedSeconds)
                    {
                        Task.Delay(1000).Wait();
                        ++seconds;
                    }
                    _lastUsedSeconds = seconds;
                    var path = $@"{serializationDirectory}\{date}\{seconds}_{type}.json";
                    var dir = Directory.GetParent(path).FullName;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    #endregion

                    var serialized = JsonConvert.SerializeObject(data);

                    File.WriteAllText(path, serialized);
                }
                catch
                {
                }
            }
            //#else
            //public static void SerializeData<T>(T data) { }
            //#endif
        }

        /// <summary>
        /// 
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        public class QueryStringConstraintAttribute : ActionMethodSelectorAttribute
        {
            public string ValueName { get; set; }
            public bool ValuePresent { get; set; }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="valueName"></param>
            /// <param name="valuePresent"></param>
            public QueryStringConstraintAttribute(string valueName, bool valuePresent)
            {
                ValueName = valueName;
                ValuePresent = valuePresent;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="routeContext"></param>
            /// <param name="action"></param>
            /// <returns></returns>
            public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
            {
                var value = routeContext.HttpContext.Request.Query[ValueName];
                return ValuePresent ? !StringValues.IsNullOrEmpty(value) : StringValues.IsNullOrEmpty(value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static string AssemblyVer
        {
            get
            {
                var version = Assembly.GetExecutingAssembly()?.GetName()?.Version;
                if (version == null) return "Unknown";
                var result = $"{version.Major}.{version.Minor}.{version.Build}";

                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                var launchSettings = new LaunchSettings();
                config.GetSection("LaunchSettings").Bind(launchSettings);
                if (launchSettings.NodeId.HasValue)
                    result = result + $"(node {launchSettings.NodeId.Value})";

                return result;
            }
        }

        public static OkObjectResult I_am_a_teapot(ControllerBase controller, object? value)
        {
            var result = controller.Ok(value);
            result.StatusCode = 418;
            return result;
        }

        public static OkObjectResult I_am_a_teapot(ControllerBase controller, ExceptionContainer ec)
        {
            var serializedEx = JsonConvert.SerializeObject(ec);
            var result = controller.Ok(serializedEx);
            result.StatusCode = 418;
            return result;
        }

        public static DateTime GetMidnight(DateTime date) => new DateTime(date.Year, date.Month, date.Day);

        public static DateTime? GetMidnight(DateTime? date, int shiftHours = 0) => date.HasValue ? GetMidnight(new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, date.Value.Hour, date.Value.Minute, date.Value.Second).AddHours(shiftHours)) : (DateTime?)null;
    }
}
