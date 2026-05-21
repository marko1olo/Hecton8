# SHINOBU_249 Report - BUOYANCY_SLEEP_STATE_INTEGRATOR

Timestamp: 2026-05-21 local session.

What was wrong:
- Buoyancy state could mark rows sleeping, but sleep was one-way and lacked wake propagation.
- Settled debris still had no SDF-confirmed unmanaged sleep authority for the 50,000-object seabed case.
- `KinematicStateDTO` had no `Flags` at offset 52; the prompt-required rollback-visible sleep bit route did not exist.
- No dedicated 300-frame sleep telemetry ring or SHINOBU_249 dump path existed.
- No proof artifact existed for eradication of `Rigidbody.Sleep`, `.IsSleeping`, or `sleepThreshold` usage in active Physics scripts.

What was done:
- Preserved `KinematicStateDTO` at 64 bytes and placed `uint Flags` at offset 52; moved drag/counters into remaining padding.
- Added `EvaluateKinematicSleepStateJob` and `ProcessKinematicSleepWakeTriggersJob` for raw `KinematicStateDTO` rows.
- Extended buoyancy DTOs with rest/deep-sleep counters and material sleep profile index without increasing the 64-byte state size.
- Raised buoyancy state/mock capacity to 50,000 and updated mock seeding to generate decaying seabed debris velocities.
- Added SDF density/config Vault buffers, material settling profile table, sleep telemetry ring/cursor, and fixed BufferIDs.
- Integrated wake prepass from `SignalBus<WakeRequestSignal>` and low-frequency ambient-current wake polling.
- Added continuous `GlobalQualityWeight` sleep aggression: lower quality inflates sleep threshold and reduces required rest frames.
- Added deep-sleep `FlagStaticPromotionPending` for presentation-owner static batching.
- Added cold `ReadOnlySpan<byte>` parser for `Data/Physics/material_settling_profiles.csv`.
- Added UI Toolkit `Physics Sleep State X-Ray` editor window, live sleep gizmos, layout validator, static scanner, JSON report, and architecture card.

Cinematic cheats used:
- Nearest-cell signed byte SDF contact instead of high-order terrain solve.
- Plane seafloor fallback when SDF is not configured.
- Triangle/analytic flow remains a visual cheap path where authored abyssal samples are missing.
- Sleep aggression uses a smooth quality curve instead of hardware-tier branches.

Exact microseconds saved estimates:
- Fixed-row flag sleep versus active/inactive row migration: estimated 0.04 us per sleeping row skipped, 2,000 us per 50,000 rows per full pass.
- Avoided property/indexer DTO mutation: estimated 0.06 us per row, 3,000 us per 50,000-row evaluation.
- Avoided atomic RMW in row-owned jobs: estimated 0.01 us per row, 500 us per 50,000-row evaluation.
- No-signal wake prepass: estimated 0 us scheduled when `WakeRequestSignal` count is zero.
- Ambient current polling cadence: skips 7/8 frames at high quality and 44/45 frames at low quality, saving the skipped poll walks.
- Sleep telemetry: one 64-byte write per frame; cost accepted for black-box proof.

