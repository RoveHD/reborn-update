import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import build_game_hotfix
import package_release
import prepare_byteon_hotfix


class GameHotfixBuilderTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.baseline = self.root / "old"
        self.target = self.root / "new"
        self.baseline.mkdir()
        self.target.mkdir()
        self.old = {"patch/Mods/change.rb": b"old code", "patch/Mods/retired.rb": b"retired"}
        self.new = {"patch/Mods/change.rb": b"new code", "patch/Mods/new-follower.png": b"sprite"}
        self._write_release(self.baseline, "7.3.12", self.old, [])
        removed = [{"path": "patch/Mods/retired.rb", "sha256": package_release.hashlib.sha256(b"retired").hexdigest()}]
        self._write_release(self.target, "7.3.13", self.new, removed)

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def _write_release(directory, version, files, removed):
        archive = directory / package_release.GAME_ARCHIVE
        inventory = {}
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as output:
            for path, data in sorted(files.items()):
                output.writestr(path, data)
                inventory[path] = {"sha256": package_release.hashlib.sha256(data).hexdigest(), "size": len(data)}
        game = {"version": version, "url": f"https://example.invalid/v{version}/{package_release.GAME_ARCHIVE}",
                "sha256": package_release.sha256(archive), "size": archive.stat().st_size, "removedFiles": removed}
        (directory / "update.json").write_text(json.dumps({"game": game}), encoding="utf-8")
        (directory / "game-files.json").write_text(json.dumps(inventory), encoding="utf-8")
        (directory / "SHA256SUMS.txt").write_text("", encoding="utf-8")

    def test_builds_small_add_replace_delete_v2_delta(self):
        entry = build_game_hotfix.build(self.baseline, self.target)
        self.assertEqual(entry["fromVersion"], "7.3.12")
        self.assertEqual({item["path"] for item in entry["files"]}, set(self.old) | {"patch/Mods/new-follower.png"})
        by_path = {item["path"]: item for item in entry["files"]}
        self.assertEqual(by_path["patch/Mods/change.rb"]["beforeSha256"], package_release.hashlib.sha256(b"old code").hexdigest())
        self.assertFalse(by_path["patch/Mods/new-follower.png"]["beforeExists"])
        self.assertEqual(by_path["patch/Mods/new-follower.png"]["beforeSha256"], "")
        self.assertTrue(by_path["patch/Mods/retired.rb"]["delete"])
        with zipfile.ZipFile(self.target / entry["url"].rsplit("/", 1)[-1]) as delta:
            self.assertEqual(set(delta.namelist()), {"patch/Mods/change.rb", "patch/Mods/new-follower.png"})
        manifest = json.loads((self.target / "update.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["gameHotfixesV2"], [entry])
        self.assertEqual(manifest.get("gameHotfixes", []), [])

    def test_rejects_undeclared_removed_file(self):
        manifest = json.loads((self.target / "update.json").read_text(encoding="utf-8"))
        manifest["game"]["removedFiles"] = []
        (self.target / "update.json").write_text(json.dumps(manifest), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "does not authorize"):
            build_game_hotfix.build(self.baseline, self.target)

    def test_deletion_only_delta_has_no_archive_entries(self):
        target_files = {"patch/Mods/change.rb": b"old code"}
        old_hash = package_release.hashlib.sha256(b"retired").hexdigest()
        self._write_release(self.target, "7.3.13", target_files, [{"path": "patch/Mods/retired.rb", "sha256": old_hash}])
        entry = build_game_hotfix.build(self.baseline, self.target)
        self.assertEqual(len(entry["files"]), 1)
        self.assertTrue(entry["files"][0]["delete"])
        with zipfile.ZipFile(self.target / entry["url"].rsplit("/", 1)[-1]) as delta:
            self.assertEqual(delta.namelist(), [])

    def test_ci_build_creates_missing_baseline_manifest_without_changing_channel(self):
        source_archive = self.baseline / package_release.GAME_ARCHIVE
        source_inventory = self.baseline / "game-files.json"
        archive_hash, archive_size = prepare_byteon_hotfix.sha256_file(source_archive)
        inventory_hash = package_release.sha256(source_inventory)
        channel = json.loads((self.baseline / "update.json").read_text(encoding="utf-8"))
        channel["schemaVersion"] = 1
        channel["game"]["url"] = prepare_byteon_hotfix.BASE_URL + package_release.GAME_ARCHIVE
        channel["launcher"] = {"version": "7.4.0", "url": "https://example.invalid/current-launcher.exe",
                               "sha256": "a" * 64, "size": 123}
        channel_path = self.root / "channel.json"
        channel_path.write_text(json.dumps(channel), encoding="utf-8")
        original_channel = channel_path.read_bytes()
        payload = self.root / "payload"
        mod_path = "patch/Mods/byteon.rb"
        (payload / mod_path).parent.mkdir(parents=True)
        (payload / mod_path).write_bytes(b"reviewed byteon fix\n")
        digest, size = prepare_byteon_hotfix.sha256_file(payload / mod_path)
        approval = self.root / "approved.json"
        approval.write_text(json.dumps({"files": [{"path": mod_path, "sha256": digest, "size": size}]}), encoding="utf-8")
        notes = self.root / "notes.txt"
        notes.write_text("Byteon regression fixture", encoding="utf-8")
        # Reproduce the CI download state: no baseline update.json exists.
        ci_baseline, output = self.root / "ci-baseline", self.root / "ci-output"
        downloads = []

        def download_fixture(url, destination, expected_hash, expected_size=None):
            source = source_inventory if url.endswith("game-files.json") else source_archive
            self.assertEqual(prepare_byteon_hotfix.sha256_file(source)[0], expected_hash)
            if expected_size is not None:
                self.assertEqual(source.stat().st_size, expected_size)
            destination.write_bytes(source.read_bytes())
            downloads.append(url)

        argv = ["prepare_byteon_hotfix.py", "--ci", "--baseline", str(ci_baseline),
                "--channel", str(channel_path), "--approval", str(approval), "--payload", str(payload),
                "--notes", str(notes), "--output", str(output), "--expected-archive-sha256", "b" * 64]
        with mock.patch.object(prepare_byteon_hotfix, "BASE_ARCHIVE_SHA256", archive_hash), \
             mock.patch.object(prepare_byteon_hotfix, "BASE_ARCHIVE_SIZE", archive_size), \
             mock.patch.object(prepare_byteon_hotfix, "BASE_INVENTORY_SHA256", inventory_hash), \
             mock.patch.object(prepare_byteon_hotfix, "os", SimpleNamespace(name="nt", environ={"GITHUB_ACTIONS": "true"})), \
             mock.patch.object(prepare_byteon_hotfix, "download", side_effect=download_fixture), \
             mock.patch.object(sys, "argv", argv), mock.patch.object(sys, "version_info", (3, 8, 10)), \
             mock.patch("zlib.ZLIB_RUNTIME_VERSION", "1.2.11"):
            # Establish the reviewed hash through the local preparation path,
            # then reproduce it with both real builders through the CI entrypoint.
            reviewed = self.root / "reviewed"
            prepare_byteon_hotfix.build(source_archive, source_inventory, channel_path,
                                       approval, payload, notes, reviewed)
            argv[-1] = json.loads((reviewed / "update.json").read_text(encoding="utf-8"))["game"]["sha256"]
            prepare_byteon_hotfix.main()
        self.assertEqual(len(downloads), 2)
        self.assertEqual(json.loads((ci_baseline / "update.json").read_text(encoding="utf-8")), channel)
        self.assertEqual(channel_path.read_bytes(), original_channel)
        target = json.loads((output / "update.json").read_text(encoding="utf-8"))
        self.assertEqual(target["launcher"], channel["launcher"])
        entry = target["gameHotfixesV2"][0]
        self.assertEqual((entry["fromVersion"], entry["version"]), ("7.3.12", "7.3.13"))
        self.assertEqual([item["path"] for item in entry["files"]], [mod_path])
        self.assertFalse(entry["files"][0]["beforeExists"])


if __name__ == "__main__":
    unittest.main()
