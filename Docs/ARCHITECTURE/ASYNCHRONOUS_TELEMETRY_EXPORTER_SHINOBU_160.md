# SHINOBU_160 Asynchronous Telemetry Exporter



Owner: SHINOBU_160



Domain: Echelon 9 Meta/Polish/Integration



Runtime file: `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`



First 20 Minutes moment: Proof/route-testability for Copper Wire route deaths, resource pickups, route samples, hazard/perf spikes, and return-path heatmap review.



## Source Anchors



Evidence: UNITY_IMPORT_ATTEMPTED / COMPILE_BLOCKED_BY_DEPENDENCIES.

- Proves cited paths exist.
- Proves Unity saw runtime source during batchmode compilation.
- Does not prove clean compile, Play Mode, profiler, GC, player-build, network, endpoint, or analytics runtime.



- `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`



- `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs.meta`



- `Assets/_Project/Tests/Editor/AsynchronousTelemetryExporterEditTests.cs`



## Route



- Gameplay producers publish unmanaged `AnalyticEventDTO` records through the owner-local `TryRecordEvent` facade into Vault-owned routine or critical analytics ring buffers.

- The facade is owner-thread gated and performs continuous quality/backlog shedding before ring write; hot-path counters are atomics flushed to Vault in `POST_SIMULATION`, not per-event Vault metadata writes.

- Non-finite facade inputs increment a hot non-finite delta and fail closed before entering ingress.

- If a fixed ingress ring saturates, `TryWriteIngressEvent` increments the lane-specific `AnalyticsIngressCursorDTO` overflow field and returns an overflow result so the generic hot-drop delta is not incremented a second time.

- Exporter drains critical telemetry first, then routine telemetry. Quality-derived budget bounds POST_SIMULATION by `min(stagingCapacity, lerp(10,1000,GlobalQualityWeight))`, not ring backlog.

- Routine drain pressure uses deterministic quality/backlog/AUP-bit decimation, not an all-or-nothing threshold.

- Accepted events mirror into Vault and fixed batches hand off to the `H8_Analytics_IO` background thread.

- Worker flags use CAS helpers.
- Worker accumulation count is volatile-published for backlog telemetry.
- Gameplay thread excludes HTTP scheme validation, network send, failed-response disposal, RLE compression, disk fallback, and black-box file writes.



- Frame identity comes from `DispatcherTimingDTO.FrameId`, with an owner-local fallback counter only when the dispatcher sends zero.
- Emergency mock RNG: `Unity.Mathematics.Random`.
- Seed: `SystemHash ^ SectorHash ^ SimulationFrame`.
- Mock writes are counted from the ingress cursor delta.
- CI fallback load stays visible to culling; overflow does not over-report backlog.
- Cold startup runs the reflectionless DTO layout guard and initializes the Vault ingress cursor capacities before gameplay.
- No SHINOBU runtime path reads `Time.frameCount` or `UnityEngine.Random`.



- Existing gameplay truth enters through contract `SignalBus` snapshots only:
  - `EntityDeathSignal` and `SurvivalVitalsChangedSignal`: death telemetry.
  - `ItemAcquiredSignal`: resource telemetry.
  - `FrameTimeSignal`: perf-spike telemetry, anchored to last KCC AUP.
  - Fresh KCC velocity snapshots: player anchor refresh during `POST_SIMULATION`.
  - Route heatmap disabled: anchor refresh still runs.
- Sparse route heatmap samples are emitted only when the heatmap flag is enabled and the configurable sample timer has elapsed.



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



- Routine and critical ingress are now fixed Vault rings, not exporter-owned persistent `NativeQueue` objects.
- Exporter locks ingress and worker transfer buffers against compaction while active.
- It keeps only `VaultBufferHandle<T>` fields.
- Hot facade events write through locked handle pointers into `RoutineIngress` or `CriticalIngress`.
- `AnalyticsIngressCursorDTO` is explicit 64 bytes: cursors, capacities, overflow counters, frame, state hash. Drain job folds live/mock overflow into `DroppedEvents` once.
- Telemetry backlog fields use the same owner-local pressure estimate as culling: ingress pending plus handoff plus worker accumulation.


