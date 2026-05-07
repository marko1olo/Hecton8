#!/usr/bin/env python3
"""Dry-run registry rewrite for first-party singleton callsites.

The script intentionally uses an explicit map. Unmapped Instance access is reported
instead of guessed, because several remaining managers still need interface/slot
decisions before they can be safely converted to registry-owned services.
"""

from __future__ import annotations

import argparse
import pathlib
import re
from typing import Dict, Iterable, Tuple


PROJECT_ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT_ROOT = PROJECT_ROOT / "Assets" / "_Project" / "Scripts"

REGISTRY_GET = "global::Hecton8.Core.GlobalRegistry.Get"

SERVICE_REWRITES: Dict[str, str] = {
    "AbyssalThermalManager": f"{REGISTRY_GET}<global::Hecton8.World.AbyssalThermalManager>()",
    "AmbientWaterMotionManager": f"{REGISTRY_GET}<global::Hecton8.Core.AmbientWaterMotionManager>()",
    "AutonomousExtractorSystem": f"{REGISTRY_GET}<global::Hecton8.Core.AutonomousExtractorSystem>()",
    "CullingManager": f"{REGISTRY_GET}<global::Hecton8.World.CullingManager>()",
    "DynamicResolutionScaler": f"{REGISTRY_GET}<global::Hecton8.World.DynamicResolutionScaler>()",
    "EnvironmentalStrainManager": f"{REGISTRY_GET}<global::Hecton8.World.EnvironmentalStrainManager>()",
    "FaunaGeneticsManager": f"{REGISTRY_GET}<global::Hecton8.Ecosystem.FaunaGeneticsManager>()",
    "HectonFloatingOrigin": f"{REGISTRY_GET}<global::Hecton8.Core.HectonFloatingOrigin>()",
    "HectonMusicDirector": f"{REGISTRY_GET}<global::Hecton8.Audio.HectonMusicDirector>()",
    "HectonRockManager": f"{REGISTRY_GET}<global::Hecton8.Core.HectonRockManager>()",
    "HectonSurfaceWeatherDirector": f"{REGISTRY_GET}<global::Hecton8.Atmosphere.HectonSurfaceWeatherDirector>()",
    "ImpostorSystem": f"{REGISTRY_GET}<global::Hecton8.World.ImpostorSystem>()",
    "InputManager": f"{REGISTRY_GET}<global::Hecton8.Core.IInputService>()",
    "LODSystemManager": f"{REGISTRY_GET}<global::Hecton8.World.LODSystemManager>()",
    "MapMagicBridge": f"{REGISTRY_GET}<global::Hecton8.World.MapMagicBridge>()",
    "ObjectPoolManager": f"{REGISTRY_GET}<global::Hecton8.Core.ObjectPoolManager>()",
    "PersistentWorldRegistry": f"{REGISTRY_GET}<global::Hecton8.SaveSystem.PersistentWorldRegistry>()",
    "PlayerActionController": f"{REGISTRY_GET}<global::Hecton8.Gameplay.PlayerActionController>()",
    "PlayerExpressionManager": f"{REGISTRY_GET}<global::Hecton8.Gameplay.PlayerExpressionManager>()",
    "RunModifierController": f"{REGISTRY_GET}<global::Hecton8.Meta.RunModifierController>()",
    "SargassumCutManager": f"{REGISTRY_GET}<global::Hecton8.World.SargassumCutManager>()",
    "SargassumGlobalDragManager": f"{REGISTRY_GET}<global::Hecton8.World.SargassumGlobalDragManager>()",
    "ScavengePopulator": f"{REGISTRY_GET}<global::Hecton8.Core.ScavengePopulator>()",
    "SettingsManager": f"{REGISTRY_GET}<global::Hecton8.UI.SettingsManager>()",
    "SpectrumSystem": f"{REGISTRY_GET}<global::Hecton8.Core.SpectrumSystem>()",
    "WorldStateManager": f"{REGISTRY_GET}<global::Hecton8.Core.WorldStateManager>()",
}

INSTANCE_PATTERN = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\.Instance\b")


def iter_sources() -> Iterable[pathlib.Path]:
    for path in SCRIPT_ROOT.rglob("*.cs"):
        parts = set(path.parts)
        if "Editor" in parts or "Dev" in parts:
            continue
        yield path


def rewrite_text(text: str) -> Tuple[str, Dict[str, int], Dict[str, int]]:
    applied: Dict[str, int] = {}
    unmapped: Dict[str, int] = {}

    def replace(match: re.Match[str]) -> str:
        type_name = match.group(1)
        replacement = SERVICE_REWRITES.get(type_name)
        if replacement is None:
            unmapped[type_name] = unmapped.get(type_name, 0) + 1
            return match.group(0)

        applied[type_name] = applied.get(type_name, 0) + 1
        return replacement

    return INSTANCE_PATTERN.sub(replace, text), applied, unmapped


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="write mapped replacements")
    args = parser.parse_args()

    total_applied = 0
    total_unmapped = 0
    for path in iter_sources():
        original = path.read_text(encoding="utf-8-sig")
        rewritten, applied, unmapped = rewrite_text(original)
        applied_count = sum(applied.values())
        unmapped_count = sum(unmapped.values())
        if applied_count == 0 and unmapped_count == 0:
            continue

        rel_path = path.relative_to(PROJECT_ROOT)
        print(f"{rel_path}: mapped={applied_count} unmapped={unmapped_count}")
        if applied:
            print("  mapped:", ", ".join(f"{key}={value}" for key, value in sorted(applied.items())))
        if unmapped:
            print("  unmapped:", ", ".join(f"{key}={value}" for key, value in sorted(unmapped.items())))

        if args.apply and rewritten != original:
            path.write_text(rewritten, encoding="utf-8")

        total_applied += applied_count
        total_unmapped += unmapped_count

    print(f"TOTAL mapped={total_applied} unmapped={total_unmapped} mode={'apply' if args.apply else 'dry-run'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
