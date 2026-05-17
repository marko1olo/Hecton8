# Rationale_SUBMARINE_BALLAST_PID_V2

Agent: HYDRO_MECHANIC
Prompt ID: SUBMARINE_BALLAST_PID_V2
Status: VERIFIED MASTER GRADE / STATIC PASS CURRENT / FULL BUILD PREVIOUSLY BLOCKED BY EXTERNAL DEPENDENCY / UNITY RUNTIME PROFILING PENDING

## Decision 0 - Existing Controller vs Parallel Controller
Problem: The requested domain path is `Assets/_Project/Scripts/Physics/Vehicles/`, while the live submarine controller is currently `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`.
Solution: Extend the existing live controller and add only missing contract types under the vehicle physics contract boundary if needed. This prevents two PID systems from applying conflicting torque to the same hull.
Rejected Alternatives: Creating a new independent MonoBehaviour in the missing path would duplicate tick registration, flood buffer ownership, and force routing.
Scalability potential: Low runs low-cadence mass truth with pitch interpolation; Middle/High/Ultra can increase drag tensor richness and VFX/audio response without changing authority.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is avoiding duplicate PID and duplicate flood scans, roughly 20-60 us/frame during active flood.

## Decision 1 - Mandate Set
Problem: Task touches physics, vehicles, flooding, signals, DataVault, AUP, and telemetry.
Solution: Loaded mandates: CORE_Submarine_Vehicles_Kinematics_AUP, PHYS_Fluid_Incursion_Interior, PHYS_Physics_Integrity_Determinism_ForceMode, OPT_Zero_GC_Policy_AllocFree_Mandate, DBG_Telemetry_Crash_Reporting_PostMortem, MATH_AUP_Determinism_Sync, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, ARCH_Execution_Phases.
Rejected Alternatives: Reading only the two named mandates would miss force routing, telemetry dump, and signal lane rules.
Scalability potential: Mandates define Low/Middle/High/Ultra behavior explicitly, including MX350 1 Hz flood truth and high-tier visual overkill.
Hardware Impact: Mandate-driven low-tier cadence prevents 60 Hz PhysX mass/CoM mutation on i3/MX350.

## Decision 2 - Dynamic Flood Mass Authority
Problem: Flooding had to pull the hull through real mass/CoM authority without creating a second submarine physics owner.
Solution: Reworked the existing controller path so `SubmarineMassSolverJob` consumes DataVault SOA room water, volume, and local AUP buffers, computes flood mass with seawater density, and writes center of mass, CoM offset, inertia tensor multiplier, and global pivot anchor to a NativeArray output.
Rejected Alternatives: A new PHYSICS/VEHICLES MonoBehaviour was rejected because it would duplicate force ownership and race the existing auto-level controller.
Scalability potential: Low/Middle use low-cadence flood truth and existing angular damping; High/Ultra use the extra inertia and tensor output for stronger 6DOF response.
Hardware Impact: i3/MX350 avoids per-frame room scans and keeps flood solve cadence to 1 Hz under low math LOD, estimated 20-40 us/frame saved during active flooding.

## Decision 3 - AUP And NaN Vaccination
Problem: Floating origin and bad room data can corrupt CoM math if the solver trusts runtime float positions or direct divides.
Solution: The solver carries `double3 GlobalPivotAnchor`, uses AUP-to-double conversion for room mass accumulation, and normalizes mass terms with `math.rcp(math.max(value, 0.01f))`.
Rejected Alternatives: Pure `Transform.position` authority and direct division were rejected because both fail under origin shifts or zero/corrupt mass.
Scalability potential: Low devices get deterministic fallback; High/Ultra can layer richer inertia and drag response on the same authoritative double anchor.
Hardware Impact: Correctness gate with trivial ALU cost; finite fallback prevents NaN cascades that would force emergency physics resets on low-end silicon.

