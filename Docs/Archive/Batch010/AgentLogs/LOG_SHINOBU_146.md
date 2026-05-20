# SHINOBU_146 Log - 2026-05-19

What was wrong: mesofauna predator cognition risked the exact failure mode from the prompt: managed state-machine thinking, runtime-float steering, potential NavMesh assumptions, no isolated mock targets, no blackbox ring, and no designer-facing cold tuning path.

What was done: added `MesofaunaBehavioralStateMachine.cs`, integrated it through `PredatorCognitionDomain`, reserved owner-local mesofauna Vault buffer IDs, added editor-only UI Toolkit tuner and gizmo hooks, and wrote status/rationale evidence to disk. Runtime authority is now a Burst deterministic byte FSM over explicit DTOs, not `NavMeshAgent` or OOP state objects.

Lifecycle hardening: after static review, mesofauna species profile, profile count, and CSV scratch lanes were added to the initialized-check, partial-allocation release path, and dispose default reset. This removes a stale-handle risk during failed boot or hot reload.

Contract hardening: scent tracking no longer references unproven `ChemicalBreadcrumbWaypoint.AbsolutePositionDouble`. It uses the existing breadcrumb contract (`RuntimePosition`, `RadiusMeters`, `ExpiresAt`, `Channels`) and converts runtime waypoint coordinates into AUP-local deltas before steering.

Timeout hardening: `StateTimeoutSeconds` now feeds a deterministic quality-scaled tick cap for Search/Flee transitions. The editor slider is no longer a cosmetic DTO field.

Cinematic cheats used: no Animator blend tree. FSM emits `MesofaunaVisualSyncDTO` state/speed/scent/obstacle scalars for VAT/IK/shader swim waves. No raycast pathfinding. Obstacle avoidance is SDF/voxel gradient repulsion. Target lookup is a flat spatial hash, not physics overlap.

Exact microseconds saved, static estimates only until profiler proof: OOP state dispatch removal 35-70 us/frame at 50 predators; NavMesh avoidance prevents unbounded bake/query stalls; flat bucket lookup 60-180 us/frame at 256 slots vs broadphase scans; Dear Lie animation scalar output 30-80 us/frame; SDF avoidance 25-120 us/frame under terrain clutter; low-quality slice modulo 10 can shed roughly 0.1-0.4 ms on dense predator frames.

Build status: attempted once after gate cleared. `dotnet build Hecton8.Core.csproj --no-restore` failed before SHINOBU_146 code analysis with CS2001 because tracked files `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` are absent on disk. I did not restore or recreate them from fauna ownership.

