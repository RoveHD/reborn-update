"""Fail closed unless approved local game files, release ZIP and inventory match."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

import package_release as package

RUNTIME_ROOTS = ("patch/Mods", "patch/Graphics", "patch/Audio", "patch/Data",
                 "Audio", "Graphics", "Data", "Fonts", "Animations", "Gems", "Scripts", "stdlib")
GERMAN_BUILD_PROOF = Path("translation-build/german-dat-build.json")
GERMAN_RUNTIME_FILES = ("Game.exe", "Game.ini", "Scripts/PBIntl.rb",
                        "Scripts/ScriptLoader.rb", "Data/Scripts.rxdata")
BASELINE_INVENTORY = Path("release-baselines/game-files-7.3.23.json")
BASELINE_APPROVALS = Path("release-baselines/approvals-7.3.23.json")


def verify_translation_build(game_root: Path, project_root: Path) -> dict:
    """Require german.dat to be built from the exact intl.txt with bundled runtime."""
    proof_path = project_root / GERMAN_BUILD_PROOF
    if not proof_path.is_file():
        raise FileNotFoundError("Missing bundled-runtime german.dat proof: " + str(proof_path))
    proof = json.loads(proof_path.read_text(encoding="utf-8-sig"))
    game_root = game_root.resolve()
    if (proof.get("success") is not True or
            proof.get("compiler") != "Pokemon Reborn bundled runtime pbCompileText"):
        raise ValueError("German translation build proof is not a successful official-runtime compile")
    expected_intl = proof.get("intlSha256")
    expected_dat = proof.get("germanDatSha256")
    if (not isinstance(expected_intl, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_intl) or
            not isinstance(expected_dat, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_dat)):
        raise ValueError("German translation build proof has invalid hashes")
    for relative, digest in (("intl.txt", expected_intl), ("Data/german.dat", expected_dat)):
        path = game_root / relative
        if not path.is_file() or package.sha256(path) != digest:
            raise ValueError("German translation source/build differs from compiler proof: " + relative)
    runtime = proof.get("runtimeFiles")
    if not isinstance(runtime, dict) or set(runtime) != set(GERMAN_RUNTIME_FILES):
        raise ValueError("German translation proof does not identify all bundled compiler runtime files")
    for relative in GERMAN_RUNTIME_FILES:
        path = game_root / relative
        if not path.is_file() or runtime[relative] != package.sha256(path):
            raise ValueError("German translation compiler runtime differs from current game source: " + relative)
    entries = proof.get("entries")
    if not isinstance(entries, int) or entries <= 0:
        raise ValueError("German translation build proof has no compiled entry count")
    return {"compiler": proof["compiler"], "entries": entries,
            "intlSha256": expected_intl, "germanDatSha256": expected_dat,
            "runtimeFilesVerified": len(runtime)}


def custom_runtime_path(source: Path) -> str | None:
    """Return the in-game path for conventional runtime assets in a mod package."""
    parts = source.parts
    for root in RUNTIME_ROOTS:
        root_parts = root.split("/")
        for index in range(1, len(parts) - len(root_parts) + 1):
            candidate = "/".join(parts[index:index + len(root_parts)])
            if candidate.casefold() == root.casefold():
                return "/".join(parts[index:])
    return None


def source_inventory(root: Path) -> dict:
    result = {}
    folded = set()
    for relative, path in package.files_in(root.resolve()):
        key = relative.casefold()
        if key in folded:
            raise ValueError("Case-insensitive duplicate in approved local source: " + relative)
        folded.add(key)
        result[relative] = {"sha256": package.sha256(path), "size": path.stat().st_size}
    return result


def verify_release_baseline(inventory: dict, project_root: Path) -> dict:
    """Require an exact .23 baseline match plus individually hash-approved changes."""
    baseline_path = project_root.resolve() / BASELINE_INVENTORY
    approvals_path = project_root.resolve() / BASELINE_APPROVALS
    if not baseline_path.is_file() or not approvals_path.is_file():
        raise FileNotFoundError("Missing fail-closed release baseline or approval ledger")
    baseline_bytes = baseline_path.read_bytes()
    baseline = json.loads(baseline_bytes.decode("utf-8-sig"))
    ledger = json.loads(approvals_path.read_text(encoding="utf-8-sig"))
    if (ledger.get("schemaVersion") != 1 or ledger.get("version") != "7.3.23" or
            ledger.get("allGenVersion") != "0.9.9" or
            ledger.get("baselineInventory") != BASELINE_INVENTORY.name or
            ledger.get("baselineInventorySha256") != hashlib.sha256(baseline_bytes).hexdigest() or
            not isinstance(ledger.get("approvedChanges"), list)):
        raise ValueError("Invalid or stale .23 release approval ledger")
    expected = dict(baseline)
    seen = set()
    for row in ledger["approvedChanges"]:
        if not isinstance(row, dict):
            raise ValueError("Invalid baseline approval entry")
        path = row.get("path")
        before = row.get("beforeSha256")
        after = row.get("afterSha256")
        reason = row.get("reason")
        if (not isinstance(path, str) or not package.allowed(path) or
                Path(path).as_posix() != path or any(part in {"", ".", ".."} for part in Path(path).parts) or
                path.casefold() in seen or not isinstance(reason, str) or not reason.strip() or
                (before is not None and not re.fullmatch(r"[0-9a-f]{64}", str(before))) or
                (after is not None and not re.fullmatch(r"[0-9a-f]{64}", str(after))) or
                (before is None and after is None)):
            raise ValueError("Invalid path, hashes, or reason in baseline approval")
        seen.add(path.casefold())
        existing = next((key for key in expected if key.casefold() == path.casefold()), None)
        if before is None:
            if existing is not None:
                raise ValueError("Baseline approval expected a new path: " + path)
        elif existing is None or expected[existing].get("sha256") != before:
            raise ValueError("Baseline approval before hash does not match baseline: " + path)
        if existing is not None:
            del expected[existing]
        if after is not None:
            size = row.get("size")
            if not isinstance(size, int) or size < 0:
                raise ValueError("Baseline approval needs the approved after-size: " + path)
            expected[path] = {"sha256": after, "size": size}
    if inventory != expected:
        missing = sorted(set(expected) - set(inventory))
        extra = sorted(set(inventory) - set(expected))
        changed = sorted(path for path in set(expected) & set(inventory) if expected[path] != inventory[path])
        raise ValueError("Game source differs from approved .23 baseline: " + json.dumps(
            {"missing": missing, "extra": extra, "changed": changed}, ensure_ascii=False))
    return {"baselineVersion": "7.3.23", "allGenVersion": "0.9.9",
            "baselineSha256": hashlib.sha256(baseline_bytes).hexdigest(),
            "approvedChanges": len(seen)}


def verify_latest_hotfix_payload(game_root: Path, project_root: Path) -> dict:
    """Require the newest locally prepared hotfix payload to be in the full source."""
    versions = []
    hotfix_root = project_root.resolve() / "game-hotfixes"
    if not hotfix_root.is_dir():
        return {"checked": False, "version": None, "files": 0}
    for child in hotfix_root.iterdir():
        if child.is_dir() and re.fullmatch(r"\d+\.\d+\.\d+", child.name):
            versions.append((tuple(int(part) for part in child.name.split(".")), child))
    if not versions:
        return {"checked": False, "version": None, "files": 0}
    _, latest = max(versions, key=lambda row: row[0])
    approval_path = latest / "approved-files.json"
    payload_root = latest / "payload"
    approval = json.loads(approval_path.read_text(encoding="utf-8-sig"))
    approved = approval.get("files")
    if not isinstance(approved, list) or not payload_root.is_dir():
        raise ValueError("Latest local hotfix has no approved-files.json/payload: " + latest.name)
    approved_map = {}
    for row in approved:
        if not isinstance(row, dict) or not isinstance(row.get("path"), str):
            raise ValueError("Invalid approved hotfix file entry: " + latest.name)
        relative = row["path"].replace("\\", "/")
        if relative in approved_map or not package.allowed(relative):
            raise ValueError("Duplicate or protected approved hotfix path: " + relative)
        approved_map[relative] = row
    payload_map = {relative: path for relative, path in package.files_in(payload_root)}
    if set(payload_map) != set(approved_map):
        raise ValueError("Latest hotfix payload differs from approved-files.json: " + json.dumps({
            "unapprovedPayload": sorted(set(payload_map) - set(approved_map)),
            "missingPayload": sorted(set(approved_map) - set(payload_map)),
        }))
    mismatches = []
    game_root = game_root.resolve()
    for relative, payload in payload_map.items():
        row = approved_map[relative]
        target = (game_root / relative).resolve()
        if game_root not in target.parents or not target.is_file():
            mismatches.append(relative)
            continue
        digest = package.sha256(payload)
        size = payload.stat().st_size
        if (digest != row.get("sha256") or size != row.get("size") or
                digest != package.sha256(target) or size != target.stat().st_size):
            mismatches.append(relative)
    if mismatches:
        raise ValueError("Latest approved hotfix payload is not byte-identical to the full game source: " +
                         json.dumps(sorted(mismatches)))
    return {"checked": True, "version": latest.name, "files": len(payload_map)}


def verify_project_mod_sources(game_root: Path, project_root: Path) -> dict:
    """Require every custom-mod source to have an explicit, verified disposition.

    `custom-mods/release-mods.json` is the approval ledger for split mod sources:
    byte-for-byte runtime mirrors, renamed mirrors, or reviewed merged sources.
    Unknown files fail closed. Backups are excluded from runtime inputs.
    """
    game_root = game_root.resolve()
    project_root = project_root.resolve()
    mods_root = project_root.resolve() / "custom-mods"
    if not mods_root.is_dir():
        return {"checked": False, "files": 0, "matched": 0}

    registry_path = mods_root / "release-mods.json"
    if not registry_path.is_file():
        raise ValueError("Missing custom-mod release registry: " + str(registry_path))
    registry = json.loads(registry_path.read_text(encoding="utf-8-sig"))
    if registry.get("schemaVersion") != 1 or not isinstance(registry.get("mods"), list):
        raise ValueError("Invalid custom-mod release registry")

    candidates = {}
    for source in sorted(mods_root.rglob("*")):
        if not source.is_file() or source == registry_path or "backup" in (part.casefold() for part in source.relative_to(mods_root).parts):
            continue
        if custom_runtime_path(source.relative_to(mods_root)) is None:
            continue
        candidates[source.relative_to(project_root).as_posix()] = source

    entries = {}
    for entry in registry["mods"]:
        if not isinstance(entry, dict) or not isinstance(entry.get("source"), str):
            raise ValueError("Invalid custom-mod registry entry")
        name = entry["source"].replace("\\", "/")
        if name in entries:
            raise ValueError("Duplicate custom-mod registry source: " + name)
        entries[name] = entry
    unregistered = sorted(set(candidates) - set(entries))
    missing_sources = sorted(set(entries) - set(candidates))
    if unregistered or missing_sources:
        raise ValueError("Custom-mod registry coverage mismatch: " + json.dumps(
            {"unregistered": unregistered, "missingSources": missing_sources}, ensure_ascii=False))

    mismatches = []
    dispositions = {"mirror", "renamed", "merged"}
    for name, entry in entries.items():
        source = candidates[name]
        mode = entry.get("disposition")
        runtime = entry.get("runtime")
        reason = entry.get("reason")
        runtime_path = Path(runtime) if isinstance(runtime, str) else Path()
        if (mode not in dispositions or not isinstance(runtime, str) or
                not any(runtime.startswith(root + "/") for root in RUNTIME_ROOTS) or
                runtime_path.as_posix() != runtime or
                any(part in {"", ".", ".."} for part in runtime_path.parts)):
            raise ValueError("Invalid custom-mod disposition or runtime path: " + name)
        if mode == "merged" and (not isinstance(reason, str) or not reason.strip()):
            raise ValueError("Merged custom-mod source needs a review reason: " + name)
        target = (game_root / runtime_path).resolve()
        if game_root not in target.parents:
            raise ValueError("Custom-mod runtime path escapes game root: " + name)
        if not target.is_file():
            mismatches.append({"source": name, "runtimePath": runtime,
                               "reason": "missing from local game source"})
            continue
        source_hash = package.sha256(source)
        target_hash = package.sha256(target)
        if mode in {"mirror", "renamed"} and (source_hash != target_hash or source.stat().st_size != target.stat().st_size):
            mismatches.append({"source": name, "runtimePath": runtime,
                               "reason": "content differs from local game source"})
        if mode == "merged":
            expected_source = entry.get("sourceSha256")
            expected_target = entry.get("runtimeSha256")
            if source_hash != expected_source or target_hash != expected_target:
                mismatches.append({"source": name, "runtimePath": runtime,
                                   "reason": "reviewed merge fingerprint changed"})

    # A deleted/renamed tracked source must not leave its old runtime copy behind.
    try:
        tracked = subprocess.run(
            ["git", "-C", str(project_root.resolve()), "ls-files", "-z", "custom-mods"],
            check=True, capture_output=True,
        ).stdout.decode("utf-8").split("\0")
    except (FileNotFoundError, subprocess.CalledProcessError):
        tracked = []
    for tracked_name in filter(None, tracked):
        source = project_root / Path(tracked_name)
        if source.is_file():
            continue
        name = Path(tracked_name).as_posix()
        parts = Path(tracked_name).parts
        source_relative = Path(*parts[1:]) if len(parts) > 1 else Path()
        runtime_relative = custom_runtime_path(source_relative)
        if runtime_relative is None:
            continue
        if "backup" in (part.casefold() for part in parts):
            continue
        target = game_root.resolve() / Path(runtime_relative)
        if target.exists():
            mismatches.append({"source": name.replace("\\", "/"),
                               "runtimePath": runtime_relative.replace("\\", "/"),
                               "reason": "source was deleted but stale runtime file remains"})
    if mismatches:
        raise ValueError("Custom-mod source files are not synced into the game source: " +
                         json.dumps(mismatches, ensure_ascii=False))
    hotfix = verify_latest_hotfix_payload(game_root, project_root)
    return {"checked": True, "files": len(candidates), "matched": len(candidates),
            "deletedSourcesChecked": len(tracked), "latestHotfix": hotfix}


def archive_inventory(path: Path) -> dict:
    result = {}
    folded = set()
    with zipfile.ZipFile(path) as archive:
        if archive.testzip() is not None:
            raise ValueError("Game archive failed ZIP CRC validation")
        for info in archive.infolist():
            if info.is_dir():
                continue
            relative = info.filename.replace("\\", "/")
            key = relative.casefold()
            if key in folded or not package.allowed(relative):
                raise ValueError("Duplicate or non-game path in release archive: " + relative)
            folded.add(key)
            with archive.open(info) as stream:
                digest = hashlib.sha256()
                size = 0
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(block)
                    size += len(block)
            result[relative] = {"sha256": digest.hexdigest(), "size": size}
    return result


def verify_archive_only(release: Path, project_root: Path | None = None) -> dict:
    """CI-side gate for immutable uploads when the canonical game folder is absent.

    The local publisher still must run the full source check. This second check
    makes direct GitHub Actions upload steps reject packages that lack the
    locally-created proof or whose archive/inventory changed afterwards.
    """
    release = release.resolve()
    manifest = json.loads((release / "update.json").read_text(encoding="utf-8-sig"))
    inventory_path = release / "game-files.json"
    proof_path = release / "source-parity.json"
    if not proof_path.is_file():
        raise FileNotFoundError("Missing source-parity.json; package was not locally source-verified")
    proof = json.loads(proof_path.read_text(encoding="utf-8-sig"))
    expected = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
    baseline = verify_release_baseline(expected, project_root or Path(__file__).resolve().parents[1])
    archive_path = release / Path(manifest["game"]["url"]).name
    actual = archive_inventory(archive_path)
    if actual != expected:
        raise ValueError("Release archive differs from game-files.json")
    inventory_hash = package.sha256(inventory_path)
    archive_hash = package.sha256(archive_path)
    if manifest["game"]["sha256"] != archive_hash:
        raise ValueError("Release archive hash differs from update.json")
    if (not proof.get("success") or proof.get("localSourceEqualsInventory") is not True or
            proof.get("archiveEqualsInventory") is not True or
            proof.get("inventorySha256") != inventory_hash or
            proof.get("archiveSha256") != archive_hash or
            proof.get("archiveFiles") != len(expected) or
            proof.get("sourceFiles") != len(expected)):
        raise ValueError("source-parity.json does not attest the current inventory/archive")
    translation = proof.get("translationBuild")
    if (not isinstance(translation, dict) or
            translation.get("intlSha256") != expected.get("intl.txt", {}).get("sha256") or
            translation.get("germanDatSha256") != expected.get("Data/german.dat", {}).get("sha256") or
            translation.get("compiler") != "Pokemon Reborn bundled runtime pbCompileText" or
            not isinstance(translation.get("entries"), int) or translation["entries"] <= 0 or
            translation.get("runtimeFilesVerified") != len(GERMAN_RUNTIME_FILES)):
        raise ValueError("Release archive lacks a proof-bound current german.dat compilation")
    deltas = manifest.get("gameHotfixesV2", [])
    if not isinstance(deltas, list):
        raise ValueError("update.json gameHotfixesV2 must be a list")
    checked_deltas = []
    for delta in deltas:
        if not isinstance(delta, dict) or delta.get("version") != manifest["game"]["version"]:
            raise ValueError("V2 delta target version differs from the full package")
        delta_url = delta.get("url")
        if not isinstance(delta_url, str) or not delta_url.rsplit("/", 1)[-1]:
            raise ValueError("V2 delta has no valid asset URL")
        delta_path = release / Path(delta_url.rsplit("/", 1)[-1]).name
        if not delta_path.is_file() or package.sha256(delta_path) != delta.get("sha256") or delta_path.stat().st_size != delta.get("size"):
            raise ValueError("V2 delta archive hash/size differs from update.json: " + delta_path.name)
        rows = delta.get("files")
        if not isinstance(rows, list) or not rows:
            raise ValueError("V2 delta has no file operations: " + delta_path.name)
        operations = {}
        expected_delta_files = {}
        for row in rows:
            if not isinstance(row, dict) or not isinstance(row.get("path"), str):
                raise ValueError("Invalid V2 delta file operation: " + delta_path.name)
            relative = row["path"].replace("\\", "/")
            key = relative.casefold()
            if key in operations or not package.allowed(relative):
                raise ValueError("Duplicate or protected V2 delta path: " + relative)
            operations[key] = row
            before_exists = row.get("beforeExists")
            before_hash = row.get("beforeSha256")
            if not isinstance(before_exists, bool) or not isinstance(before_hash, str):
                raise ValueError("V2 delta is missing its base-state fingerprint: " + relative)
            if before_exists and not re.fullmatch(r"[0-9a-fA-F]{64}", before_hash):
                raise ValueError("Invalid V2 delta beforeSha256: " + relative)
            if not before_exists and before_hash != "":
                raise ValueError("New V2 delta path has a beforeSha256: " + relative)
            if row.get("delete") is True:
                if (not before_exists or row.get("sha256") != "" or row.get("size") != 0 or
                        relative in expected):
                    raise ValueError("V2 deletion does not match the target inventory: " + relative)
                removed = manifest["game"].get("removedFiles", [])
                if not any(item.get("path") == relative and item.get("sha256", "").lower() == before_hash.lower()
                           for item in removed):
                    raise ValueError("V2 deletion is not authorized by game.removedFiles: " + relative)
            else:
                target = expected.get(relative)
                if (target is None or row.get("sha256") != target.get("sha256") or
                        row.get("size") != target.get("size")):
                    raise ValueError("V2 delta file differs from the full target package: " + relative)
                expected_delta_files[relative] = target
        actual_delta_files = archive_inventory(delta_path)
        if actual_delta_files != expected_delta_files:
            raise ValueError("V2 delta contents differ from its operations/target inventory: " + delta_path.name)
        checked_deltas.append(delta_path.name)
    return {"success": True, "archiveSha256": archive_hash,
            "inventorySha256": inventory_hash, "files": len(expected),
            "locallySourceVerified": True, "releaseBaseline": baseline,
            "deltasVerified": checked_deltas}


def verify(release: Path, source_root: Path, installed_root: Path | None = None,
           project_root: Path | None = None) -> dict:
    release = release.resolve()
    source_root = source_root.resolve()
    project_root = (project_root or Path(__file__).resolve().parents[1]).resolve()
    mod_sources = verify_project_mod_sources(source_root, project_root)
    translation_build = verify_translation_build(source_root, project_root)
    manifest = json.loads((release / "update.json").read_text(encoding="utf-8-sig"))
    archive_path = release / Path(manifest["game"]["url"]).name
    inventory_path = release / "game-files.json"
    expected = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
    actual_source = source_inventory(source_root)
    release_baseline = verify_release_baseline(actual_source, project_root)
    actual_archive = archive_inventory(archive_path)
    if actual_source != expected:
        details = {
            "missingFromPackage": sorted(set(actual_source) - set(expected)),
            "notInLocalSource": sorted(set(expected) - set(actual_source)),
            "hashOrSizeMismatch": sorted(path for path in set(actual_source) & set(expected)
                                          if actual_source[path] != expected[path]),
        }
        raise ValueError("Local releasable source differs from package inventory: " + json.dumps(details, ensure_ascii=False))
    if actual_archive != expected:
        details = {
            "missingFromArchive": sorted(set(expected) - set(actual_archive)),
            "extraInArchive": sorted(set(actual_archive) - set(expected)),
            "hashOrSizeMismatch": sorted(path for path in set(expected) & set(actual_archive)
                                          if expected[path] != actual_archive[path]),
        }
        raise ValueError("Release archive differs from package inventory: " + json.dumps(details, ensure_ascii=False))
    archive_hash = package.sha256(archive_path)
    if archive_hash != manifest["game"]["sha256"]:
        raise ValueError("Release archive hash differs from update.json")

    installed_verified = False
    if installed_root is not None:
        installed = source_inventory(installed_root)
        mismatches = sorted(path for path, data in expected.items() if installed.get(path) != data)
        if mismatches:
            raise ValueError("Installed game differs from release for managed files: " + json.dumps(mismatches))
        installed_verified = True

    return {
        "success": True,
        "version": manifest["game"]["version"],
        "sourceRootLabel": source_root.name,
        "sourceFiles": len(actual_source),
        "customModSources": mod_sources,
        "releaseBaseline": release_baseline,
        "translationBuild": translation_build,
        "archiveFiles": len(actual_archive),
        "inventorySha256": package.sha256(inventory_path),
        "archiveSha256": archive_hash,
        "localSourceEqualsInventory": True,
        "archiveEqualsInventory": True,
        "installedGameChecked": installed_verified,
        "installedManagedFilesVerified": len(expected) if installed_verified else 0,
        "unknownInstalledFilesIgnoredByComparison": True,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release-dir", type=Path, required=True)
    parser.add_argument("--source-game", type=Path,
                        help="Canonical game folder; otherwise the local, non-published source-game.path is used")
    parser.add_argument("--project-root", type=Path,
                        help="Release repository containing custom-mods/ (defaults to this tool's repository)")
    parser.add_argument("--installed-game", type=Path,
                        help="Optional isolated installed game folder to compare managed-file hashes")
    parser.add_argument("--write", action="store_true", help="Write source-parity.json into release directory")
    parser.add_argument("--archive-only", action="store_true",
                        help="CI upload guard: validate package against its local parity proof")
    args = parser.parse_args()
    if args.archive_only:
        print(json.dumps(verify_archive_only(args.release_dir), ensure_ascii=False))
        return
    proof_path = args.release_dir / "source-parity.json"
    source_path = args.release_dir / "source-game.path"
    source = args.source_game
    if source is None:
        if not source_path.is_file():
            raise FileNotFoundError("Missing local source pointer: " + str(source_path))
        source = Path(source_path.read_text(encoding="utf-8-sig").strip())
    report = verify(args.release_dir, source, args.installed_game, args.project_root)
    if args.write:
        source_path.write_text(str(source.resolve()) + "\n", encoding="utf-8")
        proof_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    elif proof_path.is_file():
        previous = json.loads(proof_path.read_text(encoding="utf-8-sig"))
        for key in ("version", "sourceRootLabel", "inventorySha256", "archiveSha256"):
            if previous.get(key) != report.get(key):
                raise ValueError("Stored release parity proof is stale: " + key)
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
