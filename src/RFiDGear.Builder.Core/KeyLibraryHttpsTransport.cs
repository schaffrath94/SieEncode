using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

/// <summary>
/// The sole network transport for the existing central encrypted Vault protocol.
/// The TLS server certificate is pinned by SHA-256 of its exact DER bytes;
/// there is no trust-on-first-use, HTTP fallback, redirect or client password.
/// Keep the personal identity alive until this transport is disposed.
/// </summary>
public sealed class KeyLibraryHttpsTransport : IDisposable
{
    private const long MaximumEncryptedResponse = 32L * 1024 * 1024;
    private const long MaximumMetadataResponse = 16 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    private readonly HttpClient client;
    private readonly Guid libraryId;

    public KeyLibraryHttpsTransport(Uri origin, Guid libraryId,
        string serverCertificateSha256, X509Certificate2 personalIdentity)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(personalIdentity);
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            origin.AbsolutePath != "/" || libraryId == Guid.Empty ||
            serverCertificateSha256 is not { Length: 64 } ||
            !serverCertificateSha256.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
            !personalIdentity.HasPrivateKey)
            throw new InvalidDataException("HTTPS-Zentrale, fest bestätigter Serverzertifikat-Fingerabdruck und persönlicher Schlüssel sind erforderlich.");
        using var key = personalIdentity.GetRSAPrivateKey();
        if (key is null || key.KeySize < 3072 ||
            DateTime.UtcNow < personalIdentity.NotBefore.ToUniversalTime() ||
            DateTime.UtcNow >= personalIdentity.NotAfter.ToUniversalTime())
            throw new CryptographicException("Ungültige oder abgelaufene persönliche TLS-Identität.");

        var pinned = Convert.FromHexString(serverCertificateSha256);
        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null ||
                    DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() ||
                    DateTime.UtcNow >= certificate.NotAfter.ToUniversalTime())
                    return false;
                // A separately verified exact server pin replaces the usual CA
                // trust anchor. DNS/chain errors cannot redirect to another key.
                return CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(certificate.RawData), pinned);
            }
        };
        handler.ClientCertificates.Add(personalIdentity);
        client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = origin,
            Timeout = TimeSpan.FromMinutes(2)
        };
        this.libraryId = libraryId;
    }

    public Task<CentralKeyLibraryHead> ReadHeadAsync(CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibraryHead>(HttpMethod.Get, "head", null,
            MaximumMetadataResponse, cancellationToken);

    public Task<CentralKeyLibraryHead> UploadAsync(SignedKeyLibrarySync transfer,
        CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibraryHead>(HttpMethod.Post, "upload", transfer,
            MaximumMetadataResponse, cancellationToken);

    public Task<SignedKeyLibrarySync> DownloadAsync(SignedKeyLibraryDownload proof,
        CancellationToken cancellationToken = default) =>
        SendAsync<SignedKeyLibrarySync>(HttpMethod.Post, "download", proof,
            MaximumEncryptedResponse, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string action,
        object? body, long maxResponse, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method,
            $"v1/libraries/{libraryId:D}/{action}");
        if (body is not null) request.Content = JsonContent.Create(body, options: Options);
        using var response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ConcurrentFileChangeException(client.BaseAddress!.ToString());
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new CryptographicException("Zentrale hat die Anmeldung oder die signierte Berechtigung abgelehnt.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Zentraler Schlüsselabgleich wurde abgewiesen.",
                null, response.StatusCode);
        await response.Content.LoadIntoBufferAsync(maxResponse,
            cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<T>(Options,
            cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidDataException("Zentrale hat keine gültige Antwort übertragen.");
    }

    public void Dispose() => client.Dispose();
}
