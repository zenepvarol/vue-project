using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace IHA_Backend.Hubs
{
    /// <summary>
    /// NotificationHub: İHA operasyonları ve durum değişiklikleri için gerçek zamanlı bildirimleri
    /// tüm bağlı istemcilere yayınlayan SignalR Hub birimidir.
    /// </summary>
    public class NotificationHub : Hub
    {
        /// <summary>
        /// İstemcilerden gelen veya istemcilere yayınlanan sistem bildirimlerini yönetir.
        /// </summary>
        public async Task SendNotification(string type, string title, string text, string icao)
        {
            await Clients.All.SendAsync("ReceiveNotification", type, title, text, icao);
        }
    }
}