Verification:
- Forbidden active Physics managed sleep scan: 0 findings excluding scanner source.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`: written.
- `git diff --check`: passed with line-ending warnings only.
- Compile: not launched. CPU gate reported 88.39%, then 100%, and `dotnet` process 29812 was active. The project instruction forbids build under those conditions.

<SELF_AUDIT>
Agent: SHINOBU_249
DTOs:
- KinematicStateDTO: 64 bytes. AUP offset 0, Velocity 24, AngularVelocity 36, Mass 48, Flags 52, DragCoefficient 56, RestingFrameCount 60.
- BuoyancyStateDTO: 64 bytes. CurrentAUP offset 0, Velocity 24, Volume 36, Mass 40, EntityHashID 44, Flags 48, RestingFrameCount 52, DeepSleepTickCount 53, MaterialSleepProfileIndex 54.
- SleepStateTelemetryEntry: 64 bytes, 300 rows.
Vault BufferIDs:
- ShinobuBuoyancySleepSdfDensity = 71643
- ShinobuBuoyancySleepSdfConfig = 71644
- ShinobuBuoyancySleepTelemetryRing = 71645
- ShinobuBuoyancySleepTelemetryCursor = 71646
- ShinobuBuoyancyMaterialSettlingProfiles = 71647
GC:
- Hot jobs allocate 0 managed bytes by code inspection: no managed collections, no C# events, no LINQ, no `Rigidbody.Sleep`.
- Cold/editor paths may allocate for file IO/UI/reporting only.
Bitwise authority:
- Sleep/wake uses `&`, `|`, `~` flag mutation on unmanaged DTO rows.
- No active/inactive array migration was introduced.
AUP:
- SDF/wake math subtracts `double3` origins before local `float3` sampling.
Wake coverage:
- `WakeRequestSignal` snapshots wake sleeping rows by radius before the buoyancy evaluation pass.
Compile:
- Static source checks were run, but Unity import, Burst Inspector, Profiler/GCMonitor, and full compile remain pending under the CPU/dotnet gate.
</SELF_AUDIT>

## Ultra-Think Polish Addendum 3 - 2026-05-21

What was wrong:
- Compile proof is still absent and must not be implied.
- The latest gate check showed CPU at 100 with active `dotnet` PID `29148`, so launching another build would violate project policy.

What was done:
- Re-extracted the SHINOBU_249 prompt from `CURRENT_BATCH.md` lines `3610..3674`.
- Re-read the KCC `KinematicStateDTO` layout: 64 bytes, `Flags@52`, sleep counters in tail bytes, no wrapper state.
- Re-read `FixedTick` and `TryPrepareRuntimeVault`; hot fixed tick now requires cold boot and ready handles and does not run descriptor recovery.
- Re-scanned active SHINOBU hot surfaces for PhysX sleep, active/inactive lists, managed hot collections, stale SDF predicate, stale editor `Resolve` names, and cold recovery symbols.

Cinematic cheats used:
- No additional simulation added. The route remains nearest-cell signed-byte SDF contact plus SignalBus wake snapshots instead of raycasts/collider polling.

Microseconds saved:
- No new frame-time saving claimed. This pass reduces verification risk only.

Verification:
- Compile not launched: CPU 100 and active `dotnet` PID `29148`.
- Static prompt/layout/hot-pattern checks passed for the SHINOBU_249 surfaces inspected in Loop 9.
- Generated `.csproj` files do not yet include the new SHINOBU_249 KCC/editor scripts, so stale `dotnet build` output must not be used as full proof until Unity imports/regenerates project files.

## Ultra-Think Polish Addendum 4 - 2026-05-21

What was wrong:
- Already sleeping rows still traversed too much of `EvaluateBuoyancyJob`, including material/flow/SDF/force math, and deep-sleep counters could be reset on later evaluation passes.

What was done:
- Added an early dormant-row branch after quality cadence resolution and before material profile, density, flow, drag, SDF, and force packet work.
- The branch preserves `FlagSleeping`, increments `DeepSleepTickCount`, marks deep/static promotion when due, writes debug telemetry, clears the force candidate slot, and returns.

Cinematic cheats used:
- The row-local `SLEEP_BIT` is now a true Dear Lie bypass for settled debris: no raycast, no collider query, no fluid calculation, no list migration.

Microseconds saved:
- Not measured. The removed operations per sleeping evaluated row are material profile probing, flow sampling, density/drag calculation, nearest SDF sampling, and force packet construction.

Verification:
- Static stale/forbidden scans remain clean.
- `git diff --check` reports no whitespace errors for the patched job; CRLF warning only.
- Compile still not launched because CPU gate returned 100.

## Ultra-Think Polish Addendum - 2026-05-21

What was wrong:
- Prior status language overclaimed static verification while Unity import, Burst Inspector, profiler, GCMonitor, and player-build proof are absent.
- Standalone `KinematicSleepStateJobs.cs` sat under Buoyancy and imported KCC, creating an avoidable cross-domain dependency smell.
- The shared `PHYSICS_OPTIMIZATION_REPORT.json` had been treated as a single-agent report path even though other agents also write addenda there.
- The new Vault/telemetry/SDF route needed a dedicated route card, not only a descriptive architecture note.

What was done:
- Status and rationale now remain `PENDING VERIFICATION / POLISH PASS ACTIVE`.
- `KinematicSleepStateJobs.cs` moved to `Assets/_Project/Scripts/Physics/KCC/` and now uses KCC-local `KinematicSleepSdfConfigDTO=64`, removing the Buoyancy config dependency from that standalone job.
- `RigidbodySleepScanner` now writes `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_249.json` and adds a shared-report addendum instead of overwriting the shared report.
- Added `Docs/ARCHITECTURE/SHINOBU_249_BUOYANCY_SLEEP_ROUTE_CARD.md`.

Cinematic cheats used:
- No new heavy simulation was added. The cheap signed-byte SDF contact and passive SignalBus wake snapshot remain the Dear Lie route.

Microseconds saved:
- No additional runtime saving claimed in this addendum. The change reduces compile-wall and authority risk, not frame time.

Verification:
- Pending. Static gates must be rerun after this polish pass, and compile remains gated by CPU/dotnet policy.

## Ultra-Think Polish Addendum 2 - 2026-05-21

What was wrong:
- Subagent audit found a real SDF predicate bug: positive signed distances could be treated as grounded.
- `FixedTick` could enter cold Vault recovery and descriptor allocation paths.
- Buoyancy route hardcoded angular energy to zero.
- Mutable editor buffer accessors still used read-like `Resolve` naming.
- Scanner/report language implied AST-level proof while implementation was text scanning.

What was done:
- SDF grounding in buoyancy and KCC sleep kernels now requires `abs(signedDistance) <= contactEpsilon`.
- `FixedTick` now only accepts already cold-booted, ready Vault handles; failed resolution returns without recovery allocation.
- `BuoyancyStateDTO` uses prior padding at offset 56 for `AngularSpeedSq`; state size remains 64 bytes.
- Editor APIs were renamed to `TryOpen*` and callers updated.
- Scanner now masks comments/strings and labels proof as tokenized text scan, not AST proof.
- Route docs now state active sleep authority is `BuoyancyStateDTO`; KCC `KinematicStateDTO` jobs are isolated KCC-owned kernel artifacts, not scheduled by Buoyancy.
- Added XML docs on new public SHINOBU_249 KCC/editor/runtime view APIs.

Cinematic cheats used:
- Kept nearest-cell signed-byte SDF contact. No raycasts, no collider sleep, no object migration.

Microseconds saved:
- No new frame-time savings claimed here. This pass closes correctness/allocation risks; profiler proof remains pending.

Verification:
- JSON reports parse.
- Forbidden active Physics managed sleep scan: 0 findings excluding scanner source.
- Stale SDF/editor/recovery symbol scan: 0 findings.
- `git diff --check`: no whitespace errors; CRLF warnings only.
- Compile not launched: latest CPU sample 96.30%, no visible `dotnet`/`csc` rows; policy forbids build above 50%.

<SELF_AUDIT pass="POLISH_LOOP_7" status="PENDING_COMPILE_AND_RUNTIME_PROOF">
Tasks 01-20:
01 PASS static forbidden managed sleep scan clean.
02 PASS no active/inactive row migration introduced.
03 PASS hot DTO mutation uses unmanaged fields and bitwise flags.
04 PASS `KinematicStateDTO` remains 64 bytes, `Flags` offset 52.
05 PASS mock settling jobs exist for buoyancy and KCC kernels.
06 PASS Burst sleep kernels exist; active runtime route is buoyancy-owned, KCC kernel is not scheduled by Buoyancy.
07 PASS SDF grounding now requires `abs(signedDistance) <= contactEpsilon`.
08 PASS wake route uses `SignalBus<WakeRequestSignal>` snapshot.
09 PASS ambient current wake is cadence-gated; non-sleeping rows branch out before flow sampling.
10 PASS continuous quality curve controls threshold/rest frames/cadence.
11 PASS deep sleep raises static-promotion flag only.
12 PASS SDF/wake deltas subtract `double3` AUP before `float3`.
13 PASS DTOs remain blittable explicit layouts; compile/runtime proof pending.
14 PASS hot tick does not run cold descriptor recovery; cold buffers use uninitialized options where overwritten.
15 PASS 300-row sleep telemetry route and dump target exist.
16 PASS UI Toolkit X-Ray exists under editor.
17 PASS cold `ReadOnlySpan<byte>` material settling parser exists.
18 PASS editor gizmo exists.
19 PASS scanner sidecar/shared addendum exist; proof is tokenized text scan, not AST.
20 PASS static self-audit artifacts updated and public surfaces documented; Unity/Burst/profiler proof pending.
Struct layout:
KinematicStateDTO 64 bytes: AUP 0..23, Velocity 24..35, AngularVelocity 36..47, Mass 48..51, Flags 52..55, DragCoefficient 56..59, RestingFrameCount 60, DeepSleepTickCount 61.
KinematicSleepSdfConfigDTO 64 bytes: SdfOriginAUP 0..23, CellSize 24..27, DecodeScale 28..31, ContactEpsilon 32..35, Width 36..39, Height 40..43, Depth 44..47, StrideY 48..51, StrideZ 52..55, Flags 56..59, pad 60..63.
BuoyancyStateDTO 64 bytes: CurrentAUP 0..23, Velocity 24..35, Volume 36..39, Mass 40..43, EntityHashID 44..47, Flags 48..51, RestingFrameCount 52, DeepSleepTickCount 53, MaterialSleepProfileIndex 54..55, AngularSpeedSq 56..59, pad 60..63.
Vault handles:
ShinobuBuoyancySleepSdfDensity 71643, ShinobuBuoyancySleepSdfConfig 71644, ShinobuBuoyancySleepTelemetryRing 71645, ShinobuBuoyancySleepTelemetryCursor 71646, ShinobuBuoyancyMaterialSettlingProfiles 71647.
Dependency graph:
Consumes SignalBus WakeRequestSignal snapshot and previous fixed-tick dependency; schedules wake/current prepass, EvaluateBuoyancyJob, compact job, telemetry reduce; outputs dispatcher-held `_pendingHandle`. No mid-frame `.Complete()` in runtime tick.
Dear Lie:
Nearest-cell signed-byte SDF plus passive wake snapshot replaces collider sleep/raycast polling and active/inactive migration. Complexity remains O(N) branch scan for fixed rows; heavy force/SDF/flow work collapses toward O(activeAwake + cadenceSleeping).
</SELF_AUDIT>

## Ultra-Think Polish Addendum 5 - 2026-05-21

What was wrong:
- The awake evaluator still treated authored flow samples as mandatory, even though the math has a deterministic analytic fallback.

What was done:
- `EvaluateBuoyancyJob` now accepts empty/missing flow sample buffers and uses analytic flow in that case.
- Force-candidate slots are still cleared for dormant/out-of-range work items.

Cinematic cheats used:
- Analytic triangle-wave flow remains the cheap fallback; no new current-field simulation was introduced.

Microseconds saved:
- Not measured. This prevents an early frame abort and preserves stale-candidate hygiene when the flow sample route is unavailable.

Verification:
- Static pattern scan remains clean.
- `git diff --check` reports no whitespace errors for the touched job; CRLF warning only.

## POLISH_LOOP_8_ADDENDUM
What was wrong: Subagent static audit found a P0 editor compile blocker: `RigidbodySleepScanner` called nonexistent `HydrodynamicKccLayoutValidator.Validate()` at two sites.

What was done: Replaced both calls with the existing `HydrodynamicKccLayoutValidator.ValidateRuntimeLayout(out _)` route. Re-ran stale API scan and scanner `diff --check`; no stale `Validate()` call remains and whitespace gate is clean.

Cinematic Cheats used: None in runtime. This is editor proof-surface repair only.

Exact Microseconds saved: 0 runtime us. The change removes a compile blocker; profiler-backed runtime savings still depend on Unity import/compile and frame capture once the CPU gate opens.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. CPU gate sampled at 100%, so Unity compile/build remains deferred by project policy.

## POLISH_LOOP_9_ADDENDUM
What was wrong: The scanner P0 patch needed a follow-up boundary classification and report proof recheck.

What was done: Confirmed no active Buoyancy/KCC runtime asmdef split exists, so the cross-import is not an immediate compile fault. Kept the scanner in its editor-only Physics location and recorded the future split risk in the rationale. Re-parsed both physics optimization report JSON files successfully.

Cinematic Cheats used: None. This is verification hygiene only.

Exact Microseconds saved: 0 runtime us. Avoided speculative asmdef churn that could force broad recompiles for other agents.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. CPU gate remains 100%, no Unity compile/build launched.

## POLISH_LOOP_10_ADDENDUM
What was wrong: New SHINOBU_249 `.cs.meta` files lacked `MonoImporter` blocks, unlike existing Unity script metas in the project.

What was done: Added `MonoImporter` metadata to the KCC sleep job, Rigidbody sleep scanner, and Physics Sleep State X-Ray window metas while preserving GUIDs. Verified each meta has `assetBundleName` and `assetBundleVariant`; `git diff --check` is clean.

Cinematic Cheats used: None. This is import hygiene only.

Exact Microseconds saved: 0 runtime us. Prevents avoidable Unity import churn and keeps script GUIDs stable.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. CPU gate remains above the build threshold.

## POLISH_LOOP_11_ADDENDUM
What was wrong: Euler found that `EvaluateBuoyancyJob` still returned early when `FlowSamples` was not created, contradicting the analytic-flow fallback and risking stale force packet slots. The scanner also double-counted `.sleepThreshold` through both a narrow and broad pattern.

What was done: Removed the evaluator's stale `!FlowSamples.IsCreated` entry guard. Missing flow samples now produce `flowSampleCount = 0` and route through deterministic analytic flow. Removed the duplicate `.sleepThreshold` scanner pattern while preserving `sleepThreshold`.

Cinematic Cheats used: Analytic triangle-wave flow remains the cheap deterministic fallback; no authored current field is mandatory for sleep evaluation.

Exact Microseconds saved: Not measured. This closes a correctness/fail-safe gap; profiler proof remains pending.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Targeted static scans passed; CPU gate remains above the build threshold.

## POLISH_LOOP_12_ADDENDUM
What was wrong: The scanner's shared report method was named like an upsert but returned immediately when `shinobu249BuoyancySleep` already existed. Future scanner runs could leave stale SHINOBU_249 proof in the shared JSON.

What was done: Added object-brace matching and replacement for the existing SHINOBU_249 property. Other agents' report fields are preserved.

Cinematic Cheats used: None. This is editor/report evidence hygiene.

Exact Microseconds saved: 0 runtime us. The gain is audit correctness, not frame time.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Scanner source static checks passed; Unity import/compile still CPU-gated.

## POLISH_LOOP_13_ADDENDUM
What was wrong: Task 17 claimed a default `material_settling_profiles.csv`, but the authoring file was absent under `Assets/_Project/Data/Physics/`.

What was done: Added `Assets/_Project/Data/Physics/material_settling_profiles.csv` with seven cold-tuned material rows, plus Unity metas for the new Physics data folder and CSV. Normalized the SHINOBU-owned Buoyancy editor folder meta.

Cinematic Cheats used: Material-specific settling thresholds let light debris freeze earlier under low quality while heavier salvage keeps stricter settling. No per-material physics object model was added.

Exact Microseconds saved: 0 measured runtime us. The CSV is cold input only; Burst jobs consume the existing Vault table.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. CSV import parsed 7 rows with 0 invalid rows via PowerShell structural check; Unity import/compile remains CPU-gated.

## POLISH_LOOP_16_ADDENDUM
What was wrong: The central binary payload ledger did not yet name SHINOBU_249's sleep SDF/profile/telemetry BufferIDs or the cold CSV source boundary, even though source/docs used those lanes.

What was done: Patched `SHINOBU_249_BUOYANCY_SLEEP_ROUTE_CARD.md`, `Buoyancy_Sleep_State_SHINOBU_249.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with BufferIDs `71643..71647`, DTO anchors, CSV source `Assets/_Project/Data/Physics/material_settling_profiles.csv`, endian/save boundaries, active authority owner, and `Dump_SHINOBU_249.bin` fault route.

