# SHINOBU_219 Rationale

## Intake

Problem: Prior visual aging implementations are suspected to use per-renderer material mutation, dynamic decals, or spawned corrosion objects, which breaks SRP Batcher and adds managed/runtime hierarchy cost.
Solution: Execute fake-first rendering pipeline: one global GPU aging buffer, explicit 64-byte DTO lanes, procedural UberNoir blending, no gameplay truth ownership.
Rejected Alternatives: Unity `Renderer.material`, per-instance `Material.SetFloat`, decal projectors, material clones, and spawned damage GameObjects are rejected because they add draw calls, material state divergence, and heap/object overhead.
Scalability potential: Low uses cheap uniform blend/noise gate; Middle adds spatial procedural breakup; High adds richer per-pixel corrosion/fracture; Ultra spends saved CPU on heavier shader detail only through continuous `GlobalQualityWeight`.
Hardware Impact: Expected low-end i3/MX350 gain is reduced CPU submission and preserved SRP Batcher. Exact microseconds remain PENDING VERIFICATION until profiler data exists.

## Mandate Decision

Problem: Task mixes rendering, unmanaged data layout, AUP precision, and crash telemetry.
Solution: Read 8 mandates: zero-GC, ARM64 layout, AUP/floating origin, URP hot path, noir shader aesthetics, GPU sovereignty, telemetry, and cinematic fake-first.
Rejected Alternatives: Reading all 35 mandates was rejected as context waste; skipping mandates violates batch protocol.
Scalability potential: Mandates enforce continuous quality scalar and GPU-sovereign rendering across weak, middle, high, and ultra tiers.
Hardware Impact: Prevents material clone stalls and unaligned DTO reads; static estimate is 20-150 us CPU saved per 100 aged renderers versus per-instance mutation, PENDING VERIFICATION.

## Loop 1 - Sanitation And DTO

Problem: Base aging was coupled to authoring decal matrices and child GameObject activation, while no local `BaseCorrosion.cs` or `GlassFracture.cs` renderer-material mutation source existed.
Solution: Removed active authoring aging decal paths from `BaseDegradationSystem`; rupture VFX remains only as breach jets/fluid rupture feedback, while rust/crack appearance moves to UberNoir procedural shader data.
Rejected Alternatives: Keeping decal matrices hidden or toggling children inactive was rejected because it preserves hierarchy/decal architecture and can regress into SRP Batcher breaks.
Scalability potential: Low uses existing geometry with flat blended aging; Middle adds spatial masks; High and Ultra add richer shader crack/rust propagation without extra draw calls.
Hardware Impact: Removes three authoring child activation/transform/renderer writes per rupture and prevents persistent decal list rebuilds. Static gain estimate: 12-80 us per rupture event on i3/MX350 scenes with many modules; PENDING PROFILER VERIFICATION.

Problem: Visual aging GPU payload required stable ABI across x86 and ARM64 with no CS1612 property traps.
Solution: Added `VisualAgingParamsDTO` as explicit 64-byte layout with four `float4` lanes at offsets 0/16/32/48 and raw public fields only; added `ValidateLayout()` using `UnsafeUtility.SizeOf` plus editor offset checks.
Rejected Alternatives: Sequential layout and properties were rejected because Burst/GPU ABI could drift and property setters trigger copy-modify-write failure patterns.
Scalability potential: One 64-byte lane per module scales from 128 active visual entries on weak devices to 4096 entries on high/ultra without changing shader contract.
Hardware Impact: 64-byte lane aligns to cache lines and four GPU vector registers; expected ARM64 unaligned-read risk reduced to zero by construction. Runtime gain is correctness plus lower copy complexity, not a measured frame saving.

Problem: Shader testing could not wait for real corrosion state to accrue.
Solution: Added `GenerateMockAgingDataJob` to write deterministic high-depth/high-stress patterns into Vault-backed `VisualAgingParamsDTO`, driven by frame/hash seeds and mock temperature.
Rejected Alternatives: Editor-only managed test arrays and scene-spawned damaged modules were rejected because they do not exercise the zero-GC Vault/GPU upload path.
Scalability potential: Mock count obeys continuous `GlobalQualityWeight`, so toaster paths upload a reduced active subset while ultra paths exercise the full payload.
Hardware Impact: Mock job is Burst `IJobParallelFor`; static CPU estimate is 0.010-0.026 us per active entry, PENDING COMPILER/PROFILER VERIFICATION.

## Loop 2 - Kernel And GPU Upload

