using IHA_Backend.Core.Entities;

namespace IHA_Backend.Business.Interfaces
{
    /// <summary>
    /// Havalimanı işlemleri için iş mantığı sözleşmesi.
    /// </summary>
    public interface IAirportService
    {
        Task<IEnumerable<Airport>> GetAllAsync();
        Task<Airport?> GetByIdAsync(string id);
        Task<Airport> AddAsync(Airport airport);
        Task DeleteAsync(string id);
    }
}
