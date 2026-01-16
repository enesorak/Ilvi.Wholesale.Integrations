using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;

namespace Ilvi.Modules.AmoCrm.Domain.TaskTypes;

// TaskType ID'leri AmoCRM'de 'int' olarak gelir.
public class TaskType : BaseEntity<int>
{
    protected TaskType() { }

    public TaskType(int id)
    {
        Id = id;
    }

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int IconId { get; set; }
 
 
}