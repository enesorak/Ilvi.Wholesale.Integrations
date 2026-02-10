using Ilvi.Modules.AmoCrm.Domain.Settings;

namespace Ilvi.Modules.AmoCrm.Abstractions;

public interface ISettingsService
{
    /// <summary>
    /// Tüm ayarları kategoriye göre gruplu getirir
    /// </summary>
    Task<Dictionary<string, List<AppSetting>>> GetAllGroupedAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Belirli bir kategorinin ayarlarını getirir
    /// </summary>
    Task<List<AppSetting>> GetByCategoryAsync(string category, CancellationToken ct = default);
    
    /// <summary>
    /// Tek bir ayarı getirir
    /// </summary>
    Task<AppSetting?> GetAsync(string category, string key, CancellationToken ct = default);
    
    /// <summary>
    /// Ayar değerini string olarak getirir
    /// </summary>
    Task<string?> GetValueAsync(string category, string key, CancellationToken ct = default);
    
    /// <summary>
    /// Ayar değerini int olarak getirir
    /// </summary>
    Task<int> GetIntAsync(string category, string key, int defaultValue = 0, CancellationToken ct = default);
    
    /// <summary>
    /// Ayar değerini bool olarak getirir
    /// </summary>
    Task<bool> GetBoolAsync(string category, string key, bool defaultValue = false, CancellationToken ct = default);
    
    /// <summary>
    /// Ayarı günceller veya oluşturur
    /// </summary>
    Task<AppSetting> SetAsync(string category, string key, string value, string? updatedBy = null, CancellationToken ct = default);
    
    /// <summary>
    /// Birden fazla ayarı toplu günceller
    /// </summary>
    Task BulkUpdateAsync(List<SettingUpdateDto> updates, string? updatedBy = null, CancellationToken ct = default);
    
    /// <summary>
    /// AmoCrmOptions nesnesini DB'den doldurur
    /// </summary>
    Task<AmoCrmOptions> GetAmoCrmOptionsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Cache'i temizler (ayar değiştiğinde çağrılır)
    /// </summary>
    void InvalidateCache();
}

public record SettingUpdateDto(string Category, string Key, string Value);