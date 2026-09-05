# SieEncode: drei Arbeitsbereiche und gemeinsame Berichte

Status: Gestaltungsentwurf zur Rückmeldung, keine Änderung der produktiven WPF-Anwendung.
Branch: `codex/ui-pdf-concept`. Ausgangspunkt: RC15.

## Festlegungen des Nutzers

- Reduzierte normale Oberfläche; technische Einzelheiten gezielt aufklappen.
- Karten kodieren, Projekte erstellen/bearbeiten und Karten untersuchen sind gleich wichtig.
- Jede Aufgabe erhält einen eigenen Arbeitsbereich innerhalb einer gemeinsamen Anwendung.
- Ein bereitgestelltes fünfseitiges technisches Analyseprotokoll dient als Layoutreferenz.
  Referenzdatei, Kundenbezeichnungen, UID und Kartenstruktur werden nicht ins Repository übernommen.
  Alle Entwurfsbeispiele verwenden unabhängig erfundene Daten.

## Gemeinsamer Rahmen

Dauerhafte Navigation: Karten kodieren / Projekte / Karten untersuchen.
Darunter ein gemeinsamer Zugang zu Einstellungen, Vault und Hilfe. Keine separate
Startseite mit konkurrierenden Schnellaktionen. Der zuletzt genutzte Bereich kann
wieder geöffnet werden. Reader- und Vaultstatus bleiben erreichbar, sind aber keine
Voraussetzung für das bloße Öffnen einer Oberfläche.

Die drei Bereiche behalten ihren eigenen Zustand. Bereichswechsel verwirft keine
Eingaben und beendet keinen Kartenlauf. Während Readerzugriffen gilt eine gemeinsame
Sperre; andere Bereiche erklären, welcher Lauf den Reader belegt. Ungespeicherte
Projektänderungen werden vor einem Wechsel bewusst gespeichert oder verworfen.

## Karten kodieren

Aufgabe: bekannte Projekte sicher auf einzelne Karten oder Serien anwenden.

1. Projekt und freigegebenen Stand auswählen.
2. Reader, benötigten Vaultzugang und Kartenzustand prüfen.
3. Variable Kartendaten eingeben und Sollnutzdaten vorbereiten.
4. Konkreten Kodierlauf starten; Ergebnis durch Tasknachweise und Readbacks bestätigen.
5. Nächste Karte oder Serienabschluss mit Bericht.

Immer sichtbar: aktuelles Projekt, nächster Schritt, verständliche Sperrgründe und
Ergebnis. Pfade, RunId und detaillierte Taskereignisse stehen unter Details.
Mastering und Recovery bleiben eigene, klar bezeichnete Vorgänge; kein implizites
Formatieren bei unbekannten Karten. Eine neu aufgelegte Karte verlangt eine neue
Prüfung. Beispieldaten im Entwurf erzeugen keine reale Freigabe.

## Projekte

Aufgabe: Projekte und wiederverwendbare Profile entwickeln und pflegen.

Bibliothek und Editor bilden einen eigenen Arbeitsbereich. Editorabschnitte:
Allgemeines / Kartenstruktur / Daten und Variablen / Schlüsselzugriff / Prüfung.
Anwendungen und Dateien erscheinen als Baum; die ausgewählte Datei wird daneben
bearbeitet. Fachliche Beschriftung zuerst, AID und File-ID bleiben sichtbar.
Veränderungen, aktive Profilversion und ungespeicherter Zustand sind eindeutig.

Projektprüfung funktioniert ohne Reader. Schlüsselzugriff wird nur verlangt, wenn
der konkrete Vorgang ihn benötigt. Sollreport und Übergabe an die Kodierstation
sind unterschiedliche Aktionen; Übergabe verlangt einen validierten, gespeicherten
Stand und ersetzt keine Kartenprüfung.

## Karten untersuchen

Aufgabe: eine Karte unabhängig von einem Projekt lesen und nachvollziehbar bewerten.

1. Reader wählen und Karte strikt lesend untersuchen.
2. Karten-/Anwendungs-/Dateibaum mit belegbaren Ergebnissen anzeigen.
3. Für geschützte Inhalte bei Bedarf einen Sitzungsschlüssel bereitstellen.
4. Optional einen Projektstand zum Soll/Ist-Vergleich hinzunehmen.
5. Istreport oder Vergleich exportieren.

Kein Projektzwang. Kein blindes Durchprobieren von Schlüsseln. Fehlender Zugriff,
nicht unterstützte Funktionen und tatsächlich leere Strukturen werden getrennt
ausgewiesen. Destruktive Wartung gehört nicht in den normalen Analyseablauf.
Teilweise gelesene Daten erhalten Umfang/Offset, Begrenzung und Zeitpunkt.

## Visuelle Sprache

Helle, ruhige Grundflächen mit zurückhaltender blauer Akzentfarbe; dunkle Darstellung
entsprechend der Systemvorgabe. Segoe UI, gut erkennbare Fokusmarkierungen und
mindestens 40 px hohe primäre Bedienelemente. Farbe ergänzt immer einen Textstatus.
Normalmodus reduziert die Informationsdichte, nicht die Genauigkeit der Aussagen.

## PDF-Struktur

