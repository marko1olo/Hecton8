# SHINOBU_157 Status

Agent: SHINOBU_157
Domain: Echelon 6 Habitat & Vehicles / Autonomous Submarine Navigation
Primary directive: Burst ray-feeler autopilot over Voxel SDF, no NavMesh, current compensation, AUP-safe.

## Loop 0 - Rehydrate
- [x] Extracted SHINOBU_157 block from CURRENT_BATCH.md before implementation.
  - DOD practice: CLI extraction by agent id, neighboring prompts ignored.
  - Rejected: relying on chat memory after context compaction.
  - Hot-path estimate: 0 us, documentation-only.
- [x] Read relevant mandates: CORE_Submarine_Vehicles_Kinematics_AUP, VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline, CORE_Weather_Abyssal_FlowField_Currents, DATA_Runtime_Struct_Layout_ARM64, MATH_AUP_Determinism_Sync, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, DBG_Telemetry_Crash_Reporting_PostMortem.
  - DOD practice: mandate-bound design, not Unity-default navigation.
  - Rejected: NavMesh, Physics.Raycast/SphereCast, managed waypoint objects.
  - Hot-path estimate: 0 us, planning-only.

## Task Checklist
- [x] 1. NavMesh eradication audit.
- [x] 2. Physics SphereCast/Raycast purge for submarine autopilot.
- [x] 3. DTO property purge and raw-field layout.
- [x] 4. ARM64 layout validation hooks.
- [x] 5. Mock SDF generation job.
- [x] 6. EvaluateCollisionAvoidanceJob.
- [x] 7. ComputeDesiredVelocityJob.
- [x] 8. DataVault handoff for desired velocity.
- [x] 9. Async waypoint routing buffer.
- [x] 10. Continuous GlobalQualityWeight feeler count.
- [x] 11. Abyssal flow compensation.
- [x] 12. AUP precision path.
- [x] 13. Deterministic netcode fence.
- [x] 14. Uninitialized DataVault buffers plus init job.
- [x] 15. 300-frame telemetry and binary dump trigger.
- [x] 16. Editor tuner.
- [x] 17. Allocation-free CSV parser.
- [x] 18. Gizmo feeler visualization.
- [x] 19. Dynamic waypoint injection.
- [x] 20. Self-audit and log.

## Compile Attempts
- Blocked 2026-05-19: build not launched because 7 dotnet processes were active and CPU sampled at 98.1%, violating the batch CPU/build guard.
- Blocked 2026-05-19 polish pass: build not launched because CPU sampled at 100% then 97.3037%; `dotnet`/`csc` were absent, but CPU guard alone forbids compile.
- Blocked 2026-05-19 final guard check: build still not launched because CPU sampled at 100% and 100%; `dotnet`/`csc` were absent.
- Attempted 2026-05-19 after guard opened: CPU 14.42%/9.73%, no `dotnet`/`csc`; `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed before SHINOBU_157 on missing unrelated source files `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.

## Loop 1 - Tasks 1-5
- [x] Archaeology scan found no submarine NavMeshAgent/NavMeshPath/CalculatePath dependency in Echelon 6 submarine/autopilot files.
  - DOD practice: targeted rg scan over `Physics/Vehicles` and `Gameplay` submarine/autopilot files.
  - Rejected: deleting unrelated AI pathfinding systems outside the assigned domain.
  - Hot-path estimate: 0 us, no runtime dependency.
- [x] Submarine autopilot spatial awareness uses SDF math only; no `Physics.Raycast` or `Physics.SphereCast` was introduced.
  - DOD practice: static scan against new runtime/editor files.
  - Rejected: Unity collider sweeps.
  - Hot-path estimate: removes engine-cast stalls; SDF low tier is about 16 vehicles * 5 feelers * 4 steps.
- [x] DTOs are explicit unmanaged structs with raw public fields.
  - DOD practice: `StructLayout(Explicit)` and pointer/ref access through `UnsafeUtility.AsRef`.
  - Rejected: C# properties and managed route node classes.
  - Hot-path estimate: contiguous cache-line stream, 0 B GC.
- [x] AutopilotStateDTO layout validator added.
  - DOD practice: `UnsafeUtility.SizeOf` plus `UnsafeUtility.GetFieldOffset`.
  - Rejected: trusting CLR default packing.
  - Hot-path estimate: editor/static validation only.