<SELF_AUDIT agent_id="SHINOBU_146" date="2026-05-19">
  <TASK_RECONCILIATION>
    <TASK id="01" name="OOP_STATE_MACHINE_ERADICATION" result="PASS">No first-party `IState`, `State_Wander`, `State_Attack`, or virtual `UpdateState` state classes found in fauna AI scan. Remaining `FaunaStateMachine` is serialized legacy facade/cache. New authority is byte state in `MesofaunaStateDTO` and Burst `switch(CurrentState)`.</TASK>
    <TASK id="02" name="NAVMESH_AGENT_PURGE" result="PASS">`rg NavMeshAgent|UnityEngine.AI|m_AgentTypeID` across source/prefabs/scenes/data returned no first-party hit. Navigation is steering plus SDF repulsion.</TASK>
    <TASK id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS">Hot DTOs expose fields. New files have no auto-properties. `MesofaunaStateDTO.AsMutableRef(void*)` uses `UnsafeUtility.AsRef` for direct state mutation.</TASK>
    <TASK id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS">`MesofaunaStateDTO` is explicit 64 bytes with pad bytes at 48-63 and runtime/editor validation through `UnsafeUtility.SizeOf` and offsets.</TASK>
    <TASK id="05" name="EMERGENCY_MOCK_TARGET_DATA" result="PASS">`GenerateMesofaunaMockTargetsJob` writes deterministic moving target AUPs/hashes into `MesofaunaMockPreyTargets` Vault lane.</TASK>
    <TASK id="06" name="BURST_FSM_EVALUATION_KERNEL" result="PASS">`MesofaunaBehaviorJob : IJobParallelFor` is Burst deterministic, NoAlias annotated, raw-pointer state mutation, and switch-governed Idle/Search/Hunt/Flee/TrackScent logic.</TASK>
    <TASK id="07" name="SPATIAL_HASH_TARGET_ACQUISITION" result="PASS_WITH_ARCHITECTURE_SUBSTITUTION">Implemented flat Vault bucket heads/next hash arrays instead of private `NativeParallelMultiHashMap`. This preserves the requested spatial hash while obeying H-PHI Vault ownership and flat-array mandate.</TASK>
    <TASK id="08" name="THE_DEAR_LIE_ANIMATION_STATE" result="PASS">`MesofaunaVisualSyncDTO` emits state byte and speed scalar for VAT/IK/shader swim fakes. No Animator parameter writes.</TASK>
    <TASK id="09" name="SDF_OBSTACLE_AVOIDANCE" result="PASS">Threat/SDF voxel payload is sampled in Burst; gradient repulsion uses guarded reciprocal and no raycast/path nodes.</TASK>
    <TASK id="10" name="CONTINUOUS_SCALABILITY_TIME_SLICING" result="PASS">`GlobalQualityWeight` smoothstep drives radius 22-104m and slice modulo 10->1. Continuity frames keep smooth motion while expensive brain refresh degrades.</TASK>
    <TASK id="11" name="CHEMICAL_SCENT_TRACKING" result="PASS">Search/Hunt fallback reads `ChemicalBreadcrumbWaypoint` runtime positions, radius, expiry, and channels, converts waypoint runtime to AUP-local delta, then enters `StateTrackScent` on valid attractant gradient.</TASK>
    <TASK id="12" name="AUP_PRECISION_INTERCEPTION_MATH" result="PASS">Target selection and interception subtract target AUP from predator AUP before casting to local `float3`; intercept lead is local delta plus target velocity.</TASK>
    <TASK id="13" name="DAMAGE_AND_FLEE_ROUTING" result="PASS">`SignalBus<CombatDamageSignal>` is consumed in `BeginDispatcherFrame`; matching predator writes `StateFlee`, source hash, due flag, and override threat position.</TASK>
    <TASK id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS">Authoritative mesofauna DTOs are blittable/fixed-size; jobs use `FloatMode.Deterministic`; no `Time.deltaTime` drives state transitions.</TASK>
    <TASK id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS">Vault lanes use `NativeArrayOptions.UninitializedMemory` where appropriate and are cold-overwritten by Burst init job before dispatch.</TASK>
    <TASK id="16" name="TELEMETRY_AI_RECORDER" result="PASS">300-entry 64-byte telemetry ring records counts, microsecond estimates, quality, slice, hashes, AUP probe, and fault metadata; dumps `.bin` and `.h8dump` on non-finite fault.</TASK>
    <TASK id="17" name="BEHAVIOR_TUNER_EDITOR_WINDOW" result="PASS">`MesofaunaAiTunerWindow` UI Toolkit facade reads/writes Vault tuning DTO and draws state pie chart from telemetry. Editor-only cold arrays are explicitly bounded.</TASK>
    <TASK id="18" name="CSV_SPECIES_PARAMETERS_INGESTOR" result="PASS_WITH_ARCHITECTURE_SUBSTITUTION">Cold parser reads CSV bytes into Vault scratch, uses `ReadOnlySpan<byte>` and FNV-1a, then writes fixed flat profile table. Literal `NativeHashMap` replaced with Vault-owned open addressing to satisfy H-PHI.</TASK>
    <TASK id="19" name="LIVE_FSM_DEBUG_GIZMO" result="PASS">Editor-only `MesofaunaFsmDebugGizmo` and tuner SceneView hook draw state-colored vectors from copied Vault data.</TASK>
    <TASK id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="PASS_STATIC_BUILD_GATED">Static scans passed. Build blocked by CPU gate, not by code evidence.</TASK>
  </TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION>
    <MesofaunaStateDTO size="64" alignment_goal="one_64_byte_cache_line">
      <field name="AUP_Position" offset="0" size="24" type="double3"/>
      <field name="Velocity" offset="24" size="12" type="float3"/>
      <field name="TargetHashID" offset="36" size="4" type="uint"/>
      <field name="CurrentState" offset="40" size="1" type="byte"/>
      <field name="PreviousState" offset="41" size="1" type="byte"/>
      <field name="StateTimerTicks" offset="42" size="2" type="ushort"/>
      <field name="AggressionScalar" offset="44" size="4" type="float"/>
      <field name="_pad0_to_pad15" offset="48" size="16" type="byte[16]"/>
      <math>24 + 12 + 4 + 1 + 1 + 2 + 4 + 16 = 64 bytes. 64 % 16 = 0. No Pack=1.</math>
    </MesofaunaStateDTO>
    <MesofaunaTelemetryEntry size="64" false_sharing="not_atomic_counter">
      <math>Frame/counts/timing/quality/hash fields occupy 0-31; ProbeAup double3 occupies 32-55; dump/flee/reserved occupy 56-63. 64 bytes total.</math>
    </MesofaunaTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, `Smooth01(q)` lowers vision radius toward 22m, slice modulo approaches 10, and only about one in ten predators performs expensive target/scent/SDF state refresh each frame. Non-sliced predators still run continuity output: velocity direction is normalized from previous state, speed is clamped, visual sync remains smooth. SDF probe distance lerps toward 1.75m; scent follow range lerps toward the cheap bound; interception lead horizon lerps toward 0.25s. At q=1, slice modulo is 1, radius approaches 104m, scent range/lead/probe distance expand, and the same visual DTO gives VAT/IK more state richness without expanding authoritative memory.
  </SCALABILITY_CURVE>

  <H_PHI_VAULT_STATUS private_persistent_native_allocations="0">
    <VaultBufferHandle owner_local_id="71180" name="MesofaunaStateDTOs" type="MesofaunaStateDTO" count="Capacity"/>
    <VaultBufferHandle owner_local_id="71181" name="MesofaunaMockPreyTargets" type="MesofaunaTargetDTO" count="Capacity"/>
    <VaultBufferHandle owner_local_id="71182" name="MesofaunaVisualSync" type="MesofaunaVisualSyncDTO" count="Capacity"/>
    <VaultBufferHandle owner_local_id="71183" name="MesofaunaTelemetryRing" type="MesofaunaTelemetryEntry" count="300"/>
    <VaultBufferHandle owner_local_id="71184" name="MesofaunaTuning" type="MesofaunaTuningDTO" count="1"/>
    <VaultBufferHandle owner_local_id="71185" name="MesofaunaTargetHashBucketHeads" type="int" count="1024"/>
    <VaultBufferHandle owner_local_id="71186" name="MesofaunaTargetHashNext" type="int" count="Capacity"/>
    <VaultBufferHandle owner_local_id="71187" name="MesofaunaSpeciesProfiles" type="MesofaunaSpeciesProfileDTO" count="64"/>
    <VaultBufferHandle owner_local_id="71188" name="MesofaunaSpeciesProfileCount" type="int" count="1"/>
    <VaultBufferHandle owner_local_id="71189" name="MesofaunaCsvScratch" type="byte" count="4096"/>
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>InitializeMesofaunaStateJob, GenerateMesofaunaMockTargetsJob, BuildMesofaunaTargetSpatialHashJob, and MesofaunaBehaviorJob annotate NativeArray fields with NoAlias where applicable.</NoAlias>
    <RawPointerMutation>MesofaunaBehaviorJob obtains `MesofaunaStateDTO*` from `States.GetUnsafePtr()` and mutates via `UnsafeUtility.AsRef`.</RawPointerMutation>
    <JobHandles>
      <edge from="default" to="mesofaunaHashHandle">Build flat target bucket heads/next.</edge>
      <edge from="default" to="mesofaunaMockHandle">Generate deterministic mock targets.</edge>
      <edge from="default" to="_scheduledSwarmHandle">Existing swarm analysis admitted through Lane3_AI.</edge>
      <edge from="_scheduledSwarmHandle" to="_scheduledEvaluationHandle">Existing predator cognition, if admitted.</edge>
      <edge from="_scheduledEvaluationHandle + mesofaunaMockHandle + mesofaunaHashHandle" to="mesofaunaHandle">Mesofauna FSM evaluation.</edge>
      <edge from="mesofaunaHandle" to="LateFrameTick telemetry">Telemetry written only after dispatcher completion.</edge>
    </JobHandles>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    Domain code remains inside existing `Hecton8.Core` fauna files and the editor facade inside existing `Hecton8.Editor`. SHINOBU_146 introduced no new asmdef and no new assembly reference. Caveat: the pre-existing root `Assets/_Project/Scripts/Hecton8.Core.asmdef` already directly references sibling Runtime assemblies including `Hecton8.AI.Cognition`, `Hecton8.Logistics`, and `Hecton8.Cartography`; this is recorded as existing compile-wall debt outside the safe fauna ownership boundary. Mesofauna Vault IDs are owner-local numeric casts inside `PredatorCognitionDomain`; no `BufferID.Mesofauna*` symbols remain in shared `H8Memory.cs`. Communication routes are existing GlobalRegistry/Vault, SignalBus, and borrowed published snapshots.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    Heavy CPU animation was replaced by scalar visual intent. Before: O(n * AnimatorGraphBlendCost + state behaviour dispatch) with managed engine participation. After: O(n) writes of state byte, speed scalar, scent signal, and obstacle pressure. Shaders/VAT/IK can synthesize Hunt frantic waves, Idle glides, Flee pulses, and scent-tracking undulation from the DTO. Obstacle navigation likewise rejects raycast/path-node realism for an SDF gradient fake: before O(n * raycast/path query), after O(n * constant voxel samples) on evaluated slices.
  </DEAR_LIE_CONFIRMATION>

  <STATIC_VERIFICATION>
    <check name="NavMesh scan" result="PASS">No first-party hits.</check>
    <check name="Forbidden hot-path syntax" result="PASS">No `foreach`, LINQ marker, `UnityEngine.Random`, `Pack=1`, or hot auto-properties in new SHINOBU files.</check>
    <check name="Burst/NoAlias scan" result="PASS">All new jobs carry Burst deterministic directives and NoAlias annotations.</check>
    <check name="Compile-wall scan" result="PASS">No `BufferID.Mesofauna*` references and no mesofauna enum lines remain in `H8Memory.cs`.</check>
    <check name="Vault lifecycle scan" result="PASS">Species profile, species count, and CSV scratch lanes are included in initialized-check, failure release, and dispose reset.</check>
    <check name="Chemical breadcrumb contract scan" result="PASS">No `AbsolutePositionDouble` field dependency remains in SHINOBU_146 code.</check>
    <check name="Diff whitespace" result="PASS_WITH_LINE_ENDING_WARNINGS">`git diff --check` reports only LF->CRLF warnings from repo settings.</check>
    <check name="Build" result="FAILED_EXTERNAL_COMPILE_WALL">CS2001 missing tracked files outside SHINOBU_146 ownership: `World/ChemicalInfluenceGrid.cs`, `Construction/LogisticsPipeEvents.cs`.</check>
  </STATIC_VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 Polish Mandate Re-Audit

