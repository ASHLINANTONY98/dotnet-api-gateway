using ESS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESS.Infrastructure.Persistence.Configurations
{
    public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
    {
        public void Configure(EntityTypeBuilder<Vendor> builder)
        {
            builder.ToTable("VENDOR_MASTER_ESS");

            builder.HasKey(x => x.VendorId);

            builder.Property(x => x.VendorId).HasColumnName("VENDOR_ID");
            builder.Property(x => x.VendorName).HasColumnName("VENDOR_NAME");
            builder.Property(x => x.ApiKey).HasColumnName("API_KEY");
            builder.Property(x => x.IsActive).HasColumnName("IS_ACTIVE");
            builder.Property(x => x.VendorRole).HasColumnName("VENDOR_ROLE");
        }
    }
}
