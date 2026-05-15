#!/usr/bin/env python3
"""Offline L-system validator and preview renderer for HECTON-8 flora genetics."""

from __future__ import annotations

import argparse
import datetime as _dt
import json
import math
import sys
from pathlib import Path


EXPECTED_LIBRARY_SCHEMA = "H8_LSYSTEM_LIBRARY_V1"
EXPECTED_TABLE_VERSION = 1
VALIDATION_SCHEMA = "H8_LSYSTEM_VALIDATION_V1"
METRICS_SCHEMA = "H8_LSYSTEM_METRICS_V1"
BRANCH_SYMBOLS = set("FACKTRVGBH")
BUD_SYMBOLS = set("LS")
DRAW_COMMANDS = BRANCH_SYMBOLS | BUD_SYMBOLS | set("+-[]")
SDF_ALLOWED = {"Capsule", "Cone", "TaperedCylinder"}
MAX_EXPANDED_CHARS = 24000
EXPECTED_BIOME_COUNTS = {
    "safe_shallows": ("SS_", 20),
    "kelp_forest": ("KF_", 20),
    "deep_abyss": ("DA_", 20),
    "thermal_vents": ("TV_", 20),
    "alien_caves": ("AC_", 20),
}
REQUIRED_LOD_TIERS = ("Low", "Middle", "High", "Ultra")
EXPECTED_FIELD_ORDER = [
    "id",
    "family",
    "axiom",
    "rules",
    "angle",
    "angleVariance",
    "stepSize",
    "iterationDepth",
    "budLogic",
    "biologicalLogic",
]


