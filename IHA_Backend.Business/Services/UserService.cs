using IHA_Backend.Business.Interfaces;
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using IHA_Backend.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IHA_Backend.Business.Services
{
    /// <summary>
    /// Kullanıcı iş mantığı servisi.
    /// Kimlik doğrulama ve JWT üretimini yönetir.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<User> _genericRepository;
        private readonly IConfiguration _configuration;

        public UserService(AppDbContext context, IGenericRepository<User> genericRepository, IConfiguration configuration)
        {
            _context = context;
            _genericRepository = genericRepository;
            _configuration = configuration;
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _genericRepository.GetAllAsync();
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
        }

        public async Task<User?> RegisterAsync(User user)
        {
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                return null; // Kullanıcı adı zaten alınmış
            }

            await _genericRepository.AddAsync(user);
            await _genericRepository.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Kullanıcıya ait kimlik bilgilerini içeren şifreli JWT anahtarını üretir.
        /// </summary>
        public string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(5),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
