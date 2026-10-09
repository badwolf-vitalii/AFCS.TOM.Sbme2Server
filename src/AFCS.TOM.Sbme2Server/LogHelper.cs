using AFCS.TOM.Sbme2Server.Configurations;
using NLog;
using System.Runtime.CompilerServices;

namespace AFCS.TOM.Sbme2Server
{
    public static class LogHelper
    {
        public enum LogType
        {
            Debug,
            Info,
            Error,
            Warning,
            Fatal,
            Trace
        }

        private static DebuggingConfiguration? _debugConfig { get; set; }
        private static DebuggingConfiguration? DebugConfig
        {
            get
            {
                try
                {
                    var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                    _debugConfig = config?.GetSection("DebuggingConfiguration")?.Get<DebuggingConfiguration>();
                }
                catch
                {
                }
                return _debugConfig ?? DebuggingConfiguration.Default;
            }
        }

        public static void Debug<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Debug, logger, ex, lineNumber, caller);
        public static void Info<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Info, logger, ex, lineNumber, caller);
        public static void Error<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Error, logger, ex, lineNumber, caller);
        public static void Warning<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Warning, logger, ex, lineNumber, caller);
        public static void Fatal<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Fatal, logger, ex, lineNumber, caller);
        public static void Trace<T>(Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) => Log(LogType.Trace, logger, ex, lineNumber, caller);

        public static void Log<T>(LogType logType, Logger logger, T ex, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null)
        {
            if (logger != null) LogCallerInfo(logger, lineNumber, caller);

            if (!string.IsNullOrWhiteSpace(DebugConfig?.WLog))
            {
                try
                {
                    var mode = File.Exists(DebugConfig.WLog) ? FileMode.Append : FileMode.OpenOrCreate;
                    using (var fileStream = new FileStream(DebugConfig.WLog, mode))
                    using (var streamWriter = new StreamWriter(fileStream))
                    {
                        streamWriter.Write(DateTime.Now.ToString($"yyyy-MM-dd HH:mm:ss.fff {logType}: "));
                        streamWriter.WriteLine(ex?.ToString() ?? "NULL");
                        streamWriter.Close();
                    }
                }
                catch
                {
                }
            }

            switch (logType)
            {
                case LogType.Debug:
                    logger?.Debug(ex);
                    break;
                case LogType.Info:
                    logger?.Info(ex);
                    break;
                case LogType.Error:
                    logger?.Error(ex);
                    break;
                case LogType.Warning:
                    logger?.Warn(ex);
                    break;
                case LogType.Fatal:
                    logger?.Fatal(ex);
                    break;
                case LogType.Trace:
                    logger?.Trace(ex);
                    break;
            }

            if (logger != null && ex is Exception exc && exc.InnerException != null)
                Log(logType, logger, exc.InnerException, lineNumber, caller);
        }

        public static void LogCallerInfo(Logger logger, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? caller = null) =>
            logger?.Debug($"{caller}: line {lineNumber}");
    }
}
