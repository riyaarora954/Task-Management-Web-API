using Microsoft.EntityFrameworkCore;
using TM.Model.Entities;
using Task = TM.Model.Entities.Task;

namespace TM.Model.Data
{
    public class TMDbContext : DbContext
    {
        public TMDbContext(DbContextOptions<TMDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            // SEEDING LOGIC
            modelBuilder.Entity<User>().HasData(new User

            {
                Id = 1,
                Username = "superadmin",
                Email = "superadmin@jira.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin123"),
                Role = UserRole.SuperAdmin,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
        }
    }
}