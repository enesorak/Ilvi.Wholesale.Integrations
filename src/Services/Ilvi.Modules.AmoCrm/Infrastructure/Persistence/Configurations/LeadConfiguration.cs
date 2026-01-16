using Ilvi.Modules.AmoCrm.Domain.Leads;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => LeadId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ResponsibleUserId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(x => x.Name).HasMaxLength(255);
        
        builder.Property(x => x.StatusId)
            .HasColumnName("Status") 
            .IsRequired();

        // JSON Alanları
        builder.Property(x => x.Contact).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Company).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Tag).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");

        builder.Property(x => x.ComputedHash).HasMaxLength(64).IsFixedLength();
    }
}