## Decision 4 - Visual Overkill Without New Simulation
Problem: High-end hardware needs heavier flood feel, but a full fluid simulation in every room violates the 0.1 ms suspicion threshold.
Solution: Added flood-derived 6DOF drag tensor multipliers, a bounded `BubbleSpawnSignal` at engine vents when tail-heavy pitch exceeds 20 degrees, and a high-tier-only `FluidImpulseSignal` to drive volumetric silt/wake consumers without creating another physics owner.
Rejected Alternatives: Continuous particle simulation and per-cell slosh drag were rejected as uncontrolled VFX and CPU expansion.
Scalability potential: Low disables the extra drag tensor and emits only bounded events; Middle/High/Ultra increase perceived mass and struggle through tensor response and VFX consumers.
Hardware Impact: i3/MX350 avoids high-tier tensor shaping in low math LOD; RTX-class machines spend saved cycles on heavier vehicle feel and engine vent struggle.

## Decision 5 - Stress-Aware PID And Audio
Problem: A full derivative term during global system stress adds ALU and can amplify jitter while the hull is already under stress.
Solution: `SystemHealthIndexSignal` pressure drives `_systemStress01`; above 0.8 the PID job disables D and runs PI. PID error also produces `HullStressSignal` through `IAudioService`, with procedural fallback.
Rejected Alternatives: Audio polling and always-on full PID were rejected because they spend work at idle and ignore homeostasis load.
Scalability potential: Low drops derivative work under stress; High/Ultra retain full PID while stable and get louder structural feedback during heavy corrections.
Hardware Impact: Estimated 1-2 us/solve saved during stress on i3/MX350, plus zero idle audio polling.

## Decision 6 - Build Validation Boundary
Problem: `dotnet build` is required, but the current workspace fails before integration due unrelated fauna assembly namespace errors.
Solution: Ran multiple builds and stopped at the external compile wall after the visible errors referenced `PredatorCognitionDomain.cs` and `FaunaKinematicsRuntime.cs`, not the submarine files. Marked final validation blocked by dependency instead of editing outside domain.
Rejected Alternatives: Fixing AI/Animation assembly references was rejected as outside PHYSICS/VEHICLES and likely owned by parallel agents.
Scalability potential: No runtime effect; preserves domain boundaries while leaving a clear integrator blocker.
Hardware Impact: No frame-time gain; prevents cross-domain churn that could destabilize unrelated systems on all hardware tiers.

## Decision 7 - Multiplatform Struct Layout
Problem: Sequential `Pack=4` structs leave runtime-dependent padding risk for ARM64/Quest and make binary telemetry stride less obvious.
Solution: Converted the new ballast PID job output, telemetry ring entry, dynamic flood output, dynamic flood contract samples/results, hydro kinematic packets, hydro black box entry, and splash payload to explicit fixed-size layouts.
Rejected Alternatives: Keeping sequential structs was rejected because the layout contract was implicit and harder to audit across Mono, IL2CPP, ARM64, and x64.
Scalability potential: Low/Quest/Android get deterministic NativeArray strides; High/Ultra can safely consume the same black-box packets for richer debug tooling.
Hardware Impact: No claimed frame-time gain; reduces layout ambiguity and crash surface on alignment-sensitive platforms.

## Decision 8 - DataVault-Only State Allocation
Problem: The ballast PID and submarine fluid owner still had direct persistent `H8Memory.Allocate` fallbacks after requesting DataVault buffers.
Solution: Removed direct allocation fallback from both state allocation helpers. The ballast PID now stores only `VaultBufferHandle<T>` identities and resolves transient DataVault NativeArray views at job/write boundaries.
Rejected Alternatives: Keeping private fallback NativeArrays or persistent NativeArray fields in the PID controller was rejected because it creates split-brain state and violates data sovereignty under integration load.
Scalability potential: Steam Deck and MicroSD scenarios avoid private buffer churn; Low/Middle/High/Ultra all read the same authoritative SOA buffers.
Hardware Impact: No fabricated microsecond claim; the real gain is reduced ownership ambiguity and fewer persistent allocation paths.

## Decision 9 - Typed Lane And Bubble Guard
Problem: The ballast PID consumed typed signal lanes through NativeArray snapshots and the new bubble VFX signal had no finite guard.
Solution: Switched PID signal reads to `ReadOnlySpan<T>` from `SignalBus<T>.GetFrameSnapshot()` and added `BubbleSpawnSignal` to signal finite guards and non-critical VFX lane policy.
Rejected Alternatives: Managed delegates, legacy event fanout, or unguarded VFX payloads were rejected because they can amplify NaN or queue pressure on mobile.
Scalability potential: Low tier can shed bubble VFX as non-critical; High/Ultra can consume the same lane for dense vent effects without coupling to physics ownership.
Hardware Impact: No fake timing; expected benefit is bounded queue pressure and sanitized VFX data.

