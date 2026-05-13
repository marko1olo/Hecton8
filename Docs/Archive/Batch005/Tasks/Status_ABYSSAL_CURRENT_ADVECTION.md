# Status_ABYSSAL_CURRENT_ADVECTION

Agent: FLUID_MECHANIC  
Prompt ID: ABYSSAL_CURRENT_ADVECTION  
Domain: Echelon 2.19 Abyssal Flow Fields / Echelon 7.66 Marine Snow & Silt Compute  
Status: PENDING VERIFICATION  
Task Count: 18 numbered primary objectives. XML header says 19; no task 19 exists.

## Mandates Read Before Coding
- CORE_Weather_Abyssal_FlowField_Currents.txt
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Loop 0 - Prompt Extraction
- [x] Extracted `<AGENT_PROMPT id="ABYSSAL_CURRENT_ADVECTION">` from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell raw file read and regex, from opening tag to closing tag.
  - DOD practice: full-cover extraction, not partial MCP reading.
  - Alternative rejected: relying on open tabs or neighbor prompts.
  - Estimate: 250 us one-time CLI scan.
- [x] Read authoritative domain map.
  - DOD practice: bounded domain assignment against `Docs/Actual Domains of Project.txt`.
  - Alternative rejected: treating "fluid" as generic Physics ownership.
  - Estimate: 200 us one-time CLI read.

## Loop 1 - Tasks 1-5
- [x] 1. Extend `HectonFluidEngine`; singleton eradication N/A.
  - DOD practice: extended the GlobalRegistry-owned fluid runtime; no classic singleton or direct cross-agent dependency introduced.
  - Alternative rejected: separate advection singleton/manager outside the fluid service.
  - Estimate: 18-35 us CPU late-frame queue work at caps; GPU dispatch pending Unity profile.
- [x] 2. Consume `DebrisSpawnSignal`.
  - DOD practice: drains bounded `GlobalSignals.TryDequeueDebrisSpawn` budget in late frame and writes debris slots into fixed GPU buffers.
  - Alternative rejected: Rigidbody force injection or direct dropped-loot component dependency.
  - Estimate: 0.7-1.5 us per drained signal on i3-class CPU; cap 64 per late frame.
- [x] 3. ASMDEF isolation: `Hecton8.Environment.Fluids` to Contracts.
  - DOD practice: verified existing `Hecton8.Environment.Fluids` asmdef already points at `Hecton8.Environment.Fluids.Contracts`; this task required no new assembly dependency.
  - Alternative rejected: moving brine math during advection work, which would break unrelated assemblies while other agents are active.
  - Estimate: 0 us/frame.
- [x] 4. Dead code hunt: dropped loot must not use `Rigidbody.AddForce`.
  - DOD practice: project scan found only centralized `PhysicsApplySystem` `AddForce` calls; no dropped-loot/debris force path edited or added.
  - Alternative rejected: deleting central force router calls that are not dropped-loot ownership.
  - Estimate: 0 us/frame.
- [x] 5. Add unified dispatch/binds in `Hecton_FluidAdvection.compute`.
  - DOD practice: added one kernel binding Silt, Bubble, and Debris structured read/write buffers.
  - Alternative rejected: three separate dispatches, which would waste GPU bandwidth and synchronization.
  - Estimate: one 64-thread-group dispatch stream; worst cap 4096 silt / 2000 bubbles / 1000 debris.
- [x] Compile verification after Tasks 1-5. [BLOCKED BY DEPENDENCY]
  - DOD practice: Unity compile requested; console blocked by unrelated `HectonPlayerMovement` interface errors before clean project verification.
  - Alternative rejected: reporting local `dotnet build` as authority; project rules forbid that.
  - Estimate: 0 us/frame; verification blocked externally.

## Loop 2 - Tasks 6-10
- [x] 6. Integrate velocity from `AbyssalFlowField`.
  - DOD practice: compute samples 3D flow texture first, structured flow buffer fallback second, then integrates world position.
  - Alternative rejected: CPU-side per-particle flow sampling.
  - Estimate: 1 texture sample per active high-tier particle; low tier bypasses bubble/debris flow samples.
