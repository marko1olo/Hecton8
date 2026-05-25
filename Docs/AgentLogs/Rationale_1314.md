# Rationale_1314 - AUDIO_MASTER_BUS_ALIGNMENT_REPAIRER

Status: STATIC APEX RUNTIME THREAD BARRIER FREE / RAW TELEMETRY PASS WITH TASK08 DATAVAULT HOT-RING LIMITATION / NATIVE DUMP ASYNC QUEUE / COMPILE NOT RUN BY USER INSTRUCTION

## R0 - Phase 0 Boundary

Problem: Native audio master bus registration is reported blocked by an unaligned `WriteIndex` pointer derived from `sharedStatePtr + 1`, placing an `int*` cursor at base + 4 instead of an 8-byte boundary.

Solution: Restrict first pass to direct source archaeology and pointer layout proof in `NativeAudioFrameRingBuffer.cs` and `HectonSensoryKernelNativeBridge.cs`; use the audio SPSC, ARM64 layout, zero-GC, native memory, registry DI, and blackbox mandates as acceptance law.

Rejected Alternatives: Broad audio architecture refactor rejected because the prompt names a specific pointer arithmetic defect. GlobalRegistry polling rejected by doctrine; any re-registration path must cache cold dependencies or listen to explicit hotswap/reinit signals.

Scalability potential: Low/MX350 uses the same descriptor truth and lock-free writer with minimal telemetry cadence. Middle keeps full 300-frame audio bridge telemetry. High adds heavier editor stress validation. Ultra can increase procedural synthesis richness only after the bridge registers and hot path remains zero-GC.

Hardware Impact: Expected gain on i3/MX350 is not CPU micro-optimization yet; primary gain is restoring native DSP registration. Avoiding managed fallback/audio-source patch paths prevents unpredictable GC and thread stalls.

Proof State: STATIC_SOURCE_PENDING. No compile or Unity runtime proof yet.

## R1 - Phase 0 Source Archaeology

Problem: `NativeAudioKernelRingBufferDescriptor.WriteIndexSlot` is `1`, and `TryCreateNativeDescriptor` directly uses `sharedStatePtr + WriteIndexSlot`. For an `int*`, slot 1 means a 4-byte offset, so `WriteIndex` fails the 8-byte pointer alignment gate.

Solution: Record the live-source ledger in `Docs/Reports/AUDIO_BRIDGE_ARCHAEOLOGY_1314.json`; correct path is even-slot shared-state layout: read cursor at slot 0, write cursor at slot 2, metadata on even slots, shared-state length expanded to keep every exported cursor address naturally aligned.

Rejected Alternatives: Leaving slots dense and weakening `IsDescriptorValid` is rejected because it would push a known unaligned pointer into native code. Allocating a separate managed cursor object is rejected because it violates DataVault/native ownership and hot path zero-GC. Replacing the whole audio renderer is rejected as scope creep.

Scalability potential: Low/MX350 pays only a few padded ints and pointer checks. Middle/High/Ultra keep the same truth layout; higher tiers can spend audio budget on richer DSP after native registration is stable.

Hardware Impact: Shared-state padding cost is 24 extra bytes if slots move from 6 dense ints to 12 padded ints. CPU impact is below measurement noise; the real gain is avoiding native registration rejection and managed fallback pressure.

Proof State: STATIC_SOURCE_CONFIRMED_ALIGNMENT_FAULT. Runtime proof still absent.

## R2 - Registry And Re-registration Gap

Problem: `PlayerCriticalProceduralAudioRenderer.RefreshNativeOutputBridge()` contains the bridge registration call but static search found no call site. Audio device reset currently calls `RefreshAudioConfiguration()` only, which rebuilds buffers and clears the bridge but does not register the descriptor again.

Solution: Implement the repair so `HectonSensoryKernelNativeBridge` owns descriptor validity/status discipline, then add an explicit registration gate at the existing renderer call site only if strict source scope permits. If kept inside the two-file boundary, final status must mark re-registration as blocked by unreachable caller.

Rejected Alternatives: Per-frame bridge polling is rejected. Registry polling from audio producer thread is rejected. Silent failure is rejected; failures must write telemetry and fail closed to silence.

Scalability potential: Re-registration cadence must be event-driven on all tiers. Low tier logs compact telemetry; High/Ultra may keep fuller bridge history but cannot change descriptor truth.

Hardware Impact: Event-driven registration avoids recurring CPU cost. Calling native registration only on buffer/context replacement should be effectively 0 us per frame.

Proof State: STATIC_SOURCE_CALLSITE_GAP. Compile/runtime proof absent.

## R3 - Tasks 04-05 Pointer Correction And Descriptor Hardening

Problem: Dense shared-state slots exported `WriteIndex` at int slot 1. On an 8-byte-aligned `int*` base this creates `base + 4`, which fails the bridge's own `RequiredAlignmentBytes = 8` pointer validation.

Solution: Move every exported shared-state field to even int slots: `ReadIndexSlot = 0`, `WriteIndexSlot = 2`, metadata at 4/6/8/10, `SourceChannelsSlot = 12`, and `SharedStateSlotCount = 14`. `TryCreateNativeDescriptor` now materializes `readIndexPtr` and `writeIndexPtr` from named constants, while `IsDescriptorValid` also verifies that cursor pointers are inside `SharedState` at the exact expected byte offsets.

Rejected Alternatives: Weakening alignment validation rejected because it hides the native crash vector. Separate managed cursor boxes rejected because they break DataVault ownership and add GC risk. A dedicated native allocation per cursor rejected as unnecessary while the existing DataVault lane can be padded by 32 bytes.

Scalability potential: Low tier pays 32 bytes of padded state and two cold validation checks. Middle, High, and Ultra use the same ABI; higher tiers spend recovered stability on richer DSP, not a different truth layout.

Hardware Impact: Runtime hot-path cost is 0 us/frame after descriptor creation. Cold registration validation adds a few integer comparisons. i3/MX350 impact is below measurable frame cost; primary gain is avoiding native bridge rejection.

Proof State: STATIC_SOURCE_PASS. Runtime native plugin registration proof pending compile/editor run.

## R4 - Task 06 Lock-Free SPSC Writer

Problem: The writer must not allocate or lock inside the audio producer path, and it must not rely on managed array bridges.

Solution: `TryWriteInterleaved` now resolves source/frame native pointers with `NativeArrayUnsafeUtility`, writes mono/stereo paths directly by pointer, clamps non-finite samples to silence, and publishes the write cursor through the existing `Volatile.Write` route.

Rejected Alternatives: `float[]` mixing rejected as managed allocation/GC risk. `lock`/`Monitor` rejected because the audio producer cannot block on the consumer. Per-sample channel loops rejected for stereo because the shipped layout can use fixed two-lane stores.

Scalability potential: Low tier gets the cheapest deterministic writer. Middle/High/Ultra can increase procedural synthesis density while preserving the same SPSC publish rule.

Hardware Impact: Expected saving is avoidance of managed callback/fallback pressure; no profiler-backed microsecond claim. The direct pointer stereo path removes bounds-check noise from the inner block write.

Proof State: STATIC_SOURCE_PASS. Live DSP allocation profiling pending Unity Editor/runtime.

## R5 - Task 07 Re-registration Gate

Problem: `RefreshNativeOutputBridge()` had the registration logic but audio/DataVault refresh paths did not reliably invoke it. Native bridge failures were also not recorded into the audio black box.

Solution: Add `TryRegisterWithRetryGate` to validate first, retry bounded registration, and fail closed with `TryClear`. Route DataVault/audio configuration refreshes through `RefreshNativeOutputBridge()` and record registration failures via `_sampleRingBuffer.RecordBridgeFailure(bridgeStatus)`.

Rejected Alternatives: Per-frame registry polling rejected by GlobalRegistry doctrine. Same-frame busy retry loops rejected as hardware-state thrash. Throwing on plugin failure rejected because the correct fail-closed audio state is silence plus telemetry.

Scalability potential: All tiers use event-driven re-registration. Low tier pays zero recurring frame cost. High/Ultra can retry no more often than context replacement; audio richness remains decoupled from bridge identity.

Hardware Impact: 0 us/frame steady-state. Cold retry is two native calls maximum and only on context/buffer replacement.

Proof State: STATIC_SOURCE_PASS. Compile proof blocked by external build gate.

## R6 - Task 08 Telemetry And Black-Box Dump

