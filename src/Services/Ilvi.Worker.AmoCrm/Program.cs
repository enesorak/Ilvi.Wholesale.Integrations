using Hangfire;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Ilvi.Worker.AmoCrm.Endpoints;  // Endpointler burada
using Ilvi.Worker.AmoCrm.Extensions; // Extensionlar burada
using Ilvi.Worker.AmoCrm.Jobs;
using Microsoft.EntityFrameworkCore; // Job tanımları

var builder = WebApplication.CreateBuilder(args);

// 1. Servis Kayıtları (Extension Metotlar)
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWorkerServices(builder.Configuration);

var app = builder.Build();

// 2. Middleware (Extension Metot)
app.UseWorkerMiddleware();

// 3. Endpoint Tanımları (Extension Metot)
app.MapJobEndpoints();


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("⏳ Veritabanı kontrol ediliyor...");
        var context = services.GetRequiredService<AmoCrmDbContext>();
        
        // Varsa eksik migration'ları uygular (Tablo yoksa oluşturur, kolon değiştiyse günceller)
        await context.Database.MigrateAsync();
        
        logger.LogInformation("✅ Veritabanı başarıyla güncellendi!");
    }
    catch (Exception ex)
    {
        // DB hatası varsa uygulama çalışmamalı, loglayıp durduruyoruz.
        logger.LogCritical(ex, "❌ Veritabanı Migration hatası! Uygulama durduruluyor.");
        throw; 
    }
}



// 4. Jobları Başlatma (İlk Kurulum)
// Uygulama her başladığında varsayılan jobları tekrar kaydeder (Idempotent)
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // --- CONTACTS ---
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-contacts-incremental", job => job.SyncContactsIncremental(null!, default), Cron.MinuteInterval(30));
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-contacts-full", job => job.SyncContactsFull(null!, default), Cron.Daily(3));

    // --- LEADS ---
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-leads-incremental", job => job.SyncLeadsIncremental(null!, default), Cron.MinuteInterval(30));
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-leads-full", job => job.SyncLeadsFull(null!, default), Cron.Daily(3, 30));

  
    // --- TASKS ---
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-tasks-incremental", job => job.SyncTasksIncremental(null!, default), "20,50 * * * *");
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-tasks-full", job => job.SyncTasksFull(null!, default), Cron.Daily(4, 30));

    // --- DEFINITIONS ---
    recurringJobManager.AddOrUpdate<CrmJobs>(
        "sync-pipelines", 
        job => job.SyncPipelines(null!, default), 
        Cron.Daily(5)
    );

    // Task Types (Günde 1 kez, sabah 05:05)
    recurringJobManager.AddOrUpdate<CrmJobs>(
        "sync-task-types", 
        job => job.SyncTaskTypes(null!, default), 
        Cron.Daily(5, 5)
    );

    // --- EVENTS & MESSAGES ---
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-events", job => job.SyncEvents(null!, default), Cron.Hourly());
    recurringJobManager.AddOrUpdate<CrmJobs>("sync-messages", job => job.SyncMessages(null!, default), Cron.MinuteInterval(15));
}

app.Run();