Cinematic Cheats used: Material settling stays a cold authoring/Vault profile route; runtime sleep math remains flag/SDF driven and does not parse or allocate profile objects.

Exact Microseconds saved: 0 runtime us. This removes audit drift before compile/import verification.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Unity import/compile remains CPU-gated.

## POLISH_LOOP_17_ADDENDUM
What was wrong: Compile/import proof remains blocked by CPU/dotnet policy, so source-level regressions needed another focused pass instead of waiting idle.

What was done: Re-ran SHINOBU-owned Physics scans. No active forbidden PhysX sleep/list/managed collection/foreach/LINQ/hidden `.Complete()` hits appeared in Buoyancy/KCC sleep surfaces excluding the scanner source. Burst sleep/evaluator jobs use deterministic mode. Stale `Validate()`, permissive `signedDistance >=`, fixed-tick recovery, and duplicate `.sleepThreshold` scanner symbols are absent. Normalized `material_settling_profiles.csv.meta` with `TextScriptImporter`.

Cinematic Cheats used: The analytic triangle-wave flow fallback remains the cheap current proxy when authored flow samples are absent; no heavy current-field dependency was added.

Exact Microseconds saved: Not measured. Static gate only; runtime savings remain profiler-pending.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. CPU sampled at `99%` with seven active `dotnet` workers, so Unity/dotnet compile was not launched.

