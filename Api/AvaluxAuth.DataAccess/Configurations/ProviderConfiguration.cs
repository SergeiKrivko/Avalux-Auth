using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class ProviderConfiguration : IEntityTypeConfiguration<ProviderEntity>
{
    public void Configure(EntityTypeBuilder<ProviderEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.ProviderId).IsRequired();

        builder.Property(x => x.ClientName);
        builder.Property(x => x.ClientId);
        builder.Property(x => x.ClientSecret);
        builder.Property(x => x.ProviderUrl);
        builder.Property(x => x.SaveTokens).IsRequired();
        builder.Property(x => x.DefaultScope).HasDefaultValue(Array.Empty<string>());

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasMany(x => x.Accounts)
            .WithOne(x => x.Provider)
            .HasForeignKey(x => x.ProviderId);
    }
}