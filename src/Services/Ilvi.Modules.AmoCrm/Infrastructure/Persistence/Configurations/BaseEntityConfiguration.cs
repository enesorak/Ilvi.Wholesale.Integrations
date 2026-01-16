// Örnek bir Extension Method ile tüm entitylere bu kuralı uygulayabilirsin.

using Microsoft.EntityFrameworkCore;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Configurations;

public static class ModelBuilderExtensions
{
    public static void ApplyPowerBiNamingConvention(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // 1. LastSyncDate -> CheckedAtUtc
            var lastSyncProp = entityType.FindProperty("LastSyncDate");
            if (lastSyncProp != null) lastSyncProp.SetColumnName("CheckedAtUtc");

            // 2. UpdatedAt -> SourceUpdatedAtUtc (Events hariç genelde kaynak update tarihidir)
            var updatedProp = entityType.FindProperty("UpdatedAt");
            if (updatedProp != null) updatedProp.SetColumnName("SourceUpdatedAtUtc");

            // 3. CreatedAt -> CreatedAtUtc (Genel kural)
            // DİKKAT: Events ve Messages için özel ayar aşağıda yapılacak.
            var createdProp = entityType.FindProperty("CreatedAt");
            if (createdProp != null && entityType.ClrType.Name != "AmoEvent" && entityType.ClrType.Name != "AmoMessage") 
            {
                createdProp.SetColumnName("CreatedAtUtc");
            }
        }
    }
}