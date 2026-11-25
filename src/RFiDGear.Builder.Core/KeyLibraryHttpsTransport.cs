using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RFiDGear.Builder.Core;

/// <summary>
/// The sole network transport for the existing central encrypted Vault protocol.
/// The TLS server certificate is pinned by SHA-256 of its exact DER bytes;
/// there is no trust-on-first-use, HTTP fallback, redirect or client password.
/// Keep the personal identity alive until this transport is disposed.
/// </summary>
public sealed class KeyLibraryHttpsTransport : IDisposable, IKeyLibraryPolicyTransitionTransport
{
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
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
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
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
        this.libraryId = libraryId;
    }

    public Task<CentralKeyLibraryHead> ReadHeadAsync(CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibraryHead>(HttpMethod.Get, "head", null,
            KeyLibrarySyncWire.MaximumMetadataBytes, cancellationToken);

    public Task<CentralKeyLibraryHead> UploadAsync(SignedKeyLibrarySync transfer,
        CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibraryHead>(HttpMethod.Post, "upload", transfer,
            KeyLibrarySyncWire.MaximumMetadataBytes, cancellationToken);

    public Task<CentralKeyLibraryHead> ChangePolicyAsync(SignedKeyLibraryPolicyTransition transition,
        CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibraryHead>(HttpMethod.Post, "policy-transition", transition,
            KeyLibrarySyncWire.MaximumMetadataBytes, cancellationToken);

    public Task<SignedKeyLibrarySync> DownloadAsync(SignedKeyLibraryDownload proof,
        CancellationToken cancellationToken = default) =>
        SendAsync<SignedKeyLibrarySync>(HttpMethod.Post, "download", proof,
            KeyLibrarySyncWire.MaximumEncryptedBytes, cancellationToken);

    public Task<CentralKeyLibrarySnapshot> DownloadSnapshotAsync(SignedKeyLibraryDownload proof,
        CancellationToken cancellationToken = default) =>
        SendAsync<CentralKeyLibrarySnapshot>(HttpMethod.Post, "snapshot", proof,
            KeyLibrarySyncWire.MaximumEncryptedBytes, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string action,
        object? body, int maxResponse, CancellationToken cancellationToken) where T : class
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        using var request = new HttpRequestMessage(method,
            $"v1/libraries/{libraryId:D}/{action}");
        if (body is not null)
        {
            var bytes = KeyLibrarySyncWire.Write(body);
            var maxRequest = action is "download" or "snapshot" ? KeyLibrarySyncWire.MaximumDownloadRequestBytes
                : KeyLibrarySyncWire.MaximumEncryptedBytes;
            if (bytes.Length > maxRequest)
                throw new InvalidDataException("Synchronisationsanfrage ist zu groß.");
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        using var response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new ConcurrentFileChangeException(client.BaseAddress!.ToString());
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new CryptographicException("Zentrale hat die Anmeldung oder die signierte Berechtigung abgelehnt.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Zentraler Schlüsselabgleich wurde abgewiesen.",
                null, response.StatusCode);
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType,
            "application/json", StringComparison.OrdinalIgnoreCase) ||
            response.Content.Headers.ContentLength is long length && length > maxResponse)
            throw new InvalidDataException("Zentrale hat keine gültige oder ausreichend kleine JSON-Antwort übertragen.");
        await using var stream = await response.Content.ReadAsStreamAsync(token)
            .ConfigureAwait(false);
        return await KeyLibrarySyncWire.ReadAsync<T>(stream, maxResponse, token)
            .ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();
}
