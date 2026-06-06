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
PROCEDURAL_FINALS_PREFIX = "Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals"
BIOFORGE_KELP_PREFIX = "Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp"
BIOFORGE_TUBE_CORAL_PREFIX = "Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral"
BIOFORGE_POROUS_ROCK_PREFIX = "Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock"
WORLD_PROXY_PREFIX = "Assets/_Project/Prefabs/WorldProceduralProxy"
PLACEHOLDER_PREFIX = "Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders"
CONSTRUCTION_PREFIX = "Assets/_Project/Prefabs/Construction/Final"
OCEAN_PREFAB_NAMES = ("Hecton Ocean.prefab", "Ocean_Crest.prefab")
WORLD_PROXY_MATERIAL_PREFIX = "Assets/_Project/Art/Materials/WorldProceduralProxy"
MATERIAL_GUID_PATTERN = re.compile(r"fileID:\s*2100000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*2")
META_GUID_PATTERN = re.compile(r"^guid:\s*([0-9a-fA-F]{32})", re.MULTILINE)


@dataclass(frozen=True)
class QueueRow:
    queue_order: str
    priority: str
    pool: str
    representative_paths: str
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
    folder: str = ""
    mesh_filter_token_count: int = 0
    renderer_token_count: int = 0
    material_token_count: int = 0


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
                representative_paths=row["representative_paths"],
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
                folder=row.get("folder", ""),
                has_lodgroup_token=parse_bool(row["has_lodgroup_token"]),
                builtin_primitive_mesh_ref_count=parse_int(row["builtin_primitive_mesh_ref_count"]),
                mesh_filter_token_count=parse_int(row.get("mesh_filter_token_count", "0")),
                renderer_token_count=parse_int(row.get("renderer_token_count", "0")),
                mesh_collider_token_count=parse_int(row["mesh_collider_token_count"]),
                material_token_count=parse_int(row.get("material_token_count", "0")),
                policy_flags=row["policy_flags"],
            )
        )
    return rows


def rows_under(rows: list[PrefabRow], prefix: str) -> list[PrefabRow]:
    return [row for row in rows if row.path.startswith(prefix)]


def rows_by_path(rows: list[PrefabRow]) -> dict[str, PrefabRow]:
    return {row.path: row for row in rows}


def resolve_representative_prefabs(row: QueueRow, prefabs: list[PrefabRow]) -> list[PrefabRow]:
    prefabs_by_path = rows_by_path(prefabs)
    resolved: list[PrefabRow] = []
    seen: set[str] = set()
    context_parent = ""

    for raw_token in row.representative_paths.split(";"):
        token = raw_token.strip()
        if not token:
            continue

        candidate_paths: list[str] = []
        if token.startswith("Assets/"):
            normalized = token.replace("\\", "/")
            if normalized.endswith(".prefab"):
                candidate_paths.append(normalized)
                context_parent = str(Path(normalized).parent).replace("\\", "/")
            else:
                context_parent = str(Path(normalized).parent).replace("\\", "/")
                candidate_paths.extend(prefab.path for prefab in rows_under(prefabs, normalized))
        elif token.endswith(".prefab") and context_parent:
            candidate_paths.append(f"{context_parent}/{token}")
        elif context_parent:
            sibling_prefix = f"{context_parent}/{token}"
            candidate_paths.extend(prefab.path for prefab in rows_under(prefabs, sibling_prefix))

        for candidate_path in candidate_paths:
            prefab = prefabs_by_path.get(candidate_path)
            if prefab is None or prefab.path in seen:
                continue
            resolved.append(prefab)
            seen.add(prefab.path)
    return resolved


def require_row_text(row: QueueRow, field_name: str, needle: str) -> None:
    value = getattr(row, field_name)
    if needle.lower() not in value.lower():
        raise SystemExit(f"FAIL: queue row {row.pool} {field_name} missing required text: {needle}")


