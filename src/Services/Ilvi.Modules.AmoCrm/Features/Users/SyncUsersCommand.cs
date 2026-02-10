using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire.Console;
using Hangfire.Server;
using Ilvi.Core.Utils;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Features.Users;

public record SyncUsersCommand : IRequest<bool>
{
    [JsonIgnore]
    public PerformContext? Context { get; set; }
}

public class SyncUsersCommandHandler : IRequestHandler<SyncUsersCommand, bool>
{
    private readonly IAmoCrmService _apiService;
    private readonly IAmoRepository<User, UserId> _repository;
    private readonly ILogger<SyncUsersCommandHandler> _logger;

    public SyncUsersCommandHandler(
        IAmoCrmService apiService,
        IAmoRepository<User, UserId> repository,
        ILogger<SyncUsersCommandHandler> logger)
    {
        _apiService = apiService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(SyncUsersCommand request, CancellationToken ct)
    {
        request.Context?.WriteLine("🚀 Kullanıcılar (Users) Eşitleme Başladı...");
        _logger.LogInformation("Starting Users Synchronization...");

        // AmoCRM Users endpoint'i: /api/v4/users
        var jsonResponse = await _apiService.GetRawJsonAsync("users", ct);

        if (string.IsNullOrEmpty(jsonResponse))
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine("❌ API'den boş yanıt döndü.");
            request.Context?.ResetTextColor();
            return false;
        }

        var usersToUpsert = new List<User>();

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("_embedded", out var embedded) &&
                embedded.TryGetProperty("users", out var usersArray))
            {
                foreach (var item in usersArray.EnumerateArray())
                {
                    long id = item.GetProperty("id").GetInt64();
                    string name = item.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";
                    string email = item.TryGetProperty("email", out var pEmail) ? pEmail.GetString() ?? "" : "";

                    string rawJson = item.GetRawText();
                    string hash = HashGenerator.ComputeSha256(rawJson);

                    var user = new User(UserId.From(id))
                    {
                        Name = name,
                        Email = email,
                        Raw = rawJson,
                        ComputedHash = hash,
                        CheckedAtUtc = DateTime.UtcNow,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    usersToUpsert.Add(user);
                }
            }

            if (usersToUpsert.Any())
            {
                await _repository.BulkUpsertAsync(usersToUpsert, 100, ct);

                request.Context?.SetTextColor(ConsoleTextColor.Green);
                request.Context?.WriteLine($"✅ Toplam {usersToUpsert.Count} kullanıcı güncellendi.");
                request.Context?.ResetTextColor();
            }
            else
            {
                request.Context?.WriteLine("ℹ️ Hiç kullanıcı bulunamadı.");
            }
        }
        catch (Exception ex)
        {
            request.Context?.SetTextColor(ConsoleTextColor.Red);
            request.Context?.WriteLine($"❌ Hata: {ex.Message}");
            request.Context?.ResetTextColor();
            _logger.LogError(ex, "Error syncing users");
            throw;
        }

        request.Context?.WriteLine("🏁 Users Eşitleme Tamamlandı.");
        return true;
    }
}
 