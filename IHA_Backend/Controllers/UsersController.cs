/** UsersController.cs - Kullanıcı kimlik bilgilerini doğrulayan ve başarılı oturumlar için 
 * JWT üretimini sağlayan denetleyici. */
using IHA_Backend.Core.Entities;
using IHA_Backend.Repository.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IHA_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UygulamaDbContext _context;
        private readonly IConfiguration _configuration;

        public UsersController(UygulamaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/Users/login
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
        {
            // Kullanıcı veritabanı sorgusu yürütülür.
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Hatalı kullanıcı adı veya şifre!" });
            }

            // Kimlik doğrulaması başarılı olan kullanıcı için benzersiz bir erişim anahtarı (JWT) üretilir.
            var token = GenerateJwtToken(user);

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role,
                isAuthenticated = true,
                token = token // İstemci tarafında yetkilendirme işlemleri için kullanılacak anahtar.
            });
        }

        /** Kullanıcıya ait kimlik bilgilerini içeren şifreli JWT anahtarını üreten metot. */
        private string GenerateJwtToken(User user)
        {
            // Yapılandırma dosyasındaki gizli anahtar (Secret Key) üzerinden güvenlik anahtarı oluşturulur.
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            
            // HmacSha256 algoritması kullanılarak imzalama kimlik bilgileri tanımlanır.
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Token içerisine yerleştirilecek kullanıcıya ait talepler (Claims) tanımlanır.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // Belirlenen parametreler doğrultusunda JWT nesnesi yapılandırılır.
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                // expires: DateTime.Now.AddHours(1),
                expires: DateTime.Now.AddMinutes(5),
                signingCredentials: credentials);

            // Yapılandırılan nesne, iletilebilir string formatına dönüştürülür.
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // POST: api/Users/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] User user)
        {
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                return BadRequest("Bu kullanıcı adı zaten alınmış.");
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(user);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
