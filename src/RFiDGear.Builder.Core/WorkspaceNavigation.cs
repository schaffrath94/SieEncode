namespace RFiDGear.Builder.Core;

/// <summary>Session navigation only; switching workspace never reloads project or card state.</summary>
public sealed class WorkspaceNavigation
{
    private readonly Stack<string> history = new();
    private readonly Dictionary<string, string> lastPages = new(StringComparer.Ordinal);
    public string CurrentPage { get; private set; } = "Home";
    public bool CanGoBack => history.Count > 0;

    public static string WorkspaceFor(string page) => page switch
    {
        "Projects" or "Editor" or "Profiles" or "Analysis" or "Diff" => "Projects",
        "TestStation" or "Acceptance" => "TestStation",
        "Station" or "Home" or "Settings" or "Help" => page,
        _ => throw new ArgumentException("Unknown page.", nameof(page))
    };

    public string LastPageFor(string workspace) => lastPages.GetValueOrDefault(workspace, workspace);

    public void Navigate(string page, bool addHistory = true)
    {
        var workspace = WorkspaceFor(page);
        if (addHistory && CurrentPage != page) history.Push(CurrentPage);
        CurrentPage = page;
        lastPages[workspace] = page;
    }

    public bool GoBack()
    {
        if (!history.TryPop(out var page)) return false;
        Navigate(page, false);
        return true;
    }
}
