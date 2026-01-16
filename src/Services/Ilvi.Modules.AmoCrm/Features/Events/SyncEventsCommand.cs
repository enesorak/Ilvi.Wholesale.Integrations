using System.Security.Cryptography; // Hash için
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Events; // AmoEvent burada
using Ilvi.Modules.AmoCrm.Domain.Users;
using Ilvi.Modules.AmoCrm.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ilvi.Modules.AmoCrm.Features.Events;

public record SyncEventsCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
}

public class SyncEventsCommandHandler : IRequestHandler<SyncEventsCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<AmoEvent, string> _repository;
    private readonly ILogger<SyncEventsCommandHandler> _logger;
    private readonly AmoCrmSyncSettings _settings;

    public SyncEventsCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<AmoEvent, string> repository,
        ILogger<SyncEventsCommandHandler> logger,
        IOptions<AmoCrmSyncSettings> settings)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<bool> Handle(SyncEventsCommand request, CancellationToken ct)
    {
        Log(request, "🚀 Olay Günlüğü (Events) Eşitleme Başladı...");

        // 1. Son Olayın Tarihini Bul
        // Repository "EventAtUtc" kolonuna bakar ama C#'da "CreatedAtUtc" üzerinden sorgular.
        var lastEventDate = await _repository.GetLastCreatedDateAsync(ct);
        long timestamp = 0;
        
        if (lastEventDate.HasValue)
        {
            // 1 saniye ekle ki aynı olayı tekrar çekmeyelim
            var startFrom = lastEventDate.Value.AddSeconds(1);
            timestamp = ((DateTimeOffset)startFrom).ToUnixTimeSeconds();
            
            Log(request, $"📅 En son kayıt: {lastEventDate.Value}. Kaldığı yerden devam ediliyor (Unix: {timestamp})...");
        }
        else
        {
             // Veritabanı boşsa ayarlardan kaç ay geriye gideceğini oku
             int monthsBack = _settings.EventsLookBackMonths;
             if (monthsBack == 0) monthsBack = 1; // Güvenlik

             timestamp = ((DateTimeOffset)DateTime.UtcNow.AddMonths(-monthsBack)).ToUnixTimeSeconds();
             
             Log(request, $"ℹ️ Veritabanı boş. Ayarlara göre son {monthsBack} aydan başlanıyor (Unix: {timestamp})...");
        }

        // 2. Filtre Listesi
        string eventTypes = "lead_added,lead_deleted,lead_restored,lead_status_changed,lead_linked,lead_unlinked," +
                            "contact_added,contact_deleted,contact_restored,contact_linked,contact_unlinked," +
                            "company_added,company_deleted,company_restored,company_linked,company_unlinked," +
                            "task_added,task_deleted,task_completed,task_type_changed,task_text_changed,task_deadline_changed," +
                            "entity_tag_added,entity_tag_deleted,entity_linked,entity_unlinked,entity_merged,sale_field_changed," +
                            "common_note_added,common_note_deleted";

        string endpointUrl = $"events?filter[created_at][from]={timestamp}&filter[type]={eventTypes}&order[created_at]=asc";
        
        Log(request, $"📡 İstek URL: {endpointUrl}");

        var buffer = new List<AmoEvent>();
        const int BufferSize = 500;
        int totalProcessed = 0;

        await foreach (var (idRaw, json) in _apiService.GetRawDataStreamAsync<string>(endpointUrl, "events", ct))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                string id = root.GetProperty("id").ToString();
                string type = root.TryGetProperty("type", out var pType) ? pType.GetString() ?? "" : "";
                long entityId = root.TryGetProperty("entity_id", out var pEntId) ? pEntId.GetInt64() : 0;
                string entityType = root.TryGetProperty("entity_type", out var pEntType) ? pEntType.GetString() ?? "" : "";
                long createdBy = root.TryGetProperty("created_by", out var pBy) ? pBy.GetInt64() : 0;
                
                // Olay Tarihi (Unix -> DateTime)
                long createdAtUnix = root.TryGetProperty("created_at", out var pAt) ? pAt.GetInt64() : 0;
                var createdAt = DateTimeOffset.FromUnixTimeSeconds(createdAtUnix).UtcDateTime;

                // Değerler
                string? valAfter = null;
                string? valBefore = null;

                if (root.TryGetProperty("value_after", out var va)) valAfter = va.GetRawText();
                if (root.TryGetProperty("value_before", out var vb)) valBefore = vb.GetRawText();

                // Entity Oluşturma (YENİ MİMARİ)
                var amoEvent = new AmoEvent(id)
                {
                    // 1. Modüle Özel Alanlar
                    Type = type,       // Type -> EventType
                    EntityId = entityId,
                    EntityType = entityType,
                    CreatedBy = UserId.From(createdBy),
                    ValueAfter = valAfter,
                    ValueBefore = valBefore,

                    // 2. BaseEntity Alanları
                    Raw = json,
                    ComputedHash = ComputeSha256Hash(json), // Hash zorunlu

                    // 3. Tarih Alanları (İsimlendirmeler Düzeltildi)
                    
                    // Code: CreatedAtUtc -> SQL: EventAtUtc (Configuration sayesinde)
                    EventAtUtc = createdAt, 
                    CreatedAtUtc = DateTime.UtcNow,
                    // Code: CheckedAtUtc -> SQL: CheckedAtUtc
                    CheckedAtUtc = DateTime.UtcNow, 
                    
                    // Olaylarda güncelleme olmaz ama şema gereği null veya createdAt
                    SourceUpdatedAtUtc = createdAt, 
                    
                    // DB'ye kayıt anı (Audit)
                    UpdatedAtUtc = DateTime.UtcNow
                };

                buffer.Add(amoEvent);

                if (buffer.Count >= BufferSize)
                {
                    var uniqueBuffer = buffer
                        .GroupBy(x => x.Id)
                        .Select(g => g.Last()) // Sonuncuyu al (en güncel)
                        .ToList();
                    await _repository.BulkUpsertAsync(uniqueBuffer, BufferSize, ct);
                   
                    totalProcessed += buffer.Count;
                    Log(request, $"✅ {totalProcessed} olay işlendi...");
                    buffer.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event Parse Error. Raw: {Json}", json);
            }
        }

        if (buffer.Any())
        {
            await _repository.BulkUpsertAsync(buffer, BufferSize, ct);
            totalProcessed += buffer.Count;
        }

        Log(request, $"🏁 Toplam {totalProcessed} olay kaydedildi.");
        return true;
    }

    private void Log(SyncEventsCommand request, string message)
    {
        _logger.LogInformation("{Message}", message);
        request.Context?.WriteLine(message);
    }
    
    // Basit Hash Hesaplama
    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}