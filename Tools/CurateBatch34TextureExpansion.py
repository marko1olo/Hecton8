#!/usr/bin/env python3
"""Build a curated downstream package from Batch34 texture intake output."""

from __future__ import annotations

import csv
import filecmp
import json
import math
import os
import shutil
import stat
import time
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion"
QA_DIR = OUTPUT_ROOT / "QA"
CONTACT_DIR = OUTPUT_ROOT / "ContactSheets"
CURATED_DIR = OUTPUT_ROOT / "Curated"
MANIFEST_PATH = QA_DIR / "Batch34_TextureExpansion_IntakeManifest.json"
REGEN_TARGETS_MANIFEST = OUTPUT_ROOT / "RegenTargets/QA/Batch34_RegenTargets_IntakeManifest.json"


CURATION_OVERRIDES: dict[str, dict[str, str]] = {
    "B34-3401": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Terrain detail and triplanar blend for photic limestone shelves.",
        "integrationNote": "Use as detail/blend layer first; broad flat hero terrain still needs Unity route screenshot.",
    },
    "B34-3402": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Shallow root-mat/sand transition detail layer.",
        "integrationNote": "Dense fine fibers hide repeat acceptably at gameplay scale; avoid huge single-material planes.",
    },
    "B34-3403": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Brine canyon salt-crust/silt terrain detail.",
        "integrationNote": "Good mineral pattern; verify normals do not over-crack brine route readability.",
    },
    "B34-3404": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Abyssal nodule plain ground material and resource-biome base.",
        "integrationNote": "Nodule scale reads well; use with scatter meshes to avoid flat painted resource feel.",
    },
    "B34-3406": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Directional serpentinite/fault wall material.",
        "integrationNote": "Use on walls/slabs or triplanar rock, not as a perfectly isotropic floor tile.",
    },
    "B34-3407": {
        "curationStatus": "LOCAL_ONLY_OR_REGEN_SEAMLESS",
        "targetRole": "Iron seep local patch or reference for a regenerated seamless material.",
        "integrationNote": "2x2 exposes a repeated circular/rust landmark; do not use as broad terrain tile.",
    },
    "B34-3409": {
        "curationStatus": "REGEN_RECOMMENDED",
        "targetRole": "Reference only for limestone drip direction.",
        "integrationNote": "Visible block repeat and vertical landmarking; request regenerated seamless cave mineral tile if needed.",
    },
    "B34-3410": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Drowned concrete rubble local terrain blend.",
        "integrationNote": "Useful for ruin transitions; combine with meshes/decals before hero-route use.",
    },
    "B34-3413": {
        "curationStatus": "LOCAL_ONLY_STATIC",
        "targetRole": "Wet service deck fixed panel source, not a seamless terrain/floor tile.",
        "integrationNote": "Yellow/smudge landmarks repeat; use as authored panel/trim crop or regenerate pure anti-slip tile.",
    },
    "B34-3415": {
        "curationStatus": "LOCAL_ONLY_STATIC",
        "targetRole": "Cable repair-wrap local variant source.",
        "integrationNote": "Good prop texture, but broad bands make it unsuitable as generic repeating cable material.",
    },
    "B34-3417": {
        "curationStatus": "LOCAL_ONLY_OR_CENTER_CROP",
        "targetRole": "Amber lens source after center crop or fixed lamp panel use.",
        "integrationNote": "Hard border tiles in 2x2; crop central ribbed lens before using as repeat material.",
    },
    "B34-3421": {
        "curationStatus": "CURATED_READY_STATIC",
        "targetRole": "Damped insulation blanket for interiors/equipment backing.",
        "integrationNote": "Quilt repeat is expected; still needs Unity roughness/normal review.",
    },
    "B34-3418": {
        "curationStatus": "REGEN_OR_MANUAL_MATTE",
        "targetRole": "Viewport glass wear decal reference only until background is matted.",
        "integrationNote": "Baked checkerboard/removable background risk; do not import as alpha decal directly.",
    },
    "B34-3424": {
        "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
        "targetRole": "Paint chip/scratch decal atlas after padding check.",
        "integrationNote": "Good source, but some islands are near edges; split/pad before decal texture array import.",
    },
    "B34-3426": {
        "curationStatus": "CURATED_READY_ALPHA_SOURCE",
        "targetRole": "Instrument glass smudge/scratch alpha source.",
        "integrationNote": "Black background drives clipping warning by design; extract mask before shader use.",
    },
    "B34-3433": {
        "curationStatus": "CURATED_READY_ALPHA_SOURCE",
        "targetRole": "Brine vane flora UV source with dark matte background.",
        "integrationNote": "Clipping is background/matte, not content failure; split islands before mesh UV use.",
    },
    "B34-3438": {
        "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
        "targetRole": "Tube worm crown/tube UV source after island padding.",
        "integrationNote": "Useful material set, but edge risk means atlas must be split or expanded before import.",
    },
    "B34-3440": {
        "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
        "targetRole": "Cave lichen/biofilm overlay source after padding.",
        "integrationNote": "Good deep-organic palette; edge islands need split/pad for decal use.",
    },
    "B34-3442": {
        "curationStatus": "CURATED_READY_ALPHA_SOURCE",
        "targetRole": "Filter feeder gill membrane UV source.",
        "integrationNote": "Black matte background caused clipping warning; content is usable after island extraction.",
    },
    "B34-3443": {
        "curationStatus": "MANUAL_SPLIT_BEFORE_IMPORT",
        "targetRole": "Small predator feature material reference.",
        "integrationNote": "Contains strong object/head forms, not clean UV islands; split useful zones before production use.",
    },
    "B34-3444": {
        "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
        "targetRole": "Armored benthic shell UV source after padding.",
        "integrationNote": "Good shell language; edge proximity must be fixed before mesh atlas import.",
    },
    "B34-3446": {
        "curationStatus": "CURATED_READY_ALPHA_SOURCE",
        "targetRole": "Carcass bone/flesh UV source.",
        "integrationNote": "Dark matte background causes black clipping warning; isolate islands before use.",
    },
    "B34-3447": {
        "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
        "targetRole": "Creature eye/sensory organ UV source after padding.",
        "integrationNote": "Good close-inspection material; edge islands need split/pad before import.",
    },
}


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def load_entries() -> list[dict]:
    data = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return list(data["entries"])


