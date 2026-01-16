using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;
using Ilvi.Modules.AmoCrm.Domain.Users;

namespace Ilvi.Modules.AmoCrm.Domain.Events;

// Event ID'leri string olarak saklamak en güvenlisidir (UUID gelebilir)
public class AmoEvent : BaseEntity<string>
{
    protected AmoEvent() { }

    public AmoEvent(string id)
    {
        Id = id;
    }

    public string Type { get; set; } = string.Empty; // lead_added, task_completed vs.
    public long EntityId { get; set; }               // Değişen kaydın ID'si
    public string EntityType { get; set; } = string.Empty; // lead, contact, company
    
    public UserId CreatedBy { get; set; }
 
    public DateTime EventAtUtc { get; set; }
    // Değişiklik detayları (JSON olarak saklanır)
    public string? ValueAfter { get; set; }  
    public string? ValueBefore { get; set; } 
    
 
}