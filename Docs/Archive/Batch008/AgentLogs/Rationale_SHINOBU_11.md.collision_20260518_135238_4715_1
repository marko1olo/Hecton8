Date: 2026-05-17
Agent: SHINOBU_11
Domain: SUBMARINE_MASS_AND_BUOYANCY_SOLVER
Status: ACTIVE - PENDING VERIFICATION

## Session Initialization
Problem: SHINOBU_11 status/rationale files were absent; the batch protocol requires disk-backed memory before work proceeds.
Solution: Created fresh status and rationale files with all 20 tasks unchecked and the selected mandate set marked pending.
Rejected Alternatives: Chat-only tracking rejected; context compaction would erase state. Reusing another SHINOBU status file rejected; cross-agent contamination.
Scalability potential: Disk-backed checklist keeps low-end and high-end behavior irrelevant to process state; no runtime impact.
Hardware Impact: 0 us runtime impact; editor/documentation-only filesystem write.

## Pending Decision Slots
- Mandate read decisions: Selected and scanned eight mandates: submarine AUP kinematics, deterministic physics, fluid incursion, cinematic fake first, zero-GC, native memory/job discipline, telemetry black box, and AUP/floating-origin precision.
- Existing architecture fit: PENDING
- Submarine dynamics implementation shape: PENDING

## Mandate Selection Rationale
Problem: SHINOBU_11 touches physics authority, AUP precision, flood mass, hot-path Burst jobs, and crash telemetry.
Solution: Bound implementation to eight mandates that directly govern those surfaces. The solver must be pure math/Burst over unmanaged buffers, with mocked signals where peer systems are unavailable.
Rejected Alternatives: Reading generic rendering/audio mandates rejected; no direct runtime ownership. Using Unity Rigidbody/Joint/ForceMode doctrine as implementation rejected; prompt forbids Rigidbody and mandates kinematic DTO integration.
Scalability potential: Low tier uses 1D drag LUT, low-cadence PID/CoM, and scalar flood ratios. Middle adds steady 60Hz local integration. High adds richer cavitation/slosh signals. Ultra spends saved cycles on visual/debug overkill, not more authoritative fluid truth.
Hardware Impact: Expected low-end gain versus per-polygon displacement/PhysX is approximately 35-90 us per active submarine; exact profiler proof absent.

## SELF_AUDIT Before Code
<SELF_AUDIT>
1. Rigidbody/AddForce usage: No new Rigidbody, ConfigurableJoint, ConstantForce, or AddForce path is permitted. Existing PhysX submarine scripts remain legacy neighbors; SHINOBU_11 writes a separate vault-owned kinematic lane.
2. ARM64 layout: SubmarineStateDTO final layout is double3 AUP at bytes 0-23, quaternion 24-39, float3 local/linear/angular/CoM/CoB/inertia from 40-111, scalar state 112-143, explicit padding to 192 bytes. No Pack=1 on SHINOBU DTOs.
3. CS1612: DTOs expose public fields only. Mutating access uses direct vault handles and an unsafe ref method over the underlying buffer pointer.
4. Mock isolation: Flood, impact, and cavitation payloads are local unmanaged structs with local NativeQueue lanes. Existing SignalBus snapshots are optional consumers, not hard dependencies.
5. Human facade: Submarine Dyno-Tuner editor window will write config floats directly to GlobalDataVault and draw CoB/CoM/thrust vectors.
</SELF_AUDIT>

## Archaeology Probe
Problem: Task 01 requires locating submarine_mass_profiles.h8bin or hydro_drag_constants.bin plus any Rationale_*.md struct.pack hints.
Solution: Ran full filesystem search against Docs/Archive and StreamingAssets and searched Docs/AgentLogs for struct.pack/submarine hydro constants. No files or format hints were found.
Rejected Alternatives: Blocking on absent OSHINO binaries rejected. Inferring a complex legacy format rejected; that would be fake archaeology.
Scalability potential: Emergency mock profile keeps low tier alive with aligned defaults; high tier can replace the same vault buffers with real OSHINO constants later.
Hardware Impact: Boot-time only. Runtime impact 0 us; fallback avoids a dead initialization branch.

## Existing Architecture Fit
Problem: The current submarine runtime contains PhysX-backed scripts, including Rigidbody-dependent control surfaces, while SHINOBU_11 explicitly forbids Rigidbody authority.
Solution: Added a separate GlobalDataVault-backed kinematic lane under `Assets/_Project/Scripts/Physics/Vehicles/` instead of ripping legacy public components out mid-batch. Presentation sync writes a Transform in LateFrame only after the Burst job completes; authority remains in vault DTOs.
Rejected Alternatives: Rewriting `SubmarineFluidDynamics.cs`/station-keeping scripts rejected because it would break unknown public callers and collide with concurrent agents. Adding a wrapper around Rigidbody rejected because it leaves determinism inside PhysX.
Scalability potential: Low tier runs scalar Dear-Lie drag/PID/CoM and throttles slow solvers on SystemHealth pressure. Middle runs full 60Hz solver. High/Ultra can consume the same cavitation/thrust/telemetry buffers for visual wake and hull effects without changing gameplay truth.
Hardware Impact: Expected i3/MX350 gain versus PhysX/per-polygon buoyancy is 35-90 us per active submarine. This is an estimate; compile wall prevented profiler proof.

