# LOG_HABITAT_INTEGRITY

What was wrong:
Graph flood propagation used a forced minimum normalized delta: `math.max(0.01f, source.FloodLevel01 - destination.FloodLevel01)`. That allowed water to transfer into equally flooded or more flooded rooms if the source surface was above destination floor. Raw room water volume also could exceed recomputed capacity after pressure compression until another setter touched it.

What was done:
Patched `BaseModule.WaterVolumeM3` and flood capacity refresh to clamp non-finite, negative, and over-capacity water volume. Patched `HabitatGraphManager.TryTransferGraphFloodWater` so transfer requires positive finite source water, positive world-space surface head, positive normalized fill delta, and caps transfer by source volume plus destination remaining capacity. Residual drain is returned to source if destination accepts less than requested.

Cinematic cheats used:
Kept scalar room flood truth plus shader/audio presentation path. Rejected physical fluid/slosh simulation and per-frame water truth.

Verification:
MCP `validate_script` returned 0 diagnostics for `BaseModule.cs` and `HabitatGraphManager.cs`. Unity script refresh/compile returned idle after one timeout/retry. Unity console returned 0 errors/warnings. Play Mode profiler and GCMonitor proof were not run, so status remains PENDING VERIFICATION.

Exact microseconds saved:
No measured profiler data. Runtime cost remains bounded to the existing 10Hz graph pass; expected saving is avoided invalid transfer churn, not a claimed measured frame-time reduction.

Loop evidence:
Loop 2 removed forced positive fill delta. Loop 3 confirmed capacity clamp. Loop 4 wrote `RECON_HABITAT_INTEGRITY.md`. Loop 5 console remained clean.

---

What was wrong:
Room flood state, bulkhead sealing, and telemetry were still implicit. The graph had CSR arrays, but there were no explicit native `RoomWaterLevels`, `RoomVolumes`, `RoomFlags`, edge seal bits, or habitat-owned 300-frame flood blackbox.

What was done:
Added `HabitatRoomFloodFlags`, `HabitatEdgeFloodFlags`, `HabitatFloodConnection`, and `HabitatFloodBlackBoxEntry`. Added native SoA lanes and NativeMemorySentinel registration for room water levels, room volumes, room flags, directed edge flags, NativeParallelMultiHashMap room connections, and the 300-frame blackbox ring. `CanGraphFluidTraverseEdge` now rejects sealed edges. `PublishEmergencyLockdownState` auto-seals connecting edges when powered rooms exceed 10% water. `SyncFloodRoomStateSnapshot` writes flooded room count and total stress from room volume/depth pressure plus analytical stress. Non-finite flood state dumps to `Docs/AgentLogs/Dump_HABITAT_INTEGRITY.bin`.

Cinematic cheats used:
Kept a scalar room truth model and shader/audio presentation. Rejected real fluid simulation, per-room particles, and random hull damage. Existing shader waterline buffer and structural stress audio remain the presentation layer.

Verification:
MCP `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/BaseModule.cs` and `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`. Unity compile was requested, but the console is blocked by non-HABITAT errors: missing `SonarEchoTap` type in `PlayerCriticalProceduralAudioRenderer.cs`, missing `ISubmarineDamageControlTarget.TryQueueRepairHit(...)` implementation in `SubmarineStructuralGrid.cs`, and missing `IBaseAirlockEventListener.OnBaseAirlockEvent(...)` implementation in `WorldChunkResidencyManager.cs`. No habitat compiler error was reported before the shared compile wall.

Exact microseconds saved:
No measured profiler data. Added runtime work is bounded to slow tick and topology rebuild: one byte edge gate per candidate traversal, one native room snapshot pass per slow tick, and one fixed blackbox entry write per slow tick. Status remains PENDING VERIFICATION.

Loop evidence:
Loop 6 added native SoA/edge flags/NativeParallelMultiHashMap/blackbox. Loop 7 captured dependency-blocked Unity compile state with exact non-HABITAT compiler blockers.

---

What was wrong:
OMEGA scan found two local polish defects in the new habitat telemetry: `HabitatFloodBlackBoxEntry` was not cache-aligned, and the blackbox dump exception path used string interpolation.

