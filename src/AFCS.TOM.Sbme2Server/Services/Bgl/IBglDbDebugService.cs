namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public interface IBglDbDebugService : IBglServiceBase
    {
        bool IsServiceEnabled { get; }

        Task DeleteAccountingPeriods();
        Task DeleteSaleTransactions();
    }

    public class EmptyBglDbDebugService : IBglDbDebugService
    {
        public bool IsServiceEnabled { get; }

        public Task DeleteAccountingPeriods()
        {
            throw new NotImplementedException();
        }

        public Task DeleteSaleTransactions()
        {
            throw new NotImplementedException();
        }

        public void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public Task<int> SaveChangesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
