using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using IHA_Backend.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IHA_Backend.Business.Services
{
    /// <summary>
    /// Uçuş geçmişi iş mantığı servisi.
    /// </summary>
    public class FlightHistoryService : IFlightHistoryService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<FlightHistory> _genericRepository;

        public FlightHistoryService(AppDbContext context, IGenericRepository<FlightHistory> genericRepository)
        {
            _context = context;
            _genericRepository = genericRepository;
        }

        public async Task<IEnumerable<FlightHistory>> GetAllAsync()
        {
            return await _context.FlightHistories
                .OrderByDescending(f => f.FlightDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<FlightHistory>> GetByIcaoAsync(string icao)
        {
            return await _context.FlightHistories
                .Where(f => f.Icao.ToUpper() == icao.ToUpper())
                .OrderByDescending(f => f.FlightDate)
                .ToListAsync();
        }

        public async Task<FlightHistory> AddAsync(FlightHistory history)
        {
            history.FlightDate = DateTime.UtcNow;
            await _genericRepository.AddAsync(history);
            await _genericRepository.SaveChangesAsync();
            return history;
        }

        public async Task<bool> DeleteByIcaoAsync(string icao)
        {
            var history = await _context.FlightHistories
                .Where(f => f.Icao.ToUpper() == icao.ToUpper())
                .ToListAsync();

            if (history.Count == 0) return false;

            _context.FlightHistories.RemoveRange(history);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAllAsync()
        {
            var history = await _context.FlightHistories.ToListAsync();
            if (history.Count == 0) return false;

            _context.FlightHistories.RemoveRange(history);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
