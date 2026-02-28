using Ilvi.Api.AmoCrm.Services;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Ilvi.Modules.AmoCrm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ilvi.Api.AmoCrm.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddApiInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database (EF Core) - Worker ile aynı DB
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AmoCrmDbContext>(options =>
            options.UseSqlServer(connectionString,
                b => b.MigrationsAssembly("Ilvi.Modules.AmoCrm")));

        // 2. Memory Cache
        services.AddMemoryCache();

        // 3. Settings Service
        services.AddScoped<ISettingsService, SettingsService>();

        // 4. HttpClient for AmoCRM API (bağlantı testi vb. için)
        services.AddHttpClient("AmoCrm", (sp, client) =>
        {
            var baseUrl = configuration["AmoCrm:BaseUrl"] ?? "";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 5. HttpClient for Telegram
        services.AddHttpClient("Telegram", client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Token Expiry Service
        services.AddScoped<ITokenExpiryService, TokenExpiryService>();

        // 2. Telegram Notification Service
        services.AddScoped<ITelegramNotificationService, TelegramNotificationService>();

        // 3. Rate Limit Monitor Service
        services.AddSingleton<IRateLimitMonitorService, RateLimitMonitorService>();

        return services;
    }
}
