# ASYNC_PERSISTENCE_SURGEON Status

Status: PENDING - GLOBAL COMPILE BLOCKED BY OTHER AGENTS
Domain: CORE & MEMORY INFRASTRUCTURE / Data Archivist Persistence
Prompt: Background LZ4 Saving
Task Count: 19

## Mandates Read
- DATA_Save_Persistence_Binary_Delta_Checksum
- STRM_Async_Standard
- STRM_ModuleDTO_LZ4_Dictionary
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- DBG_Telemetry_Crash_Reporting_PostMortem
- ARCH_Global_Registry_ServiceLocator_DI_Init
- OPT_Performance_Budgets_FrameTime_VRAM_Limits

## Checklist
- [x] 1. SINGLETON ERADICATION: `SaveManager.Instance` was not present; `SaveManager` now implements `IAsyncPersistenceService`, registers through `GlobalRegistry.RegisterAsyncPersistenceService`, and exposes `GlobalRegistry.AsyncPersistence`. DOD: registry-owned service slot. Rejected: new singleton alias. Estimate: 0 us/frame.
- [x] 2. SIGNAL MIGRATION: Added `SaveRequestSignal`, `SaveCompletedSignal`, and `SaveStatusSignal`; `SaveManager.Tick` drains request signals and emits status/completion. DOD: NativeQueue/SignalBus lane, hash payloads only. Rejected: direct UI/controller calls. Estimate: <5 us per save request.
- [x] 3. ASMDEF ISOLATION: Added `Assets/_Project/Scripts/Core/Persistence/Hecton8.Core.Persistence.asmdef` depending on `Hecton8.Core.Contracts` plus a marker type. DOD: isolated persistence assembly boundary exists. Rejected: pushing new interfaces into feature asmdefs. Estimate: 0 us/frame.
- [x] 4. DEAD CODE HUNT: Runtime search found no runtime `File.WriteAllBytes`; remaining hits are editor authoring tools. Runtime save writer now uses `FileStream` with async option. DOD: no runtime main-thread `File.WriteAllBytes`. Rejected: editing editor asset tools outside domain. Estimate: removes worst-case blocking write from frame.
- [x] 5. THE MEMORY ARENA: Allocated persistent 10MB `_saveStagingBuffer` and registered it with `NativeMemorySentinel`. DOD: fixed NativeArray arena. Rejected: per-save managed byte array staging. Estimate: prevents 10MB per-save managed allocation.
- [x] 6. PRE_SIMULATION SNAPSHOT: Save path publishes `SimulationPauseSignal`, waits one frame, snapshots existing save DTOs, and writes a fixed `SaveStagingHeader` into the native arena. DOD: pause gate plus DTO snapshot boundary. Rejected: serializing while simulation mutates. Estimate: bounded snapshot target <5ms, pending profiler proof.
- [x] 7. RESUME: Save path publishes resume immediately after snapshot staging and before background compression/IO. DOD: pause is not held during disk work. Rejected: holding simulation frozen through compression. Estimate: avoids 100+ ms freeze.
- [x] 8. AWAITABLE BACKGROUND: Save path captures main-thread frame data, then calls `Awaitable.BackgroundThreadAsync()` before verified save pipeline execution. DOD: Unity API capture before background switch. Rejected: `Task.Run` service fork. Estimate: removes compression/IO from frame.
- [x] 9. [BLOCKED BY CODEC DEPENDENCY] LZ4 BURST: Existing protected LZ4 block compression runs on the background thread. True Burst-compiled full-save LZ4 is blocked because the current codec uses native LZ4 plus managed fallback and no Burst-safe full-save dictionary/job binding exists. DOD used: no new unproven codec path. Rejected: fake Burst wrapper around managed fallback. Estimate: main-thread cost removed; Burst delta unverified.
- [x] 10. FILE IO: `AsyncWriteManager.WriteAll` now routes temp-file segments through `FileStream(... FileOptions.Asynchronous ...)` and `WriteAsync` using static 64KB scratch; write handles are explicit `using FileStream` scopes. DOD: temp save writer uses async file API on the background path; repair overwrite keeps the existing synchronous segment writer. Rejected: `File.WriteAllBytes` and per-call byte array copy. Estimate: removes blocking sequential disk write from frame.
- [x] 11. ATOMIC RENAME: Existing `CommitTempSaveToPrimary` still promotes `.tmp` to `.sav` and retains `.bak` generations after write verification. DOD: transaction remains temp-first. Rejected: direct primary overwrite. Estimate: correctness item, not frame budget.
- [x] 12. CONCURRENT SAVE LOCK: `_isBusy` rejects direct and signal-driven save requests, emits rejected status, and keeps only one background save in flight. DOD: one writer owner. Rejected: queueing multiple full-save jobs. Estimate: prevents multi-10MB buffer contention.
- [x] 13. CORRUPTION RECOVERY: Load path already falls back to backup/self-repair; added `HUDNotificationSignal` when backup or repair is used. DOD: player-visible recovery signal. Rejected: silent repair only. Estimate: 0 us/frame except recovery event.
- [x] 14. [BLOCKED BY EXISTING SAVE DTO CONTRACT] ZERO-GC: New request/status/telemetry paths are fixed-size and stringless. Full snapshot still passes through existing managed `SaveData`/collections, so absolute 0 managed bytes cannot be certified in this batch. DOD used: no new hot-path managed staging allocations. Rejected: unsafe rewrite of every saveable contract. Estimate: new code adds 0 steady-frame GC.
- [x] 15. MATH LOD: IO is tier-invariant. Low/Middle/High/Ultra behavior is documented in rationale; saved cycles are visual budget, not altered save truth. DOD: same format for every tier. Rejected: tier-dependent save format. Estimate: 0 us/frame.
- [x] 16. BLACKBOX DUMP: Added 300-entry native telemetry ring for `SaveDurationMs`, `CompressedSizeBytes`, raw bytes, flags, slot hash; failure dumps to `Docs/AgentLogs/Dump_ASYNC_PERSISTENCE_SURGEON.bin`. DOD: fixed NativeArray circular buffer. Rejected: managed log list. Estimate: <5 us per save completion.
- [x] 17. UI SPINNER: `SaveStatusSignal(InProgress)` is emitted when save starts, then `Completed`/`Failed`/`Rejected` on terminal states; lifecycle mirror also feeds legacy consumers. DOD: hash-only status lane. Rejected: direct pause-menu coupling. Estimate: <5 us per state event.
- [x] 18. VRAM ABORT: After save completion, if VRAM exceeds 1800MB, `GC.Collect(0, Optimized, false)` is deferred until frame delta is under 14ms. DOD: post-save only, frame-gated. Rejected: full blocking GC during save. Estimate: avoids mid-save spike; GC cost only under pressure.
- [x] 19. [BLOCKED BY GLOBAL COMPILE WALL] OMEGA COMPILE CHECK: `dotnet build Hecton8.Core.csproj --no-restore` fails on 114 unrelated errors before persistence proof; Unity MCP reports no active Unity session for console reads. Static scan now confirms `SaveBinaryStorage` has no `Application.*`, `SceneManager`, `Time.*`, or `GlobalRegistry` references. DOD: compile attempted and blocker recorded. Rejected: changing other agents' domains. Estimate: no frame estimate.

