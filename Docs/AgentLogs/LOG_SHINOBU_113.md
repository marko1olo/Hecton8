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
- Added `_dumpedFaultMask` so `Dump_KINEMATICS_SURGEON.bin` is written once per distinct fault mask while preserving the fault flag for diagnostics.

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
    <Task id="16" status="PASS_STATIC">300-frame telemetry ring and 64-byte per-entity fault flags are implemented; dump path writes native span to `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`.</Task>
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
