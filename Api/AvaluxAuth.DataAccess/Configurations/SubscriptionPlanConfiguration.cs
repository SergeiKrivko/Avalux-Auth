using AvaluxAuth.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvaluxAuth.DataAccess.Configurations;

internal class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlanEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.ApplicationId).IsRequired();
        builder.Property(e => e.Key).IsRequired();
        builder.Property(e => e.DisplayName).IsRequired();
        builder.Property(e => e.Description);
        builder.Property(e => e.Advantages);
        builder.Property(e => e.IsHidden).HasDefaultValue(false);
        builder.Property(e => e.IsDefault).HasDefaultValue(false);
        builder.Property(e => e.PriceCurrency).IsRequired();
        builder.Property(e => e.PriceAmount).IsRequired();
        builder.Property(e => e.Data);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasMany(x => x.Subscriptions)
            .WithOne(x => x.Plan)
            .HasForeignKey(x => x.PlanId);
    }
}