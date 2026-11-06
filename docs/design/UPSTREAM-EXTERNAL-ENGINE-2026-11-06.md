# Offizielle externe RFiDGear-Engine: Stand 06.11.2026

Benutzerprioritaet: naechstes funktionsfaehiges Release fertigstellen; keine
automatische Desktopbedienung. Synthetische Tests und schriftliche Anleitung
fortsetzen, native Bedienung manuell durch Benutzer, Bilder spaeter ergaenzen.

Gepruefter Upstream-HEAD: `8ec3ba5ef888303937a57d581d471934393c82b2`.
[PR 231](https://github.com/c3rebro/RFiDGear/pull/231) ist integriert:
Application Extensions erhalten beim Oeffnen den Host-Service. Das erleichtert
eine spaetere kleine Bridge, ersetzt aber noch nicht unseren Laufzeitvertrag.

Die drei Benutzer-Issues behandeln die passenden Restanforderungen:

- [232](https://github.com/c3rebro/RFiDGear/issues/232): eigenstaendige externe Anwendung, Steuerung und korrelierte Ergebnisse.
- [233](https://github.com/c3rebro/RFiDGear/issues/233): kontinuierlicher Kartenbetrieb ueber IAutoModeHost.
- [234](https://github.com/c3rebro/RFiDGear/issues/234): sichere temporaere Schluesselaufloesung ohne Klartextpersistenz.

Der direkt gelesene Vertrag `RFiDGear.Contracts/IAutoModeHost.cs` dokumentiert
weiterhin einen Stub: Start wirft NotSupportedException, Stop tut nichts,
IsAutoModeEnabled bleibt false. Deshalb ist ein sofortiger vollstaendiger Ersatz
der privaten Runtime nicht freigegeben. Offene Anforderungen sind keine
implementierte oder gepruefte Funktion.

Releaseentscheidung: konkrete Studiofehler jetzt beheben, aktuelle Runtime
beibehalten. Spaeter offiziellen Versionsstand fest pinnen und eine getrennte
optionale Bridge gegen reale Capabilities pruefen: Projekt laden, RunId/Outcomes,
Abbruch/Busy, Automatik ohne Doppelausfuehrung, Secretresolver fail-closed,
Save/Logs ohne Schluesselwerte. Erst nach nachgewiesener Funktionsparitaet und
Hardwareabnahme die private Runtime aus dem Paket entfernen. Keine zweite
Reader- oder Kartenengine in Studio aufbauen.

Unabhaengiger aktueller Fehler: Passphrasen-Vault v2 wurde in der Station nicht
entsperrt; dadurch fehlte der Secretserver fuer neuere secret://-Projekte.
Kein Nachweis falscher Kartenschluessel. Korrektur und synthetische Regression
laufen in Studio; echte Kartenerkennung muss anschliessend manuell bestaetigt
werden. Bestehende Kundendaten und Vaults werden nicht migriert.
