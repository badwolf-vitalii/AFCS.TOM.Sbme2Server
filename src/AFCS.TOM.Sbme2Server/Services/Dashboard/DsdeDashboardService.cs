using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.DsdeDashboard;
using Microsoft.EntityFrameworkCore;

namespace AFCS.TOM.Sbme2Server.Services.Dashboard
{
    public class DsdeDashboardService : IDsdeDashboardService
    {
        public async Task<IList<DeviceListRecord>> GetDeviceList(string connectionString) =>
         await DBOracleManager2.GetDeviceList(connectionString).ConfigureAwait(false);

        public Task<SaleDevice?> GetSaleDeviceStatus(DataLayerContext context, int saleDeviceId)
        {
            var saleDevice = context.SaleDevices
                .Include("SaleDeviceConfigurations")
                .Include("DsdePeriferalDevices")
                .Include("DsdePeriferalDevices.DsdePeriferalDeviceConfigurations")
                .Include("DsdePeriferalDevices.DsdePeriferalDeviceModules")
                .Include("DsdePeriferalDevices.DsdePeriferalDeviceModules.DsdePeriferalDeviceModuleAlarms")
                .FirstOrDefault(p => p.Id == saleDeviceId);
            return Task.FromResult(saleDevice);
        }
    }
}
