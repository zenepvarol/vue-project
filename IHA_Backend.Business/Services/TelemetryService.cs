using System.Collections.Concurrent;
using System.Collections.Generic;
using IHA_Backend.Core.DTOs;
using IHA_Backend.Business.Interfaces;

namespace IHA_Backend.Business.Services
{
    public class TelemetryService : ITelemetryService
    {
        // Thread-safe Dictionary: RAM üzerinde hızlı ve güvenli veri depolama sağlar.
        private readonly ConcurrentDictionary<string, TelemetryDataDto> _activeFlights = new();

        public void UpdateTelemetry(TelemetryDataDto telemetryData)
        {
            // Eğer uçak varsa üzerine yazar, yoksa yeni bir kayıt olarak ekler.
            _activeFlights.AddOrUpdate(telemetryData.Icao, telemetryData, (key, oldValue) => telemetryData);
        }

        public void EndMission(string icao)
        {
            _activeFlights.TryRemove(icao, out _);
        }

        public IEnumerable<TelemetryDataDto> GetActiveFlights()
        {
            return _activeFlights.Values;
        }
    }
}
