using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IHA_Backend.Models;

namespace IHA_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlightHistoryController : ControllerBase
    {
        private readonly UygulamaDbContext _context;

        public FlightHistoryController(UygulamaDbContext context)
        {
            _context = context;
        }

        // Bütün uçuş geçmişini listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FlightHistory>>> GetAllFlightHistory()
        {
            return await _context.FlightHistories
                .OrderByDescending(f => f.FlightDate)
                .ToListAsync();
        }

        // Bir uçağın uçuş geçmişini getir
        [HttpGet("{icao}")]
        public async Task<ActionResult<IEnumerable<FlightHistory>>> GetFlightHistory(string icao)
        {
            return await _context.FlightHistories
                .Where(f => f.Icao.ToUpper() == icao.ToUpper())
                .OrderByDescending(f => f.FlightDate)
                .ToListAsync();
        }

        // Yeni uçuş kaydı ekle
        [HttpPost]
        public async Task<ActionResult<FlightHistory>> PostFlightHistory(FlightHistory history)
        {
            history.FlightDate = DateTime.UtcNow;
            _context.FlightHistories.Add(history);
            await _context.SaveChangesAsync();
            return Ok(history);
        }

        // Bir uçağın tüm geçmişini sil
        [HttpDelete("{icao}")]
        public async Task<IActionResult> DeleteFlightHistoryByIcao(string icao)
        {
            var history = await _context.FlightHistories
                .Where(f => f.Icao.ToUpper() == icao.ToUpper())
                .ToListAsync();

            if (history.Count == 0) return NotFound("Bu ICAO koduna ait geçmiş bulunamadı.");

            _context.FlightHistories.RemoveRange(history);
            await _context.SaveChangesAsync();

            return Ok($"{icao} kodlu uçağın tüm geçmişi silindi.");
        }
    }
}
