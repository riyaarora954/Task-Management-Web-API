using System.Threading.Tasks;
using TM.Contracts.Auth;

namespace TM.ServiceLogic.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);

        Task<AuthResponse?> LoginAsync(LoginRequest request);
    }
}