## Submarine Dynamics Implementation Shape
Problem: SHINOBU_11 requires 6D kinematics, mass/flood/cargo CoM, PID ballast, cavitation, gyro stabilization, collision impulse, slosh, and black-box telemetry without owning private NativeArrays.
Solution: Implemented `Submarine6DIntegratorJob` as stateless Burst math over vault-resolved NativeArray views: state, controls, PID, mass properties, force accumulator, config, drag LUT, and telemetry ring. Local NativeQueues are only mock/signal lanes and are registered with NativeMemorySentinel.
Rejected Alternatives: Managed component state, per-room fluid loops, Rigidbody forces, and same-lane Schedule().Complete() rejected. Completion uses `DispatcherJobSwap.TryComplete` in PostFixed.
Scalability potential: Toaster path uses 1D LUT drag, 1D slosh oscillator, O(1) CoM, and 15Hz PID/CoM under thermal pressure. Ultra path keeps gameplay math identical and spends saved cycles on debug/visual consumers.
Hardware Impact: Hot-path entity cost target is <50 us fleet. Telemetry cost marker is deliberately `0f` until a profiler artifact exists; earlier nonzero microsecond wording is superseded and must not be treated as measured proof.

## Alignment And CS1612 Decision
Problem: ARM64/Quest can stall or fault on Pack=1 runtime DTOs, and NativeArray value properties cause CS1612/copy bugs.
Solution: SHINOBU DTOs use `StructLayout(LayoutKind.Sequential, Size = N)` with cache-line-aligned hot sizes 64/128/192, public fields, explicit padding, and `SubmarineKinematicAccess.GetStateRef` over the vault handle pointer. Existing `DynamicFloodMassContracts` Pack=1 was removed in the assigned physics-contract domain.
Rejected Alternatives: Leaving existing Pack=1 flood DTOs rejected because they are in the assigned mass/flood interface. Private properties rejected because they create stack-copy mutation paths.
Scalability potential: Predictable 8-byte multiples keep low-end ARM64 memory behavior stable and let high-end Burst vectorize without branchy defensive copying.
Hardware Impact: Prevents unaligned-access regressions; exact microsecond gain not measured.

## Dear Lie Hydrodynamics
Problem: Real displacement, slosh particles, and cavitation bubbles would burn CPU while adding little controllable gameplay value.
Solution: Drag is `speedSq -> 16-sample LUT -> force opposite velocity`. Buoyancy uses one center-of-buoyancy offset and a cubic ease against target depth. Slosh is a 1D spring over flood CoM. Cavitation is a scalar depth/throttle/speed index that stutters thrust and emits a local acoustic signal.
Rejected Alternatives: Navier-Stokes, per-polygon buoyancy, and particle slosh rejected as academic simulation.
Scalability potential: Low tier fakes mass with a few scalar ops. Ultra tier can turn cavitation and force buffers into visual overkill without modifying the solver.
Hardware Impact: Estimated 20-80 us saved per active vessel versus rich CPU hydrodynamics; unmeasured.

## Compile Wall Evidence
Problem: Core compile verification is dirty under concurrent agents and Unity Temp volatility.
Solution: Restored missing generated build intermediates, ran `dotnet restore`, added existing `VolumetricSiltContracts.cs` to the generated Core project because `HectonMarineSnowRenderer.cs` already depends on its DTOs, then reran Core build.
Rejected Alternatives: Fixing `SpatialAudioManager.cs` ambiguity rejected; audio SDF namespace collision is outside SHINOBU domain. Reverting SHINOBU code rejected because compiler did not name SHINOBU files.
Scalability potential: No runtime effect. Build-bridge include reduces false compile blockers.
Hardware Impact: 0 us runtime. Current compile blocker: `SpatialAudioManager.cs` ambiguous `MockSDFSampler`; SHINOBU-owned files not named.

## Polish Correction
Problem: Concurrent agents extended `BufferID` after SHINOBU_11 added submarine buffer IDs; a stale `VaultBufferContract.MaxBufferId` reference briefly targeted an ID that was no longer current-disk truth.
Solution: Re-read `H8Memory.cs` during that pass and set MaxBufferId to the then-highest present enum value. Reran Core build. Later post-compaction reconciliation superseded this with `ShinobuInventoryDumpScratch = 70140`; current-disk reconciliation supersedes both with `FloraGenomeCsvScratch = 70502`.
Rejected Alternatives: Pinning MaxBufferId to `SubmarineKinematicDragLut` rejected because it would hide later valid buffers. Targeting a vanished thermodynamics ID rejected after current-disk verification.
Scalability potential: Maintains vault buffer contract validity for all current agents; no runtime math impact.
Hardware Impact: 0 us runtime. Core compile succeeded during this pass with 0 errors and 3 unrelated VFX warnings before later concurrent churn.

## Editor Build Boundary
Problem: The editor project could not reach source compilation because generated third-party/editor metadata DLLs are absent in `Temp/bin/Debug`.
Solution: Recorded the failure as generated-reference state, not SHINOBU source failure. The Core assembly containing the runtime and DTOs compiles.
Rejected Alternatives: Creating fake third-party DLLs or altering editor references rejected.
Scalability potential: Editor-only; no runtime effect.
Hardware Impact: 0 us runtime.