Problem: The bridge had overflow warning counters but no fixed 300-frame high-level state record for alignment faults, non-finite samples, bridge failures, cursor state, or DSP timing.

Solution: Add `BufferID.AudioFrameRingTelemetry`, an explicit 64-byte `AudioBridgeTelemetryEntry`, a 300-entry DataVault ring, hash/status fields, DSP tick recording, bridge failure recording, and a background dump path to `Docs/AgentLogs/Dump_1314_AudioBridge.bin` when non-finite samples or bridge failure are observed.

Rejected Alternatives: Managed `List<>`/JSON telemetry rejected for hot path GC. Per-frame file writes rejected as I/O stalls. GlobalDataVault diagnostic fallback polling rejected; the ring owns its telemetry handle from initialization.

Scalability potential: Low tier writes compact fixed telemetry only. Middle/High/Ultra retain the same 300-frame black box; optional higher-detail forensic work must be cold/editor only.

Hardware Impact: Telemetry write is one fixed native struct store per producer event. Dump allocation and file I/O occur only after fault detection, not during normal DSP.

Proof State: STATIC_SOURCE_PASS. Dump file not generated because no fault was injected in Unity runtime.

## R7 - Tasks 09-10 Fuzzer And Metric Scanner

Problem: Static pointer repair is not enough; the bridge needs a repeatable editor stress route and a source scanner that can reject regression to `base + 4`.

Solution: Add editor-only `AudioBridgeConcurrencyFuzzer1314` with Burst sample generation and producer/consumer threads using the descriptor's read/write pointers. Add `OOP_AudioBridge_Scanner` to verify slot math, pointer writer route, telemetry lane, re-registration gate, fuzzer source, and JSON report output to `Docs/Reports/AUDIO_BRIDGE_OPTIMIZATION_REPORT_1314.json`.

Rejected Alternatives: A fake report without a callable fuzzer rejected. Runtime fuzzer rejected because validation load belongs in editor. Scanner-only proof rejected because it cannot exercise SPSC producer/consumer pointer behavior.

Scalability potential: Low tier never runs the fuzzer in shipped runtime. Middle/High/Ultra editor validation can raise iteration counts without changing runtime code.

Hardware Impact: 0 us/frame in player builds due `UNITY_EDITOR` guard. Editor fuzzer default pushes 2,097,152 frames / 4,194,304 stereo samples in 65,536-frame blocks; larger runs are available by parameter.

Proof State: STATIC_SOURCE_PASS. Live fuzzer pending Unity Editor DataVault context.

## R8 - Verification Wall

Problem: Full `dotnet build` is currently forbidden by project build-gate rules: CPU samples observed 94.58%, 29.86%, then 84.00%, and seven `dotnet` processes stayed present. Earlier scoped build also hit unrelated missing `FixedUiEventQueue<>` symbols in UI/PDA/Spectrum files before audio proof could be isolated.

Solution: Do not start another compiler. Use `rg`, `git diff --check`, exact source checks, prompt re-extraction, and report JSON as current proof. Mark compile/live fuzzer as pending instead of fabricating green status.

Rejected Alternatives: Launching another `dotnet build` rejected by explicit CPU/compiler rule. Fixing `FixedUiEventQueue<>` rejected as outside agent 1314 domain. Claiming native plugin registration without Unity runtime rejected as fake proof.

Scalability potential: Verification discipline is tier-independent; runtime bridge changes remain valid but final acceptance still needs a clean compile and Unity Editor fuzzer run when the build lane is free.

Hardware Impact: No runtime impact. Avoiding another compiler on saturated CPU prevents degrading parallel agents and keeps the shared workstation stable.

Proof State: COMPILE_BLOCKED_EXTERNAL. Static source proof complete for agent scope.

## R9 - APEX Rescan Dump Repair

Problem: The previous Task 08 implementation still had a managed fault-dump route in `NativeAudioFrameRingBuffer.cs`: managed `byte[]`, `new Thread`, `Path`, `Directory`, and `FileStream`. That violates the stricter release Zero-GC audit even though it was fault-triggered instead of steady-state.

Solution: Replace the release runtime dump writer with fixed unmanaged dump bytes and unmanaged `UnsafeUtility.MemCpy` copies of the 16-byte header plus 300 x 64-byte telemetry entries. The initial DataVault dump-byte lane was superseded by R18 raw `H8Memory` ownership. Remove runtime `System.IO`, managed snapshot allocation, background thread creation, and file path construction. Add scanner checks that reject managed runtime dump regression.

Rejected Alternatives: Keeping background managed I/O rejected because the user requested release-grade zero managed allocation and no hidden managed fault path. Throwing on invalid capacity rejected; invalid state now returns/disposes. Direct native plugin file I/O rejected because no existing project-owned unmanaged crash exporter was present in the two-file domain. The safe route is native byte snapshot ownership now, with any disk export left to an existing cold crash/export owner.

Scalability potential: Low and MX350 pay one 19,216-byte unmanaged dump buffer and no fault thread. Middle keeps the same 300-frame black box. High and Ultra can add editor/development dump exporters without changing runtime DTO truth or hot audio behavior.

Hardware Impact: Steady-state frame cost remains 0 us for dump I/O because no thread or file path exists in runtime. Fault path copies 19,216 bytes into existing native memory. On low-end silicon this avoids GC and thread scheduler spikes; on high-end it preserves deterministic forensic bytes for richer tooling.

Proof State: STATIC_APEX_SOURCE_PASS. Compile blocked by latest CPU sample 93.06% plus seven existing `dotnet` processes; Unity live fuzzer still blocked by editor DataVault context.

## R10 - Full Runtime File Scrub

Problem: Even with `#if UNITY_EDITOR`, keeping the thread-heavy fuzzer in `NativeAudioFrameRingBuffer.cs` made full-file text audit noisy and easy to misread as release code. `PlayerCriticalProceduralAudioRenderer.cs` diff also contained a value-type `new float3`, which is not GC but violates the user's literal text audit bar.

Solution: Move `AudioBridgeConcurrencyFuzzer1314` and `AudioBridgeConcurrencyFuzzerResult` into `OOP_AudioBridge_Scanner.cs`, which is editor-only from line 1. Replace the renderer diff's `new float3(...)` with `math.float3(...)`. Re-run full-file scan on `NativeAudioFrameRingBuffer.cs` and `HectonSensoryKernelNativeBridge.cs`; result is zero hits without editor-region stripping.

Rejected Alternatives: Leaving fuzzer under `#if UNITY_EDITOR` in the runtime source rejected because it weakens audit clarity. Treating value-type `new float3` as harmless rejected because the requested gate is textual and uncompromising.

Scalability potential: Player builds now carry no fuzzer thread/allocation text in the ring source. Editor validation remains available without changing runtime DTO layout or bridge behavior.

Hardware Impact: Runtime impact is 0 us/frame. Editor fuzzer cost is unchanged and isolated to explicit menu execution.

Proof State: FULL_RUNTIME_TEXT_SCAN_PASS. No compile attempted by user instruction.

## R11 - Native Plugin ABI Rejection Fix

Problem: The previous C# repair was still not release-valid. `NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp` kept `kWriteIndexSlot = 1`, metadata slots `2/3/4/5`, `kSharedStateSlotCount = 6`, and pointer alignment checks based on `sizeof(SInt32)`. That means the repaired C# descriptor exported `WriteIndex = base + 8`, but native validation still expected `base + 4` and would reject registration.

Solution: Repair the native plugin slot map to the same padded contract as C#: `Read=0`, `Write=2`, `CapacityFrames=4`, `CapacityMask=6`, `GuardA=8`, `GuardB=10`, `SourceChannels=12`, `SharedStateSlotCount=14`. Add `kRequiredPointerAlignmentBytes = 8u` and validate frames/sharedState/readIndex/writeIndex with that constant. Extend `OOP_AudioBridge_Scanner` so stale native constants cannot pass static validation again.

Rejected Alternatives: Rejected claiming native validation pass from managed-side descriptor checks only. Rejected leaving native plugin at 4-byte pointer alignment because it would accept the old `base + 4` defect. Rejected changing the descriptor field order because the native ABI has an established magic-at-offset-0 layout documented in prior Batch008 proof; pointer fields are already 8-byte aligned through explicit padding.

Scalability potential: Low/MX350 and higher tiers now share one ABI truth. The fix costs no per-frame CPU; it only changes cold registration validation and native read indices. Higher tiers can spend saved stability on richer DSP rather than divergent descriptor layouts.

