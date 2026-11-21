# SieEncode 1.0 – Vault einfach erklärt

**Entwicklungsstand für das erste Release.** Es gibt noch **kein veröffentlichtes SieEncode-Projekt und keine zu übernehmenden Vault-Dateien aus einem früheren Release**. Der Einstieg unten beschreibt die **Neuanlage**. Bereits vorhandene interne Teststände begründen keine zusätzliche Benutzerpflicht zur Migration.

## Das Wichtigste in 30 Sekunden

Ein **Projekt** beschreibt, *was* auf eine Karte geschrieben werden soll. Der **Projekt-Vault** schützt die geheimen Kartenschlüssel, die zum späteren Kodieren und Ändern gebraucht werden. Die **persönliche Identität** ist Ihr privater Zugang auf Ihrem Windows-Benutzerkonto. Die **Offline-Gruppe** entscheidet, welche Personen mit ihren freigegebenen Geräten einen bestimmten Gruppen-Vault öffnen dürfen.

> Persönliche Identität vorhanden ≠ Gruppenmitgliedschaft ≠ Zugriff auf jeden Projekt-Vault.

Der normale Vorgang ist: **persönliche Identität einmalig einrichten → Hauptverwaltung erteilt Teamfreigabe → signierten Gruppenstand importieren → neues Gruppenprojekt mit der freigegebenen Gruppe erstellen → Vault beim Arbeiten mit berechtigtem Windows-Zugang öffnen.** SieEncode speichert Kartenschlüssel im Vault, nicht offen in der Projektdefinition.

## Begriffe ohne Fachchinesisch

| Begriff | Bedeutung | Wer macht das? |
|---|---|---|
| Persönlicher Zugang | Zertifikat und **privater** Schlüssel im aktuellen Windows-Benutzer. Einmalig pro Benutzer; bei mehreren Identitäten den richtigen Fingerabdruck prüfen | Jede Person selbst |
| Öffentliche Einladung (`.recipient.json`) | Öffentlicher Zertifikatsteil. Darf an die Hauptverwaltung gehen; enthält **keinen** privaten Schlüssel und gibt noch keine Rechte | Kollege exportiert, Hauptverwaltung prüft |
| Hauptverwaltung | Signiert und verteilt die Liste der Personen, Geräte und Gruppen. Besitzt einen **separaten Verwaltungs-Signierschlüssel** | Zuständige Verwaltungsstelle |
| Gruppenstand (`.group-enrollment.json`) | Von der Hauptverwaltung signierte öffentliche Freigaben mit Revision. Wird auf jedem Offline-Rechner **ausdrücklich** importiert und unabhängig geprüft | Hauptverwaltung exportiert; jeder Kollege importiert |
| Gruppe | Beispielsweise „Kodierer Aachen“. Ein Gruppenprojekt wird genau einer ausgewählten Gruppe zugeordnet. Eine Person kann mehreren Gruppen angehören | Hauptverwaltung |
| Gruppen-Vault | Verschlüsselte Kartenschlüssel **eines Projekts**. Er wird für die freigegebenen Gruppengeräte und den separaten Recovery-Zugang verschlüsselt | SieEncode erstellt nach bewusster Projektauswahl |
| Recovery-Identität | Separater, geschützter Rettungszugang für den Gruppen-Vault – **nicht** der alltägliche Benutzerzugang und **nicht** bloß ein Sicherungsordner | Hauptverwaltung organisiert und genehmigt |
| Vault-Sicherung | Zusätzliche verschlüsselte Dateisicherung außerhalb des Projektordners, bei Mastering verpflichtend | Einrichtender des Arbeitsplatzes/Administrierende |

## Beispiel: Hauptverwaltung und zwei Kodierer

