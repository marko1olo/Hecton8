#!/usr/bin/env python3
"""Ship a6c96w absolute-column spall into Tools/Blender/h8forge/texture.py.

Scratch only — do not commit this file. Product change is texture.py alone.
Winning knobs (proof@2048 seeds 0,1,2,7,13):
  count=96 wmin=0.045 wmax=0.11 joints=18
  ncols=6 jfrac=0.08 tilt=0.10 cjit=0.08 fg=False ps=0.3
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXTURE = ROOT / "Tools" / "Blender" / "h8forge" / "texture.py"

NEW_BUILD = r'''def _build_spall_scars(spec: GeologyTextureSpec,
                       base_coordinate: np.ndarray, boundaries: np.ndarray,
                       thicknesses: np.ndarray,
                       joint_traces: Optional[list]) -> tuple:
    """Absolute-X column spall corridors with minority joint-side scars.

    a6c96w ship: ncols=SPALL_ABS_NCOL, joint_frac=SPALL_ABS_JOINT_FRAC,
    tilt_blend=SPALL_ABS_TILT_BLEND, centre_jitter=SPALL_ABS_CENTRE_JITTER,
    force_grid=SPALL_ABS_FORCE_GRID, phase_scale=SPALL_ABS_PHASE_SCALE.

    Absolute columns break the per-row longestIntactRunFraction.p95 corridor
    that joint-side-only scars left intact (law GEOLOGY_LAMINA_MAX_RUN_FRACTION).
    """
    rng = substream(spec.seed, "spall")
    field_rng = substream(spec.seed, "spall.field")
    resolution = spec.resolved_resolution()
    axis = (np.arange(resolution) + 0.5) / resolution * spec.tile_m
    py, px = np.meshgrid(axis, axis, indexing="ij")

    lamina_count = len(thicknesses)
    mean_thickness = float(thicknesses.mean())

    usable = []
    for trace in (joint_traces or ()):
        if abs(float(trace.get("dirRow", 0.0))) > 0.35:
            usable.append(trace)
    if not usable:
        usable = list(joint_traces or ())

    mask = np.zeros((resolution, resolution), dtype=np.float64)
    offset = np.zeros((resolution, resolution), dtype=np.float64)
    edge_jitter = periodic_warp(field_rng, resolution, spec.tile_m,
                                wavelength_m=0.055, amplitude=0.007)

    total = max(1, SPALL_SCAR_COUNT)
    ncols = max(1, int(SPALL_ABS_NCOL))
    col_w = spec.tile_m / float(ncols)
    joint_frac = float(SPALL_ABS_JOINT_FRAC)
    tilt_blend = float(SPALL_ABS_TILT_BLEND)
    centre_jitter = float(SPALL_ABS_CENTRE_JITTER)
    force_grid = bool(SPALL_ABS_FORCE_GRID)
    phase_scale = float(SPALL_ABS_PHASE_SCALE)

    for index in range(total):
        stratum = (index + rng.random()) / float(total)
        start_lamina = int(min(lamina_count - 1, stratum * lamina_count))
        package = int(rng.integers(SPALL_PACKAGE_LAMINAE_MIN,
                                   SPALL_PACKAGE_LAMINAE_MAX + 1))
        lo = float(boundaries[start_lamina])
        hi = float(boundaries[min(start_lamina + package, lamina_count)])
        if hi <= lo:
            continue
        if hi <= spec.tile_m:
            in_package = (base_coordinate >= lo) & (base_coordinate < hi)
        else:
            in_package = ((base_coordinate >= lo)
                          | (base_coordinate < (hi - spec.tile_m)))

        width = float(rng.uniform(SPALL_WIDTH_MIN_FRACTION,
                                  SPALL_WIDTH_MAX_FRACTION)) * spec.tile_m
        use_joint = bool(usable) and (rng.random() < joint_frac)

        if use_joint:
            trace = usable[int(rng.integers(0, len(usable)))]
            origin = np.array([trace["startRow"], trace["startCol"]],
                              dtype=np.float64)
            direction = np.array([trace["dirRow"], trace["dirCol"]],
                                 dtype=np.float64)
            norm = float(np.hypot(direction[0], direction[1]))
            if norm < 1e-9:
                continue
            direction = direction / norm
            normal = np.array([direction[1], -direction[0]])
            orow = py - origin[0]
            ocol = px - origin[1]
            orow = orow - spec.tile_m * np.rint(orow / spec.tile_m)
            ocol = ocol - spec.tile_m * np.rint(ocol / spec.tile_m)
            lateral = orow * normal[0] + ocol * normal[1] + edge_jitter
            side = 1.0 if rng.random() < 0.5 else -1.0
            in_slab = (lateral * side >= 0.0) & (lateral * side < width)
        else:
            col_i = index % ncols
            phase = phase_scale * ((index // ncols) % 3) * (col_w / 3.0)
            if force_grid:
                centre = (col_i + 0.5) * col_w + phase
            else:
                centre = ((col_i + 0.5) * col_w
                          + phase
                          + (rng.random() - 0.5) * centre_jitter * col_w)
            centre = centre % spec.tile_m
            dx = px - centre
            dx = dx - spec.tile_m * np.rint(dx / spec.tile_m)
            # mild tilt: blend a vertical shear so edges aren't CAD-perfect
            tilt = tilt_blend * (py / max(spec.tile_m, 1e-9) - 0.5) * width
            half = 0.5 * width
            in_slab = np.abs(dx + tilt + edge_jitter * 0.35) < half

        scar = in_package & in_slab
        if not scar.any():
            continue
        scar_offset = mean_thickness * float(rng.uniform(SPALL_OFFSET_MIN_FRACTION,
                                                         SPALL_OFFSET_MAX_FRACTION))
        offset = np.where(scar, scar_offset, offset)
        mask = np.where(scar, 1.0, mask)

    return mask, offset
'''

ABS_CONSTANTS = """
# Absolute-column spall knobs (a6c96w ship — proof@2048 p95<=0.55 eros>=0.18).
SPALL_ABS_NCOL = 6
SPALL_ABS_JOINT_FRAC = 0.08
SPALL_ABS_TILT_BLEND = 0.10
SPALL_ABS_CENTRE_JITTER = 0.08
SPALL_ABS_FORCE_GRID = False
SPALL_ABS_PHASE_SCALE = 0.3
"""


def main() -> int:
    text = TEXTURE.read_text(encoding="utf-8")
    original = text

    # --- constants ---
    if "SPALL_SCAR_COUNT = 44" not in text and "SPALL_SCAR_COUNT = 96" not in text:
        print("FAIL: SPALL_SCAR_COUNT not found in expected form", file=sys.stderr)
        return 2

    text = text.replace(
        "SPALL_SCAR_COUNT = 44",
        "SPALL_SCAR_COUNT = 96  # a6c96w abs-col ship",
        1,
    )
    text = text.replace(
        "SPALL_WIDTH_MIN_FRACTION = 0.05",
        "SPALL_WIDTH_MIN_FRACTION = 0.045  # a6c96w",
        1,
    )
    text = text.replace(
        "SPALL_WIDTH_MAX_FRACTION = 0.16",
        "SPALL_WIDTH_MAX_FRACTION = 0.11  # a6c96w",
        1,
    )
    text = text.replace(
        "JOINT_TRACE_COUNT = 11",
        "JOINT_TRACE_COUNT = 18  # a6c96w",
        1,
    )

    if "SPALL_ABS_NCOL" not in text:
        # Insert after SPALL_OFFSET_MAX_FRACTION line
        m = re.search(
            r"(SPALL_OFFSET_MAX_FRACTION\s*=\s*[^\n]+\n)",
            text,
        )
        if not m:
            print("FAIL: SPALL_OFFSET_MAX_FRACTION anchor missing", file=sys.stderr)
            return 2
        insert_at = m.end()
        text = text[:insert_at] + ABS_CONSTANTS + text[insert_at:]

    # --- replace _build_spall_scars def body ---
    # Match from def _build_spall_scars through its return mask, offset + blank line
    pat = re.compile(
        r"def _build_spall_scars\(spec: GeologyTextureSpec,.*?"
        r"return mask, offset\n",
        re.DOTALL,
    )
    m = pat.search(text)
    if not m:
        print("FAIL: could not locate def _build_spall_scars ... return mask, offset",
              file=sys.stderr)
        return 2
    if "SPALL_ABS_NCOL" in m.group(0) and "Absolute-X column spall" in m.group(0):
        print("INFO: _build_spall_scars already looks shipped; constants-only pass")
    else:
        text = text[: m.start()] + NEW_BUILD + text[m.end() :]

    if text == original:
        print("NOOP: texture.py already matches a6c96w ship target")
        return 0

    TEXTURE.write_text(text, encoding="utf-8", newline="\n")
    # verify
    check = TEXTURE.read_text(encoding="utf-8")
    ok = True
    for needle in (
        "SPALL_SCAR_COUNT = 96",
        "SPALL_WIDTH_MIN_FRACTION = 0.045",
        "SPALL_WIDTH_MAX_FRACTION = 0.11",
        "JOINT_TRACE_COUNT = 18",
        "SPALL_ABS_NCOL = 6",
        "SPALL_ABS_JOINT_FRAC = 0.08",
        "SPALL_ABS_TILT_BLEND = 0.10",
        "SPALL_ABS_CENTRE_JITTER = 0.08",
        "SPALL_ABS_FORCE_GRID = False",
        "SPALL_ABS_PHASE_SCALE = 0.3",
        "Absolute-X column spall",
        "col_i = index % ncols",
    ):
        if needle not in check:
            print(f"FAIL post-check missing: {needle!r}", file=sys.stderr)
            ok = False
    if "SPALL_SCAR_COUNT = 44" in check:
        print("FAIL: old SPALL_SCAR_COUNT = 44 still present", file=sys.stderr)
        ok = False
    if not ok:
        return 3
    print("SHIP_OK: texture.py a6c96w abs-col spall applied")
    print(f"  path={TEXTURE}")
    print(f"  bytes={len(check)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
