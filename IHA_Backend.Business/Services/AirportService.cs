using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace IHA_Backend.Business.Services
{
    /// <summary>
    /// Havalimanı iş mantığı servisi.
    /// Controller ile veritabanı arasındaki köprüdür.
    /// </summary>
    public class AirportService : IAirportService
    {
        private readonly AppDbContext _context;

        public AirportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Airport>> GetAllAsync()
        {
            return await _context.Airports.ToListAsync();
        }

        public async Task<Airport?> GetByIdAsync(string id)
        {
            return await _context.Airports.FindAsync(id);
        }

        public async Task<Airport> AddAsync(Airport airport)
        {
            _context.Airports.Add(airport);
            await _context.SaveChangesAsync();
            return airport;
        }

        public async Task DeleteAsync(string id)
        {
            var airport = await _context.Airports.FindAsync(id);
            if (airport != null)
            {
                _context.Airports.Remove(airport);
                await _context.SaveChangesAsync();
            }
        }
    }
}
