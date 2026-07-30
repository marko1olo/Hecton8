# -*- coding: utf-8 -*-
"""Sweep SPALL params for geology run budget. Do not commit."""
from __future__ import annotations

import json
import os
import sys
import time
import traceback
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
sys.path.insert(0, str(ROOT / "Tools" / "Blender"))
os.chdir(ROOT)

OUT = ROOT / "Tools" / "_cline_geo_sweep_out.txt"


def extract_payload(out) -> dict:
    if isinstance(out, dict):
        return out
    payload = {}
    for a in ("structuralExtent", "manifest", "report", "meta", "spallCoverage"):
        if hasattr(out, a):
            payload[a] = getattr(out, a)
    # HeightField may nest report
    rep = payload.get("report") or payload.get("meta") or payload.get("manifest")
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
            spec = Spec(seed=seed, resolution=res)
            out = tex.build_height_field(spec)
            payload = extract_payload(out)
            se = payload.get("structuralExtent") or {}
            if not se and hasattr(out, "__dict__"):
                # debug once
                keys = [k for k in dir(out) if not k.startswith("_")]
                raise RuntimeError(f"no structuralExtent; attrs={keys[:40]} payload={list(payload)}")
            p95 = (se.get("longestIntactRunFraction") or {}).get("p95")
            eros = se.get("erosionalCoverage")
            met = se.get("runBudgetMet")
            eros_met = se.get("erosionalCoverageMet")
            spall_cov = payload.get("spallCoverage")
            rows.append(
                {
                    "seed": seed,
                    "p95": p95,
                    "erosional": eros,
                    "runBudgetMet": met,
                    "erosionalCoverageMet": eros_met,
                    "spallCoverage": spall_cov,
                    "dt": round(time.time() - t0, 3),
                }
            )
        except Exception as e:
            rows.append(
                {
                    "seed": seed,
                    "error": f"{e}\n{traceback.format_exc()[-500:]}",
                    "dt": round(time.time() - t0, 3),
                }
            )
    return rows


def summarize(rows, label, count, wmin, wmax, buf):
    good = [r for r in rows if r.get("p95") is not None]
    bad = [r for r in rows if r.get("p95") is None]
    if not good:
        line = f"{label} count={count} width={wmin}-{wmax} NO_DATA bad={len(bad)}"
        print(line, flush=True)
        buf.append(line)
        for r in bad[:2]:
            buf.append("  ERR " + str(r)[:500])
        return {
            "label": label,
            "count": count,
            "wmin": wmin,
            "wmax": wmax,
            "p95_mean": 9.0,
            "p95_max": 9.0,
            "eros_mean": 0.0,
            "eros_max": 0.0,
            "all_run_met": False,
            "all_eros_met": False,
        }
    p95s = [r["p95"] for r in good]
    eross = [r["erosional"] for r in good if r.get("erosional") is not None]
    line = (
        f"{label} count={count} width={wmin}-{wmax} "
        f"p95_mean={sum(p95s)/len(p95s):.4f} p95_max={max(p95s):.4f} p95_min={min(p95s):.4f} "
        f"eros_mean={sum(eross)/len(eross):.4f} eros_max={max(eross):.4f} "
        f"all_run_met={all(r.get('runBudgetMet') for r in good)} "
        f"all_eros_met={all(r.get('erosionalCoverageMet') for r in good)}"
    )
    print(line, flush=True)
    buf.append(line)
    for r in rows:
        buf.append("  " + json.dumps({k: v for k, v in r.items() if k != "error"}, default=str))
    return {
        "label": label,
        "count": count,
        "wmin": wmin,
        "wmax": wmax,
        "p95_mean": sum(p95s) / len(p95s),
        "p95_max": max(p95s),
        "eros_mean": sum(eross) / len(eross),
        "eros_max": max(eross),
        "all_run_met": all(r.get("runBudgetMet") for r in good),
        "all_eros_met": all(r.get("erosionalCoverageMet") for r in good),
    }


def main() -> int:
    from h8forge import texture as tex
    from h8forge import law

    # discover HeightField attrs once
    s0 = tex.GeologyTextureSpec(seed=0, resolution=64)
    hf = tex.build_height_field(s0)
    print("HF attrs", [k for k in dir(hf) if not k.startswith("_")], flush=True)
    print("extract", extract_payload(hf).keys(), flush=True)

    buf = []
    seeds = [0, 1, 2, 7, 13]
    base_count = tex.SPALL_SCAR_COUNT
    base_wmin = tex.SPALL_WIDTH_MIN_FRACTION
    base_wmax = tex.SPALL_WIDTH_MAX_FRACTION
    buf.append(
        f"budget run<={law.GEOLOGY_LAMINA_MAX_RUN_FRACTION} eros>={law.GEOLOGY_MIN_EROSIONAL_COVERAGE}"
    )
    buf.append(f"baseline constants count={base_count} width={base_wmin}-{base_wmax}")

    candidates = [
        (44, 0.05, 0.16),
        (52, 0.05, 0.16),
        (60, 0.05, 0.16),
        (68, 0.05, 0.16),
        (72, 0.05, 0.16),
        (80, 0.05, 0.14),
        (56, 0.05, 0.18),
        (64, 0.05, 0.18),
        (64, 0.05, 0.20),
        (56, 0.06, 0.18),
        (48, 0.06, 0.20),
        (56, 0.04, 0.14),
    ]

    summaries = []
    for count, wmin, wmax in candidates:
        tex.SPALL_SCAR_COUNT = count
        tex.SPALL_WIDTH_MIN_FRACTION = wmin
        tex.SPALL_WIDTH_MAX_FRACTION = wmax
        rows = measure(tex, seeds, res=512)
        summaries.append(summarize(rows, "cfg", count, wmin, wmax, buf))

    tex.SPALL_SCAR_COUNT = base_count
    tex.SPALL_WIDTH_MIN_FRACTION = base_wmin
    tex.SPALL_WIDTH_MAX_FRACTION = base_wmax

    ok = [
        s
        for s in summaries
        if s["all_run_met"] and s["all_eros_met"] and s["eros_max"] < 0.42
    ]
    buf.append("=== PASSING (run+eros, eros_max<0.42) ===")
    if ok:
        ok.sort(key=lambda s: (s["eros_mean"], s["p95_max"]))
        for s in ok:
            buf.append(json.dumps(s))
        best = ok[0]
    else:
        buf.append("none fully passing; nearest by p95_max among eros_met")
        near = [s for s in summaries if s["all_eros_met"] and s["eros_max"] < 0.50]
        near.sort(key=lambda s: (s["p95_max"], s["eros_mean"]))
        for s in near[:6]:
            buf.append(json.dumps(s))
        best = near[0] if near else min(summaries, key=lambda s: s["p95_max"])

    buf.append("BEST " + json.dumps(best))
    print("BEST", best, flush=True)

    if best.get("all_run_met"):
        tex.SPALL_SCAR_COUNT = best["count"]
        tex.SPALL_WIDTH_MIN_FRACTION = best["wmin"]
        tex.SPALL_WIDTH_MAX_FRACTION = best["wmax"]
        buf.append("=== VERIFY 2048 seeds 0,1,2 ===")
        print("verifying 2048...", flush=True)
        rows2048 = measure(tex, [0, 1, 2], res=2048)
        summarize(rows2048, "ship2048", best["count"], best["wmin"], best["wmax"], buf)

    OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
    print(f"wrote {OUT}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
