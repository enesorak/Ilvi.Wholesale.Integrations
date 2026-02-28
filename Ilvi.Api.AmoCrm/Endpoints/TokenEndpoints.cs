using Ilvi.Api.AmoCrm.Services;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class TokenEndpoints
{
    public static void MapTokenEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tokens").WithTags("Token Management");

        // 1. Token durumu
        group.MapGet("/status", async (ITokenExpiryService tokenService, CancellationToken ct) =>
        {
            var info = await tokenService.CheckTokenExpiryAsync(ct);
            return Results.Ok(info);
        });

        // 2. Token güncelle (JSON body)
        group.MapPost("/update", async (
            TokenUpdateRequest request,
            ITokenExpiryService tokenService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { message = "Token boş olamaz!" });

            var success = await tokenService.UpdateTokenAsync(
                request.Token, request.ExpiresAt, request.UpdatedBy, ct);

            return success
                ? Results.Ok(new { message = "✅ Token başarıyla güncellendi." })
                : Results.BadRequest(new { message = "❌ Token güncellenemedi." });
        });

        // 2b. Token güncelle (düz metin - kolaylık için)
        group.MapPost("/update-raw", async (
            HttpRequest httpRequest,
            ITokenExpiryService tokenService,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(httpRequest.Body);
            var token = (await reader.ReadToEndAsync(ct)).Trim();

            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { message = "Token boş olamaz!" });

            var success = await tokenService.UpdateTokenAsync(token, null, "API-Raw", ct);

            return success
                ? Results.Ok(new { message = "✅ Token başarıyla güncellendi." })
                : Results.BadRequest(new { message = "❌ Token güncellenemedi." });
        });

        // 3. Manuel token kontrol tetikle (Telegram bildirim gönderir)
        group.MapPost("/check", async (ITokenExpiryService tokenService, CancellationToken ct) =>
        {
            var info = await tokenService.CheckTokenExpiryAsync(ct);
            return Results.Ok(new
            {
                checked_at = DateTime.UtcNow,
                result = info,
                notification_sent = info.Status is "warning" or "expired"
            });
        });
    }
}

public record TokenUpdateRequest(
    string Token,
    DateTime? ExpiresAt = null,
    string? UpdatedBy = null
);
