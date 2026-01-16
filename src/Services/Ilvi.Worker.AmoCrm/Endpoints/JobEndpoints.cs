using Hangfire;
using Hangfire.Storage;
using Ilvi.Worker.AmoCrm.Jobs; // Jobların olduğu namespace
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;

namespace Ilvi.Worker.AmoCrm.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs");

        // 1. Listele
        group.MapGet("/", (IRecurringJobManager manager) =>
        {
            using var connection = JobStorage.Current.GetConnection();
            var recurringJobs = connection.GetRecurringJobs();
            
            var list = recurringJobs.Select(j => new 
            {
                Id = j.Id,
                Cron = j.Cron,
                Queue = j.Queue,
                NextExecution = j.NextExecution?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Beklemede",
                LastExecution = j.LastExecution?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Henüz Çalışmadı",
                LastJobState = j.LastJobState,
                Error = j.LastJobId
            }).ToList();

            return Results.Ok(list);
        });

        // 2. Tetikle (Trigger)
        group.MapPost("/trigger/{id}", (string id, IRecurringJobManager manager) =>
        {
            manager.Trigger(id);
            return Results.Ok(new { message = $"Job '{id}' tetiklendi." });
        });

        // 3. Güncelle (Update Cron) - Switch Logic Burada
        group.MapPost("/update", (JobUpdateRequest req, IRecurringJobManager manager) =>
        {
            // Job Registry: String ID -> Metot Eşleştirmesi
            Expression<Func<CrmJobs, Task>>? jobExpression = req.Id switch
            {
                // Contacts
                "sync-contacts-incremental" => job => job.SyncContactsIncremental(null!, CancellationToken.None),
                "sync-contacts-full"        => job => job.SyncContactsFull(null!, CancellationToken.None),
                
                // Leads
                "sync-leads-incremental"    => job => job.SyncLeadsIncremental(null!, CancellationToken.None),
                "sync-leads-full"           => job => job.SyncLeadsFull(null!, CancellationToken.None),

            
                // Tasks
                "sync-tasks-incremental"    => job => job.SyncTasksIncremental(null!, CancellationToken.None),
                "sync-tasks-full"           => job => job.SyncTasksFull(null!, CancellationToken.None),
 
                "sync-pipelines"  => job => job.SyncPipelines(null!, CancellationToken.None),
                "sync-task-types" => job => job.SyncTaskTypes(null!, CancellationToken.None),
                
                // Events & Messages
                "sync-events"               => job => job.SyncEvents(null!, CancellationToken.None),
                "sync-messages"             => job => job.SyncMessages(null!, CancellationToken.None),

                _ => null 
            };

            if (jobExpression == null)
            {
                return Results.BadRequest(new { message = "Bu Job ID sistemde kod tarafında tanımlı değil." });
            }

            try
            {
                manager.AddOrUpdate<CrmJobs>(req.Id, jobExpression, req.Cron);
                return Results.Ok(new { message = $"Job '{req.Id}' zamanlaması güncellendi: {req.Cron}" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = $"Geçersiz Cron: {ex.Message}" });
            }
        });

        // 4. Sil
        group.MapDelete("/{id}", (string id, IRecurringJobManager manager) =>
        {
            manager.RemoveIfExists(id);
            return Results.Ok(new { message = $"Job '{id}' silindi." });
        });
    }
}

// DTO
public record JobUpdateRequest(string Id, string Cron);