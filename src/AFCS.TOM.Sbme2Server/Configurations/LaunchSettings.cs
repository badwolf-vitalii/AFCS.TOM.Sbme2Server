namespace AFCS.TOM.Sbme2Server.Configurations
{
    public class LaunchSettings
    {
        public string ApplicationUrl { get; set; }
        public string BacpacApplicationUrl { get; set; }
        public bool BglServicesEnabled { get; set; }
        public bool SalesThresholdsServiceEnabled { get; set; }
        public bool Sbme1ServicesEnabled { get; set; }
        public bool Sbme2ServicesEnabled { get; set; }
        public bool CustomerIdInheritedFromSbme1 { get; set; }
        public bool? DsdeDashboardEnabled { get; set; }
        public bool? UseCors { get; set; }
        public int? NodeId { get; set; }
    }
}