def fnv1a_64_bytes(data: bytes) -> str:
    value = 14695981039346656037
    for byte in data:
        value ^= byte
        value = (value * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return f"{value:016x}"


def species_fingerprint(species: list[object], table_version: object) -> str:
    payload = json.dumps(
        {"tableVersion": table_version, "species": species},
        ensure_ascii=False,
        separators=(",", ":"),
    )
    return fnv1a_64_bytes(payload.encode("utf-8"))


def stable_unit(seed: str) -> float:
    value = 2166136261
    for byte in seed.encode("utf-8"):
        value ^= byte
        value = (value * 16777619) & 0xFFFFFFFF
    return (value / 0xFFFFFFFF) * 2.0 - 1.0


def parse_rules(rule_text: str) -> dict[str, str]:
    rules: dict[str, str] = {}
    if not rule_text:
        return rules
    for chunk in rule_text.split(";"):
        if not chunk:
            continue
        if "=" not in chunk:
            raise ValueError(f"Rule chunk lacks '=': {chunk}")
        key, value = chunk.split("=", 1)
        key = key.strip()
        value = value.strip()
        if len(key) != 1:
            raise ValueError(f"Rule key must be one symbol: {chunk}")
        rules[key] = value
    return rules


def expand_recursive(source: str, rules: dict[str, str], depth: int, max_chars: int) -> str:
    if depth <= 0:
        return source
    next_text = "".join(rules.get(ch, ch) for ch in source)
    if len(next_text) > max_chars:
        raise OverflowError(f"Expanded string exceeded {max_chars} chars at depth {depth}: {len(next_text)}")
    return expand_recursive(next_text, rules, depth - 1, max_chars)


def walk_lsystem(text: str, angle_deg: float, angle_variance: float, step_size: float, seed: str) -> dict[str, object]:
    x = 0.0
    y = 0.0
    heading = math.pi / 2.0
    stack: list[tuple[float, float, float]] = []
    lines: list[tuple[float, float, float, float]] = []
    buds: list[tuple[float, float, str]] = []
    max_depth = 0
    turn_count = 0
    left_turns = 0
    right_turns = 0
    min_x = max_x = x
    min_y = max_y = y

    for index, ch in enumerate(text):
        if ch in BRANCH_SYMBOLS:
            scale = 1.0 + stable_unit(f"{seed}:{index}:step") * 0.08
            nx = x + math.cos(heading) * step_size * scale
            ny = y + math.sin(heading) * step_size * scale
            lines.append((x, y, nx, ny))
            x = nx
            y = ny
            min_x = min(min_x, x)
            max_x = max(max_x, x)
            min_y = min(min_y, y)
            max_y = max(max_y, y)
        elif ch == "L" or ch == "S":
            buds.append((x, y, ch))
        elif ch == "+" or ch == "-":
            jitter = stable_unit(f"{seed}:{index}:angle") * angle_variance
            delta = math.radians(angle_deg + jitter)
            if ch == "+":
                heading += delta
                left_turns += 1
            else:
                heading -= delta
                right_turns += 1
            turn_count += 1
        elif ch == "[":
            stack.append((x, y, heading))
            max_depth = max(max_depth, len(stack))
        elif ch == "]":
            if not stack:
                raise ValueError("Stack underflow while drawing")
            x, y, heading = stack.pop()
        elif ch not in DRAW_COMMANDS:
            raise ValueError(f"Unknown command '{ch}'")

    if stack:
        raise ValueError(f"Unclosed stack frames: {len(stack)}")

    width = max_x - min_x
    height = max_y - min_y
    aspect = width / max(0.001, height)
    return {
        "lines": lines,
        "buds": buds,
        "bounds": (min_x, min_y, max_x, max_y),
        "line_count": len(lines),
        "bud_count": len(buds),
        "max_stack": max_depth,
        "turn_count": turn_count,
        "left_turns": left_turns,
        "right_turns": right_turns,
        "aspect": aspect,
    }


def iter_species(library: dict[str, object]) -> list[tuple[str, list[object], dict[str, object]]]:
    result: list[tuple[str, list[object], dict[str, object]]] = []
    for biome in library.get("biomes", []):
        biome_id = str(biome["id"])
        for species in biome.get("species", []):
            result.append((biome_id, species, biome))
    return result


def validate_library_structure(library: dict[str, object]) -> list[str]:
    issues: list[str] = []
    if library.get("schema") != EXPECTED_LIBRARY_SCHEMA:
        issues.append("schema mismatch")
    if library.get("tableVersion") != EXPECTED_TABLE_VERSION:
        issues.append("tableVersion mismatch")
    if library.get("status") != "GENETICS STABILIZED":
        issues.append("library status is not GENETICS STABILIZED")
    if library.get("fieldOrder") != EXPECTED_FIELD_ORDER:
        issues.append("fieldOrder mismatch")
    branch_sdf = library.get("branchSDF")
    if not isinstance(branch_sdf, dict):
        issues.append("branchSDF missing or invalid")
    else:
        expected_symbols = set(BRANCH_SYMBOLS)
        actual_symbols = set(branch_sdf.keys())
        if actual_symbols != expected_symbols:
            missing = "".join(sorted(expected_symbols - actual_symbols))
            extra = "".join(sorted(actual_symbols - expected_symbols))
            issues.append(f"branchSDF symbol mismatch missing={missing} extra={extra}")
    bud_meshes = library.get("budMeshes")
    if bud_meshes != {"L": "Leaf", "S": "Seed"}:
        issues.append("budMeshes mismatch")

    math_lod = library.get("mathLOD")
    if not isinstance(math_lod, dict):
        issues.append("missing mathLOD")
    else:
        previous_cap = 0
        for tier in REQUIRED_LOD_TIERS:
            data = math_lod.get(tier)
            if not isinstance(data, dict):
                issues.append(f"missing mathLOD tier {tier}")
                continue
            depth_cap = int(data.get("depthCap", 0))
            if depth_cap < previous_cap:
                issues.append(f"mathLOD tier {tier} depthCap regresses")
            if depth_cap > 8:
                issues.append(f"mathLOD tier {tier} exceeds depth cap 8")
            previous_cap = depth_cap

    seen_biomes: set[str] = set()
    for biome in library.get("biomes", []):
        biome_id = str(biome.get("id", ""))
        seen_biomes.add(biome_id)
        expected = EXPECTED_BIOME_COUNTS.get(biome_id)
        if expected is None:
            issues.append(f"unexpected biome {biome_id}")
            continue
        expected_prefix, expected_count = expected
        species = biome.get("species", [])
        if len(species) != expected_count:
            issues.append(f"{biome_id} has {len(species)} species, expected {expected_count}")
        if not str(biome.get("taxonomy", "")).strip():
            issues.append(f"{biome_id} taxonomy is empty")
        for row in species:
            if len(row) != 10:
                issues.append(f"{biome_id} species row does not have 10 fields")
                continue
            if not str(row[0]).startswith(expected_prefix):
                issues.append(f"{row[0]} does not match biome prefix {expected_prefix}")

    for biome_id in EXPECTED_BIOME_COUNTS:
        if biome_id not in seen_biomes:
            issues.append(f"missing biome {biome_id}")
    return issues


def validate_species(library: dict[str, object], species: list[object], pass_index: int) -> tuple[str, dict[str, object], list[str]]:
    issues: list[str] = []
    species_id = str(species[0])
    axiom = str(species[2])
    rule_text = str(species[3])
    angle = float(species[4])
    angle_variance = float(species[5])
    step_size = float(species[6])
    depth = int(species[7])
    bud_logic = str(species[8])
    rules = parse_rules(rule_text)

    if depth > 8:
        issues.append("depth exceeds 8")
    if depth < 1:
        issues.append("depth below 1")
    if step_size <= 0.0:
        issues.append("step size must be positive")
    if not bud_logic or ("L:" not in bud_logic and "S:" not in bud_logic):
        issues.append("bud logic lacks Leaf/Seed placement")

    branch_sdf = library.get("branchSDF", {})
    for symbol, primitive in branch_sdf.items():
        if primitive not in SDF_ALLOWED:
            issues.append(f"SDF primitive invalid for {symbol}: {primitive}")

    used_symbols = set(axiom)
    for key, value in rules.items():
        if key not in BRANCH_SYMBOLS:
            issues.append(f"invalid rule key: {key}")
        used_symbols.add(key)
        used_symbols.update(value)
    for symbol in used_symbols:
        if symbol in BRANCH_SYMBOLS and symbol not in branch_sdf:
            issues.append(f"branch symbol lacks SDF primitive: {symbol}")
        if symbol not in DRAW_COMMANDS:
            issues.append(f"unknown symbol in grammar: {symbol}")

    expanded = ""
    metrics: dict[str, object] = {}
    try:
        expanded = expand_recursive(axiom, rules, depth, MAX_EXPANDED_CHARS)
        metrics = walk_lsystem(expanded, angle, angle_variance, step_size, f"{species_id}:{pass_index}")
    except Exception as exc:
        issues.append(str(exc))

    if expanded:
        if len(expanded) > MAX_EXPANDED_CHARS:
            issues.append("expanded string too large")
        if metrics.get("line_count", 0) < 4:
            issues.append("too few drawable segments")
        if metrics.get("turn_count", 0) < 2:
            issues.append("too geometric: insufficient turn diversity")
        if metrics.get("bud_count", 0) < 1:
            issues.append("no bud markers after expansion")
        left = int(metrics.get("left_turns", 0))
        right = int(metrics.get("right_turns", 0))
        if left == 0 or right == 0:
            issues.append("too geometric: one-sided branching")
        aspect = float(metrics.get("aspect", 1.0))
        if aspect > 14.0 or aspect < 0.02:
            issues.append("glitch risk: extreme aspect ratio")

    return expanded, metrics, issues


def build_metric_record(biome_id: str, species: list[object], expanded: str, metrics: dict[str, object], library: dict[str, object]) -> dict[str, object]:
    branch_sdf = library["branchSDF"]
    used_branch_symbols = sorted({ch for ch in expanded if ch in BRANCH_SYMBOLS})
    leaf_count = 0
    seed_count = 0
    for _x, _y, bud in metrics["buds"]:
        if bud == "L":
            leaf_count += 1
        elif bud == "S":
            seed_count += 1
    return {
        "id": species[0],
        "speciesFingerprintFNV1A64": species_fingerprint(species, library.get("tableVersion")),
        "biome": biome_id,
        "family": species[1],
        "iterationDepth": species[7],
        "angle": species[4],
        "angleVariance": species[5],
        "stepSize": species[6],
        "expandedChars": len(expanded),
        "lineCount": metrics["line_count"],
        "budCount": metrics["bud_count"],
        "leafCount": leaf_count,
        "seedCount": seed_count,
        "maxStack": metrics["max_stack"],
        "aspect": round(float(metrics["aspect"]), 6),
        "sdfProfile": {symbol: branch_sdf[symbol] for symbol in used_branch_symbols},
    }


def render_block_svg(block: list[tuple[str, list[object], dict[str, object], str, dict[str, object]]], out_path: Path, title: str) -> None:
    cell_w = 320
    cell_h = 260
    cols = 5
    rows = 2
    width = cols * cell_w
    height = rows * cell_h + 32
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#071013"/>',
        f'<text x="12" y="20" fill="#9fd8c8" font-family="monospace" font-size="14">{title}</text>',
    ]
    for index, (_biome_id, species, _biome, _expanded, metrics) in enumerate(block):
        col = index % cols
        row = index // cols
        ox = col * cell_w + 12
        oy = row * cell_h + 42
        min_x, min_y, max_x, max_y = metrics["bounds"]
        span_x = max(0.001, max_x - min_x)
        span_y = max(0.001, max_y - min_y)
        scale = min((cell_w - 24) / span_x, (cell_h - 44) / span_y)

        def tx(value: float) -> float:
            return ox + (value - min_x) * scale

        def ty(value: float) -> float:
            return oy + (cell_h - 32) - (value - min_y) * scale

        parts.append(f'<text x="{ox}" y="{oy - 10}" fill="#d6c56d" font-family="monospace" font-size="11">{species[0]}</text>')
        path_parts: list[str] = []
        for x0, y0, x1, y1 in metrics["lines"]:
            path_parts.append(f'M{tx(x0):.1f},{ty(y0):.1f}L{tx(x1):.1f},{ty(y1):.1f}')
        if path_parts:
            parts.append(f'<path d="{"".join(path_parts)}" fill="none" stroke="#9fd8c8" stroke-width="0.7" stroke-linecap="round"/>')
        for x, y, bud in metrics["buds"]:
            color = "#8adf77" if bud == "L" else "#d6c56d"
            radius = "1.6" if bud == "L" else "2.1"
            parts.append(f'<circle cx="{tx(x):.2f}" cy="{ty(y):.2f}" r="{radius}" fill="{color}"/>')
    parts.append("</svg>")
    out_path.write_text("\n".join(parts), encoding="utf-8")


