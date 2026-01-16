using Ilvi.Modules.AmoCrm.Domain.Messages;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class AmoMessageConfiguration : IEntityTypeConfiguration<AmoMessage>
{
    public void Configure(EntityTypeBuilder<AmoMessage> builder)
    {
        // Tablo Adı
        builder.ToTable("Messages");
        
        // Primary Key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // String ID olduğu için

        // AuthorId Dönüşümü (Value Object -> Long)
        builder.Property(x => x.AuthorId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        // --- İSİM EŞLEŞTİRMELERİ (Kritik Kısım) ---

        builder.Property(x => x.EventAtUtc)
            .HasColumnName("EventAtUtc") // SQL'de bu isimle oluşsun
            .IsRequired();

        // 2. Type -> EventType
        builder.Property(x => x.Type)
            .HasColumnName("EventType")
            .HasMaxLength(255);

        // 3. Text
        builder.Property(x => x.Text)
            .HasMaxLength(4000); 

        // 4. Raw Data
        builder.Property(x => x.Raw)
            .HasColumnName("Raw")
            .HasColumnType("nvarchar(max)");
            
        // Diğer BaseEntity alanları (CheckedAtUtc, SourceUpdatedAtUtc, ComputedHash)
        // isimleri C# ve SQL'de aynı olduğu için EF Core onları otomatik eşleştirir.
        // Ekstra bir şey yazmana gerek yok.
    }
}