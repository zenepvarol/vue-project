using Microsoft.AspNetCore.Mvc;
using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

namespace IHA_Backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TelemetryController(ITelemetryService telemetryService) : ControllerBase
    {
        private readonly ITelemetryService _telemetryService = telemetryService;

        // Herkes (Viewer dahil) havada kimler olduğunu görebilir
        [HttpGet("active-flights")]
        public ActionResult<IEnumerable<TelemetryDataDto>> GetActiveFlights()
        {
            return Ok(_telemetryService.GetActiveFlights());
        }

        // Sadece Admin telemetri verisini güncelleyebilir
        [Authorize(Roles = "Admin")]
        [HttpPost("update")]
        public IActionResult UpdateTelemetry([FromBody] TelemetryDataDto telemetryData)
        {
            _telemetryService.UpdateTelemetry(telemetryData);
            return Ok(new { message = "Canlı telemetri güncellendi." });
        }

        // Uçuş bittiğinde RAM'den temizler
        [Authorize(Roles = "Admin")]
        [HttpPost("end/{icao}")]
        public IActionResult EndMission(string icao)
        {
            _telemetryService.EndMission(icao);
            return Ok(new { message = "Uçuş telemetri listesinden kaldırıldı." });
        }
    }
}
