using Hangfire;
using Hangfire.SqlServer;
 
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence;
using Ilvi.Modules.AmoCrm.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Ilvi.Modules.AmoCrm.Infrastructure.Services;
// Diğer gerekli using'ler...

namespace Ilvi.Worker.AmoCrm.Extensions;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructureServices(IConfiguration configuration)
        {
            // 1. Veritabanı (EF Core)
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<AmoCrmDbContext>(options =>
                options.UseSqlServer(connectionString,
                    b => b.MigrationsAssembly("Ilvi.Modules.AmoCrm"))); // Migration assembly'ye dikkat

            // 2. Repository & Servisler
            services.AddScoped(typeof(IAmoRepository<,>), typeof(AmoRepository<,>));
            // HttpClient kullanan servisler
            services.AddHttpClient<IAmoCrmService, AmoCrmService>((sp, client) =>
            {
                // AppSettings'den ayarları oku
                var baseUrl = configuration["AmoCrm:BaseUrl"];
                var token = configuration["AmoCrm:AccessToken"];

                if (string.IsNullOrEmpty(baseUrl))
                    throw new ArgumentNullException("AmoCrm:BaseUrl", "appsettings.json içinde AmoCRM BaseUrl bulunamadı!");

                // BaseUrl ayarla (Sonunda / yoksa ekle)
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                client.BaseAddress = new Uri(baseUrl);

                // Timeout ayarı (Büyük veriler için süreyi uzat)
                client.Timeout = TimeSpan.FromMinutes(10);

                // Token varsa Header'a ekle (Long-Lived Token kullanıyorsan)
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            });

            // 3. MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IAmoCrmService).Assembly));
        }

        public void AddWorkerServices(IConfiguration configuration)
        {
            // 1. Hangfire
            var connectionString = configuration.GetConnectionString("DefaultConnection");
        
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

            services.AddHangfireServer();

            // 2. Windows Service Desteği
            services.AddWindowsService(options =>
            {
                options.ServiceName = "Ilvi.AmoCrm.Integration";
            });
        }
    }
}