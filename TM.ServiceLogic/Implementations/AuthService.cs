using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TM.Contracts.Auth;
using TM.Model.Data;
using TM.Model.Entities;
using TM.ServiceLogic.Interfaces;

namespace TM.ServiceLogic.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly TMDbContext _context;
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;

        public AuthService(TMDbContext context, IMapper mapper, IConfiguration config, ILogger<AuthService> logger)
        {
            _context = context;
            _mapper = mapper;
            _config = config;
            _logger = logger;
        }

        // The RegisterAsync method checks if the email is already registered, hashes the password, assigns a role, and saves the new user to the database. It returns an AuthResponse with user details if successful, or null if the email is already in use.
        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthService] RegisterAsync | Email={Email} Role={Role}", request.Email, request.Role);

            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] RegisterAsync | DUPLICATE email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                    return null;
                }

                if (!Enum.TryParse<UserRole>(request.Role, true, out var assignedRole))
                {
                    assignedRole = UserRole.User;
                    _logger.LogWarning("[AuthService] RegisterAsync | Invalid role '{Role}' — defaulting to User", request.Role);
                }

                var user = new User
                {
                    Email = request.Email,
                    Username = request.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = assignedRole,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                sw.Stop();
                _logger.LogInformation("[AuthService] RegisterAsync | SUCCESS UserId={UserId} Role={Role} | {Elapsed}ms", user.Id, assignedRole, sw.ElapsedMilliseconds);

                return _mapper.Map<AuthResponse>(user);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthService] RegisterAsync | FAILED Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                throw new Exception($"Service Error: Unable to register user. {ex.Message}");
            }
        }

        // Verifies credentials, generates a JWT token upon success, and returns user details.
        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthService] LoginAsync | Email={Email}", request.Email);

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] LoginAsync | FAILED invalid credentials Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                    return null;
                }

                var response = _mapper.Map<AuthResponse>(user);
                response.Token = GenerateJwtToken(user);

                sw.Stop();
                _logger.LogInformation("[AuthService] LoginAsync | SUCCESS UserId={UserId} Role={Role} | {Elapsed}ms", user.Id, user.Role, sw.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthService] LoginAsync | FAILED Email={Email} | {Elapsed}ms", request.Email, sw.ElapsedMilliseconds);
                throw new Exception($"Service Error: Login failed. {ex.Message}");
            }
        }

        // Retrieves users by role, ensuring that if the role is invalid or no users are found, an empty enumerable is returned instead of null.
        public async Task<IEnumerable<UserResponse>> GetUsersByRoleAsync(string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthService] GetUsersByRoleAsync | Role={Role}", role);

            try
            {
                if (!Enum.TryParse<UserRole>(role, true, out var roleEnum))
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] GetUsersByRoleAsync | Invalid role='{Role}' | {Elapsed}ms", role, sw.ElapsedMilliseconds);
                    return Enumerable.Empty<UserResponse>();
                }

                var users = await _context.Users
                    .Where(u => !u.IsDeleted && u.Role == roleEnum)
                    .ToListAsync();

                sw.Stop();

                if (sw.ElapsedMilliseconds > 500)
                    _logger.LogWarning("[AuthService] GetUsersByRoleAsync | SLOW QUERY Role={Role} | Count={Count} | {Elapsed}ms", role, users.Count, sw.ElapsedMilliseconds);
                else
                    _logger.LogInformation("[AuthService] GetUsersByRoleAsync | Role={Role} | Count={Count} | {Elapsed}ms", role, users.Count, sw.ElapsedMilliseconds);

                return _mapper.Map<IEnumerable<UserResponse>>(users) ?? Enumerable.Empty<UserResponse>();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthService] GetUsersByRoleAsync | FAILED Role={Role} | {Elapsed}ms", role, sw.ElapsedMilliseconds);
                throw new Exception($"Service Error: Could not fetch users for role {role}. {ex.Message}");
            }
        }

        // Validates and soft-deletes a user after checking role restrictions and active task assignments.
        public async Task<(bool Success, string Message)> SoftDeleteUserAsync(int id)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[AuthService] SoftDeleteUserAsync | UserId={UserId}", id);

            try
            {
                if (id <= 0)
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] SoftDeleteUserAsync | Invalid UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return (false, "Invalid user ID.");
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
                if (user == null)
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] SoftDeleteUserAsync | NOT FOUND UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return (false, "User not found.");
                }

                if (user.Role == UserRole.SuperAdmin)
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] SoftDeleteUserAsync | BLOCKED attempt to delete SuperAdmin UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return (false, "The SuperAdmin cannot be deleted.");
                }

                bool isAssignedToAnything = await _context.Tasks.AnyAsync(t =>
                    t.AssignedToUserId == id && !t.IsDeleted);

                if (isAssignedToAnything)
                {
                    sw.Stop();
                    _logger.LogWarning("[AuthService] SoftDeleteUserAsync | BLOCKED UserId={UserId} still has active task assignments | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return (false, "User is still assigned to tasks. Please unassign them first.");
                }

                user.IsDeleted = true;
                await _context.SaveChangesAsync();

                sw.Stop();
                _logger.LogInformation("[AuthService] SoftDeleteUserAsync | SUCCESS UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);

                return (true, "User deleted successfully.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[AuthService] SoftDeleteUserAsync | FAILED UserId={UserId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return (false, $"Internal Error: {ex.Message}");
            }
        }

        // Generates a JWT token
        private string GenerateJwtToken(User user)
        {
            var key = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(key)) throw new Exception("JWT Key is missing in configuration.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.Name, user.Username),
                new System.Security.Claims.Claim(ClaimTypes.Role, user.Role.ToString()),
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Audience"],
                claims,
                expires: DateTime.UtcNow.AddHours(3),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}