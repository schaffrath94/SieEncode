using System.Security.Cryptography;
using System.Text;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>Authenticates the local trust cache for this Windows user. Does not protect against that user's own code.</summary>
internal sealed class WindowsGroupTrustProtection(Guid libraryId) : ILocalGroupTrustProtection
{
    private readonly byte[] entropy = libraryId != Guid.Empty
        ? Encoding.UTF8.GetBytes("SieEncode.OfflineGroupTrust.v1:" + libraryId.ToString("D"))
        : throw new ArgumentException("Bibliotheks-ID fehlt.", nameof(libraryId));

    public byte[] Protect(byte[] plaintext)
        => ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedState)
        => ProtectedData.Unprotect(protectedState, entropy, DataProtectionScope.CurrentUser);
}
