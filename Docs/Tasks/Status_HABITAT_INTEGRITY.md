# HABITAT_INTEGRITY Status

Prompt: HABITAT_INTEGRITY
Role: HABITAT_ARCHITECT
Domain: ECHELON 6 Habitat & Vehicles / Fluid Incursion
Status baseline: PENDING VERIFICATION
Compile status: PENDING AFTER LATEST STATIC PATCH. Per user instruction, no build or `dotnet build` was run after the latest HABITAT graph hardening edits. Earlier CLI success after prior CSR hardening is historical only and is not claimed as current verification. Latest verification is static hygiene only: `git diff --check` reports only CRLF normalization warnings on touched HABITAT/doc files. Unity Console clean, Play Mode, GC, and profiler verification remain PENDING.

## Task Checklist

- [x] 1. ROOM S.O.A. GRAPH
  - DOD practice: Added native SoA lanes in `HabitatGraphManager`: `RoomWaterLevels`, `RoomVolumes`, and `RoomFlags`, registered with `NativeMemorySentinel`.
  - Alternative rejected: Managed `RoomNode` objects; would add GC and pointer chasing.
  - Microsecond estimate: PENDING MEASUREMENT; static cost is one 10Hz node scan.
- [x] 2. BREACH NODE
  - DOD practice: Room flags now set `Breached`; ingress already uses pressure delta from external depth versus internal air pocket pressure.
  - Alternative rejected: Constant leak rate; ignores depth/pressure and lies in abyssal bases.
  - Microsecond estimate: PENDING MEASUREMENT; LUT/approx pressure-root path stays scalar.
- [x] 3. FLUID PROPAGATION JOB
  - DOD practice: Added `HabitatFloodPropagationJob` Burst `IJob`; it reads `RoomWaterLevels`, `RoomVolumes`, `RoomFlags`, directed edge flags, and `NativeParallelMultiHashMap<int, HabitatFloodConnection>`, then writes native room delta levels for the manager to commit through existing `BaseModule` setters. Follow-up audit fixed multi-edge source overdraw by resolving pending outgoing deltas before each edge. Restore/authored flood state now synchronizes `waterVolumeM3` with the flooded flag so graph snapshots do not read visually flooded rooms as dry.
  - Alternative rejected: Managed BFS as the primary solver; dead managed transfer fallback was removed after the job became authoritative.
  - Microsecond estimate: PENDING MEASUREMENT; static cost is one synchronous Burst `Run()` at 10Hz plus a managed side-effect commit for changed rooms only.
- [x] 4. BULKHEAD BITMASK
  - DOD practice: Added `NativeArray<byte> EdgeFlags`; `Sealed` edges are rejected by `CanGraphFluidTraverseEdge` and by `HabitatFloodPropagationJob`. Follow-up audit removed the stale copied `Flags` byte from `HabitatFloodConnection` so `_edgeFlags` is the only runtime truth. Edge seal telemetry now hashes directed edge flags, and traversal overflow is recorded as a blackbox flag.
  - Alternative rejected: Reading only per-module bulkhead state or duplicated per-connection flag snapshots; both can desync from directed sealed connections.
  - Microsecond estimate: PENDING MEASUREMENT; one byte check per candidate edge.
- [x] 5. AUTO-SEAL LOGIC
  - DOD practice: `PublishEmergencyLockdownState` applies module lockdown first, then `PublishFloodEdgeFlags` seals connecting edge flags when either endpoint has authoritative scalar water level > 0.1 and power or an endpoint is bulkhead-locked.
  - Alternative rejected: Manual-only hatch lockdown, publishing edge flags before module lock state is updated, or using cached visual flood level as graph truth; all allow desync or synthetic water flow.
  - Microsecond estimate: PENDING MEASUREMENT; runs on existing graph publish path.
- [x] 6. HULL STRESS SOLVER
  - DOD practice: `SyncFloodRoomStateSnapshot` calculates `BaseTotalStress` from analytical stress plus depth pressure against room volume/fill.
  - Alternative rejected: Per-particle water stress; no determinism, no cheap-device path.
  - Microsecond estimate: PENDING MEASUREMENT; O(room count) at slow tick.
- [x] 7. INTEGRITY DEGRADATION
  - DOD practice: Verified existing pressure buckling, bulkhead flood stress, and deterministic analytical breach targeting feed `BaseModule.ApplyDamage`/rupture paths. Follow-up audit bounded rupture-cascade CSR traversal by native buffer capacities before applying neighbor stress.
  - Alternative rejected: Truly random outer hull damage; deterministic seeded breach is replayable and debug-safe.
  - Microsecond estimate: PENDING MEASUREMENT; existing degradation path retained.
