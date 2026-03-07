using System.Text.RegularExpressions;

namespace Ilvi.Api.AmoCrm.Services;

public interface ITelegramNotificationService
{
    Task<bool> SendMessageAsync(string message);
    Task<bool> TestConnectionAsync();
}

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        if (!_configuration.GetValue("Telegram:Enabled", false))
        {
            _logger.LogDebug("Telegram bildirimleri devre dışı.");
            return false;
        }

        var botToken = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];

        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
        {
            _logger.LogWarning("Telegram bot token veya chat ID yapılandırılmamış.");
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            // HTML ile dene
            var payload = new Dictionary<string, object>
            {
                ["chat_id"] = long.Parse(chatId),
                ["text"] = $"🏢 Ilvi AmoCRM\n\n{message}",
                ["parse_mode"] = "HTML",
                ["disable_web_page_preview"] = true
            };

            var response = await client.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Telegram mesajı gönderildi.");
                return true;
            }

            // HTML parse hatası → düz metin fallback
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Telegram HTML hata: {Error}. Düz metin deneniyor...", error);

            var fallback = new Dictionary<string, object>
            {
                ["chat_id"] = long.Parse(chatId),
                ["text"] = $"🏢 Ilvi AmoCRM\n\n{StripHtml(message)}"
            };

            var retryResponse = await client.PostAsJsonAsync(url, fallback);

            if (retryResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Telegram mesajı (düz metin) gönderildi.");
                return true;
            }

            var retryError = await retryResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Telegram düz metin de başarısız: {Error}", retryError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram mesaj gönderme hatası");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        var botToken = _configuration["Telegram:BotToken"];

        if (string.IsNullOrEmpty(botToken))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.telegram.org/bot{botToken}/getMe");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string StripHtml(string html)
    {
        return Regex.Replace(html, "<[^>]+>", "");
    }
}