What was wrong: Direct prey/player `TargetHashID` and scent breadcrumb target hashes were derived from runtime float positions even though steering used AUP-local deltas. That left a deterministic identity crack at floating-origin shifts. Obstacle avoidance also used occupancy-shaped pressure instead of the explicit Task 09 reciprocal distance term. Mesofauna output preserved `RetinalBlind` but could clear `EcoHeadless`.

What was done: `TryResolveDirectTarget` now hashes the AUP-local `toTarget` vector after subtracting predator AUP. `TryAcquireScent` now hashes the AUP-local breadcrumb delta. `TryResolveObstacleRepulsion` computes guarded approximate `sdfDistance` and uses `math.rcp(math.max(0.1f, sdfDistance))` to drive pressure. `WriteVisualAndOutput` preserves `RetinalBlind | EcoHeadless` while clearing stale behavior flags.

Cinematic cheats used: no raycast/path node was introduced; SDF/voxel gradient remains the fake navigation authority. Visual animation remains VAT/IK scalar intent through `MesofaunaVisualSyncDTO`; no Animator route was added.

Exact microseconds saved, static estimates only: no new savings claimed. This pass is determinism and contract hardening. Runtime cost is one extra bitmask and a few scalar ALU operations on evaluated SDF slices. Build was not rerun because the last compile wall is still external CS2001 missing files in World/Construction domains.