- [x] 8. PUMP MODULE COUPLING
  - DOD practice: Verified existing `WaterPumpModule` registry and `ApplyWaterPumpDrainage` drain connected flood components through graph edges. Follow-up audit bounded pump BFS by actual native buffer capacities and clamps CSR ranges before neighbor traversal.
  - Alternative rejected: New pump dependency, per-module search, or trusting node count after topology corruption; existing active-pump registry plus bounded BFS is enough.
  - Microsecond estimate: PENDING MEASUREMENT; drain remains bounded by active pump count and graph traversal.
- [x] 9. VISUAL FAKE WATER LEVEL
  - DOD practice: Verified `BaseModule.PublishActiveModuleWaterLevelsToShader` publishes flood level and water surface data to the global shader buffer.
  - Alternative rejected: Mesh/particle water simulation per room; shader/local volume fake is the correct presentation path.
  - Microsecond estimate: PENDING MEASUREMENT; buffer upload remains capped by module shader capacity.
- [x] 10. OXYGEN DEPRIVATION
  - DOD practice: `RoomFlags` mark `OxygenDisabled` at >= 0.8 water; existing carbon filter logic disables scrubber availability while flooded/fire or unpowered.
  - Alternative rejected: New hard dependency on atmosphere internals; BaseModule life-support API is the correct boundary.
  - Microsecond estimate: PENDING MEASUREMENT; existing slow tick life-support path retained.
- [x] 11. AUDIO CREAKS
  - DOD practice: Verified `ProceduralAudioEvents.RaiseStructuralStressTriggered` fires from analytical and joint shear stress thresholds.
  - Alternative rejected: Per-edge creak emitters; would spam audio and allocate authoring overhead.
  - Microsecond estimate: PENDING MEASUREMENT; cooldown-gated event path retained.
- [x] 12. MATH LOD 10HZ
  - DOD practice: Verified `ConstructionManager` runs habitat hydrodynamic stress from `SlowTickDeltaTime = 0.1f`.
  - Alternative rejected: Per-frame flooding simulation.
  - Microsecond estimate: 0 us added; existing cadence retained.
- [x] 13. ZERO-GC NATIVE GRAPH
  - DOD practice: Added `NativeParallelMultiHashMap<int, HabitatFloodConnection>` for directed room connections; no OOP node classes added.
  - Alternative rejected: `Dictionary<int, List<Edge>>` runtime graph; violates zero-GC hot-path rules.
  - Microsecond estimate: PENDING MEASUREMENT; native map is rebuilt only with topology.
- [x] 14. RECON LEGACY FLOODING UPDATE
  - DOD practice: Ran filtered `rg` scan for habitat/base flooding `Update()` offenders and wrote `Docs/AgentLogs/RECON_HABITAT_INTEGRITY.md`.
  - Alternative rejected: Manual IDE search; CLI scan is reproducible.
  - Microsecond estimate: 0 runtime us; documentation/static scan only.
- [x] 15. BLACKBOX TELEMETRY
  - DOD practice: Added 300-frame `NativeArray<HabitatFloodBlackBoxEntry>` with `FloodedRoomCount`, `BaseTotalStress`, state hash, and non-finite dump to `Docs/AgentLogs/Dump_HABITAT_INTEGRITY.bin`. Follow-up audits fixed the dump writer to include the `Reserved0` field, bumped the binary version to 2, and hashed directed `_edgeFlags` into the blackbox state hash so sealed-edge changes are visible in postmortem telemetry.
  - Alternative rejected: Global telemetry event only; it would not preserve last-300-frame crash context.
  - Microsecond estimate: PENDING MEASUREMENT; one fixed entry write per slow tick.
- [x] 16. FLOOD ELECTRICAL SHORT-CIRCUIT ESCALATION
  - DOD practice: Added a deterministic module-level short-circuit roll when runtime flood fill reaches 50%; the roll is stable per module and escalates to guaranteed trip at full flood. When it fires, existing `BaseModuleFailureMode.ShortCircuit` and `PowerNode.SetShortCircuited(true)` carry gameplay/power consequences. Follow-up audit routed `ForceFlood()` through the same path so full forced flood cannot bypass the electrical consequence.
  - Alternative rejected: Unity random chance, per-frame electrical simulation, or new event IDs; all would reduce determinism or add cross-domain noise.
  - Microsecond estimate: PENDING MEASUREMENT; static cost is one scalar hash/chance check on slow tick and flood-volume writes only.

