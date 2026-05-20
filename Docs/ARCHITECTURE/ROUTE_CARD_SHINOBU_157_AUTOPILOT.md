# Route Card: SHINOBU_157 Submarine Autopilot

Owner: Echelon 6 Habitat & Vehicles / Autonomous Submarine Navigation
Runtime file: Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs
Review disposition: YELLOW - static source route only; guarded dotnet compile is blocked by unrelated stale project includes before SHINOBU_157 source; no Unity import, Burst compile, profiler, or Play Mode evidence attached.

## R43 Normalized Route-Card Fields

| Field | Value |
|---|---|
| Route ID | `SHINOBU_157_SUBMARINE_AUTOPILOT` |
| Owner | SHINOBU_157 / SUBMARINE_AUTOPILOT |
| Producer phase | `SIMULATION` autopilot command solve and budgeted attitude/depth correction |
| Consumer phase | `POST_SIMULATION` force-command publication plus `VISUAL_SYNC` debug/gizmo readback when enabled |
| Cadence | Fixed simulation cadence for command truth; visual/debug consumers are quality-gated and must not force runtime completion |
| Capacity | Vault-backed command/state/telemetry lanes; telemetry ring fixed at 300 entries; debug readback bounded by authoring tool cadence |
| Overflow/failure | Invalid or saturated inputs publish bounded safe command output and telemetry; route stays YELLOW until compile/runtime/profiler proof exists |
| Shutdown/disposal | Owner completes/drains owned scheduled handles before clearing command state; Vault/SignalBus owners retain buffer and queue disposal authority |
| Proof required before GREEN | Fresh compile/import artifact, Burst/job proof, Play Mode route proof, profiler/GC proof, and linked output path with command, timestamp, environment, and result |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY |

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.

