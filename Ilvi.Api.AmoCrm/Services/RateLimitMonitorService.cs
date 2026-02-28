using System.Collections.Concurrent;

namespace Ilvi.Api.AmoCrm.Services;

/// <summary>
/// AmoCRM API Rate Limit Kuralları (resmi dokümantasyon):
/// - Tek entegrasyon: max 7 istek/saniye
/// - Tüm hesap (tüm entegrasyonlar): max 50 istek/saniye
/// - Limit aşılırsa 429 Too Many Requests döner
/// - Ciddi ihlallerde hesap API'si tamamen bloke edilebilir (sadece destek açabilir)
/// 
/// Bu servis:
/// 1. Request sayısını takip eder (sliding window)
/// 2. Adaptive throttling uygular (429 alınca otomatik yavaşlar)
/// 3. Rate limit istatistiklerini raporlar
/// </summary>
public interface IRateLimitMonitorService
{
    /// <summary>Bir istek gönderilmeden ÖNCE çağrılır. Gerekirse bekler.</summary>
    Task WaitIfNeededAsync(CancellationToken ct = default);

    /// <summary>Başarılı istek kaydeder</summary>
    void RecordSuccess();

    /// <summary>429/403 rate limit hatası kaydeder</summary>
    void RecordRateLimitHit();

    /// <summary>Mevcut rate limit durumunu döner</summary>
    RateLimitStatus GetStatus();

    /// <summary>İstatistikleri sıfırlar</summary>
    void ResetStats();

    /// <summary>Ayarları günceller</summary>
    void UpdateSettings(int maxRequestsPerSecond, int retryAfterMs, bool enableAdaptive);
}

public record RateLimitStatus(
    int MaxRequestsPerSecond,
    int CurrentRequestsInWindow,
    int TotalRequestsSent,
    int TotalRateLimitHits,
    int ConsecutiveHits,
    double CurrentDelayMs,
    bool IsThrottled,
    DateTime? LastRequestAt,
    DateTime? LastRateLimitHitAt,
    double HitRatePercent,
    string AdaptiveStatus // "normal", "cautious", "throttled", "backoff"
);

public class RateLimitMonitorService : IRateLimitMonitorService
{
    private readonly ILogger<RateLimitMonitorService> _logger;
    private readonly IConfiguration _configuration;

    // Sliding window: son 1 saniyedeki istek zamanlarını tutar
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();

    // İstatistikler
    private int _totalRequests;
    private int _totalRateLimitHits;
    private int _consecutiveHits;
    private DateTime? _lastRequestAt;
    private DateTime? _lastRateLimitHitAt;

