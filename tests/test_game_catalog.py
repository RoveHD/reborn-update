"""Validate the actual-engine development catalog without loading saves or starting Game.exe."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "translation-review/catalog-source"
UPDATE_ROOT = ROOT / "reborn-update"


def load(name):
    return json.loads((OUTPUT / name).read_text(encoding="utf-8"))


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def release_inventory_for_runtime(runtime_root, update_root=UPDATE_ROOT):
    version_file = Path(runtime_root) / "version.txt"
    if not version_file.is_file():
        raise FileNotFoundError(f"Missing isolated launcher version proof: {version_file}")
    version = version_file.read_text(encoding="utf-8-sig").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError(f"Invalid isolated launcher version: {version!r}")
    inventory_path = Path(update_root) / "dist" / f"release-game-{version}-channel" / "game-files.json"
    if not inventory_path.is_file():
        raise FileNotFoundError(f"Missing release inventory for launcher game version {version}: {inventory_path}")
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    if not isinstance(inventory, dict) or not inventory:
        raise ValueError(f"Invalid or empty release inventory: {inventory_path}")
    for relative, proof in inventory.items():
        if (not isinstance(relative, str) or not isinstance(proof, dict) or
                not re.fullmatch(r"[0-9a-f]{64}", str(proof.get("sha256", ""))) or
                not isinstance(proof.get("size"), int) or isinstance(proof.get("size"), bool) or proof["size"] < 0):
            raise ValueError(f"Invalid file proof in release inventory {inventory_path}: {relative!r}")
    return version, inventory, inventory_path


def launcher_version_from_proof(runtime_root, source_info):
    """Validate launcher-owned version metadata separately from game inventory files."""
    version_file = Path(runtime_root) / "version.txt"
    if not version_file.is_file():
        raise FileNotFoundError(f"Missing isolated launcher version proof: {version_file}")
    version = version_file.read_text(encoding="utf-8-sig").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError(f"Invalid isolated launcher version: {version!r}")
    proof = source_info.get("launcherVersionProof") if isinstance(source_info, dict) else None
    if (not isinstance(proof, dict) or proof.get("path") != "version.txt" or
            proof.get("value") != version or proof.get("sha256") != sha256(version_file)):
        raise ValueError("Missing or mismatched launcher version proof")
    return version


def intl_ids(path):
    sections = {}
    section = None
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        text = line.rstrip()
        if not text.strip() or text.startswith("###"):
            continue
        match = re.fullmatch(r"\s*\[\s*([^\]]+)\s*\]\s*", text)
        if match:
            section = match.group(1)
            sections[section] = []
        else:
            if section is None:
                raise ValueError(f"Text before section on line {number}")
            sections[section].append(text)
    result = set()
    for section, lines in sections.items():
        indexed = lines[0].isdigit() if lines else False
        width = 3 if indexed else 2
        if len(lines) % width:
            raise ValueError(f"Invalid row count in {section}")
        for offset in range(0, len(lines), width):
            result.add(f"{section}/index/{lines[offset]}" if indexed else f"{section}/ordinal/{offset // width + 1:06d}")
    return result


class ActualGameCatalogTests(unittest.TestCase):
    def test_actual_engine_catalog_is_complete_and_hash_bound(self):
        catalog = load("entities.json")
        coverage = load("coverage.json")
        observations = load("runtime-texts.json")["observations"]
        proof = load("source-files.json")
        source = Path(catalog["source"]["runtimeRoot"])
        self.assertTrue(coverage["success"], coverage["errors"])
        self.assertTrue(coverage["actualEngineExport"])
        self.assertFalse(coverage["linguisticReviewComplete"])
        self.assertEqual(coverage["errors"], [])
        self.assertEqual(coverage["duplicateEntityIds"], [])
        self.assertEqual(catalog["source"]["gameTitle"], "AllGen Reborn 0.9.9")
        self.assertEqual(catalog["source"]["baseline"], "7.3.23 / AllGen 0.9.9")
        self.assertEqual(catalog["source"], proof["source"])
        launcher_version = launcher_version_from_proof(source, catalog["source"])
        self.assertEqual(catalog["source"]["launcherGameVersion"], launcher_version)
        self.assertNotIn("version.txt", proof["files"], "Launcher-owned metadata is not a game runtime file")
        self.assertEqual(catalog["source"]["exporterSha256"], sha256(ROOT / "reborn-update/tools/export_game_catalog.rb"))
        for relative, file_proof in proof["files"].items():
            with self.subTest(source=relative):
                self.assertFalse(file_proof.get("missing", False), relative)
                file = source / relative
                self.assertEqual(file.stat().st_size, file_proof["size"])
                self.assertEqual(sha256(file), file_proof["sha256"])
        entities = catalog["entities"]
        by_id = {row["entityId"]: row for row in entities}
        self.assertEqual(len(entities), len(by_id))
        self.assertEqual(len(entities), coverage["entityCount"])
        valid_refs = intl_ids(source / "intl.txt")
        for category, ids in catalog["categories"].items():
            with self.subTest(category=category):
                report = coverage["categories"][category]
                self.assertEqual(report["expected"], len(ids))
                self.assertEqual(report["exported"], len(ids))
                self.assertEqual(report["uniqueIds"], len(set(ids)))
                self.assertTrue(report["inventoryComplete"])
                self.assertTrue(all(by_id[entity_id]["category"] == category for entity_id in ids))
        for row in entities:
            self.assertTrue(set(row["entryIds"]) <= valid_refs, row["entityId"])
            self.assertIn(row["sourceFile"], proof["files"])
        for row in observations:
            self.assertIn(row["entityId"], by_id)
            self.assertTrue(set(row["entryIds"]) <= valid_refs, row["observationId"])
            self.assertEqual(row["linguisticStatus"], "unreviewed")
        self.assertEqual(len(catalog["categories"]["items"]), 1158)
        for category in ("items", "moves", "abilities"):
            descriptions = [row["entityId"] for row in observations if row["category"] == category and row["textType"] == "description"]
            self.assertCountEqual(descriptions, catalog["categories"][category])
        forms = [by_id[entity_id] for entity_id in catalog["categories"]["speciesForms"]]
        self.assertEqual(len(forms), sum(by_id[entity_id]["metadata"]["formCount"] for entity_id in catalog["categories"]["species"]))
        self.assertEqual(len(forms), len({(row["metadata"]["speciesEntityId"], row["form"]) for row in forms}))
        physical_maps = {int(path.stem[3:]) for root in (source / "Data", source / "patch/Data") for path in root.glob("Map[0-9]*.rxdata")}
        self.assertEqual(physical_maps, {int(by_id[entity_id]["sourceKey"]) for entity_id in catalog["categories"]["maps"]})
        diagnostics = load("event-context-maps-9-11-13.json")
        self.assertEqual({row["mapId"] for row in diagnostics["maps"]}, {9, 11, 13})
        self.assertTrue(all(row["events"] for row in diagnostics["maps"]))
        self.assertEqual(len(observations), coverage["textObservationCount"])
        self.assertEqual(sum(not row["entryIds"] for row in observations), coverage["textObservationsWithoutIntlRefs"])
        result = {"success": True, "actualEngineExportVerified": True, "linguisticReviewComplete": False,
                  "entityCount": len(entities), "textObservationCount": len(observations), "validIntlEntryIds": len(valid_refs),
                  "sourceFilesVerified": len(proof["files"]), "missingReferences": 0, "duplicateEntityIds": 0,
                  "deliberatelyExcludedDiagnosticHooks": [str(path.relative_to(source)) for path in (source / "patch/Mods").glob("*RuntimeSmoke.rb")],
                  "artifacts": {name: sha256(OUTPUT / name) for name in ("entities.json", "coverage.json", "runtime-texts.json", "source-files.json", "event-context-maps-9-11-13.json")}}
        inventory_version, managed, release_inventory = release_inventory_for_runtime(source)
        self.assertEqual(inventory_version, launcher_version)
        differences = [relative for relative, file_proof in proof["files"].items()
                       if file_proof["sha256"] != managed.get(relative, {}).get("sha256")]
        self.assertEqual(differences, [], "Engine stage differs from version-matched release sources")
        result["releaseSourceParity"] = {"launcherGameVersion": launcher_version,
                                         "inventory": str(release_inventory), "inventorySha256": sha256(release_inventory),
                                         "sourceFilesCompared": len(proof["files"]), "differences": differences}
        (OUTPUT / "verification.json").write_text(json.dumps(result, indent=2), encoding="utf-8")

    def test_launcher_version_selects_the_exact_release_inventory(self):
        from tempfile import TemporaryDirectory
        with TemporaryDirectory() as temp:
            root = Path(temp); runtime = root / "isolated-game"; runtime.mkdir()
            update = root / "update"
            (runtime / "version.txt").write_text("7.3.26\n", encoding="utf-8")
            inventory = update / "dist/release-game-7.3.26-channel/game-files.json"
            inventory.parent.mkdir(parents=True)
            inventory.write_text(json.dumps({"intl.txt": {"sha256": "a" * 64, "size": 12}}), encoding="utf-8")
            version, managed, selected = release_inventory_for_runtime(runtime, update)
            self.assertEqual(version, "7.3.26")
            self.assertEqual(selected, inventory)
            self.assertEqual(managed["intl.txt"]["size"], 12)
            proof = {"launcherGameVersion": "7.3.26", "launcherVersionProof": {
                "path": "version.txt", "value": "7.3.26", "sha256": sha256(runtime / "version.txt")}}
            self.assertEqual(launcher_version_from_proof(runtime, proof), "7.3.26")
            self.assertNotIn("version.txt", managed)

    def test_exporter_requires_and_records_isolated_version_and_fixed_baseline(self):
        exporter = (ROOT / "reborn-update/tools/export_game_catalog.rb").read_text(encoding="utf-8")
        self.assertIn("def launcher_game_version", exporter)
        self.assertIn("Missing isolated launcher version.txt", exporter)
        self.assertIn(r"/\A\d+\.\d+\.\d+\z/", exporter)
        self.assertIn("launcher_version = launcher_game_version", exporter)
        self.assertIn("def launcher_version_proof(value)", exporter)
        self.assertIn("launcherGameVersion: launcher_version, launcherVersionProof: launcher_proof", exporter)
        source_list = next(line for line in exporter.splitlines() if line.lstrip().startswith("%w[Game.exe"))
        self.assertNotIn("version.txt", source_list)
        self.assertIn("Launcher version changed during export", exporter)
        self.assertIn("baseline: '7.3.23 / AllGen 0.9.9'", exporter)

    def test_missing_invalid_or_wrong_version_inventory_fails_closed(self):
        from tempfile import TemporaryDirectory
        with TemporaryDirectory() as temp:
            root = Path(temp); runtime = root / "isolated-game"; runtime.mkdir()
            update = root / "update"
            with self.assertRaisesRegex(FileNotFoundError, "Missing isolated launcher version proof"):
                release_inventory_for_runtime(runtime, update)
            version_file = runtime / "version.txt"
            for bad_version in ("", "latest", "7.3", "7.3.26-beta"):
                version_file.write_text(bad_version, encoding="utf-8")
                with self.subTest(version=bad_version), self.assertRaisesRegex(ValueError, "Invalid isolated launcher version"):
                    release_inventory_for_runtime(runtime, update)
            version_file.write_text("7.3.26\n", encoding="utf-8")
            valid_proof = {"launcherVersionProof": {"path": "version.txt", "value": "7.3.26",
                                                       "sha256": sha256(version_file)}}
            self.assertEqual(launcher_version_from_proof(runtime, valid_proof), "7.3.26")
            for bad_proof in ({}, {"launcherVersionProof": {"path": "version.txt", "value": "7.3.25", "sha256": sha256(version_file)}},
                              {"launcherVersionProof": {"path": "version.txt", "value": "7.3.26", "sha256": "0" * 64}}):
                with self.subTest(proof=bad_proof), self.assertRaisesRegex(ValueError, "launcher version proof"):
                    launcher_version_from_proof(runtime, bad_proof)
            wrong = update / "dist/release-game-7.3.25-channel/game-files.json"
            wrong.parent.mkdir(parents=True)
            wrong.write_text(json.dumps({"intl.txt": {"sha256": "a" * 64, "size": 1}}), encoding="utf-8")
            with self.assertRaisesRegex(FileNotFoundError, "version 7.3.26"):
                release_inventory_for_runtime(runtime, update)
            exact = update / "dist/release-game-7.3.26-channel/game-files.json"
            exact.parent.mkdir(parents=True)
            for invalid_inventory in ("{}", "[]", json.dumps({"intl.txt": {"sha256": "bad", "size": 1}}),
                                      json.dumps({"intl.txt": {"sha256": "a" * 64, "size": -1}})):
                exact.write_text(invalid_inventory, encoding="utf-8")
                with self.assertRaises(ValueError):
                    release_inventory_for_runtime(runtime, update)


if __name__ == "__main__":
    unittest.main(verbosity=2)
