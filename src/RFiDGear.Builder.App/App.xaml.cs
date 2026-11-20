using System.Windows;
using System.IO;
using RFiDGear.Builder.Core;

namespace RFiDGear.Builder.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if SIEENCODE_DIALOG_TESTS
        if (e.Args is ["--test-station-layout"])
        {
            try
            {
                var window = CreateIsolatedTestWindow("TestStation");
                window.Show();
                window.UpdateLayout();
                window.VerifyStationLayout();
                window.Close();
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            return;
        }
        if (e.Args.Length == 3 && e.Args[0] == "--dialog-tests")
        {
            try { DialogTestWorkspace.Start(e.Args[1], e.Args[2]); }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            return;
        }
#endif
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var window = CreateIsolatedTestWindow("Home");
                window.RunSelfTest();
                window.Close();
                var stationWindow = CreateIsolatedTestWindow("Station");
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
                var window = CreateIsolatedTestWindow("Home");
                var loaded = new TaskCompletionSource<bool>();
                window.Loaded += (_, _) => loaded.TrySetResult(true);
                window.Show();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
                window.UpdateLayout();
                var report = window.RunUiSmokeTest();
                Console.WriteLine("UI smoke test: " + report);
                window.Close();
                Shutdown(0);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--ui-test-workspace", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var window = CreateIsolatedTestWindow("Home");
                window.Title += " · Testdaten";
                window.Show();
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

    private static MainWindow CreateIsolatedTestWindow(string startupPage)
    {
        var root = Path.Combine(Path.GetTempPath(), "SieEncode", "startup-test-" + Guid.NewGuid().ToString("N"));
        var store = new BuilderSettingsStore(Path.Combine(root, "studio.settings.json"));
        var settings = new BuilderSettings();
        settings.Projects.Directory = Path.Combine(root, "projects");
        settings.Profiles.Directory = Path.Combine(root, "profiles");
        settings.VaultBackup.Directory = Path.Combine(root, "vault-backups");
        // Seed before constructing the window: no legacy migration or user-default paths.
        store.Save(settings);
        var baseProfiles = Path.Combine(settings.Profiles.Directory, "base");
        Directory.CreateDirectory(baseProfiles);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "test-fixtures", "Synthetic-AES-Mixed.base.json"),
            Path.Combine(baseProfiles, "Synthetic-AES-Mixed.base.json"));
        return new MainWindow(startupPage, store, Path.Combine(root, "group-trust"));
    }
}
