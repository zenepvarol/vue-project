using IHA_Backend.Core.Entities;
using IHA_Backend.Core.DTOs;

namespace IHA_Backend.Business.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<UserDto>> GetAllAsync();
        Task<User?> LoginAsync(string username, string password);
        Task<UserDto?> RegisterAsync(User user);
        string GenerateJwtToken(User user);
    }
}
