namespace AFCS.TOM.Sbme2Server.Configurations
{
    public class DebuggingConfiguration
    {
        public bool NoSBME { get; set; }
        public bool SerializeData { get; set; }
        public string? WLog { get; set; }
        public bool WLogGetInstanceOfType { get; set; }
        public bool? LogInsertTSCRequest { get; set; }
        public bool? ReceiptsDebugControllerEnabled { get; set; }

        public static DebuggingConfiguration Default { get; } = new DebuggingConfiguration
        {
            NoSBME = false,
            SerializeData = false,
            WLog = null,
            WLogGetInstanceOfType = false,
            ReceiptsDebugControllerEnabled = false
        };
    }
}
