#!/usr/bin/env python3
"""Validate texture role intent against the static texture technical ledger."""

from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

ROLE_MATRIX = ROOT / "Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv"
TECHNICAL_LEDGER = ROOT / "Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv"

ASSET_SCOPE_PREFIXES = ("Assets/",)
DOC_SOURCE_PREFIXES = ("Docs/GeneratedAssets/",)

ALBEDO_NAME_TOKENS = ("albedo", "basecolor", "base_color", "color", "diff")
NORMAL_NAME_TOKENS = ("normal", "normalgl", "norm")
MASK_NAME_TOKENS = ("ao", "ambientocclusion", "mrao", "mask", "orm", "rough", "detail", "storm")


@dataclass(frozen=True)
class RoleRow:
    priority: str
    texture_family: str
    role: str
    source_scope: str
    srgb: str
    texture_type: str
    mipmaps: str
    streaming_mips: str
    blockers: str
    disposition: str

    @property
    def row_id(self) -> str:
        return f"{self.texture_family}:{self.role}"


@dataclass(frozen=True)
class TextureLedgerRow:
    path: str
    meta_texture_type: str
    meta_srgb: str
    meta_mipmaps: str
    meta_streaming_mips: str
    ledger_class: str
    policy_flags: str

    @property
    def lower_name(self) -> str:
        return Path(self.path).name.lower()


