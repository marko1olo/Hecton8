# Rationale_1314 - AUDIO_MASTER_BUS_ALIGNMENT_REPAIRER

Status: STATIC FAIL BLOCKERS GATED / RAW TELEMETRY PASS WITH TASK08 DATAVAULT HOT-RING LIMITATION / DIRECT REGISTER-CLEAR STATUS EXPORTS / NATIVE DUMP THREAD REMOVED / COMPILE NOT RUN BY USER INSTRUCTION

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

## R39 - Raw Bridge Regression Scrub

Problem: The live audio ring had drifted back toward the rejected DataVault hot-buffer route: native-visible frames/shared/dump ownership and hot telemetry were no longer aligned with the raw `H8Memory` proof recorded in the scanner and reports. A relocatable vault-backed or writer-locked path is not acceptable for a native audio callback descriptor or SPSC writer.

Solution: Restore the raw `H8Memory` bridge state for frames, shared state, 300-entry telemetry, and dump bytes. The authoritative hot telemetry ring is stable unmanaged memory; `GlobalDataVault` remains only the cold `BufferID.AudioFrameRingTelemetry` mirror copied during dump/dispose. `TryWriteInterleaved`, `RecordTelemetry`, `WriteTelemetryEntry`, `RequestTelemetryDump`, and `TryReadTelemetryEntryStable` now scan clean for managed allocation/string/I/O/LINQ/lock/throw and DataVault writer-lock tokens; only `TryMirrorTelemetryToDataVault` owns the cold mirror write-view.

Rejected Alternatives: Rejected per-record `GlobalDataVault.TryAcquireWriteLock` because it adds a producer-side writer fence. Rejected long-lived vault pins because they block relocation/growth. Rejected transient vault view mutation because native-visible pointers cannot survive compaction. Rejected rewriting Core/Memory in this pass because the approved lock-free vault owner-write API does not exist yet.

Scalability potential: Low devices keep stable raw memory and no vault writer fence per telemetry record. Middle keeps the cold DataVault mirror for editor/forensic reads. High/Ultra can consume richer mirror diagnostics later without changing DTO layout, bridge ABI, or gameplay truth ownership.

Hardware Impact: No profiler microseconds claimed. Static reduction is the removal of hot-path DataVault lock/fence exposure from the audio writer and native-visible buffer lifetime. Compile/native rebuild not run by explicit user instruction.

Proof State: STATIC_RAW_H8MEMORY_HOT_RING_RESTORED_WITH_TASK08_LIMITATION. Strict Task08 hot-ring-inside-GlobalDataVault remains architecturally false until Core/Memory exposes a lock-free non-relocating owner-write route.

## R40 - POSIX Callback Drain Yield

Problem: `WaitForProcessCallbacksToDrain` used `Sleep(0)` on Windows but no yield on non-Windows. On Linux/macOS/Android that meant register/clear could burn up to `kDrainSpinLimit` tight polls while waiting for an active audio callback to exit. It was bounded and cold, but still wasteful and hostile to mobile/Quest scheduling.

Solution: Include `<sched.h>` on non-Windows and call `sched_yield()` inside the bounded drain loop. The function still fails closed with Busy status if callback depth does not drain. The scanner now requires `sched_yield();` so the POSIX branch cannot regress into a pure tight spin.

Rejected Alternatives: Rejected unbounded waiting because plugin register/clear must fail closed. Rejected millisecond sleeps because clear/register should not inject arbitrary latency. Rejected managed waiting because this is native plugin callback coordination.

Scalability potential: Low/Quest avoids wasting CPU quanta on a cold contention event. Middle/High/Ultra keep identical audio callback behavior; no per-sample or per-frame work is added.

Hardware Impact: 0 us/frame steady-state. Cold register/clear contention trades a tight poll for OS yield on non-Windows. Native rebuild not run in this pass.

Proof State: STATIC_POSIX_DRAIN_YIELD_PASS.

## R41 - Android Native Audio Route Gate

Problem: Android/Quest was a fake native-audio story: the bridge P/Invoke block excluded `UNITY_ANDROID`, and the build matrix did not require an Android arm64 `libHectonAudioKernel.so`. The C++ source was POSIX-compatible enough to audit, but an Android build could silently ship without the native master-bus route.

Solution: Add `UNITY_ANDROID` to the bridge P/Invoke compile gate, then add an Android arm64 audio-kernel requirement to `NativePluginMatrixValidator`. The scanner now guards both the Android bridge route and the Android plugin matrix route. Current file scan found only the Windows x86_64 DLL; Android arm64 native audio is therefore still blocked until `libHectonAudioKernel.so` is built and packaged.

Rejected Alternatives: Rejected enabling Android P/Invoke without a build-matrix gate because that would move failure to runtime. Rejected claiming Quest readiness because no Android native rebuild or headset smoke test was run. Rejected leaving Android excluded because the project already carries Android/Quest validation paths and the audio bridge would remain permanently unreachable on the target.

Scalability potential: Low/Quest gets a binary gate before release rather than a silent managed fallback. Middle/High/Ultra desktop targets keep the same P/Invoke route. Future Android native DSP can use the same 56-byte descriptor and 64-byte telemetry DTO without schema fork.

Hardware Impact: 0 us/frame. Build-time validation cost only. Runtime cost is unchanged when the plugin is present; missing plugin now has a preflight blocker instead of relying on first-call load failure.

Proof State: STATIC_ANDROID_ROUTE_WIRED_BUILD_GATE_ADDED. Android plugin binary is absent in this workspace; no build or device proof was run.

## R42 - Fail-Closed Source Channel Contract

Problem: `NativeAudioFrameRingBuffer.Initialize` silently clamped invalid `sourceChannels` into `[1,2]`, while writer/native validation rejected invalid channel counts. That could hide a caller bug and create telemetry/descriptor evidence for a different contract than the caller requested.

Solution: Reject invalid `sourceChannels` before allocation/descriptor registration by recording `NativeAudioKernelBridgeStatus.SharedStateInvalid` when a live ring can accept telemetry, then returning without `Dispose()`. `TryWriteInterleaved` and `TryCreateNativeDescriptor` validate backing storage against `_frameSampleCapacity`, so accepted capacity uses the initialized channel contract rather than recomputed ad hoc channel math.

Rejected Alternatives: Rejected `math.clamp` because it is a silent ABI mutation. Rejected logging managed strings because the runtime path must stay zero-GC. Rejected broad renderer edits because this is a bridge contract failure and the caller can only succeed with mono/stereo.

Scalability potential: Low/MX350 avoids undefined stride behavior without extra runtime cost. Middle/High/Ultra keep the same descriptor DTO and shared-state slot map; quality tiers do not change audio truth ownership.

Hardware Impact: 0 us/frame. The added check is initialization-only. Static scan shows no managed allocation/string/LINQ/I/O token in the runtime ring/bridge files.

Proof State: STATIC_SOURCE_CHANNEL_FAIL_CLOSED_PASS. No Unity compile or live audio fuzzer was run.

## R43 - Native Dump Directory Gate

Problem: The native dump writer wrote to `Docs/AgentLogs/Dump_1314_AudioBridge.bin` but did not guarantee that `Docs/AgentLogs` existed. A fault dump could fail at `fopen` after the C# side had already queued a valid 19,216-byte snapshot.

Solution: Add `EnsureTelemetryDumpDirectory`: Windows calls `CreateDirectoryA("Docs")` and `CreateDirectoryA("Docs/AgentLogs")`; non-Windows calls `mkdir("Docs", 0755)` and `mkdir("Docs/AgentLogs", 0755)`. `WriteTelemetryDumpFile` calls it before `fopen/fopen_s`. This keeps directory creation native and fixed-path.

