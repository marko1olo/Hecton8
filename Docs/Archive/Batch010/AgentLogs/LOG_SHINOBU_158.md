# SHINOBU_158 Log - Buoyancy And Displacement Solver

Date: 2026-05-19
Status: PENDING VERIFICATION

Session initialized. Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; 20 tasks identified. Agent-specific status and rationale files were absent and created fresh.

## 2026-05-19 Static Architecture Audit

What was wrong:
- The force-packet route evidence had stale `NativeQueue` wording while the source uses a Vault-owned force-packet window. That is now documented explicitly instead of hidden.
- Buffer `71621` was a raw cast in `BuoyancyDisplacementBufferIds`; it is now the named `BufferID.ShinobuBuoyancyForcePackets`.
- Large readable buffers requested with `UninitializedMemory` needed a cold deterministic clear before any flow/material/debug/telemetry read could trust flags.
- Compile proof is still blocked by the user gate: CPU sampled at `100%` with 7 `dotnet`/`csc` processes already active. No build was launched.

What was done:
- Added a pure Burst buoyancy lane under `Assets/_Project/Scripts/Physics/Buoyancy/`.
- Added explicit 64-byte `BuoyancyStateDTO` and 64-byte `BuoyancyCounterDTO`; all runtime DTOs are blittable explicit-layout records.
- Added `GenerateMockBuoyantObjectsJob`, `InitializeBuoyancyColdBuffersJob`, `EvaluateBuoyancyJob`, and `ReduceBuoyancyTelemetryJob` with deterministic Burst directives and `[NoAlias]` on NativeArray fields.
- Added Vault buffers for states, force packets, flow samples, tuning, telemetry, cursor, material volumes, CSV scratch, debug forces, counters, and body bindings.
- Added the `PhysicsApplySystem` partial drain bridge and `GlobalPhysicsStateManager` partial body lookup bridge.
- Added the UI Toolkit editor tuner and cold byte-span CSV parser.
- Updated the binary payload ledger, route card, status, and rationale.

Cinematic Cheats used:
- Submerged volume is the "Dear Lie": prebaked scalar volume plus cube-root-ish height and smooth submersion fraction; no runtime mesh intersection.
- Surface equilibrium is faked by near-surface vertical damping and snap-to-surface, avoiding endless harmonic bobbing.
- Abyssal drift falls back to deterministic triangle-wave current when no Vault flow sample owns the object; no fluid simulation.

Exact microseconds saved:
- Static estimate only. Replacing per-object scripts and mesh/sample volume truth with O(n) scalar Burst traversal should save tens to hundreds of microseconds for 1000 loose items on i3/MX350-class hardware. No profiler number is claimed because Unity import, Burst Inspector, Play Mode, and GC captures are still pending.

