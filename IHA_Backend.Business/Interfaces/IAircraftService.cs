using IHA_Backend.Core.Entities;

namespace IHA_Backend.Business.Interfaces
{
    /// <summary>
    /// Uçak işlemleri için iş mantığı sözleşmesi.
    /// </summary>
    public interface IAircraftService
    {
        Task<IEnumerable<Aircraft>> GetAllAsync();
        Task<Aircraft> AddOrUpdateAsync(Aircraft aircraft);
        Task DeleteAsync(int id);
        Task DeleteAllAsync();
    }
}
