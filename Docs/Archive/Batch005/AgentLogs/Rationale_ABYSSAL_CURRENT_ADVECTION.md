# Rationale_ABYSSAL_CURRENT_ADVECTION

Status: PENDING VERIFICATION  
Agent: FLUID_MECHANIC  
Prompt ID: ABYSSAL_CURRENT_ADVECTION

## Decision 0 - Prompt Count Mismatch
Problem: The XML header claims "19 TITANIUM TASKS" but the primary numbered list contains tasks 1-18 only.
Solution: Treat the actual numbered primary objectives as authority and record the mismatch in status/log files.
Rejected Alternatives: Inventing a task 19 would contaminate scope and create fake completion evidence.
Scalability potential: No runtime impact. Prevents unbounded work and keeps the batch integrator's accounting deterministic.
Hardware Impact: 0 us/frame. No i3/MX350 runtime cost.

## Decision 1 - Mandate Selection
Problem: Fluid advection touches GPU compute, abyssal currents, VFX particles, AUP, zero-GC, telemetry, and fake-first policy.
Solution: Read eight targeted mandates before code: abyssal flow, fluid VFX, MX350 compute kernels, mobile warp sizing, AUP precision, crash telemetry, zero-GC, and cinematic cheat.
Rejected Alternatives: Reading all mandates would waste context and increase risk of neighbor-domain drift; reading none violates batch protocol.
Scalability potential: Keeps design locked to Low/Mid/High/Ultra tier gates instead of a single middle path.
Hardware Impact: 0 us/frame. One-time CLI reads only.

## Decision 2 - Unified Advection Owner
Problem: Debris, bubbles, and silt need a common flow response without coupling to loot, habitat, or VFX ownership that may be edited by parallel agents.
Solution: Extend `HectonFluidEngine` as the buffer owner and expose a RenderGraph payload builder. Inputs arrive through `DebrisSpawnSignal`, AUP shift snapshots, and a small exhale bridge; consumers do not depend on unfinished neighboring systems.
Rejected Alternatives: A new singleton advection manager, direct dropped-loot component references, or ParticleSystem-only movement.
Scalability potential: Low/MX350 uses linear bubble/debris vectors; Mid keeps flow on silt plus cheap bubble/debris fallback; High/Ultra samples abyssal flow and SDF for visually denser drift.
Hardware Impact: On i3/MX350, expected savings are up to 3000 skipped 3D flow samples per frame at bubble/debris caps. High-end can spend those samples for visual overkill drift.

## Decision 3 - Collision Is A Texture Lie
Problem: Real collision for thousands of tiny particles would violate the 0.1 ms suspicion threshold and allocate pressure through physics ownership.
Solution: The compute shader samples `VoxelSdfTexture3D`; solid density stops debris/silt and pops bubbles.
Rejected Alternatives: Rigidbody collisions, Physics.Overlap, per-particle raycasts, or CPU SDF marching.
Scalability potential: Low tier can keep SDF inactive or sparse; High/Ultra can bind cave SDF and spend one texture sample per active lane.
Hardware Impact: Replaces collider queries with one texture read. Estimated low-end gain: 100-400 us avoided when 1000+ particles overlap cave surfaces.

## Decision 4 - RenderGraph Dispatch Boundary
Problem: Gameplay-side raw compute dispatch would desynchronize with URP resource lifetime and violate the task's RenderGraph requirement.
Solution: Added `HectonFluidAdvectionRenderFeature`, a URP RenderGraph pass that imports all particle/flow buffers, binds textures/constants, dispatches, then unbinds fallback resources.
Rejected Alternatives: `ComputeShader.Dispatch` from `LateFrameTick`, raw renderer command buffer outside RenderGraph, or extending marine-snow renderer with another hidden dispatch.
Scalability potential: One pass can be conditionally present only on renderers that need underwater VFX; high-end renderers can keep it before transparents for richer bubble/debris motion.
Hardware Impact: Single dispatch with 64-wide groups. CPU command recording is expected below 30 us; GPU cost requires profiler verification after external compile errors are cleared.

