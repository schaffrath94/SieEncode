# Kurze manuelle Restabnahme – Kandidat 1.3.0-offline-groups.4

Stand 07.11.2026. B1–B3 sind durch Benutzer-Rückmeldung bestanden: normales
Entsperren, ausdrückliche Recovery-Prüfung mit Build-Sperre und erneutes normales
Entsperren mit verfügbarer Build-Aktion. Keine eigene UI-Beobachtung des Agenten.
Nachweis: [manueller Recovery-Wechsel](design/offline-groups-candidate4-native-recovery-result.json).
Die übrigen unten aufgeführten Bediennachweise bleiben offen.
Kopie und Wiederöffnen des konkreten Projekts „Neues Projekt Gruppe Kopie“ sind
inzwischen zusätzlich per installierter Produkt-API in zwei getrennten Prozessen
bestanden: neue ID, identische Schlüssel und unveränderte Quelldateien.
[Nachweis](design/offline-groups-candidate4-existing-project-copy-result.json).
B4/B5 bleiben ausschließlich als Prüfung der Dialogbedienung und des vollständigen
Anwendungsneustarts offen; den fachlichen Skripttest nicht erneut ausführen.
Installation/Upgrade in der VM, Erhalt der Dateien und RSA-Schlüssel in drei
Windows-Profilen sowie installierter Self-Test und 61 Anwendungsprüfungen sind bereits
belegt. Diese Prüfungen müssen für dieselbe unveränderte Umgebung nicht erneut
ausgeführt werden. Der Installer ersetzt eine vorhandene SieEncode-Version.

Zusätzlich sind 22 Produkt-API-Phasen für Gruppen/Recovery/Projektkopie/Delegation
und Verwaltungsnachfolge unter zwei Windows-Benutzern bestanden. Diese fachlichen
Skripttests nicht wiederholen; B–D prüfen hier nur noch die tatsächliche Bedienung
einschließlich Dialogen und Rollenwechsel. Für den Stationsfehler zuerst die
[gezielte Passphrasen-Abnahme](ABNAHME-PASSPHRASEN-STATION.md) verwenden.

MSI SHA256: `0F1AABF98BF91D72A4B2475757C97017563766414EFD2E60D64ACD48F8E7FD73`.
Keine Schlüsselwerte oder privaten Sicherungen in Rückmeldungen aufnehmen.

## A – Gemeldeten Fehler prüfen (ohne Karte)

1. Version **1.3.0-offline-groups.4** im Fenster kontrollieren.
2. Dasselbe Projekt in **Projekte → Bibliothek** auswählen, **Projekt prüfen**.
3. Erwartung: Keine Transport-Key-Längenfehler allein wegen der im Vault
   gespeicherten Werte. Andere Meldungen wörtlich notieren, nicht übergehen.

Antwort genügt: `A bestanden, Version …` oder `A Fehler: …`.
Dieser Schritt bestätigt weder Kartenschlüssel noch vollständige Buildfähigkeit.

## B – Bestehenden Test-Vault und Projektkopie prüfen

Nur ein synthetisches Gruppenprojekt verwenden. Normale Mitgliedschaft und
separate Recovery-Identität müssen bereits eingerichtet sein.

In der bestehenden Test-VM dafür **testerB** verwenden. Die Bedienprojekte
„Neues Projekt Gruppe“ und „Neues Projekt Gruppe Kopie“ liegen unter
`C:\Users\testerB\Documents\SieEncode-Projekte`; dort wurden auch die getrennten
normalen und Recovery-Identitäten eingerichtet. Eine unter labadmin gestartete
Anwendung zeigt diese eigene Bibliothek nicht automatisch. Nicht nur den Ordner
umstellen oder neue Identitäten anlegen, sondern die Anwendung unter testerB
starten. Die zusätzlichen API-Fixtures VaultProbe4 sind separate Skripttestdaten.

1. **Bearbeiten → Vault entsperren**: normaler Zugang muss bestätigt werden.
2. **Mit Recovery-Schlüssel öffnen → Recovery-Zugang prüfen**: Zugang,
   Projektbindung und Integrität müssen bestätigt, Build muss gesperrt bleiben.
3. Wieder **Vault entsperren**: normaler Zugang muss wiederhergestellt werden.
4. Unter **Weitere Editoraktionen · Experten → Speichern unter** einen neuen
   Testnamen verwenden. Editor, Kopfzeile und ausgewähltes Bibliotheksprojekt
   müssen jetzt dieselbe neue Kopie nennen. Die Quelle muss erhalten bleiben.
