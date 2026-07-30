# -*- coding: utf-8 -*-
"""One-shot: hoist vatReadiness to manifest root + copy fauna raw + fix NEVER_COMPILE_TESTS.

Does NOT touch sibling dirty files (README UU, Thermocline tests, etc.).
"""
from __future__ import annotations

import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def hoist_vat_readiness(manifest_path: Path) -> str:
    text = manifest_path.read_text(encoding="utf-8")
    data = json.loads(text)
    root_vr = data.get("vatReadiness")
    nested = None
    extra = data.get("extra")
    if isinstance(extra, dict):
        nested = extra.get("vatReadiness")

    if root_vr and isinstance(root_vr, dict) and int(root_vr.get("vertexCountLOD0") or 0) > 0:
        return f"OK already root vatReadiness vertexCountLOD0={root_vr.get('vertexCountLOD0')}"

    if not nested or not isinstance(nested, dict):
        return "FAIL no nested extra.vatReadiness to hoist"

    # Hoist full block to root (JsonUtility only needs vertexCountLOD0; keep siblings for forge tools).
    data["vatReadiness"] = nested
    # Keep nested copy for any forge consumer still looking under extra.
    if isinstance(extra, dict):
        extra["vatReadiness"] = nested
        data["extra"] = extra

    manifest_path.write_text(
        json.dumps(data, ensure_ascii=False, indent=1) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return f"HOISTED vatReadiness vertexCountLOD0={nested.get('vertexCountLOD0')}"


def ensure_raw_folder() -> str:
    src_dir = ROOT / "Assets/_Project/Art/Generated/Forge/Fauna"
    dst_dir = ROOT / "Assets/_Project/Art/Fauna/Raw"
    dst_dir.mkdir(parents=True, exist_ok=True)

    copied = []
    for name in (
        "MESH_Fauna_Fish_2207_00.fbx",
        "MESH_Fauna_Fish_2207_00.fbx.meta",
        "MANIFEST_Fauna_Fish_2207_00.json",
        "MANIFEST_Fauna_Fish_2207_00.json.meta",
    ):
        src = src_dir / name
        dst = dst_dir / name
        if not src.exists():
            return f"FAIL missing source {src}"
        shutil.copy2(src, dst)
        copied.append(name)

    # After copy, hoist on BOTH forge + raw manifests so sibling reimport paths agree.
    notes = []
    for mp in (
        src_dir / "MANIFEST_Fauna_Fish_2207_00.json",
        dst_dir / "MANIFEST_Fauna_Fish_2207_00.json",
    ):
        notes.append(f"{mp.relative_to(ROOT)}: {hoist_vat_readiness(mp)}")
    return "copied " + ", ".join(copied) + " | " + " ; ".join(notes)


def fix_asmdefs() -> str:
    paths = [
        ROOT / "Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef",
        ROOT / "Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef",
    ]
    out = []
    for p in paths:
        data = json.loads(p.read_text(encoding="utf-8"))
        before = list(data.get("defineConstraints") or [])
        # UNITY_INCLUDE_TESTS is the project-standard enable gate used by sibling test asmdefs.
        data["defineConstraints"] = ["UNITY_INCLUDE_TESTS"]
        p.write_text(json.dumps(data, indent=4) + "\n", encoding="utf-8", newline="\n")
        out.append(f"{p.name}: {before} -> {data['defineConstraints']}")
    return " ; ".join(out)


def main() -> None:
    print("RAW:", ensure_raw_folder())
    print("ASMDEF:", fix_asmdefs())


if __name__ == "__main__":
    main()
