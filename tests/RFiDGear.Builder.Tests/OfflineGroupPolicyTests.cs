using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RFiDGear.Builder.Core;

internal static class OfflineGroupPolicyTests
{
    public static void Verify()
    {
        using var admin = RSA.Create(3072);
        using var stranger = RSA.Create(3072);
        using var publicAdmin = RSA.Create();
        publicAdmin.ImportSubjectPublicKeyInfo(admin.ExportSubjectPublicKeyInfo(), out _);
        var library = Guid.NewGuid();
        var a = new string('A', 64); var b = new string('B', 64);
        var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1,
            [new("lokal", "Lokal", [a, b]), new("lokal-2", "Lokal 2", [b])]);
        var signed = OfflineGroupPolicyService.Sign(policy, admin);
        var verified = OfflineGroupPolicyService.Verify(signed, publicAdmin, library);
        var package = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, library, admin.ExportSubjectPublicKeyInfo(), signed);
        var packageBytes = GroupEnrollmentPackages.Write(package);
        var decoded = GroupEnrollmentPackages.Read(packageBytes);
        if (decoded.LibraryId != library) throw new Exception("Enrollment transport lost library binding.");
        Reject(() => GroupEnrollmentPackages.Validate(package with { LibraryId = Guid.NewGuid() }));
        Reject(() => GroupEnrollmentPackages.Validate(package with { AdministratorPublicKey = stranger.ExportSubjectPublicKeyInfo() }));
        Reject(() => GroupEnrollmentPackages.Read(new byte[GroupEnrollmentPackages.MaximumBytes + 1]));
        Reject(() => GroupEnrollmentPackages.Read(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(packageBytes).Insert(1, "\"Version\":1,"))));
        if (verified.Policy.Groups[1].Members.Single() != b) throw new Exception("Overlapping membership lost.");
        OfflineGroupPolicyService.Verify(signed, publicAdmin, library, verified.Checkpoint);
        Reject(() => OfflineGroupPolicyService.Verify(signed, stranger, library));
        Reject(() => OfflineGroupPolicyService.Verify(signed, publicAdmin, Guid.NewGuid()));
        var changed = signed.Payload.ToArray(); changed[0] ^= 1;
        Reject(() => OfflineGroupPolicyService.Verify(new(changed, signed.Signature), publicAdmin, library));
        var changedSignature = signed.Signature.ToArray(); changedSignature[0] ^= 1;
        Reject(() => OfflineGroupPolicyService.Verify(new(signed.Payload, changedSignature), publicAdmin, library));
        var next = OfflineGroupPolicyService.Verify(OfflineGroupPolicyService.Sign(policy with { Revision = 2 }, admin), publicAdmin, library, verified.Checkpoint);
        Reject(() => OfflineGroupPolicyService.Verify(signed, publicAdmin, library, next.Checkpoint));
        var conflict = OfflineGroupPolicyService.Sign(policy with { Groups = [] }, admin);
        Reject(() => OfflineGroupPolicyService.Verify(conflict, publicAdmin, library, verified.Checkpoint));
        Reject(() => OfflineGroupPolicyService.Verify(signed, publicAdmin, library, new(0, "")));
        Reject(() => OfflineGroupPolicyService.Sign(policy with { Groups = [new("lokal", "Lokal", [a, a])] }, admin));
        Reject(() => OfflineGroupPolicyService.Sign(policy with { Groups = [policy.Groups[0], policy.Groups[0]] }, admin));
        Reject(() => OfflineGroupPolicyService.Sign(policy with { Version = 99 }, admin));
        using var weak = RSA.Create(2048);
        Reject(() => OfflineGroupPolicyService.Sign(policy, weak));
        Reject(() => OfflineGroupPolicyService.Verify(new(new byte[1024 * 1024 + 1], signed.Signature), publicAdmin, library));
        var json = Encoding.UTF8.GetString(signed.Payload);
        foreach (var invalid in new[] { json.Replace("\"Revision\":1", "\"Revision\":1,\"Revision\":2"), json.Insert(1, "\"Unrecognized\":true,"), json.Replace("\"Groups\":[", "\"Groups\":null,\"Extra\":[") })
        {
            var bytes = Encoding.UTF8.GetBytes(invalid);
            var malformed = new SignedGroupPolicy(bytes, admin.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
            Reject(() => OfflineGroupPolicyService.Verify(malformed, publicAdmin, library));
        }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is CryptographicException or InvalidDataException or JsonException or ArgumentException) { return; }
        throw new Exception("Invalid or unauthorized policy was accepted.");
    }
}
