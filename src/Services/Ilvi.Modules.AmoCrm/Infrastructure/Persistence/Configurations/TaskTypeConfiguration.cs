using Ilvi.Modules.AmoCrm.Domain.TaskTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class TaskTypeConfiguration : IEntityTypeConfiguration<TaskType>
{
    public void Configure(EntityTypeBuilder<TaskType> builder)
    {
        builder.ToTable("TaskTypes");

        builder.HasKey(x => x.Id);

        // API'den gelen ID'yi kullanacağız, SQL üretmemeli.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Color).HasMaxLength(50); // Örn: #FF0000

        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");
    }
}