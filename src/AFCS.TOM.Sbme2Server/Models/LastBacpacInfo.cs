using Newtonsoft.Json;

namespace AFCS.TOM.Sbme2Server.Models
{
    public class LastBacpacInfo
    {
        public DateTime? DateTimeAuto { get; set; }
        public DateTime? DateTimeRequested { get; set; }
        public DateTime? DateTimeMonthly { get; set; }
        [JsonIgnore] public bool Updated { get; set; } = false;
        public bool CleanedUp { get; set; } = true;
    }
}