<SELF_AUDIT agent_id="SHINOBU_158" domain="BUOYANCY_AND_DISPLACEMENT_SOLVER" date="2026-05-19" evidence_class="STATIC_SOURCE" compile_status="BLOCKED_CPU_DOTNET_GATE">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">MONOBEHAVIOUR_BUOYANCY_ERADICATION: source scan found no direct per-object buoyancy `FixedUpdate` plus `Rigidbody.AddForce` offender. Legacy `BuoyancyObject`/`HectonFluidEngine` were logged but not deleted because they own dry-zone/acoustic/player-adjacent behavior outside this route.</TASK>
    <TASK id="02" status="PASS">MESH_VOLUME_CALCULATION_PURGE: `Physics.ComputePenetration` was absent. SHINOBU_158 solver uses prebaked `VolumeCubicMeters` and the sphere/box Dear Lie instead of mesh-volume truth.</TASK>
    <TASK id="03" status="PASS">CS1612_ENCAPSULATION_PURGE: `BuoyancyStateDTO` has public unmanaged fields only; owned-file scan found no hot DTO `get; set;` properties.</TASK>
    <TASK id="04" status="PASS">ARM64_PADDING_RECONSTRUCTION: `BuoyancyStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 64)]` with cached `UnsafeUtility.SizeOf`/field-offset validation.</TASK>
    <TASK id="05" status="PASS">EMERGENCY_MOCK_PHYSICS_DATA: `GenerateMockBuoyantObjectsJob` writes up to 1000 deterministic mock records into Vault state/debug buffers for isolated profiling.</TASK>
    <TASK id="06" status="PASS">BURST_ARCHIMEDES_KERNEL: `EvaluateBuoyancyJob` calculates depth, density, displaced volume, buoyant force, gravity, and finite debug state over flat NativeArrays.</TASK>
    <TASK id="07" status="PASS">FLUID_DRAG_INTEGRATION: drag opposes relative velocity and blends linear/quadratic force through a smooth `GlobalQualityWeight` curve.</TASK>
    <TASK id="08" status="PASS">THE_DEAR_LIE_SURFACE_SNAP: near-surface vertical damping and snap prevent persistent bobbing jitter.</TASK>
    <TASK id="09" status="PASS">MATHEMATICAL_SLEEP_STATE: sleepers return immediately from `EvaluateBuoyancyJob`; stable surface/seafloor states set sleep flags and zero velocity.</TASK>
    <TASK id="10" status="PASS">FORCE_PACKET_ROUTING: Burst writes `BuoyancyForcePacketDTO` rows to Vault buffer `71621`, then `PhysicsApplySystem` drains and applies on main thread. The XML NativeQueue request was superseded by the H-PHI Vault law and one-owner force-window route; no Burst job calls `Rigidbody`.</TASK>
    <TASK id="11" status="PASS">CONTINUOUS_SCALABILITY_DRS: quality weight drives evaluation stride, drag blend, flow amplitude, snap depth, and cheap/exact speed blend. No binary hardware switch was added.</TASK>
    <TASK id="12" status="PASS">ABYSSAL_CURRENT_ADVECTION: job reads Vault flow samples when active and otherwise uses deterministic triangle-wave lateral current.</TASK>
    <TASK id="13" status="PASS">AUP_PRECISION_DEPTH_MATH: depth is `OceanSurfaceAUP - CurrentAUP`, then the local vertical delta is cast to `float`.</TASK>
    <TASK id="14" status="PASS">ROLLBACK_NETCODE_STATE_FENCE: authoritative jobs use `FloatMode.Deterministic`; DTOs are blittable and memcpy-friendly.</TASK>
    <TASK id="15" status="PASS">ZERO_INIT_OVERHEAD_BYPASS: large buffers use `UninitializedMemory`; readable uninitialized buffers are cold-cleared by `InitializeBuoyancyColdBuffersJob` before use.</TASK>
    <TASK id="16" status="PASS">TELEMETRY_BUOYANCY_RECORDER: 300-entry Vault telemetry ring and non-finite dump path `Docs/AgentLogs/Dump_FLUID_DYNAMICS.bin` are implemented.</TASK>
    <TASK id="17" status="PASS">BUOYANCY_TUNER_EDITOR_WINDOW: UI Toolkit tuner writes Vault tuning DTOs and displays cached telemetry. Editor label text still requires a managed string assignment only when values change; this is editor-only and not gameplay hot path.</TASK>
    <TASK id="18" status="PASS">CSV_MATERIAL_VOLUMES_INGESTOR: cold `ReadOnlySpan<byte>` parser writes FNV-1a rows to a fixed Vault-owned open-address NativeArray table. This is the current DataVault-compatible replacement for a persistent `NativeHashMap`.</TASK>
    <TASK id="19" status="PASS">LIVE_FORCE_DEBUG_GIZMO: editor-only `OnDrawGizmos` reads debug DTOs and draws blue buoyancy, red gravity, and green drag vectors.</TASK>
    <TASK id="20" status="FAIL">SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION: this XML audit is appended and static checks passed, but Unity import, C# compile, Burst Inspector, Play Mode, profiler, and GC proof are not available because the CPU/dotnet gate blocked a build.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <BuoyancyStateDTO size_bytes="64" alignment="8-byte fields first, 64-byte cache-line record">
      <FIELD name="CurrentAUP" offset="0" size="24" type="double3" />
      <FIELD name="Velocity" offset="24" size="12" type="float3" />
      <FIELD name="VolumeCubicMeters" offset="36" size="4" type="float" />
      <FIELD name="MassKg" offset="40" size="4" type="float" />
      <FIELD name="EntityHashID" offset="44" size="4" type="uint" />
      <FIELD name="Flags" offset="48" size="4" type="uint" />
      <FIELD name="_pad0" offset="52" size="4" type="uint" />
      <FIELD name="_pad1" offset="56" size="8" type="ulong" />
      <MATH>24 + 12 + 4 + 4 + 4 + 4 + 4 + 8 = 64 bytes. Offsets 0 and 56 are 8-byte aligned; final size is one 64-byte L1 cache line.</MATH>
    </BuoyancyStateDTO>
    <BuoyancyCounterDTO size_bytes="64" false_sharing_guard="true">
      <FIELD name="EvaluatedObjects" offset="0" size="4" />
      <FIELD name="SleepingObjects" offset="4" size="4" />
      <FIELD name="ForcePackets" offset="8" size="4" />
      <FIELD name="NonFiniteCount" offset="12" size="4" />
      <FIELD name="TotalBuoyantForce" offset="16" size="4" />
      <FIELD name="TotalDragForce" offset="20" size="4" />
      <FIELD name="MaxDepthMeters" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <FIELD name="LastEntityHashID" offset="32" size="4" />
      <FIELD name="ComputeMicros" offset="36" size="4" />
      <FIELD name="_pad0" offset="40" size="8" />
      <FIELD name="_pad1" offset="48" size="8" />
      <FIELD name="_pad2" offset="56" size="8" />
      <MATH>40 bytes live data + 24 bytes padding = 64 bytes. Atomic force-packet count sits in a dedicated cache-line DTO.</MATH>
    </BuoyancyCounterDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    `GlobalQualityWeight` is saturated, then shaped by `Smooth01(q) = q*q*(3-2*q)`. `ResolveEvaluationStride` uses `round(lerp(12, 1, Smooth01(q)))`, so q=0.1 evaluates roughly every twelfth record and q below 0.3 remains around ten-to-twelve stride. Drag transitions by `math.lerp(linearDrag, quadraticDrag, qualityCurve)`. `FastSpeed` uses cheap component max at low quality and lerps toward exact sqrt as quality rises. Flow fallback amplitude lerps from 0.08 to 0.55, and snap depth lerps from 0.5 m to tuning depth. Below 0.3, most ALU is shed through stride, near-linear drag, cheap speed, and low-amplitude triangle current; high quality evaluates every record with richer quadratic drag and exact speed.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS private_native_arrays="0" private_native_lists="0" private_native_hashmaps="0">
    <BUFFER id="71620" name="ShinobuBuoyancyStates" handle="_statesHandle" />
    <BUFFER id="71621" name="ShinobuBuoyancyForcePackets" handle="_forcePacketsHandle" />
    <BUFFER id="71622" name="ShinobuBuoyancyFlowSamples" handle="_flowSamplesHandle" />
    <BUFFER id="71623" name="ShinobuBuoyancyTuning" handle="_tuningHandle" />
    <BUFFER id="71624" name="ShinobuBuoyancyTelemetryRing" handle="_telemetryRingHandle" />
    <BUFFER id="71625" name="ShinobuBuoyancyTelemetryCursor" handle="_telemetryCursorHandle" />
    <BUFFER id="71626" name="ShinobuBuoyancyMaterialVolumes" handle="_materialVolumesHandle" />
    <BUFFER id="71627" name="ShinobuBuoyancyCsvScratch" handle="_csvScratchHandle" />
    <BUFFER id="71629" name="ShinobuBuoyancyDebugForces" handle="_debugForcesHandle" />
    <BUFFER id="71630" name="ShinobuBuoyancyCounters" handle="_countersHandle" />
    <BUFFER id="71631" name="ShinobuBuoyancyBodyBindings" handle="_bodyBindingsHandle" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>All SHINOBU_158 Burst NativeArray job fields are annotated `[NoAlias]` where applicable. Force-packet and counter arrays also use `NativeDisableParallelForRestriction` because packet writes are indexed through an interlocked counter and the single counter row is intentionally shared.</NO_ALIAS>
    <HANDLES>Cold initializer is scheduled and completed only during cold startup/hot-swap reacquire. Runtime fixed tick schedules `EvaluateBuoyancyJob` as `evaluateHandle`; `ReduceBuoyancyTelemetryJob` consumes it and returns `_pendingHandle`. PostFixed/LateFrame only complete when `_pendingHandle.IsCompleted` unless teardown/hot-swap forces completion. No arbitrary main-thread `Complete()` is used in the fixed hot path.</HANDLES>
    <CONSUMES>GlobalDataVault handles, `HomeostasisBrain.GlobalQualityWeight`, fixed tick delta, current simulation frame, optional flow samples.</CONSUMES>
    <OUTPUTS>Vault force-packet rows `71621`, debug rows `71629`, counters `71630`, telemetry ring `71624`, mutated sleep flags in states `71620`.</OUTPUTS>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling runtime assembly reference or asmdef was added. Owned buoyancy files use Unity namespaces plus `Hecton8.Core`/`Hecton8.Core.Memory`; static scan found no direct `Hecton8.AI`, `Hecton8.World`, `Hecton8.Gameplay`, `Hecton8.Inventory`, `Hecton8.Environment`, `Hecton8.Vehicles`, `Hecton8.Tools`, `Hecton8.UI`, `Hecton8.VFX`, `Hecton8.Graphics`, `Hecton8.Audio`, `Hecton8.Lighting`, `Hecton8.Thermodynamics`, or `Hecton8.Physiology` usings in the owned route.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    The fake is scalar displacement over prebaked volume, cube-root-ish object height, smooth submersion fraction, snap-to-surface damping, and triangle-wave current fallback. Before: runtime exact volume or point-floater truth trends toward O(n * mesh/sample count) and per-object script dispatch. After: O(n / stride) scalar Burst math, with sleepers reduced to one state read, one branch, and one debug write.
  </THE_DEAR_LIE_CONFIRMATION>
  <VERIFICATION_STATUS>
    Static scans passed for owned files on `Pack=`, hot DTO properties, gameplay `Update`/`FixedUpdate`/`LateUpdate`, direct `Rigidbody.AddForce`, mesh-volume APIs, private NativeArray/List/HashMap allocations, LINQ, `StringBuilder`, numeric `.ToString()`, direct sibling-domain usings, and Burst directive presence. CLI compile was not launched: gate 1 was CPU `100%` with 7 `dotnet`/`csc` processes; final retry after 20 seconds was CPU `94.19%` with 0 `dotnet`/`csc` processes. Unity import, Console, Burst Inspector, profiler, GCMonitor, Play Mode stress, and visual gizmo proof remain pending.
  </VERIFICATION_STATUS>
