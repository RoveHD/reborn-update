# Pokémon Reborn – Launcher

Deutscher Windows-Launcher für die gemeinsame Reborn-/AllGen-Installation. Er aktualisiert zuerst sich selbst, startet neu und setzt danach das Spielupdate fort. Reborn-Logo und originales Reborn-Icon sind eingebettet. Die EXE bringt ihre .NET-Laufzeit mit.

## Für Mitspieler

1. Spiel und alten Launcher schließen.
2. [Reborn-Launcher-7.2.0-Setup.exe](https://github.com/RoveHD/reborn-update/releases/download/v7.2.0/Reborn-Launcher-7.2.0-Setup.exe) herunterladen und installieren. Keine Administratorrechte nötig.
3. Den Launcher über das Startmenü öffnen und einmal den vorhandenen Reborn-Spielordner auswählen.

Der Launcher ist separat unter `%LOCALAPPDATA%\Programs\RebornLauncher` installiert. Das Setup verändert keine Spieldateien. Neue veröffentlichte Launcher-Versionen kommen beim Start automatisch über GitHub. Ein vorhandener C#-Launcher 7.0/7.1 kann sich ebenfalls selbst aktualisieren; für den dauerhaften Installationsordner einmal das Setup verwenden.

Alte Python-Launcher brauchen einmalig das neue Setup. Das alte `version.txt`-Updateverfahren bleibt auf 6.1.0; das Spielpaket heißt `Reborn-AllGen-0.9.9.zip`. Dadurch installiert der alte Updater umbenannte Mods nicht ohne die notwendige Löschmigration. Das Spielpaket bleibt 7.0.0 / AllGen 0.9.9.

### Jahreszeiten und Gestaltung

Themes wechseln automatisch jedes Jahr nach dem lokalen Datum, auch wenn der Launcher über Mitternacht offen bleibt:

| Zeitraum | Design |
| --- | --- |
| 7. Januar bis Ende Februar | Standard: Nachtara / Absol |
| März–Mai | Frühling mit Haspiror / Shaymin, Blüten, Blättern und Pokémon-Eiern; Ostergruß um den jährlich berechneten Ostertermin |
| Juni–August | Sommer mit Schiggy / Aquana, Sonne, Wasserwellen und Blasen |
| September–14. Oktober und 3.–30. November | Herbst mit Folipurba / Kronjuwild und fallendem Laub |
| 15. Oktober–2. November | Halloween mit Irrbis / Gengar, Kürbissen und Geistern |
| 1.–30. Dezember und 3.–6. Januar | Winter/Weihnachten mit Botogel / Glaziola und Schneefall |
| 31. Dezember–2. Januar | Silvester/Neujahr mit Jirachi / Pikachu und Feuerwerk |

Eine sehr seltene Überraschung mit Arceus kann einen Tag lang erscheinen. Neustarts erhöhen die Tageschance nicht. Es gibt keine manuelle Themenauswahl. „Motiv wechseln“ tauscht nur das Pokémon innerhalb der aktuellen Saison.

Jede Saison hat eigene lokal eingebettete Musik. Ein/Aus, Lautstärke, Mausspur und reduzierte Effekte sind einstellbar. Musik pausiert beim Minimieren und während eines vom Launcher gestarteten Spiels. Mauszeiger und Leuchtspur passen sich der Saisonfarbe an. Unter der Saisonanzeige stehen Kalender und Musikquellen; vollständige Credits sind im Installationsordner enthalten.

Die automatische Spielaktualisierung ist standardmäßig aktiv und abschaltbar. Selbstupdates werden bei jedem Start und vor manuellen Updates geprüft. Nach erfolgreichem Austausch setzt die neue EXE den Updateablauf fort. Bei abgeschalteten Spielupdates oder fehlendem Internet lässt sich ein vorhandenes, konsistentes Spiel lokal starten.

### Zwischen zwei Spielversionen wechseln

Unter **Spielversion wählen** stehen nach dem ersten Versionsupdate mit diesem Launcher **Aktuell** und **Vorherige** bereit. Beide bleiben erhalten und lassen sich beliebig oft auswählen und starten. Reborn muss beim Umschalten geschlossen sein. Der Launcher tauscht die unterschiedlichen Dateien im vorhandenen Spielordner aus; es läuft jeweils die ausgewählte Version. Dafür ist kein erneuter Download erforderlich.

Die Auswahl bleibt nach dem Schließen erhalten. Solange **Vorherige** aktiv ist, pausieren automatische Spielupdates; Update und Reparatur werden erst nach dem Wechsel zu **Aktuell** wieder freigegeben. Ein weiteres Versionsupdate ersetzt das Paar: Aus A/B wird B/C. Eine Reparatur derselben Version erhält den vorhandenen Rückweg. Gespeichert werden die unterschiedlichen Dateien, keine vollständige zweite Kopie des Spiels.

Spielstände und persönliche Einstellungen werden beim Wechsel nicht zurückgesetzt. Ein mit einer neueren Spielversion gespeicherter Spielstand kann mit der älteren Version inkompatibel sein; vor dem ersten Start mit einer neuen Version wichtige Spielstände separat sichern. Bereits vor Installation dieses Launchers entfernte Spielversionen können nicht nachträglich wiederhergestellt werden.

**Downloadgröße:** Updates laden derzeit weiterhin das vollständige Spielpaket. Nur das lokale Ersetzen und die Sicherung sind auf geänderte Dateien begrenzt; Downloads einzelner Änderungen sind noch nicht implementiert.

**Dateien reparieren** lädt das geprüfte Spielpaket erneut, prüft vorhandene Dateien und ersetzt beschädigte oder veraltete Dateien. Spielstände, `settings.json`, `mkxp.json` und unbekannte zusätzliche Dateien bleiben erhalten. Mitgelieferte Mod-Dateien werden auf den Paketstand aktualisiert; eigene Änderungen an genau diesen Dateien gehören daher vorher in eine eigene Sicherung.

## Updateverfahren

- `update.json` enthält getrennte Versionen für Launcher und Spiel, feste Release-Adressen, Größen und SHA-256-Prüfsummen. Zusammengehörige Version und Download können dadurch nicht durch einen wechselnden `latest`-Link auseinanderlaufen.
- HTTPS-Zertifikate werden geprüft. Fehlerhafte Downloads werden vor der Installation verworfen. Ein fehlendes Manifest wird offen angezeigt; es gibt keinen Rückfall auf den unsicheren alten Updateablauf.
- Reborn muss geschlossen sein. Das Paket wird separat entpackt und geprüft. Identische Dateien bleiben unverändert. Alle betroffenen Originaldateien werden gesichert, bevor die erste Spieländerung erfolgt.
- Bei Fehler oder Abbruch wird der vorherige Stand wiederhergestellt. Nach Prozessabbruch wird das dauerhafte Updateprotokoll beim nächsten Start ausgewertet, bevor Reborn starten darf. Bei gesperrten Dateien bleiben Sicherung und Protokoll für einen erneuten Versuch erhalten.
- Löschungen sind nur für ausdrücklich genannte Dateien mit passender alter Prüfsumme erlaubt. Unbekannte Mods werden nicht pauschal entfernt.
- Mehrere belegte Altstände derselben Datei werden berücksichtigt. Kollidiert eine selbst veränderte alte Moddatei mit ihrer neuen Benennung, bricht das Update vor der Installation ab und nennt die Datei. Sie muss dann außerhalb des Spiels gesichert und die Anpassung gezielt übernommen werden.
- Beim Selbstupdate bleibt `launcher.exe.previous` als Rückweg erhalten. Der neu gestartete Prozess bestätigt den Austausch; ein nicht startfähiger neuer Launcher wird nach Möglichkeit durch die vorherige EXE ersetzt.

Der Launcher aktualisiert eine vorhandene Installation. Das aktuelle Paket basiert auf AllGen 0.9.9 mit den vorhandenen Anpassungen. Die Paketversion 7.0.0 ist unabhängig von der AllGen-Versionsnummer.

## Quellcode, Build und Tests

Die vorhandenen C#-/WPF-Quellen wurden vom Desktop nach `src/RebornLauncher` übernommen und dort überarbeitet. Die ursprünglichen Desktop-Dateien bleiben unverändert. Zum Bauen sind Windows und .NET SDK 9 nötig; Mitspieler benötigen kein separates .NET.

```powershell
./tools/build.ps1
./tools/build-installer.ps1 -BootstrapCompiler
./installer/test-installer.ps1
dotnet run --project tests/CoreTests -c Release
python -m unittest discover -s tests -p 'test_*.py' -v
./dist/launcher/launcher.exe --smoke-test ./test-results/ui/result.json
./tools/test-self-update.ps1
```

Ergebnis: `dist/launcher/launcher.exe`. Das Icon unter `src/RebornLauncher/assets/reborn.ico` stammt aus der vorhandenen Reborn-`Game.exe`; `logo.png` aus dem bisherigen Launcher. Die Vorschau rendert das tatsächliche WPF-Fenster groß und bei Mindestgröße. Die Selbstupdate-Prüfung tauscht zwei echte EXEs in einem isolierten Ordner aus und kontrolliert Neustart, Sicherung und unveränderte Test-Spielstände.

## Spielpaket bauen

```powershell
python tools/package_release.py --game "D:/Pfad/geprüftes-Spiel" --previous-game "D:/Pfad/altes-Spiel" --launcher dist/launcher/launcher.exe --output dist/release-7.0.0 --version 7.0.0 --notes release-notes-7.0.0.txt
dotnet run --project tests/ReleaseChecks -- dist/release-7.0.0 test-results/full-package
```

Der Paketbau nimmt nur vorgesehene Spielordner und Engine-Dateien auf. Spielstände, persönliche Einstellungen, Logs, Verknüpfungen, Testharnesses und Launcher werden ausgeschlossen. Jede ZIP-Datei wird anschließend erneut gelesen und per SHA-256 gegen die Quelle geprüft. `game-files.json` enthält das Dateiinventar. Wiederholbares `--previous-game` und `--previous-archive` erstellen hashgeschützte Löschanweisungen aus bekannten alten Quellen. Bei umbenannten Mods wird zusätzlich der neue Zielpfad als `replacedBy` vermerkt.

Für Release 7.0.0 wird zusätzlich das tatsächlich veröffentlichte 6.1.0-Paket in eine neue Testkopie entpackt. Weil dieses alte Paket nur einen Patch enthält, ergänzt eine separate alte Spielkopie ausschließlich fehlende Basisdateien. Der Test akzeptiert hierfür nur Pfade auf D:. Danach werden Reparatur und ein synthetisches Folgeupdate in derselben isolierten Testkopie geprüft:

```powershell
dotnet run --project tests/ReleaseChecks -c Release -- dist/release-7.0.0 test-results/migration-6.1.0 --migration artifacts/previous-6.1.0/Reborn-19.5.0.zip "D:/Pfad/alte-Test-Spielkopie"
dotnet run --project tests/ReleaseChecks -c Release -- dist/release-7.0.0 test-results/migration-6.1.0 --repair
dotnet run --project tests/ReleaseChecks -c Release -- dist/release-7.0.0 test-results/migration-6.1.0 --switch
```

Für einen erneuten vollständigen Testlauf wieder mit `--migration` beginnen. Der letzte Wechseltest hinterlässt absichtlich die vorherige Version ausgewählt; eine anschließende Reparatur wäre deshalb gesperrt.

Für eine neue Version die Versionsangaben im Projekt anheben, neu bauen und testen. Launcher und Spiel dürfen im Manifest unterschiedliche Versionen haben; das Veröffentlichungswerkzeug ist für gemeinsame Releases ausgelegt.

## Veröffentlichung

Lokales Bauen veröffentlicht nichts. Nach den Installations-, Reparatur- und Selbstupdate-Prüfungen bindet `python tools/finalize_release.py` die geprüfte EXE und die Nachweise an das vorbereitete Release 7.0.0. Die Ergebnisse stehen in [docs/PRUEFUNG-7.0.0.md](docs/PRUEFUNG-7.0.0.md). Nach ausdrücklicher Freigabe zuerst die Quellcodeänderungen ins Repository übertragen.

```powershell
# Nur prüfen; GitHub bleibt unverändert:
./tools/publish-release.ps1 -ReleaseDirectory dist/release-7.0.0

# Bewusst veröffentlichen:
./tools/publish-release.ps1 -ReleaseDirectory dist/release-7.0.0 -Publish
```

Benötigt eine angemeldete GitHub CLI. Das Skript erstellt zuerst einen Entwurf am veröffentlichten Quellcode-Commit, lädt alle Assets hoch und vergleicht die von GitHub gemeldeten Größen und Prüfsummen. Dann wird der Release öffentlich. `update.json` und `patchnotes.txt` werden danach gemeinsam auf `main` aktualisiert. Veröffentlichte Releases werden nicht überschrieben. Das alte `version.txt` bleibt absichtlich auf 6.1.0; der neue Launcher nutzt ausschließlich das geprüfte Manifest und versionsgebundene URLs.

## Grenzen und Rückweg

Die EXE besitzt kein gekauftes Code-Signing-Zertifikat; Windows kann daher eine Reputationsmeldung zeigen. Die Prüfungen umfassen lokale Installation, Wiederherstellung und echte EXE-Neustarts. Der Download durch Freunde ist erst nach Veröffentlichung separat zu bestätigen.

Bei einem fehlgeschlagenen Spielupdate den Launcher neu starten und die Wiederherstellung abwarten. `.reborn-updater` während einer unvollständigen Installation nicht löschen. Für eine manuelle Launcher-Rückkehr bei geschlossenem Launcher `launcher.exe.previous` als `launcher.exe` zurückkopieren.
