using Microsoft.EntityFrameworkCore;

namespace Ilvi.Infrastructure.Data;

public abstract class AppDbContext : DbContext
{
    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from the assembly of the inheriting context
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        
        
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var updatedProp = entityType.FindProperty("UpdatedAt");
            if (updatedProp != null && updatedProp.ClrType == typeof(DateTime))
            {
                updatedProp.SetColumnName("SourceUpdatedAtUtc");
            }
        }
    }
}