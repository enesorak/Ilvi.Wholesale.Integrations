using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Ilvi.Api.AmoCrm.Endpoints;
using Ilvi.Api.AmoCrm.Extensions;
using Ilvi.Api.AmoCrm.Jobs;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Ilvi.Modules.AmoCrm.Infrastructure.Repositories;
using Ilvi.Modules.AmoCrm.Infrastructure.Services;
using Ilvi.Modules.AmoCrm.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("========================================");
    Log.Information("🚀 Ilvi AmoCRM API başlatılıyor...");
    Log.Information("========================================");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // =====================================================
    // 0. DB YOKSA OLUŞTUR (Hangfire'dan ÖNCE!)
    // =====================================================
    await EnsureDatabaseExistsAsync(connectionString!);

    // =====================================================
    // 1. INFRASTRUCTURE
    // =====================================================
    builder.Services.AddDbContext<AmoCrmDbContext>(options =>
        options.UseSqlServer(connectionString,
            b => b.MigrationsAssembly("Ilvi.Modules.AmoCrm")));

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped(typeof(IAmoRepository<,>), typeof(AmoRepository<,>));
    builder.Services.AddScoped<ISettingsService, SettingsService>();

    // HttpClient for AmoCRM API
    builder.Services.AddHttpClient<IAmoCrmService, AmoCrmService>()
        .ConfigureHttpClient((sp, client) =>
        {
            using var scope = sp.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            var allSettings = settings.GetByCategoryAsync("AmoCrm").GetAwaiter().GetResult();
            var baseUrl = allSettings.FirstOrDefault(s => s.Key == "BaseUrl")?.Value ?? "";
            var token = allSettings.FirstOrDefault(s => s.Key == "AccessToken")?.Value ?? "";

            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = builder.Configuration["AmoCrm:BaseUrl"] ?? "";
            if (string.IsNullOrEmpty(token))
                token = builder.Configuration["AmoCrm:AccessToken"] ?? "";

            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        });

    // SyncSettings
    builder.Services.Configure<AmoCrmSyncSettings>(
        builder.Configuration.GetSection("AmoCrm:SyncSettings"));

    // HttpClient for Telegram
    builder.Services.AddHttpClient("Telegram", client =>
    {
        client.BaseAddress = new Uri("https://api.telegram.org/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // MediatR
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(IAmoCrmService).Assembly));

    // =====================================================
    // 2. API SERVICES
    // =====================================================
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddScoped<HealthCheckJob>();

    // =====================================================
    // 3. HANGFIRE
    // =====================================================
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseConsole()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = "Ilvi.Api.AmoCrm";
        options.WorkerCount = builder.Configuration.GetValue<int>("Hangfire:WorkerCount", 5);
    });

    // =====================================================
    // 4. SWAGGER + CORS
    // =====================================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Ilvi AmoCRM Management API",
            Version = "v2.0",
            Description = "AmoCRM entegrasyon yönetim API'si — Jobs, Settings, Tokens, Health, Rate Limit"
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    var app = builder.Build();

    // =====================================================
    // 5. AUTO-MIGRATE
    // =====================================================
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AmoCrmDbContext>();
        for (int i = 0; i < 5; i++)
        {
            try
            {
                var pending = await context.Database.GetPendingMigrationsAsync();
                if (pending.Any())
                {
                    await context.Database.MigrateAsync();
                    Log.Information("✅ {Count} migration uygulandı.", pending.Count());
                }
                else
                {
                    Log.Information("✅ Veritabanı güncel, migration gerekmedi.");
                }
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⏳ DB migration bekleniyor... ({Attempt}/5)", i + 1);
                await Task.Delay(3000);
                if (i == 4) throw;
            }
        }
    }

    // =====================================================
    // 5.1 TEMP TABLOLARI TEMİZLE
    // =====================================================
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AmoCrmDbContext>();
        try
        {
            var tempTables = await context.Database
                .SqlQueryRaw<string>(
                    "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Temp[a-f0-9][a-f0-9][a-f0-9]%'")
                .ToListAsync();

            foreach (var table in tempTables)
            {
                await context.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS [{table}]");
                Log.Information("🗑️ Temp tablo silindi: {Table}", table);
            }

            if (tempTables.Count > 0)
                Log.Information("✅ {Count} temp tablo temizlendi.", tempTables.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Temp tablo temizliği sırasında hata (önemsiz).");
        }
    }

    // =====================================================
    // 5.2 SEED SETTINGS
    // =====================================================
    using (var scope = app.Services.CreateScope())
    {
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        try
        {
            var existing = await settingsService.GetByCategoryAsync("AmoCrm");
            var baseUrlSetting = existing.FirstOrDefault(s => s.Key == "BaseUrl");

            if (baseUrlSetting == null || string.IsNullOrEmpty(baseUrlSetting.Value))
            {
                var config = builder.Configuration;
                var baseUrl = config["AmoCrm:BaseUrl"];
                var token = config["AmoCrm:AccessToken"];
                var pageSize = config["AmoCrm:PageSize"];
                var delay = config["AmoCrm:RequestDelayMs"];

                if (!string.IsNullOrEmpty(baseUrl))
                {
                    Log.Information("📥 appsettings → DB ayar aktarımı yapılıyor...");
                    await settingsService.SetAsync("AmoCrm", "BaseUrl", baseUrl, "System-Seed");
                    if (!string.IsNullOrEmpty(token))
                        await settingsService.SetAsync("AmoCrm", "AccessToken", token, "System-Seed");
                    if (!string.IsNullOrEmpty(pageSize))
                        await settingsService.SetAsync("AmoCrm", "PageSize", pageSize, "System-Seed");
                    if (!string.IsNullOrEmpty(delay))
                        await settingsService.SetAsync("AmoCrm", "RequestDelayMs", delay, "System-Seed");

                    var eventsLookBack = config["AmoCrm:SyncSettings:EventsLookBackMonths"];
                    var messagesLookBack = config["AmoCrm:SyncSettings:MessagesLookBackMonths"];
                    if (!string.IsNullOrEmpty(eventsLookBack))
                        await settingsService.SetAsync("Sync", "EventsLookBackMonths", eventsLookBack, "System-Seed");
                    if (!string.IsNullOrEmpty(messagesLookBack))
                        await settingsService.SetAsync("Sync", "MessagesLookBackMonths", messagesLookBack, "System-Seed");

                    Log.Information("✅ Ayarlar DB'ye aktarıldı.");
                }
            }
            else
            {
                Log.Information("✅ DB ayarları zaten mevcut, seed atlandı.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Seed sırasında hata.");
        }
    }

    // =====================================================
    // 6. MIDDLEWARE
    // =====================================================
    app.UseCors();
    app.UseFileServer();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ilvi AmoCRM API v2");
        c.RoutePrefix = "swagger";
    });

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "Ilvi AmoCRM - Job Dashboard",
    });

    // =====================================================
    // 6.1 HEALTH CHECK RECURRING JOB (Günlük 09:00 UTC)
    // =====================================================
    RecurringJob.AddOrUpdate<HealthCheckJob>(
        "daily-health-report",
        job => job.DailyReport(null!, default),
        builder.Configuration.GetValue<string>("HealthCheck:CronExpression", "0 9 * * *"));

    // =====================================================
    // 7. MAP ENDPOINTS
    // =====================================================
    app.MapJobEndpoints();
    app.MapSettingsEndpoints();
    app.MapHealthEndpoints();
    app.MapTokenEndpoints();
    app.MapRateLimitEndpoints();
    app.MapRunJobEndpoints();

    app.MapGet("/", () => Results.Ok(new
    {
        service = "Ilvi AmoCRM Management API",
        version = "2.0.0",
        status = "running",
        hangfireServer = true,
        timestamp = DateTime.UtcNow,
        endpoints = new
        {
            swagger = "/swagger",
            hangfire = "/hangfire",
            jobs = "/api/jobs",
            available = "/api/jobs/available",
            setupAll = "POST /api/jobs/setup-all",
            health = "/api/health",
            settings = "/api/settings",
            tokens = "/api/tokens",
            rateLimit = "/api/rate-limit"
        }
    }));

    // =====================================================
    // 8. STARTUP BİLDİRİMİ
    // =====================================================
    try
    {
        using var scope = app.Services.CreateScope();
        var healthJob = scope.ServiceProvider.GetRequiredService<HealthCheckJob>();
        await healthJob.SendStartupNotification();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Startup bildirimi gönderilemedi.");
    }

    Log.Information("========================================");
    Log.Information("✅ API başarıyla başlatıldı!");
    Log.Information("📋 Swagger:     http://localhost:5090/swagger");
    Log.Information("🔧 Hangfire:    http://localhost:5090/hangfire");
    Log.Information("📊 Jobs:        http://localhost:5090/api/jobs");
    Log.Information("❤️ Health:      http://localhost:5090/api/health");
    Log.Information("========================================");

    // =====================================================
    // 9. SHUTDOWN BİLDİRİMİ
    // =====================================================
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var healthJob = scope.ServiceProvider.GetRequiredService<HealthCheckJob>();
            healthJob.SendShutdownNotification().GetAwaiter().GetResult();
        }
        catch { }
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ API başlatılamadı!");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ==========================================================
// DB yoksa oluşturur
// ==========================================================
static async Task EnsureDatabaseExistsAsync(string connectionString)
{
    var connBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    var databaseName = connBuilder.InitialCatalog;
    connBuilder.InitialCatalog = "master";

    for (int i = 0; i < 5; i++)
    {
        try
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connBuilder.ConnectionString);
            await connection.OpenAsync();

            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT database_id FROM sys.databases WHERE name = '{databaseName}'";
            var result = await checkCmd.ExecuteScalarAsync();

            if (result == null)
            {
                var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE [{databaseName}]";
                await createCmd.ExecuteNonQueryAsync();
                Log.Information("✅ Veritabanı '{Database}' oluşturuldu.", databaseName);
            }
            else
            {
                Log.Information("✅ Veritabanı '{Database}' mevcut.", databaseName);
            }
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⏳ SQL Server bağlantısı bekleniyor... ({Attempt}/5)", i + 1);
            await Task.Delay(3000);
            if (i == 4) throw;
        }
    }
}