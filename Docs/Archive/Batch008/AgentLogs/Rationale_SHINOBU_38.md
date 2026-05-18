# SHINOBU_38 Rationale

Date: 2026-05-18
Domain: Echelon 9 / QA Watchdog Bot
Status: IMPLEMENTED; FULL RUNTIME PROOF BLOCKED BY CROSS-DOMAIN COMPILE WALL

## Decision Log

### R0 - Disk Memory Initialization

Problem: SHINOBU_38 had no durable status or rationale file, so context compression would erase task state.

Solution: Created `Docs/Tasks/Status_SHINOBU_38.md` and this rationale before code edits. Selected the zero-GC, native memory, crash telemetry, AUP, registry, execution phase, signal lane, and QA evidence mandates.

Rejected Alternatives: Chat-only tracking and reading neighboring prompts were rejected because the batch protocol requires disk-backed state and strict prompt isolation.

Scalability potential: Low = no runtime cost; Middle = avoids repeated work; High = keeps integration evidence tied to files; Ultra = supports long parallel-agent batches.

Hardware Impact: 0 us runtime gain; prevents process churn on i3/MX350 by keeping implementation state recoverable.

### R1 - Agent 36 Input ABI Mirror

Problem: The watchdog must inject virtual input into Agent 36's unmanaged input DTO without pulling `Hecton8.Input.Determinism` into the QA assembly.

Solution: Defined local `Shinobu38InputStateDTO` with the same ABI: `float2 LookDelta` at 0, `float2 MoveAxis` at 8, `uint ButtonMask` at 16, `uint _pad0` at 20, total 24 bytes. The runtime writes this payload into `BufferID.ShinobuInputCurrentDto` through `GlobalDataVault`.

Rejected Alternatives: Unity Input System/XR synthetic events were rejected as managed and nondeterministic. Direct reference to Agent 36 assembly was rejected because it expands compile dependencies and risks rebuild churn.

Scalability potential: Low = same one DTO write; Middle = add more virtual buttons by bitmask; High = batch multiple scripted profiles; Ultra = CI endurance matrix can feed deterministic input journals without gameplay coupling.

Hardware Impact: Estimated 5-30 us/frame saved versus managed input path and no per-frame allocation. On i3/MX350 the write is one 24-byte cache-line-local payload.

### R2 - DataVault Ownership Instead Of Private NativeArrays

Problem: The first implementation still cached private `NativeArray` and `byte[]` fields, violating H-Phi data sovereignty after the polish mandate.

Solution: Replaced persistent storage with `VaultBufferHandle<T>` for state, snapshot, Agent 36 input, waypoints, rebase signal, tuning, mock vault, 300-frame telemetry ring, CSV scratch, waypoint scratch, and dump scratch. Runtime aliases are resolved per use and buffers are locked with `IDataVault.TryLockBuffer`.

Rejected Alternatives: Keeping local H8Memory allocations was rejected because ownership would be hidden from the vault and compaction watchdogs. Adding new public `BufferID` enum entries was rejected because it touches Core and increases compile-wall blast radius; QA-local numeric IDs are used for private lanes.

Scalability potential: Low = vault-owned small buffers only; Middle = bigger route set by count; High = longer telemetry through a vault-resident ring; Ultra = CI farms can collect many runs without per-domain allocator fragmentation.

Hardware Impact: Avoids duplicated persistent allocations and local array lifetime bugs. Estimated 0.1-0.4 ms cold-start churn avoided on weak silicon when compared with repeated local initialization paths.

### R3 - Mock SDF Dear Lie

Problem: A real cave traversal test must avoid terrain, but depending on the actual terrain sampler or NavMesh would create domain coupling and slow manual integration.

Solution: Implemented `Shinobu38MockTerrainSdf` with sine-based cave radius and finite-difference normal. `BotNavigationJob` samples a point 12 m ahead; if distance < 10 m, it biases desired velocity along the SDF normal.

Rejected Alternatives: NavMesh, raycast probes, and direct world terrain sampler calls were rejected because they add runtime dependencies, scene wiring assumptions, and heavier query costs.

Scalability potential: Low = cheap sine SDF for smoke/endurance; Middle = CSV route overrides for authored paths; High = swap SDF coefficients from binary profiles; Ultra = actual terrain sampler can be bridged later through a contract, not a direct assembly reference.

Hardware Impact: Estimated 20-200 us/frame saved versus physics/raycast/nav queries. The fake is deterministic and cheap enough for i3/MX350.

### R4 - AUP Precision Policy

Problem: Directly casting absolute 100 km AUP coordinates to `float3` creates float jitter and false drift failures.

