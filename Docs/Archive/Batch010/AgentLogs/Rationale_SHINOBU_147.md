# Rationale_SHINOBU_147

Status: IMPLEMENTED; COMPILE BLOCKED BY EXTERNAL DEPENDENCIES

## Decision 000 - Domain Gate
Problem: Surface-wave task touches rendering, physics, AUP, weather, and abyssal currents; unmanaged cross-domain edits can break parallel agents.
Solution: Keep ownership in `Hecton8.Atmosphere`/surface weather files; expose only compact DTO/query interfaces or existing registry/vault routes after source verification.
Rejected Alternatives: Direct edits to submarine buoyancy/vehicle classes before discovering existing interfaces; this couples domains and violates parallel-agent isolation.
Scalability potential: Low = one to two broad octaves and tiny readback grid; Middle = partial octaves and moderate sample count; High = full visual Gerstner stack; Ultra = foam/whitecap overkill while physics sample count remains bounded.
Hardware Impact: Expected benefit on i3/MX350 comes from eliminating CPU mesh vertex loops and PhysX mesh rebuilds; exact microseconds are unmeasured and remain PENDING VERIFICATION.

## Decision 001 - Prompt Extraction
Problem: Batch file contains neighboring agent directives; polluted context would drive wrong file edits.
Solution: CLI regex extracted only `<AGENT_PROMPT id="SHINOBU_147">...</AGENT_PROMPT>`.
Rejected Alternatives: Manual skim or MCP partial read; both risk truncation and neighboring-prompt leakage.
Scalability potential: Not runtime-facing.
Hardware Impact: None at runtime.

## Decision 002 - 64B Packed Wave DTO
Problem: Previous wave data shape could not pack six Gerstner definitions into an ARM64/cacheline clean shader payload.
Solution: Rebuilt `WaveParametersDTO` as explicit 64 bytes: `Wave1`, `Wave2`, `Wave3`, `GlobalWindAndStorm` at 16-byte boundaries. Each wave lane stores heading radians, steepness, wavelength, and phase speed; amplitude is derived from steepness/wavelength.
Rejected Alternatives: Retaining amplitude/phase fields per DTO was rejected because it only carried one wave per record and forced larger GPU upload/loop count. `[StructLayout(Pack=1)]` was rejected because it creates unaligned mobile reads.
Scalability potential: Low = one broad lane active; Middle = fractional 2-4 lanes; High/Ultra = six lanes with foam/Jacobian work.
Hardware Impact: One 64-byte cacheline carries three waves; two records cover six waves. Upload is 128 bytes instead of many per-wave records.

## Decision 003 - GPU-Only Visual Truth
Problem: Surface waves must look violent without CPU mesh deformation or mesh collider rebuilds.
Solution: HLSL now evaluates Gerstner displacement and Jacobian foam from the global wave buffer. Runtime uploads compact DTOs and never mutates mesh vertices in the surface domain.
Rejected Alternatives: CPU Gerstner mesh deformation and runtime `MeshCollider` updates were rejected as O(vertices * waves) CPU work plus PhysX rebuild spikes.
Scalability potential: Low devices evaluate 1-2 broad lanes; mid devices evaluate partial lanes; high/ultra devices spend saved CPU on shader foam/whitecaps.
Hardware Impact: Removes the dominant CPU loop. MX350-class gain is workload-dependent but expected to be milliseconds when old mesh water was active.

## Decision 004 - Targeted Async Readback
Problem: Buoyancy needs wave height at a tiny set of points, not the full visual mesh.
Solution: Added `Hecton_WaveHeightSampler.compute`. Runtime queues XZ samples into vault-backed staging arrays, dispatches a tiny compute pass, and consumes `AsyncGPUReadback.Request` results from a 3-slot ring on later frames.
Rejected Alternatives: `Texture2D.ReadPixels`, `WaitForCompletion`, current-frame GPU waits, and CPU fallback wave grids were rejected because each causes stalls or a second visual truth.
Scalability potential: Low = 4 samples/frame; middle = polynomially more samples; high/ultra = up to 64 targeted points while visual shader still owns geometry.
Hardware Impact: PCIe/unified-memory readback is capped to 64 `float4` results, 1024 bytes before overhead.

