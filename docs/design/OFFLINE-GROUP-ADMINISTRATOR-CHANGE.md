# Wechsel der Hauptverwaltung: noch offener Implementierungsumfang

Stand 2026-10-21. Erzeugungs-/Verifikationskern, getrennte öffentliche
Anforderung/Autorisierung und lokaler Vertrauensimport sind implementiert,
ebenso der empfangsseitige Importdialog und geschützte private Vorbereitung
im Core. Autorisierung in der Verwaltungsoberfläche und Vorbereitung/Sicherung/
Anfrageexport beim Nachfolger sind integriert. Geführte Aktivierung fehlt noch.
Bezug: Implementierungsfolge Punkt 2 in
[Offline-Vault-Gruppen](OFFLINE-VAULT-GROUPS.md).

## Verifizierter Iststand

`OfflineGroupAdministrationStore.ActivateSuccessor` ist der separate Core-Schritt
nach dem ausdrücklich bestätigten Vertrauenswechsel. Er verlangt Bestätigung,
erwarteten aktuellen Checkpoint und einen vorbereiteten Schlüssel, der zum bereits
registrierten neuen Anker passt. Vorbereitung, Bibliothek und ursprünglicher
Besitznachweis werden erneut geprüft. Ohne importierten neuen Anker scheitert die
Aktivierung. Der private Schlüssel wird nur intern zwischen Stores verwendet.

Bei fehlender Verwaltungsdatei wird sie neu angelegt. Bei vorhandener Datei darf
nur der exakte vorbereitete Vorgänger ersetzt werden. Dessen geschützte Bytes
werden zuerst unter `.retired-<Datei-SHA256>.bin` neben der Verwaltungsdatei
archiviert und per Hash geprüft. Erst danach erfolgt die konfliktgeprüfte
Ersetzung, mit erneuter Trust-Prüfung im Schreibschritt. Ein fehlgeschlagener
Schreibversuch kann ein zusätzliches Archiv hinterlassen; Wiederaufnahme verwendet
es nur bei identischem Hash. Vorbereitung und Archiv werden nicht gelöscht.
Eine bereits vollständig aktivierte Datei wird bei identischem Checkpoint ohne
erneutes Schreiben akzeptiert. Abweichende/fremde Verwaltung wird nicht ersetzt.

Archiv und Vorbereitung bleiben an den vorhandenen lokalen Schutz gebunden und
ersetzen keine portable private Sicherung. Das Archiv enthält weiterhin einen
funktionsfähigen alten Schlüssel; es ist kein globaler Widerruf. Trustimport und
private Aktivierung sind zwei Schritte, keine dateiübergreifende atomare Transaktion.
Nach einem Abbruch zwischen beiden kann die Aktivierung wiederholt werden.
Die Einbindung dieses Aktivierungsschritts in den Dialog steht noch aus.

`OfflineGroupAdministrationStore.AuthorizeSuccessor` lädt den bisherigen privaten
Schlüssel intern aus dem geschützten Verwaltungsspeicher. Der Aufrufer übergibt
nur die öffentliche Anforderung, vorhandene lokale Registrierung, erwarteten
Checkpoint, unabhängig bestätigten Fingerabdruck und ausdrückliche Bestätigung.
Verwaltungsschlüssel und signierter lokaler Stand müssen zur Registrierung
passen. Vor Rückgabe werden Verwaltungsdateihash und registrierter Stand erneut
geprüft. Die Rückgabe enthält ausschließlich den öffentlichen signierten Wechsel;
die Methode verändert keine Schlüsseldatei und keine Registrierung.
Empfänger müssen den Wechsel weiterhin gegen ihren dann aktuellen Stand prüfen;
eine exportierte Signatur sperrt spätere Änderungen nicht global.