## Current Vault High-Water Reconciliation
Problem: Older SHINOBU notes recorded `ShinobuInventoryDumpScratch` as the highest visible `BufferID`, but current disk truth has moved again under concurrent agents.
Solution: Re-read `H8Memory.cs`; the current visible high-water mark is `FloraGenomeCsvScratch = 70502`, and `VaultMemoryContracts.MaxBufferId` already targets `BufferID.FloraGenomeCsvScratch`. No code overwrite was applied.
Rejected Alternatives: Reverting `MaxBufferId` back to a SHINOBU-owned enum rejected because it would break newer valid vault IDs. Editing unrelated Flora enum ownership rejected.
Scalability potential: Keeps DataVault range validation compatible with current shared memory IDs.
Hardware Impact: 0 us runtime.

## Literal Mock Fluid Density Reconciliation
Problem: Task 05 explicitly required a local `MockFluidDensityGenerator`, but the first implementation only seeded `FluidDensityKgPerM3` with a constant and then treated density as static. The blackbox also wrote a nonzero `EstimatedCostUs` placeholder, which looked like a measured microsecond value without profiler proof.
Solution: Added `MockFluidDensityGenerator` with deterministic Burst-compatible density sampling: low tier uses base density plus depth compression only, higher tiers add a tiny deterministic micro-layer bias. `SubmarineDynamicsRuntime` consumes the existing `FluidDensityChangedSignal` latest-state bridge when present and otherwise uses the mock fallback. Telemetry now writes `EstimatedCostUs = 0f` until a profiler artifact exists.
Rejected Alternatives: Direct dependency on the unseen fluid solver rejected. Reusing the existing `FluidDensityChangedSignal` as the only source rejected because Task 05 requires isolated execution. Keeping the nonzero telemetry estimate rejected as fake evidence.
Scalability potential: Low tier gets one scalar density expression. Middle/high get slightly richer deterministic fluid layering without changing gameplay truth. Ultra visual systems can still consume cavitation/force buffers for wakes without increasing authoritative hydro cost.
Hardware Impact: One scalar sample per simulated submarine in the Burst job; measured microsecond proof absent.

## Post-Density Compile Wall
Problem: After adding the literal mock density generator, a focused Core build was needed but the previous shared latest log path was locked by another process.
Solution: Wrote a unique build artifact at `Docs/AgentLogs/Build_SHINOBU_11_20260518_000329.log`. The build reached C# and failed outside SHINOBU in `VoxelDeltaProcessor.cs`: missing `IDataVault`, missing `VaultBufferHandle<>`, and duplicate `StructLayout` attributes. A filtered scan of that log found no SHINOBU-owned source names.
Rejected Alternatives: Editing `VoxelDeltaProcessor.cs` rejected as outside submarine mass/buoyancy authority. Reusing the locked latest log path rejected because it would produce a false verification artifact.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.

## Current Disk Compile Drift
Problem: A post-success Core rebuild hit new unrelated errors after concurrent source churn.
Solution: Re-ran Core build and recorded the current blockers: `HectonSeismicTideDirector.cs` lacks `ILateFrameTickable.LateFrameTick()` and `MockNarrativeTriggerSignal`, while `PowerGridManager.cs` lacks `ShinobuLogisticsRouter`. SHINOBU files were not named.
Rejected Alternatives: Editing environment seismic or power logistics domains rejected as architectural boundary violation.
Scalability potential: No SHINOBU runtime effect.
Hardware Impact: 0 us runtime.

## Post-Compaction Contract Reconciliation
Problem: Concurrent agents extended `BufferID` again and reverted `VaultBufferContract.MaxBufferId` below current enum truth, leaving legal high-range vault buffers outside the contract.
Solution: Re-read current `H8Memory.cs` during that historical pass; the highest visible enum value was `ShinobuInventoryDumpScratch = 70140`, so `MaxBufferId` then targeted `BufferID.ShinobuInventoryDumpScratch`. This entry is historical only; current-disk truth later moved to `FloraGenomeCsvScratch = 70502`. SHINOBU buffer IDs remain 587-594.
Rejected Alternatives: Keeping `VaultSharedTransformMatrices` rejected because it excludes SHINOBU and newer agent buffers. Guessing `SaveWorldPagerHotState` rejected after disk truth showed newer IDs.
Scalability potential: Keeps DataVault range checks valid across low/high tier systems without direct cross-domain references.
Hardware Impact: 0 us runtime; prevents boot/registration faults.

## Latest Compile Wall
Problem: After the contract correction, `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` still fails before SHINOBU source is named.
Solution: Recorded current external blockers: `VRSomaticRuntimeBootstrap.cs` missing `SomaticKinematicsRuntime`, `GlobalWorldSampler.cs` readonly mutation, `BinaryLayoutManifest.cs` missing ambient/ecosystem DTOs, `HectonSeismicTideDirector.cs` missing seismic jobs/signals/fields, and `EcosystemRuntimeInstaller.cs` missing `ShinobuEcosystemBalancer`.
Rejected Alternatives: Editing somatic, ecosystem, seismic, or manifest domains rejected; this is a compile wall outside the submarine mass/buoyancy domain.
Scalability potential: No SHINOBU runtime effect.
Hardware Impact: 0 us runtime.

