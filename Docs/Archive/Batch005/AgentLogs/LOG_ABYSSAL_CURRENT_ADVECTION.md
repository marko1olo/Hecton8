# LOG_ABYSSAL_CURRENT_ADVECTION

## 2026-05-13 - Compute Advection
What was wrong: Marine snow had flow behavior, but debris, exhale bubbles, and silt had no unified advection owner. Collision pressure would have been pushed toward Rigidbody/Collider truth if left unchecked.

What was done: Added `Hecton_FluidAdvection.compute` with one 64-wide kernel for Silt/Bubble/Debris read-write buffers. Extended `HectonFluidEngine` with fixed graphics buffers, bounded debris/bubble caps, AUP shift intake, debris signal drain, exhale bubble queueing, SDF payload binding, active particle telemetry, 300-frame black-box ring, binary dump path, and RenderGraph payload export. Added `HectonFluidAdvectionRenderFeature` to record dispatch through URP RenderGraph and unbind compute resources after dispatch. Bridged underwater exhale into the fluid queue through `GlobalRegistry.Fluid`.

Cinematic Cheats used: SDF texture density replaces physical collision. Low/MX350 tier disables bubble/debris flow sampling and uses linear buoyancy vectors. Bubbles pop by flag/life zero on solid density. Debris rests by flag/zero velocity. Spawn spread uses hash jitter, not a simulation.

Exact microseconds saved: 40-180 us/frame estimated on i3/MX350 by skipping up to 3000 bubble/debris 3D flow samples at caps. 100-400 us/frame estimated avoided when particles overlap cave surfaces by sampling `VoxelSdfTexture3D` instead of collider queries. 15-45 us/frame CPU avoided by fixed buffers and no managed list churn in the hot path. One structured-flow fallback scalar divide replaced by `rcp` multiply per fallback sample.

Verification: Status remains PENDING VERIFICATION. `HectonFluidAdvectionRenderFeature.cs` validates 0 errors/0 warnings. `HectonUnderwaterVisuals.cs` validates 0 errors with 2 pre-existing broad warnings. `HectonFluidEngine.cs` Unity validator timed out on the large file after final polish; prior validation passed before the small `rcp/rsqrt/log` polish edits. Unity refresh/compile is blocked by unrelated `WorldGenerativeGeologyTerrainSeamApplier.cs` missing-symbol errors; console filters for `HectonFluid` and `FluidAdvection` return 0 errors. Post-polish asset refresh hit a Unity plugin session disconnect, with `FluidAdvection` console filter still clean afterward. `dotnet build Hecton8.Core.csproj --no-restore` remains non-authoritative and fails on 154 project-wide missing namespace/type errors outside this domain.

Files changed: `Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute`, `Assets/_Project/Scripts/HectonFluidEngine.cs`, `Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs`, `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`, `Docs/Tasks/Status_ABYSSAL_CURRENT_ADVECTION.md`, `Docs/AgentLogs/Rationale_ABYSSAL_CURRENT_ADVECTION.md`.

## 2026-05-13 - Continuation Hardening
What was wrong: The RenderGraph advection pass wrote imported external buffers as a side effect and did not explicitly disable pass culling. The feature class also was not wired into URP renderer assets, so the dispatch could remain inert even if the code compiled.

What was done: Added `AllowPassCulling(false)` and `AllowGlobalStateModification(true)` to `HectonFluidAdvectionRenderFeature`. Inspected Unity 6000.4 RenderGraph APIs and rejected `AddComputePass` for this pass because public texture import is `RTHandle`-only while the fluid path binds existing `Texture3D` flow/SDF resources. Added `HectonFluidAdvectionRenderFeature` sub-assets to `PC_Renderer.asset`, `PC_High_Renderer.asset`, and `Mobile_Renderer.asset`; regenerated and verified `m_RendererFeatureMap` against each fileID list.

Cinematic Cheats used: No new simulation truth was added. The renderer wiring only makes the existing texture-SDF and low-tier linear-drift cheats execute.

Exact microseconds saved: No extra hot-path CPU work when no advected particles are active. Prevented a silent 100% visual failure from RenderGraph culling. Preserved the previous MX350 savings: up to 3000 skipped bubble/debris flow samples per active capped frame.

Verification: `git diff --check` passed on the renderer assets and feature. Renderer feature maps decode to the same fileID count and order: PC 14/14, PC_High 13/13, Mobile 11/11. Unity MCP session was unavailable during continuation; local `dotnet build ... | Select-String FluidAdvection` returned no fluid/advection-specific errors before the known project-wide dependency wall.

