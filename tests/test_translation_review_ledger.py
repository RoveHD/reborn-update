import base64
from contextlib import redirect_stdout
from copy import deepcopy
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import translation_review_ledger as ledger


class TranslationReviewLedgerTests(unittest.TestCase):
    def sync_fixture(self, root, source, changes_source, **kwargs):
        options = {"changes_source": changes_source, "compile_path": root / "none", "structure_path": root / "none",
                   "runtime_path": root / "none", "proposals_source": root / "none", "entity_catalog_path": root / "none",
                   "runtime_texts_path": root / "none", "coverage_path": root / "none", "expected_reviewed_changes": 0}
        options.update(kwargs)
        return ledger.sync_ledger(source, root / "review", root, **options)

    def save_state(self, root, state, changes=None):
        review = root / "review"
        review.mkdir(exist_ok=True)
        (review / ledger.STATUS_FILE).write_text(json.dumps(state), encoding="utf-8")
        if changes is not None:
            (review / ledger.CHANGES_FILE).write_text(json.dumps(changes), encoding="utf-8")

    def change_source(self, root, changes=None, version="7.3.25"):
        path = root / ("changes-" + version + ".json")
        path.write_text(json.dumps({"baseline": "7.3.23", "releaseVersion": version, "changes": changes or []}), encoding="utf-8")
        return path

    def publication_snapshot(self, source_hash="a" * 64):
        return {"status": "published", "version": "7.3.25", "manifestSha256": "1" * 64,
                "inventorySha256": "2" * 64, "archiveProofSha256": "3" * 64, "sourceIntlSha256": source_hash,
                "publicChannelVerification": {"status": "passed", "sha256": "4" * 64},
                "requiredDeltaBaselines": ["7.3.23", "7.3.24"],
                "publicDeltaProofs": [{"status": "passed", "version": "7.3.25", "previousVersion": baseline,
                                      "manifestSha256": "1" * 64, "inventorySha256": "2" * 64}
                                     for baseline in ("7.3.23", "7.3.24")]}

    def test_parser_preserves_pair_and_indexed_three_line_ids(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "intl.txt"
            source.write_text("### header\n[Map1]\nhello\nhallo\nbye\ntschüss\n[20]\n7\nPOTION\nTrank\n", encoding="utf-8")
            entries, info = ledger.parse_source(source)
            self.assertEqual([entry["id"] for entry in entries], [
                "Map1/ordinal/000001", "Map1/ordinal/000002", "20/index/7"])
            self.assertEqual(entries[-1]["sourceLines"], {"en": 9, "de": 10})
            self.assertEqual((info["sections"], info["pairs"]), (2, 3))

    def test_identical_pairs_in_different_sections_have_distinct_stable_ids(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "intl.txt"
            source.write_text("[Map1]\nhello\nhallo\n[Map2]\nhello\nhallo\n", encoding="utf-8")
            entries, _ = ledger.parse_source(source)
            self.assertEqual(entries[0]["en"], entries[1]["en"])
            self.assertNotEqual(entries[0]["stableId"], entries[1]["stableId"])

    def test_sync_preserves_stable_entries_and_rechecks_changed_text_or_neighbor_context(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"; review = root / "review"
            source.write_text("[Map1]\na\nA\nb\nB\nc\nC\nd\nD\n", encoding="utf-8")
            changes = root / "changes-7.3.25.json"; changes.write_text(json.dumps({"baseline": "7.3.23", "releaseVersion": "7.3.25", "changes": []}))
            first, _ = ledger.sync_ledger(source, review, root, changes_source=changes,
                                         compile_path=root / "none", structure_path=root / "none",
                                         runtime_path=root / "none", proposals_source=root / "none",
                                         entity_catalog_path=root / "none", expected_reviewed_changes=0)
            first["entries"][0].update({"status": "reviewed-correct",
                "reviewedFingerprint": first["entries"][0]["textFingerprint"],
                "reviewedContextFingerprint": first["entries"][0]["contextFingerprint"],
                "reviewer": "root"})
            review.mkdir()
            (review / ledger.STATUS_FILE).write_text(json.dumps(first), encoding="utf-8")
            stable, _ = ledger.sync_ledger(source, review, root, changes_source=changes,
                                          compile_path=root / "none", structure_path=root / "none",
                                          runtime_path=root / "none", proposals_source=root / "none",
                                          entity_catalog_path=root / "none", expected_reviewed_changes=0)
            self.assertEqual(stable["entries"][0]["status"], "reviewed-correct")

            source.write_text("[Map1]\na\nA\nb\nNEW\nc\nC\nd\nD\n", encoding="utf-8")
            changed, _ = ledger.sync_ledger(source, review, root, changes_source=changes,
                                            compile_path=root / "none", structure_path=root / "none",
                                            runtime_path=root / "none", proposals_source=root / "none",
                                            entity_catalog_path=root / "none", expected_reviewed_changes=0)
            states = {row["id"]: row["status"] for row in changed["entries"]}
            self.assertEqual(states["Map1/ordinal/000002"], "recheck-required")
            self.assertEqual(states["Map1/ordinal/000001"], "recheck-required")
            self.assertEqual(states["Map1/ordinal/000002"], "recheck-required")
            self.assertEqual(states["Map1/ordinal/000004"], "unreviewed")

    def test_inserting_an_immediate_neighbor_preserves_identity_but_invalidates_review(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"; review = root / "review"
            source.write_text("[Map1]\nhello\nhallo\nbye\ntschüss\n", encoding="utf-8")
            changes = root / "changes-7.3.25.json"; changes.write_text(json.dumps({"baseline": "7.3.23", "releaseVersion": "7.3.25", "changes": []}))
            first, _ = ledger.sync_ledger(source, review, root, changes_source=changes,
                                         compile_path=root / "none", structure_path=root / "none",
                                         runtime_path=root / "none", proposals_source=root / "none",
                                         entity_catalog_path=root / "none", expected_reviewed_changes=0)
            reviewed = first["entries"][0]
            reviewed.update({"status": "reviewed-correct", "reviewer": "root"})
            review.mkdir()
            (review / ledger.STATUS_FILE).write_text(json.dumps(first), encoding="utf-8")
            source.write_text("[Map1]\nnew\nneu\nhello\nhallo\nbye\ntschüss\n", encoding="utf-8")
            second, _ = ledger.sync_ledger(source, review, root, changes_source=changes,
                                          compile_path=root / "none", structure_path=root / "none",
                                          runtime_path=root / "none", proposals_source=root / "none",
                                          entity_catalog_path=root / "none", expected_reviewed_changes=0)
            hello = next(row for row in second["entries"] if row["en"] == "hello")
            self.assertEqual(hello["id"], "Map1/ordinal/000002")
            self.assertEqual(hello["stableId"], reviewed["stableId"])
            self.assertEqual(hello["status"], "recheck-required")

    def test_agent_proposals_stay_separate_from_applied_changes_and_release(self):
        source = ledger.DEFAULT_SOURCE
        entries, _ = ledger.parse_source(source)
        proposals = ledger.load_agent_proposals(entries)
        self.assertEqual(len(proposals), 11)
        self.assertTrue(all(not row["applied"] and not row["published"] for row in proposals))
        self.assertTrue(all(row["rootDecision"] == "stale_source" for row in proposals if row["stale"]))
        self.assertTrue(all(not row["stale"] for row in proposals if row["rootDecision"] == "rejected_preserve_existing"))

    def test_current_inputs_have_expected_counts_and_only_43_applied_changes(self):
        entries, source = ledger.parse_source(ledger.DEFAULT_SOURCE)
        self.assertEqual((source["sections"], source["pairs"]), (916, 83933))
        self.assertEqual(sum(1 for row in entries if re_map(row["section"], 1, 9)), 136)
        self.assertEqual(sum(1 for row in entries if re_map(row["section"], 10, 19)), 538)
        runtime = ledger.read_json(ledger.RUNTIME_PROOF)
        changes = ledger.load_reviewed_changes(ledger.CHANGE_SOURCE, entries, runtime)
        self.assertEqual(len(changes), 43)
        self.assertTrue(all(row["releaseStatus"] == "pending" for row in changes))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            state, delta = ledger.sync_ledger(ledger.DEFAULT_SOURCE, root / "review", ledger.DEFAULT_SOURCE.parent)
            self.assertEqual(len(delta["changes"]), 43)
            self.assertEqual(delta["releaseStatus"], "pending")
            self.assertEqual(state["entityCatalog"]["entityCount"], 9093)
            self.assertEqual(state["entityCatalog"]["runtimeTextCount"], 116433)
            self.assertEqual(state["entityCatalog"]["coverageStatus"], "partial")
            self.assertTrue(state["coverageFlags"])
            self.assertTrue(all(row["releaseStatus"] == "pending" for row in delta["changes"]))

    def test_unverified_publication_cannot_mark_release_published(self):
        with self.assertRaises(ValueError):
            ledger.apply_publication({"entries": []}, {"changes": [], "releaseStatus": "pending"},
                                     {"status": "not-verified", "version": "7.3.25"})

    def test_entity_aggregate_requires_all_linked_texts_and_complete_observation_mapping(self):
        entries = [
            {"id": "1/ordinal/000001", "status": "reviewed-correct"},
            {"id": "1/ordinal/000002", "status": "unreviewed"},
        ]
        catalog = {"entities": [
            {"entityId": "items/mixed", "category": "items", "entryIds": ["1/ordinal/000001", "1/ordinal/000002"],
             "intlRefs": ["1/ordinal/000001"]},
            {"entityId": "items/unmapped", "category": "items", "entryIds": ["1/ordinal/000001"], "coverageFlags": []},
        ], "runtimeTexts": {"observations": [
            {"entityId": "items/mixed", "intlRefs": ["1/ordinal/000001"]},
            {"entityId": "items/unmapped", "intlRefs": []},
        ]}}
        entities, categories = ledger.aggregate_entity_reviews(entries, catalog)
        self.assertEqual(entities["items/mixed"]["status"], "unreviewed")
        self.assertEqual((entities["items/mixed"]["reviewed"], entities["items/mixed"]["total"], entities["items/mixed"]["percent"]), (1, 2, 50))
        self.assertEqual(entities["items/unmapped"]["status"], "unreviewed")
        self.assertTrue(entities["items/unmapped"]["unmapped"])
        self.assertEqual((categories["items"]["reviewed"], categories["items"]["total"], categories["items"]["percent"]), (1, 2, 50))
        self.assertEqual(categories["items"]["runtimeCount"], 2)

    def test_next_release_batch_keeps_prior_change_and_publication_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"; review = root / "review"
            source.write_text("[Map1]\nhello\nhallo\n", encoding="utf-8")
            review.mkdir()
            previous = {"schemaVersion": 1, "releaseVersion": "7.3.25", "releaseStatus": "published",
                        "publicationProof": self.publication_snapshot(),
                        "sourceVersion": "7.3.23", "changes": [{"entryId": "old-id", "after": "alt",
                        "releaseVersion": "7.3.25", "releaseStatus": "published"}]}
            (review / ledger.CHANGES_FILE).write_text(json.dumps(previous), encoding="utf-8")
            changes_source = root / "reviewed-7.3.26.json"
            changes_source.write_text(json.dumps({"baseline": "7.3.25", "releaseVersion": "7.3.26", "changes": []}))
            _, current = ledger.sync_ledger(source, review, root, changes_source=changes_source,
                                           compile_path=root / "none", structure_path=root / "none",
                                           runtime_path=root / "none", proposals_source=root / "none",
                                           entity_catalog_path=root / "none", expected_reviewed_changes=0)
            self.assertEqual(current["releaseVersion"], "7.3.26")
            self.assertEqual(current["releaseStatus"], "pending")
            self.assertEqual(current["history"][0]["releaseVersion"], "7.3.25")
            self.assertEqual(current["history"][0]["releaseStatus"], "published")
            self.assertEqual(current["history"][0]["publicationProof"]["manifestSha256"], "1" * 64)
            self.assertEqual(current["history"][0]["changes"][0]["entryId"], "old-id")

    def test_context_only_invalidation_survives_repeated_sync_of_the_same_review_source(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\nhello\nhallo\nbye\nTschuess\n", encoding="utf-8")
            changes_source = self.change_source(root, [{"section": "[Map1]", "line": 3, "en": "hello", "before": "alt",
                "after": "hallo", "reason": next(iter(ledger.CHANGE_CATEGORIES)), "reviewedAt": "2026-10-30"}])
            first, changes = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
            self.assertEqual(first["entries"][0]["status"], "reviewed-corrected")
            reviewed_context = first["entries"][0]["reviewedContextFingerprint"]
            self.save_state(root, first, changes)
            source.write_text("[Map1]\nhello\nhallo\nbye\nAnderer Kontext\n", encoding="utf-8")
            second, changes = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
            target = second["entries"][0]
            self.assertEqual(target["textFingerprint"], first["entries"][0]["textFingerprint"])
            self.assertNotEqual(target["contextFingerprint"], reviewed_context)
            self.assertEqual(target["status"], "recheck-required")
            self.assertEqual(target["invalidatedReview"]["reason"], "Context changed")
            self.assertEqual(target["reviewedContextFingerprint"], reviewed_context)
            self.save_state(root, second, changes)
            third, _ = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
            self.assertEqual(third["entries"][0]["status"], "recheck-required")
            self.assertEqual(len(third["entries"][0]["reviewHistory"]), 1)
            self.save_state(root, third)
            updated_review = ledger.read_json(changes_source)
            updated_review["changes"][0]["reviewedContextFingerprint"] = third["entries"][0]["contextFingerprint"]
            updated_review["changes"][0]["reviewedAt"] = "2026-10-31"
            changes_source.write_text(json.dumps(updated_review), encoding="utf-8")
            renewed, _ = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
            self.assertEqual(renewed["entries"][0]["status"], "reviewed-corrected")
            self.assertEqual(renewed["entries"][0]["reviewedContextFingerprint"], renewed["entries"][0]["contextFingerprint"])

    def test_old_rejected_proposal_cannot_reapprove_a_changed_context(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map11]\nhello\nhallo\nbye\nTschuess\n", encoding="utf-8")
            changes_source = self.change_source(root)
            proposals = root / "proposals.json"
            proposals.write_text(json.dumps({"reviewer": "root", "proposals": [{"section": "Map11", "source_line": 2,
                "en": "hello", "oldDE": "hallo", "proposedDE": "Guten Tag", "reason": "Preserve voice",
                "rootDecision": "rejected_preserve_existing", "applied": False, "published": False}]}), encoding="utf-8")
            first, _ = self.sync_fixture(root, source, changes_source, proposals_source=proposals)
            self.assertEqual(first["entries"][0]["status"], "reviewed-correct")
            self.save_state(root, first)
            source.write_text("[Map11]\nhello\nhallo\nbye\nKontext geaendert\n", encoding="utf-8")
            for _ in range(2):
                current, _ = self.sync_fixture(root, source, changes_source, proposals_source=proposals)
                self.assertEqual(current["entries"][0]["status"], "recheck-required")
                self.save_state(root, current)

    def test_map42_proposal_import_is_source_bound_and_stays_unapplied(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "intl.txt"
            source.write_text("[Map42]\nQuestion?\\ch[62,-1, A, B]\nFrage?\\ch[62,-1, A, B]\n", encoding="utf-8")
            source_hash = ledger.digest_bytes(source.read_bytes())
            proposals = root / "map42-proposals.json"
            proposals.write_text(json.dumps({"source": {"section": "Map42", "sha256": source_hash},
                "proposals": [{"line": 2, "deLine": 3, "exactEN": "Question?\\ch[62,-1, A, B]",
                    "beforeDE": "Frage?\\ch[62,-1, A, B]", "afterDE": "Frage?\\ch[62,-1, Alpha, Beta]",
                    "reason": "choice labels", "status": "proposal-pending-root-review", "applied": False,
                    "published": False}]}), encoding="utf-8")
            changes = self.change_source(root)
            state, delta = self.sync_fixture(root, source, changes, proposals_source=proposals)
            imported = delta["proposals"][0]
            self.assertEqual(imported["rootDecision"], "pending_root_review")
            self.assertEqual(imported["reviewedFingerprint"], state["entries"][0]["textFingerprint"])
            self.assertFalse(imported["applied"])
            self.assertFalse(imported["published"])
            self.assertEqual(state["entries"][0]["status"], "unreviewed")
            stale = json.loads(proposals.read_text(encoding="utf-8"))
            stale["source"]["sha256"] = "0" * 64
            proposals.write_text(json.dumps(stale), encoding="utf-8")
            stale_state, stale_delta = self.sync_fixture(root, source, changes, proposals_source=proposals)
            self.assertTrue(stale_delta["proposals"][0]["stale"])
            self.assertEqual(stale_delta["proposals"][0]["rootDecision"], "stale_source")
            self.assertEqual(stale_state["entries"][0]["status"], "unreviewed")

    def test_proposal_manifest_imports_multiple_bound_files_and_marks_stale_text(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "intl.txt"
            source.write_text("[Map1]\nA?\nA-DE\nB?\nB-DE\n", encoding="utf-8")
            files = []
            for line, en, de in ((2, "A?", "A-DE"), (4, "B?", "B-DE")):
                batch = root / f"p{line}.json"
                batch.write_text(json.dumps({"proposals": [{"section": "Map1", "source_line": line,
                    "en": en, "oldDE": de, "proposedDE": de + "!", "reason": "test",
                    "rootDecision": "pending_root_review", "reviewedFingerprint": ledger.digest_text([en, de]),
                    "applied": False, "published": False}]}), encoding="utf-8")
                files.append(batch.name)
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({"proposalFiles": files}), encoding="utf-8")
            state, delta = self.sync_fixture(root, source, self.change_source(root), proposals_source=manifest)
            self.assertEqual(len(delta["proposals"]), 2)
            self.assertTrue(all(row["rootDecision"] == "pending_root_review" for row in delta["proposals"]))
            stale = json.loads((root / "p2.json").read_text(encoding="utf-8"))
            stale["proposals"][0]["oldDE"] = "Old stale"
            (root / "p2.json").write_text(json.dumps(stale), encoding="utf-8")
            current, current_delta = self.sync_fixture(root, source, self.change_source(root), proposals_source=manifest)
            stale_proposal = next(item for item in current_delta["proposals"] if item["source"] == "p2.json")
            self.assertTrue(stale_proposal["stale"])
            self.assertEqual(stale_proposal["rootDecision"], "stale_source")
            self.assertEqual(current["entries"][1]["status"], "unreviewed")

    def test_only_explicit_current_root_review_scope_marks_named_entries(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "intl.txt"
            source.write_text("[Map1]\nA?\nA-DE\nB?\nB-DE\nC?\nC-DE\n", encoding="utf-8")
            entries, info = ledger.parse_source(source)
            section_fp = info["sectionStats"][0]["fingerprint"]
            scope_path = root / "scopes.json"
            scope = {"schemaVersion": 1, "scopes": [{"id": "agent-map1", "section": "Map1", "reviewer": "agent",
                "rootReviewed": False, "sectionFingerprint": section_fp,
                "entries": [{"entryId": entries[0]["id"], "textFingerprint": entries[0]["textFingerprint"],
                             "contextFingerprint": entries[0]["contextFingerprint"]}]}]}
            scope_path.write_text(json.dumps(scope), encoding="utf-8")
            changes = self.change_source(root)
            pending, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual([row["status"] for row in pending["entries"]], ["unreviewed"] * 3)
            self.assertEqual(pending["reviewScopes"][0]["scopeStatus"], "agent-review-pending")

            scope["scopes"][0].update({"rootReviewed": True, "reviewer": "root", "reviewedAt": "2026-10-30"})
            scope["scopes"][0]["entries"][0]["textFingerprint"] = "stale-text"
            scope_path.write_text(json.dumps(scope), encoding="utf-8")
            bad_entry_fp, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual(bad_entry_fp["reviewScopes"][0]["scopeStatus"], "stale")
            self.assertEqual([row["status"] for row in bad_entry_fp["entries"]], ["unreviewed"] * 3)

            scope["scopes"][0]["entries"][0]["textFingerprint"] = entries[0]["textFingerprint"]
            scope_path.write_text(json.dumps(scope), encoding="utf-8")
            approved, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual([row["status"] for row in approved["entries"]], ["reviewed-correct", "unreviewed", "unreviewed"])
            self.assertEqual(approved["reviewScopes"][0]["scopeStatus"], "approved-current")
            self.save_state(root, approved)

            scope["scopes"][0]["sectionFingerprint"] = "stale"
            scope_path.write_text(json.dumps(scope), encoding="utf-8")
            stale, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual(stale["reviewScopes"][0]["scopeStatus"], "stale")
            self.assertEqual(stale["entries"][0]["status"], "reviewed-correct")

    def test_reviewed_scope_invalidates_when_any_section_entry_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\nA?\nA-DE\nB?\nB-DE\nC?\nC-DE\nD?\nD-DE\n", encoding="utf-8")
            entries, info = ledger.parse_source(source)
            scope_path = root / "scopes.json"
            scope_path.write_text(json.dumps({"schemaVersion": 1, "scopes": [{"section": "Map1", "reviewer": "root",
                "rootReviewed": True, "reviewedAt": "2026-10-30", "sectionFingerprint": info["sectionStats"][0]["fingerprint"],
                "entries": [{"entryId": entries[0]["id"], "textFingerprint": entries[0]["textFingerprint"],
                             "contextFingerprint": entries[0]["contextFingerprint"]}]}]}), encoding="utf-8")
            changes = self.change_source(root)
            first, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual(first["entries"][0]["status"], "reviewed-correct")
            self.save_state(root, first)
            source.write_text("[Map1]\nA?\nA-DE\nB?\nB-DE\nC?\nC-DE\nD?\nD-CHANGED\n", encoding="utf-8")
            changed, _ = self.sync_fixture(root, source, changes, review_scopes_path=scope_path)
            self.assertEqual(changed["entries"][0]["status"], "recheck-required")
            self.assertEqual(changed["entries"][0]["invalidatedReview"]["reason"], "Reviewed section changed")

    def test_review_scope_cannot_bypass_an_unapplied_accepted_correction(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\nA?\nA-DE\n", encoding="utf-8")
            entries, info = ledger.parse_source(source)
            proposal_path = root / "proposal.json"
            proposal_path.write_text(json.dumps({"proposals": [{"section": "Map1", "source_line": 2,
                "en": "A?", "oldDE": "A-DE", "proposedDE": "A corrected", "reason": "known error",
                "rootDecision": "accepted_pending_application", "applied": False, "published": False}]}), encoding="utf-8")
            scope_path = root / "scopes.json"
            scope_path.write_text(json.dumps({"schemaVersion": 1, "scopes": [{"section": "Map1", "reviewer": "root",
                "rootReviewed": True, "reviewedAt": "2026-10-30", "sectionFingerprint": info["sectionStats"][0]["fingerprint"],
                "entries": [{"entryId": entries[0]["id"], "textFingerprint": entries[0]["textFingerprint"],
                             "contextFingerprint": entries[0]["contextFingerprint"]}]}]}), encoding="utf-8")
            state, _ = self.sync_fixture(root, source, self.change_source(root), proposals_source=proposal_path,
                                         review_scopes_path=scope_path)
            self.assertEqual(state["entries"][0]["status"], "error-found")

    def test_next_release_change_accepts_explicit_category_and_rejects_missing_category(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map26]\nOriginal source text\nSource mapping corrected\n", encoding="utf-8")
            entries, _ = ledger.parse_source(source)
            batch = root / "fresh-reviewed-translations-7.3.26.json"
            change = {"section": "Map26", "line": 3, "en": "Original source text", "before": "Original German",
                      "after": "Source mapping corrected", "reason": "Repair explicitly reviewed source alignment",
                      "category": "Quelltext-Zuordnung", "reviewedAt": "2026-10-30"}
            batch.write_text(json.dumps({"baseline": "7.3.25", "releaseVersion": "7.3.26", "changes": [change]}), encoding="utf-8")
            imported = ledger.load_reviewed_changes(batch, entries, {}, expected_count=1)
            self.assertEqual(imported[0]["category"], "Quelltext-Zuordnung")
            self.assertEqual(imported[0]["releaseVersion"], "7.3.26")
            del change["category"]
            batch.write_text(json.dumps({"baseline": "7.3.25", "releaseVersion": "7.3.26", "changes": [change]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "no explicit error category"):
                ledger.load_reviewed_changes(batch, entries, {}, expected_count=1)
            change["category"] = "   "
            batch.write_text(json.dumps({"baseline": "7.3.25", "releaseVersion": "7.3.26", "changes": [change]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "empty or invalid explicit category"):
                ledger.load_reviewed_changes(batch, entries, {}, expected_count=1)

    def test_runtime_event_context_and_map_source_changes_invalidate_unchanged_text(self):
        for change_file in (False, True):
            with self.subTest(map_source=change_file), tempfile.TemporaryDirectory() as temp:
                root = Path(temp); source = root / "intl.txt"
                source.write_text("[Map1]\nhello\nhallo\n", encoding="utf-8")
                changes_source = self.change_source(root)
                runtime_file = root / "runtime-texts.json"
                map_file = root / "Data/Map001.rxdata"
                map_file.parent.mkdir(); map_file.write_bytes(b"original event conditions")
                runtime_data = {"observations": [{"entityId": "maps/1", "textType": "eventDialogue", "sourceFile": "Data/Map001.rxdata",
                    "sourceKey": "1", "en": "hello", "de": "hallo", "sourceText": "hello",
                    "intlRefs": ["Map1/ordinal/000001"], "context": {"eventId": 1, "pageIndex": 0, "commandIndex": 3}}]}
                runtime_file.write_text(json.dumps(runtime_data), encoding="utf-8")
                first, _ = self.sync_fixture(root, source, changes_source, runtime_texts_path=runtime_file)
                first["entries"][0].update(status="reviewed-correct", reviewedFingerprint=first["entries"][0]["textFingerprint"],
                                            reviewedContextFingerprint=first["entries"][0]["contextFingerprint"])
                self.save_state(root, first)
                if change_file:
                    map_file.write_bytes(b"different event branch condition")
                else:
                    runtime_data["observations"][0]["context"]["pageIndex"] = 1
                    runtime_file.write_text(json.dumps(runtime_data), encoding="utf-8")
                second, _ = self.sync_fixture(root, source, changes_source, runtime_texts_path=runtime_file)
                self.assertEqual(second["entries"][0]["textFingerprint"], first["entries"][0]["textFingerprint"])
                self.assertEqual(second["entries"][0]["status"], "recheck-required")

    def test_nonadjacent_insert_preserves_current_review_and_pinned_fingerprints(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\na\nA\nb\nB\nc\nC\n", encoding="utf-8")
            changes_source = self.change_source(root)
            first, _ = self.sync_fixture(root, source, changes_source)
            first["entries"][2].update(status="reviewed-correct", reviewedFingerprint=first["entries"][2]["textFingerprint"],
                                        reviewedContextFingerprint=first["entries"][2]["contextFingerprint"])
            self.save_state(root, first)
            source.write_text("[Map1]\nnew\nNEU\na\nA\nb\nB\nc\nC\n", encoding="utf-8")
            second, _ = self.sync_fixture(root, source, changes_source)
            current = next(row for row in second["entries"] if row["en"] == "c")
            self.assertEqual(current["stableId"], first["entries"][2]["stableId"])
            self.assertEqual(current["status"], "reviewed-correct")
            self.assertEqual(current["reviewedContextFingerprint"], first["entries"][2]["reviewedContextFingerprint"])

    def public_release_fixture(self, root):
        release = root / "release"; release.mkdir()
        source = b"[Map1]\nhello\nhallo\n"
        (release / "intl.txt").write_bytes(source)
        manifest = {"game": {"version": "7.3.25", "url": "https://github.com/RoveHD/reborn-update/releases/download/v7.3.25/full.zip",
                             "sha256": ledger.digest_bytes(b"full archive"), "size": len(b"full archive")}, "gameHotfixesV2": []}
        inventory = {"intl.txt": {"sha256": ledger.digest_bytes(source), "size": len(source)}}
        for baseline in ("7.3.23", "7.3.24"):
            artifact = (baseline + " delta").encode()
            name = baseline + ".zip"
            (release / name).write_bytes(artifact)
            manifest["gameHotfixesV2"].append({"fromVersion": baseline, "version": "7.3.25", "files": [],
                "url": "https://github.com/RoveHD/reborn-update/releases/download/v7.3.25/" + name,
                "sha256": ledger.digest_bytes(artifact), "size": len(artifact)})
        (release / "update.json").write_text(json.dumps(manifest), encoding="utf-8")
        (release / "game-files.json").write_text(json.dumps(inventory), encoding="utf-8")
        (release / "source-parity.json").write_text(json.dumps({"success": True}), encoding="utf-8")
        manifest_bytes = (release / "update.json").read_bytes()
        manifest_hash = ledger.digest_bytes(manifest_bytes)
        inventory_hash = ledger.digest_bytes((release / "game-files.json").read_bytes())
        manifest_url = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json"
        channel = {"success": True, "version": "7.3.25", "verifiedAt": "2026-10-30T20:00:00Z", "publicManifestUrl": manifest_url,
                   "githubManifestUrl": "https://api.github.com/repos/RoveHD/reborn-update/contents/update.json?ref=main",
                   "manifestSha256": manifest_hash, "inventorySha256": inventory_hash, "gameSha256": manifest["game"]["sha256"],
                   "githubResponse": {"encoding": "base64", "content": base64.b64encode(manifest_bytes).decode()}}
        (release / "public-channel-verification.json").write_text(json.dumps(channel), encoding="utf-8")
        for delta in manifest["gameHotfixesV2"]:
            previous = root / ("previous-" + delta["fromVersion"])
            previous.mkdir()
            (previous / "game-files.json").write_text(json.dumps(inventory), encoding="utf-8")
            (previous / "update.json").write_text(json.dumps({"game": {"version": delta["fromVersion"]}}), encoding="utf-8")
            check = {"success": True, "proofMode": "public", "realUpdateServiceUsed": True, "updateServiceUsedPublicChannel": True,
                     "version": "7.3.25", "previousVersion": delta["fromVersion"], "publicManifestUrl": manifest_url,
                     "candidateManifestUrl": manifest_url, "publicDeltaUrl": delta["url"], "candidateDeltaUrl": delta["url"],
                     "candidateManifestSha256": manifest_hash, "targetInventorySha256": inventory_hash,
                     "archiveSha256": manifest["game"]["sha256"], "archiveSize": manifest["game"]["size"],
                     "deltaSha256": delta["sha256"], "deltaSize": delta["size"], "fullPackageDownloaded": False,
                     "installedTargetFilesVerified": len(inventory), "baselineFixtureUnchanged": True, "switchesAfterDownload": 2,
                     "previousFilesVerifiedAfterSwitch": len(inventory), "previousReleaseDirectory": str(previous),
                     "previousInventorySha256": ledger.digest_bytes((previous / "game-files.json").read_bytes()),
                     "personalSentinelsPreserved": ["mkxp.json", "settings.json", "Save Data/Game_3.rxdata", "patch/Mods/FriendsOwnMod.rb"],
                     "requestedUrls": [manifest_url, delta["url"]], "deletedPathsVerified": 0, "addedPathsVerifiedAfterSwitchBack": 0}
            (release / ("release-update-delta-public-from-" + delta["fromVersion"] + ".json")).write_text(json.dumps(check), encoding="utf-8")
        return release

    def verify_fixture(self, root, release):
        # Public evidence validation is real. Expensive local engine/package parity has its own integration tests.
        parity = SimpleNamespace(verify=lambda *args, **kwargs: {"success": True},
                                 verify_archive_only=lambda *args, **kwargs: {"success": True})
        with patch.dict(sys.modules, {"verify_release_parity": parity}):
            return ledger.verify_release_proof(release, root, root)

    def test_local_only_parity_never_confirms_publication(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); release = self.public_release_fixture(root)
            (release / "public-channel-verification.json").unlink()
            with self.assertRaisesRegex(ValueError, "Local package parity is not publication"):
                self.verify_fixture(root, release)
        with self.assertRaises(ValueError):
            ledger.apply_publication({"entries": []}, {"changes": [], "releaseVersion": "7.3.25"},
                                     {"status": "published", "version": "7.3.25", "sourceParity": {"success": True}})

    def test_all_offered_public_delta_proofs_and_real_github_manifest_are_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); release = self.public_release_fixture(root)
            proof = self.verify_fixture(root, release)
            self.assertTrue(ledger.publication_verified(proof, "7.3.25"))
            self.assertEqual({row["previousVersion"] for row in proof["publicDeltaProofs"]}, {"7.3.23", "7.3.24"})
            path = release / "release-update-delta-public-from-7.3.24.json"
            original = path.read_bytes()
            path.unlink()
            with self.assertRaisesRegex(ValueError, "Missing public UpdateService"):
                self.verify_fixture(root, release)
            path.write_bytes(original)
            for key, value in (("proofMode", "local"), ("candidateManifestSha256", "0" * 64),
                               ("targetInventorySha256", "0" * 64), ("archiveSha256", "0" * 64),
                               ("publicDeltaUrl", "https://example.test/wrong.zip"), ("fullPackageDownloaded", True),
                               ("installedTargetFilesVerified", 0), ("previousFilesVerifiedAfterSwitch", 0),
                               ("personalSentinelsPreserved", ["mkxp.json"]), ("success", False)):
                with self.subTest(key=key):
                    check = json.loads(original)
                    check[key] = value
                    path.write_text(json.dumps(check), encoding="utf-8")
                    with self.assertRaisesRegex(ValueError, "Public UpdateService delta proof"):
                        self.verify_fixture(root, release)
            path.write_bytes(original)
            channel_path = release / "public-channel-verification.json"
            channel = ledger.read_json(channel_path)
            channel["githubResponse"]["content"] = base64.b64encode(b"stale GitHub manifest").decode()
            channel_path.write_text(json.dumps(channel), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Actual public GitHub manifest differs"):
                self.verify_fixture(root, release)

    def test_valid_publication_and_history_survive_repeated_sync(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\nhello\nhallo\n", encoding="utf-8")
            changes_source = self.change_source(root, [{"section": "[Map1]", "line": 3, "en": "hello", "before": "alt",
                "after": "hallo", "reason": next(iter(ledger.CHANGE_CATEGORIES)), "reviewedAt": "2026-10-30"}])
            first, changes = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
            proof = self.publication_snapshot(ledger.digest_bytes(source.read_bytes()))
            ledger.apply_publication(first, changes, proof)
            self.save_state(root, first, changes)
            for _ in range(2):
                current, delta = self.sync_fixture(root, source, changes_source, expected_reviewed_changes=1)
                self.assertEqual(delta["releaseStatus"], "published")
                self.assertEqual(delta["changes"][0]["releaseStatus"], "published")
                self.assertEqual(current["entries"][0]["evidence"]["release"]["status"], "published")
                self.assertEqual(current["entries"][0]["change"]["releaseStatus"], "published")
                self.assertEqual(current["proofs"]["release"], proof)
                self.save_state(root, current, delta)
            next_source = self.change_source(root, version="7.3.26")
            for _ in range(2):
                current, delta = self.sync_fixture(root, source, next_source)
                self.assertEqual(delta["releaseStatus"], "pending")
                self.assertEqual(len(delta["history"]), 1)
                self.assertEqual(delta["history"][0]["releaseStatus"], "published")
                self.assertEqual(delta["history"][0]["publicationProof"], proof)
                self.assertEqual(current["entries"][0]["evidence"]["release"]["version"], "7.3.25")
                self.save_state(root, current, delta)

    def test_legacy_local_only_published_status_is_not_reused(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "intl.txt"
            source.write_text("[Map1]\nhello\nhallo\n", encoding="utf-8")
            changes_source = self.change_source(root)
            first, changes = self.sync_fixture(root, source, changes_source)
            first["entries"][0]["evidence"]["release"] = {"status": "published", "version": "7.3.25"}
            changes.update(releaseStatus="published", publicationProof={"status": "published", "version": "7.3.25", "sourceParity": {"success": True}})
            self.save_state(root, first, changes)
            current, delta = self.sync_fixture(root, source, changes_source)
            self.assertEqual(delta["releaseStatus"], "pending")
            self.assertIsNone(delta["publicationProof"])
            self.assertIn("invalidatedPublicationProof", delta)
            self.assertEqual(current["entries"][0]["evidence"]["release"]["status"], "not-confirmed")

    def test_html_command_loads_persisted_catalog_without_sync_or_status_writes(self):
        with tempfile.TemporaryDirectory() as temp:
            review = Path(temp)
            state = {"entries": [], "entityCatalog": {"entityCount": 1}}
            changes = {"changes": [], "releaseVersion": "7.3.25"}
            catalog = {"entities": [{"entityId": "species/BULBASAUR", "category": "species", "deName": "Bisasam", "entryIds": []}]}
            for name, value in ((ledger.STATUS_FILE, state), (ledger.CHANGES_FILE, changes), (ledger.ENTITY_CATALOG_FILE, catalog)):
                (review / name).write_text(json.dumps(value), encoding="utf-8")
            before = {name: (review / name).read_bytes() for name in (ledger.STATUS_FILE, ledger.CHANGES_FILE, ledger.ENTITY_CATALOG_FILE)}
            with patch.object(sys, "argv", ["ledger", "html", "--review-root", str(review)]), \
                    patch.object(ledger, "sync_ledger", side_effect=AssertionError("HTML must not sync")), redirect_stdout(io.StringIO()):
                ledger.main()
            self.assertIn("species/BULBASAUR", (review / ledger.HTML_FILE).read_text(encoding="utf-8"))
            for name, data in before.items():
                self.assertEqual((review / name).read_bytes(), data)

    def test_generated_html_escapes_untrusted_text_and_is_paginated(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "index.html"
            ledger.generate_html({"entries": [{"id": "Map1/ordinal/000001", "section": "Map1",
                "sectionKind": "map", "en": "<script>&", "de": "Hallo", "status": "unreviewed"}]},
                {"changes": [], "releaseVersion": "7.3.25"}, output)
            html = output.read_text(encoding="utf-8")
            self.assertIn("\\u003cscript\\u003e\\u0026", html)
            self.assertNotIn("<script>&", html)
            self.assertIn("pageSize=100", html)
            self.assertIn("textContent", html)
            self.assertEqual(html.count('id="release"'), 1)
            self.assertIn('id="releaseVersion"', html)
            self.assertIn("const entitySearchIndex=new Map(entityRows.map", html)
            self.assertIn("refs.some(entity=>matchingEntityIds.has(entity.entityId))", html)
            self.assertIn("for(const [id,haystack] of entitySearchIndex)", html)
            self.assertNotIn("refs.map(entitySearchIndex", html)
            self.assertIn('document.createElement("details")', html)
            self.assertIn('details.dataset.loaded="true"', html)
            self.assertIn('t.en??"nicht eindeutig erfasst"', html)
            self.assertIn('"Quelltext: "+t.sourceText', html)
            self.assertIn('entityReviewStats=DATA.entityReviews', html)
            self.assertIn('"entityCategoryStats"', html)
            self.assertIn('"Prüfung"', html)
            self.assertIn('"Geprüft am: "+e[9]', html)
            self.assertIn('EN-Zeile "+(e[10]?.en', html)


def re_map(section, low, high):
    if not section.startswith("Map") or not section[3:].isdigit():
        return False
    number = int(section[3:])
    return low <= number <= high


if __name__ == "__main__":
    unittest.main()
