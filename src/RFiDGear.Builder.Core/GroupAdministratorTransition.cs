using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Builder.Core;

public sealed record SignedAdministratorTransition(byte[] Payload, byte[] PreviousSignature, byte[] NextSignature);
public sealed record AdministratorTransitionRequest(string Format, int Version, byte[] Payload, byte[] NextSignature);

/// <summary>Explicit root-key transition verification only; does not enroll or persist trust.
/// The current package MUST come from the already enrolled local trust store.</summary>
public static class GroupAdministratorTransition
{
    public const string Format = "SieEncode-Administrator-Transition";
    public const string RequestFormat = "SieEncode-Administrator-Transition-Request";
    public const int MaximumBytes = 3 * 1024 * 1024;
    private sealed record Payload(string Format, int Version, Guid LibraryId, byte[] PreviousPublicKey,
        GroupPolicyCheckpoint PreviousCheckpoint, GroupEnrollmentPackage Next);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 24
    };

    // The pure transition preserves all rights. Changing/deleting delegations is a separate signed policy operation.
    public static SignedAdministratorTransition Create(GroupEnrollmentPackage current, RSA previousKey, RSA nextKey)
        => Authorize(Prepare(current, nextKey), current, previousKey,
            OfflineGroupTrustStore.AdministratorFingerprint(nextKey.ExportSubjectPublicKeyInfo()), confirmed: true);

    /// <summary>Successor side: uses only the successor's private key and the enrolled public state.</summary>
    public static AdministratorTransitionRequest Prepare(GroupEnrollmentPackage current, RSA nextKey)
    {
        var snapshot = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(current));
        var verified = GroupEnrollmentPackages.Validate(snapshot);
        var nextPolicy = verified.Policy with { Revision = checked(verified.Policy.Revision + 1) };
        var next = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, snapshot.LibraryId,
            nextKey.ExportSubjectPublicKeyInfo(), OfflineGroupPolicyService.Sign(nextPolicy, nextKey));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(Format, 1, snapshot.LibraryId,
            snapshot.AdministratorPublicKey, verified.Checkpoint, next), Options);
        var request = new AdministratorTransitionRequest(RequestFormat, 1, payload,
            nextKey.SignData(SignatureInput(payload, "next"), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        _ = InspectRequest(request, snapshot);
        return request;
    }

    /// <summary>Current administrator side: receives only public bytes from the successor.</summary>
    public static SignedAdministratorTransition Authorize(AdministratorTransitionRequest request,
        GroupEnrollmentPackage current, RSA previousKey, string independentlyConfirmedNewFingerprint, bool confirmed)
    {
        if (!confirmed) throw new InvalidDataException("Verwaltungswechsel muss ausdrücklich autorisiert werden.");
        var candidate = ReadRequest(WriteRequest(request));
        var snapshot = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(current));
        var next = InspectRequest(candidate, snapshot);
        if (!previousKey.ExportSubjectPublicKeyInfo().SequenceEqual(snapshot.AdministratorPublicKey))
            throw new CryptographicException("Bisheriger privater Verwaltungsschlüssel passt nicht zum registrierten Stand.");
        if (!string.Equals(OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey),
                independentlyConfirmedNewFingerprint, StringComparison.Ordinal))
            throw new CryptographicException("Neuer Verwaltungsschlüssel wurde nicht unabhängig bestätigt.");
        return new(candidate.Payload,
            previousKey.SignData(SignatureInput(candidate.Payload, "previous"), HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            candidate.NextSignature);
    }

    public static GroupEnrollmentPackage InspectRequest(AdministratorTransitionRequest request, GroupEnrollmentPackage enrolledCurrent)
    {
        var candidate = ReadRequest(WriteRequest(request));
        var current = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(enrolledCurrent));
        return InspectSuccessor(candidate.Payload, candidate.NextSignature, current);
    }

    public static GroupEnrollmentPackage Verify(SignedAdministratorTransition transition,
        GroupEnrollmentPackage enrolledCurrent, string independentlyConfirmedNewFingerprint)
    {
        var next = Inspect(transition, enrolledCurrent);
        if (!string.Equals(OfflineGroupTrustStore.AdministratorFingerprint(next.AdministratorPublicKey),
                independentlyConfirmedNewFingerprint, StringComparison.Ordinal))
            throw new CryptographicException("Neuer Verwaltungsschlüssel wurde nicht unabhängig bestätigt.");
        return next;
    }

    /// <summary>Cryptographic inspection for a confirmation screen, not consent to replace trust.
    /// The current package must come from the locally enrolled store.</summary>
    public static GroupEnrollmentPackage Inspect(SignedAdministratorTransition transition, GroupEnrollmentPackage enrolledCurrent)
    {
        // Snapshot all caller-owned arrays before signature checks and parsing.
        var signed = Read(Write(transition));
        var current = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(enrolledCurrent));
        using var previousKey = OpenKey(current.AdministratorPublicKey);
        if (signed.PreviousSignature.Length != previousKey.KeySize / 8 ||
            !previousKey.VerifyData(SignatureInput(signed.Payload, "previous"), signed.PreviousSignature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Verwaltungswechsel wurde nicht vom registrierten Schlüssel bestätigt.");
        return InspectSuccessor(signed.Payload, signed.NextSignature, current);
    }

    private static GroupEnrollmentPackage InspectSuccessor(byte[] payloadBytes, byte[] nextSignature, GroupEnrollmentPackage current)
    {
        var oldPolicy = GroupEnrollmentPackages.Validate(current);
        var payload = Parse<Payload>(payloadBytes);
        if (payload.Format != Format || payload.Version != 1 || payload.LibraryId != current.LibraryId ||
            payload.PreviousPublicKey is null || !payload.PreviousPublicKey.SequenceEqual(current.AdministratorPublicKey) ||
            payload.PreviousCheckpoint != oldPolicy.Checkpoint || payload.Next is null)
            throw new CryptographicException("Verwaltungswechsel passt nicht zum registrierten Ausgangsstand.");
        var nextPolicy = GroupEnrollmentPackages.Validate(payload.Next);
        if (payload.Next.LibraryId != current.LibraryId ||
            payload.Next.AdministratorPublicKey.SequenceEqual(current.AdministratorPublicKey))
            throw new CryptographicException("Neuer Verwaltungsschlüssel oder Bibliotheksbindung ungültig.");
        using var nextKey = OpenKey(payload.Next.AdministratorPublicKey);
        if (nextSignature.Length != nextKey.KeySize / 8 ||
            !nextKey.VerifyData(SignatureInput(payloadBytes, "next"), nextSignature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Besitz des neuen Verwaltungsschlüssels nicht nachgewiesen.");
        var expected = oldPolicy.Policy with { Revision = checked(oldPolicy.Policy.Revision + 1) };
        if (!JsonSerializer.SerializeToUtf8Bytes(expected, Options).SequenceEqual(
                JsonSerializer.SerializeToUtf8Bytes(nextPolicy.Policy, Options)))
            throw new CryptographicException("Ein reiner Verwaltungswechsel darf keine Zugriffsrechte ändern.");
        return payload.Next;
    }

    public static byte[] WriteRequest(AdministratorTransitionRequest request)
    {
        CheckRequest(request);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Wechselanforderung ist zu groß.");
        return bytes;
    }

    public static AdministratorTransitionRequest ReadRequest(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Ungültige Wechselanforderung.");
        var request = Parse<AdministratorTransitionRequest>(bytes.ToArray());
        CheckRequest(request);
        return request;
    }

    private static void CheckRequest(AdministratorTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Format != RequestFormat || request.Version != 1 ||
            request.Payload is null || request.Payload.Length is 0 or > GroupEnrollmentPackages.MaximumBytes ||
            request.NextSignature is null || request.NextSignature.Length is 0 or > 2048)
            throw new InvalidDataException("Ungültige öffentliche Wechselanforderung.");
    }

    public static byte[] Write(SignedAdministratorTransition transition)
    {
        CheckEnvelope(transition);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(transition, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Verwaltungswechseldatei ist zu groß.");
        return bytes;
    }

    public static SignedAdministratorTransition Read(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Ungültige Verwaltungswechseldatei.");
        var transition = Parse<SignedAdministratorTransition>(bytes.ToArray());
        CheckEnvelope(transition);
        return transition;
    }

    private static void CheckEnvelope(SignedAdministratorTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Payload is null || transition.Payload.Length is 0 or > GroupEnrollmentPackages.MaximumBytes ||
            transition.PreviousSignature is null || transition.PreviousSignature.Length is 0 or > 2048 ||
            transition.NextSignature is null || transition.NextSignature.Length is 0 or > 2048)
            throw new InvalidDataException("Ungültiger signierter Verwaltungswechsel.");
    }

    private static RSA OpenKey(byte[] bytes)
    {
        var key = RSA.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(bytes, out var read);
            if (read != bytes.Length || key.KeySize < 3072) throw new CryptographicException("Ungültiger Verwaltungsschlüssel.");
            return key;
        }
        catch { key.Dispose(); throw; }
    }

    private static byte[] SignatureInput(byte[] payload, string role)
        => Encoding.UTF8.GetBytes("SieEncode-Administrator-Transition/v1/" + role + "\0").Concat(payload).ToArray();

    private static T Parse<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 24 });
        CheckDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Verwaltungswechsel fehlt.");
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Doppeltes Feld im Verwaltungswechsel.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckDuplicates(child);
    }
}
