import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import build_game_hotfix
import package_release


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


if __name__ == "__main__":
    unittest.main()