Rejected Alternatives: Rejected managed `Directory.CreateDirectory`, `Path`, or `FileStream` because fault dump must not allocate managed memory. Rejected reporting success back to C# after thread start because the current async export only returns queue acceptance; changing that would need a native completion/status API and runtime exercise.

Scalability potential: Low devices avoid managed fault allocation. Middle/High/Ultra get the same fixed binary dump path and schema; richer diagnostics can read the file later without changing callback behavior.

Hardware Impact: 0 us/frame. Directory calls are fault-only and idempotent. Static proof only; native rebuild/runtime dump creation was not run.

Proof State: STATIC_NATIVE_DUMP_DIRECTORY_GATE_PASS_WITH_RUNTIME_LIMITATION.

## R44 - Player Build Native Matrix Hard Fail

Problem: The native plugin matrix could warn without breaking an actual player build unless a strict define was present. That made missing native audio plugin binaries a process preference instead of a build contract.

Solution: `OnPreprocessBuild` calls `Validate(report.summary.platform, strictBuild: true)`. Advisory scan remains limited to the editor menu route. Android arm64 now requires `Assets/Plugins/Android/arm64-v8a/libHectonAudioKernel.so` or `Assets/Plugins/Android/libs/arm64-v8a/libHectonAudioKernel.so`.

Rejected Alternatives: Rejected `HECTON_STRICT_NATIVE_PLUGIN_BUILD` because release safety cannot depend on a forgotten scripting define. Rejected runtime-only `PluginUnavailable` because that still permits a bad player artifact to ship.

Scalability potential: Low/Quest gets a preflight blocker instead of silent managed-only output. Middle/High/Ultra desktop builds keep the same plugin route. CI can fail before runtime smoke tests.

Hardware Impact: 0 us/frame. Build-time validation only. Current workspace still lacks the Android arm64 audio kernel binary, so Quest native audio remains blocked until the binary is built and packaged.

Proof State: STATIC_PLAYER_BUILD_HARD_FAIL_GATE_PASS. No player build was launched.

## R45 - Native Utility Link Exclusion

Problem: `BuildHectonSensoryKernel.bat` compiled `AudioPluginUtil.cpp`. That Unity sample utility file still contains heap-allocating FFT/analyzer/test helper paths (`new float[]`, `new UnityComplexNumber[]`, `new char[]`, cached `new unsigned int[]`). Depending on linker DCE to hide a heap-bearing translation unit is not a clean release proof.

Solution: Remove `AudioPluginUtil.cpp` from the Hecton kernel build and implement `UnityGetAudioEffectDefinitions` directly in `Plugin_HectonSensoryKernel.cpp`. The local export uses fixed static `UnityAudioEffectDefinition` storage and bounded char copy through `CopyEffectName`; the plugin includes `AudioPluginInterface.h` directly. `/Gy /Gw` and `/OPT:REF /OPT:ICF` remain as normal link hygiene, not as the primary proof.

Rejected Alternatives: Rejected partial mutation of `AudioPluginUtil.cpp` because it would damage a reusable sample utility while still leaving other heap paths. Rejected "dead-strip should remove it" as insufficient evidence. Rejected adding a custom allocator because Hecton has zero parameters and does not need the utility translation unit.

Scalability potential: Low/MX350 gets a smaller native link surface and no utility heap code in the Hecton binary after rebuild. Middle/High/Ultra keep identical DSP callback behavior and can still add future native DSP through explicit Hecton-owned fixed storage.

Hardware Impact: 0 us/frame. Rebuilt binary should have less unused native code linked; no native rebuild or symbol scan was run, so binary proof remains pending.

Proof State: STATIC_NATIVE_UTILITY_LINK_EXCLUDED. `AudioPluginUtil.cpp` remains in the repo as a sample utility but is no longer in `BuildHectonSensoryKernel.bat`.

## R46 - Native Plugin Importer Matrix Proof

Problem: The build matrix hard-failed on missing files, but still accepted a raw native binary if the file existed. `Assets/Plugins/x86_64/HectonAudioKernel.dll.meta` is GUID-only in this workspace, with no `PluginImporter:` block. That means a Windows x64 player could pass the file-exists check while the Unity importer route remained unproven.

Solution: Route every native dependency check in `NativePluginMatrixValidator` through `RequirePlugin` or `RequireAnyPlugin`. Both helpers now call `HasPluginImporter`, which resolves `AssetImporter.GetAtPath(assetPath) as PluginImporter` and requires `importer.GetCompatibleWithPlatform(target)`. This follows Unity's documented PluginImporter compatibility API and rejects raw files, GUID-only metadata, and target-disabled plugin import settings.

Rejected Alternatives: Rejected editing the `.dll.meta` by hand because Unity importer YAML is version-sensitive and a manual meta patch would be a fake binary/import proof without editor reimport. Rejected relying on folder naming like `x86_64` because path convention does not prove importer compatibility. Rejected runtime-only `DllNotFoundException` handling because a bad player artifact should be blocked before packaging.

Scalability potential: Low/Quest now fails before shipping if Android arm64 `.so` is absent or not imported for Android. Middle/High/Ultra desktop builds must prove both binary presence and Unity importer routing; future platform binaries use the same gate without changing runtime DTOs.

Hardware Impact: 0 us/frame. Build-time validation only. Current workspace still lacks Android/Linux/macOS Hecton audio binaries, and the Windows DLL metadata is not PluginImporter-proven.

Proof State: STATIC_PLUGIN_IMPORTER_GATE_PASS. Source proof only; no Unity editor reimport or player build was run.

## R47 - Invalid Initialize Must Preserve Known-Good Bridge

Problem: `NativeAudioFrameRingBuffer.Initialize` rejected invalid `sourceChannels`, but the rejection branch called `Dispose()`. A bad repeat init call could therefore tear down an already registered native ring even though the previous bridge state was valid.

Solution: Change the invalid source-channel branch to call `RecordBridgeFailure(NativeAudioKernelBridgeStatus.SharedStateInvalid)` and return without allocation, descriptor registration, or `Dispose()`. If no ring exists, there is nothing to record and no memory is allocated. If a ring exists, the black-box telemetry records the caller contract fault and keeps the known-good bridge alive.

Rejected Alternatives: Rejected `math.clamp` because silent normalization mutates the ABI contract. Rejected disposal on invalid input because fail-closed should reject the bad call, not destroy a valid prior state. Rejected managed logs/exceptions because the runtime path must stay zero-GC.

Scalability potential: Low devices avoid an avoidable audio outage from a bad cold-path parameter. Middle/High/Ultra keep the same descriptor DTO, source-channel metadata route, and native callback stride contract.

Hardware Impact: 0 us/frame. The new branch is initialization-only; the added telemetry call is fault-only. Static scan found no managed allocation/string/I/O/LINQ/thread-barrier tokens in runtime ring/bridge files.

Proof State: STATIC_INVALID_INIT_RETAINS_BRIDGE_PASS. No Unity compile, player build, native rebuild, or fuzzer was run.

## R48 - SPSC Capacity Allocation Bomb Clamp

Problem: `AudioFrameSpscRingBuffer` declared `AudioBufferCapacity = 65536`, but `ResolvePowerOfTwoCapacity` could still return up to `1 << 30`. A bad cold-path capacity request could attempt multi-gigabyte raw H8Memory frame allocation before failing, which is unacceptable on MX350, Steam Deck, Quest, and any 8GB target.

