using System.Security.Cryptography;
using System.Text;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class WindowsGroupSuccessorProtection(Guid libraryId) : ILocalGroupTrustProtection
{
    private readonly byte[] entropy = libraryId != Guid.Empty
        ? Encoding.UTF8.GetBytes("SieEncode.OfflineGroupSuccessor.v1:" + libraryId.ToString("D"))
        : throw new ArgumentException("Bibliotheks-ID fehlt.", nameof(libraryId));
    public byte[] Protect(byte[] plaintext) => ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] protectedState) => ProtectedData.Unprotect(protectedState, entropy, DataProtectionScope.CurrentUser);
}
