using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<UserSubscriptionEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.PlanId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.StartsAt);
        builder.Property(x => x.ExpiresAt).IsRequired();
    }
}