Solution: Remove the `MaximumCapacityFrames = 1 << 30` route. `ResolvePowerOfTwoCapacity` now clamps any request at or above `AudioBufferCapacity` to exactly 65,536 frames before allocation. `NextPowerOfTwo` also falls back to `AudioBufferCapacity` on overflow. The editor fuzzer default capacity now uses the same bridge constant and its default block is half capacity, preserving the SPSC empty-slot guard.

Rejected Alternatives: Rejected leaving the upper cap to allocation failure because the allocator should not be asked for impossible audio buffers. Rejected unbounded power-of-two growth because the audio SPSC mandate names 16,384/32,768/65,536 as the intended capacity scale. Rejected a 65,536-frame fuzzer block against a 65,536-frame ring because a one-slot empty guard makes maximum writable frames 65,535.

Scalability potential: Low/MX350 and Quest cannot be forced into a multi-GB audio bridge allocation by a bad parameter. Middle/High/Ultra keep the same 65,536-frame maximum, using saved memory budget for actual presentation lanes rather than oversized ring slack.

Hardware Impact: 0 us/frame. Cold init now has a fixed upper allocation of 65,536 frames * channels * 4 bytes. Worst stereo frame storage is 524,288 bytes plus 56 bytes shared state, 19,200 bytes telemetry, and 19,216 bytes dump scratch.

Proof State: STATIC_CAPACITY_CLAMP_PASS. No Unity compile, player build, native rebuild, or fuzzer was run.

## R49 - Renderer Native Clear Status Truth

Problem: `PlayerCriticalProceduralAudioRenderer.ClearNativeOutputBridge` called `HectonSensoryKernelNativeBridge.TryClear()` and then unconditionally set `_nativeOutputRegistered=false`. If native clear failed with Busy or Active, the renderer forgot that the native callback could still retain the descriptor.

Solution: Make the renderer call `TryClear(out clearStatus)`. It now clears `_nativeOutputRegistered` only when native clear proves release. On failed clear it records the status into `_sampleRingBuffer` telemetry. It only force-clears the flag on `PluginUnavailable`, because no loaded native plugin can retain the descriptor in that state.

Rejected Alternatives: Rejected blind flag reset because it breaks one fact -> one owner -> one proof for native descriptor ownership. Rejected always calling `TryRegister` after failed clear because that can overwrite a descriptor while native may still be Busy. Rejected managed exceptions/log spam in release path because telemetry already carries the failure.

Scalability potential: Low/Quest avoids use-after-free-adjacent state lies during teardown. Middle/High/Ultra keep the same native bridge and telemetry DTOs; richer output quality does not change ownership truth.

Hardware Impact: 0 us/frame. The branch is cold clear/rebind only. Failed clear now preserves state instead of causing a false renderer-side release.

Proof State: STATIC_RENDERER_CLEAR_STATUS_TRUTH_PASS. No Unity compile, player build, native rebuild, or fuzzer was run.

## R50 - Invalid Candidate Descriptor Must Not Clear Active Bridge

Problem: `HectonSensoryKernelNativeBridge.TryRegisterWithRetryGate` called `TryClear(out _)` when the candidate descriptor failed local C# validation. No native registration call had happened yet, so clearing native state there could tear down a previously valid bridge because of an unrelated bad candidate.

Solution: Return false immediately on invalid candidate descriptor. Original Loop 26 kept final cleanup-clear after actual registration attempts; R68 supersedes that choice because native register now preserves old ownership on failed attempts and cleanup-clear would erase it.

Rejected Alternatives: Rejected clearing on local validation failure because it violates ownership truth and creates a destructive side effect before any native call. Rejected pushing this entirely to renderer because the bridge gate itself is an internal API and should not encode a trap for future callers.

Scalability potential: All tiers preserve stable native audio registration under bad cold-path rebind inputs. Low/Quest especially benefits because transient native plugin failures should not cascade into avoidable silence.

Hardware Impact: 0 us/frame. Cold registration path only.

Proof State: STATIC_INVALID_DESCRIPTOR_NO_CLEAR_PASS. No Unity compile, player build, native rebuild, or fuzzer was run.

## R51 - Native Callback Bounds Must Be Explicit

Problem: `Plugin_HectonSensoryKernel.cpp ProcessCallback` accepted Unity host `length` and `outchannels` directly. The debug export had a 4096*8 scratch cap, but the real callback path used int frame/channel products for buffer clear and indexing. A bad host contract could drive integer overflow or a pathological loop before the native bridge failed closed.

Solution: Add fixed callback bounds: max 65,536 frames and max 64 host channels, plus a fixed maximum output-sample budget of 65,536 * 64. `ProcessCallback` now validates the frame/channel product before `memset`; products beyond that fixed budget return with `kStatusSharedStateInvalid` before touching host output. Bounded but oversized host contracts clear output to silence first, then return before bridge mixing. Normal blocks compute `outputSampleCount` as `size_t` and use `size_t` input/output base indices. Input passthrough is only copied for bounded channel layouts.

Rejected Alternatives: Rejected an 8-channel real-callback cap because it would unnecessarily block uncommon multichannel routes; 8 remains only the debug scratch export cap. Rejected leaving it to Unity's normal DSP buffer behavior because fail-closed code must defend the native boundary. Rejected managed-side checks because the vulnerable arithmetic was in the native callback.

Scalability potential: Low/MX350/Quest get a constant-cost defensive branch and no accidental pathological native loop. Middle/High/Ultra retain support for uncommon high-channel layouts up to 64 while the bridge source remains mono/stereo.

Hardware Impact: 0 us/frame in normal audio blocks. The new branches are scalar bounds checks; sample clear/indexing stays linear in the host buffer size.

Proof State: STATIC_NATIVE_CALLBACK_BOUNDS_PASS. No Unity compile, native rebuild, player build, or fuzzer was run.

## R52 - Oversized Native Callback Must Produce Bounded Silence

Problem: The first callback-bounds patch returned immediately on `length > 65536` or `outchannels > 64`. That protected index math but could leave a bounded host output buffer uncleared. Fail-closed audio should prefer silence when the host product is inside a fixed budget.

Solution: Add `kMaxProcessOutputSamples` and `TryComputeOutputSampleCount`. `ProcessCallback` now rejects impossible frame/channel products before touching output, but clears bounded products to silence before returning from oversized-but-bounded contracts.

Rejected Alternatives: Rejected clearing unbounded host products because a malicious or corrupted callback contract could force a large real-time memset. Rejected leaving bounded oversized output untouched because that is not fail-closed silence.

Scalability potential: Low/MX350/Quest get deterministic silence for bad but bounded callback contracts. Middle/High/Ultra keep the same normal path and support up to 64 host channels.

Hardware Impact: 0 us/frame in normal operation. Fault-only bounded clear can touch up to 4,194,304 samples, but only when the host has already violated the callback contract and the product remains inside the fixed safety budget.

Proof State: STATIC_NATIVE_CALLBACK_BOUNDED_SILENCE_PASS. No Unity compile, native rebuild, player build, or fuzzer was run.

## R53 - Deferred Native Clear Must Retain Managed Raw-Buffer Owner

Problem: `AudioFrameSpscRingBuffer.Dispose()` could refuse to free raw H8Memory bridge buffers after native `TryClear` reported Active/Busy, but the method returned `void`. `PlayerCriticalProceduralAudioRenderer.DisposeBuffers()` then nulled `_sampleRingBuffer` unconditionally, losing the managed owner and the later retry route for the retained raw pointers.

Solution: Add `AudioFrameSpscRingBuffer.TryDispose()` as the authoritative teardown contract. It returns false when H8Memory is still initialized and native clear did not prove descriptor release; `Dispose()` remains a compatibility wrapper. `Initialize()` now stops on `!TryDispose()`, so it cannot allocate or rebind over native-retained raw pointers. The renderer keeps `_sampleRingBuffer` until `TryDispose()` succeeds, and `EnsureBuffers()` validates the ring capacity/stereo source-channel contract after initialization before setting `_buffersInitialized`.

