namespace AFCS.TOM.Sbme2Server.Configurations
{
    public sealed class SalesThresholdConfiguration
    {
        public static SalesThresholdConfiguration Instance { get; private set; }

        public decimal InitialMax { get; set; }
        public decimal InitialWarning { get; set; }

        public SalesThresholdConfiguration()
        {
            Instance = this;
        }
    }
}
