using System.Windows;
using System.Windows.Controls;

namespace RFiDGear.Builder.App;

public partial class TutorialWindow : Window
{
    private readonly TutorialStep[] steps =
    [
        new("1 · Schnellstart", "Arbeitsplatz einordnen", "Die Übersicht zeigt vier normale Stationen: Arbeitsplatz, Kodiervorschrift, Vault und Kodieren. Rot beziehungsweise noch nicht bereit bedeutet, dass vor einem Kartenlauf ein Schritt fehlt.", "Sicher: reine Orientierung; Reader und Karte werden nicht angesprochen.", "Home"),
        new("2 · Grundprofile", "Gemeinsame Kartenstandards verwalten", "In den Einstellungen kann ein gemeinsamer Profilordner gewählt werden. Administratoren importieren dort validierte, versionierte Grundprofile; jedes Projekt wählt genau eines und kann mehrere Zusatzprofile ergänzen.", "Ein neues Profil ersetzt gebundene Projektstände nie still. Abweichungen müssen bewusst geprüft und neu gebunden werden.", "Profiles"),
        new("3 · Projekt", "Kodiervorschrift auswählen oder erstellen", "Ein Projekt enthält Grundprofil, optionale Zusatzprofile, Projektwerte und den Buildumfang. Bestehende Definitionen werden direkt gespeichert; ‚Speichern unter‘ erzeugt bewusst eine Kopie.", "Sicher: Projektbearbeitung verändert keine Karte.", "Editor"),
        new("4 · Schlüssel", "Produktivkeys kontrolliert verwalten", "Produktivkeys werden kryptografisch erzeugt, sind in der Sitzung sichtbar und gehören anschließend in den verschlüsselten Vault. Die Projektdefinition bleibt keywertfrei. Vor einem Mastering muss der Vault in derselben Sitzung erneut geöffnet werden; SieEncode legt dann automatisch eine verifizierte Wiederherstellungskopie an dem in den Einstellungen festgelegten, unabhängigen Speicherort an.", "Die Passphrase wird nicht gespeichert und kann nicht wiederhergestellt werden. Ohne erfolgreich verifizierte Wiederherstellungskopie bleibt Mastering gesperrt.", "Editor"),
        new("5 · Mehrbenutzer", "Persönliche Freigaben verwalten", "Jede Person legt einmal eine nicht exportierbare Windows-Identität an und gibt nur ihre öffentliche *.recipient.json weiter. Der Freigebende vergleicht den angezeigten SHA-256-Fingerabdruck über einen zweiten Kanal und nimmt die Person als Empfänger auf. Eine schlüsselfreie Keyset-ID verbindet Sicherung und Kodierläufe eindeutig.", "Kein gemeinsames Tageskennwort verteilen. Die Recovery-Passphrase bleibt ausschließlich in KeePass. Beim Kodieren werden produktive rfPrj-Dateien nur lokal für die Sitzung erzeugt und danach entfernt.", "Editor"),
        new("6 · Projektprüfung", "Bauen und vorprüfen", "Dry-Run erklärt den Karten- und Taskplan ohne Schlüsselwerte. Erst ein erfolgreicher Build und Projekt-Preflight geben den betrieblichen Weg frei.", "Sicher: Dry-Run und Projektprüfung greifen nicht auf Reader oder Karte zu.", "Projects"),
        new("7 · Altprojekte", "Vorhandenen Build weiterverwenden", "„Altbuild nutzbar“ bedeutet: Die vorhandenen geprüften rfPrj-Dateien können weiter zum Prüfen, Mastern und Kodieren verwendet werden. Fehlt der Vault, bleiben nur Bearbeiten und Neubauen gesperrt. Neuere Zusatzfunktionen wie „Transponder erkennen“ können fehlen.", "Keine Schlüssel aus rfPrj zurückkopieren und keine neuen Schlüssel als vermeintliche Wiederherstellung erzeugen. Neue Schlüssel schaffen eine neue Identität.", "Projects"),
        new("8 · Teststation", "Transponder unabhängig vom Projekt untersuchen", "Die Teststation bündelt Reader-Einrichtung, projektlose AID-/PICC-Analyse und projektbezogene Leseprüfungen. Nach erfolgreicher AID-Inventur kann eine Application mit einem nur für die Sitzung bereitgestellten Schlüssel bis zu File-Settings und lesbaren Inhalten untersucht werden.", "Analyse ist read-only. Ein Auth- oder Lesefehler bedeutet niemals automatisch ‚leer‘. Formatieren ist getrennt als destruktive Wartungsaktion gekennzeichnet.", "TestStation"),
        new("9 · Karte", "Transponder vor dem Kodieren sicher erkennen", "Die Kodierstation verwendet dieselbe geprüfte Erkennungslogik wie die Teststation, zeigt aber nur die zum Projektablauf nötige Entscheidung. Ein Altbuild verwendet ersatzweise die vorhandene Transportprüfung.", "Read-only; während des gesamten Laufs muss derselbe freigegebene Transponder aufliegen.", "Station"),
        new("10 · Bestehende Karte", "Eine neue Application sicher ergänzen", "Im Projekt den Kartenvorgang ‚Bestehende Karte um Zusatzprofil erweitern‘ wählen. SieEncode erzeugt Schlüssel und Tasks nur für neue AIDs des Zusatzprofils. Vor dem Hinzufügen muss die Erkennung bestätigen, dass die Ziel-AID fehlt und die PICC-Einstellung Create/Delete ohne bekannten Masterkey erlaubt.", "Bestehende Applications, PICC-Key und PICC-KeySettings bleiben unangetastet. Bei unbekannter Fähigkeit bleibt der Schreiblauf gesperrt.", "Editor"),
        new("11 · Kodieren", "Schreibenden Lauf bewusst starten", "Bereite Kartendaten vor, kontrolliere die sichtbaren Werte und starte erst dann RFiDGear. SieEncode überwacht Terminalstatus und Readbacks automatisch und schließt RFiDGear danach regulär.", "Schreibend: nur freigegebenes Medium und zum Projekt gehörende Schlüssel verwenden.", "Station"),
        new("12 · Ergebnis", "Bedienentscheidung lesen und sicher handeln", "Die große Bedienentscheidung erklärt nach jedem Lauf in Alltagssprache: Was ist passiert, darf die Karte verwendet werden und was ist als Nächstes zu tun? Darunter bleiben Run-ID, Taskresultate, Fehlercode und Readbacks für Fachanwender und Support vollständig erhalten.", "Nur „KODIERUNG ERFOLGREICH BESTÄTIGT“ gibt die Karte frei. Bleibt RFiDGear offen, Karte und Datensatz nicht wechseln und die angezeigte Abschlussaktion befolgen.", "Station")
    ];