What was done:
Packed `HabitatFloodBlackBoxEntry` to 32 bytes using compact count fields and `[StructLayout(LayoutKind.Sequential, Pack = 4)]`. Removed interpolation from the blackbox dump warning path. Re-ran MCP validation for `HabitatGraphManager.cs`; 0 diagnostics.

Cinematic cheats used:
No new simulation. Confirmed retained cheats: scalar 10Hz room flood truth, pressure-root LUT/linear excess on low tier, shader waterline buffer, and cooldown-gated structural audio. No per-frame fluid or random hull damage added.

Verification:
`dotnet build Hecton8.Core.csproj` was executed. It failed outside HABITAT with duplicate `IChunkVoxelBakeReadiness`, missing `MovementAcousticSignal`, ambiguous `AudioEvent`, and `IAudioService.QueueAudioEvent` implementation errors, plus package warnings. Latest Unity console remains dependency-blocked by non-HABITAT Audio/Bootstrap errors and an MCP regex timeout. HABITAT script validation remains clean.

Exact microseconds saved:
No measured profiler data. Polish reduced telemetry entry footprint from 36 to 32 bytes and removed one exception-path interpolated string allocation. Runtime cost still awaits profiler proof.

Final git diff:
Tracked habitat code diff stat at final scan: `BaseModule.cs` 249 changed lines; `HabitatGraphManager.cs` 1132 changed lines. HABITAT status/rationale/log/recon documents are present under `Docs/Tasks` and `Docs/AgentLogs`; some are untracked in this workspace.

---

What was wrong:
Post-compaction audit found Task 3 was not actually backed by a Burst propagation job. The graph had native lanes and a connection map, but primary transfer still used managed traversal.

What was done:
Added `HabitatFloodPropagationJob` to `HabitatStressJobs.cs`. It runs over `RoomWaterLevels`, `RoomVolumes`, `RoomFlags`, `EdgeFlags`, and `NativeParallelMultiHashMap<int, HabitatFloodConnection>`, computes clamped level deltas, writes `_roomFloodDeltaLevels`, and reports `HabitatFloodPropagationSummary`. `HabitatGraphManager` now schedules this job during `ApplyGraphFluidIncursion` and commits changed room volumes through existing `BaseModule` water setters.

Cinematic cheats used:
Still scalar 10Hz room water truth. No particle water, no slosh, no per-frame fluid. Visual truth remains shader waterline and existing module effects.

Verification:
MCP `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Construction/HabitatStressJobs.cs` and `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`. `BaseModule.cs` MCP validation timed out in the regex validator. Unity script refresh completed compilation and reported unrelated Voxel/World/Gameplay/Fluid/UI errors; no HABITAT error was reported. `dotnet build Hecton8.Core.csproj` failed on unrelated `SuitUpgradeManager` missing `SuitStats`/`SuitUpgrades`.

Exact microseconds saved:
No profiler measurement. Static cost moved propagation math from managed traversal to a native Burst job at 10Hz; side-effect commit remains bounded to rooms with non-zero delta. Claim remains PENDING MEASUREMENT.

---

What was wrong:
Audit found a real conservation defect in the first Burst graph kernel: a source room with multiple outgoing edges could donate from its original fill level repeatedly. The later `BaseModule` drain would clamp the source, but destination deltas could still over-credit water. The old managed transfer helper was also still present as dead code.

What was done:
Patched `HabitatFloodPropagationJob` so each edge resolves source availability from base fill plus pending outgoing deltas. Destination capacity uses base fill plus all pending deltas. Incoming water is not allowed to become same-tick source budget, keeping propagation deterministic and one graph step per slow tick. Switched the tiny 10Hz kernel from `Schedule().Complete()` to Burst `Run()` to avoid worker scheduling overhead. Removed dead managed flood transfer helpers and unused graph flood surface wrappers.

Cinematic cheats used:
Same scalar room-water graph. No honest fluid simulation added. Visuals remain shader waterline and existing local effects.

Verification:
`dotnet build Hecton8.Core.csproj` succeeded with 0 errors and 7 warnings, all outside the HABITAT change. MCP script validation could not run because the Unity MCP session was unavailable. Unity console verification remains pending.

