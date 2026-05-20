# LOG_SHINOBU_160

## 2026-05-20 - Active Polish Re-Entry

What was wrong:
- Active SHINOBU status/rationale/log files were absent after Batch010 archival.
- Runtime still used `Time.frameCount` in dispatcher-owned analytics state.
- Emergency mock events used a custom LCG instead of `Unity.Mathematics.Random`.
- Public facade could attempt ingress enqueue without an active exporter owner.

What was done:
- Restored active SHINOBU_160 status/rationale/log files from archive as the active memory base.
- Patched frame identity to `DispatcherTimingDTO.FrameId` with local fallback.
- Patched mock RNG to `Unity.Mathematics.Random` with `SystemHash ^ SectorHash ^ SimulationFrame`.
- Patched `TryRecordEvent` to fail closed when inactive.
- Replaced hot DTO object initializers with `default` field assignment.

Cinematic Cheats used:
- Analytics remains batched external observation, not gameplay truth. The server sees compressed chunks, not per-event live streaming.

Exact Microseconds saved:
- Frame-domain repair: estimated <1 us/frame; profiler proof absent.
- Fail-closed ingress: 0 us normal path, prevents stale native writes.
- Main-thread JSON/web avoidance remains estimated 100-5000 us per telemetry burst, pending profiler proof.

Verification:
- Static runtime scan clean after this patch: no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, file byte-array helpers, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, public `ParallelWriter`, `H8Memory.CreateNativeArrayView`, or `Directory.GetFiles`.
- Brace count clean: `Open=209 Close=209 Delta=0`.
- `git diff --check` clean for SHINOBU runtime/test/docs touched in this pass except the existing CRLF warning on `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Compile/import not launched in this pass because CPU guard samples were `75.82, 99.81, 86.96` average `87.53%`, and prior Unity import is already blocked by foreign-domain compile errors with no SHINOBU-specific compiler errors.

<SELF_AUDIT agent="SHINOBU_160" status="ACTIVE_POLISH_STATIC_VERIFIED_COMPILE_NOT_LAUNCHED">
  <task_reconciliation>
    <task id="01" name="UNITY_WEB_REQUEST_ERADICATION" result="PASS_STATIC" />
    <task id="02" name="JSON_SERIALIZATION_PURGE" result="PASS_STATIC" />
    <task id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS_STATIC" />
    <task id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS_STATIC" />
    <task id="05" name="EMERGENCY_MOCK_EVENT_GENERATOR" result="PASS_STATIC_REPAIRED_RNG" />
    <task id="06" name="BURST_EVENT_INGESTION_KERNEL" result="PASS_STATIC" />
    <task id="07" name="BACKGROUND_I_O_THREAD_MANAGEMENT" result="PASS_STATIC" />
    <task id="08" name="THE_DEAR_LIE_BATCHED_TRANSMISSION" result="PASS_STATIC" />
    <task id="09" name="DISK_FALLBACK_ROUTING" result="PASS_STATIC" />
    <task id="10" name="CONTINUOUS_SCALABILITY_QUEUE_CULLING" result="PASS_STATIC" />
    <task id="11" name="HEATMAP_DATA_AGGREGATION" result="PASS_STATIC" />
    <task id="12" name="CRITICAL_EVENT_PRIORITIZATION" result="PASS_STATIC" />
    <task id="13" name="AUP_PRECISION_SERIALIZATION" result="PASS_STATIC" />
    <task id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS_STATIC" />
    <task id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS_STATIC" />
    <task id="16" name="TELEMETRY_EXPORTER_RECORDER" result="PASS_STATIC" />
    <task id="17" name="BINARY_COMPRESSION_INTEGRATION_JOB" result="PASS_STATIC_RLE_NOT_FAKE_LZ4" />
    <task id="18" name="ANALYTICS_TUNER_EDITOR_WINDOW" result="PASS_STATIC" />
    <task id="19" name="CSV_ENDPOINT_CONFIGURATION_INGESTOR" result="PASS_STATIC" />
    <task id="20" name="LIVE_HEATMAP_DEBUG_GIZMO" result="PASS_STATIC" />
  </task_reconciliation>
  <struct_layout name="AnalyticEventDTO" size="32" alignment="multiple_of_16">
    <field name="EventHashID" offset="0" size="4" />
    <field name="TimestampSeconds" offset="4" size="4" />
    <field name="EventAUP" offset="8" size="24" />
    <padding bytes="0" />
    <proof>4+4+24=32; 32 mod 16 = 0; no Pack=1; public fields only.</proof>
  </struct_layout>
  <vault_buffers ids="71860..71873" persistent_private_arrays="0" />
  <scalability_curve>Routine retention uses GlobalQualityWeight through smoothstep and math.lerp from low threshold 10 to ultra threshold 1000; pressure uses math.step to probabilistically drop non-critical events before NativeQueue growth. Critical high-bit events bypass routine cull.</scalability_curve>
  <dependency_graph>Producer facade -> NativeQueue ingress -> POST_SIMULATION Burst Run drain -> Vault handoff -> H8_Analytics_IO. No returned JobHandle because IDispatcherSystem.PostSimulationTick has void boundary; jobs are synchronous Burst kernels and worker I/O is isolated.</dependency_graph>
  <noalias>ProcessAnalyticsQueueJob and CompressAnalyticsBufferJob fields use NoAlias/ReadOnly where applicable.</noalias>
  <compile_guard>No sibling runtime assembly reference was added; no Hecton8.World using exists in runtime exporter.</compile_guard>
  <dear_lie>Server stream is faked by batched RLE chunks: O(N events) local accumulation plus one network/file write per threshold instead of O(N network sends). Gameplay truth never waits for analytics.</dear_lie>
</SELF_AUDIT>

## 2026-05-19 - ASYNCHRONOUS_TELEMETRY_AND_HEATMAP_EXPORTER

What was wrong:
- Active first-party runtime source had no verified native async gameplay analytics route for deaths/resources/routes/perf spikes.
- Scan found no first-party gameplay analytics `UnityWebRequest` or JSON sender to eradicate. Existing hits were cold data/config/dev smoke/editor/vendor routes outside SHINOBU_160 domain.
- Analytics had no SHINOBU-owned Vault forensic ring, no fixed DTO layout proof, no background network/compression worker, and no 300-frame black-box dump route for exporter failures.

What was done:
- Added `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`.
- Added explicit 32-byte `AnalyticEventDTO`: `uint EventHashID` offset 0, `uint TimestampSeconds` offset 4, `double3 EventAUP` offset 8.
- Added owner-local Vault buffers `71860..71873` for event ring, staging, counters, telemetry ring, cursor, tuning, CSV scratch, compressed scratch, heatmap debug, double handoff, worker accumulation, raw batch scratch, and dump snapshot.
- Added `AnalyticsEventIngress` native queue, registered with `NativeMemorySentinel`; DataVault owns forensic arrays while the queue stays exporter-owned because `IDataVault` has no queue allocation API.
- Added Burst mock generator for 500 synthetic DTOs/sec behind a tuning flag.
- Added Burst POST_SIMULATION queue drain with finite AUP validation, routine-event culling by continuous `GlobalQualityWeight`, critical-event bypass, staging copy, event-ring write, telemetry write, and black-box fault dump.
- Added dedicated background thread `H8_Analytics_IO` for batch accumulation, RLE compression, HTTP POST via `HttpWebRequest`, disk fallback `.h8log`, and backlog replay. No `UnityWebRequest`, `JsonUtility`, `ToJson`, `HttpClient`, `SendAsync`, `Task.Run`, or `ThreadPool` in SHINOBU runtime/editor source.
- Added exact little-endian IEEE754 double serialization for AUP, avoiding `float3` and JSON numeric drift.
- Added KCC route heatmap sampler from `SignalBus<KccVelocitySignal>` using full AUP every configurable 5 seconds.
- Added UI Toolkit editor tuner at `Hecton8/Diagnostics/Asynchronous Telemetry`.
- Added editor layout tests in `Assets/_Project/Tests/Editor/AsynchronousTelemetryExporterEditTests.cs`.
- Added CSV endpoint config at `Assets/_Project/Data/Analytics/analytics_endpoint.csv`; later added XML-literal `Assets/_Project/Data/Analytics/telemetry_config.csv`.
- Added architecture route doc `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`.
- Added SHINOBU_160 section to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic cheats used:
- Dear-lie batching: visible game receives no per-event network truth; exporter ships delayed analytical truth by byte threshold or 60-second timer.
- Sparse route heatmap: KCC route samples default to 5 seconds instead of per-frame movement spam.
- Routine event pressure valve: continuous quality threshold maps from 10 retained routine events on weak devices to 1000 on visual-overkill tiers.
- RLE payload envelope: simple deterministic compression was used instead of fake LZ4 because no verified native LZ4 binding was proven during this pass.
- Editor-only heatmap gizmo: debug visibility is outside player runtime.

Exact microseconds saved:
- Main-thread UnityWebRequest removal: no gameplay offender found; 0 us measured removal, risk closed for SHINOBU route.
- JSON/string serialization avoidance: estimated 100-5000 us saved per telemetry burst versus managed JSON/network path; not profiler-measured because compile/play verification was blocked.
- Native DTO enqueue path: target <1 us per ordinary event under capacity on i3/MX350; static estimate only.
- POST_SIMULATION cull under backlog: estimated 10-1000 routine events avoided per frame depending on `GlobalQualityWeight`; exact frame saving pending profiler.
- Background compression/network: main-thread network/compression cost moved to `H8_Analytics_IO`; exact runtime saving unmeasured until legal compile/play window.

Verification state:
- Static forbidden-source scan: PASS for SHINOBU runtime/editor source.
- Buffer ID collision scan for `71860..71873`: PASS in active source/architecture docs.
- Brace-balance scan: PASS for new runtime/editor/test files.
- Non-ASCII scan: PASS for new SHINOBU files.
- `git diff --check`: PASS for touched files except a line-ending warning on `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Compile/test: BLOCKED_BY_CPU_GUARD. First CPU check averaged `99.68%`; retry averaged `82.63%`. No active `dotnet`/`csc`, but AGENTS forbids dotnet launch above 50%.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="IMPLEMENTED_STATIC_COMPILE_BLOCKED">
  <dto name="AnalyticEventDTO" bytes="32" eventHashOffset="0" timestampOffset="4" aupOffset="8" aupType="double3" />
  <vaultBuffers first="71860" last="71873" collisionScan="pass" />
  <hotPath gcBytes="0-intended" managedStrings="none" json="none" unityWebRequest="none" />
  <thread name="H8_Analytics_IO" compression="RLE" network="HttpWebRequest-background-only" diskFallback="h8log" />
  <blackBox frames="300" dump="Docs/AgentLogs/Dump_SHINOBU_160.bin" alias="Docs/AgentLogs/Dump_ANALYTICS_CRASH.bin" />
  <verification compile="blocked-by-cpu-guard" cpuAverageRetry="82.63" />
