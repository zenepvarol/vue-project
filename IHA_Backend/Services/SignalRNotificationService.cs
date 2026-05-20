using IHA_Backend.Core.Interfaces;
using IHA_Backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace IHA_Backend.Services
{
    /// <summary>
    /// SignalRNotificationService: INotificationService arayüzünün SignalR tabanlı implementasyonudur.
    /// Web API katmanında yer alarak alt katmanların (Business/Core) SignalR kütüphanesine bağımlı olmasını önler.
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// SignalR Hub üzerinden tüm bağlı istemcilere bildirim yayınlar.
        /// </summary>
        public async Task SendNotificationAsync(string type, string title, string text, string icao)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", type, title, text, icao);
        }
    }
}
