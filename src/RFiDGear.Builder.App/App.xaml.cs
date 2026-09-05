using System.Windows;

namespace RFiDGear.Builder.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var window = new MainWindow();
                window.RunSelfTest();
                window.Close();
                var stationWindow = new MainWindow("Station");
                stationWindow.RunStartupModeSelfTest();
                stationWindow.Close();
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--ui-smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var window = new MainWindow();
                window.Show();
                window.UpdateLayout();
                var report = window.RunUiSmokeTest();
                Console.WriteLine("UI smoke test: " + report);
                window.Close();
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            return;
        }
        var hardwareIndex = Array.FindIndex(e.Args, argument => argument.Equals("--hardware-smoke-test", StringComparison.OrdinalIgnoreCase));
        if (hardwareIndex >= 0)
        {
            try
            {
                if (hardwareIndex + 1 >= e.Args.Length) throw new ArgumentException("Projektordner nach --hardware-smoke-test fehlt.");
                var resultPath = hardwareIndex + 2 < e.Args.Length && !e.Args[hardwareIndex + 2].StartsWith("--", StringComparison.Ordinal) ? e.Args[hardwareIndex + 2] : null;
                var productive = e.Args.Contains("--productive", StringComparer.OrdinalIgnoreCase);
                await HardwareSmokeTestRunner.RunAsync(e.Args[hardwareIndex + 1], productive, resultPath);
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(2); }
            return;
        }
        var acceptanceIndex = Array.FindIndex(e.Args, argument => argument.Equals("--teststation-hardware-acceptance", StringComparison.OrdinalIgnoreCase));
        if (acceptanceIndex >= 0)
        {
            try
            {
                var resultPath = acceptanceIndex + 1 < e.Args.Length && !e.Args[acceptanceIndex + 1].StartsWith("--", StringComparison.Ordinal)
                    ? e.Args[acceptanceIndex + 1] : null;
                var report = await TestStationHardwareAcceptanceRunner.RunAsync(resultPath);
                Console.WriteLine("Teststations-Hardwareabnahme: " + report);
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(3); }
            return;
        }
        var startupPage = e.Args.Contains("--station", StringComparer.OrdinalIgnoreCase) ? "Station" : "Home";
        new MainWindow(startupPage).Show();
    }
}