## POLISH_LOOP_18_ADDENDUM
What was wrong: Existing log-embedded self-audit text predated the final CSV, ledger, meta, and static-source gates.

What was done: Added `Docs/Reports/SHINOBU_249_SELF_AUDIT.xml` with 20-task reconciliation, struct layout offsets, Vault BufferIDs, dependency graph, compile guard, Dear Lie complexity, and cold CSV boundary. The artifact explicitly marks Unity/profiler proof as pending. Added an explicit `using Hecton8.Physics;` import to the editor X-Ray window to avoid namespace lookup ambiguity during Unity import. Restored the shared report `shinobu249BuoyancySleep` addendum to match the SHINOBU sidecar.

Cinematic Cheats used: The audit records the flag-only sleeping bypass and analytic flow fallback as the cheap substitutes for full dormant-object physics/current simulation.

Exact Microseconds saved: 0 runtime us; evidence artifact only.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Runtime verification remains gated.

## POLISH_LOOP_19_ADDENDUM
What was wrong: Unity import/compile proof had been pending. Once CPU dropped to `12.75%` with no active `dotnet`/`csc`/Unity process, Unity batchmode was allowed and launched. The compile did not fail in SHINOBU_249 code; it failed in existing external assemblies and then stopped advancing after `06:00:17` with Bee still alive.

