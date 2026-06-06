#!/usr/bin/env python3
"""Classify HECTON-8 terrain probe logs as production proof or rejected evidence.

The default mode is a read-only classifier: it exits 0 and prints whether the
provided probe is accepted or rejected. Use --require-production when a caller
needs a hard gate that fails on diagnostic or unsafe evidence.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
CAPTURE_PNG_PATTERN = re.compile(r"\[H8VisualProofCapture[^\]]*\]\s+Wrote\s+.*\.png\b", re.IGNORECASE)
CAPTURE_METADATA_PATTERN = re.compile(r"\[H8VisualProofCapture[^\]]*\]\s+Wrote\s+.*\.txt\b", re.IGNORECASE)
REQUIRED_EROSION_LINKS = (
    ("heightOutput.in", "height-output-not-eroded", "MapMagic height output is not sourced from hydraulic erosion"),
    ("splat.heightIn", "splat-height-not-eroded", "MapMagic splat height input is not sourced from hydraulic erosion"),
    ("anomaly.heightIn", "anomaly-height-not-eroded", "MapMagic anomaly height input is not sourced from hydraulic erosion"),
    ("splat.sedimentIn", "splat-sediment-not-eroded", "MapMagic splat sediment input is not sourced from hydraulic erosion"),
)
REQUIRED_ENABLED_GENERATORS = (
    (
        "erosion",
        "erosion-enabled-row-missing",
        "erosion-enabled-not-proven",
        "MapMagic hydraulic erosion node enabled row is missing",
        "MapMagic hydraulic erosion node is not proven enabled=True",
    ),
    (
        "anomaly",
        "anomaly-enabled-row-missing",
        "anomaly-enabled-not-proven",
        "MapMagic anomaly node enabled row is missing",
        "MapMagic anomaly node is not proven enabled=True",
    ),
)


@dataclass(frozen=True)
class BlockerRule:
    code: str
    source: str
    pattern: re.Pattern[str]
    description: str


@dataclass(frozen=True)
class ProbeEvidence:
    status: str
    blockers: tuple[str, ...]

    @property
    def is_production_ready(self) -> bool:
        return not self.blockers


LOG_BLOCKERS = (
    BlockerRule(
        code="unity-memory-leaks",
        source="log",
        pattern=re.compile(r'"type"\s*:\s*"MemoryLeaks"|##utp:\{[^\n]*MemoryLeaks', re.IGNORECASE),
        description="Unity MemoryLeaks payload present",
    ),
    BlockerRule(
        code="compile-error",
        source="log",
        pattern=re.compile(r"\berror\s+CS\d+\b"),
        description="Unity compile error present",
    ),
    BlockerRule(
        code="tundra-build-failed",
        source="log",
        pattern=re.compile(r"\bTundra build failed\b", re.IGNORECASE),
        description="Unity Tundra build failed",
    ),
    BlockerRule(
        code="editor-compiler-errors",
        source="log",
        pattern=re.compile(r"\bEditor compiler errors found\b|\bReloading assemblies failed\b", re.IGNORECASE),
        description="Unity editor compiler errors blocked assembly reload",
    ),
    BlockerRule(
        code="compile-input-mutated",
        source="log",
        pattern=re.compile(r"\bModification date of `[^`]+` changed while running `Csc\b", re.IGNORECASE),
        description="source file changed while Csc was running",
    ),
    BlockerRule(
        code="hydraulic-delta-apply-job",
        source="log",
        pattern=re.compile(r"\bHydraulicErosionDeltaApplyJob\b"),
        description="delta-apply job failure or marker present",
    ),
    BlockerRule(
        code="height-delta-budget",
        source="log",
        pattern=re.compile(r"\bHeightDeltaBudget\b"),
        description="HeightDeltaBudget optional-container failure or marker present",
    ),
    BlockerRule(
        code="editor-worker-thread-api",
        source="log",
        pattern=re.compile(r"get_isUpdating can only be called from main thread"),
        description="UnityEditor worker-thread API failure present",
    ),
    BlockerRule(
        code="worker-thread-failed",
        source="log",
        pattern=re.compile(r"\bThread failed\b", re.IGNORECASE),
        description="worker thread failure present",
    ),
    BlockerRule(
        code="invalid-operation",
        source="log",
        pattern=re.compile(r"\bInvalidOperationException\b"),
        description="InvalidOperationException present",
    ),
    BlockerRule(
        code="tempjob-leak",
        source="log",
        pattern=re.compile(
            r"\b(?:TempJob|JobTempAlloc)\b|Native Collection[^\n]*has not been disposed",
            re.IGNORECASE,
        ),
        description="TempJob or native-collection leak marker present",
    ),
)

METADATA_BLOCKERS = (
    BlockerRule(
        code="editor-only-unsaved",
        source="metadata",
        pattern=re.compile(r"captureTruth=.*editor_only_unsaved", re.IGNORECASE),
        description="captureTruth is editor-only unsaved diagnostic state",
    ),
    BlockerRule(
        code="h8-1914-diagnostic",
        source="metadata",
        pattern=re.compile(r"h8_1914", re.IGNORECASE),
        description="h8_1914 diagnostic capture marker present",
    ),
    BlockerRule(
        code="erosion-disabled",
        source="metadata",
        pattern=re.compile(r"^\s*erosion=.*\benabled=False\b", re.IGNORECASE | re.MULTILINE),
        description="MapMagic hydraulic erosion node is disabled",
    ),
    BlockerRule(
        code="anomaly-disabled",
        source="metadata",
        pattern=re.compile(r"^\s*anomaly=.*\benabled=False\b", re.IGNORECASE | re.MULTILINE),
        description="MapMagic anomaly node is disabled",
    ),
    BlockerRule(
        code="anomaly-height-unlinked",
        source="metadata",
        pattern=re.compile(r"^\s*link anomaly\.heightIn=UNLINKED\b", re.IGNORECASE | re.MULTILINE),
        description="MapMagic anomaly height input is unlinked",
    ),
    BlockerRule(
        code="splat-sediment-unlinked",
        source="metadata",
        pattern=re.compile(r"^\s*link splat\.sedimentIn=UNLINKED\b", re.IGNORECASE | re.MULTILINE),
        description="MapMagic splat sediment input is unlinked",
    ),
)


def read_optional_text(path: Path | None) -> str:
    if path is None:
        return ""
    return path.read_text(encoding="utf-8-sig", errors="replace")


def read_evidence_text(path: Path | None, source: str) -> tuple[str, tuple[str, ...]]:
    if path is None:
        return "", ()
    if not path.exists():
        return "", (f"missing-{source}: {source} artifact not found: {rel(path)}",)
    return read_optional_text(path), ()


def find_metadata_line(metadata_text: str, link_name: str) -> str | None:
    prefix = f"link {link_name}="
    for raw_line in metadata_text.splitlines():
        line = raw_line.strip()
        if line.startswith(prefix):
            return line
    return None


def find_generator_line(metadata_text: str, generator_name: str) -> str | None:
    prefix = f"{generator_name}="
    for raw_line in metadata_text.splitlines():
        line = raw_line.strip()
        if line.startswith(prefix):
            return line
    return None


def find_capture_truth_line(metadata_text: str) -> str | None:
    for raw_line in metadata_text.splitlines():
        line = raw_line.strip()
        if line.lower().startswith("capturetruth="):
            return line
    return None


def collect_link_source_blockers(metadata_text: str) -> tuple[str, ...]:
    blockers: list[str] = []
    for link_name, code, description in REQUIRED_EROSION_LINKS:
        line = find_metadata_line(metadata_text, link_name)
        if line is None or "sourceType=" not in line:
            continue
        if "HectonHydraulicErosionMapMagicNode" not in line:
            blockers.append(f"{code}: {description}")
    return tuple(blockers)


def has_completed_capture_outputs(log_text: str) -> bool:
    return CAPTURE_PNG_PATTERN.search(log_text) is not None and CAPTURE_METADATA_PATTERN.search(log_text) is not None


def collect_capture_completion_blockers(log_text: str) -> tuple[str, ...]:
    if "H8VisualProofCapture" not in log_text:
        return ()

    if has_completed_capture_outputs(log_text):
        return ()
    return ("capture-output-missing: H8 visual proof capture did not write both PNG and metadata",)


def collect_required_production_blockers(
    log_text: str,
    metadata_text: str,
    metadata_available: bool,
) -> tuple[str, ...]:
    blockers: list[str] = []
    if log_text and not has_completed_capture_outputs(log_text):
        blockers.append("capture-output-missing: production terrain proof did not write both PNG and metadata")
    if metadata_available:
        capture_truth = find_capture_truth_line(metadata_text)
        if capture_truth is None:
            blockers.append("metadata-capture-truth-missing: production terrain metadata must include captureTruth")
        elif "production" not in capture_truth.lower():
            blockers.append("capture-truth-not-production: production terrain metadata must declare production captureTruth")
    for generator_name, missing_code, not_proven_code, missing_description, not_proven_description in REQUIRED_ENABLED_GENERATORS:
        if not metadata_available:
            continue
        line = find_generator_line(metadata_text, generator_name)
        if line is None:
            blockers.append(f"{missing_code}: {missing_description}")
            continue
        if "enabled=True" not in line:
            if "enabled=False" in line:
                continue
            blockers.append(f"{not_proven_code}: {not_proven_description}")
    for link_name, code, description in REQUIRED_EROSION_LINKS:
        if metadata_available and find_metadata_line(metadata_text, link_name) is None:
            blockers.append(f"{code}: missing metadata row for {description}")
    return tuple(blockers)


def classify(log_text: str, metadata_text: str = "", extra_labels: tuple[str, ...] = ()) -> ProbeEvidence:
    blockers: list[str] = []
    combined_metadata = "\n".join(part for part in (*extra_labels, metadata_text, log_text) if part)

    for rule in LOG_BLOCKERS:
        if rule.pattern.search(log_text):
            blockers.append(f"{rule.code}: {rule.description}")

    for rule in METADATA_BLOCKERS:
        if rule.pattern.search(combined_metadata):
            blockers.append(f"{rule.code}: {rule.description}")

    blockers.extend(collect_capture_completion_blockers(log_text))
    blockers.extend(collect_link_source_blockers(combined_metadata))

    status = "TERRAIN_PROBE_EVIDENCE_ACCEPTED" if not blockers else "TERRAIN_PROBE_EVIDENCE_REJECTED"
    return ProbeEvidence(status=status, blockers=tuple(blockers))


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
    except ValueError:
        return str(path)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", required=True, type=Path, help="Unity batchmode log to classify.")
    parser.add_argument("--metadata", type=Path, help="Optional screenshot/probe metadata text file.")
    parser.add_argument(
        "--require-production",
        action="store_true",
        help="Exit non-zero when the evidence is rejected.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    log_text, log_missing = read_evidence_text(args.log, "log")
    metadata_text, metadata_missing = read_evidence_text(args.metadata, "metadata")
    labels = (rel(args.log), rel(args.metadata)) if args.metadata else (rel(args.log),)
    evidence = classify(log_text, metadata_text, labels)
    required_metadata_missing = (
        "missing-metadata: metadata artifact is required for production terrain proof",
    ) if args.require_production and args.metadata is None else ()
    required_production_blockers = (
        collect_required_production_blockers(
            log_text,
            metadata_text,
            args.metadata is not None and not metadata_missing,
        )
        if args.require_production
        else ()
    )
    existing_codes = {blocker.split(":", 1)[0] for blocker in evidence.blockers}
    required_production_blockers = tuple(
        blocker for blocker in required_production_blockers if blocker.split(":", 1)[0] not in existing_codes
    )
    missing_blockers = (*log_missing, *metadata_missing, *required_metadata_missing, *required_production_blockers)
    if missing_blockers:
        evidence = ProbeEvidence(
            status="TERRAIN_PROBE_EVIDENCE_REJECTED",
            blockers=(*missing_blockers, *evidence.blockers),
        )

    print(f"{evidence.status} blockers={len(evidence.blockers)}")
    for blocker in evidence.blockers:
        print(f"- {blocker}")

    if args.require_production and not evidence.is_production_ready:
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
