"""Build the single-file 7.3.5 game hotfix on the verified public 7.3.2 package."""
from pathlib import Path
import json

import build_game_hotfix
import prepare_catch_hotfix as builder

ROOT = Path(__file__).resolve().parents[1]
VERSION = "7.3.5"
BASE_VERSION = "7.3.2"
BASE_ARCHIVE_SHA256 = "a7016240d9d75df4a2c37ec779bfea581911d8fa234bab68e2b9574ab2c8d87b"
BASE_INVENTORY_SHA256 = "cc5af5de897412d5cf18328224d9d3d351726279e495a1ff1655051c494cc511"
MOD_SHA256 = "2adf338088066a3b67eeb1d17b9d1077acbffac7b03cf23395430ffc7a619dd3"


def main():
    baseline = ROOT / "dist/release-game-7.3.2"
    output = ROOT / f"dist/release-game-{VERSION}"
    mod = ROOT / f"game-hotfixes/{VERSION}" / builder.MOD_RELATIVE
    channel = builder.read_json(ROOT / "update.json")
    live = builder.read_json(ROOT / "artifacts/current-hotfix-channel.json")
    if channel != live or channel["game"]["version"] != BASE_VERSION:
        raise ValueError("Local channel differs from the reviewed live snapshot.")
    if channel["game"]["sha256"] != BASE_ARCHIVE_SHA256 or channel["game"]["size"] != 1476960959:
        raise ValueError("Unexpected live baseline archive.")
    if builder.sha256_file(baseline / builder.ARCHIVE_NAME) != (BASE_ARCHIVE_SHA256, 1476960959):
        raise ValueError("Local baseline archive differs from the public GitHub asset.")
    if builder.sha256_file(baseline / "game-files.json")[0] != BASE_INVENTORY_SHA256:
        raise ValueError("Local baseline inventory differs from the public GitHub asset.")
    proof_path = mod.parents[2] / "catch-checks.json"
    proof = builder.read_json(proof_path)
    if builder.sha256_file(mod)[0] != MOD_SHA256 or proof["mod_sha256"] != MOD_SHA256:
        raise ValueError("Catch tests do not belong to this mod.")
    if proof["total_checks"] != 264 or len(proof["runs"]) != 4 or any(run["exit_code"] != 0 for run in proof["runs"]):
        raise ValueError("The four Ruby load orders did not all pass.")
    harness = ROOT / "tests/catch_gate_7_3_5.rb"
    if builder.sha256_file(harness)[0] != proof["harness_sha256"]:
        raise ValueError("The tested Ruby harness changed.")
    proof.update(success=True, sha256=MOD_SHA256)
    builder.write_json(proof_path, proof)

    # Reuse the checked archive builder; old version-specific entry points remain pinned.
    builder.VERSION = VERSION
    builder.BASE_VERSION = BASE_VERSION
    builder.BASE_URL = f"https://github.com/{builder.REPOSITORY}/releases/download/v{BASE_VERSION}/"
    builder.BASE_ARCHIVE_SHA256 = BASE_ARCHIVE_SHA256
    builder.BASE_ARCHIVE_SIZE = 1476960959
    builder.BASE_INVENTORY_SHA256 = BASE_INVENTORY_SHA256
    builder.MOD_SHA256 = MOD_SHA256
    print("Verified public baseline and 264 catch checks; building coherent full fallback package.", flush=True)
    builder.build(baseline / builder.ARCHIVE_NAME, baseline / "game-files.json", mod,
                  ROOT / f"release-notes-game-{VERSION}.md", output)
    build_game_hotfix.build(baseline, output)
    print("Prepared the small replacement-file hotfix and full fallback; live channel unchanged.", flush=True)


if __name__ == "__main__":
    main()
