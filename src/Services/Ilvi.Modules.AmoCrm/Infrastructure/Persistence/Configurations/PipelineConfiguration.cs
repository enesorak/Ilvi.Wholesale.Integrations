using Ilvi.Modules.AmoCrm.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("Pipelines");

        builder.HasKey(x => x.Id);

        // ID veritabanında otomatik artan olmamalı, API'den gelen ID'yi basacağız.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Statuses)
            .HasColumnName("Status") 
            .HasColumnType("nvarchar(max)");
        
        // JSON Kolonları
        builder.Property(x => x.Statuses).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Raw).HasColumnName("Raw").HasColumnType("nvarchar(max)");
    }
}