Problem: Structural integrity truth is owned by Agent 218; visual aging cannot invent gameplay state or couple directly to construction objects.
Solution: Added `ProcessAgingParametersJob` that opportunistically reads existing Vault `IntegrityStateDTO`, `StructuralIntegrityNodeAups`, and tuning buffers, then writes render-only `VisualAgingParamsDTO` coefficients. If structural buffers are locked or absent, the mock job feeds the same GPU path.
Rejected Alternatives: Direct `BaseModule` polling and per-component visual state were rejected because they create gameplay/render dependencies and managed traversal cost.
Scalability potential: Active count scales continuously from a reduced quality-budget subset to full 4096 entries; low, middle, high, and ultra differ by count and shader math weight, not separate code paths.
Hardware Impact: Burst estimate is 0.010-0.026 us per entry; 512 low-tier entries estimate 5.1-13.3 us, 4096 ultra entries estimate 41-106 us. PENDING PROFILER VERIFICATION.

Problem: GPU upload of hundreds/thousands of aging DTOs must not stall or allocate.
Solution: Added double-buffered `GraphicsBuffer` upload during dispatcher `VisualSync` using `LockBufferForWrite<VisualAgingParamsDTO>` and one `UnsafeUtility.MemCpy`, then bound `_GlobalBaseAgingParams` globally for the existing UberNoir pass.
Rejected Alternatives: `SetData`, per-renderer `MaterialPropertyBlock`, `Material.SetFloat`, and decal render passes were rejected because they add CPU submission work or draw/material divergence.
Scalability potential: Same buffer contract feeds all quality tiers; low uploads fewer entries, ultra uploads the full payload and spends saved draw-call budget on richer shader noise.
Hardware Impact: Upload payload is `64 * activeCount` bytes. Static memcpy estimate: 32 KB low path <10 us, 256 KB full path <80 us on desktop memory bandwidth; exact value PENDING PROFILER.

Problem: Rust and glass cracks need spatial growth without CPU geometry changes.
Solution: Added `_GlobalBaseAgingParams` consumption in UberNoir by `materialIndex`/instance ID, using localized AUP offsets, continuous quality-weighted growth masks, and procedural glass microfracture blending.
Rejected Alternatives: Texture swaps, decal projectors, spawned crack meshes, and binary low/high shader branches were rejected because they break batching or produce discontinuous visual quality.
Scalability potential: Low uses cheap line/triangle masks; middle adds procedural breakup; high/ultra increases noise detail and glass catchlight through `GlobalQualityWeight`.
Hardware Impact: Zero extra draw calls. GPU ALU increases per shaded pixel only; low-tier path avoids high-frequency noise. CPU savings dominate on i3/MX350 where draw-call/material churn is expensive.

## Loop 3 - Thermal, AUP, Rollback, Telemetry

Problem: Corrosion rate must react to environment while staying presentation-only.
Solution: `ProcessAgingParametersJob` samples `ThermodynamicsTemperatureFrontMirror` when available and falls back to a Vault mock temperature. Temperature only multiplies render coefficients; gameplay truth remains unchanged.
Rejected Alternatives: Owning thermodynamics state or writing back into structural integrity was rejected because it crosses authority boundaries.
Scalability potential: Temperature boost is a scalar multiply across all tiers; high/ultra only make the visual consequence richer in shader.
Hardware Impact: One float sample per active entry. Static additional CPU estimate under 1 us per 512 entries; PENDING PROFILER.

