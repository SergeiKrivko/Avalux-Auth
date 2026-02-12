using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class AccountConfiguration : IEntityTypeConfiguration<AccountEntity>
{
    public void Configure(EntityTypeBuilder<AccountEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ProviderId).IsRequired();
        builder.Property(x => x.ProviderUserId).IsRequired();
        builder.Property(x => x.Name);
        builder.Property(x => x.Email);
        builder.Property(x => x.AvatarUrl);

        builder.Property(x => x.AccessToken);
        builder.Property(x => x.RefreshToken);
        builder.Property(x => x.ExpiresAt);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.DeletedAt);
    }
}