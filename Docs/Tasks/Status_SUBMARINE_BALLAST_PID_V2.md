# Status_SUBMARINE_BALLAST_PID_V2

Agent: HYDRO_MECHANIC
Prompt ID: SUBMARINE_BALLAST_PID_V2
Domain: PHYSICS/VEHICLES
Task Count: 18
State: CORE POLISHED / BUILD BLOCKED BY EXTERNAL DEPENDENCY

## Relevant Mandates
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- PHYS_Fluid_Incursion_Interior.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_AUP_Determinism_Sync.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- ARCH_Execution_Phases.txt

## Phase 1
- [x] 1. PURGE_SINGLETONS | DONE | DOD: `rg "SubmarineManager.Instance"` returned no submarine PID dependency; controller uses `GlobalRegistry` services and owned `SubmarineCoreDirector` references. Alternative rejected: singleton compatibility wrapper. Estimate: 3 us/frame avoided if legacy polling returned.
- [x] 2. DEBT_CLEANUP | DONE | DOD: no submarine fake pitch `Transform.Rotate` hot path added or retained; PID torque routes through `PhysicsForceRouter`. Alternative rejected: visual transform pitch on Rigidbody hull. Estimate: 5 us/frame plus desync avoided.
- [x] 3. DATA_EVICTION | DONE | DOD: ballast fill, tank positions, PID output, flood-mass output, telemetry, and room flood inputs now resolve through `GlobalDataVault` `VaultBufferHandle<T>` handles; no persistent `NativeArray` fields or `H8Memory.Allocate` fallback remain in `SubmarineAutoLevelBallastController`. Alternative rejected: private authoritative NativeArray mirrors. Estimate: no fake timing claim; eliminates split-brain state and stale ownership.

## Phase 2
- [x] 4. BURST_ALGORITHM | DONE | DOD: Burst `SubmarineMassSolverJob` computes `WaterMassKg = WaterLevel * Volume * 1025.0f` in the job loop. Alternative rejected: managed main-thread room scan. Estimate: 12 us/solve saved.
- [x] 5. AUP_INTEGRITY | DONE | DOD: solver carries `double3 GlobalPivotAnchor`, converts room local AUP offsets into double absolute space, and falls back on finite guard failure. Alternative rejected: deriving authority from `Transform.position` only. Estimate: correctness gate, no raw CPU claim.
- [x] 6. DOD_SOA_LAYOUT | DONE | DOD: `DynamicFloodMassOutput` NativeArray now carries `DynamicCenterOfMassLocal`, `DynamicCenterOfMassOffsetLocal`, `InertiaTensorMultiplier`, and `GlobalPivotAnchor`. Alternative rejected: MonoBehaviour-only truth. Estimate: 4 us/readback saved.
- [x] 7. SIGNAL_FLOW | DONE | DOD: `SubmarineFloodStateSignal` snapshot consumption dirties flood mass state and requests recalculation. Alternative rejected: polling flood owner every FixedTick. Estimate: 8 us/frame saved while flood is idle.

## Phase 3
- [x] 8. LOW_TIER_FAKE | DONE | DOD: low math LOD keeps the existing 1 Hz flood mass cadence and avoids high-tier drag tensor mutation. Alternative rejected: 60 Hz mass mutation on MX350. Estimate: 20-40 us/frame saved on low tier.
- [x] 9. HIGH_END_OVERKILL | DONE | DOD: high tier pushes flood mass into linear and angular 6DOF drag tensor multipliers in `SubmarineFluidDynamics`. Alternative rejected: same scalar damping on RTX and MX350. Estimate: neutral CPU target; buys heavier vehicle feel.
- [x] 10. REACTIVE_VFX | DONE | DOD: pitch > 20 degrees and active flood publishes `BubbleSpawnSignal`; high tier also publishes existing `FluidImpulseSignal` for silt/wake VFX from the same engine vent AUP, while low tier skips the extra impulse. Alternative rejected: continuous particle simulation. Estimate: avoids unbounded VFX work.
- [x] 11. STP_STABILIZATION | DONE | DOD: PID torque is accepted through `FastNlerp` before `PhysicsForceRouter.QueueTorque`. Alternative rejected: raw torque step spikes. Estimate: 3 us/frame cost accepted for stability.