def load_regen_overrides() -> dict[str, dict]:
    if not REGEN_TARGETS_MANIFEST.exists():
        return {}
    payload = json.loads(REGEN_TARGETS_MANIFEST.read_text(encoding="utf-8"))
    overrides: dict[str, dict] = {}
    for entry in payload.get("entries", []):
        if not entry.get("selected"):
            continue
        source_id = str(entry.get("sourceId", "")).strip()
        final_candidate = str(entry.get("finalCandidatePath", "")).strip()
        if not source_id or not final_candidate:
            continue
        source_type = str(entry.get("sourceType", "")).strip()
        if source_type == "DECAL_ATLAS":
            status = "CURATED_READY_ALPHA_SOURCE"
        elif source_type in {"SEAMLESS_TILE", "TRIM_SHEET"}:
            status = "CURATED_READY_STATIC"
        else:
            continue
        overrides[source_id] = {
            "curationStatus": status,
            "baseColorCandidatePath": final_candidate,
            "downloadSource": str(entry.get("downloadSource", "")),
            "maps": entry.get("maps", {}),
            "regenTargetId": str(entry.get("id", "")),
            "regenTargetVariant": str(entry.get("variant", "")),
            "regenTargetDecision": str(entry.get("decision", "")),
            "regenTargetManifest": project_rel(REGEN_TARGETS_MANIFEST),
            "regenBroadSeamlessAccepted": bool(entry.get("broadSeamlessAccepted", False)),
            "integrationNote": f"Selected targeted regen candidate: {entry.get('decision', '')}. {entry.get('note', '')}",
        }
    return overrides


def default_curation(entry: dict) -> dict[str, str]:
    if entry["verdict"] == "INTAKE_READY_STATIC":
        return {
            "curationStatus": "CURATED_READY_STATIC",
            "targetRole": str(entry["use"]),
            "integrationNote": "Static source accepted; still requires Unity import/material preview before production claim.",
        }
    return {
        "curationStatus": "REVIEW_REQUIRED_STATIC",
        "targetRole": str(entry["use"]),
        "integrationNote": "Automated intake warning still unresolved; inspect source before Unity import.",
    }


