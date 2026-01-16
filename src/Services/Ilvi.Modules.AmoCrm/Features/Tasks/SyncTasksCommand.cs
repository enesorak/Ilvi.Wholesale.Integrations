using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Core.Utils; // HashGenerator burada
using Ilvi.Modules.AmoCrm.Abstractions;
 
using Ilvi.Modules.AmoCrm.Domain.Tasks; // AmoTask Entity
using Ilvi.Modules.AmoCrm.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.Tasks;

public record SyncTasksCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
    public bool IsFullSync { get; set; } = false;
}

public class SyncTasksCommandHandler : IRequestHandler<SyncTasksCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<AmoTask, TaskId> _repository;
    private readonly ILogger<SyncTasksCommandHandler> _logger;

    public SyncTasksCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<AmoTask, TaskId> repository,
        ILogger<SyncTasksCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncTasksCommand request, CancellationToken ct)
    {
        string mode = request.IsFullSync ? "FULL SYNC" : "INCREMENTAL";
        request.Context?.WriteLine($"🚀 AmoTask Eşitleme Başladı! Mod: {mode}");

        string endpointUrl = "tasks";

        if (!request.IsFullSync)
        {
            // Repository artık SourceUpdatedAtUtc kolonuna bakarak tarihi getirir.
            var lastUpdateDate = await _repository.GetLastUpdateDateAsync(ct);
            if (lastUpdateDate.HasValue)
            {
                var since = lastUpdateDate.Value.AddMinutes(-5);
                var unixTimestamp = ((DateTimeOffset)since).ToUnixTimeSeconds();
                endpointUrl += $"?filter[updated_at][from]={unixTimestamp}";
                request.Context?.WriteLine($"📅 Son Güncelleme: {since}");
            }
            else
            {
                request.Context?.WriteLine("ℹ️ Veritabanı boş, Full Sync yapılıyor.");
            }
        }
        else
        {
            request.Context?.WriteLine("🌕 Gece Modu: Full Sync.");
        }

        string separator = endpointUrl.Contains("?") ? "&" : "?";
        endpointUrl += $"{separator}with=leads,companies,contacts";
        
        request.Context?.WriteLine($"📡 URL: {endpointUrl}");

        var buffer = new List<AmoTask>();
        const int BufferSize = 250;
        int totalProcessed = 0;

        await foreach (var (id, json) in _apiService.GetRawDataStreamAsync<long>(endpointUrl, "tasks", ct))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var newHash = HashGenerator.ComputeSha256(json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // --- TEMEL ALANLAR ---
                string text = root.TryGetProperty("text", out var pText) ? pText.GetString() ?? "" : "";
                long respUserId = root.TryGetProperty("responsible_user_id", out var pUser) ? pUser.GetInt64() : 0;
                long accountId = root.TryGetProperty("account_id", out var pAcc) ? pAcc.GetInt64() : 0;
                int taskTypeId = root.TryGetProperty("task_type_id", out var pType) ? pType.GetInt32() : 0;
                bool isCompleted = root.TryGetProperty("is_completed", out var pComp) && pComp.GetBoolean();
                
                long completeTillUnix = root.TryGetProperty("complete_till", out var pTill) ? pTill.GetInt64() : 0;
                DateTime? completeTill = completeTillUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(completeTillUnix).UtcDateTime 
                    : null;

                string? resultText = null;
                if (root.TryGetProperty("result", out var resObj) && resObj.ValueKind == JsonValueKind.Object)
                {
                    if (resObj.TryGetProperty("text", out var resTxt))
                        resultText = resTxt.GetString();
                }

                // --- TARİHLERİ OKUMA (YENİ STANDART) ---

                // 1. Updated At (Kaynaktaki güncelleme)
                long updatedAtUnix = root.TryGetProperty("updated_at", out var pUpd) ? pUpd.GetInt64() : 0;
                var updatedAt = updatedAtUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(updatedAtUnix).UtcDateTime 
                    : DateTime.UtcNow;

                // 2. Created At (Kaynaktaki oluşturulma - YENİ EKLENDİ)
                long createdAtUnix = root.TryGetProperty("created_at", out var pCre) ? pCre.GetInt64() : 0;
                var createdAt = createdAtUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(createdAtUnix).UtcDateTime 
                    : DateTime.UtcNow;

                // JSON Alanları
                string? leadsJson = null;
                string? companiesJson = null;
                string? contactsJson = null;

                if (root.TryGetProperty("_embedded", out var embedded))
                {
                    if (embedded.TryGetProperty("leads", out var lArr)) leadsJson = lArr.GetRawText();
                    if (embedded.TryGetProperty("companies", out var cArr)) companiesJson = cArr.GetRawText();
                    if (embedded.TryGetProperty("contacts", out var conArr)) contactsJson = conArr.GetRawText();
                }

                // --- ENTITY OLUŞTURMA ---
                var task = new AmoTask(
                    TaskId.From(id),
                    UserId.From(respUserId),
                    accountId
                )
                {
                    Text = text,
                    TaskTypeId = taskTypeId,
                    IsCompleted = isCompleted,
                    CompleteTill = completeTill,
                    ResultText = resultText,

                    Lead = leadsJson,
                    Company = companiesJson,
                    Contact = contactsJson,
                    
                    // BaseEntity Alanları
                    Raw = json,
                    ComputedHash = newHash,

                    // --- İSİM DEĞİŞİKLİKLERİ ---
                    
                    // Eski: UpdatedAt -> Yeni: SourceUpdatedAtUtc
                    SourceUpdatedAtUtc = updatedAt, 

                    // Eski: LastSyncDate -> Yeni: CheckedAtUtc
                    CheckedAtUtc = DateTime.UtcNow,

                    // Eski: Yoktu -> Yeni: CreatedAtUtc (BaseEntity Zorunlu Kılıyor)
                    CreatedAtUtc = createdAt,
                    
                    // Audit (Opsiyonel ama iyi pratik)
                    UpdatedAtUtc = DateTime.UtcNow
                };

                buffer.Add(task);

                if (totalProcessed % 50 == 0)
                    request.Context?.WriteLine($"🔄 Okunuyor... Son ID: {id} | Top: {totalProcessed + buffer.Count}");

                if (buffer.Count >= BufferSize)
                {
                    await ProcessBatchAsync(buffer, ct);
                    totalProcessed += buffer.Count;
                    request.Context?.SetTextColor(ConsoleTextColor.Green);
                    request.Context?.WriteLine($"✅ {buffer.Count} AmoTask kaydedildi. (Top: {totalProcessed})");
                    request.Context?.ResetTextColor();
                    buffer.Clear();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TaskCanceledException)
            {
                _logger.LogError(ex, "AmoTask ID: {Id} Hatası", id);
                request.Context?.SetTextColor(ConsoleTextColor.Red);
                request.Context?.WriteLine($"❌ Hata (ID: {id}): {ex.Message}");
                request.Context?.ResetTextColor();
            }
        }

        if (buffer.Any() && !ct.IsCancellationRequested)
        {
            await ProcessBatchAsync(buffer, ct);
            totalProcessed += buffer.Count;
            request.Context?.SetTextColor(ConsoleTextColor.Green);
            request.Context?.WriteLine($"✅ Kalan {buffer.Count} AmoTask kaydedildi.");
            request.Context?.ResetTextColor();
        }

        request.Context?.WriteLine($"🏁 AmoTask Eşitleme Bitti. Toplam: {totalProcessed}");
        return true;
    }

    private async Task ProcessBatchAsync(List<AmoTask> tasks, CancellationToken ct)
    {
        var ids = tasks.Select(t => t.Id).ToList();
        var existingHashes = await _repository.GetHashesAsync(ids, ct);
        var listToUpsert = new List<AmoTask>();

        foreach (var task in tasks)
        {
            if (existingHashes.TryGetValue(task.Id, out var currentHash) && currentHash == task.ComputedHash)
                continue;
            listToUpsert.Add(task);
        }

        if (listToUpsert.Any())
        {
            await _repository.BulkUpsertAsync(listToUpsert, 250, ct);
        }
    }
}