Static verification:
- Prompt re-extraction: PASS via PowerShell line-range capture of `SHINOBU_146` block.
- Architecture preflight: PASS, binary/global/systems ledgers re-read before edits.
- Runtime target-hash scan: PASS, no `HashFloat3(targetPosition)` or `HashFloat3(waypoint.RuntimePosition)` remains in SHINOBU_146 code.
- Output flag scan: PASS, mesofauna output preserves `RetinalBlind | EcoHeadless`.
- Asmdef scan: PASS for no new SHINOBU asmdef/reference; FAIL_PREEXISTING for root `Hecton8.Core.asmdef` direct sibling Runtime references outside SHINOBU_146 ownership.
- Editor facade route: PASS. `Hecton8.Editor.csproj` includes `MesofaunaAiTunerWindow.cs`; `Hecton8.Core.csproj` includes `MesofaunaFsmDebugGizmo.cs` behind `#if UNITY_EDITOR`; `AssemblyInfo.cs` has `InternalsVisibleTo("Hecton8.Editor")`, so hot DTOs can stay internal.
- Forbidden pattern scan: PASS for `Pack=1`, `UnityEngine.Random`, `Time.deltaTime`, `NavMeshAgent`, `UnityEngine.AI` in owned SHINOBU_146 surface.
- Whitespace scan: PASS_WITH_REPO_LINE_ENDING_WARNING; `git diff --check` reports only LF->CRLF warning for the edited file.

## 2026-05-19 Dependency And Target Identity Re-Audit

What was wrong: Mesofauna helper jobs were scheduled before the swarm admission gate, forcing a frame-lane `Complete()` on admission failure. Direct target selection also had a mixed route: prey could be selected while player AUP/hash was used if both target flags were present. CSV profile reload could clear rows while leaving a stale nonzero count after malformed input.

What was done: Moved mesofauna hash/mock scheduling after swarm admission succeeds. Added `selectedPlayer` to bind selected target position, AUP route, and hash salt to the same fact. Reset species profile count before parser mutation so malformed CSV fails closed.

Cinematic cheats used: unchanged; the CPU still emits intent and scalar state only. No pathfinding, Animator, raycast, or managed physics broadphase was introduced.

Exact microseconds saved, static estimates only: admission-failure frames avoid a forced wait for one mesofauna hash clear/build and mock target pass. Normal admitted-frame cost is unchanged. Target identity repair is one bool; CSV repair is cold-only.

Static verification:
- Job completion scan: PASS. Remaining `.Complete()` calls are disposal and cold initialization only.
- Target identity scan: PASS. Direct target AUP/hash now keys off `selectedPlayer`, not broad `hasPlayer`.
- CSV fail-closed scan: PASS. `_mesofaunaSpeciesProfileCount[0]` resets before `ParseMesofaunaSpeciesProfilesCsv(...)` mutates rows.
- Build: not rerun. External CS2001 missing tracked `World/ChemicalInfluenceGrid.cs` and `Construction/LogisticsPipeEvents.cs` remains the first compile wall.

## 2026-05-19 Blackbox Target AUP Re-Audit

What was wrong: `MesofaunaTelemetryEntry.TargetHash` was valid, but `ProbeAup` was fed from predator self-position. That did not satisfy the blackbox intent for current target AUP during hunt/flee/scent autopsy.

What was done: `MesofaunaVisualSyncDTO` was widened from 32 to 64 bytes. New layout: 0 `DesiredVelocity` float3, 12 `SpeedScalar`, 16 `CurrentState`, 17 `PreviousState`, 18 `Flags`, 20 `TargetHashID`, 24 `ScentSignal01`, 28 `ObstaclePressure01`, 32 `TargetAup` double3, 56 `TargetDistanceMeters`, 60 `TargetFlags`. Hunt/flee/scent writes validated target AUP; direct/spatial/scent acquisition passes the source `double3 targetAup` to interception math and the writer instead of rehydrating it from a runtime float; continuity preserves previous finite target AUP; telemetry writes `ProbeAup` from target AUP when `TargetFlags & 1`.

Cinematic cheats used: no physics probe or GameObject target tracker added. Scent now returns selected breadcrumb AUP-local target position from the existing chemical grid, and gizmos draw this target vector directly from visual sync.

Exact Microseconds saved: avoided a new target-AUP Vault lane and another copy job; estimated 3-7 us/frame saved at 256 active slots versus a separate post-pass. Cost accepted: +32 bytes/slot in visual sync and one same-slot read on continuity frames.

Verification: static scans found no remaining old `TryAcquireScent` call signature, no `VisualSyncDtoSizeBytes = 32`, no `ResolveInterceptDirection(... targetPosition ...)`, and no `ProbeAup = state.AUP_Position`. No new build attempt was made while external missing tracked files still block `Hecton8.Core.csproj`.

## 2026-05-19 Telemetry Budget Dump Re-Audit

