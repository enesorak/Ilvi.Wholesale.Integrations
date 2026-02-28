using Ilvi.Api.AmoCrm.Services;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class RateLimitEndpoints
{
    public static void MapRateLimitEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/rate-limit").WithTags("Rate Limit");

        // 1. Mevcut rate limit durumu
        group.MapGet("/status", (IRateLimitMonitorService service) =>
        {
            var status = service.GetStatus();
            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                amoCrmLimits = new
                {
                    description = "AmoCRM API Rate Limit Kuralları",
                    perIntegration = "Max 7 istek/saniye",
                    perAccount = "Max 50 istek/saniye (tüm entegrasyonlar toplamı)",
                    onExceed = "429 Too Many Requests → Otomatik bekleme",
                    onSevereAbuse = "Hesap API erişimi tamamen bloke edilebilir"
                },
                currentStatus = status
            });
        });

        // 2. İstatistikleri sıfırla
        group.MapPost("/reset", (IRateLimitMonitorService service) =>
        {
            service.ResetStats();
            return Results.Ok(new { message = "✅ Rate limit istatistikleri sıfırlandı." });
        });

        // 3. Ayarları güncelle
        group.MapPut("/settings", (
            RateLimitSettingsRequest request,
            IRateLimitMonitorService service) =>
        {
            // AmoCRM limiti: max 7/s per integration
            if (request.MaxRequestsPerSecond > 7)
            {
                return Results.BadRequest(new
                {
                    message = "AmoCRM limiti: Tek entegrasyon için max 7 istek/saniye!",
                    hint = "Daha yüksek limit için AmoCRM'den ek paket satın almanız gerekir."
                });
            }

            service.UpdateSettings(
                request.MaxRequestsPerSecond,
                request.RetryAfterMs,
                request.EnableAdaptiveThrottling);

            return Results.Ok(new
            {
                message = "✅ Rate limit ayarları güncellendi.",
                settings = new
                {
                    request.MaxRequestsPerSecond,
                    request.RetryAfterMs,
                    request.EnableAdaptiveThrottling
                }
            });
        });

        // 4. Rate limit test - belirli sayıda dummy request simüle et
        group.MapPost("/simulate", async (
            RateLimitSimulateRequest request,
            IRateLimitMonitorService service,
            CancellationToken ct) =>
        {
            var results = new List<object>();
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < Math.Min(request.RequestCount, 20); i++)
            {
                var beforeWait = DateTime.UtcNow;
                await service.WaitIfNeededAsync(ct);
                var afterWait = DateTime.UtcNow;
                var waited = (afterWait - beforeWait).TotalMilliseconds;

                service.RecordSuccess();

                results.Add(new
                {
                    request = i + 1,
                    waitedMs = Math.Round(waited, 1),
                    timestamp = afterWait.ToString("HH:mm:ss.fff")
                });
            }

            var totalTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return Results.Ok(new
            {
                message = $"✅ {results.Count} request simüle edildi.",
                totalTimeMs = Math.Round(totalTime, 1),
                effectiveRps = Math.Round(results.Count / (totalTime / 1000.0), 2),
                details = results,
                status = service.GetStatus()
            });
        });
    }
}

public record RateLimitSettingsRequest(
    int MaxRequestsPerSecond = 7,
    int RetryAfterMs = 1000,
    bool EnableAdaptiveThrottling = true
);

public record RateLimitSimulateRequest(int RequestCount = 10);
