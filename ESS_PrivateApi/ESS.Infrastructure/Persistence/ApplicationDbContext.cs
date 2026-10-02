using ESS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Vendor> Vendors => Set<Vendor>();
        public DbSet<EssSoftTokens> EssSoftTokens => Set<EssSoftTokens>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.Property(x => x.Token)
                    .HasMaxLength(44)
                    .IsRequired();

                entity.HasIndex(x => x.Token)
                    .IsUnique();
            });
        }
    }
}