@dataclass(frozen=True)
class ValidationResult:
    role_rows: int
    exact_asset_rows: int
    directory_rows: int
    docs_source_only_rows: int
    ledger_matches: int
    blockers: tuple[str, ...]


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_role_rows(path: Path = ROLE_MATRIX) -> list[RoleRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing texture role matrix: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        rows: list[RoleRow] = []
        for row_number, row in enumerate(reader, start=2):
            try:
                rows.append(
                    RoleRow(
                        priority=(row["priority"] or "").strip(),
                        texture_family=(row["texture_family"] or "").strip(),
                        role=(row["role"] or "").strip(),
                        source_scope=(row["source_scope"] or "").strip(),
                        srgb=(row["srgb"] or "").strip(),
                        texture_type=(row["texture_type"] or "").strip(),
                        mipmaps=(row["mipmaps"] or "").strip(),
                        streaming_mips=(row["streaming_mips"] or "").strip(),
                        blockers=(row["blockers"] or "").strip(),
                        disposition=(row["disposition"] or "").strip(),
                    )
                )
            except KeyError as exc:
                raise SystemExit(f"FAIL: texture role matrix missing column at row {row_number}: {exc}") from exc
    return rows


def load_ledger_rows(path: Path = TECHNICAL_LEDGER) -> dict[str, TextureLedgerRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing texture technical ledger: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        rows: dict[str, TextureLedgerRow] = {}
        for row_number, row in enumerate(reader, start=2):
            texture_path = (row.get("path") or "").strip()
            if not texture_path:
                raise SystemExit(f"FAIL: texture technical ledger empty path at row {row_number}")
            rows[texture_path] = TextureLedgerRow(
                path=texture_path,
                meta_texture_type=(row.get("meta_texture_type") or "").strip(),
                meta_srgb=(row.get("meta_srgb") or "").strip(),
                meta_mipmaps=(row.get("meta_mipmaps") or "").strip(),
                meta_streaming_mips=(row.get("meta_streaming_mips") or "").strip(),
                ledger_class=(row.get("ledger_class") or "").strip(),
                policy_flags=(row.get("policy_flags") or "").strip(),
            )
    return rows


def source_scope_parts(value: str) -> list[str]:
    parts = [part.strip() for part in value.replace(" sources", "").split(" and ")]
    return [part for part in parts if part.startswith(("Assets/", "Docs/"))]


def is_file_scope(scope: str) -> bool:
    return Path(scope).suffix.lower() in {".png", ".jpg", ".jpeg", ".tga", ".psd", ".webp"}


def classify_role(row: RoleRow) -> str:
    role = row.role.lower()
    if "albedo" in role and "mask" not in role:
        return "albedo"
    if "normal" in role:
        return "normal_mask" if any(token in role for token in ("mrao", "mask", "detail")) else "normal"
    if any(token in role for token in ("mrao", "mask", "detail", "storm")):
        return "mask"
    return "unknown"


def matches_name(row: TextureLedgerRow, role_class: str) -> bool:
    name = row.lower_name
    if role_class == "albedo":
        return any(token in name for token in ALBEDO_NAME_TOKENS)
    if role_class == "normal":
        return any(token in name for token in NORMAL_NAME_TOKENS)
    if role_class == "mask":
        return any(token in name for token in MASK_NAME_TOKENS)
    if role_class == "normal_mask":
        return any(token in name for token in (*NORMAL_NAME_TOKENS, *MASK_NAME_TOKENS))
    return False


def is_normal_name(row: TextureLedgerRow) -> bool:
    return any(token in row.lower_name for token in NORMAL_NAME_TOKENS)


def is_mask_name(row: TextureLedgerRow) -> bool:
    return not is_normal_name(row) and any(token in row.lower_name for token in MASK_NAME_TOKENS)


def desired_bool(value: str) -> str | None:
    lowered = value.lower()
    if lowered == "true":
        return "1"
    if lowered == "false":
        return "0"
    return None


def expected_srgb(role_row: RoleRow, ledger_row: TextureLedgerRow) -> str | None:
    special = role_row.srgb.lower()
    if special == "false_if_mask_true_if_icon":
        if "mask" in role_row.role.lower():
            return "0"
        return "1"
    return desired_bool(role_row.srgb)


def expected_texture_type(role_row: RoleRow, ledger_row: TextureLedgerRow) -> str | None:
    texture_type = role_row.texture_type.lower()
    if "sprite" in texture_type:
        return "8"
    if "normalmap" in texture_type:
        return "1" if "normal" in ledger_row.lower_name else None
    if texture_type.startswith("default"):
        return "0"
    return None


def validate_exact_asset(role_row: RoleRow, ledger_row: TextureLedgerRow) -> list[str]:
    blockers: list[str] = []
    row_id = role_row.row_id
    expected = expected_srgb(role_row, ledger_row)
    if expected is not None and ledger_row.meta_srgb != expected:
        blockers.append(f"{row_id}:srgb_mismatch:{ledger_row.path}:expected={expected}:actual={ledger_row.meta_srgb}")

    expected_type = expected_texture_type(role_row, ledger_row)
    if expected_type is not None and ledger_row.meta_texture_type != expected_type:
        blockers.append(
            f"{row_id}:texture_type_mismatch:{ledger_row.path}:expected={expected_type}:actual={ledger_row.meta_texture_type}"
        )

    expected_mips = desired_bool(role_row.mipmaps)
    if expected_mips is not None and ledger_row.meta_mipmaps != expected_mips:
        blockers.append(
            f"{row_id}:mipmaps_mismatch:{ledger_row.path}:expected={expected_mips}:actual={ledger_row.meta_mipmaps}"
        )

    expected_streaming = desired_bool(role_row.streaming_mips)
    if expected_streaming is not None and ledger_row.meta_streaming_mips != expected_streaming:
        blockers.append(
            f"{row_id}:streaming_mips_mismatch:{ledger_row.path}:"
            f"expected={expected_streaming}:actual={ledger_row.meta_streaming_mips}"
        )

    return blockers


def validate_directory_scope(role_row: RoleRow, candidates: list[TextureLedgerRow]) -> list[str]:
    blockers: list[str] = []
    row_id = role_row.row_id
    role_class = classify_role(role_row)
    matched = [row for row in candidates if matches_name(row, role_class)]
    if not matched:
        return [f"{row_id}:no_role_named_ledger_match"]

    if role_class in {"normal", "normal_mask"}:
        normal_rows = [row for row in matched if is_normal_name(row)]
        if not normal_rows:
            blockers.append(f"{row_id}:missing_normal_ledger_match")
        elif not any(row.meta_texture_type == "1" and row.meta_srgb == "0" for row in normal_rows):
            blockers.append(f"{row_id}:normal_meta_mismatch")

    if role_class in {"mask", "normal_mask"}:
        mask_rows = [row for row in matched if is_mask_name(row)]
        if not mask_rows:
            blockers.append(f"{row_id}:missing_linear_mask_ledger_match")
        elif not any(row.meta_srgb == "0" for row in mask_rows):
            blockers.append(f"{row_id}:mask_srgb_mismatch")

    if role_class == "albedo" and not any(row.meta_srgb == "1" and row.meta_texture_type == "0" for row in matched):
        blockers.append(f"{row_id}:albedo_meta_mismatch")

    expected_mips = desired_bool(role_row.mipmaps)
    if expected_mips is not None and not any(row.meta_mipmaps == expected_mips for row in matched):
        blockers.append(f"{row_id}:no_matching_mipmap_setting:expected={expected_mips}")

    expected_streaming = desired_bool(role_row.streaming_mips)
    if expected_streaming is not None and not any(row.meta_streaming_mips == expected_streaming for row in matched):
        blockers.append(f"{row_id}:no_matching_streaming_mips_setting:expected={expected_streaming}")

    return blockers


def validate_texture_role_technical_ledger() -> ValidationResult:
    role_rows = load_role_rows()
    ledger_by_path = load_ledger_rows()
    ledger_rows = tuple(ledger_by_path.values())

    exact_asset_rows = 0
    directory_rows = 0
    docs_source_only_rows = 0
    ledger_matches = 0
    blockers: list[str] = []

    for role_row in role_rows:
        parts = source_scope_parts(role_row.source_scope)
        asset_parts = [part for part in parts if part.startswith(ASSET_SCOPE_PREFIXES)]
        docs_parts = [part for part in parts if part.startswith(DOC_SOURCE_PREFIXES)]

        if docs_parts and not asset_parts:
            docs_source_only_rows += 1
            continue

        if not asset_parts:
            blockers.append(f"{role_row.row_id}:no_asset_or_docs_generated_source_scope")
            continue

        for asset_scope in asset_parts:
            if is_file_scope(asset_scope):
                exact_asset_rows += 1
                ledger_row = ledger_by_path.get(asset_scope)
                if ledger_row is None:
                    blockers.append(f"{role_row.row_id}:missing_exact_technical_ledger_row:{asset_scope}")
                    continue
                ledger_matches += 1
                blockers.extend(validate_exact_asset(role_row, ledger_row))
            else:
                directory_rows += 1
                prefix = asset_scope.rstrip("/") + "/"
                candidates = [row for row in ledger_rows if row.path.startswith(prefix)]
                if not candidates:
                    blockers.append(f"{role_row.row_id}:missing_directory_technical_ledger_rows:{asset_scope}")
                    continue
                ledger_matches += len(candidates)
                blockers.extend(validate_directory_scope(role_row, candidates))

    return ValidationResult(
        role_rows=len(role_rows),
        exact_asset_rows=exact_asset_rows,
        directory_rows=directory_rows,
        docs_source_only_rows=docs_source_only_rows,
        ledger_matches=ledger_matches,
        blockers=tuple(blockers),
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-fail", action="store_true", help="Print rejection status but return success.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    result = validate_texture_role_technical_ledger()
    status = "TEXTURE_ROLE_TECHNICAL_LEDGER_OK"
    if result.blockers:
        status = "TEXTURE_ROLE_TECHNICAL_LEDGER_REJECTED"
    print(
        f"{status} blockers={len(result.blockers)} rows={result.role_rows} "
        f"exact_asset_rows={result.exact_asset_rows} directory_rows={result.directory_rows} "
        f"docs_source_only_rows={result.docs_source_only_rows} ledger_matches={result.ledger_matches}"
    )
    for blocker in result.blockers:
        print(f"BLOCKER: {blocker}")
    if result.blockers and not args.no_fail:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
