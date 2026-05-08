/** UsersController.cs - Kullanıcı kimlik bilgilerini doğrulayan ve başarılı oturumlar için 
 * JWT üretimini sağlayan denetleyici. */
using IHA_Backend.Core.Entities;
using IHA_Backend.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IHA_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // POST: api/Users/login
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
        {
            var user = await _userService.LoginAsync(request.Username, request.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Hatalı kullanıcı adı veya şifre!" });
            }

            var token = _userService.GenerateJwtToken(user);

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role,
                isAuthenticated = true,
                token = token
            });
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            var users = await _userService.GetAllAsync();
            return Ok(users);
        }

        // POST: api/Users/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] User user)
        {
            var result = await _userService.RegisterAsync(user);
            if (result == null)
            {
                return BadRequest("Bu kullanıcı adı zaten alınmış.");
            }
            return Ok(result);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