## Ultra-Polish L1 And NaN Pass
Problem: The first SHINOBU DTO layout met 8-byte alignment but did not respect L1 cache-line stride. `SubmarineKinematicState` was 160 bytes, `SubmarineMassProperties` 96 bytes, and `SubmarinePidState` 48 bytes. Parallel writes over those arrays can straddle cache lines unpredictably. NaN detection also set a flag but still risked writing non-finite state into Vault and telemetry.
Solution: Padded hot DTOs to cache-line multiples: state 192 bytes, mass 128 bytes, PID 64 bytes, cavitation signal 64 bytes, flood room sample 64 bytes, flood result 128 bytes. Added safe finite/positive helpers, quaternion normalization by guarded `rsqrt`, double3 AUP sanitization, and a fatal-state fallback that writes finite identity/zero authority before Vault commit while preserving the fatal flag for the 300-frame blackbox.
Rejected Alternatives: Leaving 8-byte-only layout rejected; it satisfies ARM64 alignment but not L1 stride. Logging NaN without fallback rejected because it keeps poison in the pipeline. Full SoA split rejected during this pass because it would mutate public buffer IDs and create a broader compile wall under active concurrent churn.
Scalability potential: Low tier gets stable contiguous strides and low-cadence math without LOD flicker; high/ultra keep richer visual consumers fed from finite force/cavitation/telemetry data.
Hardware Impact: Prevents false-sharing and NaN propagation risk; measured profiler proof absent.

## Ultra-Polish Scheduler And Hysteresis Pass
Problem: Signal consumption wrote Vault arrays before taking the Vault locks, and the integrator scheduled with batch size 1. Thermal math LOD could also flip immediately with `SystemHealthIndexSignal`, which violates the 2-3 second hysteresis rule and risks VR discomfort.
Solution: Moved `LockSimulationBuffers()` before `ConsumeSignals()`, switched the IJobParallelFor inner-loop batch to `SubmarineDynamicsConstants.IntegratorBatchSize = 4`, and added `SubmarinePidState.LowLodHoldSeconds` with a 2-second hold for low math LOD.
Rejected Alternatives: Per-index scheduling rejected after L1 audit. Immediate LOD switching rejected because frame-to-frame physics cadence flicker is visible in VR.
Scalability potential: MX350 gets cheaper low-cadence PID/CoM under pressure with deterministic hysteresis; RTX path keeps full cadence after pressure clears for at least the hold window.
Hardware Impact: Reduced scheduling overhead and LOD thrash risk; measured microseconds absent.

## Current Batch Prompt Reconciliation
Problem: One preflight extraction pass transiently failed to find `<AGENT_PROMPT id="SHINOBU_11">`, but a fresh disk read now shows the XML block present in `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Re-extracted the full SHINOBU_11 block and verified exact task markers `Task 01:` through `Task 20:`. The surviving status matrix matches the current XML assignment, so no neighboring agent prompt was used.
Rejected Alternatives: Treating the transient missing block as permanent rejected after current disk truth changed. Reading neighboring current-batch prompts rejected as cross-agent contamination.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime.

## Current Session Pack1 Eradication
Problem: The ultra-polish ARM64 scan found two forbidden runtime `Pack = 1` declarations in adjacent vehicle automation DTOs: `ActiveSplineData` and `DockingSplineSample` inside `Physics/Vehicles/Automation/DockingAutopilotService.cs`.
Solution: Removed `Pack = 1` from both `StructLayout(LayoutKind.Explicit, Size = N)` declarations. Their `FieldOffset` values and sizes remain unchanged, so ABI offsets are stable while the forbidden pack directive is gone.
Rejected Alternatives: Leaving the violation because it was adjacent to SHINOBU rejected; the file is in the same vehicle physics runtime tree and the edit is a two-line layout hygiene fix. Rewriting the autopilot architecture rejected as outside the mass/buoyancy task.
Scalability potential: Low-tier ARM64/Quest avoids a forbidden layout declaration in vehicle runtime; high-tier behavior is unchanged.
Hardware Impact: Prevents an alignment-risk class; exact microsecond gain not measured.

## Current Verification Boundary
Problem: Latest Core verification is blocked outside the submarine domain after a successful restore.
Solution: Ran `dotnet restore Hecton8.Core.csproj --ignore-failed-sources` successfully, then reran Core build. The build fails in `VoxelDeltaProcessor.cs` on missing `IDataVault`/`VaultBufferHandle<>` and duplicate `StructLayout` attributes, with duplicate-source warnings for `HectonPhysicsContract.cs` and `GlobalTelemetryBus.Blackbox.cs`. No SHINOBU-owned source file is named.
Rejected Alternatives: Editing voxel delta/persistence code rejected as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER` and likely another agent's concurrent churn. Claiming clean compile rejected.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime.

## Current-Disk Hot-Path DI Correction
Problem: `SubmarineDynamicsRuntime.FixedTick()` could call `EnsureVaultBuffers()` when `_buffersReady` was false. That cold helper can resolve `GlobalRegistry.DataVault`, which violates the hot-path dependency-cache mandate even though the normal path is initialized in `OnEnable`.
Solution: Changed `FixedTick()` to return immediately when buffers are not ready. Cold recovery remains in `OnEnable` and `SlowTick`, so the fixed-step solver no longer performs registry/Vault acquisition work.
Rejected Alternatives: Leaving the fallback in `FixedTick` rejected because it hides a dependency lookup inside a physics cadence method. Resolving through another service locator rejected for the same reason.
Scalability potential: Low tier avoids rare fixed-step stalls during delayed bootstrap or Vault churn; high/ultra behavior is unchanged.
Hardware Impact: Runtime microsecond gain is unmeasured; the value is removal of a cold dependency path from the fixed-step lane.

