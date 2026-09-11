using System.Text.Json;
using System.Xml.Linq;

internal static class UiWiringParity
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static void Verify()
    {
        var root = Directory.GetCurrentDirectory();
        using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/design/rc15-inventory.json")));
        var rows = baseline.RootElement.GetProperty("controls").EnumerateArray().ToArray();
        foreach (var group in rows.GroupBy(row => row.GetProperty("file").GetString()!))
        {
            var current = XDocument.Load(Path.Combine(root, group.Key));
            var failures = Missing(group.ToArray(), current);
            if (failures.Count > 0) throw new InvalidOperationException(group.Key + ": " + string.Join("; ", failures));
        }

        // Prove this gate detects disconnected actions, lost fields and changed dispatch targets.
        var mainRows = rows.Where(row => row.GetProperty("file").GetString()!.EndsWith("/MainWindow.xaml")).ToArray();
        var main = XDocument.Load(Path.Combine(root, "src/RFiDGear.Builder.App/MainWindow.xaml"));
        foreach (var mutation in new[] { "handler", "field", "tag" })
        {
            var copy = new XDocument(main);
            var row = mainRows.First(row => mutation switch
            {
                "handler" => row.GetProperty("events").TryGetProperty("Click", out _),
                "field" => row.GetProperty("name").GetString()!.Length > 0,
                _ => row.GetProperty("tag").GetString()!.Length > 0
            });
            var node = copy.Descendants().First(node => Matches(row, node));
            if (mutation == "handler") node.Attribute("Click")!.Remove();
            else if (mutation == "field") node.Remove();
            else node.SetAttributeValue("Tag", "__wrong_dispatch_target__");
            if (Missing(mainRows, copy).Count == 0)
                throw new InvalidOperationException("UI parity gate missed mutation: " + mutation);
        }
    }

    private static List<string> Missing(JsonElement[] rows, XDocument current)
    {
        var nodes = current.Descendants().ToList();
        var missing = new List<string>();
        foreach (var row in rows)
        {
            var match = nodes.FirstOrDefault(node => Matches(row, node));
            if (match is not null) nodes.Remove(match);
            else missing.Add(row.GetProperty("container").GetString() + "/" +
                row.GetProperty("control").GetString() + " " + row.GetProperty("name").GetString() +
                " " + row.GetProperty("events").GetRawText());
        }
        return missing;
    }

    private static bool Matches(JsonElement row, XElement node)
    {
        if (node.Name.LocalName != row.GetProperty("control").GetString()) return false;
        var name = row.GetProperty("name").GetString();
        if (!string.IsNullOrEmpty(name) && (string?)node.Attribute(Xaml + "Name") != name) return false;
        var tag = row.GetProperty("tag").GetString();
        if (!string.IsNullOrEmpty(tag) && (string?)node.Attribute("Tag") != tag) return false;
        return row.GetProperty("events").EnumerateObject().All(binding =>
        {
            var expected = binding.Value.GetString();
            // These three RC15 navigation entries now restore the workspace's last page.
            // WorkspaceShell.Workspace_Click delegates to Navigate; WPF smoke covers history.
            if (binding.Name == "Click" && expected == "Navigate_Click" &&
                row.GetProperty("file").GetString() == "src/RFiDGear.Builder.App/MainWindow.xaml" &&
                name is "NavProjectsButton" or "NavStationButton" or "NavTestStationButton")
                expected = "Workspace_Click";
            return (string?)node.Attribute(binding.Name) == expected;
        });
    }
}
