"""Prepare the reviewed game-only 7.3.11 wild legendary PP hotfix; never publish."""
import argparse
import os
from pathlib import Path
import sys
import zlib

import build_game_hotfix
import prepare_catch_hotfix as builder
from prepare_glitter_hotfix import restore_tested_bytes

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.11"
BASE_VERSION = "7.3.10"
BASE_ARCHIVE_SHA256 = "c668b926499aacd365cb7783013427c468ab5d25c1f07c2866bf73de7ac40f30"
BASE_ARCHIVE_SIZE = 1476961663
BASE_INVENTORY_SHA256 = "16a411f93258c185cc8d02a2a9185dcebbafc3a4fb834074691ee14693a3da34"
MOD_SHA256 = "3abdad5467530e87c798733c7029a2bdd57713d39b1184fa6b468fa8e0de7b2d"
TESTED_INPUTS = {
    "tests/glitter_event_balance.rb": "5363cba70cc710df6b10570e5f4aa7ac268fadbc21ffb0fb2db8e5d27bdcf8d5",
    "tests/catch_event_balance.rb": "b204e38a831a48c4e9aadffb44bcc7a534a2e70eb7436422f0aa25824fcfa7ff",
    "custom-mods/legendary-catch/fangstufen.json": "30e327a9717b4dc2f749ad144765768f11bc4965a51dcf5759a76f892ca4d7bd",
    "tests/catch_infinite_pp.rb": "f973d04f9df05fd0ba5685dc8f7a0e5acaf231fe78366c6f8aed82326e87c19f",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ci", action="store_true")
    parser.add_argument("--expected-archive-sha256")
    args = parser.parse_args()
    if len(MOD_SHA256) != 64:
        raise ValueError("The root-reviewed source has not been frozen yet.")
    baseline = ROOT / f"dist/release-game-{BASE_VERSION}"
    output = ROOT / f"dist/release-game-{VERSION}"
    mod = ROOT / f"game-hotfixes/{VERSION}" / builder.MOD_RELATIVE
    proof = builder.read_json(mod.parents[2] / "catch-checks.json")
    channel_bytes = (ROOT / "update.json").read_bytes()
    channel = builder.read_json(ROOT / "update.json")
    expected = dict(version=BASE_VERSION,
                    url=f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/{builder.ARCHIVE_NAME}",
                    sha256=BASE_ARCHIVE_SHA256, size=BASE_ARCHIVE_SIZE)
    if channel.get("schemaVersion") != 1 or any(channel["game"].get(k) != v for k, v in expected.items()):
        raise ValueError("The current game channel differs from the reviewed public 7.3.10 baseline.")
    if args.ci:
        if os.environ.get("GITHUB_ACTIONS") != "true" or os.name != "nt":
            raise RuntimeError("CI mode requires the Windows GitHub Actions runner.")
        if sys.version_info[:3] != (3, 8, 10) or zlib.ZLIB_RUNTIME_VERSION != "1.2.11":
            raise RuntimeError("Reproduce with Python 3.8.10 / zlib 1.2.11.")
        if not args.expected_archive_sha256 or len(args.expected_archive_sha256) != 64:
            raise ValueError("CI requires the locally verified full archive SHA256.")
        baseline.mkdir(parents=True, exist_ok=True)
        base_url = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
        builder.download(base_url + builder.ARCHIVE_NAME, baseline / builder.ARCHIVE_NAME,
                         BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE)
        builder.download(base_url + "game-files.json", baseline / "game-files.json", BASE_INVENTORY_SHA256)
        (baseline / "update.json").write_bytes(channel_bytes)
        restore_tested_bytes(mod, MOD_SHA256)
        for relative, expected_hash in TESTED_INPUTS.items():
            restore_tested_bytes(ROOT / relative, expected_hash)
    if proof.get("success") is not True or proof.get("sha256") != MOD_SHA256 or proof.get("mod_sha256") != MOD_SHA256:
        raise ValueError("The successful root test proof does not match the frozen mod.")
    runs = proof.get("runs", [])
    if proof.get("total_checks") != 1539 or [run.get("order") for run in runs] != ["original", "swm-first", "catch-first", "script-reload"]:
        raise ValueError("The four reviewed catch regression orders are missing.")
    if [run.get("checks") for run in runs] != [358, 359, 359, 360] or any(run.get("exit_code") != 0 for run in runs):
        raise ValueError("The 1436 catch regression checks did not all pass.")
    pp = proof.get("infinite_pp", {})
    if pp.get("success") is not True or pp.get("checks") != 103 or pp.get("species") != 94 or pp.get("simulated_turns") != 23500 or pp.get("mod_sha256") != MOD_SHA256:
        raise ValueError("The reviewed 103 AP checks / 23500 simulated turns are missing.")
    if builder.read_json(mod.parents[2] / "infinite-pp-checks.json") != pp:
        raise ValueError("The dedicated AP proof differs from the combined catch proof.")
    if builder.sha256_file(mod)[0] != MOD_SHA256:
        raise ValueError("The mod changed after the reviewed source freeze.")
    for relative, expected_hash in TESTED_INPUTS.items():
        if builder.sha256_file(ROOT / relative)[0] != expected_hash:
            raise ValueError(f"Root-reviewed test input changed: {relative}")
    if builder.sha256_file(baseline / builder.ARCHIVE_NAME) != (BASE_ARCHIVE_SHA256, BASE_ARCHIVE_SIZE):
        raise ValueError("The baseline archive differs from the published 7.3.10 asset.")
    if builder.sha256_file(baseline / "game-files.json")[0] != BASE_INVENTORY_SHA256:
        raise ValueError("The baseline inventory differs from the published 7.3.10 asset.")
    if builder.read_json(baseline / "update.json")["game"] != channel["game"]:
        raise ValueError("The baseline manifest and current game channel differ.")

    builder.VERSION = VERSION
    builder.BASE_VERSION = BASE_VERSION
    builder.BASE_URL = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
    builder.BASE_ARCHIVE_SHA256 = BASE_ARCHIVE_SHA256
    builder.BASE_ARCHIVE_SIZE = BASE_ARCHIVE_SIZE
    builder.BASE_INVENTORY_SHA256 = BASE_INVENTORY_SHA256
    builder.MOD_SHA256 = MOD_SHA256
    print("Building the complete fallback from the verified public 7.3.10 package.", flush=True)
    builder.build(baseline / builder.ARCHIVE_NAME, baseline / "game-files.json", mod,
                  ROOT / f"release-notes-game-{VERSION}.md", output)
    build_game_hotfix.build(baseline, output)
    import shutil
    shutil.copy2(mod.parents[2] / "infinite-pp-checks.json", output / "infinite-pp-checks.json")
    sums = output / "SHA256SUMS.txt"
    with sums.open("a", encoding="utf-8") as stream:
        stream.write(builder.sha256_file(output / "infinite-pp-checks.json")[0] + "  infinite-pp-checks.json\n")
    manifest = builder.read_json(output / "update.json")
    if manifest["launcher"] != channel["launcher"] or (ROOT / "update.json").read_bytes() != channel_bytes:
        raise ValueError("The launcher or current channel changed during preparation.")
    delta = manifest["gameHotfixes"]
    if len(delta) != 1 or delta[0]["fromVersion"] != BASE_VERSION or delta[0]["version"] != VERSION:
        raise ValueError("Unexpected delta versions.")
    if [entry["path"] for entry in delta[0]["files"]] != [builder.MOD_RELATIVE]:
        raise ValueError("Only the frozen catch mod may change.")
    if args.expected_archive_sha256 and manifest["game"]["sha256"] != args.expected_archive_sha256.lower():
        raise ValueError("The CI fallback differs from the reviewed local archive.")
    print("Prepared one-file delta and verified full fallback; public channel unchanged.", flush=True)


if __name__ == "__main__":
    main()
