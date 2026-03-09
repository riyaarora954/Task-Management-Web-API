using Microsoft.AspNetCore.Mvc;
using TM.Contracts.Auth;
using TM.ServiceLogic.Interfaces;

namespace TM.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var response = await _authService.RegisterAsync(request);

            if (response == null)
                return BadRequest("Username already exists.");
            return Ok(response);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            if (response == null) return Unauthorized("Invalid username or password.");
            return Ok(response);
        }
    }
}