What was done: Captured `Docs/AgentLogs/UnityCompile_SHINOBU_249.log`, parsed `Library/Bee/tundra.log.json`, and checked `Hecton8.Core.rsp`. SHINOBU sources are in the Unity compile DAG, but `Hecton8.Core` did not reach a `Csc` result because upstream contract/editor assemblies failed first. Confirmed no `SHINOBU_249`, `KinematicSleepStateJobs`, `RigidbodySleepScanner`, `PhysicsSleepStateXRayWindow`, or buoyancy sleep symbols appeared in compiler error output, and stopped only the owned Unity/Bee process to release the compile lock. Updated status, rationale, self-audit, and physics reports to mark compile verification as externally blocked rather than passed.

Cinematic Cheats used: None in this verification loop. Runtime cheat remains the flag-only sleeping row bypass plus deterministic analytic flow fallback.

Exact Microseconds saved: 0 measured runtime us. The verification attempt exposed a compile wall outside this agent's domain.

External compile blockers observed: `World/Contracts/TerrainChunkGeneratedSignal.cs` missing `Hecton8.Core`/`ISignal`; `Core/Contracts/AupPrecisionContracts.cs` missing `long3`; Habitat DamageBake missing `ObjectField`; InteriorClutterForge uses unavailable `Mesh.MeshData.GetVertexAttribute`; TextureChannelPacker missing `HectonEditorMeshUtility`; GeologyForge missing `Mix` plus unsafe address errors; OfflineHadalArchBaker missing a `Schedule` extension; VoxelTerrainSeamBinder uses unavailable `MeshUpdateFlags.DontRecalculateNormals`.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Unity proof is `BLOCKED_EXTERNAL_COMPILE_ERRORS`; no SHINOBU-owned compiler error was found in Bee output.

