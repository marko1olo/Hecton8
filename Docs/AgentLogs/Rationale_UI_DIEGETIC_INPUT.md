# Rationale_UI_DIEGETIC_INPUT

STATUS: VERIFIED CONTINUATION
Recovery baseline: restored from `Docs/Archive/Batch002/AgentLogs/Rationale_UI_DIEGETIC_INPUT.md` because live rationale file was missing on 2026-05-12.

## Continuation Decision 1 - Console Spam Root Cause
Problem: Console spam continued after SRP command-buffer fixes. Runtime stack pointed to `RectMask2D`/nested Canvas touching HUD gauge `MaskableGraphic` instances without `CanvasRenderer`.
Solution: Added a cold subtree repair in `SuitHUDV4CanvasOverlay` before root scissor masks and isolated dynamic canvases mutate clipping/sorting state.
Rejected Alternatives: Suppressing logs, disabling `RectMask2D`, or restoring ScreenSpace overlay were rejected because they hide the defect or reintroduce stretched UI.
Scalability potential: Low/Middle/High/Ultra pay only a cold setup repair; steady-state UI remains zero extra frame work.
Hardware Impact: 0 us steady frame. Editor log spam and exception cost removed.

## Continuation Decision 2 - Black Scene Root Cause
Problem: Game View remained near-black after UI was fixed. Measurements showed eclipse state at full occlusion and later showed `HectonUnderwaterVisuals` reading SceneView depth in edit mode, pushing Game View into abyssal lighting.
Solution: Capped eclipse presentation darkness in `HectonCelestialEngine`, raised surface sun/ambient floors, lowered surface fog ceiling, and changed editor depth selection to prefer active Game/Main Camera before SceneView.
Rejected Alternatives: Disabling eclipse, hard-authoring one scene light, or manual Crest material edits were rejected because runtime owners would overwrite them.
Scalability potential: Low keeps a cheap color/float floor. Middle/High/Ultra retain eclipse drama while avoiding full blackout; saved cycles can remain in Crest/sky visuals.
Hardware Impact: No extra render passes or allocations; only scalar/color clamps in existing update paths.

## Continuation Decision 3 - Compile Wall Handling
Problem: Unity compile exposed missing native bridge metadata and later Burst attempted to compile managed `DeflateStream` fallback from `SaveBinaryStorage` compression jobs.
Solution: Imported `HectonNativeBridge.cs` to create `.meta`; next fix must split Burst-safe compression from managed fallback.
Rejected Alternatives: Creating duplicate native bridge types or disabling all save compression was rejected as wider architectural churn.
Scalability potential: Native plugin path stays fast where present; managed fallback must stay main-thread/non-Burst only.
Hardware Impact: Import metadata has 0 us runtime effect; Burst fix should remove editor exception without adding hot-path cost.

## Continuation Decision 4 - Burst Compression Exception Closure
Problem: `SaveBinaryStorage` exposed managed compression fallback inside a Burst-compiled job path, producing editor console exceptions after compile.
Solution: Kept the Burst job as a native probe/result shell and moved managed Deflate/LZ4 fallback completion out of Burst. The compression fallback now runs on the managed completion path only.
Rejected Alternatives: Disabling binary save compression or removing Burst from unrelated save jobs was rejected as wider save-system churn.
Scalability potential: Low/Middle/High/Ultra keep native plugin compression where present; managed fallback is isolated to save completion and does not tax frame rendering.
Hardware Impact: 0 us steady frame. Save-path impact only when native compression is absent.

## Continuation Decision 5 - Crest Surface Blackout
Problem: Above-water Game View was still dark because edit-mode `HectonUnderwaterVisuals` could lose the runtime Main Camera, sample SceneView depth, and leave `Crest.UnderwaterRenderer` enabled on the gameplay camera at surface.
Solution: Editor camera resolution now scans real runtime `MainCamera`; surface editor tick is allowed; Crest underwater pass is disabled at surface; Ocean material receives surface-safe scatter/depth floors through the visual owner.
Rejected Alternatives: Deleting the Crest underwater renderer, hard-authoring the Ocean material, or forcing all cameras to surface mode were rejected because underwater preview and runtime transitions still need the pass.
Scalability potential: Low keeps a cheap branch and material scalar clamp. Middle/High/Ultra keep underwater rendering when actually submerged and spend saved fill-rate on sky/ocean readability.
Hardware Impact: Saves the surface preview from a fullscreen underwater pass. Estimated low-end gain: one avoided fullscreen post pass when camera depth is 0, plus lower editor console churn.