## Current-Disk Vault High-Water Repair
Problem: Current `VaultMemoryContracts.MaxBufferId` had regressed to `BufferID.VaultSharedTransformMatrices` while `H8Memory.cs` now contains higher legal ids including `FloraGenomeCsvScratch = 70502` and SHINOBU submarine buffers 587-594. That can reject legal Vault registrations.
Solution: Set `MaxBufferId` back to the current visible high-water `BufferID.FloraGenomeCsvScratch` and documented that the contract must not be narrowed to one owner range.
Rejected Alternatives: Pinning to `SubmarineKinematicDragLut` rejected because newer peer ids above SHINOBU are legal current-disk truth. Leaving `VaultSharedTransformMatrices` rejected because it is below active BufferID values.
Scalability potential: Keeps shared DataVault range validation compatible with concurrent low/high tier systems without direct assembly coupling.
Hardware Impact: 0 us hot path; prevents initialization/registration faults.

## Current Compile Wall - Physics Culling
Problem: After restore and the SHINOBU corrections, `Hecton8.Core.csproj` now fails in `GlobalPhysicsStateManager.cs` on missing `WakeRequestSignal`. Source scan shows an incomplete external `Shinobu37` physics-culling partial: missing `QueuePhysicsWakeRequest`, `FlushPhysicsWakeRequests`, and multiple `Shinobu37PhysicsCulling*` helpers.
Solution: Recorded the blocker as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER`. No no-op signal or fake culling queue was added, because that would hide an incomplete culling system behind a compile-only patch.
Rejected Alternatives: Adding a dummy `WakeRequestSignal` rejected because the next missing methods would still leave behavior undefined. Editing the whole global physics culling overseer rejected as outside SHINOBU_11 ownership and already modified by another agent.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.

## Current Impact Corridor Correction
Problem: The external `DeferredSubmarineImpactSignal` carries a local hit point and relative impact speed, not a world-space normal and not a Newton-second impulse. SHINOBU_11 was consuming `LocalPoint` as a world normal and feeding speed directly into the kinematic impulse path, which could push the vessel in the wrong direction and make gyro suppression unreachable for real impacts.
Solution: Added a SHINOBU-local impact-normal flag and converted deferred impact speed into a bounded impulse using current dry mass plus trauma/integrity severity. Deferred impacts now derive a local outward normal from `-LocalPoint`, transform that normal by the current submarine rotation inside the Burst integrator, and retain point/normal data from the strongest signal in a frame instead of mixing max magnitude with the last weak normal. Mock impacts still use their supplied world normal.
Rejected Alternatives: Changing `DeferredSubmarineImpactSignal` in `GlobalSignals.cs` rejected because that signal file is owned by the global signal corridor and currently contains broad external Pack=1 debt. Adding a new one-off SHINOBU impact signal rejected because an existing corridor already exists. Treating relative speed as impulse rejected because it under-scales large submarine collisions by mass.
Scalability potential: Low tier keeps a scalar conversion and one normal transform only on impact frames. Middle/high/ultra can turn the same corrected impulse and cavitation buffers into richer audio/VFX without changing gameplay truth.
Hardware Impact: 0 us steady-state when no impact signal is present; impact-frame cost is a few scalar operations and one quaternion-vector multiply, unmeasured.

## Current CSV And AUP Display Tightening
Problem: The CSV bridge opened edited files exclusively through `File.OpenRead` and had no file-size cap. The editor facade also cast absolute AUP directly to `Vector3` for display, which violates the AUP mental model even if editor-only.
Solution: CSV override reads now use shared read/write access, sequential scan, and a 4096-byte maximum before parsing. The editor window now displays `AUP Local Delta` by subtracting `LocalOriginAup` before casting to `Vector3`.
Rejected Alternatives: A full background MMF CSV worker rejected for this pass because Task 19 is a designer hot-reload bridge, not runtime WAL storage, and adding a worker would introduce new lifecycle/thread ownership without Unity runtime proof. Leaving absolute AUP display rejected because it trains the wrong coordinate habit.
Scalability potential: Low tier avoids large accidental CSV parse stalls; editor tuning remains human-readable. High/ultra behavior unchanged.
Hardware Impact: 0 us fixed-step impact; cold SlowTick file check remains `PENDING VERIFICATION` without profiler proof.

## Current Compile Retry Boundary
Problem: First Core build after the impact polish caught a SHINOBU-owned compile error: `SubmarineDynamicsRuntime.cs` used `math.min` on byte inputs, and Unity.Mathematics overload resolution made that ambiguous.
Solution: Converted `traumaLevel` and `integrityDelta` to `int` before clamping. Reran Core build and filtered the retry log for SHINOBU-owned source names.
Rejected Alternatives: Ignoring the local compile error rejected. Editing `SubtitleManager.cs` or `GlobalPhysicsStateManager.cs` rejected as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER`.
Scalability potential: No runtime design change; this is compile hygiene.
Hardware Impact: 0 us runtime. Retry build still fails externally in `SubtitleManager.cs` missing `SubtitleSignal`/`SignalBus` visibility and `GlobalPhysicsStateManager.cs` incomplete `Shinobu37` physics-culling helpers; no SHINOBU-owned file is named.