def apply_curation(entries: list[dict]) -> list[dict]:
    curated: list[dict] = []
    regen_overrides = load_regen_overrides()
    for entry in entries:
        merged = dict(entry)
        curation = default_curation(entry)
        curation.update(CURATION_OVERRIDES.get(str(entry["id"]), {}))
        curation.update(regen_overrides.get(str(entry["id"]), {}))
        merged.update(curation)
        curated.append(merged)
    return curated


def curation_bucket(status: str) -> str:
    if status.startswith("CURATED_READY"):
        return "ReadyStatic"
    if status.startswith("LOCAL_ONLY"):
        return "LocalOnly"
    return "NeedsWork"


def make_writable(path: Path) -> None:
    try:
        os.chmod(path, stat.S_IWRITE | stat.S_IREAD)
    except OSError:
        pass


def remove_best_effort(path: Path, attempts: int = 5) -> bool:
    for attempt in range(attempts):
        try:
            if path.is_dir():
                shutil.rmtree(path, onerror=lambda func, raw, exc: make_writable(Path(raw)))
            else:
                make_writable(path)
                path.unlink()
            return True
        except PermissionError:
            if attempt + 1 >= attempts:
                return False
            time.sleep(0.2)
        except FileNotFoundError:
            return True
    return False


def copy2_idempotent(src: Path, dst: Path) -> None:
    try:
        shutil.copy2(src, dst)
        return
    except PermissionError:
        if dst.exists() and filecmp.cmp(src, dst, shallow=False):
            return
        raise


def copy_curated_assets(entries: list[dict]) -> None:
    if CURATED_DIR.exists():
        for path in CURATED_DIR.glob("*"):
            remove_best_effort(path)
    for entry in entries:
        rel = entry.get("baseColorCandidatePath")
        if not rel:
            continue
        src = ROOT / str(rel)
        if not src.exists():
            continue
        bucket = curation_bucket(str(entry["curationStatus"]))
        dst_dir = CURATED_DIR / bucket
        dst_dir.mkdir(parents=True, exist_ok=True)
        dst = dst_dir / src.name
        copy2_idempotent(src, dst)
        entry["curatedBaseColorPath"] = project_rel(dst)


def write_csv(entries: list[dict], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "id",
        "title",
        "sourceType",
        "family",
        "verdict",
        "curationStatus",
        "targetRole",
        "integrationNote",
        "baseColorCandidatePath",
        "curatedBaseColorPath",
        "tilePreviewPath",
        "unityImportStatus",
        "visualStatus",
    ]
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for entry in entries:
            writer.writerow({field: entry.get(field, "") for field in fields})


