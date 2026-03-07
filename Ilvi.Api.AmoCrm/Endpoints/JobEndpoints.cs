using Hangfire;
using Hangfire.Storage;
using Ilvi.Api.AmoCrm.Jobs;
using System.Linq.Expressions;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class JobEndpoints
{
    private static readonly Dictionary<string, JobDefinition> JobRegistry = new()
    {
       // ["sync-contacts-incremental"] = new("👥 Kişiler (Incremental)", "Sadece değişen kişileri çeker", "*/30 * * * *",
        //    (Expression<Func<CrmJobs, Task>>)(j => j.SyncContactsIncremental(null!, default))),
        ["sync-contacts-full"] = new("🌕 Kişiler (Full Sync)", "Tüm kişileri baştan çeker", "0 3,5,7 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncContactsFull(null!, default))),

        //["sync-leads-incremental"] = new("💼 Fırsatlar (Incremental)", "Sadece değişen fırsatları çeker", "*/30 * * * *",
         //   (Expression<Func<CrmJobs, Task>>)(j => j.SyncLeadsIncremental(null!, default))),
        ["sync-leads-full"] = new("🌕 Fırsatlar (Full Sync)", "Tüm fırsatları baştan çeker", "0 3,5,7 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncLeadsFull(null!, default))),

      //  ["sync-tasks-incremental"] = new("📅 Görevler (Incremental)", "Sadece değişen görevleri çeker", "20,50 * * * *",
     //       (Expression<Func<CrmJobs, Task>>)(j => j.SyncTasksIncremental(null!, default))),
        ["sync-tasks-full"] = new("🌕 Görevler (Full Sync)", "Tüm görevleri baştan çeker", "0 3,5,7 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncTasksFull(null!, default))),

        ["sync-events"] = new("📜 Olaylar", "Olay günlüğünü senkronize eder", "0 1,3,5 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncEvents(null!, default))),

        ["sync-messages"] = new("💬 Mesajlar", "Chat mesajlarını senkronize eder", "0 1,3,5 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncMessages(null!, default))),

        ["sync-pipelines"] = new("📊 Pipelines", "Pipeline ve aşamaları senkronize eder", "0 3 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncPipelines(null!, default))),

        ["sync-task-types"] = new("📝 Görev Tipleri", "Görev tiplerini senkronize eder", "0 3 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncTaskTypes(null!, default))),

        ["sync-users"] = new("👤 Kullanıcılar", "AmoCRM kullanıcılarını senkronize eder", "0 3 * * *",
            (Expression<Func<CrmJobs, Task>>)(j => j.SyncUsers(null!, default))),
    };

    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Job Management");

        // 1. Kurulu jobları listele
        group.MapGet("/", (JobStorage storage) =>
        {
            using var connection = storage.GetConnection();
            var jobs = connection.GetRecurringJobs();
            return Results.Ok(new
            {
                count = jobs.Count,
                jobs = jobs.Select(j => new
                {
                    j.Id, j.Cron, j.Queue,
                    DisplayName = JobRegistry.TryGetValue(j.Id, out var d) ? d.DisplayName : j.Id,
                    Description = JobRegistry.TryGetValue(j.Id, out var d2) ? d2.Description : null,
                    NextExecution = j.NextExecution?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Beklemede",
                    LastExecution = j.LastExecution?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Henüz Çalışmadı",
                    j.LastJobState, j.LastJobId, j.Error
                })
            });
        });

        // 2. Kurulabilecek tüm job tanımları
        group.MapGet("/available", (JobStorage storage) =>
        {
            using var connection = storage.GetConnection();
            var existing = connection.GetRecurringJobs().Select(j => j.Id).ToHashSet();

            return Results.Ok(JobRegistry.Select(kv => new
            {
                Id = kv.Key, kv.Value.DisplayName, kv.Value.Description,
                DefaultCron = kv.Value.DefaultCron,
                IsInstalled = existing.Contains(kv.Key)
            }));
        });

        // 3. TÜM JOBLARI KUR
        group.MapPost("/setup-all", (IRecurringJobManager manager) =>
        {
            var installed = new List<object>();
            foreach (var (id, def) in JobRegistry)
            {
                manager.AddOrUpdate(id, def.Expression, def.DefaultCron);
                installed.Add(new { id, def.DisplayName, cron = def.DefaultCron });
            }
            return Results.Ok(new { message = $"✅ {installed.Count} job kuruldu.", jobs = installed });
        });

        // 4. Tek job kur (varsayılan cron)
        group.MapPost("/setup/{id}", (string id, IRecurringJobManager manager) =>
        {
            if (!JobRegistry.TryGetValue(id, out var def))
                return Results.NotFound(new { message = $"Job '{id}' tanımlı değil.", availableJobs = JobRegistry.Keys });

            manager.AddOrUpdate(id, def.Expression, def.DefaultCron);
            return Results.Ok(new { message = $"✅ '{def.DisplayName}' kuruldu.", id, cron = def.DefaultCron });
        });

        // 5. Tek job kur (özel cron)
        group.MapPost("/setup-with-cron", (JobSetupRequest request, IRecurringJobManager manager) =>
        {
            if (!JobRegistry.TryGetValue(request.Id, out var def))
                return Results.NotFound(new { message = $"Job '{request.Id}' tanımlı değil." });

            var cron = string.IsNullOrWhiteSpace(request.Cron) ? def.DefaultCron : request.Cron;
            try { Cronos.CronExpression.Parse(cron); }
            catch (Exception ex) { return Results.BadRequest(new { message = $"Geçersiz cron: {ex.Message}" }); }

            manager.AddOrUpdate(request.Id, def.Expression, cron);
            return Results.Ok(new { message = $"✅ '{def.DisplayName}' kuruldu: {cron}", id = request.Id, cron });
        });

        // 6. Hemen tetikle (kurulu değilse önce kurar)
        group.MapPost("/trigger/{id}", (string id, IRecurringJobManager manager, JobStorage storage) =>
        {
            using var connection = storage.GetConnection();
            var exists = connection.GetRecurringJobs().Any(j => j.Id == id);

            if (!exists)
            {
                if (JobRegistry.TryGetValue(id, out var def))
                {
                    manager.AddOrUpdate(id, def.Expression, def.DefaultCron);
                    manager.Trigger(id);
                    return Results.Ok(new { message = $"✅ '{def.DisplayName}' kurulup tetiklendi.", autoInstalled = true });
                }
                return Results.NotFound(new { message = $"Job '{id}' bulunamadı." });
            }

            manager.Trigger(id);
            return Results.Ok(new { message = $"✅ Job '{id}' tetiklendi." });
        });

        // 7. Tümünü tetikle
        group.MapPost("/trigger-all", (IRecurringJobManager manager, JobStorage storage) =>
        {
            using var connection = storage.GetConnection();
            var jobs = connection.GetRecurringJobs();
            var triggered = jobs.Select(j => { manager.Trigger(j.Id); return j.Id; }).ToList();
            return Results.Ok(new { message = $"✅ {triggered.Count} job tetiklendi.", jobs = triggered });
        });

        // 8. Cron güncelle
        group.MapPost("/update-cron", (JobCronUpdateRequest request, IRecurringJobManager manager) =>
        {
            if (!JobRegistry.TryGetValue(request.Id, out var def))
                return Results.NotFound(new { message = $"Job '{request.Id}' tanımlı değil." });

            try { Cronos.CronExpression.Parse(request.Cron); }
            catch (Exception ex) { return Results.BadRequest(new { message = $"Geçersiz cron: {ex.Message}" }); }

            manager.AddOrUpdate(request.Id, def.Expression, request.Cron);
            return Results.Ok(new { message = $"✅ '{def.DisplayName}' cron güncellendi: {request.Cron}" });
        });

        // 9. Job sil
        group.MapDelete("/{id}", (string id, IRecurringJobManager manager) =>
        {
            manager.RemoveIfExists(id);
            return Results.Ok(new { message = $"✅ Job '{id}' silindi." });
        });

        // 10. Özet istatistikler
        group.MapGet("/summary", (JobStorage storage) =>
        {
            using var connection = storage.GetConnection();
            var jobs = connection.GetRecurringJobs();
            return Results.Ok(new
            {
                total = jobs.Count,
                succeeded = jobs.Count(j => j.LastJobState == "Succeeded"),
                failed = jobs.Count(j => j.LastJobState == "Failed"),
                processing = jobs.Count(j => j.LastJobState == "Processing"),
                unknown = jobs.Count(j => string.IsNullOrEmpty(j.LastJobState)),
            });
        });

        // 11. Hangfire server durumu
        group.MapGet("/servers", (JobStorage storage) =>
        {
            var monitor = storage.GetMonitoringApi();
            var servers = monitor.Servers();
            return Results.Ok(new
            {
                count = servers.Count,
                workerRunning = servers.Count > 0,
                servers = servers.Select(s => new
                {
                    s.Name, s.Queues, s.WorkersCount,
                    StartedAt = s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    Heartbeat = s.Heartbeat?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                })
            });
        });

        // 12. Hangfire genel istatistikler
        group.MapGet("/hangfire-stats", (JobStorage storage) =>
        {
            var monitor = storage.GetMonitoringApi();
            var stats = monitor.GetStatistics();
            return Results.Ok(new
            {
                stats.Enqueued, stats.Failed, stats.Processing, stats.Succeeded,
                stats.Deleted, stats.Scheduled, stats.Servers, stats.Queues, stats.Recurring
            });
        });
        
        // 13. One-time job (tek seferlik, recurring oluşturmaz)
        group.MapPost("/run/{id}", (string id, IBackgroundJobClient client) =>
        {
            if (!JobRegistry.TryGetValue(id, out var def))
                return Results.NotFound(new { message = $"Job '{id}' tanımlı değil.", availableJobs = JobRegistry.Keys });

            var jobId = client.Enqueue(def.Expression);

            return Results.Ok(new
            {
                message = $"✅ '{def.DisplayName}' tek seferlik başlatıldı.",
                hangfireJobId = jobId
            });
        });
    }
    
    

    private record JobDefinition(string DisplayName, string Description, string DefaultCron, Expression<Func<CrmJobs, Task>> Expression);
}

public record JobCronUpdateRequest(string Id, string Cron);
public record JobSetupRequest(string Id, string? Cron = null);
