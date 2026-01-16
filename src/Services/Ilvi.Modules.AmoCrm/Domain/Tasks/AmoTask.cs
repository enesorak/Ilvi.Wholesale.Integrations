using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;
using Ilvi.Modules.AmoCrm.Domain.Users;

namespace Ilvi.Modules.AmoCrm.Domain.Tasks;

public class AmoTask : BaseEntity<TaskId>
{
    protected AmoTask() { }

    public AmoTask(TaskId id, UserId responsibleUserId, long accountId)
    {
        Id = id;
        ResponsibleUserId = responsibleUserId;
        AccountId = accountId;
    }

    public long AccountId { get; private set; }
    public UserId ResponsibleUserId { get; private set; }

    public string Text { get; set; } = string.Empty;
    public int TaskTypeId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompleteTill { get; set; }
    public string? ResultText { get; set; }

    // JSON Kolonları
    public string? Lead { get; set; }
    public string? Company { get; set; }
    public string? Contact { get; set; }
 

 
}