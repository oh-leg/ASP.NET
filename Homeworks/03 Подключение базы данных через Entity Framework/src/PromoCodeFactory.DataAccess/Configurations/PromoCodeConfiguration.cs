using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace PromoCodeFactory.DataAccess.Configurations;

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable(nameof(PromoCode));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.ServiceInfo)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.PartnerName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.BeginDate)
            .IsRequired();

        builder.Property(p => p.EndDate)
            .IsRequired();

        builder.HasOne(p => p.PartnerManager)
            .WithMany()
            .IsRequired();

        builder.HasOne(p => p.Preference)
            .WithMany()
            .IsRequired();

        builder.HasMany(p => p.CustomerPromoCodes)
            .WithOne()
            .HasForeignKey(cpc => cpc.PromoCodeId);
    }
}