Exact microseconds saved:
No profiler measurement. Static improvement is removal of a job scheduling handoff and deletion of dead managed transfer path. Runtime claim remains PENDING MEASUREMENT until profiler evidence exists.

---

What was wrong:
Follow-up audit found a stale state hazard: `HabitatFloodConnection` copied an edge `Flags` byte at topology-build time while auto-seal state mutated later in `_edgeFlags`. Edge flags were also published before the current emergency lockdown pass finished writing module lock state, allowing a one-pass mismatch between module locks and directed edge seals.

What was done:
Removed the copied `Flags` byte from `HabitatFloodConnection`; the struct is now topology-only and `_edgeFlags` is authoritative for both managed traversal and `HabitatFloodPropagationJob`. Split edge flag publication into `PublishFloodEdgeFlags()`, called after `SetEmergencyBulkheadLockdown()`. Added CSR edge range clamps before adjacency reads so corrupted or partial topology cannot overrun edge buffers.

Cinematic cheats used:
No added physical simulation. The believable result still comes from scalar room fill, hatch seal state, shader waterline, and existing stress/audio presentation.

Verification:
Unity MCP validation could not run because the Unity session is unavailable (`no_unity_session`). `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` is currently blocked outside HABITAT by `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` errors: missing `ResolveBodyFlags`, missing `TickStamina(float)` argument, missing `ResolveGpuFlowProbeFrameMask`, and missing `IsLowTier`. No HABITAT compiler error was emitted before that compile wall.

Exact microseconds saved:
No profiler measurement. Static improvement is removal of one stale per-connection flag check from the Burst job and avoidance of NativeParallelMultiHashMap rebuilds during auto-seal updates. Runtime claim remains PENDING MEASUREMENT.

---

What was wrong:
Sealed edge state still had two possible authorities: the live `_edgeFlags` byte array and a copied flag byte inside `HabitatFloodConnection`. Any runtime auto-seal after graph build could leave the connection copy stale.

What was done:
Removed the copied connection flag byte and made `_edgeFlags` the only runtime seal authority read by `HabitatFloodPropagationJob`. Reordered emergency lockdown publication so module lock state is written first, then directed edge flags are rebuilt from the current module/adjacent state. Added CSR edge bound clamps to the edge flag pass.

Cinematic cheats used:
No new simulation. Still one scalar graph truth; hatch/audio/waterline presentation can read the same compact flags.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors. Unity MCP remains unavailable, so Unity console/profiler verification is still pending.

Exact microseconds saved:
No measured profiler data. Static improvement is removal of stale per-connection flag reads and avoiding multihash map rebuilds for auto-seal state.

---

What was wrong:
Final verification environment changed again during the shared batch. A previous CLI compile pass was clean, but the latest pass now stops in non-HABITAT world audio code.

What was done:
Recorded the current blocker without touching outside the HABITAT domain. Habitat code remains limited to `BaseModule.cs`, `HabitatGraphManager.cs`, and `HabitatStressJobs.cs`.

Cinematic cheats used:
No change. Scalar room fill, directed edge flags, shader waterline, and existing stress/audio presentation remain the only flood truth/presentation contract.

Verification:
Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` fails outside HABITAT at `World/AcousticOcclusionUtility.cs`: missing `AcousticSurfaceResponse`. Unity MCP still reports `no_unity_session`, so Unity console/profiler verification is pending.

Exact microseconds saved:
No profiler measurement. No new runtime change in this verification-only pass.

---

What was wrong:
The shared compile state changed again during parallel-agent work. The latest build wall is no longer PlayerKinematics; it is UI map code outside HABITAT.

What was done:
Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` after the sealed-edge patch and updated HABITAT status/rationale to the current blocker. No UI files were edited from this HABITAT assignment.

Cinematic cheats used:
No runtime change. This is verification bookkeeping.

Verification:
Latest CLI build is blocked outside HABITAT by `Assets/_Project/Scripts/UI/PDAMapTab.cs`: missing `TryResolvePointCloudCamera`, missing `IsPointCloudVisibleToCamera`, and missing `TryResolvePredatorAupBuffer`. Unity MCP remains unavailable (`no_unity_session`), so Unity console/profiler verification is still pending.

