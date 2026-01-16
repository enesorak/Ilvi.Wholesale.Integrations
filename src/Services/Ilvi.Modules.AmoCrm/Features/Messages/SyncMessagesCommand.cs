using System.Security.Cryptography; // Hash için
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Messages; // AmoMessage Entity
using Ilvi.Modules.AmoCrm.Domain.Users;
using Ilvi.Modules.AmoCrm.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ilvi.Modules.AmoCrm.Features.Messages;

public record SyncMessagesCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
}

public class SyncMessagesCommandHandler : IRequestHandler<SyncMessagesCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<AmoMessage, string> _repository;
    private readonly ILogger<SyncMessagesCommandHandler> _logger;
    private readonly AmoCrmSyncSettings _settings;

    public SyncMessagesCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<AmoMessage, string> repository,
        ILogger<SyncMessagesCommandHandler> logger, 
        IOptions<AmoCrmSyncSettings> settings)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<bool> Handle(SyncMessagesCommand request, CancellationToken ct)
    {
        Log(request, "🚀 Mesajlar (Chat) Eşitleme Başladı...");

        // 1. Son Mesaj Tarihi
        // Repository "EventAtUtc" kolonuna bakar, C#'da "CreatedAtUtc" üzerinden gelir.
        var lastMsgDate = await _repository.GetLastCreatedDateAsync(ct);
        long timestamp = 0;

        if (lastMsgDate.HasValue)
        {
            var startFrom = lastMsgDate.Value.AddSeconds(1);
            timestamp = ((DateTimeOffset)startFrom).ToUnixTimeSeconds();
            Log(request, $"📅 Son mesaj: {lastMsgDate.Value}. Devam ediliyor (Unix: {timestamp})...");
        }
        else
        {
            // Veritabanı boşsa ayarlardan oku (Varsayılan 12 ay)
            int monthsBack = _settings.MessagesLookBackMonths; 
            if (monthsBack == 0) monthsBack = 12;

            var startDate = DateTime.UtcNow.AddMonths(-monthsBack);
            timestamp = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
        
            Log(request, $"ℹ️ Veritabanı boş. Ayarlara göre son {monthsBack} aydan başlanıyor (Unix: {timestamp})...");
        }

        // 2. Filtre
        string endpointUrl = $"events?filter[created_at][from]={timestamp}&filter[type]=incoming_chat_message,outgoing_chat_message&order[created_at]=asc";
        
        Log(request, $"📡 URL: {endpointUrl}");

        var buffer = new List<AmoMessage>();
        const int BufferSize = 250;
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
                long createdBy = root.TryGetProperty("created_by", out var pBy) ? pBy.GetInt64() : 0;
                
                long createdAtUnix = root.TryGetProperty("created_at", out var pAt) ? pAt.GetInt64() : 0;
                var createdAt = DateTimeOffset.FromUnixTimeSeconds(createdAtUnix).UtcDateTime;

                // Mesaj parsing
                string text = "";
                long chatId = 0; // API'den çekilebiliyorsa buraya eklenmeli

                if (root.TryGetProperty("value_after", out var va))
                {
                    if (va.ValueKind == JsonValueKind.Array) 
                    {
                        foreach (var item in va.EnumerateArray())
                        {
                            if (item.TryGetProperty("message", out var msgObj))
                            {
                                text = msgObj.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                            }
                        }
                    }
                    else if (va.ValueKind == JsonValueKind.Object)
                    {
                         if (va.TryGetProperty("message", out var msgObj))
                         {
                             text = msgObj.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                         }
                    }
                }
                
                // Entity Oluşturma (YENİ MİMARİ)
                var message = new AmoMessage(id)
                {
                    // Mesaj Detayları
                    Type = type,       // Type -> EventType
                    EntityId = entityId,
                    ContactId = entityId,   // Chat mesajları genelde Contact'a bağlıdır
                    ChatId = chatId,
                    AuthorId = UserId.From(createdBy),
                    Text = text,

                    // BaseEntity Alanları
                    Raw = json,
                    ComputedHash = ComputeSha256Hash(json), // Hash hesapla

                    // Tarih Alanları
                    // Code: CreatedAtUtc -> SQL: EventAtUtc (Configuration mapping ile)
                    CreatedAtUtc = createdAt, 
                    
                    // Mesajlar güncellenmez ama null olmasın diye oluşturma tarihini basabiliriz
                    SourceUpdatedAtUtc = createdAt, 

                    // Bizim Sync kontrol tarihimiz
                    CheckedAtUtc = DateTime.UtcNow,
                    
                    // Audit
                    UpdatedAtUtc = DateTime.UtcNow
                };

                buffer.Add(message);

                if (buffer.Count >= BufferSize)
                {
                    await _repository.BulkUpsertAsync(buffer, BufferSize, ct);
                    totalProcessed += buffer.Count;
                    Log(request, $"✅ {totalProcessed} mesaj işlendi.");
                    buffer.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message Parse Error ID: {Id}", idRaw);
            }
        }

        if (buffer.Any())
        {
            await _repository.BulkUpsertAsync(buffer, BufferSize, ct);
            totalProcessed += buffer.Count;
        }

        Log(request, $"🏁 Toplam {totalProcessed} mesaj kaydedildi.");
        return true;
    }

    private void Log(SyncMessagesCommand request, string message)
    {
        _logger.LogInformation("{Message}", message);
        request.Context?.WriteLine(message);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}