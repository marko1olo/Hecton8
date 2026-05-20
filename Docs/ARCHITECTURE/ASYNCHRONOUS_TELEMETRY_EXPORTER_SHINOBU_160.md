# SHINOBU_160 Asynchronous Telemetry Exporter

Owner: SHINOBU_160
Domain: Echelon 9 Meta/Polish/Integration
Runtime file: `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`
First 20 Minutes moment: Proof/route-testability for Copper Wire route deaths, resource pickups, route samples, hazard/perf spikes, and return-path heatmap review.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary
This route card is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction). R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R37/R36/R35/R34/R33 remain prior correction layers. Current static gates: `Tools/AtlasCheck.py` fails `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes as static-tool orientation only.

Unity batchmode import/compile was attempted on 2026-05-20 and the log is archived at `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile.log`. A second attempt after hotpath/dump repair is archived at `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log`. Active SHINOBU_160 status/rationale/log files have been restored under `Docs/Tasks` and `Docs/AgentLogs`, but the active `Docs/Tasks/CURRENT_BATCH.md` has shifted to `SHINOBU_200+` and no longer contains the SHINOBU_160 prompt. Targeted searches found no `AsynchronousTelemetryExporter*.cs(` compiler errors, but project compilation is blocked by unrelated domains. No clean Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, analytics endpoint, network send, disk fallback, or visual proof is implied until the dependency wall is cleared.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: UNITY_IMPORT_ATTEMPTED / COMPILE_BLOCKED_BY_DEPENDENCIES. These anchors prove that the cited local paths exist and that Unity saw the runtime source during batchmode compilation; they are not clean compile, Play Mode, profiler, GC, player-build, network, endpoint, or analytics-runtime proof.

- `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`
- `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs.meta`
- `Assets/_Project/Tests/Editor/AsynchronousTelemetryExporterEditTests.cs`

## Route

Gameplay producers publish unmanaged `AnalyticEventDTO` records through the owner-local `TryRecordEvent` facade into Vault-owned routine or critical analytics ring buffers. The facade is owner-thread gated and performs continuous quality/backlog shedding before ring write; hot-path counters are atomics flushed to Vault in `POST_SIMULATION`, not per-event Vault metadata writes. Non-finite facade inputs increment a hot non-finite delta and fail closed before entering ingress. If a fixed ingress ring saturates, `TryWriteIngressEvent` increments the lane-specific `AnalyticsIngressCursorDTO` overflow field and returns an overflow result so the generic hot-drop delta is not incremented a second time. The exporter drains critical telemetry first, then routine telemetry, with a quality-derived drain budget so POST_SIMULATION cost is bounded by `min(stagingCapacity, lerp(10,1000,GlobalQualityWeight))` rather than total ring backlog. Routine drain pressure uses deterministic quality/backlog/AUP-bit decimation, not an all-or-nothing threshold. Accepted events mirror into Vault and fixed batches hand off to the `H8_Analytics_IO` background thread. Worker flags use CAS helpers, worker accumulation count is volatile-published for backlog telemetry, and HTTP scheme validation, network send, failed-response disposal, RLE compression, disk fallback, and black-box file writes do not run on the gameplay thread.
Frame identity comes from `DispatcherTimingDTO.FrameId`, with an owner-local fallback counter only when the dispatcher sends zero. The emergency mock generator uses `Unity.Mathematics.Random` seeded by `SystemHash ^ SectorHash ^ SimulationFrame`; generated mock writes are counted from the ingress cursor delta, so CI fallback load cannot hide from continuous culling and overflow does not over-report backlog. Cold startup runs the reflectionless DTO layout guard and initializes the Vault ingress cursor capacities before gameplay. No SHINOBU runtime path reads `Time.frameCount` or `UnityEngine.Random`.
Existing gameplay truth enters through contract `SignalBus` snapshots, not concrete domain references: `EntityDeathSignal` and `SurvivalVitalsChangedSignal` feed death telemetry, `ItemAcquiredSignal` feeds resource telemetry, `FrameTimeSignal` feeds perf-spike telemetry anchored to the last KCC AUP, and fresh KCC velocity snapshots refresh the player anchor during `POST_SIMULATION` even when route heatmap export is disabled. Sparse route heatmap samples are emitted only when the heatmap flag is enabled and the configurable sample timer has elapsed.
This makes the First 20 Minutes route more testable; it does not make a new gameplay route playable by itself.

## Vault Buffers

- `71860` `EventRing`: `AnalyticEventDTO[]`, native circular event truth.
- `71861` `Staging`: `AnalyticEventDTO[]`, per-frame POST_SIMULATION drain staging.
- `71862` `Counters`: `AnalyticsCountersDTO[1]`, false-sharing padded counters.
- `71863` `TelemetryRing`: `AnalyticsExporterTelemetryEntry[300]`, black-box health.
- `71864` `TelemetryCursor`: `int[1]`, telemetry ring cursor.
- `71865` `Tuning`: `AnalyticsTuningDTO[1]`, continuous quality and batch scalars.
- `71866` `CsvScratch`: `byte[16384]`, cold endpoint CSV ingest.
- `71867` `CompressedScratch`: `byte[393304]`, Vault-owned compressed worker scratch, sized for worst-case RLE envelope.
- `71868` `HeatmapDebug`: `AnalyticEventDTO[512]`, editor heatmap readback lane.
- `71869` `HandoffA`: `AnalyticEventDTO[4096]`, first POST_SIMULATION-to-worker transfer buffer.
- `71870` `HandoffB`: `AnalyticEventDTO[4096]`, second POST_SIMULATION-to-worker transfer buffer.
- `71871` `WorkerAccum`: `AnalyticEventDTO[4096]`, background-thread accumulation buffer.
- `71872` `RawBatchScratch`: `byte[131096]`, raw little-endian payload scratch before RLE.
- `71873` `DumpSnapshot`: `byte[19232]`, fixed black-box snapshot written by POST_SIMULATION and flushed to disk by the worker.
- `71874` `RoutineIngress`: `AnalyticEventDTO[]`, Vault-owned routine ingress ring.
- `71875` `CriticalIngress`: `AnalyticEventDTO[]`, Vault-owned critical ingress ring.
- `71876` `IngressCursor`: `AnalyticsIngressCursorDTO[1]`, 64-byte ring cursor/control row.

Routine and critical ingress are now fixed Vault rings, not exporter-owned persistent `NativeQueue` objects. The exporter locks ingress and worker transfer buffers against compaction while active, keeps only `VaultBufferHandle<T>` fields, and writes hot facade events through the locked handle pointers into `RoutineIngress` or `CriticalIngress`. `AnalyticsIngressCursorDTO` is explicit 64 bytes, owns read/write cursors, capacities, overflow counters, frame, and state hash; live hot overflow and mock overflow are both folded into `DroppedEvents` once by the drain job. Telemetry backlog fields use the same owner-local pressure estimate as culling: ingress pending plus handoff plus worker accumulation.
The worker transfer buffers are Vault-owned and locked against compaction while `H8_Analytics_IO` is alive. Handoff publication uses an `Idle -> Writing -> Pending -> Idle` state machine so the worker cannot observe a partially copied Vault batch. The background thread creates transient views from cached locked handle pointers via `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray` and does not call `GlobalDataVault.ResolveBuffer` on worker-owned buffers.
Shutdown cleanup runs only after `StopWorker()` succeeds; if a first `OnDisable` cannot stop the worker, `OnDestroy` retries and preserves Vault locks while the worker might still dereference them. The static active exporter reference resets on subsystem registration so stale facade writes fail closed.

## Scaling

`GlobalQualityWeight` continuously maps routine event retention and per-frame drain work from roughly 10 records per drain on survival-tier hardware to 1000 on ultra-tier hardware. Routine pressure culling uses a deterministic hash of event type, timestamp, backlog, and full AUP double lanes so load shedding remains spatially distributed instead of dropping same-second cohorts. Critical hashes use the high bit, route through the critical ingress lane, and survive routine pressure culling.

Low: cull routine route/resource/mock samples early, retain death/perf events, disk fallback only if needed.
Middle: retain route samples every 5 seconds from KCC AUP.
High: keep dense staging and editor heatmap.
Ultra: tolerate larger backlog and richer deterministic mock stress without changing gameplay determinism.

## Serialization

Event payloads are fixed little-endian binary:

- `uint EventHashID`
- `uint TimestampSeconds`
- `double AUP.x`
- `double AUP.y`
- `double AUP.z`

Compression is explicitly RLE envelope compression, not claimed LZ4. `CompressAnalyticsBufferJob` remains a Burst unmanaged RLE kernel, while the live worker path uses equivalent span RLE because Unity Job scheduling from the dedicated I/O thread is rejected. Endpoint configuration is cold CSV only from `telemetry_config.csv` with legacy `analytics_endpoint.csv` fallback; runtime hot capture does not use JSON, `UnityWebRequest`, managed worker arrays, `ParallelWriter` caching, or `Schedule().Complete()` fences. Disk fallback writes uniquely sequenced `.tmp` files with `FileMode.CreateNew`, flushes, atomically renames to `.h8log` without deleting an existing final backlog file, validates each replay payload, deletes corrupt/partial/replayed files only after the read stream is closed, and fault-counts replay exceptions or short reads without killing the worker loop. Failed publication cleans its `.tmp` residue on the worker path. Shutdown aborts the active `HttpWebRequest` before the second bounded worker join; if the worker still lives, the exporter enters a hard local fault state and keeps Vault locks instead of clearing active ownership.

## Verification

- Static forbidden-source scan text reported no matching SHINOBU runtime/editor forbidden-route tokens: no `UnityWebRequest`, JSON serialization route, `HttpClient`, `Task.Run`, `ThreadPool`, private worker arrays, public analytics `ParallelWriter`, direct `Hecton8.World` import, `Pack=`, DTO properties, `H8Memory.CreateNativeArrayView`, `Directory.GetFiles`, old single-lane `IngressQueue` field, old `TryGetQueue(` API, or `Schedule().Complete()` fence remains in the exporter source. Evidence class: `STATIC_SOURCE`; artifact tuple required before treating this as current proof.
- 2026-05-20 active polish scan text reported no `Time.frameCount`, `UnityEngine.Random`, or `new AnalyticEventDTO` matches in the runtime exporter source after dispatcher-frame and deterministic mock RNG repair. Evidence class: `STATIC_SOURCE`; artifact tuple required before treating this as current proof.
- 2026-05-20 bounded-drain polish added routine/critical queue labels, quality-derived `drainBudget`, and `VaultBytes` at telemetry DTO offset 60. Build/import was not relaunched because one guard found active `dotnet` despite CPU below 50%, and the later guard found both active `dotnet` and CPU averaging above 50%.
- 2026-05-20 lifecycle hardening added partial ingress rollback, deferred destroy cleanup, and editor null guards. Static scan after this patch reported brace balance `Open=228 Close=228 Delta=0` and no runtime forbidden-route matches.
- 2026-05-20 KCC/mock polish corrected anchor freshness and CI fallback load scaling. Static scan after this patch reported brace balance `Open=226 Close=226 Delta=0`, no runtime forbidden-route matches, no fixed `EventCount = 500`, and `git diff --check` text was recorded for the changed C# files; link command, timestamp, environment, and output before treating it as proof.
- 2026-05-20 reflectionless layout polish removed runtime `System.Reflection`/`typeof(...).GetField(...)` from `AnalyticsLayout`; primary DTO offsets are checked through `UnsafeUtility.AddressOf` pointer arithmetic on unmanaged locals. Static scan after this patch reported brace balance `Open=229 Close=229 Delta=0`, no runtime reflection offset route, and `git diff --check` text was recorded for the changed C# files; link command, timestamp, environment, and output before treating it as proof.
- 2026-05-20 disk replay polish moved corrupt/replayed `.h8log` deletion outside the open `FileStream` scope and wrapped replay faults so poison files do not kill `H8_Analytics_IO`. Static scan after this patch reported brace balance `Open=238 Close=238 Delta=0`, no forbidden runtime route matches, no hot private persistent collection fields, and `git diff --check` text reported the known ledger CRLF warning; link command, timestamp, environment, and output before treating it as proof.
- 2026-05-20 partial-read/fallback-publication/AUP-gate polish treats short reads against a closed `.h8log` length as poison-file faults, marks the file for after-close deletion, skips validation/resend of the partial buffer, prevents fallback publication from deleting an existing final `.h8log` on filename collision, and seeds routine stochastic culling with event AUP bits. Static scan after this patch reported brace balance `Open=245 Close=245 Delta=0`, no runtime/editor forbidden-route matches, no `File.Delete(finalPath)`, no hot private persistent collection fields, and `git diff --check` text was recorded for the changed C# files; link command, timestamp, environment, and output before treating it as proof.
- 2026-05-20 hot-overflow cursor polish split fixed-ring saturation from generic hot drops. Static scan after this patch reported brace balance `Open=269 Close=269 Delta=0`, no stale `NativeQueue<AnalyticEventDTO>`/`AnalyticsEventIngress` route, no forbidden runtime web/json/threadpool/random/reflection routes, and `git diff --check` text was recorded for changed C# files; link command, timestamp, environment, and output before treating it as proof.
- Worker-owned Vault buffers use cached locked handle pointer views on `H8_Analytics_IO`; the worker does not enter `GlobalDataVault.ResolveBuffer` for handoff, accumulation, raw scratch, compressed scratch, or dump snapshot.
- Unity batchmode compile log: `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile.log`.
- Unity batchmode hotpath/dump patch log: `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log`.
- Compile status: blocked by dependency errors in `Physics/HabitatFluidIncursionJobs.cs`, `World/ProceduralCoral/*`, `World/ProceduralWreckage/*`, `Narrative/Prologue/AwaitableDropSequenceDirector.cs`, and `Hecton8.MockDomain.Runtime` Burst ILPostProcessor. No SHINOBU-specific compiler errors were found by targeted log search.


