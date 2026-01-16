using Ilvi.Modules.AmoCrm.Domain.Events;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

 

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class AmoEventConfiguration : IEntityTypeConfiguration<AmoEvent>
{
    public void Configure(EntityTypeBuilder<AmoEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedBy)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(x => x.Type) 
            .HasColumnName("EventType") // SQL'deki adı
            .HasMaxLength(100);

        builder.Property(x => x.EventAtUtc)
            .HasColumnName("EventAtUtc") // SQL'de bu isimle oluşsun
            .IsRequired();
        
        builder.Property(x => x.EntityType).HasMaxLength(50);
        
        // JSON Kolonları
        builder.Property(x => x.ValueAfter).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ValueBefore).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");
    }
}