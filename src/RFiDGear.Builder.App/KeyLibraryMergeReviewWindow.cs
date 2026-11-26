using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

/// <summary>Transient metadata-only review. All choices remain provisional
/// until the caller recomputes and explicitly approves the exact preview.</summary>
internal sealed class KeyLibraryMergeReviewWindow : Window
{
    private sealed record Choice(Guid SourceId, string Text);
    private readonly List<(KeyLibraryMergeIssue Issue, ComboBox Box)> selections = [];
    internal KeyLibraryMergeDecision[] Decisions { get; private set; } = [];

    internal KeyLibraryMergeReviewWindow(Window owner, KeyLibraryMergeReview review,
        IReadOnlyDictionary<Guid, string> sourceNames)
    {
        Owner = owner;
        Title = "Bibliotheksbestände vergleichen";
        Width = 860; MaxHeight = 760; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(Note("Geprüfte verschlüsselte Bestände · keine Schlüsselwerte werden angezeigt."));
        foreach (var source in review.Preview.Inventory.Sources)
            panel.Children.Add(Note($"{sourceNames[source.SourceId]} · Katalog {source.CatalogRevision} · " +
                $"{source.CustomerCount} Kunden · {source.KeyVersionCount} Schlüsselversionen"));
        panel.Children.Add(Note($"Eindeutige Ergänzungen: {review.Preview.AddedCustomers} Kunden, " +
            $"{review.Preview.AddedKeyVersions} Schlüsselversionen. Offene Konflikte: {review.Details.Length}."));
        var hard = review.Details.Any(d => d.Options.Length == 0);
        if (hard)
            panel.Children.Add(Note("Nicht entscheidbare Konflikte vorhanden. Kein Upload möglich. " +
                "Alle Quellen unverändert aufbewahren und die Historie gesondert klären."));
        foreach (var detail in review.Details)
        {
            var issue = detail.Issue;
            panel.Children.Add(Note($"{Kind(issue.Kind)} · " +
                (issue.KeyId is Guid key ? $"Schlüssel {key:D}, v{issue.Version}" :
                    issue.CustomerId is Guid customer ? $"Kunde {customer:D}" : "Bibliotheksbestand")));
            if (detail.Options.Length == 0)
            {
                panel.Children.Add(Note("Gesperrt · " + string.Join(", ", issue.SourceIds.Select(id => sourceNames[id]))));
                continue;
            }
            foreach (var option in detail.Options.Where(o => !o.Allowed))
                panel.Children.Add(Note($"{sourceNames[option.SourceId]}: {option.Label} (nicht zulässig)"));
            var box = new ComboBox
            {
                DisplayMemberPath = nameof(Choice.Text),
                ItemsSource = detail.Options.Where(o => o.Allowed)
                    .Select(o => new Choice(o.SourceId, $"{sourceNames[o.SourceId]}: {o.Label}")).ToArray(),
                MinWidth = 350, Margin = new Thickness(0, 0, 0, 8)
            };
            selections.Add((issue, box));
            panel.Children.Add(box);
        }
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var next = new Button { Content = "Entscheidungen prüfen", IsDefault = true, IsEnabled = !hard, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Schließen", IsCancel = true, Margin = new Thickness(4) };
        next.Click += (_, _) =>
        {
            if (selections.Any(s => s.Box.SelectedItem is not Choice))
            {
                MessageBox.Show(this, "Bitte jeden Konflikt ausdrücklich entscheiden.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Decisions = selections.Select(s => new KeyLibraryMergeDecision(s.Issue.Kind,
                s.Issue.CustomerId, s.Issue.KeyId, s.Issue.Version, ((Choice)s.Box.SelectedItem).SourceId)).ToArray();
            DialogResult = true;
        };
        actions.Children.Add(next); actions.Children.Add(cancel); panel.Children.Add(actions);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static TextBlock Note(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private static string Kind(KeyLibraryMergeIssueKind kind) => kind switch
    {
        KeyLibraryMergeIssueKind.CustomerMetadata => "Kundenbezeichnung",
        KeyLibraryMergeIssueKind.KeyMetadata => "Schlüsselmetadaten",
        KeyLibraryMergeIssueKind.Status => "Schlüsselstatus",
        KeyLibraryMergeIssueKind.SecretValue => "Widersprüchliche Schlüsselwerte (gleiche Version)",
        KeyLibraryMergeIssueKind.ImmutableIdentity => "Unveränderliche Schlüsselidentität",
        KeyLibraryMergeIssueKind.MissingBaseVersion => "Fehlende historische Schlüsselversion",
        KeyLibraryMergeIssueKind.MissingBaseCustomer => "Fehlender historischer Kunde",
        KeyLibraryMergeIssueKind.OlderSource => "Älterer Quellbestand",
        KeyLibraryMergeIssueKind.DuplicateCustomerName => "Doppelte Kundenbezeichnung",
        _ => "Nicht automatisch auflösbarer Konflikt: " + kind
    };
}
