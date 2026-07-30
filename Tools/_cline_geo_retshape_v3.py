# -*- coding: utf-8 -*-
"""v3: absolute tile-X column bands (metric is per-row horizontal runs).

v1 strat best p95_max=0.6024; v2 joint-relative grid worse (~0.70+).
Root cause: lateral centres computed in joint frame do not guarantee cuts at
absolute column positions that the run metric measures. Fix: place most scars
as finite-width bands at stratified ABSOLUTE px centres. Keep a minority of
joint side-slabs for angular termination reading.

Do not commit this script.
"""
from __future__ import annotations

import json
import math
import os
import sys
import time
import traceback
from pathlib import Path

import numpy as np

ROOT = Path(r"C:\hades\Hecton8")
sys.path.insert(0, str(ROOT / "Tools" / "Blender"))
os.chdir(ROOT)

OUT = ROOT / "Tools" / "_cline_geo_retshape_v3_out.txt"


def extract_payload(out) -> dict:
    payload = {}
    for a in ("structuralExtent", "manifest", "report", "meta", "spallCoverage"):
        if hasattr(out, a):
            payload[a] = getattr(out, a)
    rep = payload.get("report") or {}
    if isinstance(rep, dict):
        if "structuralExtent" not in payload and "structuralExtent" in rep:
            payload["structuralExtent"] = rep["structuralExtent"]
        if "spallCoverage" not in payload and "spallCoverage" in rep:
            payload["spallCoverage"] = rep["spallCoverage"]
    return payload


def measure(tex, seeds, res=512):
    Spec = tex.GeologyTextureSpec
    rows = []
    for seed in seeds:
        t0 = time.time()
        try:
            out = tex.build_height_field(Spec(seed=seed, resolution=res))
            payload = extract_payload(out)
            se = payload.get("structuralExtent") or {}
            p95 = (se.get("longestIntactRunFraction") or {}).get("p95")
            rows.append(
                {
                    "seed": seed,
                    "p95": p95,
                    "erosional": se.get("erosionalCoverage"),
                    "runBudgetMet": se.get("runBudgetMet"),
                    "erosionalCoverageMet": se.get("erosionalCoverageMet"),
                    "spallCoverage": payload.get("spallCoverage"),
                    "dt": round(time.time() - t0, 3),
                }
            )
        except Exception as e:
            rows.append(
                {
                    "seed": seed,
                    "error": f"{e}\n{traceback.format_exc()[-400:]}",
                    "dt": round(time.time() - t0, 3),
                }
            )
    return rows


def summarize(rows, label, buf):
    good = [r for r in rows if r.get("p95") is not None]
    if not good:
        line = f"{label} NO_DATA"
        print(line, flush=True)
        buf.append(line)
        for r in rows[:2]:
            buf.append("  ERR " + str(r)[:400])
        return None
    p95s = [r["p95"] for r in good]
    eross = [r["erosional"] for r in good if r.get("erosional") is not None]
    line = (
        f"{label} p95_mean={sum(p95s)/len(p95s):.4f} p95_max={max(p95s):.4f} "
        f"p95_min={min(p95s):.4f} eros_mean={sum(eross)/len(eross):.4f} "
        f"eros_max={max(eross):.4f} all_run={all(r.get('runBudgetMet') for r in good)} "
        f"all_eros={all(r.get('erosionalCoverageMet') for r in good)}"
    )
    print(line, flush=True)
    buf.append(line)
    for r in rows:
        buf.append("  " + json.dumps({k: v for k, v in r.items() if k != "error"}, default=str))
    return {
        "label": label,
        "p95_mean": sum(p95s) / len(p95s),
        "p95_max": max(p95s),
        "eros_mean": sum(eross) / len(eross),
        "eros_max": max(eross),
        "all_run_met": all(r.get("runBudgetMet") for r in good),
        "all_eros_met": all(r.get("erosionalCoverageMet") for r in good),
    }


