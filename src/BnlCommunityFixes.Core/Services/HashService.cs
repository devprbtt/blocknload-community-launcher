using System.Security.Cryptography;

namespace BnlCommunityFixes.Core.Services;

public static class HashService
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    public static async Task<bool> VerifySha256Async(string filePath, string expectedSha256, CancellationToken cancellationToken = default)
    {
        var actual = await ComputeSha256Async(filePath, cancellationToken);
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
