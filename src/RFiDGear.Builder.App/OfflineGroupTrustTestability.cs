using System.IO;
using System.Security.Cryptography;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyWindowsGroupTrust(List<string> checks)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SieEncode-WindowsTrust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var administrator = RSA.Create(3072);
            var library = Guid.NewGuid();
            var delegatePath = Path.Combine(directory, "delegate.bin");
            var delegateProtection = new WindowsGroupDelegateProtection(library, "synthetic-person");
            var delegateInvitation = new OfflineGroupDelegateStore(delegatePath, library, "synthetic-person", delegateProtection).Create();
            var delegateRestart = new OfflineGroupDelegateStore(delegatePath, library, "synthetic-person", new WindowsGroupDelegateProtection(library, "synthetic-person"));
            if (!delegateRestart.ExportPublic().SigningPublicKey.SequenceEqual(delegateInvitation.SigningPublicKey)) throw new Exception("Delegate key lost after restart.");
            var protectedDelegate = File.ReadAllBytes(delegatePath);
            Reject(() => new WindowsGroupDelegateProtection(library, "different-person").Unprotect(protectedDelegate));
            Reject(() => new WindowsGroupDelegateProtection(Guid.NewGuid(), "synthetic-person").Unprotect(protectedDelegate));
            Reject(() => new WindowsGroupTrustProtection(library).Unprotect(protectedDelegate));
            Reject(() => new WindowsGroupTrustProtection(library, signingAuthority: true).Unprotect(protectedDelegate));
            checks.Add("Delegatenschlüssel: Windows-DPAPI-Neuladen erfolgreich; falsche Person, Bibliothek sowie Trust-/Hauptverwalterzweck abgewiesen. Kein Zertifikatsspeichereingriff.");
            var adminPath = Path.Combine(directory, "administration.bin");
            var adminStore = new OfflineGroupAdministrationStore(adminPath, library, new WindowsGroupTrustProtection(library, signingAuthority: true));
            var publicAdministration = adminStore.Create();
            var original = GroupEnrollmentPackages.Validate(publicAdministration);
            var a = new string('A', 64); var b = new string('B', 64);
            var updatedAdministration = adminStore.UpdateGroups([new("lokal", "Lokal", [a, b]), new("lokal-2", "Lokal 2", [b])], original.Checkpoint);
            var adminRestart = new OfflineGroupAdministrationStore(adminPath, library, new WindowsGroupTrustProtection(library, signingAuthority: true));
            var exported = GroupEnrollmentPackages.Read(GroupEnrollmentPackages.Write(adminRestart.ExportPublic()));
            if (GroupEnrollmentPackages.Validate(exported).Policy.Revision != 2 ||
                !exported.AdministratorPublicKey.SequenceEqual(publicAdministration.AdministratorPublicKey))
                throw new InvalidOperationException("Verwaltungsstand oder Schlüssel nach Neustart verändert.");
            Reject(() => new WindowsGroupTrustProtection(library).Unprotect(File.ReadAllBytes(adminPath)));
            var before = File.ReadAllBytes(adminPath);
            try { adminRestart.UpdateGroups([], original.Checkpoint); throw new Exception("Stale administration update accepted."); }
            catch (ConcurrentFileChangeException) { }
            try { adminRestart.Create(); throw new Exception("Existing administrator overwritten."); }
            catch (ConcurrentFileChangeException) { }
            if (!before.SequenceEqual(File.ReadAllBytes(adminPath))) throw new Exception("Rejected administration update changed file.");
            var exportJson = System.Text.Encoding.UTF8.GetString(GroupEnrollmentPackages.Write(updatedAdministration));
            if (exportJson.Contains("PrivateKey", StringComparison.Ordinal)) throw new Exception("Public package contains private key field.");
            checks.Add("Gruppenverwaltung: eigener DPAPI-Signierspeicher, überlappende Gruppen signiert, öffentlicher Export nach Neustart gültig; falscher Schutzzweck und veraltete Änderung abgewiesen.");
            var uiDirectory = Path.Combine(directory, "administration-ui");
            var administrationWindow = new GroupAdministrationWindow(this, uiDirectory);
            try
            {
                VerifyAutomationContract(administrationWindow);
                var uiLibrary = administrationWindow.CreateAdministration();
                var uiGrid = DescendantsAndSelf(administrationWindow).OfType<System.Windows.Controls.DataGrid>().Single(table => table.Columns.Any(column => Equals(column.Header, "Gruppe")));
                var add = DescendantsAndSelf(administrationWindow).OfType<System.Windows.Controls.Button>().Single(button => Equals(button.Content, "Gruppe hinzufügen"));
                add.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                var draft = uiGrid.Items.Cast<GroupAdministrationDraft>().Single();
                var person = administrationWindow.AddPerson("Synthetische Person");
                using var deviceKey = RSA.Create(3072);
                using var firstDevice = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic workstation one", deviceKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
                using var secondDevice = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=Synthetic workstation two", deviceKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
                var firstInvitation = CertificateVaultService.ExportInvitation(firstDevice, "Testrechner 1");
                administrationWindow.AddDevice(person.Id, firstInvitation);
                administrationWindow.AddDevice(person.Id, CertificateVaultService.ExportInvitation(secondDevice, "Testrechner 2"));
                try { administrationWindow.AddDevice(person.Id, firstInvitation); throw new Exception("Duplicate device accepted by UI."); }
                catch (InvalidOperationException) { }
                draft.Name = "Lokal"; draft.Members.Add(person.Id);
                administrationWindow.SaveGroups();
                administrationWindow.LoadAdministration(uiLibrary);
                var reread = uiGrid.Items.Cast<GroupAdministrationDraft>().Single();
                if (reread.Name != "Lokal" || reread.Members.Single() != person.Id) throw new Exception("Administrative UI lost group members.");
                var uiStore = new OfflineGroupAdministrationStore(Path.Combine(uiDirectory, "Administrations", uiLibrary.ToString("N") + ".admin.bin"), uiLibrary, new WindowsGroupTrustProtection(uiLibrary, signingAuthority: true));
                var external = GroupEnrollmentPackages.Validate(uiStore.ExportPublic());
                if (external.Policy.Version != 2 || external.Policy.People.Single().Devices.Length != 2 ||
                    OfflineGroupDirectory.ResolveRecipients(external.Policy, draft.Id, DateTimeOffset.UtcNow).Length != 2)
                    throw new Exception("Person/device UI did not persist a usable signed directory.");
                var delegationInvitation = new GroupDelegateInvitation(uiLibrary, person.Id, administrator.ExportSubjectPublicKeyInfo());
                Reject(() => administrationWindow.GrantInvitation(delegationInvitation, new string('0', 64), [draft.Id]));
                administrationWindow.GrantInvitation(delegationInvitation, GroupDelegationTransport.InvitationFingerprint(delegationInvitation), [draft.Id]);
                external = GroupEnrollmentPackages.Validate(uiStore.ExportPublic());
                if (external.Policy.Delegations.Single().PersonId != person.Id || external.Policy.Delegations.Single().GroupIds.Single() != draft.Id)
                    throw new Exception("Administrative UI did not persist scoped grant.");
                var publicChange = OfflineGroupDelegationService.Sign(new(OfflineGroupDelegationService.Format, 1, uiLibrary,
                    external.Policy.Delegations.Single().Id, external.Checkpoint, [new(draft.Id, "Delegiert geändert", [person.Id])]), administrator);
                uiStore.ApplyDelegatedChange(GroupDelegationTransport.ReadChange(GroupDelegationTransport.WriteChange(publicChange)), external.Checkpoint);
                try { administrationWindow.SaveGroups(); throw new Exception("Stale UI overwrote group policy."); }
                catch (ConcurrentFileChangeException) { }
                administrationWindow.LoadAdministration(uiLibrary);
            }
            finally { administrationWindow.Close(); }
            checks.Add("Gruppen-Verwaltungsdialog: Person mit zwei Geräten, signiertes Verzeichnis, Vollmacht nur mit bestätigtem Fingerabdruck und Gruppe; delegierte Änderung über Transport geprüft; veralteter Entwurf abgewiesen.");
            var key = administrator.ExportSubjectPublicKeyInfo();
            var policy = new OfflineGroupPolicy(OfflineGroupPolicyService.Format, 1, library, 1, []);
            var signed = OfflineGroupPolicyService.Sign(policy, administrator);
            var registrationDirectory = Path.Combine(directory, "registration");
            var package = new GroupEnrollmentPackage(GroupEnrollmentPackages.Format, 1, library, key, signed);
            var registration = new GroupEnrollmentWindow(this, package, registrationDirectory);
            try
            {
                VerifyAutomationContract(registration);
                var fingerprint = OfflineGroupTrustStore.AdministratorFingerprint(key);
                try { registration.Register(fingerprint, false); throw new Exception("Unconfirmed registration accepted."); }
                catch (InvalidDataException) { }
                Reject(() => registration.Register(new string('0', 64), true));
                if (Directory.Exists(registrationDirectory)) throw new Exception("Rejected enrollment wrote local state.");
                registration.Register(fingerprint.ToLowerInvariant(), true);
                try { registration.Register(fingerprint, true); throw new Exception("Existing enrollment overwritten."); }
                catch (ConcurrentFileChangeException) { }
                var stored = new OfflineGroupTrustStore(Path.Combine(registrationDirectory, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library)).LoadOffline();
                if (stored.Policy.LibraryId != library) throw new Exception("Wrong library enrolled.");
            }
            finally { registration.Close(); }
            checks.Add("Gruppen-Erstregistrierung: ohne Bestätigung/falscher Fingerabdruck ohne Schreibzugriff; bestätigte Registrierung Windows-geschützt lesbar; erneute Registrierung überschreibt nicht.");
            var path = Path.Combine(directory, "trust.bin");
            new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library))
                .Enroll(key, OfflineGroupTrustStore.AdministratorFingerprint(key), signed);
            var reloaded = new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library)).LoadOffline();
            if (reloaded.Policy.LibraryId != library || reloaded.Policy.Revision != 1)
                throw new InvalidOperationException("Windows-Vertrauensspeicher nicht wieder lesbar.");
            var protectedBytes = File.ReadAllBytes(path);
            Reject(() => new WindowsGroupTrustProtection(Guid.NewGuid()).Unprotect(protectedBytes));
            protectedBytes[^1] ^= 1;
            File.WriteAllBytes(path, protectedBytes);
            Reject(() => new OfflineGroupTrustStore(path, library, new WindowsGroupTrustProtection(library)).LoadOffline());
            checks.Add("Offline-Gruppen: echter Windows-DPAPI-Roundtrip im aktuellen Benutzer; falsche Bibliotheksbindung und manipulierte Datei abgewiesen. Keine Zweitbenutzerabnahme.");
        }
        finally { Directory.Delete(directory, true); }

        static void Reject(Action action)
        {
            try { action(); }
            catch (CryptographicException) { return; }
            throw new InvalidOperationException("Ungültiger Windows-Vertrauensspeicher wurde akzeptiert.");
        }
    }
}
