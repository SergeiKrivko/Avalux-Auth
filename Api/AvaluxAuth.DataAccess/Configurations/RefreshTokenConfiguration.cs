using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
{
    public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
    {
        builder.HasKey(x => x.RefreshToken);

        builder.Property(x => x.RefreshToken).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ClientId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
    }
}