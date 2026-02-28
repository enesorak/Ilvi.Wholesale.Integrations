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
            var client = _httpClientFactory.CreateClient("Telegram");
            var url = $"bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text = $"🏢 Ilvi AmoCRM\n\n{message}",
                parse_mode = "HTML"
            };

            var response = await client.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Telegram mesajı gönderildi.");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Telegram mesaj gönderilemedi: {StatusCode} - {Error}",
                response.StatusCode, error);
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
            var client = _httpClientFactory.CreateClient("Telegram");
            var response = await client.GetAsync($"bot{botToken}/getMe");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
