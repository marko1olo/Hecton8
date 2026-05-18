# Rationale_SHINOBU_33

Date: 2026-05-18
Status: VAULT-BACKED SHINOBU PATCHED / RUNTIME VERIFICATION PENDING / GLOBAL BUILD BLOCKED BY UNRELATED DEPENDENCIES

## Decision 01 - Central blackbox shape

Problem: Existing telemetry paths are split between `GlobalTelemetryBus` 1024 event ring and `CrashTelemetryBuffer` 1024/1000 frame export. SHINOBU_33 requires a compact 300-frame blackbox with raw byte payloads and a 1024-byte sealed dump header.

Solution: Extend `GlobalTelemetryBus` with a SHINOBU-owned raw `NativeArray<byte>` blackbox while preserving existing public telemetry API. Use `TelemetryHeaderDTO` and `TelemetryEventDTO` with sequential 16-byte layouts and no `Pack=1`. Use event hashes instead of stack traces.

Rejected Alternatives: Replacing `CrashTelemetryBuffer` wholesale would create regression risk in existing bootstrap/watchdog/fault routes. Writing a new sibling-domain assembly would add asmdef pressure and cross-domain dependency risk.

Scalability potential: Low uses 60 frames; Middle, High, Ultra use 300 frames. High/Ultra can retain richer payload source registrations without increasing baseline low-tier memory.

Hardware Impact: MX350/i3 gains predictable fixed memory and avoids managed log allocations; estimated hot-path event push stays sub-microsecond plus bounded memcpy cost.

## Decision 02 - Dependency blindness

Problem: Prompt requires Origin Shift and Physics evidence, but Agent 33 cannot depend on Agent 30 or concrete physics/AI domains.

Solution: Provide unmanaged `MockOriginShiftSignal` and `MockPhysicsState` payloads and generic unmanaged source registration. External systems can write raw DTOs through `void*`/stride/count contracts.

Rejected Alternatives: Direct references to `HectonFloatingOrigin`, `HectonPlayerMovement`, AI, or vehicle classes would couple Core forensics to active sibling edits.

Scalability potential: Weak devices can record minimal event DTOs; high-tier devices can register additional payload blocks while the dump parser remains layout-stable.

Hardware Impact: Avoids extra class/object graph traversal on i3/MX350; reduces compile-wall risk from sibling assembly churn.

## Decision 03 - Crash I/O policy

Problem: A true crash/NaN path cannot trust async queues or Unity callbacks to survive.

Solution: Normal pressure flush uses a background thread. Catastrophic failure writes synchronously to `Docs/AgentLogs/Dump_CRASH_[Timestamp].h8dump` with a sealed 1024-byte header and raw payload bytes.

Rejected Alternatives: Writing only to `Application.persistentDataPath` hides evidence from the mandated CTO log path. Queueing the dump through WAL or telemetry threads risks losing the last state.

Scalability potential: Low-tier shrinks retained frames to prevent telemetry causing OOM; high-tier can spend memory on wider payload slices.

Hardware Impact: Healthy path avoids disk I/O; fatal path intentionally blocks. Estimated saved hot-path time versus `Debug.Log` spam is unbounded under fault storms, normally tens to hundreds of microseconds.

## Decision 04 - Watchdog counters

Problem: A frozen main thread may never reach `LateFrameUpdate`, so a pure frame-commit dump path cannot prove where an infinite loop or deadlock occurred.

Solution: Add fixed unmanaged 64-lane watchdog counter arrays and a 500 ms background probe thread. Critical systems call `SignalBlackboxWatchdog(int lane)`. If an active lane stops advancing for four probes, the thread sets fatal hash `WDG!`, writes the blackbox from the background path, and kills the player process outside the editor.

Rejected Alternatives: Waiting for Unity exceptions or `Debug.Log` watchdog strings was rejected because a deadlocked main thread cannot be trusted to flush managed logs. Direct AI/pathfinding references were rejected to avoid sibling-domain compile coupling.

Scalability potential: Low, Middle, High, and Ultra use the same 64-int counter layout. Weak devices pay a 500 ms sleeping probe; high-tier devices can register more critical lanes without changing the dump ABI.

Hardware Impact: i3/MX350 steady-state cost is one atomic increment for opted-in lanes and one 64-lane scan every 500 ms; estimated frame impact <1 us for the default lane.

## Decision 05 - Replay hash verification

Problem: The prompt requires a replay determinism warning, but Agent 33 has no legal direct ownership of the full DataVault or deterministic input stream.

Solution: Hash the copied hot payload bytes with `xxHash3.Hash64` in editor/development builds and expose `ArmBlackboxExpectedDeterminismHash(ulong)`. If the armed expected hash mismatches the next committed frame, emit hash-only `DSYN` telemetry and store the last hash in the dump header.

Rejected Alternatives: Direct DataVault assembly coupling or string reports were rejected. Comparing arbitrary consecutive hashes was rejected because normal game state changes would generate false positives.

