using AFCS.TOM.Sbme2Server.Configurations;
using Microsoft.EntityFrameworkCore;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class BglDbDebugService : IBglDbDebugService
    {
        private DL.DataLayerContext _context { get; }
        public bool IsServiceEnabled { get; }

        public BglDbDebugService(IConfiguration configuration, DL.DataLayerContext context)
        {
            var launchSettings = new LaunchSettings();
            configuration.GetSection("LaunchSettings").Bind(launchSettings);
            IsServiceEnabled = launchSettings.BglServicesEnabled;
            _context = context;
        }

        public void SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task DeleteAccountingPeriods()
        {
            await DeleteSaleTransactions();
            await _context.CashDrawers.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.CashDrawers.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.DeviceShifts.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.DeviceShifts.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.AgentShifts.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.AgentShifts.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.AccountingPeriods.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.AccountingPeriods.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            //await _context.Personalizations.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            //await _context.SaveChangesAsync();
            //await _context.Personalizations.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
        }

        public async Task DeleteSaleTransactions()
        {
            await _context.CscContractArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.CscContractArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.CscContractRefundArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.CscContractRefundArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.ProfileRenewalArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.ProfileRenewalArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.ContactlessCardArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.ContactlessCardArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.MagneticArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.MagneticArticleInfos.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.PosDetails.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.PosDetails.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.PaymentDetails.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.PaymentDetails.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.Articles.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.Articles.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
            await _context.SaleTransactions.ForEachAsync(p => _context.Entry(p).State = EntityState.Deleted);
            await _context.SaveChangesAsync();
            await _context.SaleTransactions.ForEachAsync(p => _context.Entry(p).State = EntityState.Detached);
        }
    }
}
