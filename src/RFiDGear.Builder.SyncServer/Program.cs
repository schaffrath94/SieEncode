using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Mvc;
using RFiDGear.Builder.Core;

// Server provisioning is deliberately out of band: a remote upload cannot
// register or replace its own signing authority. No web UI/admin endpoints.
static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException("Erforderliche Headless-Dienstkonfiguration fehlt: " + name);

var libraryId = Guid.Parse(Required("SIEENCODE_SYNC_LIBRARY_ID"));
var stateFile = Path.GetFullPath(Required("SIEENCODE_SYNC_STATE_FILE"));
var tlsPath = Path.GetFullPath(Required("SIEENCODE_SYNC_TLS_PFX"));
var tlsPassword = Required("SIEENCODE_SYNC_TLS_PASSWORD");
var portText = Required("SIEENCODE_SYNC_PORT");
if (!int.TryParse(portText, out var port) || port is < 1024 or > 65535)
    throw new InvalidOperationException("HTTPS-Port ist ungültig.");
// One-time local bootstrap only. The public enrollment package originates
// from the existing SieEncode Studio group administration. Its fingerprint
// must be independently verified and supplied by the server operator.
// No remote HTTP request can create or replace the trust root.
var store = new CentralKeyLibraryStore(stateFile, libraryId);
if (!File.Exists(stateFile))
{
    var publicPackageFile = Path.GetFullPath(
        Required("SIEENCODE_SYNC_BOOTSTRAP_ENROLLMENT_FILE"));
    var confirmedFingerprint = Required(
        "SIEENCODE_SYNC_BOOTSTRAP_ADMIN_FINGERPRINT");
    var info = new FileInfo(publicPackageFile);
    if (!info.Exists || info.Length is <= 0 or > GroupEnrollmentPackages.MaximumBytes)
        throw new InvalidDataException("Öffentliches SieEncode-Gruppenpaket fehlt oder ist ungültig.");
    var signedEnrollment = GroupEnrollmentPackages.Read(
        File.ReadAllBytes(publicPackageFile));
    store.Register(signedEnrollment, confirmedFingerprint);
}
using var serverCertificate = new X509Certificate2(tlsPath, tlsPassword,
    X509KeyStorageFlags.EphemeralKeySet);
if (!serverCertificate.HasPrivateKey ||
    DateTime.UtcNow < serverCertificate.NotBefore.ToUniversalTime() ||
    DateTime.UtcNow >= serverCertificate.NotAfter.ToUniversalTime())
    throw new CryptographicException("Gültiges TLS-Serverzertifikat mit privatem Schlüssel fehlt.");

_ = store.ReadHead(); // Verify the already pinned authority before listening.

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.ConfigureKestrel(server =>
{
    server.AddServerHeader = false;
    server.Limits.MaxRequestBodySize = KeyLibrarySyncWire.MaximumEncryptedBytes;
    server.ListenAnyIP(port, endpoint =>
        endpoint.UseHttps(https =>
        {
            https.ServerCertificate = serverCertificate;
            https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            // Public-CA chain validation is deliberately replaced by exact
            // certificate enrollment in the signed people/device directory.
            // TLS still proves possession of the client certificate private key.
            https.ClientCertificateValidation = (clientCertificate, _, _) =>
                clientCertificate is not null &&
                store.IsAuthorizedTlsMember(clientCertificate, DateTimeOffset.UtcNow);
        }));
});
var app = builder.Build();

app.Use(async (context, next) =>
{
    var certificate = context.Connection.ClientCertificate;
    if (!context.Request.IsHttps || certificate is null ||
        !store.IsAuthorizedTlsMember(certificate, DateTimeOffset.UtcNow))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    // No body, key, certificate or signable payload is written to server logs.
    await next();
});

app.MapGet("/v1/libraries/{id:guid}/head", (Guid id) =>
{
    if (id != libraryId) return Results.NotFound();
    return Run(() => WireResponse(store.ReadHead()));
});

app.MapPost("/v1/libraries/{id:guid}/upload", async (Guid id, HttpRequest request) =>
{
    if (id != libraryId) return Results.NotFound();
    var transfer = await ReadPayload<SignedKeyLibrarySync>(request, KeyLibrarySyncWire.MaximumEncryptedBytes);
    if (transfer is null) return Results.BadRequest();
    return Run(() => WireResponse(store.Accept(transfer)));
});

app.MapPost("/v1/libraries/{id:guid}/policy-transition", async (Guid id, HttpRequest request) =>
{
    if (id != libraryId) return Results.NotFound();
    var certificate = request.HttpContext.Connection.ClientCertificate;
    if (certificate is null) return Results.Unauthorized();
    var transition = await ReadPayload<SignedKeyLibraryPolicyTransition>(request,
        KeyLibrarySyncWire.MaximumEncryptedBytes);
    if (transition is null) return Results.BadRequest();
    return Run(() => WireResponse(store.AcceptPolicyTransition(transition,
        certificate, DateTimeOffset.UtcNow)));
});

app.MapPost("/v1/libraries/{id:guid}/download", async (Guid id, HttpRequest request) =>
{
    if (id != libraryId) return Results.NotFound();
    var claim = await ReadPayload<SignedKeyLibraryDownload>(request, KeyLibrarySyncWire.MaximumDownloadRequestBytes);
    if (claim is null) return Results.BadRequest();
    return Run(() => WireResponse(store.ReadEncryptedSnapshot(claim,
        DateTimeOffset.UtcNow)));
}).WithMetadata(new RequestSizeLimitAttribute(KeyLibrarySyncWire.MaximumDownloadRequestBytes));

app.MapPost("/v1/libraries/{id:guid}/snapshot", async (Guid id, HttpRequest request) =>
{
    if (id != libraryId) return Results.NotFound();
    var certificate = request.HttpContext.Connection.ClientCertificate;
    if (certificate is null) return Results.Unauthorized();
    var proof = await ReadPayload<SignedKeyLibraryDownload>(request, KeyLibrarySyncWire.MaximumDownloadRequestBytes);
    if (proof is null) return Results.BadRequest();
    return Run(() => WireResponse(store.ReadAuthorizedSnapshot(proof,
        certificate, DateTimeOffset.UtcNow)));
}).WithMetadata(new RequestSizeLimitAttribute(KeyLibrarySyncWire.MaximumDownloadRequestBytes));

app.Run();

static IResult Run(Func<IResult> handler)
{
    try { return handler(); }
    catch (ConcurrentFileChangeException)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Zentraler Revisionskonflikt");
    }
    catch (CryptographicException)
    {
        return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Signatur, Mitgliedschaft oder Berechtigung nicht bestätigt");
    }
    catch (InvalidDataException)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "Ungültige Bibliotheks- oder Transportdaten");
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
}

static IResult WireResponse<T>(T value) where T : class =>
    Results.Bytes(KeyLibrarySyncWire.Write(value), "application/json");

static async Task<T?> ReadPayload<T>(HttpRequest request, int limit) where T : class
{
    if (!request.HasJsonContentType() || request.ContentLength is long length && length > limit)
        return null;
    try
    {
        return await KeyLibrarySyncWire.ReadAsync<T>(request.Body, limit,
            request.HttpContext.RequestAborted);
    }
    catch (JsonException) { return null; }
    catch (InvalidDataException) { return null; }
}
