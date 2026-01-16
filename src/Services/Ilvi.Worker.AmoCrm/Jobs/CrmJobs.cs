using Hangfire; // DisplayName için
using Hangfire.Server;

using Ilvi.Modules.AmoCrm.Features.Contacts;
using Ilvi.Modules.AmoCrm.Features.Events;
using Ilvi.Modules.AmoCrm.Features.Leads;
using Ilvi.Modules.AmoCrm.Features.Messages;
using Ilvi.Modules.AmoCrm.Features.Pipelines;
using Ilvi.Modules.AmoCrm.Features.Tasks;
using Ilvi.Modules.AmoCrm.Features.TaskTypes;
using MediatR;

namespace Ilvi.Worker.AmoCrm.Jobs;

public class CrmJobs
{
    private readonly IMediator _mediator;

    public CrmJobs(IMediator mediator)
    {
        _mediator = mediator;
    }

    // 1. INCREMENTAL JOB (Hızlı)
    [JobDisplayName("👥 AmoCRM > Kişiler (Sadece Değişenler)")]
    public async Task SyncContactsIncremental(PerformContext context, CancellationToken ct)
    {
        var command = new SyncContactsCommand 
        { 
            Context = context,
            IsFullSync = false // Sadece değişenleri al
        };
        await _mediator.Send(command, ct);
    }

    // 2. FULL SYNC JOB (Gece)
    [JobDisplayName("🌕 AmoCRM > Kişiler (FULL RESET)")]
    public async Task SyncContactsFull(PerformContext context, CancellationToken ct)
    {
        var command = new SyncContactsCommand 
        { 
            Context = context,
            IsFullSync = true // Her şeyi baştan al
        };
        await _mediator.Send(command, ct);
    }
    
    
    [JobDisplayName("💼 AmoCRM > Fırsatlar (Incremental)")]
    public async Task SyncLeadsIncremental(PerformContext context, CancellationToken ct)
    {
        var command = new SyncLeadsCommand { Context = context, IsFullSync = false };
        await _mediator.Send(command, ct);
    }

    [JobDisplayName("🌕 AmoCRM > Fırsatlar (FULL RESET)")]
    public async Task SyncLeadsFull(PerformContext context, CancellationToken ct)
    {
        var command = new SyncLeadsCommand { Context = context, IsFullSync = true };
        await _mediator.Send(command, ct);
    }
    

    
    [JobDisplayName("📅 AmoCRM > Görevler (Incremental)")]
    public async Task SyncTasksIncremental(PerformContext context, CancellationToken ct)
    {
        // IsFullSync = false -> Sadece değişenleri getir
        var command = new SyncTasksCommand { Context = context, IsFullSync = false };
        await _mediator.Send(command, ct);
    }

    [JobDisplayName("🌕 AmoCRM > Görevler (FULL RESET)")]
    public async Task SyncTasksFull(PerformContext context, CancellationToken ct)
    {
        // IsFullSync = true -> Her şeyi baştan çek
        var command = new SyncTasksCommand { Context = context, IsFullSync = true };
        await _mediator.Send(command, ct);
    }
    
    
    

    // --- EVENTS (OLAYLAR) ---
    [JobDisplayName("📜 AmoCRM > Olay Günlüğü (Events)")]
    public async Task SyncEvents(PerformContext context, CancellationToken ct)
    {
        await _mediator.Send(new SyncEventsCommand { Context = context }, ct);
    }

    // --- MESSAGES (MESAJLAR) ---
    [JobDisplayName("💬 AmoCRM > Mesajlar (Chat)")]
    public async Task SyncMessages(PerformContext context, CancellationToken ct)
    {
        await _mediator.Send(new SyncMessagesCommand { Context = context }, ct);
    }
    
    
    [JobDisplayName("📊 AmoCRM > Satış Boru Hatları (Pipelines)")]
    public async Task SyncPipelines(PerformContext context, CancellationToken ct)
    {
        await _mediator.Send(new SyncPipelinesCommand { Context = context }, ct);
    }

    [JobDisplayName("📝 AmoCRM > Görev Tipleri (Task Types)")]
    public async Task SyncTaskTypes(PerformContext context, CancellationToken ct)
    {
        await _mediator.Send(new SyncTaskTypesCommand { Context = context }, ct);
    }
    
    
 
}