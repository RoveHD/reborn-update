"""Prepare the reviewed single-file 7.3.9 glitter-ball hotfix; never publish."""
from __future__ import annotations

import argparse
import hashlib
import os
from pathlib import Path
import sys
import zipfile
import zlib

import build_game_hotfix
import prepare_catch_hotfix as builder

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.9"
BASE_VERSION = "7.3.5"
BASE_ARCHIVE_SHA256 = "9e1fa3b885e051db90ef1b0973d17a56024c687ee25094231b6842c0a9110b54"
BASE_ARCHIVE_SIZE = 1476961003
BASE_INVENTORY_SHA256 = "9e2610f631db74b952ce396570c591c9eb2c740486ccd83d5d73299eb872bac4"
MOD_SHA256 = "82f918bdd7855ec8e3fb57a5af054ff86308514d8b35e3f197954b21d0b8ddf8"
ARCHIVE_SHA256 = "0d9bfe384cac9ffd34f8fbf48bbeb27c7dafbe2a730bb65112a5c23fcc98df0f"


def restore_tested_bytes(path: Path, expected: str):
    """Git checkout line endings must reproduce the exact tested Ruby bytes."""
    data = path.read_bytes().replace(b"\r\n", b"\n")
    for candidate in (data, data.replace(b"\n", b"\r\n")):
        if hashlib.sha256(candidate).hexdigest() == expected:
            path.write_bytes(candidate)
            return
    raise ValueError(f"Checkout differs from the tested source: {path}")