Rejected Alternatives: Rejected force-free because a callback may already have copied the descriptor. Rejected a blocking wait/spin in managed teardown because native clear already reports Busy and the audio thread cannot be stalled. Rejected nulling the owner after failed dispose because it converts a protected retention state into a raw-memory leak and removes forensic telemetry retry.

Scalability potential: Low/MX350/Quest avoid a native lifecycle leak after a teardown race. Middle/High/Ultra keep identical steady-state DSP behavior; the only added work is cold-path status propagation.

Hardware Impact: 0 us/frame. Cold teardown/reinit adds one bool branch and avoids a retained raw bridge becoming unreachable.

Proof State: STATIC_DEFERRED_CLEAR_OWNER_RETAINED_PASS. Both JSON reports parse, ring/bridge strict runtime forbidden-token scan is clean, deferred-clear scanner guards are present, and no Unity compile, native rebuild, player build, or fuzzer was run.

Known Debt: Full `PlayerCriticalProceduralAudioRenderer.cs` is not Zero-GC clean as a whole. Existing cold dump methods still use `System.IO`/`FileStream`/`BinaryWriter`, the producer owns a managed `Thread`, and the file has pre-existing arrays/value-type `new` tokens. This pass did not rewrite that monolith because the current defect was bridge raw-buffer ownership, but reports now state the limitation explicitly.

## R54 - Windows Native Dump Thread Must Use CRT Thread Entry

Problem: The native fault-dump queue used `CreateThread` on Windows for `TelemetryDumpThreadMain`, but that thread entry calls C runtime file I/O (`fopen_s`, `fwrite`, `fclose`). Microsoft documents `_beginthread`/`_beginthreadex` in `<process.h>` as the CRT thread-start route. A raw Win32 thread here was a release-quality defect in the fault path even though it was outside the DSP hot loop.

Solution: Add Windows `<process.h>` and `<stdint.h>`, change the Windows dump thread entry to `static unsigned __stdcall TelemetryDumpThreadMain(void*)`, and start it with `_beginthreadex(NULL, 0, TelemetryDumpThreadMain, NULL, 0, NULL)`. Close the returned handle after successful queue. Keep POSIX on `pthread_create`. Add scanner guards requiring `_beginthreadex` and rejecting the old `CreateThread(NULL, 0, TelemetryDumpThreadMain` route.

Rejected Alternatives: Rejected synchronous native disk write from `HectonSensoryKernel_DumpAudioBridgeTelemetry` because the managed bridge should only queue the native dump. Rejected managed `Thread`/`FileStream`/`Path` because the fault route must stay outside managed allocation. Rejected leaving `CreateThread` with CRT calls because the dump writer is intentionally using C runtime I/O.

Scalability potential: Low/MX350/Quest pay 0 us/frame because this is fault-only. Middle/High/Ultra keep the same fixed 19,216-byte dump schema and can build richer postmortem tooling without changing the runtime bridge DTOs.

Hardware Impact: 0 us/frame. Fault-only Windows path avoids CRT thread-state risk without adding locks, heap allocation, or managed involvement. Static proof only; native rebuild/runtime dump completion is still required.

Proof State: STATIC_WINDOWS_CRT_DUMP_THREAD_PASS. `JSON_PARSE_OK`; `WINDOWS_CRT_DUMP_THREAD_GUARDS_PRESENT`; strict ring/bridge forbidden-token scan clean; native heap/format/raw-thread scan clean; no Unity compile, native rebuild, player build, dotnet build, or fuzzer was run.

## R55 - Managed Dump Gate Must Rearm After Every Native Queue Attempt

Problem: `NativeAudioFrameRingBuffer.RequestTelemetryDump` used `_telemetryDumpQueued` as a one-shot managed gate. It reset that gate only when `HectonSensoryKernelNativeBridge.TryDumpAudioBridgeTelemetry` returned false. Since the native export copies the full 19,216-byte snapshot into fixed static scratch before returning success, keeping the managed gate set after success suppresses every later NaN/shared-state/bridge-failure dump for the rest of the session.

Solution: Scope `_telemetryDumpQueued` to managed snapshot construction only. `RequestTelemetryDump` now sets the gate with `Interlocked.CompareExchange`, performs the fixed snapshot write, optional DataVault mirror, and native queue attempt inside `try`, then resets the gate in `finally` with `Volatile.Write`. The native `g_telemetryDumpInUse` gate remains the writer serialization owner.

Rejected Alternatives: Rejected leaving the C# gate set until process shutdown because it destroys black-box postmortem coverage after the first accepted fault dump. Rejected adding a managed completion callback, managed file status, or polling thread because that would introduce managed allocation/scheduling risk in the fault route. Rejected synchronous native disk write from C# because the export boundary must stay queue-only.

Scalability potential: Low/MX350/Quest keep deterministic fault capture without any frame-time cost. Middle/High/Ultra keep the same binary dump schema and can tolerate repeated postmortem dumps across long sessions without changing DTO layout or GlobalDataVault lanes.

Hardware Impact: 0 us/frame. The added `try/finally` is fault-only. Normal DSP/sample-write paths are unchanged. Repeated fault storms may rebuild the fixed 19,216-byte snapshot while the native writer is busy, but native returns fail-closed through `g_telemetryDumpInUse` and no managed heap/I/O/thread route is introduced.

Proof State: STATIC_DUMP_GATE_REARM_PASS. `FINAL_JSON_PARSE_OK`; `DUMP_GATE_REARM_METHOD_SCAN_OK`; strict ring/bridge forbidden-token scan clean; native heap/format/raw-thread scan clean; stale dump-gate text absent except the intentional scanner negative guard; no Unity compile, native rebuild, player build, dotnet build, or fuzzer was run.

## R56 - POSIX Dump Thread Detach Must Be a Scanner-Enforced Contract

Problem: The current native source already detaches the non-Windows dump writer with `pthread_detach(threadHandle);`, but the scanner/report proof only enforced `_beginthreadex` on Windows and absence of raw `CreateThread`. The POSIX side could regress to a joinable `pthread_create` resource leak and still pass the 1314 static gate.

Solution: Add a scanner guard requiring `pthread_detach(threadHandle);` in `Plugin_HectonSensoryKernel.cpp`, and record the detach route in both 1314 JSON reports. This is a proof-layer repair, not a runtime code change, because the source implementation was already correct.

Rejected Alternatives: Rejected rewriting the dump worker route because the native code already starts, detaches, and serializes with fixed static scratch. Rejected leaving the detach requirement as human memory because the project requires one fact -> one proof artifact. Rejected moving POSIX dump writing to managed threads or managed I/O because that would violate the fault-route isolation rule.

Scalability potential: Low/MX350/Quest pay 0 us/frame. Linux/macOS/Android fault dumps remain bounded and detached; Middle/High/Ultra get the same repeated-dump postmortem route without accumulating joinable thread resources.

Hardware Impact: 0 us/frame. Editor/static guard only. Fault-only native path is unchanged.

Proof State: STATIC_POSIX_DUMP_THREAD_DETACH_GUARD_PASS. JSON parse clean; scanner guard present; source order proves `pthread_create` success path reaches `pthread_detach` before returning success; no Unity compile, native rebuild, player build, dotnet build, or fuzzer was run.

## R57 - Dump Gate Proof Must Be Method-Scoped