## POLISH_LOOP_20_ADDENDUM
What was wrong: The Unity compile response file from the blocked import included `PhysicsSleepStateXRayWindow.cs` and `RigidbodySleepScanner.cs` in `Hecton8.Core.rsp`. Those files are editor-only proof tooling and should not ride in the runtime assembly compile lane.

What was done: Added `Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef` plus normalized meta. The assembly is Editor-only and `autoReferenced: false`; Loop 22 later added Roslyn precompiled references inside this Editor-only lane for AST scanning. Reports mark the isolation as source-fixed and Unity-reimport-pending.

Cinematic Cheats used: None. This is compile-wall hygiene for editor tooling; runtime cheat remains flag-only dormant-row bypass plus analytic flow fallback.

Exact Microseconds saved: 0 measured runtime us. Expected gain is compile-scope hygiene and removal of editor-only code from the runtime response file after Unity reimport.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. A fresh Unity import is still blocked by the external compile errors already logged in Loop 19, and an active Unity/dotnet/Bee compiler session was present after this patch, so no new build/import was launched.

## POLISH_LOOP_21_ADDENDUM
What was wrong: The force packet queue resets only the compacted packet counter. Without a written proof, this looked like a stale-force risk for objects that enter `FlagSleeping`.

What was done: Traced the full route: `TryPrepareBuoyancyForcePackets` resets `Counters[0].ForcePackets`; `EvaluateBuoyancyJob` writes `default` for sleeping/out-of-range candidate slots; `CompactBuoyancyForcePacketsJob` scans only `min(scheduledEvaluationCount, packetCapacity)` and writes the valid compact count; `DrainBuoyancyForcePackets` drains only that count. Recorded the invariant in status, rationale, self-audit, route card, and JSON reports.

