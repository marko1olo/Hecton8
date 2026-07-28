"""Silhouette-only render plus outline metrics. Decisive test for "reads as broken stone".

Why this exists and why ``preview.py`` mode ``flat`` is NOT a substitute: ``flat`` is a
LIT matte-grey render. Shading gradients on a smooth surface imply facets that the
outline does not have, exactly the way a normal map fakes geometry, so a lit render
cannot answer the question "do the fractures read in silhouette". This renders the
subject as a black shape on white -- taken from the ALPHA channel with
``film_transparent``, so it is a coverage mask and not a lighting result -- and then
MEASURES the outline.

The measurement matters as much as the image. "Looks faceted" is an impression; a
photogrammetry-grade fractured rock and a displaced-icosphere potato differ in one
measurable way:

    a potato spreads its 360 degrees of outline turning EVENLY around the outline;
    broken stone CONCENTRATES it into a few arrises separated by straight runs.

So the headline number is ``turn_top10_fraction``: the share of total absolute turning
carried by the ten sharpest resampled outline vertices. A circle at 128 samples spends
2.8 degrees per sample and scores ~0.08. A convex polygon with eight faces scores ~0.95.
``--controls`` renders both of those, plus a Perlin-displaced icosphere, so the numbers
have calibration instead of a threshold someone invented.

Owned by the geology lane. It reads ``h8forge.preview`` for camera/bounds/visibility
parity with the production sheets and writes nothing into ``h8forge`` -- the proposed
core addition is reported as a diff, not applied.

Standalone use (renders the controls and prints their metrics)::

    blender.exe -b --factory-startup -P Tools/Blender/generators/silhouette_probe.py -- \
        --controls --out Docs/AgentLogs/ForgePreviews/rock
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
from dataclasses import dataclass, field

import bmesh
import bpy
import numpy as np
from mathutils import Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
_BLENDER_TOOLS = os.path.dirname(_HERE)
if _BLENDER_TOOLS not in sys.path:
    # AGENTS.md [RULE] Relative Path Requirement: derived from __file__, never typed.
    sys.path.insert(0, _BLENDER_TOOLS)

from h8forge import law, preview  # noqa: E402

# Resampled outline vertices, and the arc-length smoothing that has to precede them.
#
# MEASURED, and the reason this constant block exists at all: without smoothing, the
# control sphere reported turnTotalDeg 566 and the control polytope 1137. Both are
# impossible -- a closed convex outline turns through exactly 360 degrees -- so the
# surplus was pure rasterisation staircase. A traced boundary alternates 1 px axial and
# 1.41 px diagonal steps, which at a 5 px resample stride injects roughly 20 degrees of
# turning noise per sample, i.e. the same order as a real arris. The corner count and the
# concentration ratio were then measuring the rasteriser.
#
# Fix is a circular Gaussian low-pass over the contour coordinates at
# SMOOTH_SIGMA_FRACTION of the perimeter before resampling. A corner survives it spread
# over about one resample stride; a half-pixel staircase does not survive it at all.
OUTLINE_SAMPLES = 128
SMOOTH_SIGMA_FRACTION = 0.005
CORNER_DEG = 22.0
TOP_TURNS = 10


@dataclass
class SilhouetteMetrics:
    view: str
    png: str
    pixels: int
    coverage_fraction: float
    perimeter_px: float
    complexity: float          # P^2 / (4 pi A): 1.0 = circle
    convexity: float           # A / A_hull: 1.0 = convex
    hull_gap_rms: float        # RMS outline-to-hull distance / sqrt(A)
    corner_count: int          # outline vertices turning more than CORNER_DEG
    turn_top10_fraction: float  # share of total turning in the 10 sharpest vertices
    turn_total_deg: float
    fuzz_fraction: float       # share of turning in sub-6-degree wobble

    def as_dict(self) -> dict:
        return {
            "view": self.view, "png": self.png, "pixels": self.pixels,
            "coverageFraction": round(self.coverage_fraction, 5),
            "perimeterPx": round(self.perimeter_px, 2),
            "complexity": round(self.complexity, 4),
            "convexity": round(self.convexity, 4),
            "hullGapRms": round(self.hull_gap_rms, 5),
            "cornerCount": self.corner_count,
            "turnTop10Fraction": round(self.turn_top10_fraction, 4),
            "turnTotalDeg": round(self.turn_total_deg, 1),
            "fuzzFraction": round(self.fuzz_fraction, 4),
        }


@dataclass
class SilhouetteResult:
    sheet_path: str
    tiles: tuple = field(default_factory=tuple)
    metrics: tuple = field(default_factory=tuple)

    @property
    def mean_top10(self) -> float:
        values = [m.turn_top10_fraction for m in self.metrics]
        return sum(values) / max(1, len(values))

    @property
    def mean_corners(self) -> float:
        values = [m.corner_count for m in self.metrics]
        return sum(values) / max(1, len(values))

    @property
    def mean_convexity(self) -> float:
        values = [m.convexity for m in self.metrics]
        return sum(values) / max(1, len(values))


# ---------------------------------------------------------------------------
# Mask extraction
# ---------------------------------------------------------------------------

def _largest_component(mask: np.ndarray) -> np.ndarray:
    """4-connected largest blob. A stray anti-aliased speck would corrupt the perimeter.

    Iterative BFS, not recursion: a 640x640 frame is 409,600 cells and Python's recursion
    limit would abort on a legitimately large subject.
    """
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    best = None
    best_size = 0
    for start_y in range(height):
        row = mask[start_y]
        for start_x in range(width):
            if not row[start_x] or seen[start_y, start_x]:
                continue
            stack = [(start_y, start_x)]
            seen[start_y, start_x] = True
            cells = []
            while stack:
                y, x = stack.pop()
                cells.append((y, x))
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width \
                            and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(cells) > best_size:
                best_size = len(cells)
                best = cells
    out = np.zeros_like(mask, dtype=bool)
    if best:
        ys, xs = zip(*best)
        out[np.array(ys), np.array(xs)] = True
    return out


def _trace_outline(mask: np.ndarray) -> np.ndarray:
    """Moore-neighbour boundary trace with Jacob's stopping criterion.

    Returns an ordered (n, 2) array of (x, y) pixel centres. Ordered is the whole point:
    turning angle is meaningless on an unordered boundary pixel set, and the cheap
    alternatives (per-row left/right extents, or sorting by angle about the centroid)
    both silently discard reentrant parts of the outline -- which for a rock is precisely
    where an undercut or a spall scar lives.
    """
    height, width = mask.shape
    padded = np.zeros((height + 2, width + 2), dtype=bool)
    padded[1:-1, 1:-1] = mask

    start = None
    for y in range(padded.shape[0]):
        xs = np.nonzero(padded[y])[0]
        if xs.size:
            start = (y, int(xs[0]))
            break
    if start is None:
        return np.zeros((0, 2))

    # Clockwise neighbour ring in (dy, dx), starting from west.
    ring = ((0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1))
    contour = [start]
    backtrack = (start[0], start[1] - 1)
    current = start
    guard = 8 * int(mask.sum()) + 64
    for _ in range(guard):
        offset = (backtrack[0] - current[0], backtrack[1] - current[1])
        try:
            index = ring.index(offset)
        except ValueError:
            index = 0
        found = False
        for step in range(1, 9):
            dy, dx = ring[(index + step) % 8]
            candidate = (current[0] + dy, current[1] + dx)
            if padded[candidate]:
                backtrack = (current[0] + ring[(index + step - 1) % 8][0],
                             current[1] + ring[(index + step - 1) % 8][1])
                current = candidate
                found = True
                break
        if not found:
            break
        if current == start and len(contour) > 2:
            break
        contour.append(current)
    points = np.array([[p[1] - 1, p[0] - 1] for p in contour], dtype=np.float64)
    return points


def _smooth_closed(points: np.ndarray, sigma_px: float) -> np.ndarray:
    """Circular Gaussian low-pass on a closed contour. Wrap, never pad.

    Zero-padding a closed contour drags the first and last samples toward the origin and
    manufactures a false corner exactly at the trace start point -- which is the topmost
    pixel, so on a rock every measurement would gain one spurious summit arris.
    """
    if sigma_px <= 0.5 or len(points) < 8:
        return points
    radius = max(1, int(round(sigma_px * 3.0)))
    offsets = np.arange(-radius, radius + 1)
    kernel = np.exp(-0.5 * (offsets / sigma_px) ** 2)
    kernel /= kernel.sum()
    out = np.zeros_like(points)
    for weight, offset in zip(kernel, offsets):
        out += weight * np.roll(points, int(offset), axis=0)
    return out


def _resample(points: np.ndarray, count: int) -> np.ndarray:
    """Uniform arc-length resampling of a closed polyline.

    Uniform in ARC LENGTH, not in vertex index: a traced boundary has diagonal steps of
    length sqrt(2) and axial steps of length 1, so index-uniform sampling weights
    diagonal runs 41 percent short and biases every turning angle.
    """
    if len(points) < 4:
        return points
    closed = np.vstack([points, points[0:1]])
    deltas = np.diff(closed, axis=0)
    seg = np.hypot(deltas[:, 0], deltas[:, 1])
    total = float(seg.sum())
    if total <= 1e-9:
        return points
    cumulative = np.concatenate([[0.0], np.cumsum(seg)])
    targets = np.linspace(0.0, total, count, endpoint=False)
    xs = np.interp(targets, cumulative, closed[:, 0])
    ys = np.interp(targets, cumulative, closed[:, 1])
    return np.stack([xs, ys], axis=1)


def _convex_hull(points: np.ndarray) -> np.ndarray:
    """Andrew monotone chain. Counter-clockwise, no repeated endpoint."""
    if len(points) < 3:
        return points
    order = np.lexsort((points[:, 1], points[:, 0]))
    sorted_points = points[order]

    def half(seq):
        out = []
        for point in seq:
            while len(out) >= 2:
                a, b = out[-2], out[-1]
                cross = ((b[0] - a[0]) * (point[1] - a[1])
                         - (b[1] - a[1]) * (point[0] - a[0]))
                if cross <= 0:
                    out.pop()
                else:
                    break
            out.append(point)
        return out

    lower = half(sorted_points)
    upper = half(sorted_points[::-1])
    return np.array(lower[:-1] + upper[:-1])


def _polygon_area(points: np.ndarray) -> float:
    if len(points) < 3:
        return 0.0
    x = points[:, 0]
    y = points[:, 1]
    return abs(float(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1)))) * 0.5


def _distance_to_hull(points: np.ndarray, hull: np.ndarray) -> np.ndarray:
    """Shortest distance from each outline point to the hull polygon's boundary."""
    if len(hull) < 3:
        return np.zeros(len(points))
    a = hull
    b = np.roll(hull, -1, axis=0)
    ab = b - a
    length_sq = np.maximum(1e-12, (ab * ab).sum(axis=1))
    out = np.empty(len(points))
    for i, point in enumerate(points):
        ap = point - a
        t = np.clip((ap * ab).sum(axis=1) / length_sq, 0.0, 1.0)
        closest = a + ab * t[:, None]
        delta = closest - point
        out[i] = float(np.min(np.hypot(delta[:, 0], delta[:, 1])))
    return out


