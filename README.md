# Pokemon Reborn Launcher

Custom launcher with auto-update system, patch notes, and modern UI.

---

## Features

* Auto update system (optional)
* Manual update check
* Patch notes viewer
* Download progress with speed display
* Cancel update
* Dark mode UI
* Fullscreen launcher
* Background + logo support
* Fade-in animation

---

## How it works

The launcher checks a remote version file:

```
version.txt
```

If the version is different, it downloads:

```
Reborn-19.5.0.zip
```

Then:

* extracts files
* updates local version
* launches game

---

## File Structure

```
/launcher
 ├── updater.exe
 ├── Game.exe
 ├── version.txt
 ├── settings.json
 ├── background.png
 ├── logo.png
 ├── icon.ico
```

---

## Auto Update

Auto update can be enabled in settings:

* Enabled → updates automatically on start
* Disabled → manual update only

---

## URLs

* Version:
  https://raw.githubusercontent.com/RoveHD/reborn-update/main/version.txt

* Patch Notes:
  https://raw.githubusercontent.com/RoveHD/reborn-update/main/patchnotes.txt

* Download:
  https://github.com/RoveHD/reborn-update/releases/latest/download/Reborn-19.5.0.zip

---

## Build (PyInstaller)

Recommended settings:

```
--onefile
--noconsole
--add-data "background.png;."
--add-data "logo.png;."
--add-data "icon.ico;."
```

---

## Notes

* The launcher cannot update itself while running
* For self-updating, a separate updater is required
* SSL verification is disabled (`verify=False`) → not recommended for production

---

## Credits

Launcher made by Elias