Solution: All float math subtracts the active target AUP first, then casts the local delta to float. Jitter audit reconstructs `target + float(localDelta)` and compares against the original double AUP.

Rejected Alternatives: `(float3)CurrentAUP` was rejected. Floating-origin assumptions were rejected because the bot intentionally tests origin shifts.

Scalability potential: Low/Middle/High/Ultra all use the same double truth plus local float math; visual overkill can consume local deltas without changing gameplay truth.

Hardware Impact: Correctness gain; avoids false error-count growth and keeps math SIMD-friendly.

### R5 - CSV And Dump Without Managed Per-Record Buffers

Problem: A 10-hour endurance bot must log CSV and fault dumps without allocating strings or byte arrays per row.

Solution: `Shinobu38CsvStreamer` encodes rows through a vault-owned `NativeArray<byte>` scratch and `Shinobu38AsciiBuffer`, an unsafe ASCII appender over raw bytes. The main tick enqueues the fixed payload into a vault-backed SPSC ring; a single background writer thread performs FileStream I/O. Binary dumps and result JSON use the same background writer path.

Rejected Alternatives: `StreamWriter`, string interpolation, `StringBuilder`, `byte[]` staging, LINQ parsing, and main-thread FileStream appends were rejected due GC and disk-stall risk.

Scalability potential: Low = low telemetry Hz; Middle = higher CSV Hz; High = longer endurance runs; Ultra = the same fixed ring can be swapped to MMF/WAL sink when that project contract is standardized.

Hardware Impact: Estimated 10-80 us saved per CSV row, all row-level GC removed, and main-thread disk stalls are removed. Background writer reports actual CSV write micros into `Shinobu38FileWriterStateDTO`.

### R6 - Blackbox Ring

Problem: The bot cannot answer "why did it fail" after a stuck route, CSV stall, memory leak, or low FPS fault without prior state retention.

Solution: Added `Shinobu38WatchdogTelemetryEntry` ring with 300 entries and deterministic binary dump to `Docs/AgentLogs/Dump_SHINOBU_38.bin` plus `Docs/AgentLogs/Dump_SHINOBU_38.h8dump`. Entry size is 32 bytes. Dumps are enqueued to the background writer, not written from the simulation tick.

Rejected Alternatives: Console logs and dynamically sized diagnostic collections were rejected because failure storms can allocate, truncate, or miss the previous frames.

Scalability potential: Low = 9.6 KB ring; Middle/High/Ultra can raise ring count through vault sizing later without changing the DTO.

Hardware Impact: <0.1 us/frame estimated ring write; fault I/O occurs only after a terminal condition.

### R7 - Editor Facade And CSV Override

Problem: Designers need to tune speed/avoidance/telemetry and author waypoint routes without recompiling C#.

Solution: Added `Shinobu38QaWatchdogCommanderWindow`, a batch runner menu command, live tuning write into the vault DTO, `qa_bot_waypoints.csv` ingestion, and SceneView handles for route/avoidance visualization.

Rejected Alternatives: Hardcoded constants and runtime `OnDrawGizmos` in the headless host were rejected. The editor facade keeps normal runtime/batch paths clean.

Scalability potential: Low = short local smoke route; Middle = longer authored CSV; High = profile matrix via command args; Ultra = CI-generated CSV suites.

Hardware Impact: 0 us in player/batch hot path except active QA cold tick file timestamp check.

### R8 - Compile Wall Classification

Problem: Full Unity batch launch did not reach Play Mode, so a fake "10 km completed" claim would be a lie.

Solution: Ran isolated Roslyn compile for `Hecton8.QA.Headless` and `Hecton8.QA.Headless.Editor` with local Core DLL reference after the background writer polish; both exited 0. Then launched Unity batch runner. The project stopped before the bot could run: `Docs/AgentLogs/Unity_SHINOBU_38_Run_after_bgwriter.log` contains 84 `error CS` hits outside `Assets/_Project/Scripts/QA/Headless` and no QA Headless errors.

Rejected Alternatives: Fixing unrelated Core/Quest/Audio/UI/Physics compile errors was rejected because it crosses SHINOBU_38 domain boundaries and risks architectural sabotage. Reporting success without CSV/result artifact was rejected.

Scalability potential: Low/Middle/High/Ultra require a clean project compile before the endurance route can generate real metrics.

Hardware Impact: Avoided rebuild spam by using isolated QA compile first. Full Unity launch was attempted once and killed after 240 seconds to protect the developer machine.

## Struct Layout