## Decision 5 - Verification Block
Problem: Full Unity compile cannot currently verify this task because the project console is blocked by unrelated `HectonPlayerMovement` interface errors.
Solution: Mark compile checks as `[BLOCKED BY DEPENDENCY]`, record the exact external errors, and validate touched scripts individually through Unity MCP.
Rejected Alternatives: Reporting `dotnet build` as authoritative or fixing player movement interfaces outside the assigned fluid domain.
Scalability potential: No runtime impact. Prevents fluid task from contaminating player-movement ownership.
Hardware Impact: 0 us/frame.

## Decision 6 - OMEGA Polish
Problem: The finished advection path still contained one HLSL scalar division in the flow-buffer fallback and one normalize-family call in cold spawn jitter. The exception dump path also used interpolated logging.
Solution: Replaced fallback grid division with `rcp()` multiplication, replaced `math.normalizesafe` with `dot + rsqrt`, and wrapped the exception warning in an editor-only non-interpolated string path. Added-line audit now reports no `foreach`, `string.Format`, `$"`, `.ToString()`, `math.sqrt`, or `math.normalize` additions.
Rejected Alternatives: Leaving the divide because it was not the dominant cost; leaving `normalizesafe` because spawn is cold; using full exact vector magnitude. HECTON mandate rejects that laziness.
Scalability potential: Low tier keeps bubble/debris flow sampling disabled and uses linear buoyancy vectors. Middle tier can run silt plus cheap bubble/debris. High tier can sample flow and SDF across all active lanes. Ultra can spend the saved cycles on denser visible particle counts and richer flow textures without changing the contract.
Hardware Impact: MX350/i3 avoids up to 3000 bubble/debris 3D flow samples per frame at caps and one fallback divide per structured-flow sample. Estimated low-end saved cost: 40-180 us when bubble/debris caps are active; 100-400 us avoided by texture SDF instead of collider queries. Top-tier spends the same pipeline on visual overkill density. Velocity integration now stores velocity and applies `Position += velocity * dt`, preserving stable damping across frames.

## Decision 7 - Domain Boundary Exception
Problem: Task 10 required exhale bubbles from the underwater/flood renderer path, but ownership is not the core fluid file.
Solution: Touched `HectonUnderwaterVisuals.cs` only at the exhale source and routed through `GlobalRegistry.Fluid.TryQueueAdvectedBubbleBurst`. No direct dependency on unfinished renderer internals was introduced.
Rejected Alternatives: New event contract inside another agent's renderer, direct component reference, or CPU ParticleSystem-only exhale.
Scalability potential: Cheap devices can keep the visual source but let low-tier advection use linear upward drift; high-tier uses flow-driven bubbles.
Hardware Impact: One burst upload by locked GPU buffer writes; no per-frame managed allocation from the bridge.

## Decision 8 - RenderGraph Hardening And Renderer Wiring
Problem: The first RenderGraph pass wrote external GPU buffers and had no downstream texture dependency, so RenderGraph could legally cull it. The feature class also was not present in renderer assets, which would leave the compute path inert.
Solution: Added `AllowPassCulling(false)` and `AllowGlobalStateModification(true)` to the RenderGraph unsafe pass. Kept the unsafe pass because Unity 6000.4 public RenderGraph import only accepts `RTHandle` textures; this advection path must bind existing `Texture3D` SDF and flow resources. Wired the feature into PC, PC_High, and Mobile renderer assets and verified `m_RendererFeatureMap` byte order against the fileID list.
Rejected Alternatives: `AddComputePass` was rejected after local package inspection showed no `ImportTexture(Texture/Texture3D)` path. Leaving renderer assets untouched was rejected because it creates fake implementation.
Scalability potential: Mobile/MX350 renderer now records the feature but the engine still dispatches only when active particles exist and low tier skips bubble/debris flow sampling. High/Ultra renderer records the same pass and spends the saved cycles on SDF/flow-rich drift.
Hardware Impact: No new per-frame cost while particle counts are zero. When active, the pass remains one 64-wide dispatch stream; culling guard prevents silent missing visuals, not extra simulation.