Tests prüfen fehlende/falsche Bestätigung, falschen Checkpoint, manipulierten
Besitznachweis, fremden lokalen Verwaltungsschlüssel, auseinanderlaufende Stände,
veraltete Anforderung sowie die Ablehnung der alten lokalen Verwaltung nach dem
Vertrauenswechsel. Erfolgreiche Autorisierung erhält beide lokalen Dateien
bytegleich. In der Verwaltungsoberfläche öffnet „Nachfolgeranfrage prüfen und
autorisieren“ eine größenbegrenzte `.group-successor.json`. Der neue Fingerabdruck
wird unabhängig eingegeben; eine zusätzliche Bestätigung benennt unveränderte
Rechte und die verbleibenden Offline-Grenzen. Vor der Dateiauswahl erfolgt die
Autorisierungsprüfung, unmittelbar vor dem Schreiben erneut. Ausgabe ist eine
neue `.group-transition.json`; vorhandene Dateien werden niemals überschrieben.
Die Oberfläche importiert den erzeugten Wechsel nicht automatisch.

`OfflineGroupSuccessorStore` speichert einen neu erzeugten RSA-3072-Schlüssel
zusammen mit dem registrierten Ausgangsstand und der öffentlichen Anforderung.
Die Datei wird ausschließlich neu angelegt, niemals überschrieben. Neuladen
prüft Bibliothek, Signaturen und Übereinstimmung des privaten Schlüssels mit
der Anforderung. Export einer Anforderung verlangt weiterhin den identischen
registrierten Ausgangsstand. Ein konkurrierendes Update während der Vorbereitung
wird vor Veröffentlichung erneut geprüft. Änderungen unmittelbar nach dem
Schreiben können den abschließenden Export verhindern; die vorbereitete Datei
bleibt dann für die Sicherung erhalten.

Eine passwortgeschützte private Sicherung bleibt auch nach einem Vertrauenswechsel
exportierbar. Die bestehende Wiederherstellung akzeptiert diesen Schlüssel erst,
wenn er zur ausdrücklich registrierten neuen Verwaltung passt; eine vorhandene
aktive Verwaltungsdatei wird nicht ersetzt. Die Vorbereitung selbst erteilt
keine Verwaltungsrechte. Für Windows ist ein eigener CurrentUser-DPAPI-Zweck
`SieEncode.OfflineGroupSuccessor.v1:` plus Bibliotheks-ID implementiert.
Die neuen Core-Tests verwenden einen synthetischen authentisierten Schutz,
keine echte Zweitbenutzerumgebung. Zusätzlich wurde der neue DPAPI-Zweck unter
dem aktuellen Windows-Benutzer geprüft: Datei neu geladen, fremde Bibliothek
und beide anderen Schutzzwecke abgewiesen, Aktivierung vor Autorisierung
abgewiesen, nach Übergangsimport wiederhergestellt und neue Richtlinie signiert.
UI-Smoke Exit 0, status OK, 57 Checks, stderr leer:
`tmp/group-successor-dpapi-smoke/SieEncode/ui-smoke/ui-smoke-result.json`.
`GroupSuccessorSetupWindow` bietet die registrierte Bibliotheksauswahl, explizite
Vorbereitung, separate private Sicherung und öffentlichen Anfrageexport.
Vorhandene Vorbereitung wird wieder geladen; jede neue Sitzung verlangt vor
Anfrageexport eine erfolgreich gespeicherte und zurückgelesene private Sicherung.
Fehlerhafte Bibliothekswahl verwirft das vorherige Ziel. Bei einem geänderten
Trust bleibt die Vorbereitung sicherbar, ihr Anfrageexport scheitert.
Die geführte Aktivierung sowie visuelle Dateidialogabnahme stehen noch aus.

`OfflineGroupAdministrationStore.RestorePrivateBackup` stellt den zum aktuell
registrierten Fingerabdruck passenden privaten Schlüssel wieder her. Das hilft
beim Rechnerwechsel, entzieht einer alten Kopie des Schlüssels aber keine Rechte.
`OfflineGroupTrustStore.VerifyPackageUpdate` verlangt denselben öffentlichen
Verwaltungsschlüssel wie bei der Registrierung. Ein anderer Schlüssel wird
absichtlich abgewiesen. Der gesonderte Übergangsimport ist unten beschrieben.

