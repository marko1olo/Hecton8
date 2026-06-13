#!/usr/bin/env python3
"""Read-only static gate for HECTON-8 global authority pressure.

Default behavior writes nothing and exits non-zero only for hard violations:
- hot-style generic GlobalRegistry.Get<T>/TryGet<T> usage
- duplicate central BufferID numeric values

Debt surfaces such as GlobalSignals.Publish, HectonEventBus usage, local
numeric BufferID casts, raw NativeArray constructors, Pack=1 layouts, and
SignalBus producer/config gaps are reported as warnings unless explicitly
promoted with flags.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_H8_MEMORY = DEFAULT_SOURCE_ROOT / "Core" / "Memory" / "H8Memory.cs"

if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import BufferIDSovereigntyAudit as buffer_audit  # noqa: E402


SCHEMA = "hecton8.global_authority_gate.v1"
SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
    "Editor",
    "Tests",
}


PATTERNS: dict[str, re.Pattern[str]] = {
    "globalRegistryDot": re.compile(r"GlobalRegistry\."),
    "globalRegistryGenericGet": re.compile(r"GlobalRegistry\.(?:Get|TryGet)\s*<"),
    "signalBusRefs": re.compile(r"SignalBus\s*<"),
    "signalBusPushTryPush": re.compile(r"SignalBus\s*<[^>]+>\s*\.\s*(?:Push|TryPush)\b"),
    "signalBusConfigure": re.compile(r"SignalBus\s*<[^>]+>\s*\.\s*Configure\b"),
    "signalBusEnsureInitialized": re.compile(r"SignalBus\s*<[^>]+>\s*\.\s*EnsureInitialized\b"),
    "globalSignalsPublish": re.compile(r"GlobalSignals\.Publish\b"),
    "hectonEventBusPubSub": re.compile(r"HectonEventBus\.(?:Publish|Subscribe)\b"),
    "dataVaultRefs": re.compile(r"GlobalDataVault|IDataVault|VaultBufferHandle|DataVault"),
    "nativeArrayCtor": re.compile(r"new\s+NativeArray\s*<"),
    "nativeCollectionRefs": re.compile(r"\bNative(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<"),
    "packOne": re.compile(r"\[StructLayout[^\]]*\bPack\s*=\s*1\b", re.DOTALL),
    "localNumericBufferCast": re.compile(r"\(\s*BufferID\s*\)\s*-?(?:0x[0-9A-Fa-f_]+|\d[\d_]*)"),
}

SIGNALBUS_PRODUCER_RE = re.compile(r"SignalBus\s*<\s*([^>]+?)\s*>\s*\.\s*(?:Push|TryPush)\b")
SIGNALBUS_CONFIG_RE = re.compile(r"SignalBus\s*<\s*([^>]+?)\s*>\s*\.\s*(?:Configure|EnsureInitialized)\b")


@dataclass(frozen=True)
class PatternCount:
    matches: int
    files: int


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_cs_files(source_root: Path) -> list[Path]:
    return [
        path
        for path in sorted(source_root.rglob("*.cs"))
        if not should_skip(path.relative_to(source_root))
    ]


def read_sources(files: list[Path]) -> dict[Path, str]:
    return {
        path: path.read_text(encoding="utf-8", errors="ignore")
        for path in files
    }


def count_pattern(sources: dict[Path, str], pattern: re.Pattern[str]) -> PatternCount:
    matches = 0
    files = 0
    for text in sources.values():
        count = len(pattern.findall(text))
        if count:
            matches += count
            files += 1
    return PatternCount(matches=matches, files=files)


def count_all_patterns(sources: dict[Path, str]) -> dict[str, dict[str, int]]:
    result: dict[str, dict[str, int]] = {}
    for name, pattern in PATTERNS.items():
        count = count_pattern(sources, pattern)
        result[name] = {"matches": count.matches, "files": count.files}
    return result


def extract_signalbus_types(sources: dict[Path, str], pattern: re.Pattern[str]) -> set[str]:
    types: set[str] = set()
    for text in sources.values():
        for match in pattern.finditer(text):
            types.add(re.sub(r"\s+", " ", match.group(1).strip()))
    return types


def build_buffer_payload(h8_memory_path: Path, source_root: Path) -> dict[str, object]:
    entries = buffer_audit.parse_buffer_enum(h8_memory_path)
    enum_by_value: dict[int, list[str]] = {}
    for entry in entries:
        enum_by_value.setdefault(entry.value, []).append(entry.name)

    casts = buffer_audit.scan_buffer_casts(
        source_root=source_root,
        enum_by_value={value: tuple(names) for value, names in enum_by_value.items()},
        h8_memory_path=h8_memory_path,
    )
    payload = buffer_audit.build_payload(entries, casts, h8_memory_path)
    return {
        "bufferIdCount": payload["bufferIdCount"],
        "duplicateValueCount": payload["duplicateValueCount"],
        "duplicateEntries": payload["duplicateEntries"],
        "localNumericCastCount": payload["localNumericCastCount"],
        "localNumericCastFileCount": payload["localNumericCastFileCount"],
    }


def build_payload(source_root: Path, h8_memory_path: Path) -> dict[str, object]:
    files = iter_cs_files(source_root)
    sources = read_sources(files)
    counts = count_all_patterns(sources)

    producer_types = extract_signalbus_types(sources, SIGNALBUS_PRODUCER_RE)
    config_types = extract_signalbus_types(sources, SIGNALBUS_CONFIG_RE)
    suspect_types = sorted(producer_types - config_types)
    buffer_payload = build_buffer_payload(h8_memory_path, source_root)

    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "csFileCount": len(files),
        "counts": counts,
        "signalBus": {
            "producerTypeCount": len(producer_types),
            "configuredTypeCount": len(config_types),
            "suspectProducerTypeCount": len(suspect_types),
            "suspectProducerTypes": suspect_types,
        },
        "bufferId": buffer_payload,
    }


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    failures: list[str] = []
    counts = payload["counts"]
    if not isinstance(counts, dict):
        raise TypeError("counts payload malformed")
    registry_get = counts["globalRegistryGenericGet"]["matches"]
    if registry_get > args.max_registry_get:
        failures.append(f"GlobalRegistry.Get/TryGet generic hits {registry_get} > {args.max_registry_get}")

    buffer_id = payload["bufferId"]
    if not isinstance(buffer_id, dict):
        raise TypeError("bufferId payload malformed")
    duplicates = int(buffer_id["duplicateValueCount"])
    if duplicates > 0:
        failures.append(f"duplicate central BufferID numeric values: {duplicates}")

    signal_bus = payload["signalBus"]
    if not isinstance(signal_bus, dict):
        raise TypeError("signalBus payload malformed")
    suspects = int(signal_bus["suspectProducerTypeCount"])
    if args.fail_on_signalbus_suspects and suspects > 0:
        failures.append(f"SignalBus producer types without config proof: {suspects}")

    local_casts = int(buffer_id["localNumericCastCount"])
    if args.fail_on_local_buffer_casts and local_casts > 0:
        failures.append(f"local numeric BufferID casts: {local_casts}")

    if args.max_global_signals_publish is not None:
        hits = counts["globalSignalsPublish"]["matches"]
        if hits > args.max_global_signals_publish:
            failures.append(f"GlobalSignals.Publish hits {hits} > {args.max_global_signals_publish}")

    if args.max_hecton_eventbus_pubsub is not None:
        hits = counts["hectonEventBusPubSub"]["matches"]
        if hits > args.max_hecton_eventbus_pubsub:
            failures.append(f"HectonEventBus publish/subscribe hits {hits} > {args.max_hecton_eventbus_pubsub}")

    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    counts = payload["counts"]
    signal_bus = payload["signalBus"]
    buffer_id = payload["bufferId"]
    print("Global authority gate")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"csFiles={payload['csFileCount']}")
    for key in (
        "globalRegistryDot",
        "globalRegistryGenericGet",
        "signalBusRefs",
        "signalBusPushTryPush",
        "signalBusConfigure",
        "signalBusEnsureInitialized",
        "globalSignalsPublish",
        "hectonEventBusPubSub",
        "dataVaultRefs",
        "nativeArrayCtor",
        "nativeCollectionRefs",
        "packOne",
        "localNumericBufferCast",
    ):
        value = counts[key]
        print(f"{key}={value['matches']} files={value['files']}")
    print(
        "signalBusSuspects="
        f"{signal_bus['suspectProducerTypeCount']} "
        f"producerTypes={signal_bus['producerTypeCount']} "
        f"configuredTypes={signal_bus['configuredTypeCount']}"
    )
    if signal_bus["suspectProducerTypes"]:
        print("signalBusSuspectTypes=" + "; ".join(signal_bus["suspectProducerTypes"]))
    print(
        "bufferId="
        f"duplicates={buffer_id['duplicateValueCount']} "
        f"localCasts={buffer_id['localNumericCastCount']} "
        f"castFiles={buffer_id['localNumericCastFileCount']}"
    )
    if failures:
        print("status=FAIL")
        for failure in failures:
            print(f"failure={failure}")
    else:
        print("status=PASS_WITH_WARNINGS")


def run(args: argparse.Namespace) -> int:
    payload = build_payload(Path(args.source_root), Path(args.h8_memory))
    failures = hard_failures(payload, args)
    if args.json:
        print(json.dumps(payload | {"failures": failures}, indent=2, sort_keys=True))
    else:
        print_text(payload, failures)
    return 1 if failures else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--h8-memory", default=str(DEFAULT_H8_MEMORY))
    parser.add_argument("--json", action="store_true", help="Print JSON to stdout.")
    parser.add_argument("--max-registry-get", type=int, default=0)
    parser.add_argument("--max-global-signals-publish", type=int)
    parser.add_argument("--max-hecton-eventbus-pubsub", type=int)
    parser.add_argument("--fail-on-signalbus-suspects", action="store_true")
    parser.add_argument("--fail-on-local-buffer-casts", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    sys.exit(main())
