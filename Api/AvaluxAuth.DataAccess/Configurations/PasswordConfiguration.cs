using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

public class PasswordConfiguration : IEntityTypeConfiguration<PasswordEntity>
{
    public void Configure(EntityTypeBuilder<PasswordEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Login).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.Name);
        builder.Property(x => x.Email);
        builder.Property(x => x.AvatarId);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.DeletedAt);
    }
}