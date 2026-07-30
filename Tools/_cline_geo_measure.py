# -*- coding: utf-8 -*-
"""Measure geology bedding p95 offline (no Blender GUI). Do not commit."""
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

OUT = ROOT / "Tools" / "_cline_geo_measure_out.txt"


def log(msg: str, buf: list[str]) -> None:
    print(msg, flush=True)
    buf.append(msg)


def main() -> int:
    buf: list[str] = []
    try:
        import numpy as np  # noqa: F401
        from h8forge import law
        from h8forge import texture as tex

        log(f"SPALL_SCAR_COUNT={tex.SPALL_SCAR_COUNT}", buf)
        log(
            f"SPALL_WIDTH={tex.SPALL_WIDTH_MIN_FRACTION}-{tex.SPALL_WIDTH_MAX_FRACTION}",
            buf,
        )
        log(f"GEOLOGY_LAMINA_MAX_RUN_FRACTION={law.GEOLOGY_LAMINA_MAX_RUN_FRACTION}", buf)
        log(f"GEOLOGY_MIN_EROSIONAL_COVERAGE={law.GEOLOGY_MIN_EROSIONAL_COVERAGE}", buf)

        # Find GeologyTextureSpec + bake entry
        names = [n for n in dir(tex) if "Geology" in n or "geology" in n]
        log(f"texture symbols with Geology: {names}", buf)

        Spec = getattr(tex, "GeologyTextureSpec", None)
        bake = None
        for cand in (
            "bake_geology_texture",
            "generate_geology_texture",
            "build_geology_texture",
            "render_geology_texture",
            "geology_texture",
        ):
            if hasattr(tex, cand):
                bake = getattr(tex, cand)
                log(f"found bake fn: {cand}", buf)
                break

        # Also search callables mentioning structural
        if bake is None:
            for n in dir(tex):
                obj = getattr(tex, n)
                if callable(obj) and n.startswith("build") or n.startswith("bake") or n.startswith("generate"):
                    if "geo" in n.lower() or "lamina" in n.lower() or "texture" in n.lower():
                        log(f"candidate {n}", buf)

        # Read source for class + public API near end of file
        src = (ROOT / "Tools/Blender/h8forge/texture.py").read_text(encoding="utf-8")
        import re

        for m in re.finditer(r"^def (\w+).*", src, re.M):
            name = m.group(1)
            if any(k in name.lower() for k in ("geo", "lamina", "struct", "bake", "build_height", "compose")):
                log(f"def {name} @ line ~{src[:m.start()].count(chr(10))+1}", buf)

        if Spec is None:
            log("NO GeologyTextureSpec", buf)
            OUT.write_text("\n".join(buf), encoding="utf-8")
            return 2

        # Inspect Spec fields
        import dataclasses

        if dataclasses.is_dataclass(Spec):
            log(f"Spec fields: {[f.name for f in dataclasses.fields(Spec)]}", buf)

        # Prefer a lightweight resolution for probe, then ship 2048 if time allows
        resolutions = [512]
        seeds = [0, 1, 2, 7, 13]
        results = []

        # Try to find the full pipeline function that returns structuralExtent
        # Looking at report section ~1687 structuralExtent is inside a larger builder.
        # Search for function that contains measure_structural_extent call.
        for m in re.finditer(r"^def (\w+)\(", src, re.M):
            start = m.start()
            # next def
            nxt = re.search(r"^def \w+\(", src[m.end() :], re.M)
            body = src[m.start() : m.end() + (nxt.start() if nxt else 5000)]
            if "measure_structural_extent" in body and m.group(1) != "measure_structural_extent":
                log(f"caller of measure_structural_extent: {m.group(1)}", buf)
                bake = getattr(tex, m.group(1), bake)

        log(f"using bake={getattr(bake, '__name__', bake)}", buf)

        for res in resolutions:
            for seed in seeds:
                t0 = time.time()
                try:
                    # Try common construction patterns
                    spec = None
                    errors = []
                    for kwargs in (
                        {"seed": seed, "resolution": res},
                        {"seed": seed, "resolution_px": res},
                        {"seed": seed},
                    ):
                        try:
                            spec = Spec(**kwargs)
                            break
                        except TypeError as e:
                            errors.append(str(e))
                    if spec is None:
                        # try no-arg + assign
                        try:
                            spec = Spec()
                            for attr, val in (("seed", seed), ("resolution", res), ("resolution_px", res)):
                                if hasattr(spec, attr):
                                    setattr(spec, attr, val)
                        except Exception as e:
                            errors.append(repr(e))
                    if spec is None:
                        log(f"spec construct fail seed={seed}: {errors}", buf)
                        continue

                    # resolved_resolution override if method exists
                    if hasattr(spec, "resolved_resolution"):
                        # monkey if needed
                        pass

                    # Call bake
                    if bake is None:
                        # Manual path: joints + lamina stack + measure
                        joint_fn = getattr(tex, "build_joint_traces", None) or getattr(
                            tex, "_build_joint_traces", None
                        )
                        # fall through to build_lamina_stack
                        joint_traces = None
                        joint = None
                        if joint_fn:
                            try:
                                outj = joint_fn(spec)
                                if isinstance(outj, tuple):
                                    joint_traces = outj[0] if isinstance(outj[0], list) else outj
                                    joint = outj[1] if len(outj) > 1 else None
                                else:
                                    joint_traces = outj
                            except Exception as e:
                                log(f"joint_fn err: {e}", buf)
                        lamina = tex.build_lamina_stack(spec, joint_traces=joint_traces)
                        if joint is None:
                            joint = getattr(lamina, "spall", None) * 0.0  # zeros
                            # try build joint field
                            for jn in ("build_joint_field", "_build_joints", "sample_joints"):
                                if hasattr(tex, jn):
                                    try:
                                        joint = getattr(tex, jn)(spec, joint_traces)
                                        break
                                    except Exception:
                                        pass
                        if joint is None:
                            import numpy as np

                            joint = np.zeros_like(lamina.spall)
                        extent = tex.measure_structural_extent(lamina, joint, spec.tile_m)
                        payload = {"structuralExtent": extent, "spallCoverage": float((lamina.spall > 0.5).mean())}
                    else:
                        out = bake(spec)
                        if isinstance(out, dict):
                            payload = out
                        elif hasattr(out, "keys"):
                            payload = dict(out)
                        else:
                            payload = {"raw_type": type(out).__name__}
                            # try attributes
                            for a in ("structuralExtent", "manifest", "report", "meta"):
                                if hasattr(out, a):
                                    payload[a] = getattr(out, a)

                    se = payload.get("structuralExtent") or payload.get("manifest", {}).get(
                        "structuralExtent"
                    )
                    if se is None and isinstance(payload.get("report"), dict):
                        se = payload["report"].get("structuralExtent")

                    dt = time.time() - t0
                    row = {
                        "seed": seed,
                        "res": res,
                        "dt": round(dt, 3),
                        "structuralExtent": se,
                        "spallCoverage": payload.get("spallCoverage"),
                        "runBudgetMet": (se or {}).get("runBudgetMet") if isinstance(se, dict) else None,
                        "p95": ((se or {}).get("longestIntactRunFraction") or {}).get("p95")
                        if isinstance(se, dict)
                        else None,
                        "erosional": (se or {}).get("erosionalCoverage") if isinstance(se, dict) else None,
                    }
                    results.append(row)
                    log(json.dumps(row, ensure_ascii=False), buf)
                except Exception:
                    log(f"FAIL seed={seed} res={res}: {traceback.format_exc()}", buf)

        # Summary
        p95s = [r["p95"] for r in results if r.get("p95") is not None]
        if p95s:
            log(
                f"SUMMARY n={len(p95s)} p95_min={min(p95s):.4f} p95_max={max(p95s):.4f} "
                f"p95_mean={sum(p95s)/len(p95s):.4f} budget={law.GEOLOGY_LAMINA_MAX_RUN_FRACTION} "
                f"all_met={all(r.get('runBudgetMet') for r in results if r.get('p95') is not None)}",
                buf,
            )
        else:
            log("SUMMARY no p95 results", buf)

        OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
        return 0
    except Exception:
        buf.append(traceback.format_exc())
        OUT.write_text("\n".join(buf) + "\n", encoding="utf-8")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
