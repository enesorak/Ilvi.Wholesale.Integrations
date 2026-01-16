using System.Text.Json.Serialization;

namespace Ilvi.Modules.AmoCrm.Models;

// AmoCRM genelde listeleri "_embedded" içinde döner.
public class AmoListResponse<T>
{
    [JsonPropertyName("_embedded")]
    public EmbeddedData<T>? Embedded { get; set; }
}

public class EmbeddedData<T>
{
    // API endpoint'ine göre bu property adı değişebilir (leads, contacts, tasks)
    // Bu yüzden dinamik yönetmektense her biri için ayrı wrapper yapabiliriz
    // Veya JSON path ile çözebiliriz. Basitlik adına her tip için ayrı DTO yapalım.
}

// CONTACT RESPONSE
public class ContactListResponse
{
    [JsonPropertyName("_embedded")]
    public ContactEmbedded? Embedded { get; set; }
}
public class ContactEmbedded
{
    [JsonPropertyName("contacts")]
    public List<AmoContactDto> Items { get; set; } = new();
}
public class AmoContactDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
    
    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
    
    [JsonPropertyName("responsible_user_id")]
    public long ResponsibleUserId { get; set; }
    
    // Custom Fields vb. hepsi buraya eklenebilir ama
    // RAW JSON mantığını koruyacağımız için bu kadarı yeterli.
}

// LEAD RESPONSE
public class LeadListResponse
{
    [JsonPropertyName("_embedded")]
    public LeadEmbedded? Embedded { get; set; }
}
public class LeadEmbedded
{
    [JsonPropertyName("leads")]
    public List<AmoLeadDto> Items { get; set; } = new();
}
public class AmoLeadDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("price")]
    public int Price { get; set; }
    
    [JsonPropertyName("pipeline_id")]
    public long PipelineId { get; set; }
    
    [JsonPropertyName("status_id")]
    public long StatusId { get; set; }
    
    [JsonPropertyName("responsible_user_id")]
    public long ResponsibleUserId { get; set; }
    
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
    
    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
    
    [JsonPropertyName("closed_at")]
    public long? ClosedAt { get; set; }
}

// GENEL KULLANIM
// Diğer entity'ler (Tasks, Users vb.) serviste işlenirken tanımlanacak.