Problem: Shader noise needs world-stable positions but GPU floats cannot hold absolute AUP.
Solution: CPU subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` from node AUP, clamps to an 8192 m local window, and writes the localized `float3` into `DepthAndPressure.xyz`.
Rejected Alternatives: Passing absolute `double3`, reconstructing absolute world position in shader, or relying on runtime world float at 100 km scale were rejected due precision jitter.
Scalability potential: Same localized coordinate lane works across all quality tiers; quality changes noise detail only.
Hardware Impact: Double subtract/clamp/cast per entry is negligible versus avoiding visible phase jitter and shader-side precision failures.

Problem: Visual aging must not enter rollback/Merkle state.
Solution: Added only render-owned BufferIDs `71240-71246`; static scan found no `VisualAgingParamsDTO` or `VisualPressureAging*` references in networking rollback or save Merkle state. Runtime flag records `FlagNoRollbackState`.
Rejected Alternatives: Hashing visual aging into `StateRingBuffer` was rejected because transient presentation would create false desyncs and bandwidth cost.
Scalability potential: Network cost remains zero across weak through ultra devices; visuals continue smoothly after rollback.
Hardware Impact: Saves `64 * activeCount` bytes from rollback snapshots; full 4096 payload exclusion avoids 256 KB per snapshot.

Problem: Crash/NaN postmortem requires a fixed black-box ring.
Solution: Added 300-entry `VisualAgingTelemetryEntry` ring plus runtime DTO, recording active count, average stress, max depth proxy, active glass fracture count, upload bytes, CPU estimate, GPU upload microseconds, hashes, layout hash, and fault flags. Fault path dumps to `Docs/AgentLogs/Dump_SHINOBU_219.bin`.
Rejected Alternatives: Logging strings or relying on chat reports were rejected because they allocate and do not survive crash analysis.
Scalability potential: Telemetry samples a bounded budget based on quality; low tiers sample less, ultra samples more.
Hardware Impact: Single Burst telemetry job with bounded sampling; static estimate 16-256 samples per frame, below suspicious 0.1 ms threshold pending profiler proof.

## Loop 4 - Human Control Facades

Problem: Designers need pressure-aging control without recompiles or per-material mutations.
Solution: Added `VisualPressureAgingTunerWindow` UI Toolkit facade with sliders for rust stress, corrosion pressure, salt depth, biomass temperature, glass threshold, temperature boost, and quality noise scale. It writes the Vault tuning DTO through the runtime bridge and displays runtime active/upload/flag counters plus a compact editor graph.
Rejected Alternatives: Inspector-only serialized fields, `Material.SetFloat` debug sliders, and ScriptableObject recompiles were rejected because they either miss live tuning or reintroduce material state divergence.
Scalability potential: The same tuning fields drive low, middle, high, and ultra shader behavior through `GlobalQualityWeight`; no binary quality switch is introduced.
Hardware Impact: Editor-only allocations are cold. Hot path impact is one DTO read in existing dispatcher flow; expected runtime frame cost change is 0 us outside tuning changes.

Problem: Biome/water corrosion constants require external data while avoiding managed CSV token churn.
Solution: Added `Data/Visuals/environmental_aging_rules.csv` and a cold-path byte parser that reads into Vault `NativeArray<byte>` scratch, hashes ASCII keys with FNV-1a, and writes raw tuning fields.
Rejected Alternatives: `string.Split`, LINQ parsing, JSON deserialization, and ScriptableObject profile swaps were rejected because they allocate managed strings/objects or require import/recompile loops.
Scalability potential: Weak devices keep cheap scalar constants; high/ultra devices use the same constants to spend more shader detail on rust breakup and crack catchlights.
Hardware Impact: CSV load is capped to 4096 bytes and polled every 96 frames only in editor/development builds. Hot shipping path remains unaffected unless a profile changes.

Problem: Visual debugging cannot depend on the full graphics route or spawned helper objects.
Solution: Added `VisualPressureAgingGizmoVisualizer.OnDrawGizmos` and SceneView overlay that acquire the Vault aging buffer, cap display to 128 rings, and release the lock in `finally`.
Rejected Alternatives: Runtime marker prefabs and gizmo GameObject spawning were rejected because they add hierarchy noise and can hide data race issues.
Scalability potential: Designers can inspect low/middle/high/ultra parameter spread from the same DTO lanes; gizmo count is capped so editor remains usable on weak machines.
Hardware Impact: Editor-only. No player frame cost.

## Loop 5 - Validator, Self-Audit, Build Gate

Problem: The validator must prove aging-scope takeover without failing on unrelated project systems that still legitimately use materials or decals for other domains.
Solution: `Visual_Aging_Inquisition` writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`, counts whole-project material/decal references for visibility, and gates PASS only on `BaseDegradationSystem`/UberNoir aging scope: no aging material mutations, no authoring aging decal calls, global shader buffer present, layout valid.
Rejected Alternatives: Treating every unrelated `.material` in the project as SHINOBU failure was rejected because it would cross domain boundaries and generate false failures.
Scalability potential: Validator output remains static and deterministic across hardware tiers; it guards architecture rather than frame time.
Hardware Impact: Cold editor scan only. No runtime cost.

Problem: Documentation must record the cheat without pretending runtime proof exists.
Solution: Updated `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` with the visual pressure aging shader fake and explicitly labeled it static source route only.
Rejected Alternatives: Claiming profiler, Frame Debugger, GCMonitor, or build proof was rejected because the compile gate blocked build launch and no runtime capture was produced.
Scalability potential: Ledger records the low/middle/high/ultra scaling contract: same draw route, variable shader detail through continuous quality scalar.
Hardware Impact: Static documentation only.

Problem: Compile verification is required, but project protocol forbids build when CPU is under load or dotnet/csc is active.
Solution: Checked `dotnet`, `csc`, and `VBCSCompiler` processes: none active. Checked `\Processor(_Total)\% Processor Time`: 100%. Build was not launched.
Rejected Alternatives: Running `dotnet build` under CPU 100% was rejected because it violates the explicit HECTON-8 build gate.
Scalability potential: No hardware-tier claim depends on this missing build; all microsecond values remain static estimates until profiler proof.
Hardware Impact: Build/profiler status is PENDING. No fake benchmark numbers recorded.