## Decision 10 - Current Compile Wall
Problem: After the polish pass, `dotnet build Hecton8.Core.csproj` still fails outside this domain.
Solution: Recorded the current blocker set after the DataVault-handle pass: `DiegeticGyroCompassRuntime` missing `_stateBuffer`/`_outputBuffer`/`_blackBox`, `LockstepReplayBlockHeader.HashCadenceFrames`, `HectonPlayerState` missing motor array helpers, `HomeostasisBrain` missing hardware metric fields/helpers, `ItemAcquiredSignal`, and `TetherFiredSignal` not implementing the current signal interface. No submarine edited file appears in the error set.
Rejected Alternatives: Editing UI/Core/Homeostasis/Inventory/Tether ownership was rejected because it is outside PHYSICS/VEHICLES and would cross other agents' work.
Scalability potential: None in runtime terms; preserving ownership prevents integration churn.
Hardware Impact: None.

## Decision 11 - Steam Deck Fault I/O And Handle-Based H-Phi
Problem: The fault path wrote three duplicate dump files, and persistent PID NativeArray fields still made the controller look like a private data owner even after DataVault allocation fallback removal.
Solution: Collapsed crash/NaN dump output to the required `Dump_SUBMARINE_BALLAST_PID_V2.bin` file and converted persistent PID buffers to `VaultBufferHandle<T>` fields resolved through `GlobalDataVault` on demand. DataVault service replacement now clears handles before reacquiring them.
Rejected Alternatives: Keeping legacy autopilot/flood dump aliases was rejected as unnecessary MicroSD write pressure; keeping persistent NativeArray aliases was rejected as H-Phi debt.
Scalability potential: Low/Steam Deck avoids extra fault-time I/O and stale buffer views; Middle/High/Ultra still get the same telemetry and high-tier VFX impulse lane without extra ownership.
Hardware Impact: Two fault-time file writes removed. Runtime microsecond savings are not claimed because the handle resolution trades ownership correctness for negligible pointer-refresh work.

## Decision 12 - Fluid State Vault Buffer Wrapper
Problem: `SubmarineFluidDynamics` still had persistent `NativeArray<T>` fields, and the first pure-property replacement was not viable because C# cannot assign through a value-type property indexer returned by value.
Solution: Replaced the persistent fluid `NativeArray<T>` fields with `VaultNativeBuffer<T>` wrappers that store `VaultBufferHandle<T>` identity and the cached `IDataVault` reference. Hot scalar reads/writes use the vault pointer directly; Burst jobs receive transient `NativeArray<T>` views only at schedule boundaries.
Rejected Alternatives: Keeping private `NativeArray<T>` aliases was rejected as data-sovereignty debt. Resolving through `GlobalDataVault` on every compartment indexer read was rejected because it would add dictionary/generation validation inside the fixed-step fluid loop. Expanding the public `GlobalDataVault` API was rejected because the domain fix did not require a Core public contract change.
Scalability potential: Low and Steam Deck keep the same bounded compartment counts without private buffer ownership; Middle/High/Ultra keep hydrodynamic drag, flood mass, and black-box telemetry on the same authoritative vault buffers.
Hardware Impact: No fabricated microsecond claim. The measurable result is ownership hardening with no compile regression; domain static scans show no persistent `NativeArray` fields in the submarine fluid/PID owners.

