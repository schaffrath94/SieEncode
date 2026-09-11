using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RFiDGear.Builder.App;

public sealed record TransponderFormatRequest(string KeyType, string KeyValue);

public sealed class TransponderFormatDialog : Window
{
    private readonly ComboBox keyType = new() { MinWidth = 190 };
    private readonly PasswordBox keyValue = new() { MinWidth = 360, FontFamily = new FontFamily("Consolas") };
    private readonly TextBlock validation = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public TransponderFormatDialog()
    {
        Title = "Transponder formatieren";
        Width = 610;
        Height = 340;
        MinWidth = 560;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        keyType.Items.Add("DF_KEY_DES");
        keyType.Items.Add("DF_KEY_AES");
        keyType.Items.Add("DF_KEY_3K3DES");
        keyType.SelectedIndex = 0;
        keyValue.Password = new string('0', 32);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = "Alle Applications und Files werden unwiderruflich gelöscht.", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(new TextBlock { Text = "Geben Sie den aktuell gültigen PICC-Schlüssel an. Er bleibt nur für diesen Lauf im Arbeitsspeicher.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 14) });
        root.Children.Add(new TextBlock { Text = "Schlüsseltyp" });
        root.Children.Add(keyType);
        root.Children.Add(new TextBlock { Text = "Aktueller PICC-Schlüssel (Hex)", Margin = new Thickness(0, 10, 0, 0) });
        root.Children.Add(keyValue);
        root.Children.Add(validation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Abbrechen", MinWidth = 100, Margin = new Thickness(4) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button { Content = "Formatierung vorbereiten", MinWidth = 170, Margin = new Thickness(4), FontWeight = FontWeights.SemiBold };
        confirm.Click += (_, _) => Confirm();
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        root.Children.Add(buttons);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        MainWindow.InitializeAutomationContract(this, "TransponderFormat");
    }

    public TransponderFormatRequest? Request { get; private set; }

    private void Confirm()
    {
        var type = keyType.SelectedItem?.ToString() ?? "";
        var value = new string(keyValue.Password.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        var expected = type == "DF_KEY_3K3DES" ? 48 : 32;
        if (value.Length != expected)
        {
            validation.Text = $"Der gewählte Schlüsseltyp benötigt genau {expected} Hex-Zeichen.";
            return;
        }
        Request = new(type, value);
        DialogResult = true;
    }
}
