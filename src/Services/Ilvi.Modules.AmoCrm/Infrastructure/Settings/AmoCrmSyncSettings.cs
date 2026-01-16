namespace Ilvi.Modules.AmoCrm.Infrastructure.Settings;

public class AmoCrmSyncSettings
{ 
    public int EventsLookBackMonths { get; set; } = 6; 
    public int MessagesLookBackMonths { get; set; } = 12;
}