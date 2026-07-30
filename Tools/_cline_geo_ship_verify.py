#!/usr/bin/env python3
"""Proof a6c96w ship in texture.py @2048 — scratch, do not commit.

Seeds 0,1,2,7,13 must all satisfy:
  longestIntactRunFraction.p95 <= GEOLOGY_LAMINA_MAX_RUN_FRACTION (0.55)
  erosionalCoverage            >= GEOLOGY_MIN_EROSIONAL_COVERAGE (0.18)
Exit 0 only if all_run and all_eros.
"""
from __future__ import annotations

import json
import sys
import time
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools" / "Blender"))

from h8forge import texture as tex  # noqa: E402
from h8forge.law import (  # noqa: E402
    GEOLOGY_LAMINA_MAX_RUN_FRACTION,
    GEOLOGY_MIN_EROSIONAL_COVERAGE,
)

SEEDS = (0, 1, 2, 7, 13)
RES = 2048


def extract_payload(out) -> dict:
    payload = {}
    for a in ("structuralExtent", "manifest", "report", "meta", "spallCoverage"):
        if hasattr(out, a):
            payload[a] = getattr(out, a)
        elif isinstance(out, dict) and a in out:
            payload[a] = out[a]
    rep = payload.get("report") or {}
    if isinstance(rep, dict):
        if "structuralExtent" not in payload and "structuralExtent" in rep:
            payload["structuralExtent"] = rep["structuralExtent"]
        if "spallCoverage" not in payload and "spallCoverage" in rep:
            payload["spallCoverage"] = rep["spallCoverage"]
    return payload


def measure(seed: int, res: int = RES) -> dict:
    t0 = time.perf_counter()
    try:
        out = tex.build_height_field(tex.GeologyTextureSpec(seed=seed, resolution=res))
        payload = extract_payload(out)
        se = payload.get("structuralExtent") or {}
        if not isinstance(se, dict):
            se = dict(se) if hasattr(se, "items") else {}
        p95_raw = (se.get("longestIntactRunFraction") or {})
        if isinstance(p95_raw, dict):
            p95 = p95_raw.get("p95")
        else:
            p95 = None
        eros = se.get("erosionalCoverage")
        if eros is None:
            eros = payload.get("erosionalCoverage")
        p95_f = float(p95) if p95 is not None else None
        eros_f = float(eros) if eros is not None else None
        dt = time.perf_counter() - t0
        run_ok = p95_f is not None and p95_f <= GEOLOGY_LAMINA_MAX_RUN_FRACTION
        eros_ok = eros_f is not None and eros_f >= GEOLOGY_MIN_EROSIONAL_COVERAGE
        return {
            "seed": seed,
            "res": res,
            "p95": p95_f,
            "eros": eros_f,
            "run_ok": run_ok,
            "eros_ok": eros_ok,
            "runBudgetMet": se.get("runBudgetMet"),
            "erosionalCoverageMet": se.get("erosionalCoverageMet"),
            "seconds": round(dt, 2),
        }
    except Exception as e:
        return {
            "seed": seed,
            "res": res,
            "error": f"{e}\n{traceback.format_exc()[-500:]}",
            "run_ok": False,
            "eros_ok": False,
            "seconds": round(time.perf_counter() - t0, 2),
        }


def main() -> int:
    print(
        f"VERIFY a6c96w @res={RES} law run<={GEOLOGY_LAMINA_MAX_RUN_FRACTION} "
        f"eros>={GEOLOGY_MIN_EROSIONAL_COVERAGE}",
        flush=True,
    )
    print(
        f"  SPALL_SCAR_COUNT={tex.SPALL_SCAR_COUNT} "
        f"W=[{tex.SPALL_WIDTH_MIN_FRACTION},{tex.SPALL_WIDTH_MAX_FRACTION}] "
        f"JOINT={tex.JOINT_TRACE_COUNT} "
        f"ABS_NCOL={getattr(tex, 'SPALL_ABS_NCOL', '?')} "
        f"JFRAC={getattr(tex, 'SPALL_ABS_JOINT_FRAC', '?')} "
        f"TILT={getattr(tex, 'SPALL_ABS_TILT_BLEND', '?')} "
        f"CJIT={getattr(tex, 'SPALL_ABS_CENTRE_JITTER', '?')} "
        f"FG={getattr(tex, 'SPALL_ABS_FORCE_GRID', '?')} "
        f"PS={getattr(tex, 'SPALL_ABS_PHASE_SCALE', '?')}",
        flush=True,
    )
    rows = []
    for s in SEEDS:
        row = measure(s)
        rows.append(row)
        if row.get("error"):
            print(f"  seed={s:2d} ERROR {row['error'][:300]}", flush=True)
        else:
            flag = "PASS" if (row["run_ok"] and row["eros_ok"]) else "FAIL"
            print(
                f"  seed={s:2d} p95={row['p95']:.4f} eros={row['eros']:.4f} "
                f"run_ok={row['run_ok']} eros_ok={row['eros_ok']} "
                f"t={row['seconds']}s [{flag}]",
                flush=True,
            )

    good = [r for r in rows if r.get("p95") is not None]
    if not good:
        print("SUMMARY NO_DATA", flush=True)
        return 2
    p95s = [r["p95"] for r in good]
    eross = [r["eros"] for r in good if r.get("eros") is not None]
    all_run = all(r["run_ok"] for r in rows) and len(good) == len(SEEDS)
    all_eros = all(r["eros_ok"] for r in rows) and len(eross) == len(SEEDS)
    summary = {
        "res": RES,
        "seeds": list(SEEDS),
        "p95_max": max(p95s),
        "p95_min": min(p95s),
        "eros_max": max(eross) if eross else None,
        "eros_min": min(eross) if eross else None,
        "all_run": all_run,
        "all_eros": all_eros,
        "pass": all_run and all_eros,
        "rows": rows,
        "knobs": {
            "SPALL_SCAR_COUNT": tex.SPALL_SCAR_COUNT,
            "SPALL_WIDTH_MIN_FRACTION": tex.SPALL_WIDTH_MIN_FRACTION,
            "SPALL_WIDTH_MAX_FRACTION": tex.SPALL_WIDTH_MAX_FRACTION,
            "JOINT_TRACE_COUNT": tex.JOINT_TRACE_COUNT,
            "SPALL_ABS_NCOL": getattr(tex, "SPALL_ABS_NCOL", None),
            "SPALL_ABS_JOINT_FRAC": getattr(tex, "SPALL_ABS_JOINT_FRAC", None),
            "SPALL_ABS_TILT_BLEND": getattr(tex, "SPALL_ABS_TILT_BLEND", None),
            "SPALL_ABS_CENTRE_JITTER": getattr(tex, "SPALL_ABS_CENTRE_JITTER", None),
            "SPALL_ABS_FORCE_GRID": getattr(tex, "SPALL_ABS_FORCE_GRID", None),
            "SPALL_ABS_PHASE_SCALE": getattr(tex, "SPALL_ABS_PHASE_SCALE", None),
        },
    }
    out = ROOT / "Tools" / "_cline_geo_ship_verify.json"
    out.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(
        f"SUMMARY p95_max={summary['p95_max']:.4f} "
        f"eros_min={summary['eros_min']} "
        f"all_run={all_run} all_eros={all_eros} PASS={summary['pass']}",
        flush=True,
    )
    print(f"JSON {out}", flush=True)
    return 0 if summary["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
