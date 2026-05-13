using System.Collections.Generic;
using IHA_Backend.Core.DTOs;

namespace IHA_Backend.Business.Interfaces
{
    public interface ITelemetryService
    {
        // Uçağın konumunu RAM'de günceller (Yoksa ekler)
        void UpdateTelemetry(TelemetryDataDto telemetryData);
        
        // Uçak inince RAM'den siler
        void EndMission(string icao);
        
        // Havada olan tüm uçuşları liste olarak döner
        IEnumerable<TelemetryDataDto> GetActiveFlights();
    }
}
