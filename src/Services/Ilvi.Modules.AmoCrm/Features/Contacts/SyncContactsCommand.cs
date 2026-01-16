using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Core.Utils;
using Ilvi.Modules.AmoCrm.Abstractions;
 using Ilvi.Modules.AmoCrm.Domain.Contacts;
using Ilvi.Modules.AmoCrm.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.Contacts;

public record SyncContactsCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
    public bool IsFullSync { get; set; } = false;
}

public class SyncContactsCommandHandler : IRequestHandler<SyncContactsCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<Contact, ContactId> _repository;
    private readonly ILogger<SyncContactsCommandHandler> _logger;

    public SyncContactsCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<Contact, ContactId> repository,
        ILogger<SyncContactsCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncContactsCommand request, CancellationToken ct)
    {
        string mode = request.IsFullSync ? "FULL SYNC" : "INCREMENTAL";
        request.Context?.WriteLine($"🚀 Kişi Eşitleme Başladı! Mod: {mode}");
        
        // 1. URL Hazırlığı
        string endpointUrl = "contacts";

        if (!request.IsFullSync)
        {
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

        // --- DÜZELTME BURADA: 'with' parametresi EKLENDİ ---
        // Eğer URL'de '?' varsa '&' ile, yoksa '?' ile ekle
        string separator = endpointUrl.Contains("?") ? "&" : "?";
        endpointUrl += $"{separator}with=leads,companies,tags";
        // ----------------------------------------------------

        request.Context?.WriteLine($"📡 URL: {endpointUrl} (Leads, Tags istendi)");
        
        var buffer = new List<Contact>();
        const int BufferSize = 250; 
        int totalProcessed = 0;

        await foreach (var (id, json) in _apiService.GetRawDataStreamAsync<long>(endpointUrl, "contacts", ct))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var newHash = HashGenerator.ComputeSha256(json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string name = root.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";
                long respUserId = root.TryGetProperty("responsible_user_id", out var pUser) ? pUser.GetInt64() : 0;
                long accountId = root.TryGetProperty("account_id", out var pAcc) ? pAcc.GetInt64() : 0;
                
                long updatedAtUnix = root.TryGetProperty("updated_at", out var pUpd) ? pUpd.GetInt64() : 0;
                var updatedAt = updatedAtUnix > 0 
                    ? DateTimeOffset.FromUnixTimeSeconds(updatedAtUnix).UtcDateTime 
                    : DateTime.UtcNow;

                // JSON Alanları (Embedded)
                string? leadsJson = null;
                string? tagsJson = null;
                string? companiesJson = null;

                if (root.TryGetProperty("_embedded", out var embedded))
                {
                    if (embedded.TryGetProperty("leads", out var leadsArray))
                        leadsJson = leadsArray.GetRawText(); // Olduğu gibi JSON string al

                    if (embedded.TryGetProperty("tags", out var tagsArray))
                        tagsJson = tagsArray.GetRawText();

                    if (embedded.TryGetProperty("companies", out var compArray))
                        companiesJson = compArray.GetRawText();
                }

                var contact = new Contact(
                    ContactId.From(id),
                    UserId.From(respUserId),
                    accountId
                )
                {
                    Name = name,
                    // Şemaya uygun isimler
                    Lead = leadsJson,
                    Company = companiesJson,
                    Tag = tagsJson,
                    Raw = json, // RawJson -> Raw
                    ComputedHash = newHash,
                    SourceUpdatedAtUtc = updatedAt,
          
                    CheckedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow, // Veya DateTime.UtcNow
                 
                };

                buffer.Add(contact);

                if (totalProcessed % 50 == 0)
                {
                    request.Context?.WriteLine($"🔄 Okunuyor... Son ID: {id} | Toplam: {totalProcessed + buffer.Count}");
                }

                if (buffer.Count >= BufferSize)
                {
                    await ProcessBatchAsync(buffer, ct);
                    totalProcessed += buffer.Count;
                    request.Context?.WriteLine($"✅ {buffer.Count} kayıt kaydedildi. (Top: {totalProcessed})");
                    buffer.Clear();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TaskCanceledException)
            {
                _logger.LogError(ex, "Hata ID: {Id}", id);
            }
        }

        if (buffer.Any() && !ct.IsCancellationRequested)
        {
            await ProcessBatchAsync(buffer, ct);
            totalProcessed += buffer.Count;
            request.Context?.WriteLine($"✅ Kalan {buffer.Count} kayıt kaydedildi.");
        }
        
        request.Context?.WriteLine($"🏁 Bitti. Toplam: {totalProcessed}");
        return true;
    }

    private async Task ProcessBatchAsync(List<Contact> contacts, CancellationToken ct)
    {
        var ids = contacts.Select(c => c.Id).ToList();
        var existingHashes = await _repository.GetHashesAsync(ids, ct);
        var listToUpsert = new List<Contact>();

        foreach (var contact in contacts)
        {
            if (existingHashes.TryGetValue(contact.Id, out var currentHash) && currentHash == contact.ComputedHash)
                continue; 
            listToUpsert.Add(contact);
        }

        if (listToUpsert.Any())
        {
            await _repository.BulkUpsertAsync(listToUpsert, 250, ct);
        }
    }
}