## Post-Compaction Prompt And Compile Verification
Problem: One post-compaction prompt extraction command was incorrectly escaped and returned the neighboring `SHINOBU_01` block. Treating that output as authoritative would violate the strict parsing protocol.
Solution: Discarded the bad extraction result, reran the CLI extraction with the correct `SHINOBU_11` tag, and verified current disk truth: lines 566-619 contain exactly 20 tasks for `SUBMARINE_MASS_AND_BUOYANCY_SOLVER`. Re-ran static source scans and Core compile verification afterward.
Rejected Alternatives: Using the accidental neighboring prompt output rejected. Continuing from chat memory alone rejected.
Scalability potential: No runtime effect; this protects task boundaries under 20+ concurrent agents.
Hardware Impact: 0 us runtime.

## Current Compile Wall - Localization Signal
Problem: The latest focused Core build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_finalcheck.log` and failed in `LocRegistry.cs(404,55)` because `ISignal` is not visible/resolved. This is outside the submarine mass/buoyancy domain.
Solution: Filtered the finalcheck log for `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, and `SHINOBU_11`; no SHINOBU-owned source names were found. Recorded the blocker instead of editing localization or global signal ownership.
Rejected Alternatives: Adding a fake `ISignal` or editing `LocRegistry.cs` rejected; that would mask a signal-corridor ownership problem and create cross-domain coupling.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.

## Cold Binary I/O Pressure Polish
Problem: The CSV bridge already used shared sequential file access, but the legacy `submarine_mass_profiles.h8bin` and `hydro_drag_constants.bin` archaeology readers still used `File.OpenRead`. Those readers are cold boot paths, not fixed-step paths, but exclusive-style opening is still brittle while OSHINO generators or editors may rewrite files.
Solution: Replaced both `File.OpenRead` calls with `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan)`. Re-ran the code-only ban scan with `File.OpenRead` included; it returned no SHINOBU matches.
Rejected Alternatives: Building a background MMF worker for boot-time binary archaeology rejected; Task 01 needs robust fallback parsing, not a runtime WAL pipeline. Leaving the calls because they are cold rejected because the change is isolated and lowers tool contention risk.
Scalability potential: Low-tier storage avoids unnecessary exclusive read contention during boot/slow tuning. High/ultra behavior is unchanged.
Hardware Impact: 0 us fixed-step impact; boot/cold I/O only, unmeasured.

## Current Compile Wall - Subtitle Signal
Problem: After the cold I/O polish, `Docs/AgentLogs/Build_SHINOBU_11_20260518_finalcheck2.log` fails outside SHINOBU in `SubtitleManager.cs(737/741)` because `SubtitleSignal` and `SignalBus` are unresolved.
Solution: Filtered the finalcheck2 log for SHINOBU source names and domain paths; no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, or `SHINOBU_11` errors were found.
Rejected Alternatives: Editing UI subtitle or global signal contracts rejected; that is not `SUBMARINE_MASS_AND_BUOYANCY_SOLVER` and would create cross-domain compile coupling.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.

## Mock Signal Gating And Cavitation Corridor
Problem: The fallback `MockFloodSignalSeederJob` was scheduled every fixed tick, which is useful for isolated breach testing but unacceptable as a production default because it can randomly flood a live submarine. Cavitation events were also dequeued and discarded after the Burst job, leaving Task 11's acoustic feedback trapped inside a local mock queue.
Solution: Added inspector-gated `enableMockSignals` so the mock breach seeder is opt-in. Post-fixed cavitation drain now reconstructs AUP as `LocalOriginAup + LocalPosition`, validates finite values, and publishes an existing `AcousticPingSignal` on `ChannelMetalStress` instead of inventing a SHINOBU-only audio signal.
Rejected Alternatives: Always-on random breach rejected because it mutates gameplay truth without an explicit test toggle. Creating a new cavitation global signal rejected because `AcousticPingSignal` already exists in the signal corridor. Publishing from inside the Burst job rejected because GlobalSignals is managed and must stay outside the hot kernel.
Scalability potential: Low tier keeps scalar cavitation and no signal work unless cavitation actually fires. Middle/high/ultra can consume the typed acoustic ping for richer audio, wake, and hull-stress presentation without changing the authoritative submarine dynamics.
Hardware Impact: Fixed-step Burst cost unchanged except the mock seeder job is now skipped when disabled. Post-fixed bridge is bounded to 64 dequeues and only publishes when intensity is nonzero; measured microsecond proof absent.

