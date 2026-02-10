using System.Runtime.CompilerServices;
using System.Text.Json;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Models;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Services;

public class AmoCrmService : IAmoCrmService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AmoCrmService> _logger;

    public AmoCrmService(
        HttpClient httpClient, 
        ISettingsService settingsService,
        ILogger<AmoCrmService> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async IAsyncEnumerable<(TId Id, string Json)> GetRawDataStreamAsync<TId>(
        string endpoint, 
        string rootProperty, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Her seferinde güncel ayarları al (cache'li)
        var options = await _settingsService.GetAmoCrmOptionsAsync(ct);
        
        int page = 1;
        bool hasMore = true;
        
        string separator = endpoint.Contains("?") ? "&" : "?";

        while (hasMore && !ct.IsCancellationRequested)
        {
            var url = $"{endpoint}{separator}limit={options.PageSize}&page={page}";
            
            _logger.LogInformation("Fetching AmoCRM Data: {Url}", url);
            
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                hasMore = false;
                break;
            }

            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(jsonString);

            if (doc.RootElement.TryGetProperty("_embedded", out var embedded) &&
                embedded.TryGetProperty(rootProperty, out var itemsArray))
            {
                var count = itemsArray.GetArrayLength();
                if (count == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var item in itemsArray.EnumerateArray())
                {
                    TId idValue = default!;

                    if (item.TryGetProperty("id", out var idProp))
                    {
                        if (typeof(TId) == typeof(string))
                        {
                            string sVal = idProp.ValueKind == JsonValueKind.Number 
                                ? idProp.ToString() 
                                : (idProp.GetString() ?? "");
                                
                            idValue = (TId)(object)sVal;
                        }
                        else 
                        {
                            long val = 0;
                            if (idProp.ValueKind == JsonValueKind.Number)
                            {
                                if (idProp.TryGetInt64(out long lVal)) val = lVal;
                            }
                            else if (idProp.ValueKind == JsonValueKind.String)
                            {
                                long.TryParse(idProp.GetString(), out val);
                            }

                            if (typeof(TId) == typeof(int))
                                idValue = (TId)(object)(int)val;
                            else
                                idValue = (TId)(object)val;
                        }
                    }
                    
                    string rawJson = item.GetRawText();
                    yield return (idValue, rawJson);
                }

                // Rate limit için bekleme
                await Task.Delay(options.RequestDelayMs, ct);
                
                page++;
            }
            else
            {
                hasMore = false;
            }
        }
    }

    public async IAsyncEnumerable<AmoContactDto> GetContactsStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (id, json) in GetRawDataStreamAsync<string>("contacts", "contacts", ct))
        {
            var dto = JsonSerializer.Deserialize<AmoContactDto>(json);
            if (dto != null) yield return dto;
        }
    }

    public async IAsyncEnumerable<AmoLeadDto> GetLeadsStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var (id, json) in GetRawDataStreamAsync<string>("leads", "leads", ct))
        {
            var dto = JsonSerializer.Deserialize<AmoLeadDto>(json);
            if (dto != null) yield return dto;
        }
    }

    public async Task<string> GetRawJsonAsync(string endpoint, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}