</SELF_AUDIT>
```

## 2026-05-20 - Vault-Owned Ingress Rings Chronology Tail

What was wrong:
- Runtime ingress still used exporter-owned persistent `NativeQueue<AnalyticEventDTO>` lanes.
- That contradicted the stricter current Vault Law wording and the original SHINOBU_160 requirement for a native DTO ring buffer in Vault.
- Mock pressure accounting reported requested mock count, not actual writes when ingress was saturated.

What was done:
- Removed `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, queue labels, `TryGetQueues`, and `PrewarmQueue` from the runtime exporter.
- Added `71874 RoutineIngress`, `71875 CriticalIngress`, and `71876 IngressCursor`.
- Added explicit 64-byte `AnalyticsIngressCursorDTO` with routine/critical cursors, capacities, overflow counters, frame, hash, and padding.
- Rewired hot `TryRecordEvent` to write locked Vault ingress pointers through `UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>` and `AnalyticEventDTO*`.
- Rewired `GenerateMockAnalyticsEventsJob` and `ProcessAnalyticsQueueJob` to use Vault-backed `NativeArray` ingress rings with `[NoAlias]`.
- Mock fallback now adds actual routine cursor delta into `_hotEnqueuedDelta` and `_ingressPendingEstimate`.

Cinematic Cheats used:
- No route simulation was added. The exporter samples owner facts and lets offline analysis reconstruct heatmaps.
- Fixed ring memory makes telemetry invisible to frame time: overflow is dropped/accounted instead of growing a queue or widening the drain.

Exact Microseconds saved:
- Normal hot path remains O(1); no profiler number claimed because Unity compile remains blocked by foreign domains.
- Removes native queue growth/prewarm dependency and per-queue lifecycle cleanup from gameplay runtime.
- Avoids hidden queue block allocation under bursty telemetry pressure; static estimate remains low single-digit microseconds per pressured event, unmeasured.

Verification state:
- Runtime scan found no `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, `new NativeQueue`, `TryGetQueues`, `PrewarmQueue`, `RoutineQueue`, `CriticalQueue`, or queue-label remnants.
- Runtime forbidden scan found no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, runtime reflection offset guard, `File.Delete(finalPath)`, or binary `pressureCull`.
- Private persistent native collection field scan returned no runtime matches.
- Brace balance after patch: `Open=267 Close=267 Delta=0`.
- `git diff --check` on changed runtime/test C# files returned clean.
- Dotnet/Unity build was not launched by explicit user order and because archived Unity logs already show the foreign compile wall with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_VAULT_INGRESS_RINGS">
  <taskReconciliation count="20" task01="PASS_STATIC" task02="PASS_STATIC" task03="PASS_STATIC" task04="PASS_STATIC" task05="PASS_STATIC" task06="PASS_STATIC" task07="PASS_STATIC" task08="PASS_STATIC" task09="PASS_STATIC" task10="PASS_STATIC" task11="PASS_STATIC" task12="PASS_STATIC" task13="PASS_STATIC" task14="PASS_STATIC" task15="PASS_STATIC" task16="PASS_STATIC" task17="PASS_STATIC" task18="PASS_STATIC" task19="PASS_STATIC" task20="PASS_STATIC" />
  <structLayout name="AnalyticsIngressCursorDTO" bytes="64" offsets="RoutineWriteCursor@0:uint,RoutineReadCursor@4:uint,CriticalWriteCursor@8:uint,CriticalReadCursor@12:uint,RoutineCapacity@16:int,CriticalCapacity@20:int,RoutineOverflowDrops@24:uint,CriticalOverflowDrops@28:uint,LastFrameIndex@32:uint,StateHash@36:uint,Reserved0@40:uint,Reserved1@44:uint,Reserved2@48:uint,Reserved3@52:uint,Reserved4@56:uint,Reserved5@60:uint" alignment="64-byte control row" />
  <vaultBuffers ids="71860..71876" newIds="71874:RoutineIngress,71875:CriticalIngress,71876:IngressCursor" persistentPrivateNativeCollections="0" />
  <dependencyGraph consumes="DispatcherTimingDTO,SignalBus snapshots,GlobalQualityWeight,Vault handles" outputs="Vault ingress rings,EventRing,Staging,Telemetry,H8_Analytics_IO handoff" noAlias="GenerateMockAnalyticsEventsJob and ProcessAnalyticsQueueJob ingress/event/staging fields" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-launched" reason="user order plus foreign dependency wall already logged" />
  <dearLie before="unbounded/growing ingress abstraction risk" after="fixed Vault rings with representative deterministic culling" complexityBefore="O(1)-enqueue-with-hidden-growth-risk" complexityAfter="O(1)-bounded-ring-write-and-O(min(backlog,qualityBudget))-drain" />
</SELF_AUDIT>
```

## 2026-05-20 - Vault-Owned Ingress Rings

What was wrong:
- Runtime ingress still used exporter-owned persistent `NativeQueue<AnalyticEventDTO>` lanes.
- That contradicted the stricter current Vault Law wording and the original SHINOBU_160 requirement for a native DTO ring buffer in Vault.
- Mock pressure accounting reported requested mock count, not the number actually written if ingress capacity was already saturated.

What was done:
- Removed `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, queue labels, `TryGetQueues`, and `PrewarmQueue` from the runtime exporter.
- Added `71874 RoutineIngress`, `71875 CriticalIngress`, and `71876 IngressCursor`.
- Added explicit 64-byte `AnalyticsIngressCursorDTO` with routine/critical read/write cursors, capacities, overflow counters, last frame, state hash, and padding.
- Rewired `TryRecordEvent` to write into locked Vault ingress pointers through `UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>` and `AnalyticEventDTO*`.
- Rewired `GenerateMockAnalyticsEventsJob` and `ProcessAnalyticsQueueJob` to use Vault-backed `NativeArray` ingress rings with `[NoAlias]`.
- Mock fallback now adds the actual routine cursor delta into `_hotEnqueuedDelta` and `_ingressPendingEstimate`, not the requested event count.
- EditMode source guards now reject `NativeQueue<AnalyticEventDTO>`, `new NativeQueue`, `TryGetQueues`, and `PrewarmQueue` in the runtime source.

Cinematic Cheats used:
- The exporter still does no route simulation. It samples owner facts and lets offline analysis reconstruct heatmaps.
- Fixed ring memory makes telemetry invisible to frame time: overflow is dropped/accounted instead of growing a queue or widening the drain.

Exact Microseconds saved:
- Normal hot path remains O(1); no measured profiler number claimed because Unity compile remains blocked by foreign domains.
- Removes native queue growth/prewarm dependency and per-queue lifecycle cleanup from gameplay runtime.
- Avoids hidden queue block allocation under bursty telemetry pressure; static estimate remains low single-digit microseconds per pressured event versus dynamic native queue growth risk, unmeasured.

Verification state:
- Runtime scan found no `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, `new NativeQueue`, `TryGetQueues`, `PrewarmQueue`, `RoutineQueue`, `CriticalQueue`, or queue-label remnants.
- Runtime forbidden scan found no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, runtime reflection offset guard, `File.Delete(finalPath)`, or binary `pressureCull`.
- Private persistent `NativeArray`/`NativeList`/`NativeHashMap`/`NativeQueue` field scan returned no runtime matches.
- Brace balance after patch: `Open=267 Close=267 Delta=0`.
- `git diff --check` on changed runtime/test C# files returned clean.
- Dotnet/Unity build was not launched by explicit user order and because archived Unity logs already show the foreign compile wall with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_VAULT_INGRESS_RINGS">
  <taskReconciliation count="20">
    <task id="01" result="PASS_STATIC" proof="No UnityWebRequest route; worker still owns HTTP POST off main thread." />
    <task id="02" result="PASS_STATIC" proof="No JSON route; DTO remains fixed little-endian binary." />
    <task id="03" result="PASS_STATIC" proof="AnalyticEventDTO and AnalyticsIngressCursorDTO use raw fields, no hot DTO properties." />
    <task id="04" result="PASS_STATIC" proof="AnalyticEventDTO 32 bytes; AnalyticsIngressCursorDTO 64 bytes; no Pack=1." />
    <task id="05" result="PASS_STATIC" proof="Mock generator writes Vault routine ring and accounts actual cursor delta." />
    <task id="06" result="PASS_STATIC" proof="ProcessAnalyticsQueueJob drains Vault critical/routine rings with Burst fast flags." />
    <task id="07" result="PASS_STATIC" proof="H8_Analytics_IO remains the only network/compression thread." />
    <task id="08" result="PASS_STATIC" proof="Batched handoff unchanged; ingress is fixed ring instead of growing queue." />
    <task id="09" result="PASS_STATIC" proof="Disk fallback remains worker-only .tmp to .h8log route." />
    <task id="10" result="PASS_STATIC" proof="GlobalQualityWeight still drives hot cull and drain budget continuously." />
    <task id="11" result="PASS_STATIC" proof="Heatmap debug receives accepted ring-drained events." />
    <task id="12" result="PASS_STATIC" proof="Critical ring drains before routine ring." />
    <task id="13" result="PASS_STATIC" proof="EventAUP remains double3, serialized as doubles." />
    <task id="14" result="PASS_STATIC" proof="Analytics remains owner-local diagnostics, not rollback gameplay truth." />
    <task id="15" result="PASS_STATIC" proof="Ingress event rings are UninitializedMemory and overwritten by cursor slots." />
    <task id="16" result="PASS_STATIC" proof="Telemetry ring and VaultBytes now include ingress buffers." />
    <task id="17" result="PASS_STATIC" proof="Compression remains RLE worker/span path plus Burst kernel proof." />
    <task id="18" result="PASS_STATIC" proof="Editor facade remains cold diagnostics only." />
    <task id="19" result="PASS_STATIC" proof="CSV endpoint parser unchanged; no hot string route added." />
    <task id="20" result="PASS_STATIC" proof="Heatmap gizmo reads Vault mirror, not ingress ownership state." />
  </taskReconciliation>
  <structLayout name="AnalyticsIngressCursorDTO" bytes="64" offsets="RoutineWriteCursor@0:uint,RoutineReadCursor@4:uint,CriticalWriteCursor@8:uint,CriticalReadCursor@12:uint,RoutineCapacity@16:int,CriticalCapacity@20:int,RoutineOverflowDrops@24:uint,CriticalOverflowDrops@28:uint,LastFrameIndex@32:uint,StateHash@36:uint,Reserved0@40:uint,Reserved1@44:uint,Reserved2@48:uint,Reserved3@52:uint,Reserved4@56:uint,Reserved5@60:uint" alignment="64-byte false-sharing-safe control row" />
  <vaultBuffers ids="71860..71876" newIds="71874:RoutineIngress,71875:CriticalIngress,71876:IngressCursor" persistentPrivateNativeCollections="0" />
  <dependencyGraph consumes="DispatcherTimingDTO,SignalBus snapshots,GlobalQualityWeight,Vault handles" outputs="Vault ingress rings,EventRing,Staging,Telemetry,H8_Analytics_IO handoff" noAlias="GenerateMockAnalyticsEventsJob and ProcessAnalyticsQueueJob ingress/event/staging fields" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-launched" reason="user order plus foreign dependency wall already logged" />
  <dearLie before="unbounded/growing ingress abstraction risk" after="fixed Vault rings with representative deterministic culling" complexityBefore="O(1)-enqueue-with-hidden-growth-risk" complexityAfter="O(1)-bounded-ring-write-and-O(min(backlog,qualityBudget))-drain" />
