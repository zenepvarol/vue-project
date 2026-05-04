/** UcakController.cs - Bu dosyadaki verilere erişmek için [Authorize] kilidi gereklidir. 
 * Sadece geçerli bir anahtarı (token) olan kullanıcılar uçak verilerini görebilir. */
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IHA_Backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace IHA_Backend.Controllers
{
    [Authorize] // Sadece geçerli bir JWT Token'ı olanlar bu sınıfa erişebilir.
    [Route("api/[controller]")]
    [ApiController]
    public class UcakController : ControllerBase
    {
        private readonly UygulamaDbContext _context;

        public UcakController(UygulamaDbContext context)
        {
            _context = context;
        }

        // Bütün uçakları listele
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Ucak>>> GetUcaklar()
        {
            return await _context.Ucaklar.ToListAsync();
        }

        // Yeni bir uçak verisi kaydet veya mevcut olanı güncelle (Upsert Mantığı)
        [HttpPost]
        public async Task<ActionResult<Ucak>> PostUcak(Ucak ucak)
        {
            var currentIcao = ucak.Icao24?.ToUpper();
            var existingUcak = await _context.Ucaklar
                .FirstOrDefaultAsync(u => u.Icao24.ToUpper() == currentIcao);

            if (existingUcak != null)
            {
                // Varsa verileri güncelle
                existingUcak.Callsign = ucak.Callsign;
                existingUcak.ModelType = ucak.ModelType;
                existingUcak.Latitude = ucak.Latitude;
                existingUcak.Longitude = ucak.Longitude;
                existingUcak.Speed = ucak.Speed;
                existingUcak.Fuel = ucak.Fuel;
                existingUcak.Status = ucak.Status;
                existingUcak.IsSiha = ucak.IsSiha;
                _context.Ucaklar.Update(existingUcak);
            }
            else
            {
                // Yoksa yeni ekle
                _context.Ucaklar.Add(ucak);
            }

            await _context.SaveChangesAsync();
            return Ok(ucak);
        }

        // Belirli bir ID'ye göre uçağı sil
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUcak(int id)
        {
            var ucak = await _context.Ucaklar.FindAsync(id);
            if (ucak == null) return NotFound();

            _context.Ucaklar.Remove(ucak);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Bütün uçakları sil
        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAllUcaklar()
        {
            var all = await _context.Ucaklar.ToListAsync();
            _context.Ucaklar.RemoveRange(all);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}