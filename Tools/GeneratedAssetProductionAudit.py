#!/usr/bin/env python3
"""Static production-package audit for generated HECTON-8 visual assets.

This is source-only proof. It does not open Unity, import assets, or claim render
quality. It catches missing LOD/COL/prefab/material/proof package pieces so
procedural output cannot be accepted just because files exist.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


LOD_RE = re.compile(r"^(?P<stem>.+)_LOD(?P<lod>[012])(?:_Mesh)?\.asset$", re.IGNORECASE)
COL_RE = re.compile(r"^(?:(?P<prefix>COL)_(?P<stem_a>.+)|(?P<stem_b>.+)_COL)\.asset$", re.IGNORECASE)
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
MATERIAL_REF_RE = re.compile(r"m_Materials:\s*(?:\r?\n\s*-\s*\{fileID:\s*2100000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*2\})+")
SINGLE_MATERIAL_GUID_RE = re.compile(r"\{fileID:\s*2100000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*2\}")
FAMILY_ID_RE = re.compile(r"^\s*familyId:\s*(.+?)\s*$", re.MULTILINE)
ALLOW_RUNTIME_SCATTER_RE = re.compile(r"^\s*allowRuntimeScatter:\s*1\s*$", re.MULTILINE)
VARIANT_BLOCK_RE = re.compile(
    r"^\s*-\s+variantId:\s*(?P<variant>[^\r\n]+)(?P<body>.*?)(?=^\s*-\s+variantId:|^\s{2}[A-Za-z_]\w*:|\Z)",
    re.MULTILINE | re.DOTALL,
)
PREFAB_GUID_RE = re.compile(r"prefab:\s*\{fileID:\s*[-0-9]+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}")

PROCEDURAL_FAMILY_ROOT = "Assets/_Project/Data/World/ProceduralFamilies"
FINAL_PREFAB_ROOTS = (
    "Assets/_Project/Prefabs/Construction/Final",
    "Assets/_Project/Prefabs/Nature/OrganicMisc/Final",
    "Assets/_Project/Prefabs/WorldSupport/Final",
)
PRODUCT_FACE_PREFAB_ROOTS = (
    "Assets/_Project/Prefabs/Tools/Held",
    "Assets/_Project/Prefabs/Items/Tools",
    "Assets/_Project/Prefabs/Resources/Pickups",
    "Assets/_Project/Prefabs/Transport",
)
PRODUCT_FACE_PREFAB_FILES = (
    "Assets/_Project/Prefabs/Player.prefab",
    "Assets/_Project/Prefabs/Sky_System.prefab",
    "Assets/_Project/Prefabs/Ocean_Crest.prefab",
    "Assets/_Project/Prefabs/Item_Titanium.prefab",
    "Assets/_Project/Prefabs/STRUCTURES.prefab",
    "Assets/_Project/Prefabs/Buildings/Cube.prefab",
)
BUILTIN_PRIMITIVE_GUID = "0000000000000000e000000000000000"
PLACEHOLDER_MARKER = "WorldProceduralPlaceholderMarker"

BAD_NAME_MARKERS = (
    "placeholder",
    "debug",
    "mock",
    "temp",
    "todo",
    "crayon",
    "flatcolor",
    "flat_color",
    "lowpoly",
    "low_poly",
)


@dataclass
class Package:
    family: str
    stem: str
    root: str
    lods: dict[int, str] = field(default_factory=dict)
    col: list[str] = field(default_factory=list)
    prefabs: list[str] = field(default_factory=list)
    material_guids: list[str] = field(default_factory=list)
    material_paths: list[str] = field(default_factory=list)
    manifest_paths: list[str] = field(default_factory=list)
    proof_paths: list[str] = field(default_factory=list)
    issues: list[dict[str, str]] = field(default_factory=list)

    def rel(self) -> dict[str, object]:
        return {
            "family": self.family,
            "stem": self.stem,
            "root": self.root,
            "lods": {str(k): v for k, v in sorted(self.lods.items())},
            "colliders": self.col,
            "prefabs": self.prefabs,
            "materials": self.material_paths,
            "manifests": self.manifest_paths,
            "proof": self.proof_paths,
            "issues": self.issues,
        }


@dataclass(frozen=True)
class AuditFamily:
    name: str
    root: str
    expect_col: bool
    expect_prefab: bool
    source_only: bool
    shallow_or_surface: bool


FAMILIES = (
    AuditFamily(
        name="bioforge_shallow_source_meshes",
        root="Assets/_Project/Art/Generated/Flora/BioForge/Shallows",
        expect_col=False,
        expect_prefab=False,
        source_only=True,
        shallow_or_surface=True,
    ),
    AuditFamily(
        name="baked_flora_prefabs",
        root="Assets/_Project/Prefabs/Nature/Flora/Baked",
        expect_col=False,
        expect_prefab=True,
        source_only=False,
        shallow_or_surface=True,
    ),
    AuditFamily(
        name="world_procedural_geology_meshes",
        root="Assets/_Project/Art/Meshes/WorldProceduralGeology",
        expect_col=True,
        expect_prefab=False,
        source_only=True,
        shallow_or_surface=True,
    ),
    AuditFamily(
        name="rock_sculptor_1713_meshes",
        root="Assets/_Project/Art/Meshes/GeologyForge/RockSculptor1713",
        expect_col=True,
        expect_prefab=False,
        source_only=True,
        shallow_or_surface=True,
    ),
    AuditFamily(
        name="rock_sculptor_1713_prefabs",
        root="Assets/_Project/Prefabs/GeologyForge/RockSculptor1713",
        expect_col=True,
        expect_prefab=True,
        source_only=False,
        shallow_or_surface=True,
    ),
)


def rel(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def build_guid_index(root: Path) -> dict[str, str]:
    index: dict[str, str] = {}
    for meta in root.rglob("*.meta"):
        text = read_text(meta)
        match = GUID_RE.search(text)
        if not match:
            continue
        asset = meta.with_suffix("")
        if asset.exists():
            index[match.group(1).lower()] = rel(root, asset)
    return index


def collect_prefabs(root: Path) -> dict[str, list[Path]]:
    prefabs: dict[str, list[Path]] = {}
    for path in root.rglob("*.prefab"):
        prefabs.setdefault(path.stem, []).append(path)
    return prefabs


def collect_named_artifacts(root: Path, needles: Iterable[str]) -> dict[str, list[str]]:
    lowered_needles = [needle.lower() for needle in needles if needle]
    artifacts: dict[str, list[str]] = {needle: [] for needle in lowered_needles}
    search_roots = [
        root / "Docs" / "Reports",
        root / "Docs" / "Screenshots",
        root / "Docs" / "AgentLogs",
        root / "Docs" / "Orchestration",
    ]
    for search_root in search_roots:
        if not search_root.exists():
            continue
        for path in search_root.rglob("*"):
            if not path.is_file():
                continue
            name = path.name.lower()
            for needle in lowered_needles:
                if needle in name:
                    artifacts[needle].append(rel(root, path))
    return artifacts


def discover_packages(root: Path, family: AuditFamily, all_prefabs: dict[str, list[Path]]) -> dict[str, Package]:
    packages: dict[str, Package] = {}
    family_root = root / family.root
    if not family_root.exists():
        return packages

    for asset in family_root.rglob("*.asset"):
        lod_match = LOD_RE.match(asset.name)
        if lod_match:
            stem = lod_match.group("stem")
            package = packages.setdefault(stem, Package(family=family.name, stem=stem, root=family.root))
            package.lods[int(lod_match.group("lod"))] = rel(root, asset)
            continue

        col_match = COL_RE.match(asset.name)
        if col_match:
            stem = col_match.group("stem_a") or col_match.group("stem_b")
            package = packages.setdefault(stem, Package(family=family.name, stem=stem, root=family.root))
            package.col.append(rel(root, asset))

    for prefab in family_root.rglob("*.prefab"):
        package = packages.setdefault(prefab.stem, Package(family=family.name, stem=prefab.stem, root=family.root))
        package.prefabs.append(rel(root, prefab))

    for stem, package in packages.items():
        if not package.prefabs and stem in all_prefabs:
            package.prefabs.extend(rel(root, prefab) for prefab in all_prefabs[stem])

    return packages


def extract_materials(root: Path, package: Package, guid_index: dict[str, str]) -> None:
    seen: set[str] = set()
    for prefab_rel in package.prefabs:
        text = read_text(root / prefab_rel)
        for guid in SINGLE_MATERIAL_GUID_RE.findall(text):
            guid_l = guid.lower()
            if guid_l in seen:
                continue
            seen.add(guid_l)
            package.material_guids.append(guid_l)
            path = guid_index.get(guid_l, "")
            if path:
                package.material_paths.append(path)
            else:
                package.issues.append({
                    "severity": "ERROR",
                    "code": "MATERIAL_GUID_UNRESOLVED",
                    "detail": f"{prefab_rel} references material guid {guid_l} not found in meta index.",
                })


def find_manifest_and_proof(root: Path, package: Package, artifact_index: dict[str, list[str]]) -> None:
    stem_l = package.stem.lower()
    for path in (root / package.root).rglob("*"):
        if not path.is_file():
            continue
        name_l = path.name.lower()
        if stem_l in name_l and "manifest" in name_l:
            package.manifest_paths.append(rel(root, path))

    for artifact in artifact_index.get(stem_l, []):
        package.proof_paths.append(artifact)


def add_issue(package: Package, severity: str, code: str, detail: str) -> None:
    package.issues.append({"severity": severity, "code": code, "detail": detail})


def validate_package(family: AuditFamily, package: Package) -> None:
    for lod in range(3):
        if lod not in package.lods:
            add_issue(package, "FATAL", "MISSING_LOD", f"{package.stem} lacks LOD{lod} mesh.")

    if family.expect_col and not package.col:
        add_issue(package, "ERROR", "MISSING_COLLISION_PROXY", f"{package.stem} lacks COL mesh/proxy in {package.root}.")

    if family.expect_prefab and not package.prefabs:
        add_issue(package, "ERROR", "MISSING_PREFAB", f"{package.stem} lacks runtime prefab.")

    if package.prefabs and not package.material_paths:
        add_issue(package, "ERROR", "PREFAB_HAS_NO_MATERIAL_PROOF", f"{package.stem} prefab has no resolvable shared material references.")

    for path in package.lods.values():
        name_l = Path(path).name.lower()
        for marker in BAD_NAME_MARKERS:
            if marker in name_l:
                add_issue(package, "ERROR", "BAD_ASSET_NAME_MARKER", f"{path} contains marker '{marker}'.")

    for material in package.material_paths:
        material_l = material.lower()
        for marker in BAD_NAME_MARKERS:
            if marker in material_l:
                add_issue(package, "ERROR", "BAD_MATERIAL_NAME_MARKER", f"{material} contains marker '{marker}'.")

    if family.source_only:
        add_issue(
            package,
            "WARN",
            "SOURCE_ONLY_PACKAGE",
            f"{package.stem} is source/library output; production acceptance still needs assembled prefab, manifest, and visual proof.",
        )

    if not package.manifest_paths:
        add_issue(package, "WARN", "MISSING_MANIFEST", f"{package.stem} has no local MANIFEST file.")

    if not package.proof_paths:
        add_issue(package, "WARN", "MISSING_NAMED_PROOF", f"{package.stem} has no filename-matched report/screenshot proof.")

    if family.shallow_or_surface and not package.proof_paths:
        add_issue(
            package,
            "WARN",
            "SURFACE_SHALLOW_VISUAL_PROOF_PENDING",
            f"{package.stem} is shallow/surface-adjacent; Subnautica-level visual claim remains pending without screenshot/render proof.",
        )


def discover_family_link_packages(root: Path, guid_index: dict[str, str]) -> list[Package]:
    family_root = root / PROCEDURAL_FAMILY_ROOT
    if not family_root.exists():
        return []

    packages: list[Package] = []
    for asset in sorted(family_root.glob("*.asset")):
        text = read_text(asset)
        family_match = FAMILY_ID_RE.search(text)
        family_id = family_match.group(1).strip() if family_match else asset.stem
        package = Package(family="procedural_family_links", stem=family_id, root=PROCEDURAL_FAMILY_ROOT)
        package.manifest_paths.append(rel(root, asset))

        allow_runtime_scatter = ALLOW_RUNTIME_SCATTER_RE.search(text) is not None
        final_nonproxy_count = 0
        real_final_count = 0
        placeholder_final_count = 0

        for match in VARIANT_BLOCK_RE.finditer(text):
            variant_id = match.group("variant").strip()
            body = match.group("body")
            final_ready = re.search(r"^\s*finalReady:\s*1\s*$", body, re.MULTILINE) is not None
            proxy_only = re.search(r"^\s*proxyOnly:\s*1\s*$", body, re.MULTILINE) is not None
            if not final_ready or proxy_only:
                continue

            final_nonproxy_count += 1
            prefab_match = PREFAB_GUID_RE.search(body)
            if not prefab_match:
                add_issue(
                    package,
                    "ERROR",
                    "FAMILY_FINAL_READY_PREFAB_MISSING",
                    f"{variant_id} is final-ready/non-proxy but has no resolvable prefab reference.",
                )
                continue

            prefab_guid = prefab_match.group(1).lower()
            prefab_path = guid_index.get(prefab_guid, "")
            if not prefab_path:
                add_issue(
                    package,
                    "ERROR",
                    "FAMILY_PREFAB_GUID_UNRESOLVED",
                    f"{variant_id} references prefab guid {prefab_guid} not found in meta index.",
                )
                continue

            if prefab_path not in package.prefabs:
                package.prefabs.append(prefab_path)

            prefab_text = read_text(root / prefab_path)
            prefab_path_l = prefab_path.lower()
            is_placeholder = "proceduralplaceholders" in prefab_path_l or PLACEHOLDER_MARKER in prefab_text
            uses_builtin_mesh = BUILTIN_PRIMITIVE_GUID in prefab_text
            if is_placeholder:
                placeholder_final_count += 1
                add_issue(
                    package,
                    "ERROR",
                    "FAMILY_FINAL_READY_PLACEHOLDER",
                    f"{variant_id} points at placeholder prefab {prefab_path}.",
                )
                continue

            if uses_builtin_mesh:
                add_issue(
                    package,
                    "ERROR",
                    "FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH",
                    f"{variant_id} points at prefab {prefab_path} using Unity built-in primitive mesh ids.",
                )
                continue

            real_final_count += 1

        if final_nonproxy_count > 0 and real_final_count <= 0 and placeholder_final_count > 0:
            add_issue(
                package,
                "ERROR",
                "FAMILY_PLACEHOLDER_ONLY_PRODUCTION_LINKS",
                f"{family_id} has final-ready/non-proxy variants but all resolved finals are placeholders.",
            )
        elif allow_runtime_scatter and real_final_count <= 0:
            add_issue(
                package,
                "WARN",
                "FAMILY_NO_REAL_FINAL_LINKS",
                f"{family_id} allows runtime scatter but has no resolved real final-ready/non-proxy prefab links.",
            )

        packages.append(package)

    return packages


def discover_final_prefab_root_packages(root: Path) -> list[Package]:
    packages: list[Package] = []
    for final_root_rel in FINAL_PREFAB_ROOTS:
        final_root = root / final_root_rel
        if not final_root.exists():
            continue

        for prefab in sorted(final_root.rglob("*.prefab")):
            prefab_rel = rel(root, prefab)
            prefab_text = read_text(prefab)
            package = Package(family="final_prefab_roots", stem=prefab.stem, root=final_root_rel)
            package.prefabs.append(prefab_rel)

            if PLACEHOLDER_MARKER in prefab_text:
                add_issue(
                    package,
                    "ERROR",
                    "FINAL_PREFAB_PLACEHOLDER_MARKER",
                    f"{prefab_rel} contains {PLACEHOLDER_MARKER}; production Final prefabs cannot carry placeholder markers.",
                )

            if BUILTIN_PRIMITIVE_GUID in prefab_text:
                add_issue(
                    package,
                    "ERROR",
                    "FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH",
                    f"{prefab_rel} uses Unity built-in primitive mesh ids; production Final prefabs need authored/generated meshes.",
                )

            if package.issues:
                packages.append(package)

    return packages


def discover_product_face_prefab_packages(root: Path) -> list[Package]:
    packages: list[Package] = []
    prefab_paths: list[Path] = []

    for product_root_rel in PRODUCT_FACE_PREFAB_ROOTS:
        product_root = root / product_root_rel
        if not product_root.exists():
            continue
        prefab_paths.extend(sorted(product_root.rglob("*.prefab")))

    for prefab_rel in PRODUCT_FACE_PREFAB_FILES:
        prefab_path = root / prefab_rel
        if prefab_path.exists():
            prefab_paths.append(prefab_path)

    seen: set[str] = set()
    for prefab in prefab_paths:
        prefab_rel = rel(root, prefab)
        if prefab_rel in seen:
            continue
        seen.add(prefab_rel)

        prefab_text = read_text(prefab)
        package = Package(family="product_face_prefabs", stem=prefab.stem, root="Assets/_Project/Prefabs")
        package.prefabs.append(prefab_rel)

        if PLACEHOLDER_MARKER in prefab_text:
            add_issue(
                package,
                "ERROR",
                "PRODUCT_FACE_PREFAB_PLACEHOLDER_MARKER",
                f"{prefab_rel} contains {PLACEHOLDER_MARKER}; product-face prefabs cannot carry placeholder markers.",
            )

        if BUILTIN_PRIMITIVE_GUID in prefab_text:
            add_issue(
                package,
                "ERROR",
                "PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH",
                f"{prefab_rel} uses Unity built-in primitive mesh ids; player-facing art needs authored/generated meshes or hidden input-only proof.",
            )

        if package.issues:
            packages.append(package)

    return packages


def summarize(packages: list[Package]) -> dict[str, object]:
    counts: dict[str, int] = {"FATAL": 0, "ERROR": 0, "WARN": 0}
    by_code: dict[str, int] = {}
    by_family: dict[str, dict[str, int]] = {}
    for package in packages:
        family_counts = by_family.setdefault(package.family, {"packages": 0, "FATAL": 0, "ERROR": 0, "WARN": 0})
        family_counts["packages"] += 1
        for issue in package.issues:
            severity = issue["severity"]
            counts[severity] = counts.get(severity, 0) + 1
            family_counts[severity] = family_counts.get(severity, 0) + 1
            code = issue["code"]
            by_code[code] = by_code.get(code, 0) + 1
    return {"counts": counts, "byCode": by_code, "byFamily": by_family}


def write_markdown(path: Path, report: dict[str, object], packages: list[Package]) -> None:
    summary = report["summary"]
    lines = [
        "# Generated Asset Production Audit 1851",
        "",
        "Evidence class: SOURCE_ONLY_STATIC_AUDIT. No Unity import, render, profiler, or runtime proof is claimed.",
        "",
        "Purpose: reject generated visual output as production-ready unless its package has LODs, collider/prefab/material routing where required, manifest, and proof artifacts.",
        "Family-link scan: procedural family assets are also checked so final-ready/non-proxy variants cannot point at placeholder or Unity primitive prefabs.",
        "Final-prefab root scan: production Final prefab folders are checked directly so unlinked primitive finals cannot remain hidden for later relinking.",
        "Product-face prefab scan: player, tool, pickup, transport, sky, and ocean prefabs are checked so first-minute visible blockout art cannot hide outside Final folders.",
        "",
        "## Summary",
        "",
        f"- Packages scanned: {len(packages)}",
        f"- Fatal issues: {summary['counts'].get('FATAL', 0)}",
        f"- Error issues: {summary['counts'].get('ERROR', 0)}",
        f"- Warning issues: {summary['counts'].get('WARN', 0)}",
        "",
        "## By Family",
        "",
    ]
    for family, counts in sorted(summary["byFamily"].items()):
        lines.append(
            f"- {family}: packages={counts.get('packages', 0)}, fatal={counts.get('FATAL', 0)}, "
            f"error={counts.get('ERROR', 0)}, warn={counts.get('WARN', 0)}"
        )

    lines.extend(["", "## Issue Codes", ""])
    for code, count in sorted(summary["byCode"].items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"- {code}: {count}")

    lines.extend(["", "## Fatal And Error Issues", ""])
    high_severity_written = 0
    for package in packages:
        for issue in package.issues:
            if issue["severity"] not in ("FATAL", "ERROR"):
                continue
            lines.append(
                f"- [{issue['severity']}] {issue['code']} | {package.family}/{package.stem}: {issue['detail']}"
            )
            high_severity_written += 1

    if high_severity_written <= 0:
        lines.append("- None.")

    lines.extend(["", "## First 160 Warning Samples", ""])
    written = 0
    for package in packages:
        for issue in package.issues:
            if issue["severity"] != "WARN":
                continue
            if written >= 160:
                break
            lines.append(
                f"- [{issue['severity']}] {issue['code']} | {package.family}/{package.stem}: {issue['detail']}"
            )
            written += 1
        if written >= 160:
            break

    lines.extend([
        "",
        "## Acceptance Rule",
        "",
        "This report cannot prove beauty. It only prevents package-level false completion. Any surface, shallow, coast, or hero-route asset still needs render/screenshot proof against TASTE.md and the Subnautica-level visual floor.",
        "",
    ])
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".", help="Repository root, default: current directory.")
    parser.add_argument("--json", default="Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.json")
    parser.add_argument("--markdown", default="Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md")
    parser.add_argument("--fail-on-fatal", action="store_true")
    parser.add_argument("--fail-on-error", action="store_true")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    guid_index = build_guid_index(root)
    all_prefabs = collect_prefabs(root)

    discovered: list[Package] = []
    for family in FAMILIES:
        discovered.extend(discover_packages(root, family, all_prefabs).values())
    family_link_packages = discover_family_link_packages(root, guid_index)
    final_prefab_root_packages = discover_final_prefab_root_packages(root)
    product_face_prefab_packages = discover_product_face_prefab_packages(root)

    artifact_index = collect_named_artifacts(root, (package.stem for package in discovered))
    for family in FAMILIES:
        family_packages = [package for package in discovered if package.family == family.name]
        for package in family_packages:
            extract_materials(root, package, guid_index)
            find_manifest_and_proof(root, package, artifact_index)
            validate_package(family, package)

    discovered.extend(family_link_packages)
    discovered.extend(final_prefab_root_packages)
    discovered.extend(product_face_prefab_packages)
    discovered.sort(key=lambda package: (package.family, package.stem))
    report = {
        "evidenceClass": "SOURCE_ONLY_STATIC_AUDIT",
        "generatedAtUtc": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "summary": summarize(discovered),
        "packages": [package.rel() for package in discovered],
    }

    json_path = root / args.json
    md_path = root / args.markdown
    json_path.parent.mkdir(parents=True, exist_ok=True)
    md_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_markdown(md_path, report, discovered)

    counts = report["summary"]["counts"]
    print(
        "generated_asset_packages="
        f"{len(discovered)} fatal={counts.get('FATAL', 0)} "
        f"error={counts.get('ERROR', 0)} warn={counts.get('WARN', 0)}"
    )
    print(f"json={args.json}")
    print(f"markdown={args.markdown}")

    if args.fail_on_fatal and counts.get("FATAL", 0) > 0:
        return 2
    if args.fail_on_error and counts.get("ERROR", 0) > 0:
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