def measure_mask(mask: np.ndarray, view: str, png: str) -> SilhouetteMetrics:
    """Every outline statistic, from a boolean coverage mask."""
    blob = _largest_component(mask)
    area = float(blob.sum())
    total_pixels = float(mask.size)
    if area < 32.0:
        return SilhouetteMetrics(view, png, int(area), area / total_pixels,
                                 0.0, 0.0, 0.0, 0.0, 0, 0.0, 0.0, 0.0)

    outline = _trace_outline(blob)
    raw_closed = np.vstack([outline, outline[0:1]])
    raw_seg = np.diff(raw_closed, axis=0)
    raw_perimeter = float(np.hypot(raw_seg[:, 0], raw_seg[:, 1]).sum())
    outline = _smooth_closed(outline, raw_perimeter * SMOOTH_SIGMA_FRACTION)
    samples = _resample(outline, OUTLINE_SAMPLES)
    closed = np.vstack([samples, samples[0:1]])
    deltas = np.diff(closed, axis=0)
    perimeter = float(np.hypot(deltas[:, 0], deltas[:, 1]).sum())

    previous = np.roll(deltas, 1, axis=0)
    dot = (deltas * previous).sum(axis=1)
    cross = previous[:, 0] * deltas[:, 1] - previous[:, 1] * deltas[:, 0]
    turns = np.degrees(np.abs(np.arctan2(cross, dot)))
    turn_total = float(turns.sum())

    # Non-maximum suppression before counting corners: one physical arris straddles two
    # or three resampled vertices, so a raw threshold count inflates by that factor and
    # would make a 6-facet rock report 15 corners.
    corners = 0
    for i, value in enumerate(turns):
        if value < CORNER_DEG:
            continue
        window = [turns[(i + k) % len(turns)] for k in (-2, -1, 1, 2)]
        if value >= max(window):
            corners += 1

    top = np.sort(turns)[::-1][:TOP_TURNS]
    top_fraction = float(top.sum() / max(1e-9, turn_total))
    fuzz = float(turns[turns < 6.0].sum() / max(1e-9, turn_total))

    hull = _convex_hull(samples)
    hull_area = _polygon_area(hull)
    outline_area = _polygon_area(samples)
    convexity = float(outline_area / max(1e-9, hull_area))
    gaps = _distance_to_hull(samples, hull)
    hull_gap_rms = float(math.sqrt(float((gaps * gaps).mean())) / math.sqrt(area))

    complexity = float(perimeter * perimeter / (4.0 * math.pi * max(1e-9, outline_area)))
    return SilhouetteMetrics(
        view=view, png=png, pixels=int(area),
        coverage_fraction=area / total_pixels, perimeter_px=perimeter,
        complexity=complexity, convexity=convexity, hull_gap_rms=hull_gap_rms,
        corner_count=corners, turn_top10_fraction=top_fraction,
        turn_total_deg=turn_total, fuzz_fraction=fuzz)