What was wrong: Task 16 requires `Dump_MESOFAUNA_DIRECTOR` on NaN or >1.0 ms FSM budget. The previous path dumped only on non-finite state/visual faults.

What was done: Added `DumpReasonOverBudgetHash` and an `overBudget = _mesofaunaLastChainMicroseconds > 1000f` check in post-evaluation telemetry. Ring `Flags` now uses bit 1 for fault and bit 2 for budget breach. Budget breach emits `.bin` and `.h8dump` without resetting predator state.

Cinematic Cheats used: unchanged; this is blackbox rigor, not simulation work.

Exact Microseconds saved: none claimed. Added cost is one post-evaluation float compare and two bit writes; hot Burst jobs unchanged.

Verification: static scan confirms `DumpReasonOverBudgetHash`, `_mesofaunaLastChainMicroseconds > 1000f`, telemetry flag bit, and `DumpMesofaunaBlackBoxCold(frameId)` are wired. Build still not rerun because external CS2001 files are absent.

## 2026-05-19 Flag And Spatial Hash Contract Re-Audit

What was wrong: SHINOBU-owned code still had literal target/telemetry flag bits (`1u`, `1`, `2`) and one hard-coded mesofauna spatial hash query cell size (`8f`). They were not behavioral bugs in the current constants, but they were brittle forensic semantics and a future builder/searcher drift point.

What was done: Added named constants in `MesofaunaBehaviorConstants` for hunt visual flag, valid target-AUP flag, telemetry fault flag, and telemetry over-budget flag. Replaced visual sync, debug gizmo, and telemetry consumers with those constants. Added `TargetHashCellSizeMeters` to `MesofaunaBehaviorJob` and scheduled it from `SwarmBucketCellSize`, matching `BuildMesofaunaTargetSpatialHashJob.CellSizeMeters`.

Cinematic Cheats used: unchanged. No physics/pathfinding/Animator route was introduced.

Exact Microseconds saved: none claimed. Runtime cost is effectively 0 us after constant propagation; the job cell-size field is one scalar copied at schedule time. The benefit is deterministic maintainability and fewer silent target-acquisition failures after bucket tuning.

Verification:
- Flag magic scan: PASS. No `TargetFlags & 1u`, `telemetryFlags |= 1`, or `telemetryFlags |= 2` remains in SHINOBU-owned code.
- Spatial hash literal scan: PASS. No `ResolveBucket(input.Position, SwarmBoundsMin, 8f)` remains.
- Build: not rerun. Existing external CS2001 compile wall still precedes SHINOBU code analysis.

## 2026-05-19 Target DTO Spatial Hash Authority Re-Audit

What was wrong: The mesofauna target spatial hash inserted buckets from `input.Position`, but target scoring read `MockTargets[candidate].AUP_Position`. Those are not necessarily the same location once the mock/prey/player target DTO is offset from the owning slot.

What was done: `BuildMesofaunaTargetSpatialHashJob` now receives `MockTargets`, requires `TargetFlagValid`, converts the DTO `AUP_Position` to runtime-local coordinates with the slot's floating origin, and inserts the bucket for that target position. Scheduler order now runs `GenerateMesofaunaMockTargetsJob` before the hash build. `TargetFlagValid` is separate from `VisualTargetFlagValid`.

Cinematic Cheats used: unchanged. The target hash is still flat arrays and deterministic math; no physics broadphase or managed target registry was introduced.

Exact Microseconds saved: no direct per-frame saving claimed. The added cost is one 64B target DTO read per active slot in the hash builder and one job dependency edge. The avoided cost is repeated failed adjacent-bucket target acquisition after target offsets, which would otherwise spill predators into search/scent fallback frames.

Verification:
- Builder/searcher authority scan: PASS. `BuildMesofaunaTargetSpatialHashJob` now reads `MockTargets`.
- Dependency scan: PASS. `mesofaunaHashJob.Schedule(mesofaunaMockHandle)` fences target DTO generation before bucket insertion.
- Target flag scan: PASS. `MesofaunaTargetDTO.Flags` uses `TargetFlagValid`; visual target AUP uses `VisualTargetFlagValid`.
- Build: not rerun. CPU sampled at 100 and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is still absent on disk.

## 2026-05-19 Damage Flee And AUP Target Packet Re-Audit

What was wrong: Damage pre-simulation routing wrote `StateFlee`, `TargetHashID`, and `Control.OverrideThreatPosition`, but the mesofauna FSM did not read `CognitionControl`. That meant the byte state could flee while the steering vector was not guaranteed to point away from the damage source. Direct prey/mock target AUP also reconstructed from runtime+origin even though `PackTargetAup` exists.

What was done: `MesofaunaBehaviorJob` now receives `[ReadOnly, NoAlias] NativeArray<CognitionControl> Controls`. `ResolveThreatPosition()` first checks `HasOverrideThreatPosition` and `OverrideUntilTime > CurrentTime`, then falls back to threat/player/backward. Ongoing `StateFlee` preserves the nonzero `state.TargetHashID` written from `CombatDamageSignal.SourceHash`. Direct prey and mock prey target AUP now use `PackTargetAup` with runtime fallback; player paths use `PlayerTargetAup`.

Cinematic Cheats used: unchanged. No physics query, NavMesh, or per-creature managed callback was added; the damage vector is read from the existing Vault control lane.

