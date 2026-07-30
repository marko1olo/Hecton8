# -*- coding: utf-8 -*-
"""v4: kill seed1 corridor @2048 that beat a6c72 (p95=0.584).

v3 a6c72: PASS@512 p95_max=0.5332; FAIL@2048 seed1=0.584.
v3 a3c72w: PASS@512 p95_max=0.5028 eros_max=0.408 — never verified @2048.

Strategy:
  * lower centre jitter (0.30→0.10-0.15) so columns stay regular
  * denser ncols 6-8, slightly wider floors, lower joint_frac
  * hybrid: a6 denser + a3c72w-style wider bands
  * force-grid mode: every scar lands on fixed absolute X lattice (no rng centre)
  * ALWAYS verify top candidates @2048 (not just first passer)
  * probe@512 filter: p95_max<=0.52 for margin before 2048 spend

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

OUT = ROOT / "Tools" / "_cline_geo_retshape_v4_out.txt"


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


def make_abs_col_spall(
    tex,
    n_col: int = 6,
    joint_frac: float = 0.12,
    tilt_blend: float = 0.15,
    centre_jitter: float = 0.15,
    force_grid: bool = False,
    phase_scale: float = 0.5,
):
    """Absolute-column scar placement with tunable regularity.

    force_grid: centre_x is pure lattice (no rng jitter) — max gap deterministic.
    centre_jitter: fraction of column pitch used as ±rng offset (v3 used 0.30).
    phase_scale: how much vertical stratum shifts column X (v3 used 0.5).
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
        pitch = spec.tile_m / float(ncols)

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
                col_i = index % ncols
                phase = (start_lamina / max(1.0, float(lamina_count))) * phase_scale
                # Secondary stagger: half-pitch shift every other vertical half
                # so stacked packages don't leave the same vertical corridor free.
                half = 0.5 if (start_lamina // max(1, lamina_count // 4)) % 2 else 0.0
                base_x = ((col_i + 0.5 + phase + half) / float(ncols)) * spec.tile_m
                if force_grid:
                    centre_x = base_x % spec.tile_m
                else:
                    centre_x = (
                        base_x + (rng.random() - 0.5) * pitch * centre_jitter
                    ) % spec.tile_m

                d_col = px - centre_x
                d_col = d_col - spec.tile_m * np.rint(d_col / spec.tile_m)
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
    buf.append("v4 absolute-column denser/regular + a3c72w 2048 + force_grid")

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

    # Around a6c72 (best eros margin) and a3c72w (best p95@512).
    # Also force_grid variants and denser 7-8 cols.
    candidates = [
        # --- re-check v3 passers with lower jitter ---
        {"label": "a6c72j10", "count": 72, "wmin": 0.035, "wmax": 0.10, "joints": 16,
         "ncols": 6, "jfrac": 0.12, "tilt": 0.15, "cjit": 0.10, "fg": False, "ps": 0.5},
        {"label": "a6c72j05", "count": 72, "wmin": 0.035, "wmax": 0.10, "joints": 16,
         "ncols": 6, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.05, "fg": False, "ps": 0.5},
        {"label": "a6c72fg", "count": 72, "wmin": 0.04, "wmax": 0.11, "joints": 16,
         "ncols": 6, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.0, "fg": True, "ps": 0.4},
        # denser columns
        {"label": "a7c80", "count": 80, "wmin": 0.035, "wmax": 0.10, "joints": 16,
         "ncols": 7, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.12, "fg": False, "ps": 0.5},
        {"label": "a7c80w", "count": 80, "wmin": 0.045, "wmax": 0.12, "joints": 16,
         "ncols": 7, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.10, "fg": False, "ps": 0.4},
        {"label": "a7c80fg", "count": 80, "wmin": 0.04, "wmax": 0.11, "joints": 16,
         "ncols": 7, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.0, "fg": True, "ps": 0.35},
        {"label": "a8c84", "count": 84, "wmin": 0.035, "wmax": 0.10, "joints": 18,
         "ncols": 8, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.10, "fg": False, "ps": 0.4},
        {"label": "a8c84w", "count": 84, "wmin": 0.04, "wmax": 0.11, "joints": 18,
         "ncols": 8, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.08, "fg": False, "ps": 0.35},
        {"label": "a8c88fg", "count": 88, "wmin": 0.04, "wmax": 0.10, "joints": 18,
         "ncols": 8, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.0, "fg": True, "ps": 0.3},
        # a6 wider (kill seed1 thin corridors)
        {"label": "a6c80w", "count": 80, "wmin": 0.045, "wmax": 0.12, "joints": 16,
         "ncols": 6, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.10, "fg": False, "ps": 0.4},
        {"label": "a6c88w", "count": 88, "wmin": 0.05, "wmax": 0.12, "joints": 16,
         "ncols": 6, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.08, "fg": False, "ps": 0.35},
        {"label": "a6c96w", "count": 96, "wmin": 0.045, "wmax": 0.11, "joints": 18,
         "ncols": 6, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.08, "fg": False, "ps": 0.3},
        # a3c72w family (best p95@512 v3) — must 2048-verify
        {"label": "a3c72w", "count": 72, "wmin": 0.06, "wmax": 0.14, "joints": 14,
         "ncols": 3, "jfrac": 0.18, "tilt": 0.20, "cjit": 0.30, "fg": False, "ps": 0.5},
        {"label": "a3c72wj", "count": 72, "wmin": 0.06, "wmax": 0.14, "joints": 14,
         "ncols": 3, "jfrac": 0.12, "tilt": 0.15, "cjit": 0.12, "fg": False, "ps": 0.4},
        {"label": "a3c80w", "count": 80, "wmin": 0.055, "wmax": 0.13, "joints": 16,
         "ncols": 3, "jfrac": 0.12, "tilt": 0.15, "cjit": 0.10, "fg": False, "ps": 0.35},
        {"label": "a4c80w2", "count": 80, "wmin": 0.055, "wmax": 0.13, "joints": 16,
         "ncols": 4, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.10, "fg": False, "ps": 0.35},
        {"label": "a4c88w", "count": 88, "wmin": 0.05, "wmax": 0.12, "joints": 16,
         "ncols": 4, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.08, "fg": False, "ps": 0.3},
        {"label": "a5c88w", "count": 88, "wmin": 0.045, "wmax": 0.12, "joints": 16,
         "ncols": 5, "jfrac": 0.10, "tilt": 0.12, "cjit": 0.08, "fg": False, "ps": 0.3},
        # half-pitch stagger heavy (ps low, half-shift active)
        {"label": "a6c80st", "count": 80, "wmin": 0.04, "wmax": 0.11, "joints": 16,
         "ncols": 6, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.05, "fg": False, "ps": 0.15},
        {"label": "a7c88st", "count": 88, "wmin": 0.04, "wmax": 0.11, "joints": 18,
         "ncols": 7, "jfrac": 0.08, "tilt": 0.10, "cjit": 0.05, "fg": False, "ps": 0.15},
    ]

    summaries = []
    for c in candidates:
        restore()
        tex.SPALL_SCAR_COUNT = c["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = c["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = c["wmax"]
        tex.JOINT_TRACE_COUNT = c["joints"]
        tex._build_spall_scars = make_abs_col_spall(
            tex,
            n_col=c["ncols"],
            joint_frac=c["jfrac"],
            tilt_blend=c["tilt"],
            centre_jitter=c["cjit"],
            force_grid=c["fg"],
            phase_scale=c["ps"],
        )
        label = (
            f"{c['label']} c={c['count']} w={c['wmin']}-{c['wmax']} "
            f"j={c['joints']} ncol={c['ncols']} jf={c['jfrac']} t={c['tilt']} "
            f"cj={c['cjit']} fg={int(c['fg'])} ps={c['ps']}"
        )
        print("===", label, flush=True)
        rows = measure(tex, seeds, res=512)
        s = summarize(rows, label, buf)
        if s:
            s.update(c)
            summaries.append(s)

    restore()

    # Prefer full passers with eros_max headroom; else near by p95_max
    ok = [
        s
        for s in summaries
        if s["all_run_met"] and s["all_eros_met"] and s["eros_max"] < 0.42
    ]
    buf.append("=== PASSING@512 ===")
    if ok:
        # prefer lower p95_max then lower eros (margin for 2048)
        ok.sort(key=lambda s: (s["p95_max"], s["eros_mean"]))
        for s in ok:
            buf.append(json.dumps({k: v for k, v in s.items() if k != "label"} | {"label": s["label"]}))
    else:
        buf.append("none fully passing@512")

    # Candidates for 2048: all passers + near-misses with p95_max<=0.56
    verify_pool = list(ok) if ok else []
    near = [
        s
        for s in summaries
        if s not in verify_pool
        and s.get("all_eros_met")
        and s["p95_max"] <= 0.56
        and s["eros_max"] < 0.45
    ]
    near.sort(key=lambda s: s["p95_max"])
    for s in near[:4]:
        if s not in verify_pool:
            verify_pool.append(s)

    # Always include a3c72w and lowest p95_max overall
    by_label = {s["label"].split()[0] if " " in s["label"] else s.get("label"): s for s in summaries}
    # summaries store full label string in s["label"] from summarize — also c keys
    for s in summaries:
        lab = s.get("label", "")
        # recover short label from candidate match
        for c in candidates:
            if lab.startswith(c["label"]) or s.get("count") == c["count"] and s.get("ncols") == c["ncols"]:
                pass
        short = None
        for c in candidates:
            if s.get("count") == c["count"] and s.get("ncols") == c["ncols"] and s.get("wmin") == c["wmin"] and s.get("fg") == c["fg"] and s.get("cjit") == c["cjit"]:
                short = c["label"]
                break
        if short:
            s["short"] = short

    # force include known interesting shorts
    for want in ("a3c72w", "a6c72j10", "a6c80w", "a7c80w", "a8c84w", "a4c80w2"):
        for s in summaries:
            if s.get("short") == want and s not in verify_pool:
                verify_pool.append(s)
                break

    # Cap 2048 verifies (each ~3.5min) — top 5 by p95_max among pool with all_run or near
    verify_pool = list({id(s): s for s in verify_pool}.values())
    verify_pool.sort(key=lambda s: (0 if s.get("all_run_met") else 1, s["p95_max"], s["eros_mean"]))
    verify_pool = verify_pool[:6]

    buf.append("=== VERIFY POOL ===")
    for s in verify_pool:
        buf.append(
            f"  {s.get('short', s['label'])} p95_max={s['p95_max']:.4f} "
            f"eros_max={s['eros_max']:.4f} run={s['all_run_met']}"
        )
        print(
            f"POOL {s.get('short', s['label'])} p95_max={s['p95_max']:.4f} "
            f"run={s['all_run_met']}",
            flush=True,
        )

    best2048 = None
    for s in verify_pool:
        restore()
        # recover params from s (merged from c)
        tex.SPALL_SCAR_COUNT = s["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = s["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = s["wmax"]
        tex.JOINT_TRACE_COUNT = s["joints"]
        tex._build_spall_scars = make_abs_col_spall(
            tex,
            n_col=s["ncols"],
            joint_frac=s["jfrac"],
            tilt_blend=s["tilt"],
            centre_jitter=s["cjit"],
            force_grid=s["fg"],
            phase_scale=s["ps"],
        )
        short = s.get("short", s["label"][:20])
        buf.append(f"=== VERIFY 2048 {short} ===")
        print(f"verifying 2048 {short}...", flush=True)
        rows2048 = measure(tex, seeds, res=2048)
        s2048 = summarize(rows2048, "ship2048 " + short, buf)
        if s2048:
            s2048["src"] = short
            s2048["params"] = {
                "count": s["count"],
                "wmin": s["wmin"],
                "wmax": s["wmax"],
                "joints": s["joints"],
                "ncols": s["ncols"],
                "jfrac": s["jfrac"],
                "tilt": s["tilt"],
                "cjit": s["cjit"],
                "fg": s["fg"],
                "ps": s["ps"],
            }
            buf.append("VERIFY2048 " + json.dumps(s2048))
            if (
                s2048["all_run_met"]
                and s2048["all_eros_met"]
                and s2048["eros_max"] < 0.45
            ):
                if best2048 is None or s2048["p95_max"] < best2048["p95_max"]:
                    best2048 = s2048
                    print(f"*** 2048 PASS {short} p95_max={s2048['p95_max']:.4f}", flush=True)
                # keep scanning for better eros margin if we have budget
            else:
                print(
                    f"2048 FAIL {short} p95_max={s2048['p95_max']:.4f} "
                    f"run={s2048['all_run_met']}",
                    flush=True,
                )

    restore()
    if best2048:
        buf.append("BEST2048 " + json.dumps(best2048))
        print("BEST2048", best2048, flush=True)
    else:
        buf.append("BEST2048 none")
        print("BEST2048 none", flush=True)
        # report nearest
        # re-read from buf is hard; just note
        near_note = min(summaries, key=lambda x: x["p95_max"])
        buf.append("NEAREST512 " + json.dumps({k: near_note[k] for k in (
            "label", "p95_max", "p95_mean", "eros_max", "all_run_met", "count",
            "ncols", "wmin", "wmax", "jfrac", "tilt", "cjit", "fg", "ps", "joints"
        ) if k in near_note}))

    OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
    print(f"wrote {OUT}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
