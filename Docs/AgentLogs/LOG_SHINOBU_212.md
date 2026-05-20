# LOG_SHINOBU_212

## 2026-05-20 - Session Start

What was wrong -> No SHINOBU_212 status/rationale/log existed for the batch. Runtime distant-geometry impostor policy was not implemented in first-party source for this agent.
What was done -> Extracted the XML prompt with a CLI command, read task-relevant mandates, created batch state files, and began static archaeology.
Cinematic Cheats used -> Distant massive geometry is being converted to baked albedo/normal/depth cards instead of runtime geometry or capture.
Exact Microseconds saved -> PENDING PROFILER. Static source work only at this point.

## 2026-05-20 - Implementation Report

What was wrong -> Existing `HectonOctahedralImpostorBaker` baked 8 views through `RenderTexture.active`, `ReadPixels`, and managed PNG encode. Runtime shader selected 8 hardcoded views with dither instead of continuous interpolation. GPU payload used sequential `Pack=4` instead of explicit layout.

What was done -> Added explicit DTO/layout validation, Burst angle/mock jobs, compute atlas packing, compute edge dilation, AsyncGPUReadback PNG serialization, BC7 importer setup, generated quad/material/data assets, standalone `Hecton_HLOD_Impostor.shader`, Forge UI Toolkit window, CSV profile parser, SceneView preview, LOD distance scanner, rollback fence validator, reports, and architecture doc.

Cinematic Cheats used -> Geometry collapsed to a two-triangle card; view-dependent parallax faked by 16 atlas frames; fog/depth participation faked from captured depth alpha; mip halo hidden by compute dilation.

Exact Microseconds saved -> Runtime capture cost is 0 because no runtime capture path exists. Runtime geometry savings are PENDING PROFILER. Editor pack microseconds are emitted per bake to `Docs/Reports/IMPOSTOR_BAKE_REPORT.json`.

Verification -> `git diff --check` passed with line-ending warnings only. SHINOBU baker/job/forge scan has no `ReadPixels`, `GetPixels`, `EncodeToPNG`, `Camera.Render`, managed `byte[]`, `File.ReadAllBytes`, `ToArray`, or `MemClear`. Rendering/Environment runtime capture scan returned no hits. Built-in `BillboardRenderer` / tree component scan returned no hits after filtering terrain preserve metadata. Compile/import was not run because CPU stayed at 100%; no `csc`/`dotnet` process was active, but project rule forbids build over 50% CPU.

<SELF_AUDIT agent="SHINOBU_212">
  <runtime_capture_logic>ERADICATED_FROM_SHINOBU_PATH</runtime_capture_logic>
  <atlas_formats>AlbedoDepth RGBA8 PNG import BC7 sRGB; NormalXY RGBA8 PNG import BC7 linear; capture normal depth R16G16B16A16_SFloat</atlas_formats>
  <view_count_default>16</view_count_default>
  <atlas_grid_default>4x4</atlas_grid_default>
  <runtime_geometry>single generated quad mesh, no generated runtime capture MonoBehaviour</runtime_geometry>
  <quality_scaling>continuous _HectonGlobalQualityWeight; profile-driven view count, atlas size, dilation, swap distance</quality_scaling>
  <rollback_state>HLOD impostor matrices excluded from StateRingBuffer; validator writes Docs/Reports/IMPOSTOR_ROLLBACK_FENCE.json</rollback_state>
  <resource_release>RenderTextures released in finally for pre-readback failure and callback Release after AsyncGPUReadback finalization</resource_release>
  <compile_status>PENDING_CPU_GATE</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Ultra Polish Mandate Pass

What was wrong -> SHINOBU implementation still had four integration liabilities: Burst jobs lacked explicit synchronous compile/no-alias proof, the renderer owned a persistent private native upload cache, the Forge window held persistent native editor state, and the audit artifact did not show byte layouts or the full 20-task reconciliation.

What was done -> Added `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` and `[NoAlias]`; removed `_uploadedInstances`; changed fallback/HLOD instance upload to direct `GraphicsBuffer.LockBufferForWrite` with `finally` unlock; removed hot `GlobalRegistry.ScalabilityTier` reads from SHINOBU renderer; converted Forge persistent arrays to cold managed UI caches plus TempJob preview buffers; added `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml`; updated architecture/status/rationale.