5. Anwendung schließen, neu starten, Kopie erneut öffnen und normal entsperren.

Antwort: `B1 …, B2 …, B3 …, B4 …, B5 …`; nicht ausgeführte Punkte offenlassen.
Für den unabhängigen Nachweis neuer Projekt-ID und unveränderter Quellhashes
die verwendeten synthetischen Testnamen nennen. Diesen Nachweis ergänzt Codex.

## C – Zentralen Rechtewechsel prüfen (zwei Testbenutzer)

Vorbereitung nach [Gruppen-Abnahme](OFFLINE-GRUPPEN-ABNAHME.md): getrennte
Windows-Benutzer A/B, A Hauptverwaltung und Mitglied beider Gruppen, B Mitglied
von Lokal, nicht von Lokal 2; separate Recovery aktiv. Nur Testprojekte ändern.

1. B öffnet ein Lokal-Projekt, Lokal 2 wird abgewiesen. Eine alte Testkopie von
   Lokal getrennt aufbewahren, bevor die Verwaltung geändert wird.
2. A: **Einstellungen → Offline-Gruppen verwalten**, Lokal auswählen, B über
   **Mitglied aus Gruppe entfernen** entfernen, **Gruppen signiert speichern**.
   **Gespeicherten Stand öffentlich exportieren**.
3. A öffnet den aktuellen Lokal-Vault und wählt **Gruppenempfänger abgleichen**.
   Vorschau muss den entfernten Gerätezugang zeigen. Erst nach Prüfung bestätigen.
   Verschlüsselte Altsicherung und Erfolgsmeldung müssen vorhanden sein.
4. B übernimmt den neuen Stand über **Registrierte Gruppen aktualisieren**.
   Der aktuelle Lokal-Vault muss abgewiesen werden. Die alte lokale Kopie kann
   vor diesem Import unter altem Stand weiterhin zugänglich sein: erwartete
   Offline-Grenze, keine sofortige globale Sperrung.
5. A nimmt B wieder auf, speichert/exportiert und gleicht den Vault erneut ab.
   B importiert den neuen Stand; aktuelles Lokal-Projekt wieder entsperrbar.

Antwort: `C1 … bis C5 …`, mit Bibliotheksrevisionen und genauer Fehlermeldung.
Ein Zugriffsversuch auf eine alte Kopie ersetzt nicht die Prüfung des aktuellen Vaults.

## D – Begrenzte Verwaltungsvollmacht prüfen

A besitzt den privaten Hauptverwaltungsschlüssel der Testbibliothek. B ist dort als Person erfasst und Mitglied von Lokal. Für D1–D5 die beiden zugehörigen Windows-Profile verwenden. Wer mit einem vorhandenen Teststand einsteigt, prüft zuerst Bibliotheks-ID, aktuelle Revision und Vollmacht. Bereits abgeschlossene Schritte mit passendem Stand können entfallen.

