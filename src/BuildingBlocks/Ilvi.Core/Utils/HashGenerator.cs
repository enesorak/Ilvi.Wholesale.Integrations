using System.Security.Cryptography;
using System.Text;

namespace Ilvi.Core.Utils;

public static class HashGenerator
{
    // MD5, değişiklik algılama için yeterli ve hızlıdır.
    // Güvenlik (şifreleme) değil, checksum için kullanıyoruz.
    public static string ComputeSha256(string rawData)
    {
        if (string.IsNullOrEmpty(rawData)) return string.Empty;

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(rawData);
        var hash = sha.ComputeHash(bytes);
        
        return Convert.ToHexString(hash);
    }
}