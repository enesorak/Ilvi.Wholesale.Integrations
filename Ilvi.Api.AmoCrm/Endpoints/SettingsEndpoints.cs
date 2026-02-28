using Ilvi.Modules.AmoCrm.Abstractions;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        // 1. Tüm ayarları listele
        group.MapGet("/", async (ISettingsService settingsService, CancellationToken ct) =>
        {
            var amoCrmSettings = await settingsService.GetByCategoryAsync("AmoCrm", ct);
            var syncSettings = await settingsService.GetByCategoryAsync("Sync", ct);
            var lastSyncSettings = await settingsService.GetByCategoryAsync("LastSync", ct);

            // Token'ı maskele
            var result = amoCrmSettings.Select(s => new
            {
                s.Category,
                s.Key,
                Value = s.Key == "AccessToken"
                    ? (string.IsNullOrEmpty(s.Value) ? "(boş)" : $"{s.Value[..20]}...***")
                    : s.Value,
                s.UpdatedAtUtc,
                s.UpdatedBy
            }).ToList();

            return Results.Ok(new
            {
                amoCrm = result,
                sync = syncSettings.Select(s => new { s.Key, s.Value, s.UpdatedAtUtc }),
                lastSync = lastSyncSettings.Select(s => new { s.Key, s.Value, s.UpdatedAtUtc })
            });
        });

        // 2. Kategori bazlı ayarları getir
        group.MapGet("/{category}", async (
            string category,
            ISettingsService settingsService,
            CancellationToken ct) =>
        {
            var settings = await settingsService.GetByCategoryAsync(category, ct);
            return Results.Ok(settings.Select(s => new
            {
                s.Category,
                s.Key,
                Value = s.Key == "AccessToken"
                    ? (string.IsNullOrEmpty(s.Value) ? "(boş)" : "***masked***")
                    : s.Value,
                s.UpdatedAtUtc,
                s.UpdatedBy
            }));
        });

        // 3. Tek bir ayar güncelle
        group.MapPut("/", async (
            SettingUpdateRequest request,
            ISettingsService settingsService,
            CancellationToken ct) =>
        {
            var result = await settingsService.SetAsync(
                request.Category, request.Key, request.Value, request.UpdatedBy ?? "API", ct);

            return Results.Ok(new
            {
                message = $"Ayar güncellendi: {request.Category}.{request.Key}",
                updatedAt = result.UpdatedAtUtc
            });
        });

        // 4. AmoCRM bağlantı testi
        group.MapPost("/test-connection", async (
            ISettingsService settingsService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            try
            {
                var options = await settingsService.GetAmoCrmOptionsAsync(ct);
                var token = options.AccessToken;
                var baseUrl = options.BaseUrl;

                if (string.IsNullOrEmpty(token))
                    token = configuration["AmoCrm:AccessToken"] ?? "";
                if (string.IsNullOrEmpty(baseUrl))
                    baseUrl = configuration["AmoCrm:BaseUrl"] ?? "";

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    return Results.BadRequest(new { success = false, message = "BaseUrl veya AccessToken boş!" });
                }

                using var client = httpClientFactory.CreateClient("AmoCrm");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync("account", ct);

                return Results.Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    message = response.IsSuccessStatusCode
                        ? "✅ Bağlantı başarılı!"
                        : $"❌ API hatası: {response.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    success = false,
                    statusCode = 0,
                    message = $"❌ Bağlantı hatası: {ex.Message}"
                });
            }
        });

        // 5. Cache temizle
        group.MapPost("/invalidate-cache", (ISettingsService settingsService) =>
        {
            settingsService.InvalidateCache();
            return Results.Ok(new { message = "✅ Cache temizlendi." });
        });
    }
}

public record SettingUpdateRequest(string Category, string Key, string Value, string? UpdatedBy = null);