Cinematic Cheats used -> Far objects still collapse to a quad with albedo-depth and normal-XY atlases. Runtime avoids original O(V) mesh vertex cost and uses O(1) quad vertices plus O(K) atlas samples, where K is reduced by the continuous quality weight.

Exact Microseconds saved -> One renderer-owned persistent native allocation removed; one CPU NativeArray-to-NativeArray copy removed per direct impostor bind; hot registry tier read removed from Tick. Exact runtime microseconds remain PENDING PROFILER because CPU gate blocked Unity import/build.

Verification -> Self-audit XML parses. Static SHINOBU scan finds no `_uploadedInstances`, `Allocator.Persistent`, `GlobalRegistry.ScalabilityTier`, missing Burst compile flags, `ReadPixels`, `Camera.Render`, managed PNG bytes, `MemClear`, `ClearMemory`, `IntegerSlider`, or `UsePass` in active SHINOBU path. The only `ReadPixels` token remains inside the static validator's forbidden-token scan. CPU sample was 100%; no `dotnet` or `csc` process was active; build was not launched under the >50% CPU rule.

## 2026-05-20 - Runtime Allocation / Shader Cost Pass

What was wrong -> The renderer still contained lazy runtime fallback creation for mesh/material/shader, Unity `Time.*` reads, and a dead integer quality-flags material path. The shader reduced blend weight at low quality but still paid secondary atlas sample cost. Editor bake/profile DTO layout proof did not cover the Forge recipe records.

What was done -> Removed runtime fallback mesh/material creation and `Shader.Find`; renderer now consumes only baked mesh/material/data assets. Replaced `Time.time`/`Time.frameCount` with dispatcher delta accumulation and local tick count. Removed shader/material quality flags. Added explicit 96-byte layouts for `HlodImpostorBakeSettings` and `HlodImpostorProfileRecord`. Added layout validation for those records. Changed shader interpolation to skip secondary atlas samples below the continuous quality gate. Expanded runtime capture scanner to flag `RenderWithShader` in runtime directories.

Cinematic Cheats used -> Same impostor card fake, now cheaper under survival quality: q<0.22 samples only one baked view; q=0.22..0.55 restores parallax with smoothstep; high/ultra spend bandwidth on view interpolation.

Exact Microseconds saved -> Two texture samples skipped per surviving impostor pixel at survival quality; first-draw managed mesh/material allocations removed; Unity global time reads removed from SHINOBU renderer. Exact microseconds remain PENDING PROFILER.

Verification -> Static scans find no `Time.*`, `new Mesh`, `new Material`, `Shader.Find`, `new[]`, runtime fallback fields, `QualityFlags`, `GlobalRegistry.ScalabilityTier`, `Allocator.Persistent`, or private `NativeArray` in the renderer/shader path. `git diff --check` passes with LF/CRLF warnings only. Unity compile/import still not launched under CPU gate.

## 2026-05-20 - Boundary / AUP Link Pass

What was wrong -> SHINOBU renderer still read floating-origin data through `HectonMapMagicVegetationBridge`, a concrete terrain/world bridge outside the impostor baker boundary.

What was done -> Replaced the bridge read with `HectonFloatingOrigin.CurrentTotalOffset`; re-scanned the renderer for MapMagic bridge coupling, global shader mutation, Unity time reads, runtime mesh/material fallback allocation, quality flag residue, persistent NativeArrays, and MPB/material mutation tokens.

Cinematic Cheats used -> Unchanged: far geometry remains a baked two-triangle impostor card; the runtime uses core AUP offset only to place that visual card without 100km float jitter.

Exact Microseconds saved -> No direct frame-time claim. This pass removes compile-wall/coupling risk; runtime savings remain the earlier geometry collapse and low-quality atlas-sample collapse, both PENDING PROFILER.

Verification -> Renderer scan returned no forbidden tokens for `HectonMapMagicVegetationBridge`, `Shader.SetGlobal`, `SetGlobal`, `Time.*`, runtime fallback mesh/material/shader allocation, `QualityFlags`, `GlobalRegistry.ScalabilityTier`, `Allocator.Persistent`, `private NativeArray`, `MaterialPropertyBlock`, `renderer.material`, or `.materials`. Runtime `Rendering` and `Environment` directories returned no `Camera.Render`, `RenderWithShader`, `ReadPixels`, or `EncodeToPNG` matches. Self-audit XML parses. `git diff --check` reports only LF-to-CRLF working-copy warnings. CPU sample remained 100% and compiler process count was 0, so Unity compile/import remains blocked by CPU gate.

