using Ilvi.Core.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Common;

namespace Ilvi.Modules.AmoCrm.Domain.Users;

public class User : BaseEntity<UserId>
{
    private User() { }
    public User(UserId id) : base(id) { }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
 
}