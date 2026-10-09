using AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    public class GetAccountingPeriodResponse
    {
        public AccountingPeriod? AccountingPeriod { get; set; }
        public TimeSpan? RemainingTime { get; set; }
    }
}