</SELF_AUDIT>

## 2026-05-19 Polish Addendum - Source Review Corrections

What was wrong:
- Fallback abyssal current used absolute AUP X/Z before casting to `float`; that violates the 100 km AUP rule outside the depth path.
- Low quality still computed enough of the drag path that the q=0.1 collapse was weaker than documented.
- `Awake` could allocate/request Vault buffers outside Play Mode.
- Runtime quality wrote the resolved thermal value back into the authored `GlobalQualityWeight` cap, preventing recovery after a throttle frame.

What was done:
- `EvaluateBuoyancyJob` now forms local `float3` flow coordinates from `CurrentAUP - SectorAUP`.
- Drag now uses `math.step` and `Smooth01`: q<0.25 stays linear and bypasses relative-speed work; q>0.25 blends quadratic drag; q>0.3 allows exact speed interpolation.
- Surface snap now updates state velocity/depth before force construction, so queued force packets match the snapped state.
- `BuoyancyTuningDTO` uses its 124-byte slot as `ResolvedQualityWeight`; `GlobalQualityWeight` stays designer-authored.
- `InitializeBuoyancyColdBuffersJob` uses explicit typed clear loops instead of a generic helper.
- `Awake` is play-mode guarded.

Cinematic Cheats used:
- No new physical truth was added. The solution remains prebaked volume, local triangle current, surface damping/snap, and sleep.