1. **Verwaltung:** Unter `Einstellungen → 2 · Teamzugriff und Offline-Gruppen → Ich verwalte Personen und Gruppen` bei einem neuen Team einmalig eine Hauptverwaltung erstellen. **Nur ein tatsächlich neues Team benötigt einen neuen Verwaltungsschlüssel.**
2. **Alle Beteiligten:** Unter `Einstellungen → 1 · Mein persönlicher Vault-Zugang` den eigenen Zertifikatszugang einmalig anlegen, falls keiner vorhanden ist. Bei mehreren vorhandenen Identitäten ausdrücklich wählen. **Nicht für jedes Projekt neue Zertifikate erzeugen.**
3. **Kollegen:** Die öffentliche Einladung exportieren und an die Verwaltung geben. Den Fingerabdruck **über einen zweiten unabhängigen Weg** bestätigen, nicht einfach nur aus der Datei ablesen.
4. **Verwaltung:** Die Einladungen den vorgesehenen Personen/Geräten zuordnen und die Gruppe „Kodierer Aachen“ freigeben. Außerdem eine **separate Recovery-Identität** einrichten, deren öffentliche Einladung freigeben und deren private Sicherung getrennt geschützt aufbewahren. Änderungen **signiert speichern**, dann den **öffentlichen Gruppenstand exportieren**.
5. **Kollegen:** Unter `2 · Teamzugriff und Offline-Gruppen` die signierte Datei bei der ersten Nutzung registrieren, bei späteren Änderungen **aktualisieren**. Auch den Verwaltungsfingerabdruck unabhängig prüfen. Der Dateitransfer oder das Anmelden bei Windows allein aktiviert keine neue Gruppenmitgliedschaft.
6. **Projektverantwortlicher mit aktiver Gruppenzugehörigkeit:** `Neues Projekt` wählen, Grundprofil, Werte und Kartenschlüssel vorbereiten. Im Projekteditor `Gruppen-Vault für dieses Projekt einrichten` öffnen, Bibliothek und Gruppe wählen, neuen Projektnamen prüfen und die Erstellung ausdrücklich bestätigen. SieEncode kontrolliert die aktuelle Richtlinie, den eigenen berechtigten privaten Zugang und mindestens einen aktiven Recovery-Zugang, bevor das neue verschlüsselte Projekt veröffentlicht wird.
7. **Beim täglichen Arbeiten:** Berechtigter Windows-Benutzer öffnet das Projekt. SieEncode prüft den lokalen signierten Gruppenstand, das zum Projekt gehörende Vault und den passenden privaten Schlüssel. Danach lassen sich Schlüssel für die lokale Sitzung verwenden. Die spätere Kartenkodierung verlangt weiterhin eigene Karten-/Reader- und Laufprüfungen.

**Für ein rein persönliches Projekt** kann stattdessen der persönliche Projekt-Vault mit einem lokalen Zertifikat und getrennter Recovery-Passphrase verwendet werden. Die Gruppenverwaltung wird dann nicht benötigt. Der Gruppenweg ist der vorgesehene Weg, wenn mehrere Kollegen gemeinsam ein Projekt nutzen.

## Was passiert, wenn etwas fehlt?

- **Kein persönliches Zertifikat:** Im eigenen Windows-Benutzer einmalig den persönlichen Zugang erstellen. Nicht stellvertretend auf einem anderen Windows-Konto erzeugen.
- **Mehrere Zertifikate:** Bewusst auswählen und den Fingerabdruck prüfen. Keine dritte Identität als „Lösung“ erzeugen.
- **Keine Gruppenregistrierung:** Signierte öffentliche Registrierungsdatei von der Hauptverwaltung anfordern und deren Fingerabdruck unabhängig bestätigen. SieEncode erzeugt daraus **keine** eigene Freigabe.
- **Gruppenregistrierung vorhanden, aber kein Zugriff:** Die Hauptverwaltung muss Person, Gerät und richtige Gruppe prüfen und ggf. **signiert aktualisieren**. Anschließend die neue Version am Arbeitsplatz importieren. Es kann auch eine veraltete Projekt-Vault-Empfängerliste vorliegen; nach echter Rechteänderung den Gruppen-Vault **ausdrücklich** abgleichen.
- **Kein aktiver separater Recovery-Zugang:** Hauptverwaltung muss diesen vor der Neuanlage freigeben. Der Benutzer darf diese Sicherheitsanforderung nicht umgehen.
- **Vault lässt sich nicht öffnen:** Keine neuen Produktivkeys erzeugen, keinen neuen „Ersatz-Vault“ anlegen und nicht ungeprüft weiterkodieren. Zuerst Zertifikat, signierte Revision, Gruppe, Projektbindung und vorhandene sichere Sicherung klären.
- **Schlüssel des einzigen berechtigten Windows-Benutzers weg:** Ein gültig eingerichteter separater Recovery-Zugang oder ein geeigneter anderer noch berechtigter Gruppenzugang ist erforderlich. Eine öffentliche Einladung allein stellt den privaten Schlüssel **nicht** wieder her.