## 2026-05-20 - SRP Batcher / Material Churn Pass

What was wrong -> Active and legacy impostor shaders carried renderer-written material uniforms outside `UnityPerMaterial`, and the legacy fallback shader still had `_HectonImpostorQualityFlags`. The renderer also re-entered static atlas metadata refresh every Tick even when material/data were unchanged.

What was done -> Moved `_HectonImpostorTimeSeconds`, `_HectonImpostorFadeOutSeconds`, `_HectonUseVisibleMatrixStream`, and `_GlobalFloatingOffset` into `CBUFFER_START(UnityPerMaterial)` in both impostor shaders; moved `Hecton_Impostor.hlsl` include after that CBUFFER; removed the legacy quality flag; made legacy fallback use the same low-quality sample-collapse path as the active shader; added renderer dirty gates for static atlas data and floating-origin vector writes.

Cinematic Cheats used -> The same 2D impostor card remains the fake. Under low quality the fallback and active shaders both pay one-view atlas sampling, while middle/high/ultra restore two-view parallax through the same smoothstep curve.

Exact Microseconds saved -> Removes steady-state ScriptableObject atlas metadata polling and one non-shift `_GlobalFloatingOffset` material vector write per active SHINOBU renderer. GPU sample savings remain two skipped texture samples per surviving low-quality impostor pixel. Exact profiler numbers remain pending.

Verification -> Scoped scans show both active and legacy shaders declare impostor dynamic fields inside `UnityPerMaterial`; `Hecton_Impostor.hlsl` now declares only StructuredBuffers and consumes those CBUFFER fields from the owning shader; `_HectonImpostorQualityFlags` / `QualityFlags` no longer appear in the SHINOBU renderer or impostor shaders. Self-audit XML parses. Runtime capture scan is empty. Hot DTO/job property scan is empty. `git diff --check` reports only LF-to-CRLF working-copy warnings. CPU sample remained 100% and compiler process count was 0, so Unity compile/import still was not launched under CPU gate.

## 2026-05-20 - Renderer Rebind / Validator Compile-Risk Pass

What was wrong -> `HectonOctahedralImpostorRenderer` could recreate `_argsBuffer` while keeping stale `_argsMesh` / `_lastArgsInstanceCount`, letting a fresh indirect-args buffer skip its first write when mesh and instance count matched. Static atlas binding also allowed missing `HectonOctahedralImpostorData` or atlas textures to proceed into draw with stale material payload. `HlodImpostorStaticValidators.ScanBillboardAssets` had a local `string[] files` declaration shadowing the `StringBuilder files` parameter.

What was done -> Reset args mesh/count cache on args-buffer allocation and release; wrapped the indirect args write lock in `finally`; reset renderer counters/visible-stream/static-payload state during resource release; changed static atlas binding to return a validity bit; `Tick` now returns before draw when baked data or either atlas is absent; renamed the validator local array to `paths`.

Cinematic Cheats used -> No new simulation was added. The same baked-card Dear Lie remains: distant giant geometry stays collapsed to one quad, and the runtime refuses to draw unless the baked albedo-depth and normal-depth payload is present.

Exact Microseconds saved -> No new measured runtime delta. This pass prevents a stale/zero indirect draw after GPU buffer recreation and prevents wrong-atlas overdraw. Existing savings remain geometry collapse to two triangles and low-quality one-view atlas sampling; profiler proof is still pending.

Verification -> Re-extracted the full SHINOBU_212 XML prompt from `CURRENT_BATCH.md`. `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml` parses as XML. Runtime `Rendering` and `Environment` capture scan returns no `Camera.Render`, `RenderWithShader`, `ReadPixels`, or `EncodeToPNG`. Renderer/shader forbidden-token scan returns no `QualityFlags`, `Shader.SetGlobal`, `Time.*`, runtime fallback allocation, `Allocator.Persistent`, private `NativeArray`, MPB/material mutation, or MapMagic bridge residue. Hot DTO/job property scan is empty. `git diff --check` reports only LF-to-CRLF working-copy warnings. CPU sample remained 100% and compiler process count was 0, so Unity compile/import was not launched under the >50% CPU rule.
