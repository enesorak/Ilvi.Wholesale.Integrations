using System.Security.Cryptography; // Hash için
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Pipelines;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.Pipelines;

public record SyncPipelinesCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
}

public class SyncPipelinesCommandHandler : IRequestHandler<SyncPipelinesCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    // Pipeline ID'si int olduğu için Repository<Pipeline, int> kullanıyoruz
    private readonly IAmoRepository<Pipeline, int> _repository; 
    private readonly ILogger<SyncPipelinesCommandHandler> _logger;

    public SyncPipelinesCommandHandler(
        IAmoCrmService apiService, 
        IAmoRepository<Pipeline, int> repository, 
        ILogger<SyncPipelinesCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncPipelinesCommand request, CancellationToken ct)
    {
        request.Context?.WriteLine("🚀 Pipeline (Boru Hattı) Eşitleme Başladı...");
        _logger.LogInformation("Starting Pipeline Synchronization...");

        // Endpoint: api/v4/leads/pipelines
        var jsonResponse = await _apiService.GetRawJsonAsync("leads/pipelines", ct);

        if (string.IsNullOrEmpty(jsonResponse))
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine("❌ API'den boş yanıt döndü.");
            request.Context?.ResetTextColor();
            return false;
        }

        var pipelinesToUpsert = new List<Pipeline>();

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("_embedded", out var embedded) && 
                embedded.TryGetProperty("pipelines", out var pipelinesArray))
            {
                foreach (var item in pipelinesArray.EnumerateArray())
                {
                    // 1. Alanları Parse Et
                    int id = item.GetProperty("id").GetInt32();
                    string name = item.GetProperty("name").GetString() ?? "";
                    int sort = item.GetProperty("sort").GetInt32();
                    bool isMain = item.GetProperty("is_main").GetBoolean();
                    
                    // 2. Statüleri JSON olarak al
                    string? statusesJson = null;
                    if (item.TryGetProperty("_embedded", out var embItem) && 
                        embItem.TryGetProperty("statuses", out var statusArray))
                    {
                        statusesJson = statusArray.GetRawText();
                    }

                    // Raw JSON ve Hash
                    string rawJson = item.GetRawText();
                    string hash = ComputeSha256Hash(rawJson);

                    // 3. Entity Oluştur (YENİ MİMARİ)
                    var pipeline = new Pipeline(id)
                    {
                        Name = name,
                        Sort = sort,
                        IsMain = isMain,
                        Statuses = statusesJson,
                        
                        // BaseEntity Alanları
                        Raw = rawJson,
                        ComputedHash = hash, // Hash zorunlu

                        // --- TARİH ALANLARI ---

                        // Eski: LastSyncDate -> Yeni: CheckedAtUtc
                        CheckedAtUtc = DateTime.UtcNow,

                        // Pipelines genelde created_at dönmez, o yüzden şu anki zamanı veriyoruz.
                        // Eğer BaseEntity zorunlu tutuyorsa boş geçemeyiz.
                        CreatedAtUtc = DateTime.UtcNow,

                        // Audit
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    pipelinesToUpsert.Add(pipeline);
                }
            }

            // 4. Veritabanına Kaydet
            if (pipelinesToUpsert.Any())
            {
                await _repository.BulkUpsertAsync(pipelinesToUpsert, 100, ct);

                request.Context?.SetTextColor(ConsoleTextColor.Green);
                request.Context?.WriteLine($"✅ Toplam {pipelinesToUpsert.Count} adet Pipeline ve Statüleri güncellendi.");
                request.Context?.ResetTextColor();
            }
            else
            {
                request.Context?.WriteLine("ℹ️ Hiç pipeline kaydı bulunamadı.");
            }
        }
        catch (Exception ex)
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine($"❌ Hata: {ex.Message}");
            request.Context?.ResetTextColor();
            _logger.LogError(ex, "Error syncing pipelines");
            throw;
        }

        request.Context?.WriteLine("🏁 Pipeline Eşitleme Tamamlandı.");
        return true;
    }

    // Hash Helper
    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}