1. **B** öffnet **Einstellungen → Mein Gruppenverwaltungsplatz**, wählt die Bibliotheks-ID und klickt **Bibliothek laden**. Eigene Person auswählen, **Person und Arbeitsplatz laden → Öffentliche Verwaltungseinladung exportieren**. Fragt Studio nach einem separaten lokalen Verwaltungsschlüssel, mit **Ja** bestätigen. Die **.group-delegate.json** an A geben. Den anschließend angezeigten Verwaltungsfingerabdruck über einen zweiten Weg mitteilen.
2. **A** öffnet **Einstellungen → Offline-Gruppen verwalten** und wählt dieselbe Bibliotheks-ID. In der Gruppentabelle genau die Zeile **Lokal** anklicken. Bei gleichen Namen die stabile Gruppen-ID mit dem Testprojekt abgleichen. **Gruppenverwalter und Vollmachten** aufklappen, **Verwaltungseinladung für ausgewählte Gruppen freigeben** klicken und Bs **.group-delegate.json** öffnen. Im Dialog Person und Gruppenumfang prüfen, dann Bs unabhängig erhaltenen Fingerabdruck eingeben und bestätigen. Erwartet wird eine aktive Vollmacht für B und ausschließlich Lokal. Sie ist jetzt gespeichert. **Gespeicherten Stand öffentlich exportieren** wählen. **B** öffnet **Einstellungen → Registrierte Gruppen aktualisieren**, wählt die neue **.group-enrollment.json**, prüft den Stand, setzt den Bestätigungshaken und klickt **Geprüften Stand übernehmen**.
3. **B** öffnet **Mein Gruppenverwaltungsplatz** erneut und lädt Bibliothek und Person wie in D1. Unter **Meine freigegebene Gruppe** darf nur Lokal bearbeitbar sein. Den Namen dieser Zelle in **Lokal (Abnahme)** ändern, mit Eingabetaste übernehmen und **Änderung signieren und exportieren** klicken. Die **.group-change.json** an A geben. Der gemeinsame Gruppenstand hat zu diesem Zeitpunkt noch den alten Namen.
4. **A** lädt die Bibliothek in **Offline-Gruppen verwalten**, öffnet **Gruppenverwalter und Vollmachten → Signierte Gruppenänderung prüfen und übernehmen** und wählt Bs **.group-change.json**. Die Vorschau muss genau die beabsichtigte Umbenennung zeigen. Mit **Ja** bestätigen und die neue Revision notieren. Dieselbe Datei über dieselbe Schaltfläche erneut wählen. Erwartet werden eine Ablehnung und dieselbe Revision wie nach der ersten Übernahme.
5. **A** markiert unter **Gruppenverwalter und Vollmachten** Bs aktive Vollmacht für **Lokal (Abnahme)** und klickt **Ausgewählte Vollmacht entziehen → Ja**. Den Stand öffentlich exportieren. **B** importiert ihn wie in D2 und lädt seinen Verwaltungsplatz erneut. Die Person muss weiterhin auswählbar sein. Bei dieser einzigen Vollmacht muss die Liste freigegebener Gruppen jetzt leer sein.

Antwort: `D1 … bis D5 …`, mit Revisionen und tatsächlichem Ergebnis.

## Dokumentation und Abschluss

Codex trägt gemeldete Ergebnisse mit Version, Datum, Testumgebung und Herkunft
„Benutzerrückmeldung“ ein. Eigene Toolbeobachtungen und automatische Prüfungen
bleiben getrennt. Eine fehlende Rückmeldung bedeutet niemals „bestanden“.

Separat offen: visuelle Schlusskontrolle der Kollegenanleitung, tatsächliche
Reader-/Testkartenprüfung und gegebenenfalls eine zweite Rechnerumgebung.
Die Schritte A–D schreiben keine Karte. Keine erneute Paketproduktion allein
für Protokolländerungen; Codefehler erfordern einen neuen identifizierten Kandidaten.

## Ergänzung vom 18.11.2026 – Kandidat 5

D1 bestanden im installierten Studio unter testerB. Der Benutzer führte den Export aus. Im offenen Dialog war die Exportbestätigung für Testperson B sichtbar, die Liste der freigegebenen Gruppen war leer. Die öffentliche .group-delegate.json lag in Dokumente. LibraryId und PersonId aus der Datei stimmten mit der im Dialog angezeigten Bibliothek d9c6ffd7-0081-4189-b965-518c9204c10c und der ausgewählten Testperson überein. D2 bis D5 und die native Verwaltungsnachfolge bleiben offen.

D2, lokaler Freigabeschritt: Am 18.11.2026 im installierten Kandidaten 5 direkt abgelesen: „Testperson B · aktiv · Lokal“, gespeicherte Revision 6 derselben Bibliothek. Durchführung im Profil testerB, das auch den wiederhergestellten Hauptverwaltungsschlüssel besitzt. Export und Import dieser Revision zwischen zwei Windows-Profilen sowie D3–D5 sind hierdurch noch nicht belegt.

Bedienbefund für die nächste UI-Überarbeitung: Beide Gruppen hießen zunächst „Neue Gruppe“. Die Zuordnung zum Testprojekt erforderte einen externen ID-Abgleich. Die Rollenbezeichnung „Hauptverwaltungsprofil“ wurde als Aufforderung zum Windows-Benutzerwechsel verstanden. Der aktuelle Dialog erklärt auch nach der Freigabe erneut das signierte Speichern, obwohl Revision 6 bereits gespeichert ist. Abnahmekriterium: Eine Person kann die Zielgruppe anhand ihres Namens und Projektbezugs auswählen, erkennt ihre aktuelle Rolle und sieht nach der Freigabe, für wen die Änderung schon gilt und wer den öffentlichen Stand noch importieren muss.

## E – Hauptverwaltung an einen Kollegen übergeben (Kandidaten 6 und 7)