## Continuation Decision 6 - Final Readability Lift
Problem: Eclipse caps fixed the sky but the frame still read too dark after the underwater pass was removed.
Solution: Raised the authored surface post profile through Global Volume `ColorAdjustments` and kept eclipse/sky/ocean scalar floors in code.
Rejected Alternatives: Disabling eclipse, deleting fog, or adding a new fill-light system were rejected; the existing volume profile already owns final exposure.
Scalability potential: Low/Middle use the same post pass already in the renderer. High/Ultra keep the stylized eclipse and can add richer sky/ocean detail without blackout.
Hardware Impact: 0 extra render passes. Scalar post-profile change only; no new allocations.

## Continuation Decision 7 - Surface Water Luminance Floor
Problem: After console and full-screen underwater fixes, the frame was no longer black, but the above-water Crest surface still collapsed into a near-black lower band. Fresh screenshot metrics showed bottom P10 luminance at 0.0423 and water-band P10 at 0.0838.
Solution: Added a code-owned perceived-luminance floor in `HectonUnderwaterVisuals` for surface-only Crest base, shallow, diffuse-shadow, and shallow-shadow colors. The branch only affects the non-underwater shared ocean material and uses existing material scalar/color writes.
Rejected Alternatives: Adding fill lights, adding a URP renderer feature, saving one-off material colors, or raising global exposure further were rejected because they either tax every frame, flatten the noir sky, or get overwritten by the visual owner.
Scalability potential: Low keeps one cheap material color clamp and avoids pure-black water. Middle/High/Ultra can keep richer Crest surface reflections and sky drama because the luminance floor prevents blackout without extra passes.
Hardware Impact: 0 extra render passes, 0 new allocations, no additional GameObject/component churn. Estimated low-end MX350 impact: below measurable noise; scalar color math inside existing owner update only.

## Continuation Decision 8 - Compile Verification Without Live MCP
Problem: Unity MCP stopped reporting live editor instances while Editor.log still contained stale compile failures. Relying on the log would keep chasing already-fixed errors.
Solution: Used Unity's own Roslyn/Bee response files to compile current sources directly, then swept `Assembly-CSharp*` and all `Hecton8*.rsp` assemblies.
Rejected Alternatives: Reporting MCP console clean without a live session, killing Unity, or waiting on stale MCP refresh timeouts was rejected.
Scalability potential: Low/Middle/High/Ultra unaffected; this is verification infrastructure only.
Hardware Impact: 0 us runtime. Saves engineer time by separating stale console state from current compiler truth.

## Continuation Decision 9 - AUP Ref-Parameter Repairs
Problem: `WorldChunkResidencyManager` passed indexer/field expressions into `in` parameters, producing `CS8156` under Unity's C# compiler.
Solution: Copied AUP blit values to locals before calling `DistanceSq` / `ToAbsoluteDouble3`.
Rejected Alternatives: Removing `in` from shared math helpers was rejected because it weakens existing by-ref contracts across streaming code.
Scalability potential: Low/Middle/High/Ultra keep identical chunk prediction math.
Hardware Impact: Stack-local value copies only; no GC, no frame-time regression.

## Continuation Decision 10 - GPU PDA Point Cloud Contract
Problem: `PDAMapTab` drifted between CPU point-cloud fields and GPU append/indirect draw path, causing missing symbols and duplicate layout definitions.
Solution: Restored one 16-byte `SonarPointCloudPoint` for the dormant CPU upload fallback and kept the GPU append buffer as the active draw source.
Rejected Alternatives: Keeping a duplicate 32-byte point struct was rejected because the shader consumes `StructuredBuffer<float4>`.
Scalability potential: Low uses smaller dispatch axis; Middle/High/Ultra keep GPU raymarch detail without CPU per-point draw.
Hardware Impact: 0 steady GC. GPU indirect count avoids CPU vertex-count synchronization.

## Continuation Decision 11 - Audio Rupture Tinnitus Closure
Problem: DSP mix called `RenderEardrumRuptureTinnitusSample` but the implementation was absent.
Solution: Added a Burst-compatible oscillator using existing `RupturePhase`, rupture drive, and maximum gain constants.
Rejected Alternatives: Removing the call would hide an authored damage cue; allocating another DSP lane was rejected.
Scalability potential: Low/Middle/High/Ultra run only a single oscillator when rupture drive is active.
Hardware Impact: Below measurable cost; no allocations, no new buffers.

## Continuation Decision 12 - Acoustic Surface Response Contract
Problem: `AcousticOcclusionUtility` returned `AcousticSurfaceResponse` without the value type definition.
Solution: Restored a readonly stack value containing absorption, transmission, and low-pass cutoff.
Rejected Alternatives: Replacing the response with tuple-like loose floats was rejected because material-response meaning would become less explicit.
Scalability potential: Low/Middle use cheap scalar response; High/Ultra high-tier raycast occlusion keeps material-aware filtering.
Hardware Impact: Stack-only value type; 0 GC.

