
using TM.Contracts.Auth;

namespace TM.ServiceLogic.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        Task<IEnumerable<UserResponse>> GetUsersByRoleAsync(string role);
        Task<(bool Success, string Message)> SoftDeleteUserAsync(int id);
    }
}