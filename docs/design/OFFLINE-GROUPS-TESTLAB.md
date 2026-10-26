# Separate VirtualBox-Testumgebung

## Gesicherter Vault-Nachweis vom 26.10.2026

Alle acht Phasen des Laufs vom 24.10. aus der VM gesichert und geprueft:
[Ergebnisse und Hashidentitaet](offline-groups-vm-vault-results.json).
Zwei normale Windows-Benutzer mit geladenen Profilen; echte persoenliche
Schluesselspeicher, Gruppenfreigabe/-ablehnung, eigener Vault durch B,
Entzug/Rotation/Altsicherung, alter lokaler Stand und Rollbackablehnung bestanden.
Echter Produkt-Recoveryinstaller und Wiederentschluesselung in einem separaten
Prozess bestanden; normaler Zugang mit Recoveryidentitaet abgewiesen.
Keine nativen Dialoge, komplette Projektkopien, Delegation oder Verwaltungsnachfolge
getestet. Ein lokaler gemeinsamer Ordner ersetzt keine SMB-/Cloud-Abnahme.
Die alte lokale Kopie wurde ohne Zugriff auf den gemeinsamen Vault gelesen;
die gemeinsame Ablage selbst war nicht getrennt. Harnessruntime 8.0.30,
installierte App 8.0.32; Produkt-DLLs anhand Hashes identifiziert.
Keine erneute Ausfuehrung der bestandenen Phasen zur Berichtssicherung.

## Bisheriger Installationsstand

Stand 24.10.2026: vom Benutzer ausdruecklich autorisierter Neuaufbau.
Erster Windows-Installationsversuch fehlgeschlagen; LTSC-Test-VM einsatzbereit.
Reale Installation 1.2.1 und Upgrade 1.3.0-offline-groups.1 bestanden, jeweils
MSI Exit 0, Studio-DLL-Identitaet bestaetigt sowie Self-Test/UI-Smoke Exit 0.
Nachweis: [Installergebnis](offline-groups-installed-suite.json).
Die Anwendungstests liefen als Administrator; echte SieEncode-Mehrbenutzer-,
Recovery- und native Gesamtabnahme bleiben offen. Bestehende Unternehmens-VMs
werden nicht wiederverwendet. Keine Hostinstallation von SieEncode.

## Identitaet und Isolation

- VirtualBox 7.2.20r175154.
- VM `SieEncode-Testlab`, UUID `b0642a52-be42-45e3-b1a4-081b1d444060`.
- Lokaler Speicher ausserhalb OneDrive: `D:/VirtualBox/SieEncode-Testlab`.
- 4 CPUs, 8192 MB RAM, dynamische 80-GB-VDI, EFI, TPM 2.0, Secure Boot.
- NAT, keine Portweiterleitung, keine gemeinsamen Hostordner, Zwischenablage
  und Drag-and-drop deaktiviert, keine USB-Durchreichung.
- Zufallskennwort und Installationsantwortdateien ausschliesslich im lokalen
  ACL-geschuetzten Unterordner `Private`; keine Geheimnisse im Repository.
- Oracle-Unattended-Vorlage lokal angepasst: Hardwarecheck-Bypaesse entfernt,
  deutsche Tastatur; Guest Additions vorgesehen, kein TestExecService.

## Betriebssystemmedium

Offizielle 90-Tage-Evaluierung von
[Microsoft](https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise).
VirtualBox erkennt Windows 11 Enterprise Evaluation, x64, de-DE,
Build `10.0.26300.9457`, Image-Index 1.