Die Referenz liefert die Informationshierarchie: Allgemeines, Hardware/Software,
Produktion, Anwendungseinstellungen, Dateien und Datenblöcke. Wir übernehmen das
Prinzip der zweispaltigen Informationsgruppen in ein eigenes SieEncode-Layout.

Drei klar benannte Typen:

- Sollreport: ausschließlich Projektvorgaben; UID und reale Chipdaten bleiben nicht anwendbar.
- Istreport: gelesene Karte samt Umfang, Zeitpunkt und Grenzen der Untersuchung.
- Soll/Ist-Vergleich: ein bestimmter Projektstand gegen eine bestimmte Untersuchung.

Erste Seite mit Ergebnis und Kontext, danach Anwendung/File-Details. Jeder
Seitenkopf enthält Typ und Bericht-ID; jede Seite hat Seitennummer und Projekt/Karte.
Anwendungsköpfe und Tabellenköpfe werden bei Fortsetzung wiederholt. Beschriftungen
und Werte dürfen nicht still abgeschnitten werden. Lange Datenblöcke umbrechen
mit Offset und wiederholtem Kopf. Schrift ist eingebettet und Text suchbar.

Dateien zeigen Typ, Größe, Communication Mode und einzeln bezeichnete Rechte
für Lesen, Schreiben, Lesen/Schreiben sowie Rechte ändern. Der rohe Rechtewert
bleibt als technische Ergänzung verfügbar. Value-/Record-Felder erscheinen nur,
wenn für den Dateityp sinnvoll; ohne Runtime-Unterstützung keine erfundenen Werte.

HEX und ASCII stehen mit Byteoffset nebeneinander. Der Bericht unterscheidet
vollständig gelesen, teilweise gelesen, nicht gelesen und kein Zugriff. Schlüsselwerte
werden nie exportiert. Nutzdaten können personenbezogene Informationen enthalten:
Exportoptionen müssen deren bewusste Einbeziehung erlauben; der Entwurf enthält
ausschließlich synthetische Bytes.

Statusmodell: Übereinstimmung / Abweichung / Nicht geprüft / Kein Zugriff /
Nicht unterstützt / Nicht anwendbar. Eine nicht prüfbare Pflichtanforderung lässt
keine vollständige Abnahme zu. Synthetische Vorschauen tragen auf jeder Seite
die Kennzeichnung ENTWURF / SYNTHETISCHE DATEN.

Optionale Bilder, Notizen, Datum/Ort und Unterschriftsbereich werden nur bei Auswahl
ausgegeben. Eine leere Bild-/Unterschriftsseite ist kein Standardanhang. Eine
Unterschriftslinie ist keine digitale Signatur oder technische Freigabe.

## Umsetzung und Abnahme

Nach Rückmeldung zuerst WPF-Navigation/Arbeitsbereiche und gemeinsames Zustandsmodell,
danach Berichtsdatenmodell und Renderer. Bestehende Karten- und Schlüsselprüfungen
bleiben verbindlich. Der Vorlagen-PDF-Entwurf ist noch keine produktive Exportfunktion.

Softwareabnahme: Navigation mit ungespeicherten Eingaben, gemeinsame Readersperre,
Vault fehlt/gesperrt, leere und geschützte Karte, Teilinventur, lange Bezeichnungen,
mehrseitige HEX-Tabellen, Sollreport ohne Hardware, nicht prüfbarer Soll/Ist-Vergleich,
Tastaturbedienung sowie 100/125/150 Prozent Windows-Skalierung.

Physische Abnahme bleibt getrennt: ChangeFileSettings, installierter Gesamtbedienlauf,
zweiter Windows-Benutzer/Rechner, anschließend neue Pakete mit exakt zugeordneten
Studio-/Runtime-Hashes. Das Konzept erweitert keine bestehende Hardwarefreigabe.

## Verbindlicher Funktionserhalt und Expertenumfang

Die Zuordnung in FUNKTIONSPARITAET.md und die technische RC15-BEDIENINVENTUR.md
sind Grundlage der Migration. Bestehende Aktionen dürfen nicht verschwinden,
weil sie im neuen Normalmodus nicht auf die Hauptseite passen. Das betrifft
insbesondere Profilhistorie, Import/Export, Projektvergleich, Serienbetrieb,
Mastering, gezielten Rückbau, Recovery, manuelle Sitzungsschlüssel, Vaultprüfung,
Diagnose, CLI und Startparameter. Neue Funktionen sind ergänzend.

Der Expertenmodus erhält eigene Detailreiter je Arbeitsbereich. Er zeigt
vollständige Strukturen und Rechte, Schlüsselnummern und Referenzen, Datenrezepte,
Taskreihenfolge mit Voraussetzungen und Ergebnissen, Reader-/Runtime-Diagnose,
Readbacks sowie explizite Grenzen der Kartenanalyse. Der Moduswechsel verändert
weder Projekt noch ungespeicherte Eingaben oder den laufenden Auftrag.

Berichtstyp (Soll, Ist, Vergleich) und Umfang (Kompakt, Technisch vollständig)
sind unabhängig vom UI-Modus auswählbar. Ein Vollbericht darf Rohdaten nicht still
kürzen. Noch nicht erhobene Chipfelder und noch nicht unterstützte Dateitypen
bleiben als solche erkennbar. Der neue fünfseitige Entwurf konkretisiert den
Umfang; die produktive Implementierung und Prüfung stehen aus.
