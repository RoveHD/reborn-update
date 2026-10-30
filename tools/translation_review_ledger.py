"""Maintain a durable translation review ledger and generate local reports."""
from __future__ import annotations

import argparse
import base64
from collections import Counter
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import re
import sys
from datetime import datetime, timezone
from typing import Any


SCHEMA_VERSION = 1
GAME_RELATIVE = Path("Reborn-19.5.42_Allgen_0.9.5")
DEFAULT_SOURCE = Path(__file__).resolve().parents[2] / GAME_RELATIVE / "intl.txt"
DEFAULT_REVIEW_ROOT = Path(__file__).resolve().parents[2] / "translation-review"
DEFAULT_PROOF_ROOT = Path(__file__).resolve().parents[1]
CHANGE_SOURCE = DEFAULT_REVIEW_ROOT / "item-regression-20261030/fresh-reviewed-translations-7.3.25.json"
RUNTIME_PROOF = DEFAULT_REVIEW_ROOT / "item-regression-20261030/runtime-layout.json"
MAPS_10_19_PROPOSALS = DEFAULT_REVIEW_ROOT / "item-regression-20261030/review-proposals-maps10-19.json"
ENTITY_CATALOG = DEFAULT_REVIEW_ROOT / "catalog-source/entities.json"
RUNTIME_TEXTS = DEFAULT_REVIEW_ROOT / "catalog-source/runtime-texts.json"
COVERAGE_REPORT = DEFAULT_REVIEW_ROOT / "catalog-source/coverage.json"
COMPILE_PROOF = DEFAULT_PROOF_ROOT / "translation-build/german-dat-build.json"
STRUCTURE_PROOF = DEFAULT_REVIEW_ROOT / "item-regression-20261030/fresh-audit-7.3.25/audit-summary.json"
STATUS_FILE = "translation-review-status.json"
ENTITY_CATALOG_FILE = "translation-review-catalog.json"
CHANGES_FILE = "translation-changes.json"
CHANGELOG_FILE = "TRANSLATION-CHANGELOG.md"
RELEASES_DIR = "releases"
HTML_FILE = "index.html"

STATUSES = ("reviewed-correct", "reviewed-corrected", "unreviewed",
            "context-review-required", "recheck-required", "error-found")
STATUS_LABELS = {
    "reviewed-correct": "Perfekt / Geprüft",
    "reviewed-corrected": "Korrigiert / Geprüft",
    "unreviewed": "Ungeprüft",
    "context-review-required": "Kontextprüfung nötig",
    "recheck-required": "Erneute Prüfung nötig",
    "error-found": "Fehler festgestellt",
}
CHANGE_CATEGORIES = {
    "Singular, neutrales Pokemon-Geschlecht und Reiten statt Fahren": "Pokémon-Form und Grammatik",
    "Ferns Dialog frisch mit dem Original verglichen: Anrede, Aussage und Ton": "Dialogton, Anrede und Bedeutung",
    "Schild und Ladenname: vorhandenen Eigennamen erhalten, eindeutiges Deutsch": "Begriffe und Eigennamen",
}


def _proposal_paths(path: Path) -> list[Path]:
    if not path.is_file():
        return []
    data = read_json(path)
    if isinstance(data, dict) and isinstance(data.get("proposalFiles"), list):
        paths = [(path.parent / item).resolve() for item in data["proposalFiles"]]
        if not paths or any(not item.is_file() for item in paths):
            raise ValueError("Proposal manifest contains a missing or empty proposalFiles list")
        return paths
    return [path]


def load_agent_proposals(entries: list[dict[str, Any]], path: Path = MAPS_10_19_PROPOSALS,
                         source_sha256: str | None = None) -> list[dict[str, Any]]:
    by_line = {(row["section"], row["sourceLines"]["en"]): row for row in entries}
    proposals = []
    used_ids: set[str] = set()
    for proposal_path in _proposal_paths(path):
        data = read_json(proposal_path)
        if not isinstance(data, dict) or not isinstance(data.get("proposals"), list):
            raise ValueError(f"Invalid agent proposal data: {proposal_path.name}")
        source_meta = data.get("source") if isinstance(data.get("source"), dict) else {}
        file_sha = source_meta.get("sha256", data.get("sourceSha256"))
        file_sha_stale = file_sha is not None and file_sha != source_sha256
        for item in data["proposals"]:
            section = str(item.get("section") or source_meta.get("section") or "").strip("[]")
            source_line = item.get("source_line", item.get("line"))
            en = item.get("en", item.get("exactEN"))
            old_de = item.get("oldDE", item.get("beforeDE"))
            proposed_de = item.get("proposedDE", item.get("afterDE"))
            row = by_line.get((section, source_line))
            stale_reason = "source SHA changed" if file_sha_stale else None
            if row is None:
                stale_reason = stale_reason or "source entry no longer exists"
            elif row["en"] != en or row["de"] != old_de or (
                    item.get("deLine") is not None and item["deLine"] != row["sourceLines"]["de"]):
                stale_reason = stale_reason or "exact EN/DE source changed"
            claimed_fingerprint = item.get("reviewedFingerprint", item.get("textFingerprint"))
            # Exact EN/DE fields themselves provide a deterministic source fingerprint for legacy proposal files.
            bound_fingerprint = claimed_fingerprint or digest_text([en, old_de])
            if row is not None and bound_fingerprint != row["textFingerprint"]:
                stale_reason = stale_reason or "reviewed text fingerprint changed"
            if item.get("applied") is True or item.get("published") is True:
                raise ValueError("Proposal inputs cannot claim application or publication")
            if row is not None and not stale_reason and row["id"] in used_ids:
                raise ValueError(f"Duplicate proposal for entry {row['id']}")
            if row is not None and not stale_reason:
                used_ids.add(row["id"])
            decision = "stale_source" if stale_reason else item.get("rootDecision", "pending_root_review")
            if decision not in {"accepted_pending_application", "rejected_preserve_existing",
                                "context_required", "pending_root_review", "stale_source"}:
                raise ValueError(f"Unknown root proposal decision: {decision}")
            proposals.append({"entryId": row["id"] if row else None, "section": section,
                              "sourceLines": row["sourceLines"] if row else None, "en": en,
                              "before": old_de, "proposed": proposed_de,
                              "reason": item.get("reason", ""), "rootDecision": decision,
                              "reviewer": data.get("reviewer"), "reviewedAt": item.get("reviewedAt", data.get("reviewedAt")),
                              "applied": False, "published": False,
                              "reviewedFingerprint": bound_fingerprint,
                              "reviewedContextFingerprint": item.get("reviewedContextFingerprint"),
                              "sourceSHA256": file_sha, "semanticReviewRequired": item.get("semanticReviewRequired", False),
                              "stale": bool(stale_reason), "staleReason": stale_reason,
                              "source": proposal_path.name})
    return proposals


def load_review_scopes(path: Path | None, entries: list[dict[str, Any]],
                       section_fingerprints: dict[str, str]) -> tuple[list[dict[str, Any]], dict[str, dict[str, Any]]]:
    """Read schemaVersion 1 `{scopes:[{section, rootReviewed, sectionFingerprint,
    entries:[{entryId, textFingerprint, contextFingerprint}]}]}` scopes.

    Agent scopes remain metadata only; root-reviewed scopes bind to the full current
    section fingerprint and every explicitly named entry's current text/context.
    """
    if path is None or not path.is_file():
        return [], {}
    data = read_json(path)
    if not isinstance(data, dict) or data.get("schemaVersion") != 1 or not isinstance(data.get("scopes"), list):
        raise ValueError("Invalid review scope file; expected schemaVersion 1 and scopes[]")
    by_id = {row["id"]: row for row in entries}
    by_stable_id = {row["stableId"]: row for row in entries}
    output: list[dict[str, Any]] = []
    eligible: dict[str, dict[str, Any]] = {}
    for scope in data["scopes"]:
        section = str(scope.get("section", "")).strip("[]")
        section_matches = section_fingerprints.get(section) == scope.get("sectionFingerprint")
        records = scope.get("entries")
        if not isinstance(records, list) or not records:
            records = []
        resolved = []
        valid = section_matches and bool(records)
        seen: set[str] = set()
        for record in records:
            if isinstance(record, str):
                valid = False
                continue
            row = by_id.get(record.get("entryId")) or by_stable_id.get(record.get("entryId"))
            if (row is None or row["section"] != section or row["id"] in seen or
                    record.get("textFingerprint") != (row or {}).get("textFingerprint") or
                    record.get("contextFingerprint") != (row or {}).get("contextFingerprint")):
                valid = False
                continue
            seen.add(row["id"])
            resolved.append(row)
        root_reviewed = scope.get("rootReviewed") is True
        scope_status = "approved-current" if root_reviewed and valid else "stale" if root_reviewed else "agent-review-pending"
        normalized = {"id": scope.get("id", f"{section}:{path.name}"), "section": section,
                      "reviewer": scope.get("reviewer"), "reviewedAt": scope.get("reviewedAt"),
                      "rootReviewed": root_reviewed, "sectionFingerprint": scope.get("sectionFingerprint"),
                      "scopeStatus": scope_status, "entryIds": [row["id"] for row in resolved],
                      "source": path.name}
        output.append(normalized)
        if root_reviewed and valid:
            for row in resolved:
                eligible[row["id"]] = normalized
    return output, eligible


def digest_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def digest_text(value: Any) -> str:
    return digest_bytes(json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))


