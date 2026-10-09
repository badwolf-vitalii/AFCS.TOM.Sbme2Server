using AFCS.TOM.Sbme2Server.Services.Bgl;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Serialization;

namespace AFCS.TOM.Sbme2Server.Configurations
{
    public sealed class BglDataLayerConfiguration
    {
        public static BglDataLayerConfiguration Instance { get; private set; }

        public string Server { get; set; }
        public string Database { get; set; }
        public bool TrustedConnection { get; set; }
        public string User { get; set; }
        public int BasketVersion { get; set; }
        public bool SqlSrv2014 { get; set; }
        public bool GenerateBacpacOnShiftClosure { get; set; }
        public bool AllowArchive { get; set; }
        public byte? GetMostlyUsedTariffsVersion { get; set; } = 1;
        public int? BacpacLaunchMonths { get; set; }
        public int? BacpacDataMonths { get; set; }

        private string _password;
        [XmlIgnore]
        [field: NonSerialized]
        [JsonIgnore]
        public string Password
        {
            get
            {
                if ((KeysManager.LocalDbKeys?.Length ?? 0) <= 1 && !string.IsNullOrWhiteSpace(_password))
                {
                    return _password;
                }
                try
                {
                    var data = KeysManager.GetLocalDbKey()?.Result;
                    if (data != null)
                    {
                        var str = Encoding.ASCII.GetString(data);
                        _password = Crypt.Chewbacca.Decode(str);
                    }
                    else
                    {
                        _password = string.Empty;
                    }
                }
                catch
                {
                }
                return _password;
            }
            set => _password = value;
        }

        public string ConnectionString => TrustedConnection
                    ? $"Server={Server};Database={Database};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                    : $"Server={Server};Database={Database};Trusted_Connection=false;user={User};password={Password};TrustServerCertificate=true;Encrypt=false;";

        public BglDataLayerConfiguration()
        {
            Instance = this;
        }
    }
}