## 2026-05-13 - Native State Lifecycle Hardening
What was wrong: The advection graphics buffers could remain valid after `DisposeNativeArrays()` disposed the CPU staging arrays and telemetry ring during native resize/teardown. That left a stale `_fluidAdvectionStateReady` path and risked dispatching or uploading from half-disposed state.

What was done: Added a native-state readiness predicate for silt, bubble, debris, abyssal-flow fallback upload, and telemetry arrays. `IsFluidAdvectionReady()` now requires those arrays plus valid fallback buffers. `DisposeNativeArrays()` now resets advection ready/queued state and telemetry cursors immediately after staging teardown.

Cinematic Cheats used: No new physical truth. This preserves the existing GPU texture-flow/SDF fake and low-tier linear drift without adding CPU simulation.

Exact microseconds saved: No direct steady-state speedup claimed. Prevented a resize/reload edge from causing invalid dispatch, failed telemetry, or cold reallocation churn. Runtime cost is a handful of boolean checks; expected i3/MX350 impact is below measurable noise.

Verification: `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs` validate with 0 errors/0 warnings through Unity MCP after the lifecycle patch. `HectonUnderwaterVisuals.cs` validator disconnected twice after an earlier clean validation; no new edits were made there in this pass. Console filters for `FluidAdvection` and `HectonFluid` return 0 entries. Full Unity compile remains blocked by unrelated `Core/Memory/GlobalDataVault.cs` and missing `Hecton8.Vehicles.VFX` assembly errors. `git diff --check` passed on touched files; added-line GC/math audit found no new red-flag additions beyond line-ending warnings.

## 2026-05-13 - Ring Buffer And Resource Tracking Hardening
What was wrong: Exhale bubbles used a fixed count gate and would stop accepting new bursts after the first 2000 bubbles, because GPU life expiry is not read back to CPU. Pending AUP shifts could also survive when no particles existed, then shift particles spawned after the rebase. The RenderGraph pass bound flow/SDF textures through the native command buffer but did not declare those texture reads to RenderGraph.

What was done: Bubble bursts now overwrite the 2000-slot ring exactly like debris. Empty-buffer AUP shifts are cleared before new bubble/debris writes. The RenderGraph pass now imports the flow and SDF RTHandles and declares `UseTexture(..., Read)` alongside the existing buffer dependencies.

Cinematic Cheats used: Still no CPU particle truth and no readback. Expired bubbles are handled by ring overwrite instead of lifecycle synchronization. Origin shifts are applied only to particles that actually exist.

Exact microseconds saved: Avoids a GPU readback path that would cost synchronization stalls under load. Adds only one active-count branch around spawn/drain and RenderGraph metadata for two texture reads. Preserves the previous MX350 savings from low-tier bubble/debris flow bypass and SDF texture collision.

Verification: `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs` validate with 0 errors/0 warnings. `HectonUnderwaterVisuals.cs` validates with 0 errors and 2 pre-existing broad warnings. Console filters for `FluidAdvection`, `HectonFluid`, and `HectonUnderwaterVisuals` return 0 entries. Full compile remains blocked by unrelated `Core/Memory/GlobalDataVault.cs` and existing project-reference issues outside this domain.

## 2026-05-13 - Consumer Audit And Meta Repair
What was wrong: The new render-feature script meta contained only `fileFormatVersion` and `guid`, not the standard Unity `MonoImporter` payload. Stale external logs also claimed duplicate fluid helper methods and an underwater EOF error that did not match the current file.

What was done: Repaired `HectonFluidAdvectionRenderFeature.cs.meta` with a normal `MonoImporter` block. Audited project references: each advection helper now has one definition, renderer assets contain the feature, and renderer feature map counts match fileID lists for PC, PC_High, and Mobile. Cleared stale console state and requested a fresh script compile read.

Cinematic Cheats used: No new physical truth and no new draw path. Existing ParticleSystem exhale remains the visible fallback while compute buffers stay bounded motion authority for downstream consumers.

Exact microseconds saved: 0 us/frame direct runtime change. Avoided unprofiled renderer scope creep and removed editor import instability. Full compile now blocks on unrelated `WorldChunkResidencyManager.cs(1042)` overload drift, not fluid/advection.

Verification: `HectonFluidEngine.cs`, `HectonFluidAdvectionRenderFeature.cs`, and `HectonUnderwaterVisuals.cs` validate with 0 errors. Console filters for `FluidAdvection`, `HectonUnderwaterVisuals`, and `HectonFluidAdvectionRenderFeature` return 0 entries after stale console clear.
