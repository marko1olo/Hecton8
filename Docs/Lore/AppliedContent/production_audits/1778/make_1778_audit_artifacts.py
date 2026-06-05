from __future__ import annotations

import csv
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
AUDIT = ROOT / "Docs" / "Lore" / "AppliedContent" / "production_audits" / "1778"

TARGET_LOCALES = [
    "en_US", "ru_RU", "ja_JP", "zh_CN", "fr_FR", "es_ES", "de_DE", "pl_PL",
    "uk_UA", "ar_SA", "id_ID", "ko_KR", "he_IL", "pt_BR", "nl_NL",
]

SURFACE_MASKS = {
    "title": 1 << 0,
    "scanner": 1 << 1,
    "terminal": 1 << 2,
    "audio": 1 << 3,
    "in_game_wiki": 1 << 4,
    "external_site": 1 << 5,
    "field_note": 1 << 6,
}

KNOWN_HOOKS = {
    ("NarrativeDiscovery", "appliedLorePacketHash"),
    ("NarrativeSpatialTriggerAuthoring", "AppliedLoreHash"),
    ("MessageTerminal", "appliedLorePacketHash"),
    ("ScannableFragment", "appliedLoreQuarterPacketHash"),
    ("ScannableFragment", "appliedLoreHalfPacketHash"),
    ("ScannableFragment", "appliedLoreFinalPacketHash"),
}

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619


def fnv1a32(value: str) -> int:
    if not value:
        return 0
    hash_value = FNV_OFFSET
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value or 1


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, object]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in fieldnames})


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path).replace("\\", "/")


def split_semicolon(value: str) -> list[str]:
    return [part.strip() for part in (value or "").split(";") if part.strip()]


def parse_hash(value: str) -> int | None:
    if not value:
        return None
    try:
        return int(value, 0)
    except ValueError:
        return None


def exists_project_path(value: str) -> bool:
    value = value.strip().strip('"')
    if not value:
        return False
    if value.startswith(("Assets/", "Docs/", "Tools/")):
        return (ROOT / value).exists()
    return False


def build_route_matrix(packet_set: set[str]) -> tuple[int, set[str], Counter[str]]:
    route_dir = ROOT / "Docs" / "Lore" / "AppliedContent" / "route_cards"
    route_sources = sorted(route_dir.glob("*_route_cards.csv"), key=lambda path: path.name.lower())
    route_rows_out: list[dict[str, object]] = []
    direct_packet_coverage: set[str] = set()
    status_counts: Counter[str] = Counter()
    total_rows = 0

    for source in route_sources:
        with source.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            for line_number, row in enumerate(reader, start=2):
                total_rows += 1
                packets = split_semicolon(row.get("packet_ids", ""))
                required = split_semicolon(row.get("required_packet_ids", ""))
                direct_packet_coverage.update(packets)
                missing_packets = [packet for packet in packets if packet not in packet_set]
                missing_required = [packet for packet in required if packet not in packet_set]
                status: list[str] = []
                if not row.get("route_card_id"):
                    status.append("MISSING_ROUTE_ID")
                if not packets:
                    status.append("NO_PACKET_IDS")
                if missing_packets:
                    status.append("UNKNOWN_PACKET_ID")
                if missing_required:
                    status.append("UNKNOWN_REQUIRED_PACKET_ID")
                if row.get("primary_surface") not in SURFACE_MASKS:
                    status.append("UNSUPPORTED_SURFACE")
                try:
                    depth_min = float(row.get("depth_min_m", ""))
                    depth_max = float(row.get("depth_max_m", ""))
                    if depth_min < 0 or depth_max < depth_min:
                        status.append("BAD_DEPTH_RANGE")
                except ValueError:
                    status.append("BAD_DEPTH_RANGE")
                if not status:
                    status.append("OK")
                status_counts.update(status)
                route_rows_out.append({
                    "source_file": rel(source),
                    "line": line_number,
                    "route_card_id": row.get("route_card_id", ""),
                    "phase_id": row.get("phase_id", ""),
                    "depth_min_m": row.get("depth_min_m", ""),
                    "depth_max_m": row.get("depth_max_m", ""),
                    "primary_surface": row.get("primary_surface", ""),
                    "primary_surface_mask": SURFACE_MASKS.get(row.get("primary_surface", ""), ""),
                    "ending_pressure": row.get("ending_pressure", ""),
                    "packet_count": len(packets),
                    "packet_ids": ";".join(packets),
                    "required_packet_count": len(required),
                    "required_packet_ids": ";".join(required),
                    "unknown_packet_ids": ";".join(missing_packets),
                    "unknown_required_packet_ids": ";".join(missing_required),
                    "status": "|".join(status),
                })

    write_csv(
        AUDIT / "route_card_runtime_matrix.csv",
        [
            "source_file", "line", "route_card_id", "phase_id", "depth_min_m", "depth_max_m",
            "primary_surface", "primary_surface_mask", "ending_pressure", "packet_count",
            "packet_ids", "required_packet_count", "required_packet_ids", "unknown_packet_ids",
            "unknown_required_packet_ids", "status",
        ],
        route_rows_out,
    )
    return total_rows, direct_packet_coverage, status_counts


