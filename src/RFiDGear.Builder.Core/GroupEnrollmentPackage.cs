using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record GroupEnrollmentPackage(string Format, int Version, Guid LibraryId,
    byte[] AdministratorPublicKey, SignedGroupPolicy Policy);

/// <summary>Validates a public transport package. Its signature is not proof of administrator trust.</summary>
public static class GroupEnrollmentPackages
{
    public const string Format = "SieEncode-Group-Enrollment";
    public const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 20 };

    public static GroupEnrollmentPackage Read(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Registrierungsdatei ist leer oder zu groß.");
        using (var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 20 })) RejectDuplicates(document.RootElement);
        var package = JsonSerializer.Deserialize<GroupEnrollmentPackage>(bytes, Options) ?? throw new InvalidDataException("Registrierungsdatei fehlt.");
        Validate(package);
        return package;
    }

    public static byte[] Write(GroupEnrollmentPackage package)
    {
        Validate(package);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Registrierungsdatei ist zu groß.");
        return bytes;
    }

    public static VerifiedGroupPolicy Validate(GroupEnrollmentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Format != Format || package.Version != 1 || package.LibraryId == Guid.Empty ||
            package.AdministratorPublicKey is null || package.AdministratorPublicKey.Length is 0 or > 4096 || package.Policy is null)
            throw new InvalidDataException("Ungültige Registrierungsdatei.");
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(package.AdministratorPublicKey, out var read);
        if (read != package.AdministratorPublicKey.Length) throw new InvalidDataException("Unerwartete Daten nach Verwaltungsschlüssel.");
        return OfflineGroupPolicyService.Verify(package.Policy, rsa, package.LibraryId);
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Doppeltes Feld in Registrierungsdatei.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicates(child);
    }
}
