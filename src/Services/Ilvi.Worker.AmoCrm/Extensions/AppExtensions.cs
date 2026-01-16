using Hangfire;

namespace Ilvi.Worker.AmoCrm.Extensions;

public static class AppExtensions
{
    public static void UseWorkerMiddleware(this WebApplication app)
    {
        // Statik dosyalar (dashboard.html için)
        app.UseFileServer();

        // Hangfire Dashboard
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "Ilvi Entegrasyon Paneli",
            DarkModeEnabled = false // İstersen true yapabilirsin
        });
    }
}