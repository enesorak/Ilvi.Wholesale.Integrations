using Ilvi.Modules.AmoCrm.Domain.Contacts;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => ContactId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ResponsibleUserId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(x => x.Name).HasMaxLength(255);
        
        // JSON Kolonları (Sınırsız uzunluk)
        builder.Property(x => x.Lead).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Company).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Tag).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");
 
        builder.Property(x => x.ComputedHash).HasMaxLength(64).IsFixedLength();
    }
}