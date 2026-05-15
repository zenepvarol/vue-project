using Microsoft.AspNetCore.Mvc;
using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

namespace IHA_Backend.Controllers
{
    /// <summary>
    /// TelemetryController: Hava araçlarından gelen anlık telemetri verilerinin yönetimini sağlayan API katmanı.
    /// Bu kontrolcü, Singleton servis üzerinden bellek içi (in-memory) veri akışını koordine eder.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TelemetryController(ITelemetryService telemetryService) : ControllerBase
    {
        private readonly ITelemetryService _telemetryService = telemetryService;

        /// <summary>
        /// Sistem genelindeki tüm aktif uçuşların listesini döndürür. 
        /// Viewer (İzleyici) rolü için ana veri kaynağıdır.
        /// </summary>
        [HttpGet("active-flights")]
        public ActionResult<IEnumerable<TelemetryDataDto>> GetActiveFlights()
        {
            return Ok(_telemetryService.GetActiveFlights());
        }

        /// <summary>
        /// Hava aracından gelen güncel telemetri paketini işleyerek Singleton serviste günceller.
        /// Sadece yetkili Admin kullanıcıları tarafından erişilebilir.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("update")]
        public IActionResult UpdateTelemetry([FromBody] TelemetryDataDto telemetryData)
        {
            _telemetryService.UpdateTelemetry(telemetryData);
            return Ok(new { message = "Telemetri verisi başarıyla senkronize edildi." });
        }

        /// <summary>
        /// Uçuş görevi tamamlandığında veya acil iniş gerçekleştiğinde telemetri kaydını sonlandırır.
        /// Veri bütünlüğü için uçuş verisini RAM (In-Memory) belleğinden temizler.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("end/{icao}")]
        public IActionResult EndMission(string icao)
        {
            _telemetryService.EndMission(icao);
            return Ok(new { message = "Uçuş görevi başarıyla sonlandırıldı ve bellek temizlendi." });
        }
    }
}
