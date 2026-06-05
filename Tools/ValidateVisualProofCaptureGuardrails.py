#!/usr/bin/env python3
"""Validate static guardrails for HECTON-8 visual proof capture tooling."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_PATH = ROOT / "Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs"
RISK_REVIEW_PATH = ROOT / "Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md"
NEXT_ACTION_PATH = ROOT / "Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv"
OWNER_36_PATH = ROOT / "taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md"
OWNER_37_PATH = ROOT / "taskslocal/asset_system_20260605/ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md"
FILE_MAP_PATH = ROOT / "Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv"


@dataclass(frozen=True)
class SourceRisk:
    token: str
    category: str
    line_number: int


SOURCE_RISK_TOKENS = (
    ("EditorSceneManager.SaveScene", "scene_save"),
    ("EditorSceneManager.MarkSceneDirty", "scene_dirty_mark"),
    ("ApplyModifiedPropertiesWithoutUndo", "serialized_object_mutation"),
    ("new Material(", "editor_material_clone"),
    ("CreatePrimitive", "editor_probe_geometry"),
    ("editor_only_unsaved", "diagnostic_unsaved_capture"),
)

REQUIRED_DOC_TERMS = {
    RISK_REVIEW_PATH: (
        "H8VisualProofCapture1912.cs",
        "EditorSceneManager.SaveScene",
        "ApplyModifiedPropertiesWithoutUndo",
        "editor_only_unsaved",
        "SurfaceWaterReadabilityShaderPath",
    ),
    NEXT_ACTION_PATH: (
        "h8_1475_proof_tool_integrity",
        "editor_only_unsaved",
        "missing shader path",
    ),
    OWNER_36_PATH: (
        "H8VisualProofCapture1912",
        "diagnostic/editor-mutating capture paths",
        "canonical h8_1475 proof tooling",
    ),
    OWNER_37_PATH: (
        "H8VisualProofCapture1912",
        "Anti-False-Proof",
        "editor_only_unsaved",
    ),
    FILE_MAP_PATH: (
        "H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md",
        "ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md",
    ),
}


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def find_source_risks(source: str) -> list[SourceRisk]:
    risks: list[SourceRisk] = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        for token, category in SOURCE_RISK_TOKENS:
            if token in line:
                risks.append(SourceRisk(token=token, category=category, line_number=line_number))
    return risks


def validate_required_terms(required_terms: dict[Path, tuple[str, ...]]) -> None:
    for path, terms in required_terms.items():
        text = load_text(path)
        for term in terms:
            if term not in text:
                raise SystemExit(f"FAIL: {display_path(path)} missing guardrail term: {term}")


def validate_guardrails(
    source_path: Path = SOURCE_PATH,
    required_terms: dict[Path, tuple[str, ...]] = REQUIRED_DOC_TERMS,
) -> list[SourceRisk]:
    source = load_text(source_path)
    risks = find_source_risks(source)
    if not risks:
        raise SystemExit("FAIL: source risk scan found no proof-tool risks; validator may be pointed at wrong file")
    validate_required_terms(required_terms)
    return risks


def main() -> None:
    risks = validate_guardrails()
    categories = sorted({risk.category for risk in risks})
    print(
        "VISUAL_PROOF_CAPTURE_GUARDRAILS_OK "
        f"risks={len(risks)} categories={','.join(categories)}"
    )


if __name__ == "__main__":
    main()
