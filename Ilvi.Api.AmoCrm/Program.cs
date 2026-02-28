using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Ilvi.Api.AmoCrm.Endpoints;
using Ilvi.Api.AmoCrm.Extensions;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;

using Ilvi.Modules.AmoCrm.Infrastructure.Repositories;
using Ilvi.Modules.AmoCrm.Infrastructure.Services;
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
    // 1. INFRASTRUCTURE (DB, Repository, HttpClient, Cache)
    // =====================================================
    builder.Services.AddDbContext<AmoCrmDbContext>(options =>
        options.UseSqlServer(connectionString,
            b => b.MigrationsAssembly("Ilvi.Modules.AmoCrm")));

    builder.Services.AddMemoryCache();
    builder.Services.AddScoped(typeof(IAmoRepository<,>), typeof(AmoRepository<,>));
    builder.Services.AddScoped<ISettingsService, SettingsService>();

    // HttpClient for AmoCRM API
    builder.Services.AddHttpClient<IAmoCrmService, AmoCrmService>((sp, client) =>
    {
        var baseUrl = builder.Configuration["AmoCrm:BaseUrl"] ?? "";
        var token = builder.Configuration["AmoCrm:AccessToken"] ?? "";

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromMinutes(10);

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    });

    // HttpClient for Telegram
    builder.Services.AddHttpClient("Telegram", client =>
    {
        client.BaseAddress = new Uri("https://api.telegram.org/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // MediatR (Modules assembly'deki tüm command handler'ları bulur)
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(IAmoCrmService).Assembly));

    // =====================================================
    // 2. API SERVICES (Token, Telegram, RateLimit)
    // =====================================================
    builder.Services.AddApiServices(builder.Configuration);

    // =====================================================
    // 3. HANGFIRE (Server + Client - jobları hem kurar hem çalıştırır)
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
        options.WorkerCount = 2; // Aynı anda max 2 job (rate limit için güvenli)
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
    // 5. MIDDLEWARE
    // =====================================================
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ilvi AmoCRM API v2");
        c.RoutePrefix = "swagger";
    });

    // Hangfire Dashboard (opsiyonel - API'den de erişilebilir)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "Ilvi AmoCRM - Job Dashboard",
    });

    // =====================================================
    // 6. AUTO-MIGRATE
    // =====================================================
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AmoCrmDbContext>();
        await context.Database.MigrateAsync();
        Log.Information("✅ Veritabanı migration tamamlandı.");
    }

    // =====================================================
    // 7. MAP ENDPOINTS
    // =====================================================
    app.MapJobEndpoints();
    app.MapSettingsEndpoints();
    app.MapHealthEndpoints();
    app.MapTokenEndpoints();
    app.MapRateLimitEndpoints();

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

    Log.Information("========================================");
    Log.Information("✅ API başarıyla başlatıldı!");
    Log.Information("📋 Swagger:     http://localhost:5090/swagger");
    Log.Information("🔧 Hangfire:    http://localhost:5090/hangfire");
    Log.Information("📊 Jobs:        http://localhost:5090/api/jobs");
    Log.Information("❤️ Health:      http://localhost:5090/api/health");
    Log.Information("========================================");

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
