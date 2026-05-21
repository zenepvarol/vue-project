using System.Collections.Generic;
using IHA_Backend.Core.DTOs;

namespace IHA_Backend.Business.Interfaces
{
    public interface ITelemetryService
    {
        // Uçağın konumunu RAM'de günceller (Yoksa ekler)
        bool UpdateTelemetry(TelemetryDataDto telemetryData, string controllerName);
        
        // Uçak inince RAM'den siler
        bool EndMission(string icao, string controllerName);
        
        // Havada olan tüm uçuşları liste olarak döner
        IEnumerable<TelemetryDataDto> GetActiveFlights();
    }
}
