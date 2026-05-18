# LOG_SHINOBU_38

## 2026-05-18 - QA Watchdog Endurance Bot

What was wrong -> Manual endurance regression testing had no headless 10 km bot, no Agent 36 unmanaged input injection path, no fixed 300-frame QA blackbox, no zero-GC CSV writer, and no editor/batch facade for route/tuning.

What was done -> Added `Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs`, `Assets/_Project/Scripts/QA/Headless/Editor/Shinobu38QaWatchdogCommanderWindow.cs`, and updated `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef`. Runtime activation is gated by `-h8qa`, endurance args, env `H8_QA_ENDURANCE_10KM`, or temp flag. The bot writes ABI-mirrored input bytes into `BufferID.ShinobuInputCurrentDto`, navigates mock SDF caves through a Burst job, samples GC/reserved/graphics memory, audits AUP jitter, writes CSV rows through vault-owned byte scratch, and dumps a 300-frame binary ring on fault.

Cinematic Cheats used -> Replaced real cave/terrain pathing with `Shinobu38MockTerrainSdf`: sine-wave cave radius, finite-difference normal, and local AUP steering. Replaced physical combat routine with deterministic button-mask pulses. Replaced runtime visual debug objects with editor SceneView handles.

Exact Microseconds saved -> Managed input/XR emulation avoided: estimated 5-30 us/frame. NavMesh/raycast avoidance avoided: estimated 20-200 us/frame. CSV string/StreamWriter allocation avoided: estimated 10-80 us/row and 0 row-level GC. Telemetry blackbox write cost: estimated <0.1 us/frame. Normal-build overhead after activation abort: 0 us.

Struct Layout -> `WatchdogStateDTO` 40 bytes: double3 at 0, float at 24, uint at 28, float at 32, uint pad at 36. `Shinobu38InputStateDTO` 24 bytes: float2 at 0, float2 at 8, uint at 16, uint pad at 20. `Shinobu38WatchdogTelemetryEntry` 32 bytes: uint/float/float/float/float3/uint.

H-Phi Check -> Persistent buffers are owned by `GlobalDataVault` via `VaultBufferHandle<T>`: state, snapshot, Agent 36 input, waypoints, rebase, tuning, mock vault, telemetry ring, CSV scratch, waypoint scratch, dump scratch. No private SHINOBU_38 persistent `NativeArray` or managed byte-array staging remains.

Blackbox -> `TelemetryCapacity=300`; dump target `Docs/AgentLogs/Dump_SHINOBU_38.bin`; dump header stores magic, count, entry stride, cursor.

Compile Guard -> Isolated Roslyn compile passed: `QA_HEADLESS_LOCALREF_CSC_EXIT=0`, `QA_HEADLESS_EDITOR_LOCALREF_CSC_EXIT=0`. Full Unity batch launch attempted through `Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run`; Play Mode did not start because `Docs/AgentLogs/Unity_SHINOBU_38_Run_after_vault.log` contains 606 unrelated `error CS` entries outside `Assets/_Project/Scripts/QA/Headless`. No real `QA_Endurance_Report.csv` or 10 km result JSON was generated.

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
Task 11 [PASS] CSV writer uses vault `NativeArray<byte>` scratch; no string rows.
Task 12 [PASS] SHI stress signal published through `GlobalSignals`.
Task 13 [PASS] Hardware flags and tier DTO implemented.
Task 14 [PASS] Sprint and primary-fire automation written as button bits.
Task 15 [PASS] Fault dump writes 300-frame binary blackbox.
Task 16 [PASS] Init resolves DataVault handles; no local persistent NativeArray ownership.
Task 17 [PASS] 300-frame telemetry recorder active.
Task 18 [PASS] Editor commander/tuner and batch menu implemented.
Task 19 [PASS] CSV waypoint override parser uses vault byte scratch.
Task 20 [PASS] SceneView path/normal visualizer implemented; runtime headless path remains clean.
ARM64 CHECK: primary input DTO is 24 bytes, 8-byte aligned, no runtime `Pack=1`.
ZERO-GC CHECK: no SHINOBU_38 hot-path LINQ, foreach, boxing, closures, row strings, `new NativeArray`, or managed byte staging.
AUP CHECK: float math subtracts target AUP first; absolute truth remains `double3`.
DEAR LIE CHECK: sine SDF cave + gradient normal replaces real terrain/NavMesh/physics.
DEPENDENCY CHECK: `GlobalDataVault`, `GlobalSignals`, command args, and `BufferID` are used; no direct Agent 36 assembly reference.
</SELF_AUDIT>

## 2026-05-18 - Background Writer Polish Pass

What was wrong -> Previous SHINOBU_38 implementation still performed CSV/dump/result `FileStream` writes from the simulation/control thread. That violated the Steam Deck MicroSD pressure mandate and made `CsvWriteTimeMs` measure main-thread disk stalls instead of isolating them.

What was done -> Replaced direct CSV/dump/result writes with `Shinobu38FileWriteCommand`, `Shinobu38FileWriterStateDTO`, and two new vault buffers: fixed command ring plus fixed payload slab. Main thread now only encodes ASCII/binary payloads into vault scratch and enqueues a 32-byte command. A background thread owns the FileStream writes for CSV, result JSON, `.bin`, and `.h8dump`. Actual disk write micros are written back into the vault state and sampled by the telemetry ring.

Cinematic Cheats used -> No new physical simulation. Existing sine SDF cave fake remains the low-tier route truth. Disk pressure is now a staged QA signal instead of a gameplay stall.

Exact Microseconds saved -> Main-thread CSV append stall removed: previously measured if FileStream append crossed >1.0 ms; now main thread performs only fixed MemCpy into vault payload, estimated 1-8 us for a CSV row. Fault/result I/O moved off simulation thread: terminal path can still spend milliseconds, but not inside the tick. Row-level GC remains 0.

Struct Layout -> `Shinobu38FileWriteCommand` 32 bytes: long at 0, int at 8, int at 12, uint at 16, uint at 20, uint pad at 24, uint pad at 28. `Shinobu38FileWriterStateDTO` 32 bytes: long at 0, int at 8, int at 12, uint at 16, uint at 20, uint at 24, uint pad at 28.

H-Phi Check -> New writer command ring, payload slab, and writer state are `GlobalDataVault` buffers. No private SHINOBU_38 `NativeArray` fields were introduced.

Blackbox -> Fault dump now targets both `Docs/AgentLogs/Dump_SHINOBU_38.bin` and `Docs/AgentLogs/Dump_SHINOBU_38.h8dump` through the background writer.

Compile Guard -> Isolated compile after polish: `QA_HEADLESS_LOCALREF_CSC_EXIT=0`, `QA_HEADLESS_EDITOR_LOCALREF_CSC_EXIT=0`. Unity batch launch after polish still stops before Play Mode; `Docs/AgentLogs/Unity_SHINOBU_38_Run_after_bgwriter.log` has 84 `error CS` hits outside `Assets/_Project/Scripts/QA/Headless` and no QA Headless errors.