Scalability potential: Low-tier builds can leave the verifier unarmed; High/Ultra debug builds can arm exact expected hashes from replay/input systems without changing the runtime dump contract.

Hardware Impact: 0 us in release player builds. Editor/development builds pay one XXHash3 over the copied payload slice per committed blackbox frame.

## Decision 06 - Human facade without runtime strings

Problem: A binary blackbox is useless if the lead has to inspect raw hex after every physics failure.

Solution: Add `BlackboxXRayViewer` in the editor assembly. It reads active frame snapshots and recent `TelemetryEventDTO` values through editor-only copy APIs, maps event hashes from `telemetry_hash_dictionary.csv`, monitors `telemetry_flags.csv`, and draws Scene View red X markers from mock origin impact positions.

Rejected Alternatives: Runtime string dictionaries, debug GameObjects, or `OnGUI` were rejected. They either pollute the hot loop, add scene objects, or ignore current editor UI patterns.

Scalability potential: Low-tier runtime pays nothing; editor tooling can allocate managed labels/dictionaries because it is isolated from player hot paths. Ultra debug workflows can keep larger dictionaries without changing runtime memory.

Hardware Impact: 0 us player runtime. Editor refresh interval is 250 ms and reads bounded 300-frame/128-event snapshots.

## Decision 07 - Compile boundary

Problem: The generated `Hecton8.Core.csproj` did not include the new partial file, causing the main telemetry file to miss SHINOBU symbols during CLI verification.

Solution: Add `GlobalTelemetryBus.Blackbox.cs` to `Hecton8.Core.csproj` and `BlackboxXRayViewer.cs` to `Hecton8.Editor.csproj` so the current CLI build surface sees the same source Unity will import. No asmdef references or sibling domain dependencies were added.

Rejected Alternatives: Leaving placeholder methods in `GlobalTelemetryBus.cs` was rejected because it would silently bypass the blackbox. Adding new contracts/asmdef references was rejected because GlobalTelemetryBus already owns the correct Core surface.

Scalability potential: Build metadata only; no runtime behavior change.

Hardware Impact: 0 us runtime. Verification now fails only on unrelated dirty-worktree systems, not SHINOBU files.

## Decision 08 - Polish thread-origin guard

Problem: Public blackbox entry points could cold-initialize the SHINOBU native state from a non-main thread if an external system called source registration, mock state push, or manual dump before `GlobalTelemetryBus.Initialize()`. That would touch Unity `Application` path/version APIs off-thread.

Solution: Guard `TryRegisterBlackboxSource`, `PushMockPhysicsState`, `PushMockOriginShift`, and `TryDumpBlackboxNow` so background calls fail/return until the main thread has initialized the blackbox. Expose `ShinobuBlackboxSourceFlagFloatScan` and `ShinobuBlackboxMainThreadWatchdogLane` to remove magic numbers from external integration.

Rejected Alternatives: Letting background code initialize Unity-owned paths was rejected. Adding direct dependencies to AI/Physics/DataVault owners for lifecycle ordering was rejected because the prompt requires blind decoupling.

Scalability potential: Low/Middle/High/Ultra all keep the same initialization policy. Higher tiers can register more payload sources after main-thread init without changing the ABI.

Hardware Impact: 0 us steady-state on initialized paths; one branch in cold public entry paths prevents a crash-prone off-thread initialization class.

## Decision 09 - Ultra-polish concurrency hardening

Problem: The fatal dump path shared `_blackboxDumpHeader` across main-thread, watchdog, and emergency background writes with no serialization. MMF/watchdog shutdown also used timeout-based joins before native disposal, which could leave a worker thread holding `_blackboxMmfScratch` or watchdog arrays after teardown. Ring readback paths sampled `validFrames`, `activeFrames`, and `writeIndex` independently, allowing inconsistent chronological dumps under ARM memory ordering.

Solution: Add `_blackboxDumpGate`, snapshot ring bounds through one volatile helper, retry fatal dump if the first disk write fails, and make worker shutdown join before disposing native arrays. `BlackboxSourceSlot` was reordered to pointer-first layout: 8-byte pointer at offset 0, 4-byte hash/flags at 8/12, 4-byte payload/pad at 16/20, explicit Size=32. Runtime blackbox capacity is now selected once at initialization instead of disposing/reallocating the ring when the scalability tier changes under live workers.

Rejected Alternatives: Leaving timeout joins was rejected because a 250 ms timeout cannot prove the MMF thread is out of `UnsafeUtility.MemCpy`. Runtime resizing was rejected because it creates use-after-free risk and loses the 300-frame forensic window mid-session. Per-dump local managed header allocation was rejected because fatal paths should not allocate when native header memory already exists.

Scalability potential: Low/MX350 still initializes a 60-frame ring. Middle/High/Ultra initialize 300 frames and may register more payload sources. The tier is not reallocated live; visual/scalability systems can still shed their own payload registrations without moving SHINOBU memory.

Hardware Impact: Measured microsecond proof is absent. Static impact: dump lock is cold/fatal only; added hot-path cost is volatile reads/writes around ring indices, estimated sub-microsecond on i3/MX350. The real gain is eliminating a native use-after-free class that would produce unparseable dumps or hard crashes before evidence is written.

