using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace PromoCodeFactory.DataAccess.Configurations;

public class CustomerPromoCodeConfiguration : IEntityTypeConfiguration<CustomerPromoCode>
{
    public void Configure(EntityTypeBuilder<CustomerPromoCode> builder)
    {
        builder.ToTable(nameof(CustomerPromoCode));

        builder.HasKey(cpc => cpc.Id);

        builder.Property(cpc => cpc.CustomerId)
            .IsRequired();

        builder.Property(cpc => cpc.PromoCodeId)
            .IsRequired();

        builder.Property(cpc => cpc.CreatedAt)
            .IsRequired();

        builder.Property(cpc => cpc.AppliedAt)
            .IsRequired(false);

        builder.HasOne<PromoCode>()
            .WithMany(p => p.CustomerPromoCodes)
            .HasForeignKey(cpc => cpc.PromoCodeId);
    }
}