## Loop Log

- Loop 0: Prompt extracted from CURRENT_BATCH.md by CLI. Status and rationale initialized. Core code not touched yet.
- Loop 1: Read mandates, inspected habitat graph flooding, patched transfer clamp/head math, validated scripts with MCP, refreshed Unity compile path, console returned 0 errors/warnings.
- Loop 2: Re-read patched transfer code; confirmed the forced `math.max(0.01f, source-destination)` pattern is gone.
- Loop 3: Re-read capacity clamp path; confirmed water volume is clamped after flood capacity recomputation.
- Loop 4: Ran legacy flooding `Update()` recon scan; no filtered offenders found.
- Loop 5: Re-checked Unity console after compile refresh; 0 errors/warnings reported.
- Loop 6: Added native room SoA, native edge flags, NativeParallelMultiHashMap connections, and habitat blackbox. MCP script validation returned 0 diagnostics for `BaseModule.cs` and `HabitatGraphManager.cs`.
- Loop 7: Unity compile request became dependency-blocked by non-HABITAT errors in Audio/Submarine/World. No habitat script diagnostics were reported. Status remains PENDING VERIFICATION.
- Loop 8: Executed OMEGA anti-bloat pass. Aligned habitat blackbox entries to 32 bytes, removed dump-path string interpolation, revalidated `BaseModule.cs` and `HabitatGraphManager.cs` with 0 diagnostics, and ran `dotnet build Hecton8.Core.csproj`; build failed on non-HABITAT World/Core/Bootstrap/Audio contract errors plus package warnings. Latest Unity console still has non-HABITAT Audio/Bootstrap errors and an MCP regex timeout.
- Loop 9: Re-read implementation after context transition and found Task 3 was not job-backed. Added `HabitatFloodPropagationJob`, `_roomFloodDeltaLevels`, and `_floodPropagationSummary`; MCP validation returned 0 diagnostics for `HabitatStressJobs.cs` and `HabitatGraphManager.cs`. Unity refresh compiled to unrelated Voxel/World/Gameplay/Fluid/UI blockers; dotnet build failed on unrelated Suit upgrade symbols.
- Loop 10: Re-audited propagation math. Fixed source overdraw across multiple outgoing edges, prevented same-tick incoming-water cascade from creating order-dependent free transfer, removed dead managed transfer helpers, switched tiny 10Hz solve from `Schedule().Complete()` to synchronous Burst `Run()`, and removed unused graph flood surface helpers. `dotnet build Hecton8.Core.csproj` succeeded with 0 errors and 7 external warnings. Unity MCP session unavailable; Unity console still PENDING.
- Loop 11: Re-audited sealed-edge authority. Removed stale per-connection flag copy, made `_edgeFlags` authoritative for the Burst job, moved edge-flag publication after emergency lockdown state writes, and added CSR edge bounds clamps. Unity MCP validation is unavailable (`no_unity_session`). Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` is blocked outside HABITAT by missing `AcousticSurfaceResponse` in `World/AcousticOcclusionUtility.cs`.
- Loop 12: Re-checked the shared compile state after parallel-agent edits. `AcousticSurfaceResponse` is now present. The next wall was `PDAMapTab` losing `System.Runtime.InteropServices` while retaining `[StructLayout]`; restored the missing using as a minimal cross-domain compile unblock. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 5 external CS0649 warnings. MCP validation for HABITAT/UI scripts still returns `no_unity_session`.
- Loop 13: Implemented the prompt-tail flood short-circuit escalation. `BaseModule` now evaluates a deterministic 50%+ flood hazard roll, triggers the existing short-circuit failure mode, marks the attached power node shorted, and dirties the power grid. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 0 warnings. MCP validation still returns `no_unity_session`.
- Loop 14: Re-audited forced flood entry points. `ForceFlood()` now evaluates the same deterministic short-circuit path used by graph ingress and avoids duplicate flood audio/notification/state sync when the short-circuit handler already performed those side effects. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` succeeds with 0 errors and 0 warnings. MCP validation still returns `no_unity_session`.
- Loop 15: Re-audited HABITAT blackbox serialization. Fixed `WriteFloodBlackBoxEntry` to write `Reserved0`, explicitly initialized that field in samples, and bumped `FloodBlackBoxVersion` to 2. `git diff --check` reports only CRLF normalization warnings. A clean `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` was observed after one transient GPUInstancer file-lock retry, but the latest build now fails outside HABITAT in dirty `HectonPlayerMovement.cs` player-kinematics edits (`_registeredPostFixedTick`, `CompletePlayerKinematicsDragJob`, `SchedulePlayerKinematicsDragJob`). Unity MCP console read still returns `no_unity_session`.
- Loop 16: Re-audited load/authoring flood state. `IsFlooded`, `SetIntegrityState(float)`, and `SetState(...)` now synchronize scalar water volume to the flood flag, preserving finite partial volumes and filling empty flooded modules to capacity for graph snapshots. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings. MCP validation still returns `no_unity_session`.
- Loop 17: Re-audited blackbox state coverage. Added `_edgeFlags` count and bytes to the flood state hash so bulkhead/auto-seal transitions alter the postmortem signature even if room volumes are unchanged. Latest CLI compile is clean with `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false`; Unity MCP console is reachable and reports a current non-HABITAT Fauna compile error in `LeviathanTentacleVerletSolver.cs`, so editor verification remains blocked/pending.
- Loop 18: Final hygiene pass after doc correction. `git diff --check` reports only CRLF normalization warnings on HABITAT code files, MCP `validate_script` reports 0 diagnostics for the three HABITAT scripts, and the no-node-reuse CLI build remains clean. Unity editor remains blocked/pending on the non-HABITAT Fauna compile error.
- Loop 19: Re-audited graph flood truth. `SyncFloodRoomStateSnapshot`, emergency lockdown thresholding, and auto-seal checks now use authoritative scalar water volume instead of cached visual flood level. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings.
- Loop 20: Re-audited runtime BFS capacity. Pump drainage, fungal target search, anchor reachability, and component power island walks now clamp node counts to actual native/list capacities, clamp CSR edge ranges, and emit `FloodBlackBoxTraversalOverflowFlag` on queue saturation. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings.
- Loop 21: Re-audited native job traversal bounds and verification state. `HabitatDirtyRegionRebuildJob` now clamps node/edge capacities and queue writes like the runtime graph BFS paths. First build retry hit a transient `Temp/obj/EasySave3/EasySave3.dll` file lock; second retry succeeded with 0 errors and 0 warnings. MCP validation and console reads currently return `no_unity_session`.
- Loop 22: Re-audited rupture cascade and lockdown publication bounds. `ApplyRuptureCascadeStressFromRupturedNodes` now clamps `_nodes`, `_edgeOffsets`, and `_edgeDestinations` before CSR reads, and `PublishEmergencyLockdownState` no longer assumes anchor reachability length. Earlier CLI build succeeded with 0 errors; remaining warnings were external package/GPUInstancer/Crest/ShaderGraph warnings, not HABITAT files. MCP remained `no_unity_session`.
- Loop 23: Re-audited temporary bypass live insertion. Removed fragile in-place CSR shifting and route live bypass edits through `BuildEdgeRecords()` so edge flags, edge resistance, CSR indices, and flood `NativeParallelMultiHashMap` connections rebuild from the same edge buffer authority. Superseded by Loop 25 verification.
- Loop 24: Re-audited HABITAT Burst job and topology rebuild fail-closed behavior. `HabitatFloodPropagationJob` now reports malformed directed connections through `InvalidConnectionCount` and `FloodBlackBoxTopologyInvalidFlag` instead of mislabeling topology faults as non-finite math. `HabitatDirtyRegionRebuildJob` now preserves isolated dirty-node rebuilds when valid edge count is zero, clamps negative edge-offset capacity to zero, `HabitatWaterlineShaderUpdateJob` checks every source lane length before reading, and `BuildEdgeRecords()` clears/prefixes CSR offsets only to actual `_edgeOffsets`/`_edgeWriteCursor` capacity. Superseded by Loop 25 verification.
- Loop 25: Corrective static verification and CSR rebuild/publication hardening. `BuildEdgeRecords()` now severs/ignores invalid edge endpoints, guards CSR write indices, sets `_edgeCount` from actually written directed edges, and bounds CSR clear/prefix/write-cursor initialization by actual native capacities. `BuildNodeRecords`, `PublishDegradationState`, and `PublishGraphKernel` now clamp publication counts to real native/list capacities before reads. Per user instruction, no build or `dotnet build` was run after the latest patch; current verification is static `git diff --check` only, with CRLF normalization warnings.