Exact microseconds saved:
- Static estimate only. Below q=0.25, each evaluated object skips drag `lengthsq` and exact-speed eligibility; exact savings require Burst profiler and remain unclaimed.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <AUP_CORRECTION>Fallback current no longer casts absolute X/Z AUP. The object-sector delta is cast to local `float3` before triangle-wave flow.</AUP_CORRECTION>
  <QUALITY_CURVE_CORRECTION>Authored `GlobalQualityWeight` is preserved. Runtime `ResolvedQualityWeight` is written separately, preventing sticky low quality after thermal recovery.</QUALITY_CURVE_CORRECTION>
  <LOW_TIER_COLLAPSE>Below q=0.25, drag remains linear and bypasses relative-speed math; below q=0.3, exact sqrt remains inaccessible.</LOW_TIER_COLLAPSE>
  <EDITOR_GUARD>`Awake` no longer performs Vault boot work outside Play Mode.</EDITOR_GUARD>
  <VERIFICATION_STATUS>Static scans were rerun after source edits; CLI compile still requires CPU <= 50% and no active `dotnet`/`csc` process.</VERIFICATION_STATUS>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Deferred Force Drain Fence

What was wrong:
- Late solver completion could occur in `LateFrameTick` after the post-fixed drain window. The next fixed tick could reset counters and overwrite packet rows before the previous packets reached `PhysicsApplySystem`.

