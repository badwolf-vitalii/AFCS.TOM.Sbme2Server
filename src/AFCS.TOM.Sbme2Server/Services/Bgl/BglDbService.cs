using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.BGL;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.VtCashFlow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.SqlServer.Dac;
using Newtonsoft.Json;
using NLog;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class BglDbService : IBglDbService
    {
        public static bool IsBacpacGenerationActive { get; private set; }
        private Logger _logger = LogManager.GetLogger("Sbme2Server");
        private DL.DataLayerContext _context { get; }
        private BglDataLayerConfiguration _bglDataLayerConfiguration { get; }
        private SalesThresholdConfiguration _salesThresholdConfiguration { get; }
        public bool IsServiceEnabled { get; }
        private ReceiptsManager _receiptsManager { get; }
        private int HourMinuteChangeOperatingDay { get; }

        public BglDbService(IConfiguration configuration, ReceiptsManager receiptsManager, DL.DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            var bglDataLayerConfiguration = new BglDataLayerConfiguration();
            configuration.GetSection("BglDataLayerConfiguration").Bind(bglDataLayerConfiguration);
            var salesThresholdConfiguration = new SalesThresholdConfiguration();
            configuration.GetSection("SalesThresholdConfiguration").Bind(salesThresholdConfiguration);
            var hourMinuteChangeOperatingDay = configuration.GetValue<int>("HourMinuteChangeOperatingDay");
            if (hourMinuteChangeOperatingDay == 0) hourMinuteChangeOperatingDay = 03_45;
            HourMinuteChangeOperatingDay = hourMinuteChangeOperatingDay;
            _bglDataLayerConfiguration = bglDataLayerConfiguration;
            _salesThresholdConfiguration = salesThresholdConfiguration;
            IsServiceEnabled = launchSettings.BglServicesEnabled;
            _context = context;
            _receiptsManager = receiptsManager;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        #region DB
        public async Task<bool> AddDbVersionChangeLog(int dbVersion, string? changeLog)
        {
            changeLog = changeLog?.Replace("\\r", "\r")?.Replace("\\n", "\n");
            DatabaseInfo? dbInfo = null;
            try
            {
                dbInfo = _context.DatabaseInfos.FirstOrDefault(p => p.Version == dbVersion);
            }
            catch
            {
                return false;
            }
            if (dbInfo == null) return false;
            dbInfo.ChangeLog = changeLog;
            return true;
        }

        public async Task<bool> UpdateDbVersionDateTime(int dbVersion, DateTime? dateTime)
        {
            DatabaseInfo? dbInfo = null;
            try
            {
                dbInfo = _context.DatabaseInfos.FirstOrDefault(p => p.Version == dbVersion);
            }
            catch
            {
                return false;
            }
            if (dbInfo == null) return false;
            if (!dateTime.HasValue) dateTime = DateTime.Now;
            dbInfo.LastModified = dateTime;
            return true;
        }

        public Task<DatabaseInfo?> GetDatabaseInfo(int dbVersion) => GetDatabaseInfo(_context, dbVersion);

        public Task<DatabaseInfo?> GetDatabaseInfo(DL.DataLayerContext context, int dbVersion)
        {
            try
            {
                return Task.FromResult(context.DatabaseInfos.FirstOrDefault(p => p.Version == dbVersion));
            }
            catch
            {
                return Task.FromResult((DatabaseInfo?)null);
            }
        }

        public Task<DatabaseInfo?> GetLastDatabaseInfo() => GetLastDatabaseInfo(_context);

        public Task<DatabaseInfo?> GetLastDatabaseInfo(DL.DataLayerContext context)
        {
            var max = 0;
            try
            {
                max = context.DatabaseInfos.Max(p => p.Version);
            }
            catch
            {
                return Task.FromResult((DatabaseInfo?)null);
            }
            var dbInfo = GetDatabaseInfo(context, max);
            return dbInfo;
        }

        public Task<DatabaseInfo?> GetNewDatabaseInfo(int dbVersion) => GetNewDatabaseInfo(_context, dbVersion);

        public Task<DatabaseInfo?> GetNewDatabaseInfo(DL.DataLayerContext context, int dbVersion)
        {
            try
            {
                return Task.FromResult(context.DatabaseInfos.Where(p => p.Version > dbVersion).OrderBy(p => p.Version).FirstOrDefault());
            }
            catch
            {
                return Task.FromResult((DatabaseInfo?)null);
            }
        }

        private DL.DataLayerContext GetDbContext()
        {
            var context = _context;
            try
            {
                LogHelper.Debug(_logger, "Getting DB connection");
                _ = _context.Database.GetDbConnection();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);

                try
                {
                    var connectionString = _bglDataLayerConfiguration.TrustedConnection
                        ? $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                        : $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database};Trusted_Connection=false;user={_bglDataLayerConfiguration.User};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=false;";
                    LogHelper.Error(_logger, $"Trying with new connection string: {connectionString}");
                    connectionString = connectionString.Replace("#PASSWORD#", _bglDataLayerConfiguration.Password);
                    if (File.Exists("LogConnectionString")) _logger.Debug($"ConnectionString: {connectionString}");
                    context = new DataLayerContext(connectionString);
                    _ = context.Database.GetDbConnection();
                }
                catch
                {
                }
            }
            return context;
        }

        public async Task<bool> UpdateDatabase(bool oneStepUpdate, int? dbVersion)
        {
            var updated = false;

            DatabaseInfo? dbInfo = null;

            var context = GetDbContext();

            try
            {
                if (dbVersion.HasValue)
                    dbInfo = await GetNewDatabaseInfo(context, dbVersion.Value - 1);
                else
                    dbInfo = await GetLastDatabaseInfo(context);
            }
            catch
            {
            }

            if (dbInfo == null)
                dbInfo = new DatabaseInfo {
                    Version = 0
                };

            var updatesPath = _bglDataLayerConfiguration.SqlSrv2014 ? "BglDbUpdate2014" : "BglDbUpdate";
            var availableVersions = !Directory.Exists(updatesPath) ? new List<int>()
                : Directory.GetFiles(updatesPath, "*.sql", SearchOption.TopDirectoryOnly)
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(p => int.TryParse(p, out var ver) && ver > dbInfo.Version)
                .Select(p => int.Parse(p))
                .ToList();

            if (availableVersions.Count == 0) return true;

            bool Update(int version)
            {
                var optional = false;
                const string optionalTag = "[OPTIONAL]";
                try
                {
                    var updatesPath = _bglDataLayerConfiguration.SqlSrv2014 ? "BglDbUpdate2014" : "BglDbUpdate";
                    var cmdFileName = Path.Combine(updatesPath, $"{version}.sql");
                    var cmdText = File.ReadAllText(cmdFileName);
                    optional = cmdText.StartsWith(optionalTag, StringComparison.InvariantCultureIgnoreCase);
                    if (optional)
                        cmdText = cmdText.Substring(optionalTag.Length);

                    context.Database.OpenConnection();
                    try
                    {
                        using var transaction = context.Database.BeginTransaction();
                        using var command = context.Database.GetDbConnection().CreateCommand();
                        command.Transaction = transaction.GetDbTransaction();
                        command.CommandText = cmdText;
                        LogHelper.Debug(_logger, $"Executing query {cmdFileName}");
                        command.ExecuteNonQuery();
                        transaction.Commit();
                        return true;
                    }
                    finally
                    {
                        context.Database.CloseConnection();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, ex);
                    return optional ? UpdateOnlyDatabaseInfo(version) : false;
                }
            }

            bool UpdateOnlyDatabaseInfo(int version, string description = "[OPTIONAL] Update Failed.")
            {
                try
                {
                    context.Database.OpenConnection();
                    try
                    {
                        using var transaction = context.Database.BeginTransaction();
                        using var command = context.Database.GetDbConnection().CreateCommand();
                        command.Transaction = transaction.GetDbTransaction();
                        command.CommandText = "INSERT INTO [dbo].[DatabaseInfo] ([Version], [LastModified], [ChangeLog]) VALUES (@version, @modified, @description)";
                        var versionParameter = command.CreateParameter();
                        versionParameter.ParameterName = "@version";
                        versionParameter.Value = version;
                        command.Parameters.Add(versionParameter);
                        var modifiedParameter = command.CreateParameter();
                        modifiedParameter.ParameterName = "@modified";
                        modifiedParameter.Value = DateTime.Now;
                        command.Parameters.Add(modifiedParameter);
                        var descriptionParameter = command.CreateParameter();
                        descriptionParameter.ParameterName = "@description";
                        descriptionParameter.Value = description;
                        command.Parameters.Add(descriptionParameter);
                        command.ExecuteNonQuery();
                        transaction.Commit();
                        return true;
                    }
                    finally
                    {
                        context.Database.CloseConnection();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, ex);
                    return false;
                }
            }

            try
            {
                var min = availableVersions.Min();
                updated = Update(min);
                if (updated) availableVersions.Remove(min);
            }
            catch
            {
                return false;
            }

            while (updated && !oneStepUpdate && availableVersions.Count > 0)
            {
                try
                {
                    var min = availableVersions.Min();
                    updated = Update(min);
                    if (updated) availableVersions.Remove(min);
                }
                catch
                {
                    break;
                }
            }
            return updated;
        }

        public void GenerateBacpac()
        {
            try
            {
                if (IsBacpacGenerationActive)
                    throw new BacpacGenerationInProgressException();
                IsBacpacGenerationActive = true;

                var connectionString = _bglDataLayerConfiguration.TrustedConnection
                        ? $"Server={_bglDataLayerConfiguration.Server};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                        : $"Server={_bglDataLayerConfiguration.Server};Trusted_Connection=false;user={_bglDataLayerConfiguration.User};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=false;";
                LogHelper.Error(_logger, $"Trying with new connection string: {connectionString}");
                connectionString = connectionString.Replace("#PASSWORD#", _bglDataLayerConfiguration.Password);

                var ds = new DacServices(connectionString);
                var outputFileName = _bglDataLayerConfiguration.Database + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss.bacpac");
                var startupPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var dir = Path.Combine(startupPath, "Bacpacs");
                outputFileName = Path.Combine(dir, outputFileName);
                string[]? oldFiles = null;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    oldFiles = Directory.GetFiles(dir);
                }
                ds.ExportBacpac(outputFileName, _bglDataLayerConfiguration.Database);
                Task.Run(() =>
                {
                    try
                    {
                        if (oldFiles?.Any() ?? false)
                        {
                            foreach (var file in oldFiles)
                            {
                                File.Delete(file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, ex);
                    }
                });
            }
            catch
            {
                throw;
            }
            finally
            {
                IsBacpacGenerationActive = false;
            }
        }

        private static string? GetLatestBacpacFName()
        {
            var dir = "Bacpacs";
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "*.bacpac", SearchOption.TopDirectoryOnly);
            if (files == null || !files.Any()) return null;
            var latest = files.Select(p => new FileInfo(p)).OrderByDescending(p => p.CreationTimeUtc).FirstOrDefault();
            if (latest == null) return null;
            return latest.FullName;
        }

        public string? GetLatestBacpacFileName()
        {
            var latest = GetLatestBacpacFName();
            if (string.IsNullOrWhiteSpace(latest)) return null;
            return latest;
        }

        public byte[]? DownloadLatestBacpac()
        {
            var latest = GetLatestBacpacFName();
            if (string.IsNullOrWhiteSpace(latest)) return null;
            var bacpac = File.ReadAllBytes(latest);
            return bacpac;
        }
        #endregion

        #region Accounting Period
        public async Task<GetAccountingPeriodResponse?> GetAccountingPeriod(bool includeDeviceShifts = true)
        {
            GetAccountingPeriodResponse? result = null;
            DateTime? end = null;
            TimeSpan? remaining = null;
            AccountingPeriod? mostRecent = null;

            try
            {
                mostRecent = _context.AccountingPeriods.OrderByDescending(p => p.StartDate)?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, "Error during getting the most recent accounting period: " + ex.Message);
                throw;
            }

            var now = DateTime.Now;

            if (File.Exists(@"C:\Tmp\now.txt"))
            {
                now = JsonConvert.DeserializeObject<DateTime>(File.ReadAllText(@"C:\Tmp\now.txt"));
            }

            var period = mostRecent;
            if (!(period?.EndDate.HasValue ?? true)) // if it's not ended ...
            {
                var periodRealEndA = new DateTime(period.StartDate.Year, period.StartDate.Month, period.StartDate.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0).AddDays(1);
                var periodRealEndB = period.StartDate.AddDays(1);
                var periodRealEnd = periodRealEndA < periodRealEndB ? periodRealEndA : periodRealEndB;

                end = new DateTime(now.Year, now.Month, now.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
                if (now.Hour * 100 + now.Minute >= end.Value.Hour * 100 + end.Value.Minute) end = end.Value.AddDays(1);

                if (now < end && (end.Value - periodRealEnd).TotalDays <= 0)
                {
                    try
                    {
                        //result = await GetAccountingPeriod(mostRecent.Id, includeDeviceShifts);
                        result = GetAccountingPeriodV2(mostRecent, includeDeviceShifts);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, "Error during GetAccountingPeriodV2(): " + ex.Message);
                        throw;
                    }
                }
                else
                {
                    try
                    {
                        await CloseOldAccountingPeriods(false);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(_logger, "Error during CloseOldAccountingPeriods(): " + ex.Message);
                        throw;
                    }
                }
            }

            var hourMinute = now.Hour * 100 + now.Minute;

            if (mostRecent != null)
            {
                // close all ongoing agent shifts
                List<AgentShift>? agentShiftIndices = null;
                try
                {
                    agentShiftIndices = _context.AgentShifts.Where(p => p.AccountingPeriodId.Equals(mostRecent.Id) && !p.EndDate.HasValue).ToList();
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, "Error during getting all ongoing agent shifts: " + ex.Message);
                    throw;
                }
                if (agentShiftIndices?.Any() ?? false)
                {
                    var sw = Stopwatch.StartNew();
                    foreach (var aShift in agentShiftIndices)
                    {
                        var vtsShiftId = aShift.VtsShiftId ?? 0;
                        try
                        {
                            await CloseAgentShift(aShift.Id, vtsShiftId, ReceiptTemplateType.NotSpecified, null, true);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(_logger, $"Error during closing agent shift {vtsShiftId}, sw={sw.ElapsedMilliseconds}ms: " + ex.Message);
                            throw;
                        }
                    }
                    sw.Reset();
                }
            }

            try
            {
                // create a new one.
                period = new AccountingPeriod {
                    Id = Guid.NewGuid(),
                    AgentShifts = new List<AgentShift>(),
                    StartDate = new DateTime(now.Year, now.Month, now.Day, hourMinute / 100, hourMinute % 100, 0)
                };
                _context.AccountingPeriods.Add(period);
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, "Error during creation of a new accounting period: " + ex.Message);
                throw;
            }

            end = new DateTime(period.StartDate.Year, period.StartDate.Month, period.StartDate.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            if (now.Hour * 100 + now.Minute >= end.Value.Hour * 100 + end.Value.Minute) end = end.Value.AddDays(1);
            remaining = end - period.StartDate;
            result = new GetAccountingPeriodResponse
            {
                AccountingPeriod = period,
                RemainingTime = remaining
            };
            return result;
        }

        public Task<GetAccountingPeriodResponse?> GetAccountingPeriod(Guid periodId, bool includeDeviceShifts = true, bool includeSaleTransactions = true)
        {
            var period = _context.AccountingPeriods.FirstOrDefault(p => p.Id.Equals(periodId));
            if (period == null) return Task.FromResult((GetAccountingPeriodResponse?) null);
            if (period != null)
            {
                var shifts = _context.AgentShifts.Where(p => p.AccountingPeriodId.Equals(period.Id));
                if (includeDeviceShifts)
                {
                    shifts = shifts.Include("DeviceShifts");
                }
                if (includeSaleTransactions)
                {
                    shifts = shifts.Include("DeviceShifts.SaleTransactions");
                }
                period.AgentShifts = shifts.ToList();
            }
            var now = DateTime.Now;
            if (File.Exists(@"C:\Tmp\now.txt"))
            {
                now = JsonConvert.DeserializeObject<DateTime>(File.ReadAllText(@"C:\Tmp\now.txt"));
            }
            var end = new DateTime(now.Year, now.Month, now.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            //var end = new DateTime(period.StartDate.Year, period.StartDate.Month, period.StartDate.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            if (now.Hour * 100 + now.Minute >= end.Hour * 100 + end.Minute) end = end.AddDays(1);
            var remaining = end - now;
            var result = new GetAccountingPeriodResponse
            {
                AccountingPeriod = period,
                RemainingTime = remaining
            };
            return Task.FromResult(result);
        }

        public GetAccountingPeriodResponse? GetAccountingPeriodV2(AccountingPeriod? period, bool includeDeviceShifts = true, bool includeSaleTransactions = true)
        {
            if (period == null) return null;
            
            var shifts = _context.AgentShifts.Where(p => p.AccountingPeriodId.Equals(period.Id));
            if (includeDeviceShifts)
            {
                shifts = shifts.Include("DeviceShifts");
            }
            if (includeSaleTransactions)
            {
                shifts = shifts.Include("DeviceShifts.SaleTransactions");
            }
            period.AgentShifts = shifts.ToList();
            
            var now = DateTime.Now;
            if (File.Exists(@"C:\Tmp\now.txt"))
            {
                now = JsonConvert.DeserializeObject<DateTime>(File.ReadAllText(@"C:\Tmp\now.txt"));
            }
            var end = new DateTime(now.Year, now.Month, now.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            //var end = new DateTime(period.StartDate.Year, period.StartDate.Month, period.StartDate.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            if (now.Hour * 100 + now.Minute >= end.Hour * 100 + end.Minute) end = end.AddDays(1);
            var remaining = end - now;
            var result = new GetAccountingPeriodResponse {
                AccountingPeriod = period,
                RemainingTime = remaining
            };
            return result;
        }

        private DateTime AccountingPeriodSwitchTime
        {
            get
            {
                var td = DateTime.Today;
                if (File.Exists(@"C:\Tmp\today.txt"))
                    td = JsonConvert.DeserializeObject<DateTime>(File.ReadAllText(@"C:\Tmp\today.txt"));
                var today = new DateTime(td.Year, td.Month, td.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
                return today;
            }
        }

        private List<AccountingPeriod>? OldAccountingPeriods()
        {
            var td = DateTime.Today;
            if (File.Exists(@"C:\Tmp\today.txt"))
                td = JsonConvert.DeserializeObject<DateTime>(File.ReadAllText(@"C:\Tmp\today.txt"));
            var today = new DateTime(td.Year, td.Month, td.Day, HourMinuteChangeOperatingDay / 100, HourMinuteChangeOperatingDay % 100, 0);
            var periods = _context.AccountingPeriods?.OrderByDescending(p => p.StartDate)?.Where(p => !p.EndDate.HasValue && p.StartDate < today)?.ToList();
            return periods;
        }

        private async Task CloseOldAccountingPeriods(bool save = true)
        {
            var oldPeriods = OldAccountingPeriods();
            if (oldPeriods?.Any() ?? false)
            {
                oldPeriods.ForEach(p => p.EndDate = p.StartDate.AddDays(1).AddSeconds(-1));
                if (save)
                {
                    await _context.SaveChangesAsync();
                }
            }
        }
        #endregion

        #region Agent Shift
        public async Task<byte> GetAgentShiftNumber(int agentId, short companyId, string deviceIdentifier, Guid? periodId = null)
        {
            // !!! Every check here is performed for the current accounting period

            // get the current accounting period
            GetAccountingPeriodResponse? accPeriod = null;
            AccountingPeriod ? period = null;
            if (periodId.HasValue)
            {
                accPeriod = await GetAccountingPeriod(periodId.Value, true, false);
            }
            else
            {
                accPeriod = await GetAccountingPeriod();
            }
            period = accPeriod?.AccountingPeriod;

            // get the agent if he already worked
            var agent = await GetAgentShift(agentId, companyId, period, false);

            // get the device if it was already used

            var device = period?.AgentShifts
                ?.SelectMany(p => p.DeviceShifts)
                ?.Where(p => p.DeviceIdentifier.Equals(deviceIdentifier))
                ?.Distinct()
                ?.OrderByDescending(p => p.StartDate)
                ?.FirstOrDefault();

            //// check if the device is not occupied anymore
            //if (device != null && device.AgentShift != null && !device.AgentShift.EndDate.HasValue)
            //    return (byte)device.AgentShift.ShiftNumber;

            var shiftId = 0M;

            var condition1 = device != null && device.AgentShift != null; // there was another agent working previously on this device
            var condition2 = agent == null || agent.EndDate.HasValue; // the agent is only starting his work
            var condition3 = !condition2; // the agent has been already working on this device

            if (condition1)
            {
                if (condition2)
                    shiftId = device!.AgentShift!.ShiftNumber + 1; // take his shift id incremented by 1
                else
                    shiftId = agent.ShiftNumber; // let the agent occupy a new device and continue his work
            }
            else if (condition3)
            {
                shiftId = agent!.ShiftNumber; // let the agent continue his work
            }
            else // otherwise it must be the first shift
            {
                shiftId = 1;
            }

            return (byte)shiftId;
        }

        public async Task<AgentShift?> GetAgentShift(int agentId, short companyId, Guid? periodId = null, bool includeDeviceShifts = true)
        {
            // !!! Every check here is performed for the current accounting period

            // get the current accounting period
            GetAccountingPeriodResponse? accPeriod = null;
            AccountingPeriod? period = null;
            if (periodId.HasValue)
            {
                accPeriod = await GetAccountingPeriod(periodId.Value, includeDeviceShifts);
            }
            else
            {
                accPeriod = await GetAccountingPeriod();
            }
            period = accPeriod?.AccountingPeriod;

            return await GetAgentShift(agentId, companyId, period, includeDeviceShifts);
        }

        public async Task<AgentShift?> GetAgentShift(int agentId, short companyId, AccountingPeriod? period, bool includeDeviceShifts = true)
        {
            // !!! Every check here is performed for the current accounting period

            // get the agent if he already worked
            var agent = period?.AgentShifts.OrderByDescending(p => p.StartDate)?.FirstOrDefault(p => p.AgentId == agentId && p.CompanyId == companyId);

            return agent;
        }

        public async Task<AgentShift> AddAgentShift(AgentShift shift, Guid? periodId = null)
        {
            // !!! Every check here is performed for the current accounting period

            // get the current accounting period
            GetAccountingPeriodResponse? accPeriod = null;
            AccountingPeriod? period = null;
            if (periodId.HasValue)
            {
                accPeriod = await GetAccountingPeriod(periodId.Value);
            }
            else
            {
                accPeriod = await GetAccountingPeriod();
            }
            period = accPeriod?.AccountingPeriod;

            if (shift.Id.Equals(Guid.Empty)) shift.Id = Guid.NewGuid();
            if (shift.DeviceShifts == null) shift.DeviceShifts = new List<DeviceShift>();
            if (shift.CashDrawers == null) shift.CashDrawers = new List<CashDrawer>();
            if (shift.Receipt == null) shift.Receipt = new byte[0];
            shift.StartDate = DateTime.Now;
            shift.AccountingPeriodId = period.Id;

            period.AgentShifts.Add(shift);

            return shift;
        }

        public async Task<bool> OpenAgentShift(Guid shiftId, short serviceType, short sellingRegion)
        {
            if (_bglDataLayerConfiguration.GenerateBacpacOnShiftClosure && IsBacpacGenerationActive)
            {
                var ex = new BacpacGenerationInProgressException();
                ExHelper.ThrowExceptionContainer(ex, 1032, ex.Message, "Cannot open a shift while generating Bacpac");
            }

            var existing = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (existing == null) return false;
            var state = existing.State == 0 ? BglAgentShiftState.Created : (BglAgentShiftState)existing.State;
            if (state != BglAgentShiftState.Created) return false;
            existing.State = (byte)BglAgentShiftState.Open;
            existing.ServiceType = serviceType;
            existing.SellingRegion = sellingRegion;
            return true;
        }

        public async Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, PaymentMethodReport[]? paymentMethods = null, bool closedAutomatically = false)
        {
            if (vtsShiftId <= 0)
            {
                var ash = _context.AgentShifts
                    .Include("DeviceShifts")
                    .Include("DeviceShifts.SaleTransactions")
                    .FirstOrDefault(p => p.Id == shiftId);
                if (ash != null && !(ash.DeviceShifts?.Any(p => (p.SaleTransactions?.Count() ?? 0) > 0) ?? false))
                {
                    vtsShiftId = ash.VtsShiftId ?? 0;
                    if (vtsShiftId <= 0)
                        return await DeleteAgentShift(shiftId);
                }
            }

            var existing = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (existing == null) return false;
            var state = existing.State == 0 ? BglAgentShiftState.Created : (BglAgentShiftState)existing.State;
            //if (state == BglAgentShiftState.Closed) return false;

            var deviceShift = await GetDeviceShiftOfAgent(shiftId);
            if (!(deviceShift?.EndDate.HasValue ?? true)) await CloseDeviceShift(deviceShift.Id, (byte)BglMachineShiftClosingReason.RegularClosure);

            existing.State = (byte)BglAgentShiftState.Closed;
            existing.EndDate = DateTime.Now;
            existing.ReportId = (uint)await GenerateReportId(_context);
            existing.Receipt = _receiptsManager.CreateCloseShiftReceipt(existing.Id, templateType, paymentMethods);
            existing.ClosedByAgent = null;
            existing.ClosedAutomatically = (byte)(closedAutomatically ? 1 : 0);

            GenerateBacpacOnShiftClosure();

            return true;
        }
       
        public async Task<bool> CloseAgentShift(Guid shiftId, int vtsShiftId, ReceiptTemplateType templateType, int closingAgentId, PaymentMethodReport[]? paymentMethods = null)
        {
            if (vtsShiftId <= 0)
            {
                var ash = _context.AgentShifts.FirstOrDefault(p => p.Id == shiftId);
                vtsShiftId = ash?.VtsShiftId ?? 0;
                if (vtsShiftId <= 0)
                    return await DeleteAgentShift(shiftId);
            }

            var existing = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (existing == null) return false;
            var state = existing.State == 0 ? BglAgentShiftState.Created : (BglAgentShiftState)existing.State;
            if (/*state == BglAgentShiftState.Closed ||*/ state == BglAgentShiftState.Created) return false;

            var deviceShift = await GetDeviceShiftOfAgent(shiftId);
            if (!(deviceShift?.EndDate.HasValue ?? true)) await CloseDeviceShift(deviceShift.Id, (byte)BglMachineShiftClosingReason.RegularClosure);

            existing.State = (byte)BglAgentShiftState.Closed;
            existing.EndDate = DateTime.Now;
            existing.ClosedByAgent = closingAgentId;
            existing.ClosedAutomatically = 0;
            existing.EndDate = DateTime.Now;
            existing.ReportId = (uint)await GenerateReportId(_context);
            existing.Receipt = _receiptsManager.CreateCloseShiftReceipt(existing.Id, templateType, paymentMethods);

            GenerateBacpacOnShiftClosure();

            return true;
        }

        private void GenerateBacpacOnShiftClosure()
        {
            if (_bglDataLayerConfiguration.GenerateBacpacOnShiftClosure)
            {
                try
                {
                    var canBacpac = true;
                    var startupPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var dir = Path.Combine(startupPath, "Bacpacs");
                    var files = Directory.Exists(dir)
                        ? Directory.GetFiles(dir, "*.bacpac", SearchOption.TopDirectoryOnly)
                        : null;
                    if (files != null && files.Any())
                    {
                        var latest = files.Select(p => new FileInfo(p).CreationTimeUtc).OrderBy(p => p).Last();
                        if ((DateTime.UtcNow - latest).TotalMinutes < 10)
                        {
                            canBacpac = false;
                        }
                    }
                    if (canBacpac)
                    {
                        _ = Task.Run(() => GenerateBacpac());
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(_logger, ex);
                }
            }
        }

        private static async Task<int> GenerateReportId(DataLayerContext _context, int startingYear = 2024)
        {
            var now = DateTime.Now;
            var startingDate = new DateTime(startingYear, 1, 1);
            var seconds = (uint)(now - startingDate).TotalSeconds;
            var result = (int)seconds;
            if (seconds > int.MaxValue) // seconds > result
            {
                startingDate = new DateTime(startingDate.AddSeconds(int.MaxValue).Year, 1, 1);
                result = (int)(now - startingDate).TotalSeconds;
            }
            while (_context.AgentShifts.Any(p => p.ReportId == result))
            {
                ++result;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            return result;
        }

        public async Task<bool> LockAgentShift(Guid shiftId)
        {
            var existing = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (existing == null) return false;
            var state = existing.State == 0 ? BglAgentShiftState.Created : (BglAgentShiftState)existing.State;
            if (state == BglAgentShiftState.Closed || state == BglAgentShiftState.Created) return false;
            existing.State = (byte)(state == BglAgentShiftState.Locked ? BglAgentShiftState.Open : BglAgentShiftState.Locked);
            return true;
        }

        public async Task<bool> SetAgentVtsShift(Guid shiftId, int vtsShiftId)
        {
            var ash = _context.AgentShifts.FirstOrDefault(p => p.Id == shiftId);
            if (ash == null) return false;
            ash.VtsShiftId = vtsShiftId;
            return true;
        }

        private async Task<bool> DeleteAgentShift(Guid shiftId)
        {
            var ash = _context.AgentShifts.FirstOrDefault(p => p.Id == shiftId);
            if (ash == null) return false;
            var dsh = _context.DeviceShifts.FirstOrDefault(p => p.AgentShiftId == shiftId);
            if (dsh != null)
            {
                _context.Entry(dsh).State = EntityState.Deleted;
                await _context.SaveChangesAsync();
                _context.Entry(dsh).State = EntityState.Detached;
            }
            var sd = _context.SaleDevices.FirstOrDefault(p => ash.Id.Equals(p.ActiveAgentShift));
            if (sd != null) sd.ActiveAgentShift = null;
            try
            {
                _context.Entry(ash).State = EntityState.Deleted;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                throw;
            }
            try
            {
                _context.Entry(ash).State = EntityState.Detached;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error(_logger, ex);
                throw;
            }
            return true;
        }
        #endregion

        #region Device Shift
        public Task<DeviceShift?> GetDeviceShift(string deviceIdentifier, bool includeContainers = true)
        {
            includeContainers = false;
            var period = GetAccountingPeriod();
            var agentShifts = _context.AgentShifts.Where(p => p.AccountingPeriod.Id.Equals(period.Id)).OrderByDescending(p => p.StartDate).Take(10).Select(p => p.Id);
            var deviceShift = includeContainers
                ? _context.DeviceShifts.Where(p => agentShifts.Contains(p.AgentShiftId)).OrderByDescending(p => p.StartDate).Include("SaleTransactions").FirstOrDefault(p => p.DeviceIdentifier.Equals(deviceIdentifier))
                : _context.DeviceShifts.Where(p => agentShifts.Contains(p.AgentShiftId)).OrderByDescending(p => p.StartDate).FirstOrDefault(p => p.DeviceIdentifier.Equals(deviceIdentifier));
            if (deviceShift == null)
            {
                deviceShift = includeContainers
                    ? _context.DeviceShifts.OrderByDescending(p => p.StartDate).Include("SaleTransactions").FirstOrDefault(p => p.DeviceIdentifier.Equals(deviceIdentifier))
                    : _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault(p => p.DeviceIdentifier.Equals(deviceIdentifier));
            }
            return Task.FromResult(deviceShift);
        }

        public Task<DeviceShift?> GetDeviceShift(Guid deviceShiftId, bool includeContainers = true)
        {
            var deviceShift = includeContainers
                ? _context.DeviceShifts.OrderByDescending(p => p.StartDate).Include("SaleTransactions").FirstOrDefault(p => p.Id.Equals(deviceShiftId))
                : _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault(p => p.Id.Equals(deviceShiftId));
            return Task.FromResult(deviceShift);
        }

        public Task<DeviceShift?> GetDeviceShiftOfAgent(Guid agentShiftId, bool includeContainers = true)
        {
            DeviceShift? shift = null;
            var agentShift = _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(agentShiftId));
            if (agentShift != null)
            {
                var deviceShifts = includeContainers
                ? _context.DeviceShifts.Include("SaleTransactions").Where(p => p.AgentShiftId.Equals(agentShift.Id))
                : _context.DeviceShifts.Where(p => p.AgentShiftId.Equals(agentShift.Id));
                shift = deviceShifts?.OrderByDescending(p => p.StartDate)?.FirstOrDefault();
            }
            return Task.FromResult(shift);
        }

        public async Task<DeviceShift> AddDeviceShift(DeviceShift shift, Guid agentShiftId)
        {
            // closes the old device shift of the agent
            var deviceShift = await GetDeviceShiftOfAgent(agentShiftId, false);
            if (!(deviceShift?.EndDate.HasValue ?? true)) await CloseDeviceShift(deviceShift.Id, (byte)BglMachineShiftClosingReason.RegularClosure);
            
            // closes the old device shift on the same device
            deviceShift = await GetDeviceShift(shift.DeviceIdentifier, false);
            if (!(deviceShift?.EndDate.HasValue ?? true)) await CloseDeviceShift(deviceShift.Id, (byte)BglMachineShiftClosingReason.RegularClosure);

            if (shift.Id.Equals(Guid.Empty)) shift.Id = Guid.NewGuid();
            if (shift.SaleTransactions == null) shift.SaleTransactions = new List<SaleTransaction>();
            shift.StartDate = DateTime.Now;
            shift.AgentShiftId = agentShiftId;

            _context.AgentShifts.FirstOrDefault(p => p.Id.Equals(agentShiftId))?.DeviceShifts?.Add(shift);

            return shift;
        }

        public async Task<bool> OpenDeviceShift(Guid shiftId)
        {
            var shift = _context.DeviceShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (shift == null) return false;
            var state = shift.State == 0 ? BglMachineShiftState.Created : (BglMachineShiftState)shift.State;
            if (state != BglMachineShiftState.Created) return false;
            shift.State = (byte)BglMachineShiftState.Open;
            return true;
        }

        public async Task<bool> CloseDeviceShift(Guid shiftId, byte closingReason = 0, string closingReasonDescription = null)
        {
            var shift = _context.DeviceShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (shift == null) return false;
            var state = shift.State == 0 ? BglMachineShiftState.Created : (BglMachineShiftState)shift.State;
            //if (state != BglMachineShiftState.Open) return false;
            shift.State = (byte)BglMachineShiftState.Closed;
            if (!shift.EndDate.HasValue)
            {
                shift.EndDate = DateTime.Now;
            }
            shift.ClosingReason = closingReason;
            shift.ClosingReasonDescription = closingReasonDescription ?? ((BglMachineShiftClosingReason)closingReason).ToString();
            return true;
        }

        public async Task<bool> UpdateDeviceShift(DeviceShift shift)
        {
            if (shift == null) return false;
            var deviceShift = await GetDeviceShift(shift.Id, false);
            if (deviceShift == null) return false;

            var modified = false;
            if (!string.IsNullOrWhiteSpace(shift.AppVersion))
            {
                deviceShift.AppVersion = shift.AppVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.DsdeVersion))
            {
                deviceShift.DsdeVersion = shift.DsdeVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.VtsServerVersion))
            {
                deviceShift.VtsServerVersion = shift.VtsServerVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.PluginVersion))
            {
                deviceShift.PluginVersion = shift.PluginVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.TpfVersion))
            {
                deviceShift.TpfVersion = shift.TpfVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.VtokenStreamerVersion))
            {
                deviceShift.VtokenStreamerVersion = shift.VtokenStreamerVersion;
                modified = true;
            }
            if (!string.IsNullOrWhiteSpace(shift.BootstrapperVersion))
            {
                deviceShift.BootstrapperVersion = shift.BootstrapperVersion;
                modified = true;
            }

            if (!string.IsNullOrWhiteSpace(shift.PtCodiceRivendita))
            {
                deviceShift.PtCodiceRivendita = shift.PtCodiceRivendita;
                modified = true;
            }
            if (shift.PtCodiceEsattoria.HasValue)
            {
                deviceShift.PtCodiceEsattoria = shift.PtCodiceEsattoria;
                modified = true;
            }

            return modified;
        }

        public Task<bool> IsDeviceOperating(Guid shiftId)
        {
            var shift = _context.DeviceShifts.FirstOrDefault(p => p.Id.Equals(shiftId));
            if (shift == null || shift.EndDate.HasValue) return Task.FromResult(false);
            var state = shift.State == 0 ? BglMachineShiftState.Created : (BglMachineShiftState)shift.State;
            if (state == BglMachineShiftState.Closed) return Task.FromResult(false);
            return Task.FromResult(true);
        }

        public async Task<Guid> RegisterCardAnomaly(CardAnomaly anomaly)
        {
            if (anomaly.Id == Guid.Empty) anomaly.Id = Guid.NewGuid();
            anomaly.CardSerialNumberPh = anomaly.CardSerialNumberPh ?? string.Empty;
            anomaly.CardSerialNumberLo = anomaly.CardSerialNumberLo ?? string.Empty;
            _context.CardAnomalies.Add(anomaly);
            return anomaly.Id;
        }

        public async Task ResolveCardAnomaly(CardAnomaly anomaly)
        {
            List<CardAnomaly>? anomalies = null;
            if (!anomaly.Id.Equals(Guid.Empty))
                anomalies = _context.CardAnomalies.Where(p => p.Id.Equals(anomaly.Id)).ToList();
            else if (!string.IsNullOrWhiteSpace(anomaly.CardSerialNumberPh))
                anomalies = _context.CardAnomalies.Where(p => p.CardSerialNumberPh.Equals(anomaly.CardSerialNumberPh) && p.ShortCardModel == anomaly.ShortCardModel && p.DeviceShiftId.Equals(anomaly.DeviceShiftId)).ToList();
            else if (!string.IsNullOrWhiteSpace(anomaly.CardSerialNumberLo))
                anomalies = _context.CardAnomalies.Where(p => p.CardSerialNumberLo.Equals(anomaly.CardSerialNumberLo) && p.DeviceShiftId.Equals(anomaly.DeviceShiftId)).ToList();
            anomalies?.ForEach(p => p.ResolvedTime = DateTime.Now);
        }
        #endregion

        public async Task<Guid> RegisterApplicationShutdown(ApplicationShutdown shutdown)
        {
            if (shutdown.Id == Guid.Empty) shutdown.Id = Guid.NewGuid();
            _context.ApplicationShutdowns.Add(shutdown);
            return shutdown.Id;
        }

        public async Task SetDeviceStatus(SaleDevice saleDevice)
        {
            var devices = saleDevice.DsdePeriferalDevices;
            var cfgs = saleDevice.SaleDeviceConfigurations;

            await CleanUpDeviceStatus(saleDevice.Id);

            LogHelper.Debug(_logger, $"Registering the SaleDevice {saleDevice.Id}.");
            saleDevice.DsdePeriferalDevices = null;
            saleDevice.SaleDeviceConfigurations = null;
            var serialized = JsonConvert.SerializeObject(saleDevice);
            saleDevice.DsdePeriferalDevices = devices;
            saleDevice.SaleDeviceConfigurations = cfgs;
            var insertion = JsonConvert.DeserializeObject<SaleDevice>(serialized);
            _context.SaleDevices.Add(insertion);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Registering app.config ({cfgs?.Count() ?? 0} records).");
            var newCfgs = cfgs?.Where(p => p != null)?.Select(p => new SaleDeviceConfiguration {
                SaleDeviceId = saleDevice.Id,
                Name = p.Name,
                Value = p.Value
            })?.ToArray();
            if (newCfgs?.Any() ?? false) _context.AddRange(newCfgs);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Registering periferal devices ({devices?.Count() ?? 0} records).");
            var newDevices = devices?.Select(p => {
                if (p == null) return null;
                var tmpCfgs = p.DsdePeriferalDeviceConfigurations;
                var tmpModules = p.DsdePeriferalDeviceModules;
                p.DsdePeriferalDeviceConfigurations = null;
                p.DsdePeriferalDeviceModules = null;
                var serialized = JsonConvert.SerializeObject(p);
                p.DsdePeriferalDeviceConfigurations = tmpCfgs;
                p.DsdePeriferalDeviceModules = tmpModules;
                var insertion = JsonConvert.DeserializeObject<DsdePeriferalDevice>(serialized);
                if (insertion != null) insertion.SaleDeviceId = saleDevice.Id;
                return insertion;
            }).Where(p => p != null).ToArray();
            if (newDevices?.Any() ?? false) _context.AddRange(newDevices);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Registering periferal device modules.");
            var newModules = devices
                ?.ToDictionary(key => key, value => value.DsdePeriferalDeviceModules)
                ?.Select(p => p.Value.AsEnumerable()
                    ?.Select(q => {
                        if (q == null) return null;
                        var tmpModules = q.DsdePeriferalDeviceModuleAlarms;
                        q.DsdePeriferalDeviceModuleAlarms = null;
                        var serialized = JsonConvert.SerializeObject(q);
                        q.DsdePeriferalDeviceModuleAlarms = tmpModules;
                        var insertion = JsonConvert.DeserializeObject<DsdePeriferalDeviceModule>(serialized);
                        if (insertion != null)
                        {
                            insertion.DeviceType = p.Key.Type;
                            insertion.DeviceSubType = p.Key.SubType;
                        }
                        return insertion;
                    }))
                ?.Where(p => p != null)?.SelectMany(p => p)?.ToArray();
            if (newModules?.Any() ?? false) _context.AddRange(newModules);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Registering periferal device alarms.");
            var newAlarms = devices
                ?.SelectMany(p => p.DsdePeriferalDeviceModules)
                ?.ToDictionary(key => key, value => value.DsdePeriferalDeviceModuleAlarms)
                ?.Select(p => p.Value.AsEnumerable()
                    ?.Select(q => {
                        if (q == null) return null;
                        var serialized = JsonConvert.SerializeObject(q);
                        var insertion = JsonConvert.DeserializeObject<DsdePeriferalDeviceModuleAlarm>(serialized);
                        if (insertion != null)
                        {
                            insertion.Id = Guid.NewGuid();
                            insertion.ModuleType = p.Key.Type;
                            insertion.ModuleSubType = p.Key.SubType;
                        }
                        return insertion;
                    }))
                ?.Where(p => p != null)?.SelectMany(p => p)?.ToArray();
            if (newAlarms?.Any() ?? false) _context.AddRange(newAlarms);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Registering periferal device configs.");
            var newDeviceCfgs = devices
                ?.ToDictionary(key => key, value => value.DsdePeriferalDeviceConfigurations)
                ?.Select(p => p.Value.AsEnumerable()
                    ?.Select(q => {
                        if (q == null) return null;
                        var serialized = JsonConvert.SerializeObject(q);
                        var insertion = JsonConvert.DeserializeObject<DsdePeriferalDeviceConfiguration>(serialized);
                        if (insertion != null)
                        {
                            insertion.Id = Guid.NewGuid();
                            insertion.DeviceType = p.Key.Type;
                            insertion.DeviceSubType = p.Key.SubType;
                        }
                        return insertion;
                    }))
                ?.Where(p => p != null)?.SelectMany(p => p)?.ToArray();
            if (newDeviceCfgs?.Any() ?? false) _context.AddRange(newDeviceCfgs);

            LogHelper.Debug(_logger, $"SaleDevice {saleDevice.Id}: Saving changes.");
        }

        public async Task CleanUpDeviceStatus(int saleDeviceId)
        {
            LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Searching existing data for being deleted.");
            var existing = _context.SaleDevices
                .Where(p => p.Id == saleDeviceId)
                .Include("SaleDeviceConfigurations")
                .FirstOrDefault();

            if (existing != null)
            {
                var exAppCfg = existing.SaleDeviceConfigurations;
                if (exAppCfg?.Any() ?? false)
                {
                    LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Clean up active app.config data.");
                    _context.RemoveRange(exAppCfg);
                }
            }

            LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Searching existing periferal device data for being deleted.");
            var exDevices = _context.DsdePeriferalDevices
                .Where(p => p.SaleDeviceId == saleDeviceId)
                .Include("DsdePeriferalDeviceConfigurations")
                .Include("DsdePeriferalDeviceModules")
                .Include("DsdePeriferalDeviceModules.DsdePeriferalDeviceModuleAlarms");
            var exCfgs = exDevices?.SelectMany(p => p.DsdePeriferalDeviceConfigurations);
            var exModules = exDevices?.SelectMany(p => p.DsdePeriferalDeviceModules);
            var exAlarms = exModules?.SelectMany(p => p.DsdePeriferalDeviceModuleAlarms);

            if (exCfgs?.Any() ?? false)
            {
                LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Clean up active periferal device configuration data.");
                _context.RemoveRange(exCfgs);
            }
            if (exAlarms?.Any() ?? false)
            {
                LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Clean up active periferal device data.");
                _context.RemoveRange(exAlarms);
            }
            if (exModules?.Any() ?? false)
            {
                LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Clean up active periferal device module data.");
                _context.RemoveRange(exModules);
            }
            if (exDevices?.Any() ?? false)
            {
                LogHelper.Debug(_logger, $"SaleDevice {saleDeviceId}: Clean up active periferal device alarm data.");
                _context.RemoveRange(exDevices);
            }

            if (existing != null)
            {
                LogHelper.Debug(_logger, $"Unregistering the existing SaleDevice {saleDeviceId}.");
                _context.Remove(existing);
            }
        }

        public byte[]? GetApplicationSnapshot(string id, bool downloadHugeFile = false)
        {
            var record = _context.ApplicationShutdowns.AsEnumerable().FirstOrDefault(p => p.Id.ToString().Equals(id, StringComparison.InvariantCultureIgnoreCase));
            if (record == null) throw new Exception("Record not found.");
            var result = record.Context;
            if (result == null) return null;
            if (downloadHugeFile)
            {
                if (Encoding.ASCII.GetString(result.Take(5).ToArray()).Equals("path:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var path = Encoding.ASCII.GetString(result.Skip(5).ToArray());
                    var startupPath = new Uri(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)).AbsolutePath;
                    path = Path.Combine(startupPath, "snap", path);
                    if (!File.Exists(path)) throw new FileNotFoundException(path);
                    result = File.ReadAllBytes(path);
                }
            }
            return result;
        }

        public byte[]? DownloadApplicationShutdownContext(string id, string fileName)
        {
            var record = _context.ApplicationShutdowns.AsEnumerable().FirstOrDefault(p => p.Id.ToString().Equals(id, StringComparison.InvariantCultureIgnoreCase));
            if (record == null) throw new Exception("Record not found.");
            var data = record.Context;
            return data;
        }

        public SbmeProfilePriceMapping? GetSbmeProfilePriceMapping(int profileId, short issuingReasonCode = 0) =>
            _context.SbmeProfilePriceMappings.FirstOrDefault(p => p.ProfileId == profileId && p.IssuingReasonCode == issuingReasonCode);

        public async Task RegisterNewDevice(string deviceIdentifier)
        {
            await _context.StaticVariables.AddAsync(new StaticVariablesList {
                DeviceIdentifier = deviceIdentifier,
                MaxSalesThreshold = _salesThresholdConfiguration.InitialMax,
                WarningSalesThreshold = _salesThresholdConfiguration.InitialWarning,
                CurrentResidual = _salesThresholdConfiguration.InitialMax,
                IsResidualVirgin = 0
            });
        }

        public async Task<StaticVariablesList?> GetStaticVariables(string deviceIdentifier)
        {
            var save = false;
            var record = _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));
            if (record?.DeviceIdentifier.Equals("localhost") ?? false)
            {
                var copy = new StaticVariablesList();
                CopyStaticVariablesList(record, copy);
                copy.DeviceIdentifier = deviceIdentifier;
                _context.StaticVariables.Remove(record);
                _context.StaticVariables.Add(copy);
                record = copy;
                save = true;
            }
            if (!_context.StaticVariables.Any())
            {
                record = new StaticVariablesList {
                    DeviceIdentifier = "localhost",
                    PreventSaleOperations = 0,
                    AdminBlock = 0,
                    MaxSalesThreshold = _salesThresholdConfiguration.InitialMax,
                    WarningSalesThreshold = _salesThresholdConfiguration.InitialWarning,
                    CurrentResidual = 0,
                    IsResidualVirgin = 1
                };
                await _context.StaticVariables.AddAsync(record);
                save = true;
            }
            //if (record!.IsResidualVirgin == 1)
            //{
            //    record.MaxSalesThreshold = _salesThresholdConfiguration.InitialMax;
            //    record.WarningSalesThreshold = _salesThresholdConfiguration.InitialWarning;
            //    record.CurrentResidual = _salesThresholdConfiguration.InitialMax;
            //    record.IsResidualVirgin = 0;
            //    save = true;
            //}
            if (save)
            {
                await SaveChangesAsync();
            }
            return record;
        }

        public async Task SetStaticVariables(StaticVariablesList variables)
        {
            var entry = await GetStaticVariables(variables.DeviceIdentifier);
            if (entry == null)
            {
                _context.StaticVariables.Add(variables);
            }
            else
            {
                CopyStaticVariablesList(variables, entry);
            }
        }

        public async Task DeclareAdminBlockUnlockManaged(string deviceIdentifier)
        {
            var record = _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));
            if (record!.DeviceIdentifier.Equals("localhost"))
            {
                var copy = new StaticVariablesList();
                CopyStaticVariablesList(record, copy);
                copy.DeviceIdentifier = deviceIdentifier;
                _context.StaticVariables.Remove(record);
                _context.StaticVariables.Add(copy);
                record = copy;
            }
            record!.AdminBlockUnlockDateTime = DateTime.Now;
        }

        public async Task DeclareThresholdBlockUnlockManaged(string deviceIdentifier)
        {
            var record = _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));
            if (record!.DeviceIdentifier.Equals("localhost"))
            {
                var copy = new StaticVariablesList();
                CopyStaticVariablesList(record, copy);
                copy.DeviceIdentifier = deviceIdentifier;
                _context.StaticVariables.Remove(record);
                _context.StaticVariables.Add(copy);
                record = copy;
            }
            record!.ThresholdBlockUnlockDateTime = DateTime.Now;
        }

        public async Task DeclareOfflineBlockUnlockManaged(string deviceIdentifier)
        {
            var record = _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));
            if (record!.DeviceIdentifier.Equals("localhost"))
            {
                var copy = new StaticVariablesList();
                CopyStaticVariablesList(record, copy);
                copy.DeviceIdentifier = deviceIdentifier;
                _context.StaticVariables.Remove(record);
                _context.StaticVariables.Add(copy);
                record = copy;
            }
            record!.OfflineBlockUnlockDateTime = DateTime.Now;
        }

        public string? LoadPersonalization(int agentId, short companyId) =>
            _context.Personalizations.FirstOrDefault(p => p.AgentId == agentId && p.CompanyId == companyId)?.Configuration;

        public void SavePersonalization(string? personalization, int agentId, short companyId)
        {
            var record = _context.Personalizations.FirstOrDefault(p => p.AgentId == agentId && p.CompanyId == companyId);
            if (record == null)
            {
                record = new Personalization {
                    AgentId = agentId,
                    CompanyId = companyId
                };
                _context.Personalizations.Add(record);
            }
            record.Configuration = personalization;
        }

        private void CopyStaticVariablesList(StaticVariablesList from, StaticVariablesList to)
        {
            to.PreventSaleOperations = from.PreventSaleOperations;
            to.AdminBlock = from.AdminBlock;
            to.MaxSalesThreshold = from.MaxSalesThreshold;
            to.WarningSalesThreshold = from.WarningSalesThreshold;
            to.CurrentResidual = from.CurrentResidual;
            to.AutoCloseShiftWhenBlockingSaleOperations = from.AutoCloseShiftWhenBlockingSaleOperations;
            to.AutoCloseBasketWhenBlockingSaleOperations = from.AutoCloseBasketWhenBlockingSaleOperations;
            to.DaysOfflineBeforeAutoblockingSaleOperations = from.DaysOfflineBeforeAutoblockingSaleOperations;
            to.IsResidualVirgin = 0;
            to.AdminBlockUnlockDateTime = from.AdminBlockUnlockDateTime;
            to.ThresholdBlockUnlockDateTime = from.ThresholdBlockUnlockDateTime;
            to.OfflineBlock = from.OfflineBlock;
            to.OfflineBlockUnlockDateTime = from.OfflineBlockUnlockDateTime;
        }

        public async Task RegisterSellContract(VtSellContractInfo info)
        {
            info.Id = Guid.NewGuid();
            await _context.VtSellContractInfos.AddAsync(info);
        }

        private static string Md5(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(data);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        public async Task RegisterSellCommit(VtSellCommitInfo info)
        {
            VtSellCommitInfo? existing = null;
            if (info.TransactionUndoData == null || info.TransactionUndoData.Length == 0)
            {
                existing = await _context.VtSellCommitInfos.FirstOrDefaultAsync(p => !string.IsNullOrWhiteSpace(p.GroupUid) && p.GroupUid.Equals(info.GroupUid));
            }
            else
            {
                var md5 = Md5(info.TransactionUndoData);
                existing = await _context.VtSellCommitInfos
                    .Where(p => p.TransactionUndoData != null && p.TransactionUndoData.Length > 0)
                    .FirstOrDefaultAsync(p => md5.Equals(p.TransactionUndoDataMd5));
            }
            if (existing != null)
            {
                existing.InsertDate = info.InsertDate;
                existing.RequestResult = info.RequestResult;
                existing.CommitResult = info.CommitResult;
                existing.VtTransactionUid = info.VtTransactionUid;
                existing.ContractUid = info.ContractUid;
                existing.ContractData = info.ContractData;
                existing.Vtoken = info.Vtoken;
                existing.TransactionUndoData = info.TransactionUndoData;
                if (info.TransactionUndoData != null && info.TransactionUndoData.Length > 0)
                {
                    var md5 = Md5(info.TransactionUndoData);
                    existing.TransactionUndoDataMd5 = md5;
                }
            }
            else
            {
                info.Id = Guid.NewGuid();
                if (info.TransactionUndoData != null && info.TransactionUndoData.Length > 0)
                {
                    var md5 = Md5(info.TransactionUndoData);
                    info.TransactionUndoDataMd5 = md5;
                }
                await _context.VtSellCommitInfos.AddAsync(info);
            }
        }

        public async Task RegisterSellCommitPamentType(VtSellCommitPaymentTypeInfo info)
        {
            info.Id = Guid.NewGuid();
            await _context.VtSellCommitPaymentTypeInfos.AddAsync(info);
        }

        public async Task RegisterSaleTransactionPamentType(VtSellTransactionPaymentTypeInfo info)
        {
            info.Id = Guid.NewGuid();
            await _context.VtSellTransactionPaymentTypeInfos.AddAsync(info);
        }

        public async Task<VtSellCommitShortInfo?> GetVtSellCommit(string groupUid)
        {
            if (string.IsNullOrWhiteSpace(groupUid)) return null;
            var info = await _context.VtSellCommitInfos.FirstOrDefaultAsync(p => groupUid.Equals(p.GroupUid));
            if (info == null) return null;
            var shortInfo = new VtSellCommitShortInfo {
                GroupUid = info.GroupUid,
                Result = (int)info.CommitResult!
            };
            if (info.RequestResult == 0)
            {
                // TODO: WARNING
            }
            return shortInfo;
        }

        public async Task<List<VtTransactionCashFlow>?> GetVtTransactionCashFlow(string transactionUid)
        {
            if (string.IsNullOrWhiteSpace(transactionUid)) return null;
            var infos = _context.VtSellTransactionPaymentTypeInfos.Where(p => transactionUid.Equals(p.VtTransactionUid.ToString()));
            if (!(infos?.Any() ?? false)) return null;
            var cf = await infos.Select(p => new VtTransactionCashFlow {
                    SaleTransactionId = p.SaleTransactionId ?? Guid.Empty,
                    VtTransactionId = p.VtTransactionUid ?? 0,
                    VtPaymentType = p.PaymentType1 ?? 0,
                    VtFlowType = p.FlowType ?? 0,
                    Price = p.AmountEuroCent ?? 0
                })
                .ToListAsync();
            return cf;
        }

        public async Task<string?> GetMinAppVersione()
        {
            string? deviceIdentifier = null;
            var ds = _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (ds != null)
            {
                deviceIdentifier = ds.DeviceIdentifier;
            }

            var variables = deviceIdentifier == null
                ? _context.StaticVariables.FirstOrDefault()
                : _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));

            if (string.IsNullOrWhiteSpace(variables?.MinAppVersioneAllowed))
            {
                var first = await _context.StaticVariables.FirstOrDefaultAsync(p => !string.IsNullOrWhiteSpace(p.MinAppVersioneAllowed));
                return first?.MinAppVersioneAllowed;
            }

            return variables?.MinAppVersioneAllowed;
        }

        public async Task<string?> GetMinVtsVersione()
        {
            string? deviceIdentifier = null;
            var ds = _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (ds != null)
            {
                deviceIdentifier = ds.DeviceIdentifier;
            }

            var variables = deviceIdentifier == null
                ? _context.StaticVariables.FirstOrDefault()
                : _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));

            if (string.IsNullOrWhiteSpace(variables?.MinVtsVersioneAllowed))
            {
                var first = await _context.StaticVariables.FirstOrDefaultAsync(p => !string.IsNullOrWhiteSpace(p.MinVtsVersioneAllowed));
                return first?.MinVtsVersioneAllowed;
            }

            return variables?.MinVtsVersioneAllowed;
        }

        public async Task SetMinAppVersione(string version)
        {
            string? deviceIdentifier = null;
            var ds = _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (ds != null)
            {
                deviceIdentifier = ds.DeviceIdentifier;
            }

            var vars = _context.StaticVariables.Take(100).ToList();
            if (vars?.Any() ?? false)
            {
                foreach (var v in vars)
                {
                    v.MinAppVersioneAllowed = version;
                }
            }
        }

        public async Task SetMinVtsVersione(string version)
        {
            string? deviceIdentifier = null;
            var ds = _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
            if (ds != null)
            {
                deviceIdentifier = ds.DeviceIdentifier;
            }

            var vars = _context.StaticVariables.Take(100).ToList();
            if (vars?.Any() ?? false)
            {
                foreach (var v in vars)
                {
                    v.MinVtsVersioneAllowed = version;
                }
            }
        }
    }
}
