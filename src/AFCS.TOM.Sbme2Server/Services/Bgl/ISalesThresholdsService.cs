using AFCS.TOM.SbmeModels.SalesThresholds;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public interface ISalesThresholdsService : IBglServiceBase
    {
        bool IsServiceEnabled { get; }

        Task<ThresholdsInfo> GetSalesThresholds();
        Task<decimal> GetCurrentResidual();
        Task<FullInfo> GetFullInfo();
        Task SetSalesMaxThreshold(SetSalesMaxThresholdRequest parameters);
        Task SetSalesAlarmThreshold(SetSalesAlarmThresholdRequest parameters);
        Task SetExactCurrentResidual(SetExactCurrentResidualRequest parameters);
        Task ResetCurrentResidual();
        Task BlockSaleOperations(BlockSaleOperationsRequest parameters);
        Task UnlockSaleOperations(UnlockSaleOperationsRequest parameters);
    }
}