## Decision 9 - Native Staging Readiness Guard
Problem: `DisposeNativeArrays()` can run during resize or teardown and dispose the advection staging arrays while graphics buffers survive. The old readiness guard could still treat the advection state as ready because it only checked a subset of GPU resources.
Solution: Added `HasFluidAdvectionNativeState()`, required native staging and telemetry arrays inside `IsFluidAdvectionReady()`, checked fallback buffer validity, and reset `_fluidAdvectionStateReady`, `_fluidAdvectionRenderGraphQueued`, and telemetry cursors when native advection state is disposed.
Rejected Alternatives: Releasing all advection graphics buffers on every native-array resize would add avoidable cold churn; trusting `GraphicsBuffer.IsValid()` alone misses disposed CPU staging and black-box telemetry state.
Scalability potential: Low tier keeps the same cheap linear drift path, but cannot dispatch from a half-disposed state. High/Ultra keep the same buffer contract and regain staging deterministically after a resize without stale telemetry.
Hardware Impact: Adds a few boolean readiness checks outside the GPU kernel. Estimated i3/MX350 frame impact is below measurable noise; avoids invalid-buffer dispatch and black-box write failure during resize/reload edges.

## Decision 10 - Ring Overwrite And Empty-Shift Hygiene
Problem: Bubble bursts stopped accepting new particles after the CPU count first reached 2000, even though GPU bubble life can expire without CPU readback. A pending AUP shift could also survive through an empty particle set and later offset newly spawned runtime-space particles.
Solution: Bubble bursts now overwrite the fixed ring like debris while keeping the 2000 cap. Pending runtime shift is cleared whenever there are no active advected particles before new writes. The RenderGraph pass also imports flow/SDF texture handles and declares read access so its resource dependencies are visible.
Rejected Alternatives: GPU-to-CPU readback of expired bubble life was rejected as synchronization-heavy and unnecessary for a visual effect. Leaving native command-buffer texture binds untracked was rejected because it hides resource use from RenderGraph.
Scalability potential: Low/MX350 keeps the same fixed VRAM footprint and cheap linear bubble drift. High/Ultra can sustain repeated exhale/debris visuals indefinitely without growing buffers or adding readback.
Hardware Impact: No new GPU dispatch or texture sample. Adds one branch before bubble/debris writes and RenderGraph metadata for two texture reads. Prevents permanent visual loss after 2000 exhale bubbles and prevents bad AUP offsets on empty-buffer rebases.

## Decision 11 - Import Hygiene Before New Rendering Scope
Problem: The new RenderFeature script meta was missing the normal `MonoImporter` block, and a consumer audit showed no dedicated draw path for the advection buffers beyond the RenderGraph compute dispatch and existing exhale ParticleSystem fallback.
Solution: Repair the script meta to match neighboring first-party C# assets. Do not add a new advection draw renderer inside this compute task; record the consumer boundary and keep the compute dispatch/output contract stable for the renderer owner.
Rejected Alternatives: Building a standalone bubble/debris/silt renderer here would duplicate marine-snow rendering and cross into VFX ownership. Leaving bare script meta was rejected because asset refresh can become unstable even when direct script validation passes.
Scalability potential: Low/MX350 keeps only the bounded compute dispatch when active plus existing visual fallback. High/Ultra can add a richer downstream draw consumer later without changing the compute buffer contract.
Hardware Impact: 0 us/frame for meta repair and consumer audit. Avoids editor/import instability and prevents a scope creep renderer that would add unprofiled draw cost.