</SELF_AUDIT>
```

## 2026-05-20 - Disk Fallback Unique Publication

What was wrong:
- Fallback file names used timestamp ticks only.
- Publication deleted an existing final `.h8log` before moving the new `.tmp`, which can lose valid backlog data on filename collision or stale final file presence.
- Failed publication could leave `.tmp` residue outside replay scope.

What was done:
- Added monotonic `_fallbackFileSequence` to the fallback filename stem.
- Switched temp creation to `FileMode.CreateNew`.
- Removed `File.Delete(finalPath)`.
- Added worker-side temp cleanup on failed fallback publication.
- Added source guards for the unique publication path and no final-path delete.

Cinematic Cheats used:
- No live analytics channel was added. Disk fallback remains invisible worker-side backlog, not gameplay state.

Exact Microseconds saved:
- Gameplay frame impact: 0 us.
- Worker steady overhead: one `Interlocked.Increment` and longer filename per fallback write, estimated <1 us plus filesystem cost, unmeasured.
- Fault-path gain: prevents valid backlog overwrite and reduces tmp residue after disk/network failure.

Verification state:
- Runtime brace balance after patch: `Open=244 Close=244 Delta=0`.
- Runtime/editor forbidden scan after patch: no matches for runtime reflection, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue APIs, `EventCount = 500`, `Directory.GetFiles`, or `File.Delete(finalPath)`.
- `git diff --check` passed on the changed C# files.
- Build/import not launched; existing Unity logs still classify the remaining compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_FALLBACK_PUBLICATION_GUARD">
  <taskReconciliation count="20" status="unchanged-pass-static" diskFallback="sequenced-create-new-no-final-delete" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" />
  <vaultBuffers ids="71860..71873" workerLocked="71867,71869,71870,71871,71872,71873" persistentPrivateArrays="0" />
  <worker name="H8_Analytics_IO" fallbackPublication="tmp-create-new-flush-move" replayCleanup="after-close-delete" mainThreadNetwork="none" />
  <compileGuard siblingRuntimeRefsAdded="0" build="not-launched-existing-foreign-wall" />
</SELF_AUDIT>
```

## 2026-05-20 - Disk Replay Partial-Read Poison Cleanup

What was wrong:
- Disk fallback replay handled zero-byte, invalid, and successfully replayed `.h8log` files, but a short read against the closed-file length returned silently.
- Because fallback files are published by `.tmp -> .h8log`, a short read is a poison-file condition; silent return can retry the same file after every later successful network send.

What was done:
- `TryFlushDiskBacklogUnchecked` now increments worker fault telemetry, sets `WorkerFlagFaulted`, and marks the file for after-close deletion when `read != length`.
- Validation/resend is skipped for the partial buffer.
- EditMode source guard now asserts the partial-read branch exists.
- Route card and binary payload ledger updated with the partial/corrupt replay boundary.

Cinematic Cheats used:
- No live telemetry stream or gameplay simulation was added. The exporter remains delayed binary observation with sparse heatmap samples, bounded replay, and background-only network/disk work.

Exact Microseconds saved:
- Gameplay frame impact: 0 us. All work remains on `H8_Analytics_IO`.
- Worker fault path: avoids repeated open/read/return loops for poisoned fallback files, estimated 100-3000 us per poisoned replay attempt, unmeasured.

Verification state:
- Runtime brace balance after patch: `Open=239 Close=239 Delta=0`.
- Runtime/editor forbidden scan after patch: no matches in runtime/editor source for runtime reflection, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue APIs, `EventCount = 500`, or `Directory.GetFiles`.
- Hot private persistent NativeArray/List/HashMap field scan: no matches.
- `git diff --check` passed on the changed C# files.
- Build/import not launched; existing Unity logs already classify the remaining compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_PARTIAL_READ_REPLAY_GUARD">
  <taskReconciliation count="20" status="unchanged-pass-static" replayFault="partial-read-poison-files-deleted-after-close" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" />
  <telemetry name="AnalyticsExporterTelemetryEntry" bytes="64" vaultBytesOffset="60" falseSharing="single-entry-64-byte-record" />
  <scalability routineDrain="lerp(10,1000,GlobalQualityWeight)" routineCull="quality/backlog-smoothstep" critical="high-bit-lane-bypasses-routine-cull" />
  <vaultBuffers ids="71860..71873" workerLocked="71867,71869,71870,71871,71872,71873" persistentPrivateArrays="0" />
  <dependencyGraph phase="POST_SIMULATION-drain-to-worker-handoff" worker="H8_Analytics_IO" mainThreadNetwork="none" />
  <compileGuard siblingRuntimeRefsAdded="0" unityImport="blocked-by-foreign-domains" dotnetBuild="not-launched" />
  <dearLie before="per-event-send-O(N)-packets" after="batched-binary-RLE-O(1)-network-batches-per-threshold" />
</SELF_AUDIT>
```

## 2026-05-20 - Disk Replay Handle-Safety Polish

What was wrong:
- Worker fallback replay deleted corrupt `.h8log` files while the file was still open with `FileShare.Read`.
- On Windows this can throw in `H8_Analytics_IO`, leaving poison fallback files to be reopened every replay pass.

What was done:
- Added `deleteAfterRead` in `TryFlushDiskBacklog`.
- Zero-byte, corrupt, and successfully replayed files are now marked while read, then deleted only after the `FileStream` scope exits.
- Split replay into `TryFlushDiskBacklogUnchecked` behind a fault-counting shell.
- Added `TryDeleteReplayFile` so delete failures increment worker fault telemetry and stop the current replay pass instead of terminating the worker loop.
- Added an EditMode source guard for the delete-after-close path.
- Updated the active route card and binary payload ledger with the handle-safety boundary.

Cinematic Cheats used:
- No gameplay route or live stream was added. Disk cleanup remains a worker-side maintenance fake behind the delayed analytics stream.

Exact Microseconds saved:
- Gameplay frame: 0 us; this is entirely on `H8_Analytics_IO`.
- Worker fault path: avoids repeated open/read/delete exception churn and worker-loop termination for poison fallback files, estimated 100-5000 us per corrupt replay attempt; unmeasured.

Verification state:
- Runtime brace balance: `Open=238 Close=238 Delta=0`.
- Runtime forbidden scan: no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue API, fixed `EventCount = 500`, `Directory.GetFiles`, runtime reflection offset route, or hot private persistent NativeArray/List/HashMap fields.
- `git diff --check`: only the known CRLF warning on `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Build/import was not launched. Existing Unity logs already classify the remaining compile wall as foreign-domain failure with no SHINOBU compiler errors, and the user explicitly ordered no build until needed.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_DISK_REPLAY_HANDLE_SAFE_BUILD_NOT_RELAUNCHED">
  <taskReconciliation count="20" status="unchanged-pass-static" diskFallback="delete-after-stream-close" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0,TimestampSeconds@4,EventAUP@8" alignment="multiple-of-16" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <scalability unchanged="GlobalQualityWeight still drives hot routine culling, drainBudget, mock density, and route sparsity" />
  <dependencyGraph workerReplay="H8_Analytics_IO reads bounded h8log files, validates payload, sends, closes stream, deletes through fault-counted helper" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors in archived Unity logs" />
  <dearLie after="batched delayed binary telemetry with worker-only disk maintenance" />
