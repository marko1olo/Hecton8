# ASYNC_PERSISTENCE_SURGEON Rationale

Status: PENDING - GLOBAL COMPILE BLOCKED

## Decision 0: Batch Scope
Problem: Save hitches are reported at 200ms from main-thread binary serialization and LZ4 work.
Solution: Treat this as Data Archivist persistence work. Main thread may only perform bounded snapshot copy into a persistent staging buffer; compression and disk IO must run from an owned async persistence service.
Rejected Alternatives: Keep SaveManager.Instance and hide File.WriteAllBytes behind a helper. Rejected because singleton access violates the registry mandate and direct writes still risk main-thread stalls.
Scalability potential: Low tier uses the same persistence correctness with minimal status signals; Middle tier can record extra telemetry; High and Ultra can spend saved frame time on richer UI feedback without increasing save truth cost.
Hardware Impact: i3/MX350 target saves the 200ms hitch by removing compression and file IO from the frame; expected main-thread budget is a bounded copy under 5ms pending profiler proof.

## Decision 1: LZ4 Dictionary Scope
Problem: Prompt requires LZ4, but mandate forbids dictionary mode without offline corpus proof and native dictionary bindings.
Solution: Use existing baseline LZ4 binding or project codec surface only. Do not add LZ4 dictionary APIs unless the codebase already owns them.
Rejected Alternatives: Add dictionary compression now. Rejected because current mandate says dictionary APIs are not bound/version-pinned and no benchmark exists.
Scalability potential: Low/Middle/High/Ultra all use the same save format for compatibility; future Ultra compression can be a versioned format upgrade after corpus proof.
Hardware Impact: Avoids risky codec churn on low-end silicon; expected gain is stability, not ratio inflation.

## Decision 2: Registry Service Surface
Problem: Save requests were still exposed only through the legacy save service shape, which encourages direct runtime calls and concrete `SaveManager` lookups.
Solution: Add `IAsyncPersistenceService : ISaveService` with `TryRequestSave(byte slotIndex, uint sourceHash, uint operationId)`, expose it through `GlobalRegistry.AsyncPersistence`, and register `SaveManager` through `RegisterAsyncPersistenceService`.
Rejected Alternatives: Add `SaveManager.Instance` or a static `SaveNow` helper. Rejected because direct singleton access violates the registry mandate and makes concurrent agent work brittle.
Scalability potential: Low tier gets one request lane and one writer; Middle/High/Ultra can add richer UI feedback from status signals without changing the save contract.
Hardware Impact: i3/MX350 avoids multiple full-save jobs contending for 64MB/68MB buffers; expected gain is hitch containment rather than raw throughput.

## Decision 3: Signal-Only Save UI Contract
Problem: The UI spinner and recovery notification needed save state without direct dependencies from persistence into UI controllers.
Solution: Add fixed 32-byte `SaveRequestSignal`, `SaveCompletedSignal`, `SaveStatusSignal`, mirror status into existing `SaveLifecycleSignal`, and emit `HUDNotificationSignal` only on backup/self-repair load recovery.
Rejected Alternatives: Call pause menu or HUD components directly from `SaveManager`. Rejected because it creates scene-order and domain coupling.
Scalability potential: Low tier can show one spinner; High/Ultra can add diegetic save effects from the same hash-only lane.
Hardware Impact: Hash-only NativeQueue events are microsecond-scale and avoid managed string traffic during save state changes.

## Decision 4: Snapshot Pause Boundary
Problem: Background serialization must not read mutable simulation state while other systems write to it.
Solution: Publish `SimulationPauseSignal`, wait one frame, extract the existing save DTO snapshots, stage a fixed native header, then immediately publish resume before compression and disk IO.
Rejected Alternatives: Hold pause through compression/IO. Rejected because it would turn a save hitch into a visible simulation freeze.
Scalability potential: Low tier keeps the pause window bounded; High/Ultra can spend the recovered frame time on save-cover presentation without extending truth extraction.
Hardware Impact: i3/MX350 target keeps the simulation stop to the snapshot window; compression/write are moved out of the frame.

