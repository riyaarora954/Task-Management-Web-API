using System;
namespace TM.Contracts.Auth
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty; // 📧 Added this
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}