namespace Ilvi.Core.Abstractions;

public abstract class Entity<TId> : IEntity where TId : notnull
{
    // 1. ID Yönetimi (Doğru yapmıştın)
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        Id = id;
    }

    protected Entity() { } // EF Core için gerekli

    // 2. Audit Alanları (DÜZELTME: Setter eklendi + İsimler Utc oldu)
    
    // Kaydın veritabanına ilk atıldığı an
    public DateTime CreatedAtUtc { get; set; } 

    // Kaydın veritabanında güncellendiği an (System Audit)
    public DateTime? UpdatedAtUtc { get; set; }
    
    // 3. Sync Kontrol Tarihi (DÜZELTME: İsim PowerBI uyumlu oldu)
    // LastSyncDate yerine CheckedAtUtc kullanıyoruz.
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;

    // 4. Hash (Doğru)
    public string ComputedHash { get; set; } = string.Empty;
}