/** AircraftController.cs - Bu dosyadaki verilere erişmek için [Authorize] kilidi gereklidir. 
 * Sadece geçerli bir anahtarı (token) olan kullanıcılar uçak verilerini görebilir. */
using Microsoft.AspNetCore.Mvc;
using IHA_Backend.Core.Entities;
using IHA_Backend.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace IHA_Backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AircraftController : ControllerBase
    {
        private readonly IAircraftService _aircraftService;

        public AircraftController(IAircraftService aircraftService)
        {
            _aircraftService = aircraftService;
        }

        // Bütün uçakları listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Aircraft>>> GetAircrafts()
        {
            var aircrafts = await _aircraftService.GetAllAsync();
            return Ok(aircrafts);
        }

        // Yeni bir uçak verisi kaydet veya mevcut olanı güncelle (Upsert Mantığı)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Aircraft>> PostAircraft(Aircraft aircraft)
        {
            var result = await _aircraftService.AddOrUpdateAsync(aircraft);
            return Ok(result);
        }

        // Belirli bir ID'ye göre uçağı sil
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAircraft(int id)
        {
            await _aircraftService.DeleteAsync(id);
            return NoContent();
        }

        // Bütün uçakları sil
        [Authorize(Roles = "Admin")]
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllAircrafts()
        {
            await _aircraftService.DeleteAllAsync();
            return NoContent();
        }
    }
}