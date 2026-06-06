#!/usr/bin/env python3
"""Validate static texture import role and Batch31 channel decision queues."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

TEXTURE_ROLE_MATRIX = ROOT / "Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv"
BATCH31_CHANNEL_QUEUE = ROOT / "Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.csv"

TEXTURE_ROLE_DOC = ROOT / "Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md"
BATCH31_CHANNEL_DOC = ROOT / "Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.md"

FORBIDDEN_READY_TERMS = ("ACCEPTED", "COMPLETE", "GREEN", "PASS")
PROOF_TERMS = ("Unity", "readback", "screenshot", "Frame Debugger", "proof", "material", "import")
BLOCKER_TERMS = ("source-only", "blocked", "blocker", "unproven", "no ", "without", "missing", "rejected", "unresolved")


@dataclass(frozen=True)
class MatrixSpec:
    name: str
    path: Path
    companion: Path
    id_columns: tuple[str, ...]
    priority_column: str
    status_column: str
    required_columns: tuple[str, ...]
    expected_ids: tuple[str, ...]
    expected_p0_count: int
    companion_terms: tuple[str, ...]


TEXTURE_ROLE_SPEC = MatrixSpec(
    name="texture_import_role_matrix",
    path=TEXTURE_ROLE_MATRIX,
    companion=TEXTURE_ROLE_DOC,
    id_columns=("texture_family", "role"),
    priority_column="priority",
    status_column="disposition",
    required_columns=(
        "priority",
        "texture_family",
        "role",
        "source_scope",
        "srgb",
        "texture_type",
        "mipmaps",
        "streaming_mips",
        "standalone_format_target",
        "compact_lane",
        "middle_lane",
        "high_lane",
        "ultra_lane",
        "proof_needed",
        "blockers",
        "disposition",
    ),
    expected_ids=(
        "foam_contact:albedo",
        "foam_contact:normal",
        "foam_contact:mrao_mask",
        "foam_contact:rgba_contact_mask",
        "aegir_cloud:band_albedo",
        "aegir_cloud:storm_mask_rgba",
        "aegir_cloud:detail",
        "wet_basalt_shell_sand:albedo",
        "wet_basalt_shell_sand:normal_mrao",
        "flora_coral:albedo",
        "flora_coral:normal_detail_mask",
        "ui_oxygen:icon_albedo",
        "ui_oxygen_mask:mask",
    ),
    expected_p0_count=4,
    companion_terms=(
        "No Unity import settings",
        "Source-only cleanup outputs under `Docs/GeneratedAssets` must not be imported directly as final art.",
        "Do not raw-patch `.meta`, `.mat`, `.prefab`, `.unity`, or `.asset` files.",
        "Final status: `PENDING_VERIFICATION`.",
    ),
)

BATCH31_SPEC = MatrixSpec(
    name="batch31_channel_semantics_decision_queue",
    path=BATCH31_CHANNEL_QUEUE,
    companion=BATCH31_CHANNEL_DOC,
    id_columns=("DecisionId",),
    priority_column="Priority",
    status_column="Status",
    required_columns=(
        "DecisionId",
        "Priority",
        "Package",
        "ArtifactSet",
        "Decision",
        "OwnerRoute",
        "RequiredBeforeUnityPromotion",
        "RejectIf",
        "LowConsequence",
        "MiddleConsequence",
        "HighConsequence",
        "UltraConsequence",
        "Status",
    ),
    expected_ids=tuple(f"B31DEC-{index:02d}" for index in range(1, 8)),
    expected_p0_count=3,
    companion_terms=(
        "Unity import: absent.",
        "Material binding: absent.",
        "Visual acceptance: absent.",
        "Runtime proof: absent.",
        "Importing any `MRAOSource`, `PROMO_MRAO_Candidate`, or `_MaskMap` by filename alone is rejected.",
    ),
)

ALL_SPECS = (TEXTURE_ROLE_SPEC, BATCH31_SPEC)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def row_id(spec: MatrixSpec, row: dict[str, str]) -> str:
    return ":".join(row[column] for column in spec.id_columns)


def load_rows(spec: MatrixSpec) -> list[dict[str, str]]:
    if not spec.path.exists():
        raise SystemExit(f"FAIL: missing matrix: {display_path(spec.path)}")

    with spec.path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in spec.required_columns if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: {spec.name} missing column(s): {', '.join(missing)}")

        rows: list[dict[str, str]] = []
        for row_number, row in enumerate(reader, start=2):
            current: dict[str, str] = {}
            for column in spec.required_columns:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: {spec.name} empty {column} at row {row_number}")
                current[column] = value
            rows.append(current)

    return rows


def require_any(matrix: str, row_name: str, field_name: str, value: str, terms: tuple[str, ...]) -> None:
    lowered = value.lower()
    if not any(term.lower() in lowered for term in terms):
        raise SystemExit(f"FAIL: {matrix} {row_name} {field_name} missing one of: {', '.join(terms)}")


def validate_status(matrix: str, row_name: str, status: str) -> None:
    upper = status.upper()
    if any(term in upper for term in FORBIDDEN_READY_TERMS):
        raise SystemExit(f"FAIL: {matrix} {row_name} has proof-looking status: {status}")
    allowed_fragments = ("PENDING", "BLOCKED", "SOURCE_ONLY", "CANDIDATE_BLOCKED", "MASK_ONLY", "STATIC_SOURCE")
    if not any(fragment in upper for fragment in allowed_fragments):
        raise SystemExit(f"FAIL: {matrix} {row_name} unsupported status boundary: {status}")


def validate_texture_role_semantics(row: dict[str, str]) -> None:
    name = f"{row['texture_family']}:{row['role']}"
    role = row["role"].lower()
    texture_type = row["texture_type"].lower()
    srgb = row["srgb"].lower()

    if "albedo" in role and "mask" not in role:
        if srgb != "true":
            raise SystemExit(f"FAIL: texture_import_role_matrix {name} albedo role must be sRGB true")

    if "normal" in role:
        if srgb != "false":
            raise SystemExit(f"FAIL: texture_import_role_matrix {name} normal role must be linear")
        if "normalmap" not in texture_type:
            raise SystemExit(f"FAIL: texture_import_role_matrix {name} normal role missing NormalMap texture type")

    if any(token in role for token in ("mrao", "mask", "detail", "storm")):
        if name == "ui_oxygen_mask:mask":
            if srgb != "false_if_mask_true_if_icon":
                raise SystemExit(f"FAIL: texture_import_role_matrix {name} UI mask special sRGB boundary missing")
        elif srgb != "false":
            raise SystemExit(f"FAIL: texture_import_role_matrix {name} mask/detail role must be linear")

    if row["mipmaps"].lower() not in {"true", "false"}:
        raise SystemExit(f"FAIL: texture_import_role_matrix {name} invalid mipmaps value")
    if row["streaming_mips"].lower() not in {"true", "false"}:
        raise SystemExit(f"FAIL: texture_import_role_matrix {name} invalid streaming_mips value")


def source_scope_paths(value: str) -> list[str]:
    normalized = value.replace(" sources", "")
    parts = [part.strip() for part in normalized.split(" and ")]
    paths = [part for part in parts if part.startswith(("Assets/", "Docs/"))]
    if value.startswith(("Assets/", "Docs/")) and not paths:
        paths.append(value)
    return paths


def validate_texture_source_paths(row: dict[str, str]) -> None:
    name = f"{row['texture_family']}:{row['role']}"
    paths = source_scope_paths(row["source_scope"])
    if not paths:
        raise SystemExit(f"FAIL: texture_import_role_matrix {name} source_scope has no project path")
    for value in paths:
        path = ROOT / value
        if not path.exists():
            raise SystemExit(f"FAIL: texture_import_role_matrix {name} missing source path: {value}")


def validate_rows(spec: MatrixSpec, rows: list[dict[str, str]]) -> None:
    ids = [row_id(spec, row) for row in rows]
    if ids != list(spec.expected_ids):
        raise SystemExit(f"FAIL: {spec.name} unexpected id order: {', '.join(ids)}")

    p0_count = sum(1 for row in rows if row[spec.priority_column] == "P0")
    if p0_count != spec.expected_p0_count:
        raise SystemExit(f"FAIL: {spec.name} expected P0 count {spec.expected_p0_count}, got {p0_count}")

    for row in rows:
        name = row_id(spec, row)
        priority = row[spec.priority_column]
        if priority not in {"P0", "P1", "P2"}:
            raise SystemExit(f"FAIL: {spec.name} {name} invalid priority: {priority}")

        validate_status(spec.name, name, row[spec.status_column])
        require_any(spec.name, name, "proof", " ".join(row.values()), PROOF_TERMS)
        require_any(spec.name, name, "boundary", " ".join(row.values()), BLOCKER_TERMS)

        if spec is TEXTURE_ROLE_SPEC:
            validate_texture_role_semantics(row)
            validate_texture_source_paths(row)
        elif spec is BATCH31_SPEC:
            require_any(spec.name, name, "OwnerRoute", row["OwnerRoute"], ("ASSET_OWNER",))
            if row["Status"] == "BLOCKED_CHANNEL_SEMANTICS":
                require_any(spec.name, name, "RequiredBeforeUnityPromotion", row["RequiredBeforeUnityPromotion"], ("ARM_REPACK", "MRAO_TARGET"))


def validate_companion_doc(spec: MatrixSpec) -> None:
    text = load_text(spec.companion)
    for term in spec.companion_terms:
        if term not in text:
            raise SystemExit(f"FAIL: {spec.name} companion doc missing term: {term}")


def validate_spec(spec: MatrixSpec) -> list[dict[str, str]]:
    rows = load_rows(spec)
    validate_rows(spec, rows)
    validate_companion_doc(spec)
    return rows


def validate_texture_import_role_matrix() -> dict[str, list[dict[str, str]]]:
    return {spec.name: validate_spec(spec) for spec in ALL_SPECS}


def main() -> None:
    results = validate_texture_import_role_matrix()
    role_rows = results[TEXTURE_ROLE_SPEC.name]
    batch31_rows = results[BATCH31_SPEC.name]
    role_p0 = sum(1 for row in role_rows if row["priority"] == "P0")
    batch31_blocked = sum(1 for row in batch31_rows if row["Status"] == "BLOCKED_CHANNEL_SEMANTICS")
    print(
        "TEXTURE_IMPORT_ROLE_MATRIX_OK "
        f"roles={len(role_rows)}:p0={role_p0} "
        f"batch31={len(batch31_rows)}:blocked_masks={batch31_blocked}"
    )


if __name__ == "__main__":
    main()
