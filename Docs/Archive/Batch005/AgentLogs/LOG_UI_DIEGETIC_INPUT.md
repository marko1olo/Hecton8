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

## 2026-05-12 Continuation - Console Compile Cleanup / Honest R&D
What was wrong:
Unity MCP lost the live editor session (`mcpforunity://instances` returned `instance_count=0`) while Editor.log still showed stale compile failures. Current source failures were real but different: AUP `in` argument misuse in chunk residency, missing encounter budget helper, broken PDA point-cloud type/field contract after mixed CPU/GPU paths, missing acoustic surface response value type, missing rupture tinnitus renderer, and transient warnings in the Leviathan tentacle solver.

What was done:
Used Unity's own Roslyn/Bee response files for evidence instead of trusting stale console text. Fixed `WorldChunkResidencyManager` by copying AUP blits to locals before by-ref math calls. Replaced obsolete editor-only `FindObjectsByType` usage in `HectonUnderwaterVisuals`. Restored `ResolveCheapestAllowedCost` in `EncounterDirector`. Reconciled `PDAMapTab` to one 16-byte sonar point struct plus persistent fallback fields while keeping GPU append/indirect rendering primary. Restored `AcousticSurfaceResponse`. Added Burst-compatible eardrum rupture tinnitus synthesis. Reconnected Leviathan high-tier constraint iteration authoring and retained radius material caches now used by `BindRadiusReferenceToMaterial`.

Cinematic cheats used:
PDA point cloud remains GPU append/indirect instead of CPU point rendering. Rupture tinnitus is a single oscillator, not a sample/asset pipeline. Encounter budget guard is four scalar class checks, not a container scan. Surface material fallback stays editor-only.

Exact microseconds saved / spent:
AUP repair: 0 GC, stack copies only. PDA point-cloud path: avoids CPU vertex-count synchronization; indirect count stays on GPU. Rupture tinnitus: one oscillator only while active, below measurable frame/audio budget. Leviathan LOD: low/MX350 remains 1 constraint iteration; high/ultra authorable 1-3 iterations. Obsolete editor fallback: no runtime frame cost.

Verification:
Direct Unity Roslyn/Bee sweep completed with exit code 0 and no compiler output for `Assembly-CSharp*` plus every `Hecton8*.rsp`: `Assembly-CSharp`, `Assembly-CSharp-Editor`, `Assembly-CSharp-Editor-firstpass`, `Assembly-CSharp-firstpass`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Core`, `Hecton8.EditModeTests`, `Hecton8.Editor`, `Hecton8.Input.Generated`, `Hecton8.Input`, `Hecton8.Optimization.Editor`, `Hecton8.PlayModeTests`, `Hecton8.Plugins`, `Hecton8.SpaceEngine098Terrain`, `Hecton8.UI.Editor`, `Hecton8.World.Contracts`. MCP console/screenshot verification is blocked by Unity-MCP session loss, not by source compile errors.

## 2026-05-12 Continuation - Compile Spam Sweep / Honest R&D
What was wrong:
Fresh `Editor.log` contained a current UI compile failure in `PDADataArchaeologyDecryptLabel`: `ReadOnlySpan<>` and `Span<>` were unresolved because `System` was not imported. A later source sweep exposed a transient `SubmarineStructuralGrid` contract drift while another lane was editing leak-plume damage-control visuals: late-frame registration and rendering existed, but duplicate/missing `_registeredLateFrame` state could break compile depending on file timing. Unity-MCP remains detached (`instance_count=0`, `no_unity_session`), so live console/screenshot tools cannot be used honestly.

What was done:
Added `using System;` to the PDA decrypt label and preserved the existing `CharBufferPool` + `TMP_Text.SetCharArray` zero-GC render path. Reconciled `SubmarineStructuralGrid` to one `_registeredLateFrame` field while keeping its existing `ILateFrameTickable` leak-plume render path. Audited `PDAMapTab` point-cloud code: no dead CPU point-cloud fallback or `if(false)` branch remains in the current file; the active path is GPU append plus indirect draw.

Cinematic cheats used:
PDA text still uses pooled char-buffer updates, not string UI. Hull leak visuals remain a bounded GPU plume fake driven by breach records instead of physical water simulation. Low-tier visibility remains capped by existing `ResolveVisibleBreachCount()` logic.

Exact microseconds saved / spent:
PDA namespace fix: 0 us runtime, compile-only. Structural contract reconciliation: restores existing cadence, no new hot-path allocation. Avoided a `.text` fallback and avoided deleting the visual fake, so no extra Canvas rebuild or physical leak simulation cost was introduced.

Verification:
Dependency-first direct Unity Roslyn/Bee sweep passed with exit code 0 for: `Hecton8.Bootstrap.Contracts`, `Hecton8.World.Contracts`, `Hecton8.Core`, `Hecton8.Input.Generated`, `Hecton8.Input`, `Hecton8.Plugins`, `Hecton8.SpaceEngine098Terrain`, `Assembly-CSharp-firstpass`, `Assembly-CSharp`, `Assembly-CSharp-Editor-firstpass`, `Hecton8.Editor`, `Hecton8.UI.Editor`, `Hecton8.Optimization.Editor`, `Assembly-CSharp-Editor`, `Hecton8.EditModeTests`, `Hecton8.PlayModeTests`. `git diff --check` passed for touched files, with only LF-to-CRLF warnings on dirty files. MCP console/screenshot verification remains blocked by missing Unity session.