def make_abs_col_spall(tex, n_col: int = 4, joint_frac: float = 0.22, tilt_blend: float = 0.25):
    """Absolute-column scar placement.

    Most scars: finite-width band at stratified absolute px centre, lightly tilted
    toward the nearest usable joint normal so edges stay angular (not pure masonry
    vertical strips). Minority: authentic joint side-slabs.
    """
    substream = tex.substream
    periodic_warp = tex.periodic_warp

    def _build_spall_scars(spec, base_coordinate, boundaries, thicknesses, joint_traces):
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
        edge_jitter = periodic_warp(
            field_rng, resolution, spec.tile_m, wavelength_m=0.055, amplitude=0.007
        )

        total = max(1, tex.SPALL_SCAR_COUNT)
        n_joints = max(1, len(usable)) if usable else 1
        ncols = max(2, int(n_col))

        for index in range(total):
            stratum = (index + rng.random()) / float(total)
            start_lamina = int(min(lamina_count - 1, stratum * lamina_count))
            package = int(
                rng.integers(tex.SPALL_PACKAGE_LAMINAE_MIN, tex.SPALL_PACKAGE_LAMINAE_MAX + 1)
            )
            lo = float(boundaries[start_lamina])
            hi = float(boundaries[min(start_lamina + package, lamina_count)])
            if hi <= lo:
                continue
            if hi <= spec.tile_m:
                in_package = (base_coordinate >= lo) & (base_coordinate < hi)
            else:
                in_package = (base_coordinate >= lo) | (
                    base_coordinate < (hi - spec.tile_m)
                )

            if usable:
                trace = usable[index % n_joints]
                direction = np.array(
                    [float(trace["dirRow"]), float(trace["dirCol"])], dtype=np.float64
                )
                origin = np.array(
                    [float(trace["startRow"]), float(trace["startCol"])], dtype=np.float64
                )
                along = (rng.random() - 0.5) * spec.tile_m
                origin = np.mod(origin + direction * along, spec.tile_m)
            else:
                angle = math.radians(
                    tex.JOINT_STRESS_AZIMUTH_DEG + tex.JOINT_CONJUGATE_DEG
                )
                origin = rng.random(2) * spec.tile_m
                direction = np.array([math.sin(angle), math.cos(angle)])

            norm = float(np.hypot(direction[0], direction[1]))
            if norm < 1e-9:
                continue
            direction = direction / norm
            joint_normal = np.array([direction[1], -direction[0]])

            offset_row = py - origin[0]
            offset_col = px - origin[1]
            offset_row = offset_row - spec.tile_m * np.rint(offset_row / spec.tile_m)
            offset_col = offset_col - spec.tile_m * np.rint(offset_col / spec.tile_m)
            joint_lateral = (
                offset_row * joint_normal[0] + offset_col * joint_normal[1] + edge_jitter
            )

            width = (
                float(rng.uniform(tex.SPALL_WIDTH_MIN_FRACTION, tex.SPALL_WIDTH_MAX_FRACTION))
                * spec.tile_m
            )

            if rng.random() < joint_frac and usable:
                side = 1.0 if rng.random() < 0.5 else -1.0
                in_slab = (joint_lateral * side >= 0.0) & (joint_lateral * side < width)
            else:
                # Absolute column centre — this is what the row-run metric sees.
                col_i = index % ncols
                # Also advance column slowly with vertical stratum so stacked
                # packages do not share the exact same X (avoids ladder look).
                phase = (start_lamina / max(1.0, float(lamina_count))) * 0.5
                centre_x = (
                    ((col_i + 0.5 + phase) / float(ncols)) * spec.tile_m
                    + (rng.random() - 0.5) * (spec.tile_m / float(ncols)) * 0.30
                )
                centre_x = centre_x % spec.tile_m

                # Pure column distance on px, plus mild joint-normal tilt so the
                # band is not a drafting vertical (masonry risk).
                d_col = px - centre_x
                d_col = d_col - spec.tile_m * np.rint(d_col / spec.tile_m)
                # Tilt: project a bit of joint_lateral so walls lean with the joint.
                d = d_col * (1.0 - tilt_blend) + joint_lateral * tilt_blend + edge_jitter * 0.5
                in_slab = np.abs(d) < (width * 0.5)

            scar = in_package & in_slab
            if not scar.any():
                continue
            scar_offset = mean_thickness * float(
                rng.uniform(tex.SPALL_OFFSET_MIN_FRACTION, tex.SPALL_OFFSET_MAX_FRACTION)
            )
            offset = np.where(scar, scar_offset, offset)
            mask = np.where(scar, 1.0, mask)

        return mask, offset

    return _build_spall_scars


