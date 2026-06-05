#!/usr/bin/env python3
"""Validate static mesh/prefab review queue evidence for HECTON-8."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv"
PREFAB_PROPERTIES_PATH = ROOT / "Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv"

BAKED_FLORA_PREFIX = "Assets/_Project/Prefabs/Nature/Flora/Baked"
WORLD_PROXY_PREFIX = "Assets/_Project/Prefabs/WorldProceduralProxy"
PLACEHOLDER_PREFIX = "Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders"
CONSTRUCTION_PREFIX = "Assets/_Project/Prefabs/Construction/Final"
WORLD_PROXY_MATERIAL_PREFIX = "Assets/_Project/Art/Materials/WorldProceduralProxy"
MATERIAL_GUID_PATTERN = re.compile(r"fileID:\s*2100000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*2")
META_GUID_PATTERN = re.compile(r"^guid:\s*([0-9a-fA-F]{32})", re.MULTILINE)


@dataclass(frozen=True)
class QueueRow:
    queue_order: str
    priority: str
    pool: str
    static_evidence: str
    required_proof: str
    reject_condition: str
    disposition: str
    status: str


@dataclass(frozen=True)
class PrefabRow:
    path: str
    has_lodgroup_token: bool
    builtin_primitive_mesh_ref_count: int
    mesh_collider_token_count: int
    policy_flags: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def parse_bool(value: str) -> bool:
    return value.strip().lower() == "true"


def parse_int(value: str) -> int:
    return int(value.strip())


def load_queue(path: Path = QUEUE_PATH) -> list[QueueRow]:
    rows = []
    for row in load_csv(path):
        rows.append(
            QueueRow(
                queue_order=row["queue_order"],
                priority=row["priority"],
                pool=row["pool"],
                static_evidence=row["static_evidence"],
                required_proof=row["required_proof"],
                reject_condition=row["reject_condition"],
                disposition=row["disposition"],
                status=row["status"],
            )
        )
    return rows


def load_prefab_rows(path: Path = PREFAB_PROPERTIES_PATH) -> list[PrefabRow]:
    rows = []
    for row in load_csv(path):
        rows.append(
            PrefabRow(
                path=row["path"],
                has_lodgroup_token=parse_bool(row["has_lodgroup_token"]),
                builtin_primitive_mesh_ref_count=parse_int(row["builtin_primitive_mesh_ref_count"]),
                mesh_collider_token_count=parse_int(row["mesh_collider_token_count"]),
                policy_flags=row["policy_flags"],
            )
        )
    return rows


def rows_under(rows: list[PrefabRow], prefix: str) -> list[PrefabRow]:
    return [row for row in rows if row.path.startswith(prefix)]


def validate_queue_rows(rows: list[QueueRow]) -> None:
    if len(rows) != 8:
        raise SystemExit(f"FAIL: expected 8 queue rows, got {len(rows)}")

    by_pool = {row.pool: row for row in rows}
    required = (
        "Flora Baked pool",
        "WorldProceduralProxy visible placement",
        "WorldRuntime ProceduralPlaceholders",
        "Construction Final product-face pool",
    )
    for pool in required:
        if pool not in by_pool:
            raise SystemExit(f"FAIL: missing queue pool: {pool}")

    for row in rows:
        if row.status != "PENDING_VERIFICATION":
            raise SystemExit(f"FAIL: queue row {row.pool} status must remain PENDING_VERIFICATION")
        if "Unity" not in row.required_proof and "proof" not in row.required_proof.lower():
            raise SystemExit(f"FAIL: queue row {row.pool} missing proof boundary")

    if by_pool["WorldProceduralProxy visible placement"].disposition != "REJECT_VISIBLE_ROUTE_PLACEMENT":
        raise SystemExit("FAIL: WorldProceduralProxy row must reject visible route placement")
    if by_pool["WorldRuntime ProceduralPlaceholders"].disposition != "REJECT_VISIBLE_ROUTE_PLACEMENT":
        raise SystemExit("FAIL: WorldRuntime placeholder row must reject visible route placement")
    if by_pool["Flora Baked pool"].disposition != "CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL":
        raise SystemExit("FAIL: Baked flora row must remain material-blocked candidate")


def validate_prefab_counts(prefabs: list[PrefabRow]) -> tuple[int, int, int, int]:
    baked = rows_under(prefabs, BAKED_FLORA_PREFIX)
    proxy = rows_under(prefabs, WORLD_PROXY_PREFIX)
    placeholders = rows_under(prefabs, PLACEHOLDER_PREFIX)
    construction = rows_under(prefabs, CONSTRUCTION_PREFIX)

    if len(baked) != 89:
        raise SystemExit(f"FAIL: expected 89 baked flora prefabs, got {len(baked)}")
    if any(not row.has_lodgroup_token for row in baked):
        raise SystemExit("FAIL: baked flora queue claims LODGroup coverage but a row lacks LODGroup")
    if any(row.builtin_primitive_mesh_ref_count != 0 for row in baked):
        raise SystemExit("FAIL: baked flora queue claims no primitive mesh refs but a row has one")

    if len(proxy) != 88:
        raise SystemExit(f"FAIL: expected 88 WorldProceduralProxy prefabs, got {len(proxy)}")
    if any(row.has_lodgroup_token for row in proxy):
        raise SystemExit("FAIL: WorldProceduralProxy queue claims no LODGroups but a row has LODGroup")
    if any(row.builtin_primitive_mesh_ref_count <= 0 for row in proxy):
        raise SystemExit("FAIL: WorldProceduralProxy queue claims primitive mesh refs but a row lacks them")

    if len(placeholders) != 30:
        raise SystemExit(f"FAIL: expected 30 placeholder prefabs, got {len(placeholders)}")
    if any(row.has_lodgroup_token for row in placeholders):
        raise SystemExit("FAIL: placeholder queue claims no LODGroups but a row has LODGroup")
    if any(row.builtin_primitive_mesh_ref_count <= 0 for row in placeholders):
        raise SystemExit("FAIL: placeholder queue claims primitive mesh refs but a row lacks them")

    if len(construction) != 10:
        raise SystemExit(f"FAIL: expected 10 construction final prefabs, got {len(construction)}")
    missing_lod = sum(1 for row in construction if not row.has_lodgroup_token)
    if missing_lod != 4:
        raise SystemExit(f"FAIL: expected 4 construction prefabs without LODGroup, got {missing_lod}")
    if any(row.builtin_primitive_mesh_ref_count <= 0 for row in construction):
        raise SystemExit("FAIL: construction queue claims primitive mesh refs but a row lacks them")

    return len(baked), len(proxy), len(placeholders), len(construction)


def load_material_guid_map(root: Path = ROOT) -> dict[str, str]:
    guid_map: dict[str, str] = {}
    for meta_path in root.glob("Assets/**/*.mat.meta"):
        text = meta_path.read_text(encoding="utf-8", errors="ignore")
        match = META_GUID_PATTERN.search(text)
        if not match:
            continue
        material_path = str(meta_path.relative_to(root)).replace("\\", "/")
        material_path = material_path[:-5]
        guid_map[match.group(1).lower()] = material_path
    return guid_map


def extract_material_guids(prefab_path: Path) -> set[str]:
    if not prefab_path.exists():
        raise SystemExit(f"FAIL: missing prefab path: {display_path(prefab_path)}")
    text = prefab_path.read_text(encoding="utf-8", errors="ignore")
    return {match.group(1).lower() for match in MATERIAL_GUID_PATTERN.finditer(text)}


def collect_proxy_material_baked_prefabs(
    prefabs: list[PrefabRow],
    material_guid_map: dict[str, str],
    root: Path = ROOT,
) -> set[str]:
    baked_prefabs: set[str] = set()
    for row in rows_under(prefabs, BAKED_FLORA_PREFIX):
        material_guids = extract_material_guids(root / row.path)
        material_paths = [material_guid_map.get(guid, "") for guid in material_guids]
        if not material_paths:
            raise SystemExit(f"FAIL: baked flora prefab has no resolvable material GUIDs: {row.path}")
        if not any(path.startswith(WORLD_PROXY_MATERIAL_PREFIX) for path in material_paths):
            continue
        baked_prefabs.add(row.path)
    return baked_prefabs


def validate_proxy_material_coverage(prefabs: list[PrefabRow], root: Path = ROOT) -> int:
    material_guid_map = load_material_guid_map(root=root)
    baked_proxy_prefabs = collect_proxy_material_baked_prefabs(prefabs, material_guid_map, root=root)
    if len(baked_proxy_prefabs) != 89:
        raise SystemExit(
            "FAIL: expected proxy material GUID coverage for 89 baked flora prefabs, "
            f"got {len(baked_proxy_prefabs)}"
        )
    return len(baked_proxy_prefabs)


def validate_mesh_prefab_review_queue() -> tuple[int, int, int, int, int]:
    queue = load_queue()
    prefabs = load_prefab_rows()
    validate_queue_rows(queue)
    baked, proxy, placeholders, construction = validate_prefab_counts(prefabs)
    baked_proxy_refs = validate_proxy_material_coverage(prefabs)
    return baked, baked_proxy_refs, proxy, placeholders, construction


def main() -> None:
    baked, baked_proxy_refs, proxy, placeholders, construction = validate_mesh_prefab_review_queue()
    print(
        "MESH_PREFAB_REVIEW_QUEUE_OK "
        f"baked={baked} baked_proxy_refs={baked_proxy_refs} "
        f"proxy={proxy} placeholders={placeholders} construction={construction}"
    )


if __name__ == "__main__":
    main()