## Decision 5: Async Disk Writer
Problem: The native save writer used synchronous `FileStream.Write`, which still blocks whichever thread owns the save pipeline and violates the requested async IO surface.
Solution: Route `AsyncWriteManager.WriteAll` through an internal async writer using `FileOptions.Asynchronous | FileOptions.SequentialScan`, `WriteAsync`, and one static 64KB scratch buffer guarded by an interlocked spin gate.
Rejected Alternatives: `File.WriteAllBytes` or allocating a managed byte array sized to the save. Rejected because both create large blocking or GC-prone writes.
Scalability potential: Low tier benefits from bounded IO chunks; High/Ultra can increase visual save feedback without changing save correctness.
Hardware Impact: MX350/i3 avoids a large sequential write on the main frame; disk latency is pushed behind the background Awaitable.

## Decision 6: LZ4 Burst Boundary
Problem: Task 9 asks for a Burst-compiled full-save LZ4 job, but the current save codec uses native LZ4 calls with a managed deflate fallback inside protected block compression.
Solution: Keep existing protected LZ4 on the background Awaitable thread and mark true Burst full-save compression blocked until a Burst-safe codec binding exists.
Rejected Alternatives: Wrap the managed fallback in an `IJob` and call it "Burst". Rejected because Burst cannot compile the managed fallback path and the report would be false.
Scalability potential: Low/Middle/High/Ultra keep the same save format. Future Ultra can use a versioned Burst-native codec only after benchmark and compatibility proof.
Hardware Impact: The main hardware win is removing LZ4 from the frame; additional Burst gain is unverified and therefore not claimed.

## Decision 7: Black Box And VRAM Gate
Problem: Save failures and pressure events need post-mortem evidence without allocating log structures during the save path.
Solution: Add a 300-entry `NativeArray<AsyncPersistenceTelemetryEntry>` circular buffer, publish compressed-size/duration telemetry, dump binary state on save failure, and defer generation-0 optimized GC until after save completion and only under VRAM pressure.
Rejected Alternatives: Managed log list or immediate full GC. Rejected because both create avoidable stalls.
Scalability potential: Low tier records minimal state; High/Ultra can visualize telemetry from the same ring and use saved cycles for richer effects.
Hardware Impact: i3/MX350 gets fixed-size telemetry and avoids a save-time GC wall; the GC hook only activates above 1800MB VRAM pressure.

## Decision 8: Compile Wall Handling
Problem: Verification is required, but the project compile currently fails on unrelated cross-domain missing symbols and Unity MCP will not provide a console session.
Solution: Attempt `dotnet build Hecton8.Core.csproj --no-restore`, record the external failures, retry Unity MCP, and mark task 19 blocked by dependency instead of editing other agents' domains.
Rejected Alternatives: Patch audio, physics, GPR, foveated simulation, and binary layout contracts from the persistence task. Rejected as architectural sabotage outside domain.
Scalability potential: Keeping domain boundaries intact prevents persistence from becoming a hidden integrator for unrelated systems.
Hardware Impact: No runtime impact; this is build integrity triage.

## Decision 9: Async Writer Drift Correction
Problem: Re-audit found the temp writer had async `FileStream` flags and a dedicated async scratch buffer, but still called the synchronous pointer segment writer. Screenshot kilobyte telemetry also had an avoidable integer overflow edge.
Solution: Route `AsyncWriteManager.WriteAll` temp segments through `TryWritePointerSegmentAsync`, copy each native chunk into one static 64KB scratch buffer, call `FileStream.WriteAsync(...).GetAwaiter().GetResult()` on the background save path, and keep `OverwriteAll` on the existing synchronous repair writer. Convert screenshot kilobyte rounding to long math before narrowing to int.
Rejected Alternatives: Allocate a managed byte array sized to the save, introduce a `Task.Run` wrapper, or rewrite unrelated compile blockers. Rejected because the first two add GC/scheduler risk and the last violates the domain boundary.
Scalability potential: Low tier keeps bounded 64KB IO chunks and no large managed save buffer; Middle/High/Ultra can spend the reclaimed frame time on save feedback without a different save format.
Hardware Impact: i3/MX350 keeps large temp save writes off the frame and avoids full-payload managed copies; exact profiler proof remains blocked by the global compile wall.

