using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.SbmeModels.Enums;
using NLog;
using System.Diagnostics;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class KeysManager : IBglServiceBase
    {
        private static Logger Logger = LogManager.GetLogger("Sbme2Server");
        private DL.DataLayerContext _context { get; }
        private bool _requireShiftIdForGettingKeys { get; }

        public static bool Enabled { get; set; } = true;

        public KeysManager(IConfiguration configuration, DL.DataLayerContext context)
        {
            _context = context;
            _requireShiftIdForGettingKeys = configuration.GetValue<bool>("RequireShiftIdForGettingKeys");
        }

        public void SaveChanges()
        {
        }

        public Task<int> SaveChangesAsync() => Task.FromResult(0);

        private static byte _localDbKeyIndex;
        public static byte LocalDbKeyIndex
        {
            get => _localDbKeyIndex;
            set
            {
                var tmp = _localDbKeyIndex;
                _localDbKeyIndex = value;
                if (_localDbKeyIndex >= (LocalDbKeys?.Length ?? 0))
                {
                    _localDbKeyIndex = 0;
                }
                if (tmp != _localDbKeyIndex)
                {
                    Logger?.Info($"BglDbKey #{_localDbKeyIndex} activated");
                }
            }
        }

        public static byte[]? LocalDbKeys { get; set; }

        private static async Task<byte[]?> GetKey(string keyName)
        {
            if (!Enabled) return new byte[0];
            var root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? string.Empty;
            var keyDat = Path.Combine(root, "Keys", keyName);
            if (!File.Exists(keyDat))
            {
                Logger?.Error($"File not found: {keyDat}");
                return null;// throw new FileNotFoundException("File not found", keyDat);
            }
            return await File.ReadAllBytesAsync(keyDat);
        }

        private Task<byte[]> GetKey(Guid deviceShiftId, string keyName)
        {
            var usingCheats = deviceShiftId.Equals(new Guid("a0b1bc61-9ae9-4964-895f-0089dc735f97"));
            if (usingCheats || !_requireShiftIdForGettingKeys)
            {
                return GetKey(keyName);
            }
            if (!_context.DeviceShifts.Any(p => p.Id.Equals(deviceShiftId) && p.State != (byte)BglMachineShiftState.Closed))
            {
                throw new UnauthorisedAccessToMifareKeysException();
            }
            return GetKey(keyName);
        }

        private static async Task<string?> GetKeyV2(string keyName)
        {
            if (!Enabled) return string.Empty;
            var root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? string.Empty;
            var keyDat = Path.Combine(root, "Keys", keyName);
            if (!File.Exists(keyDat))
            {
                Logger?.Error($"File not found: {keyDat}");
                return null;// throw new FileNotFoundException("File not found", keyDat);
            }
            return await File.ReadAllTextAsync(keyDat);
        }

        private Task<string?> GetKeyV2(Guid deviceShiftId, string keyName)
        {
            var usingCheats = deviceShiftId.Equals(new Guid("a0b1bc61-9ae9-4964-895f-0089dc735f97"));
            if (usingCheats || !_requireShiftIdForGettingKeys)
            {
                return GetKeyV2(keyName);
            }
            if (!_context.DeviceShifts.Any(p => p.Id.Equals(deviceShiftId) && p.State != (byte)BglMachineShiftState.Closed))
                throw new UnauthorisedAccessToMifareKeysException();
            return GetKeyV2(keyName);
        }

        private static byte _keyLogged1 { get; set; } = byte.MaxValue;

        public static Task<byte[]?> GetLocalDbKey()
        {
            var key = LocalDbKeys?[LocalDbKeyIndex] ?? 0;
            if (_keyLogged1 != key)
            {
                Logger?.Info($"Loading key-{key}");
                _keyLogged1 = key;
            }
            if (key > 0) return GetKey($"BGLSolutionKey{LocalDbKeys![LocalDbKeyIndex]}.dat");
            return GetKey("BGLSolutionKey.dat");
        }

        public Task<byte[]> GetMifareKeys(Guid deviceShiftId) => GetKey(deviceShiftId, "kbDLL.dat");
        
        public Task<string?> GetMifareKeysV2(Guid deviceShiftId) => GetKeyV2(deviceShiftId, File.Exists("Keys\\kbDLLv2.dat") ? "kbDLLv2.dat" : "kbDLL.dat");

        public Task<byte[]> GetSamKeys(Guid deviceShiftId) => GetKey(deviceShiftId, "SamKeys.dat");
    }
}