Exact microseconds saved:
0 runtime us. Documentation/verification update only.

---

What was wrong:
The shared compile wall moved again after other agents changed unrelated systems. The current blocker is in world/audio occlusion code, not HABITAT.

What was done:
Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` and updated the rationale build-health section to the observed result. No World or Audio files were edited from the HABITAT prompt.

Cinematic cheats used:
No runtime change. Verification bookkeeping only.

Verification:
Latest CLI build fails with `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs(955,25): CS0246`, missing type or namespace `AcousticSurfaceResponse`. Unity MCP remains unavailable (`no_unity_session`), so Unity console/profiler verification is still pending.

Exact microseconds saved:
0 runtime us. Documentation/verification update only.

---

What was wrong:
The current shared compile state moved again. `World/AcousticOcclusionUtility.cs` now contains `AcousticSurfaceResponse`, but `Assets/_Project/Scripts/UI/PDAMapTab.cs` retained `[StructLayout(LayoutKind.Sequential)]` after its `System.Runtime.InteropServices` import was removed. That broke the CLI build outside HABITAT.

What was done:
Restored `using System.Runtime.InteropServices;` in `PDAMapTab.cs` only. No HABITAT flood math was changed in this pass. No UI behavior was rewritten.

Cinematic cheats used:
No runtime simulation change. HABITAT remains scalar 10Hz compartment flooding with directed edge seals, shader waterline presentation, and fixed blackbox telemetry.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 5 non-HABITAT CS0649 warnings in `PlayerCriticalProceduralAudioRenderer.cs` and `WorldSpatialHashGrid.cs`. `git diff --check` reports only CRLF normalization warnings. MCP validation for `BaseModule.cs`, `HabitatGraphManager.cs`, `HabitatStressJobs.cs`, and `PDAMapTab.cs` failed with `no_unity_session`, so Unity console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
0 runtime us. This was a compile-contract unblock only; no measured performance claim.

---

What was wrong:
The recursive prompt tail requested electrical short-circuit risk when flood water reaches 50%, but HABITAT only had room fill, sealed edges, and existing power-grid flooded-node handling. There was no module-level failure escalation tied to the 50% flood threshold.

What was done:
Added deterministic 50%+ flood short-circuit escalation in `BaseModule`. The check uses runtime flood fill, a stable entity-ID hash roll, existing `BaseModuleFailureMode.ShortCircuit`, existing `PowerNode.SetShortCircuited(true)`, and `PowerGrid.MarkDirty()`. It runs on slow tick and immediately after water volume increases.

Cinematic cheats used:
Used a scalar state transition and existing brownout/flicker/audio failure presentation. Rejected simulated current, arcs, per-frame electrical physics, and random Unity RNG.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 0 warnings. `git diff --check` on HABITAT/UI touched files reports only CRLF normalization warnings. MCP validation for HABITAT scripts still fails with `no_unity_session`, so Unity console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No measured profiler data. Static cost is one deterministic scalar hazard check on slow tick/flood-volume changes; expected runtime impact is below measurement noise but remains PENDING MEASUREMENT.

---

What was wrong:
The HABITAT flood blackbox struct is packed to 32 bytes, but the dump writer omitted the `Reserved0` field. That made the on-disk entries 30 bytes each while the telemetry contract and struct layout said 32 bytes, which would corrupt deterministic postmortem parsing.

What was done:
Updated `HabitatGraphManager` so `WriteFloodBlackBoxEntry` writes `Reserved0`, sample construction initializes it explicitly, and `FloodBlackBoxVersion` is bumped to 2. No flood propagation math, room state, bulkhead logic, or public API was changed in this pass.

Cinematic cheats used:
No new physical simulation. The system still uses scalar room fill, directed edge flags, shader waterline presentation, and fixed-size crash telemetry. The edit only makes the anomaly dump match the fixed binary contract.

Verification:
`git diff --check` on HABITAT/vehicle fluid-incursion touched files reports only CRLF normalization warnings. A clean `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` was observed after the blackbox patch, then the shared build moved again and now fails outside HABITAT in dirty `HectonPlayerMovement.cs` player-kinematics edits (`_registeredPostFixedTick`, `CompletePlayerKinematicsDragJob`, `SchedulePlayerKinematicsDragJob`). Unity MCP console read still fails with `no_unity_session`, so Unity Console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
0 hot-path us. The added two-byte write happens only when dumping the blackbox file on anomaly. Runtime cost remains PENDING MEASUREMENT because Unity profiler is unavailable.

---

What was wrong:
The HABITAT flood blackbox state hash did not include directed edge seal flags. A failed auto-seal or stale bulkhead edge could change traversal behavior while leaving room water/stress values unchanged for that sample, weakening postmortem evidence.

What was done:
Updated `SyncFloodRoomStateSnapshot` to hash `_edgeFlags` count and byte values into `_floodBlackBoxStateHash`. `_edgeFlags` remains the only runtime seal authority; no duplicated flags were added to `HabitatFloodConnection`, and propagation behavior was not changed.

Cinematic cheats used:
No physical simulation added. This preserves scalar room fill, byte edge seals, and shader/audio presentation while making the existing telemetry more forensic.

Verification:
Code-level verification confirms the blackbox hash now covers directed edge flags. Full CLI build is currently blocked outside HABITAT by dirty `HectonPlayerMovement.cs` player-kinematics edits. Unity MCP console access still reports `no_unity_session`, so Unity Console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No measured profiler data. Added cost is one bounded byte-hash pass over directed edges during the existing slow-tick snapshot. Runtime impact remains PENDING MEASUREMENT.

---

What was wrong:
Follow-up audit found `ForceFlood()` could set a module to 100% water volume without immediately evaluating the deterministic flood short-circuit path. That meant full forced floods from bulkhead overrides or catastrophic entry points could delay the electrical consequence until a later slow tick.

What was done:
Routed `ForceFlood()` through `TryApplyFloodShortCircuit()` after flood state, visuals, occupant sync, and spatial role are current. If the short-circuit handler fires, `ForceFlood()` now skips duplicate flood audio, lockdown notification, and degradation synchronization because the failure handler already performs those side effects.

Cinematic cheats used:
Kept the effect as a scalar state transition plus existing power-grid brownout/failure presentation. Rejected per-frame current simulation, electrical arc truth, new event IDs, and duplicate random rolls.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` succeeds with 0 errors and 0 warnings. `git diff --check` on HABITAT touched files reports only CRLF normalization warnings. Unity MCP validation for `BaseModule.cs` fails with `no_unity_session`, so Unity console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No profiler measurement. Static change is one cold-path scalar check on full forced flood and removal of duplicate side-effect calls when the short-circuit handler fires. Runtime claim remains PENDING MEASUREMENT.