Cinematic Cheats used: No new physics. The existing cheat remains flag-only dormant-row bypass; this pass preserves it by proving old force packets cannot reanimate sleeping debris.

Exact Microseconds saved: Not measured. Avoided a full 8,192-slot packet clear per fixed tick; at 128 bytes per packet that is up to 1 MB of unnecessary memory writes per tick avoided.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Static source proof only; Unity/profiler proof remains externally blocked.

## POLISH_LOOP_22_ADDENDUM
What was wrong: Task 19 demanded AST parsing, but the current scanner proof was honestly marked `TOKENIZED_TEXT_SCAN_NOT_AST`. That left a documentation-grade gap even though forbidden managed sleep APIs were absent by static scan.

What was done: Upgraded `RigidbodySleepScanner` to Roslyn AST parsing using `LanguageVersion.Preview`, with token fallback only for parser failures. It now records parser mode, scanned file count, parser failures, and parser source per finding. Updated the Buoyancy editor asmdef with Roslyn precompiled references under an Editor-only assembly. Updated sidecar/shared JSON and self-audit to record the AST scanner source patch while preserving the fact that Unity has not executed the new scanner yet.

Cinematic Cheats used: None in runtime. This is cold proof tooling. It protects the existing cheat: settled debris exits on a flag check rather than paying PhysX sleep or full buoyancy math.