def prepare_ci_baseline(baseline: Path, mod: Path):
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.name != "nt":
        raise RuntimeError("CI mode is only for the Windows GitHub Actions build.")
    if sys.version_info[:3] != (3, 8, 10) or zlib.ZLIB_RUNTIME_VERSION != "1.2.11":
        raise RuntimeError("Use the reviewed Python 3.8.10 / zlib 1.2.11 archive writer.")
    baseline.mkdir(parents=True, exist_ok=True)
    base_url = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
    builder.download(base_url + builder.ARCHIVE_NAME, baseline / builder.ARCHIVE_NAME,
                     BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE)
    builder.download(base_url + "game-files.json", baseline / "game-files.json", BASE_INVENTORY_SHA256)
    # Only the game fields are relevant to the delta baseline; the launcher is
    # copied unchanged from the reviewed current channel into the new manifest.
    (baseline / "update.json").write_bytes((ROOT / "update.json").read_bytes())
    restore_tested_bytes(mod, MOD_SHA256)
    proof = builder.read_json(mod.parents[2] / "catch-checks.json")
    restore_tested_bytes(ROOT / "tests/glitter_ball_7_3_9.rb", proof["harness_sha256"])
    restore_tested_bytes(ROOT / "tests/catch_gate_7_3_5.rb", proof["base_harness_sha256"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ci", action="store_true", help="Download pinned public inputs on the Windows build runner")
    parser.add_argument("--expected-archive-sha256", default=ARCHIVE_SHA256,
                        help="Require exact local/CI fallback reproducibility")
    args = parser.parse_args()
    if args.expected_archive_sha256.lower() != ARCHIVE_SHA256:
        parser.error("Expected checksum must match the pinned locally verified archive")
    baseline = ROOT / f"dist/release-game-{BASE_VERSION}"
    output = ROOT / f"dist/release-game-{VERSION}"
    mod = ROOT / f"game-hotfixes/{VERSION}" / builder.MOD_RELATIVE
    proof_path = mod.parents[2] / "catch-checks.json"
    notes = ROOT / f"release-notes-game-{VERSION}.md"
    if args.ci:
        prepare_ci_baseline(baseline, mod)

    channel_path = ROOT / "update.json"
    channel_bytes = channel_path.read_bytes()
    channel = builder.read_json(channel_path)
    expected_game = dict(version=BASE_VERSION,
                         url=f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/{builder.ARCHIVE_NAME}",
                         sha256=BASE_ARCHIVE_SHA256, size=BASE_ARCHIVE_SIZE)
    if channel.get("schemaVersion") != 1 or any(channel["game"].get(k) != v for k, v in expected_game.items()):
        raise ValueError("Current channel differs from the reviewed public 7.3.5 game baseline.")
    if builder.read_json(baseline / "update.json")["game"] != channel["game"]:
        raise ValueError("Baseline inventory manifest differs from the current game channel.")
    if builder.sha256_file(baseline / builder.ARCHIVE_NAME) != (BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE):
        raise ValueError("Local baseline archive differs from the pinned public asset.")
    if builder.sha256_file(baseline / "game-files.json")[0] != BASE_INVENTORY_SHA256:
        raise ValueError("Local baseline inventory differs from the pinned public asset.")
    proof = builder.read_json(proof_path)
    if proof.get("success") is not True or proof.get("sha256") != MOD_SHA256 or builder.sha256_file(mod)[0] != MOD_SHA256:
        raise ValueError("Successful Ruby proof and reviewed mod bytes do not match.")
    runs = proof.get("runs", [])
    if proof.get("total_checks") != 1140 or [run.get("order") for run in runs] != ["original", "swm-first", "catch-first", "script-reload"]:
        raise ValueError("The four reviewed Ruby load orders are missing.")
    if [run.get("checks") for run in runs] != [284, 285, 285, 286] or any(run.get("exit_code") != 0 for run in runs):
        raise ValueError("The reviewed 1140 Ruby checks did not all pass.")
    for relative, proof_key in (("tests/glitter_ball_7_3_9.rb", "harness_sha256"),
                                ("tests/catch_gate_7_3_5.rb", "base_harness_sha256")):
        if builder.sha256_file(ROOT / relative)[0] != proof.get(proof_key):
            raise ValueError(f"Tested Ruby harness changed: {relative}")
    if not notes.is_file():
        raise FileNotFoundError(notes)
    # This release must preserve every earlier catch fix verbatim. Appending the
    # reviewed ball handler is the only permitted change, not a rewritten mod.
    with zipfile.ZipFile(baseline / builder.ARCHIVE_NAME) as source:
        previous_mod = source.read(builder.MOD_RELATIVE)
    if not mod.read_bytes().startswith(previous_mod) or mod.stat().st_size <= len(previous_mod):
        raise ValueError("The reviewed glitter handler must only append to the exact 7.3.5 mod.")

    builder.VERSION = VERSION
    builder.BASE_VERSION = BASE_VERSION
    builder.BASE_URL = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
    builder.BASE_ARCHIVE_SHA256 = BASE_ARCHIVE_SHA256
    builder.BASE_ARCHIVE_SIZE = BASE_ARCHIVE_SIZE
    builder.BASE_INVENTORY_SHA256 = BASE_INVENTORY_SHA256
    builder.MOD_SHA256 = MOD_SHA256
    print("Verified public baseline and matching Ruby proof; building complete fallback.", flush=True)
    builder.build(baseline / builder.ARCHIVE_NAME, baseline / "game-files.json", mod, notes, output)
    build_game_hotfix.build(baseline, output)
    manifest = builder.read_json(output / "update.json")
    delta = manifest["gameHotfixes"]
    if len(delta) != 1 or delta[0]["fromVersion"] != BASE_VERSION or delta[0]["version"] != VERSION:
        raise ValueError("Unexpected hotfix source or target version.")
    if [entry["path"] for entry in delta[0]["files"]] != [builder.MOD_RELATIVE]:
        raise ValueError("The tiny hotfix must replace only the reviewed catch mod.")
    if manifest["launcher"] != channel["launcher"] or channel_path.read_bytes() != channel_bytes:
        raise ValueError("The launcher or live channel changed during preparation.")
    if args.expected_archive_sha256 and manifest["game"]["sha256"] != args.expected_archive_sha256:
        raise ValueError("CI archive differs from the locally verified release.")
    print("Prepared exact single-file delta and verified full fallback; live channel unchanged.", flush=True)


if __name__ == "__main__":
    main()
