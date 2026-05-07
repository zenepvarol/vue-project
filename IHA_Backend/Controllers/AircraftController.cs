/** AircraftController.cs - Bu dosyadaki verilere erişmek için [Authorize] kilidi gereklidir. 
 * Sadece geçerli bir anahtarı (token) olan kullanıcılar uçak verilerini görebilir. */
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using Microsoft.AspNetCore.Authorization;

namespace IHA_Backend.Controllers
{
    [Authorize] // Sadece geçerli bir JWT Token'ı olanlar bu sınıfa erişebilir.
    [Route("api/[controller]")]
    [ApiController]
    public class AircraftController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AircraftController(AppDbContext context)
        {
            _context = context;
        }

        // Bütün uçakları listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Aircraft>>> GetAircrafts()
        {
            return await _context.Aircrafts.ToListAsync();
        }

        // Yeni bir uçak verisi kaydet veya mevcut olanı güncelle (Upsert Mantığı)
        [HttpPost]
        public async Task<ActionResult<Aircraft>> PostAircraft(Aircraft aircraft)
        {
            var currentIcao = aircraft.Icao24?.ToUpper();
            var existingAircraft = await _context.Aircrafts
                .FirstOrDefaultAsync(u => u.Icao24.ToUpper() == currentIcao);

            if (existingAircraft != null)
            {
                // Varsa verileri güncelle
                existingAircraft.Callsign = aircraft.Callsign;
                existingAircraft.ModelType = aircraft.ModelType;
                existingAircraft.Latitude = aircraft.Latitude;
                existingAircraft.Longitude = aircraft.Longitude;
                existingAircraft.Speed = aircraft.Speed;
                existingAircraft.Fuel = aircraft.Fuel;
                existingAircraft.Status = aircraft.Status;
                existingAircraft.IsSiha = aircraft.IsSiha;
                _context.Aircrafts.Update(existingAircraft);
            }
            else
            {
                // Yoksa yeni ekle
                _context.Aircrafts.Add(aircraft);
            }

            await _context.SaveChangesAsync();
            return Ok(aircraft);
        }

        // Belirli bir ID'ye göre uçağı sil
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAircraft(int id)
        {
            var aircraft = await _context.Aircrafts.FindAsync(id);
            if (aircraft == null) return NotFound();

            _context.Aircrafts.Remove(aircraft);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Bütün uçakları sil
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllAircrafts()
        {
            var all = await _context.Aircrafts.ToListAsync();
            _context.Aircrafts.RemoveRange(all);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}