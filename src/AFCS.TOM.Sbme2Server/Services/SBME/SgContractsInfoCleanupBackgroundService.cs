using AFCS.TOM.Sbme2Server.Controllers;
using AFCS.TOM.SbmeModels.Enums;
using NLog;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public class SgContractsInfoCleanupBackgroundService : BackgroundService
    {
        private static Logger Logger => LogManager.GetLogger("Sbme2Server");

        private IConfiguration _config { get; }
        private ICustomerService _customerService { get; set; }
        private DateTime HourMinuteSgContractsInfoCleanup { get; }
        private string StartupPath { get; }

        private const string LastSgCtrCleaupDtFileName = "LastSgCtrCleaupDt.ini";
        private const string LastSgCtrCleaupDtFormat = "yyyy-MM-dd HH:mm:ss";

        private async Task<DateTime> GetLastSgCtrCleaupDt()
        {
            var result = DateTime.MinValue;
            var fname = Path.Combine(StartupPath, LastSgCtrCleaupDtFileName);
            if (File.Exists(fname))
            {
                var txt = await File.ReadAllTextAsync(fname);
                try
                {
                    result = DateTime.ParseExact(txt, LastSgCtrCleaupDtFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
            }
            return GetDateMidnight(result);
        }

        private async Task SetLastSgCtrCleaupDt(DateTime date)
        {
            try
            {
                var fname = Path.Combine(StartupPath, LastSgCtrCleaupDtFileName);
                await File.WriteAllTextAsync(fname, date.ToString(LastSgCtrCleaupDtFormat, CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Logger?.Error(ex);
            }
        }

        private DateTime GetDateMidnight(DateTime date) => new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);

        public SgContractsInfoCleanupBackgroundService(IConfiguration config, ICustomerService customerService)
        {
            _config = config;
            _customerService = customerService;

            var hmCleanup = config.GetValue<int>("HourMinuteSgContractsInfoCleanup");
            if (hmCleanup == 0) hmCleanup = 03_45;
            var today = DateTime.Today;
            HourMinuteSgContractsInfoCleanup = new DateTime(today.Year, today.Month, today.Day, hmCleanup / 100, hmCleanup % 100, 0);

            //StartupPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            StartupPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var sgGestownCnStr = string.Empty;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    sgGestownCnStr = ControllersHelper.GetConnectionString(_config, ConnectionString.SG_GESTOWN);
                    if (string.IsNullOrWhiteSpace(sgGestownCnStr))
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30));
                        continue;
                    }
                    var lastSgCtrCleaupDt = await GetLastSgCtrCleaupDt();
                    if (lastSgCtrCleaupDt < DateTime.Today)
                    {
                        var now = DateTime.Now;
                        if (HourMinuteSgContractsInfoCleanup <= now)
                        {
                            await _customerService.ClearTDSDEContracts(sgGestownCnStr);
                            await SetLastSgCtrCleaupDt(now);
                        }
                        await Task.Delay(TimeSpan.FromMinutes(1));
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5));
                    }
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrWhiteSpace(sgGestownCnStr))
                    {
                        Logger?.Debug("SgContractsInfoCleanupBackgroundService: SG_GESTOWN not specified");
                    }
                    else
                    {
                        Logger?.Error(ex);
                    }
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
            }
        }
    }
}