Hardware Impact: 0 us/frame steady-state. Native registration now avoids deterministic rejection caused by slot mismatch. Shared-state padding remains 32 extra bytes versus the original dense six-int map.

Proof State: NATIVE_ABI_STATIC_PASS. Compile/native plugin rebuild and Unity fuzzer execution still not run by current user instruction.

## R12 - Native Stereo Consumption Repair

Problem: The managed ring initializes with `BinauralOutputChannels = 2` and writes interleaved stereo frames, but the native audio callback consumed `ringBuffer.frames[(readIndex + frameIndex) & mask]` as mono. Alignment could pass while every right-channel sample was ignored and the left/right stream was time-distorted.

Solution: Publish `_sourceChannels` into `SourceChannelsSlot = 12` during shared metadata write. Native validation now rejects source channel counts outside `[1,2]`. Native processing reads `sourceChannels` and, for stereo, consumes `sourceFrameIndex << 1`, sends left/right into the first two output channels, and downmixes only when Unity requests mono or more than two channels.

Rejected Alternatives: Rejected expanding the descriptor struct to 64 bytes because the existing native ABI is 56 bytes and the source-channel value is metadata, not a pointer. Rejected silently downmixing in C# because it would waste the authored binaural scratch and hide lost samples.

Scalability potential: Low tier pays one extra shared-state int slot and one branch in the native callback. Middle/High/Ultra keep the same route while preserving stereo presentation.

Hardware Impact: Per-frame native callback cost is a single metadata read plus branch; no managed GC and no additional allocations. Memory cost is +8 bytes of shared-state padding versus the already padded 12-slot repair.

Proof State: STATIC_NATIVE_STEREO_PASS. Runtime audio-device verification pending Unity/native plugin rebuild.

## R13 - Native Binary Dump Route

Problem: A DataVault byte snapshot is necessary for Zero-GC forensic state, but it does not satisfy the literal requirement to write `Dump_1314_AudioBridge.bin`. The earlier managed `FileStream`/`Thread` route violated Zero-GC; the replacement DataVault-only route avoided GC but left disk export unimplemented.

Solution: Add a native plugin export `HectonSensoryKernel_DumpAudioBridgeTelemetry(const void* bytes, int byteCount)` that writes `Docs/AgentLogs/Dump_1314_AudioBridge.bin` with C/C++ file I/O. C# fault path still builds the 19,216-byte snapshot in pre-owned DataVault memory and only passes the unmanaged pointer to the native writer.

Rejected Alternatives: Rejected managed `FileStream`, `Path`, `Directory`, `byte[]`, and background `Thread` in the runtime ring. Rejected descriptor expansion for dump pointers because the dump is a fault export, not native audio callback state.

Scalability potential: Low tier pays nothing during normal frames. Middle/High/Ultra can keep the same dump bytes and add richer offline tooling without changing hot audio routes.

Hardware Impact: 0 us/frame steady-state. Fault path performs one 19,216-byte native write. If the native plugin is unavailable or the path cannot be opened, the DataVault snapshot still exists and the managed path stays fail-closed.

Proof State: STATIC_NATIVE_DUMP_PASS. Native plugin rebuild and real file creation still pending external build/editor route.

## R14 - Native Descriptor Packing Guard [SUPERSEDED BY R26 FIELD ORDER]

Problem: The native plugin mirrored the C# descriptor field order but relied on compiler default packing. The report claimed a 56-byte ABI, yet native source had no compile-time guard for size or offsets. That is a release ABI risk: a packing pragma, compiler mode, or accidental field edit could silently desynchronize native validation from C#.

Solution: Add native compile-time assertions after `SharedRingBufferDescriptor`: `sizeof(void*) == 8`, `sizeof(SharedRingBufferDescriptor) == 56`, and `offsetof` checks for `descriptorMagic=0`, `frames=8`, `sharedState=16`, `readIndex=24`, `writeIndex=32`, `capacityFrames=40`, `capacityMask=44`, `sharedStateLengthInts=48`. Update scanner and JSON proof so this guard is mandatory.

Rejected Alternatives: Rejected `[StructLayout(Pack=1)]` or C++ packed structs because they would damage natural ARM64 pointer alignment. Rejected relying on prose byte maps. Rejected expanding the C# descriptor to 64 bytes because the existing native ABI already aligns pointer fields at 8-byte boundaries and only needs proof.

Scalability potential: All tiers use identical ABI. Low/MX350 pays zero runtime cost; High/Ultra get the same deterministic native bridge while spending budget on DSP richness instead of defensive runtime checks.

Hardware Impact: 0 us/frame. Compile-time static assertions do not execute at runtime. If a future build target violates 64-bit ABI or offsets, compilation fails instead of shipping a broken bridge.

Proof State: SUPERSEDED_BY_R26_POINTER_FIRST_DESCRIPTOR. Native plugin rebuild still not run by user instruction.

## R15 - Editor Scanner Assembly Isolation

Problem: `OOP_AudioBridge_Scanner.cs` was placed under `Assets/_Project/Scripts/Audio/Editor` without a nested editor asmdef. The scanner references `internal` runtime bridge types. Depending on Unity asmdef/special-folder resolution, this risks either player assembly pollution through `UnityEditor` references or editor assembly access errors against internal audio types.

Solution: Move the scanner/fuzzer to `Assets/_Project/Scripts/Editor/Audio/OOP_AudioBridge_Scanner.cs`, which is under existing `Hecton8.Editor.asmdef`. That editor assembly already references `Hecton8.Core`, and `Assets/_Project/Scripts/AssemblyInfo.cs` already grants `InternalsVisibleTo("Hecton8.Editor")`.

Rejected Alternatives: Rejected making `AudioFrameSpscRingBuffer`, `NativeAudioKernelRingBufferDescriptor`, or `NativeAudioKernelBridgeStatus` public because this widens the runtime API surface. Rejected adding a new asmdef for one scanner because it would require another friend assembly and more assembly graph surface.

Scalability potential: Player builds carry no scanner/fuzzer. Editor can run high-volume fuzzer without changing runtime ABI or public API.

Hardware Impact: 0 us/frame. Editor assembly relocation has no player runtime cost.

Proof State: STATIC_EDITOR_ASSEMBLY_ROUTE_FIXED. Unity compile still not run by user instruction.

## R16 - Native Portable Atomic Guard

Problem: The native source was still Windows-shaped. `AudioPluginUtil.h` includes `windows.h` only under `PLATFORM_WIN`, but `Plugin_HectonSensoryKernel.cpp` declared `static volatile LONG` globals and called `InterlockedIncrement`, `InterlockedDecrement`, `InterlockedExchange`, and `InterlockedCompareExchange` in callback/export code that is not guarded to Windows. That is a compile hazard for Linux/macOS and any future Android ARM64 plugin build.

Solution: Add a local 32-bit atomic abstraction: `HectonAtomicInt32`, `AtomicRead32`, `AtomicWrite32`, `AtomicIncrement32`, and `AtomicDecrement32`. Windows keeps the native `Interlocked*` route inside the helper. Non-Windows uses GCC/Clang `__sync_val_compare_and_swap`, `__sync_lock_test_and_set`, `__sync_add_and_fetch`, and `__sync_sub_and_fetch`. Shared-state cursor reads/writes and global callback/register state now route through the helper.

Rejected Alternatives: Rejected mutexes because the audio callback cannot block. Rejected `std::atomic` because this plugin currently follows Unity sample-style C/C++ utility headers and a local helper is smaller ABI surface. Rejected leaving the C# Linux/macOS P/Invoke route backed by Windows-only native source. Rejected claiming Quest readiness because no Android `.so` or Android native build script exists in the repository.

Scalability potential: Low/MX350 and desktop builds keep the same callback cost class. Middle/High/Ultra do not get a different ABI. Future Android ARM64 native builds have a source path that no longer fails on undefined Windows atomics, but binary/importer proof is still absent.

Hardware Impact: 0 us/frame claimed without native profiler. The helper replaces direct atomic operations with equivalent platform atomics; no managed allocation, no lock, no heap object.

Proof State: STATIC_NATIVE_PORTABLE_ATOMIC_REPAIR_DONE. Native rebuild not run by user instruction; Android/Quest binary route still unproven.

## R17 - DataVault Native Pointer Lifetime Guard