## Decision 13 - Omega Validation And Current Compile Wall
Problem: The final inquisition required ARM64/Quest-safe layout, NaN vaccination, and a current compile result rather than stale reporting.
Solution: Converted remaining submarine PID/fluid native payload structs in this domain boundary to explicit `Pack = 1` layouts, kept `math.rsqrt` guarded by `math.max(epsilon, value)`, and reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` from current disk state.
Rejected Alternatives: Leaving sequential private payloads in place was rejected because binary/native stride must be auditable. Editing `FaunaBrain.cs` helper drift was rejected because AI/Fauna is outside PHYSICS/VEHICLES and is active concurrent-agent territory.
Scalability potential: Low/Quest/Android get deterministic struct strides and finite fallbacks; High/Ultra still spend the saved physics work on typed VFX intent through `BubbleSpawnSignal` and `FluidImpulseSignal` instead of heavier gameplay simulation.
Hardware Impact: A build passed once after the domain fix, but the latest rerun now fails in `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` for missing `NormalizeVectorOrFallback`, `IsFiniteBounds`, and `IsFiniteVector`. Runtime GC/frame and profiler numbers remain unmeasured in this CLI session, so no measured frame-time victory is claimed.

## Decision 14 - DataVault Relocation Handle Refresh
Problem: `SubmarineFluidDynamics` had removed private `NativeArray<T>` ownership, but the vault wrapper still cached raw DataVault pointers. A future DataVault relocation could leave scalar compartment reads/writes dereferencing stale addresses before a Burst schedule boundary resolved the handle.
Solution: Added typed-lane `MemoryAddressShiftSignal` consumption at the start of the fixed tick and refreshed existing vault handles in place through `TryGetBufferHandle`, preserving current ping-pong buffer identity after swaps. Added GlobalRegistry hot-swap listener coverage for DataVault and PowerGrid replacement so the controller does not poll registry services in the hot path.
Rejected Alternatives: Resolving `VaultBufferHandle<T>` on every compartment indexer read was rejected because it adds dictionary/generation validation inside the fixed-step loop. Rebinding all buffers to canonical IDs on relocation was rejected because it would destroy front/back ping-pong ownership after transfer jobs. Ignoring relocation signals was rejected because stale raw pointers are an ARM64/Quest crash surface.
Scalability potential: Low/Quest/Android fail closed on relocated or missing buffers without dereferencing stale memory; Middle/High/Ultra preserve the same authoritative vault-backed hydrodynamics and black-box state while DataVault maintenance runs.
Hardware Impact: No measured microsecond claim. The hot-path cost is one typed-lane span scan per fixed tick and in-place handle refresh only when a relevant relocation signal appears; the gain is stale-pointer crash prevention without private NativeArray regression.

## Decision 15 - Docking Active Spline Handle Hardening
Problem: The PHYSICS/VEHICLES docking authority owned no private `NativeArray`, but it resolved a cached `VaultBufferHandle<ActiveSplineData>` through `ResolveBuffer`. `GlobalDataVault.ResolveBuffer` throws on stale cached metadata, so a DataVault relocation could turn docking into a hard fault even though the data was already in the vault.
Solution: Converted docking spline payloads to explicit `Pack = 1` field offsets, replaced stale-handle resolution with generation-checked `TryGetBufferGeneration` plus `TryGetBufferHandle`, and registered the service as a GlobalRegistry hot-swap listener for DataVault replacement.
Rejected Alternatives: Keeping `ResolveBuffer(ref _activeSplineHandle)` was rejected because it is a stale-handle exception path after relocation. Rebuilding docking state in a private array was rejected because it violates DataVault sovereignty. Adding a new docking signal was rejected because `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal` already exist.
Scalability potential: Low/Quest/Android get deterministic 144-byte and 56-byte spline strides plus fail-closed DataVault rebinding; High/Ultra preserve the same spline service for richer docking visuals without changing the physics contract.
Hardware Impact: No new measured runtime microsecond claim. The practical gain is crash-surface removal on ARM64 and relocation-safe vault reads. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors after this pass.

## Decision 16 - Burst Job Container Packing
Problem: The ARM64/Quest audit found two PID-owned Burst job containers without a `StructLayout` declaration, while the fluid solver still has private job containers using `NativeArray<T>` fields.
Solution: Added `StructLayout(LayoutKind.Sequential, Pack = 1)` to `SubmarineAutoLevelPidJob` and `SubmarineMassSolverJob`. Kept binary/native payload structs on explicit fixed-size layouts. Kept Burst job containers with `NativeArray<T>` handles sequential `Pack = 1` because explicit field offsets would hard-code Unity safety-handle internals that vary by build configuration.
Rejected Alternatives: Converting job containers with `NativeArray<T>` fields to explicit offsets was rejected as false portability; it would encode Unity.Collections implementation details rather than the project-owned payload ABI. Leaving the PID job containers without packing was rejected because the audit needs an explicit layout declaration.
Scalability potential: Low/Quest/Android get explicit pack declarations on every task-owned job/payload crossing the PID/fluid boundary; Middle/High/Ultra keep the same Burst job scheduling and visual overkill hooks without adding a private data owner.
Hardware Impact: No measured microsecond claim. The impact is alignment risk reduction and clearer IL2CPP/Burst auditability. Current full build is blocked outside this domain after the latest rerun: `PredatorCognitionDomain.cs` NativeArray/hash-map drift and `DroneFleetManager.cs` double3-to-float3 errors.

## Decision 17 - Fluid Definition And Hydro Job Layout Closure
Problem: The latest full-domain struct scan found three remaining submarine fluid structs without any layout declaration: the two Unity-serialized definition DTOs and the hydro drag Burst job container.
Solution: Added `StructLayout(LayoutKind.Sequential, Pack = 1)` to `CompartmentDefinition`, `BulkheadDefinition`, and `HydroKinematicDragJob`. The serialized DTO field types were left intact to avoid corrupting Unity-authored data; the change only makes packing explicit for ARM64/IL2CPP auditability.
Rejected Alternatives: Rewriting the serialized DTOs into byte-packed binary payloads was rejected because those arrays are inspector-authored configuration, not a native signal or telemetry ABI. Ignoring the missing attributes was rejected because the mandate requires an explicit multiplatform layout trail.
Scalability potential: Low/Quest/Android get a complete layout declaration chain for submarine flood configuration, hydro job input/output, telemetry, docking splines, and PID payloads. High/Ultra retain the same 6DOF drag tensor and VFX signal path with no extra simulation.
Hardware Impact: No measured microsecond claim. This is alignment/audit hardening. Current full build is blocked outside this domain by Tether signal type drift: `TetherManager.cs(264,58)` and `Physics/TetherSignals.cs(167,82)` reference missing `TetherFireRequest`.

## Decision 18 - Compartment State Vault Eviction
Problem: `SubmarineFluidDynamics` still kept `_compartmentStates` as a private managed `CompartmentState[]`, which made the compartment flood snapshot a local authority even after the NativeArray buffers were moved to `GlobalDataVault`.
Solution: Added `BufferID.SubmarineFluidCompartmentStates = 444` and converted `_compartmentStates` to `VaultNativeBuffer<CompartmentState>`. The buffer now uses the same VehiclesPhysics Vault ownership, relocation detection, refresh, dispose, and clear paths as the rest of submarine fluid state.
Rejected Alternatives: Leaving the managed array was rejected because it preserves split-brain state outside the Vault. Renumbering the existing submarine buffer IDs was rejected because later IDs are occupied by other agents; the new explicit ID avoids enum churn.
Scalability potential: Low/Quest/Android get one authoritative compartment-state lane with explicit 64-byte `Pack = 1` snapshots and fail-closed Vault rebinding. High/Ultra keep the same flood CoM, gas mix, 6DOF drag, and VFX hooks without adding simulation cost.
Hardware Impact: No measured runtime microseconds claimed. The gain is data-sovereignty hardening and stale-pointer crash-surface reduction; latest full build is blocked outside PHYSICS/VEHICLES by `DiegeticGyroCompassRuntime` signature/field drift and `EcosystemDirector` generic-inference errors.

## Decision 19 - Hydro Dump Single-File Contract
Problem: `SubmarineFluidDynamics` still wrote hydro black-box data to two legacy agent dump files and allocated a concatenated `Debug.LogError` string if fault I/O failed. It also carried a dead duplicate splash payload stub after switching to the canonical `SplashEvent` signal.
Solution: Removed the dead `RemovedSplashEventPayload` stub, routed hydro black-box dumps to `Docs/AgentLogs/Dump_SUBMARINE_BALLAST_PID_V2.bin`, removed the second fault-time file write, and replaced the fault-path log allocation with `GlobalTelemetryBus.PublishPerformanceWarning`.
Rejected Alternatives: Keeping the legacy `Dump_KINEMATICS_HYDRO_DRAG.bin` and `Dump_OCEAN_CHEMISTRY_ENGINEER.bin` writes was rejected because it doubles MicroSD fault I/O and violates the task-owned dump contract. Keeping the duplicate splash payload stub was rejected because the canonical `SplashEvent` already exists.
Scalability potential: Low/Steam Deck gets one bounded fault write and no fault-path string allocation; Quest/Android keeps the same 300-frame hydro telemetry ring without extra I/O. High/Ultra keep the same hydrodynamic black-box payload for deeper diagnostics.
Hardware Impact: One fault-time file write removed. No measured runtime microseconds claimed because this path runs only on hydro fault/NaN dump. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.

## Decision 20 - Exterior Thermal State Vault Eviction
Problem: Exterior boil-cell centers, temperatures, lifetimes, and hazard ids were private managed arrays in `SubmarineFluidDynamics`. They are active runtime state for heat hazards and boiling updrafts, not authoring data.
Solution: Added `BufferID.SubmarineFluidExteriorThermalCenters`, `SubmarineFluidExteriorThermalTemperatures`, `SubmarineFluidExteriorThermalLifetimes`, and `SubmarineFluidExteriorThermalHazardIds`. Converted the four private arrays to `VaultNativeBuffer<float3>`, `VaultNativeBuffer<float>`, `VaultNativeBuffer<float>`, and `VaultNativeBuffer<int>`, with relocation recognition, refresh, dispose, and clear coverage.
Rejected Alternatives: Leaving the arrays as component-owned state was rejected because it preserves a private state island outside the Vault. Replacing the effect with per-contact physics simulation was rejected because the existing quantized 8 m cell model is the correct Dear Lie for toaster hardware.
Scalability potential: Low/Steam Deck keeps an 8-cell bounded thermal fake with no per-particle simulation and one Vault authority. High/Ultra can still drive boiling updrafts, hazard registration, and VFX/audio consumers from the same quantized state without changing the physics contract.
Hardware Impact: No measured runtime microseconds claimed. The expected benefit is ownership hardening and stale-state prevention; current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings and 0 errors.

## Decision 21 - Current-Disk Build Revalidation
Problem: The status file still recorded a Core/Memory Defrag compile wall, but current source no longer matched the recorded `using Hecton8.Core.Memory.Defrag` failure.
Solution: Re-read the live XML/status/rationale, inspected the current Defrag declarations, and reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` with a longer timeout. The build produced `Temp/bin/Debug/Hecton8.Core.dll` with 0 warnings and 0 errors.
Rejected Alternatives: Leaving the task blocked on stale compiler output was rejected because disk state is the only authority. Editing unrelated Core/Memory files was rejected because the current build no longer required it.
Scalability potential: Low/Steam Deck/Quest keep the same Vault-backed, bounded submarine state path; High/Ultra keep 6DOF drag tensor and VFX signal hooks without additional compile debt.
Hardware Impact: No runtime microseconds claimed. This is compile validation only; Unity runtime profiling and GCMonitor numbers remain pending.

