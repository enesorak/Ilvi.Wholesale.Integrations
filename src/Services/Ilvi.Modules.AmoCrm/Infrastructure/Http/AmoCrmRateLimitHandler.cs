using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Ilvi.Modules.AmoCrm.Infrastructure.Http;

/// <summary>
/// AmoCRM API Rate Limit DelegatingHandler
/// 
/// AmoCRM Resmi Limitler:
/// - Tek entegrasyon: max 7 istek/saniye
/// - Tüm hesap: max 50 istek/saniye
/// - Limit aşılırsa: 429 Too Many Requests
/// - Ciddi ihlallerde: Hesap API erişimi tamamen bloke edilir (sadece destek açabilir)
/// 
/// Bu handler:
/// 1. Her istekten önce sliding window kontrolü yapar
/// 2. 429 hatası alınırsa exponential backoff ile retry yapar
/// 3. Adaptive throttling: sürekli 429 alınırsa otomatik yavaşlar
/// </summary>
public class AmoCrmRateLimitHandler : DelegatingHandler
{
    private readonly ILogger<AmoCrmRateLimitHandler> _logger;

    // Sliding window: son 1 saniyedeki istek zamanlarını tutar
    private static readonly ConcurrentQueue<DateTime> RequestTimestamps = new();
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    // Adaptive throttling state (static - tüm HttpClient instance'ları arasında paylaşılır)
    private static int _consecutiveRateLimitHits;
    private static double _currentDelayMs = BaseDelayMs;

    // Sabitler
    private const int MaxRequestsPerSecond = 7;   // AmoCRM per-integration limit
    private const int MaxRetries = 3;
    private const double BaseDelayMs = 150;        // ~6.6 req/s (7'nin altında güvenli)
    private const double MaxDelayMs = 5000;        // 5 saniye max backoff
    private const double BackoffMultiplier = 2.0;
    private const double RecoveryMultiplier = 0.85;

    public AmoCrmRateLimitHandler(ILogger<AmoCrmRateLimitHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 1. Rate limit kontrolü - gerekirse bekle
        await ThrottleAsync(cancellationToken);

        // 2. İsteği gönder (retry logic ile)
        HttpResponseMessage? response = null;
        var retryCount = 0;

        while (retryCount <= MaxRetries)
        {
            try
            {
                response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests) // 429
                {
                    Interlocked.Increment(ref _consecutiveRateLimitHits);
                    _currentDelayMs = Math.Min(_currentDelayMs * BackoffMultiplier, MaxDelayMs);

                    var retryAfter = GetRetryAfterMs(response);

                    _logger.LogWarning(
                        "🚨 AmoCRM 429 Rate Limit! Retry {Retry}/{Max}, bekleniyor: {Wait}ms, URL: {Url}",
                        retryCount + 1, MaxRetries, retryAfter,
                        request.RequestUri?.PathAndQuery);

                    retryCount++;

                    if (retryCount > MaxRetries)
                    {
                        _logger.LogError("❌ Max retry aşıldı! URL: {Url}", request.RequestUri?.PathAndQuery);
                        return response;
                    }

                    await Task.Delay(retryAfter, cancellationToken);

                    // Yeni bir request message oluştur (HttpRequestMessage tekrar kullanılamaz)
                    request = await CloneRequestAsync(request);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden) // 403
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);

                    // 403 IP whitelisting hatası mı yoksa rate limit mi?
                    if (body.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
                        body.Contains("throttl", StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref _consecutiveRateLimitHits);
                        _currentDelayMs = Math.Min(_currentDelayMs * BackoffMultiplier, MaxDelayMs);

                        _logger.LogWarning(
                            "🚨 AmoCRM 403 (muhtemel rate limit)! Retry {Retry}/{Max}, URL: {Url}",
                            retryCount + 1, MaxRetries, request.RequestUri?.PathAndQuery);

                        retryCount++;
                        if (retryCount > MaxRetries) return response;

                        await Task.Delay((int)_currentDelayMs, cancellationToken);
                        request = await CloneRequestAsync(request);
                        continue;
                    }

                    // Gerçek 403 (yetkilendirme/IP hatası) - retry yapma
                    _logger.LogError("❌ AmoCRM 403 Forbidden (IP/Auth hatası): {Body}", body);
                    return response;
                }

                // Başarılı istek
                RecordSuccess();
                return response;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout
                _logger.LogWarning("⏱️ AmoCRM isteği timeout! Retry {Retry}/{Max}", retryCount + 1, MaxRetries);
                retryCount++;
                if (retryCount > MaxRetries) throw;
                await Task.Delay(1000 * retryCount, cancellationToken);
                request = await CloneRequestAsync(request);
            }
        }

        return response ?? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task ThrottleAsync(CancellationToken ct)
    {
        await Semaphore.WaitAsync(ct);
        try
        {
            // 1. Sliding window temizliği
            var cutoff = DateTime.UtcNow.AddSeconds(-1);
            while (RequestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
                RequestTimestamps.TryDequeue(out _);

            // 2. Pencere doluysa bekle
            if (RequestTimestamps.Count >= MaxRequestsPerSecond)
            {
                if (RequestTimestamps.TryPeek(out var oldestInWindow))
                {
                    var waitUntil = oldestInWindow.AddSeconds(1);
                    var waitMs = (int)(waitUntil - DateTime.UtcNow).TotalMilliseconds;
                    if (waitMs > 0)
                    {
                        await Task.Delay(waitMs, ct);
                    }
                }
            }

            // 3. Adaptive throttling: 429 yaşandıysa ek gecikme
            if (_consecutiveRateLimitHits > 0 && _currentDelayMs > BaseDelayMs)
            {
                var extraDelay = (int)(_currentDelayMs - BaseDelayMs);
                if (extraDelay > 0)
                    await Task.Delay(extraDelay, ct);
            }

            // 4. Minimum istekler arası mesafe (en az ~143ms)
            if (RequestTimestamps.TryPeek(out var last))
            {
                var elapsed = (DateTime.UtcNow - last).TotalMilliseconds;
                var minGap = 1000.0 / MaxRequestsPerSecond;
                if (elapsed < minGap)
                {
                    await Task.Delay((int)(minGap - elapsed), ct);
                }
            }

            // 5. Zaman damgası ekle
            RequestTimestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static void RecordSuccess()
    {
        if (_consecutiveRateLimitHits > 0)
        {
            _consecutiveRateLimitHits = 0;
        }

        // Recovery
        var minDelay = 1000.0 / MaxRequestsPerSecond;
        _currentDelayMs = Math.Max(_currentDelayMs * RecoveryMultiplier, minDelay);
    }

    private static int GetRetryAfterMs(HttpResponseMessage response)
    {
        // Retry-After header varsa kullan
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var val = values.FirstOrDefault();
            if (int.TryParse(val, out var seconds))
                return seconds * 1000;
        }

        // Yoksa exponential backoff
        return (int)_currentDelayMs;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        // Headers kopyala
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Content kopyala (varsa)
        if (original.Content != null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