What was done:
- Added `_forcePacketsReadyToDrain`.
- `CompletePendingSolver` marks packet output ready.
- `FixedTick` refuses to schedule/clear the packet window while output is waiting for the physics owner.
- `PostFixedTick` drains through `PhysicsApplySystem` and clears the flag.
- Disable/teardown paths clear the flag after forced completion or handle reset.

Cinematic Cheats used:
- None. This is ownership/fence repair.

Exact microseconds saved:
- No speed claim. This prevents wasted work and force loss; worst low-end behavior is one skipped schedule slot after a late completion instead of a dropped packet window.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <DEPENDENCY_GRAPH_FIX>Late-completed `_pendingHandle` now produces a retained drain state; packet rows cannot be reset by the next scheduler pass before post-fixed drain.</DEPENDENCY_GRAPH_FIX>
  <NO_BLOCKING>Post-fixed still does not arbitrarily block; if the handle is not complete it returns.</NO_BLOCKING>
  <OWNER_ROUTE>Only `PhysicsApplySystem` drains/applies buoyancy packets.</OWNER_ROUTE>
  <VERIFICATION_STATUS>Static source patch only. CPU gate remains required before CLI compile.</VERIFICATION_STATUS>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Bottom Sleep / Sector AUP

What was wrong:
- `SectorAUP` was part of the DTO but not actively stamped by the runtime scheduler, so fallback current could become local only in theory.
- Seafloor sleep still required small net force. That is wrong for a supported body: contact with the bottom is the support constraint, so residual gravity/buoyancy should not keep a settled object burning solver cycles.

What was done:
- `FixedTick` now writes `SectorAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble` before scheduling `EvaluateBuoyancyJob`.
- Seafloor sleep now requires only low speed plus bottom contact. Surface sleep remains force-balanced to avoid freezing unstable floaters.

Cinematic Cheats used:
- Bottom contact is treated as a deterministic support constraint and converted into sleep instead of simulating contact-force truth in this solver.

Exact microseconds saved:
- Static estimate only. Every settled bottom object now skips density/drag/flow/packet work on following frames; profiler proof is still pending behind the CPU/build gate.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <SECTOR_AUP_BINDING>Runtime scheduler stamps `SectorAUP` from floating-origin AUP before job scheduling.</SECTOR_AUP_BINDING>
  <BOTTOM_SLEEP>Seafloor contact plus low velocity sets `FlagSleeping` without requiring force equilibrium.</BOTTOM_SLEEP>
  <SURFACE_SLEEP>Surface sleep still requires snap state plus force threshold.</SURFACE_SLEEP>
  <VERIFICATION_STATUS>Static source patch only. CPU gate remains required before CLI compile.</VERIFICATION_STATUS>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - True Strided Scheduling

What was wrong:
- `EvaluationStride` was enforced inside `EvaluateBuoyancyJob`, so low quality skipped math but still scheduled one work item per active state.
- If an active set was smaller than the current stride and the frame offset owned no row, returning without incrementing `_simulationFrame` could pin the route to an empty offset.
- `ReduceBuoyancyTelemetryJob` counted `FlagForceQueued` and `FlagEvaluated` from stale debug rows produced on older strided frames.

What was done:
- `FixedTick` now computes `EvaluationOffset` and schedules only the strided subset count.
- `EvaluateBuoyancyJob` maps `workIndex` to the actual state index with `(workIndex * stride) + offset`.
- Empty-offset frames schedule a reduce-only telemetry job so the deterministic frame/offset rotation advances.
- Telemetry now preserves force packet count from the padded counter and only accumulates current-frame evaluated force data.