## Decision 10: Background Thread Unity API Purity
Problem: Background save/self-repair could still hit Unity API indirectly: `GetPersistentAbsolutePath` called `HectonPersistentPathPolicy.CombineFile`, which reads `Application.persistentDataPath`, and `SaveBinaryStorage` had an `Application.version` fallback. Load self-repair also built fallback metadata after switching to the background thread.
Solution: Combine relative save paths against the cached persistent root inside `SaveManager`, remove `Application.version` fallback from `SaveBinaryStorage`, construct repair metadata before `Awaitable.BackgroundThreadAsync`, and pass pre-captured backup-retention counts into background promotion/repair.
Rejected Alternatives: Leave static fallbacks because normal metadata is populated. Rejected because the Omega check is about illegal fallback paths, not only the expected happy path.
Scalability potential: Low tier avoids rare background-thread stalls or Unity object access faults during recovery; High/Ultra keep identical save format and can use saved frame time for richer save/recovery presentation.
Hardware Impact: i3/MX350 gain is correctness and hitch containment. Exact microseconds are not measurable without Unity profiler access; expected hot-frame delta remains 0 us because the changed work is cold persistence.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat audit found repeated slot-name hashing in save telemetry and an inflated compressed-size telemetry value of `1` for failed saves.
Solution: Cache the slot hash once inside `RecordAsyncPersistenceTelemetry`, store it in the black-box ring, and emit exact zero scalars for zero duration or zero compressed bytes. Screenshot/file-size telemetry from other active edits remains preserved.
Rejected Alternatives: Leave repeated `ComputeSlotHash(slotName)` calls because save completion is cold. Rejected because the fix is trivial and makes telemetry more truthful.
Scalability potential: Low tier avoids avoidable string traversal on save completion; High/Ultra keep the same telemetry contract with more accurate failure values.
Hardware Impact: i3/MX350 gain is microsecond-scale only; value is accuracy and no avoidable CPU work.

### Cinematic Cheats Used
- Signal-only save presentation: the UI spinner/recovery feedback is a hash event, not a direct UI object walk.
- Pause-window cheat: simulation is paused for the snapshot boundary only, then all expensive save work becomes background presentation cover.
- VRAM pressure gate: GC is delayed until after save and frame delta is below the fixed threshold instead of pretending it is free.

### Anti-Bloat Audit Result
- No new `math.sqrt()` or `math.normalize()` in the persistence additions.
- No new `foreach` or `string.Format` in the persistence additions.
- No new direct UI dependency from persistence; recovery is `HUDNotificationSignal`.
- Burst LZ4 remains blocked rather than faked because current protected compression has native LZ4 plus managed fallback.

### Final Git Diff Evidence
- `Assets/_Project/Scripts/SaveManager.cs`: async persistence service implementation, request/status/completion signal handling, snapshot pause/resume, native staging/telemetry buffers, recovery HUD signal, VRAM post-save GC gate, project-root black-box dump path, telemetry hash cleanup.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: background temp writer now uses `FileStream` async option and `WriteAsync` with fixed scratch.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `IAsyncPersistenceService` contract.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: `AsyncPersistence` registry accessor and registration method.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: fixed-size save request/completed/status signal DTOs and lanes.
- `Assets/_Project/Scripts/Core/Persistence/Hecton8.Core.Persistence.asmdef`: persistence asmdef boundary depending on contracts.
- `Assets/_Project/Scripts/Core/Persistence/PersistenceAssemblyMarker.cs`: assembly marker.
- `Docs/Tasks/Status_ASYNC_PERSISTENCE_SURGEON.md`: task evidence and blocked compile wall.
- `Docs/AgentLogs/Rationale_ASYNC_PERSISTENCE_SURGEON.md`: decisions and Omega polish.
