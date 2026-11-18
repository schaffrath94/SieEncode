using System.IO;
using System.Security.Cryptography;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void VerifyGroupDelegateWindow(List<string> checks, string directory)
    {
        var library = Guid.NewGuid();
        var admin = new OfflineGroupAdministrationStore(Path.Combine(directory, "delegate-test-admin.bin"), library,
            new WindowsGroupTrustProtection(library, signingAuthority: true));
        var created = admin.Create();
        var package = admin.UpdateDirectory([new("alice", "Alice", true, []), new("bob", "Bob", true, [])],
            [new("lokal", "Lokal", ["alice"]), new("other", "Nicht freigegeben", ["bob"])], GroupEnrollmentPackages.Validate(created).Checkpoint);
        var trust = new OfflineGroupTrustStore(Path.Combine(directory, library.ToString("N") + ".trust.bin"), library, new WindowsGroupTrustProtection(library));
        trust.Enroll(package.AdministratorPublicKey, OfflineGroupTrustStore.AdministratorFingerprint(package.AdministratorPublicKey), package.Policy);
        var window = new GroupDelegateWindow(this, directory);
        try
        {
            VerifyAutomationContract(window);
            window.LoadLibrary(library); window.LoadPerson("alice");
            var people = DescendantsAndSelf(window).OfType<ComboBox>().Single(box => box.Name == "People");
            people.SelectedIndex = 1;
            try { window.GetInvitation(true); throw new Exception("Invitation created for a previously selected person."); }
            catch (InvalidOperationException) { }
            window.LoadPerson("alice");
            try { window.GetInvitation(false); throw new Exception("Opening delegate UI created a private key."); }
            catch (InvalidOperationException) { }
            var invitation = GroupDelegationTransport.ReadInvitation(GroupDelegationTransport.WriteInvitation(window.GetInvitation(true)));
            package = admin.GrantDelegation(invitation, GroupDelegationTransport.InvitationFingerprint(invitation), "alice-lokal", ["lokal"], GroupEnrollmentPackages.Validate(package).Checkpoint);
            trust.AcceptPackageUpdate(package, trust.LoadOffline().Checkpoint);
            window.LoadPerson("alice");
            var grid = DescendantsAndSelf(window).OfType<DataGrid>().Single();
            var group = grid.Items.Cast<GroupAdministrationDraft>().Single();
            if (group.Id != "lokal") throw new Exception("Delegate UI exposes unauthorized group.");
            group.Name = "Lokal bearbeitet";
            var memberList = DescendantsAndSelf(window).OfType<ListBox>().Single();
            memberList.SelectedItems.Add(memberList.Items[1]); // Real selection event updates membership draft.
            var change = GroupDelegationTransport.ReadChange(GroupDelegationTransport.WriteChange(window.CreateChange()));
            package = admin.ApplyDelegatedChange(change, GroupEnrollmentPackages.Validate(package).Checkpoint);
            var updated = GroupEnrollmentPackages.Validate(package);
            if (updated.Policy.Groups[0].Members.Length != 2 || updated.Policy.Groups[0].Name != "Lokal bearbeitet" || updated.Policy.Groups[1].Name != "Nicht freigegeben")
                throw new Exception("Delegate UI exchange lost edits or changed another group.");
            trust.AcceptPackageUpdate(package, trust.LoadOffline().Checkpoint);
            try { window.CreateChange(); throw new Exception("Stale delegate draft signed."); }
            catch (CryptographicException) { }
            window.LoadPerson("alice");
            grid.Items.Cast<GroupAdministrationDraft>().Single().Name = "Nach Entzug";
            package = admin.UpdateDelegations([], updated.Checkpoint);
            trust.AcceptPackageUpdate(package, trust.LoadOffline().Checkpoint);
            try { window.CreateChange(); throw new Exception("Revoked delegate signed."); }
            catch (CryptographicException) { }
            window.LoadPerson("alice");
            if (grid.Items.Count != 0) throw new Exception("Revoked scope still editable after reload.");
        }
        finally { window.Close(); }
        checks.Add("Delegaten-Arbeitsplatz: explizite Schlüsselerzeugung, Einladung/Freigabe, nur erlaubte Gruppe, Mitgliederauswahl, signierter Dateiaustausch und Entzug gegen alten Entwurf geprüft. Synthetisch, ein Windows-Benutzer.");
    }
}