Problem: The arithmetic repair made `WriteIndex` 8-byte aligned, but `TryCreateNativeDescriptor` still exported raw pointers into DataVault-owned `Frames` and `SharedState`. Runtime telemetry and dump bytes also write through DataVault NativeArray views. `TryResolveHandle` only proves validity at the instant of resolve. Native plugin registration retains frame/shared-state pointers after the managed call returns, and a later DataVault relocation/growth can make the callback or dump path dereference stale memory.

Solution: The first repair attempt used owner-tagged `TryLockBuffer` relocation pins. Follow-up audit rejected that route because the native bridge would hold active DataVault lock bits for the whole bridge lifetime, which can defer arena growth/relocation.

Rejected Alternatives: Rejected registering unpinned DataVault pointers because it leaves a stale-pointer crash window. Rejected keeping the permanent `TryLockBuffer` pins because they protect the bridge by imposing a broad DataVault relocation cost. Rejected per-callback managed locking because Unity native audio callback cannot call DataVault and must not block.

Scalability potential: The rejected lock route would have protected low-tier devices from stale pointers but at the cost of broad DataVault relocation pressure. Middle/High/Ultra would inherit the same unnecessary memory-system coupling.

Hardware Impact: The rejected lock route had 0 callback cost but a system-level memory-growth cost while active. It is recorded as a failed intermediate design, not the final architecture.

Proof State: INTERMEDIATE_ROUTE_REJECTED. Replaced by R18.

## R18 - H8Memory Raw Bridge Pool

Problem: Native plugin retains bridge pointers after registration. DataVault memory can relocate; long-lived DataVault locks protect pointers but can block arena growth. Dump scratch no longer belongs in DataVault after native export moved to fixed bytes.

Solution: Move native-exported `Frames`, `SharedState`, and dump scratch to stable owner-tagged `H8Memory.AllocateRaw` buffers with 8-byte alignment. Keep only `BufferID.AudioFrameRingTelemetry` in GlobalDataVault. Create transient `NativeArray` views over raw pointers for writer logic. Clear native plugin state before freeing raw buffers. Remove obsolete `AudioFrameRingTelemetryDumpBytes`.

Rejected Alternatives: Rejected unpinned DataVault NativeArray pointers because native callback can outlive the resolve moment. Rejected permanent DataVault locks because they can hold active lock bits across the whole bridge lifetime. Rejected managed arrays or managed dump I/O because the release fault path must stay Zero-GC. Rejected changing the native descriptor size because existing 56-byte ABI is already guarded by native static asserts.

Scalability potential: Low uses the same tiny raw bridge pool and drops telemetry if the short DataVault write lock is unavailable. Middle keeps 300 telemetry entries. High and Ultra can increase procedural audio richness without changing bridge pointer ownership or DTO layout.

Hardware Impact: Steady-state native callback cost remains 0 managed operations. Cold init allocates frames, 56 bytes of shared state, and 19,216 dump bytes from `H8Memory`; disposal frees them after native clear. The removed long-lived DataVault pins avoid blocking unrelated arena growth on i3/MX350 and stronger devices.

Proof State: STATIC_H8MEMORY_RAW_BRIDGE_POOL_ADDED. Compile/native rebuild/fuzzer not run by user instruction.

## R19 - Telemetry Writer-Fence Scrub [SUPERSEDED BY R21]

Problem: `RecordTelemetry` still entered `GlobalDataVault.TryAcquireWriteLock` from the audio producer path. That is not managed allocation, but it is a DataVault writer fence and contradicts the lock-free bar for the runtime audio bridge.

Solution: This intermediate pass tried to replace telemetry write-lock acquisition with a transient DataVault view. R21 rejects that route because a transient view is not a relocation guard.

Rejected Alternatives: Rejected retaining the short writer fence because it still touches DataVault mutation gates. Rejected long-lived telemetry locks because they recreate the relocation-pin problem. Rejected moving telemetry wholly out of GlobalDataVault because task 08 requires the 300-entry telemetry ring in GlobalDataVault.

Scalability potential: Low tier gets the cheapest producer path and may drop forensic telemetry during compaction instead of blocking. Middle keeps normal 300-entry telemetry. High/Ultra can add cold/editor telemetry export without changing the writer path.

Hardware Impact: Removes two DataVault writer-fence calls per telemetry event (`TryAcquireWriteLock` and `ReleaseWriteLock`). No profiler-backed microseconds claimed; this is a contention-risk removal, not a measured optimization.

Proof State: SUPERSEDED_BY_R21_RELOCATION_SAFE_TELEMETRY. Compile/native rebuild/fuzzer not run by user instruction.

## R20 - Final Static Rescan And Report Hygiene

Problem: After the telemetry writer-fence scrub, the code was current but several report references still pointed at older intermediate line numbers. That is a proof artifact defect: stale line refs make the audit harder to reproduce even if the runtime code is correct.

Solution: Re-scan current sources, correct the final JSON/status line references, and expand the forbidden runtime token scan to include `Monitor`, `StringBuilder`, string concatenation tokens, and boxing token patterns. Keep compile/native rebuild/fuzzer marked not run instead of inventing proof.

Rejected Alternatives: Rejected running `dotnet build` because the user explicitly ordered rare build usage and no compile was required to correct the report hygiene. Rejected editing historical log sections because they document previous intermediate states; the bottom log section now carries current proof.

Scalability potential: No runtime behavior changed. Low, Middle, High, and Ultra tiers keep the same raw bridge pool, padded ABI, and lock-free producer path.

Hardware Impact: 0 us/frame. This pass changed reports/status only and re-ran text scans.

Proof State: STATIC_FINAL_RESCAN_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R21 - Relocation-Safe DataVault Telemetry

Problem: Loop 11 removed the telemetry writer fence and used `GlobalDataVault.TryResolveHandle` for mutation. Source audit of `GlobalDataVault.cs` shows that `TryResolveHandle` only validates and returns a transient view; it does not mark the block locked. Live compaction and arena growth skip blocks with writer locks/pinned views, so mutating `BufferID.AudioFrameRingTelemetry` through a transient view is not a strict relocation-safety proof.

Solution: Restore a short compaction-aware telemetry write lock only around the DataVault telemetry mutation and fault snapshot read. `RecordTelemetry` and `RequestTelemetryDump` now call `TryAcquireWriteLock(in _telemetryHandle, VaultOwner, out telemetry)` and release in `finally`. If the telemetry lock cannot be acquired during dump generation, `_telemetryDumpQueued` is reset so the next fault can retry instead of permanently suppressing dump output. If a normal telemetry write cannot acquire the lock, the telemetry event is dropped; the SPSC sample write already completed through unmanaged raw bridge memory and `Volatile.Write`.

Rejected Alternatives: Rejected transient `TryResolveHandle` mutation because it can race relocation. Rejected long-lived DataVault pins because they block arena growth for the bridge lifetime. Rejected moving the mandated 300-entry telemetry ring out of GlobalDataVault because Task 08 explicitly requires it there. Rejected managed queues/files/threads because release runtime must remain zero-GC.

Scalability potential: Low tier may skip telemetry during DataVault contention rather than corrupting memory or stalling audio. Middle keeps normal 300-entry telemetry. High and Ultra keep the same DTO truth and can add richer cold/editor telemetry exporters without changing the audio bridge ABI.

Hardware Impact: SPSC sample publication remains unmanaged and lock-free. Telemetry now pays a short DataVault writer-lock acquire/release per recorded telemetry event, with no managed allocation and fail-closed skip on contention. This buys relocation safety on low-end silicon and prevents undefined native view writes during defrag/grow.

Proof State: STATIC_RELOCATION_SAFE_TELEMETRY_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R22 - Native Bounded Drain And Shutdown-Safe Raw Free

Problem: Native `WaitForProcessCallbacksToDrain()` used an unbounded loop while `g_processCallbackDepth` stayed non-zero. On non-Windows this was an empty spin loop. A stuck callback depth during register/clear could hang the caller forever instead of failing closed. A second teardown edge existed in C#: if `H8Memory.Shutdown()` had already freed tracked raw allocations, late audio `Dispose()` would call `H8Memory.FreeRaw` against an uninitialized tracker and raise a managed fatal memory exception.

Solution: Add `kDrainSpinLimit = 1000000`; make `WaitForProcessCallbacksToDrain()` return `bool`; make native register/clear leave `Busy` status and return when callback depth cannot drain. Add `H8Memory.IsInitialized` and gate `ReleaseNativeBridgeBuffers()` so late dispose after H8Memory shutdown nulls already-reaped raw pointers after native clear instead of calling `FreeRaw`.