    // Adaptive throttling
    private double _currentDelayMs;
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);

    // Ayarlar
    private int _maxRequestsPerSecond;
    private int _retryAfterMs;
    private int _maxRetries;
    private bool _enableAdaptive;

    // Sabitler
    private const double BaseDelayMs = 150; // ~6.6 req/s (7 limitinin altında güvenli)
    private const double MaxDelayMs = 5000; // Max backoff: 5 saniye
    private const double BackoffMultiplier = 1.5;
    private const double RecoveryMultiplier = 0.8;

    public RateLimitMonitorService(
        ILogger<RateLimitMonitorService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _maxRequestsPerSecond = configuration.GetValue("AmoCrm:RateLimit:MaxRequestsPerSecond", 7);
        _retryAfterMs = configuration.GetValue("AmoCrm:RateLimit:RetryAfterMs", 1000);
        _maxRetries = configuration.GetValue("AmoCrm:RateLimit:MaxRetries", 3);
        _enableAdaptive = configuration.GetValue("AmoCrm:RateLimit:EnableAdaptiveThrottling", true);

        // Başlangıç gecikmesi: 1000ms / maxReq = ~143ms (7 req/s için)
        _currentDelayMs = 1000.0 / _maxRequestsPerSecond;
    }

    public async Task WaitIfNeededAsync(CancellationToken ct = default)
    {
        await _throttleSemaphore.WaitAsync(ct);
        try
        {
            // 1. Sliding window temizliği: 1 saniyeden eski kayıtları çıkar
            var cutoff = DateTime.UtcNow.AddSeconds(-1);
            while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            {
                _requestTimestamps.TryDequeue(out _);
            }

            // 2. Penceredeki istek sayısını kontrol et
            if (_requestTimestamps.Count >= _maxRequestsPerSecond)
            {
                // En eski isteğin üzerinden 1 saniye geçene kadar bekle
                if (_requestTimestamps.TryPeek(out var oldestInWindow))
                {
                    var waitUntil = oldestInWindow.AddSeconds(1);
                    var waitMs = (int)(waitUntil - DateTime.UtcNow).TotalMilliseconds;
                    if (waitMs > 0)
                    {
                        _logger.LogDebug("⏳ Rate limit koruma: {WaitMs}ms bekleniyor ({Count}/{Max} req/s)",
                            waitMs, _requestTimestamps.Count, _maxRequestsPerSecond);
                        await Task.Delay(waitMs, ct);
                    }
                }
            }

            // 3. Adaptive throttling: ek gecikme uygula
            if (_enableAdaptive && _currentDelayMs > BaseDelayMs)
            {
                var extraDelay = (int)(_currentDelayMs - BaseDelayMs);
                if (extraDelay > 0)
                {
                    _logger.LogDebug("🐌 Adaptive throttle: +{ExtraMs}ms ek gecikme", extraDelay);
                    await Task.Delay(extraDelay, ct);
                }
            }

            // 4. Minimum istekler arası mesafe
            if (_lastRequestAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - _lastRequestAt.Value).TotalMilliseconds;
                var minGap = 1000.0 / _maxRequestsPerSecond;
                if (elapsed < minGap)
                {
                    var gapWait = (int)(minGap - elapsed);
                    if (gapWait > 0)
                        await Task.Delay(gapWait, ct);
                }
            }

            // 5. İsteği kaydet
            _requestTimestamps.Enqueue(DateTime.UtcNow);
            _lastRequestAt = DateTime.UtcNow;
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }

    public void RecordSuccess()
    {
        Interlocked.Increment(ref _totalRequests);

        // Başarılı isteklerde adaptive throttling'i yavaşça düşür
        if (_enableAdaptive && _consecutiveHits > 0)
        {
            _consecutiveHits = 0;
        }

        // Recovery: gecikmeyi yavaşça azalt
        if (_enableAdaptive)
        {
            var newDelay = _currentDelayMs * RecoveryMultiplier;
            var minDelay = 1000.0 / _maxRequestsPerSecond;
            _currentDelayMs = Math.Max(newDelay, minDelay);
        }
    }

    public void RecordRateLimitHit()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _totalRateLimitHits);
        Interlocked.Increment(ref _consecutiveHits);
        _lastRateLimitHitAt = DateTime.UtcNow;

        // Adaptive: gecikmeyi artır
        if (_enableAdaptive)
        {
            _currentDelayMs = Math.Min(_currentDelayMs * BackoffMultiplier, MaxDelayMs);
            _logger.LogWarning(
                "🚨 Rate limit hit! Consecutive: {Consecutive}, New delay: {Delay}ms",
                _consecutiveHits, _currentDelayMs);
        }
    }

    public RateLimitStatus GetStatus()
    {
        // Sliding window temizle
        var cutoff = DateTime.UtcNow.AddSeconds(-1);
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            _requestTimestamps.TryDequeue(out _);

        var hitRate = _totalRequests > 0
            ? (double)_totalRateLimitHits / _totalRequests * 100
            : 0;

        var adaptiveStatus = _consecutiveHits switch
        {
            0 => "normal",
            1 => "cautious",
            <= 3 => "throttled",
            _ => "backoff"
        };

        return new RateLimitStatus(
            MaxRequestsPerSecond: _maxRequestsPerSecond,
            CurrentRequestsInWindow: _requestTimestamps.Count,
            TotalRequestsSent: _totalRequests,
            TotalRateLimitHits: _totalRateLimitHits,
            ConsecutiveHits: _consecutiveHits,
            CurrentDelayMs: Math.Round(_currentDelayMs, 1),
            IsThrottled: _consecutiveHits > 0,
            LastRequestAt: _lastRequestAt,
            LastRateLimitHitAt: _lastRateLimitHitAt,
            HitRatePercent: Math.Round(hitRate, 2),
            AdaptiveStatus: adaptiveStatus
        );
    }

    public void ResetStats()
    {
        _totalRequests = 0;
        _totalRateLimitHits = 0;
        _consecutiveHits = 0;
        _lastRequestAt = null;
        _lastRateLimitHitAt = null;
        _currentDelayMs = 1000.0 / _maxRequestsPerSecond;

        while (_requestTimestamps.TryDequeue(out _)) { }

        _logger.LogInformation("📊 Rate limit istatistikleri sıfırlandı.");
    }

    public void UpdateSettings(int maxRequestsPerSecond, int retryAfterMs, bool enableAdaptive)
    {
        _maxRequestsPerSecond = Math.Clamp(maxRequestsPerSecond, 1, 7); // AmoCRM max 7
        _retryAfterMs = Math.Max(retryAfterMs, 100);
        _enableAdaptive = enableAdaptive;
        _currentDelayMs = 1000.0 / _maxRequestsPerSecond;

        _logger.LogInformation(
            "⚙️ Rate limit ayarları güncellendi: {Max} req/s, retry: {Retry}ms, adaptive: {Adaptive}",
            _maxRequestsPerSecond, _retryAfterMs, _enableAdaptive);
    }
}
