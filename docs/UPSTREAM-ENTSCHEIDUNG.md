# RFiDGear-Upstream – Integrationsentscheidung für RC6

Stand: 26. August 2026

SieEncode übernimmt nur Änderungen, die den Pilotbetrieb unmittelbar sicherer oder nachvollziehbarer machen und mit RFiDGear 2.0.10 kompatibel sind.

## Im gebündelten Runtime-Stand enthalten

- PR #205: belastbarere Task-Ausführung und Fehlerbehandlung.
- PR #208: erweitertes Logging beziehungsweise verwertbare Laufnachweise.

Diese Änderungen adressieren praktisch die für SieEncode relevanten Teile der Issues #178 und #207. Sie werden durch Runtime-, Log- und Hardwaretests abgesichert.

## Bewusst nicht lokal integriert

- PR #206: zusätzlicher Tasktyp. RFiDGear 2.0.10 kann diesen Vertrag nicht sicher deserialisieren und ausführen. Die Funktion bleibt capability-gesteuert gesperrt, bis ein kompatibler Upstream-Stand vorliegt.
- Issues #190 und #191: native Extension-/Host-Architektur. SieEncode besitzt dafür bereits den externen Host-Vertrag V1.1. Eine zweite, inkompatible interne Schnittstelle wird nicht vorab erfunden.
- Issue #204: Laufzeit-Upgrade auf .NET 10. Für den aktuellen Pilot entsteht daraus kein unmittelbarer Betriebs- oder Sicherheitsgewinn.

## Entscheidungskriterium für weitere Fixes

Ein Upstream-Fix wird lokal übernommen, wenn alle folgenden Punkte erfüllt sind:

1. Er behebt einen reproduzierten Fehler im freigegebenen SieEncode-Ablauf.
2. Das Daten-/Taskformat bleibt mit dem eingesetzten RFiDGear kompatibel.
3. Ein automatischer Regressionstest oder ein reproduzierbarer Hardwaretest belegt die Änderung.
4. Es wird keine geplante öffentliche Upstream-Schnittstelle unnötig dupliziert.

Neue Änderungen werden gegen diese Kriterien bewertet und nicht pauschal in die Pilot-Runtime gemischt.