    private readonly Action<string> navigate;
    public TutorialWindow(Action<string> navigate, string currentPage = "Home")
    {
        InitializeComponent(); Title = $"SieEncode {AppIdentity.DisplayVersion} – Hilfe & Tour"; this.navigate = navigate; StepsList.ItemsSource = steps;
        StepsList.SelectedItem = steps.FirstOrDefault(step => step.Page == currentPage) ?? steps[0];
        MainWindow.InitializeAutomationContract(this, "Window.Tutorial");
    }

    public void RunSelfTest() { if (steps.Length != 12 || StepsList.SelectedItem is null) throw new InvalidOperationException("Tutorialschritte sind unvollständig."); MainWindow.VerifyAutomationContract(this); }
    private TutorialStep Current => (TutorialStep)StepsList.SelectedItem;
    private void StepsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (StepsList.SelectedItem is not TutorialStep step) return; var index=Array.IndexOf(steps,step);StepNumber.Text=$"Schritt {index+1} von {steps.Length}";StepTitle.Text=step.Heading;StepBody.Text=step.Body;StepSafety.Text=step.Safety;PreviousButton.IsEnabled=index>0;NextButton.IsEnabled=index<steps.Length-1; }
    private void Previous_Click(object sender, RoutedEventArgs e) => StepsList.SelectedIndex = Math.Max(0, StepsList.SelectedIndex - 1);
    private void Next_Click(object sender, RoutedEventArgs e) => StepsList.SelectedIndex = Math.Min(steps.Length - 1, StepsList.SelectedIndex + 1);
    private void OpenArea_Click(object sender, RoutedEventArgs e) { navigate(Current.Page); Close(); }
    private sealed record TutorialStep(string Title, string Heading, string Body, string Safety, string Page);
}