## Phase 4
- [x] 12. NAN_VACCINATION | DONE | DOD: mass divisions use `math.rcp(math.max(value, 0.01f))`; solver, inertia, and telemetry paths finite-guard bad values. Alternative rejected: direct divide. Estimate: correctness gate.
- [x] 13. BLACKBOX_LOGGING | DONE | DOD: 300-frame telemetry ring records dynamic CoM offset, inertia tensor multiplier, PID error, and system stress; crash/NaN dump now writes one canonical `Dump_SUBMARINE_BALLAST_PID_V2.bin` file instead of triplicate fault I/O. Alternative rejected: `Debug.Log` diagnosis and multi-file dump spam. Estimate: 0 B/frame and two fault-time file writes removed.
- [x] 14. TRIPLE_STRIKE_REPAIR | DONE | DOD: torque path uses existing `PhysicsForceRouter.QueueTorque(Rigidbody, Vector3, ForceMode)`; build errors reported by `dotnet build` are outside this path. Alternative rejected: direct Rigidbody torque ownership. Estimate: duplicate force application avoided.
- [x] 15. HOMEOSTASIS_ADAPTATION | DONE | DOD: `SystemStress01 > 0.8` disables the PID D term and flags telemetry. Alternative rejected: full PID during overload. Estimate: 1-2 us/solve saved under stress.
- [x] 16. AUDIO_TIE_IN | DONE | DOD: PID error emits `HullStressSignal` through `IAudioService.QueueHullStressSignal`, with procedural fallback. Alternative rejected: audio polling controller. Estimate: event-driven, 0 idle cost.
- [x] 17. SINKING_THRESHOLD | DONE | DOD: water mass > base mass * 0.4 sets critical flood and suppresses the auto-level PID. Alternative rejected: heroic leveling during catastrophic flood. Estimate: gameplay correctness.
- [ ] 18. FINAL_VALIDATION | BLOCKED BY DEPENDENCY | DOD attempted after the fluid vault-wrapper pass: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` now fails outside PHYSICS/VEHICLES in `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` because `NormalizeVectorOrFallback`, `IsFiniteBounds`, and `IsFiniteVector` are missing. No visible compiler error references the submarine PID, submarine fluid dynamics, or dynamic flood contract files. Alternative rejected: editing AI/Fauna ownership outside this task's domain. Estimate: blocked by concurrent external integration, not frame-time work.

## Loop Log
- Loop 1 | Extracted prompt and mandates; confirmed task count 18 and domain PHYSICS/VEHICLES.
- Loop 2 | Scanned existing submarine PID/flood systems; rejected a parallel controller because the live controller already owns force routing and DataVault aliases.
- Loop 3 | Implemented `SubmarineMassSolverJob`, AUP pivot, inertia tensor output, and signal-driven recalculation.
- Loop 4 | Added high-tier flood drag tensors, tail-heavy bubble signal, PID torque smoothing, stress adaptation, and hull stress audio eventing.
- Loop 5 | Ran Omega scan for `math.rcp`, `Update`, `FixedUpdate`, `Vector3.Distance`, `foreach`, and direct mass divisions; replaced the remaining double mass divide with `math.rcp`.
- Loop 6 | Attempted `dotnet build`; final visible blocker is unrelated fauna assembly namespace drift, so validation is marked `[BLOCKED BY DEPENDENCY]`.
- Loop 7 | Multiplatform pass: changed ballast PID, dynamic flood result, hydro kinematic packet, hydro black box, splash event, and dynamic flood contract structs to explicit fixed-size layouts.
- Loop 8 | H-Phi pass: removed direct `H8Memory.Allocate` fallback from ballast PID and submarine fluid state allocation paths; state now comes from `GlobalDataVault` or fails closed.
- Loop 9 | Signal pass: converted ballast PID consumers from NativeArray snapshots to typed-lane `ReadOnlySpan<T>` and added finite guard policy for `BubbleSpawnSignal`.
- Loop 10 | Rebuild attempt still blocked outside domain by `ProceduralLadderClimbRuntime` / input namespace dependency.
- Loop 11 | H-Phi/Steam Deck pass: evicted persistent PID `NativeArray` fields into DataVault handles, collapsed black-box dump writes to the task-owned file, added high-tier `FluidImpulseSignal` for vent silt/wake, and re-ran build; compile remains blocked by external UI/Core/Homeostasis/Inventory/Tether errors.
- Loop 12 | DataVault sovereignty pass: removed persistent `NativeArray` fields from `SubmarineFluidDynamics`, replaced them with vault-backed buffer handles, purged the local splash NativeQueue path, and kept Burst jobs fed through transient resolved views only at schedule boundaries.
- Loop 13 | Omega validation: task structs in the submarine PID/fluid boundary now use explicit `Pack = 1` layouts where they cross native/binary lanes; `rg` scans for `private NativeArray`, `NativeQueue`, `H8Memory.Allocate`, `Update()`, `FixedUpdate()`, and `string.Format` in the domain returned no hits. A build passed once after the domain fix, but the latest mandatory rerun now fails in external `FaunaBrain.cs` helper drift, so final validation is blocked by dependency.
