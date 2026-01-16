using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Modules.AmoCrm.Abstractions;
 
using Ilvi.Modules.AmoCrm.Domain.TaskTypes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.TaskTypes;

public record SyncTaskTypesCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
}

public class SyncTaskTypesCommandHandler : IRequestHandler<SyncTaskTypesCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<TaskType, int> _repository;
    private readonly ILogger<SyncTaskTypesCommandHandler> _logger;

    public SyncTaskTypesCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<TaskType, int> repository,
        ILogger<SyncTaskTypesCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncTaskTypesCommand request, CancellationToken ct)
    {
        request.Context?.WriteLine("🚀 TaskTypes (Görev Tipleri) Eşitleme Başladı...");
        _logger.LogInformation("Starting TaskTypes Synchronization...");

        // Endpoint: api/v4/account?with=task_types
        var jsonResponse = await _apiService.GetRawJsonAsync("account?with=task_types", ct);

        if (string.IsNullOrEmpty(jsonResponse))
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine("❌ API'den boş yanıt döndü.");
            request.Context?.ResetTextColor();
            return false;
        }

        var listToUpsert = new List<TaskType>();

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            // Veri yolu: root -> _embedded -> task_types
            if (root.TryGetProperty("_embedded", out var embedded) &&
                embedded.TryGetProperty("task_types", out var typesArray))
            {
                foreach (var item in typesArray.EnumerateArray())
                {
                    // --- DÜZELTME BURADA ---
                    // ID Okuma (Güvenli)
                    int id = 0;
                    if (item.TryGetProperty("id", out var pId) && pId.ValueKind == JsonValueKind.Number)
                    {
                        id = pId.GetInt32();
                    }
                    else
                    {
                        // ID yoksa veya null ise bu kaydı atla
                        continue;
                    }

                    string name = item.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";
                    
                    string color = "";
                    if (item.TryGetProperty("color", out var pColor) && pColor.ValueKind == JsonValueKind.String)
                    {
                        color = pColor.GetString() ?? "";
                    }

                    // HATA VEREN KISIM BURASIYDI: icon_id null gelebilir
                    int iconId = 0;
                    if (item.TryGetProperty("icon_id", out var pIcon) && pIcon.ValueKind == JsonValueKind.Number)
                    {
                        iconId = pIcon.GetInt32();
                    }

                    // Entity Oluştur
                    var taskType = new TaskType(id)
                    {
                        Name = name,
                        Color = color,
                        IconId = iconId,
                        Raw = item.GetRawText(),
                    };

                    listToUpsert.Add(taskType);
                }
            }

            // Veritabanına Kaydet
            if (listToUpsert.Any())
            {
                await _repository.BulkUpsertAsync(listToUpsert, 100, ct);

                request.Context?.SetTextColor(ConsoleTextColor.Green);
                request.Context?.WriteLine($"✅ Toplam {listToUpsert.Count} adet Görev Tipi güncellendi.");
                request.Context?.ResetTextColor();
            }
            else
            {
                request.Context?.SetTextColor(ConsoleTextColor.Yellow);
                request.Context?.WriteLine("ℹ️ Hiç görev tipi bulunamadı veya _embedded alanı boş.");
                request.Context?.ResetTextColor();
            }
        }
        catch (Exception ex)
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine($"❌ Hata: {ex.Message}");
            request.Context?.ResetTextColor();
            _logger.LogError(ex, "Error syncing task types");
            // Kritik hata fırlat ki Hangfire retry etsin
            throw;
        }

        request.Context?.WriteLine("🏁 TaskTypes Eşitleme Tamamlandı.");
        return true;
    }
}