Problem: The runtime `RequestTelemetryDump` implementation was correct after Loop 42, but the scanner proof was weaker than the contract. It accepted file-level `finally` and `Volatile.Write(ref _telemetryDumpQueued, 0);` tokens, so a future edit could move the rearm outside `RequestTelemetryDump` and still satisfy the static gate.

Solution: Add method-body scanner helpers `AssertMethodContains` and `AssertMethodOrder`. The scanner now proves `RequestTelemetryDump` itself contains the `try` scope, that the native queue call occurs before `finally`, and that `finally` occurs before `_telemetryDumpQueued` is reset. Both JSON reports record this as `managedGateScannerProof`.

Rejected Alternatives: Rejected keeping global token checks because one fact -> one owner -> one proof requires the dump method to own its fail-closed gate. Rejected adding runtime code churn because the current method body already has the correct queue/finally/rearm order. Rejected build execution because this was a scanner/report proof repair and the user explicitly asked to avoid frequent dotnet/build runs.

Scalability potential: Low/MX350/Quest pay 0 us/frame because this is editor/static only. Middle/High/Ultra keep the same fixed native dump route; future fault telemetry regressions are caught before platform packaging.

Hardware Impact: 0 us/frame. No runtime code changed. Editor scanner gains two small string-order checks.

Proof State: STATIC_METHOD_SCOPED_DUMP_GATE_GUARD_PASS. JSON parse clean; scanner guard IDs present; `RequestTelemetryDump` method-order scan passes; strict runtime forbidden-token scan clean; native heap/format/raw-thread scan clean; no Unity compile, native rebuild, player build, dotnet build, or fuzzer was run.

## R58 - Packaged Windows DLL Must Carry PluginImporter Metadata

Problem: `NativePluginMatrixValidator` now rejects raw native files without `PluginImporter`, but `Assets/Plugins/x86_64/HectonAudioKernel.dll.meta` still had only `fileFormatVersion` and `guid`. That meant the source-level preflight rule was correct while the packaged Windows DLL remained invalid until Unity import metadata was repaired.

Solution: Preserve the existing GUID and add a Unity `PluginImporter` body. Disable `Any`, enable Windows editor loading with `OS: Windows` and `CPU: x86_64`, and enable `Standalone: Win64` with `CPU: x86_64`. Extend `OOP_AudioBridge_Scanner` to read the `.meta` file directly and require `PluginImporter:`, `Standalone: Win64`, `CPU: x86_64`, and `OS: Windows`.

Rejected Alternatives: Rejected trusting DLL file presence because the build matrix explicitly validates `AssetImporter.GetAtPath(assetPath) as PluginImporter`. Rejected enabling all standalone platforms because this binary is Windows-only and Linux/macOS need their own native artifacts. Rejected running Unity reimport/build here because the user asked to avoid frequent build/compile work and this was a static metadata repair.

Scalability potential: Low/MX350 Windows gets deterministic native DLL import routing instead of a silent managed-only fallback risk. Middle/High/Ultra keep the same bridge ABI; richer audio quality does not change plugin identity or descriptor layout. Android/Quest remains blocked until an arm64 `.so` exists and is imported for Android.

Hardware Impact: 0 us/frame. This is build/import metadata only.

Proof State: STATIC_WINDOWS_PLUGIN_IMPORTER_META_PASS. `FINAL_JSON_PARSE_AFTER_META_WHITESPACE_OK`; `FINAL_WINDOWS_AUDIO_KERNEL_PLUGIN_IMPORTER_META_PRESENT`; `FINAL_WINDOWS_META_SCANNER_GUARDS_PRESENT`; strict ring/bridge forbidden-token scan clean; native heap/format/raw-thread scan clean; current reports no longer claim the Windows packaged DLL metadata is GUID-only; `git diff --check` has no whitespace errors after YAML empty-value cleanup, only LF-to-CRLF warnings. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R59 - Synthesis Managed Callbacks Are Adjacent 1308 Debt, Not a 1314 Patch

Problem: A broad audio-domain scan found managed `OnAudioFilterRead` callbacks in `VocalBankPlaybackRuntime.cs` and `DynamicMusicGranularSynthesizer.cs`. These are real mandate risks against the native/DSP synthesis direction, but they are owned by the 1308 synthesis batch, not by the 1314 master-bus bridge. `Status_1308.md` already lists those files, the callback routes, and a pending Unity runtime GC probe.

Solution: Do not perform a destructive 1314 edit against 1308-owned output code. Record the debt and keep 1314 changes constrained to the bridge/native plugin/importer proof surface. The correct repair is an owner-local 1308 migration from managed `OnAudioFilterRead` transfer to a native/DSP output route that preserves vocal bank and dynamic music output.

Rejected Alternatives: Rejected deleting or emptying the callbacks because that is a silence bug, not a release repair. Rejected routing them through the 1314 master-bus bridge in this pass because it would create a cross-domain dependency without a 1308 route card, capacity budget, DTO contract, and runtime audio proof. Rejected pretending the broad scan was clean.

Scalability potential: Low/MX350/Quest need the 1308 route migrated before platform release claims. Middle/High/Ultra can spend more cycles on richer synthesis only after the output transport is native/DSP owned and measurable. 1314 remains stable because bridge DTO layout and plugin import metadata are unchanged by this boundary decision.

Hardware Impact: 0 us/frame from this 1314 pass. The debt itself remains a potential audio-thread managed callback cost until 1308 migrates it.

Proof State: STATIC_ADJACENT_SYNTHESIS_DEBT_RECORDED. Source lines read directly; no source mutation outside 1314 bridge/importer surface; no dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R60 - Windows PluginImporter Proof Must Be Section-Scoped

Problem: The packaged Windows DLL `.meta` was repaired, but the scanner accepted loose file-level tokens. A future `.meta` could contain `Standalone: Win64`, `CPU: x86_64`, and `OS: Windows` while Win64 was disabled, Any was enabled, or the tokens belonged to the wrong platformData section.

Solution: Add `AssertPluginMetaSectionContains` to `OOP_AudioBridge_Scanner.cs`. It normalizes line endings, extracts a single PluginImporter platformData block, and checks required tokens inside that block only. The scanner now proves Any disabled, Windows Editor enabled for OS Windows/CPU x86_64, Win32/Linux/macOS disabled, and Win64 enabled for CPU x86_64. Both 1314 JSON reports record the section-scoped proof.

Rejected Alternatives: Rejected loose token proof because it does not prove importer lane state. Rejected changing the `.meta` again because the current metadata already has the correct lanes. Rejected Unity reimport/build because this was a static proof repair and the user explicitly asked to run build tooling rarely.

Scalability potential: Low/MX350 Windows avoids accidental fallback to unsupported or wrong-architecture plugin import. Middle/High/Ultra use the same DLL identity and native bridge ABI; richer audio fidelity does not change platform importer truth. Android/Quest remains blocked until the arm64 `.so` exists and has its own PluginImporter proof.

Hardware Impact: 0 us/frame. Editor/static scanner only; no runtime path, DTO, descriptor, or native callback changed.

Proof State: STATIC_WINDOWS_PLUGIN_IMPORTER_SECTION_PROOF_PASS. JSON reports parse; Windows `.meta` section verifier passes; scanner guard tokens are present; strict runtime ring/bridge forbidden-token scan is clean; native heap/raw-thread scan is clean; no dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R61 - Packaged DLL Freshness And Mixer Effect Invocation Are Release Blockers

Problem: The current Unity-loadable Windows binary is stale. `Assets/Plugins/x86_64/HectonAudioKernel.dll` is dated `2026-04-24T18:13:13.9632833Z`, while `NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp` is dated `2026-05-27T09:57:55.5551300Z` and `BuildHectonSensoryKernel.bat` is dated `2026-05-27T06:32:46.7320745Z`. A second blocker exists in the mixer asset: `Assets/_Project/MasterMixer.mixer` has empty `m_Effects` lists and no `Hecton Sensory Kernel` effect entry. The bridge can register a descriptor and still produce no native audio if Unity never instantiates the effect.