- [x] Mock SDF and flow generation jobs added.
  - DOD practice: Burst deterministic `IJobParallelFor`, byte encoded SDF in Vault.
  - Rejected: managed test arrays.
  - Hot-path estimate: cold boot only; no per-frame generation.

## Loop 2 - Tasks 6-10
- [x] `EvaluateCollisionAvoidanceJob` added.
  - DOD practice: Burst deterministic fan feelers, trilinear byte SDF sampling, normal-derived repulsion.
  - Rejected: node search, NavMesh, collider queries.
  - Hot-path estimate: low 320 SDF samples/frame for 16 subs before gradients; ultra 5120 base samples.
- [x] `ComputeDesiredVelocityJob` added.
  - DOD practice: attraction + repulsion potential field, speed clamp, turn-rate clamp.
  - Rejected: directly modifying Transform/Rigidbody.
  - Hot-path estimate: O(vehicle count), no managed allocation.
- [x] Dear Lie handoff implemented through `AutopilotStateDTO.DesiredVelocity`.
  - DOD practice: Vault state only; physics owner remains separate.
  - Rejected: movement authority takeover.
  - Hot-path estimate: one 64-byte DTO write per vehicle.
- [x] Route range and waypoint DTO buffers added.
  - DOD practice: job-side route cursor advancement by AUP distance.
  - Rejected: managed waypoint lists.
  - Hot-path estimate: one waypoint read per active submarine.
- [x] Feeler count uses continuous `math.lerp(5, 32, GlobalQualityWeight)`.
  - DOD practice: no binary hardware branch.
  - Rejected: low/high switches.
  - Hot-path estimate: quality-proportional SDF sample count.

## Loop 3 - Tasks 11-15
- [x] Flow compensation implemented.
  - DOD practice: Vault-backed flow grid with deterministic analytic fallback.
  - Rejected: main-thread weather/fluid sampler dependency.
  - Hot-path estimate: bounded trilinear float3 read or analytic fallback.
- [x] AUP delta math implemented.
  - DOD practice: subtract target `double3` and submarine `double3` before cast to `float3`.
  - Rejected: absolute world float steering.
  - Hot-path estimate: negligible; prevents far-origin drift.
- [x] Deterministic netcode fence established.
  - DOD practice: Burst `FloatMode.Deterministic`, 64-byte DTO layout.
  - Rejected: property-backed DTOs.
  - Hot-path estimate: no allocator or virtual calls.
- [x] Uninitialized Vault buffers plus init job implemented.
  - DOD practice: autopilot-owned buffers request `NativeArrayOptions.UninitializedMemory`; Burst init fills state.
  - Rejected: OS zero-fill for every cold buffer.
  - Hot-path estimate: cold boot only.
- [x] Telemetry black box implemented.
  - DOD practice: 300-entry fixed ring, fatal/slow flags, binary dump path.
  - Rejected: logging strings in hot path.
  - Hot-path estimate: one 64-byte telemetry write per frame.

## Loop 4 - Tasks 16-19
- [x] UI Toolkit tuner added.
  - DOD practice: editor-only window writes Vault tuning DTO.
  - Rejected: runtime inspector polling.
  - Hot-path estimate: 0 us runtime, editor-only.
- [x] Allocation-free CSV parser added for `vehicle_handling_profiles.csv`.
  - DOD practice: cold byte parser, FNV-1a hashes, fixed Vault profile table.
  - Rejected: `string.Split`.
  - Hot-path estimate: 0 us hot path.
- [x] Live feeler gizmo added.
  - DOD practice: reads fixed feeler result buffer and draws line/dot/vector.
  - Rejected: allocating debug line lists.
  - Hot-path estimate: editor gizmo only.
- [x] Dynamic waypoint injection added.
  - DOD practice: Scene View ray to plane math, no Physics raycast, writes target AUP.
  - Rejected: mission-graph dependency for testing.
  - Hot-path estimate: editor-only.

## Loop 5 - Task 20
- [x] Self-audit and final log written to `Docs/AgentLogs/LOG_SHINOBU_157.md`.
  - DOD practice: explicit byte layout, Vault buffer IDs, no-NavMesh/no-physics-cast scan, AUP delta proof, black-box path.
  - Rejected: chat-only reporting.
  - Hot-path estimate: audit/report only; 0 us runtime.

