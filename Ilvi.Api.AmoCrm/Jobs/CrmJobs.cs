using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Api.AmoCrm.Services;
using Ilvi.Modules.AmoCrm.Features.Contacts;
using Ilvi.Modules.AmoCrm.Features.Events;
using Ilvi.Modules.AmoCrm.Features.Leads;
using Ilvi.Modules.AmoCrm.Features.Messages;
using Ilvi.Modules.AmoCrm.Features.Pipelines;
using Ilvi.Modules.AmoCrm.Features.Tasks;
using Ilvi.Modules.AmoCrm.Features.TaskTypes;
using Ilvi.Modules.AmoCrm.Features.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Api.AmoCrm.Jobs;

public class CrmJobs
{
    private readonly IMediator _mediator;
    private readonly ITelegramNotificationService _telegram;
    private readonly ILogger<CrmJobs> _logger;

    private static readonly HashSet<string> _runningJobs = new();
    private static readonly object _lock = new();

    public CrmJobs(
        IMediator mediator,
        ITelegramNotificationService telegram,
        ILogger<CrmJobs> logger)
    {
        _mediator = mediator;
        _telegram = telegram;
        _logger = logger;
    }

    // --- CONTACTS ---
    [JobDisplayName("👥 Kişiler (Incremental)")]
   
    public async Task SyncContactsIncremental(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-contacts", "👥 Kişiler (Incremental)", context, ct,
            () => _mediator.Send(new SyncContactsCommand { Context = context, IsFullSync = false }, ct));

    [JobDisplayName("🌕 Kişiler (Full Sync)")]
   
    public async Task SyncContactsFull(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-contacts", "🌕 Kişiler (Full)", context, ct,
            () => _mediator.Send(new SyncContactsCommand { Context = context, IsFullSync = true }, ct));

    // --- LEADS ---
    [JobDisplayName("💼 Fırsatlar (Incremental)")]
   
    public async Task SyncLeadsIncremental(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-leads", "💼 Fırsatlar (Incremental)", context, ct,
            () => _mediator.Send(new SyncLeadsCommand { Context = context, IsFullSync = false }, ct));

    [JobDisplayName("🌕 Fırsatlar (Full Sync)")]
   
    public async Task SyncLeadsFull(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-leads", "🌕 Fırsatlar (Full)", context, ct,
            () => _mediator.Send(new SyncLeadsCommand { Context = context, IsFullSync = true }, ct));

    // --- TASKS ---
    [JobDisplayName("📅 Görevler (Incremental)")]
   
    public async Task SyncTasksIncremental(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-tasks", "📅 Görevler (Incremental)", context, ct,
            () => _mediator.Send(new SyncTasksCommand { Context = context, IsFullSync = false }, ct));

    [JobDisplayName("🌕 Görevler (Full Sync)")]
   
    public async Task SyncTasksFull(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-tasks", "🌕 Görevler (Full)", context, ct,
            () => _mediator.Send(new SyncTasksCommand { Context = context, IsFullSync = true }, ct));

    // --- EVENTS ---
    [JobDisplayName("📜 Olaylar (Events)")]
   
    public async Task SyncEvents(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-events", "📜 Olaylar", context, ct,
            () => _mediator.Send(new SyncEventsCommand { Context = context }, ct));

    // --- MESSAGES ---
    [JobDisplayName("💬 Mesajlar (Chat)")]
   
    public async Task SyncMessages(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-messages", "💬 Mesajlar", context, ct,
            () => _mediator.Send(new SyncMessagesCommand { Context = context }, ct));

    // --- PIPELINES ---
    [JobDisplayName("📊 Satış Boru Hatları (Pipelines)")]
   
    public async Task SyncPipelines(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-pipelines", "📊 Pipelines", context, ct,
            () => _mediator.Send(new SyncPipelinesCommand { Context = context }, ct));

    // --- TASK TYPES ---
    [JobDisplayName("📝 Görev Tipleri (Task Types)")]
   
    public async Task SyncTaskTypes(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-task-types", "📝 Görev Tipleri", context, ct,
            () => _mediator.Send(new SyncTaskTypesCommand { Context = context }, ct));

    // --- USERS ---
    [JobDisplayName("👤 Kullanıcılar (Users)")]
   
    
    
    public async Task SyncUsers(PerformContext context, CancellationToken ct)
        => await RunSafe("sync-users", "👤 Kullanıcılar", context, ct,
            () => _mediator.Send(new SyncUsersCommand { Context = context }, ct));

    // ==========================================================
    // CORE: Skip if running + Finish/Error/Skip notifications
    // ==========================================================
    private async Task RunSafe(string jobKey, string jobName, PerformContext context, CancellationToken ct, Func<Task> action)
    {
        // 1. Skip if already running
        lock (_lock)
        {
            if (_runningJobs.Contains(jobKey))
            {
                var skipMsg = $"⏭️ '{jobName}' zaten çalışıyor, atlanıyor.";
                _logger.LogWarning(skipMsg);
                context?.WriteLine(skipMsg);

                try { _ = _telegram.SendMessageAsync($"⏭️ <b>Atlandı</b>\n📋 {jobName}\n📝 Aynı görev zaten çalışıyor."); }
                catch { }
                return;
            }
            _runningJobs.Add(jobKey);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("▶️ {JobName} başladı.", jobName);
        context?.WriteLine($"▶️ {jobName} başladı.");

        try
        {
            await action();
            sw.Stop();

            var successMsg = $"✅ {jobName} tamamlandı ({FormatDuration(sw.Elapsed)})";
            _logger.LogInformation(successMsg);
            context?.WriteLine(successMsg);

            try
            {
                await _telegram.SendMessageAsync(
                    $"✅ <b>Tamamlandı</b>\n📋 {jobName}\n⏱️ {FormatDuration(sw.Elapsed)}");
            }
            catch { }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            var cancelMsg = $"⚠️ {jobName} iptal edildi ({FormatDuration(sw.Elapsed)})";
            _logger.LogWarning(cancelMsg);
            context?.WriteLine(cancelMsg);

            try { await _telegram.SendMessageAsync($"⚠️ <b>İptal</b>\n📋 {jobName}\n⏱️ {FormatDuration(sw.Elapsed)}"); }
            catch { }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Job hatası: {JobName}", jobName);
            context?.WriteLine($"❌ {jobName} HATA! {ex.Message}");

            try
            {
                await _telegram.SendMessageAsync(
                    $"🚨 <b>HATA</b>\n📋 {jobName}\n⏱️ {FormatDuration(sw.Elapsed)}\n💥 <code>{ex.GetType().Name}</code>\n📝 {Truncate(ex.Message, 200)}");
            }
            catch { }

            throw;
        }
        finally
        {
            lock (_lock) { _runningJobs.Remove(jobKey); }
        }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{ts.TotalSeconds:F1}sn";
        if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}dk {ts.Seconds}sn";
        return $"{(int)ts.TotalHours}sa {ts.Minutes}dk";
    }

    private static string Truncate(string msg, int max)
    {
        if (string.IsNullOrEmpty(msg)) return "";
        return msg.Length <= max ? msg : msg[..max] + "...";
    }
}