def render_block_turtle(block: list[tuple[str, list[object], dict[str, object], str, dict[str, object]]], out_path: Path, title: str) -> None:
    import tkinter
    import turtle

    cell_w = 320
    cell_h = 260
    cols = 5
    rows = 2
    width = cols * cell_w
    height = rows * cell_h + 32
    root = tkinter.Tk()
    root.withdraw()
    canvas = tkinter.Canvas(root, width=width, height=height)
    canvas.pack()
    screen = turtle.TurtleScreen(canvas)
    screen.tracer(0, 0)
    pen = turtle.RawTurtle(screen)
    pen.hideturtle()
    pen.speed(0)
    pen.penup()
    pen.color("#9fd8c8")
    pen.pensize(1)
    title_item = canvas.create_text(12, 18, anchor="w", fill="#9fd8c8", font=("Courier", 11), text=title)
    _ = title_item

    for index, (_biome_id, species, _biome, _expanded, metrics) in enumerate(block):
        col = index % cols
        row = index // cols
        ox = col * cell_w + 12
        oy = row * cell_h + 42
        min_x, min_y, max_x, max_y = metrics["bounds"]
        span_x = max(0.001, max_x - min_x)
        span_y = max(0.001, max_y - min_y)
        scale = min((cell_w - 24) / span_x, (cell_h - 44) / span_y)

        def tx(value: float) -> float:
            return ox + (value - min_x) * scale - (width * 0.5)

        def ty(value: float) -> float:
            return (height * 0.5) - (oy + (cell_h - 32) - (value - min_y) * scale)

        canvas.create_text(ox, oy - 10, anchor="w", fill="#d6c56d", font=("Courier", 8), text=str(species[0]))
        pen.color("#9fd8c8")
        for x0, y0, x1, y1 in metrics["lines"]:
            pen.penup()
            pen.goto(tx(x0), ty(y0))
            pen.pendown()
            pen.goto(tx(x1), ty(y1))
        for x, y, bud in metrics["buds"]:
            pen.penup()
            pen.goto(tx(x), ty(y))
            pen.dot(3 if bud == "L" else 4, "#8adf77" if bud == "L" else "#d6c56d")

    screen.update()
    canvas.postscript(file=str(out_path), colormode="color")
    root.destroy()


