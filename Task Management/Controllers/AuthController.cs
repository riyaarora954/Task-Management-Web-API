using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TM.Contracts.Auth;
using TM.ServiceLogic.Interfaces;

namespace TM.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // Register Endpoint
        [HttpPost("register")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthController] POST register | Email={Email} Role={Role}", request.Email, request.Role);

            try
            {
                var response = await _authService.RegisterAsync(request);

                sw.Stop();

                if (response == null)
                {
                    _logger.LogWarning("[AuthController] POST register | FAILED duplicate email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                    return BadRequest(new { message = "Email already exists or registration failed." });
                }

                _logger.LogInformation("[AuthController] POST register | SUCCESS UserId={UserId} | {Elapsed}ms", response.Id, sw.ElapsedMilliseconds);
                return Ok(response);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthController] POST register | ERROR Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Registration error.", error = ex.Message });
            }
        }

        // Login Endpoint
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthController] POST login | Email={Email}", request.Email);

            try
            {
                var response = await _authService.LoginAsync(request);

                sw.Stop();

                if (response == null)
                {
                    _logger.LogWarning("[AuthController] POST login | FAILED invalid credentials Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                    return Unauthorized(new { message = "Invalid email or password." });
                }

                _logger.LogInformation("[AuthController] POST login | SUCCESS UserId={UserId} Role={Role} | {Elapsed}ms", response.Id, response.Role, sw.ElapsedMilliseconds);
                return Ok(response);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthController] POST login | ERROR Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Login error.", error = ex.Message });
            }
        }

        // GetAllUsers Endpoint
        [HttpGet("users")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllUsers()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized(new { message = "You are not authenticated. Please provide a valid token." });

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthController] GET users");

            try
            {
                var users = await _authService.GetUsersByRoleAsync("User");

                sw.Stop();

                if (users == null || !users.Any())
                {
                    _logger.LogWarning("[AuthController] GET users | No users found | {Elapsed}ms", sw.ElapsedMilliseconds);
                    return NotFound(new { message = "No users found in the system." });
                }

                _logger.LogInformation("[AuthController] GET users | Count={Count} | {Elapsed}ms", users.Count(), sw.ElapsedMilliseconds);
                return Ok(users);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthController] GET users | ERROR | {Elapsed}ms", sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error retrieving users.", error = ex.Message });
            }
        }

        // GetAllAdmins Endpoint
        [HttpGet("admins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllAdmins()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Unauthorized(new { message = "You are not authenticated. Please provide a valid token." });

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthController] GET admins");

            try
            {
                var admins = await _authService.GetUsersByRoleAsync("Admin");

                sw.Stop();

                if (admins == null || !admins.Any())
                {
                    _logger.LogWarning("[AuthController] GET admins | No admins found | {Elapsed}ms", sw.ElapsedMilliseconds);
                    return NotFound(new { message = "No admins found in the system." });
                }

                _logger.LogInformation("[AuthController] GET admins | Count={Count} | {Elapsed}ms", admins.Count(), sw.ElapsedMilliseconds);
                return Ok(admins);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthController] GET admins | ERROR | {Elapsed}ms", sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error retrieving admins.", error = ex.Message });
            }
        }

        // Delete User Endpoint
        [HttpDelete("users/{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthController] DELETE users/{UserId}", id);

            try
            {
                if (id <= 0)
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthController] DELETE users | Invalid UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return BadRequest(new { message = "Invalid User ID." });
                }

                var (success, message) = await _authService.SoftDeleteUserAsync(id);

                sw.Stop();

                if (!success)
                {
                    _logger.LogWarning("[AuthController] DELETE users/{UserId} | FAILED {Message} | {Elapsed}ms", id, message, sw.ElapsedMilliseconds);
                    return BadRequest(new { message });
                }

                _logger.LogInformation("[AuthController] DELETE users/{UserId} | SUCCESS | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthController] DELETE users/{UserId} | ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Delete error.", error = ex.Message });
            }
        }
    }
}