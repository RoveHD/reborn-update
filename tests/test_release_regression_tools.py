"""Isolated release-tool regressions; no GitHub calls or active game writes.

Run: python -m unittest discover -s tests -p test_release_regression_tools.py -v
Publisher tests stub only source parity (tested separately by verify_release_parity.py)
and reject every attempted GitHub call. All install/delta/switch proofs are generated
by ReleaseChecks using the production launcher services and tiny synthetic packages.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "tests/ReleaseChecks/ReleaseChecks.csproj"
RUNNER = ROOT / "tests/ReleaseChecks/bin/Release/net9.0/ReleaseChecks.dll"
PUBLISHER = ROOT / "tools/publish-game-hotfix.ps1"
VERSION = "7.3.25"
URL_ROOT = f"https://github.com/RoveHD/reborn-update/releases/download/v{VERSION}/"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def zip_files(path: Path, files: dict[str, bytes]) -> None:
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in files.items():
            archive.writestr(name, data)


def inventory(files: dict[str, bytes]) -> dict:
    return {name: {"sha256": digest(data), "size": len(data)} for name, data in files.items()}


@unittest.skipUnless(os.name == "nt" and ROOT.drive.upper() == "D:", "Windows D: isolated hard-link fixtures required")
class ReleaseRegressionToolsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        subprocess.run(["dotnet", "build", str(PROJECT), "-c", "Release", "--nologo"], check=True, cwd=ROOT, capture_output=True, text=True)

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="release-tool-regression-", dir=ROOT / "tests")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.release = self.root / "candidate"
        self.release.mkdir()
        self.output = self.root / "checks"
        self.previous = {}
        self.baselines = {}
        self.target = {"Game.exe": b"synthetic executable", "Game.ini": b"synthetic ini",
                       "Scripts/Summary.rb": b"target summary", "patch/Mods/Runtime.rb": b"target runtime",
                       "patch/Data/restored.dat": b"restored runtime data", "intl.txt": b"candidate translations",
                       "Data/german.dat": b"compiled candidate translations"}
        archive = self.release / "Reborn-AllGen-0.9.9.zip"
        zip_files(archive, self.target)
        self.manifest = {"schemaVersion": 1, "launcher": {"version": "7.4.0", "url": "https://example.test/launcher.exe", "sha256": "1" * 64, "size": 1},
                         "game": {"version": VERSION, "url": URL_ROOT + archive.name, "sha256": digest(archive.read_bytes()), "size": archive.stat().st_size},
                         "gameHotfixes": [], "gameHotfixesV2": []}
        for version in ("7.3.23", "7.3.24"):
            previous = self.root / f"previous-{version}"
            previous.mkdir()
            baseline_parent = self.root / f"checks-{version}"
            baseline = baseline_parent / ("game-" + version)
            baseline.mkdir(parents=True)
            old = dict(self.target)
            old["intl.txt"] = b"baseline translations"
            old["Data/german.dat"] = b"compiled baseline translations"
            old["patch/Mods/Runtime.rb"] = version.encode()
            if version == "7.3.23":
                old["patch/Data/retired.dat"] = b"retired data"
            else:
                del old["patch/Data/restored.dat"]
            for name, data in old.items():
                path = baseline / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            (baseline / "version.txt").write_text(version)
            previous_manifest = json.loads(json.dumps(self.manifest))
            previous_manifest["game"]["version"] = version
            previous_manifest["game"]["sha256"] = digest(version.encode())
            previous_manifest["gameHotfixesV2"] = []
            write_json(previous / "update.json", previous_manifest)
            write_json(previous / "game-files.json", inventory(old))
            write_json(baseline_parent / "release-install.json", {"success": True, "version": version,
                       "archiveSha256": previous_manifest["game"]["sha256"], "isolatedInstall": str(baseline)})
            files, payload = [], {}
            for name in sorted(set(old) | set(self.target)):
                if old.get(name) == self.target.get(name):
                    continue
                entry = {"path": name, "beforeExists": name in old, "beforeSha256": digest(old[name]) if name in old else "",
                         "delete": name not in self.target, "sha256": digest(self.target[name]) if name in self.target else "",
                         "size": len(self.target[name]) if name in self.target else 0}
                files.append(entry)
                if name in self.target:
                    payload[name] = self.target[name]
            delta_name = f"Reborn-Hotfix-{version}-to-{VERSION}.zip"
            delta_path = self.release / delta_name
            zip_files(delta_path, payload)
            self.manifest["gameHotfixesV2"].append({"fromVersion": version, "version": VERSION, "url": URL_ROOT + delta_name,
                         "sha256": digest(delta_path.read_bytes()), "size": delta_path.stat().st_size, "files": files})
            self.previous[version], self.baselines[version] = previous, baseline
        self.save_manifest()
        write_json(self.release / "game-files.json", inventory(self.target))

    def save_manifest(self):
        write_json(self.release / "update.json", self.manifest)

    def run_check(self, *args, success=True):
        result = subprocess.run(["dotnet", str(RUNNER), str(self.release), str(self.output), *map(str, args)], cwd=ROOT, capture_output=True, text=True, timeout=90)
        if success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        return result

    def run_delta(self, version, success=True):
        return self.run_check("--delta-update-local", self.previous[version], self.baselines[version], success=success)

    def report(self, version):
        return self.release / f"release-update-delta-local-from-{version}.json"

    def prepare_publisher(self, with_german_proof=True):
        self.run_check()
        self.run_check("--repair")
        for name in ("release-install.json", "release-repair.json"):
            shutil.copyfile(self.output / name, self.release / name)
        for version in self.previous:
            self.run_delta(version)
        write_json(self.release / "source-parity.json", {"success": True})
        write_json(self.release / "package-checks.json", {"success": True})
        write_json(self.release / "hotfix-checks.json", {"success": True, "launcherUnchanged": True,
                   "archiveSha256": self.manifest["game"]["sha256"], "baselineGameVersion": "7.3.23"})
        if with_german_proof:
            self.write_german_proof()

    def write_german_proof(self):
        """Synthetic scene evidence tests the gate schema; it is not a real engine/UI claim."""
        input_path = self.root / "ui-input-proof.json"
        scene_path = self.root / "ui-scene-proof.json"
        write_json(input_path, {"success": True, "version": VERSION, "archiveSha256": self.manifest["game"]["sha256"],
                               "manifestSha256": digest((self.release / "update.json").read_bytes()),
                               "installedFilesVerifiedBeforeInstrumentation": len(self.target)})
        item_ids = ["SYNTHETIC_ITEM_A", "SYNTHETIC_ITEM_B"]
        write_json(scene_path, {"errors": [], "bag_errors": [], "translations": [{"en": "source", "de": "translation"}],
                               "bag_selections": item_ids,
                               "items": [{"id": item, "layouts": {context: {"width": 200, "height": 100,
                                        "text_width": 180, "text_height": 80, "overflow": False}
                                        for context in ("bag", "pc", "mart")}} for item in item_ids]})
        write_json(self.release / "german-hotfix-checks.json", {
            "success": True, "version": VERSION, "archiveSha256": self.manifest["game"]["sha256"],
            "sourceIntlSha256": digest(self.target["intl.txt"]), "compiledSemanticChanges": 1, "runtimeTranslationLookups": 1,
            "loadedItems": len(item_ids), "bagSelectionsRendered": len(item_ids), "itemErrors": [], "bagErrors": [], "overflowItems": 0,
            "installedRuntimeProof": {"success": True, "inputProof": str(input_path), "runtimeReport": str(scene_path),
                "inputProofSha256": digest(input_path.read_bytes()), "runtimeReportSha256": digest(scene_path.read_bytes()),
                "installedFilesVerifiedBeforeInstrumentation": len(self.target), "loadedItems": len(item_ids),
                "bagSelectionsRendered": len(item_ids), "itemErrors": 0, "bagErrors": 0, "overflowItems": 0,
                "runtimeTranslationLookups": 1, "engineExitCode": 0}})

    def publish_preflight(self, success=True, verify_public=False, fail_public=False):
        # Source parity has its own integration tests. No command in this test may reach GitHub.
        wrapper = self.root / "preflight.ps1"
        wrapper.write_text("param($Publisher, $Candidate, [switch]$VerifyPublic, [switch]$FailPublic)\n"
                           "function global:python { $global:LASTEXITCODE = 0 }\n"
                           "function global:gh { throw 'TEST FORBIDS GITHUB ACCESS' }\n"
                           # This stub tests orchestration only; it never claims real public-network coverage.
                           "function global:dotnet {\n"
                           "  Add-Content -LiteralPath (Join-Path $Candidate 'runner-calls.txt') -Value ($args -join ' ')\n"
                           "  $global:LASTEXITCODE = 0\n"
                           "  if ($args[0] -eq 'build') { return }\n"
                           "  if ($FailPublic) { $global:LASTEXITCODE = 1; return }\n"
                           "  $baselineIndex = [array]::IndexOf($args, '--delta-update-public') + 1\n"
                           "  if ($baselineIndex -eq 0) { throw 'Unexpected runner mode' }\n"
                           "  $oldManifest = Get-Content -LiteralPath (Join-Path $args[$baselineIndex] 'update.json') -Raw | ConvertFrom-Json\n"
                           "  $version = $oldManifest.game.version\n"
                           "  $proof = Get-Content -LiteralPath (Join-Path $Candidate ('release-update-delta-local-from-' + $version + '.json')) -Raw | ConvertFrom-Json\n"
                           "  $proof.proofMode = 'public'\n"
                           "  $proof.updateServiceUsedPublicChannel = $true\n"
                           "  $proof.publicManifestUrl = $proof.candidateManifestUrl\n"
                           "  $proof.publicDeltaUrl = $proof.candidateDeltaUrl\n"
                           "  $proof | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $Candidate ('release-update-delta-public-from-' + $version + '.json'))\n"
                           "}\n"
                           "& $Publisher -ReleaseDirectory $Candidate -VerifyPublic:$VerifyPublic\n", encoding="utf-8")
        args = ["pwsh", "-NoProfile", "-File", str(wrapper), str(PUBLISHER), str(self.release)]
        if verify_public:
            args.append("-VerifyPublic")
        if fail_public:
            args.append("-FailPublic")
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=90)
        self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
        self.assertNotIn("TEST FORBIDS GITHUB ACCESS", result.stdout + result.stderr)
        return result

    def test_both_local_baselines_use_updater_and_keep_distinct_bound_proofs(self):
        for version in self.previous:
            self.run_delta(version)
            report = json.loads(self.report(version).read_text())
            self.assertEqual(report["proofMode"], "local")
            self.assertTrue(report["realUpdateServiceUsed"])
            self.assertFalse(report["updateServiceUsedPublicChannel"])
            self.assertIsNone(report["publicManifestUrl"])
            self.assertIsNone(report["publicDeltaUrl"])
            self.assertFalse(report["fullPackageDownloaded"])
            self.assertEqual(report["switchesAfterDownload"], 2)
            self.assertEqual(report["installedTargetFilesVerified"], len(self.target))
            self.assertEqual(report["addedPathsVerifiedAfterSwitchBack"], 1 if version == "7.3.24" else 0)
            self.assertEqual(report["deletedPathsVerified"], 1 if version == "7.3.23" else 0)
            self.assertEqual(report["candidateManifestSha256"], digest((self.release / "update.json").read_bytes()).upper())
            self.assertEqual((self.baselines[version] / "patch/Mods/Runtime.rb").read_bytes(), version.encode())
        self.assertTrue(all(self.report(v).is_file() for v in self.previous))

    def test_corrupt_delta_fails_and_removes_previous_success(self):
        version = "7.3.24"
        self.run_delta(version)
        delta = self.release / self.manifest["gameHotfixesV2"][1]["url"].rsplit("/", 1)[1]
        delta.write_bytes(b"bad archive")
        result = self.run_delta(version, success=False)
        self.assertIn("Local candidate delta does not match its manifest", result.stderr)
        self.assertFalse(self.report(version).exists())
        self.assertEqual((self.baselines[version] / "patch/Mods/Runtime.rb").read_bytes(), version.encode())

    def test_baseline_report_and_full_inventory_are_required(self):
        version = "7.3.24"
        report_path = self.baselines[version].parent / "release-install.json"
        report = json.loads(report_path.read_text())
        report["archiveSha256"] = "0" * 64
        write_json(report_path, report)
        result = self.run_delta(version, success=False)
        self.assertIn("baseline install report does not match", result.stderr)
        self.assertFalse(self.report(version).exists())
        report["archiveSha256"] = digest(version.encode())
        write_json(report_path, report)
        (self.baselines[version] / "Game.ini").write_bytes(b"unexpected modification")
        result = self.run_delta(version, success=False)
        self.assertIn("inventory mismatch: Game.ini", result.stderr)
        self.assertFalse(self.report(version).exists())

    def test_unknown_mode_rejected_without_installing(self):
        self.run_check("--typo", success=False)
        self.assertFalse((self.output / "release-install.json").exists())

    def test_prepublication_requires_local_but_no_public_proofs(self):
        self.prepare_publisher()
        result = self.publish_preflight()
        self.assertIn("Lokal geprueft", result.stdout)
        self.assertFalse(list(self.release.glob("*delta-public*.json")))

    def test_missing_second_baseline_proof_blocks_publisher(self):
        self.prepare_publisher()
        self.report("7.3.24").unlink()
        self.publish_preflight(success=False)

    def test_stale_candidate_hash_or_fake_public_mode_blocks_publisher(self):
        self.prepare_publisher()
        path = self.report("7.3.24")
        original = path.read_bytes()
        proof = json.loads(original)
        proof["candidateManifestSha256"] = "0" * 64
        write_json(path, proof)
        self.publish_preflight(success=False)
        path.write_bytes(original)
        proof = json.loads(original)
        proof["updateServiceUsedPublicChannel"] = True
        write_json(path, proof)
        self.publish_preflight(success=False)

    def test_incomplete_repair_proof_blocks_publisher(self):
        self.prepare_publisher()
        path = self.release / "release-repair.json"
        proof = json.loads(path.read_text())
        proof["installedFilesVerified"] -= 1
        write_json(path, proof)
        self.publish_preflight(success=False)

    def test_public_verification_mode_runs_both_baselines_without_publishing(self):
        self.prepare_publisher()
        self.publish_preflight(verify_public=True)
        calls = (self.release / "runner-calls.txt").read_text().splitlines()
        self.assertEqual(len(calls), 3)
        self.assertTrue(calls[0].startswith("build "))
        for call, version in zip(calls[1:], self.previous):
            self.assertIn("--delta-update-public", call)
            self.assertIn(str(self.previous[version]), call)
            self.assertIn(str(self.baselines[version]), call)

    def test_failed_public_runner_cannot_report_public_success(self):
        self.prepare_publisher()
        result = self.publish_preflight(success=False, verify_public=True, fail_public=True)
        self.assertIn("Oeffentliche Launcher-Installation fehlgeschlagen", result.stderr)
        self.assertFalse(list(self.release.glob("*delta-public*.json")))

    def test_translation_inventory_change_requires_german_proof_without_changed_paths_hint(self):
        self.prepare_publisher(with_german_proof=False)
        result = self.publish_preflight(success=False)
        self.assertIn("Geaenderte Uebersetzungsdateien erfordern german-hotfix-checks.json", result.stderr)

    def test_stale_german_package_or_source_proof_blocks_publisher(self):
        self.prepare_publisher()
        path = self.release / "german-hotfix-checks.json"
        original = path.read_bytes()
        for property_name, bad_value in (("version", "7.3.24"), ("archiveSha256", "0" * 64), ("sourceIntlSha256", "0" * 64)):
            with self.subTest(property=property_name):
                proof = json.loads(original)
                proof[property_name] = bad_value
                write_json(path, proof)
                result = self.publish_preflight(success=False)
                self.assertIn("Uebersetzungs-/UI-Nachweis", result.stderr)
        path.write_bytes(original)

    def test_missing_or_incomplete_installed_ui_proof_blocks_publisher(self):
        self.prepare_publisher()
        path = self.release / "german-hotfix-checks.json"
        original = path.read_bytes()
        for corrupt in (lambda proof: proof.pop("installedRuntimeProof"),
                        lambda proof: proof["installedRuntimeProof"].update(installedFilesVerifiedBeforeInstrumentation=len(self.target) - 1),
                        lambda proof: proof["installedRuntimeProof"].update(bagSelectionsRendered=1),
                        lambda proof: proof["installedRuntimeProof"].update(runtimeTranslationLookups=0),
                        lambda proof: proof["installedRuntimeProof"].update(overflowItems=1),
                        lambda proof: proof.update(itemErrors=[{"id": "SYNTHETIC_ITEM_A", "error": "injected"}])):
            proof = json.loads(original)
            corrupt(proof)
            write_json(path, proof)
            result = self.publish_preflight(success=False)
            self.assertIn("Uebersetzungs-/UI-Nachweis", result.stderr)
        path.write_bytes(original)

    def test_stale_ui_input_or_real_scene_overflow_blocks_publisher(self):
        self.prepare_publisher()
        input_path = self.root / "ui-input-proof.json"
        original = input_path.read_bytes()
        proof = json.loads(original)
        proof["manifestSha256"] = "0" * 64
        write_json(input_path, proof)
        result = self.publish_preflight(success=False)
        self.assertIn("veraltet oder unvollstaendig", result.stderr)
        input_path.write_bytes(original)
        scene_path = self.root / "ui-scene-proof.json"
        scene = json.loads(scene_path.read_bytes())
        # A summary claiming zero overflows cannot hide an overflowing item in the actual scene report.
        scene["items"][0]["layouts"]["bag"]["text_height"] = 101
        write_json(scene_path, scene)
        german_path = self.release / "german-hotfix-checks.json"
        german = json.loads(german_path.read_bytes())
        german["installedRuntimeProof"]["runtimeReportSha256"] = digest(scene_path.read_bytes())
        write_json(german_path, german)
        result = self.publish_preflight(success=False)
        self.assertIn("Itembeschreibung nicht vollstaendig dargestellt", result.stderr)

    def test_valid_translation_proof_allows_generic_item_and_managed_file_counts(self):
        self.prepare_publisher()
        result = self.publish_preflight()
        self.assertIn("Lokal geprueft", result.stdout)


if __name__ == "__main__":
    unittest.main()
