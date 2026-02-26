using ESS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESS.Infrastructure.Persistence.Configurations
{
    public class ValidateCodeConfiguration : IEntityTypeConfiguration<EssSoftTokens>
    {
        public void Configure(EntityTypeBuilder<EssSoftTokens> builder)
        {
            builder.ToTable("ESS_SOFT_TOKENS");

            builder.HasKey(x => x.EmpCode);

            builder.Property(x => x.EmpCode).HasColumnName("EMP_CODE");
            builder.Property(x => x.AuthenticationCode).HasColumnName("TOKEN");
            builder.Property(x => x.GeneratedOn).HasColumnName("GENERATED_ON");
            builder.Property(x => x.Status).HasColumnName("STATUS");

        }
    }
}
