using EFCore.BulkExtensions;
using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Repositories;

public class AmoRepository<TEntity, TId> : IAmoRepository<TEntity, TId> 
    where TEntity : Entity<TId>
    where TId : notnull // Bizim TId'lerimiz struct (UserId, ContactId...)
{
    private readonly AmoCrmDbContext _context;

    public AmoRepository(AmoCrmDbContext context)
    {
        _context = context;
    }

    public async Task<TEntity?> GetByIdAsync(TId id)
    {
        return await _context.Set<TEntity>().FindAsync(id);
    }

    public async Task BulkUpsertAsync(List<TEntity> entities, int batchSize = 2000, CancellationToken ct = default)
    {
        if (!entities.Any()) return;

        var bulkConfig = new BulkConfig
        {
            BatchSize = batchSize,
            UpdateByProperties = new List<string> { "Id" },
            // --- EKLENEN KISIM ---
            BulkCopyTimeout = 600, // 10 dakika (Yeter de artar)
           
        };

        // Eğer mevcutsa güncelle, yoksa ekle (MERGE)
        await _context.BulkInsertOrUpdateAsync(entities, bulkConfig, cancellationToken: ct);
    }

    public async Task<Dictionary<TId, string>> GetHashesAsync(IEnumerable<TId> ids, CancellationToken ct = default)
    {
        // Sadece ID ve Hash kolonunu çekerek performans kazanıyoruz
        // Not: Burada EF Core, ValueConverter (TId -> long) işlemini otomatik yapmalı.
        return await _context.Set<TEntity>()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ComputedHash, ct);
    }
    
    
    public async Task<DateTime?> GetLastUpdateDateAsync(CancellationToken ct = default)
    {
        // Tablodaki en büyük UpdatedAt tarihini alıyoruz
        return await _context.Set<TEntity>()
            .MaxAsync(x => (DateTime?)x.UpdatedAtUtc, ct);
    }
     
    
    public async Task<DateTime?> GetLastCreatedDateAsync(CancellationToken ct = default)
    {
        if (!await _context.Set<TEntity>().AnyAsync(ct)) return null;

        // EĞER Entity'de "EventAtUtc" varsa (Events/Messages gibi) ona bak.
        // YOKSA (Leads/Contacts gibi) CreatedAtUtc'ye bak.
        
        try 
        {
            // Öncelik: EventAtUtc (Gerçek olay zamanı)
            return await _context.Set<TEntity>()
                .MaxAsync(x => EF.Property<DateTime?>(x, "EventAtUtc"), ct);
        }
        catch
        {
            // Eğer "EventAtUtc" kolonu yoksa (Örn: Leads tablosu), 
            // mecburen CreatedAtUtc (Oluşturulma tarihi) döndür.
            return await _context.Set<TEntity>()
                .MaxAsync(x => x.CreatedAtUtc, ct);
        }
    }
}