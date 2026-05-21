#!/usr/bin/env python3
"""Archive active Crest roots and quarantine inactive Crest 5 outside Unity visibility."""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import time
import zipfile
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
BACKUP_DIR = PROJECT_ROOT / "Docs" / "Archive" / "Crest_Baseline_Backup"
QUARANTINE_DIR = PROJECT_ROOT / "Docs" / "Archive" / "Crest_Version_Quarantine"
REPORT_PATH = PROJECT_ROOT / "Docs" / "Reports" / "CREST_QUARANTINE_REPORT.json"

CREST_ROOTS = (
    ("crest4_assets_crest", PROJECT_ROOT / "Assets" / "Crest"),
    ("crest4_project_ocean_settings", PROJECT_ROOT / "Assets" / "_Project" / "Data" / "Ocean"),
    ("crest4_project_legacy_crest_settings", PROJECT_ROOT / "Assets" / "_Project" / "crest"),
    ("crest4_project_ocean_prefab", PROJECT_ROOT / "Assets" / "_Project" / "Prefabs" / "Ocean_Crest.prefab"),
    ("crest4_project_ocean_prefab_meta", PROJECT_ROOT / "Assets" / "_Project" / "Prefabs" / "Ocean_Crest.prefab.meta"),
    ("crest4_project_world_ocean_scene", PROJECT_ROOT / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity"),
    ("crest4_project_world_ocean_scene_meta", PROJECT_ROOT / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity.meta"),
    ("crest4_plugins_crest4", PROJECT_ROOT / "Assets" / "Plugins" / "Crest4"),
    ("crest5_plugins_crest5", PROJECT_ROOT / "Assets" / "Plugins" / "Crest5"),
    ("crest5_embedded_package", PROJECT_ROOT / "Packages" / "com.waveharmonic.crest"),
)

CREST5_FIRST_PARTY_TOOLS = (
    PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Plugins" / "Crest" / "Crest5KinematicsAdapter.cs",
    PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Editor" / "CrestMigrationTool.cs",
    PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Editor" / "CrestMigrationBatch.cs",
    PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "Editor" / "CrestParityRunner.cs",
)


def ensure_inside_project(path: Path) -> Path:
    resolved = path.resolve()
    try:
        resolved.relative_to(PROJECT_ROOT)
    except ValueError as exc:
        raise RuntimeError(f"path escapes project root: {resolved}") from exc
    return resolved


def ensure_archive_target(path: Path) -> Path:
    resolved = ensure_inside_project(path)
    resolved.relative_to(QUARANTINE_DIR.resolve())
    relative_parts = resolved.relative_to(PROJECT_ROOT).parts
    if relative_parts and relative_parts[0] in {"Assets", "Packages"}:
        raise RuntimeError(f"quarantine target remains Unity-visible: {resolved}")
    return resolved


def write_ignore_file() -> None:
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)
    QUARANTINE_DIR.mkdir(parents=True, exist_ok=True)
    (BACKUP_DIR / ".gitignore").write_text("*\n!.gitignore\n", encoding="utf-8")
    (QUARANTINE_DIR / ".gitignore").write_text("*\n!.gitignore\n", encoding="utf-8")


def zip_tree(label: str, source: Path, stamp: str, execute: bool) -> dict:
    source = ensure_inside_project(source)
    record = {
        "label": label,
        "source": str(source.relative_to(PROJECT_ROOT)),
        "exists": source.exists(),
        "zip": None,
        "file_count": 0,
        "bytes": 0,
    }
    if not source.exists():
        return record

    archive_path = BACKUP_DIR / f"{label}_{stamp}.zip"
    record["zip"] = str(archive_path.relative_to(PROJECT_ROOT))
    if not execute:
        return record

    with zipfile.ZipFile(
        archive_path,
        mode="w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        if source.is_file():
            arcname = source.relative_to(PROJECT_ROOT).as_posix()
            archive.write(source, arcname)
            record["file_count"] = 1
            record["bytes"] = source.stat().st_size
        else:
            for file_path in sorted(source.rglob("*")):
                if not file_path.is_file():
                    continue
                arcname = file_path.relative_to(PROJECT_ROOT).as_posix()
                archive.write(file_path, arcname)
                record["file_count"] += 1
                record["bytes"] += file_path.stat().st_size
    return record


def unique_destination(destination: Path) -> Path:
    if not destination.exists():
        return destination
    suffix = time.strftime("%Y%m%d_%H%M%S", time.gmtime())
    return destination.with_name(f"{destination.name}_{suffix}")


def move_path(source: Path, destination: Path, execute: bool) -> dict:
    source = ensure_inside_project(source)
    destination = unique_destination(destination)
    destination = ensure_archive_target(destination)
    record = {
        "source": str(source.relative_to(PROJECT_ROOT)),
        "destination": str(destination.relative_to(PROJECT_ROOT)),
        "exists": source.exists(),
        "moved": False,
    }
    if not source.exists():
        return record
    if not execute:
        return record

    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.move(str(source), str(destination))
    record["moved"] = True
    return record


def quarantine_first_party_tools(execute: bool) -> list[dict]:
    records: list[dict] = []
    for source in CREST5_FIRST_PARTY_TOOLS:
        relative = source.relative_to(PROJECT_ROOT)
        destination = QUARANTINE_DIR / "first_party_crest5_tools" / relative
        records.append(move_path(source, destination, execute))

        meta_path = Path(str(source) + ".meta")
        meta_destination = Path(str(destination) + ".meta")
        records.append(move_path(meta_path, meta_destination, execute))
    return records


def clean_packages_lock(execute: bool) -> dict:
    lock_path = PROJECT_ROOT / "Packages" / "packages-lock.json"
    record = {"path": str(lock_path.relative_to(PROJECT_ROOT)), "exists": lock_path.exists(), "removed": False}
    if not lock_path.exists():
        return record

    data = json.loads(lock_path.read_text(encoding="utf-8"))
    dependencies = data.get("dependencies")
    if not isinstance(dependencies, dict) or "com.waveharmonic.crest" not in dependencies:
        return record

    if execute:
        del dependencies["com.waveharmonic.crest"]
        lock_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    record["removed"] = True
    return record


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--execute", action="store_true", help="perform moves and writes; otherwise dry-run")
    args = parser.parse_args(argv)

    stamp = time.strftime("%Y%m%d_%H%M%S", time.gmtime())
    if args.execute:
        write_ignore_file()

    archives = [zip_tree(label, source, stamp, args.execute) for label, source in CREST_ROOTS]
    package_move = move_path(
        PROJECT_ROOT / "Packages" / "com.waveharmonic.crest",
        QUARANTINE_DIR / "Packages" / "com.waveharmonic.crest",
        args.execute,
    )
    first_party_moves = quarantine_first_party_tools(args.execute)
    lock_cleanup = clean_packages_lock(args.execute)

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "agent": "SHINOBU_260",
        "role": "CREST_VERSION_QUARANTINE_DIRECTOR",
        "executed": args.execute,
        "timestamp_utc": stamp,
        "project_root": str(PROJECT_ROOT),
        "backup_dir": str(BACKUP_DIR.relative_to(PROJECT_ROOT)),
        "quarantine_dir": str(QUARANTINE_DIR.relative_to(PROJECT_ROOT)),
        "archives": archives,
        "package_move": package_move,
        "first_party_moves": first_party_moves,
        "packages_lock_cleanup": lock_cleanup,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
