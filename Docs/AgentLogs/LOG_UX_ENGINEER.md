# UX_ENGINEER Log

## HARDWARE_ADAPTIVE_UI_BAKER
What was wrong: Pending analysis. Batch requires UI scaler design, icon baking, readability testing, texture-sample audit, and rationale logging.
What was done: Batch prompt extracted; relevant mandates loaded; fresh status and rationale files created because no active UX_ENGINEER state existed.
Cinematic Cheats used: Pending.
Exact Microseconds saved: PENDING VERIFICATION. No profiler artifact exists yet; any figures in this log remain static estimates unless marked otherwise.

## HARDWARE_ADAPTIVE_UI_BAKER - Completion Pass
What was wrong: UI scaling had no batch-scoped 5-bucket TMP-SDF matrix, no repeatable `O2 LOW` blur readability test, no icon pixel-snap baker, and several owned UI shaders exceeded the two-sample element budget.
What was done: Added `Docs/Design/HardwareAdaptiveUIScaler.md` and `.json`; patched `WorldSpaceTMPSharpnessController` to apply hardware-adaptive `_WeightNormal`, `_WeightBold`, `_FaceDilate`, and `_OutlineSoftness`; added `Tools/IconBaker.py`; added `Tools/UX/ui_readability_test.py`; added `Tools/UX/ui_shader_sample_audit.py`; reduced over-budget samples in curved HUD, tool screen, acoustic radar, and diegetic panel shaders.
Cinematic Cheats used: Multi-tap chromatic aberration became a channel-weight fake; acoustic neighbor smoothing became angular math widening; tool screen multi-texture composite became one RT sample plus math overlays; GOD_MODE blur/chroma is assigned to gated HUD RT/post instead of per-widget samples.
Exact Microseconds saved: Static estimates only. TMP atlas-swap avoidance: 6-12 us per SDF profile update. FOV layout contract avoiding rebuilds: 20-60 us per FOV rebucket. Icon pre-bake: 5-25 us per icon draw. UI shader sample reductions: 0.05-0.20 ms GPU in HUD-heavy frames. STATUS: PENDING PROFILER / FRAME DEBUGGER.
Verification: Python compile PASS; readability PASS for all 5 buckets; IconBaker self-test PASS; shader sample audit PASS for 13 owned UI shaders; JSON validation PASS. Unity import, Unity Console, Play Mode, GCMonitor, Frame Debugger, and player build were not run.

Polish: `<POLISH_MANDATE>` tag was absent from `Docs/Tasks/CURRENT_BATCH.md`. Local anti-bloat checks still ran. No new runtime dependency, package, singleton, event ID, or public API change was introduced.

Continuation: Added `Tools/UX/test_hardware_adaptive_ui.py`. Test result: PASS, 5 tests. Coverage: spec identity, C# SDF bucket parity, readability report, UI shader sample cap, IconBaker output dimensions and 32px alpha snapping. Unity executable for `6000.4.1f1` was not found in normal paths; Unity verification remains PENDING.

Final hardening: Reran `python -m unittest Tools.UX.test_hardware_adaptive_ui -v` with `PYTHONDONTWRITEBYTECODE=1` -> PASS, 5 tests. Reran readability and shader sample reports -> PASS. Reran `git diff --check` on touched files -> PASS with only line-ending warnings. Confirmed no generated pycache remains for `IconBaker`, `ui_readability_test`, `ui_shader_sample_audit`, or `test_hardware_adaptive_ui`.