def render_block(block: list[tuple[str, list[object], dict[str, object], str, dict[str, object]]], out_path: Path, title: str, backend: str) -> str:
    if backend == "turtle":
        turtle_path = out_path.with_suffix(".ps")
        render_block_turtle(block, turtle_path, title)
        return "turtle"

    if backend == "svg":
        svg_path = out_path.with_suffix(".svg")
        render_block_svg(block, svg_path, title)
        return "svg"

    try:
        import matplotlib

        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except Exception:
        if backend == "matplotlib":
            raise
        svg_path = out_path.with_suffix(".svg")
        render_block_svg(block, svg_path, title)
        return "svg"

    fig, axes = plt.subplots(2, 5, figsize=(14, 7), dpi=120)
    flat_axes = list(axes.ravel())
    for ax, (_biome_id, species, _biome, _expanded, metrics) in zip(flat_axes, block):
        lines = metrics["lines"]
        buds = metrics["buds"]
        for x0, y0, x1, y1 in lines:
            ax.plot((x0, x1), (y0, y1), color="#9fd8c8", linewidth=0.55)
        leaf_x = [x for x, _y, b in buds if b == "L"]
        leaf_y = [y for _x, y, b in buds if b == "L"]
        seed_x = [x for x, _y, b in buds if b == "S"]
        seed_y = [y for _x, y, b in buds if b == "S"]
        if leaf_x:
            ax.scatter(leaf_x, leaf_y, s=3, c="#8adf77", marker=".")
        if seed_x:
            ax.scatter(seed_x, seed_y, s=5, c="#d6c56d", marker="o")
        min_x, min_y, max_x, max_y = metrics["bounds"]
        pad_x = max(0.1, (max_x - min_x) * 0.08)
        pad_y = max(0.1, (max_y - min_y) * 0.08)
        ax.set_xlim(min_x - pad_x, max_x + pad_x)
        ax.set_ylim(min_y - pad_y, max_y + pad_y)
        ax.set_aspect("equal", adjustable="box")
        ax.set_title(str(species[0]), fontsize=8)
        ax.axis("off")
    for ax in flat_axes[len(block) :]:
        ax.axis("off")
    fig.suptitle(title, fontsize=11)
    fig.tight_layout()
    fig.savefig(out_path)
    plt.close(fig)
    return "png"


