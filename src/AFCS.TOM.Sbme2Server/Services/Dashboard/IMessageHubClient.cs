namespace AFCS.TOM.Sbme2Server.Services.Dashboard
{
    public interface IMessageHubClient
    {
        Task SetDeviceStatus(long deviceId, bool status);
    }
}
