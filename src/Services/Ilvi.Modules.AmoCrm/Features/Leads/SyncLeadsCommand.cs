using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Core.Utils;
using Ilvi.Modules.AmoCrm.Abstractions;
 
using Ilvi.Modules.AmoCrm.Domain.Leads;
using Ilvi.Modules.AmoCrm.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.Leads;

public record SyncLeadsCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
    public bool IsFullSync { get; set; } = false;
}

public class SyncLeadsCommandHandler : IRequestHandler<SyncLeadsCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    // LeadId kullanıyorsan repo tanımın böyle kalabilir
    private readonly IAmoRepository<Lead, LeadId> _repository; 
    private readonly ILogger<SyncLeadsCommandHandler> _logger;

    public SyncLeadsCommandHandler(
        IAmoCrmService apiService, 
        IAmoRepository<Lead, LeadId> repository, 
        ILogger<SyncLeadsCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncLeadsCommand request, CancellationToken ct)
    {
        string mode = request.IsFullSync ? "FULL SYNC" : "INCREMENTAL";
        request.Context?.WriteLine($"🚀 Fırsat (Lead) Eşitleme Başladı! Mod: {mode}");

        // 1. URL ve Filtre
        string endpointUrl = "leads";

        if (!request.IsFullSync)
        {
            // DİKKAT: Repository artık SourceUpdatedAtUtc kolonuna bakarak tarih getirmeli.
            // Eğer repository metodun hala eski ise orayı kontrol etmelisin.
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
        endpointUrl += $"{separator}with=contacts,companies,tags";
        
        request.Context?.WriteLine($"📡 URL: {endpointUrl}");

        var buffer = new List<Lead>();
        const int BufferSize = 250;
        int totalProcessed = 0;

        // 2. Veri Çekme
        // Not: ID tipin long ise burada <long> kalmalı.
        await foreach (var (id, json) in _apiService.GetRawDataStreamAsync<long>(endpointUrl, "leads", ct))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var newHash = HashGenerator.ComputeSha256(json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Temel Alanlar
                string name = root.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";
                int price = root.TryGetProperty("price", out var pPrice) ? pPrice.GetInt32() : 0;
                int statusId = root.TryGetProperty("status_id", out var pStat) ? pStat.GetInt32() : 0;
                int pipelineId = root.TryGetProperty("pipeline_id", out var pPipe) ? pPipe.GetInt32() : 0;
                long respUserId = root.TryGetProperty("responsible_user_id", out var pUser) ? pUser.GetInt64() : 0;
                long accountId = root.TryGetProperty("account_id", out var pAcc) ? pAcc.GetInt64() : 0;
                int? lossReasonId = root.TryGetProperty("loss_reason_id", out var pLoss) && pLoss.ValueKind == JsonValueKind.Number ? pLoss.GetInt32() : null;

                // --- TARİHLERİ OKUMA (YENİ EKLENEN KISIM) ---
                
                // 1. Updated At (Kaynaktaki güncelleme)
                long updatedAtUnix = root.TryGetProperty("updated_at", out var pUpd) ? pUpd.GetInt64() : 0;
                var updatedAt = updatedAtUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(updatedAtUnix).UtcDateTime 
                    : DateTime.UtcNow;

                // 2. Created At (Kaynaktaki oluşturulma - YENİ GEREKSİNİM)
                // BaseEntity'deki CreatedAtUtc'yi doldurmak için bunu okumalıyız.
                long createdAtUnix = root.TryGetProperty("created_at", out var pCre) ? pCre.GetInt64() : 0;
                var createdAt = createdAtUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(createdAtUnix).UtcDateTime 
                    : DateTime.UtcNow;


                // JSON Alanları (Embedded)
                string? contactsJson = null;
                string? tagsJson = null;
                string? companiesJson = null;

                if (root.TryGetProperty("_embedded", out var embedded))
                {
                    if (embedded.TryGetProperty("contacts", out var cArr)) contactsJson = cArr.GetRawText();
                    if (embedded.TryGetProperty("tags", out var tArr)) tagsJson = tArr.GetRawText();
                    if (embedded.TryGetProperty("companies", out var compArr)) companiesJson = compArr.GetRawText();
                }

                // --- ENTITY OLUŞTURMA (DÜZELTİLEN KISIM) ---
                var lead = new Lead(
                    LeadId.From(id),
                    UserId.From(respUserId),
                    accountId
                )
                {
                    Name = name,
                    Price = price,
                    StatusId = statusId,
                    PipelineId = pipelineId,
                    LossReasonId = lossReasonId,
                    
                    Contact = contactsJson, 
                    Company = companiesJson,
                    Tag = tagsJson,
                    Raw = json,

                    ComputedHash = newHash,

                    // --- İŞTE DEĞİŞEN İSİMLER ---
                    
                    // Eski: UpdatedAt -> Yeni: SourceUpdatedAtUtc
                    SourceUpdatedAtUtc = updatedAt, 

                    // Eski: LastSyncDate -> Yeni: CheckedAtUtc
                    CheckedAtUtc = DateTime.UtcNow,

                    // Eski: Yoktu -> Yeni: CreatedAtUtc (BaseEntity istiyor)
                    CreatedAtUtc = createdAt 
                };

                buffer.Add(lead);

                if (totalProcessed % 50 == 0)
                    request.Context?.WriteLine($"🔄 Okunuyor... Son ID: {id} | Top: {totalProcessed + buffer.Count}");

                if (buffer.Count >= BufferSize)
                {
                    await ProcessBatchAsync(buffer, ct);
                    totalProcessed += buffer.Count;
                    request.Context?.SetTextColor(ConsoleTextColor.Green);
                    request.Context?.WriteLine($"✅ {buffer.Count} Lead kaydedildi. (Top: {totalProcessed})");
                    request.Context?.ResetTextColor();
                    buffer.Clear();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TaskCanceledException)
            {
                _logger.LogError(ex, "Lead ID: {Id} Hatası", id);
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
            request.Context?.WriteLine($"✅ Kalan {buffer.Count} Lead kaydedildi.");
            request.Context?.ResetTextColor();
        }

        request.Context?.WriteLine($"🏁 Lead Eşitleme Bitti. Toplam: {totalProcessed}");
        return true;
    }

    private async Task ProcessBatchAsync(List<Lead> leads, CancellationToken ct)
    {
        var ids = leads.Select(l => l.Id).ToList();
        var existingHashes = await _repository.GetHashesAsync(ids, ct);
        var listToUpsert = new List<Lead>();

        foreach (var lead in leads)
        {
            if (existingHashes.TryGetValue(lead.Id, out var currentHash) && currentHash == lead.ComputedHash)
                continue;
            listToUpsert.Add(lead);
        }

        if (listToUpsert.Any())
        {
            await _repository.BulkUpsertAsync(listToUpsert, 250, ct);
        }
    }
}