using Ilvi.Modules.AmoCrm.Domain.Tasks;
using Ilvi.Modules.AmoCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class AmoTaskConfiguration : IEntityTypeConfiguration<AmoTask>
{
    public void Configure(EntityTypeBuilder<AmoTask> builder)
    {
        // Tablo adı "AmoTasks" veya "Tasks" olabilir. 
        // SQL'de Task rezerve kelime değildir ama karışmaması için AmoTasks daha güvenlidir.
        builder.ToTable("Tasks"); 

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => TaskId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ResponsibleUserId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.Property(x => x.ResultText).HasMaxLength(4000);

        
        
        builder.Property(x => x.Lead).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Company).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Contact).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");

        builder.Property(x => x.ComputedHash).HasMaxLength(64).IsFixedLength();
    }
}