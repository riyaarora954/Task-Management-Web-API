using System.Text.Json.Serialization;

namespace TM.Contracts.Auth
{
    public class AuthResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; } = null;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}