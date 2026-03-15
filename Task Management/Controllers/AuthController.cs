using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var response = await _authService.RegisterAsync(request);
                if (response == null)
                    return BadRequest(new { message = "Email already exists or registration failed." });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Registration error.", error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var response = await _authService.LoginAsync(request);
                if (response == null)
                    return Unauthorized(new { message = "Invalid email or password." });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Login error.", error = ex.Message });
            }
        }

        [HttpGet("users")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllUsers()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized(new { message = "You are not authenticated. Please provide a valid token." });

            try
            {
                var users = await _authService.GetUsersByRoleAsync("User");
                // Handling empty database response
                if (users == null || !users.Any())
                    return NotFound(new { message = "No users found in the system." });

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving users.", error = ex.Message });
            }
        }

        [HttpGet("admins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllAdmins()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized(new { message = "You are not authenticated. Please provide a valid token." });

            try
            {
                var admins = await _authService.GetUsersByRoleAsync("Admin");

                // Handling empty database response
                if (admins == null || !admins.Any())
                    return NotFound(new { message = "No admins found in the system." });

                return Ok(admins);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving admins.", error = ex.Message });
            }
        }

        [HttpDelete("users/{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                if (id <= 0) return BadRequest(new { message = "Invalid User ID." });

                var (success, message) = await _authService.SoftDeleteUserAsync(id);
                if (!success) return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Delete error.", error = ex.Message });
            }
        }
    }
}