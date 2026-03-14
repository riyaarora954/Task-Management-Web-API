namespace TM.Model.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public UserRole Role { get; set; } = UserRole.User; // Uses the Enum
    }
}