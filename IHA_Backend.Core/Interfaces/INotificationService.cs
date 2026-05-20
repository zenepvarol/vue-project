using System.Threading.Tasks;

namespace IHA_Backend.Core.Interfaces
{
    /// <summary>
    /// INotificationService: Operasyonel ve durum bildirimlerini istemcilere ileten servis arayüzü.
    /// Clean Architecture prensiplerine göre Business katmanının Hub bağımlılıklarından ayrıştırılmasını sağlar.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// İlgili bildirimi gerçek zamanlı olarak istemcilere gönderir.
        /// </summary>
        Task SendNotificationAsync(string type, string title, string text, string icao);
    }
}
