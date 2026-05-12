# Rationale_UI_DIEGETIC_INPUT

STATUS: VERIFIED CONTINUATION

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
