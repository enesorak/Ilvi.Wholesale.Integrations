using Ilvi.Modules.AmoCrm.Models; // DTO namespace

namespace Ilvi.Modules.AmoCrm.Abstractions;

public interface IAmoCrmService
{
    // IAsyncEnumerable: Verileri sayfa sayfa (Stream) olarak akıtır. 
    // Tüm listeyi hafızaya yüklemez, RAM dostudur.
    IAsyncEnumerable<AmoContactDto> GetContactsStreamAsync(CancellationToken ct = default);
    
    IAsyncEnumerable<AmoLeadDto> GetLeadsStreamAsync(CancellationToken ct = default);
    
    // Ham JSON dönüşü gerekirse (Mapping yapmadan kaydetmek için)
    // Bu metod tüm sayfaları gezip tek tek JSON string döner.
    IAsyncEnumerable<(TId Id, string Json)> GetRawDataStreamAsync<TId>(
        string endpoint, 
        string rootProperty, 
        CancellationToken ct = default);
    
    Task<string> GetRawJsonAsync(string endpoint, CancellationToken ct = default);
}