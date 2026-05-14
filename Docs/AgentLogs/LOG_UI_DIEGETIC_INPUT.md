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

## 2026-05-13 Continuation - Surface/HUD Readability
What was wrong:
The surface frame was still being crushed by stacked noir/fog/post values, and the diegetic HUD could vanish when its dedicated projection camera did not resolve. MCP server is running, but Unity session discovery currently reports `instance_count=0`, so live screenshots/console reads cannot be claimed.

What was done:
Lifted underwater/surface readability floors, reduced abyss/noir fog density, softened vignette/damage post, neutralized High/Default volume contrast/desaturation/cold filter, added player-camera fallback for diegetic HUD projection, preserved gameplay camera masks while adding the HUD layer, raised reticle alpha floors, and throttled HUD solve warnings.

Cinematic Cheats used:
Scalar fog/luminance floors and color clamps instead of extra lights or physical volumetrics. Existing diegetic canvas projection reused instead of new render targets. Reticle visibility improved by cached alpha values only.

Exact Microseconds saved:
No new hot-path systems. Expected runtime delta is 0 us for lighting/profile scalar changes; HUD fallback is cold resolve only; warning cooldown reduces diagnostic event frequency by roughly 10x during persistent over-budget states.

Verification:
`Hecton8.Core.rsp` compiled with exit code 0 after edits. `git diff --check` passed with only existing LF-to-CRLF warnings on dirty scripts. MCP live proof remains blocked by no active Unity session.

## 2026-05-13 Continuation - Active Warning Sweep
What was wrong:
Asset worker logs showed repeated `MapMagic/TerrainPreviewURP` and `MapMagic/TerrainPreview` unsupported-shader warnings, plus mixed line-ending warnings for the same files. These are editor preview assets, but the repetition pollutes the console and makes real errors harder to see.

What was done:
Added URP-tagged lightweight fallback passes to both preview shaders and normalized both shader files to CRLF/no-BOM. Re-ran `Hecton8.Core.rsp`; compile still exits 0.

Cinematic Cheats used:
Editor fallback is a flat texture sample for preview only, not a terrain lighting simulation. Runtime terrain/water presentation remains untouched.

Exact Microseconds saved:
0 us player runtime. Editor-only fallback avoids shader importer warning churn; no runtime render pass added.

Verification:
Line-ending audit reports `LoneLF=0` and `HasBOM=False` for both shaders. `git diff --check` is clean for the shader files. Direct Unity batch import could not be used because the project is already open; MCP still has no active Unity session.

## 2026-05-13 Continuation - Runtime Warning Hygiene
What was wrong:
`HectonUnderwaterVisuals` unresolved camera/sun diagnostics could repeat every five seconds in editor/development builds while a reference stayed missing.

What was done:
Converted those diagnostics to per-reference one-shot warnings with reset after recovery. Re-ran `Hecton8.Core.rsp`; compile exits 0.

Cinematic Cheats used:
None. Diagnostic cadence only.

Exact Microseconds saved:
No player-runtime release cost. Editor/development path now avoids repeated log work and stacktrace capture for the same unresolved reference.

Verification:
`Hecton8.Core.rsp` direct compile passed after the patch.

## 2026-05-13 Continuation - Ordered Compile Sweep
What was wrong:
The Unity log still contains stale `GlobalDataVault` compile failures and MCP websocket warnings from earlier broken states. Current source truth needed a dependency-ordered compile pass.

What was done:
Ran Unity Roslyn/Bee response-file sweep for Bootstrap.Contracts, World.Contracts, Core, Input generated/runtime, Plugins, SpaceEngineTerrain, Assembly-CSharp firstpass/runtime, editor assemblies, UI editor, optimization editor, and Hecton8 edit/play mode tests.

Cinematic Cheats used:
None. Verification only.

Exact Microseconds saved:
0 us runtime. Prevents wasted work chasing stale log entries.

Verification:
All listed response files compiled with exit code 0; sweep ended with `COMPILE_SWEEP_OK`.

## 2026-05-13 Continuation - Noir Reset Guard
What was wrong:
`ResetNoirResolveGlobals()` still restored old black-crush abyss/noir values, meaning reload/disable paths could re-darken the global resolve state even after runtime readability fixes.

What was done:
Changed the reset globals to the readable abyss floor, reduced noir exponent, and lower fog scattering coefficient. Re-ran `Hecton8.Core.rsp`; compile exits 0.

Cinematic Cheats used:
Scalar shader-global defaults only; no new lights, no new render passes.

Exact Microseconds saved:
0 us frame cost. The change prevents stale dark global state after teardown/reload.

Verification:
`Hecton8.Core.rsp` direct compile passed after the patch.