Solution: Add stale-binary freshness checks to `OOP_AudioBridge_Scanner.cs` so the report fails until the DLL timestamp is newer than both native source and build script. Patch `AudioMixerSanitizer.cs` so it removes only unresolved empty-effect-id mixer references and cannot delete a valid Hecton native effect by display name. Add `AudioMixerNativeEffectBuildGate` to fail player builds when MasterMixer lacks the Hecton native effect. Mark both 1314 JSON reports as failed static state with three explicit blockers.

Rejected Alternatives: Rejected claiming source-level native fixes as runtime proof while Unity still loads a month-old DLL. Rejected launching the native build now because CPU load samples were 71% then 100%, above the project limit of 50% for compiler/build work. Rejected manual YAML insertion of `AudioMixerEffectController` because AudioMixer effect subassets are internal Unity serialization; a bad hand-written subasset can corrupt the mixer. Rejected deleting or bypassing the sanitizer because the correct fix is to stop destructive name-based removal and add an enforceable build gate.

Scalability potential: Low/MX350/Quest get deterministic fail-fast packaging instead of silent managed/no-audio fallback. Middle/High/Ultra keep the same descriptor ABI and can spend quality weight on richer DSP only after the DLL and mixer effect path are real. The solution scales by proof route, not by runtime branches: all tiers need one correct native binary and one authored mixer hook.

Hardware Impact: 0 us/frame. The sanitizer/build gate/scanner are editor/build-time only. Runtime performance does not change; release correctness improves by preventing stale binary and missing callback invocation from shipping.

Proof State: STATIC_BLOCKERS_FOUND_AND_GATED. Timestamp proof: DLL stale vs native source and build script. Mixer proof: MasterMixer has empty effect lists. Build/native rebuild not run because CPU load was above the project gate.

## R62 - Stale Windows Audio DLL Must Fail Player Build Preflight

Problem: Loop 48 made the static scanner fail on stale `Assets/Plugins/x86_64/HectonAudioKernel.dll`, but the actual player build preflight still only checked file presence and PluginImporter compatibility. A present/imported stale DLL could therefore pass `NativePluginMatrixValidator` even though Unity would load a binary built before the current C++ ABI, callback bounds, static assertions, native dump route, and build-script exclusion changes.

Solution: Add `RequirePluginFreshness` to `NativePluginMatrixValidator`. Windows x64 builds now compare `Assets/Plugins/x86_64/HectonAudioKernel.dll` against `NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp` and `NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernel.bat` with `File.GetLastWriteTimeUtc`. If the DLL is older than either reference, the validator appends a stale-plugin blocker and strict player builds throw `BuildFailedException`.

Rejected Alternatives: Rejected relying on `OOP_AudioBridge_Scanner` alone because scanner execution is manual/editor-menu driven, while `IPreprocessBuildWithReport` is the enforced player-build lane. Rejected auto-rebuilding the DLL inside Unity preflight because build tools are platform/toolchain dependent and the project rule currently forbids build/compiler work above 50% CPU. Rejected weakening the blocker to a warning because stale native ABI is a release stop, not a quality downgrade.

Scalability potential: Low/MX350/Quest get deterministic fail-fast packaging instead of undefined native bridge behavior. Middle/High/Ultra use the same native binary freshness route; visual/audio quality scaling must happen after one correct ABI binary exists.

Hardware Impact: 0 us/frame. This is build-time timestamp validation only. It prevents stale native audio binaries from shipping on Windows without adding runtime branches, allocations, or callback work.

Proof State: STATIC_PLAYER_BUILD_FRESHNESS_GATE_PASS. JSON reports parse; `NativePluginMatrixValidator.cs` contains `RequirePluginFreshness`, source/build-script references, UTC timestamp probes, and stale-DLL blocker text; `OOP_AudioBridge_Scanner.cs` enforces those tokens; current timestamp proof still shows DLL stale vs source and build script. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R63 - Native Plugin Matrix File Probes Must Not Depend On Current Directory

Problem: `NativePluginMatrixValidator` mixed Unity asset-path APIs with raw relative `File.Exists` and timestamp probes. This usually works when the Unity editor process current directory is the project root, but build preflight should not depend on that ambient process state. A different CI/editor launch directory could report false missing native plugins or skip freshness proof incorrectly.

Solution: Add `AssetFileExists` and `ToProjectAbsolutePath`. File existence and timestamp checks now resolve paths from `Application.dataPath/..`, while `AssetImporter.GetAtPath` still receives the unchanged Unity asset path required by the importer API.

Rejected Alternatives: Rejected leaving the relative probes because the validator is a hard build gate, not an advisory script. Rejected passing absolute paths to `AssetImporter.GetAtPath` because Unity importer lookup requires asset paths. Rejected broad platform matrix refactor because the defect was a narrow path-resolution contract.

Scalability potential: Low/MX350/Quest and CI builds get the same deterministic plugin matrix result regardless of editor launch directory. Higher tiers do not change runtime behavior; the benefit is reliable packaging proof.

Hardware Impact: 0 us/frame. Build-time only; extra path normalization is cold editor work.

Proof State: STATIC_PROJECT_ROOT_FILE_PROBE_PASS. `NativePluginMatrixValidator.cs` contains `AssetFileExists`, `ToProjectAbsolutePath`, and timestamp calls through absolute project-root paths; scanner enforces those tokens; JSON reports parse. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R64 - Native Callback Must Reject Corrupt Input Channel Contracts Before Bridge Mixing

Problem: `ProcessCallback` validated output product and output channel caps, then cleared output to silence. It only copied passthrough input when `inbuffer != NULL && inchannels > 0 && inchannels <= kMaxProcessChannels`, but a corrupt host input contract outside that range could still proceed into bridge mixing. That made invalid host metadata indistinguishable from a silent-input/no-input case.

Solution: Add a fail-closed input contract gate immediately after bounded silence clear and output bounds validation: `inchannels > kMaxProcessChannels || (inbuffer != NULL && inchannels <= 0)`. The callback records `SharedStateInvalid` and returns before callback-depth increment or ring mixing. The debug native process export remains valid because it passes `inbuffer == NULL` and `inchannels == 0`.

Rejected Alternatives: Rejected silently ignoring invalid input channels because corrupted host callback metadata must not continue into the shared native bridge. Rejected moving the check before `memset` because bounded silence on known-valid output is the safer audible failure. Rejected treating `inbuffer == NULL && inchannels == 0` as invalid because the existing debug export intentionally uses that route.

Scalability potential: Low/MX350/Quest get deterministic silence on corrupted callback metadata instead of undefined bridge mixing. Middle/High/Ultra keep the same callback ABI and descriptor layout; quality scaling is unchanged.

Hardware Impact: Normal path adds one integer branch per native audio callback, below measurable frame cost. No heap, no managed route, no DTO change, no additional thread or I/O.

Proof State: STATIC_NATIVE_INPUT_CONTRACT_FAIL_CLOSED_PASS. Order proof shows bounded output clear before input-contract rejection and rejection before `AtomicIncrement32(&g_processCallbackDepth)`; scanner guard `native_callback_input_contract_fail_closed` is present; both JSON reports parse; strict runtime C# forbidden-token scan remains clean. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R65 - Heap-Using Unity Sample Utility Must Not Remain Beside the Hecton Native Kernel

