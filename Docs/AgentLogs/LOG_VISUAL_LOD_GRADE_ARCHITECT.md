# LOG_VISUAL_LOD_GRADE_ARCHITECT

## 2026-05-15 - Scalability Matrix Designer

What was wrong:
- No `Data/System/Visual_Scalability_Matrix.json` existed for `REND_DYNAMIC_RESOLUTION_ADAPTER`.
- GOD_MODE vs TOASTER budgets were not encoded as machine-readable rows.
- No offline guard existed to fail TOASTER VRAM overflow or weak GOD_MODE visual density.

What was done:
- Created `Data/System/Visual_Scalability_Matrix.json`.
- Defined TOASTER, DECK, PRO, and GOD_MODE tiers.
- GOD_MODE metadata enables 16-tap POM, SSR, screen-space refractions, triple-layer volumetric noise, detail normal maps, micro-detail ORM, and 200000 particles.
- TOASTER metadata caps particles at 5000, disables POM/SSR/Bloom, uses depth fog LUT, and estimates 1560 MiB including driver/OS reserve.
- Added `Tools/VisualStressSim.py`.
- Added `Tools/test_visual_stress_sim.py`.
- Ran `python Tools/VisualStressSim.py --write-report`.
- Wrote `Docs/AgentLogs/VisualStressSim_VISUAL_LOD_GRADE_ARCHITECT.json`.
- Created local temp-index commit from `origin/main` with only owned files.
- Set local branch `visual-lod-grade-architect` to the clean commit recorded in `Docs/AgentLogs/VisualMatrixCommit_VISUAL_LOD_GRADE_ARCHITECT.txt`.
- Wrote fallback handoff patch `Docs/AgentLogs/VisualMatrixCommit_VISUAL_LOD_GRADE_ARCHITECT.patch`.

Cinematic Cheats used:
- Depth-only exponential noir fog with 256x16 LUT on TOASTER.
- Baked AO and packed ORM instead of screen-space AO on TOASTER.
- Static baked or disabled caustics on TOASTER.
- HLOD cards, earlier cull distances, and mip bias instead of higher runtime truth.
- GOD_MODE spends saved budget on visible density, not physical simulation.

Exact Microseconds saved:
- Measured runtime microseconds saved: 0 us. No Unity runtime code changed and no profiler capture was run.
- Planned TOASTER hot-path avoidance estimates: POM disabled = adapter avoids 16 sample taps per hero pixel; SSR disabled = adapter avoids 48 screen-space steps per reflective pixel; volumetric raymarch disabled = adapter avoids 64 half-res steps per ray. These are policy deltas, not measured microseconds.
- Matrix lookup estimates recorded in status: tier row 24 us cold, volumetric scalar copy 35 us adapter-side, particle row 18 us, texture family policy 40 us.

Verification:
- `python -m json.tool Data/System/Visual_Scalability_Matrix.json` passed.
- `python -m py_compile Tools/VisualStressSim.py` passed.
- `python -m py_compile Tools/VisualStressSim.py Tools/test_visual_stress_sim.py` passed.
- `python -m unittest Tools.test_visual_stress_sim` passed: 2 tests.
- `python Tools/VisualStressSim.py --write-report` passed.
- Offline stress results: TOASTER 1560.0 MiB, PRO density 338.4, GOD_MODE density 3078.4, GOD_MODE/PRO ratio 9.097.
- JSON artifacts re-validated after report write.
- `rg --files -g '*.csproj' -g '*.sln'` surfaced no C# project or solution; no C# compile run.
- `git show --stat --oneline ac7e9977d81bcec15e8c3656aaefb64b908e6e78 --` showed only owned files.
- `git format-patch -1 ac7e9977d81bcec15e8c3656aaefb64b908e6e78 --stdout` generated a 105862-byte patch artifact.
- `git diff-tree` rejected contaminated candidate commit `8cb27a224fd65be564783f7eaa1f4706ef32d70d` because it contained 38 files.

Regression model:
- CPU: runtime unchanged; adapter must bake JSON to fixed rows at boot, not parse in Tick.
- GC: runtime unchanged; expected hot path remains 0 B if adapter follows contract.
- Memory: TOASTER offline estimate is under 1.6 GB guard including reserve.
- Cadence: tier switching requires 3 seconds hysteresis and 180 stable restore frames.
- Correctness: GOD_MODE fallbacks are present for render scale, POM, SSR, refraction, volumetric noise, particles, textures, shadows, and post.

Hot path impact:
- None in this pass. Config/tooling only.

Failure modes:
- Runtime adapter could parse JSON in hot path; forbidden by adapter contract.
- Unity import, Play Mode, Memory Profiler, Frame Debugger, RenderDoc, and visual quality are still PENDING VERIFICATION.
- Git remote push blocked: two push attempts to `refs/heads/visual-lod-grade-architect` timed out; `git ls-remote` shows no remote branch.
- Local branch exists: `visual-lod-grade-architect` -> clean commit recorded in `Docs/AgentLogs/VisualMatrixCommit_VISUAL_LOD_GRADE_ARCHITECT.txt`.
- Patch fallback exists for integrator pickup if remote transport remains unavailable.

Why kept:
- The matrix gives the dynamic resolution adapter a hard budget source.
- The stress script makes the requested self-audits repeatable.

Why rejected:
- Direct URP/Quality setting mutation was rejected.
- Physical volumetric truth on TOASTER was rejected.
- Pull/rebase/force-push in the dirty divergent repository was rejected.
- Repeated blind push loops were rejected after two long transport timeouts.