Problem: `VisualPressureAgingRuntime` reads Agent 218 structural DTOs from a different assembly, so a missing asmdef reference would fail compile despite correct source code.
Solution: Added explicit `Hecton8.Habitat.Deformation` reference to `Hecton8.Graphics.Materials.asmdef`; usage remains read-only and Vault-buffer based.
Rejected Alternatives: Duplicating `IntegrityStateDTO`/`StructuralTuningDTO` locally or using reflection was rejected because it creates ABI drift or managed runtime cost.
Scalability potential: One assembly reference preserves the shared DTO contract across all quality tiers without runtime dependency discovery.
Hardware Impact: Compile-time dependency only. Runtime cost is 0 us.

Problem: The editor tuner uses `NativeArray` and `math` directly; asmdef references are not transitive through the runtime assembly.
Solution: Added `Unity.Collections` and `Unity.Mathematics` references to `Hecton8.Graphics.Materials.Editor.asmdef`.
Rejected Alternatives: Removing `math`/`NativeArray` from the editor code was rejected because it would either duplicate clamping helpers or hide the actual Vault DTO read path.
Scalability potential: Compile-time editor dependency only; no quality-tier effect.
Hardware Impact: Runtime cost is 0 us.

Problem: Newly added Unity C# assets need stable GUIDs for version control and deterministic references.
Solution: Added `.meta` files for `VisualPressureAgingRuntime`, `VisualPressureAgingGizmoVisualizer`, `VisualPressureAgingTunerWindow`, and `Visual_Aging_Inquisition`.
Rejected Alternatives: Letting Unity generate random metas later was rejected because it creates avoidable import churn.
Scalability potential: Import determinism only; no quality-tier effect.
Hardware Impact: Runtime cost is 0 us.

## Loop 6 - Ultra Polish Compile-Wall And Vault Descriptor Correction

Problem: The prior visual runtime consumed Agent 218 structural data through a direct `Hecton8.Habitat.Deformation` Runtime asmdef reference. That protects source convenience but violates compile-wall isolation for an Echelon 8 render consumer.
Solution: Move the shared structural read ABI (`StructuralIntegrityConstants`, `IntegrityStateDTO`, `StructuralTuningDTO`) into `Hecton8.Habitat.Deformation.Contracts` while keeping the namespace `Hecton8.Habitat.Deformation` so existing runtime/editor code keeps one type identity. `Hecton8.Graphics.Materials.asmdef` now references only the Contracts assembly for structural DTOs.
Rejected Alternatives: Local mirror DTOs were rejected because `GlobalDataVault` validates type identity through `ComputeTypeHash<T>()`, which includes `typeof(T).TypeHandle`; a mirror could fail `TryGetGenerationHandle<T>`/`TryResolveHandle<T>` under collection checks. Keeping the direct Runtime reference was rejected by the ultra-polish compile-wall mandate.
Scalability potential: No runtime quality-tier change; this preserves the same low/middle/high/ultra visual shader path while reducing assembly invalidation radius during parallel development.
Hardware Impact: Runtime frame cost is 0 us. Iteration impact is compile-scope reduction only; profiler proof is not applicable.

Problem: `VisualPressureAgingRuntime` persisted legacy pointer-bearing `VaultBufferHandle<T>` descriptors across frames, contradicting the SHINOBU_202 Vault generation-handle addendum.
Solution: Replace all SHINOBU_219 persistent handles with pointer-free `VaultGenerationHandle<T>` descriptors. Every Burst/job/render phase resolves method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`, then passes those local views directly into the job or GPU upload.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` was rejected because stale cached pointers can survive Vault generation churn. Persisting `NativeArray<T>` views was rejected because the Vault may relocate/refresh the backing memory between phases.
Scalability potential: Low devices resolve the same descriptors but process fewer active rows by `GlobalQualityWeight`; ultra devices process the full payload. The descriptor model does not introduce binary tier switches.
Hardware Impact: Eliminates stale pointer risk on ARM64/x86. Added metadata resolve calls are cold/phase-local and bounded; exact microseconds pending profiler.

Problem: After relocating structural DTOs to Contracts, the Habitat deformation editor assembly still only referenced the Runtime assembly, which may not expose transitive Contracts types to Unity's asmdef compiler.
Solution: Add a direct `Hecton8.Habitat.Deformation.Contracts` reference to `Hecton8.Habitat.Deformation.Editor.asmdef`.
Rejected Alternatives: Relying on transitive references was rejected because Unity asmdef compilation is not a safe proof surface for public type availability.
Scalability potential: Editor-only compile safety. No runtime quality-tier effect.
Hardware Impact: Runtime cost is 0 us.

