using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace PromoCodeFactory.DataAccess.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable(nameof(Customer));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Ignore(c => c.FullName);

        builder.HasMany(c => c.Preferences)
            .WithMany(p => p.Customers)
            .UsingEntity(j => j.ToTable("CustomerPreference"));

        builder.HasMany(c => c.CustomerPromoCodes)
            .WithOne()
            .HasForeignKey(cpc => cpc.CustomerId);
    }
}
