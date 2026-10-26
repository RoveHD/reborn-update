"""Reproduce the reviewed Windows archive on GitHub's existing draft-only build runner."""
import hashlib
import json
import os
from pathlib import Path
import sys
import zlib

import prepare_legendary_gate_hotfix as gate


def sha(path):
    return gate.builder.sha256_file(path)[0]


def restore_tested_bytes(path: Path, expected: str):
    data = path.read_bytes().replace(b"\r\n", b"\n")
    for candidate in (data, data.replace(b"\n", b"\r\n")):
        if hashlib.sha256(candidate).hexdigest() == expected:
            path.write_bytes(candidate)
            return
    raise ValueError(f"Checkout differs from tested source: {path}")


def main():
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.name != "nt":
        raise RuntimeError("This entry point is only for the Windows GitHub Actions build.")
    if sys.version_info[:3] != (3, 8, 10) or zlib.ZLIB_RUNTIME_VERSION != "1.2.11":
        raise RuntimeError("The runner must use the reviewed Python 3.8.10 / zlib 1.2.11 archive writer.")
    root = gate.ROOT
    baseline = root / "dist/release-game-7.3.2"
    baseline.mkdir(parents=True)
    base = "https://github.com/RoveHD/reborn-update/releases/download/v7.3.2/"
    gate.builder.download(base + gate.builder.ARCHIVE_NAME, baseline / gate.builder.ARCHIVE_NAME,
                          gate.BASE_ARCHIVE_SHA256, 1476960959)
    gate.builder.download(base + "game-files.json", baseline / "game-files.json", gate.BASE_INVENTORY_SHA256)
    channel = (root / "update.json").read_bytes()
    (baseline / "update.json").write_bytes(channel)
    snapshot = root / "artifacts/current-hotfix-channel.json"
    snapshot.parent.mkdir(exist_ok=True)
    snapshot.write_bytes(channel)
    proof = gate.builder.read_json(root / "game-hotfixes/7.3.5/catch-checks.json")
    restore_tested_bytes(root / "game-hotfixes/7.3.5/patch/Mods/zzLegendaryCatchDifficulty.rb", gate.MOD_SHA256)
    restore_tested_bytes(root / "tests/catch_gate_7_3_5.rb", proof["harness_sha256"])
    gate.main()
    archive = root / "dist/release-game-7.3.5" / gate.builder.ARCHIVE_NAME
    expected = "9e1fa3b885e051db90ef1b0973d17a56024c687ee25094231b6842c0a9110b54"
    actual = sha(archive)
    if actual != expected:
        raise ValueError(f"CI archive differs from the locally verified release: {actual}")
    print(json.dumps({"success": True, "archiveSha256": actual, "exactLocalArchiveReproduced": True}), flush=True)


if __name__ == "__main__":
    main()
