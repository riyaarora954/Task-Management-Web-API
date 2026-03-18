using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sieve.Models;
using Sieve.Services;
using TM.Contracts.Auth;
using TM.ServiceLogic.Interfaces;

namespace TM.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISieveProcessor _sieveProcessor;

        public AuthController(IAuthService authService, ISieveProcessor sieveProcessor)
        {
            _authService = authService;
            _sieveProcessor = sieveProcessor;
        }

        //Register Endpoint 
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

        //Login Endpoint
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

        // GetAllUsers EndPoint with Keyset Pagination
        [HttpGet("users")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllUsers([FromQuery] SieveModel sieveModel)
        {
            // 1. Senior Dev Safety: Force a default and a maximum limit
            sieveModel.PageSize ??= 10;
            if (sieveModel.PageSize > 100) sieveModel.PageSize = 100;

            try
            {
                var users = await _authService.GetUsersByRoleAsync("User", sieveModel);

                if (users == null || !users.Any())
                    return NotFound(new { message = "No users found for the requested page." });

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving users.", error = ex.Message });
            }
        }

        //GetAllAdmins EndPoint
        [HttpGet("admins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllAdmins([FromQuery] SieveModel sieveModel)
        {
            // 1. Safety Cap
            sieveModel.PageSize ??= 10;
            if (sieveModel.PageSize > 100) sieveModel.PageSize = 100;

            try
            {
                var admins = await _authService.GetUsersByRoleAsync("Admin", sieveModel);

                if (admins == null || !admins.Any())
                    return NotFound(new { message = "No admins found for the requested page." });

                return Ok(admins);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving admins.", error = ex.Message });
            }
        }
    

        //Delete User EndPoint
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