namespace Ilvi.Modules.AmoCrm;

public class AmoCrmOptions
{
    public string BaseUrl { get; set; } = string.Empty; // https://ilvi.amocrm.com
    public string AccessToken { get; set; } = string.Empty; // Uzun süreli token (Long-lived)
    public int PageSize { get; set; } = 250; // AmoCRM max 250 destekler
    public int RequestDelayMs { get; set; } = 200; // Rate limit için bekleme
}