## Decision 10 - Vault-backed blackbox sovereignty and ABI closure

Problem: The ultra-polish audit exposed two remaining architecture defects. First, SHINOBU still used locally allocated persistent `NativeArray` owners for blackbox frames, MMF scratch, event hashes, source slots, atomic failure state, and watchdog counters. That violated the project DataVault sovereignty rule even though no allocations happened in the hot loop. Second, `BlackboxRingBufferDTO` was sequential but not explicit-sized, so its public raw ABI view did not prove a 16-byte multiple for ARM64/L1 cache alignment.

Solution: Added `BufferID` lanes `ShinobuCrashBlackboxBytes` through `ShinobuCrashWatchdogActive` and route all persistent SHINOBU buffers through `GlobalRegistry.DataVault.GetBufferHandle<T>(..., SystemID.CoreDiagnostics, ...)`. The `NativeArray` fields in `GlobalTelemetryBus.Blackbox.cs` are now resolved Vault views, not allocation owners. SHINOBU locks each Vault buffer while MMF/watchdog/background dump code can hold raw pointers, then unlocks on teardown. Existing Vault control buffers are explicitly cleared on bind so stale atomic/watchdog state cannot survive domain reload. `BlackboxRingBufferDTO` is now `[StructLayout(LayoutKind.Sequential, Size = 48)]` with explicit 12-byte tail padding, and `NanSweeperJob` is explicit `Size = 32`.

Rejected Alternatives: Keeping a documented H-Phi exception was rejected because the user mandate explicitly revoked that exception. Replacing the blackbox with a sibling-domain service was rejected because it would add compile-wall pressure and direct dependency risk. Allowing DataVault compaction to move SHINOBU buffers while background threads hold raw pointers was rejected because it would reintroduce native use-after-free. Falling back to private allocations when DataVault is absent was rejected for this pass; the bootstrap already registers DataVault before `GlobalTelemetryBus.Initialize()`, and pre-vault crash evidence remains covered only by the older telemetry path until Core boot reaches memory prewarm.

Scalability potential: Low/MX350 still requests 60 frame slices. Middle, High, and Ultra request 300 frame slices and can register more 64-byte source payloads without changing the dump ABI. Vault ownership lets memory-map tooling see SHINOBU buffers alongside other critical rings instead of treating crash telemetry as a private island.

Hardware Impact: No measured microseconds are claimed. Static impact: DataVault handle binding is cold initialization only; steady-state ring writes still use cached `NativeArray` views and raw pointers. The critical low-end gain is avoiding a separate unmanaged heap island and preventing defrag relocation from corrupting MMF/watchdog crash evidence.

## Decision 11 - L1 cache-line frame ABI and editor CSV streaming

Problem: The blackbox DTOs were explicitly aligned, but the frame slice was still 3744 bytes. That is 58.5 64-byte cache lines, so adjacent frame starts could cut across L1 cache lines. The editor CSV facade also used `File.ReadAllLines`, creating a whole `string[]` per refresh before handing lines to the span parser. Both issues were cold or bounded, but neither was acceptable under the mandate's cache-line and human-bridge scrutiny.

Solution: Changed the SHINOBU frame ABI to 3840 bytes: exactly 60 x 64-byte cache lines. The 16-byte `TelemetryHeaderDTO` still starts at offset 0, then a 48-byte zeroed pad completes cache line 0. Hash history starts at offset 64. Source payload starts at offset 512. Mock physics starts at offset 3712 and mock origin at offset 3776. Both header/hash padding regions are explicitly cleared every frame so reused uninitialized Vault memory cannot leak stale bytes into dumps. Replaced `File.ReadAllLines` in `BlackboxXRayViewer` with `StreamReader.ReadLine` loops. Converted `MockOriginShiftFireJob` from a `NativeArray<T>` field to raw pointer + length, with explicit 32-byte layout.

Rejected Alternatives: Keeping 3744 bytes was rejected because it passes 16-byte ARM alignment but fails the L1 cache-line audit. Padding only the dump file was rejected because the ring itself would remain cache-hostile. `File.ReadLines` was rejected because it still hides iterator state; explicit `StreamReader` makes the editor-only allocation boundary visible. Keeping `NativeArray<T>` inside `MockOriginShiftFireJob` was rejected because its safety-field layout varies with Unity collection checks, blocking a stable byte-layout report.

Scalability potential: Low/MX350 now pays an additional 96 bytes per retained frame, 5760 bytes at 60 frames. Middle/High/Ultra pay 28800 extra bytes at 300 frames. That is below the noise floor compared with the benefit of cache-line-stable frame starts and deterministic offline parsing.

Hardware Impact: No measured microseconds are claimed. Static impact: one frame commit clears 96 additional padding bytes and writes on 64-byte-aligned frame boundaries. The cache-line fix targets ARM64/Steam Deck stability and parser determinism, not a claimed profiler win.