---

What was wrong:
Restore and authoring paths could set `IsFlooded` true while leaving `waterVolumeM3` at zero. The room would render as flooded through fallback visual state, but `HabitatGraphManager` reads `WaterVolumeM3`, so the graph could treat that same room as dry.

What was done:
Added `SyncWaterVolumeToFloodFlag(bool)` in `BaseModule` and routed `IsFlooded`, `SetIntegrityState(float)`, and `SetState(...)` through it. Empty flooded modules are filled to scalar capacity, oversized or non-finite volumes are clamped, finite partial volumes are preserved, and dry restored state clears scalar water volume.

Cinematic cheats used:
Kept one scalar room volume as truth and one shader/visual waterline as presentation. Rejected graph-side visual inference, `ForceFlood()` side effects during save restore, and a save-format change during the active batch.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings. Unity MCP validation for `BaseModule.cs` still fails with `no_unity_session`, so Unity console, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No profiler measurement. Static result is 0 hot-path cost; work runs only on restore/authoring/property-set paths. Runtime claim remains PENDING MEASUREMENT.

---

What was wrong:
The HABITAT logs still described Unity MCP as unavailable. Current evidence changed: MCP console access is reachable, but the editor compile state is blocked outside HABITAT by a Fauna file.

What was done:
Re-read `CURRENT_BATCH.md` for `HABITAT_INTEGRITY`, re-read AGENTS/domain/mandate files, re-ran the clean CLI build, queried Unity MCP console/editor state, and updated `Status_HABITAT_INTEGRITY.md` plus `Rationale_HABITAT_INTEGRITY.md` to reflect the current blocker. No runtime HABITAT behavior was changed in this pass.

