"""Prepare verified game hotfix 7.3.10, retaining the published launcher and game contents."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil

import build_game_hotfix
import prepare_catch_hotfix as builder
from prepare_glitter_hotfix import restore_tested_bytes

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.10"
BASE_VERSION = "7.3.9"
BASE_ARCHIVE_SHA256 = "0d9bfe384cac9ffd34f8fbf48bbeb27c7dafbe2a730bb65112a5c23fcc98df0f"
BASE_ARCHIVE_SIZE = 1476961210
BASE_INVENTORY_SHA256 = "1b59c39252ed5256c781083362941c3b62e182617b3d98c3b99aa6ce0cbc1ed7"
MOD_SHA256 = "cd68e24dc94d2080cb4b8e7cb462d1d0a1d99b148139404ac8fc1979bbcd0da0"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ci", action="store_true")
    parser.add_argument("--expected-archive-sha256")
    args = parser.parse_args()
    baseline = ROOT / f"dist/release-game-{BASE_VERSION}"
    output = ROOT / f"dist/release-game-{VERSION}"
    mod = ROOT / f"game-hotfixes/{VERSION}" / builder.MOD_RELATIVE
    proof = builder.read_json(mod.parents[2] / "catch-checks.json")
    channel_bytes = (ROOT / "update.json").read_bytes()
    channel = builder.read_json(ROOT / "update.json")
    assert channel["game"]["version"] == BASE_VERSION
    assert channel["game"]["sha256"] == BASE_ARCHIVE_SHA256
    assert channel["game"]["size"] == BASE_ARCHIVE_SIZE
    if args.ci:
        assert os.environ.get("GITHUB_ACTIONS") == "true" and os.name == "nt"
        assert args.expected_archive_sha256 and len(args.expected_archive_sha256) == 64
        baseline.mkdir(parents=True, exist_ok=True)
        base_url = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
        builder.download(base_url + builder.ARCHIVE_NAME, baseline / builder.ARCHIVE_NAME,
                         BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE)
        builder.download(base_url + "game-files.json", baseline / "game-files.json", BASE_INVENTORY_SHA256)
        (baseline / "update.json").write_bytes(channel_bytes)
        for relative, key in [(f"game-hotfixes/{VERSION}/" + builder.MOD_RELATIVE, "mod_sha256"),
                              ("tests/glitter_event_balance.rb", "harness_sha256"),
                              ("tests/catch_event_balance.rb", "base_harness_sha256"),
                              ("custom-mods/legendary-catch/fangstufen.json", "fixture_sha256")]:
            restore_tested_bytes(ROOT / relative, proof[key])
    assert proof["success"] and proof["mod_sha256"] == proof["sha256"] == MOD_SHA256
    assert proof["species"] == 94 and proof["total_checks"] == 1436
    assert [r["checks"] for r in proof["runs"]] == [358, 359, 359, 360]
    assert [r["order"] for r in proof["runs"]] == ["original", "swm-first", "catch-first", "script-reload"]
    assert all(r["exit_code"] == 0 for r in proof["runs"])
    assert builder.sha256_file(mod)[0] == MOD_SHA256
    for relative, key in [("tests/glitter_event_balance.rb", "harness_sha256"),
                          ("tests/catch_event_balance.rb", "base_harness_sha256"),
                          ("custom-mods/legendary-catch/fangstufen.json", "fixture_sha256")]:
        assert builder.sha256_file(ROOT / relative)[0] == proof[key], relative
    assert builder.sha256_file(baseline / builder.ARCHIVE_NAME) == (BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE)
    assert builder.sha256_file(baseline / "game-files.json")[0] == BASE_INVENTORY_SHA256
    assert builder.read_json(baseline / "update.json")["game"] == channel["game"]
    builder.VERSION = VERSION
    builder.BASE_VERSION = BASE_VERSION
    builder.BASE_URL = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
    builder.BASE_ARCHIVE_SHA256 = BASE_ARCHIVE_SHA256
    builder.BASE_ARCHIVE_SIZE = BASE_ARCHIVE_SIZE
    builder.BASE_INVENTORY_SHA256 = BASE_INVENTORY_SHA256
    builder.MOD_SHA256 = MOD_SHA256
    print("Building and validating the complete fallback against the verified public 7.3.9 package.", flush=True)
    builder.build(baseline / builder.ARCHIVE_NAME, baseline / "game-files.json", mod,
                  ROOT / f"release-notes-game-{VERSION}.md", output)
    build_game_hotfix.build(baseline, output)
    manifest = builder.read_json(output / "update.json")
    assert manifest["launcher"] == channel["launcher"]
    assert (ROOT / "update.json").read_bytes() == channel_bytes
    delta = manifest["gameHotfixes"]
    assert len(delta) == 1 and delta[0]["fromVersion"] == BASE_VERSION and delta[0]["version"] == VERSION
    assert [f["path"] for f in delta[0]["files"]] == [builder.MOD_RELATIVE]
    if args.expected_archive_sha256:
        assert manifest["game"]["sha256"] == args.expected_archive_sha256, "CI fallback differs from local proof"
    for name in ["FANGSTUFEN.md", "fangstufen.json"]:
        shutil.copy2(ROOT / "custom-mods/legendary-catch" / name, output / name)
    files = sorted(p for p in output.iterdir() if p.is_file() and p.name != "SHA256SUMS.txt")
    (output / "SHA256SUMS.txt").write_text("".join(
        hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.name + "\n" for p in files), encoding="utf-8")
    print("Prepared single-file delta and complete fallback; live update channel unchanged.", flush=True)


if __name__ == "__main__":
    main()
