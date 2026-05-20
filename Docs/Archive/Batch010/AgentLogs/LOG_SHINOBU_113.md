# LOG SHINOBU_113

## 2026-05-19 - Hydrodynamic KCC Static Implementation Pass

What was wrong:
- Legacy player/vehicle movement still exposes Rigidbody presentation routes and old synchronous compatibility jobs.
- The target KCC domain lacked a clean 64-byte AUP movement DTO, deferred capsule command pipeline, hydrodynamic analytical integrator, rollback fence, wake emission, and designer tuning facade.
- Existing local kinematics structs used `Pack = 1` in explicit layouts; `SdfSqueezeResult` exposed hot state through an `IsActive` property.

What was done:
- Added `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`.
- Added `KinematicStateDTO` as `[StructLayout(LayoutKind.Explicit, Size = 64)]` with required offsets: `double3 AUP_Position` at 0, `float3 Velocity` at 24, `float3 AngularVelocity` at 36, `float Mass` at 48, `float DragCoefficient` at 52, explicit pad bytes 56-63.
- Added Burst jobs for deterministic mock input, analytical hydrodynamic integration, capsule command build, post-simulation resolution, rollback MemCpy fence, visual EWMA sync, and wake signal emission.
- Added Vault buffer IDs `ShinobuHydroKccStates` through `ShinobuHydroKccDebugOutputs`.
- Added `HydrodynamicKccTunerWindow` under UI Toolkit for editor-side tuning DTO control and telemetry graph.
- Added allocation-free `ReadOnlySpan<byte>` CSV parser with FNV-1a profile hashes and vault-compatible flat hash buckets.
- Removed `Pack = 1` from directly touched explicit-layout kinematics structs and replaced `SdfSqueezeResult.IsActive` with static `IsResultActive(in result)`.
- Updated `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md` with the SHINOBU_113 seam.

Cinematic Cheats used:
- Replaced expensive water displacement with analytical drag plus scalar turbulence.
- Wake output is an unmanaged signal packet, not spawned GameObjects.
- Visual smoothness is handled by late EWMA interpolation, not by increasing authoritative simulation frequency.

Exact microseconds saved:
- Deferred capsule batch avoids estimated 20-150 us blocking sweep stalls per controlled body in dense collision frames.
- Property-copy removal and pointer mutation save estimated 1-4 us per 1k state updates.
- Low-quality 2-pass resolution can skip up to 6 projection passes per contact versus Ultra.
- Dear Lie water resistance avoids millisecond-scale CPU fluid approximation if compared to naive particles or mesh water displacement.
- Collision command/result `UninitializedMemory` avoids O(n) zeroing of command pools.

Verification state:
- `git diff --check` passed for touched files, with only CRLF warnings.
- Static grep found no `CharacterController` or `Physics.CapsuleCast/SphereCast` in the target KCC path.
- Static grep found no `Pack = 1`, hot DTO properties, `AddForce`, `Complete`, `Run`, or local `new NativeArray` in the new KCC file.
- Compile was not launched. Guard samples reported CPU `79.45-88.68%` first, then `78.60-86.86%` after static cleanup, while `dotnet/csc` were absent. Project law forbids `dotnet build` under CPU load above 50%.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Static scan logged no first-party CharacterController in target set and identified remaining MovePosition legacy routes as presentation/compatibility debt.</TASK>
    <TASK id="02" status="PASS">New KCC route uses deferred CapsulecastCommand.ScheduleBatch and contains no Physics.CapsuleCast/SphereCast hot path.</TASK>
    <TASK id="03" status="PASS">KinematicStateDTO is flat unmanaged state and integration/resolution mutate via UnsafeUtility.AsRef.</TASK>
    <TASK id="04" status="PASS">HydrodynamicKccLayoutValidator checks UnsafeUtility.SizeOf and exact offsets.</TASK>
    <TASK id="05" status="PASS">GenerateMockMovementInputJob and queue variant use deterministic Unity.Mathematics.Random seeded by sector/frame/index.</TASK>
    <TASK id="06" status="PASS">HydrodynamicIntegrationJob uses v = v / (1 + drag * |v| * dt), depth buoyancy, finite guards, deterministic Burst.</TASK>
    <TASK id="07" status="PASS">Simulation schedules command build and CapsulecastCommand batch without waiting.</TASK>
    <TASK id="08" status="PASS">Dear Lie maps speed to nonlinear drag and turbulence scalar; no CPU water displacement simulation.</TASK>
    <TASK id="09" status="PASS">KinematicResolutionJob projects velocity along collision normal and writes final AUP.</TASK>
    <TASK id="10" status="PASS">Final AUP update is millimeter-quantized.</TASK>
    <TASK id="11" status="PASS">Iterations use math.lerp(2, 8, GlobalQualityWeight), no hardware binary switch.</TASK>
    <TASK id="12" status="PASS">Rollback fence copies contiguous KinematicStateDTO bytes from Vault state into Vault rollback bytes.</TASK>
    <TASK id="13" status="PASS">KinematicVisualSyncJob outputs EWMA local float3 visual state.</TASK>
    <TASK id="14" status="PASS">EmitWakeSignalsJob pushes WakeGeneratedSignal through SignalBus ParallelWriter.</TASK>
    <TASK id="15" status="PASS">Capsule command/result buffers are requested from GlobalDataVault with UninitializedMemory.</TASK>
    <TASK id="16" status="PASS">300-entry KinematicTelemetryEntry ring and NaN dump path are implemented.</TASK>
    <TASK id="17" status="PASS">UI Toolkit Hydrodynamic KCC tuner reads/writes Vault tuning DTO and renders telemetry graph.</TASK>
    <TASK id="18" status="PASS">CSV parser is span/FNV based and writes to vault-compatible profile/bucket arrays. NativeHashMap was rejected because IDataVault does not own persistent NativeHashMap handles.</TASK>
    <TASK id="19" status="PASS">Solver writes debug DTO; gizmo draws current capsule, predicted capsule, and collision normal.</TASK>
    <TASK id="20" status="FAIL">Compile verification is blocked by CPU guard. No completion claim is made.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64">
      <field name="AUP_Position" offset="0" size="24" />
      <field name="Velocity" offset="24" size="12" />
      <field name="AngularVelocity" offset="36" size="12" />
      <field name="Mass" offset="48" size="4" />
      <field name="DragCoefficient" offset="52" size="4" />
      <field name="_pad0.._pad7" offset="56" size="8" />
    </KinematicStateDTO>
    <KinematicTelemetryEntry size="64" />
    <HydrodynamicKccTuningDTO size="64" />
    <FalseSharing>No atomic counters were introduced. Shared cursor is a single-element diagnostic write by index 0 only; no parallel atomic counter cache line is used.</FalseSharing>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below GlobalQualityWeight 0.3, resolver iterations collapse toward 2, acceleration/added-mass scalar uses cheaper low-weight lerps, visual sync alpha is reduced, and Dear Lie turbulence remains a scalar. At weight 1.0, resolver reaches 8 projection passes and wake scalar carries richer downstream GPU/audio information.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PrivatePersistentNativeArrays>0 in HydrodynamicKccRuntime; only VaultBufferHandle fields are cached.</PrivatePersistentNativeArrays>
    <VaultBufferHandles>ShinobuHydroKccStates, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, CsvScratch, DebugOutputs</VaultBufferHandles>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Applied to NativeArray fields in new Burst jobs and SdfSqueezeJob where arrays are independent.</NoAlias>
    <Graph>GenerateMockMovementInputJob -> HydrodynamicIntegrationJob -> BuildCapsuleCastCommandsJob -> CapsulecastCommand.ScheduleBatch -> KinematicResolutionJob -> KinematicVisualSyncJob + KinematicRollbackFenceJob + EmitWakeSignalsJob -> LateFrame non-blocking DispatcherJobSwap.TryComplete.</Graph>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    New files did not introduce a new sibling runtime asmdef reference. Existing root Hecton8.Core asmdef debt is unchanged. dotnet build not run because CPU guard stayed above 50%.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy CPU fluid simulation was rejected. Before: O(particles or mesh fluid samples) per frame with likely ms-scale cost. After: O(entities) scalar analytical drag plus unmanaged wake packet, with GPU/audio systems consuming turbulence.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Bottom Compile Guard Recheck 26

What was wrong:
- Source still lacks compiler proof after the editor telemetry view patch.