Exact Microseconds saved: none claimed. Cost is one job field and one read-only control fetch on flee threat resolution. Avoided cost is repeated bad flee correction after damage because the vector now comes from the owner-local control override.

Verification:
- Control route scan: PASS. `Controls = _controls` is scheduled for `MesofaunaBehaviorJob`, and the job field is `[ReadOnly, NoAlias]`.
- Flee hash scan: PASS. `ResolveFleeTargetHash()` preserves nonzero damage source target hash.
- AUP packet scan: PASS. `PackTargetAup` and `PlayerTargetAup` are used in direct/mock target AUP resolution.
- Forbidden pattern scan: PASS. No NavMesh/UnityEngine.Random/Time.deltaTime/Pack=1 in SHINOBU-owned files.
- Build: not rerun. CPU/external missing-file gate remains blocked.

## 2026-05-19 Deterministic Mock RNG Re-Audit

What was wrong: Fallback mock target motion was deterministic, but it used hash/trig variation rather than an explicit `Unity.Mathematics.Random` seeded from sector and frame truth. That was acceptable behaviorally but weak against the deterministic RNG mandate.

What was done: Added local RNG creation inside `GenerateMesofaunaMockTargetsJob`: seed = AUP-derived 256m sector hash ^ `FrameId` ^ stable slot/species salt, then avalanche-mixed and clamped nonzero. The RNG adds small bounded jitter to angle/radius/vertical offset while preserving the continuous orbit phase.

Cinematic Cheats used: unchanged. Mock prey remains a mathematical profiler target, not a spawned GameObject or physics body.

Exact Microseconds saved: none. Cost is three `NextFloat` calls only on fallback mock-target slots. Avoided architecture cost is importing Networking RNG helpers and creating a sibling assembly route.

Verification:
- RNG route scan: PASS. SHINOBU-owned mock code uses `Unity.Mathematics.Random`; no `UnityEngine.Random`.
- Compile wall scan: PASS. No new dependency on Networking/Rollback assemblies.
- Smoothness guard: PASS. RNG only jitters small offsets around the existing orbit phase.

## 2026-05-19 Layout And Scheduler Compile Guard Re-Audit

What was wrong: Mesofauna scheduler code still assigned old chemical-grid fields into `MesofaunaBehaviorJob`, but the job struct had been reduced to the breadcrumb chemical contract. That would create a domain compile error once the external missing-file wall is removed. Layout validation also did not fully assert every widened visual/telemetry DTO offset.

What was done: Removed stale `ChemicalFrontGrid`, `ChemicalOverlayGrid`, grid dimensions, grid origin, and grid cell-size assignments from the mesofauna job initializer. Kept `Controls` only on `MesofaunaBehaviorJob` for damage-source flee override routing and not on `BuildMesofaunaTargetSpatialHashJob`. Expanded `MesofaunaBehaviorConstants.ValidateLayout()` to assert target DTO, full visual sync DTO, full telemetry DTO, tuning DTO, and species profile padding offsets.

Cinematic Cheats used: unchanged. Scent remains a breadcrumb/AUP math read; no full volumetric chemical raymarch, physics trigger volume, or managed GameObject scent emitter was introduced.

Exact Microseconds saved: no frame-time saving claimed. Hash-builder job ABI is one unused NativeArray field smaller. Cold layout validation costs 0 us during gameplay.

Verification:
- Stale initializer scan: PASS. No mesofauna `ChemicalFrontGrid = _chemicalFrontGrid` / overlay / dimensions / origin / cell-size assignments remain.
- Control route scan: PASS. `MesofaunaBehaviorJob` has `[ReadOnly, NoAlias] NativeArray<CognitionControl> Controls`; `ResolveThreatPosition()` reads it.
- Layout proof scan: PASS. `ValidateLayout()` now checks offset maps for target, visual, telemetry, tuning, and species DTOs.
- Forbidden pattern scan: PASS. No NavMesh/UnityEngine.Random/Time.deltaTime/Pack=1/LINQ/foreach in SHINOBU-owned files.
- Build: not run. `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` remains tracked but absent, and CPU sampled at `100/100`.

## 2026-05-19 CSV Fail-Closed Reload Re-Audit

What was wrong: `TryLoadMesofaunaSpeciesProfilesCsvCold()` cleared the species count only after a file path was found and bytes were read. A missing/empty/corrupt CSV after a previous successful load could leave old species multipliers alive while reporting reload failure.

What was done: Moved profile table clearing before path resolution and file IO. The count lane is set to zero and the fixed `MesofaunaSpeciesProfileDTO[64]` Vault table is cleared before any early return that can be caused by file absence or invalid bytes.

Cinematic Cheats used: unchanged. Species tuning remains scalar multipliers over the same byte FSM; no per-species behavior objects or managed state classes were introduced.

Exact Microseconds saved: none claimed. Cold reload clears 64 DTOs. Gameplay hot path remains 0 B / 0 us.

Verification:
- Reload order scan: PASS. `_mesofaunaSpeciesProfileCount[0] = 0` now precedes `ResolveMesofaunaSpeciesProfilesPathCold()` and `ReadMesofaunaSpeciesProfilesFileCold()`.
- Parser forbidden scan: PASS. No `string.Split`, LINQ, `foreach`, `ToArray`, or `ToList` in SHINOBU-owned CSV/editor path.
- Build: not run. External `Construction/LogisticsPipeEvents.cs` CS2001 and CPU gate remain blockers.

## 2026-05-19 Damage Override Stale-Vector Re-Audit

