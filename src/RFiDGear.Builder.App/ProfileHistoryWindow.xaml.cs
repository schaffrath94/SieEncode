using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class ProfileHistoryWindow : Window
{
    private readonly ProfileHistoryService history;
    private readonly string profileId;
    private ProfileHistoryRow? selected;

    public ProfileHistoryWindow(ProfileStore store, string profileId)
    {
        InitializeComponent();
        Title = $"Profilhistorie · SieEncode Studio {AppIdentity.DisplayVersion}";
        MainWindow.InitializeAutomationContract(this, "Window.ProfileHistory");
        history = new ProfileHistoryService(store);
        this.profileId = profileId;
        LoadHistory();
    }

    public string RestoredPath { get; private set; } = "";

    public void RunSelfTest()
    {
        if (!Title.Contains(AppIdentity.DisplayVersion, StringComparison.Ordinal)) throw new InvalidOperationException("Historienfenster zeigt die Studio-Version nicht an.");
        MainWindow.VerifyAutomationContract(this);
        if (HistoryGrid is null || DiffGrid is null || RestoreButton is null || NewVersion is null) throw new InvalidOperationException("Historienfenster ist unvollständig.");
    }

    private void LoadHistory()
    {
        var rows = history.List(profileId).Select(entry => new ProfileHistoryRow(entry)).ToArray();
        HistoryGrid.ItemsSource = rows;
        HistoryIntro.Text = $"Kundenprofil {profileId} · {rows.Count(row => !row.Entry.IsCurrent)} archivierte Vorgängerversion(en). Beschädigte oder veränderte Archive werden nicht zur Wiederherstellung freigegeben.";
        HistoryGrid.SelectedItem = rows.FirstOrDefault(row => !row.Entry.IsCurrent) ?? rows.FirstOrDefault();
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selected = HistoryGrid.SelectedItem as ProfileHistoryRow;
        DiffGrid.ItemsSource = null;
        RestoreButton.IsEnabled = selected is { Entry.IsCurrent: false, Entry.IntegrityValid: true };
        if (selected is null) { DiffSummary.Text = "Bitte einen Historienstand auswählen."; return; }
        if (!selected.Entry.IntegrityValid) { DiffSummary.Text = "Integritätsfehler: " + selected.Entry.Issue; return; }
        if (selected.Entry.IsCurrent) { DiffSummary.Text = "Dies ist der aktuelle Profilstand. Für einen Vergleich bitte einen archivierten Vorgänger auswählen."; return; }
        try
        {
            var report = history.CompareWithCurrent(selected.Entry);
            DiffGrid.ItemsSource = report.Items;
            DiffSummary.Text = report.Identical
                ? "Historienstand und aktueller Stand sind fachlich identisch."
                : $"{report.Added} hinzugefügt · {report.Removed} entfernt · {report.Changed} geändert · Schlüsselwerte sind nicht Bestandteil von Profilen oder Vergleich.";
        }
        catch (Exception exception) { DiffSummary.Text = exception.Message; RestoreButton.IsEnabled = false; }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (selected is null) throw new InvalidOperationException("Bitte einen gültigen Historienstand auswählen.");
            var newVersion = NewVersion.Text.Trim();
            var report = history.CompareWithCurrent(selected.Entry);
            if (MessageBox.Show(this,
                $"Historienversion {selected.Entry.Version} wird als neue Version '{newVersion}' aktiviert.\n\n" +
                $"Fachlicher Vergleich: {report.Added} hinzugefügt, {report.Removed} entfernt, {report.Changed} geändert.\n" +
                "Der aktuelle Stand wird archiviert. Bestehende Projekte bleiben an ihren bisherigen Fingerabdruck gebunden und müssen bei Bedarf bewusst migriert werden.\n\nFortfahren?",
                "Profilversion wiederherstellen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var result = history.RestoreAsNewVersion(selected.Entry, newVersion);
            RestoredPath = result.ProfilePath;
            MessageBox.Show(this, $"Profil wurde als Version {result.Version} wiederhergestellt.\nVorgänger des neuen Stands: {result.PreviousVersion}\n\n{result.ProfilePath}", "Profilhistorie", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Profilhistorie", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class ProfileHistoryRow
{
    public ProfileHistoryRow(ProfileHistoryEntry entry) => Entry = entry;
    public ProfileHistoryEntry Entry { get; }
    public string State => Entry.IsCurrent ? "Aktuell" : "Archiv";
    public string Version => Entry.Version;
    public string Modified => Entry.ModifiedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "–";
    public string Digest => Entry.Digest.Length >= 12 ? Entry.Digest[..12] : "–";
    public string Integrity => Entry.IntegrityValid ? "OK" : "FEHLER";
    public string Issue => Entry.Issue;
    public string Source => Entry.IntegrityValid ? Entry.Path : Entry.Path + " · " + Entry.Issue;
}