- `WatchdogStateDTO` size 40: `double3 CurrentTargetAUP` offset 0 size 24; `float DistanceTraveled` offset 24 size 4; `uint ErrorCount` offset 28 size 4; `float TestDuration` offset 32 size 4; `uint _pad0` offset 36 size 4.
- `TelemetrySnapshotDTO` size 16: `FrameTimeMs` 0; `GcAllocBytes` 4; `VramUsed` 8; `AupJitterError` 12.
- `Shinobu38InputStateDTO` size 24: `float2 LookDelta` offset 0 size 8; `float2 MoveAxis` offset 8 size 8; `uint ButtonMask` offset 16 size 4; `uint _pad0` offset 20 size 4.
- `Shinobu38WatchdogTelemetryEntry` size 32: `Frame` 0; `TargetDistanceRemaining` 4; `AvoidanceCorrections` 8; `CsvWriteTimeMs` 12; `float3 CurrentAupFloat` 16; `Flags` 28.
- `Shinobu38FileWriteCommand` size 32: `long Sequence` 0; `int PayloadOffset` 8; `int PayloadLength` 12; `uint Target` 16; `uint Flags` 20; `uint _pad0` 24; `uint _pad1` 28.
- `Shinobu38FileWriterStateDTO` size 32: `long LastWriteTicks` 0; `int LastCsvWriteMicros` 8; `int LastAnyWriteMicros` 12; `uint DroppedWrites` 16; `uint CompletedWrites` 20; `uint Flags` 24; `uint _pad0` 28.

## SELF_AUDIT

<SELF_AUDIT>
Task 01 [PASS] Archive scan executed; no usable legacy waypoint binary; emergency route active.
Task 02 [PASS] No player-prefab autoplayer; QA-only centralized bootstrap host.
Task 03 [PASS] Raw DTO fields and `UnsafeUtility.AsRef`; no CS1612-prone properties.
Task 04 [PASS] ARM64 sizes: 40/16/24/32; no `Pack=1`.
Task 05 [PASS] Mock rebase signal and target offset path implemented.
Task 06 [PASS] Env/arg/temp flag activation gate; normal builds abort.
Task 07 [PASS] Agent 36 ABI mirror writes `BufferID.ShinobuInputCurrentDto`.
Task 08 [PASS] SDF cave avoidance with local AUP math.
Task 09 [PASS] ProfilerRecorder memory sampling and leak slope fault.
Task 10 [PASS] AUP jitter audit uses double truth and local float delta.
Task 11 [PASS] CSV writer uses vault `NativeArray<byte>` scratch and background SPSC file writer; no string rows or main-thread disk append.
Task 12 [PASS] SHI stress signal published through `GlobalSignals`.
Task 13 [PASS] Hardware flags and tier DTO implemented.
Task 14 [PASS] Sprint and primary-fire automation written as button bits.
Task 15 [PASS] Fault dump enqueues 300-frame binary blackbox to `.bin` and `.h8dump`.
Task 16 [PASS] Init resolves DataVault handles; no local persistent NativeArray ownership.
Task 17 [PASS] 300-frame telemetry recorder active.
Task 18 [PASS] Editor commander/tuner and batch menu implemented.
Task 19 [PASS] CSV waypoint override parser uses vault byte scratch.
Task 20 [PASS] SceneView path/normal visualizer implemented; runtime headless path remains clean.
ARM64 CHECK: Primary DTO `Shinobu38InputStateDTO` is 24 bytes and 8-byte multiple; no packed runtime struct.
ZERO-GC CHECK: `FastTick` schedules a job using vault aliases; no LINQ, closures, boxing, per-frame strings, managed byte arrays, or main-thread FileStream append in SHINOBU_38 runtime hot path.
AUP CHECK: Absolute AUP is kept in `double3`; float math uses target-relative deltas.
DEAR LIE CHECK: Cave avoidance is a sine SDF + finite-difference normal, not NavMesh or physics.
DEPENDENCY CHECK: Cross-domain communication uses `GlobalDataVault`, `BufferID.ShinobuInputCurrentDto`, `GlobalSignals`, and command args; no direct Agent 36 assembly reference.
H-PHI CHECK: Persistent arrays are vault-owned handles; SHINOBU_38 only resolves aliases.
BLACKBOX CHECK: 300-frame ring dumps to `Docs/AgentLogs/Dump_SHINOBU_38.bin` and `Docs/AgentLogs/Dump_SHINOBU_38.h8dump` on fault/slow CSV through the background writer.
COMPILE GUARD: Isolated QA assemblies compile after background writer polish; full project Play Mode blocked by unrelated compile wall.
</SELF_AUDIT>