## Loop 6 - Ultra Think Polish
- [x] Removed SHINOBU_157 global `BufferID` enum dependency.
  - DOD practice: owner-local `SubmarineAutopilotVaultRoute` IDs 71592-71603; route card updated.
  - Rejected: widening `H8Memory.cs` global enum for a domain-local route.
  - Hot-path estimate: 0 us; compile-wall risk reduction, no runtime math change.
- [x] Added `[NoAlias]` to Burst job pointer fields.
  - DOD practice: explicit alias contract on distinct Vault buffers and kinematic input pointer.
  - Rejected: leaving Burst to assume pointer overlap and disable vectorization.
  - Hot-path estimate: expected SIMD/vectorization recovery; exact us PENDING profiler proof.
- [x] Added quality-curved solver cadence and low-tier SDF collapse.
  - DOD practice: `GlobalQualityWeight` drives cadence 12->1 frames, steps 1->12, nearest/trilinear blend, gradient gate.
  - Rejected: fixed 4-step trilinear probing at low quality.
  - Hot-path estimate: low tier moves from 5 feelers * 4 base samples plus 6 gradient taps to 5 feelers * 1 nearest sample at reduced cadence.
- [x] Fixed cold tuning initialization from uninitialized Vault memory.
  - DOD practice: first boot writes `BuildDefaultTuning()` unconditionally before any read; runtime solver preserves editor-tuned values and only refreshes quality/capacity.
  - Rejected: checking `SourceHash` inside uninitialized memory.
  - Hot-path estimate: 0 us hot path; removes undefined cold-start branch.
- [x] Reworked black-box dump and CSV ingest to `Span<byte>`/`ReadOnlySpan<byte>`.
  - DOD practice: no crash-path `byte[]` scratch and no `ReadByte()` loop; cold CSV parser slices bytes directly.
  - Rejected: managed scratch allocation in fault dump and byte-at-a-time file ingest.
  - Hot-path estimate: 0 us hot path; cold/fault path allocation pressure reduced.
- [x] Static guard scan repeated.
  - DOD practice: `rg` confirmed Burst flags on all jobs and no `NavMesh`, `Physics.Raycast`, `Physics.SphereCast`, DTO properties, `Time.deltaTime`, or SHINOBU_157 global BufferID references in owned files.
  - Rejected: claiming compile/runtime proof while CPU guard forbids build.
  - Hot-path estimate: scan-only.

## Loop 7 - Hot Path and Compile-Wall Hardening
- [x] Cached Vault handle acquisition out of steady FixedTick path.
  - DOD practice: `_resolvedVehicleCapacity` plus `AreVaultHandlesReady` fast path; capacity changes force deterministic re-init instead of exposing uninitialized new slots.
  - Rejected: repeated `GetBufferHandle` calls after boot.
  - Hot-path estimate: removes repeated Vault handle lookups from every fixed solver admission; exact us PENDING profiler proof.
- [x] Moved layout reflection behind `UNITY_EDITOR`.
  - DOD practice: offset validator remains editor-time; runtime/player builds do not carry the reflection helper.
  - Rejected: leaving `System.Reflection` reachable in player runtime.
  - Hot-path estimate: 0 us hot path; compile surface and player metadata risk reduced.
- [x] Torn editor reads blocked while jobs own locked buffers.
  - DOD practice: tuning/state/telemetry read facades return false during `_buffersLocked`, solver pending, or init pending.
  - Rejected: editor facade reading potentially mutating Vault rows.
  - Hot-path estimate: editor-only safety.
- [x] Binary payload ledger updated for SHINOBU_157.
  - DOD practice: stable architecture ledger now records owner-local IDs, DTO layout, Dear Lie boundary, quality collapse, and compile-wall state.
  - Rejected: leaving the route only in transient agent logs.
  - Hot-path estimate: docs only.
- [x] Guarded compile attempted and blocked by unrelated missing source files.
  - DOD practice: build only after CPU <50% and no dotnet/csc; errors inspected and traced to `Hecton8.Core.csproj` stale includes.
  - Rejected: editing unrelated World/Construction project entries or fabricating placeholder files.
  - Hot-path estimate: verification blocked before owned source compilation.

## Loop 8 - Editor Facade and API Contract Pass
- [x] Rechecked real Core/Vault/AUP/vehicle contracts before editing.
  - DOD practice: inspected `GlobalDataVault`, `GlobalRegistry`, `ITickable`, `SubmarineKinematicState`, `HomeostasisBrain`, `HectonFloatingOrigin`, and `AbsoluteUniversePosition` definitions.
  - Rejected: trusting generated summaries or neighboring agent assumptions.
  - Hot-path estimate: 0 us, compile-risk reduction only.