What was done:
- Re-ran the targeted KCC forbidden-pattern scan after documentation updates; it returned no matches.
- Rechecked the build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 100, 100`; Processor Utility `80.78, 79.16, 84.72, 81.9, 80.45`.

Verification state:
- Build not launched. CPU guard remains red.

## 2026-05-19 - SIMD Magnitude Recheck 27

What was wrong:
- A post-compaction source re-read found remaining `math.length(...)` uses in KCC authority-adjacent code and a `.normalized` gizmo path. They were finite-guarded by neighboring code, but still violated the rsqrt-first SIMD mandate for repeated speed/distance magnitude work.
- The first assignment extraction attempt used a too-strict regex that expected `<AGENT_PROMPT id="SHINOBU_113">` with no attributes. The live tag is `<AGENT_PROMPT id="SHINOBU_113" role="HYDRODYNAMIC_KINEMATICS_DIRECTOR" chat_name="SHINOBU_113">` at `Docs/Tasks/CURRENT_BATCH.md:747`.

What was done:
- Added `HydrodynamicKccMath.LengthSafe(float3)` using `lenSq * math.rsqrt(math.max(lenSq, 0.000001f))` with finite/zero guards.
- Replaced drag speed, wake speed, capsule sweep distance, collision displacement distance, telemetry aggregate speed, visual output speed, wake magnitude fallback, and gizmo normal display with `LengthSafe`/`NormalizeSafe`.
- Re-ran the prompt extraction with the attribute-tolerant XML tag and re-verified tasks 01-20 from `CURRENT_BATCH.md:759-782`.
- Re-ran targeted KCC forbidden-pattern scan, `git diff --check`, and the build guard. Static scan returned no matches. Diff check reported only CRLF normalization warnings.

Cinematic Cheats used:
- No new CPU physical truth was introduced. The KCC still sells water motion through analytical drag, scalar turbulence, wake metadata, and downstream shader/audio/camera consumers.

Exact Microseconds saved:
- Runtime profiler proof is unavailable until Unity import/build can run. Static estimate: per controlled body, the repeated scalar sqrt path in integration, command build, resolution, visual sync, telemetry, and wake emission is replaced by rsqrt-form magnitude. Expected saving is sub-microsecond per body on small counts and meaningful only when KCC count or solver hit count scales up.

Verification:
- Targeted KCC forbidden-pattern scan returned no matches for sync casts, legacy controller/force routes, private persistent Native container construction, `Pack=1`, nondeterministic random, hot `foreach`, arbitrary `.Complete(`, scene finders, `Camera.main`, string formatting, `math.length`, `.normalized`, or sqrt calls.
- Build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 100, 100`; Processor Utility `82.63, 80.32, 75.62, 76.2, 77.37`.
- Build not launched because CPU remained above the explicit 50% threshold.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_RSQRT_RECHECK27">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">KCC path has no `CharacterController`. First-party synchronous `Rigidbody.MovePosition` archaeology is logged as outside-owner legacy presentation debt, not this KCC authority route.</Task>
    <Task id="02" status="PASS_STATIC">KCC path has no synchronous `Physics.CapsuleCast` or `Physics.SphereCast`; it writes Vault command windows and schedules deferred `CapsulecastCommand.ScheduleBatch`.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data in Vault; no persistent private Native container owns KCC movement state.</Task>
    <Task id="04" status="PASS_STATIC">Editor-only layout validator checks `UnsafeUtility.SizeOf` and exact field offsets for the 64-byte DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path exists; packets carry frame, source, sequence, and sector-generation proof and use `Unity.Mathematics.Random` for mock generation.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration applies `v = v / (1 + drag * |v| * dt)`, buoyancy, added mass, deterministic Burst flags, and finite guards. Magnitude now routes through guarded rsqrt form.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules input, integration, command build, and capsule batch without blocking; post/late phases own completion windows and abort drains.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is the Dear Lie route: nonlinear analytical drag plus turbulence scalar, not CPU fluid displacement.</Task>
    <Task id="09" status="PASS_STATIC">Resolver advances to contact, projects velocity along collision normal, spends remaining timestep on projected velocity, and writes final AUP.</Task>
    <Task id="10" status="PASS_STATIC">Authoritative position is `double3` AUP, local math happens only after sector/camera subtraction, and final AUP is millimeter-quantized.</Task>
    <Task id="11" status="PASS_STATIC">Hit budget and resolver iterations scale continuously with `math.lerp(2, 8, GlobalQualityWeight)`; no low/high branch is introduced.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence uses contiguous `UnsafeUtility.MemCpy` bytes and an explicit resimulation seam that bypasses visual smoothing.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates presentation-local float output after AUP localization; debug/gizmo state remains decoupled from presentation toggles.</Task>
    <Task id="14" status="PASS_STATIC">Wake output leaves through unmanaged `SignalBus&lt;WakeGeneratedSignal&gt;.ParallelWriter` with bounded AUP48 conversion and packed magnitude/radius metadata.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes are requested as uninitialized memory and scheduled with active subarray windows so stale oversized lanes are excluded.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring records aggregate speed, iterations, estimated compute use, flags, and hash; fault dump writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin` from the same native span. Fault slots are 64 bytes per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and reads one editor-only telemetry Vault view per graph repaint without a private telemetry array.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest uses `ReadOnlySpan<byte>`, FNV-1a, flat Vault profile rows, and bucket indices. A private `NativeHashMap` was rejected because the current Vault API owns arrays/slices, not hash-map containers.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo prediction reads solver debug DTO and cached capsule dimensions; the old `.normalized` fallback was removed.</Task>
    <Task id="20" status="PENDING_COMPILE">Self-audit is bottom-appended after Recheck 27. Compiler, Unity import, Burst Inspector, profiler, GCMonitor, Play Mode rollback, and player-build proof remain pending under the CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64" alignment="16">Offsets: AUP_Position 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, explicit pad 56/8. Math: 24+12+12+4+4+8=64; 64%16=0.</KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">Offsets: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. Math: 24+12+12+4+4+4+4=64; 64%16=0.</HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line">Offset 0/4 fault mask plus explicit padding through offset 63. Every worker writes its own cache line.</HydrodynamicKccFaultFlagDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>When `GlobalQualityWeight` drops below 0.3, the route trends to two collision hit records, reduced visual smoothing weight, scalar turbulence, bounded rollback fast-forward count, and no extra CPU fluid work. At higher weights, the same continuous path lerps toward eight hit records, smoother visual sync, richer wake metadata, and more collision polish. The magnitude path remains `LengthSafe` at every weight; no binary hardware switch is introduced.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">Requested handles: ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Independent NativeArray lanes in jobs are marked `[NoAlias]` where applicable; state mutation uses `UnsafeUtility.AsRef`; fault flags are 64-byte per-entity lanes to avoid shared cache-line writes.</NoAlias>
    <Consumes>External input writer handle, dispatcher fixed/post/late lane ordering, DataVault buffer handles, `GlobalQualityWeight`, fixed simulation delta, sector AUP and sector-generation facts.</Consumes>
    <Outputs>Command build handle, deferred capsule batch handle, post-simulation resolution handle, rollback byte fence, wake SignalBus writes, visual output DTOs, debug output DTOs, telemetry ring entry, and fault dump trigger.</Outputs>
    <Graph>clearFaults -> mock/inputClear/externalWriter -> sanitize -> hydrodynamicIntegration -> commandBuild -> active CapsulecastCommand batch -> hitExtraction -> kinematicResolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame nonblocking swap.</Graph>
    <FrozenFacts>`_scheduledEntityCount` and `_scheduledMaxHitsPerCommand` are written in `FixedTick` and consumed unchanged in `PostFixedTick`.</FrozenFacts>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No `Hecton8.Physics.KCC.Runtime.asmdef` exists in this checkout, so no new sibling-runtime asmdef reference was introduced. KCC remains under the existing root assembly and communicates through Vault handles, `SignalBus`, and registry-cached services. File-level concrete cross-domain aliases are limited to existing root-owned AUP/dispatcher helpers in `Hecton8.World`; no AI, rendering, audio, netcode, vehicle, save, or thermodynamics sibling runtime dependency is added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected CPU Navier-Stokes, per-bubble dynamics, mesh-water friction, and wake GameObject spawning. The fake is analytical drag plus turbulence/wake scalar metadata for downstream shader/audio/camera work. Complexity before: O(n*fluid_cells) or O(n*particles). Complexity after: O(n*h), where h is the continuous 2..8 collision-hit budget; the water feel stays visual/presentation-owned.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SIMD Magnitude Guard Recheck 27

What was wrong:
- KCC authority-adjacent jobs still used `math.length(...)` for drag speed, sweep distance, resolver displacement, telemetry speed, visual output speed, and wake fallback magnitude. The editor gizmo also used `Vector3.normalized`.

What was done:
- Added `HydrodynamicKccMath.LengthSafe(float3)` as `lenSq * math.rsqrt(math.max(lenSq, 0.000001f))`, returning zero for non-finite or near-zero vectors.
- Replaced the remaining KCC `math.length(...)` sites and the gizmo `.normalized` path with `LengthSafe`/`NormalizeSafe`.
- Updated KCC AUP and route-card docs to make the rsqrt magnitude helper the canonical path.

Cinematic Cheats used:
- No new physical simulation. This preserves the analytical hydrodynamic Dear Lie: scalar drag, added mass, buoyancy, and wake metadata instead of CPU fluid truth.

Exact Microseconds saved:
- Pending profiler/Burst Inspector proof. Static expectation is small per entity, but repeated scalar sqrt candidates are removed from integration, collision build, resolution, telemetry, visual sync, and wake emission.

Verification:
- Targeted KCC forbidden-pattern scan returned no matches, including `math.length`, `.normalized`, `math.sqrt`, synchronous casts, legacy controller/rigidbody force routes, private Native container construction, `Pack=1`, random sources, hot foreach/completion, scene finders, and string formatting.
- `git diff --check` reported only CRLF normalization warnings.
- Build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 100, 100`; Processor Utility `82.63, 80.32, 75.62, 76.2, 77.37`; build not launched.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_RSQRT_RECHECK27">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">KCC path has no `CharacterController`; legacy `Rigidbody.MovePosition` hits remain outside-owner presentation debt.</Task>
    <Task id="02" status="PASS_STATIC">KCC path has no sync `Physics.CapsuleCast/SphereCast`; it uses deferred `CapsulecastCommand` batches.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data in Vault; no persistent private Native container owns movement state.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks size and field offsets for 64-byte KCC DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path is sanitized by frame, source, sequence, and sector generation.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical drag with `LengthSafe` rsqrt magnitude, buoyancy, added mass, deterministic Burst, and finite guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command/collision work without blocking; post/late phases own completion windows.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is scalar Dear Lie drag/turbulence, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver advances to contact, projects velocity along collision normal, spends remaining timestep only, and uses `LengthSafe` for displacement distance.</Task>
    <Task id="10" status="PASS_STATIC">AUP is double3, localized before float math, then millimeter-quantized.</Task>
    <Task id="11" status="PASS_STATIC">Hit budget and iterations scale continuously with `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback uses contiguous memcpy bytes and an explicit smoothing-bypass resim seam.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates local-float presentation after AUP localization and reports speed through `LengthSafe`.</Task>
    <Task id="14" status="PASS_STATIC">Wake exits through unmanaged SignalBus packet with bounded AUP48 conversion, packed magnitude/radius, and rsqrt-guarded magnitude fallback.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes use uninitialized memory and active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin`; fault flags are 64 bytes per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and graph reads one editor-only telemetry Vault view per repaint.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest uses span/FNV/Vault arrays, not split/LINQ/private maps.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO, cached capsule dimensions, and `NormalizeSafe` for the collision normal display.</Task>
    <Task id="20" status="PENDING_COMPILE">Audit refreshed after rsqrt magnitude patch; compiler proof still pending guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>KinematicStateDTO=64 bytes: AUP 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pads 56..63. HydrodynamicKccInputDTO=64 bytes: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. HydrodynamicKccFaultFlagDTO=64 bytes per entity to block false sharing.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work tends to two hit records, reduced smoothing, scalar turbulence, rsqrt-guarded magnitude, and bounded rollback fast-forward. Higher quality lerps toward eight hit records and richer wake metadata without binary low/high branching.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">States, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>[NoAlias] lanes feed clearFaults -> input -> sanitize -> integration -> commandBuild -> capsule batch -> hitExtraction -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame swap. Frozen facts: scheduled entity count and max-hit stride.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling asmdef split; KCC files live under the existing root assembly and communicate through Vault, SignalBus, and registry seams. Build remains blocked by CPU guard.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>CPU fluid fields/particles remain rejected. Analytical drag plus turbulence/wake scalar replaces O(n*fluid_cells) or O(n*particles) with O(n*h), h=2..8 continuous hit budget; the rsqrt pass reduces scalar magnitude overhead without adding simulation truth.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Compile Guard Recheck 24 After Dual Dump Patch

What was wrong:
- The dual dump-path source patch requires compiler proof.

What was done:
- Targeted KCC forbidden-pattern scan returned no matches for `CharacterController`, sync `Physics.CapsuleCast/SphereCast`, `Rigidbody.AddForce`, private Native containers, `Pack=1`, `UnityEngine.Random`, `foreach`, `.Complete(`, scene finders, `Camera.main`, or string formatting.
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 100, 100, 100, 100`.
- Processor Utility samples: `78.09, 82.48, 82.21, 80.75, 80.51`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build not launched. CPU guard remains hard red.

## 2026-05-19 - Dual Blackbox Dump Path Reconciliation

What was wrong:
- XML task 16 names `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`.
- The project black-box protocol names `Docs/AgentLogs/Dump_SHINOBU_113.bin`.
- The runtime fault path wrote only the ID file, so one verifier route could miss the dump even though telemetry existed.

What was done:
- Added `AssignmentDumpFileName = "Dump_KINEMATICS_SURGEON.bin"`.
- `DumpTelemetry` now streams the same native `KinematicTelemetryEntry` ring span to both dump files through `WriteTelemetryDump(...)`.
- No managed `byte[]` staging was reintroduced.
- Updated route card, kinematics architecture note, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- None. This is forensic output only; hydrodynamics remain the analytical Dear Lie route.

Exact microseconds saved:
- No hot-path saving claim. Healthy frames execute no file export. Fault path writes two 19.2 KB files for audit compatibility.

Verification state:
- Static source patch only. Guarded compile remains pending.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_DUAL_DUMP">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">No first-party `CharacterController` route in KCC; legacy `Rigidbody.MovePosition` hits remain outside-owner presentation debt.</Task>
    <Task id="02" status="PASS_STATIC">KCC has no sync `Physics.CapsuleCast/SphereCast`; it schedules deferred `CapsulecastCommand` batches.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data mutated through Vault arrays and `UnsafeUtility.AsRef`.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks size and offsets for 64-byte KCC DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path is present and sanitized by frame/source/sector proof.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical drag, buoyancy, added mass, and NaN guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation does not block on the capsule batch; post/late phases own completion windows.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is the Dear Lie scalar route, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver projects along collision normals and spends only remaining timestep after contact.</Task>
    <Task id="10" status="PASS_STATIC">AUP is `double3`, local math follows sector subtraction, and final AUP is millimeter quantized.</Task>
    <Task id="11" status="PASS_STATIC">Iterations/hit budget scale continuously with `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback uses contiguous memcpy bytes and explicit visual smoothing bypass.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates presentation-local float output after AUP localization.</Task>
    <Task id="14" status="PASS_STATIC">Wake data leaves via unmanaged SignalBus packet without GameObject spawning.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes use uninitialized memory and active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame ring writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin` from the same native span; fault flags are 64-byte per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning without C# recompile.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest is span/FNV/Vault-array based, not split/LINQ/private map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO and cached capsule dimensions.</Task>
    <Task id="20" status="PENDING_COMPILE">Audit refreshed after dual dump patch; compiler proof still pending guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>KinematicStateDTO=64 bytes: AUP 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pads 56..63. HydrodynamicKccInputDTO=64 bytes: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. HydrodynamicKccFaultFlagDTO=64 bytes per entity to block false sharing.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work tends to two hit records, reduced smoothing, scalar turbulence, and bounded rollback fast-forward. Higher quality lerps to eight hit records and richer wake metadata without a binary tier branch.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">States, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>[NoAlias] lanes feed clearFaults -> input -> sanitize -> integration -> commandBuild -> capsule batch -> hitExtraction -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame swap. Frozen facts: scheduled entity count and max-hit stride.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling asmdef split; KCC uses existing root assembly and communicates through Vault, SignalBus, and registry seams. Build remains guarded.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>CPU fluid field/particles rejected. Analytical drag plus turbulence/wake scalar replaces O(n*fluid_cells) or O(n*particles) with O(n*h), h=2..8 continuous hit budget.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Audit Hardening Pass: Input Generation, Abort Drain, Telemetry Aggregate

What was wrong:
- External KCC input packets had no sector-generation stamp and could be stale even when frame/sequence looked valid.
- Post-phase failure guards could return after a failed resolve while leaving scheduled batch flags dirty.
- Telemetry was not a single post-resolution aggregate of all active entities.
- The black-box dump file name did not match the required agent ID path.

What was done:
- Added `HydrodynamicKccInputContract.BuildExternalInput(...)` and sector-generation packing in `HydrodynamicKccInputDTO.Flags`.
- Added `SanitizeKccInputBufferJob` validation for frame, entity sequence, source hash, sector generation, finite movement vectors, and safe sector-local AUP range.
- Added `AbortScheduledBatch()` and routed post-phase failure exits through it.
- Moved black-box ring writes into `KinematicTelemetryAggregateJob`, a single post-resolution job folding all active entity hashes/flags into the 300-frame ring.
- Removed the editor-only managed telemetry snapshot array; the tuner now reads Vault telemetry only when no KCC collision/post batch is scheduled.
- Changed dump output to `Docs/AgentLogs/Dump_SHINOBU_113.bin`.
- Added `Docs/ARCHITECTURE/SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md` and linked the route in architecture/ledger docs.

Cinematic Cheats used:
- No CPU fluid truth was added. Water remains analytical drag, buoyancy, added mass, scalar turbulence, and wake metadata for downstream GPU/audio/camera systems.

Exact microseconds saved:
- No new speed claim. This pass spends one validation pass and one aggregate pass to prevent rollback/input divergence and telemetry races. It preserves the earlier avoided PhysX force and CPU-fluid costs.

Verification state:
- Targeted KCC static forbidden-pattern scan is clean for sync capsule casts, `Rigidbody.AddForce`, `CharacterController`, `Pack=1`, `UnityEngine.Random`, hot `new NativeArray/List/HashMap`, LINQ/foreach, and arbitrary `.Complete()`.
- `git diff --check` passed with only line-ending warnings.
- Compile remains blocked by CPU guard.

<SELF_AUDIT stage="POST_AUDIT_HARDENING" status="PENDING_COMPILE">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Legacy controller archaeology remains logged; no new `CharacterController` dependency entered the KCC route.</Task>
    <Task id="02" status="PASS_STATIC">KCC still uses deferred `CapsulecastCommand.ScheduleBatch` and active command/hit subarrays, not sync `Physics.CapsuleCast/SphereCast`.</Task>
    <Task id="03" status="PASS_STATIC">`KinematicStateDTO` is field-only explicit unmanaged state; hot mutation stays through `UnsafeUtility.AsRef` as assigned.</Task>
    <Task id="04" status="PASS_STATIC">Layout report is now explicit 64B; validator checks `UnsafeUtility.SizeOf` and exact field offsets.</Task>
    <Task id="05" status="PASS_STATIC">Mock input is deterministic and generation-stamped; external writers have a builder and explicit job-handle registration seam.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration remains analytical drag/buoyancy/added mass with finite guards and no runtime force ownership.</Task>
    <Task id="07" status="PASS_STATIC">Collision pipeline remains async in simulation; abort path now drains all scheduled handles before clearing flags.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie resistance is scalar turbulence/wake metadata, not CPU fluid simulation.</Task>
    <Task id="09" status="PASS_STATIC">Resolver consumes owner-local collision DTOs and applies remaining-time slide after contact.</Task>
    <Task id="10" status="PASS_STATIC">AUP update remains double3 truth plus millimeter quantization after local float displacement.</Task>
    <Task id="11" status="PASS_STATIC">Quality controls hit budget/iterations continuously from 2 to 8; no binary hardware branch.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence is contiguous DTO memcpy; explicit rollback resim seam avoids direct netcode assembly dependency.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync subtracts sector/camera AUP and smooths only local presentation output.</Task>
    <Task id="14" status="PASS_STATIC">Wake emission remains SignalBus based with owner-local AUP48 conversion and packed scalar metadata.</Task>
    <Task id="15" status="PASS_STATIC">Vault lanes use uninitialized command/hit storage with active-window scheduling and deterministic seed/clear jobs.</Task>
    <Task id="16" status="PASS_STATIC">Telemetry now aggregates all active entities into the 300-frame ring; fault slots are 64B per entity; dump path is `Dump_SHINOBU_113.bin`.</Task>
    <Task id="17" status="PASS_STATIC">Editor tuner edits Vault tuning and reads Vault telemetry only when no KCC batch is scheduled, avoiding live job-owned NativeArray reads and private snapshot arrays.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile storage remains span/FNV/flat-array/bucket based; no private persistent hash map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo diagnostics still read solver debug DTOs independent from transform writes.</Task>
    <Task id="20" status="PENDING_COMPILE">Self-audit and docs are refreshed; guarded compile has not run because CPU guard is red.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64" alignment="16">
      <field name="AUP_Position" offset="0" size="24"/>
      <field name="Velocity" offset="24" size="12"/>
      <field name="AngularVelocity" offset="36" size="12"/>
      <field name="Mass" offset="48" size="4"/>
      <field name="DragCoefficient" offset="52" size="4"/>
      <field name="_pad0.._pad7" offset="56" size="8"/>
      <proof>24+12+12+4+4+8=64; 64 mod 16 = 0.</proof>
    </KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">
      <field name="TargetAup" offset="0" size="24"/>
      <field name="MoveAxis" offset="24" size="12"/>
      <field name="LookAxis" offset="36" size="12"/>
      <field name="SimulationFrame" offset="48" size="4"/>
      <field name="Sequence" offset="52" size="4"/>
      <field name="Flags" offset="56" size="4"/>
      <field name="SourceHash" offset="60" size="4"/>
      <proof>24+12+12+4+4+4+4=64; 64 mod 16 = 0.</proof>
    </HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="PASS_STATIC">One mutable fault slot per entity cache line.</HydrodynamicKccFaultFlagDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight=0.3`, the solver collapses toward two capsule-hit records, smaller visual smoothing alpha, lower deterministic compute estimates, and rollback fast-forward budget near one frame. It does not switch tiers; `math.lerp`, `math.saturate`, and bounded iteration counts smoothly scale toward eight hit records and richer wake metadata as quality approaches 1.0.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_runtime_native_allocations="0">
    Requested handles: ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Independent NativeArray job lanes carry `[NoAlias]`; mutable state/fault lanes use explicit owner-local restrictions and 64B fault slots.</NoAlias>
    <Consumes>External writer JobHandle, fault clear/input/mock/clear handles, integration handle, command build handle, capsule batch handle.</Consumes>
    <Outputs>Input handle, integration handle, command handle, collision handle, hit extraction handle, post-simulation combined handle, external input fence handle.</Outputs>
    <Graph>clearFaults -> mock|external|clearInput -> sanitize -> integration -> commandBuild -> CapsulecastCommand.ScheduleBatch -> extractHits -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrameSwap.</Graph>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new KCC asmdef was added; the code remains in the existing root assembly and did not add a direct assembly reference to a sibling domain. Source still aliases two existing world-root types, `AbsoluteUniversePosition` and `DispatcherJobSwap`; resolving that compile-wall debt requires a broader root-owner migration and was not hidden in this batch. Guarded build command was not launched: CPU samples were `100,98.45,93.64,100,83.19`.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The fake is analytical drag/added-mass/buoyancy plus scalar turbulence/wake metadata. Before the fake, a naive CPU water route would be O(n*particles) or O(n*fluid_voxels). After the fake, KCC CPU work is O(n*h), where h is the continuous 2..8 hit-record budget; GPU/audio/camera systems sell the illusion.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Compile Guard Recheck 19

What was wrong:
- The audit-hardening patch requires compile proof, but the workstation is still saturated.

What was done:
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 98.45, 93.64, 100, 83.19`.
- Processor Utility samples: `86.07, 84.24, 78.98, 84.22, 70.77`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was not launched because CPU remained above 50%.

## 2026-05-19 - Compile Guard Recheck 18

What was wrong:
- The latest static polish still requires compiler proof, but build launch is forbidden while compiler-family processes are active or CPU is above the guard threshold.

What was done:
- Rechecked `dotnet/csc`: active `dotnet` processes were present: `6624, 20496, 32920, 33996, 35560, 56072, 71692`.
- Processor Time samples: `40, 86, 83, 73, 17`.
- Build command was not launched.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime path. This protects developer hardware from stacked compiler contention.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains pending until no `dotnet/csc` process is active and CPU stays below 50%.

## 2026-05-19 - Collision Slide, AUP Hash, And Owner-Local Wake Conversion

What was wrong:
- The post-sweep resolver could move to contact and then apply a full projected velocity timestep, over-advancing along collision planes.
- Black-box state hashing folded only fractional AUP meters, weakening crash forensics across large 100 km coordinates.
- Wake emission used an external AUP conversion helper inside the Burst wake job instead of keeping conversion proof in KCC-owned math.

What was done:
- `KinematicResolutionJob` now computes consumed contact fraction and spends only the remaining timestep on projected velocity.
- `HashState` now folds low/high 32-bit lanes from millimeter-quantized AUP axis longs.
- `HydrodynamicKccMath.ToAup48` converts sanitized double3 meters into `AbsoluteUniversePosition` fields locally before `WakeGeneratedSignal` emission.
- The broad World namespace import was narrowed to explicit aliases for the two required ABI types.

Cinematic Cheats used:
- Water remains the Dear Lie path: analytical drag plus wake scalar metadata. No CPU fluid grid, particles, or Navier-Stokes work was added.

Exact microseconds saved:
- No honest fixed speed claim. The change prevents collision over-correction churn and keeps wake emission at scalar metadata cost. Extra math is sub-microsecond per active KCC entity on normal counts.

Verification state:
- Static scans and compiler proof pending after this patch. `dotnet build` is still gated by CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 12

What was wrong:
- The latest KCC patch needs compiler proof, but a legal build window was not available.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `99.04, 35.70, 43.42, 31.41, 23.87`.
- Processor Utility samples: `80.57, 42.60, 49.44, 39.71, 31.68`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked because the first CPU sample exceeded 50%.

## 2026-05-19 - Compile Guard Recheck 13 And Assembly Boundary Check

What was wrong:
- The workstation remained above the legal build threshold.
- KCC compile-wall isolation cannot be completed by simply dropping an asmdef into `Physics/KCC`, because root `Hecton8.Core` scripts currently import KCC symbols.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `55.15, 54.42, 65.56, 88.11, 100.00`.
- Processor Utility samples: `56.90, 56.66, 58.74, 71.74, 77.22`.
- Verified root assembly dependency: `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` imports `Hecton8.Physics.KCC`, so a standalone KCC asmdef would require a broader owner migration to avoid cyclic references with `Hecton8.Core`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build remains blocked by CPU guard. No asmdef surgery was performed because the isolated move is unsafe under current root dependents.

## 2026-05-19 - Active Capsule Batch Window

What was wrong:
- `CapsulecastCommand.ScheduleBatch` received full Vault command/hit arrays.
- If a reused Vault buffer was larger than the active KCC count, PhysX could schedule stale commands outside current owner-local capacity.

What was done:
- Sliced commands with `commands.GetSubArray(0, capacity)`.
- Sliced hit storage with `hits.GetSubArray(0, capacity * maxHits)`.
- Kept frozen `_scheduledMaxHitsPerCommand` for post-phase extraction and resolver addressing.

Cinematic Cheats used:
- None. This is ownership and memory-window hardening.

Exact microseconds saved:
- No fixed claim. It prevents wasted PhysX command execution after capacity contraction and avoids paying collision queries for stale command slots.

Verification state:
- Static source patch applied. Compiler proof still pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 16

What was wrong:
- Build remains blocked after scheduled entity-count fence.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `37.67, 15.60, 97.87, 98.86, 39.88`.
- Processor Utility samples: `45.41, 20.41, 84.98, 83.64, 49.69`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked by CPU samples above 50%.

## 2026-05-19 - Gizmo Diagnostics Decoupling

What was wrong:
- Solver debug/gizmo cache updated only when transform application was enabled.
- Disabling presentation writes could hide Task 19 collision-normal proof.

What was done:
- `LateFrameTick` now reads visual/debug DTO output first.
- `_lastGizmoCurrent`, `_lastGizmoPredicted`, and `_lastGizmoNormal` update independently from `_applyVisualToTransform`.
- Only `_cachedTransform.localPosition` remains gated by the presentation flag.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- No speed claim. Runtime math is unchanged; this is debug-surface correctness.

Verification state:
- Static source patch applied. Compiler proof still pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 17

What was wrong:
- Build remains blocked after gizmo diagnostic decoupling.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `31.12, 78.12, 97.71, 100.00, 100.00`.
- Processor Utility samples: `36.16, 63.27, 76.05, 77.15, 75.29`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked by CPU samples above 50%.

## 2026-05-19 - Legacy Physics Archaeology Refresh

What was wrong:
- Task 01/02 evidence needed a fresh scan after KCC changes.

What was done:
- Scanned `Assets/_Project/Scripts`, `Assets/_Project/Prefabs`, and `Assets/_Project/Scenes` for `CharacterController`, `Rigidbody.MovePosition`, `Physics.CapsuleCast`, and `Physics.SphereCast`.
- Found no project-owned `CharacterController` usage and no project-owned synchronous `Physics.CapsuleCast/SphereCast`.
- Logged remaining project-owned `MovePosition` routes as legacy presentation/transport/docking/fauna/interaction paths outside this KCC replacement patch.
- Confirmed broader `Assets` hits belong to third-party packages or legacy package controllers and were not edited.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- No runtime change from scan.

Verification state:
- Static evidence refreshed. Compiler proof remains pending CPU/dotnet guard.

<SELF_AUDIT stage="POST_BATCH_WINDOW_POLISH" status="PENDING_COMPILE">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Project-owned scan found no `CharacterController`; remaining `MovePosition` routes are logged legacy presentation/transport/docking/fauna/interaction paths outside this KCC patch.</Task>
    <Task id="02" status="PASS_STATIC">Project-owned scan found no synchronous `Physics.CapsuleCast` or `Physics.SphereCast`; KCC uses deferred `CapsulecastCommand.ScheduleBatch`.</Task>
    <Task id="03" status="PASS_STATIC">`KinematicStateDTO` is field-only unmanaged state; integration/resolution mutate through `UnsafeUtility.AsRef` over Vault-backed arrays.</Task>
    <Task id="04" status="PASS_STATIC">Editor-only layout validator checks `UnsafeUtility.SizeOf` and field offsets for the 64-byte DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock input emits `HydrodynamicKccInputDTO` via Burst jobs; the runtime array path is deterministic and the profiling queue carries per-packet sequence IDs.</Task>
    <Task id="06" status="PASS_STATIC">`HydrodynamicIntegrationJob` uses analytical drag `v/(1+drag*|v|*dt)`, buoyancy, added mass, finite guards, and no `Rigidbody.AddForce`.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command build and capsule batch without hot-path `Complete`; active command/hit windows are sliced to the frozen entity count.</Task>
    <Task id="08" status="PASS_STATIC">Hydrodynamics use scalar drag/turbulence/wake metadata as the Dear Lie, not CPU water displacement.</Task>
    <Task id="09" status="PASS_STATIC">Resolver consumes owner-local hit DTOs, projects velocity along hit normals, and spends only remaining timestep after contact.</Task>
    <Task id="10" status="PASS_STATIC">Final AUP is authoritative double3 and millimeter-quantized after local float displacement is applied.</Task>
    <Task id="11" status="PASS_STATIC">Hit budget and resolver iterations scale continuously from 2 to 8 through `GlobalQualityWeight`; no low/high branch.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence MemCpys contiguous state bytes and explicit rollback resimulation bypasses visual smoothing.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync subtracts sector/camera AUP first and EWMA-lerps only local float presentation.</Task>
    <Task id="14" status="PASS_STATIC">Wake emission uses `SignalBus<WakeGeneratedSignal>.ParallelWriter`; AUP48 conversion is owner-local and bounded before signal publish.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit/result Vault lanes use `NativeArrayOptions.UninitializedMemory`; active subarrays avoid stale oversized buffer execution without clearing.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring and 64-byte per-entity fault flags live in Vault; native-span dump writes `Dump_SHINOBU_113.bin` on fault.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner reads/writes Vault tuning and draws cursor-ordered telemetry graph at 20 Hz.</Task>
    <Task id="18" status="PASS_STATIC">CSV parser is `ReadOnlySpan<byte>` + FNV-1a + Vault-backed flat profile/bucket arrays; no `string.Split`, LINQ, or private persistent hash map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO cache independent of transform-application toggle and draws current/predicted capsules plus collision normal.</Task>
    <Task id="20" status="PENDING_COMPILE">Self-audits and static scans are logged; `dotnet build` remains blocked by CPU/dotnet guard, so final verification is not closed.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <KinematicStateDTO size="64" alignment="16">
      <field name="AUP_Position" offset="0" size="24" type="double3"/>
      <field name="Velocity" offset="24" size="12" type="float3"/>
      <field name="AngularVelocity" offset="36" size="12" type="float3"/>
      <field name="Mass" offset="48" size="4" type="float"/>
      <field name="DragCoefficient" offset="52" size="4" type="float"/>
      <field name="_pad0.._pad7" offset="56" size="8" type="byte[8]"/>
      <proof>24+12+12+4+4+8=64; 64 % 16 = 0.</proof>
    </KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">
      <field name="TargetAup" offset="0" size="24" type="double3"/>
      <field name="MoveAxis" offset="24" size="12" type="float3"/>
      <field name="LookAxis" offset="36" size="12" type="float3"/>
      <field name="SimulationFrame" offset="48" size="4" type="uint"/>
      <field name="Sequence" offset="52" size="4" type="uint"/>
      <field name="Flags" offset="56" size="4" type="uint"/>
      <field name="SourceHash" offset="60" size="4" type="uint"/>
      <proof>24+12+12+4+4+4+4=64; 64 % 16 = 0.</proof>
    </HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line"/>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below 0.3 quality, command/hit scheduling and resolver work collapse toward two hit records, mock-input strafe amplitude stays low, added-mass/acceleration/drag costs are lerped down, visual smoothing alpha is reduced, and rollback fast-forward budget is capped by the same continuous scalar. At high/ultra quality the exact same kernels consume up to eight hit records and richer wake metadata; no binary hardware switch is present.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">
    ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Independent NativeArray lanes in Burst jobs are marked `[NoAlias]`; state mutation uses `UnsafeUtility.AsRef`; fault flags are 64-byte slots.</NoAlias>
    <Graph>clearFaults -> mockInput|armedExternalInput|clearInput -> integration -> commandBuild -> CapsulecastCommand.ScheduleBatch(activeCommands, activeHits, maxHits) -> extractHits -> resolution -> visualSync/rollbackFence/wakeEmit -> lateFrameSwap.</Graph>
    <FrozenFacts>FixedTick freezes `_scheduledEntityCount` and `_scheduledMaxHitsPerCommand`; PostFixedTick rejects mismatched buffers instead of recomputing or clamping the batch window.</FrozenFacts>
    <Completes>No hot-path `JobHandle.Complete()` calls in KCC; forced completion is confined to rollback resimulation, teardown, and dispatcher swap helper windows.</Completes>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No standalone KCC asmdef was added because current root `Hecton8.Core` scripts already import `Hecton8.Physics.KCC`; creating one without moving dependents would create an assembly cycle. Latest legal build command remains blocked by CPU guard: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: CPU fluid truth around the capsule would be O(n*particles) or O(n*fluid_voxels). After: KCC hydrodynamics are O(n*h), where h is the quality-scaled 2-8 hit record budget, and water feel is sold through nonlinear drag, scalar turbulence, packed wake metadata, and downstream GPU/audio consumers.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Compile Guard Recheck 14

What was wrong:
- Build remains legally blocked after the active batch-window patch.

What was done:
- Rechecked `dotnet/csc`: active `dotnet` process IDs `2880, 15852, 42588, 46472, 49196, 54384, 63912`.
- Processor Time samples: `100.00, 69.17, 33.76, 33.03, 25.22`.
- Processor Utility samples: `85.83, 61.73, 39.85, 30.40, 26.98`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked by active `dotnet` processes and CPU samples above 50%.

## 2026-05-19 - AUP48 Cast Clamp

What was wrong:
- Owner-local wake AUP conversion sanitized non-finite input but did not bound extreme finite input before `long` grid casts.

What was done:
- Added `MaxAupMagnitudeMeters`.
- Clamped sanitized double3 meters before `math.floor` and `long` conversion in `HydrodynamicKccMath.ToAup48`.
- Reused the same constant in telemetry hash quantization instead of a hard-coded literal.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- No speed claim. This adds a few scalar clamps only on wake signal emission and prevents undefined fault-path casts.

Verification state:
- Static source patch applied. Compiler proof still pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 15

What was wrong:
- Build remains blocked after the AUP48 clamp patch.

What was done:
- Rechecked `dotnet/csc`: active `dotnet` process IDs `2880, 15852, 42588, 46472, 49196, 54384, 63912`.
- Processor Time samples: `58.81, 42.63, 89.52, 100.00, 100.00`.
- Processor Utility samples: `57.47, 46.28, 75.62, 84.56, 84.13`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked by active `dotnet` processes and CPU samples above 50%.

## 2026-05-19 - Scheduled Entity Count Fence

What was wrong:
- The batch hit stride was frozen, but post-phase active entity count was still derived from live capacity.
- A capacity change between `FixedTick` and `PostFixedTick` could make extraction/resolution read a different command window than PhysX executed.

What was done:
- Added `_scheduledEntityCount`.
- `FixedTick` stores active count beside `_scheduledMaxHitsPerCommand`.
- `PostFixedTick` uses the frozen count and validates raw hits, resolved hits, previous AUP, visual, rollback bytes, wake packets, and debug outputs against that count before scheduling dependent jobs.
- Removed silent clamp-down of scheduled count against current state length; state length must now prove it can cover the exact scheduled count.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- No fixed claim. It prevents wasted hit extraction and wrong-window resolution when active capacity changes after scheduling.

Verification state:
- Static source patch applied. Compiler proof still pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 11

What was wrong:
- Build is still required, but another `dotnet` process is active and CPU remains saturated.

What was done:
- Rechecked process list: `dotnet` process `44020` is active with CPU time `16.609375`.
- Processor Time samples: `87.30, 75.72, 99.63, 71.60, 84.71`.
- Processor Utility samples: `66.31, 61.35, 75.12, 59.40, 68.40`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build remains blocked by both active `dotnet` and CPU > 50%.

## 2026-05-19 - AUP Local Float Overflow Clamp

What was wrong:
- The KCC AUP seam subtracted sector origin before float conversion, but a finite wrong-sector delta could still overflow local `float3` command endpoints.

What was done:
- Added `HydrodynamicKccMath.MaxLocalFloatMagnitude = 131072f`.
- `ResolveLocalFloat3` now clamps only the transient post-subtraction local delta before constructing `float3`.
- Authoritative `KinematicStateDTO.AUP_Position` remains double3 truth and is not clamped.

Cinematic Cheats used:
- None. This is numerical vaccination at the AUP/local seam.

Exact microseconds saved:
- No speed claim. The clamp adds scalar comparisons but prevents invalid PhysX command data and downstream black-box faults.

Verification state:
- Static source patch only. Build remains blocked by active `dotnet`/CPU guard.

## 2026-05-19 - Static Risk Closure Before Compile Gate

What was wrong:
- `RaycastHit.normal` was relying on implicit UnityEngine/Mathematics conversion assumptions inside the Burst resolver.
- Capsule command endpoints/direction were relying on implicit `float3 -> Vector3` conversion assumptions.
- `QueryParameters` received `_collisionMask` instead of `_collisionMask.value`.
- The black-box dump path could rewrite the same fault mask every LateFrame if a non-finite state persisted.

What was done:
- Replaced the implicit normal conversion with explicit `new float3(hitNormal.x, hitNormal.y, hitNormal.z)`.
- Replaced capsule command endpoint/direction arguments with explicit `Vector3` structs before constructing `CapsulecastCommand`.
- Replaced `new QueryParameters(_collisionMask, ...)` with `new QueryParameters(_collisionMask.value, ...)`.
- Added `_dumpedFaultMask` so `Dump_SHINOBU_113.bin` is written once per distinct fault mask while preserving the fault flag for diagnostics.

Cinematic Cheats used:
- No new physical simulation. The KCC still sells water via analytical drag, turbulence scalar, wake signal, and EWMA visual smoothing.

Exact microseconds saved:
- Healthy frames: no measurable new cost.
- Faulted persistent NaN frames: avoids repeated 19.2 KB managed copy and file write per LateFrame after the first dump for that fault mask.

Verification state:
- `git diff --check` passed for tracked SHINOBU_113 files with only CRLF warnings.
- Targeted grep found no `Pack = 1`, DTO auto-properties, `Complete`, `Run`, local `new NativeArray`, synchronous `Physics.CapsuleCast/SphereCast`, `CharacterController`, or `AddForce` in the new KCC/SDF target path.
- `dotnet build` remains prohibited: latest CPU utility samples were `85.62, 86.86, 83.29, 78.60, 79.24`; no `dotnet` or `csc` process was active.

## 2026-05-19 - Teardown Ownership Patch

What was wrong:
- `OnDisable` drained only post-simulation/collision handles. During a disable between scheduling stages, command/integration/input handles could remain implicit and make Vault alias ownership harder to reason about.

What was done:
- Added `DrainPendingJobsForTeardown()` and call it before lane unregister. It drains post, collision, command, integration, and input handles through `DispatcherJobSwap.TryComplete(forceComplete:true)`.

Cinematic Cheats used:
- None; this is a memory ownership fix.

Exact microseconds saved:
- Healthy frame cost: 0 us. Disable/hot-swap path cost is bounded by outstanding job work and prevents undefined ownership rather than saving steady-state frame time.

Verification state:
- Build still not launched. Latest CPU utility samples were `87.48, 88.60, 90.77, 91.41, 87.64`; no `dotnet` or `csc` process was active.

## 2026-05-19 - Rollback Seam Tightening

What was wrong:
- The KCC had a contiguous rollback byte fence, but no callable fast-forward entry point for rollback owners.
- Adding a direct reference to netcode runtime would have been a compile-wall violation.

What was done:
- Added `TryRunRollbackResimulation(int requestedFrames, float fixedDeltaTime)`.
- The method drains outstanding work, runs fixed/post KCC stages for a quality-budgeted frame count, force-completes only inside this explicit rollback path, and marks visual sync bypass frames.

Cinematic Cheats used:
- Presentation smoothing is bypassed during rollback resim. The player sees the corrected state instead of an EWMA lie.

Exact microseconds saved:
- Normal path: 0 us extra cost.
- Low quality rollback clamps to one replay frame per call, avoiding up to seven replay steps compared to the default 8-frame upper seam.

Verification state:
- Static only. Build remains blocked by CPU guard.

## 2026-05-19 - Layout Proof Tightening

What was wrong:
- The first layout validator used `Marshal.OffsetOf`. Correct, but not the exact proof path requested by Task 04.

What was done:
- Replaced the helper with `UnsafeUtility.GetFieldOffset` over a cold reflection field lookup.
- Missing fields return `-1`, making the validator fail closed.

Cinematic Cheats used:
- None; structural proof only.

Exact microseconds saved:
- Runtime: 0 us. This is cold validation.

Verification state:
- Static only. Build still blocked by CPU guard.

## 2026-05-19 - Compile Guard Recheck

What was wrong:
- Build verification is still required for Task 20, but the host remains above the project CPU threshold.

What was done:
- Rechecked `dotnet/csc`: no active process.
- Rechecked CPU utility: `96.94, 99.95, 93.34, 99.53, 92.71`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change. The guard prevents a compiler spike on a saturated workstation.

Verification state:
- `dotnet build` not launched because CPU exceeded 50%.

## 2026-05-19 - Compile Guard Recheck 2

What was wrong:
- The previous high utility readings were rechecked against both CPU counters before deciding whether to build.

What was done:
- Rechecked `dotnet/csc`: no active process.
- Processor Time samples: `91.84, 100.00, 99.57, 99.44, 100.00`.
- Processor Utility samples: `92.03, 98.50, 96.96, 96.86, 96.05`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build remains forbidden by the >50% CPU guard.

## 2026-05-19 - Polish Pass: Fault Lane, Wake Metadata, Telemetry Estimate

What was wrong:
- A single scalar fault flag was a weak parallel-write surface for NaN detection.
- `ComputeMicroseconds` existed in telemetry but was written as `0f`.
- The queue mock input job existed but lacked a clean harness API.
- The wake route emitted AUP and velocity, but radius/magnitude proof was owner-local only and the source hash polluted the low source-kind byte.

What was done:
- Added 64-byte `HydrodynamicKccFaultFlagDTO` slots and `ClearKccFaultFlagsJob`; each entity writes its own cache-line-sized fault slot.
- Filled telemetry `ComputeMicroseconds` with a deterministic compute-use estimate derived from quality, speed, collision, and iteration count.
- Added `HydrodynamicKccMockInput.GenerateMockMovementInput(...)` for caller-owned `NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter` harnesses.
- Added `WakeRadius` and `WakeMagnitude` to `HydrodynamicWakePacketDTO` and packed player source kind, quantized magnitude, and quantized radius into `WakeGeneratedSignal.SourceFlags` without changing the Core DTO.
- Extended layout validation to check wake packet, debug DTO, telemetry DTO, and fault DTO size as 64 bytes.

Cinematic Cheats used:
- Wake remains a scalar/proxy signal. No water mesh displacement, particles, or GameObjects were introduced.

Exact microseconds saved:
- Fault path: removes contested shared-cache writes; no healthy-frame fake number claimed.
- Telemetry estimate: adds scalar math only; replaces useless zero field with deterministic forensic data.
- Wake path: avoids any object spawn or fluid solve; metadata is packed into an existing 64-byte signal.

Verification state:
- `git diff --check` passed for tracked SHINOBU files with only CRLF warnings.
- Targeted grep is clean for `ComputeMicroseconds = 0f`, shared `NativeArray<int> faults`, `FaultFlags[0]`, `SourceFlags = packet.Flags`, `.Complete(`, and `.Run(` in the KCC runtime.
- Build still pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 3

What was wrong:
- The post-polish code now warrants compile verification, but project law forbids building during high CPU load.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `100.00, 100.00, 100.00, 100.00, 100.00`.
- Processor Utility samples: `86.68, 71.23, 56.70, 83.10, 82.65`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build` not launched because CPU exceeded 50%.

## 2026-05-19 - Sub-Agent Audit Corrections

What was wrong:
- The deterministic resolver still read Unity `RaycastHit` directly and quality iterations operated on one hit.
- Telemetry ring indexing used `(frame + index) % 300`, which made multi-entity writes alias the last-300-frame proof.
- `EnsureVaultBuffers()` reacquired handles on healthy tick calls instead of only on boot/capacity change/hot-swap.
- `DumpTelemetry` allocated a managed byte array before writing.
- The editor graph ignored the telemetry cursor and repainted every editor update.
- CSV parsing existed but had no runtime ingestion/apply seam.

What was done:
- Added `HydrodynamicKccCollisionHitDTO` and `ExtractCapsuleCastHitsJob`; the deterministic resolver now consumes owner-local hit DTOs.
- `CapsulecastCommand.ScheduleBatch` now uses the continuous 2-8 quality hit budget, and resolution loops over those extracted hits.
- Telemetry writes are primary-entity frame-ring writes: `frame % 300`, with cursor update.
- Added `_resolvedBufferCapacity` and `AreVaultBuffersReady(...)` so handle acquisition is cold/capacity/hot-swap only.
- Replaced managed black-box byte array copy with native-span `FileStream.Write`.
- Added cursor-ordered UI graph drawing at 20 Hz and `TryIngestFluidProfiles` / `TryApplyFluidProfile` APIs.

Cinematic Cheats used:
- Collision still uses one capsule command per entity and bounded hit records; no mesh collision truth or CPU fluid simulation was introduced.

Exact microseconds saved:
- Low quality avoids up to six hit records and six projection passes per command compared with ultra.
- Healthy hot ticks avoid repeated Vault handle reacquisition.
- Fault dump avoids the current 19.2 KB managed array allocation.

Verification state:
- Static grep is clean for managed dump byte arrays, `File.WriteAllBytes`, shared scalar fault writes, `ComputeMicroseconds = 0f`, direct completion, local native allocations, `Pack = 1`, synchronous capsule/sphere casts, `CharacterController`, and `AddForce` in the KCC target path.
- `git diff --check` passed after whitespace cleanup with CRLF warnings only.
- Build remains pending CPU/dotnet guard.

## 2026-05-19 - Compile Guard Recheck 4

What was wrong:
- Compile proof is still required, but the workstation CPU guard remained above the project threshold.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- First recheck after static pass: Processor Time `35.60, 42.08, 33.57, 13.68, 23.06`; Processor Utility `16.73, 34.61, 53.18, 28.48, 29.27`.
- Second recheck: Processor Time `55.42, 68.42, 62.95, 63.09, 68.81`; Processor Utility `52.11, 35.36, 55.94, 37.08, 43.54`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build` not launched because CPU exceeded 50% in both guard passes.

## 2026-05-19 - Compile Guard Recheck 5

What was wrong:
- The delayed build guard still had one Processor Time sample above the allowed threshold.

What was done:
- Waited 15 seconds before sampling.
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `48.38, 54.19, 47.04, 41.25, 42.41`.
- Processor Utility samples: `37.74, 39.72, 36.24, 44.28, 40.81`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build` not launched because Processor Time exceeded 50% on one sample.

## 2026-05-19 - Compile Guard Recheck 6

What was wrong:
- The final short guard pass regressed sharply above the CPU threshold.

What was done:
- Waited 8 seconds before sampling.
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `75.15, 74.23, 47.04, 74.14, 100.00`.
- Processor Utility samples: `76.17, 80.02, 78.61, 79.82, 67.60`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build` not launched because CPU exceeded 50%.

<SELF_AUDIT agent_id="SHINOBU_113" date="2026-05-19" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Scanned legacy CharacterController/MovePosition routes; new owner-local KCC seam avoids cross-domain prefab surgery until handoff.</Task>
    <Task id="02" status="PASS_STATIC">Deferred `CapsulecastCommand.ScheduleBatch` path implemented; no synchronous CapsuleCast/SphereCast in new KCC target path.</Task>
    <Task id="03" status="PASS_STATIC">`KinematicStateDTO` flattened to public fields and mutated through unsafe refs in Burst jobs.</Task>
    <Task id="04" status="PASS_STATIC">UnsafeUtility layout validator checks 64-byte DTO sizes and offsets.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic NativeArray mock input plus `NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter` harness implemented.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration uses analytical drag, buoyancy, added mass, and finite guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command build and collision batch without waiting.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie water resistance uses scalar turbulence/wake metadata instead of fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolution projects velocity against extracted hit DTO normals and writes AUP.</Task>
    <Task id="10" status="PASS_STATIC">AUP update is millimeter-quantized.</Task>
    <Task id="11" status="PASS_STATIC">Quality controls actual 2-8 hit budget and projection passes.</Task>
    <Task id="12" status="PASS_STATIC">Rollback memcpy fence and explicit resimulation seam exist; external netcode caller remains integration pending.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync localizes AUP and uses EWMA, with rollback bypass.</Task>
    <Task id="14" status="PASS_STATIC">Wake signal uses SignalBus ParallelWriter; magnitude/radius packed without Core DTO mutation.</Task>
    <Task id="15" status="PASS_STATIC">Command/result/hit DTO buffers are Vault-backed with uninitialized memory where overwritten by jobs/physics.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring, padded fault flags, and native-span dump path implemented; profiler timing still pending.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner reads/writes Vault tuning and draws cursor-ordered telemetry graph.</Task>
    <Task id="18" status="PASS_STATIC_WITH_DEVIATION">CSV parser is zero-GC span/FNV and Vault-backed flat table+buckets; literal NativeHashMap rejected because Vault does not expose hash-map ownership.</Task>
    <Task id="19" status="PASS_STATIC">Gizmos draw current/predicted capsules and solver collision normal.</Task>
    <Task id="20" status="FAIL_PENDING_COMPILE">Self-audit/log proof written; build verification blocked by CPU guard, so runtime readiness is not claimed.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <KinematicStateDTO size="64" alignment="explicit">
      <field name="AUP_Position" offset="0" size="24"/>
      <field name="Velocity" offset="24" size="12"/>
      <field name="AngularVelocity" offset="36" size="12"/>
      <field name="Mass" offset="48" size="4"/>
      <field name="DragCoefficient" offset="52" size="4"/>
      <field name="_pad0.._pad7" offset="56" size="8"/>
    </KinematicStateDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="padded_per_entity_cache_line"/>
    <HydrodynamicKccCollisionHitDTO size="64"/>
    <KinematicTelemetryEntry size="64"/>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    GlobalQualityWeight drives solver hit budget and projection iterations through `math.lerp(2,8,weight)`. Below 0.3 the KCC schedules two or three hit records, uses cheaper analytical drag/turbulence scalar, and records lower compute-use estimates; high weights spend extra collision records on smoother corner behavior and richer wake metadata.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_native_allocations="0">
    <buffer id="ShinobuHydroKccStates"/>
    <buffer id="ShinobuHydroKccInputs"/>
    <buffer id="ShinobuHydroKccProposedVelocities"/>
    <buffer id="ShinobuHydroKccCollisionCommands"/>
    <buffer id="ShinobuHydroKccCollisionHits"/>
    <buffer id="ShinobuHydroKccResolvedHits"/>
    <buffer id="ShinobuHydroKccPreviousAup"/>
    <buffer id="ShinobuHydroKccVisualOutputs"/>
    <buffer id="ShinobuHydroKccTelemetryRing"/>
    <buffer id="ShinobuHydroKccTelemetryCursor"/>
    <buffer id="ShinobuHydroKccTuning"/>
    <buffer id="ShinobuHydroKccFluidProfiles"/>
    <buffer id="ShinobuHydroKccFluidProfileBuckets"/>
    <buffer id="ShinobuHydroKccRollbackBytes"/>
    <buffer id="ShinobuHydroKccFaultFlags"/>
    <buffer id="ShinobuHydroKccWakePackets"/>
    <buffer id="ShinobuHydroKccDebugOutputs"/>
  </H_PHI_VAULT_STATUS>
  <DEPENDENCY_GRAPH>
    input -> integration -> commandBuild -> CapsulecastCommand.ScheduleBatch -> hitExtract -> resolution -> visualSync/rollbackFence/wakeEmit -> lateFrameSwap.
    No arbitrary mid-frame `Complete()` is used; explicit rollback/teardown drains through `DispatcherJobSwap.TryComplete(forceComplete:true)`.
  </DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef sibling reference was added by this domain seam. `dotnet build` was not run because CPU guard exceeded 50%; latest samples were Processor Time `75.15,74.23,47.04,74.14,100.00` and Processor Utility `76.17,80.02,78.61,79.82,67.60`.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Water heaviness is analytical drag plus turbulence/wake scalars, not CPU fluid displacement. Complexity remains O(n * h) for n entities and h quality-scaled hit records, instead of O(n * fluid_voxels_or_particles).
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Polish Recheck: State Slots And Build Guard

What was wrong:
- Vault-backed `KinematicStateDTO` lanes use `NativeArrayOptions.UninitializedMemory`; all active slots must be proven before Burst integration reads them.
- Build verification is warranted but still blocked by CPU guard.

What was done:
- Verified `SeedInitialStateIfNeeded(states, tuning, sectorOrigin, capacity)` scans every active slot and reseeds invalid state with deterministic millimeter-quantized AUP offsets.
- Verified integration writes sanitized angular velocity, mass, and drag back into state, closing uninitialized angular/drag propagation.
- Re-ran static scans over the SHINOBU KCC runtime/editor files: no `Complete()`, `.Run()`, local persistent native containers, `foreach`, sync capsule/sphere cast, `CharacterController`, `AddForce`, `Pack=1`, auto-property DTOs, `UnityEngine.Random`, or managed dump byte arrays in the target path.
- Re-ran `git diff --check`; only CRLF normalization warnings were returned.
- Rechecked `dotnet/csc`: no active process was returned.
- CPU guard samples remained above threshold: Processor Time `99.42,70.67,47.62,44.08,82.82`; Processor Utility `85.02,65.04,49.15,41.80,71.44`.

Cinematic Cheats used:
- Hydrodynamics remain analytical drag plus turbulence/wake scalars; no CPU fluid truth or particle water path was introduced.

Exact microseconds saved:
- State-slot seeding is a guard-path cost, not a frame optimization. It prevents NaN propagation and crash-dump churn from uninitialized cache-line state.
- Low quality still saves up to six hit records and projection passes per entity relative to ultra.

Verification state:
- Static verification passed for targeted architectural bans.
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was not launched because CPU exceeded 50%.

<SELF_AUDIT agent_id="SHINOBU_113" date="2026-05-19" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Legacy `CharacterController` scan is clean in target path; old `MovePosition` routes are documented as legacy presentation/out-of-domain handoff, not mutated blindly.</Task>
    <Task id="02" status="PASS_STATIC">New KCC collision path uses deferred `CapsulecastCommand.ScheduleBatch`; no sync `Physics.CapsuleCast/SphereCast` in target runtime.</Task>
    <Task id="03" status="PASS_STATIC">`KinematicStateDTO` is explicit 64-byte unmanaged public-field state; jobs mutate through unsafe refs and Vault arrays.</Task>
    <Task id="04" status="PASS_STATIC">Editor-only `UnsafeUtility.GetFieldOffset` validator proves DTO offsets and fails closed on missing fields.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock input jobs and queue harness use `Unity.Mathematics.Random` seeded from sector/frame/index.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration uses analytical drag `v/(1+drag*|v|*dt)`, buoyancy, added mass, and NaN guards.</Task>
    <Task id="07" status="PASS_STATIC">Fixed tick schedules integration, command build, and collision batch without arbitrary main-thread completion.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie resistance is scalar turbulence and wake metadata, not Navier-Stokes/particle water.</Task>
    <Task id="09" status="PASS_STATIC">Resolver consumes owner-local hit DTOs, projects velocity against valid normals, and writes `double3` AUP.</Task>
    <Task id="10" status="PASS_STATIC">Final AUP writes are millimeter quantized.</Task>
    <Task id="11" status="PASS_STATIC">Continuous `GlobalQualityWeight` maps to 2-8 scheduled hit records and resolver passes; scheduled stride is frozen per batch.</Task>
    <Task id="12" status="PASS_STATIC">Rollback memcpy fence and explicit owner-local resimulation seam exist; netcode dependency is not hardwired.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync subtracts sector AUP into local float space and EWMA-smooths unless rollback bypass is active.</Task>
    <Task id="14" status="PASS_STATIC">Wake packets emit through `SignalBus<WakeGeneratedSignal>.ParallelWriter`; radius/magnitude stay owner-local or packed without Core DTO mutation.</Task>
    <Task id="15" status="PASS_STATIC">Vault buffers own command, hit, rollback, telemetry, tuning, profile, debug, wake, and fault lanes; active state slots are explicitly seeded before use.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring and 64-byte per-entity fault flags are implemented; dump path writes native span to `Docs/AgentLogs/Dump_SHINOBU_113.bin`.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner hydrates Vault tuning and draws cursor-ordered telemetry at throttled editor cadence.</Task>
    <Task id="18" status="PASS_STATIC_WITH_DEVIATION">CSV parser is zero-GC span/FNV with Vault flat table+buckets; `NativeHashMap` ownership was rejected because current Vault API exposes arrays, not map handles.</Task>
    <Task id="19" status="PASS_STATIC">Solver writes debug DTO; gizmos draw current capsule, predicted capsule, and solver normal.</Task>
    <Task id="20" status="FAIL_PENDING_COMPILE">Self-audit and log are appended, but compiler proof is still blocked by CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <KinematicStateDTO size="64" alignment="explicit">
      <field name="AUP_Position" offset="0" size="24"/>
      <field name="Velocity" offset="24" size="12"/>
      <field name="AngularVelocity" offset="36" size="12"/>
      <field name="Mass" offset="48" size="4"/>
      <field name="DragCoefficient" offset="52" size="4"/>
      <field name="_pad0.._pad7" offset="56" size="8"/>
      <math>24+12+12+4+4+8 = 64 bytes, one ARM64-friendly cache-line-sized DTO.</math>
    </KinematicStateDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line"/>
    <HydrodynamicKccCollisionHitDTO size="64"/>
    <KinematicTelemetryEntry size="64"/>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    `GlobalQualityWeight` is consumed as a continuous scalar. Below 0.3 the collision batch resolves two to three hit records, telemetry records lower deterministic compute-use estimates, and hydrodynamic response remains analytical drag plus scalar turbulence. At high weights the same jobs spend extra hit records/projection passes and richer wake metadata; no binary low-end switch is introduced.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_native_allocations="0">
    <buffer id="ShinobuHydroKccStates"/>
    <buffer id="ShinobuHydroKccInputs"/>
    <buffer id="ShinobuHydroKccProposedVelocities"/>
    <buffer id="ShinobuHydroKccCollisionCommands"/>
    <buffer id="ShinobuHydroKccCollisionHits"/>
    <buffer id="ShinobuHydroKccResolvedHits"/>
    <buffer id="ShinobuHydroKccPreviousAup"/>
    <buffer id="ShinobuHydroKccVisualOutputs"/>
    <buffer id="ShinobuHydroKccTelemetryRing"/>
    <buffer id="ShinobuHydroKccTelemetryCursor"/>
    <buffer id="ShinobuHydroKccTuning"/>
    <buffer id="ShinobuHydroKccFluidProfiles"/>
    <buffer id="ShinobuHydroKccFluidProfileBuckets"/>
    <buffer id="ShinobuHydroKccRollbackBytes"/>
    <buffer id="ShinobuHydroKccFaultFlags"/>
    <buffer id="ShinobuHydroKccWakePackets"/>
    <buffer id="ShinobuHydroKccDebugOutputs"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>All native-array job fields that are independent lanes are marked `[NoAlias]`; mutable per-entity fault flags are padded to 64 bytes.</NoAlias>
    <Graph>clearFaults -> mockInput -> hydrodynamicIntegration -> buildCapsuleCommands -> CapsulecastCommand.ScheduleBatch -> extractHits -> kinematicResolution -> visualSync/rollbackFence/wakeEmit -> lateFrameSwap.</Graph>
    <Completes>No arbitrary hot-path `JobHandle.Complete()` is used; rollback and teardown use explicit `DispatcherJobSwap.TryComplete(forceComplete:true)` boundaries.</Completes>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Runtime uses Core/Contracts/Memory/World seams and did not add a sibling asmdef dependency. Build is pending because CPU guard exceeded 50%; latest samples were Processor Time `67.98,59.13,65.65,61.95,94.14` and Processor Utility `62.48,60.51,58.45,61.57,78.83`, with no `dotnet/csc` process active.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Hydrodynamics are faked as analytical drag, buoyancy scalar, turbulence, and wake metadata. Complexity is O(n*h) for entities times quality-scaled hit records; rejected CPU fluid truth would be O(n*particles) or O(n*fluid_voxels) plus allocation/renderer pressure.
  </DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Compile Guard Recheck 8

What was wrong:
- The build remains justified but cannot be launched under the CPU guard.

What was done:
- Waited 15 seconds and rechecked guard conditions.
- `dotnet/csc`: no active process was returned.
- Processor Time samples: `67.98, 59.13, 65.65, 61.95, 94.14`.
- Processor Utility samples: `62.48, 60.51, 58.45, 61.57, 78.83`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build` remains blocked because CPU exceeded 50% on every delayed sample.

## 2026-05-19 - Resolver Scheduled Stride Repair

What was wrong:
- `FixedTick` froze the PhysX raw-hit stride, but `KinematicResolutionJob` still multiplied entity index by the live quality-clamped iteration count.
- If quality changed between simulation and post-simulation, entity hit windows could be addressed with the wrong stride even though extraction used the correct scheduled stride.

What was done:
- Split resolver math into `scheduledHitStride` for buffer addressing and `executedIterations` for live quality compute budget.
- `hitBase` now uses the immutable scheduled stride.
- The resolver loop uses the clamped executed iteration count.
- Telemetry now records executed iterations instead of theoretical quality iterations.

Cinematic Cheats used:
- No new physical truth. Collision remains bounded capsule hit DTO projection; water feel remains analytical drag plus turbulence/wake metadata.

Exact microseconds saved:
- No direct speed claim. The repair preserves low-quality compute shedding while preventing wrong-hit reads under live scalability changes.

Verification state:
- Static source patch only. Compile remains pending CPU guard.

## 2026-05-19 - Compile Guard Recheck 9

What was wrong:
- The resolver stride repair now requires compiler proof, but CPU guard is still not clean.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `70.57, 41.94, 42.13, 68.23, 31.21`.
- Processor Utility samples: `68.08, 48.49, 42.20, 64.75, 34.48`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked because CPU exceeded 50% on multiple samples.

## 2026-05-19 - KCC Input Contract Polish

What was wrong:
- The KCC-owned 64-byte movement packet was named `InputStateDTO`, colliding by simple name with the canonical 24-byte `Hecton8.Core.InputStateDTO`.
- `_runMockInput=false` could leave `BufferID.ShinobuHydroKccInputs` as uninitialized Vault memory unless an external writer was explicitly armed for the frame.

What was done:
- Renamed the KCC packet to `HydrodynamicKccInputDTO`.
- Added `HydrodynamicKccInputDTO` to the editor layout validator.
- Added `_consumeExternalInputBuffer` as an explicit handoff flag.
- Added `TryRegisterExternalInputWriter(JobHandle)` so external producers must arm the dependency for the frame.
- Added `ClearKccInputBufferJob` and route selection: mock writer, external writer, or deterministic zero input.
- Clamped mode conflicts: mock input clears stale external latches, and external writer registration is rejected while mock input is enabled.
- Updated the architecture note to state that canonical device input remains Core-owned.

Cinematic Cheats used:
- None. This is contract hardening; water feel remains the analytical drag/turbulence fake.

Exact microseconds saved:
- No speed claim. The no-external-writer path spends one 64-byte write per active entity to prevent nondeterministic thrust from uninitialized memory.

Verification state:
- Static source patch only. Compile still pending CPU guard.

## 2026-05-19 - Compile Guard Recheck 10

What was wrong:
- The input contract/handoff patch needs compiler proof, but the CPU guard remains red.

What was done:
- Rechecked `dotnet/csc`: no active process was returned.
- Processor Time samples: `92.47, 50.45, 96.91, 100.00, 100.00`.
- Processor Utility samples: `73.34, 51.69, 78.11, 83.57, 84.29`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked because CPU exceeded 50% on all samples.

<SELF_AUDIT stage="POST_INPUT_CONTRACT_POLISH" status="PENDING_COMPILE">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Legacy controller/synchronous movement archaeology logged; new KCC route avoids `CharacterController` and direct runtime force ownership.</Task>
    <Task id="02" status="PASS_STATIC">Deferred `CapsulecastCommand.ScheduleBatch` path remains async in `FixedTick`; no sync `Physics.CapsuleCast/SphereCast` in KCC target path.</Task>
    <Task id="03" status="PASS_STATIC">`KinematicStateDTO` is field-only explicit unmanaged state; jobs mutate through `UnsafeUtility.AsRef`.</Task>
    <Task id="04" status="PASS_STATIC">Editor-only `HydrodynamicKccLayoutValidator` checks `UnsafeUtility.SizeOf` and field offsets, now including `HydrodynamicKccInputDTO`.</Task>
    <Task id="05" status="PASS_STATIC">Mock input uses deterministic `Unity.Mathematics.Random` and owner-local `NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter`; Core `InputStateDTO` is not shadowed.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration uses analytical nonlinear drag, buoyancy, added mass, finite guards, and no `Rigidbody.AddForce`.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command build and capsule batch without main-thread completion.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie water resistance is scalar drag/turbulence/wake metadata, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Post-simulation resolver consumes `HydrodynamicKccCollisionHitDTO` and projects velocity along contact normals.</Task>
    <Task id="10" status="PASS_STATIC">Resolved AUP is millimeter-quantized after adding local float translation to double3 truth.</Task>
    <Task id="11" status="PASS_STATIC">Collision hit budget and resolver passes scale continuously from 2 to 8 via `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence copies contiguous `KinematicStateDTO` bytes and exposes an owner-local resim seam.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync subtracts sector/camera AUP and EWMA-lerps local float output only at presentation edge.</Task>
    <Task id="14" status="PASS_STATIC">Wake output uses `SignalBus<WakeGeneratedSignal>.ParallelWriter`; magnitude/radius stay packed in owner-local packet/source flags.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit/result Vault lanes use `NativeArrayOptions.UninitializedMemory`; readiness and seed/clear jobs prevent unsafe reads.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring and 64-byte per-entity fault slots remain in Vault; native-span dump path avoids managed byte arrays.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner reads/writes Vault tuning and draws cursor-ordered telemetry graph.</Task>
    <Task id="18" status="PASS_STATIC">CSV ingest uses `ReadOnlySpan<byte>`, FNV-1a, flat profile table, and buckets instead of private persistent hash maps.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo path reads solver debug DTO for current/predicted capsules and collision normal.</Task>
    <Task id="20" status="PENDING_COMPILE">Self-audit and static scans are appended; build is blocked by CPU guard, so final verification is not closed.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <KinematicStateDTO size="64" alignment="16">
      <field name="AUP_Position" offset="0" size="24" type="double3"/>
      <field name="Velocity" offset="24" size="12" type="float3"/>
      <field name="AngularVelocity" offset="36" size="12" type="float3"/>
      <field name="Mass" offset="48" size="4" type="float"/>
      <field name="DragCoefficient" offset="52" size="4" type="float"/>
      <field name="_pad0.._pad7" offset="56" size="8" type="byte[8]"/>
      <proof>24+12+12+4+4+8=64; 64 % 16 = 0.</proof>
    </KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">
      <field name="TargetAup" offset="0" size="24" type="double3"/>
      <field name="MoveAxis" offset="24" size="12" type="float3"/>
      <field name="LookAxis" offset="36" size="12" type="float3"/>
      <field name="SimulationFrame" offset="48" size="4" type="uint"/>
      <field name="Sequence" offset="52" size="4" type="uint"/>
      <field name="Flags" offset="56" size="4" type="uint"/>
      <field name="SourceHash" offset="60" size="4" type="uint"/>
      <proof>24+12+12+4+4+4+4=64; 64 % 16 = 0.</proof>
    </HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line"/>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below 0.3 quality, hit scheduling/resolution collapses toward two records, visual smoothing and compute-use estimates remain low, and hydrodynamics stay analytical drag plus scalar turbulence. At higher weights the same kernels spend extra hit records and wake metadata density; there is no low/high binary switch.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">
    ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Independent NativeArray lanes in Burst jobs are marked `[NoAlias]`; fault slots are padded to 64 bytes.</NoAlias>
    <Graph>clearFaults -> mockInput|armedExternalInput|clearInput -> integration -> commandBuild -> CapsulecastCommand.ScheduleBatch -> extractHits -> resolution -> visualSync/rollbackFence/wakeEmit -> lateFrameSwap.</Graph>
    <Completes>No direct hot-path `JobHandle.Complete()` calls in KCC; explicit rollback/teardown use dispatcher-owned forced completion.</Completes>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No KCC asmdef or sibling-domain reference was added. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` remains blocked by CPU guard: latest CPU samples exceeded 50% while `dotnet/csc` were absent.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: CPU fluid truth would be O(n*particles) or O(n*fluid_voxels). After: KCC hydrodynamics are O(n*h) where h is quality-scaled 2-8 capsule-hit records, with water feel sold by scalar drag, turbulence, wake metadata, camera/audio/GPU consumers.
  </DEAR_LIE>
</SELF_AUDIT>

<SELF_AUDIT stage="LATEST_POST_BATCH_WINDOW_POLISH" status="PENDING_COMPILE">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Project-owned scan found no `CharacterController`; legacy `MovePosition` routes are logged outside this KCC patch.</Task>
    <Task id="02" status="PASS_STATIC">No project-owned synchronous `Physics.CapsuleCast/SphereCast`; KCC uses deferred `CapsulecastCommand.ScheduleBatch`.</Task>
    <Task id="03" status="PASS_STATIC">Field-only unmanaged `KinematicStateDTO`; mutation uses `UnsafeUtility.AsRef` over Vault arrays.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator proves size/offsets through `UnsafeUtility.SizeOf/GetFieldOffset`.</Task>
    <Task id="05" status="PASS_STATIC">Mock input is Burst/deterministic; KCC packet is `HydrodynamicKccInputDTO`, not Core `InputStateDTO`.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical nonlinear drag, buoyancy, added mass, finite guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation does not complete the collision job; active command/hit subarrays are sliced to frozen batch facts.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie water resistance is scalar drag/turbulence/wake metadata, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver consumes owner-local hit DTOs, projects velocity, and applies only remaining timestep after contact.</Task>
    <Task id="10" status="PASS_STATIC">Authoritative AUP stays double3 and is millimeter-quantized after local float displacement.</Task>
    <Task id="11" status="PASS_STATIC">Resolver budget is continuous `math.lerp(2,8,GlobalQualityWeight)`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence MemCpys contiguous DTO bytes; rollback resim bypasses visual smoothing.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync subtracts sector/camera AUP before local float EWMA interpolation.</Task>
    <Task id="14" status="PASS_STATIC">Wake emission uses SignalBus ParallelWriter and owner-local bounded AUP48 conversion.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit lanes use uninitialized Vault buffers; stale oversized lanes are excluded by active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring and 64-byte fault slots live in Vault; native-span dump path is implemented.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and reads telemetry graph.</Task>
    <Task id="18" status="PASS_STATIC">CSV parser is span/FNV/bucket-array based; no `string.Split`, LINQ, or private persistent hash map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo cache reads solver debug DTO independently from transform application.</Task>
    <Task id="20" status="PENDING_COMPILE">Static proof and self-audit exist; build remains blocked by CPU/dotnet guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <KinematicStateDTO size="64"><field name="AUP_Position" offset="0" size="24"/><field name="Velocity" offset="24" size="12"/><field name="AngularVelocity" offset="36" size="12"/><field name="Mass" offset="48" size="4"/><field name="DragCoefficient" offset="52" size="4"/><field name="_pad0.._pad7" offset="56" size="8"/><proof>24+12+12+4+4+8=64; 64%16=0.</proof></KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64"><field name="TargetAup" offset="0" size="24"/><field name="MoveAxis" offset="24" size="12"/><field name="LookAxis" offset="36" size="12"/><field name="SimulationFrame" offset="48" size="4"/><field name="Sequence" offset="52" size="4"/><field name="Flags" offset="56" size="4"/><field name="SourceHash" offset="60" size="4"/><proof>24+12+12+4+4+4+4=64; 64%16=0.</proof></HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable slot per entity cache line"/>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work collapses toward two hit records, smoothing alpha and hydrodynamic scalar costs are lerped down, and rollback fast-forward count is capped continuously. Higher quality consumes up to eight hit records and richer wake metadata without binary hardware switches.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">ShinobuHydroKccStates, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH><NoAlias>NativeArray lanes carry `[NoAlias]`; state mutation uses `UnsafeUtility.AsRef`; fault flags are 64-byte slots.</NoAlias><Graph>clearFaults -> input -> integration -> commandBuild -> active capsule batch -> extractHits -> resolution -> visualSync/rollbackFence/wakeEmit -> lateFrameSwap.</Graph><FrozenFacts>`_scheduledEntityCount` and `_scheduledMaxHitsPerCommand` are frozen in FixedTick and consumed in PostFixedTick.</FrozenFacts></POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No standalone KCC asmdef was added because root `Hecton8.Core` scripts already import `Hecton8.Physics.KCC`; moving KCC now would require broader owner migration to avoid cycles. Build command remains blocked: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.</COMPILE_GUARD>
  <DEAR_LIE>Before: O(n*particles) or O(n*fluid_voxels) CPU fluid truth. After: O(n*h) KCC where h is 2-8 quality-scaled hit records; water feel is sold by nonlinear drag, scalar turbulence, wake metadata, and downstream GPU/audio consumers.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Latest Append Marker After Audit Hardening

What was wrong:
- The post-audit hardening entry was appended into an earlier log position by the patch context, so the bottom of the file did not reflect the newest state.

What was done:
- Re-anchored the latest log tail here. The detailed `POST_AUDIT_HARDENING` self-audit above covers the 20-task reconciliation, layout proof, Vault handle list, dependency graph, compile guard, and Dear Lie proof.
- Current new files/docs: `Docs/ARCHITECTURE/SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`, `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md`, and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Current static check state: targeted KCC forbidden-pattern scan is clean; `git diff --check` passed with line-ending warnings only.
- H-PHI correction: editor telemetry no longer allocates a private managed snapshot array; it reads Vault telemetry only while the KCC runtime has no scheduled batch.

Cinematic Cheats used:
- Same route: analytical drag/added mass/buoyancy plus turbulence/wake metadata; no CPU fluid truth.

Exact microseconds saved:
- No new runtime speed claim beyond prior analytical-route savings.

Verification state:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was not launched. Latest guard: no active `dotnet/csc`, CPU Processor Time `100, 98.45, 93.64, 100, 83.19`; Processor Utility `86.07, 84.24, 78.98, 84.22, 70.77`.

## 2026-05-19 - Compile Guard Recheck 20

What was wrong:
- The editor telemetry H-PHI correction changed code after Recheck 19, so compile proof is still required.

What was done:
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 100, 100, 100, 100`.
- Processor Utility samples: `85.66, 83.27, 83.56, 86.12, 83.12`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build not launched. CPU guard is hard red.

## 2026-05-19 - Compile Guard Recheck 21

What was wrong:
- The editor telemetry method rename touched code after Recheck 20, so compile proof is still required.

What was done:
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 100, 100, 98.84, 79.79`.
- Processor Utility samples: `86.79, 80.31, 84.2, 80.39, 73.64`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build not launched. CPU remained above the explicit 50% guard threshold.

## 2026-05-19 - Compile Guard Recheck 23 And Fresh Self-Audit

What was wrong:
- The previous `SELF_AUDIT` block predated the final Vault window fail-closed pass at the bottom of this log, so the newest evidence needed a fresh anchor.
- Compile proof is still owed, but the explicit workstation guard is red.

What was done:
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 100, 100, 100, 100`.
- Processor Utility samples: `85.87, 81.22, 84.26, 84.13, 83.98`.
- Re-ran targeted KCC forbidden-pattern scan for `CharacterController`, sync `Physics.CapsuleCast/SphereCast`, `Rigidbody.AddForce`, private Native containers, `Pack=1`, `UnityEngine.Random`, `foreach`, `.Complete(`, `FindObject`, `Camera.main`, and string formatting. Result: no matches in `Assets/_Project/Scripts/Physics/KCC`.

Cinematic Cheats used:
- No CPU fluid truth. Hydrodynamics remain analytical drag, added-mass scalar, buoyancy scalar, turbulence scalar, wake metadata, and downstream GPU/audio presentation.

Exact microseconds saved:
- No new profiler-backed timing claim. Static route still avoids main-thread sync casts and Rigidbody force dispatch; expected savings remain bounded to prior rationale estimates.

Verification state:
- Build not launched. CPU guard is hard red.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">Legacy archaeology refreshed: no first-party `CharacterController` route entered the KCC path; remaining `Rigidbody.MovePosition` hits are logged as outside-owner legacy presentation routes.</Task>
    <Task id="02" status="PASS_STATIC">Synchronous `Physics.CapsuleCast/SphereCast` is absent from the KCC path; movement collision uses `CapsulecastCommand.ScheduleBatch` with deferred post-resolution.</Task>
    <Task id="03" status="PASS_STATIC">Hot DTOs use public fields and explicit layout; mutation is routed through `UnsafeUtility.AsRef` over Vault-backed arrays.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` for the primary 64-byte DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock input queue and external input contract use blittable `HydrodynamicKccInputDTO`, source hash, sequence, frame, and sector-generation proof.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integration is analytical drag plus buoyancy and added-mass scalar; no `Rigidbody.AddForce` authority exists in the KCC route.</Task>
    <Task id="07" status="PASS_STATIC">Collision command build, PhysX batch, hit extraction, and resolution are chained by `JobHandle` without arbitrary main-thread completion in normal frames.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie water resistance replaces CPU fluid truth with nonlinear drag and wake scalars.</Task>
    <Task id="09" status="PASS_STATIC">Slide solver advances to contact, projects velocity off the hit normal, then spends remaining timestep on the projected direction.</Task>
    <Task id="10" status="PASS_STATIC">Authoritative AUP remains `double3`; local float math happens only after sector/camera subtraction, and final AUP is millimeter quantized.</Task>
    <Task id="11" status="PASS_STATIC">Collision hit budget and resolver work scale continuously through `math.lerp(2, 8, GlobalQualityWeight)`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback seam uses contiguous `UnsafeUtility.MemCpy` snapshots and an explicit visual-smoothing bypass.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync writes local float output after AUP subtraction and EWMA smoothing; presentation can be disabled without losing gizmo diagnostics.</Task>
    <Task id="14" status="PASS_STATIC">Wake emission uses `SignalBus<WakeGeneratedSignal>.ParallelWriter`, owner-local AUP48 clamping, and packed radius/magnitude metadata.</Task>
    <Task id="15" status="PASS_STATIC">Command/result lanes are Vault-owned and uninitialized; active subarray scheduling prevents stale oversized Vault windows from being submitted.</Task>
    <Task id="16" status="PASS_STATIC">Black-box telemetry writes one 300-frame ring entry per post-resolution frame and uses 64-byte per-entity fault slots plus native-span dump path.</Task>
    <Task id="17" status="PASS_STATIC">Editor UI Toolkit tuner edits Vault tuning and reads telemetry only while no KCC batch is scheduled.</Task>
    <Task id="18" status="PASS_STATIC">CSV tuning ingestion uses allocation-free `ReadOnlySpan<byte>`, FNV-1a profile hashes, and Vault-backed profile/bucket arrays.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo prediction reads solver debug DTO and cached capsule data; no fallback component lookup remains in the gizmo route.</Task>
    <Task id="20" status="PENDING_COMPILE">Self-audit is appended here; compiler proof is blocked by Recheck 23 CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64" alignment="16">
      <Field name="AUP_Position" offset="0" size="24"/>
      <Field name="Velocity" offset="24" size="12"/>
      <Field name="AngularVelocity" offset="36" size="12"/>
      <Field name="Mass" offset="48" size="4"/>
      <Field name="DragCoefficient" offset="52" size="4"/>
      <Field name="_pad0.._pad7" offset="56" size="8"/>
      <Math>24+12+12+4+4+8=64; 64%16=0.</Math>
    </KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">
      <Field name="TargetAup" offset="0" size="24"/>
      <Field name="MoveAxis" offset="24" size="12"/>
      <Field name="LookAxis" offset="36" size="12"/>
      <Field name="SimulationFrame" offset="48" size="4"/>
      <Field name="Sequence" offset="52" size="4"/>
      <Field name="Flags" offset="56" size="4"/>
      <Field name="SourceHash" offset="60" size="4"/>
      <Math>24+12+12+4+4+4+4=64; 64%16=0.</Math>
    </HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>When `GlobalQualityWeight` drops below 0.3, the route collapses toward two collision hit records, lower smoothing alpha, bounded rollback fast-forward count, scalar turbulence, and no extra CPU fluid work. At higher weights, the same path lerps toward eight hit records, richer wake metadata, and smoother visual sync without binary low/high branches.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">Boot/requested handles: ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>NativeArray lanes in jobs are marked `[NoAlias]` where independent; state mutation uses `UnsafeUtility.AsRef`; fault flags are 64-byte per-entity slots.</NoAlias>
    <Consumes>External input writer handle, dispatcher fixed/post/late tick order, DataVault buffer ownership, GlobalQualityWeight, sector origin/generation facts.</Consumes>
    <Outputs>Post-simulation handle, rollback byte fence, wake SignalBus writes, visual output, debug output, telemetry ring entry, fault dump trigger.</Outputs>
    <Graph>clearFaults -> inputGenerationOrClear -> inputSanitize -> hydrodynamicIntegration -> commandBuild -> active CapsulecastCommand batch -> hitExtraction -> kinematicResolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame nonblocking swap.</Graph>
    <FrozenFacts>`_scheduledEntityCount` and `_scheduledMaxHitsPerCommand` are written in `FixedTick` and consumed unchanged in `PostFixedTick`.</FrozenFacts>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime asmdef is introduced; KCC remains in the existing root assembly to avoid an unsafe cyclic migration while communicating through Vault/SignalBus/GlobalRegistry seams. Recheck 23 forbids build: CPU is 100% across all Processor Time samples.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected CPU fluid truth, per-bubble dynamics, and GameObject wake spawning. The fake is analytical drag plus turbulence/wake scalar metadata for downstream shader/audio presentation. Complexity before: O(n*fluid_cells) or O(n*particles) for CPU fluid truth. Complexity after: O(n*h), where h is the continuous 2-8 collision-hit budget.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Vault Window Fail-Closed Pass

What was wrong:
- Static review found that `FixedTick` and `PostFixedTick` relied on earlier Vault readiness for some lanes instead of proving the active scheduling window locally.
- `OnDrawGizmos` still had a fallback `GetComponent<CapsuleCollider>()` lookup, creating avoidable static scan noise inside the KCC source.

What was done:
- Added active-window checks before scheduling: input, proposed velocity, command, raw hit, fault, wake, telemetry ring, and telemetry cursor lanes must resolve created and long enough for the frozen entity/hit count. Failed fixed-phase validation clears the frozen batch facts before returning.
- Added `[DisallowMultipleComponent]` and `[RequireComponent(typeof(CapsuleCollider))]` to `HydrodynamicKccRuntime`.
- Removed the gizmo fallback lookup; it now uses the cached capsule or deterministic default capsule dimensions.

Cinematic Cheats used:
- No new physical truth. The route remains analytical drag + scalar wake metadata.

Exact microseconds saved:
- No honest runtime timing claim without profiler. Static effect: avoids a possible editor gizmo component lookup and prevents wasted PhysX batch scheduling when a Vault lane is invalid.

Verification state:
- Static source re-read only. Compile still pending guarded build.

## 2026-05-19 - Compile Guard Recheck 22

What was wrong:
- The Vault window fail-closed pass changed runtime source, so compiler proof is still required.

What was done:
- Rechecked `dotnet/csc`: no active compiler-family process was returned.
- Processor Time samples: `100, 97.5, 98.65, 100, 100`.
- Processor Utility samples: `74.86, 78.67, 77.77, 76.73, 81`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Not a runtime change.

Verification state:
- Build not launched. CPU remained above the explicit 50% guard threshold.

## 2026-05-19 - Bottom Self-Audit Reanchor

What was wrong:
- The fresh audit was present but not the final entry because patch context matched an earlier verification marker.

What was done:
- Re-anchored the current `SELF_AUDIT` at the bottom of this log. This is a documentation/log correction only; no runtime source changed after Recheck 23.

Verification state:
- Compiler proof still waits on the explicit CPU/dotnet guard. Latest guard evidence remains Recheck 23: no active `dotnet/csc`, Processor Time `100, 100, 100, 100, 100`, Processor Utility `85.87, 81.22, 84.26, 84.13, 83.98`.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_BOTTOM_REANCHOR">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">No first-party `CharacterController` route in KCC; remaining `Rigidbody.MovePosition` hits are outside-owner legacy presentation routes.</Task>
    <Task id="02" status="PASS_STATIC">No sync `Physics.CapsuleCast/SphereCast` in KCC; collision uses deferred `CapsulecastCommand.ScheduleBatch`.</Task>
    <Task id="03" status="PASS_STATIC">Hot DTOs are explicit-layout public-field structs; state mutation uses `UnsafeUtility.AsRef` over Vault arrays.</Task>
    <Task id="04" status="PASS_STATIC">Editor validator checks `UnsafeUtility.SizeOf` and exact field offsets.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input uses `HydrodynamicKccInputDTO` with frame, sequence, source hash, and sector-generation proof.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamics use analytical drag, buoyancy, added mass, and finite guards; no `Rigidbody.AddForce` authority.</Task>
    <Task id="07" status="PASS_STATIC">Integration, command build, PhysX batch, extraction, resolution, rollback, wake, and visual jobs are dependency-chained.</Task>
    <Task id="08" status="PASS_STATIC">Dear Lie water resistance is scalar nonlinear drag/turbulence, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Slide solver advances to contact, projects off the normal, then spends remaining timestep on projected velocity.</Task>
    <Task id="10" status="PASS_STATIC">AUP remains `double3`; local math casts to `float3` only after sector/camera subtraction and final position is millimeter-quantized.</Task>
    <Task id="11" status="PASS_STATIC">Resolver/hit budget scales continuously with `math.lerp(2,8,GlobalQualityWeight)`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback fence uses contiguous `UnsafeUtility.MemCpy`; explicit rollback seam bypasses visual smoothing.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync is local-float EWMA after AUP subtraction and can be disabled without hiding debug DTOs.</Task>
    <Task id="14" status="PASS_STATIC">Wake output uses `SignalBus<WakeGeneratedSignal>.ParallelWriter`, bounded AUP48 conversion, and packed magnitude/radius.</Task>
    <Task id="15" status="PASS_STATIC">Vault command/hit lanes use uninitialized memory and active subarray scheduling to exclude stale oversized windows.</Task>
    <Task id="16" status="PASS_STATIC">Black-box ring is 300 frames; fault flags are 64-byte per entity; dump path writes native span.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and reads telemetry only when no batch is scheduled.</Task>
    <Task id="18" status="PASS_STATIC">CSV tuning parser is span/FNV/profile-bucket based with no split/LINQ/private hash map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo prediction uses solver debug DTO and cached capsule; no gizmo fallback lookup remains.</Task>
    <Task id="20" status="PENDING_COMPILE">Audit is bottom-anchored; compiler proof is blocked by CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64" alignment="16">Offsets: AUP_Position 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pad 56/8. Math: 24+12+12+4+4+8=64; 64%16=0.</KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">Offsets: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. Math: 24+12+12+4+4+4+4=64; 64%16=0.</HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality the route trends to two hit records, lower smoothing alpha, scalar turbulence, and bounded rollback fast-forward. Above that, the same continuous path lerps toward eight hit records, richer wake metadata, and smoother visual sync. No low/high binary switch.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">Requested handles: ShinobuHydroKccStates, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>[NoAlias] on independent NativeArray lanes; `UnsafeUtility.AsRef` for state mutation; graph: clearFaults -> input -> sanitize -> integration -> commandBuild -> capsule batch -> extractHits -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame swap. Frozen facts: `_scheduledEntityCount`, `_scheduledMaxHitsPerCommand`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>KCC remains in existing root assembly to avoid a cyclic asmdef migration; integration uses Vault/SignalBus/GlobalRegistry seams. Build is forbidden by Recheck 23 CPU guard.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected CPU fluid cells/particles and wake GameObjects. Fake: analytical drag plus turbulence/wake scalar metadata for downstream shader/audio work. Complexity before O(n*fluid_cells) or O(n*particles); after O(n*h), h=2..8 continuous hit budget.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Bottom Self-Audit Reanchor After Dual Dump Patch

What was wrong:
- The dual-dump self-audit was appended, but patch context placed it near the earlier implementation-pass audit instead of the final log entry.

What was done:
- Re-anchored the current dual-dump audit at the bottom. No runtime source changed after the dual dump-path patch.

Verification state:
- Compiler proof still waits on the explicit CPU/dotnet guard.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_DUAL_DUMP_BOTTOM_REANCHOR">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">No first-party `CharacterController` route in KCC; legacy `Rigidbody.MovePosition` hits remain outside-owner presentation debt.</Task>
    <Task id="02" status="PASS_STATIC">KCC has no sync `Physics.CapsuleCast/SphereCast`; it schedules deferred `CapsulecastCommand` batches.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data mutated through Vault arrays and `UnsafeUtility.AsRef`.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks size and offsets for 64-byte KCC DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path is present and sanitized by frame/source/sector proof.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical drag, buoyancy, added mass, and NaN guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation does not block on the capsule batch; post/late phases own completion windows.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is the Dear Lie scalar route, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver projects along collision normals and spends only remaining timestep after contact.</Task>
    <Task id="10" status="PASS_STATIC">AUP is `double3`, local math follows sector subtraction, and final AUP is millimeter quantized.</Task>
    <Task id="11" status="PASS_STATIC">Iterations/hit budget scale continuously with `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback uses contiguous memcpy bytes and explicit visual smoothing bypass.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates presentation-local float output after AUP localization.</Task>
    <Task id="14" status="PASS_STATIC">Wake data leaves via unmanaged SignalBus packet without GameObject spawning.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes use uninitialized memory and active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame ring writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin` from the same native span; fault flags are 64-byte per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning without C# recompile.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest is span/FNV/Vault-array based, not split/LINQ/private map.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO and cached capsule dimensions.</Task>
    <Task id="20" status="PENDING_COMPILE">Audit refreshed after dual dump patch; compiler proof still pending guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>KinematicStateDTO=64 bytes: AUP 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pads 56..63. HydrodynamicKccInputDTO=64 bytes: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. HydrodynamicKccFaultFlagDTO=64 bytes per entity to block false sharing.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work tends to two hit records, reduced smoothing, scalar turbulence, and bounded rollback fast-forward. Higher quality lerps to eight hit records and richer wake metadata without a binary tier branch.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">States, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>[NoAlias] lanes feed clearFaults -> input -> sanitize -> integration -> commandBuild -> capsule batch -> hitExtraction -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame swap. Frozen facts: scheduled entity count and max-hit stride.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling asmdef split; KCC uses existing root assembly and communicates through Vault, SignalBus, and registry seams. Build remains guarded.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>CPU fluid field/particles rejected. Analytical drag plus turbulence/wake scalar replaces O(n*fluid_cells) or O(n*particles) with O(n*h), h=2..8 continuous hit budget.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Bottom Compile Guard Recheck 24

What was wrong:
- The Recheck 24 evidence was appended near an earlier audit block because the log has multiple `SELF_AUDIT` sections.

What was done:
- Re-anchored Recheck 24 at the bottom. The targeted KCC forbidden-pattern scan returned no matches. `dotnet/csc` was absent. Processor Time was `100, 100, 100, 100, 100`; Processor Utility was `78.09, 82.48, 82.21, 80.75, 80.51`.

Verification state:
- Build not launched. CPU guard remains hard red after the dual dump-path source patch.

## 2026-05-19 - Editor Telemetry View Recheck 25

What was wrong:
- The editor tuner graph was allocation-free, but it resolved the same Vault telemetry ring/cursor for cursor discovery, max-speed scan, and line drawing. At 300 telemetry entries that is up to 599 read calls and hundreds of duplicate handle resolves per repaint.

What was done:
- Added `HydrodynamicKccRuntime.TryGetEditorTelemetryVaultView(...)` under `#if UNITY_EDITOR`.
- Changed `HydrodynamicKccTunerWindow.VelocityGraphElement` to resolve one diagnostic `NativeArray<KinematicTelemetryEntry>` view per repaint and read graph samples directly from that view.
- Kept the older per-index `TryReadEditorTelemetryVault(...)` as a compatibility wrapper.

Cinematic Cheats used:
- No physical simulation changed. The tuner remains a control facade over the analytical hydrodynamic Dear Lie already used by runtime: scalar drag plus turbulence/wake metadata.

Exact Microseconds saved:
- Runtime: 0 us because this path is editor-only.
- Editor repaint: removes up to 598 duplicate Vault handle resolves per 300-frame graph repaint. No profiler proof; static estimate only.

Verification:
- Targeted KCC forbidden-pattern scan returned no matches after the patch.
- `git diff --check` reported only CRLF normalization warnings.
- Build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 97.28, 100`; Processor Utility `81.2, 82.4, 79.39, 74.96, 75.27`; build not launched.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_EDITOR_VIEW_RECHECK25">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">KCC path has no `CharacterController`; legacy `Rigidbody.MovePosition` hits remain documented outside-owner presentation debt.</Task>
    <Task id="02" status="PASS_STATIC">KCC path has no sync `Physics.CapsuleCast/SphereCast`; it uses deferred `CapsulecastCommand` batches.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data in Vault; no persistent private Native container owns movement state.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks size and field offsets for 64-byte KCC DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path is sanitized by frame, source, sequence, and sector generation.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical drag, buoyancy, added mass, deterministic Burst, and finite guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command/collision work without blocking; post/late phases own completion windows.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is scalar Dear Lie drag/turbulence, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver advances to contact, projects velocity along collision normal, and spends remaining timestep only.</Task>
    <Task id="10" status="PASS_STATIC">AUP is double3, localized before float math, then millimeter-quantized.</Task>
    <Task id="11" status="PASS_STATIC">Hit budget and iterations scale continuously with `GlobalQualityWeight`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback uses contiguous memcpy bytes and an explicit smoothing-bypass resim seam.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates local-float presentation after AUP localization.</Task>
    <Task id="14" status="PASS_STATIC">Wake exits through unmanaged SignalBus packet with bounded AUP48 conversion and packed magnitude/radius.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes use uninitialized memory and active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin`; fault flags are 64 bytes per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and graph reads one editor-only telemetry Vault view per repaint.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest uses span/FNV/Vault arrays, not split/LINQ/private maps.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO and cached capsule dimensions.</Task>
    <Task id="20" status="PENDING_COMPILE">Audit refreshed after editor view patch; compiler proof still pending guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>KinematicStateDTO=64 bytes: AUP 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pads 56..63. HydrodynamicKccInputDTO=64 bytes: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. HydrodynamicKccFaultFlagDTO=64 bytes per entity to block false sharing.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work tends to two hit records, reduced smoothing, scalar turbulence, and bounded rollback fast-forward. Higher quality lerps toward eight hit records and richer wake metadata without binary low/high branching.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">States, Inputs, ProposedVelocities, CollisionCommands, CollisionHits, ResolvedHits, PreviousAup, VisualOutputs, TelemetryRing, TelemetryCursor, Tuning, FluidProfiles, FluidProfileBuckets, RollbackBytes, FaultFlags, WakePackets, DebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>[NoAlias] lanes feed clearFaults -> input -> sanitize -> integration -> commandBuild -> capsule batch -> hitExtraction -> resolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame swap. Frozen facts: scheduled entity count and max-hit stride.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling asmdef split; KCC communicates through Vault, SignalBus, and registry seams. Build remains blocked by CPU guard.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>CPU fluid fields/particles remain rejected. Analytical drag plus turbulence/wake scalar replaces O(n*fluid_cells) or O(n*particles) with O(n*h), h=2..8 continuous hit budget.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Bottom Compile Guard Recheck 26

What was wrong:
- The Recheck 26 note matched an earlier audit terminator during patching.

What was done:
- Re-anchored Recheck 26 at the current bottom after the editor telemetry view audit.
- Targeted KCC forbidden-pattern scan returned no matches.
- Build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 100, 100`; Processor Utility `80.78, 79.16, 84.72, 81.9, 80.45`.

Verification state:
- Build not launched. CPU guard remains red.

## 2026-05-19 - Bottom SIMD Magnitude Recheck 27

What was wrong:
- The Recheck 27 self-audit existed higher in this file because earlier patch context matched a prior compile-guard section. The log bottom still showed Recheck 26, so the CTO-visible newest entry did not reflect the SIMD magnitude patch.
- A too-strict prompt extraction command initially missed the live `SHINOBU_113` tag because `CURRENT_BATCH.md` includes `role` and `chat_name` attributes.

What was done:
- Re-anchored the current Recheck 27 audit at the true bottom of `Docs/AgentLogs/LOG_SHINOBU_113.md`.
- Re-confirmed the assignment from `Docs/Tasks/CURRENT_BATCH.md:747-783`.
- Re-ran KCC-targeted forbidden-pattern scans after the audit/status/rationale updates. Both targeted scans returned no matches.
- Re-ran `git diff --check` for the touched KCC/docs files; it reported only CRLF normalization warnings.
- Re-ran the build guard. No active `dotnet/csc` was returned. Processor Time was `100, 100, 100, 100, 100`; Processor Utility was `84.32, 82.31, 78.46, 80.66, 83.59`. Build was not launched.

Cinematic Cheats used:
- No CPU fluid truth was added. Hydrodynamic feel remains analytical drag plus scalar turbulence/wake metadata for presentation systems.

Exact Microseconds saved:
- Runtime proof remains pending profiler/Burst Inspector. Static estimate only: repeated magnitude work now uses a guarded rsqrt form across integration, command build, resolution, telemetry, visual sync, wake emission, and gizmo display. Expected gain is sub-microsecond per entity at small counts; it matters when entity count and 2..8 hit budget scale.

Verification state:
- Static KCC source scan is clean for the current forbidden set.
- Task 20 remains pending compiler/runtime proof because CPU is above the explicit build threshold.

<SELF_AUDIT agent_id="SHINOBU_113" domain="HYDRODYNAMIC_KINEMATICS_DIRECTOR" timestamp="2026-05-19TLOCAL" verification="PASS_STATIC_PENDING_COMPILE_RSQRT_BOTTOM_RECHECK27">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS_STATIC">KCC path has no `CharacterController`; remaining `Rigidbody.MovePosition` archaeology is outside-owner legacy presentation debt.</Task>
    <Task id="02" status="PASS_STATIC">KCC path has no synchronous `Physics.CapsuleCast` or `Physics.SphereCast`; it uses deferred `CapsulecastCommand.ScheduleBatch`.</Task>
    <Task id="03" status="PASS_STATIC">Hot state is explicit public-field DTO data in Vault; no persistent private Native container owns KCC movement state.</Task>
    <Task id="04" status="PASS_STATIC">Editor layout validator checks `UnsafeUtility.SizeOf` and exact field offsets for 64-byte KCC DTOs.</Task>
    <Task id="05" status="PASS_STATIC">Deterministic mock/external input path exists and is sanitized by frame, source, sequence, and sector generation.</Task>
    <Task id="06" status="PASS_STATIC">Hydrodynamic integrator uses analytical drag, `LengthSafe` rsqrt magnitude, buoyancy, added mass, deterministic Burst flags, and finite guards.</Task>
    <Task id="07" status="PASS_STATIC">Simulation schedules command/collision work without blocking; post/late phases own completion windows and abort drains.</Task>
    <Task id="08" status="PASS_STATIC">Water resistance is scalar Dear Lie drag/turbulence, not CPU fluid truth.</Task>
    <Task id="09" status="PASS_STATIC">Resolver advances to contact, projects velocity along collision normal, spends remaining timestep only, and uses `LengthSafe` for displacement distance.</Task>
    <Task id="10" status="PASS_STATIC">AUP is `double3`, localized before float math, and millimeter-quantized after resolution.</Task>
    <Task id="11" status="PASS_STATIC">Hit budget and iterations scale continuously with `math.lerp(2, 8, GlobalQualityWeight)`.</Task>
    <Task id="12" status="PASS_STATIC">Rollback uses contiguous memcpy bytes and an explicit visual-smoothing bypass resimulation seam.</Task>
    <Task id="13" status="PASS_STATIC">Visual sync interpolates local-float presentation after AUP localization and reports speed through `LengthSafe`.</Task>
    <Task id="14" status="PASS_STATIC">Wake exits through unmanaged SignalBus packet with bounded AUP48 conversion, packed magnitude/radius, and rsqrt-guarded magnitude fallback.</Task>
    <Task id="15" status="PASS_STATIC">Command/hit Vault lanes use uninitialized memory and active subarray scheduling.</Task>
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring writes both `Dump_SHINOBU_113.bin` and `Dump_KINEMATICS_SURGEON.bin`; fault flags are 64 bytes per entity.</Task>
    <Task id="17" status="PASS_STATIC">UI Toolkit tuner edits Vault tuning and graph reads one editor-only telemetry Vault view per repaint.</Task>
    <Task id="18" status="PASS_STATIC">CSV profile ingest uses span/FNV/Vault arrays, not split/LINQ/private maps.</Task>
    <Task id="19" status="PASS_STATIC">Gizmo reads solver debug DTO, cached capsule dimensions, and `NormalizeSafe` for collision normal display.</Task>
    <Task id="20" status="PENDING_COMPILE">Bottom audit refreshed after rsqrt magnitude patch; compiler, Unity import, Burst Inspector, profiler, GCMonitor, Play Mode rollback, and player-build proof remain pending under CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <KinematicStateDTO size="64" alignment="16">Offsets: AUP_Position 0/24, Velocity 24/12, AngularVelocity 36/12, Mass 48/4, DragCoefficient 52/4, pad 56/8. Math: 24+12+12+4+4+8=64; 64%16=0.</KinematicStateDTO>
    <HydrodynamicKccInputDTO size="64" alignment="16">Offsets: TargetAup 0/24, MoveAxis 24/12, LookAxis 36/12, SimulationFrame 48/4, Sequence 52/4, Flags 56/4, SourceHash 60/4. Math: 24+12+12+4+4+4+4=64; 64%16=0.</HydrodynamicKccInputDTO>
    <HydrodynamicKccFaultFlagDTO size="64" false_sharing="one mutable fault slot per entity cache line">Offset 0/4 fault mask plus explicit padding through offset 63. Every worker writes its own cache line.</HydrodynamicKccFaultFlagDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below 0.3 quality, collision work trends to two hit records, reduced smoothing, scalar turbulence, guarded rsqrt magnitudes, and bounded rollback fast-forward. Higher quality lerps toward eight hit records, smoother visual sync, richer wake metadata, and more collision polish. No binary hardware switch is introduced.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">Requested handles: ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccCollisionCommands, ShinobuHydroKccCollisionHits, ShinobuHydroKccResolvedHits, ShinobuHydroKccPreviousAup, ShinobuHydroKccVisualOutputs, ShinobuHydroKccTelemetryRing, ShinobuHydroKccTelemetryCursor, ShinobuHydroKccTuning, ShinobuHydroKccFluidProfiles, ShinobuHydroKccFluidProfileBuckets, ShinobuHydroKccRollbackBytes, ShinobuHydroKccFaultFlags, ShinobuHydroKccWakePackets, ShinobuHydroKccDebugOutputs.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>Independent NativeArray lanes in jobs are marked `[NoAlias]` where applicable; state mutation uses `UnsafeUtility.AsRef`; fault flags are 64-byte per-entity lanes to avoid shared cache-line writes.</NoAlias>
    <Consumes>External input writer handle, dispatcher fixed/post/late lane ordering, DataVault handles, `GlobalQualityWeight`, fixed simulation delta, sector AUP, and sector-generation facts.</Consumes>
    <Outputs>Command build handle, deferred capsule batch handle, post-simulation resolution handle, rollback byte fence, wake SignalBus writes, visual output DTOs, debug output DTOs, telemetry ring entry, and fault dump trigger.</Outputs>
    <Graph>clearFaults -> mock/inputClear/externalWriter -> sanitize -> hydrodynamicIntegration -> commandBuild -> active CapsulecastCommand batch -> hitExtraction -> kinematicResolution -> visualSync/rollbackFence/wakeEmit/telemetryAggregate -> lateFrame nonblocking swap.</Graph>
    <FrozenFacts>`_scheduledEntityCount` and `_scheduledMaxHitsPerCommand` are written in `FixedTick` and consumed unchanged in `PostFixedTick`.</FrozenFacts>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No `Hecton8.Physics.KCC.Runtime.asmdef` exists in this checkout, so no new sibling-runtime asmdef reference was introduced. KCC remains under the existing root assembly and communicates through Vault handles, SignalBus, and registry-cached services. File-level concrete cross-domain aliases are limited to existing root-owned AUP/dispatcher helpers in `Hecton8.World`; no AI, rendering, audio, netcode, vehicle, save, or thermodynamics sibling runtime dependency is added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected CPU Navier-Stokes, per-bubble dynamics, mesh-water friction, and wake GameObject spawning. The fake is analytical drag plus turbulence/wake scalar metadata for downstream shader/audio/camera work. Complexity before: O(n*fluid_cells) or O(n*particles). Complexity after: O(n*h), where h is the continuous 2..8 collision-hit budget; the rsqrt pass reduces scalar magnitude overhead without adding simulation truth.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