## Current Compile Wall - Save Binary Storage
Problem: `Docs/AgentLogs/Build_SHINOBU_11_20260518_cavitation_bridge.log` fails outside SHINOBU in `SaveBinaryStorage.cs(2423,65)` because local variable `header` is used before declaration.
Solution: Filtered the cavitation-bridge build log for SHINOBU source names and domain paths; no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, or `SHINOBU_11` errors were found.
Rejected Alternatives: Editing save binary storage rejected as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER` and likely concurrent persistence ownership. Claiming a clean build rejected because Core still fails.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.

## Legacy Submarine Pack1 Eradication
Problem: A wider submarine-domain scan found forbidden `Pack = 1` on legacy submarine gameplay, fluid, and structural DTO/job structs. These are outside the new SHINOBU Burst lane, but still sit under submarine runtime authority and can poison ARM64/cache behavior.
Solution: Removed `Pack = 1` from `SubmarineAutoLevelBallastController`, `SubmarineCoreDirector.SubmarineGridState`, `SubmarineFluidDynamics`, and `SubmarineStructuralGrid` layouts. Explicit-layout structs keep their `Size` and `FieldOffset` values. `SubmarineGridState` is now an 8-byte sequential struct with explicit padding.
Rejected Alternatives: Ignoring legacy structs rejected because Task 02/04 target the assigned submarine domain, not only newly created files. Rewriting the old Rigidbody-based controller graph rejected in this pass because `ISubmarineRuntimeContext.HullRigidbody` is consumed across physics, voxel, VFX, world, and UI systems; ripping it out is a separate compile-wall migration, not an alignment hygiene fix.
Scalability potential: Low-end ARM64 avoids forbidden unaligned layout declarations across submarine runtime data. High/ultra behavior should be unchanged because explicit offsets and sizes remain stable.
Hardware Impact: Prevents an alignment-risk class; exact timing gain is unmeasured.

## Core Compile Proof And Editor Boundary
Problem: Previous Core builds were blocked by concurrent external churn, and the editor facade still needed verification.
Solution: After the legacy Pack1 pass, `Docs/AgentLogs/Build_SHINOBU_11_20260518_pack1_submarine_legacy.log` succeeded with 0 errors and 9 warnings. A separate `Hecton8.Editor.csproj` build still fails before useful facade verification: first in `BlackboxXRayViewer.cs`, then many editor windows surface missing `Hecton8.Core.Contracts`; testing a generated-project reference bridge caused CS0433 duplicate `HectonPhysicsContract`, so the bridge was reverted.
Rejected Alternatives: Claiming editor verification rejected. Keeping the generated-project duplicate reference rejected because it worsens editor compile correctness.
Scalability potential: Core runtime proof means the submarine Burst lane and legacy layout hygiene compile in the runtime assembly. Editor tool still needs Unity/generator-level reference repair by the editor/assembly owner.
Hardware Impact: 0 us runtime; compile evidence only.

## Vehicle Command Bridge And Legacy PhysX Auto-Install Gate
Problem: The new vault kinematic lane had deterministic thrust/torque controls but did not consume the existing player vehicle command lane. Meanwhile `MountablePlayerTransport` and `SubmarineCoreDirector` could auto-add the old Rigidbody AutoLevel controller, keeping player control tied to legacy PhysX.
Solution: `SubmarineDynamicsRuntime` now implements `IVehicleCommandSignalListener`, registers with `VehicleCommandSignalBus`, flushes pending commands before vault signal consumption, and maps throttle/pitch/yaw/ballast deltas into `SubmarineKinematicControl`. `MountablePlayerTransport` publishes submarine command signals for any `SubmarineCoreDirector` without auto-adding `SubmarineAutoLevelBallastController`. Legacy AutoLevel auto-install now requires explicit `enableLegacyPhysXAutoLevelInstall`.
Rejected Alternatives: Directly reading input service inside the physics runtime rejected; the existing command bus is the typed signal corridor. Deleting all `Rigidbody` members from `ISubmarineRuntimeContext` rejected because physics, voxel, world, VFX, UI, and combat systems still compile against that interface; this pass removes auto-install/auto-require behavior without breaking external consumers.
Scalability potential: Low tier avoids unnecessary legacy component creation and routes controls straight into the cheap Burst integrator. Middle/high/ultra can still opt into legacy fallback for scenes not migrated, but the default path is the vault kinematic lane.
Hardware Impact: Fixed-step command flush is a bounded existing NativeQueue lane; measured microsecond proof absent. Removing default legacy AutoLevel auto-install avoids scheduling the old PID/Rigidbody path for new SHINOBU submarines.

## Current Core Compile Proof After Command Bridge
Problem: Command bridge and legacy Rigidbody gating touched gameplay and physics files, so runtime compile proof had to be refreshed.
Solution: `Docs/AgentLogs/Build_SHINOBU_11_20260518_vehicle_command_bridge2.log` succeeded with 0 errors and 9 warnings. Focused scans show no submarine `RequireComponent(typeof(Rigidbody))`, no `Pack=1` in `*Submarine*.cs`, and no SHINOBU-owned hot-path ban matches.
Rejected Alternatives: Reusing the older successful Core build rejected after code changed.
Scalability potential: Confirms the runtime assembly can accept the new kinematic command path while preserving external legacy APIs.
Hardware Impact: 0 us runtime; compile evidence only.

## Final Forensic Scope Boundary
Problem: The final response must not claim runtime perfection without editor/player evidence, but the task also requires a hard forensic report covering all 20 XML tasks, L1/ARM64 layout, H-Phi, Dear Lie, blackbox, and compile guards.
Solution: Re-read disk status/rationale, re-extracted the attribute-aware `SHINOBU_11` block from `Docs/Tasks/CURRENT_BATCH.md` at line 566 with exactly 20 tasks, re-ran scoped static bans, checked the latest successful runtime Core build log, and appended the final audit to `LOG_SHINOBU_11.md`.
Rejected Alternatives: Claiming `Status: Complete` rejected because Unity import, Play Mode, Profiler, GCMonitor, and VR comfort validation were not executed. Running another full build without code changes rejected as rebuild spam after the latest post-code Core build already succeeded.
Scalability potential: Low tier retains scalar/LUT math and command-bus kinematics without legacy PhysX auto-install; middle/high/ultra can consume cavitation/acoustic/telemetry outputs for visual overkill without altering gameplay truth.
Hardware Impact: No measured microsecond savings are claimed. Evidence is compile/static/layout proof only.

## H8Dump Mirror And Signal Stride Correction
Problem: The SHINOBU blackbox wrote only `Dump_SUB_KINEMATICS.bin`, while current crash-forensics doctrine requires a `.h8dump` artifact. The existing `VehicleCommandSignal` used `Pack = 4` and had no explicit 8-byte stride proof despite being the command signal corridor consumed by the submarine runtime.
Solution: `DumpBlackBoxIfFaulted()` now writes `Docs/AgentLogs/Dump_SHINOBU_11.h8dump` first and then attempts the legacy `Dump_SUB_KINEMATICS.bin` mirror. Dump writes are fault-path only and catch `IOException`/`UnauthorizedAccessException` instead of throwing out of post-fixed cleanup. `VehicleCommandSignal` now uses `StructLayout(LayoutKind.Sequential, Size = 32)` with explicit padding; no `Pack` attribute remains on that signal.
Rejected Alternatives: Renaming only to `.h8dump` rejected because the XML task explicitly requested `Dump_SUB_KINEMATICS.bin`; dual output preserves both contracts. Leaving `Pack=4` rejected because the user specifically challenged L1/ARM64 proof.
Scalability potential: Low tier gets deterministic 32-byte command copies through the existing NativeQueue lane. High/ultra can keep consuming the same command payload without adding coupling.
Hardware Impact: No hot-path measured saving. Command payload stride is now explicit and cache predictable; dump I/O is fatal-path only.

## Submarine Pack Policy Eradication
Problem: Earlier cleanup removed `Pack=1`, but a stricter scan still found `Pack=16` and `Pack=4` in legacy `*Submarine*.cs` files. Even when not `Pack=1`, packing attributes keep runtime layout dependent on CLR packing rules instead of explicit Size/manual padding.
Solution: Removed all remaining `Pack=` attributes from `*Submarine*.cs`. `SubmarineCoreDirector.SubmarinePhysicsBindingState` now has `Size=40` with explicit padding because it is stored in a `NativeArray`. Small atmosphere signal/mutation payloads now use explicit `Size=32` or `Size=24`; structural/atmosphere job structs no longer declare packing policy.
Rejected Alternatives: Rewriting legacy atmosphere/structural private NativeArrays into Vault buffers rejected as out-of-domain compile-wall work for SHINOBU_11. The new authoritative SHINOBU lane remains fully Vault-backed; legacy systems are documented debt.
Scalability potential: ARM64/Quest gets no packed submarine runtime layouts. High-end behavior is unchanged.
Hardware Impact: Alignment risk removed; profiler savings unmeasured.

## Current Compile Boundary After H8Dump/Pack Polish
Problem: Code changed after the last clean Core runtime build, so compile evidence had to be refreshed.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`, logging to `Docs/AgentLogs/Build_SHINOBU_11_20260518_h8dump_signal_stride_retry.log`.
Rejected Alternatives: Claiming the older clean build as current proof rejected because source changed. Editing `UI/TerminalOS/TerminalOsRuntime.cs` rejected as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER`.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only. Current blocker is external Terminal OS missing-field state; filtered log scan found no SHINOBU/submarine errors.

## CSV Override Vault Race Correction
Problem: The designer CSV hot-reload path ran from `SlowTick` and could resolve/write `SubmarineKinematicControls` and `SubmarineKinematicConfig` without checking whether the Burst integrator job still owned the same Vault buffers. This is cold/slow-path code, but it is still a real data-race risk.
Solution: `SlowTick` now skips Vault buffer re-ensure while an integrator job is pending or buffers are locked. `TryApplyCsvOverrides()` now returns immediately during pending integration, locks `SubmarineKinematicControls` then `SubmarineKinematicConfig` before writing, unlocks them in `finally`, and catches `IOException` / `UnauthorizedAccessException` for CSV timestamp/open/read failures. The blackbox dump path now also catches directory-creation failures instead of throwing during post-fixed cleanup.
Rejected Alternatives: Leaving CSV writes unlocked rejected because a designer edit during a long frame could collide with Burst writes. Locking config before controls rejected because the fixed-step lock order takes controls before config. Moving CSV parsing to a background thread rejected for this pass because the task requires a hot-reload bridge, not a new lifetime/thread owner.
Scalability potential: Low-tier devices avoid a rare but catastrophic data-race/fatal-path exception. Middle/high/ultra behavior is unchanged; visual overkill consumers still read force/cavitation/telemetry without adding authority cost.
Hardware Impact: 0 us fixed-step impact. CSV parsing remains slow-path and capped to 4096 bytes; no profiler microsecond saving is claimed.

## Current Compile Wall - World Residency Persistence
Problem: After the CSV/Vault race correction, current Core build `Docs/AgentLogs/Build_SHINOBU_11_20260518_csv_lock_retry.log` fails outside SHINOBU in `WorldChunkResidencyManager.cs(4064,17)` because `RefreshAsyncPersistenceService` is missing.
Solution: Filtered the build log for SHINOBU/submarine names and domain paths; no `SubmarineDynamics`, `SubmarineKinematic`, `VehicleCommandSignals`, `SubmarineCoreDirector`, `SubmarineStructuralGrid`, `SubmarineAtmosphereSystem`, `SubmarineFluidDynamics`, `DynamicFlood`, `DockingAutopilot`, or `SHINOBU_11` errors were found.
Rejected Alternatives: Editing world streaming/residency persistence rejected as outside `SUBMARINE_MASS_AND_BUOYANCY_SOLVER` and likely concurrent ownership. Claiming a current clean compile rejected because current disk build fails.
Scalability potential: No submarine runtime effect.
Hardware Impact: 0 us runtime; compile-wall evidence only.
