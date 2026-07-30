"""Probe: does a candidate structural metric separate the ACCEPTED geology tile from the
REJECTED one?

WHY THIS FILE EXISTS. ``law.GEOLOGY_LAMINA_MAX_RUN_FRACTION`` (0.55) is anti-correlated with
the lead's visual verdict in the region that decides acceptance: the metric reads GREEN
(p95 = 0.536) at the 56-scar / 33-percent-coverage configuration that was REJECTED ON SIGHT as
brick masonry, and RED (p95 = 0.807) at the 44-scar configuration that was ACCEPTED. A metric
that inverts against the verdict cannot be relaxed into correctness; it has to be replaced.

So a replacement has exactly one entry requirement, and this script is the instrument that
tests it: the new number must be GREEN on the accepted configuration and RED on the rejected
one, and it must stay that way across seeds rather than at one lucky seed.

WHY IT CAN RUN AT ALL WITHOUT BLENDER. ``h8forge/texture.py`` imports only hashlib, json,
math, os, struct, sys, zlib, dataclasses, typing and numpy at module level; ``bpy`` is
imported lazily inside the preview/render half (first at texture.py:3224). Every field
builder used here -- periodic_fbm, periodic_worley, periodic_joint_traces, periodic_warp,
_build_unconformities, _build_fault, _build_spall_scars, build_lamina_stack,
build_height_field -- is pure numpy. This script therefore drives the REAL construction code
rather than a re-implementation of it, which is the only version of this measurement worth
anything: a re-implementation would drift from the generator and prove nothing about it.

HOW THE REJECTED CONFIGURATION IS RECONSTRUCTED, stated plainly because it matters. Its exact
constant values are not recorded anywhere in the repository -- only its three measured
properties are (56 scars, 0.33 erosional coverage, run p95 0.536). This script reconstructs it
by holding the scar count at 56 and scaling the scar width band until the coverage and run
p95 match those recorded figures. Matching all three is what makes the reconstruction faithful
in every respect the metric can see.

Run:  python -B Docs/AgentLogs/geology_structure_metric_probe.py --mode reproduce
      python -B Docs/AgentLogs/geology_structure_metric_probe.py --mode battery
      python -B Docs/AgentLogs/geology_structure_metric_probe.py --mode sweep

Paths are derived from __file__, never typed: AGENTS.md bans hardcoded absolute developer
paths in scripts.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
import time

import numpy as np

_HERE = os.path.dirname(os.path.abspath(__file__))
_REPO = os.path.dirname(os.path.dirname(_HERE))          # Docs/AgentLogs -> Docs -> repo
sys.path.insert(0, os.path.join(_REPO, "Tools", "Blender"))

from h8forge import law, texture  # noqa: E402  (path must be set first)


# ---------------------------------------------------------------------------
# Configuration harness
# ---------------------------------------------------------------------------
# The two configurations differ ONLY in the spall-scar constants. Everything else -- lamina
# stack, joint set, unconformities, fault -- is drawn from the same per-purpose substreams and
# is therefore bit-identical between them. That is the fact that kills three of the four
# candidate metrics below, and it is a property of the generator, not of this script.

ACCEPTED = dict(scar_count=44, width_min=0.05, width_max=0.16)

# THE REJECTED CONFIGURATION IS NOT A GUESS -- it is recorded in the source it was removed
# from. texture.py:704-707 states: "At 0.08-0.30 width the scars met the run budget only by
# covering 49.6 percent of the tile -- half the face became scar". That is the configuration
# whose run budget went GREEN, and the handoff's "33 percent coverage" does not reproduce:
# measured on today's generator, 56 scars at 0.33 coverage sit at run p95 0.71, still RED, and
# nothing anywhere in the (count x width) grid reaches p95 <= 0.55 below 0.46 coverage.
REJECTED = dict(scar_count=56, width_min=0.08, width_max=0.30)


def build(seed: int, resolution: int, scar_count: int,
          width_min: float = 0.05, width_max: float = 0.16,
          width_scale: float = 1.0):
    """Build the real height field with the spall constants overridden.

    width_scale multiplies both width bounds. mode_reproduce/mode_battery/mode_sweep all
    sweep width that way, and every one of them called this function with a width_scale
    keyword it did not accept -- so the three search modes died on TypeError before
    measuring anything, and only mode_default (which never passes it) ever ran.
    """
    saved = (texture.SPALL_SCAR_COUNT,
             texture.SPALL_WIDTH_MIN_FRACTION,
             texture.SPALL_WIDTH_MAX_FRACTION)
    scale = float(width_scale)
    texture.SPALL_SCAR_COUNT = int(scar_count)
    texture.SPALL_WIDTH_MIN_FRACTION = float(width_min) * scale
    texture.SPALL_WIDTH_MAX_FRACTION = float(width_max) * scale
    try:
        spec = texture.GeologyTextureSpec(seed=seed, quality=1.0, resolution=resolution)
        height = texture.build_height_field(spec)
    finally:
        (texture.SPALL_SCAR_COUNT,
         texture.SPALL_WIDTH_MIN_FRACTION,
         texture.SPALL_WIDTH_MAX_FRACTION) = saved
    return spec, height


def interrupted_mask(height) -> np.ndarray:
    """Exactly the union measure_structural_extent uses (texture.py:965)."""
    lam = height.lamina
    return ((lam.spall > 0.5) | (height.joint > 0.5)
            | (lam.fault_gouge > 0.5) | (lam.unconformity > 0.5))


# ---------------------------------------------------------------------------
# Candidate 1: coefficient of variation of lamina thickness
# ---------------------------------------------------------------------------

def cv_lamina_thickness(height) -> float:
    t = np.asarray(height.lamina.thicknesses_m, dtype=np.float64)
    return float(t.std() / t.mean())


def cv_apparent_band_spacing(height) -> float:
    """CV of the spacing between visible partings, measured per column off the field.

    The stack's own thickness list is untouched by scars, so this is the only version of
    "lamina thickness variation" that could possibly respond to the change: it measures the
    APPARENT band spacing on the rendered surface, where scar offsets shift the phase.
    """
    contact = height.lamina.contact > 0.5
    gaps = []
    for col in range(0, contact.shape[1], max(1, contact.shape[1] // 128)):
        rows = np.flatnonzero(contact[:, col])
        if rows.size >= 3:
            d = np.diff(rows)
            gaps.append(d[d > 1])
    if not gaps:
        return float("nan")
    pooled = np.concatenate(gaps).astype(np.float64)
    return float(pooled.std() / pooled.mean()) if pooled.mean() > 0 else float("nan")


# ---------------------------------------------------------------------------
# Candidate 2: spectral concentration of the bedding-normal profile
# ---------------------------------------------------------------------------

def bedding_spectrum(height) -> dict:
    """1-D power spectrum of the height profile ALONG the bedding normal (V).

    Averaged over columns as a power spectrum, not as the spectrum of the averaged profile:
    averaging the profiles first would cancel every laterally-varying term and manufacture a
    cleaner peak than the surface has.
    """
    h = np.asarray(height.height_m, dtype=np.float64)
    h = h - h.mean(axis=0, keepdims=True)
    power = (np.abs(np.fft.rfft(h, axis=0)) ** 2).mean(axis=1)
    power = power[1:]                                    # drop DC
    total = float(power.sum())
    if total <= 0.0:
        return {"peakFraction": float("nan"), "flatness": float("nan")}
    peak_fraction = float(power.max() / total)
    logs = np.log(np.maximum(power, 1e-30))
    flatness = float(math.exp(logs.mean()) / (total / power.size))
    return {"peakFraction": peak_fraction, "flatness": flatness}


# ---------------------------------------------------------------------------
# Candidate 3: angular separation between cross-cutting sets
# ---------------------------------------------------------------------------

def _edge_orientation_histogram(mask: np.ndarray, bins: int = 180) -> np.ndarray:
    """Gradient-magnitude-weighted histogram of EDGE orientation, in degrees mod 180.

    The gradient of the structure mask points ACROSS an edge, so the edge itself runs
    perpendicular to it; the conversion is done here once rather than in every caller.
    Wrapped differences, because the tile is a torus.
    """
    m = mask.astype(np.float64)
    gv = 0.5 * (np.roll(m, -1, axis=0) - np.roll(m, 1, axis=0))   # d/drow
    gu = 0.5 * (np.roll(m, -1, axis=1) - np.roll(m, 1, axis=1))   # d/dcol
    magnitude = np.sqrt(gv * gv + gu * gu)
    # Edge direction = gradient rotated 90 deg: (drow, dcol) = (-gu, gv).
    angle = np.degrees(np.arctan2(-gu, gv)) % 180.0
    hist, _ = np.histogram(angle, bins=bins, range=(0.0, 180.0), weights=magnitude)
    return hist


def _circular_modes(hist: np.ndarray, min_separation_deg: float = 15.0,
                    max_modes: int = 4) -> list:
    """Greedy peak picking on a 180-degree circular histogram."""
    bins = hist.size
    per_bin = 180.0 / bins
    work = hist.astype(np.float64).copy()
    total = float(work.sum())
    modes = []
    for _ in range(max_modes):
        if total <= 0.0 or work.max() <= 0.0:
            break
        index = int(work.argmax())
        centre = (index + 0.5) * per_bin
        half = int(round(min_separation_deg / per_bin))
        window = [(index + d) % bins for d in range(-half, half + 1)]
        mass = float(work[window].sum())
        if mass / total < 0.02:
            break
        modes.append({"deg": centre, "massFraction": mass / total})
        work[window] = 0.0
    return modes


def orientation_report(mask: np.ndarray) -> dict:
    hist = _edge_orientation_histogram(mask)
    modes = _circular_modes(hist)
    separations = []
    for i in range(len(modes)):
        for j in range(i + 1, len(modes)):
            d = abs(modes[i]["deg"] - modes[j]["deg"]) % 180.0
            separations.append(min(d, 180.0 - d))
    return {
        "modes": [(round(m["deg"], 1), round(m["massFraction"], 4)) for m in modes],
        "minSeparationDeg": round(min(separations), 2) if separations else float("nan"),
        "topTwoMassFraction": round(sum(m["massFraction"] for m in modes[:2]), 4),
    }


# ---------------------------------------------------------------------------
# Candidate 4: cross-cut incidence along the bed  (density, not continuity)
# ---------------------------------------------------------------------------
# A bed running the whole tile UNCUT scores zero here, which is the point: continuity is
# legal geology. What is counted is how many SEPARATE structures a bed has to cross per
# metre -- the "dense" half of the file's own masonry diagnosis at texture.py:689.

def crosscut_incidence(mask: np.ndarray, tile_m: float) -> dict:
    """Distinct interrupting structures crossed per metre along the bedding direction."""
    rows = mask.astype(np.int8)
    # Wrapped transition count per row; a full-width interruption counts once, not twice.
    transitions = (rows != np.roll(rows, 1, axis=1)).sum(axis=1) // 2
    per_row = transitions.astype(np.float64) / tile_m
    return {
        "meanPerM": float(per_row.mean()),
        "p95PerM": float(np.percentile(per_row, 95)),
        "maxPerM": float(per_row.max()),
        "uncutRowFraction": float((transitions == 0).mean()),
    }


def gap_dispersion(mask: np.ndarray) -> dict:
    """CV of ALL intact run lengths pooled over rows, wrapped. Not the longest one."""
    intact = ~mask
    lengths = []
    width = intact.shape[1]
    step = max(1, intact.shape[0] // 256)
    for row in range(0, intact.shape[0], step):
        line = intact[row]
        if line.all() or not line.any():
            lengths.append(np.array([float(width) if line.all() else 0.0]))
            continue
        rotated = np.roll(line, -int(np.argmin(line)))
        padded = np.concatenate(([0], rotated.astype(np.int8), [0]))
        edges = np.flatnonzero(np.diff(padded))
        starts, ends = edges[::2], edges[1::2]
        if starts.size:
            lengths.append((ends - starts).astype(np.float64))
    pooled = np.concatenate(lengths) if lengths else np.array([0.0])
    mean = float(pooled.mean())
    return {"gapCv": float(pooled.std() / mean) if mean > 0 else float("nan"),
            "gapCount": int(pooled.size)}


# ---------------------------------------------------------------------------
# Candidate 5: is the face partitioned into closed cells?  (the "panel" test)
# ---------------------------------------------------------------------------

def _label_torus(binary: np.ndarray) -> np.ndarray:
    """Connected components of a boolean field on a TORUS, 4-connectivity.

    Wrap is handled explicitly, because a bed continuous across the tile boundary is ONE
    component -- counting it as two would re-import the very bug that
    ``_longest_wrapped_run`` (texture.py:930) already avoids on the 1-D side.

    NOTE FOR ANYTHING THAT SHIPS. This probe uses ``scipy.ndimage.label`` when it is present,
    which it is in the system Python. Blender 4.5.9's bundled Python has numpy 1.26.4 and NO
    scipy, so a gate that lands in ``texture.py`` may not depend on this path; the numpy
    fallback below is the one that would have to ship, and it is slow.
    """
    try:
        from scipy import ndimage
    except ImportError:
        return _label_torus_numpy(binary)

    labels, count = ndimage.label(binary)
    if count == 0:
        return labels - 1
    parent = np.arange(count + 1)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[max(ra, rb)] = min(ra, rb)

    for left, right in ((labels[0], labels[-1]), (labels[:, 0], labels[:, -1])):
        both = (left > 0) & (right > 0)
        for a, b in zip(left[both].tolist(), right[both].tolist()):
            union(a, b)
    remap = np.array([find(i) for i in range(count + 1)])
    out = remap[labels]
    return np.where(binary, out, -1)


def _label_torus_numpy(binary: np.ndarray) -> np.ndarray:
    """Label propagation with pointer jumping. numpy only, and slow -- see above."""
    height, width = binary.shape
    labels = np.where(binary, np.arange(height * width).reshape(height, width), -1)
    labels = labels.astype(np.int64)
    for _iteration in range(512):
        previous = labels
        best = labels.copy()
        for axis in (0, 1):
            for shift in (-1, 1):
                neighbour = np.roll(labels, shift, axis=axis)
                take = binary & (neighbour >= 0) & ((best < 0) | (neighbour < best))
                best = np.where(take, neighbour, best)
        flat = best.ravel().copy()
        for _jump in range(8):
            root = np.where(flat >= 0, flat[np.where(flat >= 0, flat, 0)], -1)
            if np.array_equal(root, flat):
                break
            flat = root
        labels = flat.reshape(height, width)
        if np.array_equal(labels, previous):
            break
    return labels


def cell_partition(mask: np.ndarray, tile_m: float) -> dict:
    """Statistics of the INTACT region's connected components.

    Masonry is a TESSELLATION: the structure network closes the surface into panels of
    similar size. Laterally continuous beds keep the intact region as one large connected
    sheet, which is what a real cliff face is.
    """
    labels = _label_torus(~mask)
    valid = labels[labels >= 0]
    if valid.size == 0:
        return {"cellCount": 0, "largestFraction": 0.0, "areaCv": float("nan"),
                "cellsPerM2": 0.0}
    counts = np.bincount(valid)
    counts = counts[counts > 0].astype(np.float64)
    total = float(counts.sum())
    floor = 0.0015 * mask.size            # ignore single-pixel speckle
    significant = counts[counts >= floor]
    area_m2 = tile_m * tile_m
    return {
        "cellCount": int(significant.size),
        "largestFraction": float(counts.max() / total),
        "areaCv": (float(significant.std() / significant.mean())
                   if significant.size > 1 else 0.0),
        "cellsPerM2": float(significant.size / area_m2),
    }


# ---------------------------------------------------------------------------
# Candidate 6: lattice periodicity of the structure network
# ---------------------------------------------------------------------------

def lattice_prominence(mask: np.ndarray) -> dict:
    """Strongest discrete peak in the 2-D spectrum of the structure mask, vs its ring.

    A brick wall is periodic in both axes; crazy paving is not. Measured as peak over the
    local radial median so a broadband field cannot score high just by having energy.
    """
    m = mask.astype(np.float64)
    m = m - m.mean()
    spectrum = np.abs(np.fft.rfft2(m)) ** 2
    resolution = mask.shape[0]
    fv = np.fft.fftfreq(resolution)[:, None] * resolution
    fu = np.fft.rfftfreq(resolution)[None, :] * resolution
    radius = np.sqrt(fv * fv + fu * fu)
    band = (radius >= 3.0) & (radius <= 40.0)
    if not band.any():
        return {"prominence": float("nan")}
    rings = np.rint(radius).astype(np.int64)
    best = 0.0
    for ring in range(3, 41):
        sel = band & (rings == ring)
        if sel.sum() < 8:
            continue
        values = spectrum[sel]
        median = float(np.median(values))
        if median > 1e-30:
            best = max(best, float(values.max() / median))
    return {"prominence": best}


# ---------------------------------------------------------------------------
# THE COMPOSITE under test
# ---------------------------------------------------------------------------

BEDDING_BAND_DEG = 25.0     # within this of horizontal counts as bedding-parallel
ORIENTATION_WINDOW_DEG = 20.0


def masonry_index(mask: np.ndarray, tile_m: float) -> dict:
    """Identically-oriented cross-cuts per metre of bed.

    Two factors, because the two halves of the rejection are separate facts:

      * INCIDENCE -- how many distinct structures a bed must cross per metre. Zero for an
        uncut bed, so lateral continuity is not penalised at all.
      * ORIENTATION CONCENTRATION -- what share of the CROSS-CUTTING edge length repeats one
        direction. A face cut at many angles is shattered rock; a face cut at one repeated
        angle is a manufactured pattern.

    The product has units of crossings per metre and is read as "repeated cross-cuts per
    metre". A high incidence with dispersed orientations is allowed; so is one repeated
    orientation at low incidence, which is what a single fault set looks like.
    """
    incidence = crosscut_incidence(mask, tile_m)
    hist = _edge_orientation_histogram(mask)
    bins = hist.size
    per_bin = 180.0 / bins
    centres = (np.arange(bins) + 0.5) * per_bin
    # Edge direction 90 deg == running along rows == bedding-parallel in image space.
    from_bedding = np.abs(((centres - 90.0 + 90.0) % 180.0) - 90.0)
    cross = from_bedding > BEDDING_BAND_DEG
    cross_hist = np.where(cross, hist, 0.0)
    cross_mass = float(cross_hist.sum())
    if cross_mass <= 0.0:
        concentration = 0.0
        dominant = float("nan")
    else:
        index = int(cross_hist.argmax())
        half = int(round(ORIENTATION_WINDOW_DEG / per_bin))
        window = [(index + d) % bins for d in range(-half, half + 1)]
        concentration = float(cross_hist[window].sum() / cross_mass)
        dominant = float(centres[index])
    return {
        "incidencePerM": incidence["meanPerM"],
        "incidenceP95PerM": incidence["p95PerM"],
        "crossOrientationConcentration": concentration,
        "dominantCrossEdgeDeg": dominant,
        "crossEdgeMassFraction": (cross_mass / float(hist.sum())
                                  if hist.sum() > 0 else 0.0),
        "index": incidence["meanPerM"] * concentration,
    }


# ---------------------------------------------------------------------------
# Modes
# ---------------------------------------------------------------------------

def _extent(height, spec) -> dict:
    return texture.measure_structural_extent(height.lamina, height.joint, spec.tile_m)


def mode_reproduce(resolution: int, seed: int) -> None:
    print("=== ACCEPTED configuration, as shipped in texture.py ===")
    t0 = time.time()
    spec, height = build(seed, resolution, **ACCEPTED)
    extent = _extent(height, spec)
    print("  seed {s} res {r}  build {t:.1f}s".format(s=seed, r=resolution,
                                                      t=time.time() - t0))
    print("  scars {c}  spallCoverage {p:.4f}  erosionalCoverage {e:.4f}".format(
        c=ACCEPTED["scar_count"], p=height.report["spallCoverage"],
        e=extent["erosionalCoverage"]))
    print("  run p50 {a:.4f}  p95 {b:.4f}  max {c:.4f}   budget {d} -> {v}".format(
        a=extent["longestIntactRunFraction"]["p50"],
        b=extent["longestIntactRunFraction"]["p95"],
        c=extent["longestIntactRunFraction"]["max"],
        d=law.GEOLOGY_LAMINA_MAX_RUN_FRACTION,
        v="MET" if extent["runBudgetMet"] else "FAILED"))

    print("\n=== REJECTED configuration: searching width scale at 56 scars ===")
    print("  target: erosionalCoverage ~0.33, run p95 ~0.536 (the recorded figures)")
    for scale in (1.0, 1.2, 1.4, 1.6, 1.8, 2.0, 2.4):
        spec_b, height_b = build(seed, resolution, scar_count=56, width_scale=scale)
        extent_b = _extent(height_b, spec_b)
        print("  widthScale {s:.1f}  spall {p:.4f}  erosional {e:.4f}  "
              "run p95 {b:.4f} -> {v}".format(
                  s=scale, p=height_b.report["spallCoverage"],
                  e=extent_b["erosionalCoverage"],
                  b=extent_b["longestIntactRunFraction"]["p95"],
                  v="GREEN" if extent_b["runBudgetMet"] else "RED"))


def _measure_all(spec, height) -> dict:
    mask = interrupted_mask(height)
    extent = _extent(height, spec)
    out = {
        "runP95": extent["longestIntactRunFraction"]["p95"],
        "runBudgetMet": extent["runBudgetMet"],
        "erosionalCoverage": extent["erosionalCoverage"],
        "spallCoverage": height.report["spallCoverage"],
        "cvLaminaThickness": cv_lamina_thickness(height),
        "cvApparentBandSpacing": cv_apparent_band_spacing(height),
    }
    out.update({"bedding" + k[0].upper() + k[1:]: v
                for k, v in bedding_spectrum(height).items()})
    out.update({"orient" + k[0].upper() + k[1:]: v
                for k, v in orientation_report(mask).items()})
    out.update(gap_dispersion(mask))
    out.update({"lattice" + k[0].upper() + k[1:]: v
                for k, v in lattice_prominence(mask).items()})
    out.update({"cell" + k[0].upper() + k[1:]: v
                for k, v in cell_partition(mask, spec.tile_m).items()})
    out.update(masonry_index(mask, spec.tile_m))
    return out


def mode_battery(resolution: int, seeds: list, width_scale: float) -> None:
    rows = []
    for seed in seeds:
        for label, kwargs in (("ACCEPTED 44", ACCEPTED),
                              ("REJECTED 56", dict(scar_count=56,
                                                   width_scale=width_scale))):
            t0 = time.time()
            spec, height = build(seed, resolution, **kwargs)
            measured = _measure_all(spec, height)
            measured["_label"] = label
            measured["_seed"] = seed
            rows.append(measured)
            print("  built {l} seed {s} in {t:.1f}s".format(l=label, s=seed,
                                                            t=time.time() - t0))
    keys = [k for k in rows[0] if not k.startswith("_")]
    print("\n{:<32}".format("metric") + "".join(
        "{:>17}".format(r["_label"].split()[0][:6] + "/" + str(r["_seed"]))
        for r in rows))
    for key in keys:
        line = "{:<32}".format(key)
        for row in rows:
            value = row[key]
            if isinstance(value, float):
                line += "{:>17.4f}".format(value)
            else:
                line += "{:>17}".format(str(value))
        print(line)

    print("\n--- separation summary (accepted vs rejected, per metric) ---")
    for key in keys:
        a = [r[key] for r in rows if r["_label"].startswith("ACCEPTED")]
        b = [r[key] for r in rows if r["_label"].startswith("REJECTED")]
        if not all(isinstance(v, (int, float)) and not isinstance(v, bool)
                   for v in a + b):
            continue
        a = np.array(a, dtype=np.float64)
        b = np.array(b, dtype=np.float64)
        if np.any(~np.isfinite(a)) or np.any(~np.isfinite(b)):
            continue
        gap = (b.min() - a.max()) if b.min() > a.max() else (a.min() - b.max())
        overlap = "SEPARATES" if gap > 0 else "overlaps"
        print("  {k:<32} accepted {a1:.4f}..{a2:.4f}  rejected {b1:.4f}..{b2:.4f}  "
              "{o} (margin {g:+.4f})".format(k=key, a1=a.min(), a2=a.max(),
                                             b1=b.min(), b2=b.max(), o=overlap, g=gap))


def mode_sweep(resolution: int, seeds: list, width_scale: float) -> None:
    print("scar count response, width scale {w} on the 56-scar band".format(w=width_scale))
    print("{:<8}{:>10}{:>12}{:>12}{:>14}{:>12}".format(
        "scars", "runP95", "erosional", "incid/m", "concentration", "index"))
    for count in (11, 22, 33, 44, 50, 56, 66, 78):
        scale = 1.0 if count <= 44 else width_scale
        values = []
        for seed in seeds:
            spec, height = build(seed, resolution, scar_count=count, width_scale=scale)
            mask = interrupted_mask(height)
            extent = _extent(height, spec)
            values.append((extent["longestIntactRunFraction"]["p95"],
                           extent["erosionalCoverage"],
                           masonry_index(mask, spec.tile_m)))
        print("{:<8}{:>10.4f}{:>12.4f}{:>12.4f}{:>14.4f}{:>12.4f}".format(
            count,
            float(np.mean([v[0] for v in values])),
            float(np.mean([v[1] for v in values])),
            float(np.mean([v[2]["incidencePerM"] for v in values])),
            float(np.mean([v[2]["crossOrientationConcentration"] for v in values])),
            float(np.mean([v[2]["index"] for v in values]))))


def mode_grid(resolution: int, seed: int) -> None:
    """Which (count, width) pairs make the OLD budget go green, and at what coverage.

    The point of this table is the handoff figure "56 scars at 33 percent coverage, run p95
    0.536". With the generator as it stands today that combination does not exist: 56 scars at
    0.33 coverage measures p95 0.71, and reaching 0.536 needs 0.47 coverage. The table records
    the whole surface so the reconstruction is chosen on evidence rather than on the one number
    that was remembered.
    """
    print("{:<8}{:>7}{:>11}{:>12}{:>10}{:>8}".format(
        "scars", "scale", "spallCov", "erosionalCov", "runP95", "old"))
    for count in (44, 56, 78, 110, 150):
        for scale in (0.6, 0.8, 1.0, 1.4, 1.8, 2.0):
            spec, height = build(seed, resolution, scar_count=count, width_scale=scale)
            extent = _extent(height, spec)
            print("{:<8}{:>7.1f}{:>11.4f}{:>12.4f}{:>10.4f}{:>8}".format(
                count, scale, height.report["spallCoverage"],
                extent["erosionalCoverage"],
                extent["longestIntactRunFraction"]["p95"],
                "GREEN" if extent["runBudgetMet"] else "RED"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", default="reproduce",
                        choices=("reproduce", "battery", "sweep", "grid"))
    parser.add_argument("--resolution", type=int, default=1024)
    parser.add_argument("--seeds", default="1713")
    parser.add_argument("--width-scale", type=float, default=1.6)
    args = parser.parse_args()
    seeds = [int(s) for s in args.seeds.split(",")]

    if args.mode == "reproduce":
        mode_reproduce(args.resolution, seeds[0])
    elif args.mode == "battery":
        mode_battery(args.resolution, seeds, args.width_scale)
    elif args.mode == "grid":
        mode_grid(args.resolution, seeds[0])
    else:
        mode_sweep(args.resolution, seeds, args.width_scale)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
