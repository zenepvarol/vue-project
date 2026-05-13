using Microsoft.AspNetCore.Mvc;
using IHA_Backend.Core.Entities;
using IHA_Backend.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace IHA_Backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AirportsController(IAirportService airportService) : ControllerBase
    {
        private readonly IAirportService _airportService = airportService;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Airport>>> GetAirports()
        {
            var airports = await _airportService.GetAllAsync();
            return Ok(airports);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Airport>> PostAirport(Airport airport)
        {
            var result = await _airportService.AddAsync(airport);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Airport>> GetAirport(string id)
        {
            var airport = await _airportService.GetByIdAsync(id);
            if (airport == null) return NotFound();
            return airport;
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAirport(string id)
        {
            var airport = await _airportService.GetByIdAsync(id);
            if (airport == null) return NotFound();

            await _airportService.DeleteAsync(id);
            return NoContent();
        }
    }
}
