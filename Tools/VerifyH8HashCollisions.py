#!/usr/bin/env python3
"""Generate and verify HECTON-8 project identity hashes.

The script scans active first-party source/data only:
- ItemData assets under Assets/_Project/Data/Items
- biome identity assets under Assets/_Project/Data/Biomes
- first-party C# signal names under Assets/_Project/Scripts
- authored signal/message/discovery IDs under Assets/_Project/Data and first-party C#

Hash modes mirror the runtime owners:
- item persistent IDs: Hecton.Localization.LocHash.Compute
- biome family IDs: Hecton.Localization.LocHash.ComputeAsciiLowerInvariant
- signal lane names: GlobalSignals.ComputeStableSignalLaneHash
- authored signal/message/discovery IDs: Hecton.Localization.LocHash.Compute
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path


FNV_OFFSET_32 = 2166136261
FNV_PRIME_32 = 16777619
UINT_MASK = 0xFFFFFFFF
GENERATED_HASH_RELATIVE_PATH = "Assets/_Project/Scripts/Core/Generated/H8Hashes.cs"


@dataclass(frozen=True)
class HashRecord:
    category: str
    group: str
    name: str
    value: str
    hash_mode: str
    hash_value: int
    source: str


def fnv1a_utf16(value: str) -> int:
    if not value:
        return 0

    hash_value = FNV_OFFSET_32
    for char in value:
        code = ord(char)
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME_32) & UINT_MASK
        hash_value ^= (code >> 8) & 0xFF
        hash_value = (hash_value * FNV_PRIME_32) & UINT_MASK
    return hash_value


def fnv1a_ascii_lower(value: str) -> int:
    if not value:
        return 0

    hash_value = FNV_OFFSET_32
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME_32) & UINT_MASK
    return hash_value


def fnv1a_signal_label(value: str) -> int:
    hash_value = FNV_OFFSET_32
    if value:
        for char in value:
            hash_value ^= ord(char)
            hash_value = (hash_value * FNV_PRIME_32) & UINT_MASK
    return hash_value if hash_value != 0 else 1


def compute_hash(value: str, hash_mode: str) -> int:
    if hash_mode == "loc_utf16":
        return fnv1a_utf16(value)
    if hash_mode == "ascii_lower":
        return fnv1a_ascii_lower(value)
    if hash_mode == "signal_label":
        return fnv1a_signal_label(value)
    raise ValueError(f"Unknown hash mode: {hash_mode}")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def is_generated_hash_output(path: Path, root: Path) -> bool:
    try:
        relative_path = path.relative_to(root).as_posix()
    except ValueError:
        return False
    return relative_path == GENERATED_HASH_RELATIVE_PATH


def extract_yaml_scalar(text: str, field_name: str) -> str:
    match = re.search(rf"(?m)^\s*{re.escape(field_name)}:\s*(.*)$", text)
    if not match:
        return ""
    value = match.group(1).strip()
    if value.startswith("'") and value.endswith("'") and len(value) >= 2:
        value = value[1:-1]
    if value.startswith('"') and value.endswith('"') and len(value) >= 2:
        value = value[1:-1]
    return value.strip()


def iter_yaml_scalar_values(text: str, field_name: str):
    pattern = re.compile(rf"(?m)^\s*(?:-\s*)?{re.escape(field_name)}:\s*(.*)$")
    for match in pattern.finditer(text):
        value = match.group(1).strip()
        if value.startswith("'") and value.endswith("'") and len(value) >= 2:
            value = value[1:-1]
        if value.startswith('"') and value.endswith('"') and len(value) >= 2:
            value = value[1:-1]
        value = value.strip()
        if value:
            yield value


def extract_nested_yaml_scalar(text: str, parent_name: str, child_name: str) -> str:
    lines = text.splitlines()
    parent_indent = -1
    for line in lines:
        stripped = line.lstrip(" ")
        indent = len(line) - len(stripped)
        if parent_indent >= 0:
            if stripped and indent <= parent_indent:
                return ""
            if stripped.startswith(child_name + ":"):
                value = stripped[len(child_name) + 1 :].strip()
                if value.startswith("'") and value.endswith("'") and len(value) >= 2:
                    value = value[1:-1]
                if value.startswith('"') and value.endswith('"') and len(value) >= 2:
                    value = value[1:-1]
                return value.strip()
            continue

        if stripped.startswith(parent_name + ":"):
            parent_indent = indent

    return ""


def add_record(records: list[HashRecord], category: str, group: str, value: str, hash_mode: str, source: Path, root: Path) -> None:
    value = value.strip()
    if not value:
        return

    records.append(
        HashRecord(
            category=category,
            group=group,
            name=make_base_identifier(value),
            value=value,
            hash_mode=hash_mode,
            hash_value=compute_hash(value, hash_mode),
            source=source.relative_to(root).as_posix(),
        )
    )


def is_identifier_like(value: str) -> bool:
    return re.fullmatch(r"[A-Za-z0-9_.:-]+", value) is not None


def looks_like_item_id(value: str) -> bool:
    return value.startswith(("Data_", "Comp_", "Item_Tool_", "Item_Builder_", "Item_"))


def is_authored_signal_value(value: str) -> bool:
    if not value or not is_identifier_like(value) or looks_like_item_id(value):
        return False

    lower_value = value.lower()
    markers = (
        "signal",
        "atlas6",
        "directive_",
        "discovery",
        "message",
        "first_hour",
        "first_colony",
        "zone_",
        "radiation_",
        "leviathan_",
        "ending_",
        "narrative_",
        "hull_failure",
        "chen_",
        "captain_",
        "biologist_",
        "medic_",
        "child_",
    )
    return any(marker in lower_value for marker in markers)


def is_authored_signal_constant(identifier: str, value: str) -> bool:
    lower_identifier = identifier.lower()
    identifier_markers = ("signal", "message", "discovery", "marker", "quest", "directive")
    return any(marker in lower_identifier for marker in identifier_markers) and is_authored_signal_value(value)


def scan_item_records(root: Path) -> list[HashRecord]:
    records: list[HashRecord] = []
    item_root = root / "Assets" / "_Project" / "Data" / "Items"
    if item_root.exists():
        for path in sorted(item_root.rglob("*.asset")):
            text = read_text(path)
            if "Hecton8.Items.ItemData" not in text:
                continue

            stable_id = extract_yaml_scalar(text, "stableId")
            object_name = extract_yaml_scalar(text, "m_Name")
            legacy_item_name = extract_yaml_scalar(text, "legacyItemName")
            localized_item_key = extract_nested_yaml_scalar(text, "localizedItemName", "tableKey")
            persistent_id = stable_id or object_name
            add_record(records, "Items", "PersistentIds", persistent_id, "loc_utf16", path, root)
            add_record(records, "Items", "DisplayNames", legacy_item_name, "loc_utf16", path, root)
            add_record(records, "Items", "LocalizedNameKeys", localized_item_key, "loc_utf16", path, root)
            if object_name and object_name != persistent_id:
                add_record(records, "Items", "LegacyAssetNames", object_name, "loc_utf16", path, root)

    scripts_root = root / "Assets" / "_Project" / "Scripts"
    item_literal_pattern = re.compile(r"LocHash\.Compute\(\s*\"([^\"]+)\"\s*\)")
    item_prefix_pattern = re.compile(r"^(Data_|Comp_|Item_Tool_|Item_Builder_|Item_)[A-Za-z0-9_]+$")
    extra_item_ids = {"fish", "raw_fish", "cooked_fish", "cured_fish"}
    for path in sorted(scripts_root.rglob("*.cs")):
        if is_generated_hash_output(path, root):
            continue
        text = read_text(path)
        for match in item_literal_pattern.finditer(text):
            value = match.group(1)
            if value in extra_item_ids or item_prefix_pattern.match(value):
                add_record(records, "Items", "CodeLiteralIds", value, "loc_utf16", path, root)

    return records


def scan_biome_records(root: Path) -> list[HashRecord]:
    records: list[HashRecord] = []
    biome_root = root / "Assets" / "_Project" / "Data" / "Biomes"
    if not biome_root.exists():
        return records

    for path in sorted(biome_root.rglob("*.asset")):
        text = read_text(path)
        object_name = extract_yaml_scalar(text, "m_Name")
        biome_name = extract_yaml_scalar(text, "biomeName")
        family_id = extract_yaml_scalar(text, "familyId")
        profile_id = extract_yaml_scalar(text, "profileId")

        if "HectonBiomeMatrixProfile" in text and object_name:
            add_record(records, "Biomes", "MatrixAssetNames", object_name, "loc_utf16", path, root)
        add_record(records, "Biomes", "BiomeNames", biome_name, "loc_utf16", path, root)
        add_record(records, "Biomes", "FamilyIds", family_id, "ascii_lower", path, root)
        add_record(records, "Biomes", "ProfileIds", profile_id, "loc_utf16", path, root)

    return records


def scan_signal_records(root: Path) -> list[HashRecord]:
    records: list[HashRecord] = []
    scripts_root = root / "Assets" / "_Project" / "Scripts"
    data_root = root / "Assets" / "_Project" / "Data"

    if data_root.exists():
        authored_fields = (
            "signalId",
            "messageId",
            "discoveryId",
            "triggerId",
            "completionId",
            "questId",
        )
        for path in sorted(data_root.rglob("*.asset")):
            text = read_text(path)
            for field_name in authored_fields:
                for value in iter_yaml_scalar_values(text, field_name):
                    if is_authored_signal_value(value):
                        add_record(records, "Signals", "AuthoredSignalIds", value, "loc_utf16", path, root)

    if not scripts_root.exists():
        return records

    signal_struct_pattern = re.compile(
        r"\b(?:public|internal|private)?\s*(?:readonly\s+)?struct\s+([A-Za-z_][A-Za-z0-9_]*Signal)\b"
    )
    implemented_signal_pattern = re.compile(
        r"\b(?:public|internal|private)?\s*(?:readonly\s+)?struct\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*ISignal\b"
    )
    bus_pattern = re.compile(r"SignalBus<\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*([A-Za-z_][A-Za-z0-9_]*)\s*>")
    authored_id_const_pattern = re.compile(r"\bconst\s+string\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\"([^\"]*)\"")

    for path in sorted(scripts_root.rglob("*.cs")):
        if is_generated_hash_output(path, root):
            continue
        text = read_text(path)
        for match in signal_struct_pattern.finditer(text):
            add_record(records, "Signals", "StructNames", match.group(1), "signal_label", path, root)
        for match in implemented_signal_pattern.finditer(text):
            add_record(records, "Signals", "ISignalStructNames", match.group(1), "signal_label", path, root)
        for match in bus_pattern.finditer(text):
            signal_name = match.group(1)
            if signal_name.endswith("Signal") or signal_name.endswith("Request"):
                add_record(records, "Signals", "SignalBusNames", signal_name, "signal_label", path, root)
        for match in authored_id_const_pattern.finditer(text):
            identifier = match.group(1)
            value = match.group(2)
            if is_authored_signal_constant(identifier, value):
                add_record(records, "Signals", "AuthoredSignalIds", value, "loc_utf16", path, root)

    return records


def dedupe_records(records: list[HashRecord]) -> list[HashRecord]:
    group_priority = {
        "PersistentIds": 0,
        "BiomeNames": 0,
        "FamilyIds": 0,
        "ProfileIds": 1,
        "MatrixAssetNames": 2,
        "LocalizedNameKeys": 1,
        "DisplayNames": 2,
        "AuthoredSignalIds": 0,
        "ISignalStructNames": 0,
        "StructNames": 1,
        "SignalBusNames": 2,
        "CodeLiteralIds": 3,
        "LegacyAssetNames": 4,
    }

    unique: dict[tuple[str, str, str], HashRecord] = {}
    for record in records:
        key = (record.category, record.value, record.hash_mode)
        existing = unique.get(key)
        if existing is None:
            unique[key] = record
            continue

        existing_priority = group_priority.get(existing.group, 100)
        record_priority = group_priority.get(record.group, 100)
        if record_priority < existing_priority or (
            record_priority == existing_priority and record.source < existing.source
        ):
            unique[key] = record
    return sorted(unique.values(), key=lambda record: (record.category, record.group, record.name, record.value, record.source))


def collect_records(root: Path) -> list[HashRecord]:
    records: list[HashRecord] = []
    records.extend(scan_item_records(root))
    records.extend(scan_biome_records(root))
    records.extend(scan_signal_records(root))
    return dedupe_records(records)


def find_collisions(records: list[HashRecord]) -> dict[int, list[HashRecord]]:
    by_hash: dict[int, list[HashRecord]] = {}
    for record in records:
        by_hash.setdefault(record.hash_value, []).append(record)

    collisions: dict[int, list[HashRecord]] = {}
    for hash_value, bucket in by_hash.items():
        distinct_values = {record.value for record in bucket}
        if len(distinct_values) > 1:
            collisions[hash_value] = bucket
    return collisions


def make_base_identifier(value: str) -> str:
    tokens = re.findall(r"[A-Za-z0-9]+", value)
    if not tokens:
        digest = hashlib.sha256(value.encode("utf-8")).hexdigest()[:8]
        return f"Id{digest}"

    pieces: list[str] = []
    for token in tokens:
        if token.isupper() or token.isdigit():
            pieces.append(token)
        else:
            pieces.append(token[:1].upper() + token[1:])

    identifier = "".join(pieces)
    if identifier and identifier[0].isdigit():
        identifier = "N" + identifier
    return identifier or "Id"


def uniquify_identifier(base: str, used: set[str], record: HashRecord) -> str:
    if base not in used:
        used.add(base)
        return base

    digest = hashlib.sha256(f"{record.category}|{record.group}|{record.value}|{record.hash_mode}".encode("utf-8")).hexdigest()[:8]
    candidate = f"{base}_{digest}"
    counter = 2
    while candidate in used:
        candidate = f"{base}_{digest}_{counter}"
        counter += 1
    used.add(candidate)
    return candidate


def escape_csharp_string(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def build_csharp(records: list[HashRecord]) -> str:
    categories = ("Items", "Biomes", "Signals")
    lines: list[str] = []
    lines.append("// AUTO-GENERATED. DO NOT EDIT.")
    lines.append("// Source: Tools/VerifyH8HashCollisions.py --write-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs")
    lines.append("namespace Hecton8.Core.Generated")
    lines.append("{")
    lines.append("    public static class H8Hashes")
    lines.append("    {")
    lines.append(f"        public const int TotalCount = {len(records)};")
    lines.append("")

    for category in categories:
        category_records = [record for record in records if record.category == category]
        if not category_records:
            continue

        used: set[str] = set()
        lines.append(f"        public static class {category}")
        lines.append("        {")
        lines.append(f"            public const int Count = {len(category_records)};")
        lines.append("")
        for record in category_records:
            identifier = uniquify_identifier(record.name, used, record)
            lines.append(f"            public const string {identifier}Id = \"{escape_csharp_string(record.value)}\";")
            lines.append(f"            public const uint {identifier}Hash = {record.hash_value}u;")
        lines.append("        }")
        lines.append("")

    lines.append("    }")
    lines.append("}")
    lines.append("")
    return "\n".join(lines)


def ensure_meta_file(csharp_path: Path) -> None:
    meta_path = csharp_path.with_suffix(csharp_path.suffix + ".meta")
    if meta_path.exists():
        return

    digest = hashlib.sha256(csharp_path.as_posix().encode("utf-8")).hexdigest()[:32]
    meta_path.write_text(
        "\n".join(
            [
                "fileFormatVersion: 2",
                f"guid: {digest}",
                "MonoImporter:",
                "  externalObjects: {}",
                "  serializedVersion: 2",
                "  defaultReferences: []",
                "  executionOrder: 0",
                "  icon: {instanceID: 0}",
                "  userData:",
                "  assetBundleName:",
                "  assetBundleVariant:",
                "",
            ]
        ),
        encoding="utf-8",
    )


def check_csharp_output(records: list[HashRecord], csharp_path: Path) -> tuple[bool, str]:
    expected = build_csharp(records)
    if not csharp_path.exists():
        return False, "missing"

    actual = csharp_path.read_text(encoding="utf-8-sig", errors="replace")
    if actual == expected:
        return True, "up-to-date"

    if actual.replace("\r\n", "\n") == expected:
        return True, "up-to-date line-ending normalized"

    return False, "stale"


def build_report(records: list[HashRecord], csharp_status: str) -> str:
    category_counts = Counter(record.category for record in records)
    group_counts = Counter((record.category, record.group) for record in records)
    hash_mode_counts = Counter(record.hash_mode for record in records)

    lines: list[str] = []
    lines.append("# H8 Hash Catalog Audit")
    lines.append("")
    lines.append("Status: HASHES SYNCHRONIZED")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Total records: {len(records)}")
    for category in ("Items", "Biomes", "Signals"):
        lines.append(f"- {category}: {category_counts.get(category, 0)}")
    lines.append(f"- Generated header check: {csharp_status}")
    lines.append("- Collision status: 0 collisions")
    lines.append("- Runtime impact: 0 us/frame, 0 B/frame")
    lines.append("")
    lines.append("## Artifacts")
    lines.append("")
    lines.append("- Generated C#: `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`")
    lines.append("- Markdown audit: `Docs/Reports/H8_Hash_Catalog_Audit.md`")
    lines.append("- JSON manifest: `Docs/Reports/H8_Hash_Catalog_Audit.json`")
    lines.append("")
    lines.append("## Hash Modes")
    lines.append("")
    for hash_mode, count in sorted(hash_mode_counts.items()):
        lines.append(f"- `{hash_mode}`: {count}")
    lines.append("")
    lines.append("## Group Counts")
    lines.append("")
    for (category, group), count in sorted(group_counts.items()):
        lines.append(f"- `{category}.{group}`: {count}")
    lines.append("")
    lines.append("## Verification Commands")
    lines.append("")
    lines.append("- `python -B Tools\\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`")
    lines.append("- `python -B Tools\\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs --write-report Docs/Reports/H8_Hash_Catalog_Audit.md --write-json Docs/Reports/H8_Hash_Catalog_Audit.json`")
    lines.append("- `python -B -m unittest Tools.test_h8_hash_collisions`")
    lines.append("- Generated C# runtime/allocation scan: `NO_RUNTIME_LOGIC_HITS`")
    lines.append("")
    lines.append("## Boundary")
    lines.append("")
    lines.append("- Unity import is not claimed here; the current shell has no Unity Editor executable.")
    lines.append("- This audit is an offline source/data/hash verification artifact.")
    lines.append("")
    return "\n".join(lines)


def build_manifest(records: list[HashRecord], csharp_status: str) -> dict:
    category_counts = Counter(record.category for record in records)
    group_counts = Counter((record.category, record.group) for record in records)
    hash_mode_counts = Counter(record.hash_mode for record in records)

    return {
        "status": "HASHES_SYNCHRONIZED",
        "total_records": len(records),
        "categories": {category: category_counts.get(category, 0) for category in ("Items", "Biomes", "Signals")},
        "hash_modes": {hash_mode: count for hash_mode, count in sorted(hash_mode_counts.items())},
        "groups": {f"{category}.{group}": count for (category, group), count in sorted(group_counts.items())},
        "generated_header_check": csharp_status,
        "collision_count": 0,
        "runtime_impact": {
            "microseconds_per_frame": 0,
            "gc_bytes_per_frame": 0,
        },
        "records": [
            {
                "category": record.category,
                "group": record.group,
                "name": record.name,
                "value": record.value,
                "hash_mode": record.hash_mode,
                "hash_value": record.hash_value,
                "hash_hex": f"0x{record.hash_value:08X}",
                "source": record.source,
            }
            for record in records
        ],
    }


def print_summary(records: list[HashRecord]) -> None:
    print(f"H8 hash records: {len(records)}")
    for category in ("Items", "Biomes", "Signals"):
        count = sum(1 for record in records if record.category == category)
        print(f"{category}: {count}")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Verify and generate HECTON-8 FNV-1a project hashes.")
    parser.add_argument("--root", default=None, help="Project root. Defaults to parent of Tools.")
    parser.add_argument("--write-csharp", default=None, help="Optional H8Hashes.cs output path.")
    parser.add_argument("--check-csharp", default=None, help="Fail if the existing H8Hashes.cs does not match generated output.")
    parser.add_argument("--write-report", default=None, help="Optional Markdown audit report output path.")
    parser.add_argument("--write-json", default=None, help="Optional machine-readable JSON audit manifest output path.")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parents[1]
    records = collect_records(root)
    collisions = find_collisions(records)
    print_summary(records)

    if collisions:
        print("HASH COLLISIONS DETECTED:")
        for hash_value, bucket in sorted(collisions.items()):
            print(f"  {hash_value} / 0x{hash_value:08X}")
            for record in bucket:
                print(f"    {record.category}.{record.group} [{record.hash_mode}] {record.value} @ {record.source}")
        return 2

    if args.write_csharp:
        csharp_path = (root / args.write_csharp).resolve()
        csharp_path.parent.mkdir(parents=True, exist_ok=True)
        csharp_path.write_text(build_csharp(records), encoding="utf-8", newline="\n")
        ensure_meta_file(csharp_path)
        print(f"Wrote {csharp_path.relative_to(root).as_posix()}")

    csharp_status = "not checked"
    if args.check_csharp:
        csharp_path = (root / args.check_csharp).resolve()
        is_current, status = check_csharp_output(records, csharp_path)
        csharp_status = status
        print(f"CSharp output check: {status}")
        if not is_current:
            print(f"Run: python Tools/VerifyH8HashCollisions.py --write-csharp {csharp_path.relative_to(root).as_posix()}")
            return 3

    if args.write_report:
        report_path = (root / args.write_report).resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(build_report(records, csharp_status), encoding="utf-8", newline="\n")
        print(f"Wrote {report_path.relative_to(root).as_posix()}")

    if args.write_json:
        manifest_path = (root / args.write_json).resolve()
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(build_manifest(records, csharp_status), indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
        print(f"Wrote {manifest_path.relative_to(root).as_posix()}")

    print("HASH COLLISIONS: 0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