- [x] 7. Apply buoyancy inversion per element type.
  - DOD practice: constants are neutral silt, positive bubbles, negative debris before integration.
  - Alternative rejected: mass/drag truth simulation.
  - Estimate: 3 float ops per active lane.
- [x] 8. Sample `VoxelSdfTexture3D` in compute.
  - DOD practice: compute binds `_VoxelSdfTexture3D`, world-to-local matrix, and inverse extents.
  - Alternative rejected: Rigidbody or collider queries for particle collision.
  - Estimate: 1 texture sample only when SDF payload is active.
- [x] 9. Stop debris/silt or pop bubbles on solid SDF.
  - DOD practice: density threshold >0.5 zeros velocity; debris gets resting flag; bubbles life=0 and pop flag.
  - Alternative rejected: CPU collision callbacks.
  - Estimate: branch + one sample per active SDF lane.
- [x] 10. Hook exhale/underwater bubble source into GPU bubble AUP buffer.
  - DOD practice: underwater exhale GPU path queues bubble burst into `HectonFluidEngine.TryQueueAdvectedBubbleBurst`.
  - Alternative rejected: ParticleSystem-only exhale when marine snow GPU renderer is operational.
  - Estimate: one-time burst upload by `LockBufferForWrite`; no per-frame managed allocation.
- [x] Compile verification after Tasks 6-10. [BLOCKED BY DEPENDENCY]
  - DOD practice: Unity script validator reports zero errors in touched scripts; full Unity compile remains blocked by unrelated player movement errors.
  - Alternative rejected: ignoring console dependency wall.
  - Estimate: 0 us/frame.

## Loop 3 - Tasks 11-15
- [x] 11. Apply AUP shift offset before integration.
  - DOD practice: `AupShiftSignal` snapshot is consumed, converted to runtime rebase delta, and applied in compute before flow integration.
  - Alternative rejected: waiting for CPU readback or per-object Transform shifts.
  - Estimate: one vector add per active lane.
- [x] 12. Low tier Math LOD fallback disables debris/bubble compute.
  - DOD practice: Unknown/Low/MX350 set low-tier flag; compute skips flow sampling for bubble/debris and uses linear buoyancy vectors.
  - Alternative rejected: same high-tier flow samples on MX350.
  - Estimate: saves up to 3000 3D texture samples per frame at debris/bubble caps.
- [x] 13. Fixed buffers; 0 B managed allocation in hot path.
  - DOD practice: persistent NativeArrays/GraphicsBuffers; hot paths write existing buffers and drain queues.
  - Alternative rejected: List growth or managed arrays per dispatch.
  - Estimate: 0 B/frame managed allocation in advection Tick/LateFrame paths.
- [x] 14. Enforce caps: 1000 debris, 2000 bubbles.
  - DOD practice: public constants and ring cursors clamp debris/bubble counts to requested caps.
  - Alternative rejected: unbounded particle growth from burst spam.
  - Estimate: fixed VRAM footprint: debris 32 KB, bubbles 64 KB per buffer side.
- [x] 15. Dispatch in visual sync phase.
  - DOD practice: `ILateFrameTickable.LateFrameTick` queues payload after fixed physics and before URP RenderGraph pass.
  - Alternative rejected: FixedTick dispatch with visual data races.
  - Estimate: queue/build path under 0.03 ms expected; requires profiler proof.
- [x] Compile verification after Tasks 11-15. [BLOCKED BY DEPENDENCY]
  - DOD practice: touched scripts pass Unity MCP validator; full compile blocked externally.
  - Alternative rejected: fake clean compile claim.
  - Estimate: 0 us/frame.

## Loop 4 - Tasks 16-18
- [x] 16. Push `ActiveAdvectedParticles` to telemetry.
  - DOD practice: 300-entry native black-box ring plus periodic `GlobalTelemetryBus.PublishPerformanceWarning` scalar for active count.
  - Alternative rejected: Debug.Log or string telemetry in hot path.
  - Estimate: 8 scalar writes per frame; telemetry bus emission every 30 frames.
- [x] 17. RenderGraph path or documented blocker if URP hook is absent.
  - DOD practice: added `HectonFluidAdvectionRenderFeature` with URP RenderGraph unsafe pass, resource handles for buffers, and explicit bind/unbind.
  - Alternative rejected: direct `ComputeShader.Dispatch` from gameplay code.
  - Estimate: one RenderGraph compute pass; renderer asset wiring still requires Unity-side verification.
