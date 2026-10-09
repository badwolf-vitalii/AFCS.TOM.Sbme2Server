namespace AFCS.TOM.Sbme2Server.Configurations
{
    public class SFTPConfirmTSCRequestConfiguration
    {
        public bool SendXml { get; set; } = true;
        public bool SendErrorXml { get; set; } = true;
        public string ServerURL { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Path { get; set; }
        public string LocalRepoPath { get; set; }
        public string OldFilesRepoPath { get; set; }
    }
}
