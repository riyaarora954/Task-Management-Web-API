
using TM.Contracts.Auth;
using TM.Model.Entities;
using Sieve.Models;
namespace TM.ServiceLogic.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        // Inside IAuthService.cs
        Task<IEnumerable<UserResponse>> GetUsersByRoleAsync(string role, SieveModel sieveModel);
        Task<(bool Success, string Message)> SoftDeleteUserAsync(int id);
    }
}