Problem: Compile verification remains required but the hardware gate is closed.
Solution: Rechecked process and CPU gate. No `dotnet`, `csc`, or `VBCSCompiler` process was active; `Get-Counter '\Processor(_Total)\% Processor Time'` returned 100, so build launch remains forbidden.
Rejected Alternatives: Launching `dotnet build` under CPU 100% was rejected by project rule.
Scalability potential: No performance claim depends on build status. Static architecture proof only.
Hardware Impact: No compiler or runtime load added.

Problem: `ResolveGlobalQualityWeight()` previously saturated the Homeostasis scalar without first checking `math.isfinite`, allowing a NaN quality value to poison active-count math, telemetry, and the shader runtime vector.
Solution: Add a finite guard and collapse invalid quality to `0.0f` minimum-survival mode before `math.saturate`.
Rejected Alternatives: Falling back to `1.0f` was rejected because a fault state should shed load, not spend ultra-tier ALU. Leaving `math.saturate(NaN)` was rejected because NaN propagation violates the black-box/NaN vaccination mandate.
Scalability potential: Weak, middle, high, and ultra behavior remains continuous for valid values. Invalid input collapses to the cheapest safe visual route.
Hardware Impact: One finite check per phase access; expected cost is below measurement noise, exact profiler proof pending.

## Loop 7 - Vault Lock Hardening

Problem: The descriptor migration removed persistent Vault handles, but several cold/editor paths still resolved and mutated Vault rows without an explicit lock: editor tuning reads, default hydration, pending editor tuning writes, CSV scratch/tuning writes, and VisualSync runtime counter writes.
Solution: Add scoped `TryLockBuffer`/`TryUnlockBuffer` gates with `finally` release around those exact lanes. `TryReadEditorTuning` locks tuning plus runtime; `WriteDefaults` locks tuning, mock temperature, and runtime; `ApplyPendingEditorTuningImmediate` locks tuning; `MonitorCsv` locks CSV scratch plus tuning; `VisualSyncTick` locks runtime while writing upload counters/fault flags.
Rejected Alternatives: Leaving these as "cold path only" was rejected because editor facades and render sync can still overlap in Unity editor execution and corrupt presentation telemetry. Expanding locks to shader params in VisualSync was rejected because VisualSync only reads params after dispatcher ordering and gizmo reads are also read-only; adding an exclusive lock there would add avoidable frame-phase contention without protecting a write.
Scalability potential: Low, middle, high, and ultra quality routes remain unchanged. The locks guard the same continuous `GlobalQualityWeight` pipeline and do not introduce binary quality branches or alternate shader variants.
Hardware Impact: Player hot-path change is one runtime DTO Vault lock pair in VisualSync; editor/CSV/default locks are cold. No new managed allocations, no new persistent native arrays, no shader ABI change. Exact lock-pair cost pending profiler because the build gate remains closed.

Problem: Compile verification is still required after the lock hardening patch.
Solution: Rechecked CPU and compiler processes. CPU remained at 100 percent and no active `dotnet`, `csc`, or `VBCSCompiler` process was found.
Rejected Alternatives: Launching a build anyway was rejected by the explicit project rule forbidding build above 50 percent CPU.
Scalability potential: No runtime visual-scaling claim depends on this missing build. Static source proof only.
Hardware Impact: No compiler load was added to the developer machine.

## Loop 8 - Vault Acquisition Hot-Path Collapse

Problem: `EnsureVaultState` unconditionally called `GetGenerationHandle` for all seven SHINOBU_219 owned lanes on every phase entry. In current `GlobalDataVault`, `GetGenerationHandle` routes through `TryEnsureVaultBuffer`, which performs ownership/type metadata validation, length checks, optional grow, and finite-payload sanitization. That is cold acquisition work sitting on normal dispatcher phases.
Solution: Replace unconditional acquisition with `TryResolveOrAcquire<T>`. The helper first checks the cached 16-byte descriptor against `TryGetBufferGeneration`, requires the descriptor owner to remain `SystemID.GraphicsMaterials`, resolves it as a method-local view when current, refreshes from `TryGetGenerationHandle<T>` when the buffer already exists, and only calls `GetGenerationHandle<T>` on cold miss or undersized lane.
Rejected Alternatives: Keeping unconditional `GetGenerationHandle` was rejected because it spends Vault metadata/sanitize work every PreSimulation/Simulation/VisualSync call. Resolving stale descriptors first was rejected because `TryResolveHandle` records a generation fault and can dump a black-box for a normal hot-swap recovery path. Accepting nonzero descriptors without checking owner was rejected because a BufferID collision must fail closed.
Scalability potential: Low, middle, high, and ultra visual paths keep the same continuous quality math. This change reduces owner-lane bookkeeping before the quality-scaled jobs and GPU upload; it does not introduce a tier branch.
Hardware Impact: Avoids seven repeated `TryEnsureVaultBuffer` routes per phase when descriptors are current. Exact savings pending profiler; no build/profiler run because CPU gate remains above 50 percent.

