using Microsoft.AspNetCore.SignalR;

namespace AFCS.TOM.Sbme2Server.Services.Dashboard
{
    public class MessageHub : Hub<IMessageHubClient>
    {
        public async Task SetDeviceStatus(long deviceId, bool status) => await Clients.All.SetDeviceStatus(deviceId, status);
    }
}