Rejected Alternatives: Rejected infinite native spin because it is not fail-closed. Rejected sleeping/yielding indefinitely because it still stalls shutdown/re-registration. Rejected broad H8Memory shutdown refactor because this agent only needs a read-only tracker-liveness probe for audio bridge teardown.

Scalability potential: Low tier avoids permanent hang during audio-device churn or editor shutdown. Middle, High, and Ultra keep the same ABI and callback path; the bounded drain only executes on cold register/clear.

Hardware Impact: 0 us/frame steady-state. Cold register/clear now has a bounded spin cap and deterministic `Busy` fail-closed exit. Late shutdown no longer creates a managed fatal exception from `FreeRaw` after H8Memory already released tracked records.

Proof State: STATIC_NATIVE_BOUNDED_DRAIN_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R23 - Busy Clear No-Free Gate And Native Heap Scrub

Problem: The bounded native drain fix left a C# lifetime hole. `AudioFrameSpscRingBuffer.Dispose()` called native clear and ignored the result. If native clear returned `Busy`, a currently running callback may already have copied `g_sharedRingBuffer`; C# could then free `_framesPtr` and `_sharedStatePtr`, creating a use-after-free window. The native plugin also still had cold heap tokens: `new EffectData`/`delete` in effect create/release and `malloc/free` in `HectonSensoryKernel_DebugProcessBlock`.

Solution: Make `Dispose()` call `TryClear(out clearStatus)` and return without freeing raw buffers when `clearStatus` contains `Busy`. Make `Initialize()` return immediately after `Dispose()` if `HasNativeBridgeBuffers()` is still true, so no second raw bridge allocation overwrites retained pointers. Remove heap effectdata allocation because effectdata is unused. Replace debug-process heap scratch with fixed static `g_debugProcessScratch[4096*8]` and serialize it with `g_debugProcessScratchInUse`.

Rejected Alternatives: Rejected freeing raw buffers after Busy because native may still read a descriptor copy. Rejected spinning longer in managed dispose because shutdown/reinit must fail closed, not hang. Rejected keeping debug `malloc/free` as "debug-only" because the export is compiled into the native plugin source and weakens the APEX no-heap scan. Rejected per-call static allocation growth because fixed upper bounds already exist: 4096 frames and 8 channels.

Scalability potential: Low tier avoids UAF during audio-device churn and avoids heap fragmentation from debug processing. Middle, High, and Ultra keep the same callback ABI; the only cost is a cold dispose branch and a static 128 KiB debug scratch buffer.

Hardware Impact: 0 us/frame steady-state. Busy dispose now retains memory until a later successful clear instead of risking native UAF. Native debug export no longer allocates/free heap memory per invocation.

Proof State: STATIC_BUSY_CLEAR_NO_FREE_AND_NATIVE_NO_HEAP_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R24 - TryClear Busy Semantics And Failed-Clear Raw Retention

Problem: R23 made `Dispose()` respect the boolean returned by `TryClear(out status)`, but `TryClear` itself still returned success when native status had `Active` cleared and `Busy` still set. That collapses the fail-closed native bounded-drain result into a false managed success and reopens the raw-buffer free window.

Solution: Make `TryClear` return success only when native status has neither `Active` nor `Busy`. Keep the managed lifetime rule stricter than Busy-only: `Dispose()` retains raw bridge buffers on any failed clear except `PluginUnavailable`, and `Initialize()` refuses second allocation while those pointers remain. Update scanner/report proof so this exact semantic cannot regress silently.

Rejected Alternatives: Rejected checking only `Busy` in `Dispose()` because `TryClear` is the public bridge contract and must not lie. Rejected freeing on other failed native statuses because an unexpected native status is not proof that retained descriptor pointers are gone. Rejected another native spin loop because bounded fail-closed behavior is already the correct native result.

Scalability potential: Low tier avoids UAF during audio-device churn or shutdown without adding per-frame work. Middle, High, and Ultra use the same raw-pool lifetime law; richer DSP remains gated by the same descriptor truth.

Hardware Impact: 0 us/frame steady-state. Cold dispose/reinitialize now pays one extra status-bit check and may retain raw memory until a later successful clear instead of risking callback use-after-free.

Proof State: STATIC_CLEAR_BUSY_REJECT_AND_FAILED_CLEAR_NO_FREE_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R25 - H8Memory Shutdown Stale View Guard

Problem: R24 retained raw bridge pointers on failed native clear, but the late-shutdown path was still under-specified. If `H8Memory.Shutdown()` had already reaped tracked raw allocations, `_framesPtr`, `_sharedStatePtr`, and `_telemetryDumpBytesPtr` could remain non-null. `TryResolveRingViews()` would then create `NativeArray` aliases over stale addresses, and failed-clear retention would pretend memory was preserved even when H8Memory no longer owned it.

Solution: Gate `TryResolveRingViews()` with `H8Memory.IsInitialized` before any `H8Memory.CreateNativeArrayView*` call. Narrow failed-clear retention in `Dispose()` to the case where `H8Memory.IsInitialized` is still true; after shutdown, `ReleaseNativeBridgeBuffers()` nulls already-reaped pointers without calling `FreeRaw`. Scanner now requires both the no-view-after-shutdown guard and the live-H8Memory retention guard.

Rejected Alternatives: Rejected retaining stale pointers after `H8Memory.Shutdown()` because the backing memory is already gone and retention cannot prevent native UAF. Rejected relying on `HasNativeBridgeBuffers()` alone because non-null private pointers are not proof of live allocation ownership. Rejected adding managed exceptions/log strings in the late-shutdown path because fail-closed teardown must stay allocation-free.

Scalability potential: Low tier avoids stale alias creation during shutdown/restart churn. Middle, High, and Ultra keep the same raw-pool ABI; this is a cold lifecycle guard with no frame-path cost.

Hardware Impact: 0 us/frame steady-state. Cold getters and teardown now pay one bool check before creating raw `NativeArray` views. The change removes a stale-pointer alias path after memory tracker shutdown.

Proof State: STATIC_H8MEMORY_SHUTDOWN_VIEW_GUARD_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R26 - DTO Pointer-First Descriptor ABI

Problem: The descriptor was byte-aligned but not field-order compliant. `NativeAudioKernelRingBufferDescriptor` placed a 4-byte `DescriptorMagic` at byte 0, a 4-byte pad at byte 4, and only then the 8-byte pointer fields. That satisfies pointer alignment but violates the stricter APEX DTO rule: 8-byte fields first, then 4-byte fields.

Solution: Reorder the managed and native descriptor ABI to pointer-first layout. This intermediate pass produced a 48-byte descriptor: `Frames=0`, `SharedState=8`, `ReadIndex=16`, `WriteIndex=24`, `DescriptorMagic=32`, `CapacityFrames=36`, `CapacityMask=40`, `SharedStateLengthInts=44`. R31 supersedes this by keeping the same pointer-first order and adding immutable `SourceChannels` at byte 48 plus explicit padding at byte 52, making the active descriptor 56 bytes.

Rejected Alternatives: Rejected keeping magic-first as a "stable header" because this bridge is already source-coupled to the native plugin and the user explicitly required 8-byte fields first. Rejected padding the 56-byte layout further because padding does not fix field-order noncompliance. Rejected `Pack=1` because it would weaken natural ARM64 pointer alignment.

Scalability potential: Low, Middle, High, and Ultra tiers shared the intermediate pointer-first ABI. Active R31 ABI is 56 bytes to freeze callback channel stride; no tier gets a divergent ABI or a different authority route.

Hardware Impact: 0 us/frame. The intermediate cold registration payload reduction was rejected as less important than immutable source-channel stride. Active R31 descriptor remains naturally aligned and costs one extra 4-byte field plus 4 bytes of padding.

Proof State: SUPERSEDED_BY_R31_DESCRIPTOR_SOURCE_CHANNEL_TOCTOU_REPAIR. Compile/native rebuild/fuzzer not run by user instruction.

## R27 - Exception Boundary Classification

Problem: A stricter full-file text scan found managed exception tokens after the DTO layout fix. `NativeAudioFrameRingBuffer.cs` contains `try/finally` around DataVault telemetry write-lock release. `HectonSensoryKernelNativeBridge.cs` contains `try/catch` around DllImport calls so a missing or misbound native plugin can be marked unavailable and fail closed.

