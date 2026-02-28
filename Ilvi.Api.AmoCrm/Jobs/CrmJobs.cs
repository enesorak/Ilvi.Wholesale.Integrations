using Hangfire;
using Hangfire.Server;
using Ilvi.Modules.AmoCrm.Features.Contacts;
using Ilvi.Modules.AmoCrm.Features.Events;
using Ilvi.Modules.AmoCrm.Features.Leads;
using Ilvi.Modules.AmoCrm.Features.Messages;
using Ilvi.Modules.AmoCrm.Features.Pipelines;
using Ilvi.Modules.AmoCrm.Features.Tasks;
using Ilvi.Modules.AmoCrm.Features.TaskTypes;
using Ilvi.Modules.AmoCrm.Features.Users;
using MediatR;

namespace Ilvi.Api.AmoCrm.Jobs;

public class CrmJobs
{
    private readonly IMediator _mediator;

    public CrmJobs(IMediator mediator)
    {
        _mediator = mediator;
    }

    // --- CONTACTS ---
    [JobDisplayName("👥 Kişiler (Incremental)")]
    public async Task SyncContactsIncremental(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncContactsCommand { Context = context, IsFullSync = false }, ct);

    [JobDisplayName("🌕 Kişiler (Full Sync)")]
    public async Task SyncContactsFull(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncContactsCommand { Context = context, IsFullSync = true }, ct);

    // --- LEADS ---
    [JobDisplayName("💼 Fırsatlar (Incremental)")]
    public async Task SyncLeadsIncremental(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncLeadsCommand { Context = context, IsFullSync = false }, ct);

    [JobDisplayName("🌕 Fırsatlar (Full Sync)")]
    public async Task SyncLeadsFull(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncLeadsCommand { Context = context, IsFullSync = true }, ct);

    // --- TASKS ---
    [JobDisplayName("📅 Görevler (Incremental)")]
    public async Task SyncTasksIncremental(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncTasksCommand { Context = context, IsFullSync = false }, ct);

    [JobDisplayName("🌕 Görevler (Full Sync)")]
    public async Task SyncTasksFull(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncTasksCommand { Context = context, IsFullSync = true }, ct);

    // --- EVENTS ---
    [JobDisplayName("📜 Olaylar (Events)")]
    public async Task SyncEvents(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncEventsCommand { Context = context }, ct);

    // --- MESSAGES ---
    [JobDisplayName("💬 Mesajlar (Chat)")]
    public async Task SyncMessages(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncMessagesCommand { Context = context }, ct);

    // --- PIPELINES ---
    [JobDisplayName("📊 Satış Boru Hatları (Pipelines)")]
    public async Task SyncPipelines(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncPipelinesCommand { Context = context }, ct);

    // --- TASK TYPES ---
    [JobDisplayName("📝 Görev Tipleri (Task Types)")]
    public async Task SyncTaskTypes(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncTaskTypesCommand { Context = context }, ct);

    // --- USERS ---
    [JobDisplayName("👤 Kullanıcılar (Users)")]
    public async Task SyncUsers(PerformContext context, CancellationToken ct)
        => await _mediator.Send(new SyncUsersCommand { Context = context }, ct);
}
