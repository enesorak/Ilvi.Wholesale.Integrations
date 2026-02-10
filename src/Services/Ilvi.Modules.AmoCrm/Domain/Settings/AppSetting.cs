namespace Ilvi.Modules.AmoCrm.Domain.Settings;

public class AppSetting
{
    public int Id { get; set; }
    
    /// <summary>
    /// Ayar grubu (örn: "AmoCrm", "Sync", "General")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Ayar anahtarı (örn: "BaseUrl", "AccessToken", "PageSize")
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Ayar değeri (string olarak saklanır, gerekirse parse edilir)
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Değer tipi (String, Int, Bool, Json)
    /// </summary>
    public SettingValueType ValueType { get; set; } = SettingValueType.String;
    
    /// <summary>
    /// Açıklama (UI'da gösterilir)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Hassas veri mi? (UI'da maskelenir)
    /// </summary>
    public bool IsSensitive { get; set; }
    
    /// <summary>
    /// Sıralama (UI'da gösterim sırası)
    /// </summary>
    public int DisplayOrder { get; set; }
    
    // Audit Alanları
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public enum SettingValueType
{
    String = 0,
    Int = 1,
    Bool = 2,
    Json = 3,
    Secret = 4 // Şifreli saklanacak
}