## Decision 005 - AUP Phase Safety
Problem: Absolute 100km coordinates break wave phase precision if cast directly to float for GPU math; component-wrapped camera X/Z also fails for diagonal Gerstner lanes with different wavelengths.
Solution: CPU computes per-lane camera AUP projection in double, wraps that projection by the lane wavelength, and publishes six compact phase bases through `_H8OceanWavePhaseBase0/1`. Shader/compute add only local camera-space/query XZ and time.
Rejected Alternatives: Passing absolute double3 or large world floats to GPU was rejected because HLSL cannot preserve that precision. Reusing `WaveParametersDTO.GlobalWindAndStorm` for phase bases was rejected because it merged wind/storm truth with camera-derived presentation constants.
Scalability potential: Same math across all tiers; quality only changes active lane count and sample budget.
Hardware Impact: Negligible cost; prevents visible phase tearing at sector boundaries.

## Decision 006 - Vault-Owned Cold CSV Tuning
Problem: Designers need Beaufort profile tuning without managed CSV garbage or recompiles.
Solution: Cold file read goes into vault scratch; parser slices `ReadOnlySpan<byte>`, FNV-hashes state names, and writes `BeaufortProfileDTO` records into a vault-backed open-address table. Tuner uses the reserved QSTP record for quality/choppiness controls.
Rejected Alternatives: `string.Split`, `Dictionary<string,...>`, or private persistent `NativeHashMap` fields were rejected due GC or data sovereignty violations.
Scalability potential: Low/Mid/High/Ultra profiles can map named weather states to different steepness/wavelength/frequency without code changes.
Hardware Impact: Hot path impact is zero; cold-load GC avoided.

## Decision 007 - Physics Route Boundary
Problem: Full `HectonFluidEngine` owns a separate buoyancy pipeline with legacy 16-wave CPU fallback and broad sibling-domain risk.
Solution: Surface domain exposes async GPU sampled heights through the existing `IHectonOceanKinematics` provider and vault buffers. Direct FluidEngine surgery is deferred unless the integrator authorizes owner-domain changes.
Rejected Alternatives: Flipping FluidEngine GPU parity blindly was rejected because its compute shader samples a separate 3-wave cinematic approximation and does not match the new 6-lane AUP sampler.
Scalability potential: Key player/vehicle queries use targeted readback immediately; broad object buoyancy remains a Fluid owner integration point.
Hardware Impact: Prevents compile-wall and stride risk while giving player/submarine-facing systems a lock-free sample route.

## Decision 008 - CPU Query Contract Removal
Problem: Legacy contract jobs (`OceanBuoyancy*Job`, `MockBuoyancyQueryJob`) still exposed CPU wave-height evaluation as a tempting physics route.
Solution: Removed the CPU buoyancy query jobs, mock buoyancy DTOs, and their runtime vault allocations. The remaining CPU `EvaluateWaves` helper is limited to deterministic math/editor tests; runtime physics-facing queries route through queued GPU readback.
Rejected Alternatives: Keeping the CPU jobs as "fallback" was rejected because it contradicts the assignment's async-only buoyancy truth.
Scalability potential: Low through Ultra use the same sample queue; quality changes sample count and wave lanes, not CPU/GPU ownership.
Hardware Impact: Removes a possible 10k-query CPU test path from runtime memory ownership and cuts 10000-entry mock buffers from boot allocation.

## Decision 009 - Compile Wall Classification
Problem: `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` failed before SHINOBU-owned runtime/editor files could be proven.
Solution: Classified as external compile wall after the first guarded build attempt. Errors are missing types in `HectonVisorUberPostFeature.cs`, `ModularEquipmentEngine.cs`, `SomaticTunerWindow.cs`, `EcosystemDirector.cs`, and `PlayerSwimPresentationController.cs`; SHINOBU_147 did not edit those domains.
Rejected Alternatives: Patching Visor, Equipment, Somatic, Ecosystem, or KineticCharacter contracts was rejected as cross-domain sabotage.
Scalability potential: Not runtime-facing.
Hardware Impact: No runtime effect; prevents wasting additional build attempts against known external missing DTOs.

## Decision 010 - Readback Buffer Ring Hardening
Problem: The targeted wave sampler used three `AsyncGPUReadbackRequest` slots but only one GPU query buffer and one GPU result buffer. A pending readback could observe a result buffer overwritten by a newer dispatch.
Solution: Replaced the single query/result pair with explicit slot 0/1/2 `GraphicsBuffer` fields for both query upload and GPU result output. Each slot now owns its upload buffer, compute output buffer, request metadata, and Vault query mirror until completion.
Rejected Alternatives: A managed `GraphicsBuffer[]` ring was rejected to avoid a new managed array field. A single buffer plus latency assumption was rejected because the GPU mandate requires a 3-frame readback ring and does not guarantee completion before the next dispatch.
Scalability potential: Low through Ultra keep the same bounded 4..64 sample curve; the ring only protects correctness and bus ownership. High/Ultra can sustain more queued samples without corrupting delayed physics reads.
Hardware Impact: Adds 6 KB of cold GPU buffer capacity at 64 samples x float4 x 6 buffers. Prevents readback/race artifacts without adding hot-path GC or CPU stalls.

