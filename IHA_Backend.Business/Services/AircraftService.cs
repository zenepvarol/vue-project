using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using IHA_Backend.Repository.Interfaces;
using IHA_Backend.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IHA_Backend.Business.Services
{
    /// <summary>
    /// Uçak iş mantığı servisi.
    /// ICAO koduna göre upsert (ekle veya güncelle) mantığını içerir.
    /// </summary>
    public class AircraftService : IAircraftService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Aircraft> _genericRepository;

        public AircraftService(AppDbContext context, IGenericRepository<Aircraft> genericRepository)
        {
            _context = context;
            _genericRepository = genericRepository;
        }

        public async Task<IEnumerable<Aircraft>> GetAllAsync()
        {
            return await _genericRepository.GetAllAsync();
        }

        public async Task<Aircraft> AddOrUpdateAsync(Aircraft aircraft)
        {
            var currentIcao = aircraft.Icao24?.ToUpper();
            var existingAircraft = await _context.Aircrafts
                .FirstOrDefaultAsync(u => u.Icao24.ToUpper() == currentIcao);

            if (existingAircraft != null)
            {
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
                _context.Aircrafts.Add(aircraft);
            }

            await _context.SaveChangesAsync();
            return aircraft;
        }

        public async Task DeleteAsync(int id)
        {
            var aircraft = await _context.Aircrafts.FindAsync(id);
            if (aircraft != null)
            {
                _context.Aircrafts.Remove(aircraft);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAllAsync()
        {
            var all = await _context.Aircrafts.ToListAsync();
            _context.Aircrafts.RemoveRange(all);
            await _context.SaveChangesAsync();
        }
    }
}
