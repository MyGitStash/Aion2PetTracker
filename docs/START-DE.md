# Aion2 Pet Tracker starten

## Einmalig einrichten

1. `Aion2-Pet-Tracker.zip` vollständig in einen eigenen Ordner entpacken.
2. `Aion2PetTracker.exe` öffnen.
3. Falls Npcap fehlt, zeigt der Tracker eine Einrichtungshilfe. Auf **Open official
   Npcap download** klicken, Npcap von der Herstellerseite herunterladen und den
   Installer durchlaufen. Danach im Tracker **Check again** anklicken.
4. Den Tracker geöffnet lassen und erst jetzt AION 2 starten. Normal einloggen.

Npcap ist der einmalig benötigte Windows-Treiber für den Empfang. Er ist kein
zweites Programm, das du jedes Mal öffnen musst. Die .NET-Laufzeit und der
Pet-Katalog stecken bereits in der EXE.

## Im Alltag

**Tracker öffnen → Spiel starten → spielen.** Charakter und Server werden
automatisch erkannt. Ziehe die Anzeigen an die gewünschte Stelle und aktiviere
danach **Lock overlays · Click through to game**. Die normale Anzeige enthält
die sechs Kristallbestände; beim Farmen erscheint pro Pet eine eigene Karte.
Sie verschwindet 60 Sekunden nach der letzten Seele genau dieses Pets.

Beim Cloud Merchant erscheinen Pet-Informationen in einem separaten Fenster,
wenn Angebote im Bereich Pets oder Special erkannt werden. Die Zahlen zeigen
registrierte Soul-Punkte, nicht unbenutzte Pet-Gegenstände im Inventar.

Das X im Einstellungsfenster verschiebt den Tracker ins Infobereich-Symbol.
Er empfängt und speichert weiter. Zum Beenden dort rechtsklicken und **Exit**
wählen. Alt-Tab löscht keine Bestände.

## Daten behalten und erste Synchronisierung

Der Ordner `Daten` neben der EXE enthält deine Einstellungen und die automatisch
getrennten Charakterbestände. Bei einem späteren Update diesen Ordner behalten
und die EXE bei beendetem Tracker ersetzen. Im geteilten Download sind keine
persönlichen Bestände enthalten.

Wenn das Spiel bereits läuft, kann der Tracker neue Seelen und gespeicherte
Werte zeigen. Für die erste vollständige Sammlung muss er den Spielstart
mitbekommen. Verpasste Käufe lassen sich nicht aus einem Owned-Schild berechnen.
Ein Menü- oder Gebietswechsel liefert die Sammlung nicht zuverlässig erneut;
der nächste normale vollständige Spielstart kann sie nachliefern.

`≈` bedeutet eine erkannte Empfangslücke oder eine noch nicht unterstützte
Änderung. Eine Spielpause allein erzeugt das Zeichen nicht.

## Wenn nichts ankommt

- Im Tracker prüfen, ob Npcap fehlt oder auf das Spiel gewartet wird.
- Npcap gegebenenfalls über die Einrichtungshilfe installieren.
- Tracker vor dem normalen Spielstart öffnen.
- Falls Npcap ausdrücklich nur für Administratoren installiert wurde, dessen
  Windows-Berechtigungsabfrage beachten oder die Npcap-Einstellungen prüfen.
- Bei einem verdeckten oder anders skalierten Shop-Kopf kann das separate
  Händlerfenster verborgen bleiben, obwohl die Soul-Erfassung weiterläuft.

Es gibt keine Injektion und keinen Spielspeicherzugriff. Eine offizielle Freigabe
oder Garantie gegen Spielsperren wird für dieses unabhängige Projekt nicht behauptet.