**Bedienwege in Kandidat 7.** Für E1, E5 und den ersten Teil von E6 in den Einstellungen **Hauptverwaltung an eine andere Person übergeben** aufklappen. Für E4 und das signierte Speichern nach dem Neustart in E6 **Personen, Geräte und Gruppen für das Team verwalten** aufklappen.

**Was noch praktisch geprüft wird.** A und B führen den Ablauf einmal mit den sichtbaren Bestätigungen und Dateidialogen durch. E2 ist erfolgreich, wenn B die Meldung über die gespeicherte und unverändert zurückgelesene Sicherung sieht und nach **Schließen** der öffentliche Anfrageexport möglich ist. Zwischen E2 und E3 den Nachfolgedialog geöffnet lassen. Wurde er geschlossen, dieselbe Vorbereitung auswählen und eine neue Sicherungsdatei unter einem freien Namen speichern. Die Vorbereitung und die erste Sicherung bleiben erhalten.

Der bestandene UI-Smoke-Test aus dem MSI hat Vorbereitung, Sicherung, Anfrage, Autorisierung, Import und Aktivierung über die Dialoglogik mit synthetischen Daten geprüft. Die Schlüsselwiederherstellung gegen den registrierten Stand ist darin bereits enthalten. Als Rückmeldung genügen die Beobachtungen zur Bedienung, zur Rollenübergabe und zum anschließenden Studio-Neustart.

Für diesen Test eine eigene Testbibliothek verwenden. A besitzt deren bisherigen privaten Verwaltungsschlüssel. B hat dieselbe Bibliotheks-ID und denselben aktuellen Stand bereits registriert. A und B arbeiten in ihren jeweiligen Windows-Profilen.

1. **B** öffnet **Einstellungen → Nachfolge der Hauptverwaltung vorbereiten**, wählt die Testbibliothek und klickt **1. Neuen Nachfolgeschlüssel vorbereiten → Ja**. Den angezeigten neuen Fingerabdruck für den unabhängigen Abgleich bereithalten.
2. **B** klickt **2. Private Sicherung erstellen**, gibt eine eigene Testpassphrase mit mindestens 20 Zeichen zweimal ein, setzt die Bestätigung und klickt **Verschlüsselte Sicherung speichern**. Eine neue Datei außerhalb des gemeinsamen Ordners wählen. Erfolgsmeldung prüfen und **Schließen** wählen. Passphrase getrennt aufbewahren.
3. **B** klickt **3. Öffentliche Anfrage exportieren** und gibt die Datei mit der Endung **.group-successor.json** an A. Den neuen Fingerabdruck über einen zweiten Weg abgleichen, etwa per Telefon.
4. **A** öffnet **Einstellungen → Offline-Gruppen verwalten**, wählt dieselbe Bibliothek und klickt **Nachfolgeranfrage prüfen und autorisieren**. Anfrage von B öffnen, Bibliotheks-ID prüfen und den separat erhaltenen Fingerabdruck eingeben. Bei Übereinstimmung mit **Ja** bestätigen. Die öffentliche **.group-transition.json** speichern und B bereitstellen.
5. **B** schließt den Vorbereitungsdialog und öffnet **Einstellungen → Angekündigten Verwaltungswechsel importieren**. Übergabedatei öffnen, Bibliothek auswählen, den unabhängig abgeglichenen neuen Fingerabdruck eingeben, die Bestätigung setzen und **Bestätigten Verwaltungswechsel übernehmen** anklicken. Erwartet wird die Meldung „Neuer Verwaltungsschlüssel lokal übernommen.“
6. **B** öffnet **Nachfolge der Hauptverwaltung vorbereiten** erneut, wählt die Bibliothek und klickt **4. Nach importierter Übergabe lokal aktivieren → Ja**. Erwartet wird „Neue Hauptverwaltung lokal aktiv.“ Studio schließen und erneut starten. Über **Einstellungen → Offline-Gruppen verwalten** die Testbibliothek öffnen, eine Testgruppe eindeutig umbenennen und **Gruppen signiert speichern** wählen. Den neuen Namen und die höhere Revision prüfen.

Rückmeldung zu E1–E6 mit Kandidatenversion, tatsächlicher Meldung und Revision vor und nach der Änderung. Passphrasen und private Sicherungsdateien bleiben beim Tester. Andere Arbeitsplätze übernehmen die Übergabe jeweils über Schritt 5.