Exact Microseconds saved: 0 measured runtime us. Prevents future managed PhysX sleep regressions; no new player-frame cost.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. JSON reports parse, self-audit XML parses, forbidden managed sleep scan excluding `RigidbodySleepScanner.cs` returns zero hits, and `git diff --check` has only the existing shared-report CRLF warning. CPU sampled at `100%`, so Unity/dotnet import was not launched.

## POLISH_LOOP_23_ADDENDUM
What was wrong: The wake route needed another Task 08 audit because the prompt names force packets, while the active route consumes `WakeRequestSignal`. During that audit, `ProcessBuoyancyWakeTriggersJob` was found without an explicit Burst directive.

What was done: Confirmed the current decoupled route: Cavitation publishes `WakeRequestSignal` from shockwave force events, and Buoyancy consumes the Core/Contracts SignalBus snapshot without importing Cavitation `ForcePacketDTO`. Added deterministic `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` to `ProcessBuoyancyWakeTriggersJob`. Re-scanned SHINOBU job structs in Buoyancy and KCC sleep files; no missing Burst directive remained. Updated the route card with the Cavitation-to-WakeRequest bridge boundary.

Cinematic Cheats used: Wake remains a radius signal snapshot, not per-sleeping-object collision polling. Buoyancy does not inspect Cavitation force-packet internals.

Exact Microseconds saved: Not measured. Avoids a direct sibling-domain packet scan and restores Burst eligibility for the wake prepass; profiler proof remains pending.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Static directive scan passed; `git diff --check` only reports LF->CRLF warning for the touched C# file. Unity import/profiler proof remains blocked.

## POLISH_LOOP_24_ADDENDUM
What was wrong: `FixedTick` mutated the Vault-backed tuning row before the job-buffer lock was acquired. `FinishPendingSolverCompletion` released the lock before writing completed `ComputeMicros` into counters and telemetry. Both were hot-path ownership-order defects, not GC defects.

What was done: Moved `TryLockJobBuffers(vault)` before tuning mutation, added explicit unlock on the zero-active early return, and moved microsecond telemetry writes plus fault-flag read before unlock. Fault dump file IO stays after unlock.

Cinematic Cheats used: None new. The existing sleep cheat remains the same: settled rows exit after flag/counter maintenance and never pay full buoyancy/drag/SDF/current math.

Exact Microseconds saved: Not measured. The patch targets memory-ownership determinism. It avoids potential cross-phase Vault races without adding a new allocation or full-buffer clear.

Verification state: `PENDING VERIFICATION / POLISH PASS ACTIVE`. Targeted static checks passed with LF->CRLF warnings only; CPU sampled at `100%`, so Unity/dotnet import was not launched.