- [x] Fixed editor facade namespace mismatch.
  - DOD practice: added `Hecton8.Core` import so Scene View AUP injection can resolve `HectonFloatingOrigin`.
  - Rejected: moving AUP conversion into runtime just to hide an editor import.
  - Hot-path estimate: 0 us runtime, editor-only compile fix.
- [x] Replaced formatted telemetry text with typed UI Toolkit readouts.
  - DOD practice: editor telemetry now updates integer/float fields via `SetValueWithoutNotify`; owned-file scan shows no `StringBuilder` or formatted `ToString()` remains.
  - Rejected: allocating a status string every telemetry refresh.
  - Hot-path estimate: 0 us runtime; editor refresh allocation risk reduced from one formatted managed string per 0.25s to numeric field updates.
- [x] Repeated owned-file forbidden API scan.
  - DOD practice: `rg` over runtime/editor owned files found no `NavMeshAgent`, `NavMeshPath`, `CalculatePath`, `Physics.Raycast`, `Physics.SphereCast`, `new List`, `foreach`, `Time.deltaTime`, `Time.fixedDeltaTime`, `StringBuilder`, or `ToString()`.
  - Rejected: relaunching build while generated csproj still excludes new files and previous solution build is blocked by unrelated stale includes.
  - Hot-path estimate: scan-only.

## Loop 9 - Profile Application and Flow Collapse Pass
- [x] Re-extracted the SHINOBU_157 prompt after context compression and re-read AGENTS/domain/mandate anchors.
  - DOD practice: CLI extraction by `<AGENT_PROMPT id="SHINOBU_157" ...>` plus targeted mandate refresh.
  - Rejected: treating the previous status file as the only task source.
  - Hot-path estimate: 0 us, governance-only.
- [x] Wired handling profiles into `ComputeDesiredVelocityJob`.
  - DOD practice: job reads the Vault `AutopilotHandlingProfiles` table through `[NoAlias]` pointer, applies turn rate, acceleration limit, speed scale, and repulsion scale, and uses default/scout/freighter FNV rows when CSV is absent.
  - Rejected: parser-only CSV with no steering effect; private persistent `NativeHashMap`.
  - Hot-path estimate: default profile resolves in one open-address probe; bounded worst case 32 probes per active submarine.
- [x] Added editor profile assignment controls.
  - DOD practice: editor-only buttons write `SubmarineHashID`/profile hash through a Vault-locked facade; runtime job remains data-only.
  - Rejected: string type names in the hot path.
  - Hot-path estimate: 0 us runtime except the existing job read of the profile hash.
- [x] Collapsed flow-grid sampling on low quality and hardened AUP deltas.
  - DOD practice: `GlobalQualityWeight` now gates flow interpolation from 1 nearest cell to trilinear, and far target AUP deltas are clamped in double space before float steering.
  - Rejected: always paying 8 flow taps at low quality; blind cast of unbounded double3 deltas.
  - Hot-path estimate: low tier flow grid read drops from 8 taps to 1 tap per active submarine; exact profiler us remains pending.
- [x] Added Unity `.meta` files for new owner source assets and repeated static guard scans.
  - DOD practice: created `.meta` for runtime source, editor folder, and editor window; `git diff --check` passed; forbidden API scan returned no matches.
  - Rejected: relying on Unity to generate source-control metadata later.
  - Hot-path estimate: source-control hygiene only.
- [x] Removed runtime dependency on `Hecton8.World.DispatcherJobSwap`.
  - DOD practice: owner-local `JobHandle.IsCompleted`/`Complete` helper preserves non-blocking post-fixed completion without a sibling namespace import in the runtime file.
  - Rejected: keeping a direct World namespace reference for two handle-completion calls.
  - Hot-path estimate: same completion branch; compile-wall risk reduced.

## Loop 10 - Vault Lock Transaction Pass
- [x] Re-read status/rationale and re-extracted the SHINOBU_157 prompt before editing.
  - DOD practice: disk-state anti-amnesia gate plus CLI prompt extraction.
  - Rejected: relying on context-compressed chat state.
  - Hot-path estimate: 0 us, governance-only.