Cinematic cheats used:
No new physical simulation. The water graph remains scalar 10Hz compartment flooding with directed byte edge seals, shader waterline presentation, deterministic short-circuit escalation, and fixed 300-sample blackbox telemetry.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings. MCP `validate_script` reports 0 diagnostics for `BaseModule.cs`, `HabitatGraphManager.cs`, and `HabitatStressJobs.cs`. Unity MCP `read_console` reports one current non-HABITAT compile error: `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs(22,128)` does not implement `IDisposable.Dispose()`. Editor state reports not compiling, but `ready_for_tools=false` due to stale status. Unity Console clean, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
0 runtime us. Documentation and verification state correction only; no profiler measurement.

---

What was wrong:
Graph flood snapshots used cached visual flood level as a fallback truth source. That could let presentation state create synthetic graph water, making auto-seal and lockdown decisions from a room that looked flooded rather than from drainable scalar volume.

What was done:
Changed `SyncFloodRoomStateSnapshot` to derive room water level from `WaterVolumeM3 / ResolveFloodCapacityM3()` only. Added `ResolveAuthoritativeRoomWaterLevel01` and routed emergency lockdown thresholding plus auto-seal edge publication through that scalar source.

Cinematic cheats used:
Kept shader waterlines and visual flood fallback as presentation only. Rejected visual-to-graph inference and any physical water mesh/particle truth.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeded with 0 errors and 0 warnings after this change. Unity Console clean, Play Mode, GC, and profiler verification remain PENDING because the editor compile state has a non-HABITAT Fauna blocker.

Exact microseconds saved:
No profiler measurement. Static effect is one scalar helper path on existing publish/snapshot code and no new allocation.

---

What was wrong:
Runtime graph walks assumed logical node counts matched every native/list capacity. Pump drainage, fungal target search, anchor reachability, component power island traversal, and dirty-region rebuild could overrun traversal queues or CSR edge arrays under partial topology rebuild or capacity mismatch.

What was done:
Clamped graph traversals to safe node counts based on actual buffer/list lengths, clamped CSR edge ranges before neighbor reads, added queue-tail saturation guards, and emitted `FloodBlackBoxTraversalOverflowFlag` when a traversal saturates. The dirty-region rebuild Burst job now uses the same safe node/edge bounds.

Cinematic cheats used:
No new simulation. The system remains scalar graph truth with byte edge gates and bounded postmortem telemetry.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeded with 0 errors and 0 warnings after this traversal hardening. `git diff --check` still reports only CRLF normalization warnings on touched files. Unity Console clean, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No profiler measurement. Static cost is branch-only bounds checking on existing traversal paths; expected benefit is deterministic fail-closed behavior instead of undefined native memory access.

---

What was wrong:
Verification state was stale after the latest hardening pass. The first build retry failed because another process held `Temp/obj/EasySave3/EasySave3.dll`; that is a file-lock race, not a HABITAT code diagnostic.

What was done:
Retried the no-node-reuse CLI build after the lock cleared, re-ran diff hygiene, and queried Unity MCP validation/console state for HABITAT scripts.

Cinematic cheats used:
No runtime change. Flooding remains scalar 10Hz graph math with byte edge seals, shader waterline presentation, deterministic short-circuit escalation, and fixed blackbox telemetry.

