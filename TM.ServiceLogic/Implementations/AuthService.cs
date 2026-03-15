using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

        public AuthService(TMDbContext context, IMapper mapper, IConfiguration config)
        {
            _context = context;
            _mapper = mapper;
            _config = config;
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                    return null;

                if (!Enum.TryParse<UserRole>(request.Role, true, out var 
                    assignedRole))
                {
                    assignedRole = UserRole.User;
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

                return _mapper.Map<AuthResponse>(user);
            }
            catch (Exception ex)
            {
                // Simple logging or rethrow to be caught by Controller
                throw new Exception($"Service Error: Unable to register user. {ex.Message}");
            }
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                    return null;

                var response = _mapper.Map<AuthResponse>(user);
                response.Token = GenerateJwtToken(user);
                return response;
            }
            catch (Exception ex)
            {
                throw new Exception($"Service Error: Login failed. {ex.Message}");
            }
        }

        public async Task<IEnumerable<UserResponse>> GetUsersByRoleAsync(string role)
        {
            try
            {
                if (!Enum.TryParse<UserRole>(role, true, out var roleEnum))
                    return Enumerable.Empty<UserResponse>();

                var users = await _context.Users
                    .Where(u => !u.IsDeleted && u.Role == roleEnum)
                    .ToListAsync();

                // If list is empty, return an empty enumerable instead of null
                return _mapper.Map<IEnumerable<UserResponse>>(users) ?? Enumerable.Empty<UserResponse>();
            }
            catch (Exception ex)
            {
                // Returning empty ensures the app doesn't crash, but controller can check
                throw new Exception($"Service Error: Could not fetch users for role {role}. {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SoftDeleteUserAsync(int id)
        {
            try
            {
                if (id <= 0) return (false, "Invalid user ID.");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
                if (user == null) return (false, "User not found.");

                if (user.Role == UserRole.SuperAdmin)
                    return (false, "The SuperAdmin cannot be deleted.");

                bool isAssignedToAnything = await _context.Tasks.AnyAsync(t =>
                    t.AssignedToUserId == id && !t.IsDeleted);

                if (isAssignedToAnything)
                    return (false, "User is still assigned to tasks. Please unassign them first.");

                user.IsDeleted = true;
                await _context.SaveChangesAsync();

                return (true, "User deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Internal Error: {ex.Message}");
            }
        }

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