- [x] 18. Verify `numthreads(64,1,1)`.
  - DOD practice: kernel uses `#define HECTON_FLUID_ADVECTION_THREADS 64` and `[numthreads(...,1,1)]`.
  - Alternative rejected: 128/256-wide group that overfills MX350 lanes for this mixed particle workload.
  - Estimate: stable 64-wide groups; dispatch groups = ceil(maxCount/64).
- [x] Compile verification after Tasks 16-18. [BLOCKED BY DEPENDENCY]
  - DOD practice: Unity Console captured 11 unrelated `HectonPlayerMovement` errors; touched scripts validated clean through Unity MCP.
  - Alternative rejected: claiming shader/runtime verified without clean Unity compile.
  - Estimate: 0 us/frame.

## Loop 5 - Re-Verification
- [x] Re-read prompt after Tasks 1-18.
  - DOD practice: extracted the full `<AGENT_PROMPT id="ABYSSAL_CURRENT_ADVECTION">` block again from `Docs/Tasks/CURRENT_BATCH.md`.
  - Alternative rejected: relying on prior memory after context churn.
  - Estimate: 250 us one-time CLI scan.
- [x] Re-read own code and buffer binds.
  - DOD practice: audited compute binds for silt/bubble/debris read-write buffers, flow buffer/texture, SDF texture, AUP shift, and `numthreads(64,1,1)`.
  - Alternative rejected: assuming RenderGraph pass binding was correct without static search.
  - Estimate: 0 us/frame; static verification only.
- [x] Ensure compute buffers are unbound/released safely.
  - DOD practice: `UnbindFluidAdvectionCompute` binds one-element fallbacks after dispatch; `DisposeFluidAdvectionState` releases all advection graphics buffers, native staging arrays, telemetry ring, and empty texture.
  - Alternative rejected: leaving stale compute resource bindings for Unity to warn about later.
  - Estimate: unbind is one post-dispatch command block; release is cold teardown only.
- [x] Run final compile/console check. [BLOCKED BY DEPENDENCY]
  - DOD practice: Unity refresh/compile was requested; latest console errors are unrelated `WorldGenerativeGeologyTerrainSeamApplier` missing-symbol failures. `FluidAdvection`/`HectonFluid` error filters returned 0 entries.
  - Alternative rejected: fixing geology ownership from the fluid domain.
  - Estimate: 0 us/frame; verification blocked externally.
- [x] Polish mandate read and executed only after all core tasks are done or blocked.
  - DOD practice: post-core audit removed shader scalar division, removed normalize-family call, wrapped exception-only log under editor guard, and ran added-line GC/math scan with no remaining matches.
  - Alternative rejected: early polish before task completion.
  - Estimate: saves 1 reciprocal divide in the flow-buffer fallback and avoids a cold-path interpolated string.