Problem: `BuildHectonSensoryKernel.bat` already excluded `AudioPluginUtil.cpp`, but the files still existed beside the active kernel source. `AudioPluginUtil.cpp` contained multiple heap routes (`new[]`, `delete[]`) plus an alternate `UnityGetAudioEffectDefinitions` driven by `PluginList.h`. A future wildcard native build or convenience project file could silently reattach that route.

Solution: Remove `AudioPluginUtil.cpp`, `AudioPluginUtil.h`, and `PluginList.h` from `NativeAudio/HectonSensoryKernel`. Keep `AudioPluginInterface.h` as the API contract and keep `Plugin_HectonSensoryKernel.cpp` as the only `.cpp` effect export owner. Add scanner guards proving those three utility files stay absent.

Rejected Alternatives: Rejected relying only on `.bat` exclusion because source adjacency is enough for a future regression. Rejected editing the sample utility to remove heap helpers because HectonSensoryKernel has zero parameters and does not need the Unity analyzer/FFT helper layer. Rejected keeping `PluginList.h` because it described a legacy macro registration path no longer used by the local fixed-storage export.

Scalability potential: Low/MX350/Quest avoid accidental heap-heavy utility linkage in native audio builds. Middle/High/Ultra keep the same fixed native bridge ABI; richer DSP can be added in `Plugin_HectonSensoryKernel.cpp` without resurrecting the sample utility path.

Hardware Impact: 0 us/frame. Source-tree contraction only. Build risk decreases; runtime callback path is unchanged.

Proof State: STATIC_NATIVE_SAMPLE_UTILITY_REMOVED_PASS. The three utility files are absent; scanner guards are present; both JSON reports parse; native kernel `.cpp/.h/.bat` heap-token scan is clean and only one `.cpp` defines `UnityGetAudioEffectDefinitions`. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R66 - Native Register/Clear Must Not Disable an Owned Descriptor Before Drain Succeeds

Problem: Native register/clear wrote `g_hasSharedRingBuffer=0` before callback depth drained. If `WaitForProcessCallbacksToDrain` timed out, the old descriptor remained in `g_sharedRingBuffer` and the managed owner correctly retained raw buffers, but `ProcessCallback` could no longer see the descriptor because `g_hasSharedRingBuffer` was already false. That is silent audio loss and an owner-retention contradiction.

Solution: Add `g_callbackMutationGate`. Register/clear set this gate before drain; `ProcessCallback` increments depth and exits before bridge mixing while the gate is set. `g_hasSharedRingBuffer` is zeroed only after drain succeeds. Drain failure calls `RestoreStatusAfterDrainFailure`, which reopens the mutation gate and restores `Active` if an old descriptor is still owned, otherwise `Cleared`.

Rejected Alternatives: Rejected leaving `Busy` sticky because it disables old audio with no recovery path. Rejected clearing old descriptor on failed register/clear because managed `TryDispose` intentionally retains raw buffers when native clear cannot be proven. Rejected an unbounded wait because callback drain must remain bounded on audio hosts.

Scalability potential: Low/MX350/Quest avoid silent master-bus dropout during unlucky register/clear timing. Middle/High/Ultra keep the same descriptor ABI and can still scale DSP quality independently. The mutation gate is continuous-quality agnostic; it protects ownership, not fidelity.

Hardware Impact: Normal callback path adds one atomic read and branch before bridge mixing. Register/clear remain cold. No heap, no managed route, no DTO layout change.

Proof State: STATIC_NATIVE_MUTATION_GATE_PASS. Order proof shows mutation gate before drain and `g_hasSharedRingBuffer=0` only after drain in register/clear; scanner guards are present; JSON reports parse; native heap scan stays clean. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R67 - Register Drain Failure Must Not Look Like New Descriptor Success

Problem: The mutation gate preserved old native ownership on drain failure, but restoring plain `Active` created a false-positive path: managed `TryRegister` accepted any `Active` status as success, so a retained old descriptor could be reported as if the new descriptor registered.

Solution: Mark drain failure as `Active|Busy` when an old descriptor remains, or `Cleared|Busy` when none exists. Update managed `TryRegister` to require `Active` and reject `Busy`. The old descriptor can continue after callbacks resume, but the attempted registration returns false.

Rejected Alternatives: Rejected restoring plain Active because it conflates old ownership with new registration. Rejected clearing the descriptor to avoid ambiguity because that reintroduces the owner-retention bug. Rejected adding a new ABI status bit because `Busy` already represents an incomplete register/clear mutation and does not change DTO layout.

Scalability potential: All device tiers get deterministic registration truth without ABI expansion. Low/MX350/Quest avoid silent native/managed state mismatch; high tiers keep the same quality scaling path.

Hardware Impact: Register path adds one managed status-bit test. Native callback hot path is unchanged from Loop 53.

Proof State: STATIC_REGISTER_BUSY_REJECT_PASS. Native restore writes `restoredStatus | kStatusBusy`; managed register requires Active without Busy; scanner guard `bridge_register_rejects_busy` is present; reports parse; strict runtime forbidden-token scan is clean. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R68 - Failed Register Attempts Must Not Cleanup-Clear Existing Ownership

Problem: `TryRegisterWithRetryGate` still called `TryClear(out _)` after all failed attempts. After the native mutation-gate repair, a failed register can intentionally preserve an old descriptor and return `Active|Busy`. The final cleanup-clear could then clear that preserved descriptor if drain succeeded on the cleanup call.

Solution: Remove the final cleanup-clear from register failure. Register failure now returns false with the latest status. Clearing a native descriptor requires an explicit `TryClear` call by the owner path.

Rejected Alternatives: Rejected keeping cleanup-clear for "safety" because native register no longer leaves a half-written descriptor on failure; it either succeeds after drain or preserves old ownership. Rejected conditional clear on non-Busy statuses because native validation failure also happens before descriptor replacement now. Rejected a new ABI status bit because explicit operation separation is cleaner.

Scalability potential: All tiers keep deterministic descriptor ownership. Low/MX350/Quest avoid audio dropout during register races. Higher tiers keep the same bridge ABI and can scale DSP quality independently.

Hardware Impact: 0 us/frame. Cold register path removes one possible native clear call on failure.

Proof State: STATIC_REGISTER_FAILURE_NO_CLEANUP_CLEAR_PASS. Method-scoped scan proves `TryRegisterWithRetryGate` no longer contains `TryClear(out _)`; scanner guard is present; reports parse; strict runtime forbidden-token scan remains clean. No dotnet build, Unity compile, native rebuild, player build, or fuzzer was run.

## R69 - Native Operation Status Must Be Returned Directly, Not Sampled After Callback Writes

Problem: `TryRegister` and `TryClear` called a void native mutation export, then read `HectonSensoryKernel_GetSharedRingBufferStatus()`. `ProcessCallback` writes `kStatusActive` during normal mixing. That means a failed register/clear result could be overwritten by a callback before managed code sampled status, especially after the mutation-gate owner-retention work.

Solution: Add `HectonSensoryKernel_RegisterSharedRingBufferAndGetStatus` and `HectonSensoryKernel_ClearSharedRingBufferAndGetStatus`. Both call the same native operation body and return the exact mutation status. Keep the old void exports as compatibility wrappers, but managed P/Invoke now calls the new status exports. A stale DLL that lacks the new exports fails closed through `EntryPointNotFoundException` and `HectonNativeBridge.MarkUnavailableFromException` instead of returning undefined data from a void ABI.

Rejected Alternatives: Rejected changing the old void export to `int` under the same symbol because the checked-in Windows DLL is stale and would produce undefined return values before rebuild. Rejected a separate managed `GetStatus` polling retry because callback health status and mutation result are different facts.