- `Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/Automation/Editor/SubmarineAutopilotTunerWindow.cs`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary
This route card is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) (R44 prior internal-residue/exact-route-field/proof-wording correction); R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R37 remains the prior artifact-path/proof-wording/source-counter correction; R36 remains the prior authority-spine/domain-map correction; R35 remains the prior R4/counter-residue correction, and R34 remains the prior source-counter and physical-line refresh, R33 remains the prior R32-residue/source-anchor correction, R32 remains the prior R4/proof-wording correction, R31 remains the prior current-boundary propagation correction, R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, and R28 remains the prior interior-boundary correction. Current static gates: AtlasCheck fails `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only.

No Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, route soak, save/load route, vehicle scene wiring, or visual proof is implied unless this route card links a fresh evidence artifact. `YELLOW` remains the only valid runtime disposition until evidence is attached.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Vault Buffers
Owner-local IDs are declared in `SubmarineAutopilotVaultRoute` to avoid widening the global `BufferID` enum and to preserve compile-wall isolation.

- `AutopilotStates` (`71592`): 64-byte authoritative autopilot target and desired velocity DTO.
- `AutopilotAvoidance` (`71593`): per-vehicle repulsion, flow, and feeler summary.
- `AutopilotFeelerResults` (`71594`): fixed 32-feeler debug output per vehicle.
- `AutopilotWaypoints` (`71595`): fixed waypoint DTO array for async routes.
- `AutopilotRouteRanges` (`71596`): per-submarine route cursor.
- `AutopilotTuning` (`71597`): UI Toolkit editable tuning DTO; `GlobalQualityWeight` is the authored cap and `ResolvedQualityWeight` at offset 120 is the quantized per-schedule quality actually consumed by jobs.
- `AutopilotTelemetryRing` (`71598`): 300-frame black box.
- `AutopilotTelemetryCursor` (`71599`): ring cursor.
- `AutopilotMockSdf` (`71600`): deterministic encoded byte SDF fallback.
- `AutopilotFlowSamples` (`71601`): optional abyssal flow sample grid.
- `AutopilotCsvScratch` (`71602`): cold CSV scratch bytes.
- `AutopilotHandlingProfiles` (`71603`): hashed handling profile table.

## Authority
The autopilot only writes desired velocity and route state. It does not move Transforms, Rigidbody bodies, or AUP state. Kinematic vehicle systems remain movement authority and may consume `AutopilotStateDTO.DesiredVelocity`.

## Route Ingress
External owners can seed routes through `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)`. The method writes a fixed per-submarine slice of `AutopilotWaypoints` based on resolved Vault capacity, initializes `AutopilotRouteRangeDTO`, sets the first `TargetAUP`, uses named active flags for route/waypoint binary records, and fails closed during active job locks. There is no Logistics assembly dependency and no managed waypoint list in the autopilot domain.

## Cadence
The owner registers through `GlobalRegistry` fixed, post-fixed, and slow tick lanes. This remains static source orientation until a proof artifact names owner, producer/consumer phase, capacity/overflow behavior, failure/telemetry behavior, command, timestamp, environment, and output. Fixed tick schedules deterministic Burst jobs when the resolved-quality cadence permits it; skipped/pending fixed ticks accumulate sanitized simulation delta up to 0.25s and the accumulated window is passed into steering clamps only after a solver job is actually scheduled. Post-fixed completes pending handles through an owner-local `JobHandle.IsCompleted`/`Complete` helper; slow tick ingests the cold CSV tuning file only when the route is not locked. Resolved quality is `quantize_0.001(min(HomeostasisBrain.GlobalQualityWeight, AutopilotTuningDTO.GlobalQualityWeight))`, so thermal pressure does not overwrite the authored cap.

## Editor Facade
`SubmarineAutopilotTunerWindow` writes the Vault tuning DTO, exposes an authored quality-cap slider plus resolved-quality readout, assigns default/scout/freighter handling profile hashes to the selected submarine, injects Scene View single targets through plane intersection math, and can generate a three-point dogleg route through `stackalloc Span<AutopilotWaypointDTO>` plus `TryWriteRoute`. These write facades fail closed while the runtime route owns locked buffers or pending job handles. Telemetry readout uses disabled integer/float UI Toolkit fields updated via `SetValueWithoutNotify`; it does not build formatted telemetry strings on each refresh.

## Failure Mode
NaN desired velocity or estimated solver time above 1.0 ms sets telemetry fault flags and triggers dual binary dumps: `Docs/AgentLogs/Dump_SHINOBU_157.bin` for AGENTS compliance and `Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin` for the XML task alias. If Vault locks fail, the owner skips scheduling for that tick instead of blocking the main thread. Lock rollback is transactional: `_lockMask` releases only buffers acquired by this navigator transaction, never the whole route blindly.

## Telemetry
`AutopilotTelemetryEntry` is a 64-byte, 300-frame ring entry containing first AUP, average repulsion, active autopilots, feeler count, flags, estimated microseconds, and state hash.

## Layout Guard
`AutopilotStateDTOLayout.ValidateAll()` is editor-only and checks exact size/offset contracts for state, avoidance, feeler, waypoint, route, tuning, telemetry, and handling profile DTOs. `AutopilotTuningDTO` remains 128 bytes; offset 120 is now `ResolvedQualityWeight` and offset 124 remains padding. Reflection is not part of the player/runtime path.

## Shutdown
`OnDisable` forces pending job completion through the owner-local job completion helper, unlocks only acquired Vault buffer bits, dumps black-box data if faulted, and unregisters all GlobalRegistry tick lanes.

## Guardrails
No NavMesh, no A*, no `Physics.Raycast`, no `Physics.SphereCast`, no managed waypoint nodes in hot paths. SDF probing and steering jobs are Burst deterministic and use continuous resolved quality derived from `HomeostasisBrain.GlobalQualityWeight` and the authored cap; low-quality mode collapses to 5 feelers, 1 nearest-neighbor SDF sample per feeler, no gradient taps, nearest-cell flow sampling, and a quality-curved solver cadence. Handling profiles are FNV-1a hashes in `SubmarineHashID`; cold defaults seed `default`, `scout`, and `freighter`, and CSV can override them.

## Current Verification Blocker
`dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was attempted after CPU and compiler-process guards opened. It failed before SHINOBU_157 compilation because generated project files referenced missing unrelated files at capture time. R37-era generated-project shielding covered the stale generated `Hecton8.Core.csproj` include for `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`; the remaining generated project-file refs still absent are:
- `Assets/_Project/_Archive/HectonWaterPhysics.cs`
- `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`

The generated csproj files also have not been regenerated to include the new SHINOBU_157 source paths. Unity import remains the required next proof step.