def validate_queue_rows(rows: list[QueueRow]) -> None:
    if len(rows) != 8:
        raise SystemExit(f"FAIL: expected 8 queue rows, got {len(rows)}")

    by_pool = {row.pool: row for row in rows}
    required = (
        "ProceduralFinals geology",
        "Flora Baked pool",
        "BioForge shallows kelp/tube coral",
        "BioForge porous rock",
        "WorldProceduralProxy visible placement",
        "WorldRuntime ProceduralPlaceholders",
        "Construction Final product-face pool",
        "External/prototype material refs",
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
    if by_pool["BioForge porous rock"].disposition != "ROUTE_REJECT_UNTIL_COLLIDER_PROOF":
        raise SystemExit("FAIL: PorousRock row must remain collider-proof rejected")
    if by_pool["External/prototype material refs"].disposition != "READBACK_REQUIRED":
        raise SystemExit("FAIL: external/prototype material row must remain READBACK_REQUIRED")


def validate_row_backing_evidence(
    queue: list[QueueRow],
    prefabs: list[PrefabRow],
) -> tuple[int, int, int, int]:
    by_pool = {row.pool: row for row in queue}

    geology = rows_under(prefabs, PROCEDURAL_FINALS_PREFIX)
    if len(geology) != 49:
        raise SystemExit(f"FAIL: ProceduralFinals geology row expects 49 prefabs, got {len(geology)}")
    if any(not prefab.has_lodgroup_token for prefab in geology):
        raise SystemExit("FAIL: ProceduralFinals geology row claims LODGroup coverage but a prefab lacks it")
    if any(prefab.builtin_primitive_mesh_ref_count != 0 for prefab in geology):
        raise SystemExit("FAIL: ProceduralFinals geology row claims no primitive refs but a prefab has one")
    if any(prefab.mesh_collider_token_count != 0 for prefab in geology):
        raise SystemExit("FAIL: ProceduralFinals geology row claims no MeshCollider but a prefab has one")

    geology_representatives = resolve_representative_prefabs(by_pool["ProceduralFinals geology"], prefabs)
    if len(geology_representatives) != 3:
        raise SystemExit(
            "FAIL: ProceduralFinals geology representative paths must resolve to 3 scanned prefabs, "
            f"got {len(geology_representatives)}"
        )
    require_row_text(by_pool["ProceduralFinals geology"], "required_proof", "collider proxy")
    require_row_text(by_pool["ProceduralFinals geology"], "required_proof", "LOD")

    bioforge = rows_under(prefabs, BIOFORGE_KELP_PREFIX) + rows_under(prefabs, BIOFORGE_TUBE_CORAL_PREFIX)
    if len(bioforge) != 150:
        raise SystemExit(f"FAIL: BioForge kelp/tube coral row expects 150 prefabs, got {len(bioforge)}")
    if any(not prefab.has_lodgroup_token for prefab in bioforge):
        raise SystemExit("FAIL: BioForge kelp/tube coral row claims LODGroup coverage but a prefab lacks it")
    if any(prefab.builtin_primitive_mesh_ref_count != 0 for prefab in bioforge):
        raise SystemExit("FAIL: BioForge kelp/tube coral row claims no primitive refs but a prefab has one")
    require_row_text(by_pool["BioForge shallows kelp/tube coral"], "required_proof", "final material")
    require_row_text(by_pool["BioForge shallows kelp/tube coral"], "reject_condition", "proxy material")

    porous = rows_under(prefabs, BIOFORGE_POROUS_ROCK_PREFIX)
    if len(porous) != 50:
        raise SystemExit(f"FAIL: BioForge porous rock row expects 50 prefabs, got {len(porous)}")
    if any(not prefab.has_lodgroup_token for prefab in porous):
        raise SystemExit("FAIL: BioForge porous rock row claims LODGroup coverage but a prefab lacks it")
    if any(prefab.mesh_collider_token_count <= 0 for prefab in porous):
        raise SystemExit("FAIL: BioForge porous rock row claims MeshCollider refs but a prefab lacks them")
    require_row_text(by_pool["BioForge porous rock"], "required_proof", "Collider proxy")
    require_row_text(by_pool["BioForge porous rock"], "reject_condition", "Complex MeshCollider")

    ocean = [prefab for prefab in prefabs if any(prefab.path.endswith(name) for name in OCEAN_PREFAB_NAMES)]
    if len(ocean) != 2:
        raise SystemExit(f"FAIL: external/prototype material row expects 2 ocean prefab evidence rows, got {len(ocean)}")
    if not any(prefab.builtin_primitive_mesh_ref_count > 0 for prefab in ocean):
        raise SystemExit("FAIL: external/prototype material row expects at least one ocean primitive-risk prefab")
    require_row_text(by_pool["External/prototype material refs"], "required_proof", "third-party asset integrity")
    require_row_text(by_pool["External/prototype material refs"], "required_proof", "no runtime material clones")

    return len(geology), len(bioforge), len(porous), len(ocean)


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
    geology, bioforge, porous, ocean = validate_row_backing_evidence(queue, prefabs)
    baked, proxy, placeholders, construction = validate_prefab_counts(prefabs)
    baked_proxy_refs = validate_proxy_material_coverage(prefabs)
    return baked, baked_proxy_refs, proxy, placeholders, construction, geology, bioforge, porous, ocean


def main() -> None:
    baked, baked_proxy_refs, proxy, placeholders, construction, geology, bioforge, porous, ocean = (
        validate_mesh_prefab_review_queue()
    )
    print(
        "MESH_PREFAB_REVIEW_QUEUE_OK "
        f"baked={baked} baked_proxy_refs={baked_proxy_refs} "
        f"proxy={proxy} placeholders={placeholders} construction={construction} "
        f"geology={geology} bioforge={bioforge} porous={porous} ocean={ocean}"
    )


if __name__ == "__main__":
    main()