## Compile Attempts
- Attempt 1: `dotnet build Hecton8.Core.csproj --no-restore` failed with 154 project-wide missing namespace/type errors outside fluid ownership (`Core.Memory.Layout`, `Audio.Propagation`, `Physics.CCD`, `World.Terrain`, `MacroSwarm`, `Acoustic*`, etc.). Not a fluid-specific proof.
- Attempt 2: Unity MCP `validate_script` on `HectonFluidEngine.cs` timed out in the regex validator after the final large-file patch; prior validation had passed before the small polish edits. `HectonFluidAdvectionRenderFeature.cs` validates with 0 errors/0 warnings. `HectonUnderwaterVisuals.cs` validates with 0 errors and 2 pre-existing broad warnings.
- Attempt 3: Unity refresh/compile timed out waiting for editor readiness, then console showed 5 unrelated `WorldGenerativeGeologyTerrainSeamApplier.cs` errors. Console filters for `HectonFluid` and `FluidAdvection` returned 0 errors.
- Attempt 4: Post-polish asset refresh for the compute shader hit a Unity plugin session disconnect while awaiting `command_result`; subsequent console filter for `FluidAdvection` still returned 0 errors.
- Attempt 5: Unity MCP session unavailable during continuation; `dotnet build Hecton8.Core.csproj --no-restore | Select-String FluidAdvection` returned no fluid/advection errors before the known project-wide dependency wall.
- Attempt 6: After native-state hardening, Unity MCP `validate_script` reports 0 errors/0 warnings for `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs`. `HectonUnderwaterVisuals.cs` validation disconnected twice after an earlier clean pass. Console filters for `FluidAdvection` and `HectonFluid` returned 0 entries. Full console remains blocked by unrelated `Core/Memory/GlobalDataVault.cs` and missing `Hecton8.Vehicles.VFX` assembly errors. `dotnet build Hecton8.Core.csproj --no-restore` remains non-authoritative and reports existing asmdef/project-reference failure on `Hecton8.Environment.Fluids`.
- Attempt 7: After ring/shift/RenderGraph texture tracking hardening, Unity MCP `validate_script` reports 0 errors/0 warnings for `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs`. `HectonUnderwaterVisuals.cs` reports 0 errors with 2 pre-existing broad warnings. Console filters for `FluidAdvection`, `HectonFluid`, and `HectonUnderwaterVisuals` returned 0 entries. Full console remains blocked by unrelated `Core/Memory/GlobalDataVault.cs` errors. `dotnet build Hecton8.Core.csproj --no-restore` remains non-authoritative and still reports the existing `Hecton8.Environment.Fluids` project-reference failure.
- Attempt 8: After script-meta repair and stale console clear, `HectonFluidEngine.cs`, `HectonFluidAdvectionRenderFeature.cs`, and `HectonUnderwaterVisuals.cs` validate with 0 errors. Console filters for `FluidAdvection`, `HectonUnderwaterVisuals`, and `HectonFluidAdvectionRenderFeature` return 0 entries. Fresh full-console blocker is unrelated `WorldChunkResidencyManager.cs(1042)` `BindNativeMatrices` overload drift plus Burst entry-point exception.

## Loop 6 - Continuation Hardening
- [x] Re-read status/rationale before continuing.
  - DOD practice: anti-amnesia files were read at turn start before new edits.
  - Alternative rejected: editing from chat memory.
  - Estimate: 0 us/frame.
- [x] Re-read Unity MCP workflow and relevant RenderGraph/project rules.
  - DOD practice: checked RenderGraph, zero-GC, pass culling, and buffer discipline before changing renderer wiring.
  - Alternative rejected: assuming the first RenderGraph pass was enough.
  - Estimate: 0 us/frame.
- [x] Fix RenderGraph culling risk.
  - DOD practice: `HectonFluidAdvectionRenderFeature` now calls `AllowPassCulling(false)` because the pass writes external particle buffers as side effects.
  - Alternative rejected: moving to `AddComputePass`; Unity 6000.4 RenderGraph only imports `RTHandle` textures, while this pass must bind existing `Texture3D` flow/SDF resources.
  - Estimate: prevents a silent 100% visual failure; CPU cost unchanged.
- [x] Wire renderer assets.
  - DOD practice: added `HectonFluidAdvectionRenderFeature` sub-assets to `PC_Renderer`, `PC_High_Renderer`, and `Mobile_Renderer`; renderer feature maps were regenerated and verified byte-for-byte against fileID lists.
  - Alternative rejected: leaving a correct feature class that no renderer instantiates.
  - Estimate: no dispatch when no active particles; one bounded pass when active.
- [x] Static regression checks after hardening.
  - DOD practice: `git diff --check` passed for renderer assets and feature; added-line scan found no new GC/math/red-flag additions in the hardening diff.
  - Alternative rejected: claiming AAA quality without checking YAML map integrity and diff hygiene.
  - Estimate: 0 us/frame.

## Loop 7 - Native State Reallocation Audit
- [x] Re-extract prompt after continuation work.
  - DOD practice: extracted the current `<AGENT_PROMPT id="ABYSSAL_CURRENT_ADVECTION">` block from `Docs/Tasks/CURRENT_BATCH.md` via CLI after an escaped-regex attempt failed.
  - Alternative rejected: proceeding from stale prompt memory.
  - Estimate: 250 us one-time CLI scan.
