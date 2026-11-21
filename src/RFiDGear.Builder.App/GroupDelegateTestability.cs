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
            var people = DescendantsAndSelf(window).OfType<ComboBox>().Single(box => box.Name == "People");
            var invitationButton = DescendantsAndSelf(window).OfType<Button>()
                .Single(button => button.Content?.ToString() == "Einladung an Hauptverwaltung exportieren");
            var changeButton = DescendantsAndSelf(window).OfType<Button>()
                .Single(button => button.Content?.ToString() == "Änderung signieren und exportieren");
            if (people.Items.Count != 2 || people.SelectedItem is not null || invitationButton.IsEnabled || changeButton.IsEnabled)
                throw new Exception("Delegate auto-load selected an ambiguous person or enabled an unapproved action.");
            if (window.Content is not DockPanel)
                throw new Exception("Delegate error display no longer stays visible outside the scrolling content.");
            window.LoadLibrary(library); window.LoadPerson("alice");
            if (!invitationButton.IsEnabled || changeButton.IsEnabled)
                throw new Exception("No grant yet: invitation must be available and group change must remain disabled.");
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
            if (!changeButton.IsEnabled)
                throw new Exception("Active delegation should enable only assigned group changes.");
            var reopened = new GroupDelegateWindow(this, directory);
            try
            {
                var reloadedPerson = DescendantsAndSelf(reopened).OfType<ComboBox>().Single(box => box.Name == "People");
                var reloadedChange = DescendantsAndSelf(reopened).OfType<Button>()
                    .Single(button => button.Content?.ToString() == "Änderung signieren und exportieren");
                if (reloadedPerson.SelectedItem is null || reloadedChange.IsEnabled == false ||
                    DescendantsAndSelf(reopened).OfType<DataGrid>().Single().Items.Count != 1)
                    throw new Exception("Unique existing delegate key did not restore the approved local person.");
            }
            finally { reopened.Close(); }
            var grid = DescendantsAndSelf(window).OfType<DataGrid>().Single();
            var group = grid.Items.Cast<GroupAdministrationDraft>().Single();
            if (group.Id != "lokal") throw new Exception("Delegate UI exposes unauthorized group.");
            group.Name = "Lokal bearbeitet";
            var memberList = DescendantsAndSelf(window).OfType<ListBox>().Single();
            memberList.SelectedItems.Add(memberList.Items[1]); // Real selection event updates membership draft.
            var change = GroupDelegationTransport.ReadChange(GroupDelegationTransport.WriteChange(window.CreateChange()));
            package = admin.ApplyDelegatedChange(change, GroupEnrollmentPackages.Validate(package).Checkpoint);
            var updated = GroupEnrollmentPackages.Validate(package);
            var before = trust.LoadOffline().Policy;
            var preview = GroupAdministrationWindow.DescribeDelegatedChanges(before, updated.Policy);
            if (preview != "Lokal (lokal)\nNeuer Name: Lokal bearbeitet\nHinzufügen: Bob (bob)")
                throw new Exception("Delegate approval preview does not identify the affected group and member.");
            var reordered = updated.Policy with { Groups = updated.Policy.Groups.Reverse()
                .Select(g => g with { Members = g.Members.Reverse().ToArray() }).ToArray() };
            if (GroupAdministrationWindow.DescribeDelegatedChanges(updated.Policy, reordered).Length != 0)
                throw new Exception("Delegate approval preview treats ordering as an access change.");
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
        checks.Add("Delegaten-Arbeitsplatz: eindeutiger Gruppenstand automatisch geladen, mehrere Personen NICHT automatisch ausgewählt, Einladung vor Vollmacht möglich, Änderungen erst mit aktiver Vollmacht, Fehlerbereich ohne Scrollsuche; Schlüsselerzeugung ausschließlich explizit. Synthetisch, ein Windows-Benutzer.");
        checks.Add("Delegaten-Arbeitsplatz: nur erlaubte Gruppe, Mitgliederauswahl, signierter Dateiaustausch und Entzug gegen alten Entwurf geprüft. Keine Zweitbenutzer-/Hardwareabnahme.");
    }
}
