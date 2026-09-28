using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RFiDGear.Builder.App;

// Local text only: no browser, active links, markup execution or file association required.
internal sealed class HelpDocumentWindow : Window
{
    internal HelpDocumentWindow(Window owner, string path)
    {
        Owner = owner;
        Title = "Anleitung · " + Path.GetFileNameWithoutExtension(path);
        Width = 1000; Height = 760; MinWidth = 560; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(16) };
        var close = new Button { Content = "Schließen", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var content = new TextBox
        {
            Text = File.ReadAllText(path), IsReadOnly = true, AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), FontSize = 14, Padding = new Thickness(12)
        };
        root.Children.Add(content); Content = root;
        MainWindow.InitializeAutomationContract(this, "HelpDocument");
    }
}