Worker transfer:

- Owner: Vault.
- Lifetime: locked against compaction while `H8_Analytics_IO` is alive.
- State machine: `Idle -> Writing -> Pending -> Idle`.
- Worker safety: cannot observe a partially copied Vault batch.
- Background views: cached locked handle pointers via `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray`.
- Rejected route: worker-side `GlobalDataVault.ResolveBuffer`.



Shutdown cleanup runs only after `StopWorker()` succeeds.

If first `OnDisable` cannot stop the worker, `OnDestroy` retries and keeps Vault locks while dereference risk exists. Subsystem registration resets the static exporter reference.



## Scaling



- `GlobalQualityWeight` continuously maps routine event retention and per-frame drain work from roughly 10 records per drain at `q=0` to 1000 at `q=1`.
- Routine pressure culling uses a deterministic hash of event type, timestamp, backlog, and full AUP double lanes so load shedding remains spatially distributed instead of dropping same-second cohorts.
- Critical hashes use the high bit, route through the critical ingress lane, and survive routine pressure culling.



`q=0.00..0.25`: cull routine route/resource/mock samples early, retain death/perf events, disk fallback only if needed.

`q=0.25..0.55`: retain route samples every 5 seconds from KCC AUP.

`q=0.55..0.85`: keep dense staging and editor heatmap.

`q=0.85..1.00`: tolerate larger backlog and deterministic mock stress without changing gameplay determinism.



## Serialization



Event payloads are fixed little-endian binary:



- `uint EventHashID`



- `uint TimestampSeconds`



- `double AUP.x`



- `double AUP.y`



- `double AUP.z`



- Compression is explicitly RLE envelope compression, not claimed LZ4.
- `CompressAnalyticsBufferJob` remains a Burst unmanaged RLE kernel, while the live worker path uses equivalent span RLE because Unity Job scheduling from the dedicated I/O thread is rejected.
- Endpoint configuration is cold CSV only from `telemetry_config.csv` with legacy `analytics_endpoint.csv` fallback; runtime hot capture does not use JSON, `UnityWebRequest`, managed worker arrays, `ParallelWriter` caching, or `Schedule().Complete()` fences.
- Disk fallback writes uniquely sequenced `.tmp` files with `FileMode.CreateNew`, flushes, then atomically renames to `.h8log`.
- Existing final backlog files are not deleted.
- Replay validates each payload; corrupt/partial/replayed files delete only after the read stream closes.
- Replay exceptions or short reads increment fault counters without killing the worker loop.
- Failed publication cleans its `.tmp` residue on the worker path.
- Shutdown aborts active `HttpWebRequest` before the second bounded worker join.
- If worker still lives, exporter enters hard local fault state.
- Vault locks remain held instead of clearing active ownership.


## Verification



- Static forbidden-source scan text:
  - Result: no matching SHINOBU runtime/editor forbidden-route tokens in exporter source.
  - Forbidden routes checked: `UnityWebRequest`, JSON serialization, `HttpClient`, `Task.Run`, `ThreadPool`, private worker arrays, public analytics `ParallelWriter`, direct `Hecton8.World` import.
  - Layout/API checks: no `Pack=`, DTO properties, `H8Memory.CreateNativeArrayView`, `Directory.GetFiles`, old `IngressQueue`, old `TryGetQueue(`, or `Schedule().Complete()` fence.
  - Evidence class: `STATIC_SOURCE`.
  - Required before current proof: command, timestamp, environment, output artifact.



- 2026-05-20 polish scan reported zero matches for `Time.frameCount`, `UnityEngine.Random`, and `new AnalyticEventDTO`.
- Scope: runtime exporter source after dispatcher-frame and deterministic mock RNG repair.
- Evidence: `STATIC_SOURCE`; current proof still needs an artifact tuple.