ISO SHA256 erfolgreich gegen den
[Microsoft-Nachweis](https://support.microsoft.com/en-us/servicing/os/windows/docs/2026/09/verify-the-authenticity-of-a-windows-11-enterprise-evaluation-iso-file)
abgeglichen:
`D67C5606AB6AB3F2C7A7D3EB7C66387D03714427236FD5930E3734D50716D9DF`.
Lokale ISO: `Media/Windows11-Enterprise-DE.iso`.

## Produktpakete und offene Schritte

### Installationsdiagnose und zweiter Versuch

Die erste Installation (26H2) blieb in der zweiten Phase bei 42 Prozent stehen.
Die Gaststeuerung wurde innerhalb der begrenzten Wartephase nicht erreichbar.
Kurzes Pausieren/Fortsetzen half nicht. Vor einem einmaligen Neustart wurde der
Snapshot `setup-42-before-restart` erstellt
(`9f934ae0-0b48-404d-8fd5-6d3587dc3669`). Danach meldete Windows explizit,
dass die Installation wegen eines unerwarteten Neustarts nicht fortgesetzt
werden kann. Der Neustart ist somit kein erfolgreicher Wiederherstellungsnachweis.
Die VM ist gestoppt; Datentraeger, Snapshot und Diagnosen bleiben erhalten.
Die Ursache des urspruenglichen Stillstands ist nicht abschliessend bestimmt.
VirtualBox nutzt auf diesem Host NEM/Hyper-V; Hostschutz wurde nicht geaendert.

Frischer zweiter Versuch:

- VM `SieEncode-Testlab-LTSC`, UUID `cae4442a-db81-46ef-9d82-33031d2a46b9`.
- Gleiche Ressourcen und Isolation, eigener leerer Datentraeger; Netzwerklink
  fuer die lokale Installation getrennt.
- Offizielle Microsoft-Evaluierung Windows 11 Enterprise LTSC, x64/de-DE,
  Build `10.0.26100.1742`, Image-Index 1 (keine N-Edition).
- Download ueber den LTSC-Link des Evaluation Centers, Microsoft-CDN-Datei
  `26100.1742.240906-0331.ge_release_svc_refresh_CLIENT_LTSC_EVAL_x64FRE_de-de.iso`.
- Dateigroesse 5155368960 Bytes, lokal ermittelter SHA256
  `2B3C6432F54599FA83E2A68327871E6D7424EE0422677B81C19C7E7D20B43BF7`.
  Dieser Hash dokumentiert die heruntergeladene Datei; kein Vergleich mit
  einem separat veroeffentlichten Microsoft-LTSC-Hash behauptet.
- Neue Antwortdateien im geschuetzten `Private/LTSC-Install-*`; eigene Logs.

Die vorbereiteten Gastskripte liegen lokal unter `tmp/`: Initialisierung,
Windows-DPAPI-Benutzerprobe, MSI-Installation und begrenzter Abschlusslauf.
Sie erlauben keine Ausfuehrung ihrer Gastaktionen auf dem Host (Namenspruefung).
Fortschreibung 24.10.2026: LTSC-Einrichtung abgeschlossen, Guest Additions
7.2.20 vorhanden. guest-baseline.json bestaetigt Secure Boot/TPM, zwei normale
Benutzer und deaktivierte automatische Anmeldung. windows-user-isolation.json
belegt drei erfolgreiche synthetische Windows-DPAPI-Pruefungen (Anlegen und
Wiederlesen durch testerA, Ablehnung durch testerB). Keine Produktabnahme daraus.

MSI 1.2.1 real installiert (Exit 0), installierter Studio-DLL-Hash stimmt.
Self-Test-Harness brach mit ExitCode=null ab; beide Ausgabelogs leer.
Direkter Process-Start ersetzt Start-Process fuer die Anwendungstests; lokale
Exitcode-Proben 0/7 bestanden. Korrigierter Launcher liegt im Gast, kontrollierte
Fortsetzung ohne erneute 1.2.1-Installation vorbereitet. Benutzerergebnis abwarten,
kein Statuspolling. Upgrade und echte Vault-Gesamtabnahme noch offen.

Die unveraenderten MSI-Pakete 1.2.1 und 1.3.0-offline-groups.1 liegen unter
`Packages`. Hashwerte vor dem Kopieren bestaetigt; Kandidatenidentitaet siehe
[Kandidatennachweise](OFFLINE-GROUPS-CANDIDATE-1.3.0-1.md).

1. Windows-Installation, Guest Additions und Neustart pruefen; Secure Boot/TPM
   im Gast bestaetigen, automatische Setupanmeldung anschliessend entfernen.
2. Zwei echte normale Windows-Testbenutzer und einen gemeinsamen synthetischen
   Testordner einrichten; Ausgangssnapshot vor Produkttests sichern.
3. MSI-Neuinstallation und Upgrade 1.2.1 auf den Kandidaten im Gast pruefen.
4. Mehrbenutzer-/Offline-/Recoveryablauf nach
   [Abnahmeanleitung](../OFFLINE-GRUPPEN-ABNAHME.md) mit exakten Ergebnissen pruefen.

Die laufende Windows-Einrichtung ist keine Produktfreigabe. Native Bedienablaeufe,
echte Recoveryinstallation, Gruppenrechte und Benutzerisolation bleiben bis zur
tatsaechlichen Ausfuehrung offen. Eine VM ersetzt keine Reader-/Kartenabnahme.
