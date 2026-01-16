using Ilvi.Core.Abstractions;

namespace Ilvi.Modules.AmoCrm.Domain.Common;

public abstract class BaseEntity<TId> :  Entity<TId> where TId : notnull
{
    protected BaseEntity(TId id) : base(id)
    {
    }
    protected BaseEntity() { }
    
    public string Raw { get; set; } = null!;
    public DateTime? SourceUpdatedAtUtc { get; set; }
 
}