## Continuation Decision 13 - Encounter Budget Guard
Problem: Spawn loop referenced `ResolveCheapestAllowedCost` to stop when remaining tokens cannot buy any legal class, but the helper was missing.
Solution: Implemented a bounded four-class scan against intensity gates, health suppression, simultaneous caps, and token costs.
Rejected Alternatives: Removing the post-spawn budget guard was rejected because it risks wasted spawn-loop iterations.
Scalability potential: Low/Middle/High/Ultra deterministic scalar scan; no collection allocations.
Hardware Impact: Four static checks per spawn loop, insignificant versus entity spawning.

## Continuation Decision 14 - PDA Span Compile Closure
Problem: Fresh Editor.log showed `PDADataArchaeologyDecryptLabel` failing on `ReadOnlySpan<>` and `Span<>` because the file omitted `using System;`.
Solution: Added the missing namespace import and left the zero-GC `CharBufferPool` + `TMP_Text.SetCharArray` path unchanged.
Rejected Alternatives: Falling back to `TMP_Text.text`, string concatenation, or allocating a temporary managed string was rejected by the UI mandate.
Scalability potential: Low/Middle/High/Ultra keep the same pooled char-buffer text path; high-tier scramble remains gated by quality tier.
Hardware Impact: 0 us runtime delta. Compile-only repair; no new allocations.

## Continuation Decision 15 - Structural Late-Frame Contract Reconciliation
Problem: `SubmarineStructuralGrid` was mid-edit by another lane: the file had an existing `LateFrameTick()` leak-plume render path and late-frame registration, but field/interface state drift temporarily broke compilation.
Solution: Reconciled the implementation by keeping `ILateFrameTickable` and ensuring only one `_registeredLateFrame` field exists.
Rejected Alternatives: Deleting the leak-plume draw path or removing late-frame registration was rejected because that would silently disable an existing visual fake for hull leaks.
Scalability potential: Low keeps `ResolveVisibleBreachCount()` capped by low-memory/math precision; Middle/High/Ultra keep denser leak-plume visuals from the existing GPU path.
Hardware Impact: Restores intended cadence only. No added allocations; draw work remains bounded by active breach count and low-tier cap.

## Continuation Decision 16 - MCP Boundary Kept Explicit
Problem: Unity-MCP resources are reachable but no Unity editor session is attached, so live console and screenshot tools cannot verify runtime view state.
Solution: Used dependency-first Unity Roslyn/Bee response-file compilation for source truth and recorded MCP as blocked instead of overstating verification.
Rejected Alternatives: Restarting or killing Unity without an explicit request, or reporting a clean MCP console from unavailable tools, was rejected.
Scalability potential: Runtime unaffected. Verification remains source-level until MCP reconnects.
Hardware Impact: 0 us runtime. Prevents wasted churn chasing stale Editor.log entries.

## Continuation Decision 17 - Live MCP Bootstrap Console Repairs
Problem: With MCP restored, Play Mode reached runtime and exposed real boot blockers: early tick registration before `SystemDispatcher`, duplicate `InputManager` ownership, recursive player-HUD context resolution, editor-only physics-scene benchmark exceptions, and Editor GC mode exceptions.
Solution: Delayed registration until dispatcher existence is factual, reused the existing native input manager, made HUD notification discovery use its active runtime pointer, bypassed the physics benchmark in Editor Play Mode, skipped GC disable in Editor, and reset stale native sentinel records during editor subsystem reload.
Rejected Alternatives: Log suppression, weakening `GlobalRegistry` dependency-cycle detection, scene YAML surgery, or changing player-build hardware/GC policy were rejected. The fixes are cold boot/editor guard changes only.
Scalability potential: Low/Middle/High/Ultra player builds keep the production bootstrap policy. Editor Play Mode stops failing before surface/HUD verification, enabling actual visual tuning instead of BIOS timeout churn.
Hardware Impact: 0 us steady frame. Bootstrap-only branch checks; no render pass, no per-frame allocation, no hot-path polling.

