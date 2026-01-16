using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;
using Ilvi.Modules.AmoCrm.Domain.Users;

namespace Ilvi.Modules.AmoCrm.Domain.Leads;

public class Lead : BaseEntity<LeadId>
{
    protected Lead() { }

    public Lead(LeadId id, UserId responsibleUserId, long accountId)
    {
        Id = id;
        ResponsibleUserId = responsibleUserId;
        AccountId = accountId;
    }

    public long AccountId { get; private set; }
    public UserId ResponsibleUserId { get; private set; }

    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public int StatusId { get; set; }
    public int PipelineId { get; set; }
    public int? LossReasonId { get; set; }

    // --- JSON KOLONLARI ---
    public string? Contact { get; set; } // JSON Array (Bağlı Kişiler)
    public string? Company { get; set; } // JSON Array (Bağlı Şirket)
    public string? Tag { get; set; }     // JSON Array (Etiketler)
  
    // ----------------------

 
}