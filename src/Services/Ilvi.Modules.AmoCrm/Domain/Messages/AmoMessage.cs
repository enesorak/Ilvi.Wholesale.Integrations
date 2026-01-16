using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;
using Ilvi.Modules.AmoCrm.Domain.Users;

namespace Ilvi.Modules.AmoCrm.Domain.Messages;

public class AmoMessage : BaseEntity<string>
{
    protected AmoMessage() { }
    public AmoMessage(string id) { Id = id; }

    public long ChatId { get; set; }
    public long ContactId { get; set; }  // Mesajlaşılan kişi
    public long EntityId { get; set; }   // Bağlı olduğu kayıt (Lead vs)
    public UserId AuthorId { get; set; } // Mesajı atan
    public DateTime EventAtUtc { get; set; }
    public string Type { get; set; } = string.Empty; // incoming / outgoing
    public string Text { get; set; } = string.Empty; // Mesaj metni
    
 
   
}