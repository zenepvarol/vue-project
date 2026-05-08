using IHA_Backend.Core.Entities;

namespace IHA_Backend.Business.Interfaces
{
    /// <summary>
    /// Uçuş geçmişi işlemleri için iş mantığı sözleşmesi.
    /// </summary>
    public interface IFlightHistoryService
    {
        Task<IEnumerable<FlightHistory>> GetAllAsync();
        Task<IEnumerable<FlightHistory>> GetByIcaoAsync(string icao);
        Task<FlightHistory> AddAsync(FlightHistory history);
        Task<bool> DeleteByIcaoAsync(string icao);
        Task<bool> DeleteAllAsync();
    }
}
