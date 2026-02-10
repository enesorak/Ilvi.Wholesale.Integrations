using System.Net.Http.Headers;
using Ilvi.Modules.AmoCrm.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Http;

public class AmoAuthHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;

    public AmoAuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Her istekte DB'den güncel token'ı al (cache'li)
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        
        var options = await settingsService.GetAmoCrmOptionsAsync(cancellationToken);
        
        // Base URL ayarla (eğer istek zaten tam URL değilse)
        if (request.RequestUri != null && !request.RequestUri.IsAbsoluteUri)
        {
            if (!string.IsNullOrEmpty(options.BaseUrl))
            {
                var baseUri = new Uri(options.BaseUrl);
                request.RequestUri = new Uri(baseUri, request.RequestUri.ToString());
            }
        }

        // Token'ı header'a ekle
        if (!string.IsNullOrEmpty(options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}