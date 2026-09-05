using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RFiDGear.Builder.Runtime;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private async void QuickStartCoding_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProject is null) { SetStatus("Bitte zuerst ein Projekt auswählen.", true); return; }
        var entry = selectedProject;
        if (!LoadStation(entry)) return;
        try
        {
            if (sessionDefinition is null) throw new InvalidOperationException("Kodierschema wurde nicht geladen.");
            var preflight = RunSelectedPreflight();
            if (preflight.Status != "OK")
            {
                DisplayProjectPreflight(preflight);
                Navigate("Projects");
                throw new InvalidOperationException("RFiDGear wurde nicht geöffnet.\n\n" + DescribePreflightFailure(preflight));
            }
            var result = await new CodingStationQuickStartService().StartAsync(new CodingStationQuickStartRequest(
                settings.RFiDGear.ExecutablePath, sessionDefinition, preflight.Status, entry.ArtifactOnly));
            codingLaunch = result.Launch;
            StationState.Text = "KODIERSESSION BEREIT";
            StationStateDetail.Text = result.SafetyState + " Kartendaten eingeben und mit Enter bewusst bereitstellen; erst danach RFiDGear-Tasks ausführen.";
            StationStateBorder.Background = System.Windows.Media.Brushes.LemonChiffon;
            FocusPrimaryStationInput();
            SetStatus($"Kodierstation und RFiDGear-Projekt geöffnet · Prozess {result.Launch.ProcessId} · ohne AUTORUN/AUTOMODE.");
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void FocusPrimaryStationInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            StationFieldsControl.UpdateLayout();
            if (StationFieldsControl.ItemContainerGenerator.ContainerFromIndex(0) is DependencyObject container && FindVisualChild<TextBox>(container) is { } box)
            {
                box.Focus();
                box.SelectAll();
            }
            else StationPrepareButton.Focus();
        });
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result) return result;
            if (FindVisualChild<T>(child) is { } nested) return nested;
        }
        return null;
    }
}
