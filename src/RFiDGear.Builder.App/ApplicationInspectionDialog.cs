using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RFiDGear.Builder.App;

public sealed record ApplicationInspectionRequest(string Aid, string KeyType, int KeyNumber, string KeyValue, bool ReadContent);

public sealed class ApplicationInspectionDialog : Window
{
    private readonly ComboBox aid = new() { MinWidth = 180 };
    private readonly ComboBox keyType = new() { MinWidth = 180 };
    private readonly ComboBox keyNumber = new() { MinWidth = 90 };
    private readonly PasswordBox keyValue = new() { MinWidth = 370, FontFamily = new FontFamily("Consolas") };
    private readonly CheckBox readContent = new() { Content = "Inhalte von Standard-/Backup-Files lesen, soweit dieser Schlüssel berechtigt", IsChecked = true };
    private readonly TextBlock validation = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public ApplicationInspectionDialog(IEnumerable<string> applicationIds)
    {
        Title = "Application genauer untersuchen";
        Width = 660;
        Height = 430;
        MinWidth = 590;
        MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var value in applicationIds.Distinct(StringComparer.Ordinal)) aid.Items.Add(value);
        aid.SelectedIndex = aid.Items.Count > 0 ? 0 : -1;
        foreach (var value in new[] { "DF_KEY_DES", "DF_KEY_AES", "DF_KEY_3K3DES" }) keyType.Items.Add(value);
        keyType.SelectedIndex = 0;
        for (var index = 0; index <= 13; index++) keyNumber.Items.Add(index);
        keyNumber.SelectedIndex = 0;
        keyValue.Password = new string('0', 32);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = "Vertiefte Nur-Lese-Analyse", FontSize = 20, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "Der Schlüssel wird nur für diesen Lauf im Arbeitsspeicher gehalten. Bericht und RFiDGear-Projekt enthalten keinen Schlüsselwert.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 14) });
        root.Children.Add(Label("Application (AID)")); root.Children.Add(aid);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var typePanel = new StackPanel(); typePanel.Children.Add(Label("Schlüsseltyp")); typePanel.Children.Add(keyType);
        var numberPanel = new StackPanel { Margin = new Thickness(14, 0, 0, 0) }; numberPanel.Children.Add(Label("Key-Nr.")); numberPanel.Children.Add(keyNumber);
        row.Children.Add(typePanel); row.Children.Add(numberPanel); root.Children.Add(row);
        root.Children.Add(Label("Application-/Leseschlüssel (Hex)", new Thickness(0, 10, 0, 0))); root.Children.Add(keyValue);
        root.Children.Add(readContent); root.Children.Add(validation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Abbrechen", MinWidth = 100, Margin = new Thickness(4) }; cancel.Click += (_, _) => DialogResult = false;
        var start = new Button { Content = "Nur-Lese-Analyse starten", MinWidth = 190, Margin = new Thickness(4), FontWeight = FontWeights.SemiBold }; start.Click += (_, _) => Confirm();
        buttons.Children.Add(cancel); buttons.Children.Add(start); root.Children.Add(buttons); Content = root;
    }

    public ApplicationInspectionRequest? Request { get; private set; }
    private static TextBlock Label(string text, Thickness? margin = null) => new() { Text = text, Margin = margin ?? new Thickness(0) };

    private void Confirm()
    {
        var selectedType = keyType.SelectedItem?.ToString() ?? "";
        var value = new string(keyValue.Password.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        var expected = selectedType == "DF_KEY_3K3DES" ? 48 : 32;
        if (aid.SelectedItem is null || value.Length != expected)
        {
            validation.Text = aid.SelectedItem is null ? "Bitte eine Application auswählen." : $"Der Schlüsseltyp benötigt genau {expected} Hex-Zeichen.";
            return;
        }
        Request = new(aid.SelectedItem.ToString()!, selectedType, (int)keyNumber.SelectedItem, value, readContent.IsChecked == true);
        DialogResult = true;
    }
}
