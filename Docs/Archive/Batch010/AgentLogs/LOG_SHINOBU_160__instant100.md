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
