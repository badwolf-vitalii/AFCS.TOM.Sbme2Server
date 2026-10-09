using AFCS.TOM.Sbme2Server.Configurations;
using NLog;
using Renci.SshNet;
using System.Text;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public class SftpBackgroundService : BackgroundService
    {
        private static Logger Logger => LogManager.GetLogger("Sbme2Server");

        private IConfiguration _config { get; }

        public SftpBackgroundService(IConfiguration config) => _config = config;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var sftpConfig = new SFTPConfirmTSCRequestConfiguration();
            _config.GetSection("SFTPConfirmTSCRequestConfiguration").Bind(sftpConfig);

            if (sftpConfig == null ||
                !sftpConfig.SendXml ||
                string.IsNullOrWhiteSpace(sftpConfig.Username) ||
                string.IsNullOrWhiteSpace(sftpConfig.ServerURL) ||
                string.IsNullOrWhiteSpace(sftpConfig.Password) ||
                string.IsNullOrWhiteSpace(sftpConfig.ServerURL) ||
                string.IsNullOrWhiteSpace(sftpConfig.Path))
            {
                Logger?.Debug("SFTPConfirmTSCRequestConfiguration not configured. SftpBackgroundService is being disabled.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(sftpConfig.LocalRepoPath)) return;

                    if (!Directory.Exists(sftpConfig.LocalRepoPath))
                        Directory.CreateDirectory(sftpConfig.LocalRepoPath);

                    var files = Directory.GetFiles(sftpConfig.LocalRepoPath, "*.xml");
                    foreach (var file in files)
                    {
                        var xml = await File.ReadAllTextAsync(file);
                        try
                        {
                            var responseToSend = Encoding.ASCII.GetBytes(xml);
                            using (var client = new SftpClient(sftpConfig.ServerURL, sftpConfig.Username, sftpConfig.Password))
                            {
                                client.Connect();
                                using (var memoryStream = new MemoryStream())
                                {
                                    await memoryStream.WriteAsync(responseToSend, 0, responseToSend.Length);
                                    memoryStream.Position = 0;
                                    client.UploadFile(memoryStream, $"{sftpConfig.Path}/{Path.GetFileName(file)}");
                                }
                            }

                            if (!Directory.Exists(sftpConfig.OldFilesRepoPath))
                                try
                                {
                                    Directory.CreateDirectory(sftpConfig.OldFilesRepoPath);
                                }
                                catch
                                {
                                    sftpConfig.OldFilesRepoPath = string.Empty;
                                }
                            if (Directory.Exists(sftpConfig.OldFilesRepoPath))
                                try
                                {
                                    File.Copy(file, $"{sftpConfig.OldFilesRepoPath}/{Path.GetFileName(file)}", true);
                                }
                                catch
                                {
                                }
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Logger?.Error(ex);
                        }
                    }

                    await Task.Delay(30_000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
            }
        }
    }

    //public class SftpHostedService : IHostedService
    //{
    //    public SftpHostedService()
    //    {
    //    }

    //    public Task StartAsync(CancellationToken cancellationToken)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task StopAsync(CancellationToken cancellationToken)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
