#!/usr/bin/env python3
"""Root/Docs inventory and documentation actuality scanner for HECTON-8."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


ROOT_DOCS = {"AGENTS.md", "MASTER_RELEASE_WORK_PLAN.md", "BUILD_PLAYTEST_ISSUES.md"}
DOC_EXTENSIONS = {".md", ".txt", ".diff"}
ARCHIVE_PARTS = {"DEPRECATED", "_Archive", "Archive", "AgentLogs", "Tasks"}
TRANSIENT_REPORT_SUFFIXES = ("_stdout.txt", "_stderr.txt")
STRICT_ARCHITECTURE_PARAGRAPH_WORDS = 27
STRICT_ARCHITECTURE_SENTENCE_WORDS = 27
STRICT_ARCHITECTURE_LINE_WORDS = 27
STRICT_ARCHITECTURE_FILE_WORDS = 2500
ROOT_BLOAT_ARCHIVE = Path("Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23")
REPORTS_ARCHIVE = Path("Docs/_Archive/Reports_X_012_2026-05-23")
ARCHITECTURE_ARCHIVE = Path("Docs/_Archive/Architecture_X_012_APEX_2026-05-23")
ARCHITECTURE_DIFF_ARCHIVE = Path("Docs/_Archive/Architecture_X_012_APEX_2026-05-24_RESIDUAL_PROSE")


def root() -> Path:
    return Path(__file__).resolve().parents[1]


def decode(path: Path) -> tuple[str, str, bool]:
    data = path.read_bytes()
    has_bom = data.startswith(b"\xef\xbb\xbf")
    for encoding in ("utf-8-sig", "utf-8", "cp1251"):
        try:
            return data.decode(encoding), encoding, has_bom
        except UnicodeDecodeError:
            pass
    return data.decode("utf-8", errors="replace"), "utf-8-replace", has_bom


def try_decode(path: Path) -> tuple[str, str, bool] | None:
    try:
        return decode(path)
    except FileNotFoundError:
        return None


def is_doc(path: Path) -> bool:
    return path.suffix.lower() in DOC_EXTENSIONS and not is_transient_report_output(path)


def is_transient_report_output(path: Path) -> bool:
    return (
        path.name.endswith(TRANSIENT_REPORT_SUFFIXES)
        and path.parent.name == "Reports"
        and path.parent.parent.name == "Docs"
    )


def in_scope(path: Path, repo: Path) -> bool:
    rel = path.relative_to(repo)
    if len(rel.parts) == 1:
        return path.name in ROOT_DOCS
    return rel.parts[0] == "Docs"


def is_active(path: Path, repo: Path) -> bool:
    rel = path.relative_to(repo)
    if len(rel.parts) == 1:
        return path.name in ROOT_DOCS
    if rel.parts[0] != "Docs":
        return False
    return not any(part in ARCHIVE_PARTS for part in rel.parts[1:])


def words(text: str) -> int:
    return len(re.findall(r"\b[\w./:#-]+\b", text, flags=re.UNICODE))


def first_topic(text: str) -> str:
    for line in text.splitlines():
        stripped = line.strip()
        if stripped:
            return stripped[:180]
    return ""


LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)|`([^`\n]+\.md|[^`\n]+\.txt)`")


def references(text: str) -> list[str]:
    refs: list[str] = []
    for match in LINK_RE.finditer(text):
        ref = (match.group(1) or match.group(2) or "").strip()
        if ref and len(refs) < 80:
            refs.append(ref)
    return refs


STALE = {
    "signalBusRegistry256": re.compile(r"SignalBusRegistry[^.\n]*(capacity|LaneCapacity)[^.\n]*`?256`?", re.IGNORECASE),
    "dataMonolithAbsent": re.compile(r"static_data\.h8bin[^.\n]*(is|remains|currently)\s+(still\s+)?(absent|missing)|payload status[^.\n]*absent", re.IGNORECASE),
    "h8dmHeader16": re.compile(r"H8DM header (size )?`?16`?\s*bytes", re.IGNORECASE),
    "saveVersion000AWriter": re.compile(r"SaveBinaryStorage\.CurrentVersion\s*=\s*0x000A|active writer[^.\n]*0x000A", re.IGNORECASE),
}


def stale_flags(text: str) -> dict[str, int]:
    return {key: len(pattern.findall(text)) for key, pattern in STALE.items()}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def inventory(repo: Path) -> list[dict]:
    files = [p for p in repo.rglob("*") if p.is_file() and is_doc(p) and in_scope(p, repo)]
    rows: list[dict] = []
    for path in sorted(files):
        try:
            text, encoding, bom = decode(path)
            stat = path.stat()
            digest = sha256(path)
        except FileNotFoundError:
            continue

        rel = path.relative_to(repo).as_posix()
        rows.append({
            "path": rel,
            "active": is_active(path, repo),
            "bytes": stat.st_size,
            "lastWriteUtc": datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat(),
            "encodingReadAs": encoding,
            "utf8Sig": bom,
            "wordCount": words(text),
            "topic": first_topic(text),
            "references": references(text),
            "staleFlags": stale_flags(text),
            "sha256": digest,
        })
    return rows


def extract_constant(pattern: str, paths: list[Path]) -> str | None:
    rx = re.compile(pattern)
    for path in paths:
        if not path.exists():
            continue
        decoded = try_decode(path)
        if decoded is None:
            continue
        text, _, _ = decoded
        match = rx.search(text)
        if match:
            return match.group(1)
    return None


def source_constants(repo: Path) -> dict:
    save = repo / "Assets/_Project/Scripts/SaveBinaryStorage.cs"
    signal = repo / "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs"
    monolith = repo / "Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs"
    static_blob = repo / "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin"
    return {
        "saveCurrentVersion": extract_constant(r"CurrentVersion\s*=\s*(0x[0-9A-Fa-f]+)", [save]),
        "saveCurrentHeaderSize": extract_constant(r"CurrentHeaderSize\s*=\s*(\d+)", [save]),
        "saveLegacyHeaderSize": extract_constant(r"LegacyHeaderSize\s*=\s*(\d+)", [save]),
        "alignedSectionHeaderVersion": extract_constant(r"AlignedSectionHeaderVersion\s*=\s*(0x[0-9A-Fa-f]+)", [save]),
        "signalBusRegistryLaneCapacity": extract_constant(r"LaneCapacity\s*=\s*(\d+)", [signal]),
        "signalBusDefaultExpectedCapacity": extract_constant(r"DefaultExpectedCapacity\s*=\s*(\d+)", [signal]),
        "signalBusDefaultMaxFrameSignals": extract_constant(r"DefaultMaxFrameSignals\s*=\s*(\d+)", [signal]),
        "dataMonolithHeaderBytes": extract_constant(r"HeaderSizeBytes\s*=\s*(\d+)", [monolith]),
        "dataMonolithDirectoryBytes": extract_constant(r"DirectorySizeBytes\s*=\s*(\d+)", [monolith]),
        "dataMonolithFormatVersion": extract_constant(r"FormatVersion\s*=\s*(\d+)", [monolith]),
        "dataMonolithSchemaHash": extract_constant(r"SchemaHash\s*=\s*(0x[0-9A-Fa-f]+)", [monolith]),
        "dataMonolithPayloadExists": static_blob.exists(),
        "dataMonolithPayloadBytes": static_blob.stat().st_size if static_blob.exists() else 0,
    }


def current_text_checks(rows: list[dict], constants: dict) -> dict:
    active_rows = [row for row in rows if row["active"]]
    stale_files = [
        {"path": row["path"], "staleFlags": row["staleFlags"]}
        for row in active_rows
        if any(row["staleFlags"].values())
    ]
    active_word_count = sum(row["wordCount"] for row in active_rows)
    root_text_docs = sorted(row["path"] for row in rows if "/" not in row["path"])
    reduction = word_reduction(repo=root(), active_word_count=active_word_count)
    architecture = architecture_checks(rows)
    return {
        "activeFileCount": len(active_rows),
        "activeWordCount": active_word_count,
        "inventoryFileCount": len(rows),
        "inventoryWordCount": sum(row["wordCount"] for row in rows),
        "rootTextDocs": root_text_docs,
        "rootTextDocPolicyPass": root_text_docs == sorted(ROOT_DOCS),
        "activeStaleParameterFiles": stale_files,
        "sourceConstants": constants,
        "sourceSyncPass": (
            constants["saveCurrentVersion"] == "0x000B"
            and constants["saveCurrentHeaderSize"] == "56"
            and constants["signalBusRegistryLaneCapacity"] == "512"
            and constants["dataMonolithHeaderBytes"] == "64"
            and constants["dataMonolithPayloadExists"] is True
        ),
        "wordReduction": reduction,
        "architecture": architecture,
        "activeParameterSyncPass": not stale_files,
        "finalPass": (
            root_text_docs == sorted(ROOT_DOCS)
            and not stale_files
            and constants["saveCurrentVersion"] == "0x000B"
            and constants["saveCurrentHeaderSize"] == "56"
            and constants["signalBusRegistryLaneCapacity"] == "512"
            and constants["dataMonolithHeaderBytes"] == "64"
            and constants["dataMonolithPayloadExists"] is True
            and reduction["reductionPass"]
            and architecture["pass"]
        ),
    }


def archived_words(repo: Path, relative_dir: Path) -> tuple[int, int]:
    directory = repo / relative_dir
    if not directory.exists():
        return 0, 0
    word_count = 0
    file_count = 0
    for path in directory.rglob("*"):
        if path.name in {"README.md", "MANIFEST.md"}:
            continue
        if path.is_file() and is_doc(path):
            decoded = try_decode(path)
            if decoded is None:
                continue
            text, _, _ = decoded
            word_count += words(text)
            file_count += 1
    return word_count, file_count


def current_root_anchor_words(repo: Path) -> int:
    total = 0
    for name in ("MASTER_RELEASE_WORK_PLAN.md", "BUILD_PLAYTEST_ISSUES.md"):
        path = repo / name
        if path.exists():
            decoded = try_decode(path)
            if decoded is None:
                continue
            text, _, _ = decoded
            total += words(text)
    return total


def current_active_file_words(repo: Path, relative_path: str) -> int:
    path = repo / relative_path
    if not path.exists():
        return 0
    decoded = try_decode(path)
    if decoded is None:
        return 0
    text, _, _ = decoded
    return words(text)


def word_reduction(repo: Path, active_word_count: int) -> dict:
    archived_root_words, archived_root_files = archived_words(repo, ROOT_BLOAT_ARCHIVE)
    archived_report_words, archived_report_files = archived_words(repo, REPORTS_ARCHIVE)
    archived_architecture_words, archived_architecture_files = archived_words(repo, ARCHITECTURE_ARCHIVE)
    archived_diff_words, archived_diff_files = archived_words(repo, ARCHITECTURE_DIFF_ARCHIVE)
    current_anchor_words = current_root_anchor_words(repo)
    current_binary_ledger_words = current_active_file_words(
        repo,
        "Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md",
    )
    current_actuality_ledger_words = current_active_file_words(
        repo,
        "Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md",
    )
    current_architecture_replacement_words = current_binary_ledger_words + current_actuality_ledger_words
    architecture_retired_words = max(0, archived_architecture_words - current_architecture_replacement_words)
    retired_words = (
        max(0, archived_root_words - current_anchor_words)
        + archived_report_words
        + architecture_retired_words
        + archived_diff_words
    )
    reconstructed_baseline = active_word_count + retired_words
    reduction_ratio = (retired_words / reconstructed_baseline) if reconstructed_baseline else 0.0
    return {
        "method": "current active words plus X_012 archived report/root words minus concise replacement anchor words",
        "activeWordCountAfter": active_word_count,
        "reconstructedActiveWordCountBefore": reconstructed_baseline,
        "retiredActiveWords": retired_words,
        "reductionRatio": reduction_ratio,
        "reductionPercent": reduction_ratio * 100.0,
        "reductionPass": reduction_ratio >= 0.30,
        "archivedRootFiles": archived_root_files,
        "archivedReportFiles": archived_report_files,
        "archivedArchitectureFiles": archived_architecture_files,
        "archivedArchitectureDiffFiles": archived_diff_files,
        "currentRootAnchorWords": current_anchor_words,
        "currentBinaryPayloadLedgerWords": current_binary_ledger_words,
        "currentActualityLedgerWords": current_actuality_ledger_words,
        "currentArchitectureReplacementWords": current_architecture_replacement_words,
        "architectureRetiredWords": architecture_retired_words,
    }


def architecture_checks(rows: list[dict]) -> dict:
    architecture_rows = [
        row for row in rows
        if row["active"] and row["path"].startswith("Docs/ARCHITECTURE/") and row["path"].endswith((".md", ".txt"))
    ]
    marker_names = {
        "staleDocBoundaryRefreshMarkers": "DOC" + "_GLOBAL_DOCS_REFRESH",
        "staleBoundary51Boilerplate": "R" + "5" + "1 Root/Architecture Actuality Boundary",
        "staleDocBoundaryLegacyHeadings": "DOC" + "_GLOBAL R",
        "dataMonolithWasAbsent": "static_data.h8bin` was absent",
    }
    marker_hits = {name: 0 for name in marker_names}
    long_narrative_paragraphs: list[dict] = []
    strict_unstructured_paragraphs: list[dict] = []
    strict_structured_lines: list[dict] = []
    strict_unstructured_sentences: list[dict] = []
    strict_file_word_count: list[dict] = []
    strict_non_contract_text_files: list[dict] = []
    tutorial_markers: list[dict] = []
    tutorial_re = re.compile(
        r"\b(how to|for example|in other words|this means|basically|simply put|you can|should understand)\b",
        re.IGNORECASE,
    )

    repo = root()
    for row in architecture_rows:
        decoded = try_decode(repo / row["path"])
        if decoded is None:
            continue
        text, _, _ = decoded
        text = text.replace("\r\n", "\n").replace("\r", "\n")
        if Path(row["path"]).suffix.lower() not in {".md", ".txt"}:
            strict_non_contract_text_files.append({
                "path": row["path"],
                "suffix": Path(row["path"]).suffix.lower(),
                "wordCount": row["wordCount"],
            })
        if row["wordCount"] > STRICT_ARCHITECTURE_FILE_WORDS:
            strict_file_word_count.append({
                "path": row["path"],
                "wordCount": row["wordCount"],
            })
        for name, marker in marker_names.items():
            marker_hits[name] += text.count(marker)
        in_fence = False
        for line_number, line in enumerate(text.splitlines(), start=1):
            stripped_line = line.strip()
            if stripped_line.startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence:
                continue
            is_structured_line = (
                re.match(r"^([-*+] |\d+\.\s)", stripped_line) is not None
                or (stripped_line.startswith("|") and stripped_line.endswith("|"))
            )
            line_word_count = words(stripped_line)
            if is_structured_line and line_word_count > STRICT_ARCHITECTURE_LINE_WORDS:
                strict_structured_lines.append({
                    "path": row["path"],
                    "line": line_number,
                    "wordCount": line_word_count,
                    "preview": stripped_line[:160],
                })
        for index, paragraph in enumerate(re.split(r"\n\s*\n", text), start=1):
            stripped = paragraph.lstrip()
            if (
                stripped.startswith(("-", "|", "```"))
                or re.match(r"^\d+\.\s", stripped)
                or "\n- " in paragraph
                or re.search(r"\n\d+\.\s", paragraph)
            ):
                continue
            word_count = words(paragraph)
            if word_count > 180:
                long_narrative_paragraphs.append({
                    "path": row["path"],
                    "paragraph": index,
                    "wordCount": word_count,
                    "preview": paragraph.strip().replace("\n", " ")[:160],
                })
            if word_count > STRICT_ARCHITECTURE_PARAGRAPH_WORDS:
                strict_unstructured_paragraphs.append({
                    "path": row["path"],
                    "paragraph": index,
                    "wordCount": word_count,
                    "preview": paragraph.strip().replace("\n", " ")[:160],
                })
            sentence_word_counts = [
                words(sentence)
                for sentence in re.split(r"(?<=[.!?])\s+", paragraph.strip())
                if sentence.strip()
            ]
            max_sentence_words = max(sentence_word_counts) if sentence_word_counts else 0
            if max_sentence_words > STRICT_ARCHITECTURE_SENTENCE_WORDS:
                strict_unstructured_sentences.append({
                    "path": row["path"],
                    "paragraph": index,
                    "maxSentenceWords": max_sentence_words,
                    "preview": paragraph.strip().replace("\n", " ")[:160],
                })
            tutorial_match = tutorial_re.search(paragraph)
            if tutorial_match:
                tutorial_markers.append({
                    "path": row["path"],
                    "paragraph": index,
                    "marker": tutorial_match.group(0),
                    "preview": paragraph.strip().replace("\n", " ")[:160],
                })

    return {
        "activeArchitectureFileCount": len(architecture_rows),
        "activeArchitectureWordCount": sum(row["wordCount"] for row in architecture_rows),
        "markerHits": marker_hits,
        "longNarrativeParagraphsOver180Words": long_narrative_paragraphs[:50],
        "longNarrativeParagraphCount": len(long_narrative_paragraphs),
        "strictUnstructuredParagraphThresholdWords": STRICT_ARCHITECTURE_PARAGRAPH_WORDS,
        "strictUnstructuredParagraphsOverThreshold": strict_unstructured_paragraphs[:50],
        "strictUnstructuredParagraphCount": len(strict_unstructured_paragraphs),
        "strictUnstructuredSentenceThresholdWords": STRICT_ARCHITECTURE_SENTENCE_WORDS,
        "strictUnstructuredSentencesOverThreshold": strict_unstructured_sentences[:50],
        "strictUnstructuredSentenceCount": len(strict_unstructured_sentences),
        "strictStructuredLineThresholdWords": STRICT_ARCHITECTURE_LINE_WORDS,
        "strictStructuredLinesOverThreshold": strict_structured_lines[:50],
        "strictStructuredLineCount": len(strict_structured_lines),
        "strictFileWordThreshold": STRICT_ARCHITECTURE_FILE_WORDS,
        "strictFilesOverWordThreshold": strict_file_word_count[:50],
        "strictFileWordCount": len(strict_file_word_count),
        "strictNonContractTextFiles": strict_non_contract_text_files[:50],
        "strictNonContractTextFileCount": len(strict_non_contract_text_files),
        "tutorialMarkerHits": tutorial_markers[:50],
        "tutorialMarkerHitCount": len(tutorial_markers),
        "pass": (
            all(value == 0 for value in marker_hits.values())
            and not long_narrative_paragraphs
            and not strict_unstructured_paragraphs
            and not strict_unstructured_sentences
            and not strict_structured_lines
            and not strict_file_word_count
            and not strict_non_contract_text_files
            and not tutorial_markers
        ),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", default="Docs/Reports/DOCUMENTATION_CORPUS_INVENTORY_X_012.json")
    parser.add_argument("--report", default="Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json")
    args = parser.parse_args()

    repo = root()
    rows = inventory(repo)
    constants = source_constants(repo)
    report = {
        "schema": "hecton8.documentation_actuality_scan.x012.v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "repoRoot": str(repo),
        "scope": "repository root approved text anchors plus Docs/**/*.md/txt",
        "checks": current_text_checks(rows, constants),
    }

    inventory_path = repo / args.inventory
    report_path = repo / args.report
    inventory_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    inventory_path.write_text(json.dumps(rows, indent=2, ensure_ascii=False) + "\n", encoding="utf-8-sig")
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8-sig")
    print(json.dumps({
        "inventory": str(inventory_path),
        "report": str(report_path),
        "inventoryFileCount": len(rows),
        "activeFileCount": report["checks"]["activeFileCount"],
        "activeStaleParameterFiles": len(report["checks"]["activeStaleParameterFiles"]),
        "sourceSyncPass": report["checks"]["sourceSyncPass"],
        "wordReductionPercent": report["checks"]["wordReduction"]["reductionPercent"],
        "finalPass": report["checks"]["finalPass"],
    }, ensure_ascii=False))
    return 0 if report["checks"]["finalPass"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