Scalability potential: Low/MX350/Quest get deterministic operation truth without per-frame work. Middle/High/Ultra keep the same bridge ABI behavior and can scale DSP fidelity only after one correct native binary exists.

Hardware Impact: 0 us/frame. Cold register/clear now avoid a second native status call and remove a status race.

Proof State: STATIC_DIRECT_OPERATION_STATUS_PASS. Scanner guards require the new exports and managed direct status cast; stale Windows DLL remains a hard blocker until rebuild.

## R70 - Detached Native Dump Thread Was A Plugin Lifetime Risk

Problem: The native dump path used static handoff bytes plus a detached `_beginthreadex`/`pthread_create` worker. That avoided inline I/O but left code/data lifetime coupled to a detached thread that can outlive plugin unload or editor shutdown. Queue acceptance also did not prove disk write completion.

Solution: Remove `g_telemetryDumpBuffer`, `g_telemetryDumpInUse`, `g_telemetryDumpBytes`, `TelemetryDumpThreadMain`, `_beginthreadex`, and `pthread_create`. `HectonSensoryKernel_DumpAudioBridgeTelemetry` now returns `WriteTelemetryDumpFile(bytes, byteCount)` directly. C# still builds the 19,216-byte dump snapshot in fixed H8Memory raw bytes and rearms `_telemetryDumpQueued` in `finally` after the native write attempt.

Rejected Alternatives: Rejected keeping the detached thread and documenting it because plugin unload lifetime is not release-proof. Rejected moving disk I/O back to managed `Thread`/`FileStream` because that violates the 1314 Zero-GC fault-route demand. Rejected heap-backed native queues because the dump is fixed-size and fault-only.

Scalability potential: Low/MX350/Quest avoid detached fault-thread scheduler/lifetime risk. Higher tiers can add editor-only richer crash export tooling, but the runtime bridge keeps one fixed binary dump route.

Hardware Impact: 0 us/frame. Fault path performs one bounded 19,216-byte C file write when a dump is requested.

Proof State: STATIC_NATIVE_DUMP_THREAD_REMOVED_PASS. Native heap/thread token scan returned no `QueueTelemetryDumpAsync`, `TelemetryDumpThreadMain`, `g_telemetryDump`, `_beginthreadex`, `pthread_create`, malloc/free, `new[]`, or sample utility route.

## R71 - Callback And Descriptor Capacity Must Share One Upper Bound

Problem: Native descriptor validation accepted any power-of-two `capacityFrames` with a matching mask. A corrupt descriptor could claim a huge frame capacity and still pass validation, while `ProcessCallback` only bounded the process block, not the descriptor allocation contract.

Solution: Add `MaximumCapacityFrames = 65536` to the managed descriptor contract and reject higher capacities in `IsDescriptorValid`. Native validation now rejects `descriptor.capacityFrames > kMaxProcessFrames`. `ProcessCallback` also rejects `length == 0`, `length > kMaxProcessFrames`, `outchannels <= 0`, and `outchannels > kMaxProcessChannels` before sample-count math and before output clear.

Rejected Alternatives: Rejected relying on ring initialization clamping only because P/Invoke descriptor validation must defend against corrupt/native caller inputs too. Rejected clearing up to the maximum sample budget before rejecting impossible channel counts because corrupt host contracts should fail closed before buffer writes.

Scalability potential: One capacity budget applies on all tiers. Low tier and Quest cannot accidentally allocate or validate absurd rings; high-tier fidelity must scale via DSP quality, not descriptor size inflation.

Hardware Impact: One branch in native callback and one cold descriptor validation branch. No heap, no DTO size change, no per-sample cost.

Proof State: STATIC_CAPACITY_CONTRACT_PASS. Scanner guards require managed/native max capacity checks and callback precheck ordering.

## R72 - Mixer And Platform Packaging Proof Must Be Concrete, Not Textual

Problem: The scanner only searched raw mixer text for `Hecton Sensory Kernel`, which could false-pass without a real `AudioMixerEffectController` and non-empty `m_EffectID`. Subagent audit also confirmed Android arm64 audio kernel/LZ4 binaries are absent and Windows `liblz4.dll.meta` is GUID-only.

Solution: Replace the raw mixer token proof with `AudioMixerSanitizer.HasMixerEffectAtPath(MasterMixerPath, KernelEffectName)`. Add scanner blockers for missing Android arm64 `libHectonAudioKernel.so`, missing Android arm64 `liblz4.so`, and GUID-only Windows LZ4 metadata. Keep current reports failed until these assets are rebuilt/imported/authored through Unity.

Rejected Alternatives: Rejected hand-editing `MasterMixer.mixer` YAML because Unity owns AudioMixer effect subassets. Rejected treating Windows-only DLL work as Quest-ready because the build matrix explicitly requires Android arm64 binaries.

Scalability potential: Low/MX350/Quest get fail-fast packaging instead of silent no-audio/no-native fallback. Higher tiers reuse the same proof route; quality scaling starts after the native callback path is actually invoked.

Hardware Impact: 0 us/frame. Editor/build-time validation only.

Proof State: STATIC_PACKAGING_BLOCKERS_GATED. Android native paths are absent, Windows LZ4 meta lacks `PluginImporter:`, MasterMixer has no concrete Hecton effect, and stale Windows audio DLL remains unresolved.

## R73 - Android Native Plugin Needs A Reproducible Build Route And Freshness Gate

Problem: The platform matrix already blocked Android builds when `libHectonAudioKernel.so` was absent, but there was no repo-owned Android arm64 build script and no freshness gate for a future packaged `.so`. A stale Android binary could satisfy file/importer presence while still carrying the old ABI, missing direct register/clear status exports, missing dump-thread removal, or old descriptor bounds.

Solution: Add `NativeAudio/HectonSensoryKernel/BuildHectonSensoryKernelAndroid.bat` as the explicit arm64 NDK route. It resolves NDK from argument, `ANDROID_NDK_ROOT`, `ANDROID_NDK_HOME`, `ANDROID_NDK`, or Unity Hub installs, uses `aarch64-linux-android24-clang++`, compiles only `Plugin_HectonSensoryKernel.cpp`, emits `Assets\Plugins\Android\arm64-v8a\libHectonAudioKernel.so`, and uses shared/PIC/hidden-visibility/dead-strip/no-exceptions/no-RTTI flags. `NativePluginMatrixValidator` now applies `RequireAnyCompatiblePluginFreshness` to Android audio-kernel candidates against both native source and the Android build script.

Rejected Alternatives: Rejected fake `.so` placeholders because Unity would treat that as packaging proof without runtime ABI proof. Rejected auto-running NDK build because user asked to avoid builds unless necessary and current shared machine already had active compiler load earlier. Rejected only documenting the missing Android binary because the build preflight needed a future stale-binary gate, not just current absence detection.

Scalability potential: Low/MX350/Quest get fail-fast packaging and a deterministic arm64 native route. Middle/High/Ultra keep the same ABI and build artifact route; quality scaling remains in DSP fidelity after the native plugin is actually built/imported.

Hardware Impact: 0 us/frame. This is source/build-time only. Quest avoids shipping a managed-only or stale-native audio bridge; no runtime branch or DTO layout change was added.

Proof State: STATIC_ANDROID_BUILD_ROUTE_AND_FRESHNESS_GATE_PASS. JSON reports parse with `filesScanned=12` and `failedChecks=6`; scanner tokens for arm64 clang, shared/PIC/hidden visibility, dead stripping, Android output binary, sample utility exclusion, and Android freshness gate are present. Runtime bridge forbidden-token scan and native heap/thread/sample-utility scan remain clean. No dotnet build, Unity compile, Android NDK build, native rebuild, player build, or fuzzer was run.