def build_binding_matrix(packet_set: set[str]) -> tuple[int, set[str], Counter[str]]:
    binding_dir = ROOT / "Docs" / "Lore" / "AppliedContent" / "binding_maps"
    binding_sources = sorted(binding_dir.glob("*.csv"), key=lambda path: path.name.lower())
    rows_out: list[dict[str, object]] = []
    status_counts: Counter[str] = Counter()
    packet_coverage: set[str] = set()
    total_rows = 0

    for source in binding_sources:
        with source.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            for line_number, row in enumerate(reader, start=2):
                total_rows += 1
                packet_id = row.get("packet_id") or row.get("cluster_packet_id") or ""
                if packet_id:
                    packet_coverage.add(packet_id)
                component = row.get("authoring_component") or row.get("primary_component") or ""
                field = row.get("serialized_field") or row.get("primary_field") or ""
                secondary_component = row.get("secondary_component") or ""
                secondary_field = row.get("secondary_field") or ""
                checks: list[str] = []
                if not packet_id:
                    checks.append("NO_PACKET_ID")
                elif packet_id not in packet_set:
                    checks.append("UNKNOWN_PACKET_ID")
                expected_hash = fnv1a32(packet_id) if packet_id else 0
                supplied_hash = parse_hash(
                    row.get("packet_hash_hex", "")
                    or row.get("packet_hash_decimal", "")
                    or row.get("packet_hash_uint", "")
                )
                if packet_id and supplied_hash is not None and supplied_hash != expected_hash:
                    checks.append("HASH_MISMATCH")
                if (component or field) and (component, field) not in KNOWN_HOOKS:
                    checks.append("UNKNOWN_PRIMARY_HOOK")
                if (secondary_component or secondary_field) and (secondary_component, secondary_field) not in KNOWN_HOOKS:
                    checks.append("UNKNOWN_SECONDARY_HOOK")

                missing_paths: list[str] = []
                for key in ("approved_template_prefab", "source_prefab"):
                    value = row.get(key, "")
                    if value and not exists_project_path(value):
                        missing_paths.append(value)

                candidates = split_semicolon(row.get("primary_target_candidates", "")) + split_semicolon(row.get("secondary_target_candidates", ""))
                existing_candidates = sum(1 for value in candidates if exists_project_path(value))
                if candidates and existing_candidates == 0:
                    checks.append("NO_EXISTING_CANDIDATE_PATH")
                if missing_paths:
                    checks.append("MISSING_REFERENCED_PATH")
                if not checks:
                    checks.append("OK")
                status_counts.update(checks)
                rows_out.append({
                    "source_file": rel(source),
                    "line": line_number,
                    "map_type": source.name,
                    "packet_id": packet_id,
                    "packet_known": "yes" if packet_id in packet_set else "no",
                    "expected_packet_hash_hex": f"0x{expected_hash:08X}" if packet_id else "",
                    "supplied_packet_hash": row.get("packet_hash_hex", "") or row.get("packet_hash_decimal", "") or row.get("packet_hash_uint", ""),
                    "primary_component": component,
                    "primary_field": field,
                    "primary_hook_known": "yes" if (component, field) in KNOWN_HOOKS else "no",
                    "secondary_component": secondary_component,
                    "secondary_field": secondary_field,
                    "secondary_hook_known": "yes" if (secondary_component, secondary_field) in KNOWN_HOOKS else ("n/a" if not secondary_component and not secondary_field else "no"),
                    "existing_candidate_paths": existing_candidates,
                    "missing_referenced_paths": ";".join(missing_paths),
                    "status": "|".join(checks),
                })

    write_csv(
        AUDIT / "binding_map_runtime_matrix.csv",
        [
            "source_file", "line", "map_type", "packet_id", "packet_known",
            "expected_packet_hash_hex", "supplied_packet_hash", "primary_component",
            "primary_field", "primary_hook_known", "secondary_component", "secondary_field",
            "secondary_hook_known", "existing_candidate_paths", "missing_referenced_paths", "status",
        ],
        rows_out,
    )
    return total_rows, packet_coverage, status_counts