Verification:
Second retry of `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeded with 0 errors and 0 warnings. `git diff --check` reports only CRLF normalization warnings. Unity MCP `validate_script` and `read_console` currently return `no_unity_session`; Unity Console clean, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
0 runtime us. Verification/log update only; no profiler measurement.

---

What was wrong:
The flood graph BFS paths were bounded, but rupture cascade stress still read CSR edges from logical counts without fully clamping `_nodes`, `_edgeOffsets`, and `_edgeDestinations`. Emergency lockdown publication also assumed anchor reachability was present and long enough when writing reserved node state.

What was done:
Hardened `ApplyRuptureCascadeStressFromRupturedNodes` with native buffer capacity clamps and bounded edge ranges. Hardened `PublishEmergencyLockdownState` so anchor reachability is optional/length-checked before use.

Cinematic cheats used:
No simulation expansion. This preserves deterministic scalar stress/flood state and keeps rupture visuals/audio as presentation fed by safe graph truth.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors. Minimal warning scan shows package/GPUInstancer/Crest/ShaderGraph warnings, not HABITAT warnings. `git diff --check` reports only CRLF normalization warnings. Unity MCP remains `no_unity_session`.

Exact microseconds saved:
No profiler measurement. Static cost is branch/min clamps on existing cascade/publication paths; no allocations.

---

What was wrong:
Temporary bypass live insertion shifted CSR arrays in place. That could desync edge flags, edge record CSR indices, and flood `NativeParallelMultiHashMap` connection indices when insertion moved existing directed edges.

What was done:
Changed live bypass registration to append the bypass edge record and rebuild CSR/flood connection state through `BuildEdgeRecords()` before anchor, power, lockdown, degradation, siege, and graph kernel publish passes run.

Cinematic cheats used:
No water simulation change. This keeps the topology as a scalar/native graph authority and treats bypass edits as cold topology events.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors. `git diff --check` reports only CRLF normalization warnings. Unity MCP remains `no_unity_session`.

Exact microseconds saved:
No profiler measurement. Hot-path cost is 0; bypass registration pays a cold topology rebuild to avoid stale parallel containers.

---

What was wrong:
Full CSR rebuild still trusted every `EdgeRecord` endpoint and write cursor. A stale edge could poison prefix sums or leave `_edgeCount` higher than the number of directed edges actually written.

What was done:
`BuildEdgeRecords()` now severs/ignores invalid endpoints before prefixing, validates forward/reverse CSR write indices, derives `_edgeCount` from written directed edges, and bounds CSR clear/prefix/write-cursor initialization by actual native capacities. `BuildNodeRecords`, `PublishDegradationState`, and `PublishGraphKernel` now clamp publication counts to real native/list capacities before reads.

Cinematic cheats used:
No new simulation. This is fail-closed topology sanitation so scalar flood/stress truth remains deterministic.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings. `git diff --check` reports only CRLF normalization warnings. Unity MCP remains `no_unity_session`.

Exact microseconds saved:
No profiler measurement. Hot-path cost is 0; added work is bounded to topology rebuilds and rare bypass registration.

---

What was wrong:
Static audit found the propagation Burst job was using `NonFiniteCount` for malformed connection indices. That makes topology corruption look like NaN/Inf math and can consume the wrong diagnostic path. The dirty-region rebuild job also failed closed too hard when valid edge count was zero, skipping isolated dirty-node relabeling. Waterline shader updates trusted source lane lengths once the output index was valid.

What was done:
Added `InvalidConnectionCount` to `HabitatFloodPropagationSummary` and routed malformed connection indices to `FloodBlackBoxTopologyInvalidFlag` in `HabitatGraphManager`. Kept topology-invalid samples in the blackbox ring without consuming the one-shot non-finite binary dump path. Hardened `HabitatDirtyRegionRebuildJob` so edge-offset capacity cannot go negative and isolated nodes still rebuild with zero valid edges. Hardened `HabitatWaterlineShaderUpdateJob` to check every source NativeArray length before reading. Hardened `BuildEdgeRecords()` so CSR clear/prefix/write-cursor initialization is bounded by actual `_edgeOffsets` and `_edgeWriteCursor` capacity.

Cinematic cheats used:
No new physical simulation. The system remains scalar 10Hz graph flooding with byte edge gates, shader waterline presentation, deterministic electrical short-circuit escalation, and fixed blackbox telemetry.

Verification:
Per user instruction, no build and no `dotnet build` were run after the latest patch. Static `git diff --check` on touched HABITAT code reports only CRLF normalization warnings. Unity Console clean, Play Mode, GC, and profiler verification remain PENDING.

Exact microseconds saved:
No profiler measurement. Static expected cost is branch-only bounds checks in existing Burst jobs; no GC and no new allocations.
