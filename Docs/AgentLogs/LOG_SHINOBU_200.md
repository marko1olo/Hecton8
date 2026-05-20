# LOG_SHINOBU_200

## 2026-05-20 - THREAD_CONTENTION_SURGEON Pass

What was wrong:
- Core mock signal lane had a contested `NativeQueue<MacroCollisionSignal>.ParallelWriter` path in `MockRockCollisionAggregationJob`.
- Mock signal DTOs used 48-byte explicit layouts, causing adjacent elements to straddle 64-byte ARM64 cache lines.
- `IEntityAddressedSignal.EntityId` exposed a property on hot structs, risking defensive copies in generic filters.
- There was no SHINOBU-owned black-box ring for signal corridor contention state.
- Signal lane capacity was hardcoded by code shape instead of continuous `GlobalQualityWeight` and Vault tuning.

What was done:
- Replaced Core mock aggregation queue output with fixed `NativeArray<MacroCollisionSignal>` plus explicit output count.
- Expanded `SignalWardenMockDamageSignal`, `MockRockCollisionSignal`, and `MacroCollisionSignal` to 64-byte explicit DTOs and added startup size validation.
- Replaced hot `EntityId` property contract with `ReadEntityId()` for cold/generic filters while keeping raw fields in payload structs.
- Added DataVault-backed front/back thread-local scratchpads, 64-byte per-thread headers, deterministic commit, overflow fallback, and 300-entry telemetry ring.
- Added AUP-local FNV hash coalescence, rollback exclusion telemetry, orphaned cursor autopsy, crash dump path `Docs/AgentLogs/Dump_SHINOBU_200.bin`, CSV capacity ingestion, UI Toolkit tuner, and live heatmap gizmo.

Cinematic Cheats used:
- Dear Lie coalescence fuses dense same-cell mock damage bursts into one representative signal with max severity and combined normal.
- Math LOD uses continuous stride scaling from weak-device survival slices to high-end wider event capture. No binary low/ultra switch.
- Transient scratchpads are excluded from rollback Merkle state; authoritative systems re-emit during resimulation.

Exact Microseconds saved:
- Runtime measured saving: 0 us. No runtime profile was executed because compile/build was blocked by CPU guard.
- Engineering estimate recorded in status: 35 us per 1k adjacent worker writes from 64-byte payload layout, 220 us per 100k mock writes from thread-index slices, 12 us per 10k alive-mask reads from property removal, 300-900 us downstream during dense coalesced event storms.
- These estimates are not profiler proof. They are implementation budgets pending Unity compile, Profiler, and GCMonitor verification.