Solution: Classify the tokens instead of hiding them. Hot SPSC sample writing remains free of `try`, `catch`, `throw`, `new`, string formatting, LINQ, managed I/O, and locks. The `try/finally` blocks are deterministic lock-release guards. The `try/catch` blocks are cold native plugin bind boundaries at `GetStatus`, `TryRegister`, `TryClear`, and `TryDumpAudioBridgeTelemetry`; they convert DllNotFound/EntryPointMissing/BadImageFormat into `PluginUnavailable`.

Rejected Alternatives: Rejected removing cold P/Invoke catches because the first missing-plugin call would then throw through the bridge instead of failing closed. Rejected claiming a zero exception-token codebase because the source contains the boundary by design. Rejected managed file probing or path scanning as a replacement because that adds managed I/O and still does not prove native symbol binding.

Scalability potential: Low tier avoids process failure if the audio plugin is absent or mismatched; the library is marked unavailable after the first cold failure and subsequent calls return status bits. Middle, High, and Ultra keep the same DSP path and descriptor ABI.

Hardware Impact: 0 us/frame in steady-state DSP. The only managed exception cost is a cold plugin-load failure path, not the audio writer or native callback. A completely exception-free missing-plugin proof would require a separate native/plugin preload mechanism outside this bridge patch.

Proof State: STATIC_EXCEPTION_BOUNDARY_CLASSIFIED. Compile/native rebuild/fuzzer not run by user instruction.

## R28 - Managed Shared-State Corruption Fail-Closed Gate

Problem: Managed ring readers still trusted corrupt shared-state cursor values by masking `ReadSharedIndex(...) & _capacityMask`. A raw `ReadIndex = -1`, `WriteIndex = capacity + n`, or other corrupted value could be converted into a valid-looking cursor before managed producer/getter math. Native validation rejects bad descriptor metadata, but the managed producer also consumes live shared state and therefore needed its own fail-closed range gate.

Solution: Replace the masking reader with `TryReadSharedFrameIndex` in `NativeAudioFrameRingBuffer.cs:595-607`. It rejects raw cursor values outside `[0, capacityFrames)` before ring arithmetic. `TryWriteInterleaved` now records `TelemetryStatusSharedStateInvalid`, triggers the fixed dump route, and returns false at `NativeAudioFrameRingBuffer.cs:256-262`. DSP tick bookkeeping uses the same invalid-state dump path at `:352-356`, and bridge-failure telemetry ORs the invalid bit if cursors cannot be read at `:369-377`. Native binary dump export failure resets `_telemetryDumpQueued` at `:724-725`, so a failed export does not suppress future fault dumps.

Rejected Alternatives: Rejected keeping `raw & capacityMask` because it hides corruption and violates fail-closed behavior. Rejected throwing on cursor corruption because the audio path must return false/silence and preserve telemetry instead of raising managed exceptions. Rejected resetting the dump gate after successful export because repeated NaN/corrupt bursts could spam disk; a failed native export is the only retry-unblock case.

Scalability potential: Low tier fails to silence quickly with one range check per shared cursor read and no managed allocation. Middle keeps the same 300-frame telemetry evidence. High and Ultra can add richer editor diagnostics from the dump bytes without changing DTO layout or hot authority.

Hardware Impact: Normal writer adds two unsigned range comparisons before existing ring math. No profiler-backed microseconds claimed. The saved cost is avoided undefined behavior after shared-state corruption, not a frame-time optimization.

Proof State: STATIC_SHARED_INDEX_FAIL_CLOSED_PASS. Runtime token scan returned no managed allocation/I/O/string/LINQ/throw/lock hits; stale ABI/masked-corruption scan returned no hits; compile/native rebuild/fuzzer not run by user instruction.

## R29 - Telemetry Status Namespace Collision

Problem: `StatusBits` intentionally carries both audio telemetry-local bits and native bridge failure bits. The previous local value `TelemetryStatusSharedStateInvalid = 1 << 4` collided with `NativeAudioKernelBridgeStatus.CapacityInvalid = 1 << 4`. During `RecordBridgeFailure`, that makes a native capacity failure indistinguishable from managed shared-state cursor corruption in the binary dump.

Solution: Move telemetry-local status bits to the high local range in `NativeAudioFrameRingBuffer.cs:24-28`: write/overflow/non-finite/bridge-failure/shared-state-invalid now occupy bits 16-20. Native bridge status remains in its existing low bits plus `PluginUnavailable` at bit 30. Scanner now requires `TelemetryStatusWrite = 1 << 16` and `TelemetryStatusSharedStateInvalid = 1 << 20`.

Rejected Alternatives: Rejected adding a second DTO status field because that changes the 64-byte telemetry ABI and invalidates the byte map. Rejected keeping the overlap and relying on context because the dump is for post-mortem work where ambiguous bits are unacceptable. Rejected moving native enum values because the native plugin and C# bridge already share that ABI.

Scalability potential: Low through Ultra tiers keep the same 64-byte telemetry entry. Only forensic bit interpretation changes; no runtime memory growth or extra branch is introduced.

Hardware Impact: 0 us/frame. This is a constant-value correction with no additional operations in the hot writer.

Proof State: STATIC_TELEMETRY_STATUS_NAMESPACE_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R30 - Local Shared-State Metadata Validation Parity

Problem: C# `IsDescriptorValid` proved pointer alignment, pointer offsets, and descriptor capacity fields, but it did not re-read the live `SharedState` metadata slots before registration. Native validation checks capacity, mask, guard values, and source channel metadata. That mismatch means C# could enter the retry/registration path with a descriptor that native would reject as `SharedStateInvalid`.

Solution: Add `HasValidSharedStateMetadata` in `HectonSensoryKernelNativeBridge.cs:347-365` and call it from `IsDescriptorValid` at `:111-114` only after prior null/alignment/capacity checks pass. The method uses `Volatile.Read(ref sharedStatePtr[...])` at `:356-360` for capacity, mask, guard A/B, and source channels, then returns `SharedStateInvalid` before P/Invoke if metadata no longer matches the descriptor/native contract. Scanner checks at `OOP_AudioBridge_Scanner.cs:67-68` now reject removing this local parity guard.

Rejected Alternatives: Rejected relying on native validation only because local C# status would under-classify a known bad descriptor and retry through the native boundary. Rejected copying metadata into managed objects because the bridge must stay unmanaged and zero-GC. Rejected throwing or logging strings on mismatch because fail-closed status bits and black-box dump routes are the accepted failure channel.

Scalability potential: Low tier pays five cold volatile int reads during descriptor validation, not per audio sample. Middle, High, and Ultra keep the same descriptor ABI and shared-state layout; richer DSP remains gated by the same local/native contract.

Hardware Impact: 0 us/frame steady-state. Cold registration now adds five volatile reads and integer comparisons, buying deterministic pre-P/Invoke rejection of corrupted shared-state metadata.

Proof State: STATIC_SHARED_STATE_METADATA_VALIDATION_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R31 - Descriptor Source-Channel TOCTOU Repair

Problem: The shared-state metadata parity repair still left source-channel count as mutable callback truth. Native validation read `SourceChannelsSlot`, but `ProcessCallback` could later read the same slot after registration. If SharedState was corrupted from 1 to 2 after a mono-sized buffer was registered, the callback would use `sourceFrameIndex << 1` and overread the raw frame pool. That is not an alignment fault; it is a descriptor/metadata time-of-check-to-time-of-use fault.

Solution: Freeze source-channel count inside the native descriptor. The active descriptor is pointer-first and 56 bytes: `Frames=0`, `SharedState=8`, `ReadIndex=16`, `WriteIndex=24`, `DescriptorMagic=32`, `CapacityFrames=36`, `CapacityMask=40`, `SharedStateLengthInts=44`, `SourceChannels=48`, `_pad0/reserved0=52`. C# writes `descriptor.SourceChannels = _sourceChannels` at `NativeAudioFrameRingBuffer.cs:429`. C# validation rejects descriptor source channels outside `[1,2]` at `HectonSensoryKernelNativeBridge.cs:102-103` and requires `SourceChannelsSlot == descriptor.SourceChannels` at `:356-365`. Native validation mirrors the range/equality checks at `Plugin_HectonSensoryKernel.cpp:301-305` and `:324-329`. Native callback now reads immutable `ringBuffer.sourceChannels` at `:440`.

