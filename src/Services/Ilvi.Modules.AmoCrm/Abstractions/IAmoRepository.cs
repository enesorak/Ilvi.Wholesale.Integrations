using Ilvi.Core.Abstractions;

namespace Ilvi.Modules.AmoCrm.Abstractions;

public interface IAmoRepository<TEntity, TId> 
    where TEntity : Entity<TId>
    where TId : notnull
{
    // Standart işlemler
    Task<TEntity?> GetByIdAsync(TId id);
    
    // Toplu işlemler (Bulk)
    // batchSize: SQL'e kaçarlı paketler halinde gönderileceği
    Task BulkUpsertAsync(List<TEntity> entities, int batchSize = 2000, CancellationToken ct = default);
    
    // Değişiklik algılama için mevcut hashleri çekmek gerekebilir
    Task<Dictionary<TId, string>> GetHashesAsync(IEnumerable<TId> ids, CancellationToken ct = default);
    
    Task<DateTime?> GetLastUpdateDateAsync(CancellationToken ct = default);
    
    // Mevcutların altına ekle:
    Task<DateTime?> GetLastCreatedDateAsync(CancellationToken ct = default);
}