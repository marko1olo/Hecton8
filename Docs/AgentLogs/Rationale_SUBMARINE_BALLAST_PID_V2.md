# Rationale_SUBMARINE_BALLAST_PID_V2

Agent: HYDRO_MECHANIC
Prompt ID: SUBMARINE_BALLAST_PID_V2
Status: CORE POLISHED / BUILD BLOCKED BY EXTERNAL DEPENDENCY

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
