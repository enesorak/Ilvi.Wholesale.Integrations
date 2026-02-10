using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Settings;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly AmoCrmDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingsService> _logger;
    
    private const string CacheKeyPrefix = "Settings_";
    private const string AllSettingsCacheKey = "Settings_All";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public SettingsService(
        AmoCrmDbContext context, 
        IMemoryCache cache,
        ILogger<SettingsService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<string, List<AppSetting>>> GetAllGroupedAsync(CancellationToken ct = default)
    {
        var settings = await GetAllSettingsAsync(ct);
        return settings
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.DisplayOrder).ToList());
    }

    public async Task<List<AppSetting>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        var settings = await GetAllSettingsAsync(ct);
        return settings
            .Where(s => s.Category == category)
            .OrderBy(s => s.DisplayOrder)
            .ToList();
    }

    public async Task<AppSetting?> GetAsync(string category, string key, CancellationToken ct = default)
    {
        var settings = await GetAllSettingsAsync(ct);
        return settings.FirstOrDefault(s => s.Category == category && s.Key == key);
    }

    public async Task<string?> GetValueAsync(string category, string key, CancellationToken ct = default)
    {
        var setting = await GetAsync(category, key, ct);
        return setting?.Value;
    }

    public async Task<int> GetIntAsync(string category, string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var value = await GetValueAsync(category, key, ct);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string category, string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var value = await GetValueAsync(category, key, ct);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<AppSetting> SetAsync(string category, string key, string value, string? updatedBy = null, CancellationToken ct = default)
    {
        var existing = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key, ct);

        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedBy = updatedBy;
            
            _logger.LogInformation("Setting updated: {Category}.{Key} by {User}", category, key, updatedBy ?? "System");
        }
        else
        {
            existing = new AppSetting
            {
                Category = category,
                Key = key,
                Value = value,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedBy = updatedBy
            };
            _context.AppSettings.Add(existing);
            
            _logger.LogInformation("Setting created: {Category}.{Key} by {User}", category, key, updatedBy ?? "System");
        }

        await _context.SaveChangesAsync(ct);
        InvalidateCache();

        return existing;
    }

    public async Task BulkUpdateAsync(List<SettingUpdateDto> updates, string? updatedBy = null, CancellationToken ct = default)
    {
        foreach (var update in updates)
        {
            var existing = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Category == update.Category && s.Key == update.Key, ct);

            if (existing != null)
            {
                existing.Value = update.Value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.UpdatedBy = updatedBy;
            }
        }

        await _context.SaveChangesAsync(ct);
        InvalidateCache();
        
        _logger.LogInformation("Bulk settings update: {Count} settings by {User}", updates.Count, updatedBy ?? "System");
    }

    public async Task<AmoCrmOptions> GetAmoCrmOptionsAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}AmoCrmOptions";
        
        if (_cache.TryGetValue(cacheKey, out AmoCrmOptions? cached) && cached != null)
        {
            return cached;
        }

        var settings = await GetByCategoryAsync("AmoCrm", ct);
        var syncSettings = await GetByCategoryAsync("Sync", ct);

        var options = new AmoCrmOptions
        {
            BaseUrl = settings.FirstOrDefault(s => s.Key == "BaseUrl")?.Value ?? "",
            AccessToken = settings.FirstOrDefault(s => s.Key == "AccessToken")?.Value ?? "",
            PageSize = int.TryParse(settings.FirstOrDefault(s => s.Key == "PageSize")?.Value, out var ps) ? ps : 250,
            RequestDelayMs = int.TryParse(settings.FirstOrDefault(s => s.Key == "RequestDelayMs")?.Value, out var rd) ? rd : 200
        };

        _cache.Set(cacheKey, options, CacheDuration);
        
        return options;
    }

    public void InvalidateCache()
    {
        _cache.Remove(AllSettingsCacheKey);
        _cache.Remove($"{CacheKeyPrefix}AmoCrmOptions");
        _logger.LogDebug("Settings cache invalidated");
    }

    private async Task<List<AppSetting>> GetAllSettingsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(AllSettingsCacheKey, out List<AppSetting>? cached) && cached != null)
        {
            return cached;
        }

        var settings = await _context.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        _cache.Set(AllSettingsCacheKey, settings, CacheDuration);
        
        return settings;
    }
}