- [x] Harden readiness after native-array disposal.
  - DOD practice: added `HasFluidAdvectionNativeState()`, required native staging/telemetry arrays for readiness, and reset render-queue/state/telemetry cursors when `DisposeNativeArrays()` disposes advection staging during resize/teardown.
  - Alternative rejected: trusting graphics buffer existence after staging arrays are disposed.
  - Estimate: 5 boolean checks in cold/late-frame readiness only; 0 additional GPU cost.
- [x] Validate lifecycle patch.
  - DOD practice: `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs` validate clean through Unity MCP; console filters for `FluidAdvection`/`HectonFluid` return 0 entries.
  - Alternative rejected: using non-authoritative `dotnet build` as proof while the project has asmdef/project-reference drift.
  - Estimate: 0 us/frame; verification remains blocked by external compile errors.
- [x] Static hygiene after lifecycle patch.
  - DOD practice: `git diff --check` passed on touched files; added-line GC/math scan found no red-flag additions beyond line-ending warnings.
  - Alternative rejected: leaving a disposal regression untracked because the task already had 18 boxes checked.
  - Estimate: 0 us/frame.

## Loop 8 - Ring Buffer And RenderGraph Resource Audit
- [x] Re-audit bubble cap behavior.
  - DOD practice: changed exhale bubbles from permanent cap lockout to fixed ring overwrite, matching debris behavior while preserving the 2000-bubble VRAM cap.
  - Alternative rejected: CPU readback of expired GPU bubble life, which would add synchronization pressure and defeat the visual-fake path.
  - Estimate: no new steady-state cost; prevents exhale visuals from dying permanently after the first cap fill.
- [x] Re-audit AUP shift with zero active particles.
  - DOD practice: stale pending runtime shift is cleared when no advected particles exist before new bubble/debris writes, so newly spawned runtime-space particles are not shifted by an old origin rebase.
  - Alternative rejected: applying every origin shift blindly even when the buffer is empty.
  - Estimate: one active-count branch per late frame and bubble burst; no GPU cost.
- [x] Harden RenderGraph texture tracking.
  - DOD practice: the unsafe RenderGraph pass now imports and declares flow/SDF RTHandle texture reads in addition to buffer reads/writes.
  - Alternative rejected: native command-buffer texture binding only, which hides texture dependencies from RenderGraph.
  - Estimate: command graph metadata only; no additional dispatch or samples.
- [x] Validate ring/shift/texture tracking patch.
  - DOD practice: Unity MCP validation passes for edited scripts; console filters for fluid/advection paths are clean; static diff and GC/math scans pass.
  - Alternative rejected: relying on visual assumptions without validator and console evidence.
  - Estimate: 0 us/frame; full compile remains blocked by unrelated Core/Memory dependency wall.

## Loop 9 - Consumer And Asset Import Audit
- [x] Audit advection consumers.
  - DOD practice: project-wide scan confirms the new buffers are consumed by the RenderGraph compute dispatch; exhale visual fallback still emits the existing ParticleSystem bubbles while the compute buffers provide motion authority for downstream render consumers.
  - Alternative rejected: adding an unrequested draw path inside the compute-advection task, which would cross into renderer/VFX ownership and duplicate marine-snow rendering.
  - Estimate: 0 us/frame; audit only.
- [x] Repair script meta import state.
  - DOD practice: added the standard `MonoImporter` block to `HectonFluidAdvectionRenderFeature.cs.meta` so Unity imports the new renderer feature script like neighboring first-party C# scripts.
  - Alternative rejected: leaving a bare `fileFormatVersion/guid` meta that validates by path but can become brittle through asset refresh/domain reload.
  - Estimate: 0 us/frame; editor import stability only.
- [x] Re-verify renderer asset wiring.
  - DOD practice: decoded renderer feature list counts and `m_RendererFeatureMap` entries for PC, PC_High, and Mobile; all counts match and all include `HectonFluidAdvectionRenderFeature`.
  - Alternative rejected: trusting YAML visual inspection only.
  - Estimate: 0 us/frame.
- [x] Clear stale console and recheck current blockers.
  - DOD practice: stale `HectonUnderwaterVisuals` EOF error referenced a line beyond current file length and was invalidated by validator; console was cleared and a fresh compile request shows only unrelated `WorldChunkResidencyManager` overload drift.
  - Alternative rejected: recording stale console errors as current blockers.
  - Estimate: 0 us/frame.
