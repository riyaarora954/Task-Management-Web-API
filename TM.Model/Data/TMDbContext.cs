using Microsoft.EntityFrameworkCore;
using TM.Model.Entities;
using TM.Model.Data;
using Task = TM.Model.Entities.Task;

namespace TM.Model.Data
{
    public class TMDbContext : DbContext
    {
        public TMDbContext(DbContextOptions<TMDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Comment> Comments { get; set; }
    }
}