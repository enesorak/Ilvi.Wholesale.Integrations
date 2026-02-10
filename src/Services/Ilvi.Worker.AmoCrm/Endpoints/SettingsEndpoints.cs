using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Ilvi.Worker.AmoCrm.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        // 1. Tüm ayarları getir (gruplu)
        group.MapGet("/", async (ISettingsService settingsService, CancellationToken ct) =>
        {
            var settings = await settingsService.GetAllGroupedAsync(ct);
            
            // Hassas verileri maskele
            var masked = settings.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(s => new SettingDto
                {
                    Id = s.Id,
                    Category = s.Category,
                    Key = s.Key,
                    Value = s.IsSensitive ? MaskValue(s.Value) : s.Value,
                    ValueType = s.ValueType.ToString(),
                    Description = s.Description,
                    IsSensitive = s.IsSensitive,
                    DisplayOrder = s.DisplayOrder,
                    UpdatedAtUtc = s.UpdatedAtUtc,
                    UpdatedBy = s.UpdatedBy
                }).ToList()
            );
            
            return Results.Ok(masked);
        });

        // 2. Kategori bazlı getir
        group.MapGet("/category/{category}", async (
            string category, 
            ISettingsService settingsService, 
            CancellationToken ct) =>
        {
            var settings = await settingsService.GetByCategoryAsync(category, ct);
            
            var result = settings.Select(s => new SettingDto
            {
                Id = s.Id,
                Category = s.Category,
                Key = s.Key,
                Value = s.IsSensitive ? MaskValue(s.Value) : s.Value,
                ValueType = s.ValueType.ToString(),
                Description = s.Description,
                IsSensitive = s.IsSensitive,
                DisplayOrder = s.DisplayOrder,
                UpdatedAtUtc = s.UpdatedAtUtc,
                UpdatedBy = s.UpdatedBy
            }).ToList();
            
            return Results.Ok(result);
        });

        // 3. Tek ayar getir
        group.MapGet("/{category}/{key}", async (
            string category, 
            string key, 
            ISettingsService settingsService, 
            CancellationToken ct) =>
        {
            var setting = await settingsService.GetAsync(category, key, ct);
            
            if (setting == null)
                return Results.NotFound(new { message = $"Ayar bulunamadı: {category}.{key}" });
            
            return Results.Ok(new SettingDto
            {
                Id = setting.Id,
                Category = setting.Category,
                Key = setting.Key,
                Value = setting.IsSensitive ? MaskValue(setting.Value) : setting.Value,
                ValueType = setting.ValueType.ToString(),
                Description = setting.Description,
                IsSensitive = setting.IsSensitive,
                DisplayOrder = setting.DisplayOrder,
                UpdatedAtUtc = setting.UpdatedAtUtc,
                UpdatedBy = setting.UpdatedBy
            });
        });

        // 4. Tek ayar güncelle
        group.MapPut("/{category}/{key}", async (
            string category, 
            string key, 
            [FromBody] SettingUpdateRequest request,
            ISettingsService settingsService, 
            CancellationToken ct) =>
        {
            try
            {
                var updated = await settingsService.SetAsync(category, key, request.Value, request.UpdatedBy, ct);
                
                return Results.Ok(new 
                { 
                    message = $"Ayar güncellendi: {category}.{key}",
                    setting = new SettingDto
                    {
                        Id = updated.Id,
                        Category = updated.Category,
                        Key = updated.Key,
                        Value = updated.IsSensitive ? MaskValue(updated.Value) : updated.Value,
                        ValueType = updated.ValueType.ToString(),
                        Description = updated.Description,
                        IsSensitive = updated.IsSensitive,
                        UpdatedAtUtc = updated.UpdatedAtUtc,
                        UpdatedBy = updated.UpdatedBy
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // 5. Toplu güncelle
        group.MapPut("/bulk", async (
            [FromBody] BulkSettingUpdateRequest request,
            ISettingsService settingsService, 
            CancellationToken ct) =>
        {
            try
            {
                var updates = request.Settings
                    .Select(s => new SettingUpdateDto(s.Category, s.Key, s.Value))
                    .ToList();
                    
                await settingsService.BulkUpdateAsync(updates, request.UpdatedBy, ct);
                
                return Results.Ok(new { message = $"{updates.Count} ayar güncellendi." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // 6. AmoCRM bağlantı testi
        group.MapPost("/test-connection", async (
            ISettingsService settingsService,
            IHttpClientFactory httpClientFactory,
            CancellationToken ct) =>
        {
            try
            {
                var options = await settingsService.GetAmoCrmOptionsAsync(ct);
                
                if (string.IsNullOrEmpty(options.BaseUrl) || string.IsNullOrEmpty(options.AccessToken))
                {
                    return Results.BadRequest(new { success = false, message = "BaseUrl veya AccessToken boş!" });
                }

                using var client = httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync("account", ct);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    return Results.Ok(new 
                    { 
                        success = true, 
                        message = "Bağlantı başarılı!",
                        statusCode = (int)response.StatusCode
                    });
                }
                else
                {
                    return Results.Ok(new 
                    { 
                        success = false, 
                        message = $"API hatası: {response.StatusCode}",
                        statusCode = (int)response.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                return Results.Ok(new 
                { 
                    success = false, 
                    message = $"Bağlantı hatası: {ex.Message}"
                });
            }
        });

        // 7. Cache temizle
        group.MapPost("/invalidate-cache", (ISettingsService settingsService) =>
        {
            settingsService.InvalidateCache();
            return Results.Ok(new { message = "Cache temizlendi." });
        });
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 8) return new string('*', value.Length);
        return value[..4] + new string('*', value.Length - 8) + value[^4..];
    }
}

// DTOs
public record SettingDto
{
    public int Id { get; init; }
    public string Category { get; init; } = "";
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string ValueType { get; init; } = "";
    public string? Description { get; init; }
    public bool IsSensitive { get; init; }
    public int DisplayOrder { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
}

public record SettingUpdateRequest(string Value, string? UpdatedBy = null);

public record BulkSettingUpdateRequest(List<SettingItemUpdate> Settings, string? UpdatedBy = null);

public record SettingItemUpdate(string Category, string Key, string Value);