Problem: Compile verification is still required after the acquisition patch.
Solution: Rechecked process and CPU gate. No `dotnet`, `csc`, or `VBCSCompiler` process was active; CPU was 93.636 percent.
Rejected Alternatives: Running a build under CPU 93.636 percent was rejected by project rule.
Scalability potential: Static source proof only; performance estimates remain unverified.
Hardware Impact: No compiler load added.

## Loop 9 - Shader Quality Branch Detox

Problem: The SHINOBU_219 aging shader functions still carried `_MATH_LOD_LOW` compile-time forks. Even if the wider UberNoir shader has legacy math LOD branches, the pressure-aging task requires continuous `GlobalQualityWeight` scaling inside its own rust and glass fracture math.
Solution: Remove `_MATH_LOD_LOW` forks from `H8UberNoirAgingGrowthMask` and `H8UberNoirApplyGlassMicroFracture`. Both functions now compute a cheap analytical mask first, derive a continuous `H8UberNoirSmoothRange01` detail weight from quality, and enter the richer noise branch only when the weight is nonzero. The final visual blend is still `lerp`-based, so no quality pop is introduced at valid scalar inputs.
Rejected Alternatives: Keeping compile-time forks was rejected because it makes SHINOBU_219 aging depend on shader variant state instead of the runtime quality scalar. Always evaluating rich noise was rejected because weak devices would pay high-tier ALU even when quality is below the detail window.
Scalability potential: Low quality uses weld/edge triangle masks and crack line masks. Middle quality blends in value-noise breakup. High and ultra quality use the same buffer rows to spend additional per-pixel noise and fracture branch detail without changing material or draw route.
Hardware Impact: Low-quality aging path now skips two value-noise taps in rust growth and two value-noise taps plus radial fracture work in glass when detail weight is zero. Exact GPU timing pending Frame Debugger/profiler.

Problem: Shader-side `H8UberNoirGlobalQualityWeight` treated non-finite `_H8GlobalQualityWeight` as `1.0`, contradicting the runtime NaN guard that falls back to minimum-survival mode.
Solution: Change the non-finite shader fallback to `0.0`.
Rejected Alternatives: Keeping fallback at `1.0` was rejected because a quality fault should shed ALU, not force visual overkill.
Scalability potential: Valid inputs still saturate continuously from 0..1; invalid input collapses to cheapest safe route.
Hardware Impact: One scalar fallback change, no new variant.

## Loop 10 - Vault Quality Payload Shader Binding

Problem: After branch detox, `H8UberNoirSampleSurface` still pulled the aging detail scalar from `H8UberNoirGlobalQualityWeight()` only. That allowed the wider UberNoir material quality route to override the quality value SHINOBU_219 uploads in `_GlobalBaseAgingRuntime.z` and in each `StressAndMicroFractures.w` lane.
Solution: Add `H8UberNoirVisualAgingQualityWeight`. It computes a finite base quality, finite runtime upload quality, finite lane quality, then blends toward the uploaded payload through `H8UberNoirSmoothRange01(0.0, 1.0, activeCount) * payloadEnabled`. `H8UberNoirSampleSurface` now uses this resolver for macro noise, rust growth, moss/salt blend, and glass fracture calls.
Rejected Alternatives: A hard `if (payloadEnabled) quality = runtimeQuality` was rejected because it creates a pop at payload activation. Leaving the broader quality resolver in place was rejected because it weakens Task 11's requirement that the visual pressure-aging shader consume the Vault scalar.
Scalability potential: Low keeps payload quality near 0 and the aging functions stay on cheap analytical weld/line masks. Middle blends into value-noise breakup as the smooth payload quality rises. High/Ultra use the same rows to spend extra per-pixel rust and glass detail without extra draw calls or shader keywords.
Hardware Impact: Adds finite checks, one smoothstep-style curve, and scalar lerps. It adds no texture samples, no new variants, no C# allocations, and no CPU-side renderer work. GPU timing remains PENDING Frame Debugger/profiler verification.

Problem: Shader payload lane sanitization still relied on `saturate` for loaded `float4` lanes. If a non-finite value entered the buffer, `saturate(NaN)` is not a sufficient proof surface for NaN vaccination.
Solution: Add `H8UberNoirFiniteSaturate4` and apply it to `RustAndCorrosion`, `SaltAndBiomass`, and `StressAndMicroFractures`. `DepthAndPressure.w` now falls back to `0.0` if non-finite. `DepthAndPressure.xyz` already used `H8UberNoirFinite3`.
Rejected Alternatives: Trusting CPU telemetry alone was rejected because black-box detection does not protect the active shader frame. Zeroing the entire payload on any single depth-lane fault was rejected because localized AUP xyz and pressure scalar have different fallback requirements.
Scalability potential: Faulty rows collapse to visible minimum-survival aging rather than spending ultra-tier ALU on poisoned values. Valid rows still scale continuously from low through ultra.
Hardware Impact: Extra finite checks are ALU-only and cheaper than NaN propagation into noise/fracture math. Exact GPU cost pending profiler; build/import not launched because CPU gate was 100 percent.