Die bisherige Wiederherstellung darf deshalb nicht als Widerruf eines früheren
Hauptverwalters oder als Schlüsselrotation bezeichnet werden.

## Vorgesehener kontrollierter Übergang

### Implementierter Verifikationskern

`GroupAdministratorTransition` erzeugt und prüft einen separaten
`SignedAdministratorTransition` mit exakten Payloadbytes und zwei RSA-PSS/SHA-256-
Signaturen. Signierte Daten sind jeweils UTF-8-Präfix plus unveränderte Payload:
`SieEncode-Administrator-Transition/v1/previous\0` bzw.
`SieEncode-Administrator-Transition/v1/next\0` (abschließendes Nullbyte).
Die Payload bindet Format/Version, Bibliotheks-ID, bisherigen SPKI-Schlüssel,
exakten bisherigen Checkpoint und das neue signierte Registrierungspaket.

Der Aufrufer muss den aktuellen Stand aus dem bereits registrierten lokalen
Trust liefern. Das Paket selbst begründet kein Vertrauen. Verifikation prüft
zusätzlich den unabhängig bestätigten neuen Fingerabdruck, einen tatsächlich
anderen Verwaltungsschlüssel und exakt eine Revisionssteigerung. Die neue
Richtlinie wird nach der regulären Richtlinienprüfung erneut serialisiert und
mit dem bisherigen Inhalt plus Revisionssteigerung verglichen. Ein reiner
Wechsel erhält alle Personen, Geräte, Gruppen, Recovery-Zugänge und Vollmachten;
Rechteänderungen bleiben getrennte signierte Operationen.

Transport ist größen-/tiefenbegrenzt; unbekannte und doppelte Felder werden
abgewiesen. Signaturprüfung und Auswertung verwenden private Snapshots.
Vorhandene Trennung von Verwaltung, Mitgliedern, Recovery und Delegation gilt
auch für den neuen Schlüssel. Der Kern schreibt keine Dateien und ersetzt
keinen Vertrauensanker. Normale `AcceptPackageUpdate`-Aufrufe lehnen das neue
Verwaltungspaket weiterhin ab.

Tests umfassen gültigen Transport, beide Signaturen, Rollenkreuzung,
unbestätigten Fingerabdruck, falsche Bibliothek/Schlüssel/Checkpoints,
Wiederholungsprüfung gegen den neuen Stand, manipulierte oder korrekt
doppelt signierte Rechte-/Revisionsänderungen sowie strikte JSON-Eingaben.
Zusätzlich werden ein Personenverzeichnis und ein vorhandener Gruppen-Vault
nach dem Wechsel über normalen Zugang und separate Recovery entschlüsselt.
Mitglieds-/Recovery-Schlüssel werden als neue Verwaltungsschlüssel abgewiesen.

### Persistenter lokaler Übergangsimport

`OfflineGroupTrustStore.PreviewAdministratorTransition` liest den vorhandenen
geschützten Zustand und prüft den Übergang gegen genau diesen Anker und den
erwarteten Checkpoint. Die Vorschau verändert keine Datei.
`AcceptAdministratorTransition` verlangt zusätzlich ausdrückliche Bestätigung,
prüft erneut und schreibt neuen Anker und Richtlinie zusammen in dieselbe
geschützte Zustandsdatei. Der erwartete Dateihash stammt aus dem gelesenen
Snapshot; der bestehende konfliktgeprüfte Dateischreiber verweigert einen
zwischenzeitlich geänderten Stand. Es gibt keinen automatischen Fallback zur
Neuregistrierung und keinen Lösch-/Zurücksetzschritt.

Tests prüfen unveränderte Dateien bei abgewiesenen Bestätigungen, falschem
Fingerabdruck/Checkpoint und Fehlern im Speicherschutz. Eine konkurrierende
gewöhnliche Aktualisierung sowie ein konkurrierender Verwaltungswechsel
bleiben erhalten. Nach Neuanlage des Storeobjekts sind neuer Anker und neuer
Checkpoint aktiv; Wiederholungsimporte und alte Schlüssel mit höherer Revision
werden abgewiesen, Folgerichtlinien des neuen Schlüssels akzeptiert.
Das sind synthetische lokale Tests, keine Zweitbenutzer-/Rechnerabnahme und
keine Aussage über eine Transaktion mit dem privaten Verwaltungsspeicher.

