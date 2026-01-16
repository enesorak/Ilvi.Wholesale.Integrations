using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;
using Ilvi.Modules.AmoCrm.Domain.Users;

namespace Ilvi.Modules.AmoCrm.Domain.Contacts;

public class Contact : BaseEntity<ContactId>
{
    protected Contact() { }

    public Contact(ContactId id, UserId responsibleUserId, long accountId)
    {
        Id = id;
        ResponsibleUserId = responsibleUserId;
        AccountId = accountId;
    }

    public long AccountId { get; private set; }
    public UserId ResponsibleUserId { get; private set; }
    
    public string Name { get; set; } = string.Empty;
    
    // --- DEĞİŞEN KISIM ---
    // Artık virgüllü string değil, direkt JSON Array tutuyoruz.
    // Örnek: [{"id":123, "name":"VIP"}]
    public string? Lead { get; set; }      // Fırsatlar (JSON)
    public string? Company { get; set; }  // Şirketler (JSON)
    public string? Tag { get; set; }       // Etiketler (JSON)
    // ---------------------

 
   
 
}