## Loop 11 - First Payload Upload Fence

Problem: `VisualSyncTick` computed `uploadCount` as at least one row even when `_activeCount` was still zero. The visual-aging params Vault lane is allocated with `NativeArrayOptions.UninitializedMemory`; therefore a first `VisualSync` before a confirmed Simulation/PostSimulation payload could bind `_GlobalBaseAgingRuntime.y=1` and upload one undefined 64-byte row.
Solution: Add `_hasGeneratedPayload`. It is set only in `PostSimulationTick` after a scheduled simulation payload has passed through the dispatcher phase and `_activeCount > 0`. `VisualSyncTick` now advertises active count and enabled state as `0/0` until that proof exists. It only calls `UploadNativeArray` when `_hasGeneratedPayload` and `uploadCount > 0`.
Rejected Alternatives: Relying on shader-side finite sanitization was rejected because a render route must not knowingly upload uninitialized CPU memory. Forcing `uploadCount=1` was rejected because it creates a fake first row that has no owner fact. Clearing the entire 4096-row params lane during defaults was rejected because only row zero is relevant before payload enable and full-lane clearing is cold bandwidth waste.
Scalability potential: Low, middle, high, and ultra paths remain continuous after payload generation. Before payload generation, the shader deliberately uses its default visual aging path and no payload availability bit, preventing a startup pop from undefined data.
Hardware Impact: Hot path adds one boolean gate and avoids one possible 64-byte stale upload. Cold default path locks params and writes one zero DTO. Exact runtime cost remains pending profiler.

Problem: Vault descriptor release did not explicitly invalidate the GPU payload readiness flag.
Solution: `ReleaseVaultHandles` now clears `_hasGeneratedPayload`, `_activeCount`, `_uploadedCount`, and marks `_agingDirty`.
Rejected Alternatives: Keeping the prior flag state after releasing generation descriptors was rejected because a later shader vector could advertise data whose Vault backing was already released or reacquired.
Scalability potential: Hot-swap and teardown now fail closed across all quality tiers.
Hardware Impact: Teardown/hot-swap only; no steady-frame cost.

Problem: Compile verification is still required after the upload-fence patch.
Solution: Rechecked CPU and compiler gate. CPU remained 100 percent and active compiler processes were present: `csc` and `dotnet`.
Rejected Alternatives: Running build/import under active compiler load was rejected by project rule.
Scalability potential: Static source proof only. No runtime/profiler claim is made.
Hardware Impact: No compiler load added by SHINOBU_219.

## Loop 12 - Hot Registry Lookup Fence

Problem: `ResolveVault()` could fall back to `GlobalRegistry.DataVault` whenever `_vault` was null. Because `PreSimulationTick`, `ScheduleSimulation`, `VisualSyncTick`, and pending tuning application call this resolver, a missing cache could trigger service-locator lookup inside dispatcher phases.
Solution: Change `ResolveVault` to accept `allowRegistryLookup=false` by default. Hot dispatcher phases use the default and fail closed when `_vault` is absent. Cold/editor bridge calls use `ResolveVault(true)`, and editor tuning write calls `ApplyPendingEditorTuningImmediate(true)`.
Rejected Alternatives: Leaving the fallback in the general resolver was rejected because it hides hot-path service lookup behind a helper name. Removing all registry fallback was rejected because editor/gizmo facades need cold recovery when runtime cache is not yet populated.
Scalability potential: Low through ultra rendering paths are unchanged; this protects phase discipline and avoids hidden service lookup in fault paths across all tiers.
Hardware Impact: Removes a possible registry lookup in hot phase fault cases. Adds one boolean branch in `ResolveVault`; steady-frame with cached `_vault` remains the same.

Problem: Compile verification is still required after the registry fence.
Solution: Latest gate snapshot showed CPU 59.044 percent and no compiler processes. Build/import was still not launched because CPU remained above 50 percent.
Rejected Alternatives: Running build under CPU 59.044 percent was rejected by project rule.
Scalability potential: Static source proof only; no runtime/profiler claim is made.
Hardware Impact: No compiler load added.

## Loop 13 - Gizmo Payload Readiness Fence

