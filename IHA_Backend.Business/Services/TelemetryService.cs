using System.Collections.Concurrent;
using System.Collections.Generic;
using IHA_Backend.Core.DTOs;
using IHA_Backend.Business.Interfaces;

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

        /// <summary>
        /// Gelen telemetri paketini işleyerek bellekteki veriyi günceller veya yeni uçuş kaydı oluşturur.
        /// </summary>
        public void UpdateTelemetry(TelemetryDataDto telemetryData)
        {
            _activeFlights.AddOrUpdate(telemetryData.Icao, telemetryData, (key, oldValue) => telemetryData);
        }

        /// <summary>
        /// Belirtilen ICAO koduna sahip uçağın telemetri takibini sonlandırır ve veriyi bellekten temizler.
        /// </summary>
        public void EndMission(string icao)
        {
            _activeFlights.TryRemove(icao, out _);
        }

        /// <summary>
        /// Mevcut tüm aktif uçuşların telemetri verilerini koleksiyon olarak döndürür.
        /// </summary>
        public IEnumerable<TelemetryDataDto> GetActiveFlights()
        {
            return _activeFlights.Values;
        }
    }
}