def run_recursive_validation(args: argparse.Namespace) -> int:
    library_path = Path(args.library)
    library_bytes = library_path.read_bytes()
    library_fingerprint = fnv1a_64_bytes(library_bytes)
    library = json.loads(library_bytes.decode("utf-8-sig"))
    all_species = iter_species(library)
    report_lines: list[str] = []
    unique_axioms: set[str] = set()
    unique_ids: set[str] = set()
    total_issues = 0
    png_count = 0
    svg_count = 0
    turtle_count = 0
    skipped_preview_count = 0
    metric_records: list[dict[str, object]] = []
    max_expanded_chars = 0
    max_line_count = 0
    max_stack_depth = 0
    structure_issues = validate_library_structure(library)
    for issue in structure_issues:
        report_lines.append(f"ISSUE: {issue}")
    total_issues += len(structure_issues)

    if len(library.get("biomes", [])) != 5:
        report_lines.append("ISSUE: biome count is not 5")
        total_issues += 1
    if len(all_species) != 100:
        report_lines.append(f"ISSUE: species count is {len(all_species)}, expected 100")
        total_issues += 1

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    blocks = [all_species[i : i + args.block_size] for i in range(0, len(all_species), args.block_size)]
    for block_index, block in enumerate(blocks):
        for pass_index in range(args.passes_per_block):
            rendered_block = []
            block_issues = 0
            for biome_id, species, biome in block:
                species_id = str(species[0])
                axiom = str(species[2])
                if species_id in unique_ids and pass_index == 0:
                    report_lines.append(f"ISSUE: duplicate species id {species_id}")
                    total_issues += 1
                    block_issues += 1
                if axiom in unique_axioms and pass_index == 0:
                    report_lines.append(f"ISSUE: duplicate axiom {species_id} {axiom}")
                    total_issues += 1
                    block_issues += 1
                if pass_index == 0:
                    unique_ids.add(species_id)
                    unique_axioms.add(axiom)
                expanded, metrics, issues = validate_species(library, species, pass_index)
                max_expanded_chars = max(max_expanded_chars, len(expanded))
                if metrics:
                    max_line_count = max(max_line_count, int(metrics.get("line_count", 0)))
                    max_stack_depth = max(max_stack_depth, int(metrics.get("max_stack", 0)))
                if issues:
                    total_issues += len(issues)
                    block_issues += len(issues)
                    report_lines.append(f"ISSUE: {species_id}: {'; '.join(issues)}")
                if metrics:
                    if pass_index == 0:
                        metric_records.append(build_metric_record(biome_id, species, expanded, metrics, library))
                    rendered_block.append((biome_id, species, biome, expanded, metrics))
            preview_path = out_dir / f"flora_block_{block_index:02d}_pass_{pass_index:02d}.png"
            if args.validate_only:
                skipped_preview_count += 1
            elif rendered_block:
                render_mode = render_block(rendered_block, preview_path, f"Block {block_index:02d} Pass {pass_index:02d}", args.backend)
                if render_mode == "png":
                    png_count += 1
                elif render_mode == "turtle":
                    turtle_count += 1
                else:
                    svg_count += 1
            else:
                svg_count += 1
            report_lines.append(f"BLOCK {block_index:02d} PASS {pass_index:02d}: species={len(block)} issues={block_issues}")

    status = "GENETICS STABILIZED" if total_issues == 0 else "PENDING VERIFICATION"
    timestamp = _dt.datetime.now(_dt.timezone.utc).isoformat()
    summary = [
        "",
        f"## Recursive Validation {timestamp}",
        f"STATUS: {status}",
        f"Library: {library_path.as_posix()}",
        f"Schema: {library.get('schema')}",
        f"TableVersion: {library.get('tableVersion')}",
        f"LibraryFingerprintFNV1A64: {library_fingerprint}",
        f"Species: {len(all_species)}",
        f"Blocks: {len(blocks)}",
        f"PassesPerBlock: {args.passes_per_block}",
        f"TotalExecutions: {len(blocks) * args.passes_per_block}",
        f"Issues: {total_issues}",
        f"PNGPreviews: {png_count}",
        f"SVGPreviews: {svg_count}",
        f"TurtlePreviews: {turtle_count}",
        f"SkippedPreviews: {skipped_preview_count}",
        f"MaxExpandedChars: {max_expanded_chars}",
        f"MaxLineCount: {max_line_count}",
        f"MaxStackDepth: {max_stack_depth}",
        f"OutputDir: {out_dir.as_posix()}",
        "Results:",
        *report_lines,
    ]
    report_text = "\n".join(summary) + "\n"
    print(report_text)
    if args.rationale:
        rationale_path = Path(args.rationale)
        rationale_path.parent.mkdir(parents=True, exist_ok=True)
        with rationale_path.open("a", encoding="utf-8") as handle:
            handle.write(report_text)
    if args.summary_json:
        summary_path = Path(args.summary_json)
        summary_path.parent.mkdir(parents=True, exist_ok=True)
        summary_data = {
            "schema": VALIDATION_SCHEMA,
            "librarySchema": library.get("schema"),
            "tableVersion": library.get("tableVersion"),
            "libraryFingerprintFNV1A64": library_fingerprint,
            "status": status,
            "species": len(all_species),
            "blocks": len(blocks),
            "passesPerBlock": args.passes_per_block,
            "totalExecutions": len(blocks) * args.passes_per_block,
            "issues": total_issues,
            "pngPreviews": png_count,
            "svgPreviews": svg_count,
            "turtlePreviews": turtle_count,
            "skippedPreviews": skipped_preview_count,
            "validateOnly": bool(args.validate_only),
            "maxExpandedChars": max_expanded_chars,
            "maxLineCount": max_line_count,
            "maxStackDepth": max_stack_depth,
            "outputDir": out_dir.as_posix(),
            "timestampUtc": timestamp,
        }
        summary_path.write_text(json.dumps(summary_data, separators=(",", ":")), encoding="utf-8")
    if args.metrics_json:
        metrics_path = Path(args.metrics_json)
        metrics_path.parent.mkdir(parents=True, exist_ok=True)
        metrics_data = {
            "schema": METRICS_SCHEMA,
            "librarySchema": library.get("schema"),
            "tableVersion": library.get("tableVersion"),
            "libraryFingerprintFNV1A64": library_fingerprint,
            "status": status,
            "speciesCount": len(metric_records),
            "records": metric_records,
        }
        metrics_path.write_text(json.dumps(metrics_data, separators=(",", ":")), encoding="utf-8")
    return 0 if total_issues == 0 else 2


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Validate and preview HECTON-8 flora L-system genetics.")
    parser.add_argument("--library", default="Data/Flora/LSystem_Library.json")
    parser.add_argument("--out", default="Docs/AgentLogs/FloraPreview_FLORA_GRAMMAR_GENETICIST")
    parser.add_argument("--block-size", type=int, default=10)
    parser.add_argument("--passes-per-block", type=int, default=3)
    parser.add_argument("--rationale", default="Docs/AgentLogs/Rationale_FLORA_GENETICIST.md")
    parser.add_argument("--summary-json", default="Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json")
    parser.add_argument("--metrics-json", default="Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json")
    parser.add_argument("--backend", choices=("auto", "matplotlib", "svg", "turtle"), default="auto")
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args(argv)
    if args.block_size <= 0:
        raise SystemExit("--block-size must be positive")
    if args.passes_per_block <= 0:
        raise SystemExit("--passes-per-block must be positive")
    return run_recursive_validation(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