## Continuation Decision 18 - Surface Readability Scalar Pass
Problem: Surface and near-surface view remained dark/green because multiple presentation owners still compounded noir fog, low abyss color floors, dense fog, vignette, high contrast, desaturation, and cold filters.
Solution: Lifted surface/daylight luminance floors in `HectonUnderwaterVisuals`, reduced blackout/noir density, softened renderer feature fog/vignette scalars, and neutralized High/Default volume contrast/filter/saturation.
Rejected Alternatives: Adding lights, disabling underwater rendering, deleting renderer features, or manually editing scene-only materials was rejected because those create brittle authoring state and extra runtime cost.
Scalability potential: Low keeps cheaper scalar clamps and lower fog density; Middle keeps readable surface mood; High/Ultra can spend saved visibility budget on shafts/particles instead of black crush.
Hardware Impact: 0 new passes, 0 GC. Scalar/material/profile values only; expected low-end gain from less dense post/fog blending pressure is visual readability, not CPU savings.

## Continuation Decision 19 - Diegetic HUD Projection Recovery
Problem: UI/crosshair could become invisible when the dedicated HUD camera/controller was not resolved; projection canvas would remain world-space but return before pose/scale if `projectionCamera` stayed null. The previous visibility contract also overwrote a fallback camera culling mask with only the HUD layer.
Solution: Added a cold resolve fallback through `GlobalRegistry.Player.PlayerCamera` and owned player camera search, then changed the camera mask contract to OR the HUD layer into the target camera instead of replacing the mask. Reticle alpha floors were raised through existing cached color writes.
Rejected Alternatives: Screen-space overlay was rejected because diegetic UI contract requires physical projection. Scene YAML surgery and a second camera spawn were rejected as brittle and potentially costly.
Scalability potential: Low/Middle use the existing player camera and same canvas; High/Ultra can still use dedicated HUD camera when present. No extra render target is forced.
Hardware Impact: Cold resolve only. Runtime pose path is unchanged except an existing bit-mask visibility check. Reticle alpha changes reuse cached `Image.color` writes.

## Continuation Decision 20 - Warning Spam Cadence
Problem: HUD solve over-budget telemetry could publish every 30 frames, creating unnecessary warning noise during active debugging even when the warning is useful.
Solution: Raised cooldown to 300 frames so warnings remain actionable without flooding the console.
Rejected Alternatives: Removing the warning or suppressing console output globally was rejected because real HUD budget regressions still need visibility.
Scalability potential: Low/Middle/High/Ultra all keep diagnostics with lower publication frequency.
Hardware Impact: Fewer event publications; no allocation added.

## Continuation Decision 21 - MapMagic Preview Shader Warning Repair
Problem: Fresh asset worker logs repeatedly report `Shader Unsupported: MapMagic/TerrainPreview* - All subshaders removed`, and prior imports also reported mixed line endings in those shader files. This is editor-only but creates console noise that hides real gameplay/runtime faults.
Solution: Added URP-compatible lightweight preview fallback passes with `RenderPipeline=UniversalPipeline` and `LightMode=UniversalForward`, then normalized both files to CRLF without BOM.
Rejected Alternatives: Deleting MapMagic, disabling terrain preview, or suppressing shader warnings globally was rejected. Those would either damage tooling or hide real shader faults.
Scalability potential: Runtime Low/Middle/High/Ultra unaffected; this only gives the editor importer a cheap fallback path for preview materials.
Hardware Impact: 0 us player runtime. Editor preview fallback is a single texture sample and no terrain lighting path when the full preview shader is stripped.

## Continuation Decision 22 - Runtime Reference Warning One-Shot
Problem: `HectonUnderwaterVisuals` could log unresolved player/main camera or sun transform every five seconds in development/editor if a runtime reference stayed missing, creating noise without adding new information.
Solution: Kept the diagnostic but added a byte mask so each missing reference logs once and resets only after the reference resolves.
Rejected Alternatives: Removing diagnostics was rejected because missing camera/sun references are real visual blockers. Keeping the five-second repeat was rejected because it buries actionable console entries.
Scalability potential: Low/Middle/High/Ultra player runtime unaffected in release; dev/editor diagnostics stay precise.
Hardware Impact: One byte mask and three bit checks on an editor/development diagnostic cadence only; 0 GC.

## Continuation Decision 23 - Noir Global Reset Floor
Problem: `ResetNoirResolveGlobals()` still wrote the old near-black abyss floor and high noir exponent/density values, so domain reload, disable, or teardown could leave global shader state darker than the repaired runtime values.
Solution: Updated the reset path to the readable abyss floor, lower noir exponent, and lower fog scattering coefficient used by the active runtime path.
Rejected Alternatives: Removing the reset was rejected because stale globals must still be cleaned. Leaving old black-crush defaults was rejected because it can reintroduce the exact surface darkness being fixed.
Scalability potential: Low/Middle/High/Ultra all get safe fallback shader globals if the visual owner is disabled or reloaded.
Hardware Impact: Disable/reload scalar writes only; 0 frame cost, 0 GC.
