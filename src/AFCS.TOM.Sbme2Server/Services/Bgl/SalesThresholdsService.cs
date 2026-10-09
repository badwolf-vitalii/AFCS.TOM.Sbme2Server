using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels.SalesThresholds;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class SalesThresholdsService : ISalesThresholdsService
    {
        private DL.DataLayerContext _context { get; }
        private SalesThresholdConfiguration _salesThresholdConfiguration { get; }
        public bool IsServiceEnabled { get; }

        public SalesThresholdsService(IConfiguration configuration, DL.DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            var salesThresholdConfiguration = new SalesThresholdConfiguration();
            configuration.GetSection("SalesThresholdConfiguration").Bind(salesThresholdConfiguration);
            _salesThresholdConfiguration = salesThresholdConfiguration;
            IsServiceEnabled = launchSettings.SalesThresholdsServiceEnabled;
            _context = context;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        private async Task<DL.StaticVariablesList> GetVariables(string? deviceIdentifier = null)
        {
            if (deviceIdentifier == null)
            {
                var ds = _context.DeviceShifts.OrderByDescending(p => p.StartDate).FirstOrDefault();
                if (ds != null)
                {
                    deviceIdentifier = ds.DeviceIdentifier;
                }
            }
            var variables = deviceIdentifier == null
                ? _context.StaticVariables.FirstOrDefault()
                : _context.StaticVariables.FirstOrDefault(p => p.DeviceIdentifier.ToLower().Equals(deviceIdentifier.ToLower()) || p.DeviceIdentifier.Equals("localhost"));
            if (variables == null)
            {
                variables = new DL.StaticVariablesList {
                    DeviceIdentifier = deviceIdentifier ?? "localhost",
                    PreventSaleOperations = 0,
                    AdminBlock = 0,
                    MaxSalesThreshold = _salesThresholdConfiguration.InitialMax,
                    WarningSalesThreshold = _salesThresholdConfiguration.InitialWarning,
                    CurrentResidual = 0,
                    IsResidualVirgin = 1
                };
                _context.StaticVariables.Add(variables);
                await SaveChangesAsync();
            }
            else if (!string.IsNullOrWhiteSpace(deviceIdentifier) && variables.DeviceIdentifier.Equals("localhost"))
            {
                variables.DeviceIdentifier = deviceIdentifier;
                await SaveChangesAsync();
            }
            return variables;
        }

        public async Task<ThresholdsInfo> GetSalesThresholds()
        {
            var variables = await GetVariables();
            return new ThresholdsInfo {
                Max = variables.MaxSalesThreshold,
                Warning = variables.WarningSalesThreshold
            };
        }

        public async Task<decimal> GetCurrentResidual()
        {
            var variables = await GetVariables();
            return variables.CurrentResidual;
        }

        public async Task<FullInfo> GetFullInfo()
        {
            var variables = await GetVariables();
            return new FullInfo {
                ThresholdMax = variables.MaxSalesThreshold,
                ThresholdWarning = variables.WarningSalesThreshold,
                Residual = variables.CurrentResidual,
                IsResidualVirgin = variables.IsResidualVirgin != 0,
                IsBlockedByAdministrator = variables.AdminBlock > 0,
                IsBlockedDueToMissingConnectionWithCenter = variables.PreventSaleOperations > 0,
                AdminBlockUnlockDateTime = variables.AdminBlockUnlockDateTime,
                ThresholdBlockUnlockDateTime = variables.ThresholdBlockUnlockDateTime
            };
        }

        public async Task SetSalesMaxThreshold(SetSalesMaxThresholdRequest parameters)
        {
            var variables = await GetVariables();
            variables.MaxSalesThreshold = parameters.Amount ?? 0;
            if (variables.IsResidualVirgin != 0)
            {
                variables.IsResidualVirgin = 0;
                variables.CurrentResidual = variables.MaxSalesThreshold;
            }
        }

        public async Task SetSalesAlarmThreshold(SetSalesAlarmThresholdRequest parameters)
        {
            var variables = await GetVariables();
            variables.WarningSalesThreshold = parameters.Amount ?? 0;
        }

        public async Task SetExactCurrentResidual(SetExactCurrentResidualRequest parameters)
        {
            var variables = await GetVariables();
            variables.CurrentResidual = parameters.Amount ?? 0;
        }

        public async Task ResetCurrentResidual()
        {
            var variables = await GetVariables();
            variables.CurrentResidual = variables.MaxSalesThreshold;
        }

        public async Task BlockSaleOperations(BlockSaleOperationsRequest parameters)
        {
            var variables = await GetVariables();
            variables.AdminBlock = 1;
            variables.AdminBlockUnlockDateTime = null;
            if (parameters.CloseShift.HasValue)
            {
                variables.AutoCloseShiftWhenBlockingSaleOperations = (byte)(parameters.CloseShift.Value ? 1 : 0);
            }
            if (parameters.CloseBasket.HasValue)
            {
                variables.AutoCloseBasketWhenBlockingSaleOperations = (byte)(parameters.CloseBasket.Value ? 1 : 0);
            }
        }

        public async Task UnlockSaleOperations(UnlockSaleOperationsRequest parameters)
        {
            var variables = await GetVariables();
            variables.AdminBlock = 0;
            variables.AdminBlockUnlockDateTime = null;
            if (parameters.ResetResidual ?? false)
            {
                variables.CurrentResidual = variables.MaxSalesThreshold;
            }
        }
    }
}
