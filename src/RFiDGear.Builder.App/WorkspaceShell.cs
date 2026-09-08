using System.Windows;
using System.Windows.Controls;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    // Optional local visual evidence, only invoked by the explicit UI smoke-test path.
    private void CaptureWorkspaceReview()
    {
        var directory = Environment.GetEnvironmentVariable("SIEENCODE_UI_REVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        var originalMode = settings.ExpertMode;
        var originalPage = currentPage;
        try
        {
            foreach (var expert in new[] { false, true })
            {
                settings.ExpertMode = expert;
                ApplyOperatingMode();
                foreach (var page in new[] { "Station", "Projects", "TestStation", "Analysis" })
                {
                    Navigate(page, false);
                    UpdateLayout();
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    var surface = (FrameworkElement)Content;
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)Math.Ceiling(surface.ActualWidth), (int)Math.Ceiling(surface.ActualHeight),
                        96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(directory, $"{page}-{(expert ? "expert" : "normal")}.png"));
                    encoder.Save(stream);
                }
            }
        }
        finally
        {
            settings.ExpertMode = originalMode;
            ApplyOperatingMode();
            Navigate(originalPage, false);
        }
    }

    private static string WorkspaceTitle(string workspace) => workspace switch
    {
        "Projects" => "Projekte erstellen und bearbeiten",
        "Station" => "Karten kodieren",
        "TestStation" => "Karten untersuchen",
        "Settings" => "Einstellungen", "Help" => "Werkzeuge und Hilfe",
        _ => "Übersicht"
    };

    private void Workspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string workspace }) Navigate(navigation.LastPageFor(workspace));
    }

    private void RefreshWorkspaceNavigation()
    {
        var workspace = WorkspaceNavigation.WorkspaceFor(currentPage);
        ProjectWorkspaceTabs.Visibility = workspace == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        InspectWorkspaceTabs.Visibility = workspace == "TestStation" ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceNavigationBar.Visibility = workspace is "Projects" or "TestStation" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { NavHomeButton, NavProjectsButton, NavStationButton, NavTestStationButton, NavHelpButton, NavSettingsButton })
            button.Style = (Style)FindResource((string)button.Tag == workspace ? "NavButtonActive" : "NavButton");

        foreach (var button in new[] { NavProfilesButton, WorkspaceAnalysisButton, WorkspaceDiffButton, WorkspaceAcceptanceButton })
            button.Visibility = settings.ExpertMode || (string)button.Tag == currentPage ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in ProjectWorkspaceTabs.Children.OfType<Button>().Concat(InspectWorkspaceTabs.Children.OfType<Button>()))
        {
            var active = (string)button.Tag == currentPage;
            button.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            button.Style = (Style)FindResource(active ? "PrimaryButton" : "CompactButton");
        }
        WorkspaceModeHint.Text = !settings.ExpertMode && IsExpertPage(currentPage)
            ? "Geöffnete Fachansicht bleibt erhalten. Weitere Fachansichten finden Sie im Expertenmodus."
            : settings.ExpertMode ? "Expertenmodus · vollständige Fachansichten und bestehende Werkzeuge"
            : "Normalmodus · Fachansichten bei Bedarf über den Modusschalter einblenden";
    }

    private void VerifyWorkspaceStateContract(List<string> checks)
    {
        var originalMode = settings.ExpertMode;
        var originalSuppression = suppressOperatingModeChange;
        var originalName = EditorProjectName.Text;
        var originalPage = currentPage;
        try
        {
            suppressOperatingModeChange = true;
            EditorProjectName.Text = "Ungespeicherter Navigationstest";
            Navigate("Editor");
            Navigate("Station");
            Workspace_Click(NavProjectsButton, new RoutedEventArgs());
            if (currentPage != "Editor" || EditorProjectName.Text != "Ungespeicherter Navigationstest")
                throw new InvalidOperationException("Arbeitsbereichwechsel verliert Editorzustand.");
            Navigate("Profiles");
            settings.ExpertMode = false;
            ApplyOperatingMode();
            if (currentPage != "Profiles" || NavProfilesButton.Visibility != Visibility.Visible || !navigation.CanGoBack)
                throw new InvalidOperationException("Normalmodus entfernt die geöffnete Fachansicht oder Historie.");
            Back_Click(BackButton, new RoutedEventArgs());
            if (currentPage != "Editor" || EditorProjectName.Text != "Ungespeicherter Navigationstest")
                throw new InvalidOperationException("Zurücknavigation verliert Editorzustand.");
            checks.Add("Arbeitsbereiche: letzte Unterseite, ungespeicherter Editor und Zurückhistorie bleiben bei Bereichs-/Moduswechsel erhalten.");
        }
        finally
        {
            EditorProjectName.Text = originalName;
            settings.ExpertMode = originalMode;
            ApplyOperatingMode();
            Navigate(originalPage, false);
            suppressOperatingModeChange = originalSuppression;
        }
    }
}
