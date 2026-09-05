using System.Windows;

namespace RFiDGear.Builder.App;

public partial class MainWindow
{
    private void Tutorial_Click(object sender, RoutedEventArgs e)
    {
        var tutorial = new TutorialWindow(page => Navigate(page), currentPage) { Owner = this };
        tutorial.ShowDialog();
    }
}