Problem: `TryAcquireAgingBufferRead` locked the visual-aging params lane and exposed `_activeCount` even when no simulation payload had been generated yet. Because the params lane is initialized with `NativeArrayOptions.UninitializedMemory`, the editor gizmo could draw stale or undefined rows after cold boot, Vault rebind, or release/reacquire.
Solution: Gate the gizmo read with `_hasGeneratedPayload`, matching the GPU upload readiness contract. Clamp the exposed `activeCount` to the resolved `NativeArray<VisualAgingParamsDTO>.Length` before returning the locked view.
Rejected Alternatives: Drawing row zero as a "preview fallback" was rejected because it creates a second visual fact outside the dispatcher-generated payload. Clearing all 4096 params rows for the gizmo was rejected because it spends cold bandwidth to hide a facade bug. Trusting shader-side sanitization was rejected because the gizmo reads CPU memory directly.
Scalability potential: Low, middle, high, and ultra visual behavior after payload generation is unchanged. Before generation the editor facade displays nothing, which is the only truthful state because there is no owner-produced presentation payload yet.
Hardware Impact: Editor-only route adds one bool branch and one integer min. Runtime GPU upload, Burst jobs, shader ALU, and Vault DTO layout are unchanged.

Problem: Compile verification is still required after the gizmo fence.
Solution: Rechecked CPU and compiler gate. CPU was 83.378 percent and no `dotnet`, `csc`, or `VBCSCompiler` process was active.
Rejected Alternatives: Running a build under CPU 83.378 percent was rejected by project rule.
Scalability potential: Static source proof only; no runtime/profiler claim is made.
Hardware Impact: No compiler load added.

## Loop 14 - Construction Crack Decal Surface Removal

Problem: `BaseDegradationSystem` still declared empty `GlobalCrackDecalMatrices` and `GlobalCrackDecalAtlasIndices` compatibility lists plus a dirty/rebuild API. Search showed no consumers, so the route was dead code, but the public internal surface still advertised dynamic crack decals from the Construction aging bridge.
Solution: Delete the two lists, reset clears, internal properties, dirty flag, and rebuild methods. Keep rupture truth, breach jet, fluid aftermath, pressure compression, parasite collapse, and module rupture latch behavior unchanged.
Rejected Alternatives: Keeping the empty shim was rejected because it invites a future renderer to reattach dynamic aging decals. Rewriting SHINOBU_149 `DynamicDecalVaultRuntime` was rejected because that owner handles hull impacts/fluid/scorch signals, not base visual pressure-aging, and touching it here would cross domain boundaries.
Scalability potential: Low through ultra visual pressure aging remains a single UberNoir shader route fed by Vault rows. Removing the dead list API reduces architecture ambiguity without adding a quality branch.
Hardware Impact: Removes two cold managed list allocations and a stale no-op rebuild route. No runtime frame-time saving is claimed because the lists were already empty; the gain is route hygiene and future regression prevention.

Problem: Compile verification is still required after the Construction crack-decal surface removal.
Solution: Rechecked CPU and compiler gate. CPU was 94.052 percent and no `dotnet`, `csc`, or `VBCSCompiler` process was active.
Rejected Alternatives: Running a build under CPU 94.052 percent was rejected by project rule.
Scalability potential: Static source proof only; no runtime/profiler claim is made.
Hardware Impact: No compiler load added.

## Loop 15 - Structural Profile Decal Atlas Residue Removal

Problem: `StructuralIntegrityProfile` still carried `DefaultRuptureDecalAtlasIndex`, a serialized `ruptureDecalAtlasIndex`, and a `RuptureDecalAtlasIndex` accessor. Source search showed no consumers. The field was authoring residue from the old crack-decal path and conflicted with the UberNoir procedural-aging route.
Solution: Remove the decal atlas constant, field, constructor parameter, accessor, and default values. Keep material variant, max unsupported span, and base HP authoring data intact. Update the tooltip to state that visual pressure aging is procedural in UberNoir.
Rejected Alternatives: Keeping an unused serialized decal field was rejected because it can be reconnected later without an architecture review. Replacing it with another visual enum was rejected because visual class is now inferred in `VisualAgingParamsDTO.SaltAndBiomass.w`/stress lanes, not owned by the structural profile.
Scalability potential: Low through ultra visual aging stays on the same continuous Vault-to-UberNoir path. The structural profile no longer contains a parallel authoring route for crack/rust atlas selection.
Hardware Impact: Removes one unused serialized int per variant and one unused source API. Runtime frame-time impact is not claimed because no consumer existed.

Problem: Compile verification is still required after the profile cleanup.
Solution: Compile gate remains closed from the last sample: CPU was 94.052 percent and no compiler processes were active.
Rejected Alternatives: Running a build under CPU 94.052 percent was rejected by project rule.
Scalability potential: Static source proof only; no runtime/profiler claim is made.
Hardware Impact: No compiler load added.
