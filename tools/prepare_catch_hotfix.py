"""Build a verified game-only 7.3.2 ZIP update from the published 7.3.1 package."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile

import package_release as package

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.2"
BASE_VERSION = "7.3.1"
ARCHIVE_NAME = package.GAME_ARCHIVE
REPOSITORY = "RoveHD/reborn-update"
BASE_URL = f"https://github.com/{REPOSITORY}/releases/download/v{BASE_VERSION}/"
INVENTORY_URL = BASE_URL + "game-files.json"
BASE_ARCHIVE_SHA256 = "1bc86c047f6c2b7a96e12f5478811e6bf714e6ad3780c14db3f0306850365c97"
BASE_ARCHIVE_SIZE = 1_476_583_417
BASE_INVENTORY_SHA256 = "407486539c3f3a6274c6becd56149707d1f9fbbbf516cf414f9b81fe30ec3b6c"
MOD_RELATIVE = "patch/Mods/zzLegendaryCatchDifficulty.rb"
MOD_SHA256 = "736909fc0eca9785c2a119a7d72f085a1df804de8c8b2e2344cda6ba74c63db3"
EXPECTED_FILES = 18_864
CHUNK_SIZE = 1024 * 1024


def sha256_stream(stream):
    digest = hashlib.sha256()
    size = 0
    while True:
        block = stream.read(CHUNK_SIZE)
        if not block:
            break
        digest.update(block)
        size += len(block)
    return digest.hexdigest(), size


def sha256_file(path: Path):
    with path.open("rb") as stream:
        return sha256_stream(stream)


def download(url: str, destination: Path, expected_hash: str, expected_size: int | None = None):
    request = urllib.request.Request(url, headers={"User-Agent": "Reborn-Game-Hotfix/7.3.2"})
    digest = hashlib.sha256()
    size = 0
    with urllib.request.urlopen(request, timeout=120) as response, destination.open("wb") as output:
        while True:
            block = response.read(CHUNK_SIZE)
            if not block:
                break
            output.write(block)
            digest.update(block)
            size += len(block)
    if digest.hexdigest() != expected_hash or (expected_size is not None and size != expected_size):
        destination.unlink(missing_ok=True)
        raise ValueError(f"Download stimmt nicht mit der veröffentlichten Prüfsumme überein: {url}")


def obtain_input(local: Path, url: str, expected_hash: str, expected_size: int | None, scratch: Path):
    if local.is_file():
        digest, size = sha256_file(local)
        if digest == expected_hash and (expected_size is None or size == expected_size):
            return local
    target = scratch / local.name
    download(url, target, expected_hash, expected_size)
    return target


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def build(archive: Path, inventory_path: Path, mod_path: Path, notes_path: Path, output: Path):
    channel_path = ROOT / "update.json"
    channel = read_json(channel_path)
    baseline_inventory = read_json(inventory_path)
    if channel.get("schemaVersion") != 1 or channel["game"].get("version") != BASE_VERSION:
        raise ValueError("Der Live-Kanal ist nicht mehr auf dem geprüften Spielstand 7.3.1.")
    if channel["game"].get("url") != BASE_URL + ARCHIVE_NAME or channel["game"].get("sha256") != BASE_ARCHIVE_SHA256 or channel["game"].get("size") != BASE_ARCHIVE_SIZE:
        raise ValueError("Der lokale Update-Kanal weicht vom veröffentlichten 7.3.1-Paket ab.")
    if len(baseline_inventory) != EXPECTED_FILES or MOD_RELATIVE not in baseline_inventory:
        raise ValueError("Das veröffentlichte 7.3.1-Inventar hat nicht die erwartete Dateimenge.")
    inv_hash, _ = sha256_file(inventory_path)
    if inv_hash != BASE_INVENTORY_SHA256:
        raise ValueError("Das heruntergeladene Spielinventar ist nicht das geprüfte 7.3.1-Inventar.")
    mod_hash, mod_size = sha256_file(mod_path)
    if mod_hash != MOD_SHA256:
        raise ValueError("Die Fangschwierigkeits-Mod stimmt nicht mit der geprüften 7.3.2-Version überein.")
    if output.exists():
        raise FileExistsError(f"Zielordner existiert bereits; wird nicht überschrieben: {output}")
    if not package.allowed(MOD_RELATIVE):
        raise ValueError("Der Mod-Pfad ist für das Spielpaket nicht freigegeben.")

    output.mkdir(parents=True)
    result_archive = output / ARCHIVE_NAME
    pending_archive = output / (ARCHIVE_NAME + ".partial")
    new_inventory = {}
    seen = set()
    replaced = False
    try:
        with zipfile.ZipFile(archive, "r") as source, zipfile.ZipFile(
            pending_archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True
        ) as target:
            target.comment = source.comment
            infos = [entry for entry in source.infolist() if not entry.is_dir()]
            if len(infos) != EXPECTED_FILES:
                raise ValueError(f"Unerwartete Zahl an Archivdateien: {len(infos)}")
            for info in infos:
                relative = info.filename.replace("\\", "/")
                if relative.casefold() in seen:
                    raise ValueError(f"Doppelter Windows-Pfad im Archiv: {relative}")
                seen.add(relative.casefold())
                if not package.allowed(relative) or relative not in baseline_inventory:
                    raise ValueError(f"Unbekannter oder nicht erlaubter Eintrag im Basisarchiv: {relative}")
                expected = baseline_inventory[relative]
                if relative == MOD_RELATIVE:
                    with source.open(info, "r") as original:
                        original_hash, original_size = sha256_stream(original)
                    if original_hash != expected["sha256"] or original_size != expected["size"]:
                        raise ValueError(f"Basis-Mod stimmt nicht mit dem 7.3.1-Inventar ueberein: {relative}")
                    expected = {"sha256": MOD_SHA256, "size": mod_size}
                    with mod_path.open("rb") as incoming, target.open(copy.copy(info), "w", force_zip64=True) as outgoing:
                        digest = hashlib.sha256()
                        copied = 0
                        while True:
                            block = incoming.read(CHUNK_SIZE)
                            if not block:
                                break
                            outgoing.write(block)
                            digest.update(block)
                            copied += len(block)
                    actual_hash, actual_size = digest.hexdigest(), copied
                    replaced = True
                else:
                    digest = hashlib.sha256()
                    copied = 0
                    with source.open(info, "r") as incoming, target.open(copy.copy(info), "w", force_zip64=True) as outgoing:
                        while True:
                            block = incoming.read(CHUNK_SIZE)
                            if not block:
                                break
                            outgoing.write(block)
                            digest.update(block)
                            copied += len(block)
                    actual_hash, actual_size = digest.hexdigest(), copied
                if actual_hash != expected["sha256"] or actual_size != expected["size"]:
                    raise ValueError(f"Basisdatei stimmt nicht mit dem 7.3.1-Inventar überein: {relative}")
                new_inventory[relative] = {"sha256": actual_hash, "size": actual_size}
            if len(seen) != EXPECTED_FILES or set(new_inventory) != set(baseline_inventory) or not replaced:
                raise ValueError("Das Basisarchiv ist unvollständig oder die Mod wurde nicht ersetzt.")
        pending_archive.replace(result_archive)

        # Re-open the completed ZIP and validate each final archived file against
        # the newly computed inventory, including the replaced mod.
        with zipfile.ZipFile(result_archive, "r") as final_zip:
            final_infos = [entry for entry in final_zip.infolist() if not entry.is_dir()]
            if len(final_infos) != EXPECTED_FILES:
                raise ValueError("Das fertige Archiv enthält nicht exakt 18.864 Dateien.")
            final_seen = set()
            for info in final_infos:
                relative = info.filename.replace("\\", "/")
                if relative.casefold() in final_seen or relative not in new_inventory:
                    raise ValueError(f"Doppelter oder unerwarteter Pfad im fertigen Archiv: {relative}")
                final_seen.add(relative.casefold())
                with final_zip.open(info, "r") as stream:
                    digest, size = sha256_stream(stream)
                if digest != new_inventory[relative]["sha256"] or size != new_inventory[relative]["size"]:
                    raise ValueError(f"Abschlussprüfung fehlgeschlagen: {relative}")

        changed = sorted(path for path, entry in new_inventory.items() if baseline_inventory.get(path) != entry)
        if changed != [MOD_RELATIVE]:
            raise ValueError(f"Es wurde mehr als die Fang-Mod geändert: {changed}")
        manifest = json.loads(json.dumps(channel))
        archive_hash, archive_size = sha256_file(result_archive)
        base = f"https://github.com/{REPOSITORY}/releases/download/v{VERSION}/"
        manifest["game"]["version"] = VERSION
        manifest["game"]["url"] = base + ARCHIVE_NAME
        manifest["game"]["sha256"] = archive_hash
        manifest["game"]["size"] = archive_size
        notes = notes_path.read_text(encoding="utf-8-sig").strip()
        manifest["patchNotes"] = notes

        write_json(output / "update.json", manifest)
        write_json(output / "game-files.json", new_inventory)
        package_report = {
            "success": True, "source": f"published game {BASE_VERSION}", "version": VERSION,
            "files": len(new_inventory), "changedPaths": changed,
            "uncompressedBytes": sum(item["size"] for item in new_inventory.values()),
            "archiveBytes": archive_size, "everyArchivedFileSha256Verified": True,
            "personalFilesIncluded": False, "launcherIncluded": False,
        }
        write_json(output / "package-checks.json", package_report)
        hotfix_report = {
            "success": True, "gameVersion": VERSION, "baselineGameVersion": BASE_VERSION,
            "launcherUnchanged": manifest["launcher"] == channel["launcher"],
            "onlyChangedPaths": changed, "removedCurrentFiles": [], "published": False,
            "files": len(new_inventory), "archiveSha256": archive_hash,
        }
        write_json(output / "hotfix-checks.json", hotfix_report)
        catch_checks = mod_path.parents[2] / "catch-checks.json"
        if not catch_checks.is_file():
            raise FileNotFoundError(f"Prüfnachweis für den Fang-Mod fehlt: {catch_checks}")
        if read_json(catch_checks).get("sha256") != MOD_SHA256:
            raise ValueError("Der Ruby-Prüfnachweis gehört nicht zur geprüften Mod-Datei.")
        shutil.copy2(catch_checks, output / "catch-checks.json")
        (output / "patchnotes.txt").write_text(notes + "\n", encoding="utf-8")
        sums = [
            f"{sha256_file(output / name)[0]}  {name}"
            for name in (ARCHIVE_NAME, "update.json", "game-files.json", "package-checks.json", "hotfix-checks.json", "catch-checks.json", "patchnotes.txt")
        ]
        (output / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n", encoding="utf-8")
        print(json.dumps({"success": True, "version": VERSION, "files": len(new_inventory),
                          "changedPaths": changed, "archiveSha256": archive_hash,
                          "archiveBytes": archive_size}, ensure_ascii=False))
    except Exception:
        # Keep a partial directory only if it contains a completed ZIP, so the
        # failure can be inspected; never silently reuse a partial archive.
        if not result_archive.exists():
            shutil.rmtree(output, ignore_errors=True)
        raise
    finally:
        pending_archive.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-archive", type=Path, help="Use a local verified 7.3.1 ZIP")
    parser.add_argument("--inventory", type=Path, help="Use a local verified 7.3.1 game-files.json")
    parser.add_argument("--mod", type=Path, help="Use a local reviewed 7.3.2 catch mod")
    parser.add_argument("--notes", type=Path, help="Use local release notes")
    parser.add_argument("--output", type=Path, default=ROOT / "dist/release-game-7.3.2")
    args = parser.parse_args()

    defaults = {
        "baseline_archive": args.baseline_archive or ROOT / "dist/release-game-7.3.1-release" / ARCHIVE_NAME,
        "inventory": args.inventory or ROOT / "dist/release-game-7.3.1-release/game-files.json",
        "mod": args.mod or ROOT / "game-hotfixes/7.3.2" / MOD_RELATIVE,
        "notes": args.notes or ROOT / "release-notes-game-7.3.2.md",
    }
    with tempfile.TemporaryDirectory(prefix="reborn-7.3.2-") as temporary:
        scratch = Path(temporary)
        archive = obtain_input(defaults["baseline_archive"], BASE_URL + ARCHIVE_NAME,
                              BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE, scratch)
        inventory = obtain_input(defaults["inventory"], INVENTORY_URL,
                                 BASE_INVENTORY_SHA256, None, scratch)
        if not defaults["mod"].is_file() or not defaults["notes"].is_file():
            raise FileNotFoundError("Lokale Fang-Mod oder Release-Notizen fehlen.")
        build(archive, inventory, defaults["mod"], defaults["notes"], args.output.resolve())


if __name__ == "__main__":
    main()
