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
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return null;

            string standardizedRole = "User";
            if (!string.IsNullOrWhiteSpace(request.Role) &&
                request.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                standardizedRole = "Admin";
            }

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = standardizedRole
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return _mapper.Map<AuthResponse>(user);
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return null;

            var response = _mapper.Map<AuthResponse>(user);
            response.Token = GenerateJwtToken(user);
            return response;
        }

        public async Task<IEnumerable<UserResponse>> GetUsersByRoleAsync(string role)
        {
            var users = await _context.Users
                .Where(u => !u.IsDeleted && u.Role.ToLower() == role.ToLower())
                .ToListAsync();

            return _mapper.Map<IEnumerable<UserResponse>>(users);
        }

        public async Task<(bool Success, string Message)> SoftDeleteUserAsync(int id)
        {
            if (id <= 0) return (false, "Invalid user ID.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null) return (false, "User not found.");

            bool hasInProgress = await _context.Tasks.AnyAsync(t =>
                t.AssignedToUserId == id &&
                t.Status == TM.Model.Entities.TaskStatus.InProgress &&
                !t.IsDeleted);

            if (hasInProgress)
                return (false, "User has tasks currently In Progress.");

            bool isAssignedToAnything = await _context.Tasks.AnyAsync(t =>
                t.AssignedToUserId == id && !t.IsDeleted);

            if (isAssignedToAnything)
                return (false, "User is still assigned to tasks. Please unassign them first.");

            user.IsDeleted = true;
            await _context.SaveChangesAsync();

            return (true, "User deleted successfully.");
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
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