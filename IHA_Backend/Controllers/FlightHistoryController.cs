using Microsoft.AspNetCore.Mvc;
using IHA_Backend.Core.Entities;
using IHA_Backend.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace IHA_Backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FlightHistoryController : ControllerBase
    {
        private readonly IFlightHistoryService _flightHistoryService;

        public FlightHistoryController(IFlightHistoryService flightHistoryService)
        {
            _flightHistoryService = flightHistoryService;
        }

        // Bütün uçuş geçmişini listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FlightHistory>>> GetAllFlightHistory()
        {
            var history = await _flightHistoryService.GetAllAsync();
            return Ok(history);
        }

        // Bir uçağın uçuş geçmişini getir
        [HttpGet("{icao}")]
        public async Task<ActionResult<IEnumerable<FlightHistory>>> GetFlightHistory(string icao)
        {
            var history = await _flightHistoryService.GetByIcaoAsync(icao);
            return Ok(history);
        }

        // ADIM 5: Gelen paketi karşıla, zaman damgasını bas ve veritabanına kalıcı olarak kaydet
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<FlightHistory>> PostFlightHistory(FlightHistory history)
        {
            var result = await _flightHistoryService.AddAsync(history);
            return Ok(result);
        }

        // Bir uçağın tüm geçmişini sil
        [Authorize(Roles = "Admin")]
        [HttpDelete("{icao}")]
        public async Task<IActionResult> DeleteFlightHistoryByIcao(string icao)
        {
            var deleted = await _flightHistoryService.DeleteByIcaoAsync(icao);
            if (!deleted) return NotFound("Bu ICAO koduna ait geçmiş bulunamadı.");
            return Ok($"{icao} kodlu uçağın tüm geçmişi silindi.");
        }

        // Bütün uçuş geçmişini sil
        [Authorize(Roles = "Admin")]
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllFlightHistory()
        {
            var deleted = await _flightHistoryService.DeleteAllAsync();
            if (!deleted) return Ok("Silinecek uçuş geçmişi bulunmuyor.");
            return Ok("Tüm uçuş geçmişi başarıyla silindi.");
        }
    }
}
