# Wechsel der Hauptverwaltung: noch offener Implementierungsumfang

Stand 2026-10-20. Ein reiner Erzeugungs-/Verifikationskern ist implementiert,
Persistenz und Bedienweg sind weiterhin ausstehend. Bezug: Implementierungsfolge Punkt 2 in
[Offline-Vault-Gruppen](OFFLINE-VAULT-GROUPS.md).

## Verifizierter Iststand

`OfflineGroupAdministrationStore.RestorePrivateBackup` stellt den zum aktuell
registrierten Fingerabdruck passenden privaten Schlüssel wieder her. Das hilft
beim Rechnerwechsel, entzieht einer alten Kopie des Schlüssels aber keine Rechte.
`OfflineGroupTrustStore.VerifyPackageUpdate` verlangt denselben öffentlichen
Verwaltungsschlüssel wie bei der Registrierung. Ein anderer Schlüssel wird
absichtlich abgewiesen. Es gibt keinen gesonderten Übergangsimport.

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

### Noch zu integrieren

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

Nächster Implementierungsschritt: konfliktgeprüfter persistenter Übergangsimport,
anschließend wiederaufnehmbare private Übergabe und UI. Bestehende
Updateprüfungen bleiben dabei erhalten.
