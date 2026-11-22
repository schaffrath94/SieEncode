using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RFiDGear.Builder.Core;

internal static class KeyLibrarySyncEndpointTests
{
    public static void Verify()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "SieEncode-sync-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var library = Guid.NewGuid();
        var file = Path.Combine(directory, "endpoint.bin");
        var protection = new OfflineGroupTrustTests.TestProtection();
        var store = new KeyLibrarySyncEndpointStore(file, library, protection);
        var pin = new string('A', 64);
        var endpoint = new KeyLibrarySyncEndpoint(library,
            "https://localhost:7443/", pin);
        using var key = RSA.Create(3072);
        using var identity = new CertificateRequest("CN=Synthetic sync client",
            key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        try
        {
            if (store.Load() is not null)
                throw new Exception("Nonexistent sync setting was treated as configured.");
            var version = store.Save(endpoint);
            if (!version.Exists || store.Load() != endpoint)
                throw new Exception("Confirmed HTTPS endpoint was not persisted for this library.");
            var raw = File.ReadAllText(file);
            if (raw.Contains(endpoint.HttpsOrigin, StringComparison.Ordinal) ||
                raw.Contains(pin, StringComparison.Ordinal))
                throw new Exception("Local endpoint config was not protected by the user protector.");
            using (var valid = new KeyLibraryHttpsTransport(
                new Uri(endpoint.HttpsOrigin), library, pin, identity)) { }
            Reject<InvalidDataException>(() => KeyLibrarySyncEndpointStore.Validate(
                endpoint with { HttpsOrigin = "http://localhost:7443/" }));
            Reject<InvalidDataException>(() => KeyLibrarySyncEndpointStore.Validate(
                endpoint with { HttpsOrigin = "https://localhost:7443/other" }));
            Reject<InvalidDataException>(() => KeyLibrarySyncEndpointStore.Validate(
                endpoint with { HttpsOrigin = "https://user:password@localhost:7443/" }));
            Reject<InvalidDataException>(() => KeyLibrarySyncEndpointStore.Validate(
                endpoint with { ServerCertificateSha256 = pin.ToLowerInvariant() }));
            Reject<CryptographicException>(() => new KeyLibrarySyncEndpointStore(
                file, Guid.NewGuid(), protection).Load());
            Reject<Exception>(() => new KeyLibrarySyncEndpointStore(
                file, library, new OfflineGroupTrustTests.TestProtection()).Load());
            Reject<CryptographicException>(() => store.Save(
                endpoint with { LibraryId = Guid.NewGuid() }));
            Reject<InvalidDataException>(() =>
            {
                using var denied = new KeyLibraryHttpsTransport(new Uri(
                    "http://localhost:7443/"), library, pin, identity);
            });
            Reject<InvalidDataException>(() =>
            {
                using var denied = new KeyLibraryHttpsTransport(new Uri(
                    "https://localhost:7443/"), library, "invalid", identity);
            });
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject<T>(Action run) where T : Exception
    {
        try { run(); }
        catch (T) { return; }
        throw new Exception("Invalid central endpoint or TLS configuration accepted: " +
            typeof(T).Name);
    }
}
