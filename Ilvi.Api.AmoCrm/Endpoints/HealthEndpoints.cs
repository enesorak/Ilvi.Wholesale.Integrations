using Ilvi.Api.AmoCrm.Services;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ilvi.Api.AmoCrm.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/health").WithTags("Health");

        // 1. Basit health check
        group.MapGet("/", async (AmoCrmDbContext context) =>
        {
            try
            {
                await context.Database.CanConnectAsync();
                return Results.Ok(new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow,
                    database = "Connected"
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "Unhealthy",
                    timestamp = DateTime.UtcNow,
                    database = "Disconnected",
                    error = ex.Message
                }, statusCode: 503);
            }
        });

        // 2. Detaylı dashboard
        group.MapGet("/dashboard", async (
            AmoCrmDbContext context,
            ITokenExpiryService tokenService,
            IRateLimitMonitorService rateLimitService,
            ITelegramNotificationService telegramService) =>
        {
            var dbHealthy = false;
            string? dbError = null;

            try
            {
                dbHealthy = await context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                dbError = ex.Message;
            }

            var tokenInfo = await tokenService.CheckTokenExpiryAsync();
            var rateLimitStatus = rateLimitService.GetStatus();
            var telegramOk = await telegramService.TestConnectionAsync();

            // Sync istatistikleri - son sync zamanlarını DB'den al
            var syncStats = new Dictionary<string, object?>();
            try
            {
                var settings = await context.AppSettings
                    .Where(s => s.Category == "Sync" || s.Category == "LastSync")
                    .ToListAsync();

                foreach (var s in settings.Where(x => x.Category == "LastSync"))
                {
                    syncStats[s.Key] = s.Value;
                }
            }
            catch { /* ignore */ }

            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                overall = dbHealthy && tokenInfo.Status != "expired" ? "Healthy" : "Degraded",
                components = new
                {
                    database = new { healthy = dbHealthy, error = dbError },
                    token = tokenInfo,
                    rateLimit = rateLimitStatus,
                    telegram = new { connected = telegramOk }
                },
                lastSyncTimes = syncStats
            });
        });

        // 3. DB istatistikleri
        group.MapGet("/db-stats", async (AmoCrmDbContext context) =>
        {
            try
            {
                var contactCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Contacts.Contact>().CountAsync();
                var leadCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Leads.Lead>().CountAsync();
                var taskCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Tasks.AmoTask>().CountAsync();
                var eventCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Events.AmoEvent>().CountAsync();
                var messageCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Messages.AmoMessage>().CountAsync();
                var userCount = await context.Set<Ilvi.Modules.AmoCrm.Domain.Users.User>().CountAsync();

                return Results.Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    counts = new
                    {
                        contacts = contactCount,
                        leads = leadCount,
                        tasks = taskCount,
                        events = eventCount,
                        messages = messageCount,
                        users = userCount
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