## Entzug: Was wirklich gesperrt wird – und was nicht

**Wichtig für SieEncode 1.0: Das Ändern einer Offline-Gruppenmitgliedschaft ist keine rückwirkende Schlüsselsperre.** Ein Mitarbeiter, der vor dem Entzug seinen **privaten** Zertifikatsschlüssel und eine Kopie des damaligen Projekt-Vaults samt altem signiertem Gruppenstand besitzt, kann diesen historischen Stand grundsätzlich weiterhin entschlüsseln. Die signierte neue Freigabe ist keine Nachricht, die ein offline abgelegtes altes Geheimnis nachträglich löscht. Selbst wenn der Mitarbeiter den neuen Stand in der aktuellen Anwendung importiert, kann er die aufbewahrte alte Dateikopie außerhalb des aktuellen Arbeitsflusses verwenden.

Daher sind **vier verschiedene Maßnahmen** erforderlich:

1. **Freigabe für künftige Arbeit ändern:** Hauptverwaltung entfernt die Person bzw. deaktiviert ihr Gerät, signiert die neue Gruppenrevision und verteilt sie. Jeder aktuelle Offline-Arbeitsplatz muss sie selbst importieren.
2. **Aktuellen Projekt-Vault aktualisieren:** Eine nach dem Entzug noch berechtigte Person öffnet jeden betroffenen aktuellen Projekt-Vault und führt ausdrücklich den **Empfängerabgleich mit neuer Verschlüsselung** durch. Die neuen Vault-Bytes enthalten keinen neuen Zugang mehr für den ehemaligen Mitarbeiter. Das kann eine **alte Kopie nicht unbrauchbar machen**.
3. **Historische Kopien schützen:** Keine alte verschlüsselte Sicherung automatisch im gemeinsamen Projektordner ablegen. SieEncode verwendet für neue historische Vault-Sicherungen beim Abgleich ein gesondertes Verzeichnis im **lokalen Benutzerprofil des Ausführenden**; ein Ziel innerhalb des Projektordners wird abgewiesen. Diese Dateien **enthalten weiterhin den alten Empfängerkreis** und müssen durch die Verantwortlichen zugriffsgeschützt, gegebenenfalls getrennt gesichert und aufbewahrt werden. Der lokale Speicherort ist allein **keine** Garantie für sichere Zugriffsrechte.
4. **Bei tatsächlichem Sicherheitsentzug der Kartenschlüssel:** Sämtliche betroffenen Schlüssel **kontrolliert auf Karten und den zuständigen Lesern/SAMs ändern**, gegebenenfalls mit neuen, getrennten Diversifikationsschlüsseln. Bis zum wirksamen Austausch können zuvor bekannte Schlüssel weiter missbraucht werden. Eine bloße neue Vault-Verschlüsselung ersetzt **keine** Karten-Schlüsselrotation.

