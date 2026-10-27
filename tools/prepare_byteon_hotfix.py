"""Prepare the reviewed Byteon evolution hotfix 7.3.13 and full fallback."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import urllib.request
import copy
import zipfile
import build_game_hotfix

import package_release as package

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.13"
BASE_VERSION = "7.3.12"
SMART_DELTA_LAUNCHER_VERSION = "7.4.0"
BASE_ARCHIVE_SHA256 = "46d25df0aed1bf8c7c30773e753cdff6b97fe4e7dd6554052d1e8bd4e3d0d955"
BASE_ARCHIVE_SIZE = 1476970387
BASE_INVENTORY_SHA256 = "741924b1187798c1d3ed3c57f3b176c42afcd5be5b71b570d4678647286ef961"
CHUNK = 1024 * 1024
BASE_URL = f"https://github.com/RoveHD/reborn-update/releases/download/v{BASE_VERSION}/"


def sha256_file(path: Path):
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(CHUNK), b""):
            digest.update(block)
            size += len(block)
    return digest.hexdigest(), size


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def approved_payload(payload_root: Path, manifest_path: Path, baseline_inventory: dict):
    approval = read_json(manifest_path)
    entries = approval.get("files") if isinstance(approval, dict) else None
    if not isinstance(entries, list) or not entries:
        raise ValueError("approved-files.json must contain a non-empty files list.")
    approved = {}
    folded = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {"path", "sha256", "size"}:
            raise ValueError("Every approval entry must contain exactly path, sha256 and size.")
        relative = entry["path"]
        if not isinstance(relative, str) or relative != relative.replace("\\", "/"):
            raise ValueError("Approved paths must use forward slashes.")
        parts = relative.split("/")
        reserved = {"CON", "PRN", "AUX", "NUL"} | {"COM%d" % n for n in range(1, 10)} | {"LPT%d" % n for n in range(1, 10)}
        if (any(part in ("", ".", "..") or ":" in part or part.endswith((".", " ")) or
                part.split(".", 1)[0].upper() in reserved for part in parts) or
                relative.startswith(("/", "\\"))):
            raise ValueError("Unsafe approved path: " + relative)
        key = relative.casefold()
        if key in folded or not package.allowed(relative):
            raise ValueError("Duplicate or non-game path in approval: " + relative)
        folded.add(key)
        baseline_case = {path.casefold(): path for path in baseline_inventory}
        if key in baseline_case and relative != baseline_case[key]:
            raise ValueError("Approved path casing differs from the baseline: " + relative)
        digest, size = entry["sha256"], entry["size"]
        if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest.lower()):
            raise ValueError("Invalid SHA256 in approval for " + relative)
        if not isinstance(size, int) or isinstance(size, bool) or size < 0:
            raise ValueError("Invalid size in approval for " + relative)
        source = payload_root.joinpath(*parts)
        if not source.is_file():
            raise FileNotFoundError("Approved payload file missing: " + str(source))
        try:
            source.resolve().relative_to(payload_root.resolve())
        except ValueError:
            raise ValueError("Approved payload path resolves outside payload root: " + relative)
        if sha256_file(source) != (digest.lower(), size):
            raise ValueError("Payload hash/size differs from approval: " + relative)
        approved[relative] = {"sha256": digest.lower(), "size": size, "source": source}
    actual = {p.relative_to(payload_root).as_posix() for p in payload_root.rglob("*") if p.is_file()}
    if actual != set(approved):
        raise ValueError("Payload contains files outside the exact approval list: " + repr(sorted(actual - set(approved))))
    return approved


def download(url: str, destination: Path, expected_hash: str, expected_size=None):
    request = urllib.request.Request(url, headers={"User-Agent": "Reborn-Game-Hotfix/7.3.13"})
    digest, size = hashlib.sha256(), 0
    with urllib.request.urlopen(request, timeout=120) as response, destination.open("wb") as output:
        for block in iter(lambda: response.read(CHUNK), b""):
            output.write(block)
            digest.update(block)
            size += len(block)
    if digest.hexdigest() != expected_hash or (expected_size is not None and size != expected_size):
        destination.unlink(missing_ok=True)
        raise ValueError("Downloaded public baseline does not match its pinned hash: " + url)


def ci_payload(payload_root: Path, approval_path: Path):
    """Normalize checkout CRLF for approved text payloads when that yields the reviewed bytes."""
    approved = read_json(approval_path)["files"]
    for entry in approved:
        path = payload_root.joinpath(*entry["path"].split("/"))
        if path.is_file():
            raw = path.read_bytes()
            normalized = raw.replace(b"\r\n", b"\n")
            if hashlib.sha256(normalized).hexdigest() == entry["sha256"].lower() and normalized != raw:
                path.write_bytes(normalized)


def build(archive: Path, inventory_path: Path, channel_path: Path, approval_path: Path,
          payload_root: Path, notes_path: Path, output: Path):
    channel_bytes = channel_path.read_bytes()
    channel = read_json(channel_path)
    inventory_bytes_hash, _ = sha256_file(inventory_path)
    baseline = read_json(inventory_path)
    if inventory_bytes_hash != BASE_INVENTORY_SHA256:
        raise ValueError("Baseline game-files.json does not match the verified public 7.3.12 hash.")
    expected_game = {"version": BASE_VERSION,
                     "url": f"https://github.com/RoveHD/reborn-update/releases/download/v{BASE_VERSION}/{package.GAME_ARCHIVE}",
                     "sha256": BASE_ARCHIVE_SHA256, "size": BASE_ARCHIVE_SIZE}
    if channel.get("schemaVersion") != 1 or any(channel.get("game", {}).get(k) != v for k, v in expected_game.items()):
        raise ValueError("update.json game baseline differs from published 7.3.12.")
    if channel.get("launcher", {}).get("version") != SMART_DELTA_LAUNCHER_VERSION:
        raise ValueError("Publish the smart-delta launcher before preparing this small game delta.")
    if sha256_file(archive) != (BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE):
        raise ValueError("Baseline ZIP does not match the verified public 7.3.12 asset.")
    approved = approved_payload(payload_root, approval_path, baseline)
    if not notes_path.is_file():
        raise FileNotFoundError("Root-provided release-notes-game-7.3.13.md is required.")
    if output.exists():
        raise FileExistsError("Output already exists; refusing to overwrite: " + str(output))

    output.mkdir(parents=True)
    final_archive = output / package.GAME_ARCHIVE
    pending = output / (package.GAME_ARCHIVE + ".partial")
    new_inventory = {}
    seen = set()
    try:
        with zipfile.ZipFile(archive) as source, zipfile.ZipFile(
                pending, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as target:
            target.comment = source.comment
            files = [item for item in source.infolist() if not item.is_dir()]
            if len(files) != len(baseline):
                raise ValueError("Baseline archive file count differs from its inventory.")
            for info in files:
                relative = info.filename.replace("\\", "/")
                key = relative.casefold()
                if key in seen or relative not in baseline or not package.allowed(relative):
                    raise ValueError("Duplicate, unknown or forbidden baseline path: " + relative)
                seen.add(key)
                expected = approved.get(relative, baseline[relative])
                source_stream = approved[relative]["source"].open("rb") if relative in approved else source.open(info, "r")
                digest = hashlib.sha256()
                size = 0
                with source_stream as incoming, target.open(copy.copy(info), "w", force_zip64=True) as outgoing:
                    for block in iter(lambda: incoming.read(CHUNK), b""):
                        outgoing.write(block)
                        digest.update(block)
                        size += len(block)
                if (digest.hexdigest(), size) != (expected["sha256"], expected["size"]):
                    raise ValueError("Copied archive file failed verification: " + relative)
                new_inventory[relative] = {"sha256": digest.hexdigest(), "size": size}
            additions = sorted(set(approved) - set(baseline))
            for relative in additions:
                entry = approved[relative]
                info = zipfile.ZipInfo(relative, date_time=(2026, 10, 27, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = (0o100644 & 0xFFFF) << 16
                info.flag_bits = 0
                with entry["source"].open("rb") as incoming, target.open(info, "w", force_zip64=True) as outgoing:
                    digest = hashlib.sha256()
                    size = 0
                    for block in iter(lambda: incoming.read(CHUNK), b""):
                        outgoing.write(block)
                        digest.update(block)
                        size += len(block)
                if (digest.hexdigest(), size) != (entry["sha256"], entry["size"]):
                    raise ValueError("Added payload file failed verification: " + relative)
                new_inventory[relative] = {"sha256": digest.hexdigest(), "size": size}
            if set(new_inventory) != set(baseline) | set(approved):
                raise ValueError("Baseline archive did not preserve the complete inventory.")
        pending.replace(final_archive)
        with zipfile.ZipFile(final_archive) as check:
            if check.testzip() is not None or len(check.infolist()) != len(new_inventory):
                raise ValueError("Completed fallback ZIP failed structural validation.")
            for info in check.infolist():
                with check.open(info) as stream:
                    digest, size = hashlib.sha256(), 0
                    for block in iter(lambda: stream.read(CHUNK), b""):
                        digest.update(block)
                        size += len(block)
                item = new_inventory[info.filename.replace("\\", "/")]
                if digest.hexdigest() != item["sha256"] or size != item["size"]:
                    raise ValueError("Final fallback verification failed: " + info.filename)

        manifest = json.loads(json.dumps(channel))
        manifest["gameHotfixes"] = []
        manifest["gameHotfixesV2"] = []
        archive_hash, archive_size = sha256_file(final_archive)
        release_base = f"https://github.com/RoveHD/reborn-update/releases/download/v{VERSION}/"
        manifest["game"].update(version=VERSION, url=release_base + package.GAME_ARCHIVE,
                                sha256=archive_hash, size=archive_size)
        notes = notes_path.read_text(encoding="utf-8-sig").strip()
        manifest["patchNotes"] = notes
        addon_name = f"Reborn-7.3.13-Zusatzdateien.zip"
        addon_path = output / addon_name
        with zipfile.ZipFile(addon_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as delta:
            for relative in sorted(approved):
                entry = approved[relative]
                info = zipfile.ZipInfo(relative, date_time=(2026, 10, 27, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = (0o100644 & 0xFFFF) << 16
                with entry["source"].open("rb") as source, delta.open(info, "w") as target:
                    target.write(source.read())
        write_json(output / "update.json", manifest)
        write_json(output / "game-files.json", new_inventory)
        proof = {"success": True, "version": VERSION, "baselineVersion": BASE_VERSION,
                 "baselineArchiveSha256": BASE_ARCHIVE_SHA256, "baselineInventorySha256": BASE_INVENTORY_SHA256,
                 "approvedManifestSha256": sha256_file(approval_path)[0],
                 "approvedFiles": [{"path": p, "sha256": v["sha256"], "size": v["size"]} for p, v in sorted(approved.items())],
                 "newFiles": sorted(set(approved) - set(baseline)), "launcherVersion": SMART_DELTA_LAUNCHER_VERSION, "launcherUnchanged": manifest["launcher"] == channel["launcher"],
                 "removedFilesPreserved": manifest["game"].get("removedFiles") == channel["game"].get("removedFiles"),
                 "published": False, "fullFallbackSha256": archive_hash}
        if not proof["launcherUnchanged"] or not proof["removedFilesPreserved"]:
            raise ValueError("Launcher metadata or historical removedFiles changed.")
        write_json(output / "package-checks.json", {"success": True, "source": "published game 7.3.12",
                   "version": VERSION, "files": len(new_inventory), "changedPaths": sorted(approved),
                   "uncompressedBytes": sum(x["size"] for x in new_inventory.values()), "archiveBytes": archive_size,
                   "everyArchivedFileSha256Verified": True, "personalFilesIncluded": False,
                   "launcherIncluded": False, "removedFiles": manifest["game"].get("removedFiles", [])})
        write_json(output / "hotfix-checks.json", {"success": True, "gameVersion": VERSION,
                   "baselineGameVersion": BASE_VERSION, "launcherUnchanged": proof["launcherUnchanged"],
                   "onlyChangedPaths": sorted(approved), "newFiles": proof["newFiles"], "removedCurrentFiles": [],
                   "historicalRemovedFilesPreserved": proof["removedFilesPreserved"], "published": False,
                   "files": len(new_inventory), "archiveSha256": archive_hash,
                   "approvedManifestSha256": proof["approvedManifestSha256"]})
        write_json(output / "byteon-hotfix-checks.json", proof)
        (output / "patchnotes.txt").write_text(notes + "\n", encoding="utf-8")
        sums = [f"{sha256_file(output / name)[0]}  {name}" for name in
                [package.GAME_ARCHIVE, "update.json", "game-files.json", "package-checks.json",
                 "hotfix-checks.json", "byteon-hotfix-checks.json", "patchnotes.txt", addon_name]]
        (output / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n", encoding="utf-8")
        if channel_path.read_bytes() != channel_bytes:
            raise ValueError("Current update.json changed during preparation.")
        print(json.dumps({"success": True, "version": VERSION, "files": len(new_inventory),
                          "changedPaths": sorted(approved), "archiveSha256": archive_hash,
                          "archiveBytes": archive_size, "published": False}))
    finally:
        pending.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", type=Path, default=ROOT / "dist/release-game-7.3.12")
    parser.add_argument("--channel", type=Path, default=ROOT / "update.json")
    parser.add_argument("--approval", type=Path, default=ROOT / "game-hotfixes/7.3.13/approved-files.json")
    parser.add_argument("--payload", type=Path, default=ROOT / "game-hotfixes/7.3.13/payload")
    parser.add_argument("--notes", type=Path, default=ROOT / "release-notes-game-7.3.13.md")
    parser.add_argument("--output", type=Path, default=ROOT / "dist/release-game-7.3.13-channel")
    parser.add_argument("--ci", action="store_true")
    parser.add_argument("--expected-archive-sha256")
    args = parser.parse_args()
    baseline = args.baseline
    if args.ci:
        if os.environ.get("GITHUB_ACTIONS") != "true" or os.name != "nt":
            raise RuntimeError("CI mode requires the Windows GitHub Actions runner.")
        import sys
        import zlib
        if sys.version_info[:3] != (3, 8, 10) or zlib.ZLIB_RUNTIME_VERSION != "1.2.11":
            raise RuntimeError("Reproduce with Python 3.8.10 / zlib 1.2.11.")
        if not args.expected_archive_sha256 or len(args.expected_archive_sha256) != 64:
            raise ValueError("CI requires the locally reviewed full fallback SHA256.")
        baseline.mkdir(parents=True, exist_ok=True)
        download(BASE_URL + package.GAME_ARCHIVE, baseline / package.GAME_ARCHIVE,
                 BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE)
        download(BASE_URL + "game-files.json", baseline / "game-files.json", BASE_INVENTORY_SHA256)
        ci_payload(args.payload, args.approval)
    build(baseline / package.GAME_ARCHIVE, baseline / "game-files.json", args.channel,
          args.approval, args.payload, args.notes, args.output.resolve())
    build_game_hotfix.build(baseline, args.output.resolve())
    if args.expected_archive_sha256:
        manifest = read_json(args.output / "update.json")
        if manifest["game"]["sha256"] != args.expected_archive_sha256.lower():
            raise ValueError("CI fallback SHA256 differs from the locally reviewed archive.")


if __name__ == "__main__":
    main()