Rejected Alternatives: Rejected continuing to read `SourceChannelsSlot` in callback because mutable shared metadata cannot be callback stride authority after validation. Rejected a second native lookup or DataVault query because callback must stay local, deterministic, and zero-GC. Rejected dropping stereo support to keep the 48-byte descriptor because the managed ring already writes interleaved stereo and the native bridge must preserve that contract.

Scalability potential: Low/MX350 pays 8 cold ABI bytes and one copied int in the native callback descriptor. Middle, High, and Ultra keep the same ABI and can increase audio richness without changing callback truth ownership.

Hardware Impact: 0 us/frame from managed code. Native callback removes a mutable shared-state read for source-channel stride and uses the already-copied descriptor field. The memory cost is 8 bytes per registered descriptor, not per sample.

Proof State: STATIC_DESCRIPTOR_SOURCE_CHANNEL_TOCTOU_PASS. Compile/native rebuild/fuzzer not run by user instruction.

## R32 - Hot Telemetry Writer-Fence Removal

Problem: The previous relocation-safe telemetry proof put `GlobalDataVault.TryAcquireWriteLock` behind `RecordTelemetry`. `TryWriteInterleaved` calls `RecordTelemetry` on normal writes, overflow, and shared-state corruption, so the sample producer could hit a DataVault writer fence after publishing audio samples. That path was managed-allocation-free, but it was not lock-free.

Solution: Add a stable raw telemetry ring owned by `H8Memory` and `SystemID.AudioFrameRing`: `_telemetryPtr` is allocated with 8-byte alignment, exposed through `RingVaultViews.Telemetry`, and written by `WriteTelemetryEntry(views.Telemetry, ...)`. Keep `BufferID.AudioFrameRingTelemetry` as a cold GlobalDataVault identity/mirror lane. `TryMirrorTelemetryToDataVault` copies raw telemetry into DataVault only during fault dump or normal dispose, with `TryAcquireWriteLock` protected by `finally`.

Rejected Alternatives: Rejected keeping per-write DataVault `TryAcquireWriteLock` because the prompt requires a lock-free SPSC writer. Rejected transient `TryResolveHandle` mutation because GlobalDataVault compaction can relocate a resolved view unless a lock or external-view contract is held. Rejected permanent `TryLockBuffer`/alias pinning because it blocks DataVault relocation/growth for the audio bridge lifetime. The accepted compromise is explicit: the authoritative hot blackbox ring is stable raw unmanaged memory; GlobalDataVault is a best-effort cold mirror until the vault exposes a non-relocating lock-free owner-write contract.

Scalability potential: Low/MX350 pays one 19,200-byte raw telemetry ring plus the existing 19,216-byte dump scratch and no per-write DataVault fence. Middle keeps full 300-entry fault evidence. High and Ultra can add richer editor readers over the DataVault mirror without changing the hot writer law.

Hardware Impact: Steady-state managed allocations remain 0. The hot writer removes one DataVault writer-fence attempt per telemetry record. Measured microseconds are not claimed because no profiler/build run was executed; static method-body scan proves the lock route is absent from `TryWriteInterleaved` and `RecordTelemetry`.

Proof State: STATIC_HOT_TELEMETRY_RAW_RING_PASS. `TryWriteInterleaved` and `RecordTelemetry` contain no `TryAcquireTelemetryWriteView` or `TryAcquireWriteLock`; `TryMirrorTelemetryToDataVault` contains the only telemetry DataVault write-lock route. Compile/native rebuild/fuzzer not run by user instruction.

## R33 - Tear-Resistant Raw Telemetry Snapshot [SUPERSEDED BY R36 FOR RUNTIME BARRIER CLAIMS]

Problem: The hot telemetry writer was lock-free after R32, but it still used one 64-byte struct assignment to publish `AudioBridgeTelemetryEntry`. A fault dump or cold DataVault mirror could read the same slot while that assignment was in progress and persist a mixed forensic record.

Solution: Replace the struct assignment with a sequence-publish protocol. `WriteTelemetryEntry` writes `Sequence=0` first, writes all DTO fields, writes `StateHash`, then publishes the final non-zero sequence with `Volatile.Write`. `TryReadTelemetryEntryStable` copies bytes only after a non-zero sequence is observed, fences the cold `MemCpy` snapshot, re-reads sequence after copy, and rejects the entry if sequence changed, stayed zero, or the recomputed hash does not match. Dump and DataVault mirror copy zero-entry placeholders for rejected/in-progress slots instead of preserving torn state.

Rejected Alternatives: Rejected adding a lock around dump/mirror because that reintroduces a producer-side synchronization hazard if later reused incorrectly. Rejected enlarging the DTO with a second sequence field because the 64-byte byte map is already clean and enough when sequence plus hash validation is enforced. Rejected managed queues or snapshots because the runtime path must stay zero-GC.

Scalability potential: Low/MX350 pays fixed field writes plus two volatile sequence writes per telemetry event, no managed allocation, no DataVault hot fence, and no hot-writer `Thread.MemoryBarrier`. Middle keeps deterministic 300-entry fault evidence. High and Ultra can consume the cold DataVault mirror knowing torn slots are zeroed instead of mixed.

Hardware Impact: Measured microseconds not claimed; no profiler/build run was executed. Static work added to telemetry event path only: two volatile sequence writes and field-wise DTO writes. Historical state at R33 retained two cold `Thread.MemoryBarrier` calls around `UnsafeUtility.MemCpy`; R36 removed both runtime barriers. Audio sample writing remains unmanaged and lock-free.

Proof State: STATIC_TEAR_RESISTANT_RAW_TELEMETRY_PASS. Source scan confirms seqlock begin/publish, stable reader, hash guard, no `telemetry[index] = entry`, and no `destination[i] = source[i]`. Compile/native rebuild/fuzzer not run by user instruction.

## R34 - Hot Writer Thread Barrier Scrub [SUPERSEDED BY R36 FOR COLD READER BARRIER CLAIMS]

Problem: R33 still described two `Thread.MemoryBarrier` calls as telemetry event-path work. A fresh structural scan after compaction showed those barriers were present in `WriteTelemetryEntry`, which made the hot-adjacent telemetry writer heavier than necessary and weakened the "no hidden managed runtime call" audit story.

Solution: Remove the writer barriers. The writer now uses `Volatile.Write(ref target.Sequence, 0u)`, writes all fields, writes `StateHash`, then publishes the final non-zero sequence with `Volatile.Write(ref target.Sequence, sequence)`. Historical state at R34 still used cold `Thread.MemoryBarrier` around `UnsafeUtility.MemCpy`; R36 removed that remaining runtime call and made the active proof rely on sequence-before/copy/sequence-after plus `StateHash`.

Rejected Alternatives: Rejected deleting the cold reader fences because a torn dump is worse than a blunt full-file token report. Rejected a DataVault lock around snapshot reads because it would reintroduce synchronization semantics near the telemetry route. Rejected managed queues or copied arrays because runtime forensic state must stay unmanaged and fixed-size.

Scalability potential: Low/MX350 telemetry event cost is now fixed field writes plus two volatile sequence publications, with no hot-writer `Thread.MemoryBarrier`. Middle/High/Ultra retain stable dump/mirror proof; higher tiers can read the cold DataVault mirror without changing hot authority.

Hardware Impact: Measured microseconds not claimed; no profiler/build run was executed. Static reduction at R34: two `Thread.MemoryBarrier` calls removed from every telemetry event. Remaining cold dump/mirror barrier cost was superseded by R36 and is no longer present in runtime code.

Proof State: STATIC_HOT_WRITER_THREAD_BARRIER_REMOVED. Compile/native rebuild/fuzzer not run by user instruction.

## R35 - Native Async Dump Queue Repair

Problem: The native dump export path still performed file open/write/close work inline. That did not allocate managed memory, but it made `HectonSensoryKernel_DumpAudioBridgeTelemetry` a synchronous native I/O boundary reachable immediately after managed fault snapshot creation.

Solution: Add fixed native scratch and a native async queue: `g_telemetryDumpBuffer[kTelemetryDumpMaxBytes]`, `g_telemetryDumpInUse`, `g_telemetryDumpBytes`, `QueueTelemetryDumpAsync`, and `TelemetryDumpThreadMain`. Dump globals are declared at `Plugin_HectonSensoryKernel.cpp:101-103` before thread/queue functions, removing the C++ declaration-order hazard. The export at `Plugin_HectonSensoryKernel.cpp:525-528` now returns `QueueTelemetryDumpAsync(bytes, byteCount)`. The actual `fopen/fwrite/fclose` work is isolated to `WriteTelemetryDumpFile` at `Plugin_HectonSensoryKernel.cpp:105-124` and called only from the unmanaged background thread.