</SELF_AUDIT>
```

## 2026-05-20 - Lifecycle Hardening Re-Audit

What was wrong:
- Active `CURRENT_BATCH.md` no longer contains `SHINOBU_160`; the active file starts at `SHINOBU_200`, so the current SHINOBU_160 scope must be preserved through active status/rationale plus Batch010 archive evidence.
- Dual-lane ingress initialization had a partial-allocation window before both `NativeMemorySentinel` registrations completed.
- `OnDestroy` retried `StopWorker()` but did not run final queue/static cleanup if a later stop succeeded after a prior `OnDisable` failure.
- The editor facade refresh callback could tick before `CreateGUI` initialized labels.

What was done:
- Confirmed `PROMPT_NOT_FOUND_ACTIVE` for `SHINOBU_160` in active `Docs/Tasks/CURRENT_BATCH.md`; ignored neighboring `SHINOBU_200+` blocks.
- Wrapped `AnalyticsEventIngress.Initialize` in rollback cleanup.
- Added `TeardownStoppedWorkerState()` and call it only after `StopWorker()` succeeds from `OnDisable` or `OnDestroy`.
- Added editor null guards before touching `_status` and `_telemetry`.
- Extended the EditMode source guard to require the teardown helper.

Cinematic Cheats used:
- No gameplay simulation or per-event live stream was added. The exporter still fakes live analytics with sparse route samples, quality-weight shedding, chunked binary transfer, and background RLE/network/disk work.

Exact Microseconds saved:
- Ingress rollback: 0 us steady-state; prevents persistent native queue leak on cold init failure.
- Deferred teardown cleanup: 0 us steady-state; prevents stale active owner after worker recovery during destroy.
- Editor null guard: editor-only cold path, estimated <1 us per refresh.

Verification state:
- Brace balance after lifecycle patch: `Open=228 Close=228 Delta=0`.
- Runtime forbidden scan after lifecycle patch: no matches for `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, public `ParallelWriter`, `H8Memory.CreateNativeArrayView`, or stale single-lane queue API.
- `git diff --check`: clean for SHINOBU files; ledger still emits the known CRLF warning.
- Compile/import not relaunched in this pass. Process scan reported `NO_DOTNET_CSC_UNITY`, but CPU samples were `28.79,93.96,23.67`; prior Unity logs already stop on foreign domains before SHINOBU proof, so a rerun would add load without clearing the dependency wall.

## 2026-05-20 - Bounded Drain / Critical Lane Polish

What was wrong:
- `ProcessAnalyticsQueueJob` staged by quality threshold, then drained routine overflow without a hard frame budget to discover critical events. That made POST_SIMULATION cost proportional to total queued telemetry under stress.
- Critical telemetry shared the same ingress lane as route/resource samples.
- Task 16 required memory consumed by telemetry buffers, but the telemetry DTO had no explicit memory field.

What was done:
- Added separate routine and critical `NativeQueue<AnalyticEventDTO>` lanes, both registered with `NativeMemorySentinel`.
- Changed `ProcessAnalyticsQueueJob` to drain critical first and cap all dequeue work with `drainBudget = min(stagingCapacity, round(lerp(10,1000,GlobalQualityWeight)))`.
- Added POST_SIMULATION SignalBus ingestion for `EntityDeathSignal`, `ItemAcquiredSignal`, `SurvivalVitalsChangedSignal`, and `FrameTimeSignal` using current frame snapshots and index loops.
- Added `VaultBytes` at telemetry DTO offset 60 and computed it from every SHINOBU_160 Vault handle.
- Rewired `BacklogEvents` / `QueueDepthEstimate` to the same owner-local pressure estimate used by culling, instead of reporting only the pending handoff slot.
- Surfaced `VaultBytes` in the editor telemetry facade and updated source guards.

Cinematic Cheats used:
- The exporter still lies as a batched stream: no per-event live send, no gameplay simulation, no direct server pressure on the frame. Critical events only bypass the batching delay after the POST_SIMULATION copy and worker handoff.

Exact Microseconds saved:
- Bounded drain avoids unbounded O(N) queue scans during synthetic or network-stalled telemetry spikes. Static estimate: prevents millisecond-class frame spikes when backlog grows past the quality-derived 10..1000 budget.
- Critical lane removes routine-backlog scanning for death/fault telemetry. Static estimate: O(critical + budgeted routine), not O(total routine backlog).
- SignalBus ingestion avoids direct domain calls and GameObject scans; normal sparse snapshots are expected under 10 us, unmeasured.
- `VaultBytes` accounting costs one small handle-length arithmetic pass per telemetry write, expected <1 us; it makes memory growth visible instead of implicit.

