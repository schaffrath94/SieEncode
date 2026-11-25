using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

internal sealed partial class GroupAdministrationWindow
{
    private sealed record DelegationLabel(string Id, string Label);
    private readonly ListBox delegationList = new() { MinHeight = 50, MaxHeight = 130, DisplayMemberPath = nameof(DelegationLabel.Label) };

    private void AddDelegationControls(Panel root)
    {
        var panel = new StackPanel();
        panel.Children.Add(Note("Wählen Sie oben die Gruppen aus, die diese Person verwalten darf. Für mehrere Gruppen halten Sie Strg gedrückt. Beim Freigeben wird die Vollmacht signiert und gespeichert."));
        panel.Children.Add(delegationList);
        panel.Children.Add(Action("Verwaltungseinladung für ausgewählte Gruppen freigeben", ImportDelegation));
        panel.Children.Add(Action("Ausgewählte Vollmacht entziehen", RevokeSelectedDelegation));
        panel.Children.Add(Action("Signierte Gruppenänderung prüfen und übernehmen", ImportDelegatedChange));
        root.Children.Add(new Expander { Header = "Gruppenverwalter und Vollmachten", Content = panel });
    }

    private void RequireSavedAdministration()
    {
        RequireStore(); CommitEdits();
        if (dirty) throw new InvalidOperationException("Bitte offene Personen-/Gruppenänderungen zuerst signiert speichern.");
    }

    private void RefreshDelegations(OfflineGroupPolicy policy)
        => delegationList.ItemsSource = policy.Delegations.Select(d => new DelegationLabel(d.Id,
            $"{policy.People.Single(p => p.Id == d.PersonId).Name} · {(d.Enabled ? "aktiv" : "entzogen")} · " +
            string.Join(", ", d.GroupIds.Select(id => DelegationGroupLabel(groups.Single(g => g.Id == id)))))).ToArray();

    internal string DelegationGroupLabel(GroupAdministrationDraft group)
    {
        var ambiguous = groups.Any(other => other.Id != group.Id &&
            string.Equals(other.Name.Trim(), group.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        return ambiguous ? $"{group.Name} ({group.Id})" : group.Name;
    }

    internal void GrantInvitation(GroupDelegateInvitation invitation, string fingerprint, string[] groupIds)
    {
        RequireSavedAdministration();
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary.Value);
        store!.GrantDelegation(invitation, fingerprint, "vollmacht-" + Guid.NewGuid().ToString("N"), groupIds, checkpoint!);
        LoadAdministration(currentLibrary!.Value);
    }

    private void ImportDelegation()
    {
        RequireSavedAdministration();
        var selected = grid.SelectedItems.Cast<GroupAdministrationDraft>().ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Bitte die freizugebenden Gruppen auswählen.");
        var dialog = new OpenFileDialog { Filter = "Verwaltungseinladung (*.group-delegate.json)|*.group-delegate.json" };
        if (dialog.ShowDialog(this) != true) return;
        var invitation = GroupDelegationTransport.ReadInvitation(GroupDelegationTransport.ReadFile(dialog.FileName));
        var person = people.SingleOrDefault(p => p.Id == invitation.PersonId);
        if (invitation.LibraryId != currentLibrary || person is null) throw new InvalidDataException("Einladung gehört nicht zu einer Person dieser Bibliothek.");
        var scope = string.Join(", ", selected.Select(DelegationGroupLabel));
        var fingerprint = TextEntryPrompt.Show(this, "Verwaltungsvollmacht bestätigen",
            $"Person: {person.Name}\nGruppen: {scope}\nDen unabhängig erhaltenen SHA-256-Verwaltungsfingerabdruck eingeben:", "");
        if (fingerprint is null) return;
        GrantInvitation(invitation, fingerprint.Trim().ToUpperInvariant(), selected.Select(g => g.Id).ToArray());
        status.Text += $"\n{person.Name} darf folgende Gruppen verwalten: {scope}. Für andere Arbeitsplätze wählen Sie „Gespeicherten Stand öffentlich exportieren“. Dort muss der neue Stand importiert werden.";
        status.BringIntoView();
    }

    private void RevokeSelectedDelegation()
    {
        RequireSavedAdministration();
        if (delegationList.SelectedItem is not DelegationLabel selected) throw new InvalidOperationException("Bitte eine Vollmacht auswählen.");
        if (MessageBox.Show(this, $"{selected.Label}\n\nVollmacht für zukünftige Änderungen entziehen? Den neuen Stand anschließend weitergeben. Offline-Kopien erfahren den Entzug erst nach Aktualisierung.", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var current = GroupEnrollmentPackages.Validate(store!.ExportPublic());
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary.Value);
        store.UpdateDelegations(current.Policy.Delegations.Select(d => d.Id == selected.Id ? d with { Enabled = false } : d).ToArray(), checkpoint!);
        LoadAdministration(currentLibrary!.Value);
    }

    private void ImportDelegatedChange()
    {
        RequireSavedAdministration();
        var dialog = new OpenFileDialog { Filter = "Signierte Gruppenänderung (*.group-change.json)|*.group-change.json" };
        if (dialog.ShowDialog(this) != true) return;
        var change = GroupDelegationTransport.ReadChange(GroupDelegationTransport.ReadFile(dialog.FileName));
        var current = GroupEnrollmentPackages.Validate(store!.ExportPublic());
        if (current.Checkpoint != checkpoint) throw new InvalidOperationException("Verwaltungsstand wurde geändert. Bitte neu laden.");
        var next = OfflineGroupDelegationService.Apply(change, current);
        var preview = DescribeDelegatedChanges(current.Policy, next);
        if (preview.Length == 0) throw new InvalidOperationException("Der Antrag enthält keine Änderungen an Gruppennamen oder Mitgliedschaften.");
        if (MessageBox.Show(this, preview + "\n\nDiese geprüften Änderungen signiert übernehmen?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        using var lease = CentralGroupPolicyWorkflow.Acquire(currentLibrary!.Value);
        CentralGroupPolicyWorkflow.RequireOfflinePolicyEdit(currentLibrary.Value);
        store.ApplyDelegatedChange(change, checkpoint!);
        LoadAdministration(currentLibrary!.Value);
    }

    internal static string DescribeDelegatedChanges(OfflineGroupPolicy current, OfflineGroupPolicy next)
    {
        var original = current.Groups.ToDictionary(group => group.Id);
        var people = current.People.ToDictionary(person => person.Id);
        var changes = new List<string>();
        foreach (var group in next.Groups)
        {
            var previous = original[group.Id];
            var lines = new List<string>();
            if (previous.Name != group.Name) lines.Add($"Neuer Name: {group.Name}");
            foreach (var id in group.Members.Except(previous.Members)) lines.Add($"Hinzufügen: {people[id].Name} ({id})");
            foreach (var id in previous.Members.Except(group.Members)) lines.Add($"Entfernen: {people[id].Name} ({id})");
            if (lines.Count > 0) changes.Add($"{previous.Name} ({group.Id})\n" + string.Join("\n", lines));
        }
        return string.Join("\n\n", changes);
    }
}
