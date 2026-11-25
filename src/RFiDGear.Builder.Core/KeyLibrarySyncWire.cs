using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>One explicit, bounded JSON wire format for the HTTPS sync host and Studio.
/// The signed inner claim bytes remain opaque and are not reserialized.</summary>
public static class KeyLibrarySyncWire
{
    public const int MaximumMetadataBytes = 16 * 1024;
    public const int MaximumDownloadRequestBytes = 64 * 1024;
    public const int MaximumEncryptedBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static byte[] Write<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    public static T Read<T>(ReadOnlyMemory<byte> bytes, int limit) where T : class
    {
        if (limit <= 0 || limit > MaximumEncryptedBytes ||
            bytes.Length == 0 || bytes.Length > limit)
            throw new InvalidDataException("Synchronisationsnachricht fehlt oder ist zu groß.");
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        OfflineGroupPolicyService.RejectDuplicateProperties(doc.RootElement);
        return JsonSerializer.Deserialize<T>(bytes.Span, Options)
            ?? throw new InvalidDataException("Synchronisationsnachricht fehlt.");
    }

    public static async Task<T> ReadAsync<T>(Stream input, int limit,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(input);
        if (limit <= 0 || limit > MaximumEncryptedBytes)
            throw new ArgumentOutOfRangeException(nameof(limit));
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // At most one byte beyond the limit is needed to reject an
                // oversized message, including a response without Content-Length.
                var wanted = (int)Math.Min(buffer.Length, limit - output.Length + 1);
                var read = await input.ReadAsync(buffer.AsMemory(0, wanted),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (output.Length + read > limit)
                    throw new InvalidDataException("Synchronisationsnachricht ist zu groß.");
                output.Write(buffer, 0, read);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Read<T>(output.GetBuffer().AsMemory(0, (int)output.Length), limit);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(output.GetBuffer().AsSpan(0, (int)output.Length));
        }
    }
}
