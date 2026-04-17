using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IHA_Backend.Models;

namespace IHA_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UcakController : ControllerBase
    {
        private readonly UygulamaDbContext _context;

        public UcakController(UygulamaDbContext context)
        {
            _context = context;
        }

        // Bütün uçakları listele (Radar ekranı için)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Ucak>>> GetUcaklar()
        {
            return await _context.Ucaklar.ToListAsync();
        }

        // Yeni bir uçak verisi kaydet (Simülasyondan gelen veri için)
        [HttpPost]
        public async Task<ActionResult<Ucak>> PostUcak(Ucak ucak)
        {
            _context.Ucaklar.Add(ucak);
            await _context.SaveChangesAsync();
            return Ok(ucak);
        }
    }
}