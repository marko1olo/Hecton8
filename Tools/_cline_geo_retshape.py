# -*- coding: utf-8 -*-
"""Probe smarter spall placement + joint count for run budget. Do not commit."""
from __future__ import annotations

import json
import math
import os
import sys
import time
import traceback
from pathlib import Path
from typing import Optional

import numpy as np

ROOT = Path(r"C:\hades\Hecton8")
sys.path.insert(0, str(ROOT / "Tools" / "Blender"))
os.chdir(ROOT)

OUT = ROOT / "Tools" / "_cline_geo_retshape_out.txt"


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


def make_stratified_spall(tex):
    """Return a replacement _build_spall_scars with lateral stratification.

    Problem measured: random joint+side clustering leaves seed-dependent horizontal
    corridors unbroken (seed 2 p95 ~0.72-0.82 across count sweeps). Fix: cycle joints
    and place finite-width bands at stratified perpendicular offsets so the face is
    carved in columns; keep joint-parallel edges for angularity.
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
                # Cycle joints so every usable fracture anchors scars, then jitter.
                trace = usable[index % n_joints]
                direction = np.array(
                    [float(trace["dirRow"]), float(trace["dirCol"])], dtype=np.float64
                )
                # Origin on the joint line, jittered along-trace so scars do not stack
                # on the joint start point alone.
                origin = np.array(
                    [float(trace["startRow"]), float(trace["startCol"])], dtype=np.float64
                )
                along = (rng.random() - 0.5) * spec.tile_m
                origin = origin + direction * along
                origin = np.mod(origin, spec.tile_m)
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
            normal = np.array([direction[1], -direction[0]])

            offset_row = py - origin[0]
            offset_col = px - origin[1]
            offset_row = offset_row - spec.tile_m * np.rint(offset_row / spec.tile_m)
            offset_col = offset_col - spec.tile_m * np.rint(offset_col / spec.tile_m)
            lateral = offset_row * normal[0] + offset_col * normal[1] + edge_jitter

            width = (
                float(rng.uniform(tex.SPALL_WIDTH_MIN_FRACTION, tex.SPALL_WIDTH_MAX_FRACTION))
                * spec.tile_m
            )

            # Stratified perpendicular centre: distribute bands across the tile so
            # random side-of-joint clustering cannot leave a seed-wide corridor.
            # Keep a fraction of scars glued to the joint (centre near 0) for the
            # "terminates at a real joint" reading; the rest roam.
            pass_i = index // n_joints
            passes = max(1, int(math.ceil(total / float(n_joints))))
            if rng.random() < 0.35:
                centre = 0.0  # on-joint
                side = 1.0 if rng.random() < 0.5 else -1.0
                in_slab = (lateral * side >= 0.0) & (lateral * side < width)
            else:
                lat_stratum = (pass_i + rng.random()) / float(passes)
                centre = (lat_stratum - 0.5) * spec.tile_m
                # Minimum-image distance to centre line parallel to the joint.
                d = lateral - centre
                d = d - spec.tile_m * np.rint(d / spec.tile_m)
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
        # baseline
        {"label": "baseline", "count": 44, "wmin": 0.05, "wmax": 0.16, "joints": 11, "place": "orig"},
        # placement only
        {"label": "strat44", "count": 44, "wmin": 0.05, "wmax": 0.16, "joints": 11, "place": "strat"},
        {"label": "strat56", "count": 56, "wmin": 0.05, "wmax": 0.16, "joints": 11, "place": "strat"},
        {"label": "strat68", "count": 68, "wmin": 0.05, "wmax": 0.14, "joints": 11, "place": "strat"},
        {"label": "strat56j16", "count": 56, "wmin": 0.05, "wmax": 0.16, "joints": 16, "place": "strat"},
        {"label": "strat64j16", "count": 64, "wmin": 0.05, "wmax": 0.14, "joints": 16, "place": "strat"},
        {"label": "strat72j18", "count": 72, "wmin": 0.04, "wmax": 0.12, "joints": 18, "place": "strat"},
        {"label": "strat48j14", "count": 48, "wmin": 0.06, "wmax": 0.18, "joints": 14, "place": "strat"},
        {"label": "strat60j14w", "count": 60, "wmin": 0.05, "wmax": 0.18, "joints": 14, "place": "strat"},
        # orig placement with more joints
        {"label": "orig56j16", "count": 56, "wmin": 0.05, "wmax": 0.16, "joints": 16, "place": "orig"},
        {"label": "orig68j16", "count": 68, "wmin": 0.05, "wmax": 0.14, "joints": 16, "place": "orig"},
    ]

    summaries = []
    for c in candidates:
        restore()
        tex.SPALL_SCAR_COUNT = c["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = c["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = c["wmax"]
        tex.JOINT_TRACE_COUNT = c["joints"]
        if c["place"] == "strat":
            tex._build_spall_scars = make_stratified_spall(tex)
        label = (
            f"{c['label']} c={c['count']} w={c['wmin']}-{c['wmax']} j={c['joints']} p={c['place']}"
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
        for s in near[:8]:
            buf.append(json.dumps(s))
        best = near[0] if near else min(summaries, key=lambda s: s["p95_max"])

    buf.append("BEST " + json.dumps(best))
    print("BEST", best, flush=True)

    # Verify best at 2048 if it passes run budget
    if best.get("all_run_met"):
        restore()
        tex.SPALL_SCAR_COUNT = best["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = best["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = best["wmax"]
        tex.JOINT_TRACE_COUNT = best["joints"]
        if best.get("place") == "strat":
            tex._build_spall_scars = make_stratified_spall(tex)
        buf.append("=== VERIFY 2048 seeds 0,1,2,7,13 ===")
        print("verifying 2048...", flush=True)
        rows2048 = measure(tex, seeds, res=2048)
        summarize(rows2048, "ship2048 " + best["label"], buf)

    restore()
    OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
    print(f"wrote {OUT}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