def main() -> int:
    from h8forge import texture as tex
    from h8forge import law

    buf = []
    seeds = [0, 1, 2, 7, 13]
    buf.append(
        f"budget run<={law.GEOLOGY_LAMINA_MAX_RUN_FRACTION} eros>={law.GEOLOGY_MIN_EROSIONAL_COVERAGE}"
    )
    buf.append("v3 absolute-column placement")

    base = {
        "SPALL_SCAR_COUNT": tex.SPALL_SCAR_COUNT,
        "SPALL_WIDTH_MIN_FRACTION": tex.SPALL_WIDTH_MIN_FRACTION,
        "SPALL_WIDTH_MAX_FRACTION": tex.SPALL_WIDTH_MAX_FRACTION,
        "JOINT_TRACE_COUNT": tex.JOINT_TRACE_COUNT,
        "_build_spall_scars": tex._build_spall_scars,
    }

    def restore():
        for k, v in base.items():
            setattr(tex, k, v)

    candidates = [
        # denser absolute columns, moderate width, keep eros <0.40
        {"label": "a3c48", "count": 48, "wmin": 0.05, "wmax": 0.14, "joints": 11, "ncols": 3, "jfrac": 0.25, "tilt": 0.25},
        {"label": "a4c48", "count": 48, "wmin": 0.05, "wmax": 0.14, "joints": 11, "ncols": 4, "jfrac": 0.22, "tilt": 0.25},
        {"label": "a3c56", "count": 56, "wmin": 0.05, "wmax": 0.14, "joints": 11, "ncols": 3, "jfrac": 0.22, "tilt": 0.25},
        {"label": "a4c56", "count": 56, "wmin": 0.05, "wmax": 0.13, "joints": 14, "ncols": 4, "jfrac": 0.22, "tilt": 0.20},
        {"label": "a5c56", "count": 56, "wmin": 0.04, "wmax": 0.12, "joints": 14, "ncols": 5, "jfrac": 0.18, "tilt": 0.20},
        {"label": "a4c64", "count": 64, "wmin": 0.04, "wmax": 0.13, "joints": 14, "ncols": 4, "jfrac": 0.20, "tilt": 0.22},
        {"label": "a5c64", "count": 64, "wmin": 0.04, "wmax": 0.12, "joints": 14, "ncols": 5, "jfrac": 0.18, "tilt": 0.18},
        {"label": "a4c68", "count": 68, "wmin": 0.04, "wmax": 0.12, "joints": 14, "ncols": 4, "jfrac": 0.20, "tilt": 0.20},
        {"label": "a5c68", "count": 68, "wmin": 0.04, "wmax": 0.11, "joints": 16, "ncols": 5, "jfrac": 0.15, "tilt": 0.18},
        {"label": "a6c72", "count": 72, "wmin": 0.035, "wmax": 0.10, "joints": 16, "ncols": 6, "jfrac": 0.15, "tilt": 0.15},
        {"label": "a4c60w", "count": 60, "wmin": 0.06, "wmax": 0.15, "joints": 14, "ncols": 4, "jfrac": 0.20, "tilt": 0.25},
        {"label": "a3c64t", "count": 64, "wmin": 0.05, "wmax": 0.14, "joints": 14, "ncols": 3, "jfrac": 0.30, "tilt": 0.35},
        # more aggressive: ensure max gap < 0.55 with 3 cols of width~0.10
        {"label": "a3c72w", "count": 72, "wmin": 0.06, "wmax": 0.14, "joints": 14, "ncols": 3, "jfrac": 0.18, "tilt": 0.20},
        {"label": "a4c80", "count": 80, "wmin": 0.04, "wmax": 0.11, "joints": 16, "ncols": 4, "jfrac": 0.15, "tilt": 0.18},
    ]

    summaries = []
    for c in candidates:
        restore()
        tex.SPALL_SCAR_COUNT = c["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = c["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = c["wmax"]
        tex.JOINT_TRACE_COUNT = c["joints"]
        tex._build_spall_scars = make_abs_col_spall(
            tex, n_col=c["ncols"], joint_frac=c["jfrac"], tilt_blend=c["tilt"]
        )
        label = (
            f"{c['label']} c={c['count']} w={c['wmin']}-{c['wmax']} "
            f"j={c['joints']} ncol={c['ncols']} jf={c['jfrac']} t={c['tilt']}"
        )
        print("===", label, flush=True)
        rows = measure(tex, seeds, res=512)
        s = summarize(rows, label, buf)
        if s:
            s.update(c)
            summaries.append(s)

    restore()

    ok = [
        s
        for s in summaries
        if s["all_run_met"] and s["all_eros_met"] and s["eros_max"] < 0.42
    ]
    buf.append("=== PASSING ===")
    if ok:
        ok.sort(key=lambda s: (s["eros_mean"], s["p95_max"]))
        for s in ok:
            buf.append(json.dumps(s))
        best = ok[0]
    else:
        buf.append("none fully passing; nearest by p95_max with eros_met and eros_max<0.50")
        near = [s for s in summaries if s["all_eros_met"] and s["eros_max"] < 0.50]
        near.sort(key=lambda s: (s["p95_max"], s["eros_mean"]))
        for s in near[:10]:
            buf.append(json.dumps(s))
        best = near[0] if near else min(summaries, key=lambda s: s["p95_max"])

    buf.append("BEST " + json.dumps(best))
    print("BEST", best, flush=True)

    if best.get("all_run_met"):
        restore()
        tex.SPALL_SCAR_COUNT = best["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = best["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = best["wmax"]
        tex.JOINT_TRACE_COUNT = best["joints"]
        tex._build_spall_scars = make_abs_col_spall(
            tex,
            n_col=best["ncols"],
            joint_frac=best["jfrac"],
            tilt_blend=best["tilt"],
        )
        buf.append("=== VERIFY 2048 seeds 0,1,2,7,13 ===")
        print("verifying 2048...", flush=True)
        rows2048 = measure(tex, seeds, res=2048)
        s2048 = summarize(rows2048, "ship2048 " + best["label"], buf)
        if s2048:
            buf.append("VERIFY2048 " + json.dumps(s2048))

    restore()
    OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
    print(f"wrote {OUT}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