### Noch zu integrieren

Der Core trennt die beiden Schlüsselinhaber jetzt auch bei der Erstellung:
`Prepare` erhält nur den neuen privaten Schlüssel und den bereits registrierten
öffentlichen Ausgangsstand. Es erzeugt eine `AdministratorTransitionRequest`
mit eigenem Format `SieEncode-Administrator-Transition-Request`, Version 1,
Payload und Nachfolgersignatur. Die Payload und Signaturdomäne entsprechen dem
fertigen Wechsel; die bisherige Unterschrift fehlt absichtlich.
`Authorize` erhält nur diese öffentliche Anforderung, den aktuellen öffentlichen
Ausgangsstand, den bisherigen privaten Schlüssel, den unabhängig bestätigten
neuen Fingerabdruck und eine ausdrückliche Bestätigung. Es prüft zuerst Besitz,
Rechteerhalt und Ausgangsbindung, dann ergänzt es die bisherige Signatur.
Keine der beiden Seiten benötigt den privaten Schlüssel der anderen Seite.

Anforderungs- und fertiges Wechselformat sind nicht austauschbar. Beide Transport-
wege begrenzen Größe/Tiefe und verweigern unbekannte/doppelte Felder. Die
Autorisierung verwendet einen eigenen Snapshot, sodass spätere Änderungen an
der eingelesenen Anforderung den fertig signierten Wechsel nicht verändern.
`Create` bleibt als kombinierter Core-Helfer bestehen und verwendet diese beiden
Schritte. Noch nicht enthalten sind das geschützte Speichern einer Vorbereitung,
die Wiederaufnahme nach Neustart und die endgültige private Übergabe.

Der empfangsseitige Dialog ist über **Angekündigten Verwaltungswechsel importieren**
in den Einstellungen erreichbar. Er friert die eingelesene Datei ein, verlangt
die Auswahl einer bestehenden lokalen Registrierung und zeigt erst nach
kryptografischer Prüfung bisherige/neue Fingerabdrücke und Revisionen an.
`Inspect` liefert nur diese geprüfte Vorschau, keine unabhängige Bestätigung.
Übernahme benötigt weiterhin die separate Fingerabdruckeingabe und Bestätigung;
der Store prüft den Ausgangscheckpoint erneut. Ein Auswahlfehler verwirft den
vorherigen Zielzustand. Die Tests nutzen isolierte Windows-DPAPI-Dateien, keinen
realen zweiten Benutzer; native Dateiauswahl und Sichtprüfung bleiben offen.

Für die noch fehlende private Übergabe gilt: Der Nachfolger erzeugt den neuen
privaten Schlüssel selbst. Er liefert eine öffentliche, an Ausgangsstand und
Zielrichtlinie gebundene Besitzbestätigung. Die bisherige Verwaltung signiert
diesen Übergang nach unabhängigem Fingerabdruckabgleich. Der bestehende
`Create`-Baustein mit beiden RSA-Objekten ist dafür noch kein verteilter
Bedienweg. Das getrennte Anforderungs-/Autorisierungsformat ist implementiert;
geschützte, wiederaufnehmbare Vorbereitung ist im Core implementiert und
mit synthetischem Schutz sowie Windows-CurrentUser-DPAPI geprüft.

Ein Schlüsselwechsel benötigt eine eigene, ausdrücklich bestätigte Aktion und
ein eigenes öffentliches Übergangsformat. Gewöhnliche Registrierungsdateien
dürfen den Vertrauensanker weiterhin niemals ersetzen.

