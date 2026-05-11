using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using IHA_Backend.Repository.Interfaces;
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
        private readonly IGenericRepository<Airport> _genericRepository;

        public AirportService(AppDbContext context, IGenericRepository<Airport> genericRepository)
        {
            _context = context;
            _genericRepository = genericRepository;
        }

        public async Task<IEnumerable<Airport>> GetAllAsync()
        {
            return await _genericRepository.GetAllAsync();
        }

        public async Task<Airport?> GetByIdAsync(string id)
        {
            return await _context.Airports.FindAsync(id);
        }

        public async Task<Airport> AddAsync(Airport airport)
        {
            await _genericRepository.AddAsync(airport);
            await _genericRepository.SaveChangesAsync();
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
