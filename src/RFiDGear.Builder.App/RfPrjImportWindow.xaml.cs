using System.Diagnostics;
using System.IO;
using System.Windows;
using RFiDGear.Builder.Core;
using RFiDGear.Builder.RfPrj;

namespace RFiDGear.Builder.App;

public partial class RfPrjImportWindow : Window
{
    private readonly RfPrjProfileImportService importer;
    private readonly RfPrjProfileImportReport report;
    private readonly string reportPath;
    private readonly RfPrjImportApplicationRow[] rows;

    public RfPrjImportWindow(RfPrjProfileImportService importer, RfPrjProfileImportFiles files)
    {
        InitializeComponent();
        Title = $"rfPrj-Profilimport · SieEncode Studio {AppIdentity.DisplayVersion}";
        MainWindow.InitializeAutomationContract(this, "Window.RfPrjImport");
        this.importer = importer;
        report = files.Report;
        reportPath = files.TextPath;
        rows = report.Applications.Select(application => new RfPrjImportApplicationRow(application)).ToArray();
        ApplicationsGrid.ItemsSource = rows;
        IssuesGrid.ItemsSource = report.Issues;
        ImportSource.Text = report.SourcePath;
        ImportSummary.Text = $"{report.Applications.Count} Application(s) · {report.TaskCount} Tasks · {report.Errors} Fehler · {report.Warnings} Warnungen · Schlüsselwerte: nicht gelesen";
        ImportSummary.Foreground = report.Errors == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkRed;
        CreateProposalButton.IsEnabled = false;
        var stem = Path.GetFileNameWithoutExtension(report.SourcePath);
        var safe = new string(stem.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.').Take(45).ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = "rfPrj";
        ProposalId.Text = "Import-" + safe;
        ProposalCode.Text = "IMP";
        ProposalVersion.Text = "1.0";
        ProposalName.Text = "Import aus " + stem;
    }

    public ProfileDefinition? Proposal { get; private set; }

    public void RunSelfTest()
    {
        if (!Title.Contains(AppIdentity.DisplayVersion, StringComparison.Ordinal)) throw new InvalidOperationException("rfPrj-Importfenster zeigt die Studio-Version nicht an.");
        MainWindow.VerifyAutomationContract(this);
        if (ApplicationsGrid is null || IssuesGrid is null || CreateProposalButton is null || ReviewAcknowledgement is null || ProposalId is null) throw new InvalidOperationException("rfPrj-Importfenster ist unvollständig.");
    }

    private void ReviewAcknowledgement_Changed(object sender, RoutedEventArgs e)
        => CreateProposalButton.IsEnabled = report is not null && report.CanCreateProposal && ReviewAcknowledgement.IsChecked == true;

    private void CreateProposal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplicationsGrid.CommitEdit();
            ApplicationsGrid.CommitEdit();
            var selected = rows.Where(row => row.Include).Select(row => row.Aid).ToArray();
            Proposal = importer.CreateProposal(report, selected, ProposalId.Text, ProposalCode.Text, ProposalName.Text, ProposalVersion.Text);
            if (MessageBox.Show(this,
                $"Der Entwurf enthält {Proposal.Applications.Count} Application(s) und {Proposal.Applications.Sum(application => application.Files.Count)} Files.\n\n" +
                "Kodierrezepte und Schlüsselwerte wurden nicht übernommen. Der Profil-Editor wird als nächste Prüfstufe geöffnet; erst dortiges Speichern aktiviert das Kundenprofil.\n\nFortfahren?",
                "Profilvorschlag prüfen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Proposal = null; return; }
            DialogResult = true;
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "rfPrj-Profilimport", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true }); }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "rfPrj-Profilimport", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed class RfPrjImportApplicationRow
{
    public RfPrjImportApplicationRow(RfPrjImportedApplication source) { Source = source; Include = source.SuggestedInclude; }
    public RfPrjImportedApplication Source { get; }
    public bool Include { get; set; }
    public string Aid => Source.Definition.Id;
    public string Name => Source.Definition.Name;
    public string Keys => $"{Source.Definition.KeyType} · {Source.Definition.KeyCount}";
    public int Files => Source.Definition.Files.Count;
    public string Confidence => Source.Confidence;
    public string ExistingProfile => Source.ExistingProfile;
    public int SourceTasks => Source.SourceTaskCount;
}