Der Übergang bindet mindestens Bibliotheks-ID, alten öffentlichen Schlüssel,
exakten Ausgangscheckpoint, neuen öffentlichen Schlüssel und neue Richtlinie.
Die bisherige Verwaltung signiert diese Bindung; die neue Verwaltung muss
zusätzlich den Besitz ihres privaten Schlüssels nachweisen. Die konkrete
Bytebindung und Domänentrennung des Prüfkerns stehen oben.

Empfänger prüfen den bisherigen lokal angehefteten Schlüssel, Ausgangscheckpoint
und beide Signaturen. Der neue Fingerabdruck muss unabhängig bestätigt werden.
Die neue Richtlinie muss die Revision strikt erhöhen. Bibliotheks-ID,
Personen-/Geräteverzeichnis, Gruppen und Recovery-Zugänge bleiben bei einem
reinen Verwaltungswechsel erhalten. Bestehende Vollmachten benötigen eine
ausdrückliche Entscheidung; keine stillschweigende Rechteausweitung.

Importiert werden neuer Vertrauensanker und Richtlinie gemeinsam über den
bestehenden konfliktgeprüften lokalen Dateiaustausch. Eine Unterbrechung darf
keinen gemischten Zustand hinterlassen. Lokaler privater Verwaltungsspeicher
und öffentlicher Vertrauensspeicher sind getrennte Dateien: ihre Übergabe muss
mit wiederaufnehmbaren Zuständen entworfen werden, nicht als gemeinsame atomare
Transaktion ausgegeben werden. Vorhandene Schlüsseldateien werden nicht
stillschweigend überschrieben oder gelöscht.

## Vertrauens- und Offline-Grenzen

- Erst nach bestätigtem Import lehnt ein Arbeitsplatz weitere Richtlinien des
  alten Schlüssels ab, auch wenn sie eine höhere Revision tragen.
- Offline gebliebene Arbeitsplätze vertrauen zunächst weiter dem alten Schlüssel.
- Alte Vaultkopien und bereits gelesene Kartenschlüssel werden dadurch nicht
  zurückgerufen. Eine Verwaltungsrotation ist keine Rotation von Vaultinhalten.
- Bei verlorenem oder kompromittiertem altem Verwaltungsschlüssel darf es keinen
  automatischen Ersatzanker aus der gemeinsamen Ablage geben. Ein gesonderter
  Wiederaufnahmeweg mit externer Vertrauensprüfung bleibt zu definieren.
- Abweichende Ausgangsstände müssen sichtbar behandelt werden. Den lokalen
  Versionsstand dafür zurückzusetzen ist kein zulässiger Konfliktlösungsweg.

## Erforderliche Nachweise vor Freigabe

1. Gültiger Übergang über öffentliche Dateien und anschließendem Neustart;
   normale Gruppen- und separate Recovery-Zugriffe bleiben erhalten.
2. Ablehnung bei falscher Bibliothek, altem Schlüssel, Ausgangscheckpoint,
   unbestätigtem neuem Fingerabdruck oder manipulierter Signatur/Richtlinie.
3. Alte Verwaltung nach Import auch mit höherer Revision abweisen; neue
   Verwaltung akzeptieren; Wiederholungsimport und parallele Änderungen
   konfliktfest behandeln.
4. Fehler vor bzw. während des lokalen Schreibens lassen den vorherigen
   konsistenten Zustand bestehen oder erlauben dokumentierte Wiederaufnahme.
5. Keine zusätzliche Gruppenmitgliedschaft durch Verwaltungswechsel.
   Delegierte Rechte bleiben begrenzt; alte private Sicherungen können den
   neuen Vertrauensanker nicht zurücksetzen.
6. Offline belassener Arbeitsplatz demonstriert ausdrücklich die verbleibende
   alte Vertrauensentscheidung. Keine sofortige globale Sperre behaupten.
7. UI führt durch neue private Sicherung, Fingerabdruckabgleich, öffentliche
   Verteilung und lokalen Import; keine privaten Schlüssel in Exporten/Logs.

Nächster Implementierungsschritt: wiederaufnehmbare private Übergabe und UI.
Bestehende
Updateprüfungen bleiben dabei erhalten.
