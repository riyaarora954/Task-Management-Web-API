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

            // SuperAdmin is now seeded in Program.cs (Stage 2)
            // using SET IDENTITY_INSERT to guarantee Id = 1
            // before Bogus fake users are added.
        }
    }
}