What was wrong: `ProcessMesofaunaDamageSignals()` could extend `OverrideUntilTime` for a new damage signal without a valid runtime point while leaving an older `HasOverrideThreatPosition` vector alive.

What was done: The damage route now clears `HasOverrideThreatPosition` and resets `OverrideThreatPosition` before decoding the new signal point. Only a fresh finite decoded point sets the flag. Missing point falls back to the input threat/player/backward chain inside the Burst FSM.

Cinematic Cheats used: unchanged. Flee routing remains a single control vector plus byte state; no per-attacker GameObject tracking or physics query was added.

Exact Microseconds saved: none claimed. Damage-frame cost is one flag clear and one `float3` reset per matched predator. Steady hot path unchanged.

Verification:
- Control override scan: PASS. Clear occurs before `CombatDamageSignalCodec.TryToRuntimePoint()`.
- Burst consumer scan: PASS. `ResolveThreatPosition()` requires the flag and finite vector before using the override.
- Prompt re-extraction: PASS. `SHINOBU_146` XML block read again from `Docs/Tasks/CURRENT_BATCH.md`.
- Build: not run. External missing-file wall and CPU gate remain.

<SELF_AUDIT revision="2026-05-19_loop17" agent_id="SHINOBU_146" domain="MESOFAUNA_BEHAVIORAL_STATE_MACHINE">
  <TASK_RECONCILIATION>
    <TASK id="01" result="PASS">OOP state classes and virtual `UpdateState` were not used. Mesofauna authority is byte `CurrentState` plus Burst `switch`.</TASK>
    <TASK id="02" result="PASS">No `NavMeshAgent` route added; static scan has no first-party underwater creature NavMesh hit. Movement uses steering, SDF pressure, and kinematic output.</TASK>
    <TASK id="03" result="PASS">Hot DTO fields are public fields. `MesofaunaStateDTO.AsMutableRef(void*)` uses `UnsafeUtility.AsRef` for raw state mutation.</TASK>
    <TASK id="04" result="PASS">`MesofaunaStateDTO` is explicit 64B; `ValidateLayout()` asserts primary and companion DTO offsets.</TASK>
    <TASK id="05" result="PASS">`GenerateMesofaunaMockTargetsJob` writes deterministic Vault-backed `MesofaunaTargetDTO` targets with explicit sector/frame RNG fallback.</TASK>
    <TASK id="06" result="PASS">`MesofaunaBehaviorJob` is deterministic Burst `IJobParallelFor`, no managed state objects, no virtual dispatch.</TASK>
    <TASK id="07" result="PASS_WITH_ARCHITECTURE_SUBSTITUTION">Target acquisition uses Vault-owned flat bucket heads/next arrays. Private persistent `NativeHashMap` was rejected under H-PHI; the algorithm remains spatial hash based.</TASK>
    <TASK id="08" result="PASS">Visual state is emitted as `MesofaunaVisualSyncDTO` state/speed/scalars for VAT/IK/shader consumers. No Animator parameter path.</TASK>
    <TASK id="09" result="PASS">SDF obstacle pressure uses guarded reciprocal distance and normal approximation. No raycast or path node.</TASK>
    <TASK id="10" result="PASS">`GlobalQualityWeight` drives smooth search radius and slice modulo 10-to-1; continuity frames preserve movement.</TASK>
    <TASK id="11" result="PASS">Scent tracking samples published chemical breadcrumbs, converts runtime point to AUP, and steers by weighted gradient.</TASK>
    <TASK id="12" result="PASS">Direct, mock, spatial, scent, and intercept paths subtract predator AUP from target AUP before casting to `float3`.</TASK>
    <TASK id="13" result="PASS">`CombatDamageSignal` is consumed pre-sim, writes `StateFlee`, source hash, due flag, and fresh control override when present.</TASK>
    <TASK id="14" result="PASS">All SHINOBU mesofauna jobs use `FloatMode.Deterministic`; DTOs are blittable fixed-size payloads.</TASK>
    <TASK id="15" result="PASS">Primary mesofauna state/mock/hash/scratch buffers are requested with `UninitializedMemory` and cold overwritten by Burst init.</TASK>
    <TASK id="16" result="PASS">300-entry telemetry ring records counts, hashes, quality, budget estimates, target AUP, and dumps on fault or >1.0 ms budget.</TASK>
    <TASK id="17" result="PASS">UI Toolkit tuner reads/writes Vault tuning DTO and draws a pie chart from telemetry. Editor allocations are cold/editor-only.</TASK>
    <TASK id="18" result="PASS_WITH_ARCHITECTURE_SUBSTITUTION">CSV parser uses Vault scratch + `ReadOnlySpan<byte>` + FNV/open addressing; missing/corrupt reload fails closed. Flat Vault table replaces private `NativeHashMap`.</TASK>
    <TASK id="19" result="PASS">Editor gizmo draws state/velocity/target vectors from copied mesofauna state and visual sync data.</TASK>
    <TASK id="20" result="PASS_STATIC">Static scans verify no forbidden hot-path GC patterns, no stale bucket/flag literals, and guarded empty buckets. Runtime GC/profiler proof awaits Unity/build unblock.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION evidence="STATIC_SOURCE_AND_VALIDATE_LAYOUT">
    <STRUCT name="MesofaunaStateDTO" size="64" proof="64 % 16 == 0">
      <FIELD name="AUP_Position" offset="0" size="24"/>
      <FIELD name="Velocity" offset="24" size="12"/>
      <FIELD name="TargetHashID" offset="36" size="4"/>
      <FIELD name="CurrentState" offset="40" size="1"/>
      <FIELD name="PreviousState" offset="41" size="1"/>
      <FIELD name="StateTimerTicks" offset="42" size="2"/>
      <FIELD name="AggressionScalar" offset="44" size="4"/>
      <FIELD name="_pad0.._pad15" offset="48" size="16"/>
    </STRUCT>
    <STRUCT name="MesofaunaTargetDTO" size="64" proof="64 % 16 == 0">
      <FIELD name="AUP_Position" offset="0" size="24"/>
      <FIELD name="Velocity" offset="24" size="12"/>
      <FIELD name="TargetHashID" offset="36" size="4"/>
      <FIELD name="SpeciesHash" offset="40" size="4"/>
      <FIELD name="Flags/ThreatClass/Reserved0" offset="44" size="4"/>
      <FIELD name="RadiusMeters" offset="48" size="4"/>
      <FIELD name="Reserved1" offset="52" size="4"/>
      <FIELD name="Reserved2" offset="56" size="8"/>
    </STRUCT>
    <STRUCT name="MesofaunaVisualSyncDTO" size="64" proof="64 % 16 == 0">
      <FIELD name="DesiredVelocity" offset="0" size="12"/>
      <FIELD name="SpeedScalar" offset="12" size="4"/>
      <FIELD name="CurrentState/PreviousState/Flags" offset="16" size="4"/>
      <FIELD name="TargetHashID" offset="20" size="4"/>
      <FIELD name="ScentSignal01" offset="24" size="4"/>
      <FIELD name="ObstaclePressure01" offset="28" size="4"/>
      <FIELD name="TargetAup" offset="32" size="24"/>
      <FIELD name="TargetDistanceMeters" offset="56" size="4"/>
      <FIELD name="TargetFlags" offset="60" size="4"/>
    </STRUCT>
    <STRUCT name="MesofaunaTelemetryEntry" size="64" proof="64 % 16 == 0">
      <FIELD name="Frame" offset="0" size="4"/>
      <FIELD name="ActivePredators/HuntingPredators" offset="4" size="4"/>
      <FIELD name="AvgSpatialHashQueryMicroseconds" offset="8" size="4"/>
      <FIELD name="FsmMicroseconds" offset="12" size="4"/>
      <FIELD name="GlobalQualityWeight" offset="16" size="4"/>
      <FIELD name="SliceModulo/Flags/NonFiniteFallbackCount" offset="20" size="4"/>
      <FIELD name="StateHash" offset="24" size="4"/>
      <FIELD name="TargetHash" offset="28" size="4"/>
      <FIELD name="ProbeAup" offset="32" size="24"/>
      <FIELD name="DumpReasonHash" offset="56" size="4"/>
      <FIELD name="FleeingPredators/Reserved0" offset="60" size="4"/>
    </STRUCT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight is smoothed by `Smooth01`. At low pressure weights, vision radius lerps toward 22m and `SliceModulo` rises toward 10, so roughly 10 percent of predators perform acquisition per frame while continuity writes preserve velocity. SDF probe distance, scent follow range, speed scalar, and intercept lead all lerp downward without binary low/high switches. At high weights, slice modulo reaches 1, vision approaches 104m before species multipliers, intercept lead grows, and visual sync updates every frame for richer VAT/IK behavior.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_persistent_native_allocations="0">
    <BUFFER id="71180" name="MesofaunaStateDTOs"/>
    <BUFFER id="71181" name="MesofaunaMockPreyTargets"/>
    <BUFFER id="71182" name="MesofaunaVisualSync"/>
    <BUFFER id="71183" name="MesofaunaTelemetryRing"/>
    <BUFFER id="71184" name="MesofaunaTuning"/>
    <BUFFER id="71185" name="MesofaunaTargetHashBucketHeads"/>
    <BUFFER id="71186" name="MesofaunaTargetHashNext"/>
    <BUFFER id="71187" name="MesofaunaSpeciesProfiles"/>
    <BUFFER id="71188" name="MesofaunaSpeciesProfileCount"/>
    <BUFFER id="71189" name="MesofaunaCsvScratch"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>All SHINOBU mesofauna job NativeArray fields are annotated `[NoAlias]`; slot-addressed outputs also use `NativeDisableParallelForRestriction` with unique ActiveSlots invariant.</NO_ALIAS>
    <DEPENDENCY>Swarm admission -> PredatorCognitionJob; GenerateMesofaunaMockTargetsJob -> BuildMesofaunaTargetSpatialHashJob; MesofaunaBehaviorJob consumes Combine(PredatorCognitionJob, hashHandle). No helper job `Complete()` remains in the frame lane.</DEPENDENCY>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_146 added no new asmdef and no new sibling runtime reference. The existing root `Hecton8.Core.asmdef` still has pre-existing direct sibling references outside this task boundary. Current build is intentionally not run because `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is tracked but absent and CPU gate sampled at 100/100.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Animation is not simulated through Animator graphs. CPU emits byte state, speed, scent, obstacle pressure, desired velocity, and target AUP; VAT/IK/shader consumers fake swim amplitude/frequency and visual intensity. Before: O(n * AnimatorGraph/StateMachineBehaviour cost) plus managed parameter traffic. After: O(n) Burst scalar writes, with acquisition itself time-sliced by quality.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