# ---------------------------------------------------------------------------
# Render
# ---------------------------------------------------------------------------

def _write_mask_png(mask: np.ndarray, path: str) -> None:
    """Black subject on white. Written through bpy.data.images -- PIL is not available."""
    height, width = mask.shape
    rgb = np.ones((height, width, 4), dtype=np.float32)
    rgb[mask, 0:3] = 0.0
    image = bpy.data.images.new("H8SIL_Mask", width=width, height=height,
                                alpha=True, float_buffer=False)
    try:
        image.pixels.foreach_set(rgb[::-1].reshape(-1))
        image.filepath_raw = path
        image.file_format = "PNG"
        image.save()
    finally:
        bpy.data.images.remove(image)


def _composite(tiles, out_path: str, columns: int) -> str:
    """Stitch the black-on-white tiles. Opaque input, so a plain copy is correct here."""
    loaded = [preview._load_pixels(p) for p in tiles]
    height, width = loaded[0].shape[0], loaded[0].shape[1]
    rows = int(math.ceil(len(loaded) / float(max(1, columns))))
    gutter = max(2, width // 160)
    sheet = np.full((rows * height + (rows + 1) * gutter,
                     columns * width + (columns + 1) * gutter, 4), 0.35,
                    dtype=np.float32)
    sheet[:, :, 3] = 1.0
    for index, tile in enumerate(loaded):
        row = index // columns
        col = index % columns
        y = gutter + row * (height + gutter)
        x = gutter + col * (width + gutter)
        sheet[y:y + height, x:x + width, 0:3] = tile[:, :, 0:3]
    image = bpy.data.images.new("H8SIL_Sheet", width=sheet.shape[1],
                                height=sheet.shape[0], alpha=True, float_buffer=False)
    try:
        image.pixels.foreach_set(sheet[::-1].reshape(-1))
        image.filepath_raw = out_path
        image.file_format = "PNG"
        image.save()
    finally:
        bpy.data.images.remove(image)
    return out_path


def render_silhouette(objects, *, name: str, output_dir: str,
                      views: tuple = ("front", "side", "three_quarter", "low"),
                      resolution: int = 640) -> SilhouetteResult:
    """Alpha-mask silhouette sheet plus outline metrics for one subject.

    Uses ``film_transparent`` so the mask is geometric COVERAGE. A luminance threshold on
    a lit render would be a different measurement: a facet turned away from every light
    goes black and would read as background, deleting exactly the part of the outline
    under test.
    """
    if isinstance(objects, bpy.types.Object):
        objects = [objects]
    os.makedirs(output_dir, exist_ok=True)

    spec = preview.PreviewSpec(name=name, output_dir=output_dir,
                               resolution=resolution, views=views, mode="flat",
                               scale_witness=False,
                               surface_class=law.SurfaceClass.GEOLOGIC)
    collection, center, radius = preview._prepare(objects, spec)
    scene = bpy.context.scene
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    hidden = preview._isolate_subject(objects)

    tiles = []
    metrics = []
    try:
        for view in views:
            direction = preview.VIEW_DIRECTIONS.get(view)
            if direction is None:
                raise ValueError("unknown view '" + view + "'")
            preview._place_camera(collection, direction, center, radius, spec.margin)
            raw = os.path.join(output_dir, "{n}_SIL_{v}_raw.png".format(n=name, v=view))
            preview._render_to(raw)
            pixels = preview._load_pixels(raw)
            mask = pixels[:, :, 3] > 0.5
            tile = os.path.join(output_dir, "{n}_SIL_{v}.png".format(n=name, v=view))
            _write_mask_png(mask, tile)
            try:
                os.remove(raw)
            except OSError:
                pass
            tiles.append(tile)
            metrics.append(measure_mask(mask, view, tile))
    finally:
        preview._restore_visibility(hidden)
        scene.render.film_transparent = False
        scene.render.image_settings.color_mode = "RGB"

    sheet = os.path.join(output_dir, "{n}_SHEET_SILHOUETTE.png".format(n=name))
    _composite(tiles, sheet, min(len(tiles), 2))
    return SilhouetteResult(sheet, tuple(tiles), tuple(metrics))


# ---------------------------------------------------------------------------
# Controls -- calibration, so the numbers mean something
# ---------------------------------------------------------------------------

def _control_potato(rng: np.random.Generator) -> bpy.types.Object:
    """Displaced icosphere: the failure mode every procedural rock generator produces."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    directions = rng.normal(size=(24, 3))
    directions /= np.linalg.norm(directions, axis=1, keepdims=True)
    freqs = rng.uniform(1.6, 5.2, size=24)
    phases = rng.uniform(0.0, 2.0 * math.pi, size=24)
    for vert in bm.verts:
        p = np.array([vert.co.x, vert.co.y, vert.co.z])
        value = float(np.sin(directions @ p * freqs + phases).mean())
        vert.co += vert.normal.normalized() * (value * 0.30)
    mesh = bpy.data.meshes.new("CTRL_Potato")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("CTRL_Potato", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _control_sphere() -> bpy.types.Object:
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0)
    mesh = bpy.data.meshes.new("CTRL_Sphere")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("CTRL_Sphere", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _control_polytope(rng: np.random.Generator) -> bpy.types.Object:
    """Convex hull of a few random points: pure flat facets and sharp arrises."""
    points = rng.normal(size=(9, 3))
    points /= np.linalg.norm(points, axis=1, keepdims=True)
    points *= rng.uniform(0.75, 1.0, size=(9, 1))
    bm = bmesh.new()
    for point in points:
        bm.verts.new(Vector((float(point[0]), float(point[1]), float(point[2]))))
    bm.verts.ensure_lookup_table()
    bmesh.ops.convex_hull(bm, input=bm.verts[:])
    mesh = bpy.data.meshes.new("CTRL_Polytope")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("CTRL_Polytope", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def run_controls(output_dir: str, resolution: int) -> dict:
    rng = np.random.default_rng(20260729)
    out = {}
    for label, factory in (("sphere", lambda: _control_sphere()),
                           ("potato", lambda: _control_potato(rng)),
                           ("polytope", lambda: _control_polytope(rng))):
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.context.view_layer.update()
        obj = factory()
        bpy.context.view_layer.update()
        result = render_silhouette(obj, name="CONTROL_" + label,
                                   output_dir=output_dir, resolution=resolution,
                                   views=("front", "three_quarter"))
        out[label] = {"sheet": result.sheet_path,
                      "meanTop10": round(result.mean_top10, 4),
                      "meanCorners": round(result.mean_corners, 2),
                      "meanConvexity": round(result.mean_convexity, 4),
                      "views": [m.as_dict() for m in result.metrics]}
    return out


def main(argv: list) -> int:
    parser = argparse.ArgumentParser(prog="silhouette_probe.py")
    parser.add_argument("--controls", action="store_true")
    parser.add_argument("--out", default="")
    parser.add_argument("--resolution", type=int, default=640)
    args = parser.parse_args(argv)

    out_dir = args.out if os.path.isabs(args.out) else os.path.join(
        law.project_root(), args.out or os.path.join("Docs", "AgentLogs",
                                                    "ForgePreviews", "rock"))
    if args.controls:
        report = run_controls(out_dir, args.resolution)
        print("[silhouette] controls " + json.dumps(report, indent=1))
    else:
        print("[silhouette] nothing requested; pass --controls or import the module")
    return 0


if __name__ == "__main__":
    _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    sys.exit(main(_argv))