def parse_source(source: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    raw = source.read_bytes()
    lines = raw.decode("utf-8-sig").splitlines()
    sections: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None
    for line_no, line in enumerate(lines, 1):
        if re.fullmatch(r"\[[^\]]+\]", line):
            current = {"name": line[1:-1], "lines": []}
            sections.append(current)
        elif current is not None:
            current["lines"].append((line_no, line))
        elif not line.startswith("###"):
            raise ValueError(f"Unexpected text before first section at line {line_no}")

    entries: list[dict[str, Any]] = []
    section_stats: list[dict[str, Any]] = []
    seen_sections: set[str] = set()
    for section in sections:
        name = section["name"]
        if name in seen_sections:
            raise ValueError(f"Duplicate translation section: {name}")
        seen_sections.add(name)
        rows = section["lines"]
        indexed = bool(rows and re.fullmatch(r"\d+", rows[0][1]))
        stride = 3 if indexed else 2
        if len(rows) % stride:
            raise ValueError(f"Invalid {stride}-line translation structure in [{name}]: {len(rows)} lines")
        section_entries: list[dict[str, Any]] = []
        duplicate_occurrences: Counter[str] = Counter()
        for offset in range(0, len(rows), stride):
            ordinal = offset // stride + 1
            raw_index: str | None = None
            if indexed:
                raw_index = rows[offset][1]
                if not re.fullmatch(r"\d+", raw_index):
                    raise ValueError(f"Invalid indexed record in [{name}] at line {rows[offset][0]}")
                en_line, en = rows[offset + 1]
                de_line, de = rows[offset + 2]
                entry_id = f"{name}/index/{raw_index}"
                stable_id = entry_id
            else:
                en_line, en = rows[offset]
                de_line, de = rows[offset + 1]
                entry_id = f"{name}/ordinal/{ordinal:06d}"
                content_key = digest_text([en, de])
                duplicate_occurrences[content_key] += 1
                stable_id = f"{name}/text/{content_key}/{duplicate_occurrences[content_key]}"
            row = {"id": entry_id, "stableId": stable_id, "section": name,
                   "sectionKind": "map" if name.casefold().startswith("map") else "indexed-data" if indexed else "other-section",
                   "ordinal": ordinal, "rawIndex": raw_index,
                   "sourceLines": {"en": en_line, "de": de_line}, "en": en, "de": de}
            if any(existing["id"] == entry_id for existing in section_entries):
                raise ValueError(f"Duplicate stable entry identifier: {entry_id}")
            row["textFingerprint"] = digest_text([en, de])
            section_entries.append(row)
        for i, row in enumerate(section_entries):
            before = section_entries[i - 1] if i else None
            after = section_entries[i + 1] if i + 1 < len(section_entries) else None
            context = {"section": name,
                       "before": [before["en"], before["de"]] if before else None,
                       "after": [after["en"], after["de"]] if after else None}
            row["contextFingerprint"] = digest_text(context)
            entries.append(row)
        section_stats.append({"section": name, "kind": section_entries[0]["sectionKind"] if section_entries else
                              "map" if name.casefold().startswith("map") else "indexed-data" if indexed else "other-section",
                              "format": "indexed" if indexed else "pairs", "entries": len(section_entries),
                              "fingerprint": digest_text([[row["stableId"], row["textFingerprint"]]
                                                          for row in section_entries])})
    if len({entry["id"] for entry in entries}) != len(entries):
        raise ValueError("Duplicate entry IDs across translation sections")
    return entries, {"sha256": digest_bytes(raw), "bytes": len(raw), "lines": len(lines),
                     "sections": len(sections), "pairs": len(entries), "sectionStats": section_stats}


def read_json(path: Path) -> dict[str, Any] | list[Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def proof_snapshot(source: Path, game_root: Path, review_root: Path,
                   compile_path: Path = COMPILE_PROOF,
                   structure_path: Path = STRUCTURE_PROOF,
                   runtime_path: Path = RUNTIME_PROOF) -> dict[str, Any]:
    source_hash = digest_bytes(source.read_bytes())
    structure: dict[str, Any] = {"status": "not-verified", "path": str(structure_path)}
    if structure_path.is_file():
        audit = read_json(structure_path)
        if isinstance(audit, dict) and audit.get("sha256") == source_hash and not audit.get("structural_errors"):
            structure = {"status": "passed", "path": str(structure_path),
                         "sha256": digest_bytes(structure_path.read_bytes()),
                         "sections": audit.get("sections"), "pairs": audit.get("pairs"),
                         "structuralErrors": len(audit.get("structural_errors", []))}

    compile_evidence: dict[str, Any] = {"status": "not-verified", "path": str(compile_path)}
    runtime_files_ok = False
    if compile_path.is_file():
        proof = read_json(compile_path)
        expected_dat = proof.get("germanDatSha256") if isinstance(proof, dict) else None
        dat_path = game_root / "Data/german.dat"
        runtime_files = proof.get("runtimeFiles", {}) if isinstance(proof, dict) else {}
        runtime_files_ok = isinstance(runtime_files, dict) and bool(runtime_files)
        if runtime_files_ok:
            for relative, expected_hash in runtime_files.items():
                path = game_root / Path(relative)
                if not path.is_file() or digest_bytes(path.read_bytes()) != expected_hash:
                    runtime_files_ok = False
                    break
        if (isinstance(proof, dict) and proof.get("success") is True and
                proof.get("compiler") == "Pokemon Reborn bundled runtime pbCompileText" and
                proof.get("intlSha256") == source_hash and dat_path.is_file() and
                digest_bytes(dat_path.read_bytes()) == expected_dat and runtime_files_ok):
            compile_evidence = {"status": "passed", "path": str(compile_path),
                                "proofSha256": digest_bytes(compile_path.read_bytes()),
                                "intlSha256": source_hash, "germanDatSha256": expected_dat,
                                "compiledEntries": proof.get("entries"),
                                "runtimeFilesVerified": len(runtime_files)}
        else:
            compile_evidence = {"status": "stale-or-incomplete", "path": str(compile_path),
                                "proofSha256": digest_bytes(compile_path.read_bytes()),
                                "intlSha256": proof.get("intlSha256") if isinstance(proof, dict) else None}

    runtime_evidence: dict[str, Any] = {"status": "not-verified", "path": str(runtime_path),
                                        "translationLookups": 0, "itemBagRenderings": 0,
                                        "itemBagOverflow": None}
    if runtime_path.is_file():
        runtime = read_json(runtime_path)
        translations = runtime.get("translations", []) if isinstance(runtime, dict) else []
        items = runtime.get("items", []) if isinstance(runtime, dict) else []
        overflows = sum(1 for item in items if any(layout.get("overflow") is True
                         for layout in item.get("layouts", {}).values()))
        if (isinstance(runtime, dict) and runtime.get("title") == "AllGen Reborn 0.9.9" and
                not runtime.get("errors") and not runtime.get("bag_errors")):
            runtime_evidence = {"status": "passed", "path": str(runtime_path),
                                "sha256": digest_bytes(runtime_path.read_bytes()),
                                "translationLookups": len(translations), "itemBagRenderings": len(items),
                                "itemBagOverflow": overflows,
                                "bagSelections": len(runtime.get("bag_selections", []))}
    return {"linguisticReview": {"status": "entry-specific-only"},
            "structure": structure, "compile": compile_evidence,
            "runtimeReport": runtime_evidence,
            "visualDisplay": {"status": "not-manually-verified",
                              "note": "Runtime lookup and layout measurements are not manual visual confirmation."},
            "release": {"status": "not-confirmed"}}


def release_version_from_source(path: Path, data: dict[str, Any]) -> str:
    explicit = data.get("releaseVersion")
    if explicit:
        return str(explicit)
    match = re.search(r"(\d+\.\d+\.\d+)", path.name)
    if not match:
        raise ValueError("Change source must declare releaseVersion or include a version in its filename")
    return match.group(1)


def load_reviewed_changes(path: Path, entries: list[dict[str, Any]], runtime: dict[str, Any],
                          expected_count: int | None = None) -> list[dict[str, Any]]:
    data = read_json(path)
    if not isinstance(data, dict) or not data.get("baseline") or not isinstance(data.get("changes"), list):
        raise ValueError("Invalid reviewed translation change source")
    release_version = release_version_from_source(path, data)
    entry_by_location = {(row["section"], row["sourceLines"]["de"]): row for row in entries}
    runtime_rows = runtime.get("translations", []) if isinstance(runtime, dict) else []
    runtime_set = {(row.get("section", "").strip("[]"), row.get("en"), row.get("de")) for row in runtime_rows}
    output = []
    used_ids = set()
    for change in data["changes"]:
        section = change.get("section", "").strip("[]")
        row = entry_by_location.get((section, change.get("line")))
        if (row is None or row["en"] != change.get("en") or row["de"] != change.get("after") or
                change.get("before") == change.get("after")):
            raise ValueError(f"Reviewed change no longer matches source at {section}:{change.get('line')}")
        if row["id"] in used_ids:
            raise ValueError(f"Duplicate reviewed change entry: {row['id']}")
        used_ids.add(row["id"])
        reason = change.get("reason", "")
        supplied_category = change.get("category")
        if supplied_category is not None and (not isinstance(supplied_category, str) or not supplied_category.strip()):
            raise ValueError(f"Reviewed change has an empty or invalid explicit category at {section}:{change.get('line')}")
        category = supplied_category.strip() if isinstance(supplied_category, str) else CHANGE_CATEGORIES.get(reason)
        if not category:
            raise ValueError(f"Reviewed change has no explicit error category: {reason}")
        lookup_passed = runtime_set and (section, row["en"], row["de"]) in runtime_set
        output.append({"entryId": row["id"], "section": section,
                       "sourceLines": row["sourceLines"], "en": row["en"],
                       "before": change["before"], "after": row["de"],
                       "reason": reason, "category": category,
                       "semanticReview": {"status": "passed", "reviewer": "root",
                                          "reviewedAt": change.get("reviewedAt"), "source": path.name,
                                          "reviewedContextFingerprint": change.get("reviewedContextFingerprint")},
                       "runtimeLookup": {"status": "passed" if lookup_passed else "not-verified"},
                       "releaseVersion": release_version, "releaseStatus": "pending"})
    if expected_count is not None and len(output) != expected_count:
        raise ValueError(f"Expected {expected_count} root-reviewed changes; found {len(output)}")
    return output


def _empty_evidence(snapshot: dict[str, Any]) -> dict[str, Any]:
    return {"linguistic": {"status": "not-reviewed"},
            "structure": {"status": snapshot["structure"]["status"], "proofRef": "structure"},
            "compile": {"status": snapshot["compile"]["status"], "proofRef": "compile"},
            "runtimeLookup": {"status": "not-tested", "proofRef": None},
            "visualDisplay": {"status": "not-manually-verified", "proofRef": "visualDisplay"},
            "release": {"status": "not-confirmed", "proofRef": None}}


def bind_runtime_context(entries: list[dict[str, Any]], runtime_texts: Any, game_root: Path,
                         source_proof: Any = None) -> None:
    """Bind mapped event locations and their current source bytes, not export timestamps or observation ordinals."""
    if not isinstance(runtime_texts, dict):
        return
    by_id = {row["id"]: row for row in entries}
    contexts: dict[str, list[str]] = {}
    source_hashes: dict[str, str | None] = {}
    matches_snapshot: dict[str, bool] = {}
    expected_sources = source_proof.get("files", {}) if isinstance(source_proof, dict) else {}
    for observation in runtime_texts.get("observations", []):
        refs = observation.get("intlRefs", [])
        matched = [ref for ref in refs if ref in by_id and any(
            str(value).replace("\\n", "\n").replace("\\r", "\r") in
            {str(by_id[ref]["en"]).replace("\\n", "\n").replace("\\r", "\r"),
             str(by_id[ref]["de"]).replace("\\n", "\n").replace("\\r", "\r")}
            for value in (observation.get("en"), observation.get("de"), observation.get("sourceText")) if value is not None)]
        if not matched:
            continue
        relative = observation.get("sourceFile", "")
        if relative not in source_hashes:
            path = (game_root / relative).resolve()
            try:
                path.relative_to(game_root.resolve())
            except ValueError as error:
                raise ValueError("Runtime context source escapes the game root") from error
            source_hashes[relative] = digest_bytes(path.read_bytes()) if path.is_file() else None
        context = {key: observation.get(key) for key in ("entityId", "textType", "sourceFile", "sourceKey", "context")}
        context["sourceSha256"] = source_hashes[relative]
        fingerprint = digest_text(context)
        for ref in matched:
            contexts.setdefault(ref, []).append(fingerprint)
            matches_snapshot[ref] = matches_snapshot.get(ref, True) and source_hashes[relative] is not None and (
                source_hashes[relative] == expected_sources.get(relative, {}).get("sha256"))
    for entry_id, fingerprints in contexts.items():
        row = by_id[entry_id]
        row["textContextFingerprint"] = row["contextFingerprint"]
        row["runtimeContextFingerprint"] = digest_text(sorted(fingerprints))
        row["runtimeContextMatchesExportedSource"] = matches_snapshot[entry_id]
        row["contextFingerprint"] = digest_text([row["contextFingerprint"], sorted(fingerprints)])


def sync_ledger(source: Path, review_root: Path, game_root: Path,
                changes_source: Path = CHANGE_SOURCE, compile_path: Path = COMPILE_PROOF,
                structure_path: Path = STRUCTURE_PROOF, runtime_path: Path = RUNTIME_PROOF,
                entity_catalog_path: Path | None = None,
                proposals_source: Path = MAPS_10_19_PROPOSALS,
                runtime_texts_path: Path = RUNTIME_TEXTS,
                coverage_path: Path = COVERAGE_REPORT,
                expected_reviewed_changes: int | None = None,
                review_scopes_path: Path | None = None) -> tuple[dict[str, Any], dict[str, Any]]:
    entries, source_info = parse_source(source)
    runtime_texts = read_json(runtime_texts_path) if runtime_texts_path.is_file() else {}
    runtime_source_path = runtime_texts_path.parent / "source-files.json"
    runtime_source_proof = read_json(runtime_source_path) if runtime_source_path.is_file() else {}
    bind_runtime_context(entries, runtime_texts, game_root, runtime_source_proof)
    snapshot = proof_snapshot(source, game_root, review_root, compile_path, structure_path, runtime_path)
    runtime = read_json(runtime_path) if runtime_path.is_file() else {}
    reviewed_changes = load_reviewed_changes(changes_source, entries, runtime if isinstance(runtime, dict) else {},
                                             expected_reviewed_changes)
    change_source_data = read_json(changes_source)
    release_version = release_version_from_source(changes_source, change_source_data)
    reviewed_change_source_hash = digest_bytes(changes_source.read_bytes())
    changes_by_id = {change["entryId"]: change for change in reviewed_changes}
    proposals = load_agent_proposals(entries, proposals_source, source_info["sha256"])
    proposals_by_id = {proposal["entryId"]: proposal for proposal in proposals
                       if proposal["entryId"] is not None and not proposal.get("stale")}
    section_fingerprints = {item["section"]: item["fingerprint"] for item in source_info["sectionStats"]}
    review_scopes, scoped_entries = load_review_scopes(review_scopes_path, entries, section_fingerprints)

    old_path = review_root / STATUS_FILE
    old = read_json(old_path) if old_path.is_file() else {}
    old_by_id = {row.get("id"): row for row in old.get("entries", [])} if isinstance(old, dict) else {}
    old_entries = {row.get("stableId", row.get("id")): row for row in old.get("entries", [])} if isinstance(old, dict) else {}
    current_section_fingerprints = section_fingerprints
    for row in entries:
        previous = old_entries.get(row["stableId"]) or old_by_id.get(row["id"])
        if (previous is not None and "runtimeContextFingerprint" not in previous and
                row.get("runtimeContextMatchesExportedSource") is True and
                previous.get("contextFingerprint") == row.get("textContextFingerprint")):
            # Migrate neighbor-only ledgers only when the engine-exported source hashes still match live files.
            previous = deepcopy(previous)
            old_context = previous["contextFingerprint"]
            previous["contextFingerprint"] = row["contextFingerprint"]
            if previous.get("reviewedContextFingerprint", old_context) == old_context:
                previous["reviewedContextFingerprint"] = row["contextFingerprint"]
            row["contextBaselineMigratedFrom"] = "neighbor-only-with-unchanged-runtime-sources"
        text_same = previous is not None and previous.get("textFingerprint") == row["textFingerprint"]
        context_same = previous is not None and previous.get("contextFingerprint") == row["contextFingerprint"]
        scope_fingerprint_changed = (previous is not None and previous.get("reviewScopeFingerprint") is not None and
                                      previous.get("reviewScopeFingerprint") != current_section_fingerprints.get(row["section"]))
        context_same = context_same and not scope_fingerprint_changed
        same = text_same and context_same
        if same:
            state = {key: deepcopy(previous.get(key)) for key in
                     ("status", "reviewedChangeSourceSha256", "lastReviewedAt",
                      "reviewer", "errorCategory", "changeReason", "evidence", "agentReview", "change",
                      "reviewedFingerprint", "reviewedContextFingerprint", "invalidatedReview", "reviewHistory",
                      "reviewScopeFingerprint")
                     if key in previous}
        else:
            state = {"status": "recheck-required" if previous and (not text_same or previous.get("status") != "unreviewed") else "unreviewed",
                     "lastReviewedAt": None, "reviewer": None, "errorCategory": None,
                     "changeReason": None, "evidence": _empty_evidence(snapshot)}
            if previous:
                reviewed_context = previous.get("reviewedContextFingerprint", previous.get("contextFingerprint"))
                reviewed_text = previous.get("reviewedFingerprint", previous.get("textFingerprint"))
                state["invalidatedReview"] = {"oldStatus": previous.get("status"),
                                              "oldTextFingerprint": reviewed_text,
                                              "oldContextFingerprint": reviewed_context,
                                              "oldReviewedChangeSourceSha256": previous.get("reviewedChangeSourceSha256"),
                                              "reason": "Reviewed section changed" if scope_fingerprint_changed else "Context changed" if text_same else "Text changed"}
                state["reviewedFingerprint"] = reviewed_text
                state["reviewedContextFingerprint"] = reviewed_context
                state["reviewHistory"] = [*deepcopy(previous.get("reviewHistory", [])),
                    {"status": previous.get("status"), "textFingerprint": reviewed_text, "contextFingerprint": reviewed_context,
                     "evidence": deepcopy(previous.get("evidence", {})), "invalidation": state["invalidatedReview"]}]
                if text_same and previous.get("evidence", {}).get("release", {}).get("status") == "published":
                    state["evidence"]["release"] = deepcopy(previous["evidence"]["release"])
        state.setdefault("status", "unreviewed")
        previously_reviewed_text = (previous.get("textFingerprint") == row["textFingerprint"] or
                                    previous.get("invalidatedReview", {}).get("oldTextFingerprint") == row["textFingerprint"]) if previous else False
        previous_change_source_hash = (previous.get("reviewedChangeSourceSha256") or
                                       previous.get("invalidatedReview", {}).get("oldReviewedChangeSourceSha256")) if previous else None
        old_context_current = previous is not None and previous.get("reviewedContextFingerprint", previous.get("contextFingerprint")) == row["contextFingerprint"]
        context_claim = changes_by_id.get(row["id"], {}).get("semanticReview", {}).get("reviewedContextFingerprint")
        eligible_fresh_review = (previous is None or
            (same and state["status"] != "recheck-required" and old_context_current) or
            (previous_change_source_hash != reviewed_change_source_hash and previously_reviewed_text and
             (old_context_current or context_claim == row["contextFingerprint"])))
        eligible_fresh_review = eligible_fresh_review and (context_claim is None or context_claim == row["contextFingerprint"])
        if row["id"] in changes_by_id and eligible_fresh_review:
            change = changes_by_id[row["id"]]
            state.update({"status": "reviewed-corrected",
                          "reviewedFingerprint": row["textFingerprint"], "reviewedContextFingerprint": row["contextFingerprint"],
                          "reviewedChangeSourceSha256": reviewed_change_source_hash,
                          "lastReviewedAt": change["semanticReview"]["reviewedAt"],
                          "reviewer": change["semanticReview"]["reviewer"],
                          "errorCategory": change["category"], "changeReason": change["reason"],
                          "change": change,
                          "evidence": {"linguistic": change["semanticReview"],
                                       "structure": {"status": snapshot["structure"]["status"], "proofRef": "structure"},
                                       "compile": {"status": snapshot["compile"]["status"], "proofRef": "compile"},
                                       "runtimeLookup": {**change["runtimeLookup"], "proofRef": "runtimeReport"},
                                       "visualDisplay": {"status": "not-manually-verified", "proofRef": "visualDisplay"},
                                       "release": {"status": "pending", "version": release_version}}})
            if same and previous.get("evidence", {}).get("release", {}).get("status") == "published":
                state["evidence"]["release"] = deepcopy(previous["evidence"]["release"])
        else:
            if state.get("evidence") is None:
                state["evidence"] = _empty_evidence(snapshot)
            else:
                state["evidence"]["structure"] = snapshot["structure"]
                state["evidence"]["compile"] = snapshot["compile"]
            proposal = proposals_by_id.get(row["id"])
            if proposal:
                state["agentProposal"] = proposal
                decision = proposal["rootDecision"]
                proposal_context = proposal.get("reviewedContextFingerprint")
                root_review_context_still_current = ((previous is None or (text_same and old_context_current) or
                    proposal_context == row["contextFingerprint"]) and
                    (proposal_context is None or proposal_context == row["contextFingerprint"]))
                if not root_review_context_still_current:
                    state["status"] = "recheck-required"
                elif decision == "accepted_pending_application":
                    state.update({"status": "error-found", "reviewer": "root",
                                  "errorCategory": "recognized correction not yet applied",
                                  "changeReason": proposal["reason"],
                                  "evidence": {**state["evidence"], "linguistic": {"status": "error-found", "reviewer": "root", "reviewedAt": None}}})
                elif decision == "context_required":
                    state.update({"status": "context-review-required", "reviewer": "root",
                                  "changeReason": proposal["reason"],
                                  "evidence": {**state["evidence"], "linguistic": {"status": "context-required", "reviewer": "root", "reviewedAt": None}}})
                elif decision == "rejected_preserve_existing":
                    state.update({"status": "reviewed-correct", "reviewer": "root",
                                  "lastReviewedAt": None,
                                  "changeReason": proposal["reason"],
                                  "evidence": {**state["evidence"], "linguistic": {"status": "passed", "reviewer": "root", "reviewedAt": None}}})
                if root_review_context_still_current:
                    state["reviewedFingerprint"] = row["textFingerprint"]
                    state["reviewedContextFingerprint"] = row["contextFingerprint"]
            scope = scoped_entries.get(row["id"])
            if scope is not None and (proposal is None or proposal["rootDecision"] == "rejected_preserve_existing"):
                state.update({"status": "reviewed-correct", "reviewer": scope.get("reviewer") or "root",
                              "lastReviewedAt": scope.get("reviewedAt"), "errorCategory": None,
                              "changeReason": "Explicitly reviewed in root-approved section scope",
                              "reviewedFingerprint": row["textFingerprint"],
                              "reviewedContextFingerprint": row["contextFingerprint"],
                              "reviewScopeFingerprint": current_section_fingerprints[row["section"]],
                              "evidence": {**state.get("evidence", {}),
                                           "linguistic": {"status": "passed", "reviewer": scope.get("reviewer") or "root",
                                                          "reviewedAt": scope.get("reviewedAt"), "scopeId": scope["id"]}}})
        active_scope = next((scope for scope in review_scopes if scope["section"] == row["section"]), None)
        if active_scope is not None:
            row["agentReview"] = active_scope
        evidence = state.get("evidence", {})
        for name, ref in (("structure", "structure"), ("compile", "compile")):
            if isinstance(evidence.get(name), dict):
                evidence[name] = {"status": evidence[name].get("status", "not-verified"), "proofRef": ref}
        if isinstance(evidence.get("runtimeLookup"), dict) and "proofRef" not in evidence["runtimeLookup"]:
            evidence["runtimeLookup"] = {"status": evidence["runtimeLookup"].get("status", "not-tested"), "proofRef": "runtimeReport"}
        state["evidence"] = evidence
        row.update(state)
        if row["status"] in {"reviewed-correct", "reviewed-corrected"}:
            row.setdefault("reviewedFingerprint", row["textFingerprint"])
            row.setdefault("reviewedContextFingerprint", row["contextFingerprint"])
        if row["status"] not in STATUSES:
            raise ValueError(f"Invalid persisted review status {row['status']} for {row['id']}")

    current_ids = {row["id"] for row in entries}
    current_stable_ids = {row["stableId"] for row in entries}
    retired = list(old.get("retiredEntries", [])) if isinstance(old, dict) else []
    retired.extend(row for entry_id, row in old_entries.items() if entry_id not in current_ids and entry_id not in current_stable_ids)
    counts = Counter(row["status"] for row in entries)
    sections = []
    for section in source_info["sectionStats"]:
        scoped = [row for row in entries if row["section"] == section["section"]]
        section["statusCounts"] = dict(Counter(row["status"] for row in scoped))
        section["complete"] = bool(scoped) and all(row["status"] in {"reviewed-correct", "reviewed-corrected"} for row in scoped)
        sections.append(section)
    proposal_groups: dict[str, list[dict[str, Any]]] = {}
    for proposal in proposals:
        proposal_groups.setdefault(proposal["source"], []).append(proposal)
    proposal_batches = [{"id": name, "sections": sorted({row["section"] for row in batch}),
                         "entries": len(batch),
                         "decisions": dict(Counter(row["rootDecision"] for row in batch)),
                         "disposition": "proposals-unapplied", "applied": 0, "published": 0}
                        for name, batch in sorted(proposal_groups.items())]
    entity_catalog_path = entity_catalog_path or ENTITY_CATALOG
    if entity_catalog_path.is_file():
        entities = read_json(entity_catalog_path)
        if not isinstance(entities, dict) or entities.get("schemaVersion") != 1:
            raise ValueError("Invalid runtime entity catalog schema")
        if runtime_texts_path.is_file():
            entities["runtimeTexts"] = runtime_texts
        if coverage_path.is_file():
            entities["coverage"] = read_json(coverage_path)
    else:
        entities = {"schemaVersion": 1, "coverageStatus": "unknown",
                    "coverageFlag": "Runtime entity export is not available yet.", "entities": [], "categories": {}}
    coverage = entities.get("coverage", {})
    coverage_complete = bool(coverage.get("success") and coverage.get("linguisticReviewComplete") and
                             not coverage.get("unresolvedGroups") and not coverage.get("unresolvedEventExpressions"))
    coverage_flag = None if coverage_complete else "Inventory extracted, but linguistic review or resource coverage remains incomplete."
    entities["coverageStatus"] = "complete" if coverage_complete else "partial"
    entities["coverageFlag"] = coverage_flag
    entity_summary = {key: entities.get(key) for key in ("schemaVersion", "source", "categories") if key in entities}
    entity_summary.update({"entityCount": len(entities.get("entities", [])),
                           "runtimeTextCount": len(entities.get("runtimeTexts", {}).get("observations", [])),
                           "coverageStatus": "complete" if coverage_complete else "partial",
                           "coverage": entities.get("coverage"),
                           "coverageFlag": coverage_flag})
    ledger = {"schemaVersion": SCHEMA_VERSION, "gameVersion": "AllGen 0.9.9",
              "source": {"path": source.name, **source_info},
              "statusCounts": {status: counts.get(status, 0) for status in STATUSES},
              "reviewBatches": proposal_batches, "reviewScopes": review_scopes, "proofs": snapshot,
              "entityCatalog": entity_summary,
              "_entityCatalogData": entities,
              "coverageFlags": [] if coverage_complete else [entity_summary["coverageFlag"]],
              "sections": sections, "entries": entries, "retiredEntries": retired}
    changes_output = {"schemaVersion": SCHEMA_VERSION, "sourceVersion": change_source_data["baseline"],
                      "releaseVersion": release_version, "releaseStatus": "pending",
                      "sourceIntlSha256": source_info["sha256"], "reviewedChangeSourceSha256": reviewed_change_source_hash,
                      "publicationProof": None, "changes": reviewed_changes,
                      "proposals": proposals, "proposalBatches": proposal_batches,
                      "reviewScopes": review_scopes}
    old_change_path = review_root / CHANGES_FILE
    if old_change_path.is_file():
        previous = read_json(old_change_path)
        history = list(previous.get("history", [])) if isinstance(previous, dict) else []
        if isinstance(previous, dict) and previous.get("releaseVersion"):
            old_batch = {key: previous.get(key) for key in
                         ("releaseVersion", "releaseStatus", "publicationProof", "changes", "sourceVersion", "sourceIntlSha256")}
            previous_public = publication_verified(previous.get("publicationProof"), previous["releaseVersion"])
            if old_batch["releaseStatus"] == "published" and not previous_public:
                old_batch = deepcopy(old_batch)
                old_batch["releaseStatus"] = "pending"
                old_batch["invalidatedPublicationProof"] = old_batch["publicationProof"]
                old_batch["publicationProof"] = None
                for change in old_batch.get("changes", []):
                    change["releaseStatus"] = "pending"
            if old_batch["releaseVersion"] != release_version:
                history.append(old_batch)
            elif previous_public and previous["publicationProof"].get("sourceIntlSha256") == source_info["sha256"]:
                changes_output["releaseStatus"] = "published"
                changes_output["publicationProof"] = previous["publicationProof"]
                ledger["proofs"]["release"] = previous["publicationProof"]
                for current in changes_output["changes"]:
                    former = next((x for x in previous.get("changes", [])
                                   if all(x.get(key) == current.get(key) for key in ("entryId", "en", "before", "after"))), None)
                    if former and former.get("releaseStatus") == "published":
                        current["releaseStatus"] = "published"
                        ledger_entry = next(row for row in entries if row["id"] == current["entryId"])
                        ledger_entry["evidence"]["release"] = deepcopy(old_by_id.get(current["entryId"], {}).get("evidence", {}).get("release",
                            {"status": "published", "version": release_version, "manifestSha256": previous["publicationProof"]["manifestSha256"]}))
                        if isinstance(ledger_entry.get("change"), dict):
                            ledger_entry["change"]["releaseStatus"] = "published"
            elif previous.get("releaseStatus") == "published":
                if previous_public:
                    history.append(old_batch)
                else:
                    changes_output["invalidatedPublicationProof"] = previous.get("publicationProof")
        changes_output["history"] = history
    valid_versions = {batch.get("releaseVersion") for batch in [changes_output, *changes_output.get("history", [])]
                      if publication_verified(batch.get("publicationProof"), batch.get("releaseVersion"))}
    for entry in entries:
        release_evidence = entry.get("evidence", {}).get("release", {})
        if release_evidence.get("status") == "published" and release_evidence.get("version") not in valid_versions:
            entry["evidence"]["release"] = {"status": "not-confirmed", "version": release_evidence.get("version"),
                                            "invalidatedPublicationEvidence": release_evidence}
        if isinstance(entry.get("change"), dict) and entry["change"].get("releaseVersion") not in valid_versions:
            entry["change"]["releaseStatus"] = "pending"
    return ledger, changes_output


def verify_release_proof(release_dir: Path, game_root: Path, project_root: Path) -> dict[str, Any]:
    manifest_path = release_dir / "update.json"
    inventory_path = release_dir / "game-files.json"
    archive_proof_path = release_dir / "source-parity.json"
    if not all(path.is_file() for path in (manifest_path, inventory_path, archive_proof_path)):
        raise FileNotFoundError("Release publication needs update.json, game-files.json, and source-parity.json")
    manifest = read_json(manifest_path)
    version = str(manifest.get("game", {}).get("version", ""))
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("Publication manifest has no game version")
    inventory = read_json(inventory_path)
    if not isinstance(inventory, dict) or not inventory:
        raise ValueError("Publication needs a nonempty managed-file inventory")
    manifest_hash = digest_bytes(manifest_path.read_bytes())
    inventory_hash = digest_bytes(inventory_path.read_bytes())
    game = manifest["game"]
    public_url = "https://raw.githubusercontent.com/RoveHD/reborn-update/main/update.json"
    github_url = "https://api.github.com/repos/RoveHD/reborn-update/contents/update.json?ref=main"
    immutable_root = f"https://github.com/RoveHD/reborn-update/releases/download/v{version}/"
    if (not str(game.get("url", "")).startswith(immutable_root) or
            not re.fullmatch(r"[a-fA-F0-9]{64}", str(game.get("sha256", ""))) or game.get("size", 0) <= 0):
        raise ValueError("Invalid immutable public game artifact")
    channel_path = release_dir / "public-channel-verification.json"
    if not channel_path.is_file():
        raise ValueError("Local package parity is not publication: public-channel-verification.json is required")
    channel = read_json(channel_path)
    if (not isinstance(channel, dict) or channel.get("success") is not True or channel.get("version") != version or
            channel.get("publicManifestUrl") != public_url or channel.get("githubManifestUrl") != github_url or
            str(channel.get("manifestSha256", "")).lower() != manifest_hash or
            str(channel.get("inventorySha256", "")).lower() != inventory_hash or
            str(channel.get("gameSha256", "")).lower() != str(game.get("sha256", "")).lower() or
            not channel.get("verifiedAt")):
        raise ValueError("Public GitHub channel verification is missing, stale, or belongs to another candidate")
    github = channel.get("githubResponse")
    if not isinstance(github, dict) or github.get("encoding") != "base64" or not isinstance(github.get("content"), str):
        raise ValueError("Public channel proof must retain the actual GitHub manifest response")
    try:
        public_manifest_bytes = base64.b64decode(re.sub(r"\s+", "", github["content"]), validate=True)
    except (ValueError, TypeError) as error:
        raise ValueError("Invalid public GitHub manifest response") from error
    if digest_bytes(public_manifest_bytes) != manifest_hash:
        raise ValueError("Actual public GitHub manifest differs from the candidate")

    # V2 has priority in the real UpdateService; every offered base must have a matching public installation proof.
    offered: dict[str, dict[str, Any]] = {}
    for delta in [*manifest.get("gameHotfixesV2", []), *manifest.get("gameHotfixes", [])]:
        baseline = delta.get("fromVersion", "")
        if (not re.fullmatch(r"\d+\.\d+\.\d+", baseline) or delta.get("version") != version or
                not str(delta.get("url", "")).startswith(immutable_root)):
            raise ValueError("Invalid offered public delta")
        if baseline in offered:
            if any(offered[baseline].get(key) != delta.get(key) for key in ("url", "sha256", "size", "files")):
                raise ValueError("Conflicting V2/legacy public delta for " + baseline)
        else:
            offered[baseline] = delta
    if not offered:
        raise ValueError("Publication needs public UpdateService proofs for the offered delta baselines")
    public_deltas = []
    expected_personal = {"mkxp.json", "settings.json", "Save Data/Game_3.rxdata", "patch/Mods/FriendsOwnMod.rb"}
    for baseline, delta in offered.items():
        delta_path = release_dir / ("release-update-delta-public-from-" + baseline + ".json")
        if not delta_path.is_file():
            raise ValueError("Missing public UpdateService installation proof for " + baseline)
        check = read_json(delta_path)
        requested = check.get("requestedUrls", [])
        expected_requests = {public_url, delta["url"]}
        if (check.get("success") is not True or check.get("proofMode") != "public" or
                check.get("realUpdateServiceUsed") is not True or check.get("updateServiceUsedPublicChannel") is not True or
                check.get("version") != version or check.get("previousVersion") != baseline or
                check.get("publicManifestUrl") != public_url or check.get("publicDeltaUrl") != delta["url"] or
                check.get("candidateManifestUrl") != public_url or check.get("candidateDeltaUrl") != delta["url"] or
                str(check.get("candidateManifestSha256", "")).lower() != manifest_hash or
                str(check.get("targetInventorySha256", "")).lower() != inventory_hash or
                str(check.get("archiveSha256", "")).lower() != str(game.get("sha256", "")).lower() or
                check.get("archiveSize") != game.get("size") or
                str(check.get("deltaSha256", "")).lower() != str(delta.get("sha256", "")).lower() or
                check.get("deltaSize") != delta.get("size") or check.get("fullPackageDownloaded") is not False or
                check.get("installedTargetFilesVerified") != len(inventory) or
                check.get("baselineFixtureUnchanged") is not True or check.get("switchesAfterDownload") != 2 or
                not isinstance(check.get("personalSentinelsPreserved"), list) or
                len(check["personalSentinelsPreserved"]) != len(expected_personal) or set(check["personalSentinelsPreserved"]) != expected_personal or
                not isinstance(check.get("previousFilesVerifiedAfterSwitch"), int) or check["previousFilesVerifiedAfterSwitch"] <= 0 or
                not isinstance(requested, list) or set(requested) != expected_requests or
                check.get("deletedPathsVerified") != sum(item.get("delete") is True for item in delta.get("files", [])) or
                check.get("addedPathsVerifiedAfterSwitchBack") != sum(item.get("beforeExists") is False for item in delta.get("files", []))):
            raise ValueError("Public UpdateService delta proof is local-only, incomplete, or stale: " + baseline)
        previous_release = Path(check.get("previousReleaseDirectory", ""))
        previous_inventory_path = previous_release / "game-files.json"
        previous_manifest_path = previous_release / "update.json"
        if (not previous_inventory_path.is_file() or not previous_manifest_path.is_file() or
                digest_bytes(previous_inventory_path.read_bytes()) != str(check.get("previousInventorySha256", "")).lower()):
            raise ValueError("Public delta rollback baseline inventory is missing or stale: " + baseline)
        previous_inventory = read_json(previous_inventory_path)
        previous_manifest = read_json(previous_manifest_path)
        if (not isinstance(previous_inventory, dict) or len(previous_inventory) != check["previousFilesVerifiedAfterSwitch"] or
                previous_manifest.get("game", {}).get("version") != baseline):
            raise ValueError("Public delta rollback did not verify every previous-version file: " + baseline)
        artifact = release_dir / delta["url"].rsplit("/", 1)[-1]
        if (not artifact.is_file() or artifact.stat().st_size != delta.get("size") or
                digest_bytes(artifact.read_bytes()) != str(delta.get("sha256", "")).lower()):
            raise ValueError("Public delta artifact no longer matches its proof: " + baseline)
        public_deltas.append({"status": "passed", "previousVersion": baseline, "version": version,
                              "publicDeltaUrl": delta["url"], "deltaSha256": delta["sha256"],
                              "path": str(delta_path), "sha256": digest_bytes(delta_path.read_bytes()),
                              "manifestSha256": manifest_hash, "inventorySha256": inventory_hash})
    sys.path.insert(0, str(project_root / "tools"))
    import verify_release_parity
    proof = verify_release_parity.verify(release_dir, game_root, project_root=project_root)
    archive_only = verify_release_parity.verify_archive_only(release_dir, project_root=project_root)
    if proof.get("success") is not True or archive_only.get("success") is not True:
        raise ValueError("Local source/archive parity failed")
    return {"status": "published", "version": version,
            "verifiedAt": datetime.now(timezone.utc).isoformat(), "sourceParity": proof,
            "archiveOnly": archive_only,
            "manifestSha256": manifest_hash, "inventorySha256": inventory_hash,
            "sourceIntlSha256": inventory.get("intl.txt", {}).get("sha256"),
            "publicChannelVerification": {"status": "passed", "publicManifestUrl": public_url,
                                          "githubManifestUrl": github_url, "path": str(channel_path),
                                          "sha256": digest_bytes(channel_path.read_bytes())},
            "publicDeltaProofs": public_deltas,
            "requiredDeltaBaselines": sorted(offered),
            "archiveProofSha256": digest_bytes(archive_proof_path.read_bytes())}


def publication_verified(proof: Any, version: str) -> bool:
    """Local-only evidence from older ledger versions is never a publication proof."""
    if not isinstance(proof, dict) or proof.get("status") != "published" or proof.get("version") != version:
        return False
    channel = proof.get("publicChannelVerification", {})
    deltas = proof.get("publicDeltaProofs", [])
    required = proof.get("requiredDeltaBaselines", [])
    return (all(re.fullmatch(r"[a-fA-F0-9]{64}", str(proof.get(key, ""))) for key in
                ("manifestSha256", "inventorySha256", "archiveProofSha256", "sourceIntlSha256")) and
            isinstance(required, list) and bool(required) and len(set(required)) == len(required) and
            isinstance(channel, dict) and channel.get("status") == "passed" and
            re.fullmatch(r"[a-fA-F0-9]{64}", str(channel.get("sha256", ""))) is not None and
            isinstance(deltas, list) and bool(deltas) and all(isinstance(delta, dict) and delta.get("status") == "passed" and
            delta.get("version") == version and delta.get("manifestSha256") == proof.get("manifestSha256") and
            delta.get("inventorySha256") == proof.get("inventorySha256") for delta in deltas) and
            len(deltas) == len(required) and {delta.get("previousVersion") for delta in deltas} == set(required))


def apply_publication(ledger: dict[str, Any], changes: dict[str, Any], proof: dict[str, Any]) -> None:
    if not publication_verified(proof, changes.get("releaseVersion")):
        raise ValueError("Unverified release proof cannot mark translations published")
    changes["releaseStatus"] = "published"
    changes["publicationProof"] = proof
    ledger.setdefault("proofs", {})["release"] = proof
    by_id = {row["id"]: row for row in ledger["entries"]}
    for change in changes["changes"]:
        change["releaseStatus"] = "published"
        entry = by_id[change["entryId"]]
        entry["evidence"]["release"] = {"status": "published", "version": proof["version"],
                                           "manifestSha256": proof["manifestSha256"],
                                           "archiveProofSha256": proof["archiveProofSha256"]}


def summarize(ledger: dict[str, Any], changes: dict[str, Any]) -> dict[str, Any]:
    categories = Counter(change["category"] for change in changes["changes"])
    return {"totalEntries": len(ledger["entries"]), "sections": len(ledger["sections"]),
            "maps": sum(1 for section in ledger["sections"] if section["kind"] == "map"),
            "statusCounts": ledger["statusCounts"], "changeCount": len(changes["changes"]),
            "changeCategories": dict(categories),
            "compile": ledger["proofs"]["compile"],
            "structure": ledger["proofs"]["structure"],
            "runtimeReport": ledger["proofs"]["runtimeReport"],
            "coverageFlags": ledger["coverageFlags"]}


def markdown_reports(ledger: dict[str, Any], changes: dict[str, Any], output_root: Path) -> tuple[str, str]:
    stats = summarize(ledger, changes)
    version = changes["releaseVersion"]
    lines = ["# Pokémon Reborn AllGen 0.9.9 – Übersetzungsübersicht", "",
             "## Inhaltsverzeichnis", "", "- [Umfang und Status](#umfang-und-status)",
             "- [Nachweise](#nachweise)", "- [Prüfvorschläge](#prüfvorschläge)",
             f"- [Änderungen in {version}](#änderungen-in-{version.replace('.', '')})", "- [Kategorien](#kategorien)",
             "- [Vollständige Einzelansicht](index.html)", "",
             "## Umfang und Status", "",
             f"- Übersetzungseinträge: **{stats['totalEntries']:,}** in **{stats['sections']}** Abschnitten; davon **{stats['maps']}** Maps.".replace(",", " "),
             f"- Sprachlich geprüft und korrekt: **{ledger['statusCounts'].get('reviewed-correct', 0)}**.",
             f"- Sprachlich geprüft und korrigiert: **{ledger['statusCounts'].get('reviewed-corrected', 0)}**.",
             f"- Ungeprüft: **{ledger['statusCounts'].get('unreviewed', 0)}**.",
             f"- Erneut zu prüfen: **{ledger['statusCounts'].get('recheck-required', 0)}**.",
             f"- Kontextprüfung nötig: **{ledger['statusCounts'].get('context-review-required', 0)}**.",
             f"- Fehler festgestellt und offen: **{ledger['statusCounts'].get('error-found', 0)}**.",
             "- Jeder Übersetzungseintrag hat einen eigenen Status; ein Abschnitt ist erst vollständig geprüft, wenn alle Einträge darin geprüft sind.",
             "- Bereits deutsche UI-Texte bleiben erhalten; englische Designlabels wie FIGHT und RUN werden nicht übersetzt.",
             "", "## Nachweise", "",
             f"- Struktureller Parserbericht: **{ledger['proofs']['structure']['status']}**; {ledger['proofs']['structure'].get('pairs', 'keine')} Paare.",
             f"- Kompilierung: **{ledger['proofs']['compile']['status']}**; Compiler-Einträge {ledger['proofs']['compile'].get('compiledEntries', 'nicht nachgewiesen')}.",
             f"- Runtime-Bericht: **{ledger['proofs']['runtimeReport']['status']}**; {ledger['proofs']['runtimeReport'].get('translationLookups', 0)} Übersetzungs-Lookups, {ledger['proofs']['runtimeReport'].get('itemBagRenderings', 0)} Item-Taschen-Layouts, {ledger['proofs']['runtimeReport'].get('itemBagOverflow', 'unbekannt')} Überläufe.",
             f"- Lookup-Prüfung ist keine manuelle Sichtprüfung einzelner Dialoge. Für die {len(changes['changes'])} geprüften Korrekturen ist `visualDisplay` daher nicht manuell bestätigt.",
             f"- Entity-Inventar: **{ledger['entityCatalog']['entityCount']:,}** Entitäten und **{ledger['entityCatalog']['runtimeTextCount']:,}** Runtime-Beobachtungen; Abdeckung `{ledger['entityCatalog']['coverageStatus']}`. Unbekannte Ressourcen und ungeprüfte Texte bleiben offen.".replace(",", " "),
             "", "## Prüfvorschläge", "",
             "Die Agentenreviews sind Vorschläge und bleiben von freigegebenen Änderungen getrennt. Sie haben keinen Status `reviewed-corrected`, zählen nicht als angewendet und sind nicht veröffentlicht.", ""]
    for batch in ledger["reviewBatches"]:
        candidates = f"{batch['candidateCorrections']} mögliche Korrekturen" if batch["candidateCorrections"] is not None else "Korrekturzahl offen"
        lines.append(f"- `{batch['id']}`: {batch['entries']} Einträge, {candidates}; **Root-Review offen**, angewendet 0, veröffentlicht 0.")
    lines.extend(["", f"## Änderungen in {version}", "",
                  f"Releasezuordnung: **{changes['releaseStatus']}**. {len(changes['changes'])} geprüfte Änderungen sind diesem Batch zugeordnet.",
                  "", "| Abschnitt / Zeile | Englisch | Deutsch vorher | Deutsch nachher | Fehlerkategorie | Grund | Status |",
                  "|---|---|---|---|---|---|---|"])
    for change in changes["changes"]:
        loc = f"{change['section']}:{change['sourceLines']['de']}"
        cells = [loc, change["en"], change["before"], change["after"], change["category"], change["reason"], change["releaseStatus"]]
        lines.append("| " + " | ".join(str(cell).replace("|", "\\|").replace("\n", "<br>") for cell in cells) + " |")
    lines.extend(["", "## Kategorien", ""])
    for category, count in sorted(stats["changeCategories"].items()):
        lines.append(f"- {category}: **{count}**.")
    lines.extend(["", "## Technischer Stand", "",
                  f"- Quelle: `{ledger['source']['path']}`, SHA-256 `{ledger['source']['sha256']}`.",
                  f"- Schema: Übersichtsledger v{ledger['schemaVersion']}; Änderungsliste v{changes['schemaVersion']}.",
                  "- Funktionale UI-Beschriftungen bleiben gemäß Projektregel Englisch.",
                  "- Keine Veröffentlichung oder GitHub-Aktion wurde durch dieses Berichtswerkzeug ausgeführt.", ""])
    changelog = "\n".join(lines)
    release_lines = [f"# Übersetzungsänderungen – Spielhotfix {version}", "",
                     "## Status", "", f"**{changes['releaseStatus']}** – {len(changes['changes'])} geprüfte Textkorrekturen sind diesem Releasebatch zugeordnet.",
                     "", "## Inhaltsverzeichnis", "", "- [Einzelkorrekturen](#einzelkorrekturen)", "- [Technische Nachweise](#technische-nachweise)", "",
                     "## Einzelkorrekturen", "", "| Abschnitt / Zeile | Englisch | Vorher | Nachher | Kategorie | Grund |",
                     "|---|---|---|---|---|---|"]
    for change in changes["changes"]:
        cells = [f"{change['section']}:{change['sourceLines']['de']}", change["en"], change["before"], change["after"], change["category"], change["reason"]]
        release_lines.append("| " + " | ".join(str(cell).replace("|", "\\|").replace("\n", "<br>") for cell in cells) + " |")
    release_lines.extend(["", "## Technische Nachweise", "",
                          f"- Strukturbericht: `{ledger['proofs']['structure']['status']}`.",
                          f"- Gebündelter Runtime-Compiler: `{ledger['proofs']['compile']['status']}`.",
                          f"- Runtime-Lookups: {ledger['proofs']['runtimeReport'].get('translationLookups', 0)}; Item-Bag-Layouts: {ledger['proofs']['runtimeReport'].get('itemBagRenderings', 0)}; Overflow: {ledger['proofs']['runtimeReport'].get('itemBagOverflow', 'unbekannt')}.",
                          "- Diese Runtime-Prüfung bestätigt Lookups und Layoutmessungen, keine manuelle Sichtprüfung der Texte.",
                          f"- Veröffentlichung: {changes['releaseStatus']}; Bestätigung erfordert lokale Quell-/ZIP-Parität, öffentliche Launcher-Installationen aller Delta-Ausgangsversionen und den tatsächlichen GitHub-Kanalbeleg.", ""])
    return changelog, "\n".join(release_lines)


def aggregate_entity_reviews(entries: list[dict[str, Any]], catalog: dict[str, Any]) -> tuple[dict[str, dict[str, Any]], dict[str, dict[str, Any]]]:
    by_id = {row["id"]: row for row in entries}
    observations: dict[str, list[dict[str, Any]]] = {}
    runtime = catalog.get("runtimeTexts", {}).get("observations", [])
    for observation in runtime:
        observations.setdefault(observation.get("entityId", ""), []).append(observation)
    entity_stats: dict[str, dict[str, Any]] = {}
    categories: dict[str, dict[str, Any]] = {}
    priorities = ("error-found", "context-review-required", "recheck-required", "unreviewed")
    for entity in catalog.get("entities", []):
        ids = list(dict.fromkeys([*(entity.get("entryIds") or []), *(entity.get("intlRefs") or [])]))
        linked = [by_id[entry_id] for entry_id in ids if entry_id in by_id]
        reviewed = sum(row["status"] in {"reviewed-correct", "reviewed-corrected"} for row in linked)
        entity_observations = observations.get(entity.get("entityId", ""), [])
        unmapped = any(not item.get("intlRefs") or any(ref not in by_id for ref in item["intlRefs"])
                       for item in entity_observations)
        unknown_coverage = bool(entity.get("coverageFlags")) or unmapped
        status = next((candidate for candidate in priorities if any(row["status"] == candidate for row in linked)), None)
        if status is None:
            if linked and reviewed == len(linked) and not unknown_coverage:
                status = "reviewed-corrected" if any(row["status"] == "reviewed-corrected" for row in linked) else "reviewed-correct"
            else:
                status = "unreviewed"
        entity_stats[entity["entityId"]] = {"status": status, "reviewed": reviewed, "total": len(linked),
                                             "percent": round(reviewed * 100 / len(linked)) if linked else None,
                                             "unknownCoverage": unknown_coverage, "unmapped": unmapped,
                                             "runtimeCount": len(entity_observations)}
        category = entity.get("category", "unknown")
        aggregate = categories.setdefault(category, {"ids": set(), "entityCount": 0, "unknownEntities": 0, "runtimeCount": 0})
        aggregate["entityCount"] += 1
        aggregate["unknownEntities"] += int(unknown_coverage)
        aggregate["runtimeCount"] += len(entity_observations)
        aggregate["ids"].update(row["id"] for row in linked)
    for aggregate in categories.values():
        rows = [by_id[entry_id] for entry_id in aggregate.pop("ids")]
        aggregate["total"] = len(rows)
        aggregate["reviewed"] = sum(row["status"] in {"reviewed-correct", "reviewed-corrected"} for row in rows)
        aggregate["percent"] = round(aggregate["reviewed"] * 100 / aggregate["total"]) if aggregate["total"] else None
    return entity_stats, categories


def generate_html(ledger: dict[str, Any], changes: dict[str, Any], output_path: Path) -> None:
    entities = ledger.get("_entityCatalogData", {})
    entity_reviews, category_stats = aggregate_entity_reviews(ledger["entries"], entities)
    compact_entries = [[row["id"], row["section"], row["sectionKind"], row["en"], row["de"], row["status"],
                        row.get("errorCategory"), row.get("change"), row.get("agentProposal"),
                        row.get("lastReviewedAt"), row.get("sourceLines")] for row in ledger["entries"]]
    payload = json.dumps({"entries": compact_entries, "changes": changes["changes"], "releaseVersion": changes["releaseVersion"],
                          "sourcePath": ledger.get("source", {}).get("path", "intl.txt"),
                          "entities": entities, "entityReviews": entity_reviews,
                          "categoryStats": category_stats}, ensure_ascii=False, separators=(",", ":"))
    payload = (payload.replace("&", "\\u0026").replace("<", "\\u003c").replace(">", "\\u003e")
               .replace("\u2028", "\\u2028").replace("\u2029", "\\u2029"))
    page = r'''<!doctype html>
<html lang="de"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Pokémon Reborn – Übersetzungsprüfung</title>
<style>
:root{color-scheme:light dark;font:16px/1.5 system-ui,sans-serif;--bg:#101722;--panel:#182433;--line:#334357;--ink:#edf3f7;--muted:#a9bac9;--accent:#75c6ae}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink)}header{padding:1.2rem clamp(1rem,4vw,3rem);border-bottom:1px solid var(--line);background:#152131;position:sticky;top:0;z-index:2}h1{font-size:1.35rem;margin:0 0 .25rem}header p{margin:0;color:var(--muted)}main{max-width:1500px;margin:auto;padding:1rem clamp(.75rem,3vw,2.5rem)}.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:.6rem;margin:1rem 0}.stat,.filters,.result,.entity{background:var(--panel);border:1px solid var(--line);border-radius:.6rem;padding:.7rem}.stat strong{display:block;font-size:1.25rem}.stat span,.muted{color:var(--muted)}.filters{display:grid;grid-template-columns:minmax(220px,2fr) repeat(4,minmax(145px,1fr));gap:.6rem;position:sticky;top:82px;z-index:1}.filters label{display:grid;gap:.2rem;color:var(--muted);font-size:.85rem}input,select,button{font:inherit;color:var(--ink);background:#101a27;border:1px solid var(--line);border-radius:.35rem;padding:.5rem}button{cursor:pointer}button:hover{border-color:var(--accent)}#results{display:grid;gap:.55rem;margin-top:1rem}.result h2{font-size:1rem;margin:.1rem 0 .4rem}.result p{margin:.2rem 0;white-space:pre-wrap;overflow-wrap:anywhere}.badge{display:inline-block;border:1px solid var(--line);border-radius:999px;padding:.08rem .5rem;font-size:.78rem;color:var(--accent);margin:.1rem .3rem .1rem 0}.change{border-left:3px solid var(--accent);padding:.4rem .6rem;margin-top:.4rem}.pager{display:flex;align-items:center;justify-content:center;gap:.7rem;padding:1rem}.coverage{border-left:3px solid #d7a65a;padding:.5rem .8rem;color:var(--muted)}.tabs{display:flex;gap:.4rem;margin:.7rem 0}.tabs button[aria-pressed=true]{border-color:var(--accent)}@media(max-width:800px){header{position:static}.filters{position:static;grid-template-columns:1fr 1fr}.filters label:first-child{grid-column:1/-1}}
main>*,.stats>*,.filters>*,#results>*,.tabs>*,.pager>*{min-width:0}
[hidden]{display:none!important}
header{position:static}.filters{top:0}
input,select{width:100%;max-width:100%;min-width:0}
.filters{grid-template-columns:minmax(0,2fr) repeat(4,minmax(0,1fr))}
.result h2,.entity summary,.entity p,.stat,.badge{overflow-wrap:anywhere}
@media(max-width:800px){.filters{grid-template-columns:repeat(2,minmax(0,1fr))}}
</style>
<header><h1>Pokémon Reborn AllGen 0.9.9 · Übersetzungsprüfung <span id="releaseVersion"></span></h1><p>Lokale Datenansicht · Vorschlag, Korrektur und Veröffentlichung bleiben getrennte Zustände.</p></header>
<main><div id="coverage" class="coverage"></div><div id="stats" class="stats"></div><div id="entityCategoryStats" class="stats" hidden></div>
<div class="tabs"><button id="textsTab" aria-pressed="true">Übersetzungstexte</button><button id="entitiesTab" aria-pressed="false">Runtime-Inhalte</button></div>
<section class="filters"><label>Suche: ID, Englisch, Deutsch oder Runtime-Name<input id="query" type="search" placeholder="z. B. Map285, Tauros, Arena"></label>
<label>Bereich<select id="section"><option value="">Alle Bereiche</option></select></label>
<label>Status<select id="status"><option value="">Alle Status</option></select></label>
<label>Fehlerkategorie<select id="category"><option value="">Alle Kategorien</option></select></label>
<label>Änderung / Release<select id="release"><option value="">Alle</option><option value="changed">Mit Korrektur</option><option value="pending">Release ausstehend</option><option value="published">Veröffentlicht</option></select></label></section>
<p id="count" class="muted" aria-live="polite"></p><div id="results"></div><div class="pager"><button id="prev">Zurück</button><span id="page"></span><button id="next">Weiter</button></div></main>
<script>
const DATA = __DATA__;
document.getElementById("releaseVersion").textContent=DATA.releaseVersion;
const LABELS={"reviewed-correct":"Perfekt / Geprüft","reviewed-corrected":"Korrigiert / Geprüft","unreviewed":"Ungeprüft","context-review-required":"Kontextprüfung nötig","recheck-required":"Erneute Prüfung nötig","error-found":"Fehler festgestellt"};
const $=id=>document.getElementById(id), pageSize=100; let pageNo=0, mode="texts";
const entries=DATA.entries, changes=new Map(DATA.changes.map(c=>[c.entryId,c]));
const entityCatalog=DATA.entities||{}, entityRows=Array.isArray(entityCatalog.entities)?entityCatalog.entities:[];
const runtimeData=entityCatalog.runtimeTexts||{}, runtimeRows=Array.isArray(runtimeData)?runtimeData:(runtimeData.entries||runtimeData.texts||[]), runtimeByEntity=new Map();
for(const t of runtimeRows){if(t.entityId){if(!runtimeByEntity.has(t.entityId))runtimeByEntity.set(t.entityId,[]);runtimeByEntity.get(t.entityId).push(t)}}
const entityRefs=new Map(), entityEntryRefs=new Map(), entryById=new Map(entries.map(e=>[e[0],e]));
for(const e of entityRows){const ids=[...new Set([...(e.entryIds||[]),...(e.intlRefs||[])])];entityEntryRefs.set(e.entityId,ids);for(const id of ids){if(!entityRefs.has(id))entityRefs.set(id,[]);entityRefs.get(id).push(e)}}
const entityText=e=>[e.entityId,e.category,e.enName,e.deName,e.nationalDex,e.form,e.sourceKey,e.sourceFile,...(entityEntryRefs.get(e.entityId)||[]),...(runtimeByEntity.get(e.entityId)||[]).flatMap(t=>[t.en??t.sourceText,t.de??t.runtimeDeutsch,t.textType])].filter(v=>v!==null&&v!==undefined).join(" ").toLocaleLowerCase("de-DE");
const entitySearchIndex=new Map(entityRows.map(e=>[e.entityId,entityText(e)]));
let indexedQuery="", matchingEntityIds=new Set();
function updateMatchingEntities(query){if(query===indexedQuery)return;indexedQuery=query;matchingEntityIds=new Set();if(query)for(const [id,haystack] of entitySearchIndex)if(haystack.includes(query))matchingEntityIds.add(id)}
const entityReviewStats=DATA.entityReviews||{},categoryReviewStats=DATA.categoryStats||{};
function renderCategoryStats(){const target=$("entityCategoryStats");target.replaceChildren();for(const [category,c] of Object.entries(categoryReviewStats).sort((a,b)=>a[0].localeCompare(b[0],"de-DE"))){const box=document.createElement("div");box.className="stat";const strong=document.createElement("strong");strong.textContent=`${c.reviewed.toLocaleString("de-DE")}/${c.total.toLocaleString("de-DE")} · ${c.percent===null?"n/a":c.percent+"%"}`;const span=document.createElement("span");span.textContent=`${category} · ${c.entityCount} Entitäten · ${c.unknownEntities} mit offener Coverage · ${c.runtimeCount.toLocaleString("de-DE")} Runtime-Beobachtungen`;box.append(strong,span);target.append(box)}}
function addOptions(select,values,labels){for(const v of [...new Set(values)].sort((a,b)=>String(a).localeCompare(String(b),"de"))){const o=document.createElement("option");o.value=v;o.textContent=labels&&labels[v]||v;select.append(o)}}
addOptions($("section"),entries.map(e=>e[1])); addOptions($("status"),Object.keys(LABELS),LABELS);
addOptions($("category"),[...entries.map(e=>e[6]).filter(Boolean),...entityRows.map(e=>e.category).filter(Boolean)]);
$("coverage").textContent=entityCatalog.coverageStatus==="complete"?"Runtime-Entity-Katalog: Export vorhanden.":"Katalogabdeckung offen: "+(entityCatalog.coverageFlag||"Entity-Export noch nicht verfügbar.");
function countFor(s){return entries.filter(e=>e[5]===s).length}
for(const [status,label] of Object.entries(LABELS)){const box=document.createElement("div");box.className="stat";const strong=document.createElement("strong");strong.textContent=countFor(status).toLocaleString("de-DE");const span=document.createElement("span");span.textContent=label;box.append(strong,span);$("stats").append(box)}
function makeText(tag,text,cls){const n=document.createElement(tag);n.textContent=text??"";if(cls)n.className=cls;return n}
function matches(e){const q=$("query").value.toLocaleLowerCase("de-DE"),refs=entityRefs.get(e[0])||[],change=changes.get(e[0]),small=`${e[0]} ${e[1]} ${e[3]} ${e[4]} ${e[8]?e[8].proposed+" "+e[8].reason:""} ${change?change.before+" "+change.after+" "+change.reason:""}`.toLocaleLowerCase("de-DE");
if(q&&!small.includes(q)&&!refs.some(entity=>matchingEntityIds.has(entity.entityId)))return false;
if($("section").value&&e[1]!==$("section").value)return false;if($("status").value&&e[5]!==$("status").value)return false;
if($("category").value&&e[6]!==$("category").value)return false;if($("release").value==="changed"&&!changes.has(e[0]))return false;
if($("release").value==="pending"&&(!changes.has(e[0])||changes.get(e[0]).releaseStatus!=="pending"))return false;
if($("release").value==="published"&&(!changes.has(e[0])||changes.get(e[0]).releaseStatus!=="published"))return false;return true}
function renderText(e){const box=document.createElement("article");box.className="result";box.append(makeText("h2",e[0]+" · "+e[1]));box.append(makeText("span",LABELS[e[5]]||e[5],"badge"));if(e[6])box.append(makeText("span",e[6],"badge"));
box.append(makeText("p","EN  "+e[3]));box.append(makeText("p","DE  "+e[4]));box.append(makeText("p","Quelle: "+DATA.sourcePath+" · EN-Zeile "+(e[10]?.en??"?")+" · DE-Zeile "+(e[10]?.de??"?"),"muted"));if(e[9])box.append(makeText("p","Geprüft am: "+e[9]));
for(const entity of entityRefs.get(e[0])||[]){const label=[entity.category,entity.enName,entity.deName,entity.form?"Form "+entity.form:""].filter(Boolean).join(" · ");box.append(makeText("span",label,"badge"))}
if(e[8]){const proposal=document.createElement("div");proposal.className="change";proposal.append(makeText("p","Vorschlag: "+e[8].proposed));proposal.append(makeText("p","Grund: "+e[8].reason+" · Root: "+e[8].rootDecision+" · angewendet: nein · veröffentlicht: nein","muted"));box.append(proposal)}
const change=changes.get(e[0]);if(change){const c=document.createElement("div");c.className="change";c.append(makeText("p","Vorher: "+change.before));c.append(makeText("p","Nachher: "+change.after));c.append(makeText("p","Grund: "+change.reason+" · "+change.category+" · "+change.releaseVersion+": "+change.releaseStatus,"muted"));box.append(c)}return box}
function entityRowsFor(e){return(entityEntryRefs.get(e.entityId)||[]).map(id=>entryById.get(id)).filter(Boolean)}
function entityPassesFilters(e){const linked=entityRowsFor(e),status=$("status").value,release=$("release").value,aggregate=entityReviewStats[e.entityId];if($("category").value&&e.category!==$("category").value)return false;if(status&&aggregate?.status!==status)return false;if(release==="changed"&&!linked.some(row=>changes.has(row[0])))return false;if(release==="pending"&&!linked.some(row=>changes.get(row[0])?.releaseStatus==="pending"))return false;if(release==="published"&&!linked.some(row=>changes.get(row[0])?.releaseStatus==="published"))return false;return true}
function renderEntity(e){const box=document.createElement("article");box.className="entity";const a=entityReviewStats[e.entityId],percent=a?.percent===null||a?.percent===undefined?"n/a":a.percent+"%",title=[e.category,e.enName||e.entityId,e.deName].filter(Boolean).join(" · "),details=document.createElement("details"),summary=document.createElement("summary");summary.textContent=`${title} · ${LABELS[a?.status]||a?.status||"Ungeprüft"} · ${a?.reviewed||0}/${a?.total||0} Texte (${percent}) · ${a?.runtimeCount||0} Runtime-Beobachtungen${a?.unknownCoverage?" · Coverage offen":""}`;details.append(summary);details.addEventListener("toggle",()=>{if(!details.open||details.dataset.loaded)return;const body=document.createElement("div"),lines=[["ID",e.entityId],["Quelle",e.sourceFile||"unbekannt"],["Source Key",e.sourceKey||"unbekannt"],["Coverage",e.coverageStatus||"unbekannt"],["Coverage-Flags",(e.coverageFlags||[]).join(", ")||"keine"],["Exportdatum",entityCatalog.source?.exportedAt||"unbekannt"],["Übersetzungsstatus",LABELS[a?.status]||a?.status||"unreviewed"],["Prüfung",`${a?.reviewed||0}/${a?.total||0} verknüpfte Texte · ${percent}`],["Runtime-Beobachtungen",a?.runtimeCount||0]];if(e.nationalDex!==null&&e.nationalDex!==undefined)lines.push(["Nationaldex",e.nationalDex]);if(e.form)lines.push(["Form",e.form]);for(const [label,value] of lines)body.append(makeText("p",label+": "+value,"muted"));for(const id of entityEntryRefs.get(e.entityId)||[]){const row=entryById.get(id);if(row)body.append(makeText("p",id+" · "+(LABELS[row[5]]||row[5])+" · EN: "+row[3]+" · DE: "+row[4]))}for(const t of runtimeByEntity.get(e.entityId)||[]){body.append(makeText("p",(t.textType||"Text")+" · EN: "+(t.en??"nicht eindeutig erfasst")));if(t.sourceText!==null&&t.sourceText!==undefined)body.append(makeText("p","Quelltext: "+t.sourceText));body.append(makeText("p","DE: "+(t.de??t.runtimeDeutsch??"nicht verfügbar")));for(const id of new Set(t.intlRefs||[]))body.append(makeText("p","Textverweis: "+id,"muted"))}details.append(body);details.dataset.loaded="true"});box.append(details);return box}
function render(){const entityMode=mode==="entities",query=$("query").value.toLocaleLowerCase("de-DE");$("section").parentElement.hidden=entityMode;$("entityCategoryStats").hidden=!entityMode;updateMatchingEntities(query);if(entityMode)renderCategoryStats();const source=mode==="texts"?entries.filter(matches):entityRows.filter(e=>(!query||matchingEntityIds.has(e.entityId))&&entityPassesFilters(e));
const filtered=mode==="entities"?source:source;const pages=Math.max(1,Math.ceil(filtered.length/pageSize));pageNo=Math.min(pageNo,pages-1);$("count").textContent=`${filtered.length.toLocaleString("de-DE")} Treffer · Seite ${pageNo+1} von ${pages}`;$("page").textContent=`${pageNo+1} / ${pages}`;$("prev").disabled=pageNo===0;$("next").disabled=pageNo>=pages-1;const result=$("results");result.replaceChildren();for(const e of filtered.slice(pageNo*pageSize,(pageNo+1)*pageSize))result.append(mode==="texts"?renderText(e):renderEntity(e))}
for(const id of ["query","section","status","category","release"]){$(id).addEventListener(id==="query"?"input":"change",()=>{pageNo=0;render()})}
$("prev").onclick=()=>{pageNo--;render()};$("next").onclick=()=>{pageNo++;render()};
$("textsTab").onclick=()=>{mode="texts";$("textsTab").setAttribute("aria-pressed","true");$("entitiesTab").setAttribute("aria-pressed","false");render()};
$("entitiesTab").onclick=()=>{mode="entities";$("entitiesTab").setAttribute("aria-pressed","true");$("textsTab").setAttribute("aria-pressed","false");render()};render();
</script></html>'''
    page = page.replace("__DATA__", payload)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(page, encoding="utf-8")


def write_outputs(ledger: dict[str, Any], changes: dict[str, Any], review_root: Path) -> None:
    review_root.mkdir(parents=True, exist_ok=True)
    entity_data = ledger.get("_entityCatalogData", ledger.get("entityCatalog", {}))
    (review_root / ENTITY_CATALOG_FILE).write_text(json.dumps(entity_data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    (review_root / CHANGES_FILE).write_text(json.dumps(changes, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    changelog, release_report = markdown_reports(ledger, changes, review_root)
    (review_root / CHANGELOG_FILE).write_text(changelog, encoding="utf-8")
    release_path = review_root / RELEASES_DIR / (changes["releaseVersion"] + ".md")
    release_path.parent.mkdir(parents=True, exist_ok=True)
    release_path.write_text(release_report, encoding="utf-8")
    generate_html(ledger, changes, review_root / HTML_FILE)
    ledger.pop("_entityCatalogData", None)
    (review_root / STATUS_FILE).write_text(json.dumps(ledger, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", nargs="?", choices=("sync", "publish", "html"), default="sync")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--game-root", type=Path, default=DEFAULT_SOURCE.parent)
    parser.add_argument("--review-root", type=Path, default=DEFAULT_REVIEW_ROOT)
    parser.add_argument("--project-root", type=Path, default=DEFAULT_PROOF_ROOT)
    parser.add_argument("--changes-source", type=Path, default=CHANGE_SOURCE)
    parser.add_argument("--compile-proof", type=Path, default=COMPILE_PROOF)
    parser.add_argument("--structure-proof", type=Path, default=STRUCTURE_PROOF)
    parser.add_argument("--runtime-proof", type=Path, default=RUNTIME_PROOF)
    parser.add_argument("--entity-catalog", type=Path)
    parser.add_argument("--runtime-texts", type=Path, default=RUNTIME_TEXTS)
    parser.add_argument("--coverage-report", type=Path, default=COVERAGE_REPORT)
    parser.add_argument("--proposals-source", type=Path, default=MAPS_10_19_PROPOSALS,
                        help="Proposal JSON or manifest with proposalFiles[] (default: existing Maps 10-19 batch)")
    parser.add_argument("--review-scopes", type=Path,
                        help="Optional schemaVersion 1 JSON containing explicit review scopes")
    parser.add_argument("--release-dir", type=Path)
    args = parser.parse_args()
    if args.command == "html":
        saved_ledger = read_json(args.review_root / STATUS_FILE)
        saved_changes = read_json(args.review_root / CHANGES_FILE)
        catalog_path = args.review_root / ENTITY_CATALOG_FILE
        if catalog_path.is_file():
            saved_ledger["_entityCatalogData"] = read_json(catalog_path)
        generate_html(saved_ledger, saved_changes, args.review_root / HTML_FILE)
        print(json.dumps({"success": True, "command": "html", "statusFilesUnchanged": True}))
        return
    ledger, changes = sync_ledger(args.source, args.review_root, args.game_root,
                                  args.changes_source, args.compile_proof,
                                  args.structure_proof, args.runtime_proof, args.entity_catalog,
                                  args.proposals_source, args.runtime_texts, args.coverage_report,
                                  review_scopes_path=args.review_scopes)
    if args.command == "publish":
        if not args.release_dir:
            parser.error("publish requires --release-dir with verifiable local artifacts and public installation/channel proofs")
        proof = verify_release_proof(args.release_dir, args.game_root, args.project_root)
        apply_publication(ledger, changes, proof)
    write_outputs(ledger, changes, args.review_root)
    print(json.dumps({"success": True, "command": args.command,
                      "entries": len(ledger["entries"]), "sections": len(ledger["sections"]),
                      "changes": len(changes["changes"]),
                      "releaseStatus": changes["releaseStatus"],
                      "statusCounts": ledger["statusCounts"],
                      "coverageFlags": ledger["coverageFlags"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
