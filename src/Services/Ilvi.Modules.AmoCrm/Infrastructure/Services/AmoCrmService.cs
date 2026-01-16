using System.Runtime.CompilerServices;
using System.Text.Json;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Services;


public class AmoCrmService : IAmoCrmService
{
    private readonly HttpClient _httpClient;
    private readonly AmoCrmOptions _options;
    private readonly ILogger<AmoCrmService> _logger;

    public AmoCrmService(HttpClient httpClient, IOptions<AmoCrmOptions> options, ILogger<AmoCrmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    // --- GENERIC RAW DATA FETCHING (EN ÖNEMLİSİ) ---
    // Bu metod: "contacts", "leads", "tasks" fark etmeksizin verilen endpoint'e gider.
    // Tüm sayfaları gezer.
    // Her bir öğenin ID'sini ve HAM JSON halini döner.
// TId: Generic yaptık (long, int veya string olabilir)
    public async IAsyncEnumerable<(TId Id, string Json)> GetRawDataStreamAsync<TId>(
        string endpoint, 
        string rootProperty, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int page = 1;
        bool hasMore = true;
        
        // URL ayırıcı mantığı (Orijinal koddaki gibi)
        string separator = endpoint.Contains("?") ? "&" : "?";

        while (hasMore && !ct.IsCancellationRequested)
        {
            // Orijinal koddaki _options kullanımı korundu
            var url = $"{endpoint}{separator}limit={_options.PageSize}&page={page}";
            
            _logger.LogInformation("Fetching AmoCRM Data: {Url}", url);
            
            // Orijinal HTTP isteği
            using var response = await _httpClient.GetAsync(url, ct);

            // 204 No Content gelirse veri bitmiş demektir.
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                hasMore = false;
                break;
            }

            response.EnsureSuccessStatusCode();

            // Orijinal Parse mantığı
            var jsonString = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(jsonString);

            // _embedded ve rootProperty kontrolü
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
                    // --- KRİTİK GÜNCELLEME BURASI ---
                    // ID Okuma işlemi Generic hale getirildi.
                    // Hem String (Events) hem Number (Contacts) destekler.
                    
                    TId idValue = default!;

                    if (item.TryGetProperty("id", out var idProp))
                    {
                        // 1. Durum: Hedef tip String ise (Events, Messages)
                        if (typeof(TId) == typeof(string))
                        {
                            string sVal = idProp.ValueKind == JsonValueKind.Number 
                                ? idProp.ToString() 
                                : (idProp.GetString() ?? "");
                                
                            idValue = (TId)(object)sVal;
                        }
                        // 2. Durum: Hedef tip Long veya Int ise (Contacts, Leads, Companies)
                        else 
                        {
                            long val = 0;
                            // Number ise direkt al
                            if (idProp.ValueKind == JsonValueKind.Number)
                            {
                                if (idProp.TryGetInt64(out long lVal)) val = lVal;
                            }
                            // String ise parse et (Bazen ID string "123" gelebilir)
                            else if (idProp.ValueKind == JsonValueKind.String)
                            {
                                long.TryParse(idProp.GetString(), out val);
                            }

                            // TId tipine göre cast et
                            if (typeof(TId) == typeof(int))
                                idValue = (TId)(object)(int)val;
                            else
                                idValue = (TId)(object)val; // long varsayıyoruz
                        }
                    }
                    
                    // RAW veri
                    string rawJson = item.GetRawText();

                    yield return (idValue, rawJson);
                }

                // --- ORİJİNAL KODDAKİ Rate Limit KORUNDU ---
                // AmoCRM saniyede belli sayıda isteğe izin verir, bu bekleme çok önemli.
                await Task.Delay(_options.RequestDelayMs, ct);
                
                page++;
            }
            else
            {
                // _embedded yoksa veri bitmiş demektir.
                hasMore = false;
            }
        }
    }

    public async IAsyncEnumerable<AmoContactDto> GetContactsStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // Raw veriyi çekip DTO'ya deserialize ederek de dönebiliriz.
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
        // Base URL ile endpointi birleştirip GET isteği at
        // HttpClient implementasyonuna göre değişebilir ama genelde şöyledir:
        var response = await _httpClient.GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}