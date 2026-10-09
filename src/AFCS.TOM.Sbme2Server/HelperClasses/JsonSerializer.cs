using AFCS.TOM.Sbme2Server.Configurations;
using Newtonsoft.Json;

namespace AFCS.TOM.Sbme2Server.HelperClasses
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

        public static T? RemoveSerializationCycle<T>(ref T data) where T : class, new() => data = RemoveSerializationCycle(data);

        public static T? RemoveSerializationCycle<T>(T data) where T : class, new()
        {
            var serialized = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            return JsonConvert.DeserializeObject<T>(serialized);
        }
    }
}
