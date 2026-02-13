using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKeyEntity>
{
    public void Configure(EntityTypeBuilder<SigningKeyEntity> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired();
        builder.Property(s => s.Kid).IsRequired();
        builder.Property(s => s.Algorithm).IsRequired();
        builder.Property(s => s.Use).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.PrivateKeyEncrypted).IsRequired();
        builder.Property(s => s.PublicJwkJson).IsRequired();
    }
}