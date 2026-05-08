using IHA_Backend.Core.Entities;

namespace IHA_Backend.Business.Interfaces
{
    /// <summary>
    /// Kullanıcı işlemleri ve kimlik doğrulama için iş mantığı sözleşmesi.
    /// </summary>
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> LoginAsync(string username, string password);
        Task<User?> RegisterAsync(User user);
        string GenerateJwtToken(User user);
    }
}
