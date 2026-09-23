using System.Security.Cryptography;
using System.Text;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed class WindowsGroupDelegateProtection : ILocalGroupTrustProtection
{
    private readonly byte[] entropy;
    public WindowsGroupDelegateProtection(Guid libraryId, string personId)
    {
        if (libraryId == Guid.Empty || string.IsNullOrWhiteSpace(personId)) throw new ArgumentException("Bibliothek und Person erforderlich.");
        entropy = Encoding.UTF8.GetBytes("SieEncode.OfflineGroupDelegate.v1:" + libraryId.ToString("D") + ":" + personId);
    }
    public byte[] Protect(byte[] plaintext) => ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] state) => ProtectedData.Unprotect(state, entropy, DataProtectionScope.CurrentUser);
}
