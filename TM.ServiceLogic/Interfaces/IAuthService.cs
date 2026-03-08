using TM.Contracts.Auth;

namespace TM.ServiceLogic.Interfaces
{
    public interface IAuthService
    {
        string GenerateToken(string username, string role);
    }
}