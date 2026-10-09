using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels.DsdeDashboard;

namespace AFCS.TOM.Sbme2Server.Services.Dashboard
{
    public interface IDsdeDashboardService
    {
        Task<IList<DeviceListRecord>> GetDeviceList(string connectionString);
        Task<SaleDevice?> GetSaleDeviceStatus(DataLayerContext context, int saleDeviceId);
    }

    public class EmptyDsdeDashboardService : IDsdeDashboardService
    {
        public Task<IList<DeviceListRecord>> GetDeviceList(string connectionString)
        {
            throw new NotImplementedException();
        }

        public Task<SaleDevice?> GetSaleDeviceStatus(DataLayerContext context, int saleDeviceId)
        {
            throw new NotImplementedException();
        }
    }
}
