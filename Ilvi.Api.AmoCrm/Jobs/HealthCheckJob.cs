using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Hangfire.Storage;
using Ilvi.Api.AmoCrm.Services;
using Ilvi.Modules.AmoCrm.Abstractions;
using Ilvi.Modules.AmoCrm.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ilvi.Api.AmoCrm.Jobs;

public class HealthCheckJob
{
    private readonly ITelegramNotificationService _telegram;
    private readonly AmoCrmDbContext _context;
    private readonly ITokenExpiryService _tokenService;
    private readonly ILogger<HealthCheckJob> _logger;

    public HealthCheckJob(
        ITelegramNotificationService telegram,
        AmoCrmDbContext context,
        ITokenExpiryService tokenService,
        ILogger<HealthCheckJob> logger)
    {
        _telegram = telegram;
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Günlük sabah özet raporu — recurring job olarak çalışır
    /// </summary>
    [JobDisplayName("📊 Günlük Sağlık Raporu")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task DailyReport(PerformContext? context, CancellationToken ct)
    {
        _logger.LogInformation("📊 Günlük sağlık raporu hazırlanıyor...");

        var report = new System.Text.StringBuilder();
        report.AppendLine("📊 <b>Günlük Sağlık Raporu</b>");
        report.AppendLine($"🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine();

        // 1. DB bağlantısı
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(ct);
            report.AppendLine(canConnect ? "✅ Veritabanı: Bağlı" : "❌ Veritabanı: Bağlantı yok!");
        }
        catch (Exception ex)
        {
            report.AppendLine($"❌ Veritabanı: {ex.Message}");
        }

        // 2. Token durumu
        try
        {
            var tokenInfo = await _tokenService.CheckTokenExpiryAsync(ct);
            var tokenIcon = tokenInfo.Status switch
            {
                "ok" => "✅",
                "warning" => "⚠️",
                "expired" => "❌",
                _ => "❓"
            };
            report.AppendLine($"{tokenIcon} Token: {tokenInfo.Message}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"❌ Token kontrolü: {ex.Message}");
        }

        // 3. Tablo kayıt sayıları
        try
        {
            var contacts = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Contacts.Contact>().CountAsync(ct);
            var leads = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Leads.Lead>().CountAsync(ct);
            var tasks = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Tasks.AmoTask>().CountAsync(ct);
            var events = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Events.AmoEvent>().CountAsync(ct);
            var messages = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Messages.AmoMessage>().CountAsync(ct);
            var users = await _context.Set<Ilvi.Modules.AmoCrm.Domain.Users.User>().CountAsync(ct);

            report.AppendLine();
            report.AppendLine("📦 <b>Kayıt Sayıları</b>");
            report.AppendLine($"  👥 Kişiler: {contacts:N0}");
            report.AppendLine($"  💼 Fırsatlar: {leads:N0}");
            report.AppendLine($"  📅 Görevler: {tasks:N0}");
            report.AppendLine($"  📜 Olaylar: {events:N0}");
            report.AppendLine($"  💬 Mesajlar: {messages:N0}");
            report.AppendLine($"  👤 Kullanıcılar: {users:N0}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"❌ Kayıt sayısı alınamadı: {ex.Message}");
        }

        // 4. Hangfire job durumları
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var stats = monitor.GetStatistics();

            report.AppendLine();
            report.AppendLine("⚙️ <b>Hangfire</b>");
            report.AppendLine($"  ✅ Başarılı: {stats.Succeeded:N0}");
            report.AppendLine($"  ❌ Başarısız: {stats.Failed:N0}");
            report.AppendLine($"  ⏳ Kuyrukta: {stats.Enqueued:N0}");
            report.AppendLine($"  🔄 Çalışan: {stats.Processing:N0}");
            report.AppendLine($"  🔁 Zamanlanmış: {stats.Recurring:N0}");
            report.AppendLine($"  🖥️ Sunucu: {stats.Servers}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"❌ Hangfire durumu: {ex.Message}");
        }

        var reportText = report.ToString();
        _logger.LogInformation(reportText);
        context?.WriteLine(reportText);

        await _telegram.SendMessageAsync(reportText);
    }

    /// <summary>
    /// API başlangıç bildirimi — Program.cs'ten bir kez çağrılır
    /// </summary>
    public async Task SendStartupNotification()
    {
        try
        {
            var dbOk = await _context.Database.CanConnectAsync();
            var tokenInfo = await _tokenService.CheckTokenExpiryAsync();

            var monitor = JobStorage.Current.GetMonitoringApi();
            var stats = monitor.GetStatistics();

            await _telegram.SendMessageAsync(
                $"🚀 <b>API Başlatıldı</b>\n\n" +
                $"🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                $"✅ Veritabanı: {(dbOk ? "Bağlı" : "❌ Bağlantı yok")}\n" +
                $"🔑 Token: {tokenInfo.Status} ({tokenInfo.DaysUntilExpiry} gün)\n" +
                $"⚙️ Recurring Jobs: {stats.Recurring}\n" +
                $"🖥️ Workers: {stats.Servers}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup bildirimi gönderilemedi.");

            // Basit bildirim gönder
            try { await _telegram.SendMessageAsync("🚀 <b>API Başlatıldı</b>\n🕐 " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"); }
            catch { }
        }
    }

    /// <summary>
    /// API kapanış bildirimi
    /// </summary>
    public async Task SendShutdownNotification()
    {
        try
        {
            await _telegram.SendMessageAsync(
                $"🛑 <b>API Durduruluyor</b>\n🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        }
        catch { }
    }

    /// <summary>
    /// DB bağlantı hatası bildirimi — ihtiyaç halinde çağrılır
    /// </summary>
    public async Task SendDbConnectionError(string errorMessage)
    {
        try
        {
            await _telegram.SendMessageAsync(
                $"🔴 <b>DB Bağlantı Hatası</b>\n\n" +
                $"💥 {errorMessage}\n" +
                $"🕐 {DateTime.UtcNow:HH:mm:ss} UTC");
        }
        catch { }
    }
}