## Decision 011 - Exact-Zero Quality Preservation
Problem: `ResolveGlobalQualityWeight()` treated `0.0` as invalid and promoted it to `1.0`, violating the continuum law under thermal survival mode. Several helper paths also relied on raw `math.saturate` and could propagate non-finite quality into C#/shader math.
Solution: Added `HectonOceanSurfaceMath.SanitizeQualityWeight()`: finite values saturate to `[0,1]`, non-finite values fail closed to `0.0`. Runtime, Burst jobs, readback budget, cadence, HLSL surface shader, and compute sampler now use the same low-work fallback. Added editor assertions that exact-zero and NaN quality resolve to one active wave lane and zero contribution for lane 1.
Rejected Alternatives: Clamping to a small positive floor was rejected because the project contract defines `0.0` as a valid minimum survival point, not an error state.
Scalability potential: Low = one broad lane, 5Hz presentation phase quantization, 4 readback samples. Middle/High/Ultra remain the same polynomial continuum.
Hardware Impact: Restores intended lowest-tier ALU/readback collapse on thermal throttle. Measured frame impact remains pending.

## Decision 012 - Agent-ID Blackbox Dump Route
Problem: The ocean telemetry fault dump used a domain-generic filename, which described the domain but did not match the active `Dump_[YourID].bin` forensic rule.
Solution: Changed the runtime dump path to `Docs/AgentLogs/Dump_SHINOBU_147.bin`.
Rejected Alternatives: Keeping the domain-generic filename was rejected because multiple ocean/physics agents can emit dumps and the CTO-facing mandate keys by agent ID.
Scalability potential: Not runtime-facing.
Hardware Impact: No frame impact; improves post-fault ownership lookup.

## Decision 013 - Phase Bases Are Shader Constants, Not Wave DTO State
Problem: Post-audit review found `GlobalWindAndStorm` was being overwritten with AUP phase bases before upload, violating one-fact-one-owner and making wind/storm payload semantics false.
Solution: Added `OceanWaveAupPhaseDTO` as a 64B explicit layout proof and publish its `PhaseBase0/PhaseBase1` lanes as shader/compute constants. `WaveParametersDTO` keeps wave lanes plus wind/storm only.
Rejected Alternatives: Adding a persistent Vault buffer for phase bases was rejected because these values are camera-derived presentation constants, recalculated from authoritative AUP/waves each publish and not long-lived simulation state.
Scalability potential: Low/Middle/High/Ultra use identical phase correctness; only the active lane contribution changes by `GlobalQualityWeight`.
Hardware Impact: Adds two float4 scalar uploads when shader state changes; avoids extra wave-buffer churn when only camera-derived phase changes.

## Decision 014 - Cold GPU Buffer Creation Boundary
Problem: Targeted readback dispatch still had a route to create `GraphicsBuffer`s from the tick path if cold initialization missed them.
Solution: `OnEnable`/`SlowTick` perform cold `EnsureWaveGraphicsBuffers()` and `EnsureWaveReadbackGraphicsBuffers()`. `DispatchWaveHeightReadback()` now only checks `HasWaveGraphicsBuffers()` and `HasWaveReadbackGraphicsBuffers()` and returns if buffers are absent.
Rejected Alternatives: Keeping `Ensure*` in dispatch was rejected because hidden cold allocation in tick path violates the zero-GC/zero-stutter mandate even when allocation is native/GPU-side.
Scalability potential: Same readback budget curve; the change is ownership hygiene, not visual tiering.
Hardware Impact: Prevents first-sample GPU buffer allocation spikes from landing inside the gameplay tick.

## Decision 015 - Cached Vault Handle Route
Problem: The hot `Tick` called `EnsureVaultBuffers()`, which could resolve `GlobalRegistry.DataVault` and fallback `GlobalDataVault.TryGetLatestCreated()` every frame.
Solution: Split vault binding into `EnsureVaultBuffersCold()`. `Tick` now requires `_vaultBuffersReady` and does not touch the registry; `SlowTick` can still recover cold if the Vault appears after component enable.
Rejected Alternatives: Resolving `GlobalRegistry` in every tick was rejected because it hides service lookup cost and violates dependency cache warmup.
Scalability potential: Runtime tiering unchanged.
Hardware Impact: Removes registry/fallback checks from the frame wave path; exact microseconds unmeasured.

