using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;

namespace Ilvi.Modules.AmoCrm.Domain.Pipelines;

public class Pipeline : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public int Sort { get; set; }
    public bool IsMain { get; set; }
    
    // Statüleri (Aşamaları) JSON olarak tutuyoruz
    // Örn: [{"id":1, "name":"İlk Görüşme"}, {"id":2, "name":"Teklif"}]
    public string? Statuses { get; set; } 
 
   

    protected Pipeline() { }
    public Pipeline(int id) { Id = id; }
}