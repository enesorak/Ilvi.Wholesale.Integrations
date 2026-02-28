using Ilvi.Modules.AmoCrm.Abstractions;

namespace Ilvi.Api.AmoCrm.Services;

public interface ITokenExpiryService
{
    Task<TokenExpiryInfo> CheckTokenExpiryAsync(CancellationToken ct = default);
    Task<bool> UpdateTokenAsync(string newToken, DateTime? expiresAt, string? updatedBy = null, CancellationToken ct = default);
}

public record TokenExpiryInfo(
    bool HasToken,
    DateTime? ExpiresAt,
    int? DaysUntilExpiry,
    string Status, // "ok", "warning", "expired", "unknown"
    string Message
);

public class TokenExpiryService : ITokenExpiryService
{
    private readonly ISettingsService _settingsService;
    private readonly ITelegramNotificationService _telegram;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenExpiryService> _logger;

    public TokenExpiryService(
        ISettingsService settingsService,
        ITelegramNotificationService telegram,
        IConfiguration configuration,
        ILogger<TokenExpiryService> logger)
    {
        _settingsService = settingsService;
        _telegram = telegram;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenExpiryInfo> CheckTokenExpiryAsync(CancellationToken ct = default)
    {
        var token = await _settingsService.GetValueAsync("AmoCrm", "AccessToken", ct);
        if (string.IsNullOrEmpty(token))
        {
            token = _configuration["AmoCrm:AccessToken"];
        }

        if (string.IsNullOrEmpty(token))
        {
            return new TokenExpiryInfo(false, null, null, "error", "Token bulunamadı!");
        }

        // JWT token'dan exp claim'ini çıkart
        var expiresAt = TryParseJwtExpiry(token);

        if (expiresAt == null)
        {
            // DB'den expiry bilgisini kontrol et
            var expiryStr = await _settingsService.GetValueAsync("AmoCrm", "TokenExpiresAt", ct);
            if (DateTime.TryParse(expiryStr, out var dbExpiry))
                expiresAt = dbExpiry;
        }

        if (expiresAt == null)
        {
            return new TokenExpiryInfo(true, null, null, "unknown", "Token var ama bitiş tarihi belirlenemiyor.");
        }

        var daysUntil = (int)(expiresAt.Value - DateTime.UtcNow).TotalDays;
        var warnDays = _configuration.GetValue("TokenExpiry:WarnDaysBeforeExpiry", 14);

        if (daysUntil <= 0)
        {
            var msg = $"⚠️ AmoCRM Token SÜRESİ DOLMUŞ! ({expiresAt:yyyy-MM-dd})";
            _logger.LogWarning(msg);
            await _telegram.SendMessageAsync(msg);
            return new TokenExpiryInfo(true, expiresAt, daysUntil, "expired", msg);
        }

        if (daysUntil <= warnDays)
        {
            var msg = $"⏰ AmoCRM Token {daysUntil} gün sonra sona erecek! ({expiresAt:yyyy-MM-dd})";
            _logger.LogWarning(msg);
            await _telegram.SendMessageAsync(msg);
            return new TokenExpiryInfo(true, expiresAt, daysUntil, "warning", msg);
        }

        return new TokenExpiryInfo(true, expiresAt, daysUntil, "ok",
            $"Token geçerli. {daysUntil} gün kaldı. ({expiresAt:yyyy-MM-dd})");
    }

    public async Task<bool> UpdateTokenAsync(string newToken, DateTime? expiresAt, string? updatedBy = null, CancellationToken ct = default)
    {
        try
        {
            await _settingsService.SetAsync("AmoCrm", "AccessToken", newToken, updatedBy ?? "API", ct);

            if (expiresAt.HasValue)
            {
                await _settingsService.SetAsync("AmoCrm", "TokenExpiresAt",
                    expiresAt.Value.ToString("O"), updatedBy ?? "API", ct);
            }
            else
            {
                // JWT'den expiry çıkart
                var jwtExpiry = TryParseJwtExpiry(newToken);
                if (jwtExpiry.HasValue)
                {
                    await _settingsService.SetAsync("AmoCrm", "TokenExpiresAt",
                        jwtExpiry.Value.ToString("O"), updatedBy ?? "API", ct);
                }
            }

            _logger.LogInformation("✅ Token güncellendi. Güncelleyen: {User}", updatedBy ?? "API");
            await _telegram.SendMessageAsync($"✅ AmoCRM Token güncellendi. ({updatedBy ?? "API"})");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token güncelleme hatası");
            return false;
        }
    }

    private static DateTime? TryParseJwtExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1];
            // Base64 padding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expProp))
            {
                var expUnix = expProp.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            }
        }
        catch
        {
            // JWT parse hatası - ignore
        }

        return null;
    }
}