## 2026-05-14 Continuation - Build/Asmdef Medic Bottom Ledger
What was wrong:
Current Unity compile/import was blocked by Core.Memory missing compaction symbols, diegetic tooltip definite assignment, incomplete split-asmdef references, prologue AUP namespace resolution, Gameplay.Loot contract exposure through Core.Memory, QA editor `Environment.NewLine` namespace collision, and `Hecton_DryZoneLit.shader` mixed line endings. MCP live console/screenshot still cannot connect to port 8088.

What was done:
Repaired compaction constants/flags and bounded memmove path in `GlobalDataVault`, initialized tooltip glyph index, exposed `WorldRuntimeReferenceUtility`, fully-qualified prologue AUP calls, added explicit asmdef references, added zero-runtime empty-assembly markers, replaced obsolete `GetInstanceID()` identity sources with `GetEntityId()`/`EntityId.ToULong()`, fixed QA editor newline qualification, normalized `Hecton_DryZoneLit.shader` to CRLF, and recorded MCP as blocked instead of fabricating proof.

Cinematic Cheats used:
None added. This pass is compile graph, import hygiene, scalar identity cleanup, and bounded memory-copy restoration only.

Exact Microseconds saved:
0 us player-frame cost added. Low-end i3/MX350 benefit is cleaner import/boot and no managed allocation churn from the repaired paths; high-end behavior unchanged.

Verification:
Temp-output Roslyn chain passed through Core.Memory, Logistics.Grid.*, World.Contracts, Core, World.Outposts, Gameplay.Loot.*, Audio.Prologue, Graphics.DRS, UI.VR.*, VFX.Debris, World.Economy, World.Streaming, Assembly-CSharp, Hecton8.Editor, and QA.Headless.Editor. Unity batchmode log `Logs/Codex_UI_DIEGETIC_INPUT_CompileCheck_20260514_062030.log` exits return code 0 with no `error CS`, `warning CS`, shader errors/warnings, Tundra failure, or mixed-line-ending warning. Remaining log entries are environment/package level: Unity licensing token, native extension probes, and MCP shutdown message.

## 2026-05-14 Continuation - UI Diagnostic Layout Hygiene
What was wrong:
The previous report pass duplicated the build-medic ledger. Static UI audit also found a rare diagnostic-tooltip correctness issue: input scheme/service changes could rebuild diagnostics through the interaction-prompt path and attach a stray binding icon.

What was done:
Removed duplicate report blocks, normalized touched files to CRLF, and gated input-scheme/service layout rebuilds so only non-diagnostic look-target prompts get binding-icon rebuilds.

Cinematic Cheats used:
None. UI correctness and artifact hygiene only.

Exact Microseconds saved:
0 us steady frame. The new branch runs only on input scheme/service changes; no allocations, no new render pass.

Verification:
`git diff --check` returns no output. Temp-output Roslyn passed for `Assembly-CSharp` and `Hecton8.Editor`. Unity batchmode log `Logs/Codex_UI_DIEGETIC_INPUT_CompileCheck_20260514_063805.log` exits return code 0 with no CS or shader failures. Remaining batch log noise is Unity licensing and MCP shutdown state, not project compile errors.

## 2026-05-14 Continuation - Live Editor Burst Probe
What was wrong:
Live Editor startup exposed a Burst ABI failure that C# compile/batchmode did not catch: `LootMagnetSignalEvent` calculated struct-layout size 82 while explicit size was 80.

What was done:
Changed `LootMagnetSignalEvent.Quantity` from `ushort` to `uint`, keeping the payload at 80 bytes while removing ambiguous 2-byte padding from the Burst NativeArray element layout. Started a fresh live Editor after the fix and closed it after verification.

Cinematic Cheats used:
None. This is a Burst payload ABI repair that preserves the jobified loot magnet path.

Exact Microseconds saved:
0 us added. Prevents Burst failure/fallback and keeps loot magnet pull/acquisition on the compiled job path; no payload stride increase.

Verification:
Roslyn temp-output compile passed for `Hecton8.Gameplay.Loot.Contracts`, `Hecton8.Gameplay.Loot`, `Assembly-CSharp`, and `Hecton8.Editor`. Fresh live Editor log `Logs/Codex_UI_DIEGETIC_INPUT_LiveEditorProbe_20260514_065227.log` reaches scene load; grep finds no Burst error, CS error/warning, shader error/warning, or Tundra failure. Batchmode log `Logs/Codex_UI_DIEGETIC_INPUT_CompileCheck_20260514_065653.log` exits return code 0 after the expected Bee rerun. Port 8088 remains closed, so MCP screenshot/console is still blocked by MCP setup.
