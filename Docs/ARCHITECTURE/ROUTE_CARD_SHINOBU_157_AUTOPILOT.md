# Route Card: SHINOBU_157 Submarine Autopilot



Owner: Echelon 6 Habitat & Vehicles / Autonomous Submarine Navigation



Runtime file: Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs



Review disposition: YELLOW - static source route only. Guarded dotnet compile is blocked by unrelated stale includes; Unity import, Burst compile, profiler, and Play Mode evidence are absent.



## R43 Normalized Route-Card Fields



| Field | Value |



|---|---|



| Route ID | `SHINOBU_157_SUBMARINE_AUTOPILOT` |



| Owner | SHINOBU_157 / SUBMARINE_AUTOPILOT |



| Instrument | GlobalDataVault autopilot buffers `71592..71603`, GlobalRegistry tick lanes, owner-local cold editor facade, and black-box dump route |



| Producer phase | `SIMULATION` autopilot command solve and budgeted attitude/depth correction |



| Consumer phase | `POST_SIMULATION` force-command publication plus `VISUAL_SYNC` debug/gizmo readback when enabled |



| Cadence | Fixed simulation cadence for command truth; visual/debug consumers are quality-gated and must not force runtime completion |



| Capacity | Vault-backed command/state/telemetry lanes; telemetry ring fixed at 300 entries; debug readback bounded by authoring tool cadence |



| Producer/consumer phase | `SIMULATION` autopilot command solve and budgeted attitude/depth correction -> `POST_SIMULATION` force-command publication plus `VISUAL_SYNC` debug/gizmo readback when enabled |



| Cadence/capacity | Fixed simulation cadence for command truth; visual/debug consumers are quality-gated and bounded by authoring cadence; Vault-backed command/state/telemetry lanes with fixed 300-entry telemetry ring |



| Overflow/failure | Invalid or saturated inputs publish bounded safe command output and telemetry; route stays YELLOW until compile/runtime/profiler proof exists |



| Shutdown/disposal | Owner completes/drains owned scheduled handles before clearing command state; Vault/SignalBus owners retain buffer and queue disposal authority |



| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_157.bin` and `Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin` are planned/generated on fault; no existing artifact is implied unless a timestamped runtime trigger and output are linked |



| Proof required before GREEN | Fresh compile/import artifact, Burst/job proof, Play Mode route proof, profiler/GC proof, and linked output path with command, timestamp, environment, and result |



| Review disposition | YELLOW / STATIC_SOURCE_ONLY |



## Source Anchors



Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.



- `Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs`



- `Assets/_Project/Scripts/Physics/Vehicles/Automation/Editor/SubmarineAutopilotTunerWindow.cs`



## R48 Exact Route Field Normalization



Route ID: ROUTE_CARD_SHINOBU_157_AUTOPILOT



Owner: Echelon 6 Habitat & Vehicles / Autonomous Submarine Navigation



Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.



Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.



Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.



Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.



Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.



Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.



Review disposition: YELLOW / STATIC_SOURCE_ONLY.



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



The autopilot only writes desired velocity and route state.

It does not move Transforms, Rigidbody bodies, or AUP state. Kinematic vehicle systems remain movement authority.



## Route Ingress



- External owners can seed routes through `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)`.
- The method writes a fixed per-submarine `AutopilotWaypoints` slice from resolved Vault capacity.
- It initializes `AutopilotRouteRangeDTO`, sets first `TargetAUP`, and uses named active flags for route/waypoint binary records.
- Active job locks fail closed.
- There is no Logistics assembly dependency and no managed waypoint list in the autopilot domain.



## Cadence



- The owner registers through `GlobalRegistry` fixed, post-fixed, and slow tick lanes.
- This remains static source orientation until a proof artifact names owner, producer/consumer phase, capacity/overflow behavior, failure/telemetry behavior, command, timestamp, environment, and output.
- Fixed tick schedules deterministic Burst jobs when resolved-quality cadence permits.
- Skipped/pending fixed ticks accumulate sanitized simulation delta up to `0.25s`.
- Accumulated window reaches steering clamps only after a solver job is scheduled.
- Post-fixed completes pending handles through an owner-local `JobHandle.IsCompleted`/`Complete` helper; slow tick ingests the cold CSV tuning file only when the route is not locked.
- Resolved quality is `quantize_0.001(min(HomeostasisBrain.GlobalQualityWeight, AutopilotTuningDTO.GlobalQualityWeight))`, so thermal pressure does not overwrite the authored cap.


## Editor Facade



- `SubmarineAutopilotTunerWindow` writes the Vault tuning DTO and exposes authored quality cap plus resolved-quality readout.
- It assigns default/scout/freighter handling profile hashes to the selected submarine.
- It injects Scene View single targets through plane intersection math.
- It can generate a three-point dogleg route through `stackalloc Span<AutopilotWaypointDTO>` plus `TryWriteRoute`.
- These write facades fail closed while the runtime route owns locked buffers or pending job handles.
- Telemetry readout uses disabled integer/float UI Toolkit fields updated via `SetValueWithoutNotify`; it does not build formatted telemetry strings on each refresh.



## Failure Mode



- NaN desired velocity or solver time above 1.0 ms sets telemetry faults and requests dual dumps: `Dump_SHINOBU_157.bin` and `Dump_NAVIGATION_SURGEON.bin`.
- No existing dump artifact is implied unless a timestamped runtime trigger and output are linked.
- If Vault locks fail, the owner skips scheduling for that tick instead of blocking the main thread.
- Lock rollback is transactional: `_lockMask` releases only buffers acquired by this navigator transaction, never the whole route blindly.



## Telemetry



`AutopilotTelemetryEntry` is a 64-byte, 300-frame ring entry containing first AUP, average repulsion, active autopilots, feeler count, flags, estimated microseconds, and state hash.



## Layout Guard



`AutopilotStateDTOLayout.ValidateAll()` is editor-only.

It checks exact size/offset contracts for state, avoidance, feeler, waypoint, route, tuning, telemetry, and handling profile DTOs.

`AutopilotTuningDTO` remains 128 bytes: offset `120` is `ResolvedQualityWeight`; offset `124` is padding. No player/runtime reflection.



## Shutdown



`OnDisable` forces pending job completion through the owner-local job completion helper, unlocks only acquired Vault buffer bits, dumps black-box data if faulted, and unregisters all GlobalRegistry tick lanes.



## Guardrails



- No NavMesh, no A*, no `Physics.Raycast`, no `Physics.SphereCast`, no managed waypoint nodes in hot paths.
- SDF probing and steering jobs are Burst deterministic.
- Continuous resolved quality derives from `HomeostasisBrain.GlobalQualityWeight` and authored cap.
- Low quality: 5 feelers, 1 nearest-neighbor SDF sample per feeler, no gradient taps, nearest-cell flow, quality-curved cadence.
- Handling profiles are FNV-1a hashes in `SubmarineHashID`; cold defaults seed `default`, `scout`, and `freighter`, and CSV can override them.



## Current Verification Blocker



Compile attempt:

- Command: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
- Launch condition: CPU and compiler-process guards opened.
- Result: failed before SHINOBU_157 compilation.
- Cause: generated project files referenced missing unrelated files.
- R37 shielded stale `Hecton8.Core.csproj` include for `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` through `Directory.Build.targets`.
- Remaining absent generated refs:



- `Assets/_Project/_Archive/HectonWaterPhysics.cs`



- `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`



The generated csproj files also have not been regenerated to include the new SHINOBU_157 source paths. Unity import remains the required next proof step.
