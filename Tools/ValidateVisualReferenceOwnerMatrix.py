#!/usr/bin/env python3
"""Validate mandatory visual-reference owner matrix against actual image files."""

from __future__ import annotations

import csv
import struct
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MATRIX_PATH = ROOT / "Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv"
PATH_LEDGER_PATH = ROOT / "Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv"
OWNER_INDEX_PATH = ROOT / "Docs/AssetAudit/ASSET_OWNER_PACKET_INDEX_20260605.csv"
DIGEST_PATH = ROOT / "Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md"

EXPECTED_IDS = tuple(f"VREF-{index:02d}" for index in range(1, 16))
REQUIRED_COLUMNS = (
    "VrefId",
    "SourceFile",
    "Dimensions",
    "PrimarySurface",
    "OwnerPackets",
    "RequiredFutureH8Artifact",
    "VisualRequirement",
    "RejectIf",
    "Status",
)


@dataclass(frozen=True)
class VisualReferenceRow:
    vref_id: str
    source_file: str
    dimensions: str
    primary_surface: str
    owner_packets: str
    required_future_h8_artifact: str
    visual_requirement: str
    reject_if: str
    status: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {display_path(path)}")
        return [{key: (value or "").strip() for key, value in row.items()} for row in reader]


def load_matrix_rows(path: Path = MATRIX_PATH) -> list[VisualReferenceRow]:
    rows = load_csv(path)
    if rows:
        missing = [column for column in REQUIRED_COLUMNS if column not in rows[0]]
        if missing:
            raise SystemExit(f"FAIL: VREF owner matrix missing column(s): {', '.join(missing)}")

    parsed: list[VisualReferenceRow] = []
    for row_index, row in enumerate(rows, start=2):
        for column in REQUIRED_COLUMNS:
            if not row[column]:
                raise SystemExit(f"FAIL: VREF owner matrix empty {column} at row {row_index}")
        parsed.append(
            VisualReferenceRow(
                vref_id=row["VrefId"],
                source_file=row["SourceFile"],
                dimensions=row["Dimensions"],
                primary_surface=row["PrimarySurface"],
                owner_packets=row["OwnerPackets"],
                required_future_h8_artifact=row["RequiredFutureH8Artifact"],
                visual_requirement=row["VisualRequirement"],
                reject_if=row["RejectIf"],
                status=row["Status"],
            )
        )
    return parsed


def load_path_ledger(path: Path = PATH_LEDGER_PATH) -> dict[str, Path]:
    rows = load_csv(path)
    ledger: dict[str, Path] = {}
    for row in rows:
        vref_id = row.get("ReferenceId", "")
        current_path = row.get("CurrentPath", "")
        size_text = row.get("SizeBytes", "")
        if not vref_id or not current_path or not size_text:
            raise SystemExit("FAIL: visual reference path ledger has an incomplete row")
        image_path = ROOT / current_path
        if not image_path.exists():
            raise SystemExit(f"FAIL: VREF path missing on disk: {current_path}")
        try:
            expected_size = int(size_text)
        except ValueError as exc:
            raise SystemExit(f"FAIL: VREF path ledger SizeBytes is not int for {vref_id}: {size_text}") from exc
        actual_size = image_path.stat().st_size
        if expected_size != actual_size:
            raise SystemExit(f"FAIL: {vref_id} size drift: expected {expected_size}, actual {actual_size}")
        ledger[vref_id] = image_path
    return ledger


def load_owner_ids(path: Path = OWNER_INDEX_PATH) -> set[str]:
    owners: set[str] = set()
    for row in load_csv(path):
        owner_id = row.get("OwnerId", "")
        if owner_id:
            owners.add(f"ASSET_OWNER_{int(owner_id):02d}")
    return owners


def read_image_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return struct.unpack(">II", data[16:24])
    if data.startswith(b"\xff\xd8"):
        return read_jpeg_dimensions(data, path)
    if data.startswith(b"RIFF") and data[8:12] == b"WEBP":
        return read_webp_dimensions(data, path)
    raise SystemExit(f"FAIL: unsupported VREF image format: {display_path(path)}")


def read_jpeg_dimensions(data: bytes, path: Path) -> tuple[int, int]:
    offset = 2
    while offset + 9 < len(data):
        if data[offset] != 0xFF:
            offset += 1
            continue
        marker = data[offset + 1]
        offset += 2
        if marker in {0xD8, 0xD9}:
            continue
        if offset + 2 > len(data):
            break
        length = int.from_bytes(data[offset:offset + 2], "big")
        if length < 2:
            break
        if marker in {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}:
            height = int.from_bytes(data[offset + 3:offset + 5], "big")
            width = int.from_bytes(data[offset + 5:offset + 7], "big")
            return width, height
        offset += length
    raise SystemExit(f"FAIL: could not read JPEG dimensions: {display_path(path)}")


