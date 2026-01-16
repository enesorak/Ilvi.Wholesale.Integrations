using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Http;

public class AmoAuthHandler : DelegatingHandler
{
    private readonly AmoCrmOptions _options;

    public AmoAuthHandler(IOptions<AmoCrmOptions> options)
    {
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Token'ı header'a ekle
        if (!string.IsNullOrEmpty(_options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        }

        // İstek devam etsin
        return await base.SendAsync(request, cancellationToken);
    }
}