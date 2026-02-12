using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvaluxAuth.DataAccess;

public class AvaluxAuthDbContext : DbContext
{
    internal DbSet<ApplicationEntity> Applications { get; init; }
    internal DbSet<ProviderEntity> Providers { get; init; }
    internal DbSet<UserEntity> Users { get; init; }
    internal DbSet<AccountEntity> Accounts { get; init; }
    internal DbSet<RefreshTokenEntity> RefreshTokens { get; init; }

    public AvaluxAuthDbContext(DbContextOptions<AvaluxAuthDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AvaluxAuthDbContext).Assembly);
    }
}