- [x] Added fail-closed write fences to public editor/cold-path write APIs.
  - DOD practice: `SlowTick`, `TryWriteTargetAup`, `TryWriteHandlingProfileHash`, and `TryWriteTuning` now refuse writes while `_buffersLocked`, `_solverPending`, or `_initPending` are true.
  - Rejected: allowing editor/CSV writes to race a scheduled Burst route.
  - Hot-path estimate: 0 us inside Burst jobs; one bool check on cold/editor write paths.
- [x] Replaced broad route unlock with owner-local acquired-lock mask.
  - DOD practice: `_lockMask` records only buffers this navigator actually locked; rollback releases only those bits.
  - Rejected: unlocking the whole route after partial lock failure, because `GlobalDataVault.TryUnlockBuffer` is refcount-based and not owner-token based.
  - Hot-path estimate: unchanged solver math; prevents cross-writer lock refcount corruption.
- [x] Repeated static guard scans for the changed runtime/editor files.
  - DOD practice: owned-file forbidden API scan returned no matches; `git diff --check` passed for the runtime file.
  - Rejected: relaunching `dotnet build` while the last guarded solution build remains blocked by unrelated stale source includes.
  - Hot-path estimate: scan-only.

## Loop 11 - Zero-GC Route Writer Pass
- [x] Added a span-based public route writer for Logistics/editor handoff.
  - DOD practice: `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)` validates finite AUP waypoints, writes a fixed per-submarine slot range in `AutopilotWaypoints`, seeds `AutopilotRouteRangeDTO`, and sets the active `TargetAUP`.
  - Rejected: managed route lists, mission graph references, or per-node objects inside the autopilot domain.
  - Hot-path estimate: 0 us inside Burst jobs; cold handoff is O(route waypoint count) capped by fixed Vault capacity.
- [x] Route writer uses transactional local locks.
  - DOD practice: waypoints, route ranges, and states are locked in order and unlocked only if acquired.
  - Rejected: reusing the scheduled-job `_lockMask` for a synchronous facade write.
  - Hot-path estimate: main-thread/editor only; avoids lock refcount corruption under failed acquisition.
- [x] Repeated route-writer static guard scans.
  - DOD practice: latest self-audit XML parses as `route_writer`; forbidden API scan returned no matches; `git diff --check` passed with only the known CRLF warning in the ledger file.
  - Rejected: relaunching build while upstream stale source includes still block solution compile before SHINOBU_157.
  - Hot-path estimate: scan-only.

## Loop 12 - Editor Route Injection Pass
- [x] Added multi-waypoint Scene View route injection.
  - DOD practice: `Scene Click Route` mode builds a three-point dogleg route with `stackalloc Span<AutopilotWaypointDTO>` and calls `TryWriteRoute`.
  - Rejected: editor `List<>` staging, mission graph references, or physics casts.
  - Hot-path estimate: 0 us runtime; editor click path is three DTO writes plus route/state updates.
- [x] Repeated editor facade static guard scans.
  - DOD practice: owned-file forbidden API scan returned no matches; editor source `git diff --check` passed.
  - Rejected: launching build while the known unrelated `Hecton8.Core.csproj` missing-file blocker remains unresolved.
  - Hot-path estimate: scan-only.

## Loop 13 - Route ABI Hygiene Pass
- [x] Replaced route magic flag writes with named constants.
  - DOD practice: `WaypointFlagActive` and `RouteFlagActive` now document route/waypoint activation semantics.
  - Rejected: raw `1u` flags that make binary route records harder to audit.
  - Hot-path estimate: no runtime cost; constants fold at compile time.
- [x] Made route writer use resolved Vault capacity when available.
  - DOD practice: `TryWriteRoute` bases fixed waypoint slices on `_resolvedVehicleCapacity` after Vault negotiation, falling back to serialized capacity only before resolution.
  - Rejected: deriving route slices solely from inspector state after the Vault route is active.
  - Hot-path estimate: editor/cold ingress only; prevents wrong slice math after capacity normalization.

## Loop 14 - Full DTO Layout Guard Pass
- [x] Expanded editor-time layout validation beyond `AutopilotStateDTO`.
  - DOD practice: `AutopilotStateDTOLayout.ValidateAll()` now checks state, avoidance, feeler, waypoint, route, tuning, telemetry, and handling profile DTO size/offset contracts, including all tuning/telemetry/profile fields.
  - Rejected: auditing only the primary state DTO while other Vault/rollback DTOs could drift.
  - Hot-path estimate: 0 us player/runtime; reflection remains behind `UNITY_EDITOR`.