## Loop Log
- Loop 0: Prompt extracted from `CURRENT_BATCH.md`. Domain and mandates verified. Code untouched.
- Loop 1: Tasks 1-5 implemented/audited. Verified no `SaveManager.Instance`; added service interface, signal DTOs, asmdef marker, staging arena. Compile deferred until helper wiring existed.
- Loop 2: Re-read prompt by CLI after task 5. Tasks 6-10 implemented/audited. Added pause/resume, background Awaitable pipeline handoff, async file writes. Rejected fake Burst LZ4 wrapper.
- Loop 3: Tasks 11-14 audited. Existing temp/backup path retained; concurrent save rejection wired; recovery HUD notification patched; zero-GC hard claim blocked by existing `SaveData` contract.
- Loop 4: Tasks 15-18 implemented/audited. Added telemetry black box, lifecycle/status mirroring, frame-gated VRAM GC. Verified black-box dump path resolves from project root.
- Loop 5: Task 19 verification attempted. Unity refresh timed out; Unity console read failed with `no_unity_session`; `dotnet build Hecton8.Core.csproj --no-restore` failed on unrelated cross-domain symbols/asmdefs before persistence proof.
- Loop 6: Re-audit found async writer drift: `WriteAll` had async flags but called the synchronous segment writer. Patched temp-file writes to `TryWritePointerSegmentAsync`/`FileStream.WriteAsync`, preserved synchronous repair overwrite, fixed screenshot KB overflow edge, reran build and Unity console retry.
- Loop 7: Re-read prompt and mandates. Audited background-thread purity. Removed `Application.version` fallback from `SaveBinaryStorage`, stopped background `GetPersistentAbsolutePath` from re-entering `HectonPersistentPathPolicy.CombineFile`, captured backup retention before background save/self-repair rotation, and moved load self-repair metadata construction before `Awaitable.BackgroundThreadAsync`.

## Verification Evidence
- `rg "SaveManager\\.Instance" Assets/_Project/Scripts` produced no runtime singleton usage.
- `rg "File\\.WriteAllBytes" Assets/_Project/Scripts` found editor-only authoring/test files, not runtime save path.
- `rg "WriteAsync|TryWritePointerSegmentAsync" Assets/_Project/Scripts/SaveBinaryStorage.cs` confirms temp save segments now use `FileStream.WriteAsync`; repair overwrite still uses `TryWritePointerSegment`.
- `rg "Application\\.version|Application\\.persistentDataPath|Application\\.dataPath|SceneManager|Time\\.frameCount|Time\\.realtime|GlobalRegistry" Assets/_Project/Scripts/SaveBinaryStorage.cs` returns no matches; binary writer no longer has Unity API/registry fallback references.
- `rg "await Awaitable\\.BackgroundThreadAsync\\(|await Awaitable\\.MainThreadAsync\\(|ExecuteVerifiedSavePipeline\\(|SelfRepairPrimaryArtifacts\\(" Assets/_Project/Scripts/SaveManager.cs` confirms save write and load self-repair background sections are bracketed by explicit main-thread capture/return points.
- `git diff --check -- Assets/_Project/Scripts/SaveBinaryStorage.cs Assets/_Project/Scripts/SaveManager.cs` exits 0; Git reports only LF-to-CRLF working-copy warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly` failed with unrelated errors including missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `InternalFloodWaterlineRuntime`, `BurstTokenBucketJobAdmissionService`, `BinaryBlittableSafe`, `SoundEmissionSignal`, `TetherFiredSignal`, `AcousticAup`, and inventory algorithm/corrosion namespaces.
- Unity MCP `refresh_unity` timed out waiting for readiness; later `read_console` returned `no_unity_session`; `set_active_instance Hecton8@5898b2fd69afdd2d` returned no connected Unity instances.