Cinematic Cheats used:
- The low-quality route intentionally evaluates a temporal subset of objects. It trades exact per-frame fluid truth for stable round-robin force refresh under thermal pressure.

Exact microseconds saved:
- Static estimate only. At q~0.1, 1000 active states schedule roughly 83-84 parallel-for iterations instead of 1000. Burst/profiler proof is still blocked by the CPU gate.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <SCHEDULER_COLLAPSE>Stride is now a scheduler-level work-count reduction, not only an in-job early return.</SCHEDULER_COLLAPSE>
  <ROUND_ROBIN_GUARD>Empty-offset frames still advance `_simulationFrame` through a reduce-only telemetry job.</ROUND_ROBIN_GUARD>
  <TELEMETRY_FRESHNESS>Evaluated force totals require `debug.FrameIndex == SimulationFrame`; packet count comes from `BuoyancyCounterDTO.ForcePackets`.</TELEMETRY_FRESHNESS>
  <BUILD_GATE>Latest gate: `dotnet/csc=0`, CPU load `100%`; CLI compile not launched.</BUILD_GATE>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Unity Meta Stabilization

What was wrong:
- New SHINOBU_158 C# assets had no checked-in `.meta` files, leaving Unity import to assign GUIDs locally.

What was done:
- Added fixed `MonoImporter` `.meta` files for `BuoyancyDisplacementContracts`, `BuoyancyDisplacementJobs`, `BuoyancyDisplacementRuntime`, `GlobalPhysicsStateManager.BuoyancyBridge`, `PhysicsApplySystem.BuoyancyQueue`, and `HydrodynamicBuoyancyTunerWindow`.
- Added fixed `DefaultImporter` `.meta` files for the new `Physics/Buoyancy` and `Editor/Physics` asset folders.

Cinematic Cheats used:
- None. This is editor/import determinism.

Exact microseconds saved:
- No frame-time claim. This prevents GUID churn and avoidable import/reference instability.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <UNITY_META>All new SHINOBU_158 C# assets and folders now have fixed `.meta` files.</UNITY_META>
  <RUNTIME_COST>No runtime cost; editor/import stability only.</RUNTIME_COST>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Runtime Rot Pass

What was wrong:
- Layout validation used `typeof(T).GetField` through the `Validate()` path.
- Unity lifecycle could run CSV load and emergency mock generation twice: once in `Awake`, once in `OnEnable`.
- `_forcePacketsReadyToDrain` could stay true forever if DataVault or packet handles disappeared before post-fixed drain.
- Telemetry `LastNetForce` used the last debug array row, which can be stale when stride skips rows.
- Fatal dump satisfied the XML name but not the AGENTS `Dump_[ID].bin` artifact name.
- Editor tuner relied on folder placement instead of an explicit `#if UNITY_EDITOR` guard.

What was done:
- Layout offset checks now use constants and contain no reflection path.
- Added `_coldBootCompleted` and an `EnsureColdBooted()` path used by both `Awake` and `OnEnable`.
- Post-fixed drain clears stale readiness when the Vault route cannot be resolved.
- Telemetry now stores a sanitized current-frame `LastNetForce`.
- Fault dump writes both `Docs/AgentLogs/Dump_FLUID_DYNAMICS.bin` and `Docs/AgentLogs/Dump_SHINOBU_158.bin`.
- Wrapped `HydrodynamicBuoyancyTunerWindow.cs` in `#if UNITY_EDITOR`.

Cinematic Cheats used:
- None added. This pass removed lifecycle/reflection/forensic rot.

Exact microseconds saved:
- Static estimate only: normal Play Mode cold startup avoids one duplicate CSV read and one duplicate mock-state job. Hot-path frame savings are not claimed.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <RUNTIME_REFLECTION>Layout validation no longer calls `typeof(T).GetField`.</RUNTIME_REFLECTION>
  <COLD_BOOT>CSV ingest and mock generation are idempotent per Vault acquisition.</COLD_BOOT>
  <DRAIN_DEADLOCK>Missing Vault/packet handles clear stale drain readiness instead of blocking future fixed ticks.</DRAIN_DEADLOCK>
  <BLACKBOX_PATHS>Fault path writes `Dump_FLUID_DYNAMICS.bin` and `Dump_SHINOBU_158.bin`.</BLACKBOX_PATHS>
  <BUILD_GATE>Latest gate: `dotnet/csc=0`, CPU load `100%`; CLI compile not launched.</BUILD_GATE>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - AsRef State Mutation