- 2026-05-20 bounded-drain polish added routine/critical queue labels, quality-derived `drainBudget`, and `VaultBytes` at telemetry DTO offset 60.
- Build/import was not relaunched: one guard found active `dotnet`; later guard found active `dotnet` plus CPU above 50%.



- 2026-05-20 lifecycle hardening added partial ingress rollback, deferred destroy cleanup, and editor null guards.
- Static scan after this patch reported brace balance `Open=228 Close=228 Delta=0`.
- Runtime forbidden-route matches: `0`.



- 2026-05-20 KCC/mock polish:
  - corrected anchor freshness and CI fallback load scaling;
  - static scan: `Open=226 Close=226 Delta=0`;
  - runtime forbidden-route matches: `0`;
  - fixed `EventCount = 500`: absent;
  - `git diff --check` text recorded for changed C# files;
  - requires command, timestamp, environment, and output before proof upgrade.



- 2026-05-20 reflectionless layout polish:
  - Removed runtime `System.Reflection` / `typeof(...).GetField(...)` from `AnalyticsLayout`.
  - DTO offset check route: `UnsafeUtility.AddressOf` pointer arithmetic on unmanaged locals.
  - Static scan text: `Open=229 Close=229 Delta=0`.
  - Runtime reflection offset route: none reported.
  - `git diff --check`: recorded for changed C# files.
  - Required before proof: command, timestamp, environment, output artifact.



- 2026-05-20 disk replay polish moved corrupt/replayed `.h8log` deletion outside the open `FileStream` scope and wrapped replay faults so poison files do not kill `H8_Analytics_IO`.
- Static scan after this patch: brace balance `Open=238 Close=238 Delta=0`.
- Forbidden runtime route matches: `0`; hot private persistent collection fields: `0`.
- `git diff --check` text reported the known ledger CRLF warning; link command, timestamp, environment, and output before treating it as proof.



- 2026-05-20 partial-read/fallback-publication/AUP-gate polish:
  - Short reads against closed `.h8log` length become poison-file faults.
  - Poison file is marked for after-close deletion.
  - Partial buffer validation/resend is skipped.
  - Fallback publication cannot delete an existing final `.h8log` on filename collision.
  - Routine stochastic culling seed uses event AUP bits.
- Static scan after the partial-read patch:
  - Brace balance: `Open=245 Close=245 Delta=0`.
  - Runtime/editor forbidden-route matches: none reported.
  - `File.Delete(finalPath)`: none reported.
  - Hot private persistent collection fields: none reported.
  - `git diff --check`: recorded for changed C# files.
  - Required before proof: command, timestamp, environment, output artifact.



- 2026-05-20 hot-overflow cursor polish:
  - Split fixed-ring saturation from generic hot drops.
  - Brace balance scan: `Open=269 Close=269 Delta=0`.
  - Stale `NativeQueue<AnalyticEventDTO>` / `AnalyticsEventIngress` route: none reported.
  - Forbidden runtime web/json/threadpool/random/reflection routes: none reported.
  - `git diff --check`: recorded for changed C# files.
  - Required before proof: command, timestamp, environment, output artifact.



- Worker-owned Vault buffers use cached locked handle pointer views on `H8_Analytics_IO`; the worker does not enter `GlobalDataVault.ResolveBuffer` for handoff, accumulation, raw scratch, compressed scratch, or dump snapshot.



- Unity batchmode compile log: `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile.log`.



- Unity batchmode hotpath/dump patch log: `Docs/Archive/Batch010/AgentLogs/Unity_SHINOBU_160_compile_after_hotpath.log`.



- Compile status: blocked by dependency errors in `Physics/HabitatFluidIncursionJobs.cs`, `World/ProceduralCoral/*`, `World/ProceduralWreckage/*`, `Narrative/Prologue/AwaitableDropSequenceDirector.cs`, and `Hecton8.MockDomain.Runtime` Burst ILPostProcessor. No SHINOBU-specific compiler errors were found by targeted log search.
