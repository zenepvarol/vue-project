using System.Collections.Concurrent;
using System.Collections.Generic;
using IHA_Backend.Core.DTOs;
using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Interfaces;

namespace IHA_Backend.Business.Services
{
    /// <summary>
    /// TelemetryService: Uygulama ömrü boyunca (Singleton) uçuş verilerini RAM belleğinde yöneten servis.
    /// Yüksek performans ve thread-safety sağlamak amacıyla ConcurrentDictionary kullanılmıştır.
    /// </summary>
    public class TelemetryService : ITelemetryService
    {
        // Thread-safe Dictionary: Eşzamanlı (multi-threaded) veri erişimi ve güncellemeleri için güvenli depolama.
        private readonly ConcurrentDictionary<string, TelemetryDataDto> _activeFlights = new();
        private readonly INotificationService _notificationService;

        public TelemetryService(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Gelen telemetri paketini işleyerek bellekteki veriyi günceller veya yeni uçuş kaydı oluşturur.
        /// </summary>
        public bool UpdateTelemetry(TelemetryDataDto telemetryData, string controllerName)
        {
            if (_activeFlights.TryGetValue(telemetryData.Icao, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.ControlledBy) && existing.ControlledBy != controllerName)
                {
                    return false; // Farklı bir admin kontrol ediyor
                }
            }

            // Uçuşu kontrol eden kullanıcıyı kaydet
            telemetryData.ControlledBy = controllerName;

            TelemetryDataDto? oldData = null;
            _activeFlights.AddOrUpdate(telemetryData.Icao, telemetryData, (key, oldValue) =>
            {
                oldData = oldValue;
                return telemetryData;
            });

            // Durum değişikliğini kontrol et ve gerekirse tüm istemcilere bildir
            CheckAndBroadcastStatusChange(oldData, telemetryData);
            return true;
        }

        /// <summary>
        /// Belirtilen ICAO koduna sahip uçağın telemetri takibini sonlandırır ve veriyi bellekten temizler.
        /// </summary>
        public bool EndMission(string icao, string controllerName)
        {
            if (_activeFlights.TryGetValue(icao, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.ControlledBy) && existing.ControlledBy != controllerName)
                {
                    return false; // Farklı bir admin sonlandırmaya çalışıyor
                }
                return _activeFlights.TryRemove(icao, out _);
            }
            return true;
        }

        /// <summary>
        /// Mevcut tüm aktif uçuşların telemetri verilerini koleksiyon olarak döndürür.
        /// </summary>
        public IEnumerable<TelemetryDataDto> GetActiveFlights()
        {
            return _activeFlights.Values;
        }

        /// <summary>
        /// Uçağın durum değişikliklerini takip eder ve kritik durumlarda SignalR üzerinden gerçek zamanlı bildirim yayınlar.
        /// </summary>
        private void CheckAndBroadcastStatusChange(TelemetryDataDto? oldData, TelemetryDataDto newData)
        {
            string oldStatus = oldData?.Status ?? string.Empty;
            string newStatus = newData.Status;

            // Durum değişmediyse işlem yapma
            if (oldStatus == newStatus) return;

            string type = "info";
            string title = string.Empty;
            string text = string.Empty;

            // Durum geçişlerine göre bildirim içeriklerini hazırla
            if ((string.IsNullOrEmpty(oldStatus) || oldStatus == "STANDBY") && 
                (newStatus == "GOING_TO_DEP" || newStatus == "GOING_TO_DEST" || newStatus == "ACTIVE"))
            {
                type = "info";
                title = "UÇUŞ BAŞLADI";
                text = $"Birim: <b>{newData.Callsign}</b> hedefe doğru yola çıktı.";
            }
            else if (newStatus == "MISSION_COMPLETE")
            {
                type = "success";
                title = "HEDEF İMHA EDİLDİ";
                text = $"Birim: <b>{newData.Callsign}</b><br>Görev tamamlandı, üsse dönülüyor!";
            }
            else if (newStatus == "EMERGENCY_LANDED")
            {
                type = "warning";
                title = "ACİL İNİŞ YAPILDI";
                text = $"Birim: <b>{newData.Callsign}</b> en yakın meydan veya güvenli bölgeye acil iniş gerçekleştirdi.";
            }
            else if (newStatus == "ARRIVED")
            {
                type = "success";
                title = "HEDEFE VARILDI";
                text = $"Birim: <b>{newData.Callsign}</b><br>Manuel rota ve ikmal tamamlandı.";
            }
            else if (newStatus == "COMPLETED")
            {
                type = "info";
                title = "GÖREV TAMAMLANDI";
                text = $"Birim: <b>{newData.Callsign}</b> rotasını tamamladı, ikmal yapıldı.";
            }
            else if (oldStatus == "RETURNING" && newStatus == "STANDBY")
            {
                type = "info";
                title = "ÜSSE DÖNÜLDÜ";
                text = $"Birim: <b>{newData.Callsign}</b> üsse dönerek iniş yaptı. İkmal tamamlandı.";
            }

            // Eğer tanımlanmış bir bildirim varsa tüm SignalR istemcilerine yayınla (fire-and-forget)
            if (!string.IsNullOrEmpty(title))
            {
                _ = _notificationService.SendNotificationAsync(type, title, text, newData.Icao);
            }
        }
    }
}