- [x] Repeated owned-source guard scan after layout guard expansion.
  - DOD practice: forbidden API scan returned no matches; runtime source `git diff --check` passed.
  - Rejected: build launch while unrelated missing source includes still block solution compile before SHINOBU_157.
  - Hot-path estimate: scan-only.

## Loop 15 - Quality Snapshot Hygiene Pass
- [x] Split authored quality cap from runtime-resolved quality.
  - DOD practice: `AutopilotTuningDTO.GlobalQualityWeight` remains the designer/network cap; offset 120 now stores `ResolvedQualityWeight = quantized min(HomeostasisBrain.GlobalQualityWeight, cap)`.
  - Rejected: overwriting the authored cap every solver frame, which made tuning sticky after thermal throttling and weakened rollback snapshot clarity.
  - Hot-path estimate: one float min plus 0.001 quantization on scheduling; Burst jobs keep the same O(vehicle count * feelers * steps) work.
- [x] Routed Burst jobs and flow interpolation through resolved quality.
  - DOD practice: scheduler cadence, SDF feelers, telemetry estimate, and flow sampling consume the same resolved scalar for a single per-frame quality fact.
  - Rejected: mixing live Homeostasis reads in jobs with an authored cap stored in the DTO.
  - Hot-path estimate: no new SDF samples; low-tier collapse remains 5 feelers, 1 nearest SDF lookup, nearest flow read, and reduced cadence.
- [x] Added editor-only quality cap facade and resolved-quality readout.
  - DOD practice: UI Toolkit tuner can set the authored cap and read the resolved scalar without formatted telemetry strings.
  - Rejected: forcing designers to edit serialized/runtime code to test quality pressure.
  - Hot-path estimate: 0 us runtime; editor-only controls.
- [x] Repeated owned C# static scans.
  - DOD practice: forbidden API scan returned no matches, Burst/NoAlias scan confirmed directives, and `git diff --check` passed for the changed C# files.
  - Rejected: relaunching `dotnet build` while the known unrelated stale include blocker remains unresolved and the user explicitly limited builds.
  - Hot-path estimate: scan-only.

## Loop 16 - Black Box Alias Pass
- [x] Added AGENTS-compliant dump alias.
  - DOD practice: fault dump now writes both `Docs/AgentLogs/Dump_SHINOBU_157.bin` and the XML-requested `Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin` from the same telemetry span.
  - Rejected: choosing one document path and leaving the other contract unverifiable.
  - Hot-path estimate: 0 us normal runtime; fault/shutdown path writes one extra 19.2 KB telemetry copy.
- [x] Kept dump writer allocation-free for telemetry bytes.
  - DOD practice: `WriteTelemetryDump` streams `ReadOnlySpan<byte>` over the existing Vault telemetry ring; no managed byte scratch is introduced.
  - Rejected: `byte[]` scratch or per-entry text log serialization.
  - Hot-path estimate: fault path only; no SDF/steering sample change.

## Loop 17 - Runtime Compile-Wall Import Pass
- [x] Removed unused runtime `Hecton8.World` import.
  - DOD practice: runtime source import scan now shows only `Hecton8.Core`, `Hecton8.Core.Memory`, and `Hecton8.Physics.Vehicles`; World AUP conversion remains editor-only in the tuner file.
  - Rejected: keeping a sibling namespace import because it was harmless at source level; asmdef isolation treats that as compile-wall debt.
  - Hot-path estimate: 0 us; compile-wall surface reduction only.

## Loop 18 - Cadence Delta Accumulation Pass
- [x] Accumulated deterministic solver delta across quality-skipped fixed ticks.
  - DOD practice: `FixedTick` sanitizes the dispatcher delta, accumulates up to 0.25s while cadence/pending gates skip work, and passes the accumulated window into `ComputeDesiredVelocityJob`.
  - Rejected: giving the solver a single 1/60s delta after dropping update frequency to 5Hz, which over-clamps acceleration and turn rate on low quality.
  - Hot-path estimate: one float add/min per fixed tick; avoids low-tier steering sluggishness without increasing SDF sample count.
- [x] Made solver scheduling transactional for delta consumption.
  - DOD practice: `ScheduleSolver` returns `bool`; accumulated delta resets only after the job is actually scheduled.
  - Rejected: losing accumulated simulation time when Vault locks or pointer resolution fail.
  - Hot-path estimate: one bool branch on the main scheduler path.