What was wrong:
- The state DTO was property-free, but hot mutation still used `NativeArray` indexer writeback instead of the requested raw-ref mutation route.

What was done:
- `EvaluateBuoyancyJob` now mutates the authoritative state row through `UnsafeUtility.AsRef<BuoyancyStateDTO>`.
- `GenerateMockBuoyantObjectsJob` uses the same raw-ref state write path for cold mock seeding.
- Static scan confirms no direct `States[index]` setter remains.

Cinematic Cheats used:
- None. This is mutation-path hardening.

Exact microseconds saved:
- Static estimate only. It removes an indexer writeback path per evaluated state; Burst profiler proof remains blocked by CPU gate.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <STATE_MUTATION>`BuoyancyStateDTO` writes now route through `UnsafeUtility.AsRef<BuoyancyStateDTO>` in both solver and mock jobs.</STATE_MUTATION>
  <CS1612_GUARD>No DTO properties and no direct `States[index]` setter remain in SHINOBU_158 jobs.</CS1612_GUARD>
  <BUILD_GATE>Latest gate: `dotnet/csc=0`, CPU load `100%`; CLI compile not launched.</BUILD_GATE>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Strided Job Safety Restriction

What was wrong:
- True strided scheduling changed the write index from `workIndex` to `(workIndex * EvaluationStride) + EvaluationOffset`.
- Unity's default `IJobParallelFor` write restriction cannot infer that this mapped index is disjoint, so the source-level proof was weaker than the scheduler math.

What was done:
- Added `[NativeDisableParallelForRestriction]` to solver `States` and `DebugForces`, where the non-workIndex writes occur.
- Added the same restriction to mock `States`, which is written through a raw pointer in a parallel job.
- Kept `[NoAlias]` on independent buffers.
- Recorded the injective mapping proof in the route card and binary payload ledger.

Cinematic Cheats used:
- None. This is job safety proof, not simulation behavior.

Exact microseconds saved:
- No new speed claim. This preserves the existing q~0.1 scheduler collapse of roughly 83-84 work items per 1000 active states instead of reverting to 1000 branch-only work items.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <PARALLEL_WRITER_PROOF>For fixed `stride >= 1` and fixed `offset`, `workIndexA != workIndexB` implies `(workIndexA * stride + offset) != (workIndexB * stride + offset)`.</PARALLEL_WRITER_PROOF>
  <SAFETY_SCOPE>`NativeDisableParallelForRestriction` is limited to mapped writer buffers and intentional shared force counter/packet writers.</SAFETY_SCOPE>
  <BUILD_GATE>Latest gate remains CPU-bound; compile must wait for CPU <= 50% and no `dotnet`/`csc` process.</BUILD_GATE>
</SELF_AUDIT_ADDENDUM>

## 2026-05-19 Polish Addendum - Emergency Mock Producer Gate

What was wrong:
- Cold boot could seed the 1000-object emergency mock whenever the serialized mock flag was true.
- That can overwrite a real producer's state rows if inventory/drop ownership has already populated the Vault.

What was done:
- `BuoyancyTuningDTO.Default()` now starts with `ActiveStateCount = 0`.
- Cold boot now calls `GenerateMockBuoyantObjects()` only if the tuning row still reports zero active state rows.
- The mock remains available for no-inventory CI/profiling; live producer data has priority.

Cinematic Cheats used:
- None. This is owner-route protection.

Exact microseconds saved:
- No frame-time claim. Cold boot skips one 4096-row mock schedule when live producer state exists.

<SELF_AUDIT_ADDENDUM agent_id="SHINOBU_158" date="2026-05-19" evidence_class="STATIC_SOURCE">
  <MOCK_GATE>Emergency mock generation is gated by `ActiveStateCount <= 0`; producer-owned active rows are not overwritten.</MOCK_GATE>
  <CI_FALLBACK>Zero active rows still seed the deterministic 1000-object mock for isolated profiling.</CI_FALLBACK>
  <BUILD_GATE>Compile remains blocked until CPU <= 50% and no `dotnet`/`csc` process is active.</BUILD_GATE>
</SELF_AUDIT_ADDENDUM>
