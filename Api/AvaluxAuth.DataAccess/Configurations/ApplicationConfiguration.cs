using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.ClientId).IsRequired();
        builder.Property(x => x.ClientSecret).IsRequired();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.RedirectUrls).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasMany(x => x.Providers)
            .WithOne(x => x.Application)
            .HasForeignKey(x => x.ApplicationId);
        builder.HasMany(x => x.Users)
            .WithOne(x => x.Application)
            .HasForeignKey(x => x.ApplicationId);
        builder.HasMany(x => x.Tokens)
            .WithOne(x => x.Application)
            .HasForeignKey(x => x.ApplicationId);
    }
}