**Unmögliches Versprechen:** Vollständig offline verteilte, bereits entschlüsselbare Daten können nicht nachträglich zentral zurückgerufen werden. Für einen **unmittelbaren, überprüfbaren Entzug** vor jeder Verwendung müssen Schlüsseloperationen von einer erreichbaren, vertrauenswürdigen Autorisierungsstelle bzw. einem entsprechend gesicherten Schlüsselgerät kontrolliert werden. Das würde den Betriebsmodus beeinflussen. Zeitlich begrenzte Offline-Freigaben und Hardwaremodule können das Risiko begrenzen, ersetzen aber nicht automatisch die sofortige Fernsperre alter frei kopierbarer Dateien.

## Geplante Erweiterung: Schlüsselverwaltung für Kodierung, Leser und SIPORT

**Diese Produktanforderung ist beschlossen, aber die zentrale Synchronisation und die neue Schlüsselverwaltungsansicht sind noch nicht implementiert.** SieEncode soll nicht nur Projekt-Vaults öffnen und Karten kodieren, sondern als **zentrale, allgemeine Schlüsselverwaltung** für Kunden arbeiten. Dafür muss ein berechtigter Benutzer Schlüsselwerte ausdrücklich **im Klartext anzeigen, kopieren, importieren und z. B. für SIPORT bereitstellen** können.

- **Zentrale Bibliothek:** Kunde → Schlüsselsatz → Schlüssel-ID → Version → Zweck (Karte, Leser, SIPORT) → berechtigte Personen und Geräte. Projekte beziehen sich auf konkrete Schlüsselversionen; die Projektdefinition bleibt frei von geheimen Werten.
- **Offline-Bestand:** Nur tatsächlich freigegebene Schlüsselsets werden verschlüsselt auf dem jeweiligen Windows-Arbeitsplatz vorgehalten. Kodieren und verantwortete Schlüsselanzeige funktionieren nach lokaler Entsperrung **ohne dauernde Internetverbindung oder spezielle SAM-Leser**.
- **Synchronisation:** Bei verfügbarer Firmenverbindung oder über ein signiertes, für den Empfänger verschlüsseltes Austauschpaket werden neue Schlüsselversionen, Rechte, Änderungen und spätere Audit-Ereignisse abgeglichen. Eine Offline-Station ist bis dahin nicht aktuell; eine entfernte Person könnte vorhandene frühere Klartextschlüssel weiterhin besitzen.
- **Schlüssel anzeigen und exportieren:** Absichtlich verdeckte Standardanzeige, gesonderte Rechte und bewusste Aktionen für Klartext, Zwischenablage und SIPORT-Export; weder der Geheimwert noch ein Klartextexport gehören in ein normales Protokoll. Ein bereits ausgegebener Klartextwert lässt sich später nicht „zurückrufen“.
- **Sicherheitsvorfall:** Berechtigungsentzug unterbindet zukünftige zentrale Ausgabe, **nicht** früheres Wissen. Echte Entwertung eines DESFire-Kartenschlüssels erfordert die kontrollierte Rotation auf allen betroffenen Karten und den zugehörigen Lesern/SIPORT-Systemen.

Siehe `docs/design/ZENTRALE-SCHLUESSELARCHITEKTUR-1.0.md`. Diese geplante zentrale Synchronisation darf nicht mit den **bereits vorhandenen** lokalen Gruppen- und Projekt-Vault-Funktionen verwechselt werden.

## 1.0 – was wir bewusst nicht verlangen

Für die erste Nutzung sind **keine Altprojekt-Migration, keine bisherige Team-Empfängerliste, kein Vault aus einer früheren SieEncode-Version und keine Konvertierung einer alten RFiDGear-Datei** erforderlich. Nur die Funktionen für neue persönliche oder Gruppenprojekte und deren sicheren Lebenszyklus gehören in den normalen Ablauf. Technische Kompatibilitätsroutinen können im Entwicklungsquellcode verbleiben, sind aber **keine** Benutzeraufgabe für Version 1.0.

**Wichtig:** Diese Anleitung beschreibt den implementierten Entwicklungsweg; die endgültige Windows-, Zwei-Benutzer-, Recovery-, MSI- und Hardwareabnahme ist noch nicht erfolgt. Sie ist keine Produktivfreigabe.
