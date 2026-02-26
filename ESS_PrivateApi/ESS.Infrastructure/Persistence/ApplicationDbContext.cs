using ESS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Vendor> Vendors => Set<Vendor>();
        public DbSet<EssSoftTokens> EssSoftTokens => Set<EssSoftTokens>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