def read_webp_dimensions(data: bytes, path: Path) -> tuple[int, int]:
    if len(data) < 30:
        raise SystemExit(f"FAIL: truncated WEBP image: {display_path(path)}")
    chunk = data[12:16]
    payload = data[20:]
    if chunk == b"VP8X":
        width = 1 + int.from_bytes(data[24:27], "little")
        height = 1 + int.from_bytes(data[27:30], "little")
        return width, height
    if chunk == b"VP8 ":
        if len(payload) < 10 or payload[3:6] != b"\x9d\x01\x2a":
            raise SystemExit(f"FAIL: unsupported VP8 WEBP header: {display_path(path)}")
        width = int.from_bytes(payload[6:8], "little") & 0x3FFF
        height = int.from_bytes(payload[8:10], "little") & 0x3FFF
        return width, height
    if chunk == b"VP8L":
        if len(payload) < 5 or payload[0] != 0x2F:
            raise SystemExit(f"FAIL: unsupported VP8L WEBP header: {display_path(path)}")
        b1, b2, b3, b4 = payload[1], payload[2], payload[3], payload[4]
        width = 1 + (((b2 & 0x3F) << 8) | b1)
        height = 1 + (((b4 & 0x0F) << 10) | (b3 << 2) | ((b2 & 0xC0) >> 6))
        return width, height
    raise SystemExit(f"FAIL: unsupported WEBP chunk {chunk!r}: {display_path(path)}")


def validate_visual_reference_owner_matrix() -> list[VisualReferenceRow]:
    rows = load_matrix_rows()
    ledger = load_path_ledger()
    owners = load_owner_ids()

    ids = tuple(row.vref_id for row in rows)
    if ids != EXPECTED_IDS:
        raise SystemExit(f"FAIL: VREF owner matrix id order drift: {', '.join(ids)}")
    if set(ledger) != set(EXPECTED_IDS):
        missing = sorted(set(EXPECTED_IDS) - set(ledger))
        extra = sorted(set(ledger) - set(EXPECTED_IDS))
        raise SystemExit(f"FAIL: VREF path ledger id drift: missing={missing} extra={extra}")

    digest_text = DIGEST_PATH.read_text(encoding="utf-8")
    if "## Files Viewed" not in digest_text:
        raise SystemExit("FAIL: mandatory reference digest missing viewed-files section")
    if str(PATH_LEDGER_PATH.parent.relative_to(ROOT)) not in digest_text and "mandatory if you work on systems" not in digest_text:
        raise SystemExit("FAIL: mandatory reference digest missing reference-folder evidence")
    for vref_id, image_path in ledger.items():
        if image_path.name not in digest_text:
            raise SystemExit(f"FAIL: mandatory reference digest missing source file for {vref_id}: {image_path.name}")

    for row in rows:
        image_path = ledger[row.vref_id]
        width, height = read_image_dimensions(image_path)
        expected_dimensions = f"{width}x{height}"
        if row.dimensions != expected_dimensions:
            raise SystemExit(
                f"FAIL: {row.vref_id} dimension drift: matrix={row.dimensions} actual={expected_dimensions}"
            )

        row_owners = {owner.strip() for owner in row.owner_packets.split(";") if owner.strip()}
        unknown = sorted(row_owners - owners)
        if unknown:
            raise SystemExit(f"FAIL: {row.vref_id} references unknown owner(s): {', '.join(unknown)}")
        if "ASSET_OWNER_36" not in row_owners:
            raise SystemExit(f"FAIL: {row.vref_id} must include ASSET_OWNER_36 proof owner")
        if row.status != "PENDING_VERIFICATION":
            raise SystemExit(f"FAIL: {row.vref_id} status must remain PENDING_VERIFICATION")
        if "Reject if" in row.reject_if:
            raise SystemExit(f"FAIL: {row.vref_id} RejectIf should be direct condition text, not prose label")
        if "proof" not in row.required_future_h8_artifact.lower() and "h8_1475_" not in row.required_future_h8_artifact:
            raise SystemExit(f"FAIL: {row.vref_id} required artifact must name h8_1475 proof target")
        if len(row.visual_requirement) < 48 or len(row.reject_if) < 32:
            raise SystemExit(f"FAIL: {row.vref_id} visual requirement/reject condition too weak")

    return rows


def main() -> None:
    rows = validate_visual_reference_owner_matrix()
    print(f"VISUAL_REFERENCE_OWNER_MATRIX_OK rows={len(rows)} images={len(rows)}")


if __name__ == "__main__":
    main()