## Decision 016 - Readback Teardown Lifetime Gate
Problem: Disabling the component could dispose query/result buffers while a slot-owned `AsyncGPUReadbackRequest` was still pending.
Solution: Disposal now calls `ConsumeWaveHeightReadbacks()`, checks active slots, and defers buffer disposal while any request is not done. Late-frame teardown retries without `WaitForCompletion`.
Rejected Alternatives: Blocking with `WaitForCompletion` or disposing source buffers optimistically was rejected; both can create stalls or undefined GPU ownership.
Scalability potential: No visual tier change.
Hardware Impact: Prevents rare disable/scene-transition GPU readback lifetime faults without adding hot-path stalls.

## Decision 017 - Fault Dump Deferral
Problem: Readback latency fault detection could write `Dump_SHINOBU_147.bin` directly from the readback consumption path.
Solution: Tick/readback paths now set `_telemetryDumpRequested`; `LateFrameTick` performs the throttled filesystem dump from the diagnostic window.
Rejected Alternatives: Direct filesystem I/O in tick was rejected because fault handling must not worsen the frame that detected the fault.
Scalability potential: Not visual-tier facing.
Hardware Impact: Removes fault-path file creation from the main wave/readback loop. Normal-frame impact is zero.

## Decision 018 - First-Party Storm Ocean Shader Consumer
Problem: `Hecton_OceanSurfaceAtmosphere.hlsl` existed as math but had no first-party shader consumer, so the GPU displacement path was source-present but not asset-visible.
Solution: Added `Hecton_StormOceanSurface.shader`, a URP transparent surface shader that includes the HLSL file and calls `H8EvaluateOceanSurface()` in the vertex stage.
Rejected Alternatives: Relying on future material authors to include the HLSL was rejected because the task requires an implemented GPU visual route now.
Scalability potential: Shader respects `GlobalQualityWeight`: low tiers collapse to one broad lane and no foam overwork; high/ultra use all six lanes and foam/fresnel.
Hardware Impact: Moves visible surface displacement to vertex/GPU work and keeps CPU mesh geometry untouched.

## Decision 019 - SlowTick Wave Job Fence
Problem: `SlowTick()` could run after `Tick()` scheduled `CalculateWaveParametersJob` but before the next frame completed it, then read or mutate the same Vault wave/weather buffers through CSV or storm surge paths.
Solution: `SlowTick()` now calls the existing non-blocking `TryCompleteWaveParameterKernel()` after cold buffer setup and returns if the job is still active. CSV/storm upload paths were changed to `UploadWaveBufferToGpu(false)` because `SlowTick()` already owns cold GPU buffer setup.
Rejected Alternatives: Forcing `JobHandle.Complete()` in `SlowTick()` was rejected because it would turn a recovery/update hook into a main-thread stall. Leaving `UploadWaveBufferToGpu(true)` in slow-only mutation paths was rejected because hidden cold allocation routes should stay in explicit setup code.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the fence protects the same quality continuum from writer/reader aliasing.
Hardware Impact: Prevents a rare wave buffer race without adding a blocking wait. Removes two remaining cold-create upload call sites from slow mutation paths.

## Decision 020 - Weather Snapshot Read Fence
Problem: Public ocean-provider methods could read `Weather[0]` for sea level or surface flow while `CalculateWaveParametersJob` was writing the same DTO. The editor tuner could also mutate wave/weather Vault buffers while the job owned them.
Solution: `SeaLevel` and `ResolveSurfaceFlow()` now return a main-thread cached snapshot refreshed after boot, job completion, CSV ingest, storm surge, and explicit weather mutation. Public weather/light writers use the existing non-blocking job fence before mutating or publishing. Static tuner reads/writes are rejected while `s_activeWaveParameterJobCount` is nonzero, and the editor window reports the active mutation lease instead of silently presenting stale control state.
Rejected Alternatives: Completing the job inside every provider read was rejected because physics queries could turn into hidden stalls. Returning to direct weather reads was rejected because read/write aliasing violates the Vault law under parallel scheduling.
Scalability potential: Low/Middle/High/Ultra visual math is unchanged; the cache prevents quality-tier independent data races.
Hardware Impact: Removes repeated weather DTO reads from provider sea/flow calls and avoids editor/runtime write races. Exact microseconds are unmeasured.
