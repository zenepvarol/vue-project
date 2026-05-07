using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;

namespace IHA_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AirportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AirportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Airport>>> GetAirports()
        {
            return await _context.Airports.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Airport>> PostAirport(Airport airport)
        {
            _context.Airports.Add(airport);
            await _context.SaveChangesAsync();
            return Ok(airport);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Airport>> GetAirport(string id)
        {
            var airport = await _context.Airports.FindAsync(id);
            if (airport == null) return NotFound();
            return airport;
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAirport(string id)
        {
            var airport = await _context.Airports.FindAsync(id);
            if (airport == null) return NotFound();

            _context.Airports.Remove(airport);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
