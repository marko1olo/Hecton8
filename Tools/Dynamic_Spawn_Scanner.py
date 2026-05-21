#!/usr/bin/env python3
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "Docs" / "Reports" / "WORLD_OPTIMIZATION_REPORT.json"

SCAN_DIRS = [
    ROOT / "Assets" / "_Project" / "Scripts" / "AI",
    ROOT / "Assets" / "_Project" / "Scripts" / "Fauna",
    ROOT / "Assets" / "_Project" / "Scripts" / "World",
    ROOT / "Assets" / "_Project" / "Scripts" / "Environment",
]

PATTERNS = [
    ("runtime_instantiate", re.compile(r"\bInstantiate\s*\(")),
    ("destroy_spike", re.compile(r"\bDestroy\s*\(")),
    ("static_spawn_point", re.compile(r"\b(SpawnPoint|SetSpawnPoint|spawnPoint)\b")),
    ("mono_enemy_spawner", re.compile(r"\b(class|struct)\s+\w*(Enemy|Creature|Predator|Fauna)\w*Spawner\b")),
    ("managed_hot_random", re.compile(r"\b(Random\.Range|new\s+System\.Random)\b")),
    ("managed_hot_collection", re.compile(r"\bnew\s+(List|Dictionary|HashSet)<")),
    ("scene_search", re.compile(r"\b(FindObjectOfType|FindObjectsOfType|GameObject\.Find|Camera\.main)\b")),
]


def iter_files():
    for directory in SCAN_DIRS:
        if not directory.exists():
            continue
        for path in directory.rglob("*.cs"):
            if "Editor" in path.parts:
                continue
            yield path


def scan():
    findings = []
    by_pattern = {name: 0 for name, _ in PATTERNS}
    for path in iter_files():
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError:
            continue

        rel = path.relative_to(ROOT).as_posix()
        for line_number, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("*"):
                continue
            for name, pattern in PATTERNS:
                if pattern.search(line):
                    by_pattern[name] += 1
                    findings.append(
                        {
                            "pattern": name,
                            "file": rel,
                            "line": line_number,
                            "sample": stripped[:180],
                        }
                    )

    return {
        "agent": "SHINOBU_253",
        "domain": "E3_FLORA_FAUNA_BIOTA",
        "report": "WORLD_OPTIMIZATION_REPORT",
        "scanned_files": sum(1 for _ in iter_files()),
        "forbidden_hits": len(findings),
        "by_pattern": by_pattern,
        "findings": findings[:500],
        "policy": {
            "runtime_spawning": "DTO injection into GlobalDataVault/PredatorCognitionDomain only",
            "hot_broadcast": "SignalBus<T> or vault snapshot",
            "quality": "continuous GlobalQualityWeight; no binary tier switch",
        },
    }


def main():
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    result = scan()
    REPORT.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Wrote {REPORT}")
    print(f"Forbidden hits: {result['forbidden_hits']}")


if __name__ == "__main__":
    main()