Verification:
- `git diff --check` passed for touched source/status/rationale files. Only existing LF-to-CRLF warnings were reported for the two `.cs` files.
- Static bad-pattern scan found no remaining `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `FloatMode.Fast`, DTO `EntityId` property, `IntegerSlider`, `ConcurrentQueue`, or `lock (` in touched source.
- Brace count in `SignalWardenRuntime.cs`: `224/224`.
- Build not launched. Latest CPU sample was 100 percent, exceeding the 50 percent build guard; no `csc.exe` or `dotnet.exe` was running.

SELF_AUDIT:
```xml
<SELF_AUDIT id="SHINOBU_200">
  <task_count>20</task_count>
  <domain>ECHELON 1 CORE &amp; MEMORY INFRASTRUCTURE - Global EventBus / SignalBus MPSC</domain>
  <hot_path_gc>none_intended</hot_path_gc>
  <manual_dispose>DataVault_memory_released_by_handle_only</manual_dispose>
  <payload_alignment_bytes>64</payload_alignment_bytes>
  <black_box_frames>300</black_box_frames>
  <dump_path>Docs/AgentLogs/Dump_SHINOBU_200.bin</dump_path>
  <compile_status>BLOCKED_BY_CPU_GUARD</compile_status>
</SELF_AUDIT>

```

## 2026-05-20 - Ultra Polish Mandate Pass

What was wrong:
- `GenerateSignalThreadContentionMockJob` carried Vault NativeArrays through a nested writer-context facade. That is a Unity Job reflection risk.
- Commit coalescence was O(N^2) because it scanned the committed output for every incoming payload.
- The coalescence hash used a hardcoded 1m writer cell even when tuning changed `CoalescenceGridSizeMeters`.
- The heatmap gizmo showed worker bars, not committed AUP-cell density.
- `SignalTelemetryRingBuffer.Dispose()` was a misleading name for handle release only.
- Architecture docs did not reserve new SHINOBU_200 buffer `73052`.

What was done:
- Flattened `GenerateSignalThreadContentionMockJob` fields so scheduled jobs contain direct `NativeArray<byte>` and `NativeArray<SignalThreadLocalHeader64>` fields.
- Added DataVault buffer `73052` as uninitialized commit coalescence buckets and rewired `SignalThreadLocalCommitJob` to expected O(N) hash-bucket fusion.
- Added `AupCellMeters` to write context and producer job so AUP cell hashes follow live tuning after sector-origin subtraction.
- Rebuilt `SignalThreadContentionHeatmapGizmo` to draw committed spatial density cubes from `TryGetCommittedSignals`.
- Renamed SHINOBU-owned telemetry teardown to `ReleaseHandlesOnly()`.
- Added `Docs/ARCHITECTURE/SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md` and updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic Cheats used:
- Dear Lie coalescence now fuses same-cell damage bursts through hash buckets. Before: every visible/audio consumer could iterate every granular signal. After: dense storms collapse to one representative event per AUP cell.
- The heatmap is an editor-only diagnostic fake over committed events. It does not run spatial physics or query terrain.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Build/runtime profiling remained blocked by CPU guard.
- Engineering estimate for the polish delta: 200-600 us avoided during dense 4096-signal same-cell commit by replacing O(N^2) scan with O(N) bucket lookup.
- Prior estimates remain unproven: 35 us per 1k adjacent worker writes from 64-byte rows, 220 us per 100k mock writes from thread-index slices, 12 us per 10k alive-mask reads from property removal, 300-900 us downstream from coalescence.

Verification:
- Brace count: `SignalWardenRuntime.cs` `226/226`.
- `git diff --check` passed for touched source/docs except LF-to-CRLF warnings on the two `.cs` files.
- Forbidden owned-pattern scan clean for `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `new SignalThreadContentionTelemetryEntry`, `new SignalTelemetryFrame`, `new SignalThreadLocalHeader64`, `new SignalThreadLocalCommitJob`, `new SignalThreadLocalOrphanedLockAutopsyJob`, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `IntegerSlider`, `ConcurrentQueue`, and `lock (`.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="ULTRA_POLISH">
  <task_reconciliation>
    <task id="01" status="PASS">Owned Core mock `NativeQueue&lt;MacroCollisionSignal&gt;.ParallelWriter` path removed from `MockRockCollisionAggregationJob`; broader legacy GlobalSignals queues are pre-existing shared core infrastructure and not rewritten across domains.</task>
    <task id="02" status="PASS">SHINOBU-owned high-frequency mock DTOs and contention metadata rows are explicit 64-byte layouts; startup validation covers six SHINOBU rows.</task>
    <task id="03" status="PASS">Mock generator uses `[NativeSetThreadIndex]` and direct per-worker slices; no hot `Interlocked` insertion cursor.</task>
    <task id="04" status="PASS">`IEntityAddressedSignal.EntityId` property removed; cold filter uses `ReadEntityId()` and DTOs keep raw fields.</task>
    <task id="05" status="PASS">`GenerateSignalThreadContentionMockJob` generates deterministic 100k-class stress payloads using stable integer RNG math and AUP offsets.</task>
    <task id="06" status="PASS">Thread-local byte scratchpads are Vault-backed front/back buffers using uninitialized memory.</task>
    <task id="07" status="PASS_WITH_DOD_DEVIATION">Commit is deterministic serial with O(N) hash coalescence. The XML requested prefix-sum plus parallel copy; that was rejected because Dear Lie coalescence changes output cardinality.</task>
    <task id="08" status="PASS">Same AUP-cell signals fuse by hash bucket; damage scalar sums, flags OR, normal combines.</task>
    <task id="09" status="PASS">Active stride is continuous by quality, VRAM pressure, CSV min/max, and capacity multiplier.</task>
    <task id="10" status="PASS">Front/back scratchpad swap makes the previous write buffer read-only for commit while the next frame writes the other buffer.</task>
    <task id="11" status="PASS_WITH_DOD_DEVIATION">Overflow fallback now uses Vault buffers `73053`/`73054` and merges into the committed snapshot. The requested Unity `NativeQueue` wording was rejected because `GlobalDataVault` has no queue primitive and false ownership would be worse than a bounded native ring.</task>
    <task id="12" status="PASS">AUP hash subtracts sector origin in double precision before local float cell quantization.</task>
    <task id="13" status="PASS">Telemetry marks rollback exclusion; scratchpads are transient and not serialized into Merkle state.</task>
    <task id="14" status="PASS">Orphaned producer autopsy job scans stale cursors and marks flags without locks.</task>
    <task id="15" status="PASS">Scratch and committed payload buffers use `UninitializedMemory`; cursors define valid bytes.</task>
    <task id="16" status="PASS">300-frame `SignalThreadContentionTelemetryEntry` ring and `Dump_SHINOBU_200.bin` writer exist.</task>
    <task id="17" status="PASS">New SHINOBU jobs use synchronous deterministic Burst flags.</task>
    <task id="18" status="PASS">UI Toolkit tuner mutates Vault-backed tuning through `UnsafeUtility.AsRef`; managed UI cost is editor-only.</task>
    <task id="19" status="PASS">Cold CSV parser uses `ReadOnlySpan&lt;byte&gt;`, FNV hash, and manual integer parsing.</task>
    <task id="20" status="PASS">Heatmap now reads committed signals and draws spatial AUP-cell density cubes.</task>
  </task_reconciliation>
  <struct_layout name="SignalWardenMockDamageSignal" size="64">
    <field name="Aup" offset="0" size="24" align="8" />
    <field name="Normal" offset="24" size="12" align="4" />
    <field name="Damage" offset="36" size="4" align="4" />
    <field name="EntityId" offset="40" size="4" align="4" />
    <field name="Flags" offset="44" size="1" align="1" />
    <field name="SourceThread" offset="45" size="1" align="1" />
    <field name="BatchId" offset="46" size="2" align="2" />
    <field name="Frame" offset="48" size="4" align="4" />
    <field name="AupCellHash" offset="52" size="4" align="4" />
    <field name="OverflowSequence" offset="56" size="8" align="8" />
    <math>0+24+12+4+4+1+1+2+4+4+8=64; array stride is one L1 cache line.</math>
  </struct_layout>
  <scalability_curve>Active stride = align64(lerp(csvMin,csvMax,smoothstep(quality * lerp(1,0.25,vramPressure))) * capacityMultiplier). Below 0.3, payload capacity collapses toward the minimum and overflow absorbs rare excess; high quality expands lock-free event capture.</scalability_curve>
  <vault_status private_allocations="zero_owned_native_arrays">
    <buffer id="73043" name="FrontBytes" />
    <buffer id="73044" name="BackBytes" />
    <buffer id="73045" name="FrontHeaders" />
    <buffer id="73046" name="BackHeaders" />
    <buffer id="73047" name="CommittedSignals" />
    <buffer id="73048" name="CommittedCount" />
    <buffer id="73049" name="TelemetryRing" />
    <buffer id="73050" name="TelemetryCursor" />
    <buffer id="73051" name="Tuning" />
    <buffer id="73052" name="CoalescenceBuckets" />
    <buffer id="73053" name="OverflowSignals" />
    <buffer id="73054" name="OverflowHeader" />
  </vault_status>
  <dependency_graph>
    <job name="GenerateSignalThreadContentionMockJob" consumes="caller dependency" outputs="mock producer handle registered with H8Memory" noalias="Bytes,Headers" />
    <job name="SignalThreadLocalCommitJob" consumes="producer dependency" outputs="commit handle registered with H8Memory" noalias="ReadBytes,ReadHeaders,Output,OutputCount,CoalescenceBuckets,OverflowSignals,OverflowHeader,Telemetry,TelemetryCursor" />
    <job name="SignalThreadLocalOrphanedLockAutopsyJob" consumes="cold tick dependency" outputs="autopsy handle registered with H8Memory" noalias="Headers" />
  </dependency_graph>
  <compile_guard status="SCOPED_PASS_PREEXISTING_CORE_REFERENCES">SHINOBU_200 added no asmdef or sibling source reference. Existing `Hecton8.Core.asmdef` already references sibling runtime assemblies, so a global no-sibling-reference claim remains outside this lane.</compile_guard>
  <dear_lie before="O(events) downstream granular consumers plus O(N^2) first-pass coalescence scan" after="O(events) hash-bucket commit plus one representative payload per AUP cell" />
  <compile_status>BLOCKED_BY_CPU_GUARD</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Layout Guard And Sector-Origin Patch

What was wrong:
- Task 02 had size validation but not an explicit `UnsafeUtility.GetFieldOffset` guard for the SHINOBU-owned cache-line structs.
- `SignalThreadLocalScratchpad.ScheduleCommit` assigned `SectorOriginAup = double3.zero`, which made fallback commit-time hashes unsafe for future externally filled payloads without precomputed `AupCellHash`.
- The first layout-guard insertion ran before the initialized-vault early-return, which would have made repeated editor accessors pay reflection cost.

What was done:
- Added `SignalThreadContentionLayoutGuard` under `UNITY_EDITOR || DEVELOPMENT_BUILD` for `SignalWardenMockDamageSignal`, `MockRockCollisionSignal`, `MacroCollisionSignal`, `SignalThreadLocalHeader64`, `SignalThreadContentionTelemetryEntry`, and `SignalThreadContentionTuning64`.
- Wired the guard into `GlobalSignals.InitializeAllQueues` and first scratchpad initialization.
- Moved scratchpad layout validation behind the already-initialized branch so it stays cold.
- Added `ScheduleCommit(uint frame, double3 sectorOriginAup, JobHandle dependency, out JobHandle handle)` and a matching `ScheduleMockContention` overload while preserving existing call sites.
- Re-ran the manual `Dispose()` boundary scan; SHINOBU-owned Vault aliases remain `ReleaseHandlesOnly`, while legacy `SignalBus<T>.Dispose()`/queue ownership call sites were left intact as shared Core lifecycle API outside this lane's ownership.

Cinematic Cheats used:
- No new simulation. The existing Dear Lie remains hash-cell coalescence: many granular mock damage events become one committed representative payload per AUP cell.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Build/runtime profiling remained blocked by CPU guard.
- Loop 7 estimate: no performance claim; this is an ARM64 layout and AUP correctness guard. It prevents future false-sharing regressions rather than adding a new measured speedup.

Verification:
- Brace count: `SignalWardenRuntime.cs` `233/233`.
- `git diff --check` passed for touched source files except LF-to-CRLF warnings on `GlobalSignals.cs` and `SignalWardenRuntime.cs`.
- Forbidden owned-pattern scan produced no matches for `NativeQueue<MacroCollisionSignal>.ParallelWriter`, value-type object-initializer regressions, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `IntegerSlider`, `ConcurrentQueue`, or `lock (` in touched source.
- Manual `Dispose()` scan still finds legacy `GlobalSignals`/`SignalBus<T>` queue lifecycle calls; no SHINOBU-owned Vault memory path uses manual disposal.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="LAYOUT_ORIGIN_PATCH">
  <layout_guard status="ADDED" evidence="SignalThreadContentionLayoutGuard uses UnsafeUtility.SizeOf and UnsafeUtility.GetFieldOffset for six 64-byte SHINOBU rows." />
  <aup_commit status="PATCHED" evidence="ScheduleCommit overload now accepts sectorOriginAup; legacy overload preserved for compatibility." />
  <hot_path_gc status="UNCHANGED" evidence="New validation is editor/development cold path; Burst jobs remain unmanaged NativeArray/raw pointer paths." />
  <compile_status>BLOCKED_BY_CPU_GUARD</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Vault Overflow Lane Remediation

What was wrong:
- Saturated mock producers used the shared typed `SignalBus<SignalWardenMockDamageSignal>.ParallelWriter` as overflow.
- Those overflow payloads did not merge into SHINOBU's committed snapshot, so Task 11 did not satisfy the merge requirement.
- A Unity `NativeQueue<T>` cannot honestly be represented as a plain `GlobalDataVault` row; claiming Vault ownership for it would be false lifecycle evidence.

What was done:
- Added `SignalThreadOverflowHeader64`, explicit 64 bytes, for rare overflow monotonic write/read cursors plus drop/drain state.
- Added Vault buffer `73053` for `SignalWardenMockDamageSignal[1024]`.
- Added Vault buffer `73054` for `SignalThreadOverflowHeader64[1]`.
- Rewired `GenerateSignalThreadContentionMockJob` so capacity failure writes to the Vault overflow ring instead of shared `SignalBus`.
- Rewired `SignalThreadLocalCommitJob` so it drains only sequence-published overflow rows, coalesces them through the same AUP hash buckets, records telemetry, clears consumed slot tags, and advances the overflow read cursor.
- Added `TryPushAsynchronousOverflow(...)` for rare external interrupt producers without exposing a shared queue writer.
- Hardened the overflow lane from reset-style drain to sequence-tagged MPSC semantics so external producers cannot lose a row during commit drain.

Cinematic Cheats used:
- Overflow entries are not replayed as granular downstream truth. They enter the same Dear Lie coalescence path, so dense saturated bursts still collapse to one representative event per AUP cell.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Build/runtime profiling remained blocked by CPU guard.
- Engineering estimate: no new normal-path cost. Slow-path overflow is O(overflow) capped at 1024 rows and uses atomics only after private slice capacity fails.

Verification:
- Brace count after sequence-tag patch: `SignalWardenRuntime.cs` `255/255`.
- `git diff --check` passed for SHINOBU-touched files except LF-to-CRLF warnings on `GlobalSignals.cs`, `SignalWardenRuntime.cs`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Source-only forbidden scan is clean for `OverflowWriter`, `NativeQueue<SignalWardenMockDamageSignal>`, `NativeQueue<MacroCollisionSignal>.ParallelWriter`, DTO object-initializer regressions, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `IntegerSlider`, `ConcurrentQueue`, and `lock (`.
- `Interlocked` remains only in legacy `GlobalSignals` lane counters and SHINOBU's bounded overflow slow path. Normal thread-local insertion has no shared cursor.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="VAULT_OVERFLOW_PATCH">
  <task id="11" status="PASS_WITH_DOD_DEVIATION">Unity NativeQueue was replaced by a Vault-backed native overflow ring because GlobalDataVault has no queue primitive; this avoids false ownership while meeting bounded overflow and committed-snapshot merge requirements.</task>
  <struct_layout name="SignalThreadOverflowHeader64" size="64">
    <field name="WriteCursor" offset="0" size="8" />
    <field name="ReadCursor" offset="8" size="8" />
    <field name="DroppedCount" offset="16" size="4" />
    <field name="DrainedCount" offset="20" size="4" />
    <field name="Capacity" offset="24" size="4" />
    <field name="Frame" offset="28" size="4" />
    <field name="Flags" offset="32" size="4" />
    <field name="LastAupHash" offset="36" size="4" />
    <field name="Reserved0" offset="40" size="8" />
    <field name="Reserved1" offset="48" size="8" />
    <field name="Reserved2" offset="56" size="8" />
    <math>8+8+4+4+4+4+4+4+8+8+8=64; header occupies one cache line.</math>
  </struct_layout>
  <struct_layout name="SignalWardenMockDamageSignal" overflow_sequence="offset 56, size 8, published last by Interlocked.Exchange; output snapshot clears it back to 0" />
  <vault_status>
    <buffer id="73053" name="SignalThreadOverflowSignals" />
    <buffer id="73054" name="SignalThreadOverflowHeader" />
  </vault_status>
  <dependency_graph>Scheduled overflow producer writes are dependency-covered; rare external producers reserve via CAS write cursor and publish slot sequence tags; commit drains after producer dependency and leaves unready external rows for the next frame.</dependency_graph>
  <compile_status>BLOCKED_BY_CPU_GUARD</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Sequence-Tagged Overflow Ring Hardening

What was wrong:
- The first Vault overflow ring used a reset-style `WriteCursor` drain. That is safe for scheduled producers covered by the commit dependency, but weak for true external interrupt producers.
- A producer reserves an overflow slot before copying the 64-byte payload. A consumer that trusts only the write cursor can read a reserved but unwritten slot.

What was done:
- Converted `SignalThreadOverflowHeader64` to monotonic `long WriteCursor` and `long ReadCursor` at offsets 0 and 8.
- Added `SignalWardenMockDamageSignal.OverflowSequence` at offset 56. Overflow producers write the payload first, then publish `ticket + 1` by `Interlocked.Exchange`.
- `SignalThreadLocalCommitJob` now drains only contiguous published sequence tags, clears consumed tags, and advances `ReadCursor`; it does not reset `WriteCursor`.

Cinematic Cheats used:
- No new physical simulation. Overflow rows still enter the same same-cell Dear Lie coalescence path before consumers see them.

Exact Microseconds saved:
- Runtime measured saving: 0 us. This is a correctness hardening for async overflow, not a normal-path speed claim.
- Normal thread-local writes remain atomic-free. Overflow CAS exists only after private slice exhaustion or explicit external interrupt injection.

Verification:
- Brace count: `SignalWardenRuntime.cs` `255/255`.
- `git diff --check` passed for SHINOBU-touched files except LF-to-CRLF warnings on `GlobalSignals.cs`, `SignalWardenRuntime.cs`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Source-only forbidden scan produced no matches for stale overflow writer names, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, removed `Reserved0` field references on `SignalWardenMockDamageSignal`, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `ConcurrentQueue`, or `lock (`.
- `Interlocked` scan shows legacy `GlobalSignals` counters plus SHINOBU overflow slow-path CAS/sequence operations only. Normal thread-local slice writes remain atomic-free.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

## 2026-05-20 - CSV Vault Scratch And Capacity Asset Patch

What was wrong:
- `SignalThreadContentionCsvHotSwap` existed, but `Assets/StreamingAssets/signal_corridor_capacities.csv` was absent. Task 19 had parser code without a checked-in human tuning source.
- The contention parser borrowed `SignalTuningTable` scratch buffer `73042`, which weakened the SHINOBU_200 Vault proof because the final H-Phi report could not list a SHINOBU-owned CSV scratch handle.
- Platform label hashing was case-sensitive at the byte level.

What was done:
- Added SHINOBU-owned Vault buffer `73055` as `SignalThreadContentionCsvScratch byte[8192]`, requested with `NativeArrayOptions.UninitializedMemory`.
- Rewired `SignalThreadContentionCsvHotSwap` to read CSV bytes from `SignalThreadLocalScratchpad.TryGetCsvScratch(...)`.
- Added `Assets/StreamingAssets/signal_corridor_capacities.csv` and `signal_corridor_capacities.csv.meta`.
- Lowercased ASCII platform bytes before FNV-1a hashing without allocating normalized strings.
- Updated `Docs/ARCHITECTURE/SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md`, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Docs/Tasks/Status_SHINOBU_200.md`, and `Docs/AgentLogs/Rationale_SHINOBU_200.md`.

Cinematic Cheats used:
- No physical simulation was added. The same Dear Lie remains: dense per-event truth is collapsed into one representative committed event per AUP cell before downstream presentation/audio/VFX consumers see it.

Exact Microseconds saved:
- Runtime measured saving: 0 us. No build/profiler run was launched.
- Static engineering impact: hot path unchanged; one 8192-byte cold Vault scratch buffer replaces any temptation to allocate managed CSV staging. The capacity source now exists for designer control without recompilation.

Verification:
- Brace count: `SignalWardenRuntime.cs` `259/259`.
- Source-only forbidden scan produced no matches in SHINOBU-owned source for `NativeQueue<SignalWardenMockDamageSignal>`, `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `FloatMode.Fast`, `Pack=`, `ConcurrentQueue`, `lock (`, `string.Split`, `int.Parse`, `UnityEngine.Random`, or DTO object-initializer regressions.
- Buffer ID collision scan: `73055` appears only in `SignalWardenRuntime.cs` after this patch.
- `git diff --check` passed for `SignalWardenRuntime.cs`, the new CSV, the new CSV `.meta`, and SHINOBU docs except existing LF-to-CRLF warnings on `SignalWardenRuntime.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Build not launched.

<SELF_AUDIT id="SHINOBU_200" pass="CSV_VAULT_PATCH">
  <task_reconciliation>
    <task id="01" status="PASS">Core mock `NativeQueue&lt;MacroCollisionSignal&gt;.ParallelWriter` removed; broad unrelated queues left outside SHINOBU ownership.</task>
    <task id="02" status="PASS">SHINOBU DTO rows guarded at 64 bytes with explicit offsets.</task>
    <task id="03" status="PASS">Mock producer uses `[NativeSetThreadIndex]`; normal insertion has no shared cursor.</task>
    <task id="04" status="PASS">Hot entity signal property removed; raw field plus `ReadEntityId()` cold/generic path remains.</task>
    <task id="05" status="PASS">`GenerateSignalThreadContentionMockJob` exists for deterministic stress generation.</task>
    <task id="06" status="PASS">Thread-local byte scratchpads live in Vault buffers `73043`/`73044`.</task>
    <task id="07" status="PASS_WITH_DOD_DEVIATION">Commit is deterministic serial hash-bucket fusion instead of pure prefix-copy because coalescence changes output cardinality.</task>
    <task id="08" status="PASS">Same AUP-cell mock damage rows coalesce before snapshot publication.</task>
    <task id="09" status="PASS">CSV/tuning min-max stride and quality/VRAM curve drive active payload bytes continuously.</task>
    <task id="10" status="PASS">Front/back Vault buffers swap before commit scheduling.</task>
    <task id="11" status="PASS_WITH_DOD_DEVIATION">Vault-backed overflow ring replaces requested `NativeQueue` wording because Vault has no queue primitive; ring still merges into snapshot.</task>
    <task id="12" status="PASS">AUP hash subtracts sector origin before float3 quantization.</task>
    <task id="13" status="PASS">Telemetry flags mark rollback exclusion; scratch/snapshots are transient.</task>
    <task id="14" status="PASS">Orphaned header autopsy job tags stale producers on cold cadence.</task>
    <task id="15" status="PASS">Scratch, committed rows, buckets, overflow rows, and CSV scratch use uninitialized Vault memory; cursors define valid ranges.</task>
    <task id="16" status="PASS">300-entry telemetry ring and `Dump_SHINOBU_200.bin` path exist.</task>
    <task id="17" status="PASS">SHINOBU jobs use deterministic synchronous Burst flags.</task>
    <task id="18" status="PASS">UI Toolkit Signal Contention tuner exists, editor-only.</task>
    <task id="19" status="PASS">CSV parser now has checked-in `signal_corridor_capacities.csv` and owned Vault scratch `73055`.</task>
    <task id="20" status="PASS">Scene View heatmap draws committed AUP-cell density, editor-only.</task>
  </task_reconciliation>
  <struct_layout name="SignalWardenMockDamageSignal" size="64">
    <field name="Aup" offset="0" size="24" />
    <field name="Normal" offset="24" size="12" />
    <field name="Damage" offset="36" size="4" />
    <field name="EntityId" offset="40" size="4" />
    <field name="Flags" offset="44" size="1" />
    <field name="SourceThread" offset="45" size="1" />
    <field name="BatchId" offset="46" size="2" />
    <field name="Frame" offset="48" size="4" />
    <field name="AupCellHash" offset="52" size="4" />
    <field name="OverflowSequence" offset="56" size="8" />
    <math>24+12+4+4+1+1+2+4+4+8=64; one 64-byte cache line.</math>
  </struct_layout>
  <scalability_curve>Below quality 0.3, `ResolveActiveStrideBytes` drives active slice bytes toward CSV/tuning minimum via `quality * lerp(1,0.25,vramPressure)` and cubic smoothstep; the commit still runs but copies only cursor-valid bytes, coalesces same-cell rows, and pushes saturation to bounded overflow. Higher quality expands the lock-free window before overflow.</scalability_curve>
  <h_phi_vault_status private_owned_allocations="zero">
    <buffer id="73043" name="SignalThreadFrontBytes" />
    <buffer id="73044" name="SignalThreadBackBytes" />
    <buffer id="73045" name="SignalThreadFrontHeaders" />
    <buffer id="73046" name="SignalThreadBackHeaders" />
    <buffer id="73047" name="SignalThreadCommittedSignals" />
    <buffer id="73048" name="SignalThreadCommittedCount" />
    <buffer id="73049" name="SignalThreadContentionTelemetry" />
    <buffer id="73050" name="SignalThreadContentionTelemetryCursor" />
    <buffer id="73051" name="SignalThreadContentionTuning" />
    <buffer id="73052" name="SignalThreadCoalescenceBuckets" />
    <buffer id="73053" name="SignalThreadOverflowSignals" />
    <buffer id="73054" name="SignalThreadOverflowHeader" />
    <buffer id="73055" name="SignalThreadContentionCsvScratch" />
  </h_phi_vault_status>
  <dependency_graph>
    <job name="GenerateSignalThreadContentionMockJob" consumes="caller dependency" outputs="mock producer handle" noalias="Bytes,Headers,OverflowSignals,OverflowHeader" />
    <job name="SignalThreadLocalCommitJob" consumes="producer dependency" outputs="commit handle" noalias="ReadBytes,ReadHeaders,Output,OutputCount,CoalescenceBuckets,OverflowSignals,OverflowHeader,Telemetry,TelemetryCursor" />
    <job name="SignalThreadLocalOrphanedLockAutopsyJob" consumes="cold tick dependency" outputs="autopsy handle" noalias="Headers" />
  </dependency_graph>
  <compile_guard status="SCOPED_PASS_PREEXISTING_CORE_REFERENCES">SHINOBU_200 added no asmdef or sibling source reference. Existing `Hecton8.Core.asmdef` sibling references predate this lane and remain outside this proof.</compile_guard>
  <dear_lie before="O(events) granular downstream consumers plus earlier O(N^2) coalescence scan" after="O(events) expected hash-bucket commit plus one representative payload per AUP cell" />
  <compile_status>NOT_LAUNCHED</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Burst Atomic Read Normalization

What was wrong:
- The sequence-tagged overflow patch used `Interlocked.Read` for 64-bit header and slot sequence loads.
- Project precedent for deterministic Burst-side atomic 64-bit reads uses `Interlocked.CompareExchange(ref value, 0L, 0L)` rather than `Interlocked.Read`.

What was done:
- Replaced SHINOBU-owned `Interlocked.Read` calls with local `AtomicRead(ref long)` helpers using `CompareExchange(ref value, 0L, 0L)`.
- Kept CAS reservation and sequence publish only in the overflow slow path.
- Verified normal thread-local producer writes still use raw worker-local slices and no shared insertion cursor.

Cinematic Cheats used:
- No simulation added. Same-cell Dear Lie coalescence remains the downstream load-shedding mechanism.

Exact Microseconds saved:
- Runtime measured saving: 0 us. This is compile-risk reduction and platform-intrinsic discipline, not a speed claim.

Verification:
- Brace count: `SignalWardenRuntime.cs` `259/259`.
- `git diff --check` passed for SHINOBU-touched source/docs/CSV except LF-to-CRLF warnings on `SignalWardenRuntime.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Source-only forbidden scan produced no matches for `Interlocked.Read`, stale overflow writer names, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, removed `Reserved0` field references on `SignalWardenMockDamageSignal`, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `ConcurrentQueue`, `lock (`, `string.Split`, `int.Parse`, or `UnityEngine.Random`.
- Atomic scope scan shows only overflow slow-path `CompareExchange`, sequence publish/clear, and read-cursor advance in SHINOBU source.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

## 2026-05-20 - CSV Platform Selection Correction

What was wrong:
- `SignalThreadContentionCsvHotSwap` computed a hash for each platform label, then applied every valid row.
- With the checked-in CSV order, `rtx4090` could override `quest3`, `steamdeck`, `mx350`, and `pc` rows during cold load.

What was done:
- Reworked parsing to scan rows into a local `ParsedCapacityRow` value struct.
- Added cold target hash selection for `quest3`, `steamdeck`, `mx350`, `rtx4090`, and fallback `pc`.
- Applied the exact target row only; `pc` is used only when no exact row exists.
- Kept parsing on `ReadOnlySpan<byte>` with manual integer parsing and ASCII lowercase hashing.

Cinematic Cheats used:
- No simulation added. Correct row selection feeds the existing continuous stride curve, so low-tier devices collapse event capture earlier and high-end devices preserve a larger lock-free window.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Hot path unchanged; this prevents cold misconfiguration that would over-allocate weak-device signal slices.

Verification:
- First source patch placed `ParsedCapacityRow` in the neighboring `SignalPriorityCsvHotSwap`; corrected it into `SignalThreadContentionCsvHotSwap` before final static gates.
- Brace count: `SignalWardenRuntime.cs` `272/272`.
- `git diff --check` passed for SHINOBU-touched source/docs/CSV except LF-to-CRLF warnings on `SignalWardenRuntime.cs` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Source-only forbidden scan produced no matches for `Interlocked.Read`, stale overflow writer names, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, removed `Reserved0` field references on `SignalWardenMockDamageSignal`, `Pack=1`, `FloatMode.Fast`, DTO `EntityId` property, `ConcurrentQueue`, `lock (`, `string.Split`, `int.Parse`, `Dictionary<`, `ToLowerInvariant`, or `UnityEngine.Random`.
- Build not launched. Latest CPU sample was 100 percent, above the 50 percent guard; no `csc.exe` or `dotnet.exe` was running.

## 2026-05-20 - Final Static Forensics Before Compile Gate

What was wrong:
- Runtime compile/profiler proof is still unavailable because the machine is under the explicit CPU guard.
- The compile-wall claim must be scoped precisely. SHINOBU_200 added no assembly references, but the existing monolithic `Hecton8.Core.asmdef` still contains legacy sibling-domain references that predate this lane.

What was done:
- Re-read `Docs/Tasks/Status_SHINOBU_200.md` and `Docs/AgentLogs/Rationale_SHINOBU_200.md` before reporting.
- Re-scanned SHINOBU-owned source for forbidden hot-path patterns.
- Reconfirmed `SignalWardenRuntime.cs` brace balance at `274/274`.
- Reconfirmed Burst attributes on all four SHINOBU jobs are deterministic synchronous attributes.
- Reviewed `RunMockContention()` `JobHandle.Complete()` calls and scoped them to an editor menu diagnostic action, not gameplay frame cadence.
- Sampled the build guard: `CPU=100 DOTNET=0 CSC=0`; no build was launched.

Cinematic Cheats used:
- No new simulation was introduced. The active Dear Lie is still AUP-cell coalescence: dense granular impacts collapse into one representative 64-byte payload per local cell before downstream consumers spend work.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Compile/runtime profiling remains blocked by CPU guard.
- Static algorithmic improvement remains the proof: previous dense same-cell output scan was O(N^2); current commit uses expected O(N) hash buckets plus bounded O(overflow) drain.

Verification:
- Source-only forbidden scan produced no matches for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `FloatMode.Fast`, `Pack=`, `ConcurrentQueue`, `lock (`, `string.Split`, `int.Parse`, `UnityEngine.Random`, or hot DTO get/set property regressions.
- `git diff --check` passed for SHINOBU-touched files except line-ending warnings on existing tracked files.
- Build not launched under project policy.

<SELF_AUDIT id="SHINOBU_200" pass="FINAL_STATIC_FORENSICS">
  <task_reconciliation status="SEE_FINAL_REPORT">Tasks 01-20 remain recorded in this log and Status_SHINOBU_200.md with DOD deviations for deterministic commit and Vault overflow ring.</task_reconciliation>
  <struct_layout primary="SignalWardenMockDamageSignal" size="64" math="24+12+4+4+1+1+2+4+4+8=64" />
  <scalability_curve evidence="CSV/tuning min/max stride endpoints feed continuous active-slice math; quality/VRAM pressure adjusts active payload bytes without binary tier branches." />
  <h_phi_vault_status buffers="73043..73055" private_allocations="zero-owned; SHINOBU scratchpad now persists only VaultGenerationHandle<T> descriptors" />
  <dependency_graph output="producer handle -> commit handle -> cold autopsy handle; no gameplay Complete()" />
  <compile_guard status="SCOPED">No SHINOBU asmdef/source reference added; pre-existing Hecton8.Core.asmdef sibling references remain an integrator-level compile-wall debt.</compile_guard>
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - CSV Exact-Read Hardening

What was wrong:
- `SignalThreadContentionCsvHotSwap.TryLoad` depended on a single `FileStream.Read(Span<byte>)`.
- That can parse a prefix if the stream returns short, and it did not reject oversized authoring files before parsing.
- The same cold-path hazard existed in the neighboring Core signal tuning CSV loader touched during this pass.

What was done:
- Added empty-file and oversized-file rejection before parsing.
- Added exact byte-count reads into Vault scratch and fail-fast short-read handling.
- Kept parsing on `ReadOnlySpan<byte>` with no `File.ReadAllBytes`, no `string.Split`, no `int.Parse`, and no managed dictionary.

Cinematic Cheats used:
- No simulation added. This protects the existing capacity curve that feeds the AUP-cell Dear Lie coalescence path.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Hot path unchanged.
- Failure avoided: silent prefix parse of capacity CSV, which could over-allocate weak hardware or under-feed high-end hardware.

Verification:
- Brace count: `SignalWardenRuntime.cs` `274/274`.
- Forbidden source scans produced no matches for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `FloatMode.Fast`, `Pack=`, `ConcurrentQueue`, `lock (`, `string.Split`, `int.Parse`, `UnityEngine.Random`, or hot DTO get/set property regressions.
- `git diff --check` passed for SHINOBU-touched files except existing line-ending warnings.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="CSV_EXACT_READ">
  <task id="19" status="PASS_HARDENED">Capacity CSV now has a checked-in source, owned Vault scratch, exact platform selection, and exact-read file loading.</task>
  <hot_path status="UNCHANGED">File I/O and string device probes are cold boot/editor tuning paths only.</hot_path>
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Vault Generation Handle Phase-Local Resolve

What was wrong:
- `SignalThreadLocalScratchpad` held static `NativeArray<T>` aliases for the SHINOBU-owned Vault buffers.
- Those aliases did not own memory, but they made the source look like private persistent arrays and weakened the DataVault/H-PHI proof.
- The class also persisted legacy pointer-bearing `VaultBufferHandle<T>` descriptors.

What was done:
- Replaced SHINOBU-owned handles with `VaultGenerationHandle<T>` descriptors for `73043..73055`.
- Removed SHINOBU-owned static `NativeArray<T>` aliases.
- Resolved phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle(...)` immediately before job scheduling, overflow ingress, telemetry readback, tuning mutation, CSV scratch access, and editor snapshot access.
- Added same-vault stale-generation recovery: failed generation resolves clear the initialized flag and reacquire descriptors on the cold initialization path.
- Updated the route card and binary payload ledger to state the handle-only boundary.

Cinematic Cheats used:
- No simulation added. The existing Dear Lie remains AUP-cell coalescence before downstream audio/render/UI consumers spend work.

Exact Microseconds saved:
- Runtime measured saving: 0 us. This is data-sovereignty and stale-pointer risk reduction, not a measured speed patch.

Verification:
- Brace count: `SignalWardenRuntime.cs` `289/289`.
- SHINOBU scratchpad source scan is absent for static front-byte/tuning alias assignments, static header aliases, front-byte legacy pointer-handle storage, and front-byte legacy pointer-handle acquisition.
- Forbidden source scans remain absent for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, `Dictionary<`, and `UnityEngine.Random`.
- `git diff --check` passed for SHINOBU-touched files except LF-to-CRLF warnings on tracked files.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="VAULT_GENERATION_HANDLE_PHASE_LOCAL_RESOLVE">
  <h_phi_vault_status buffers="73043..73055" private_native_array_aliases="removed_for_SHINOBU_scratchpad" persistent_handle_shape="VaultGenerationHandle<T>" />
  <hot_path status="UNCHANGED">Burst jobs still receive concrete NativeArray fields; producer inner loop still writes by NativeSetThreadIndex with no shared cursor.</hot_path>
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - NaN Vaccine And Active-Slice Commit Clamp

What was wrong:
- The AUP cell hash depended on caller-provided sector origin input, which could become non-finite in future integrations.
- The commit pass bounded cursor reads by the full thread stride instead of the header-recorded active stride.

What was done:
- `SignalThreadLocalAupHash.ComputeCellHash(...)` returns sentinel hash `1u` for non-finite signal AUPs, non-finite sector origins, or overflowed local float casts.
- `SignalThreadLocalCommitJob` clamps `WriteCursorBytes` to `min(header.ActiveStrideBytes, ThreadStrideBytes)` before reading payload rows.
- Route card and ledger now document the NaN sentinel path and active-slice boundary.

Cinematic Cheats used:
- No simulation added. Sentinel hashing preserves deterministic coalescence instead of escalating into expensive recovery logic.

Exact Microseconds saved:
- Runtime measured saving: 0 us. This is NaN containment and active-range correctness.

Verification:
- Brace count: `SignalWardenRuntime.cs` `289/289`.
- Burst attribute scan shows four deterministic synchronous SHINOBU jobs.
- Forbidden source scans remain clean for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, `Dictionary<`, and `UnityEngine.Random`.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="NAN_VACCINE_ACTIVE_SLICE_CLAMP">
  <hash_vaccine sentinel="1u" rejects="non_finite_aup,non_finite_sector_origin,overflowed_local_float3" />
  <commit_read_boundary source="SignalThreadLocalHeader64.ActiveStrideBytes" fallback="ThreadStrideBytes" />
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Sidecar Audit Closure

What was wrong:
- `SignalBusRegistry` still had a virtual fallback flush/clear route through `ISignalLane[]` for non-generated lanes.
- `SignalPriorityCsvHotSwap.TryLoad` could parse a prefix read and did not reject files larger than its fixed scratch.
- Scratchpad pointer paths relied on requested Vault size rather than proving resolved byte-buffer length at unsafe read/write boundaries.

What was done:
- Removed fallback interface dispatch from `FlushPreSimulation()` and `ClearPostSimulationSnapshots()`. Frame dispatch is now generated generic direct lane calls only.
- Non-generated dynamic signal lanes are marked as blocked at registration and reported in development builds instead of being virtual-dispatched every frame.
- Hardened `SignalPriorityCsvHotSwap` with empty/oversized rejection and exact-read looping.
- Added byte-buffer length fences in `SignalThreadLocalWriteContext.IsValid`, both mock writer paths, `SignalThreadLocalCommitJob.Execute`, and `ResolveBuffers`.
- Updated route card and binary payload ledger with the direct-dispatch/fail-fast fallback boundary.

Cinematic Cheats used:
- No simulation added. The existing Dear Lie remains AUP-cell coalescence; this pass removes virtual dispatch and fail-closes bad I/O/buffer states.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Static effect: removes O(fallback lanes) virtual calls from pre/post simulation. Expected fallback count is zero for generated lanes; any non-zero fallback now fails visibly.

Verification:
- Brace counts: `GlobalSignals.cs` `821/821`, `SignalWardenRuntime.cs` `303/303`.
- Source scan shows no `lane.FlushPreSimulation`, no `lane.ClearPostSimulation`, and only cold `ISignalLane[]` registry storage remains.
- Forbidden source scans remain clean for `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, `Dictionary<`, and `UnityEngine.Random` in the SHINOBU corridor.
- Build not launched. CPU/build guard sampled `CPU=100`, `dotnet=0`, `csc=0`.

<SELF_AUDIT id="SHINOBU_200" pass="SIDECAR_AUDIT_CLOSURE">
  <interface_hot_path status="REMOVED">Frame flush/clear uses generated generic direct dispatch only; interface registry is cold registration/telemetry/disposal.</interface_hot_path>
  <csv_exact_read target="SignalPriorityCsvHotSwap" status="PASS" />
  <unsafe_bounds target="thread_local_scratchpad" status="PASS" />
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Read-Only Snapshot Fence And Vault Length Validation

What was wrong:
- The editor heatmap consumed the finalized committed signal frame through a writable `NativeArray<SignalWardenMockDamageSignal>` view.
- `ResolveBuffers` previously proved handle creation but needed explicit minimum length proof for unsafe worker buffers.

What was done:
- Added `SignalThreadLocalScratchpad.TryGetCommittedSignalsReadOnly(...)` returning `NativeArray<SignalWardenMockDamageSignal>.ReadOnly`.
- Moved `SignalThreadContentionHeatmapGizmo` to the read-only snapshot accessor.
- Added minimum length checks for buffers `73043..73055` before unsafe producer/commit jobs can run.
- Updated the route card, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- No simulation added. The existing Dear Lie remains AUP-cell coalescence; this pass tightens snapshot ownership so the visualization cannot mutate the evidence it displays.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Hot producer/commit jobs are unchanged; this is H-Phi/read-only boundary hardening.

Verification:
- Brace count at that pass: `SignalWardenRuntime.cs` `295/295`; later sidecar patch changed the current count to `294/294`.
- Read-only accessor is present and the heatmap calls it.
- Forbidden source scans remain clean for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, and `UnityEngine.Random`.
- `git diff --check` passed for SHINOBU-touched files except LF-to-CRLF warnings on tracked files.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="READ_ONLY_SNAPSHOT_FENCE">
  <snapshot_access consumer="SignalThreadContentionHeatmapGizmo" surface="NativeArray<SignalWardenMockDamageSignal>.ReadOnly" />
  <vault_resolve_validation style="minimum_length_checks" buffers="73043..73055" />
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - UI Toolkit Waterfall Graph Without Per-Refresh Strings

What was wrong:
- The tuner window updated telemetry through `_metricsLabel.text`, string concatenation, and `ToString("X8")` on editor refresh.
- The UI did not implement the Task 18 waterfall graph; it only printed numeric rows.

What was done:
- Added `SignalThreadLocalScratchpad.TryGetTelemetryReadOnly(...)` returning `NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly`.
- Replaced the metrics label with `SignalThreadContentionWaterfallGraph`, a UI Toolkit visual element that draws the 300-frame telemetry ring through `Painter2D`.
- `OnInspectorUpdate` now only calls `MarkDirtyRepaint()`; it does not format telemetry strings.
- Updated route card, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- No simulation added. The graph spends editor-only visual bandwidth on the existing black-box telemetry instead of adding runtime diagnostics.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Runtime hot paths are unchanged. Editor per-refresh string churn is removed from `SignalWardenRuntime.cs`.

Verification:
- Brace count: `SignalWardenRuntime.cs` `303/303`.
- `_metricsLabel`, `.text`, and `ToString` are absent from `SignalWardenRuntime.cs`.
- Forbidden source scans remain clean for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, and `UnityEngine.Random`.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="UI_TOOLKIT_WATERFALL_GRAPH">
  <telemetry_access surface="NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly" ring="300_frames" />
  <editor_refresh status="NO_LABEL_TEXT_FORMATTING">OnInspectorUpdate marks the graph dirty only.</editor_refresh>
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 - Adjacent Core Signal Vault Handle Eviction

What was wrong:
- `SignalTuningTable` still stored static `NativeArray<T>` aliases for profiles/count/CSV scratch in the same Core signal file.
- `SignalTelemetryRingBuffer` still stored obsolete pointer-bearing `VaultBufferHandle<T>` descriptors and resolved them through `.Resolve(...)`.

What was done:
- Migrated buffers `73038..73042` to `VaultGenerationHandle<T>`.
- `SignalTuningTable` now resolves phase-local profile/count/CSV scratch views through `IDataVault.TryResolveHandle(...)` per call.
- `SignalTelemetryRingBuffer` now resolves ring/cursor views through `IDataVault.TryResolveHandle(...)`.
- Updated route card, binary payload ledger, status, and rationale.

Cinematic Cheats used:
- No simulation added. This is memory sovereignty hardening inside the existing Core signal route.

Exact Microseconds saved:
- Runtime measured saving: 0 us. Hot producer/commit jobs are unchanged; tuning and black-box access are cold/report paths.

Verification:
- Brace count: `SignalWardenRuntime.cs` `309/309`.
- `VaultBufferHandle`, `.Resolve(`, static `SignalTuningTable` NativeArray aliases, `_metricsLabel`, `.text`, and `ToString` are absent from `SignalWardenRuntime.cs`.
- Forbidden source scans remain clean for `Interlocked.Read`, `NativeQueue<SignalWardenMockDamageSignal>`, direct `NativeQueue<MacroCollisionSignal>.ParallelWriter`, `ConcurrentQueue`, `lock(`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, and `UnityEngine.Random`.
- Build not launched. CPU guard remained `100`; no `dotnet.exe` or `csc.exe` was running.

<SELF_AUDIT id="SHINOBU_200" pass="ADJACENT_CORE_SIGNAL_VAULT_HANDLE_EVICTION">
  <vault_handles buffers="73038..73042" persistent_shape="VaultGenerationHandle<T>" />
  <static_nativearray_aliases target="SignalTuningTable" status="REMOVED" />
  <legacy_resolve_calls target="SignalWardenRuntime.cs" status="ABSENT" />
  <compile_status>BLOCKED_BY_CPU_GUARD_CPU_100_DOTNET_0_CSC_0</compile_status>
</SELF_AUDIT>

## 2026-05-20 Loop 21 - SignalBus Direct Coverage And Operation-Table Fallback

What was wrong:
- Loop 18 removed hot `ISignalLane[]` fallback dispatch, but the direct-list coverage audit found a starvation risk: `135` generated Core direct lanes versus `230` distinct `SignalBus<T>` references under `Assets/_Project/Scripts`.
- The missing `95` non-direct lanes include sibling-owned or local payloads. Adding them to Core would require direct sibling payload references and violate the compile wall. Blocking them would break frame snapshots.

What was done:
- Kept generated direct generic calls for the `135` Core lanes. Verified `FlushDirectSignalLanes`, `ClearDirectSignalLaneSnapshots`, and `ResolveDirectRegistryDispatch` are aligned with zero drift.
- Added `SignalLaneDispatch[]` for non-generated lanes. Each closed `SignalBus<T>` registers cached static flush/clear operations and a cached telemetry-copy operation during cold registration.
- `FlushPreSimulation()` and `ClearPostSimulationSnapshots()` now drain fallback lanes through the operation table, not through `ISignalLane[]` virtual calls.
- `CopyTelemetry(...)` now uses cached closed-generic telemetry delegates instead of calling `ISignalLane.CopyTelemetry(...)` through the interface array.
- Updated route card and binary ledger so the proof matches source.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains event coalescence: many same-cell impacts collapse into one downstream signal, so audio/VFX read perceived impact pressure rather than every granular producer write.

Exact Microseconds saved:
- Measured: `0 us`; compile/profiler proof remains blocked by CPU guard.
- Static effect: removes per-fallback-lane virtual interface calls while keeping existing non-generated `SignalBus<T>` lanes alive. Expected saving is proportional to non-generated lane count and frame cadence, pending profiler proof.

Verification:
- `GlobalSignals.cs` braces `831/831`; `SignalWardenRuntime.cs` braces `309/309`.
- Direct lane audit: `flush=135`, `clear=135`, `direct_policy=135`, drift `0`.
- Forbidden residue scan returned no matches for `lane.FlushPreSimulation`, `lane.ClearPostSimulation`, `lane.CopyTelemetry`, `_fallbackLaneIndices`, blocked fallback text, SHINOBU-owned `NativeQueue` regressions, `ConcurrentQueue`, `lock (`, `Pack=1`, `FloatMode.Fast`, `string.Split`, `int.Parse`, or `UnityEngine.Random`.
- `git diff --check` reported only LF-to-CRLF warnings on tracked files.
- Build not launched. Latest build guard: `CPU=100`, `dotnet=0`, `csc=0`.

<SELF_AUDIT id="SHINOBU_200" pass="SIGNALBUS_DIRECT_COVERAGE_OPERATION_TABLE">
  <direct_lane_coverage flush="135" clear="135" direct_policy="135" drift="0" />
  <broader_signalbus_scan distinct_types="230" non_direct_types="95" route="closed_generic_operation_table" />
  <interface_hot_path status="REMOVED_FROM_FLUSH_CLEAR_TELEMETRY">No `lane.FlushPreSimulation`, `lane.ClearPostSimulation`, or `lane.CopyTelemetry` calls remain in `SignalBusRegistry` frame/diagnostic paths.</interface_hot_path>
  <compile_guard>Build not launched; CPU guard still requires a safe window below 50 percent and no active compiler.</compile_guard>
</SELF_AUDIT>
