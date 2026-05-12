# LOG_UI_DIEGETIC_INPUT

## 2026-05-11 Continuation - Console, Crest Surface Blackout, Diegetic UI
What was wrong:
Console spam came from HUD graphics missing `CanvasRenderer`, stale native bridge metadata, and a Burst job attempting managed compression fallback. Visual blackout came from full eclipse floors being too low, edit-mode depth resolving from SceneView, and Crest `UnderwaterRenderer` staying enabled on the gameplay camera while the real Main Camera was above water.

What was done:
Repaired `SuitHUDV4CanvasOverlay` graphic subtrees before mask/canvas operations. Moved `SaveBinaryStorage` managed compression fallback out of Burst. Raised surface readability floors in `HectonCelestialEngine` and `HectonUnderwaterVisuals`. Added editor Main Camera resolution, editor Ocean material fallback, surface tick, and surface disable for Crest underwater pass. Lifted Global Volume `ColorAdjustments` for readable surface exposure.

Cinematic cheats used:
Eclipse darkness is now capped instead of physically blacking out the scene. Surface water uses scalar color/depth floors instead of extra simulation. Editor surface preview disables the underwater fullscreen pass instead of rendering an unnecessary water-volume effect.

Exact microseconds saved:
HUD spam repair: 0 us steady frame, removes unbounded exception overhead. Native bridge import: 0 us runtime. Burst compression fix: 0 us frame, save-path only. Crest surface pass disable: avoids one fullscreen underwater pass at surface; estimated 250-900 us on MX350-class editor preview depending resolution. Exposure lift: 0 extra us, existing post stack only.

Verification:
Unity console after final compile/save/screenshot: 0 errors, 0 warnings. Final MCP screenshot: `Assets/Screenshots/codex_lighting_probe_final_console_clean.png`.

## 2026-05-12 Continuation - Honest R&D / Surface Water Readability
What was wrong:
Console stayed clean, but fresh Game View proof showed the surface frame still had a near-black lower water band. Verified this was not the old UI stretch, not the old console spam, and not the Crest full-screen underwater pass: Main Camera `Crest.UnderwaterRenderer` was disabled at surface and Ocean `_Underwater=0`. Baseline screenshot `Assets/Screenshots/codex_rd_baseline_20260512.png`: bottom P10 luminance 0.0423, water-band P10 0.0838.

What was done:
Added a surface-only perceived-luminance floor in `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`. The fix raises Crest surface base/shallow/shadow colors through the existing material owner instead of adding lights, post passes, or one-off material edits. Diagnostic probes proved bright material values affect the visible water path; final code-owned candidate uses a restrained floor.

Cinematic cheats used:
Cheap noir readability fake: material luminance floor tied to existing Crest surface color writes. No physical lighting simulation, no new render feature, no extra camera, no runtime material clone.

Exact microseconds saved / spent:
Spent: estimated under 1 us per owner material update on low-end CPU; 0 extra GPU passes. Saved: avoided a new full-screen light/post feature that would cost roughly 80-400 us on MX350-class hardware depending on resolution.

Verification:
Unity compile returned idle. Unity console after the change: 0 errors / 0 warnings / 0 logs. Final screenshot: `Assets/Screenshots/codex_rd_surface_luminance_floor_final_20260512.png`. Final metric: bottom P10 0.0642, water-band P10 0.0976.