Verification state:
- Runtime/editor forbidden-source scan: PASS. No `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, old `IngressQueue`, or old `TryGetQueue(` remained in runtime/editor.
- Brace balance: PASS, `Open=213 Close=213 Delta=0`.
- Trailing whitespace: PASS for SHINOBU runtime/editor/test files.
- `git diff --check`: PASS except the existing ledger CRLF warning.
- Compile/import: NOT LAUNCHED. First guard: CPU average `37.11%` with multiple active `dotnet` processes. Second guard: CPU average `52%` with the same active `dotnet` process class. Third guard after SignalBus ingest: CPU average `13.28%`, but multiple `dotnet` processes were still active. Latest guard: no dotnet/csc/Unity processes, but CPU average `92.42%`. AGENTS forbids a new build/import while CPU is above 50% or dotnet/csc is running.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_BUILD_BLOCKED_BY_ACTIVE_DOTNET">
  <taskReconciliation>
    <task id="01" name="UNITY_WEB_REQUEST_ERADICATION" result="PASS_STATIC" proof="runtime scan no UnityWebRequest; network route is HttpWebRequest on H8_Analytics_IO only" />
    <task id="02" name="JSON_SERIALIZATION_PURGE" result="PASS_STATIC" proof="runtime scan no JsonUtility/ToJson; payload is binary DTO" />
    <task id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS_STATIC" proof="AnalyticEventDTO fields only; no DTO properties" />
    <task id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS_STATIC" proof="AnalyticEventDTO explicit 32 bytes; AUP starts at offset 8" />
    <task id="05" name="EMERGENCY_MOCK_EVENT_GENERATOR" result="PASS_STATIC" proof="Burst GenerateMockAnalyticsEventsJob emits 500 deterministic records per second when enabled" />
    <task id="06" name="BURST_EVENT_INGESTION_KERNEL" result="PASS_STATIC" proof="ProcessAnalyticsQueueJob Burst fast flags, NoAlias fields, NaN guards, bounded drain" />
    <task id="07" name="BACKGROUND_I_O_THREAD_MANAGEMENT" result="PASS_STATIC" proof="dedicated Thread named H8_Analytics_IO with AutoResetEvent wait" />
    <task id="08" name="THE_DEAR_LIE_BATCHED_TRANSMISSION" result="PASS_STATIC" proof="worker accumulates batches, RLE-compresses, sends/writes chunks" />
    <task id="09" name="DISK_FALLBACK_ROUTING" result="PASS_STATIC" proof=".tmp then .h8log fallback plus bounded replay" />
    <task id="10" name="CONTINUOUS_SCALABILITY_QUEUE_CULLING" result="PASS_STATIC" proof="GlobalQualityWeight drives cull threshold and drainBudget 10..1000" />
    <task id="11" name="HEATMAP_DATA_AGGREGATION" result="PASS_STATIC" proof="KCC AUP sampled by timer; route emission optional but anchor refresh continues" />
    <task id="12" name="CRITICAL_EVENT_PRIORITIZATION" result="PASS_STATIC" proof="critical queue lane drains before routine and forces worker flush" />
    <task id="13" name="AUP_PRECISION_SERIALIZATION" result="PASS_STATIC" proof="double3 serialized little-endian as 3x uint64 IEEE payload" />
    <task id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS_STATIC" proof="analytics buffers are external observer Vault IDs, not StateRingBuffer ownership" />
    <task id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS_STATIC" proof="staging/event/handoff/scratch buffers request UninitializedMemory where safe" />
    <task id="16" name="TELEMETRY_EXPORTER_RECORDER" result="PASS_STATIC" proof="300-entry telemetry ring reports sent/disk/backlog/fault/worker/VaultBytes" />
    <task id="17" name="BINARY_COMPRESSION_INTEGRATION_JOB" result="PASS_STATIC" proof="Burst RLE job plus equivalent background span RLE path" />
    <task id="18" name="ANALYTICS_TUNER_EDITOR_WINDOW" result="PASS_STATIC" proof="UI Toolkit editor facade mutates Vault tuning and shows response/ratio/VaultBytes" />
    <task id="19" name="CSV_ENDPOINT_CONFIGURATION_INGESTOR" result="PASS_STATIC" proof="telemetry_config.csv cold FileStream into Vault CsvScratch; byte-span parser" />
    <task id="20" name="LIVE_HEATMAP_DEBUG_GIZMO" result="PASS_STATIC" proof="editor gizmo reads last heatmap debug DTOs and colors death/resource/perf/route" />
  </taskReconciliation>
  <structLayout>
    <dto name="AnalyticEventDTO" sizeBytes="32" alignment="8">
      <field name="EventHashID" offset="0" size="4" />
      <field name="TimestampSeconds" offset="4" size="4" />
      <field name="EventAUP" offset="8" size="24" />
      <padding bytes="0" />
    </dto>
    <dto name="AnalyticsCountersDTO" sizeBytes="64" falseSharing="padded" />
    <dto name="AnalyticsTuningDTO" sizeBytes="64" falseSharing="padded" />
    <dto name="AnalyticsExporterTelemetryEntry" sizeBytes="64" field60="VaultBytes" falseSharing="padded" />
  </structLayout>
  <vaultStatus privatePersistentArrays="zero" buffers="71860,71861,71862,71863,71864,71865,71866,71867,71868,71869,71870,71871,71872,71873" />
  <dependencyGraph consumes="DispatcherTimingDTO,SignalBus snapshots,GlobalQualityWeight,Vault handles" outputs="Vault EventRing/Staging/Telemetry,worker handoff,disk/network batch" noAlias="ProcessAnalyticsQueueJob and CompressAnalyticsBufferJob fields" />
  <compileGuard directSiblingReference="none-added" runtimeForbiddenScan="pass" build="not-launched-active-dotnet" />
  <dearLie before="per-event network stream O(events)" after="batched compressed chunks O(min(events,quality-budget)) plus background I/O" />
</SELF_AUDIT>
```

## 2026-05-20 - Hotpath/Dump Polish Repair

What was wrong:
- `TryRecordEvent` wrote Vault counters per event and only culled after the queue had already grown.
- The native ingress class was public enough to invite unfenced queue access.
- Fault dump I/O used `FileStream` from POST_SIMULATION.
- Worker cached views called `H8Memory.CreateNativeArrayView`, an internal API risk across assembly boundaries.
- Disk fallback wrote directly to final `.h8log` and replayed full directory arrays.
- XML Task 19 asked for `telemetry_config.csv`, but the runtime default path used `analytics_endpoint.csv`.

What was done:
- Added owner-thread and accepting-state gates around `TryRecordEvent`.
- Moved hot-path enqueued/dropped counts into atomics and flush them to Vault once in POST_SIMULATION.
- Added continuous pressure shedding before enqueue using `math.lerp`, a smoothstep polynomial, and `math.step`; critical hashes still bypass pressure.
- Made `AnalyticsEventIngress` internal and kept public production through the owner-local facade.
- Added Vault buffer `71873` `DumpSnapshot` and moved black-box file writes to `H8_Analytics_IO`.
- Replaced `H8Memory.CreateNativeArrayView` with `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray` plus editor safety handle.
- Changed fallback writes to `.tmp` + flush + rename, added payload validation, and bounded replay to 8 files per flush through `Directory.EnumerateFiles`.
- Added `Assets/_Project/Data/Analytics/telemetry_config.csv` with a `.meta`, while preserving `analytics_endpoint.csv` as fallback.

Cinematic Cheats used:
- Routine telemetry is not live truth. It is probabilistically shed under pressure before enqueue.
- Server stream remains a delayed chunk illusion; network/disk/compression stay outside the player frame.
- Black-box dump is a fixed snapshot, not a live forensic serializer on the main thread.

Exact Microseconds saved:
- Per-event Vault counter resolve removed: static estimate 2-15 us avoided on bursty producer frames; unmeasured.
- Main-thread black-box FileStream removed: rare fault path saving could be milliseconds; unmeasured.
- Backlog replay bounded to 8 files per flush: prevents worker-side unbounded directory scan; gameplay frame saving remains 0 us because worker-only.
- Internal API compile-risk removal: 0 us runtime, compile-wall risk reduction only.

Verification state:
- Static forbidden scan clean for SHINOBU runtime: no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `H8Memory.CreateNativeArrayView`, `Directory.GetFiles`, private arrays, DTO properties, `Pack=`, or direct `Hecton8.World`.
- Brace balance clean for runtime/editor/test files.
- `git diff --check` clean for runtime/test/new telemetry config files.
- Unity batchmode after patch wrote `Docs/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log`; targeted search found no `AsynchronousTelemetryExporter*.cs(` errors.
- Clean compile remains blocked by foreign domains: `HabitatFluidIncursionJobs.cs`, `ProceduralWreckage`, `ProceduralCoral`, `AwaitableDropSequenceDirector.cs`, and `Hecton8.MockDomain.Runtime` Burst ILPP.
- Further compile retry was not launched. Immediate process guard saw Unity's `dotnet.exe` compiler process PID `32468`; a later guard was clear, but the same foreign-domain compile wall remains and another retry would not add SHINOBU proof.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="PENDING_VERIFICATION_COMPILE_BLOCKED_BY_FOREIGN_DOMAINS">
  <tasks count="20" reconciliation="PASS_STATIC_WITH_FOREIGN_COMPILE_WALL" />
  <dto name="AnalyticEventDTO" bytes="32" eventHash="0:uint" timestamp="4:uint" eventAup="8:double3" />
  <vaultBuffers ids="71860..71873" added="71873:DumpSnapshot" persistentPrivateArrays="0" />
  <hotPath ingress="owner-thread-only" counters="atomic-delta-postsimulation-flush" culling="quality-lerp-smoothstep-step-before-enqueue" />
  <io thread="H8_Analytics_IO" network="HttpWebRequest-background-only" compression="worker-RLE-span" diskFallback="tmp-flush-rename-validated" dump="worker-writes-vault-snapshot" />
  <compileGuard h8MemoryInternalCall="removed" unityLog="Docs/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log" shinobuErrors="none-found" />
</SELF_AUDIT>
```

## 2026-05-19 - Polish Re-Entry

What was wrong:
- Worker memory was still private managed arrays, even though gameplay hot capture was unmanaged.
- POST_SIMULATION used `Schedule().Complete()` fences for tiny Burst jobs.
- Burst attributes omitted the explicit fast-float directives demanded by the polish mandate.
- The heatmap gizmo drew selected-only, broad event-ring data instead of the last 100 heatmap entries with event-type colors.
- Cold CSV endpoint hashing re-encoded endpoint/API strings into temporary managed byte arrays.

What was done:
- Added Vault worker buffers `71869..71872` and expanded `71867` compressed scratch to worst-case RLE size.
- Moved handoff A/B, worker accumulation, raw payload, compressed payload, and CSV readback memory to Vault handles.
- Locked worker Vault buffers while `H8_Analytics_IO` is alive.
- Replaced `Schedule().Complete()` with Burst `Run()` at the POST_SIMULATION void boundary.
- Added exact Burst directives: `CompileSynchronously=true`, `FloatMode.Fast`, `FloatPrecision.Standard`.
- Removed direct `using Hecton8.World;` from the exporter file.
- Reworked heatmap debug to `OnDrawGizmos`, last 100 Vault heatmap entries, with red death, green resource, magenta perf, cyan route.
- Hashes endpoint/API key directly from CSV byte spans before cold managed strings are created for `HttpWebRequest`.

Cinematic Cheats used:
- Dear-lie batching remains the core fake: the server receives delayed compressed chunks, not live event streams.
- Heatmap remains sparse route truth instead of per-frame movement spam.
- Quality pressure sheds routine analytics before network/disk work can threaten the frame.

Exact Microseconds saved:
- `Schedule().Complete()` fence removal: estimated 5-30 us per POST_SIM drain; unmeasured.
- Managed scratch array eviction: 0 us direct frame saving, but removes GC heap retention and future collection pressure.
- CSV hash re-encode removal: cold boot only, estimated 5-50 us and one temporary byte array avoided per endpoint/API line.

Verification state:
- Static forbidden-pattern scan on SHINOBU runtime/editor returned no matches for `Schedule().Complete`, `Encoding.UTF8.GetBytes`, private worker arrays, `UnityWebRequest`, JSON, `HttpClient`, `Task.Run`, `ThreadPool`, or direct `using Hecton8.World`.
- Compile/test still pending CPU guard.

## 2026-05-19 - Polish Re-Entry Static Audit Repair

What was wrong:
- Static audit found a handoff visibility race: `Pending` could become visible before DTO copy completion.
- Analytics exposed a public `NativeQueue.ParallelWriter` without a producer-job fence/refcount contract.
- Worker shutdown could only join for 750 ms while a background `HttpWebRequest` remained blocked.
- Unsafe span helper signatures widened compile-risk surfaces even though the pointer work is local.

What was done:
- Added `BatchStateWriting` and the `Idle -> Writing -> Pending -> Idle` handoff protocol.
- Removed public analytics `TryGetParallelWriter`; producers must use the owner-local facade until a fenced producer-job route is designed.
- Stored the active background `HttpWebRequest`, abort it after the first failed join, then retry the bounded join before releasing Vault locks.
- Replaced KCC heatmap read with `SignalBus<KccVelocitySignal>.GetSignals()` and moved pointer-to-span construction into safe-signature helper bodies.
- Added EditMode source assertions for no public writer, handoff state, and request abort.

Cinematic Cheats used:
- No new simulation was added. The exporter remains delayed, compressed analytical truth, not live gameplay truth.
- Sparse KCC route sampling and quality-weight routine culling still buy frame time for visuals rather than analytics fidelity.

Exact Microseconds saved:
- Handoff state repair: costs one extra interlocked exchange per batch, estimated <1 us; prevents lost batches under timeout wake.
- Public writer removal: 0 us steady-state; removes undefined native writer lifetime during scene unload.
- Request abort on shutdown: 0 us during gameplay; worst-case shutdown/network wait reduced from tuning max 30000 ms to two bounded 750 ms join attempts when abort succeeds.
- Safe helper wrappers: 0 us expected after inlining; compile-risk reduction only.

Verification state:
- Brace balance: PASS after repair.
- `git diff --check`: PASS for SHINOBU runtime/test repair.
- Static forbidden-source scan: PASS for runtime; only test assertions contain forbidden strings.
- Compile/test: still pending guarded CPU window. Latest guard found no Unity/dotnet/csc process, but CPU samples `44.02,78.17,25.02,21.43,70.55,96.88`; average excluding first sample `58.41%`, so build launch remains blocked.

## 2026-05-20 - Worker Vault Metadata Isolation

What was wrong:
- Background `H8_Analytics_IO` still resolved worker Vault handles through `GlobalDataVault.ResolveBuffer`.
- `ResolveBuffer` reads Vault metadata maps; that is owner-side memory topology work, not safe background I/O cadence.

What was done:
- Added `CreateLockedWorkerView<T>` to create transient `NativeArray<T>` views from cached `VaultBufferHandle<T>.ptr` and `Length`.
- Switched worker reads of `HandoffA/B`, `WorkerAccum`, `RawBatchScratch`, and `CompressedScratch` to locked cached pointer views.
- Left main-thread/editor paths on normal Vault handle resolution.
- Added test assertions for `CreateLockedWorkerView` and `ResolveWorkerHandoffBuffer`.

Cinematic Cheats used:
- No live stream was added; the pipeline remains delayed chunked truth. This preserves the Dear Lie and keeps the player frame uninvolved.

Exact Microseconds saved:
- Worker metadata lookup removal: estimated 2-20 us per worker flush and removal of a cross-thread map-access class; unmeasured.
- Gameplay frame cost remains intended 0 us for network/compression and <1 us per ordinary event enqueue under capacity; profiler proof still pending.

Verification state:
- Static scan confirms worker-owned handles no longer call `_handle.Resolve(_dataVault)` in worker code paths.
- Brace balance and `git diff --check` passed for the runtime file after the patch.
- Compile/test still pending guarded CPU window.

## 2026-05-20 - Unity Compile Attempt / Dependency Wall

What was wrong:
- Static verification was not enough after the worker Vault metadata isolation patch.
- A legal Unity compile window opened, but the project did not reach a clean compile because unrelated domains already fail compilation.
- Roslyn `VBCSCompiler.dll` remained active after Unity exited, so another compile launch is forbidden by AGENTS until that process clears.

What was done:
- Ran Unity batchmode with `-batchmode -nographics -quit -projectPath C:\hades\Hecton8`.
- Wrote the full log to `Docs/AgentLogs/Unity_SHINOBU_160_compile.log`.
- Verified the log includes `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs` in the compilation inputs.
- Searched the log for `AsynchronousTelemetryExporter.cs(`, `AsynchronousTelemetryTunerWindow.cs(`, and `AsynchronousTelemetryExporterEditTests.cs(`; no SHINOBU-specific compiler errors were found.
- Classified the failure as a dependency wall outside SHINOBU_160 instead of editing foreign domains.
- First 20 Minutes moment: Proof/route-testability for the Copper Wire route, not new gameplay breadth.
- Route impact: death/resource/route/hazard/perf telemetry can support route tuning once the dependency wall clears.
- Proof required: clean Unity Console, route Play Mode run, profiler/GC capture, disk fallback test, and endpoint send/replay evidence.
- Parked work rejected: no live analytics stream, no gameplay simulation, no per-frame movement spam, no unfenced cross-domain producer writer.

Cinematic Cheats used:
- No new runtime work was added. The telemetry route remains delayed binary chunk export with sparse heatmap samples, quality-weight routine culling, and background RLE/network/disk work only.

Exact Microseconds saved:
- Worker Vault metadata isolation remains estimated at 2-20 us per worker flush; still unmeasured.
- Main-thread network/compression remains 0 us intended because work runs on `H8_Analytics_IO`.
- Compile-wall discipline saves developer-machine churn only; no gameplay microsecond saving is claimed.

Verification state:
- Unity import/compile: BLOCKED_BY_DEPENDENCY.
- Blocking error groups observed in the log: `Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs`, `Assets/_Project/Scripts/World/ProceduralCoral/*`, `Assets/_Project/Scripts/World/ProceduralWreckage/*`, `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`, and `Hecton8.MockDomain.Runtime` Burst ILPostProcessor.
- SHINOBU log search: no `AsynchronousTelemetryExporter*.cs(` compiler errors found.
- Further compile retry: BLOCKED_BY_ACTIVE_DOTNET because `VBCSCompiler.dll` remained active after a 120-second wait.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="UNITY_IMPORT_ATTEMPTED_COMPILE_BLOCKED_BY_DEPENDENCIES">
  <taskReconciliation count="20" result="PASS_STATIC" compile="blocked-by-foreign-domains" />
  <dto name="AnalyticEventDTO" bytes="32" alignment="8" fields="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" />
  <vaultBuffers first="71860" last="71873" workerOwned="71867,71869,71870,71871,71872,71873" workerResolvePath="cached-locked-pointer-view" />
  <hotPath json="none" unityWebRequest="none" scheduleComplete="none" privateWorkerArrays="none" publicParallelWriter="none" />
  <thread name="H8_Analytics_IO" network="HttpWebRequest-background-only" shutdown="active-request-abort-plus-bounded-join" />
  <dependencyWall log="Docs/AgentLogs/Unity_SHINOBU_160_compile.log" shinobuCompilerErrors="none-found" />
</SELF_AUDIT>
```

## 2026-05-20 - KCC Anchor / Mock Load Static Polish

What was wrong:
- Status claimed KCC anchor refresh was decoupled from route heatmap emission, but code returned before reading KCC until the route timer elapsed.
- Emergency mock fallback still injected a fixed 500 DTOs/sec, independent of `GlobalQualityWeight` and queue pressure.

What was done:
- Moved KCC signal reading before the heatmap timer gate. `_lastKnownPlayerAup` now updates on every fresh finite KCC velocity signal in `POST_SIMULATION`; route samples still require the heatmap flag and sample timer.
- Replaced fixed mock `EventCount = 500` with `math.lerp(20f, 500f, smoothQuality)` and pressure collapse toward 25 percent of that quality-scaled count.
- Removed `new double3(...)` construction from mock event AUP offsets/origin setup; DTOs remain default field assignment.
- Added EditMode source guards for no fixed mock count, split route timer gating, and quality/backlog mock scaling.

Cinematic Cheats used:
- Route analytics remain sparse review samples, not per-frame movement truth.
- Mock analytics stress is now a tunable/load-shedding illusion for CI and editor validation, not a constant producer firehose.

Exact Microseconds saved:
- Low-quality mock fallback avoids up to 480 synthetic events/sec before additional backlog collapse. Static stress estimate: 5000-15000 us avoided across drain/copy/compress pressure during synthetic tests; unmeasured.
- Fresh KCC anchor scan adds only the existing typed signal span loop and no allocation; it buys better death/perf forensic placement without route sample spam.

Verification state:
- Runtime brace balance after patch: `Open=226 Close=226 Delta=0`.
- Runtime forbidden scan after patch: no matches for `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, stale queue API, or `EventCount = 500`.
- `git diff --check` passed on the changed runtime/test files.
- Clean compile/runtime proof remains blocked by the previously logged foreign-domain dependency wall; no new build was launched in this pass.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_BUILD_NOT_RELAUNCHED">
  <taskReconciliation count="20" task05="mock-count-quality-and-backlog-scaled" task11="kcc-anchor-fresh-route-timer-sparse" />
  <dto name="AnalyticEventDTO" bytes="32" alignment="8" fields="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" />
  <scalability mockEvents="lerp(20,500,smoothQuality)-pressure-collapse" routeSamples="timer-gated" criticalAnchors="fresh-kcc-signal" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; user ordered no build until needed" />
</SELF_AUDIT>
```

## 2026-05-20 - Reflectionless Runtime Layout Guard

What was wrong:
- `AnalyticsLayout` computed runtime DTO offsets through `typeof(T).GetField(...)` and `System.Reflection.FieldInfo`.
- This was cold code, but it added avoidable runtime reflection/AOT surface inside Core Diagnostics.

What was done:
- Replaced generic reflection offset lookup with explicit `AnalyticEventDTO` offset helpers.
- Offset proof now subtracts `UnsafeUtility.AddressOf(ref owner)` from `UnsafeUtility.AddressOf(ref field)` on a local unmanaged DTO.
- Added EditMode source guards rejecting `System.Reflection` and `.GetField(` in the runtime exporter source.
- Corrected stale status wording: contract `SignalBus<T>.GetFrameSnapshot()` ingestion is intentional for death/resource/survival/perf lanes.

Cinematic Cheats used:
- No gameplay or visual simulation added. This is compile-wall/AOT hygiene only; analytics still uses delayed chunk export and sparse route samples.

Exact Microseconds saved:
- Cold boot layout guard avoids reflection metadata lookup. Static estimate: 1-20 us cold-path only; gameplay frame saving 0 us.
- Compile-wall surface reduced by avoiding runtime reflection helper churn in Core Diagnostics.

Verification state:
- Runtime brace balance after patch: `Open=229 Close=229 Delta=0`.
- Runtime forbidden scan after patch: no matches for runtime reflection offset route, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, stale queue API, or `EventCount = 500`.
- `git diff --check` passed on the changed runtime/test files.
- Build/import not launched; existing Unity logs already classify the remaining compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_REFLECTIONLESS_LAYOUT">
  <taskReconciliation count="20" status="unchanged-pass-static" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0,TimestampSeconds@4,EventAUP@8" runtimeOffsetProof="UnsafeUtility.AddressOf-byte-delta" />
  <compileGuard runtimeReflection="removed" siblingDomainRefs="none-added" build="not-relaunched" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
</SELF_AUDIT>
```

## 2026-05-20 - AUP-Seeded Routine Cull Gate

What was wrong:
- Routine hot-path pressure culling hashed only event type, timestamp, and backlog.
- Same-second route/resource observations could be accepted or dropped as a cohort, leaving spatial holes in heatmap export under load.

What was done:
- `TryRecordEvent` now passes full `double3 eventAup` into `ShouldAcceptHotPathEvent`.
- `HashHotPathGate` folds the IEEE754 bits of all three AUP double lanes with `BitConverter.DoubleToInt64Bits` before applying the quality/backlog drop probability.
- Critical hashes still bypass pressure culling; only routine analytics are decimated.
- Status, rationale, route doc, and binary payload ledger now describe the AUP-seeded deterministic cull boundary.

Cinematic Cheats used:
- Analytics remains delayed sampled truth, not a gameplay simulation or live stream.
- The cheap deterministic spatial gate is the visual/forensic fake: retain representative heatmap density under pressure without per-event random state, object tracking, or main-thread export work.

Exact Microseconds saved:
- No direct frame saving is claimed; this is fidelity repair under existing pressure culling.
- Added cost is three double-bit folds only for pressured routine events, expected sub-us per event on desktop and low single-digit us in synthetic weak-CPU bursts; unmeasured.
- Prevents wasted exported batches that overrepresent or erase same-second spatial cohorts, reducing analyst cleanup cost rather than gameplay frame time.

Verification state:
- Runtime brace balance after AUP gate: `Open=245 Close=245 Delta=0`.
- Runtime/editor forbidden scan after AUP gate: no matches for runtime reflection, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue APIs, `EventCount = 500`, `Directory.GetFiles`, `File.Delete(finalPath)`, or hot private persistent collection fields.
- `git diff --check` passed on changed C# files.
- Build/import not relaunched; existing Unity logs already classify the compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_AUP_SEEDED_CULL_GATE">
  <taskReconciliation count="20" status="unchanged-pass-static" task10="continuous-quality-backlog-culling-spatially-seeded" task11="heatmap-sample-decimation-preserves-spatial-distribution" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" />
  <scalability curve="routine-drop-probability-from-quality-and-backlog" spatialGate="eventHash,timestamp,backlog,AUP-double-bits" criticalBypass="true" binaryTierSwitch="none" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
  <dearLie before="per-event-perfect-retention-or-random-state" after="deterministic-representative-spatial-sample" complexityBefore="O(N)-analysis-cleanup-or-stateful-rng" complexityAfter="O(1)-hash-per-pressured-routine-event" />
</SELF_AUDIT>
```

## 2026-05-20 - HTTP Failure Response Disposal

What was wrong:
- `TrySendCompressedBatch` preserved HTTP response codes from `WebException.Response`, but the failed `HttpWebResponse` was not disposed.
- Repeated endpoint failure could retain worker-side response/socket resources while the gameplay frame remained clean, creating a long-session background leak.

What was done:
- Converted the failed response through `as HttpWebResponse`.
- Wrote `_workerLastResponseCode` inside a `try` block and always called `response.Dispose()` in `finally`.
- Added an EditMode source guard for `response.Dispose();`.

Cinematic Cheats used:
- No retry simulation or live stream was added. The exporter still treats network as delayed external observation and writes fallback batches on the worker.

Exact Microseconds saved:
- Gameplay frame impact: 0 us.
- Worker fault-path cost: one dispose call per failed response.
- Long-session gain: prevents unbounded response/socket retention during endpoint failure storms; runtime microseconds are unmeasured.

Verification state:
- Runtime brace balance after patch: `Open=249 Close=249 Delta=0`.
- Runtime/editor forbidden scan after patch: no matches for runtime reflection, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue APIs, `EventCount = 500`, `Directory.GetFiles`, or `File.Delete(finalPath)`.
- `git diff --check` passed on changed runtime/test C# files.
- Build/import not relaunched; existing Unity logs already classify the compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_HTTP_FAILURE_RESPONSE_DISPOSAL">
  <taskReconciliation count="20" status="unchanged-pass-static" task07="background-thread-network-cleanup" task09="failure-fallback-resource-clean" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" />
  <worker name="H8_Analytics_IO" failedHttpResponse="disposed" mainThreadNetwork="none" unityWebRequest="none" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
</SELF_AUDIT>
```

## 2026-05-20 - HTTP Endpoint Scheme Gate

What was wrong:
- Cold CSV endpoint configuration could contain a non-HTTP URI.
- Without a scheme gate, the worker would enter `WebRequest.Create`/cast failure and report a generic send fault every flush, hiding the configuration error and adding repeated exception churn.

What was done:
- Added `IsHttpEndpoint`.
- `TrySendCompressedBatch` now accepts only `https://` or `http://` before creating `HttpWebRequest`.
- Invalid schemes write response code `-3`, increment worker fault telemetry, and fall through to disk fallback.
- EditMode source guard now asserts `IsHttpEndpoint` and ordinal scheme comparison.

Cinematic Cheats used:
- No network retry simulation, coroutine, or live analytics stream was added. Invalid endpoint data degrades into disk fallback on the worker.

Exact Microseconds saved:
- Gameplay frame impact: 0 us.
- Worker path adds two ordinal prefix checks per network flush.
- Avoids exception setup/repeated generic fault churn for malformed endpoint schemes; unmeasured.

Verification state:
- Runtime brace balance after patch: `Open=251 Close=251 Delta=0`.
- Runtime/editor forbidden scan after patch: no matches for runtime reflection, `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, stale queue APIs, `EventCount = 500`, `Directory.GetFiles`, `File.Delete(finalPath)`, or `ex.Response is HttpWebResponse`.
- `git diff --check` passed on changed runtime/test C# files.
- Route card and binary payload ledger updated with worker-only HTTP scheme validation.
- Build/import not relaunched; existing Unity logs already classify the compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_HTTP_ENDPOINT_SCHEME_GATE">
  <taskReconciliation count="20" status="unchanged-pass-static" task07="background-thread-network-precondition" task09="invalid-endpoint-disk-fallback" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" />
  <worker name="H8_Analytics_IO" endpointSchemes="http,https" invalidSchemeCode="-3" mainThreadNetwork="none" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
</SELF_AUDIT>
```

## 2026-05-20 - Continuous Burst Drain Decimation

What was wrong:
- `ProcessAnalyticsQueueJob` still used a binary `pressureCull` branch in the Burst drain.
- Once background backlog crossed the threshold, every routine event in the drain path could be dropped, even though the hot facade already used continuous quality/backlog probability.

What was done:
- Removed the hard `bool pressureCull` drain path.
- Added `dropMilli` derived from backlog pressure with guarded math.
- Added `ShouldDropRoutineDuringDrain` and `HashDrainGate`.
- The drain hash folds event type, timestamp, backlog, and all three AUP double lanes through `math.aslong`.
- Critical telemetry remains uncullable; only routine analytics are sampled under overload.

Cinematic Cheats used:
- Analytics remains representative delayed observation, not a live gameplay simulation.
- Under worker pressure the exporter keeps a deterministic spatial sample instead of pretending every routine route/resource observation must survive.

Exact Microseconds saved:
- No measured gameplay-frame number is claimed behind the foreign compile wall.
- Added cost is one O(1) hash only for pressured routine events that reach the drain job.
- The repaired path prevents all-or-nothing routine sample loss while preserving bounded POST_SIMULATION drain work.

Verification state:
- Runtime brace balance after drain decimation: `Open=254 Close=254 Delta=0`.
- Runtime/editor forbidden scan after drain decimation: no matches, including `bool pressureCull`.
- `git diff --check` passed on changed runtime/test C# files.
- EditMode source guard asserts `ShouldDropRoutineDuringDrain`, `HashDrainGate`, and `math.aslong(value)`.
- Build/import not relaunched; existing Unity logs already classify the compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_CONTINUOUS_BURST_DRAIN_DECIMATION">
  <taskReconciliation count="20" status="unchanged-pass-static" task10="continuous-quality-backlog-drain-decimation" task11="heatmap-route-sampling-retains-spatial-representative-data" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" />
  <scalability curve="dropMilli-from-backlog-pressure" spatialGate="eventHash,timestamp,backlog,AUP-double-bits" criticalBypass="true" binaryTierSwitch="none" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
  <dearLie before="retain-all-routine-or-drop-all-routine-under-pressure" after="deterministic-representative-routine-sample" complexityBefore="O(N)-queue-drain-with-binary-loss" complexityAfter="O(1)-hash-per-pressured-routine-event-with-bounded-drain" />
</SELF_AUDIT>
```

## 2026-05-20 - Mock Queue Pressure Accounting

What was wrong:
- The emergency mock generator wrote directly into the routine `NativeQueue<AnalyticEventDTO>`.
- Because it bypassed the public `TryRecordEvent` facade, `_ingressPendingEstimate` and `_hotEnqueuedDelta` did not see generated mock rows.
- Under CI/fallback stress, the pressure model could under-report queue load and keep generating more mock data than the low-quality curve intended.

What was done:
- After `GenerateMockAnalyticsEventsJob.Run()`, the runtime now calls `Interlocked.Add(ref _hotEnqueuedDelta, eventCount)`.
- It also calls `Interlocked.Add(ref _ingressPendingEstimate, eventCount)`.
- EditMode source guard now asserts both atomic additions.
- Route card and binary payload ledger now state that mock fallback load participates in owner-local backlog pressure.

Cinematic Cheats used:
- Mock analytics remains a cheap deterministic fallback, not a gameplay simulation.
- The correction makes CI heatmap data visible to the existing pressure fake instead of creating a separate mock-only route.

Exact Microseconds saved:
- Normal gameplay cost: 0 us when mock generation is disabled.
- Mock-enabled path: two atomic additions once per generated burst, not per DTO.
- Prevents hidden native queue growth under fallback stress; runtime microseconds are unmeasured behind the foreign compile wall.

Verification state:
- Source guard added for mock backlog accounting.
- Build/import not relaunched by user order and because prior Unity logs already classify the compile wall as foreign-domain failure with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_MOCK_QUEUE_PRESSURE_ACCOUNTING">
  <taskReconciliation count="20" status="unchanged-pass-static" task05="mock-generator-pressure-visible" task10="continuous-culling-sees-mock-backlog" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" />
  <scalability curve="mock-count-20-to-500-plus-backlog-collapse" backlogAccounting="Interlocked.Add-hotEnqueuedDelta-and-ingressPendingEstimate" binaryTierSwitch="none" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
  <dearLie before="mock-events-hidden-from-pressure-model" after="deterministic-mock-load-feeds-existing-backlog-fake" complexityBefore="O(N)-hidden-queue-growth-risk" complexityAfter="O(1)-atomic-accounting-per-mock-burst" />
</SELF_AUDIT>
```

## 2026-05-20 - Worker Atomic State And Cold Guard Hardening

What was wrong:
- Worker flags used volatile read/modify/write from both main and background worker paths.
- `_workerAccumCount` was written with naked worker-side mutation while the main thread sampled it for pressure telemetry.
- Facade-rejected non-finite AUP inputs were counted only as generic drops.
- Runtime layout validation existed, but startup did not call it outside editor/test paths.
- `NativeQueue` expected capacity was registered with the sentinel, but the queue blocks were not cold-prewarmed.

What was done:
- Added CAS-based `SetWorkerFlag` and `ClearWorkerFlag`.
- Published worker accumulation count through `PublishWorkerAccumCount` using `Volatile.Write`.
- Added `_hotNonFiniteDelta`, `NoteHotPathNonFinite`, and counter flush into `AnalyticsCountersDTO.NonFiniteEvents`.
- Added cold `AnalyticsLayout.ValidateOrThrow()` at `OnEnable`.
- Added cold `PrewarmQueue` for routine and critical `NativeQueue<AnalyticEventDTO>` lanes.
- EditMode source guards now assert these hardening points and reject the old worker-flag volatile RMW pattern.

Cinematic Cheats used:
- No new simulation was added. The exporter remains a sampled observation lane.
- Queue prewarm moves native queue block allocation out of gameplay pressure instead of pretending dynamic queue growth is free.

Exact Microseconds saved:
- Normal valid gameplay path: no measured frame saving claimed.
- Fault/state transitions now pay CAS instead of lossy volatile RMW.
- Non-finite path adds atomics only on invalid input.
- Queue prewarm is cold boot cost; it prevents native block growth during pressured runtime enqueue.

Verification state:
- Runtime brace balance after hardening: `Open=263 Close=263 Delta=0`.
- Static forbidden/race-pattern scan found no `Volatile.Write(ref _workerFlags, Volatile.Read(ref _workerFlags)...)`, no naked `_workerAccumCount++`, no naked `_workerAccumCount = 0`, and no existing forbidden network/json/threadpool/runtime-random routes.
- `git diff --check` reports only the known binary payload ledger CRLF warning.
- Build/import not relaunched by user order and foreign dependency wall.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_WORKER_ATOMIC_STATE_AND_COLD_GUARDS">
  <taskReconciliation count="20" status="unchanged-pass-static" task04="runtime-layout-guard-cold-start" task05="native-queue-prewarm" task10="backlog-visible" task16="nonfinite-blackbox-counter" />
  <dto name="AnalyticEventDTO" bytes="32" offsets="EventHashID@0:uint,TimestampSeconds@4:uint,EventAUP@8:double3" alignment="8" pack="none" runtimeGuard="AnalyticsLayout.ValidateOrThrow" />
  <concurrency workerFlags="CAS SetWorkerFlag/ClearWorkerFlag" workerAccumulation="Volatile.Write publication" nonFiniteFacade="hot delta flushed to Vault counters" />
  <vaultBuffers ids="71860..71873" persistentPrivateArrays="0" nativeQueues="routine-critical-cold-prewarmed" />
  <compileGuard build="not-relaunched" reason="foreign dependency wall already logged; no SHINOBU compiler errors found" />
  <dearLie before="dynamic-queue-growth-and-hidden-state-races" after="cold-prewarmed-sampled-observation-lane" complexityBefore="O(N)-runtime-pressure-risk" complexityAfter="O(N)-cold-prewarm-plus-O(1)-runtime-accounting" />
</SELF_AUDIT>
```

## 2026-05-20 - Vault-Owned Ingress Rings Chronology Tail

What was wrong:
- Runtime ingress still used exporter-owned persistent `NativeQueue<AnalyticEventDTO>` lanes.
- That contradicted the stricter current Vault Law wording and the original SHINOBU_160 requirement for a native DTO ring buffer in Vault.
- Mock pressure accounting reported requested mock count, not actual writes when ingress was saturated.

What was done:
- Removed `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, queue labels, `TryGetQueues`, and `PrewarmQueue` from the runtime exporter.
- Added `71874 RoutineIngress`, `71875 CriticalIngress`, and `71876 IngressCursor`.
- Added explicit 64-byte `AnalyticsIngressCursorDTO` with routine/critical cursors, capacities, overflow counters, frame, hash, and padding.
- Rewired hot `TryRecordEvent` to write locked Vault ingress pointers through `UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>` and `AnalyticEventDTO*`.
- Rewired `GenerateMockAnalyticsEventsJob` and `ProcessAnalyticsQueueJob` to use Vault-backed `NativeArray` ingress rings with `[NoAlias]`.
- Mock fallback now adds actual routine cursor delta into `_hotEnqueuedDelta` and `_ingressPendingEstimate`.

Cinematic Cheats used:
- No route simulation was added. The exporter samples owner facts and lets offline analysis reconstruct heatmaps.
- Fixed ring memory makes telemetry invisible to frame time: overflow is dropped/accounted instead of growing a queue or widening the drain.

Exact Microseconds saved:
- Normal hot path remains O(1); no profiler number claimed because Unity compile remains blocked by foreign domains.
- Removes native queue growth/prewarm dependency and per-queue lifecycle cleanup from gameplay runtime.
- Avoids hidden queue block allocation under bursty telemetry pressure; static estimate remains low single-digit microseconds per pressured event, unmeasured.

Verification state:
- Runtime scan found no `AnalyticsEventIngress`, `NativeQueue<AnalyticEventDTO>`, `new NativeQueue`, `TryGetQueues`, `PrewarmQueue`, `RoutineQueue`, `CriticalQueue`, or queue-label remnants.
- Runtime forbidden scan found no `UnityWebRequest`, JSON route, `HttpClient`, `Task.Run`, `ThreadPool`, `Schedule().Complete`, `Time.frameCount`, `UnityEngine.Random`, `new AnalyticEventDTO`, runtime reflection offset guard, `File.Delete(finalPath)`, or binary `pressureCull`.
- Private persistent native collection field scan returned no runtime matches.
- Brace balance after patch: `Open=267 Close=267 Delta=0`.
- `git diff --check` on changed runtime/test C# files returned clean.
- Dotnet/Unity build was not launched by explicit user order and because archived Unity logs already show the foreign compile wall with no SHINOBU compiler errors.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_VAULT_INGRESS_RINGS">
  <taskReconciliation count="20" task01="PASS_STATIC" task02="PASS_STATIC" task03="PASS_STATIC" task04="PASS_STATIC" task05="PASS_STATIC" task06="PASS_STATIC" task07="PASS_STATIC" task08="PASS_STATIC" task09="PASS_STATIC" task10="PASS_STATIC" task11="PASS_STATIC" task12="PASS_STATIC" task13="PASS_STATIC" task14="PASS_STATIC" task15="PASS_STATIC" task16="PASS_STATIC" task17="PASS_STATIC" task18="PASS_STATIC" task19="PASS_STATIC" task20="PASS_STATIC" />
  <structLayout name="AnalyticsIngressCursorDTO" bytes="64" offsets="RoutineWriteCursor@0:uint,RoutineReadCursor@4:uint,CriticalWriteCursor@8:uint,CriticalReadCursor@12:uint,RoutineCapacity@16:int,CriticalCapacity@20:int,RoutineOverflowDrops@24:uint,CriticalOverflowDrops@28:uint,LastFrameIndex@32:uint,StateHash@36:uint,Reserved0@40:uint,Reserved1@44:uint,Reserved2@48:uint,Reserved3@52:uint,Reserved4@56:uint,Reserved5@60:uint" alignment="64-byte control row" />
  <vaultBuffers ids="71860..71876" newIds="71874:RoutineIngress,71875:CriticalIngress,71876:IngressCursor" persistentPrivateNativeCollections="0" />
  <dependencyGraph consumes="DispatcherTimingDTO,SignalBus snapshots,GlobalQualityWeight,Vault handles" outputs="Vault ingress rings,EventRing,Staging,Telemetry,H8_Analytics_IO handoff" noAlias="GenerateMockAnalyticsEventsJob and ProcessAnalyticsQueueJob ingress/event/staging fields" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-launched" reason="user order plus foreign dependency wall already logged" />
  <dearLie before="unbounded/growing ingress abstraction risk" after="fixed Vault rings with representative deterministic culling" complexityBefore="O(1)-enqueue-with-hidden-growth-risk" complexityAfter="O(1)-bounded-ring-write-and-O(min(backlog,qualityBudget))-drain" />
</SELF_AUDIT>
```

## 2026-05-20 - Hot Fixed-Ring Overflow Cursor Tail

What was wrong:
- Live hot-path fixed-ring saturation returned `false` as a generic hot drop while the lane-specific `AnalyticsIngressCursorDTO` overflow fields only reflected mock job saturation.
- Adding cursor overflow without changing `TryRecordEvent` would have double-counted the same saturated write as both `_hotDroppedDelta` and cursor overflow.

What was done:
- Added `IngressWriteRejected`, `IngressWriteAccepted`, and `IngressWriteOverflow` result codes.
- Changed `TryWriteIngressEvent` to return the result code instead of bool.
- On ring-full, the writer now increments `RoutineOverflowDrops` or `CriticalOverflowDrops`, refreshes `AnalyticsIngressCursorDTO.StateHash`, and returns `IngressWriteOverflow`.
- `TryRecordEvent` returns false for overflow but skips `NoteHotPathDropped()` because `ProcessAnalyticsQueueJob` folds cursor overflows into `AnalyticsCountersDTO.DroppedEvents` once.
- EditMode source guards now assert the overflow result path and both lane overflow increments.

Cinematic Cheats used:
- No analytic physics simulation was introduced. Telemetry remains a sampled observation lane.
- Saturation is represented by one fixed cursor fact instead of growing memory, scanning backlogs, or widening frame work.

Exact Microseconds saved:
- Accepted hot writes remain O(1), unmeasured behind the foreign dependency wall.
- Ring-full writes add one cursor increment and one cursor hash; they avoid double accounting and avoid any allocation or queue growth.
- Build/rebuild was not launched by explicit user order and existing dependency-wall evidence.

Verification state:
- Runtime/test focused scan found `IngressWriteOverflow`, the non-double-count guard, and both lane overflow increments.
- Runtime forbidden scan returned no matches for stale queue symbols, main-thread web/network paths, JSON route, threadpool/task route, Unity random/time route, runtime reflection route, or old pressure cull.
- Brace balance after patch: `Open=269 Close=269 Delta=0`.
- `git diff --check` on changed C# files returned clean.
- Documentation updated: status, rationale, route card, binary payload ledger, and this CTO log.

SELF_AUDIT:
```xml
<SELF_AUDIT agent="SHINOBU_160" status="STATIC_POLISH_PASS_HOT_OVERFLOW_CURSOR">
  <taskReconciliation count="20" task01="PASS_STATIC" task02="PASS_STATIC" task03="PASS_STATIC" task04="PASS_STATIC" task05="PASS_STATIC" task06="PASS_STATIC" task07="PASS_STATIC" task08="PASS_STATIC" task09="PASS_STATIC" task10="PASS_STATIC" task11="PASS_STATIC" task12="PASS_STATIC" task13="PASS_STATIC" task14="PASS_STATIC" task15="PASS_STATIC" task16="PASS_STATIC" task17="PASS_STATIC" task18="PASS_STATIC" task19="PASS_STATIC" task20="PASS_STATIC" />
  <structLayout name="AnalyticsIngressCursorDTO" bytes="64" offsets="RoutineWriteCursor@0:uint,RoutineReadCursor@4:uint,CriticalWriteCursor@8:uint,CriticalReadCursor@12:uint,RoutineCapacity@16:int,CriticalCapacity@20:int,RoutineOverflowDrops@24:uint,CriticalOverflowDrops@28:uint,LastFrameIndex@32:uint,StateHash@36:uint,Reserved0@40:uint,Reserved1@44:uint,Reserved2@48:uint,Reserved3@52:uint,Reserved4@56:uint,Reserved5@60:uint" alignment="64-byte control row" />
  <scalability globalQualityWeight="continuous" low="producer pressure gate sheds routine writes before ring pressure; drain budget approaches 10" middle="retains representative route/resource samples with AUP-seeded stochastic culling" high="higher retention and larger drain budget" ultra="drain budget approaches 1000 without changing memory ownership" />
  <vaultBuffers ids="71860..71876" overflowProofIds="71876:IngressCursor.RoutineOverflowDrops/CriticalOverflowDrops" persistentPrivateNativeCollections="0" />
  <dependencyGraph consumes="DispatcherTimingDTO,SignalBus snapshots,GlobalQualityWeight,Vault handles" outputs="Vault ingress rings,EventRing,Staging,Telemetry,H8_Analytics_IO handoff" noAlias="GenerateMockAnalyticsEventsJob and ProcessAnalyticsQueueJob ingress/event/staging fields" />
  <compileGuard siblingRuntimeRefsAdded="none" build="not-launched" reason="user order plus foreign dependency wall already logged" />
  <dearLie before="grow-or-scan-ingress-under-pressure" after="fixed-ring-overflow-counter-as-proof" complexityBefore="O(N)-pressure-investigation-or-hidden-growth-risk" complexityAfter="O(1)-bounded-ring-write-or-overflow-counter" />
</SELF_AUDIT>
```
