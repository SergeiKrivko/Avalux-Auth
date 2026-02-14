using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class TokenConfiguration : IEntityTypeConfiguration<TokenEntity>
{
    public void Configure(EntityTypeBuilder<TokenEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Name);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.Permissions);
    }
}