## Decision 22 - Exterior Buoyancy Sample Vault Eviction
Problem: `_exteriorBuoyancySampleLocalPoints` was a component-owned `Vector3[8]` cache used every fixed step for exterior buoyancy force distribution. It was derived data, but still hot-path hydro authority outside the Vault.
Solution: Added `BufferID.SubmarineFluidExteriorBuoyancySampleLocalPoints` and converted the cache to `VaultNativeBuffer<float3>`, wired through `GlobalDataVault` allocation, relocation refresh, dispose, clear, and fail-closed hydrodynamics guards.
Rejected Alternatives: Leaving the managed array was rejected as a private hydro state island. Moving Unity physics query scratch arrays was rejected because `Collider[]`, `Rigidbody[]`, and `GetComponents` list overloads are Unity API boundary scratch, not authoritative simulation state.
Scalability potential: Low/Steam Deck keeps the same 8-point Dear Lie for exterior displacement instead of mesh-fluid truth; High/Ultra retain 6DOF drag tensor response and VFX signal hooks using the same sample points without adding simulation cost.
Hardware Impact: No measured runtime microseconds claimed. Current full build passes with 0 warnings and 0 errors; Unity runtime profiling remains pending.

## Decision 23 - Unity API Scratch Classification
Problem: The final H-Phi scan still surfaced managed arrays/lists in `SubmarineFluidDynamics`, but not all containers are state authority. Some are Unity serialization DTOs or mandatory managed-array scratch for Unity/PhysX/spatial hash APIs.
Solution: Documented `compartments` and `bulkheads` as inspector-authored DTOs mirrored into `GlobalDataVault` at enable time, and marked `_componentSearchBuffer`, `_pipeBindingBuffer`, `_depressurizationContacts`, `_depressurizationBodies`, and `_exteriorThermalContacts` as Unity API scratch, not simulation authority.
Rejected Alternatives: Moving `Collider[]`, `Rigidbody[]`, or `SpatialQueryHit[]` scratch into the Vault was rejected because the caller APIs require managed arrays and the buffers do not persist authoritative state past the method scope. Removing the scratch would reintroduce allocations or PhysX query garbage.
Scalability potential: Low/Steam Deck keeps bounded nonalloc API scratch and Vault-owned hydro truth; High/Ultra keep the same data path for 6DOF drag, VFX signals, and black-box telemetry.
Hardware Impact: No measured runtime microseconds claimed. The value is audit clarity with no runtime path change; current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` passes with 0 warnings and 0 errors.

## Decision 24 - Direct Typed-Lane Publish
Problem: Some submarine VFX/flood broadcasts used `GlobalSignals.Publish` even when the overload was only a thin `SignalBus<T>.Push` facade. That hid the typed-lane contract during audit.
Solution: Replaced those calls with direct `SignalBus<SubmarineFloodStateSignal>.Push`, `SignalBus<BubbleSpawnSignal>.Push`, and `SignalBus<FluidImpulseSignal>.Push`. Kept `GlobalSignals.Publish` for `AcousticPingSignal`, `ImpactSignal`, `HapticRequest`, and `VocalWarningSignal` paths where GlobalSignals also preserves latest-state or queue compatibility for existing consumers.
Rejected Alternatives: Replacing all `GlobalSignals.Publish` calls blindly was rejected because impact/audio/vocal lanes have additional side effects beyond typed push. Creating duplicate submarine signals was rejected because the existing signals already cover flood state, bubbles, and fluid impulses.
Scalability potential: Low/Steam Deck keeps bounded VFX intent through typed lanes; High/Ultra can consume the same lanes for denser silt, wake, and bubble rendering without coupling VFX to physics ownership.
Hardware Impact: No measured runtime microseconds claimed. The latest full build is blocked outside PHYSICS/VEHICLES by `HectonSurvivalSystem.cs(553,13)` missing `EnsurePhysiologyScalarBuffer`; no submarine files appear in the compiler error set.

## Decision 25 - Inventory Mass Typed-Lane Cargo Coupling
Problem: `SubmarineFluidDynamics` still consumed cargo mass through the legacy `InventoryEvents` listener and used a fallback registry mass poll inside fixed simulation cadence. That violated the typed-lane audit even though the poll was throttled.
Solution: Expanded the existing 32-byte explicit `InventoryChangedSignal` payload with `TotalMassKg`, `CarryCapacityKg`, and `Load01`, populated those fields from `PlayerInventory`, removed `IInventoryEventListener` from submarine fluid, and consumed inventory mass through `ReadOnlySpan<InventoryChangedSignal>` in the fixed path. Flood-state math LOD now uses `ScalabilityEvents` instead of a per-frame `GlobalRegistry.ScalabilityTier` read. Because `PlayerInventory` became a touched producer file, its local telemetry/reservation structs were also made explicit `Pack = 1` without changing offsets or sizes.
Rejected Alternatives: Creating a submarine-only cargo signal was rejected because `InventoryChangedSignal` already exists and had unused explicit-layout space. Keeping `InventoryEvents` was rejected because it is the legacy queue path this pass is purging. Polling `GlobalRegistry.PlayerInventoryMassKg` every 16 frames was rejected because typed-lane data already carries the mass.
Scalability potential: Low/Steam Deck consumes one bounded inventory snapshot scan only when inventory changes, then keeps the same 1 Hz flood mass Dear Lie. High/Ultra keep the same cargo/flood coupling and can drive heavier drag/VFX without adding a second data source.
Hardware Impact: No measured runtime microseconds claimed. Static evidence only: stale legacy inventory listener symbols are gone from `SubmarineFluidDynamics`; full build/rebuild was not rerun per user instruction, so prior external `HectonSurvivalSystem.cs` compile wall remains the last full-build fact.

## Decision 26 - Hot-Path Service Snapshot Closure
Problem: `SubmarineFluidDynamics` still had `Resolve*` helpers that could lazy-read `GlobalRegistry.Player`, `GlobalRegistry.Submarine`, `GlobalRegistry.Fluid`, or `GlobalRegistry.PowerGrid` when fixed-step call chains found a null cache. `SubmarineAutoLevelBallastController` also polled Audio/Fluid/DataVault/scalability during SlowTick and retried `GlobalRegistry.Audio` inside the PID hull-stress publish path.
Solution: Added hot-swap handling for Player, Submarine, and FluidRuntime service slots, kept registry reads in cold `CacheReferences` / `RegisterRuntime` seed paths, and changed fixed-step `Resolve*` helpers to cached-only fail-closed reads. Ballast SlowTick now only refreshes vault-backed PID buffers and requests a flood solve; scalability changes come from `ScalabilityEvents`, and hull-stress audio falls back to procedural audio if the cached service is absent.
Rejected Alternatives: Keeping lazy registry lookup was rejected because the Global Registry mandate explicitly bans hiding registry reads inside `Resolve*` helpers called by FixedTick. Creating new service-ready signals was rejected because the existing GlobalRegistry hot-swap listener already carries the service replacement event.
Scalability potential: Low/Steam Deck/Quest avoid registry polling in submarine fixed-step helpers and keep the scalar flood/cargo Dear Lie. High/Ultra retain cached service access for flow sampling, power ratio, stress audio, and richer drag/VFX without changing the physics contract.
Hardware Impact: No measured runtime microseconds claimed. Static evidence only: fixed-step helper bodies no longer contain registry fallback reads, legacy inventory listener symbols are absent, and full build/rebuild was not rerun per user instruction.

## Decision 27 - Submarine Signal Facade Purge
Problem: The submarine domain still used `GlobalSignals.Publish` for acoustic, haptic, impact, and one vocal warning payload. The typed lane existed for most of these packets, so the facade hid direct signal ownership and retained legacy queue side effects in vehicle code.
Solution: Replaced submarine acoustic, haptic, and impact publishes with direct `SignalBus<T>.Push` calls. Added an explicit finite guard before surfacing impact packets because the old facade used a sanitizer. Routed the crush-depth voice warning through the cached `IVocalWarningSystem` owner interface with GlobalRegistry hot-swap rebinding, avoiding the legacy `VocalWarningSignal` queue facade from the submarine path.
Rejected Alternatives: Keeping `GlobalSignals.Publish` was rejected because this pass is specifically purging facades in the submarine domain. Adding a new submarine-specific voice signal was rejected because `IVocalWarningSystem` already owns the one-recipient command and creating another lane would duplicate an existing interface.
Scalability potential: Low/Steam Deck/Quest keep bounded typed-lane presentation packets and no legacy queue fanout from submarine physics. High/Ultra retain the same acoustic, haptic, impact, and fluid impulse hooks for dense VFX/audio consumers without adding physical simulation.
Hardware Impact: No measured runtime microseconds claimed. Static evidence only: `rg "GlobalSignals\\.Publish|EventBus|delegate|InventoryEvents"` returns no submarine-domain hits, and full build/rebuild was not rerun per user instruction.

## Decision 28 - Docking DataVault Snapshot Closure
Problem: `DockingAutopilotService.EnsureSplineBufferAvailable` still lazy-read `GlobalRegistry.DataVault`. That helper is reachable from spline slot reserve/write/read/evaluate/release calls, so a missing cache could turn a docking operation into a live service-locator fallback.
Solution: Added `RefreshDataVaultReferenceCold` and call it during `InitializeService`. `EnsureSplineBufferAvailable` now fails closed when `_dataVault` is absent and relies on the existing DataVault hot-swap listener for live replacement.
Rejected Alternatives: Leaving the lazy fallback was rejected because vehicle physics hot helpers must use cached service snapshots. Creating a local spline array fallback was rejected because active docking spline authority already belongs in `GlobalDataVault`.
Scalability potential: Low/Steam Deck/Quest get deterministic cached-vault spline access and fail-closed behavior under DataVault absence. High/Ultra keep the same Bezier docking spline authority without adding a separate state owner.
Hardware Impact: No measured runtime microseconds claimed. Static evidence only: the spline buffer resolver no longer reads `GlobalRegistry.DataVault`; full build/rebuild was not rerun per user instruction.