def write_reports() -> None:
    packet_csv_path = ROOT / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    packet_rows = read_csv(packet_csv_path)
    packet_ids = sorted({row["packet_id"] for row in packet_rows if row.get("packet_id")})
    packet_set = set(packet_ids)
    locale_counts = Counter(row.get("locale", "") for row in packet_rows)
    draft_rows = sum(1 for row in packet_rows if (parse_hash(row.get("flags", "0")) or 0) & 1)
    by_packet: dict[str, set[str]] = defaultdict(set)
    for row in packet_rows:
        by_packet[row["packet_id"]].add(row["locale"])
    packet_locale_missing = [
        (packet_id, ";".join(sorted(set(TARGET_LOCALES) - by_packet[packet_id])))
        for packet_id in packet_ids
        if set(TARGET_LOCALES) - by_packet[packet_id]
    ]

    hash_path = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8AppliedLoreHashes.cs"
    hash_text = hash_path.read_text(encoding="utf-8")
    packet_constant_matches = re.findall(r"public const uint (P\d{3}_[A-Za-z0-9_]+) = 0x([0-9A-Fa-f]{8})u;", hash_text)
    locale_constant_matches = re.findall(r"public const uint Locale_([A-Za-z]{2}_[A-Za-z]{2}) = 0x([0-9A-Fa-f]{8})u;", hash_text)
    surface_constant_matches = re.findall(r"public const uint (SurfaceMask[A-Za-z]+) = 1u << ([0-9]+);", hash_text)
    row_flag_matches = re.findall(r"public const uint (RowFlag[A-Za-z]+) = 1u << ([0-9]+);", hash_text)
    packet_hash_constants = {name: int(value, 16) for name, value in packet_constant_matches}

    route_total_rows, route_packet_coverage, route_status_counts = build_route_matrix(packet_set)
    binding_total_rows, binding_packet_coverage, binding_status_counts = build_binding_matrix(packet_set)

    route_export_path = ROOT / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_route_cards.csv"
    route_export_rows = read_csv(route_export_path) if route_export_path.exists() else []

    blob_path = ROOT / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"
    blob_exists = blob_path.exists()
    blob_bytes = blob_path.stat().st_size if blob_exists else 0
    blob_magic = ""
    blob_format = ""
    blob_sections = ""
    blob_checksum = ""
    if blob_exists and blob_bytes >= 64:
        data = blob_path.read_bytes()[:64]
        magic, fmt, _header_bytes, checksum, _total_bytes, _directory_bytes, section_count = struct.unpack_from("<IHHQIII", data, 0)
        blob_magic = f"0x{magic:08X}"
        blob_format = str(fmt)
        blob_sections = str(section_count)
        blob_checksum = f"0x{checksum:016X}"

    route_dir = ROOT / "Docs" / "Lore" / "AppliedContent" / "route_cards"
    binding_dir = ROOT / "Docs" / "Lore" / "AppliedContent" / "binding_maps"
    route_sources = sorted(route_dir.glob("*_route_cards.csv"), key=lambda path: path.name.lower())
    binding_sources = sorted(binding_dir.glob("*.csv"), key=lambda path: path.name.lower())

    write_csv(
        AUDIT / "current_runtime_artifact_inventory.csv",
        [
            "artifact", "path", "status", "packet_count", "localized_row_count",
            "locale_count", "route_count", "hash_constant_count", "draft_localization_rows", "notes",
        ],
        [
            {
                "artifact": "AppliedLore packet CSV",
                "path": rel(packet_csv_path),
                "status": "present",
                "packet_count": len(packet_ids),
                "localized_row_count": len(packet_rows),
                "locale_count": len(locale_counts),
                "draft_localization_rows": draft_rows,
                "notes": "15-locale matrix complete" if not packet_locale_missing else "locale gaps present",
            },
            {
                "artifact": "Generated AppliedLore hashes",
                "path": rel(hash_path),
                "status": "present",
                "packet_count": len(packet_hash_constants),
                "locale_count": len(locale_constant_matches),
                "hash_constant_count": len(packet_hash_constants) + len(locale_constant_matches) + len(surface_constant_matches) + len(row_flag_matches),
                "notes": f"surface_masks={len(surface_constant_matches)} row_flags={len(row_flag_matches)}",
            },
            {
                "artifact": "Route-card source CSV directory",
                "path": rel(route_dir),
                "status": "present",
                "packet_count": len(route_packet_coverage),
                "route_count": route_total_rows,
                "notes": f"files={len(route_sources)} unknown_packet_ids={route_status_counts.get('UNKNOWN_PACKET_ID', 0)}",
            },
            {
                "artifact": "Baked route-card source export",
                "path": rel(route_export_path),
                "status": "present" if route_export_path.exists() else "missing",
                "route_count": len(route_export_rows),
                "notes": "DataMonolith Narrative source table",
            },
            {
                "artifact": "Binding map directory",
                "path": rel(binding_dir),
                "status": "present",
                "packet_count": len(binding_packet_coverage),
                "notes": f"files={len(binding_sources)} rows={binding_total_rows} ok_rows={binding_status_counts.get('OK', 0)}",
            },
            {
                "artifact": "DataMonolith H8BIN",
                "path": rel(blob_path),
                "status": "stale_after_source_generation" if blob_exists else "missing",
                "packet_count": 6900,
                "localized_row_count": 6900,
                "locale_count": 15,
                "route_count": 454,
                "notes": f"bytes={blob_bytes} magic={blob_magic} format={blob_format} sections={blob_sections} checksum={blob_checksum}; full_after_page_export_failed=P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP scanner length mismatch",
            },
        ],
    )

    tool_inventory = f"""# AppliedLore Tool Route Inventory

Evidence class: STATIC_SOURCE / CLI_HELP / CLI_AUDIT

## Tools

| Tool | Purpose | Inputs | Outputs | Required args | Safe command |
|---|---|---|---|---|---|
| `Tools/AppliedLoreImporter.py` | Imports AppliedContent packet JSON/manifests into DataMonolith Narrative CSV and generated hash constants. | `Docs/Lore/AppliedContent/release_sets/*_manifest.json`; packet JSON referenced by manifests. | `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`; `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`. | optional `--root ROOT`. | `python Tools/AppliedLoreImporter.py --root .` is deterministic but write-capable; no dry-run/help-only mode except `--help`. |
| `Tools/AppliedLorePageExporter.py` | Exports localized packet fields into publication Markdown pages and publication indexes. | Packet JSON/manifests; optional external article bodies referenced by packets; `graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv`. | `Docs/Lore/AppliedContent/in_game_wiki/<locale>/*.md`; `external_site/<locale>/*.md`; `INDEX.md`; `Localization_Status_Index.md`; `Publication_Surface_Index.csv`; `Publication_Cluster_Index.csv`. | optional `--root ROOT`; optional `--overwrite`. | `python Tools/AppliedLorePageExporter.py --root . --overwrite` is write-capable; run only when generated packet source has changed and source audit proves publication frontmatter/index drift. |
| `Tools/AppliedLoreRouteCardExporter.py` | Exports checked route-card CSVs into DataMonolith Narrative route source table with route/phase/surface/packet hashes. | `Docs/Lore/AppliedContent/route_cards/*_route_cards.csv`; `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`. | `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`. | optional `--root ROOT`. | `python Tools/AppliedLoreRouteCardExporter.py --root .` is deterministic but write-capable; no dry-run/help-only mode except `--help`. |
| `Tools/AppliedLoreRuntimeAudit.py` | Offline validation of AppliedLore source, generated hashes, binding maps, route cards, publication pages, serialized bindings, world scene markers, and optional H8BIN records. | AppliedLore CSV/hash source, route cards, binding maps, publication pages, `02_HECTON_WORLD.unity`, prefabs, and `static_data.h8bin` when not `--source-only`. | Console audit line only; captured by this task to `runtime_audit_source_only.txt` and `runtime_audit_full.txt`. | optional `--root ROOT`; optional `--source-only`. | `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` is read-only and safe. Full audit is also read-only and was safe here. |

## Help Output Captured

- `python Tools/AppliedLoreImporter.py --help`
- `python Tools/AppliedLorePageExporter.py --help`
- `python Tools/AppliedLoreRouteCardExporter.py --help`
- `python Tools/AppliedLoreRuntimeAudit.py --help`

## Safe Command Decision - Task 05

- Run source-only audit: VERIFIED SAFE. It reads source/export artifacts and produced `runtime_audit_source_only.txt`.
- Run full offline audit: VERIFIED SAFE. It reads `static_data.h8bin` and produced `runtime_audit_full.txt`.
- Run importer: CANDIDATE SAFE, write-capable, owned outputs only. Run only with immediate diff inspection.
- Run route-card exporter: CANDIDATE SAFE, write-capable, owned output only. Run only with immediate diff inspection.
- Run page exporter: RUN AFTER POST-IMPORT AUDIT FAILURE. It has no dry-run, but source audit proved 170 generated frontmatter mismatches after importer output changed. `--overwrite` repaired generated page/index drift.
- Run Unity bake/build/editor placement: BLOCKED for this pass. Not required for static proof; would risk parallel-agent scene churn.
"""
    (AUDIT / "tool_route_inventory.md").write_text(tool_inventory, encoding="utf-8", newline="\n")

    missing_hash_packets = [packet for packet in packet_ids if packet not in packet_hash_constants]
    extra_hash_packets = [packet for packet in packet_hash_constants if packet not in packet_set]
    missing_route_packets = sorted(packet_set - route_packet_coverage)
    route_non_ok = {key: value for key, value in route_status_counts.items() if key != "OK" and value}
    binding_non_ok = {key: value for key, value in binding_status_counts.items() if key != "OK" and value}

    locale_lines = [f"- `{locale}`: rows={locale_counts.get(locale, 0)}" for locale in TARGET_LOCALES]
    blockers = [
        "# Runtime Blockers - 1778",
        "",
        "Evidence class: STATIC_SOURCE / CLI_AUDIT",
        "",
        "## Blocking Findings",
    ]
    if not missing_hash_packets and not extra_hash_packets and not packet_locale_missing and not missing_route_packets and not route_non_ok:
        blockers.append("- No packet-level source blocker found for CSV locale roster, generated packet hashes, route-card packet coverage, supported route-card surfaces, or publication page/index generation.")
    else:
        if missing_hash_packets:
            blockers.append("- Missing generated packet hashes: " + ";".join(missing_hash_packets[:50]))
        if extra_hash_packets:
            blockers.append("- Extra generated packet hashes not in CSV: " + ";".join(extra_hash_packets[:50]))
        if packet_locale_missing:
            blockers.append("- Locale matrix gaps: " + ";".join(f"{packet}:{missing}" for packet, missing in packet_locale_missing[:20]))
        if missing_route_packets:
            blockers.append("- Packets missing direct route-card coverage: " + ";".join(missing_route_packets[:50]))
        if route_non_ok:
            blockers.append("- Route-card matrix non-OK statuses: " + str(route_non_ok))
    blockers.extend([
        "",
        "## Residual Integration Gates",
        "- Current `static_data.h8bin` is stale after source generation: post-page-export full audit failed at `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` scanner length (`csv=88`, `blob=71`). Run the DataMonolith bake before claiming binary runtime parity.",
        "- Scene placement is incomplete: source-only audit reports `scene_bindings=7`, `prefab_bindings=42`, `authoring_bindings=49`, `scene_placement_covered_rows=34`, while packet count is `460`.",
        "- Manual placement backlog remains: `manual_policy_rows=374`, split as `manual_terminal_policy_rows=27` and `manual_discovery_policy_rows=347`.",
        "- Terminal policy prefabs and TerminalOS slots are present for 27 terminal rows, but Unity scene placement still requires the documented editor menu on the loaded world scene.",
        "- Unity Editor import, Play Mode, player build, profiler, and actual PDA/terminal/scanner runtime proof were not run in this pass.",
        "- `AppliedLorePageExporter.py --root . --overwrite` was run because importer output changed draft flags and publication Markdown/index metadata became stale.",
        "",
        "## Locale Roster",
        f"- Fixed roster count: {len(TARGET_LOCALES)}.",
        f"- Draft localization rows flagged in CSV: {draft_rows}. These rows are baked with flags; they are native-review risk, not a source-route blocker in current audit.",
        *locale_lines,
        "",
        "## Binding Matrix Notes",
        f"- Binding matrix rows: {binding_total_rows}. OK rows: {binding_status_counts.get('OK', 0)}.",
        f"- Non-OK binding statuses: {binding_non_ok if binding_non_ok else 'none'}.",
    ])
    (AUDIT / "runtime_blockers.md").write_text("\n".join(blockers) + "\n", encoding="utf-8", newline="\n")

    recipe = f"""# DataMonolith Integration Recipe - AppliedLore 1778

Evidence class: STATIC_SOURCE / CLI_AUDIT / H8BIN_OFFLINE_PARSE

## Current Route

1. VERIFIED: Authoring packets live under `Docs/Lore/AppliedContent/packets/*.packets.json` and are referenced by `Docs/Lore/AppliedContent/release_sets/*_manifest.json`.
2. VERIFIED: `Tools/AppliedLoreImporter.py --root .` collects packet JSON, enforces the 15-locale roster, strips draft-review prose markers from player-visible text, writes `applied_lore_packets.csv`, and writes `H8AppliedLoreHashes.cs`.
3. VERIFIED: `Tools/AppliedLoreRouteCardExporter.py --root .` reads `Docs/Lore/AppliedContent/route_cards/*_route_cards.csv`, validates packet IDs against `applied_lore_packets.csv`, and writes `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`.
4. VERIFIED: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs` runs the AppliedLore importer and route-card exporter before bake, then parses `applied_lore_packets.csv` and `applied_lore_route_cards.csv` into `H8AppliedLorePacketRecord` and `H8AppliedLoreRouteRecord` sections.
5. VERIFIED: AppliedLore runtime sections are `H8DataSectionId.AppliedLorePackets = 27` and `AppliedLoreRoutes = 28`; both use fixed 128-byte records.
6. VERIFIED PRE-GENERATION: Offline full audit initially passed against `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` with `applied_records=6900` and `applied_routes=454`.
7. VERIFIED SOURCE: After importer, route-card exporter, and page exporter, source-only audit passed with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, and `publication_cluster_rows=150`.
8. BLOCKED: Current `static_data.h8bin` is stale after source generation. Post-page-export full audit failed at `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` scanner length (`csv=88`, `blob=71`).
9. BLOCKED: Runtime readiness still needs Unity import, Play Mode load, player-build package inclusion, profiler/GC proof, and actual UI/scanner/terminal interaction proof.

## Current Counts

- Packet IDs: {len(packet_ids)}.
- Localized CSV rows: {len(packet_rows)}.
- Locales per packet: {len(TARGET_LOCALES)}.
- Draft localization rows: {draft_rows}.
- Route-card source rows: {route_total_rows}.
- Baked route export rows: {len(route_export_rows)}.
- H8BIN AppliedLore packet records: 6900 in stale binary.
- H8BIN AppliedLore route records: 454 in stale binary.

## Runtime Boundary

- Runtime reads `H8AppliedLorePacketRecord`, `H8AppliedLoreRouteRecord`, UTF-8 byte slices, hashes, masks, flags, and SignalBus payloads.
- Runtime must not parse Markdown, packet JSON, publication indexes, authoring CSV, or localization dictionaries for AppliedLore.
- Publication Markdown is website/wiki output only; frontmatter explicitly states `runtime_reads_markdown: false`.

## Scalability Consequences

- Low: static binary lookup and low terminal-preview signal budget (`AppliedLoreTerminalPreviewSignal.LowTierFrameSignals = 8`) preserve zero-GC UI route.
- Middle: same binary route, more frame headroom for PDA metadata seeding and route prerequisite checks outside hot parser paths.
- High: richer terminal/PDA presentation can consume the same records without changing gameplay truth ownership.
- Ultra: visual-overkill terminal/codex presentation must remain a read-only observer of baked records and signals; no runtime Markdown/JSON parser is allowed.
"""
    (AUDIT / "datamonolith_integration_recipe.md").write_text(recipe, encoding="utf-8", newline="\n")

    surface_map = """# Runtime Surface Binding Map - AppliedLore 1778

Evidence class: STATIC_SOURCE / CLI_AUDIT

## PDA Encyclopedia

VERIFIED: `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs` can seed metadata from `H8AppliedLoreRuntime.GetPacketRecords()`, resolve active locale through `H8AppliedLoreRuntime.ResolveLocaleHash`, fetch surfaces through `TryGetAppliedLoreUtf8`, write display text through `TryWriteAppliedLoreSurfaceUtf16`, and gate entries through `TryFindRouteForPacket` plus route prerequisite hashes.

Runtime role: PDA encyclopedia is a read-only consumer of baked AppliedLore packet records and route records. It should not read publication Markdown.

## Scanner Title Route

VERIFIED: `Assets/_Project/Scripts/ScannableTarget.cs` calls `H8AppliedLoreRuntime.TryWriteTitleUtf16(hash, H8AppliedLoreRuntime.DefaultLocaleHash, destination, out written)` for title text. `ScannableFragment` publishes staged AppliedLore unlocks at 25/50/100 percent scan progress through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`.

Runtime role: scanner surfaces use baked packet hashes and localized title/string records. Scan completion may also publish world-impact signals for selected packet hashes.

## MessageTerminal

VERIFIED: `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs` has serialized `appliedLorePacketHash`, `appliedLoreLocaleHash`, `terminalOsPreviewIndex`, `terminalOsPreviewHash`, and `terminalOsPreviewSurface`. It resolves terminal/audio/title surfaces through `H8AppliedLoreRuntime.TryGetUtf8` and `TryWriteSurfaceUtf16`, and unlocks packets through `TryRaisePacketUnlockedAt`.

Runtime role: MessageTerminal is a diegetic terminal binding surface. Current source-only audit proves 27 terminal-policy prefabs and 27 TerminalOS preview slots, but scene placement is not complete.

## TerminalOS Preview Line

VERIFIED: `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` defines unmanaged `AppliedLoreTerminalPreviewSignal` as a 32-byte SignalBus payload with continuous low-tier/max-frame capacity fields. `TerminalOsRuntime` configures the lane, consumes frame snapshots, resolves packet UTF-8 through `H8AppliedLoreRuntime.TryGetUtf8`, and writes the preview line into terminal state.

Runtime role: TerminalOS is a visual-sync consumer. It receives packet/locale/surface hashes through SignalBus, not through scene search or parser reads.

## NarrativeDiscovery

VERIFIED: `Assets/_Project/Scripts/NarrativeDiscovery.cs` has serialized `appliedLorePacketHash`, exposes `AppliedLorePacketHash`, publishes unlocks through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`, and emits `NarrativeSpatialTriggerAuthoring` with `AppliedLoreHash` when AUP trigger data is valid.

Runtime role: NarrativeDiscovery is the main world-prop binding surface for manual placement rows. Current backlog is 347 discovery manual rows not yet fully scene-serialized.

## ScannableFragment

VERIFIED: `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs` has `appliedLoreQuarterPacketHash`, `appliedLoreHalfPacketHash`, and `appliedLoreFinalPacketHash`. It raises packet unlocks at scan progress thresholds using AUP and scan recon kind.

Runtime role: ScannableFragment supports staged scanner/codex packet unlocks without runtime authoring-file parsing.

## NarrativeSpatialTriggerAuthoring

VERIFIED: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` defines `NarrativeSpatialTriggerAuthoring` with `AppliedLoreHash` at fixed field offset. `NarrativeDiscovery.TryGetSpatialTrigger` fills that field from `appliedLorePacketHash`; `HectonNarrativeDirector_PoiTriggers.cs` consumes the trigger and can publish AppliedLore unlocks/world-impact presentation from AUP trigger data.

Runtime role: AUP proximity trigger path is an authored struct route, not a Markdown/JSON route.

## Current Binding Coverage

- Packets in source: 460.
- Binding map packet coverage: 460.
- Scene bindings: 7.
- Prefab bindings: 42.
- Total authoring bindings found by audit: 49.
- Placement plan rows: 374, with 27 terminal rows and 347 discovery rows.

Conclusion: runtime surfaces exist and are parser-free; scene placement coverage remains the production blocker.
"""
    (AUDIT / "runtime_surface_binding_map.md").write_text(surface_map, encoding="utf-8", newline="\n")

    handoff = f"""# HANDOFF 1778 - Applied Lore DataMonolith Integrator

Evidence class: STATIC_SOURCE / CLI_AUDIT / H8BIN_OFFLINE_PARSE

## Verified

- Post-page-export source-only audit passed: `packets=460`, `rows=6900`, `route_cards=454`, `binding_map_rows=460`, `placement_plan_rows=374`, `publication_frontmatter_pages=13800`, `authoring_bindings=49`.
- Pre-generation full offline H8BIN audit passed: `blob_bytes=3300608`, `applied_records=6900`, `applied_routes=454`.
- Post-generation full offline H8BIN audit failed at `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` scanner length (`csv=88`, `blob=71`), proving the binary now needs a DataMonolith bake.
- 15-locale roster is intact.
- Generated packet hash constants cover 460 packet IDs and 15 locale IDs.
- Route-card source coverage reaches all 460 packet IDs through 454 route-card rows.

## Blockers

- Unity scene placement is incomplete: only 7 scene bindings and 42 prefab bindings are serialized, while 460 packets exist.
- Manual placement backlog remains: 374 policy rows, including 347 NarrativeDiscovery world-prop rows and 27 terminal-anchor rows.
- Current `static_data.h8bin` is stale after source generation; bake through `Hecton8/Data Monolith/Bake Static Data` before runtime parity claims.
- No Unity Editor import, Play Mode, player build, profiler, PDA UI, scanner UI, or terminal interaction proof was produced in this pass.

## Next-Wave Tasks

1. After parallel content churn settles, rerun `python Tools/AppliedLoreImporter.py --root .`, `python Tools/AppliedLoreRouteCardExporter.py --root .`, and `python Tools/AppliedLorePageExporter.py --root . --overwrite`, then inspect diffs.
2. In Unity, open `Assets/_Project/Scenes/02_HECTON_WORLD.unity` and run `Hecton8/Lore/Apply Applied Lore Scene Placement Plan` from the loaded scene. Do not raw-edit YAML.
3. Rerun `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only`; target is increased `scene_bindings` and `scene_placement_covered_rows`.
4. Bake static data through `Hecton8/Data Monolith/Bake Static Data` after source outputs settle; rerun full audit and H8BIN header parse.
5. Produce Play Mode proof for PDA encyclopedia, scanner title route, MessageTerminal, TerminalOS preview line, NarrativeDiscovery, ScannableFragment, and NarrativeSpatialTriggerAuthoring unlock paths.

## Files Produced By 1778

- `Docs/Lore/AppliedContent/production_audits/1778/tool_route_inventory.md`
- `Docs/Lore/AppliedContent/production_audits/1778/current_runtime_artifact_inventory.csv`
- `Docs/Lore/AppliedContent/production_audits/1778/runtime_audit_source_only.txt`
- `Docs/Lore/AppliedContent/production_audits/1778/runtime_audit_full.txt`
- `Docs/Lore/AppliedContent/production_audits/1778/route_card_runtime_matrix.csv`
- `Docs/Lore/AppliedContent/production_audits/1778/binding_map_runtime_matrix.csv`
- `Docs/Lore/AppliedContent/production_audits/1778/runtime_blockers.md`
- `Docs/Lore/AppliedContent/production_audits/1778/datamonolith_integration_recipe.md`
- `Docs/Lore/AppliedContent/production_audits/1778/runtime_surface_binding_map.md`
"""
    (ROOT / "Docs" / "AgentLogs" / "HANDOFF_1778.md").write_text(handoff, encoding="utf-8", newline="\n")

    print("wrote_1778_audit_artifacts")
    print(f"packets={len(packet_ids)} rows={len(packet_rows)} draft_rows={draft_rows} route_rows={route_total_rows} binding_rows={binding_total_rows}")
    print(f"route_statuses={dict(route_status_counts)}")
    print(f"binding_statuses={dict(binding_status_counts)}")


if __name__ == "__main__":
    write_reports()
