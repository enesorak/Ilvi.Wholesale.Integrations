using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Http;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Ilvi.Modules.AmoCrm.Infrastructure.Repositories;
using Ilvi.Modules.AmoCrm.Infrastructure.Services;
using Ilvi.Modules.AmoCrm.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ilvi.Modules.AmoCrm;

public static class DependencyInjection
{
    public static IServiceCollection AddAmoCrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Ayarları Bind Et
        services.Configure<AmoCrmOptions>(configuration.GetSection("AmoCrm"));

        // 2. Veritabanı
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AmoCrmDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions => 
            {
                // Komut süresini 5 dakikaya çıkarıyoruz.
                // Bulk işlemler için bu şart.
                sqlOptions.CommandTimeout(300); 
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5, 
                    maxRetryDelay: TimeSpan.FromSeconds(30), 
                    errorNumbersToAdd: null);
            }));

        // 3. Auth Handler
        services.AddTransient<AmoAuthHandler>();

        // 4. HttpClient ve Servis Kaydı
        services.AddHttpClient<IAmoCrmService, AmoCrmService>((provider, client) =>
            {
                var options = configuration.GetSection("AmoCrm").Get<AmoCrmOptions>();
                if (options != null && !string.IsNullOrEmpty(options.BaseUrl))
                {
                    client.BaseAddress = new Uri(options.BaseUrl);
                }
            })
            .AddHttpMessageHandler<AmoAuthHandler>(); // Token'ı otomatik ekle

        // Repository Kaydı
        
        services.AddScoped(typeof(IAmoRepository<,>), typeof(AmoRepository<,>));
        
        services.Configure<AmoCrmSyncSettings>(configuration.GetSection("AmoCrm:SyncSettings"));
        return services;
    }
}