Rejected Alternatives: Rejected managed `Thread`, `FileStream`, `Path`, and `Directory` because the dump path must stay out of managed allocation/I/O. Rejected `malloc`/`free` for queue storage because the native plugin already can keep a fixed 19,216-byte max scratch. Rejected keeping synchronous export `fwrite` because it can block the managed fault route.

Scalability potential: Low/MX350 avoids blocking the fault call on disk I/O and pays one static native 19,216-byte dump buffer. Middle/High/Ultra keep the same forensic byte contract; richer diagnostics must consume the file after it is written, not change DTO layout or audio truth ownership.

Hardware Impact: Measured microseconds not claimed; no native rebuild/profile run was executed. Static reduction: export body now has no `fopen`/`fwrite` and returns after queue/copy/thread-start. Remaining cost at queue time is bounded `memcpy` of 19,216 bytes plus native thread creation on fault only.

Proof State: STATIC_NATIVE_DUMP_ASYNC_QUEUE_PASS_WITH_RUNTIME_LIMITATION. Static scans prove no native heap tokens and no inline export `fopen/fwrite`. Compile/native rebuild/fuzzer not run by user instruction. Post-queue disk-open/write failure cannot be reported back to C# in this patch; runtime validation must confirm thread linkage and dump file creation.

## R36 - Runtime Thread Barrier Removal And Task08 Contract Honesty

Problem: The last APEX pass still retained two runtime `Thread.MemoryBarrier()` calls inside `TryReadTelemetryEntryStable`. They were cold dump/mirror reader fences, not hot writer barriers, but the user requested a literal runtime scan with no hidden managed calls. The same pass also over-reported Task08: the prompt demands the 300-frame telemetry ring inside `GlobalDataVault`, while the active implementation keeps the authoritative hot ring in stable raw `H8Memory` and mirrors it to `BufferID.AudioFrameRingTelemetry` only on fault/dispose.

Solution: Remove both runtime `Thread.MemoryBarrier()` calls and rely on the existing seqlock contract: `Volatile.Read` sequence before copy, unmanaged `UnsafeUtility.MemCpy`, `Volatile.Read` sequence after copy, then `StateHash` verification. Harden `OOP_AudioBridge_Scanner.cs` to reject any future runtime `Thread.MemoryBarrier` token. Record the Task08 limitation explicitly in both JSON reports: current `GlobalDataVault` exposes mutable compaction-safe access through `TryAcquireWriteLock` or relocation-blocking pin routes, so the lock-free hot writer cannot be both inside the vault and free of writer fences with the current public API.

Rejected Alternatives: Rejected keeping cold `Thread.MemoryBarrier` for forensic conservatism because it violates the literal no-hidden-managed-runtime-call audit. Rejected per-record `GlobalDataVault.TryAcquireWriteLock` because it puts a writer fence behind `RecordTelemetry`. Rejected lifetime `TryLockBuffer`/alias pinning because it blocks relocation/growth. Rejected transient `TryResolveHandle` mutation because it does not protect against relocation while native/hot code owns a pointer. Rejected inventing a new DataVault API in this pass because that is Core/Memory authority, not the narrow audio bridge repair domain.

Scalability potential: Low/MX350 keeps one fixed 19,200-byte raw telemetry ring and no runtime BCL barrier call. Middle keeps the cold DataVault mirror for editor/forensic readers. High and Ultra can consume richer diagnostics from the mirror or a future Core-approved lock-free vault owner-write view without changing DTO layout or native bridge ABI.

Hardware Impact: Measured microseconds not claimed; no profiler/build run was executed. Static reduction: two runtime BCL memory barrier calls removed from cold dump/mirror reads. The hot writer remains field-wise unmanaged DTO writes plus two `Volatile.Write` sequence publications. Task08 strict DataVault ownership remains a documented architecture limitation, not a hidden PASS.

Proof State: STATIC_RUNTIME_THREAD_BARRIER_FREE_WITH_TASK08_DATAVAULT_HOT_RING_LIMITATION. Full runtime forbidden-token scan returned no hits for `new`, managed I/O/string/LINQ/throw/lock/boxing, `new Thread`, or `Thread.MemoryBarrier` in the ring/bridge files. Compile/native rebuild/fuzzer not run by user instruction.

## R37 - APEX Paranoid Static Re-Audit Without Build

Problem: The user demanded another post-override proof pass and explicitly ordered rare `dotnet`/build usage. The code was already runtime-barrier-free, but historical Status/Rationale sections still contained pre-Loop-28 statements saying cold `Thread.MemoryBarrier` calls remained. That is a proof-artifact contradiction even when the current code is clean.

Solution: Re-read Status, Rationale, AGENTS, domain map, six task-relevant mandates, and the full `<AGENT_PROMPT id="1314">`. Re-ran static scans over the current runtime ring/bridge, native plugin, editor scanner, JSON reports, and targeted CrashTelemetry dependency. Patched historical Status/Rationale barrier statements to mark them superseded by Loop 28/R36. No runtime code change was needed in this pass.

Rejected Alternatives: Rejected launching `dotnet build` or Unity compilation because the user explicitly ordered rare build use and no static evidence required a compiler pass. Rejected deleting old audit history because it records the correction path; superseding stale claims is more honest than rewriting history. Rejected converting the hot telemetry ring back into `GlobalDataVault` because current vault mutation routes are writer-fenced or relocation-pinning.

Scalability potential: Low/MX350 keeps the current lock-free raw telemetry ring and fixed native dump scratch. Middle keeps the cold DataVault mirror for editor/diagnostic consumption. High/Ultra can add richer diagnostics only after Core/Memory exposes a lock-free owner-write vault route; DTO layout and native ABI do not change by quality tier.

Hardware Impact: No runtime code changed in R37. Static proof preserved 0 managed allocations in the audited hot audio bridge code and avoided unnecessary compiler pressure on a shared workstation. Microsecond gains are not claimed because no profiler/build run was executed.

Proof State: STATIC_APEX_REAUDIT_PASS_WITH_LIMITATION. Runtime scan returned no `new`, managed I/O/string/LINQ/throw/lock/boxing, `new Thread`, or `Thread.MemoryBarrier` in `NativeAudioFrameRingBuffer.cs` and `HectonSensoryKernelNativeBridge.cs`; stale ABI/tear/barrier scan returned no hits; native heap scan returned no hits; AUP scan returned no hits. Strict Task08 hot-ring-inside-GlobalDataVault remains false by architecture limitation and is documented, not hidden.

## R38 - ProducedSampleCount Stereo Semantics Repair

Problem: `AudioBridgeTelemetryEntry.ProducedSampleCount` is a sample counter, but `TryWriteInterleaved` incremented `_producedSampleCount` by `safeFrameCount`. Mono was correct by coincidence; stereo under-reported produced samples by 2x. That corrupts post-mortem audio throughput evidence without tripping ABI validation.

Solution: Change `NativeAudioFrameRingBuffer.cs:335` to `Interlocked.Add(ref _producedSampleCount, (long)safeFrameCount * safeChannels)`. This keeps the 64-byte DTO, uses the already-validated channel count, and adds one integer multiply per accepted write call, not per sample.

Rejected Alternatives: Rejected renaming the field to `ProducedFrameCount` because persisted telemetry already documents sample semantics. Rejected adding a second field because the 64-byte byte map is clean and should not change for a forensic naming bug. Rejected converting to floating counters or formatted log text because the bridge must stay unmanaged/zero-GC.

Scalability potential: Low tier receives correct mono/stereo forensic counters with no allocation and no extra buffer. Middle, High, and Ultra keep identical DTO layout and can consume the same binary dump without tier-specific schema handling.

Hardware Impact: No measured microseconds claimed; no profiler/build run was executed. Static cost is one `long` multiply per successful `TryWriteInterleaved` call. No per-sample loop work was added.

Proof State: STATIC_PRODUCED_SAMPLE_COUNTER_REPAIR_PASS. Runtime forbidden-token scan and method-body scan stayed clean after the change. Compile/native rebuild/fuzzer not run by user instruction.
