using Hangfire;
using Ilvi.Api.AmoCrm.Jobs;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class RunJobEndpoints
{
    public static void MapRunJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/run").WithTags("Tek Seferlik Çalıştır");

        group.MapPost("/sync-contacts-full", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncContactsFull(null!, default));
            return Results.Ok(new { message = "✅ Kişiler (Full) başlatıldı.", jobId });
        });

        group.MapPost("/sync-leads-full", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncLeadsFull(null!, default));
            return Results.Ok(new { message = "✅ Fırsatlar (Full) başlatıldı.", jobId });
        });

        group.MapPost("/sync-tasks-full", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncTasksFull(null!, default));
            return Results.Ok(new { message = "✅ Görevler (Full) başlatıldı.", jobId });
        });

        group.MapPost("/sync-events", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncEvents(null!, default));
            return Results.Ok(new { message = "✅ Olaylar başlatıldı.", jobId });
        });

        group.MapPost("/sync-messages", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncMessages(null!, default));
            return Results.Ok(new { message = "✅ Mesajlar başlatıldı.", jobId });
        });

        group.MapPost("/sync-pipelines", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncPipelines(null!, default));
            return Results.Ok(new { message = "✅ Pipelines başlatıldı.", jobId });
        });

        group.MapPost("/sync-task-types", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncTaskTypes(null!, default));
            return Results.Ok(new { message = "✅ Görev Tipleri başlatıldı.", jobId });
        });

        group.MapPost("/sync-users", (IBackgroundJobClient client) =>
        {
            var jobId = client.Enqueue<CrmJobs>(j => j.SyncUsers(null!, default));
            return Results.Ok(new { message = "✅ Kullanıcılar başlatıldı.", jobId });
        });
    }
}