def contact_sheet(entries: list[dict], out_path: Path, thumb: int = 180) -> None:
    items: list[tuple[str, Image.Image, str]] = []
    for entry in entries:
        rel = entry.get("baseColorCandidatePath")
        if not rel:
            continue
        path = ROOT / str(rel)
        if not path.exists():
            continue
        with Image.open(path) as img:
            image = ImageOps.exif_transpose(img).convert("RGB")
            image.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
            items.append((str(entry["id"]), image.copy(), str(entry["curationStatus"])))
    if not items:
        return
    label_h = 44
    gap = 8
    columns = min(5, len(items))
    rows = int(math.ceil(len(items) / columns))
    canvas = Image.new("RGB", (columns * thumb + (columns - 1) * gap, rows * (thumb + label_h) + (rows - 1) * gap), (9, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, (label, image, status) in enumerate(items):
        cell_x = (index % columns) * (thumb + gap)
        cell_y = (index // columns) * (thumb + label_h + gap)
        x = cell_x + (thumb - image.width) // 2
        y = cell_y + (thumb - image.height) // 2
        canvas.paste(image, (x, y))
        if status.startswith("CURATED_READY"):
            color = (5, 22, 18)
        elif status.startswith("LOCAL_ONLY"):
            color = (42, 36, 10)
        else:
            color = (43, 18, 12)
        draw.rectangle((cell_x, cell_y + thumb, cell_x + thumb, cell_y + thumb + label_h), fill=color)
        draw.text((cell_x + 5, cell_y + thumb + 5), label, fill=(230, 235, 230))
        draw.text((cell_x + 5, cell_y + thumb + 22), status[:30], fill=(205, 215, 215))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out_path, "PNG")


def write_markdown(entries: list[dict], path: Path, csv_path: Path, manifest_path: Path) -> None:
    counts: dict[str, int] = {}
    for entry in entries:
        status = str(entry["curationStatus"])
        counts[status] = counts.get(status, 0) + 1
    lines = [
        "# Batch34 Texture Expansion Curation",
        "",
        "Evidence class: STATIC_IMAGE_REVIEW_PLUS_AUTOMATED_INTAKE.",
        "Unity was not run. These are downstream source candidates, not imported production assets.",
        "",
        f"Curated manifest: `{project_rel(manifest_path)}`",
        f"Unity import queue CSV: `{project_rel(csv_path)}`",
        "",
        "## Counts",
        "",
    ]
    for status in sorted(counts):
        lines.append(f"- {status}: {counts[status]}")
    lines += [
        "",
        "## Import Policy",
        "",
        "- `CURATED_READY_STATIC`: may be queued for Unity import/material preview.",
        "- `CURATED_READY_ALPHA_SOURCE`: usable source, but first extract island/mask/alpha from dark matte background.",
        "- `LOCAL_ONLY_STATIC`: useful art source, but not for the originally requested broad seamless/material role.",
        "- `LOCAL_ONLY_OR_REGEN_SEAMLESS`, `LOCAL_ONLY_OR_CENTER_CROP`, `PAD_OR_SPLIT_BEFORE_IMPORT`, `MANUAL_SPLIT_BEFORE_IMPORT`, `REGEN_OR_MANUAL_MATTE`, `REGEN_RECOMMENDED`: keep as reference or fix before import.",
        "",
        "## Entries",
        "",
        "| ID | Curation | Type | Target role | Integration note | Candidate |",
        "|---|---|---|---|---|---|",
    ]
    for entry in entries:
        lines.append(
            f"| {entry['id']} | {entry['curationStatus']} | {entry['sourceType']} | "
            f"{entry['targetRole']} | {entry['integrationNote']} | "
            f"`{entry.get('curatedBaseColorPath', entry.get('baseColorCandidatePath', ''))}` |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    entries = apply_curation(load_entries())
    copy_curated_assets(entries)

    manifest_path = QA_DIR / "Batch34_TextureExpansion_CurationManifest.json"
    csv_path = QA_DIR / "Batch34_TextureExpansion_UnityImportQueue.csv"
    markdown_path = QA_DIR / "Batch34_TextureExpansion_Curation.md"

    payload = {
        "schema": "hecton8.batch34.texture_expansion_curation.v1",
        "date": "2026-06-08",
        "outputRoot": project_rel(OUTPUT_ROOT),
        "unityImportStatus": "PENDING UNITY IMPORT",
        "visualStatus": "STATIC CONTACT SHEET REVIEW ONLY",
        "entries": entries,
    }
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    write_csv(entries, csv_path)
    write_markdown(entries, markdown_path, csv_path, manifest_path)

    contact_sheet([e for e in entries if curation_bucket(str(e["curationStatus"])) == "ReadyStatic"], CONTACT_DIR / "Batch34_CuratedReady_Contact.png")
    contact_sheet([e for e in entries if curation_bucket(str(e["curationStatus"])) == "LocalOnly"], CONTACT_DIR / "Batch34_CuratedLocalOnly_Contact.png")
    contact_sheet([e for e in entries if curation_bucket(str(e["curationStatus"])) == "NeedsWork"], CONTACT_DIR / "Batch34_CuratedNeedsWork_Contact.png")

    counts: dict[str, int] = {}
    for entry in entries:
        bucket = curation_bucket(str(entry["curationStatus"]))
        counts[bucket] = counts.get(bucket, 0) + 1
    print("BATCH34_TEXTURE_EXPANSION_CURATION_DONE")
    print(f"ready_static={counts.get('ReadyStatic', 0)} local_only={counts.get('LocalOnly', 0)} needs_work={counts.get('NeedsWork', 0)}")
    print(f"curation={project_rel(markdown_path)}")
    print(f"queue={project_rel(csv_path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
