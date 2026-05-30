# Rationale 1403 - MONOBEHAVIOUR_RESIDUAL_NATIVE_FIELD_PURGER

Status: PENDING VERIFICATION

## Decision 000 - Initialization

Problem: Assignment targets forbidden persistent native collection fields in MonoBehaviour classes. These fields can be tied to Unity object lifecycle instead of explicit native ownership.
Solution: Use a five-loop state machine: map fields, map lifecycle, isolate ownership, verify static field purge, then generate report artifacts.
Rejected Alternatives: Direct deletion without lifecycle map risks missed dispose paths and job use-after-free. Full solver/save rewrite exceeds the authorized memory ownership scope.
Scalability potential: Low keeps allocation ownership explicit and cheap; Middle/High/Ultra preserve current solver and save algorithm behavior while reducing lifecycle crash risk.
Hardware Impact: Static scan only; 0 runtime impact. Expected runtime gain is avoided leak/use-after-free risk, not frame-time optimization.

## Decision 001 - Treat Current State As Partial Purge

Problem: Raw scan shows all 36 native fields already moved out of direct MonoBehaviour class depth, but `SaveManagerNativeBufferSet`, `RigNativeBufferSet`, and `RuntimeNativeBufferSet` are passive containers. Allocation and disposal logic still lives mostly on the MonoBehaviour methods.
Solution: Preserve the existing ref facade pattern and move lifecycle ownership into `IDisposable` BufferSet owners without changing save serialization or IK solver math.
Rejected Alternatives: Stop after static zero direct-field scan; rejected because task requires wrapper disposal chains and sentinel ownership. Rewrite algorithms around GlobalDataVault; rejected because current arrays are owner-local scratch/state and the prompt forbids solver/serialization churn.
Scalability potential: Low and Middle keep deterministic cold allocation with no hot-path managed churn. High and Ultra retain existing IK overkill paths because data addresses and job inputs remain unchanged.
Hardware Impact: No intended frame-time gain. Expected benefit on i3/MX350 is lower scene-unload leak/use-after-free risk and fewer repeated persistent static buffer allocations during static save maintenance.

## Decision 002 - Static Save Buffers Need A Dedicated Island

Problem: Static save repair/load paths no longer borrow live instance arrays, but they allocated persistent fallback byte buffers per call. That avoids instance race but creates avoidable native allocation churn and weak static ownership evidence.
Solution: Extend `StaticNativeBuffers` with isolated raw/compressed byte buffer pairs, in-use guards, fallback owned pairs under contention, and explicit dispose/release routines.
Rejected Alternatives: Borrow `_savePayloadBuffer` and `_compressedSaveBuffer`; rejected due static/instance race. Single unlocked static byte buffer; rejected because concurrent static maintenance calls could corrupt reads/writes.
Scalability potential: Low uses one reusable static raw/compressed pair; Middle/High/Ultra get the same deterministic path because this is save maintenance, not visual fidelity.
Hardware Impact: Avoids repeated 64MB+ persistent allocation/free churn during static repair/metadata reads. Estimated low-end benefit: fewer native allocator stalls, no claimed frame-time measurement.

## Decision 003 - Wrapper Ownership Must Own Allocation And Disposal

Problem: Field relocation alone did not prove ownership because allocation and disposal code could still drift in MonoBehaviour methods.
Solution: Move persistent native allocation routines into `SaveManagerNativeBufferSet`, `RigNativeBufferSet`, and `RuntimeNativeBufferSet`; route shutdown through `IDisposable` wrappers while preserving `ref NativeArray<T>` facades.
Rejected Alternatives: Rewriting save binary DTOs or IK math around new handles; rejected because the task scope is memory ownership, not algorithm changes. Keeping direct MonoBehaviour disposal; rejected because ownership proof remains split.
Scalability potential: Low uses the same cold persistent buffers with explicit teardown. Middle/High/Ultra keep existing fidelity paths and do not pay extra per-frame managed work.
Hardware Impact: No claimed frame-time delta. Estimated i3/MX350 gain is reduced scene unload leak/use-after-free risk and less allocator churn during lifecycle transitions.

## Decision 004 - Static Save Buffers Use In-Use Guards Instead Of Instance Borrowing

Problem: Static save repair/read paths cannot safely borrow live instance buffers while async save work may own them.
Solution: `StaticNativeBuffers` now owns raw write, compressed write, and candidate scratch arenas with lock-protected in-use flags; binary read paths reuse the raw/compressed pair because they need both raw and compressed arenas. Contention receives owned fallback arrays that unregister and dispose on release.
Rejected Alternatives: Single global static arena without guards; rejected due corruption risk. Per-call persistent fallback only; rejected because it creates repeated large allocator traffic.
Scalability potential: Low gets deterministic static isolation with one reusable path; Middle/High/Ultra do not need a different method because this is persistence maintenance, not visual quality.
Hardware Impact: Avoids repeated 64MB raw plus 68MB compressed fallback allocations when static save routines are not contended. i3/MX350 impact: fewer native allocator stalls; exact microseconds not profiled.

## Decision 005 - Tests Are Explicit Because They Are Heavy

Problem: The required lifecycle and race tests intentionally create/destroy components and allocate large save buffers; running them unconditionally would violate the workstation protection rule.
Solution: Add editor-only NUnit tests with `[Explicit]` for the 10000-cycle lifecycle fuzzer and static race probe. Keep a non-explicit source-route IK test that is cheap and validates job payload facade use.
Rejected Alternatives: Claiming test execution without Unity Test Runner; rejected as fake proof. Running dotnet/Unity tests now; rejected because user decreed build/test CPU use only under extreme necessity.
Scalability potential: Low devices are unaffected because tests are editor-only. High/Ultra validation can run the explicit stress tests in isolated CI to prove overkill lifecycle churn.
Hardware Impact: Zero runtime impact. Test execution would allocate large persistence buffers by design; it is quarantined behind explicit invocation.

## Decision 006 - Build Skipped Under Resource Decree

Problem: A build would provide compiler proof, but the user explicitly restricted `dotnet build` to extreme necessity and static scans found no syntax-critical blocker.
Solution: Sample the build gate, record CPU/compiler state, and rely on static checks: brace balance after literal stripping, class-depth native field scan, stale helper scan, namespace scan, and hot-path allocation pattern scan.
Rejected Alternatives: Launching build because CPU was briefly below 50%; rejected because it was not necessary. Reporting compile success; rejected because no compiler was run.
Scalability potential: Low/Middle/High/Ultra code paths remain unchanged by this validation choice; only proof level is pending Unity compile/test.
Hardware Impact: Saved a full project build on the workstation. Exact microseconds saved are not measured; CPU gate sample was 18% with no active `dotnet` or `csc`.

## Decision 007 - Disposal Chains Must Be Best-Effort

Problem: A linear wrapper `Dispose()` can stop after the first `NativeArray.Dispose()` exception, leaving later arrays still registered and alive during scene unload.
Solution: Convert SaveManager, IK rig, and IK runtime wrapper disposal into best-effort loops that capture the first exception, attempt every remaining disposal, clear aliases, and throw only after the chain is drained.
Rejected Alternatives: Plain sequential disposal; rejected because one broken safety handle can mask leaks in later buffers. Swallowing exceptions; rejected because Integrator needs the first failure.
Scalability potential: Low devices avoid native leak cascades during fast scene churn. Middle/High/Ultra keep identical solver/save fidelity; only teardown reliability changed.
Hardware Impact: Runtime frame gain: 0 us measured. Scene-unload resilience improves on i3/MX350 by preventing one failed disposal from preserving tens of MB of persistent native memory.

## Decision 008 - SaveManager FileInfo Allocation Removed From Target Paths

Problem: APEX background scan found `new FileInfo(...)` in SaveManager persistence call sites, violating the no-reference-allocation proof requested for save background processing.
Solution: Replace the SaveManager call sites with `TryGetAbsoluteFileLength(...)`, routing through the existing `AsyncWriteManager.TryGetFileLength` abstraction so the target file no longer directly allocates `FileInfo`.
Rejected Alternatives: Leave local `FileInfo` because it is outside per-frame IK; rejected because the user explicitly included background SaveManager methods in the APEX scan. Rewrite `SaveBinaryStorage.AsyncWriteManager` now; rejected as outside Agent 1403 target scope and higher blast radius.
Scalability potential: Low/Middle reduce managed allocation pressure in SaveManager-local persistence flow. High/Ultra get no visual change; save identity and DTO layout remain untouched.
Hardware Impact: Exact microseconds saved: 0 measured. Known remaining outside-target risk: `Assets/_Project/Scripts/SaveBinaryStorage.cs:1056` still constructs `FileInfo` inside the shared storage layer.

## Decision 009 - APEX Build Gate Blocks Compilation

Problem: The final verification mandate asks for compilation proof, but AGENTS and the task decree forbid `dotnet build` when CPU exceeds 50%.
Solution: Resample CPU/compiler state before final proof. Latest sample: CPU 100%, `dotnet=0`, `csc=0`; build not launched. Status remains `PENDING_VERIFICATION`.
Rejected Alternatives: Running `dotnet build` under contention; rejected by explicit rule. Claiming compiler success from static scans; rejected as false evidence.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. This protects the shared workstation while preserving honest proof boundaries.
Hardware Impact: Avoided a heavy build under saturated CPU. Exact avoided duration and microseconds are not measured.

## Decision 010 - Storage File Length Must Avoid Runtime-Created FileInfo Risk

Problem: The previous APEX report correctly identified `SaveBinaryStorage.AsyncWriteManager.TryGetFileLength` as a transitive save dependency still using `new FileInfo`. That means the SaveManager-local patch removed the direct target-file allocation but did not remove the actual storage helper allocation for the active save path.
Solution: Replace the active Windows file-length path with native `CreateFileW` plus `GetFileSizeEx`, sharing read/write/delete so temp-save verification can inspect files while save maintenance owns them. Keep a non-Windows portable fallback through `File.Open` because this pass cannot prove a POSIX `stat` layout across every Unity target without a platform build.
Rejected Alternatives: Unity `AsyncReadManager.GetFileInfo`; rejected because Unity issue UUM-100207 reports runtime-created files can be reported absent in Unity 6000.x, which is exactly the save-file case. POSIX `stat` in this pass; rejected because struct layout varies by platform and no Android/macOS/Linux player build was available. Keeping `FileInfo`; rejected because the active Windows save path had a known reference allocation.
Scalability potential: Low/Middle Windows builds remove managed `FileInfo` object churn from save length verification. High/Ultra get identical save fidelity; atomic save identity and DTO layout remain unchanged.
Hardware Impact: Exact microseconds saved: 0 measured. Expected low-end impact is reduced managed allocation pressure during save verification on Windows. Non-Windows fallback remains PENDING runtime measurement.

## Decision 011 - Final Compilation Gate Still Blocked

Problem: After the SaveBinaryStorage dependency patch, syntax proof still needs a compiler, but the final sampled host state violates the project build throttle rule.
Solution: Record the final gate as CPU 100%, `dotnet=1`, `csc=0`; do not launch `dotnet build`; keep status `PENDING_VERIFICATION`.
Rejected Alternatives: Running a build while another dotnet process and saturated CPU are active; rejected by explicit rule. Treating static scans as compiler proof; rejected as false evidence.
Scalability potential: Runtime code behavior unchanged across Low/Middle/High/Ultra. Verification remains static-source only until the workstation is idle or CI runs Unity.
Hardware Impact: Avoided adding another compiler workload under saturated CPU. Exact avoided build duration is not measured.

## Decision 012 - Shutdown Must Drain Independent Owners Before Surfacing Fault

Problem: The previous best-effort disposal was local to each BufferSet. `SaveManager.ShutdownServiceState` could still stop between world pager, instance native buffers, static native buffers, and post-dispose state clears if one owner threw. `ContextualPhysicalIkRuntime.OnDestroy` could fail before clearing cached context and GlobalRegistry.
Solution: Capture the first disposal exception at the outer lifecycle boundary, attempt every independent owner cleanup, clear lifecycle aliases, then surface the first exception. Runtime IK now clears cached player/camera context and registry in `finally` even if buffer disposal faults.
Rejected Alternatives: Swallowing disposal exceptions; rejected because native safety faults must remain visible. Leaving sequential outer disposal; rejected because one owner fault could preserve another owner across scene unload.
Scalability potential: Low devices benefit most because fast scene churn and memory pressure expose teardown ordering defects. Middle/High/Ultra behavior is unchanged; this is lifecycle correctness, not fidelity.
Hardware Impact: Measured runtime microseconds saved: 0. Expected gain is reduced native memory retention risk during scene unload on constrained memory targets.

## Decision 013 - Struct Layout Proof Must Admit The Touched Telemetry DTO

Problem: The APEX report incorrectly said no struct layout diff was touched. The diff does change `AsyncPersistenceTelemetryEntry` from explicit size 32 to explicit size 64 with padding bytes.
Solution: Replace the false report text with a static offset proof: uint fields at offsets 0, 4, 8, 12, 16, 20, 24, 28; byte padding at offsets 32 through 63; total size 64; 64 is divisible by 8. Keep runtime `UnsafeUtility.SizeOf` status pending because no compiler/Unity run occurred.
Rejected Alternatives: Pretending the diff scanner returned no layout hits; rejected as false evidence. Running build solely for `UnsafeUtility.SizeOf`; rejected while CPU/compiler gate remains blocked.
Scalability potential: Low/Middle/High/Ultra all benefit from ARM64-aligned telemetry records; visual fidelity unchanged.
Hardware Impact: Measured runtime microseconds saved: 0. Expected gain is avoiding misaligned telemetry NativeArray records on ARM64/mobile targets.

## Decision 014 - Remove Dead Static Read Buffer API

Problem: The final APEX audit found that `StaticNativeBuffers.ReadBuffer`, `AcquireReadBuffer`, and `ReleaseReadBuffer` were not used by production SaveManager paths. The explicit race test exercised that dead route, making the proof weaker than the code path used by metadata/load repair.
Solution: Remove the standalone static read buffer API and update the explicit race probe to contend over the real raw/compressed static buffer pair.
Rejected Alternatives: Keeping the unused 64MB read-buffer path for symmetry; rejected because it created false evidence and a possible future allocation route with no current caller. Retrofitting read paths to use it; rejected because binary metadata/load reads require both raw and compressed arenas.
Scalability potential: Low avoids a dead 64MB allocation route. Middle/High/Ultra keep identical save fidelity; this only reduces proof surface and lifecycle ownership surface.
Hardware Impact: Measured runtime microseconds saved: 0. Expected low-end benefit is lower accidental native memory footprint if tests or future diagnostics touch static save buffers.

## Decision 015 - Recheck Build Gate After Dead-Route Patch

Problem: Removing the unused static read-buffer API changed runtime source and the editor test. Compiler proof would be valuable, but the host state again violates the explicit build throttle.
Solution: Re-sample the gate after source changes. Latest sample: CPU 100%, `dotnet=1`, `csc=0`. Do not launch `dotnet build`; keep status `PENDING_VERIFICATION`.
Rejected Alternatives: Running a build under saturated CPU and active dotnet; rejected by rule. Claiming compile success from static checks; rejected as false evidence.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra except the removed unused allocation route.
Hardware Impact: Avoided adding a second compiler workload under saturated CPU. Exact avoided duration is not measured.

## Decision 016 - Fix Editor Probe Name Binding

Problem: After the dead-route patch, the editor race probe used `nameof(AcquireWriteBuffers)` and `nameof(ReleaseWriteBuffers)` inside the test class. Those methods are private static methods on `SaveManager`, not symbols in the test class, so the test assembly would fail before reflection.
Solution: Replace those two `nameof` calls with exact private method-name strings consumed by `typeof(SaveManager).GetMethod(...)`.
Rejected Alternatives: Adding wrapper methods to the test class just to satisfy `nameof`; rejected because it hides the reflection target and adds no runtime proof. Running a build to discover the same compiler error; rejected by current CPU gate.
Scalability potential: Runtime behavior unchanged. Validation path is now less likely to fail before testing the real static buffer pair.
Hardware Impact: 0 runtime microseconds. Editor-only compile-surface correction.

## Decision 017 - Fallback Pair Allocation Must Be Failure-Atomic

Problem: `StaticNativeBuffers.AcquireWriteBuffers` allocated the contended raw fallback and then the compressed fallback. If the compressed allocation or sentinel registration failed, the raw fallback could be left allocated before ownership reached the caller.
Solution: Allocate fallbacks into local NativeArray values, track sentinel registration flags, and best-effort dispose any partial fallback buffers in the catch path before rethrowing the original allocation failure.
Rejected Alternatives: Leaving the partial-allocation leak as a low-memory edge case; rejected because weak devices are exactly where this path matters. Reusing the static pair during contention; rejected because it reintroduces aliasing.
Scalability potential: Low devices avoid a native-memory leak during allocator pressure. Middle/High/Ultra retain the same static fast path and save fidelity.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is bounded native memory after failed contended save-buffer acquisition.

## Decision 018 - File-Length Failure Path Must Not Format Strings

Problem: The Windows native file-length helper removed `FileInfo`, but its failure path still built interpolated strings with path and Win32 code data. That is cold, but it is still managed allocation pressure in persistence error handling.
Solution: Replace those file-length failure messages with static literals. Keep the active Windows path on `CreateFileW/GetFileSizeEx/CloseHandle`.
Rejected Alternatives: Keeping detailed interpolated errors; rejected because the APEX mandate is zero-allocation proof, not verbose cold-path diagnostics. Reintroducing Unity `AsyncReadManager.GetFileInfo`; rejected because Unity 6000.x can misreport runtime-created files as absent.
Scalability potential: Low devices avoid avoidable managed string churn during save error paths. Middle/High/Ultra keep the same save integrity behavior; no visual or DTO change.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower GC pressure on failing file-length probes; non-Windows `File.Open` fallback remains pending native replacement.

## Decision 019 - Unix File-Length Fallback Must Be Native Or Fail Closed

Problem: After the Windows fix, non-Windows `TryGetFileLengthPortable` still used `File.Open/FileStream`. That preserved managed allocation risk on Linux, macOS, and Android save verification paths.
Solution: Replace the portable fallback for Linux/macOS/Android with a lock-protected preallocated UTF-8 path buffer and libc `open`, `lseek(SEEK_END)`, and `close`. Unsupported non-Windows/non-Unix targets now fail closed with a static error instead of silently allocating a managed file stream.
Rejected Alternatives: Keep `File.Open` as a universal fallback; rejected because it contradicts the Zero-GC evidence target. Add POSIX `stat`; rejected because struct layout differs by target and this pass has no Linux/macOS/Android player proof. Claim iOS/WebGL readiness; rejected because no platform build artifact exists.
Scalability potential: Low Android/Steam Deck/macOS devices avoid managed file-stream construction on save length checks. Middle/High/Ultra preserve identical save file identity and checksum behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower managed allocation pressure in save verification on Unix-like targets; runtime platform proof remains pending.

## Decision 020 - Read-Only Save Mapping Must Not Allocate FileStream

Problem: `SaveBinaryStorage.AsyncWriteManager.TryOpenReadOnlyMapping` still opened a managed `FileStream` before copying a save file into a native mapping snapshot. This was a transitive persistence read route used by metadata, header, sector, and override readers.
Solution: Route read-only mapping through native file APIs. Windows opens with `CreateFileW`, reads fixed chunks with `ReadFile`, and closes with `CloseHandle`. Linux/macOS/Android encode the path into the existing preallocated UTF-8 buffer, then use `open`, `read`, and `close`. Unsupported platforms fail closed with a static error.
Rejected Alternatives: Keep `FileStream` because the route is cold; rejected because APEX asked for evidence in persistence routes. Introduce a managed `byte[]` staging buffer; rejected because it would move the allocation rather than remove it. Use Unity `AsyncReadManager.GetFileInfo`; rejected again because runtime-created save files can be misreported absent in Unity 6000.x.
Scalability potential: Low avoids managed stream allocation during save metadata/readback; Middle/High/Ultra preserve identical save bytes and checksum behavior. This is IO hygiene, not visual fidelity.
Hardware Impact: Measured microseconds saved: 0. Expected gain on i3/MX350 is lower GC pressure and fewer managed object lifetimes during save-file inspection.

## Decision 021 - WFC Vault Grid Alias Violated Data Sovereignty

Problem: `SaveManagerNativeBufferSet` stored `BufferID.WfcOutpostGrid` as a `NativeArray<byte>` alias and internal routes wrote to it after `TryResolveHandle`, not after `TryAcquireWriteLock`. That made the BufferSet a cross-phase mutable pointer cache for GlobalDataVault memory.
Solution: Replace the cached `NativeArray<byte>` alias with `VaultGenerationHandle<byte>`. Internal WFC mutation routes now acquire `TryAcquireWriteLock(in handle, SystemID.CoreDataVault, out wfcGrid)` and release through `ReleaseWfcOutpostGridWrite()` inside `finally` blocks in dirty-signal, storm, hydration, and cache-reset paths.
Rejected Alternatives: Keep `TryResolveHandle` because mutations happen on the same frame; rejected because the doctrine requires explicit writer fences for GlobalDataVault mutation. Move WFC grid to owner-local persistent NativeArray; rejected because `BufferID.WfcOutpostGrid` already declares cross-domain vault ownership and save identity must not move.
Scalability potential: Low devices avoid relocation/use-after-free risk under memory pressure. Middle/High/Ultra keep identical WFC persistence behavior; only authority and lifetime proof changed.
Hardware Impact: Measured microseconds saved: 0. Expected benefit is stability under vault relocation/defrag and scene unload, not frame-time reduction.

## Decision 022 - Sentinel Restore Must Preserve Owner Labels

Problem: Failed native disposal restored sentinel records under a generic `DisposeNativeArray` label. That preserved leak visibility but weakened forensic ownership evidence.
Solution: Add sentinel label parameters to SaveManager, rig, and runtime disposal helpers and pass the original BufferSet field names from best-effort disposal loops. If disposal fails after unregister, the restored sentinel row keeps the original field label.
Rejected Alternatives: Keep generic restore labels; rejected because final verification requires exact owner evidence. Swallow restore failures; retained only inside the restore fallback because the original disposal exception must remain the surfaced fault.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. This improves crash forensics on every device tier.
Hardware Impact: Runtime hot-path cost: 0. The label path executes only during cold disposal faults.

## Decision 023 - Compilation Gate Remains Closed

Problem: After the final source changes, compiler proof would be useful, but the workstation is still outside the permitted build window.
Solution: Sample CPU/compiler state and skip `dotnet build`. Latest sample: CPU 91%, `dotnet=1`, `csc=0`, `VBCSCompiler=0`.
Rejected Alternatives: Launching `dotnet build` while CPU exceeds 50% and another dotnet process exists; rejected by explicit project rule. Reporting compile success from static scans; rejected as false evidence.
Scalability potential: Runtime behavior unchanged. Verification status remains `PENDING_VERIFICATION` until an idle build or Unity Test Runner pass exists.
Hardware Impact: Avoided adding a compiler workload under high CPU. Exact avoided build duration is not measured.

## Decision 024 - Black-Box Dump Writers Must Not Use BinaryWriter Loops

Problem: APEX continuation found Agent 1403 fault dump callsites still created managed `FileStream` and `BinaryWriter` objects in `SaveManager` WFC/async persistence dumps and `ContextualPhysicalIkRuntime` IK telemetry dumps. The routes are cold fault paths, but they are the exact forensic paths used when memory/lifecycle code fails.
Solution: Replace the callsite writers with local `NativeArray<byte>` staging buffers, explicit little-endian `BinaryPrimitives` span writes, sanitized IK float writes, and `AsyncWriteManager.WriteAll` submission. Rename dump outputs to `Dump_1403_WFC_PERSISTENCE_SYNC.bin`, `Dump_1403_ASYNC_PERSISTENCE.bin`, and `Dump_1403_CONTEXTUAL_PHYSICAL_IK.bin` so forensic artifacts identify the 1403 ownership pass.
Rejected Alternatives: Keep `BinaryWriter` because the path is cold; rejected because black-box evidence must survive fault analysis without extra field-loop allocation debt. Write raw struct memory directly; rejected for IK because the old route sanitized non-finite floats before export. Build a new global dump service; rejected as an over-broad authority route for a scoped 1403 cleanup.
Scalability potential: Low devices avoid extra managed dump writer objects during failure export. Middle/High/Ultra behavior and visual fidelity are unchanged because this is crash evidence serialization, not simulation.
Hardware Impact: Measured runtime microseconds saved: 0. Expected impact is reduced fault-path GC pressure only. End-to-end managed IO is not fully eradicated because `AsyncWriteManager.WriteAll` still centralizes writes through existing storage IO; this remains reported as residual storage-layer debt.

## Decision 025 - Build Attempt Timed Out, No Compiler Verdict

Problem: After C# source edits, a compiler pass became justified if the workstation gate allowed it.
Solution: Gate sample before build was CPU 29%, `dotnet=0`, `csc=0`, `VBCSCompiler=0`; one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` attempt was launched. It timed out after 124 seconds without diagnostic output. The leftover build processes were stopped to avoid continuing CPU pressure.
Rejected Alternatives: Claiming compile success from a timed-out command; rejected as false evidence. Re-running immediately; rejected because post-timeout CPU rose above 50% and compiler processes existed.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra. Verification remains `PENDING_VERIFICATION`.
Hardware Impact: The aborted build consumed CPU for 124 seconds. No additional build pass was launched.

## Decision 026 - Storage Writer And Cached Read Window Must Stop Allocating Stream Wrappers

Problem: Agent 1403 black-box dump callsites had been cleaned, but they still depended on `AsyncWriteManager.WriteAll`. That storage layer still used `FileStream`, `Marshal.Copy`, managed write scratch byte arrays, and `.WriteAsync(...).GetAwaiter().GetResult()` for writeback. Cached read-window hydration also used a managed `FileStream` plus managed scratch copy before inserting the window.
Solution: Replace `WriteAll`, `OverwriteAll`, `FlushPath`, and cached read-window hydration with native OS handle routes. Windows uses `CreateFileW`, `WriteFile`, `ReadFile`, `SetFilePointerEx`, `SetEndOfFile`, and `FlushFileBuffers`. Linux/macOS/Android use preallocated UTF-8 path encoding plus `open`, `write`, `read`, `lseek`, `ftruncate`, `fsync`, and `close`. Unsupported platforms fail closed instead of silently falling back to managed stream allocation. The editor/development smoke corruption helper now edits a native read snapshot and writes it back through `OverwriteAll`.
Rejected Alternatives: Keeping `FileStream` because IO is cold; rejected because the 1403 dump proof path depends on this writer. Keeping managed `Marshal.Copy` scratch; rejected because the source pointer can be written directly to the OS handle in bounded chunks. Rewriting the byte[] read-window cache into NativeArray windows; rejected for this pass because it changes cache ownership wider than the 1403 dependency and needs separate load tests.
Scalability potential: Low devices avoid managed stream/scratch churn during save writeback, crash dump export, and cached save read hydration. Middle/High/Ultra preserve identical save bytes, checksum behavior, and IK visuals; this is storage hygiene, not a new simulation or visual feature.
Hardware Impact: Measured runtime microseconds saved: 0. Expected impact is reduced GC pressure and fewer managed object lifetimes during persistence IO and fault export on i3/MX350-class machines. Runtime platform proof remains pending.

## Decision 027 - Compilation Evidence Is Blocked By Out-Of-Domain XRPass Errors

Problem: The native interop patch required a compiler check. The full solution build exited 1 after 99.9 seconds with no diagnostic lines and left a child `dotnet` process building `Hecton8.Editor.csproj`, which had to be stopped to obey the workstation throttle rule.
Solution: After cooldown, a narrower `Hecton8.Core.csproj` build was run under an open gate. It failed with two explicit errors in `Assets/_Project/Scripts/Visor/HectonVRBrownoutFeature.cs`: `XRPass` missing at lines 441 and 480. No compiler error was reported for `SaveBinaryStorage.cs`, `SaveManager.cs`, or contextual IK in that targeted output.
Rejected Alternatives: Editing `HectonVRBrownoutFeature.cs`; rejected because Visor/XR rendering is outside Agent 1403's domain boundary and belongs to another owner or Integrator. Running a third build immediately; rejected as build spam after one full solution attempt and one targeted compile.
Scalability potential: Runtime behavior unchanged. The storage changes remain `PENDING_VERIFICATION` until an owner fixes the XRPass compile blocker and a clean project build/Unity import can run.
Hardware Impact: Full build attempt consumed 99.9 seconds plus child cleanup. Targeted Core compile consumed 31.5 seconds and produced actionable out-of-domain diagnostics.

## Decision 028 - Cached Read Windows Cannot Depend On Managed ArrayPool

Problem: The native storage IO patch removed `FileStream` and `Marshal.Copy`, but `AsyncWriteManager.CachedReadWindow` still retained a `byte[]` rented from `ArrayPool<byte>`. A cold pool can allocate a managed array, so the save read-cache route could not honestly claim zero managed buffer allocation.
Solution: Replace cached window bytes with `NativeArray<byte>`, register each window with `NativeMemorySentinel` under owner `AsyncWriteManager` and label `CachedReadWindow`, hydrate directly through `TryReadAbsoluteFileRangeToNativeBuffer`, and dispose/unregister at eviction, invalidation, or failed hydration.
Rejected Alternatives: Keep `ArrayPool<byte>` because it usually reuses arrays; rejected because evidence must hold when the pool is cold. Disable the cache; rejected because it would regress save metadata/readback IO behavior and GPU upload batching. Allocate one global native window; rejected because the existing four-slot prefetch cache is the owner boundary already used by readers.
Scalability potential: Low devices avoid cold managed array allocation during save readback. Middle/High/Ultra keep the same cached read-window behavior, prefetch window count, and GPU upload route; no save DTO, checksum, IK math, or visual system changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit on i3/MX350-class hardware is lower GC pressure and native sentinel visibility for cached save reads under memory pressure.

## Decision 029 - World Pager Allocation Must Be Cold-Warmed Before Tick

Problem: A transitive hot route remained after the cached-window sweep: `SaveManager.Tick -> DrainChunkDehydratedSignals -> EnqueueChunkDehydrationPayloads -> EnsureWorldPagerInitialized` could allocate `new H8BinaryWorldPager()` if the first dehydrated chunk signal arrived before any cold pager initialization. That is a reference-type allocation reachable from Tick.
Solution: Call `EnsureWorldPagerInitialized()` from `Awake` after native buffer and storage warmup, and from `InitializeService` after native buffer initialization. Collapse `EnsureWorldPager()` to delegate to the same initializer so there is only one allocation site, tagged as a cold allocation warmed before Tick.
Rejected Alternatives: Leave the lazy allocation because dehydrated chunk signals are rare; rejected because the Zero-GC contract is route-based, not frequency-based. Move the pager into GlobalDataVault; rejected because it is a managed persistence bridge, not a cross-domain native data owner. Add a binary low-end guard; rejected because allocation ownership is lifecycle truth and must not depend on quality tier.
Scalability potential: Low devices avoid a first-signal managed allocation spike during world streaming. Middle/High/Ultra keep identical chunk persistence behavior; no save DTO, checksum, page format, visual simulation, or GlobalQualityWeight route changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit on i3/MX350 is removal of a first-use managed allocation from the Tick-dehydration route and lower scene-streaming hitch risk.

## Decision 030 - Pager Telemetry Ring Must Use A DataVault Writer Fence

Problem: `H8BinaryWorldPager.RecordTelemetry` wrote `BufferID.SaveWorldPagerTelemetryRing` after resolving a mutable `NativeArray` through `TryResolveHandle`. The pager runs on a background worker and can outlive a single owner phase, so the mutable alias was weaker than the Data Sovereignty proof standard used for WFC.
Solution: Add `TryAcquirePagerVaultWrite` and `ReleasePagerVaultWrite`. `RecordTelemetry` captures the telemetry handle, acquires `TryAcquireWriteLock(..., SystemID.SavePersistence, out telemetryRing)`, writes one record, and releases in `finally`. Black-box dump reads now use `TryReadOnlyHandle` and no longer creates a separate dump `FileStream`; it stages bytes in `NativeArray<byte>` and writes through `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Keep `TryResolveHandle` because the ring is owned by SavePersistence; rejected because worker-thread lifetime crosses the current-phase contract. Lock all pager buffers in this pass; rejected because command queues, arenas, compression scratch, hot-state arena, and read staging need a coordinated worker/queue locking redesign to avoid deadlocks and dropped writes.
Scalability potential: Low devices gain safer telemetry visibility under vault compaction or relocation pressure. Middle/High/Ultra keep identical page/WAL behavior and no visual simulation changes.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower relocation/use-after-free risk for the 300-frame pager telemetry ring; core pager `FileStream` IO remains documented residual debt.

## Decision 031 - Hot Initializer Must Not Contain Cold Allocation Text

Problem: The previous world pager warmup fix was behaviorally cold, but `EnsureWorldPagerInitialized()` still contained `new H8BinaryWorldPager`. Because Tick-driven chunk dehydration calls that method, a strict text audit could not honestly report `newRefCandidate=0` for the hot-reachable method body.
Solution: Split allocation from initialization. `EnsureWorldPagerCold()` is the only method that creates `H8BinaryWorldPager`, and it is called from `Awake`, `InitializeService`, and cold `EnsureWorldPager()`. Tick-driven dehydration now reaches only `EnsureWorldPagerInitialized()`, which returns false if the pager was not cold-created and contains no reference-type allocation.
Rejected Alternatives: Keep the old warmup and explain it as cold; rejected because the APEX mandate asks for exact text-scan evidence. Allocate pager lazily from Tick if missing; rejected because it reintroduces a route-based managed allocation. Move pager into GlobalDataVault; rejected because the pager is a managed persistence bridge and not cross-domain native data.
Scalability potential: Low devices avoid first-dehydration allocation spikes. Middle/High/Ultra keep identical page format, WAL behavior, checksum behavior, and visual systems; this is lifecycle hygiene, not a quality-tier switch.
Hardware Impact: Measured runtime microseconds saved: 0. Expected i3/MX350 benefit is removing a first-use managed allocation hazard from the Tick dehydration route.

## Decision 032 - WFC Grid Handle Validation Must Be Read-Only

Problem: `TryEnsureWfcOutpostGridHandle` validated `BufferID.WfcOutpostGrid` with `TryResolveHandle`, which exposes a mutable `NativeArray<byte>` even though the route only checks existence and length. This weakened the Data Sovereignty proof after the writer routes had already been fenced.
Solution: Replace validation with `TryReadOnlyHandle` and add an `IsValidWfcOutpostGrid(NativeArray<byte>.ReadOnly)` overload. Write mutations remain routed through `TryAcquireWfcOutpostGridWrite` and released in `finally` blocks.
Rejected Alternatives: Keep mutable resolve because it was not writing; rejected because read accessors must be pure and should not acquire mutable aliases. Convert all WFC public APIs to read-only inputs; rejected because persistence and hydration routes intentionally mutate the caller-owned grid under writer locks.
Scalability potential: Low devices get safer vault relocation behavior under memory pressure. Middle/High/Ultra keep identical WFC persistence behavior; no binary quality switch or visual simulation was introduced.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is correctness under GlobalDataVault compaction/relocation, not frame-time reduction.

## Decision 033 - Adjacent VR Hand IK Dump Must Not Allocate Managed Writers

Problem: `VRPhysicalHandPresenceBlackBox.TryDumpTelemetry` still used `new FileStream` and `new BinaryWriter` for a 300-frame IK black-box dump. The path is cold and adjacent to the contextual IK scope, but it is the same class of forensic writer debt already removed from SaveManager and ContextualPhysicalIkRuntime.
Solution: Stage the dump into a Temp `NativeArray<byte>`, write the header and each `VRHandIkTelemetryEntry` with `BinaryPrimitives` little-endian helpers, pad every record to the declared 128-byte layout, and call `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Leave BinaryWriter because the route is cold; rejected because black-box evidence should not allocate managed writer objects during fault export. Rewrite VRPhysicalHandPresenceVault locking; rejected because `TryResolveBuffers` currently has no production caller in `rg`, and a correct writer-lock contract would need job lifetime/release ownership, not a partial local patch.
Scalability potential: Low devices avoid managed dump writer churn on IK fault export. Middle/High/Ultra keep identical IK math, haptics, and visual behavior; this is crash-evidence hygiene, not a simulation change.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower fault-path GC pressure and a correctly fixed-size dump record format.

## Decision 034 - Static Managed Scratch Must Not Survive The Purge

Problem: A final dependency scan still found `SaveBinaryStorage` static managed `byte[]` buffers: Unix path UTF-8 scratch and managed Deflate fallback copy scratch. It also found an unused private VR hand IK vault resolver that exposed mutable `TryResolveHandle`.
Solution: Replace Unix path scratch with a fixed unmanaged static buffer guarded by the existing lock. Replace managed Deflate copy scratch with a stackalloc `Span<byte>` in the copy method. Remove the unused private VR resolver methods instead of inventing a partial lock lifecycle for dead code.
Rejected Alternatives: Keep static `byte[]` because the routes are cold; rejected because the final proof artifact explicitly listed them as residual debt. Convert the Deflate fallback to a custom native deflater; rejected as a compression-format redesign outside this pass. Patch H8BinaryWorldPager FileStream in place; rejected because it is the central page/WAL storage backend and needs a full native-handle pager redesign, not a local cosmetic change.
Scalability potential: Low devices avoid static managed buffer lifetime and cold pool pressure in save IO/compression fallback paths. Middle/High/Ultra preserve identical save bytes, DTO layout, IK math, and visual behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower managed heap residency and cleaner crash/memory forensics on i3/MX350-class hardware.

## Decision 035 - Final Build Gate Still Closed By CPU

Problem: After the static scratch patch, compiler proof was still desirable, but project rules forbid `dotnet build` when CPU exceeds 50%.
Solution: Wait 30 seconds and resample. Latest gate: CPU 94%, `dotnet=0`, `csc=0`, `VBCSCompiler=0`; no build launched.
Rejected Alternatives: Running build because compiler processes were gone; rejected because CPU alone still violates the throttle rule. Reporting source scans as compile proof; rejected as false evidence.
Scalability potential: Runtime behavior unchanged. Verification remains `PENDING_VERIFICATION`.
Hardware Impact: Avoided adding a compiler workload during 94% CPU load.

## Decision 036 - New Save Writes Must Not Create Managed Deflate Blocks

Problem: After removing the static compression scratch, write-side `TryCompressBlock` could still fall back to `DeflateStream`/`UnmanagedMemoryStream` when native LZ4 was unavailable. That stopped static byte[] residue but still allowed managed compression objects and new managed-Deflate blocks.
Solution: Make write-side block compression native LZ4 only. If LZ4 is unavailable or fails, the write route fails closed. `EncodeCompressedBlockLength` no longer accepts a managed-fallback flag, so new writes cannot emit `ManagedDeflateBlockLengthFlag`. Keep `DeflateBlockDecompressManaged` only for legacy readback of old blocks already carrying that flag.
Rejected Alternatives: Keep managed Deflate fallback for compatibility; rejected for new writes because it violates the zero-GC evidence path. Remove legacy Deflate read support; rejected because it could strand existing saves/mod payloads. Replace with a new native Deflate backend; rejected as a platform plugin and compression-format project outside this scoped pass.
Scalability potential: Low devices avoid managed compression object churn under missing native LZ4. Middle/High/Ultra keep native LZ4 fast path and identical current save format for new writes.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is removal of cold managed compression allocations and deterministic fail-closed behavior when native compression is absent.

## Decision 037 - Residual MonoBehaviour Native Fields Must Become Owner-Local BufferSets

Problem: Corrected all-domain class-depth scan still found direct physical `NativeArray` fields in `AbyssalThermalManager` and `ProceduralOreSpawner`. They were local scratch/staging arenas, but tying physical native storage directly to MonoBehaviour fields weakens scene-unload ownership and sentinel proof.
Solution: Move thermal Jacobi scratch into `ThermalMapScratchBuffers` and ore generation staging into `SpawnStagingScratchBuffers`. Both wrappers allocate only on cold ensure paths, register persistent arrays with `NativeMemorySentinel`, dispose all arrays through one owner lifecycle, and preserve existing job payloads as raw `NativeArray` values. The ore black-box writer was also changed from `FileStream`/`BinaryWriter` to Temp `NativeArray<byte>` staging plus `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Move local scratch to `GlobalDataVault`; rejected because it would turn private frame-local staging into global heap and add lock traffic without improving authority. Implement actual multi-buffer write locks in `ProceduralOreSpawner.TryLockVault*`; rejected in this pass because that design would hold many write locks simultaneously and needs a separate flattened single-buffer commit plan.
Scalability potential: Low devices get deterministic native disposal and fewer fault-path managed objects. Middle/High/Ultra keep the same thermal diffusion, ore generation, HZB culling, and `GlobalQualityWeight` continuous scaling; no binary low-end switch was introduced.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower scene-unload leak risk, sentinel-visible scratch ownership, and lower crash-dump GC pressure on i3/MX350-class hardware. Residual risk remains in `ProceduralOreSpawner` pseudo-lock routes and is not marked fixed.

## Decision 038 - Procedural Ore Multi-Buffer Writes Need One Guard, Not Nested Locks

Problem: `ProceduralOreSpawner.TryLockVaultBuffer` was a false lock: it resolved or created a mutable buffer but never called `TryAcquireWriteLock`, never set the locked bit, and `UnlockVaultWriteBuffers` only cleared `_lockedVaultBufferMask`. Converting every buffer in depletion/runtime-shift to write-locks would hold several DataVault locks at once and create the deadlock vector the integrator protocol forbids.
Solution: Single-buffer writes now use `TryAcquireVaultBuffer` with one `TryAcquireWriteLock` and `finally` release. Multi-buffer depletion/runtime-shift paths now reserve one aggregate `TryAcquireMutationGuard` mask derived from BufferID active-lock bits, then mutate already-open owner views under that one guard and release it in `finally`. Nested indirect-args and telemetry updates inside guarded routes write through the guarded view instead of taking a second lock.
Rejected Alternatives: Keep `_lockedVaultBufferMask` as a local flag; rejected because it provided no DataVault protection. Take multiple DataVault write-locks; rejected because it is a deadlock-prone lock stack. Disable ore depletion/runtime-shift while waiting for a larger redesign; rejected because it would damage gameplay stability.
Scalability potential: Low devices avoid lock stalls and mutation/compaction races during ore depletion and AUP shifts. Middle/High/Ultra keep the same ore placement, HZB culling, thermal diffusion, sonar fidelity, and continuous `HomeostasisBrain.GlobalQualityWeight` behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is correctness: fewer hidden stalls, relocation races, and fault-path managed allocations on i3/MX350-class machines.

## Decision 039 - Runtime Mutation Guards Must Be Single-Layer

Problem: All-domain textual scan found two runtime methods with apparent nested `TryAcquireMutationGuard` sequences. `BulkheadContainmentIntentBus` was a real lock-stack smell because it held an intent mutation guard while helper methods acquired write locks on intent/control buffers. `ExosuitKinematicsRuntime` was behaviorally safe but produced false nested-guard evidence because full and fallback guard acquisitions lived in one method.
Solution: `BulkheadContainmentIntentBus` now acquires one intent mutation guard, reads capacity/control via `TryReadOnlyHandle`, and writes intent/control rows via guarded mutable views without taking write locks inside the guard. `ExosuitKinematicsRuntime` now uses separate single-acquire helpers for full and fallback job guard acquisition.
Rejected Alternatives: Leaving Bulkhead as guard-plus-write-lock because the route is small; rejected because it weakens the deadlock proof. Replacing Bulkhead with two sequential write locks; rejected because intent ring/control publish wants one atomic writer lane. Treating Exosuit as clean with an explanation only; rejected because the source scanner should be able to prove it.
Scalability potential: Low devices avoid hidden lock stalls in containment intent publishing and exosuit job setup. Middle/High/Ultra keep identical simulation and visual behavior; no binary quality switch or physical over-simulation was introduced.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lock-order correctness and less chance of frame stalls under DataVault contention.

## Decision 040 - Sonar Vault Mirrors Need Writer Fences

Problem: `TopographicalSonarSynthesizer` mirrored late-frame presentation state into GlobalDataVault buffers through mutable `TryResolveHandle` views. The writes were in the right phase, but Data Sovereignty proof was weak because relocation/compaction could observe unfenced telemetry, point, counter, indirect-args, or shader-globals mutations.
Solution: Add `TryAcquireVaultWriteBuffer` and route telemetry ring, telemetry cursor, mirrored point cloud, mirrored counters, indirect args DTO, and shader globals DTO through one `TryAcquireWriteLock` at a time with strict `finally` release. Keep scan jobs on owner-local `JobBufferSet`; only late-frame mirrors touch the vault.
Rejected Alternatives: Put sonar job buffers into GlobalDataVault; rejected because it would add lock traffic to transient scan work and weaken phase boundaries. Hold one broad write-lock stack across all sonar mirrors; rejected because it creates deadlock risk. Leave mutable resolve because the owner is UI; rejected because source proof must survive compaction pressure.
Scalability potential: Low devices avoid relocation races and hidden stalls during sonar pings. Middle/High/Ultra keep the same `HomeostasisBrain.GlobalQualityWeight` continuous ray count, step budget, and visual overkill behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is correctness and stable fault telemetry under DataVault contention, not raw frame-time reduction.

## Decision 041 - Salinity Corrosion Commit Must Be Row-Atomic

Problem: The inventory salinity corrosion refactor correctly used one aggregate mutation guard, but the commit helper still wrote four target lanes as four sequential lane commits. If lane two or three failed after lane one succeeded, inventory durability, byte durability, quality, and state flags could diverge for the same item.
Solution: Resolve all four target lanes before the first write while the single aggregate mutation guard is held. Commit each changed slot as one row only after every target view exists. `InventoryVaultLane.Length`, `IsCreated`, getter, and broken-item signal publication now use read-only views so read probes do not request mutable aliases.
Rejected Alternatives: Keep the sequential helper because failures are rare; rejected because rare partial state is exactly the kind of save/runtime corruption this purge is meant to eliminate. Take four `TryAcquireWriteLock` locks; rejected because that creates the multi-lock deadlock vector the integrator protocol forbids. Copy changed rows into a managed staging object; rejected because it violates Zero-GC and adds no correctness over the row-atomic native path.
Scalability potential: Low devices avoid hidden lock stalls and inconsistent item state under contention. Middle/High/Ultra keep identical salinity math, item DTO layout, rust shader scalar, and signal behavior; no binary quality switch or extra simulation was introduced.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is correctness: fewer partial writes, fewer save-corrupt state combinations, and cleaner DataVault compaction behavior on i3/MX350-class hardware.

## Decision 042 - Editor Sonar CSV Writes Still Need Real Vault Fences

Problem: The sonar runtime mirror paths were fenced, but editor CSV import still wrote CSV scratch and material LUT through mutable resolves. It is not a hot player path, but it can run against the same DataVault buffers during authoring and weakens the source proof.
Solution: Route CSV scratch through one write lock, release it in `finally`, then read scratch through `TryReadOnlyHandle`. Acquire the material LUT write lock only after scratch is released, parse into the LUT, copy to the owner-local job LUT, and release the LUT in `finally`.
Rejected Alternatives: Keep editor mutable resolve because it is editor-only; rejected because editor tooling can still corrupt runtime-authoring buffers and produce false verification. Hold scratch and LUT locks together; rejected because two simultaneous write locks are unnecessary and create an avoidable deadlock shape.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The authoring route now has the same single-lock discipline as runtime presentation mirrors.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is authoring stability and truthful DataVault lock evidence, not player-frame performance.

## Decision 043 - Exosuit CSV Authoring Must Not Poll From Physics Tick

Problem: `ExosuitKinematicsRuntime.FixedTick` called the editor CSV reload route every physics step and gated actual file IO with a 0.25 second countdown. Even though the code was editor-only, the route still tied synchronous file checks and sequential DataVault writer sections to the simulation phase.
Solution: Remove CSV reload polling from `FixedTick`. Keep only the forced cold CSV apply from `OnEnable` after vault buffers are initialized. Split the CSV path into `TryReadCsvTuningOverride` and `TryCommitCsvTuningOverride`; each helper owns exactly one `TryAcquireWriteLock` and releases it in a `finally` block.
Rejected Alternatives: Move polling to `LateFrameTick`; rejected because it would still run synchronous editor IO from a high-frequency frame phase. Keep the countdown because it is editor-only; rejected because the source proof should show no IO/control mutation route from physics tick. Hold scratch and tuning locks together; rejected because one file read and one row commit do not need simultaneous locks.
Scalability potential: Low devices avoid editor physics-step stalls during authoring play mode. Middle/High/Ultra keep the same exosuit simulation, SDF sampling, haptic/acoustic signals, and continuous `GlobalQualityWeight` tuning behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is phase safety and fewer editor-play physics stalls; player builds are unchanged because the CSV route remains under `UNITY_EDITOR`.

## Decision 044 - Editor CSV Reloads Belong To Cold Enable, Not Runtime Ticks

Problem: After the exosuit fix, the same all-domain pattern remained in somatic player kinematics, submarine dynamics, and volcanic updraft authoring routes. Each `SlowTick` ran editor-only CSV file probes and mutation commits. `SlowTick` is lower cadence than physics, but it is still a runtime loop and can stall editor play mode or hide DataVault mutation in a tick phase.
Solution: Move somatic, submarine, and volcanic CSV application to `OnEnable` after their vault buffers are initialized. Remove the CSV calls from `SlowTick`; those methods now keep cache refresh, handle refresh, and signal publication only.
Rejected Alternatives: Leave these because `SlowTick` is not listed in the narrow high-frequency examples; rejected because it is still a repeated runtime phase. Move them to `LateFrameTick`; rejected because synchronous file IO is not presentation work. Add binary low-end/editor switches; rejected because editor authoring determinism should be phase-owned, not device-tier gated.
Scalability potential: Low devices avoid editor play-mode stalls from repeated CSV polling. Middle/High/Ultra keep identical player-build simulation, because the affected routes are under `UNITY_EDITOR`; continuous `GlobalQualityWeight` math in all three systems is unchanged.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower editor-loop IO pressure and cleaner source evidence; no player-frame profiler claim was made.

## Decision 045 - Remaining MonoBehaviour NativeArrays Were Real Debt

Problem: A fresh all-domain class-depth scan contradicted the earlier clean status. Direct physical `NativeArray` fields still existed on `AutonomousExtractorSystem`, `SomaticKinematicsRuntime`, `ScavengingLootOracleRuntime`, and `ResourceDistributionDirector`. These were private scratch/workspace arrays, but the lifecycle ownership was still attached directly to MonoBehaviour instances.
Solution: Move those arrays into nested owner structs: `ExtractorNativeState`, `LocalSimulationScratch`, `SimulationNativeScratch`, and `MetamorphismWorkspaceOwner`. The public behavior, job payloads, buffer sizes, quality math, signal routes, and teardown calls remain unchanged; only the physical native storage owner moved out of class-depth MonoBehaviour fields.
Rejected Alternatives: Mark local scratch as acceptable; rejected because the project rule is physical native storage must have a non-MonoBehaviour owner. Move scratch to `GlobalDataVault`; rejected because private per-runtime scratch is not sovereign cross-domain data and would add lock traffic. Rewrite the jobs; rejected because the job contracts already consume raw `NativeArray<T>` values correctly.
Scalability potential: Low devices get cleaner scene-unload disposal and less stale native state risk. Middle/High/Ultra keep the same simulation fidelity and continuous `HomeostasisBrain.GlobalQualityWeight` routes; no binary quality switch or extra physical simulation was added.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lifecycle correctness: fewer leaked/undisposed native workspaces during scene unload, not raw frame-time reduction.

## Decision 046 - Build Gate Closed, Source Proof Only

Problem: The owner-struct sweep touched C# sources and would normally justify one compile check, but the workstation gate was closed.
Solution: Run source-only verification: class-depth native field scan, hot dependency lookup scan, hot allocation/IO pattern scan, brace balance, and targeted `git diff --check`. Latest sampled build gate was CPU 99% with active `dotnet.exe` PID 57088 building `Assembly-CSharp-Editor.csproj`; no 1403 build was launched.
Rejected Alternatives: Launch another `dotnet build` anyway; rejected because CPU exceeded 50% and a compiler process was active. Claim compile success from scans; rejected because scans are syntax/static evidence, not a compiler verdict.
Scalability potential: Runtime behavior unchanged. Verification remains source-level until the compile gate opens.
Hardware Impact: Avoided adding a compiler workload during 99% CPU load.

## Decision 047 - Owner Structs Must Not Allocate From Runtime Phases

Problem: Moving native fields into owner structs was necessary but incomplete. `AutonomousExtractorSystem.SlowTick` still called `EnsureExtractorNativeStateCold`, and `SomaticKinematicsRuntime.FixedTick` still called `EnsureLocalSimulationScratch`. Those calls made the direct hot-method scan look clean while leaving a transitive `Allocator.Persistent` allocation path in runtime phases.
Solution: Keep cold allocation in lifecycle setup only. `AutonomousExtractorSystem.OnEnable` prewarms `_nativeState`; `SlowTick`, `TryAcquireExtractorJobBuffers`, and `TryAcquireExtractorStateBuffers` now only test `_nativeState.IsReady(MaxModuleCapacity)`. `SomaticKinematicsRuntime.FixedTick` now only tests `_localScratch.IsReady()`. `ExtractorNativeState`, `LocalSimulationScratch`, `SimulationNativeScratch`, and `MetamorphismWorkspaceOwner` now register every persistent `NativeArray` with `NativeMemorySentinel`, unregister before disposal, and dispose partial cold allocations before rethrowing.
Rejected Alternatives: Leave the `Ensure*` calls in runtime because they usually no-op; rejected because rare scene reload or failed cold init would turn the first runtime tick into a persistent allocation hitch. Move private scratch to `GlobalDataVault`; rejected because these buffers are local job workspaces, not cross-domain truth. Add binary low-end branches; rejected because lifecycle correctness must not depend on device tier.
Scalability potential: Low devices avoid rare but severe runtime allocation stalls. Middle/High/Ultra keep identical simulation and visual behavior; no gameplay truth, DTO layout, `HomeostasisBrain.GlobalQualityWeight` route, or physical solver math changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is eliminating runtime allocation spikes and making scene-unload native ownership visible to the sentinel on i3/MX350-class hardware.

## Decision 048 - Cold Ensures And Editor IO Must Not Sit In SlowTick

Problem: Generic all-domain scan still found `Ensure*Cold()` calls inside runtime `SlowTick` bodies. `ShinobuOceanSurfaceAtmosphereRuntime.SlowTick` could allocate/ensure vault buffers and run editor CSV loading; `ShinobuStormPropagationRuntime.SlowTick` could allocate/ensure vault buffers and job staging when `_vaultReady` was false.
Solution: Make both slow ticks readiness-only. Ocean vault warmup and optional editor CSV import now happen from `OnEnable` after cold vault setup. Storm vault warmup stays in `OnEnable` and DataVault rebind; `SlowTick` returns while `_vaultReady` is false.
Rejected Alternatives: Keep slow-tick cold ensures because the cadence is low; rejected because low cadence still lands on the game thread and can hitch weak devices. Move CSV to `LateFrameTick`; rejected because file IO is not presentation sync. Add device-tier conditionals; rejected because phase correctness must be invariant across hardware.
Scalability potential: Low devices avoid editor/play-mode stalls from repeated cold file/vault work. Middle/High/Ultra keep the same ocean wave, storm propagation, and continuous quality-weight scaling behavior.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is fewer slow-frame spikes under cold-start/rebind edge cases.

## Decision 049 - Readiness Checks Must Replace Runtime Cold Work

Problem: A follow-up sweep found two classes of real debt: `MarauderOutpostGenerationService` still kept outpost WFC/extraction/shift `NativeArray` scratch directly on the MonoBehaviour, and several runtime phases still called cold `Ensure*` methods from `LateFrameTick`, `SlowTick`, `FixedTick`, or service tick paths. `ShinobuLogisticsRouter.SlowTick` also polled editor CSV overrides from a runtime loop and reused an initializer route that could hide mutation work.
Solution: Move outpost scratch into `OutpostScratchBuffers`, with sentinel registration on successful cold allocation, unregister-before-dispose teardown, partial-allocation cleanup, and readiness-only use from request and late-frame paths. Convert presentation/runtime loops in HLOD, distant landmarks, scan markers, GameTickManager, fauna, data archaeology, biolum, cave voxel lighting, GPU scatter, and logistics to test cached readiness instead of allocating or resolving cold dependencies. Move logistics CSV override application to cold initialization and guard it with one router mutation guard released in `finally`; parsing writes tuning directly under that guard instead of stacking another write lock.
Rejected Alternatives: Keep no-op `Ensure*` calls in loops because they usually return quickly; rejected because failed cold init, scene reload, or resource invalidation turns "usually" into a hitch. Move private outpost scratch to `GlobalDataVault`; rejected because it is local generation workspace, not cross-domain truth, and vault locks would add contention. Keep editor CSV polling in `SlowTick`; rejected because editor-only synchronous IO still breaks phase proof and can stall play mode.
Scalability potential: Low devices avoid first-frame/runtime-loop allocation spikes and editor play-mode CSV stalls. Middle devices keep stable cadence when resources are missing. High/Ultra keep identical visuals and continuous `HomeostasisBrain.GlobalQualityWeight` behavior; no binary tier switch, gameplay truth change, DTO layout change, or physical over-simulation was introduced.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is fewer frame stalls, cleaner scene-unload native ownership, and simpler deadlock proof on i3/MX350-class hardware.

## Decision 050 - Private Pager Queues Are Not Sovereign Vault Data

Problem: `H8BinaryWorldPager` still stored private worker queues, page arenas, read slot states, compression scratch, and hot-state staging in `GlobalDataVault`. These buffers are not cross-domain truth; they are pager-owned worker internals. Keeping them in the vault widened the lock/relocation surface and made scene-unload proof depend on global heap semantics.
Solution: Move those buffers into `PagerNativeState`, a local native owner with sentinel registration, unregister-before-dispose teardown, and partial-allocation cleanup. Keep only actual cross-domain lanes in the vault: read staging slices and the telemetry ring. Hot enqueue/read/write/worker routes now resolve owner-local arrays without `TryResolveHandle`.
Rejected Alternatives: Keep all pager internals in `GlobalDataVault`; rejected because private queues are not sovereign data and do not need cross-domain locks. Add write locks around every private queue access; rejected because it would add contention to a single-owner worker. Replace the pager IO system in this pass; rejected because the remaining `FileStream` random-access design is larger than this memory ownership correction and needs a separate IO route card.
Scalability potential: Low devices avoid extra vault contention and relocation checks in save pager worker paths. Middle/High/Ultra keep the same page format, WAL semantics, compression route, telemetry DTO layout, and world streaming behavior; no binary quality branch or physical simulation was added.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is lower lock surface and more deterministic scene-unload disposal under save paging pressure.

## Decision 051 - Cockpit Runtime Must Not Repair Native Ownership From LateFrame

Problem: `VehicleSubOsCockpitRuntime.LateFrameTick` could repair missing cockpit DataVault buffers by calling `EnsureNativeResources`, and blackbox dumps used `FileStream`/`BinaryWriter`. That made a visual-sync method a hidden native allocation/growth route and kept managed writer residue in an emergency path.
Solution: Make `LateFrameTick` readiness-only for native cockpit buffers. DataVault service replacement now refreshes native resources from the hot-swap callback, not from the frame loop. Presentation resources use explicit dirty flags; radar buffer capacity grows only when `HomeostasisBrain.GlobalQualityWeight` requests more than the allocated capacity, while quality drops reduce active point budget without reallocating buffers. Blackbox dumps now stage fixed little-endian records into Temp `NativeArray<byte>` and submit them through `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Leave `EnsureNativeResources` in `LateFrameTick` because it usually no-ops; rejected because failed cold init or vault replacement can turn a visual frame into a native allocation/growth hitch. Shrink radar buffers every time quality decreases; rejected because it creates capacity thrash on unstable quality weight. Keep `BinaryWriter` because dumps are rare; rejected because the project has a native save IO route and rare fault paths still need deterministic behavior.
Scalability potential: Low devices avoid LateFrame native buffer repair and quality-drop realloc stalls. Middle devices keep stable UI cadence when quality breathes. High/Ultra still use continuous quality weight to buy more radar points and richer damage hologram budgets without changing gameplay truth.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is fewer visual-sync stalls and less managed emergency IO allocation on i3/MX350-class hardware.

## Decision 052 - Tether Black Box Must Not Allocate Or Resolve From FixedTick

Problem: `TetherManager.FixedTick` wrote black-box telemetry through `TryResolveTelemetry`, which could create vault handles and resolve mutable DataVault arrays from the physics phase. `TetherManager.SlowTick` also refreshed `GlobalRegistry` dependencies and ran bootstrap work every slow tick. The `Shinobu143` mock job still needed writable AUP lanes from DataVault and previously resolved them without a job-lifetime mutation lease.
Solution: Move manager-owned black-box telemetry into nested `TetherTelemetryState` with cold `Allocator.Persistent` arrays, `NativeMemorySentinel` registration, and unregister-before-dispose teardown. Keep `FixedTick` telemetry to a local ring write. Keep `SlowTick` to continuous quality cache refresh only. Move DataVault/cable/player/weather/fluid/voxel/vegetation rebinding to `OnGlobalRegistryServiceReplaced`. Wrap the `Shinobu143` AUP mock scheduling path in one aggregate mutation guard, transfer the guard lease to the scheduled job, and release it in completion through `finally`.
Rejected Alternatives: Keep DataVault telemetry because it was already present; rejected because a manager-private black box is not cross-domain truth and should not spend vault lookup/lock surface every physics step. Leave `SlowTick` dependency refresh as a harmless poll; rejected because `GlobalRegistry` is cold identity injection, not a runtime polling bus. Resolve AUP job buffers without a lease because the route is a mock; rejected because mocks still write shared native lanes. Replace tether physics; rejected because the current problem was ownership and phase hygiene, not cable math.
Scalability potential: Low devices avoid physics-step vault allocation/resolve spikes and repeated slow-tick registry/bootstrap work. Middle devices get steadier tether cadence when services rebind. High/Ultra retain the existing continuous `HomeostasisBrain.GlobalQualityWeight` rendering overkill path for indirect tether rendering, crystal density, silt intensity, and visual tier.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is fewer physics-frame stalls and cleaner scene-unload telemetry disposal on i3/MX350-class hardware.

## Decision 053 - Tether Visual Sync Must Not First-Create GPU Resources

Problem: After the tether DataVault fix, `LateFrameTick` still had a transitive resource allocation path: indirect rendering could first-create a `Mesh` and `GraphicsBuffer`, and material resolution could first-create a fallback `Material`. That is not managed GC, but it is still Unity/GPU native allocation in `VISUAL_SYNC` and can stall the frame where quality first crosses the indirect-render threshold.
Solution: Prewarm the fallback material, indirect segment mesh, and indirect args buffer from `Awake`/`OnEnable` through `EnsurePresentationResourcesCold`. `LateFrameTick` now only reads existing objects and fails closed if the cold prewarm could not build them. Continuous `HomeostasisBrain.GlobalQualityWeight` still controls visual tier, crystal density, silt intensity, and indirect rendering admission; no binary low-end branch was added.
Rejected Alternatives: Leave the allocation behind `qualityWeight >= 0.62`; rejected because crossing a continuous quality threshold must not trigger first-use allocation. Disable indirect rendering on low devices with a boolean; rejected because quality remains a continuous scalar. Move the mesh/buffer to `GlobalDataVault`; rejected because these are render-owner resources, not cross-domain truth.
Scalability potential: Low devices avoid first-use visual allocation stalls. Middle devices keep direct rendering when quality is below the scalar threshold. High/Ultra can still spend saved time on indirect tether overkill once resources are already resident.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is eliminating a possible visual-frame hitch on MX350/i3-class hardware when tether rendering escalates.

## Decision 054 - Tether Fault Dumps Must Use Native Save IO, Not FileStream

Problem: `TetherBlackBoxDumpWriter` still used `FileStream` for queued and fallback fault dumps. A NaN or constraint fault can be detected from tether runtime paths, so the emergency writer must not drag managed stream allocation and synchronous stream semantics into the fault path.
Solution: Keep the existing fixed binary payload layout, but write the queued persistent snapshot directly through `AsyncWriteManager.WriteAll`. Fallback writes stage the same payload into a Temp `NativeArray<byte>` and call the same native save IO route. The legacy mirror now stores the latest full snapshot instead of appending through `FileStream`.
Rejected Alternatives: Keep `FileStream` because dumps are rare; rejected because rare fault paths are exactly where deterministic postmortem IO matters. Create a new append-capable IO layer now; rejected because the dump ring is already a complete 300-frame snapshot and append semantics are not required for recovery. Remove legacy mirror entirely; rejected because existing callers still pass both paths.
Scalability potential: Low/Middle/High/Ultra runtime simulation is unchanged. Fault capture now uses the same native write route as save/blackbox systems and avoids managed stream residue.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is cleaner crash-path behavior and less managed allocation pressure during tether fault capture.

## Decision 055 - Runtime Phases Must Not Contain Cold Repair Routes

Problem: A generic all-domain scan found nine cold `Ensure*` or allocation-adjacent routes still reachable from runtime phases. These were lower cadence in some cases, but `SlowTick`, `FixedTick`, and `LateFrameTick` are still runtime loops; failed cold init, service replacement, or resource loss could turn a frame into GPU/native/DataVault repair work.
Solution: Make runtime phases readiness-only. Preserve prewarm in `Awake`, `OnEnable`, `Start`, `Create`, dependency injection, and registry hot-swap. For ground-penetrating radar, apply pending DataVault rebind directly from the hot-swap callback instead of waiting for `SlowTick`. For Gerstner buoyancy, `FixedTick` now fails closed if cold boot did not complete.
Rejected Alternatives: Leave "usually no-op" ensures in runtime methods; rejected because failed setup/resource invalidation is exactly when stalls matter. Add a binary low-end switch; rejected because phase safety is invariant and `GlobalQualityWeight` remains continuous. Move render-owner GPU buffers into `GlobalDataVault`; rejected because these are local presentation resources, not cross-domain truth.
Scalability potential: Low avoids runtime allocation and repair stalls on i3/MX350. Middle fails closed instead of self-repairing during gameplay. High and Ultra keep the same visual overkill paths once resources are prewarmed; continuous `GlobalQualityWeight` behavior is unchanged.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is fewer runtime hitches and a cleaner phase proof under service/resource churn.

## Decision 056 - GPU Readback Targets Still Need Non-MonoBehaviour Native Owners

Problem: A fresh class-depth scan found physical `NativeArray` fields still declared directly on MonoBehaviour classes for async GPU readback and one-sample biome scratch. These are not gameplay truth lanes, but they are persistent native memory tied to Unity object lifecycle without an explicit owner boundary.
Solution: Move the physical arrays into nested owner structs in `HectonUnderwaterVisuals`, `ShinobuOceanSurfaceAtmosphereRuntime`, `GPUScatterDirector`, `HectonIndirectVegetationRenderer`, `SargassumMicroFaunaBoids`, `BiomeBoundarySdfRuntime`, `GpuScatterLodManager`, `AsyncBuoyancyReadbackRuntime`, and `InstanceCullingService`. Existing request/completion logic remains unchanged; allocation/disposal still happens from existing cold/release paths, and `BiomeBoundarySdfRuntime` sample scratch now registers with `NativeMemorySentinel`.
Rejected Alternatives: Move these readback buffers to `GlobalDataVault`; rejected because they are owner-local presentation/readback targets, not cross-domain sovereign data, and vault locks would add needless contention. Leave them as "small readback fields"; rejected because the project rule is physical native storage must not live at MonoBehaviour class depth. Remove readbacks entirely; rejected because diagnostics and adaptive presentation counters still need the data.
Scalability potential: Low devices get cleaner scene-unload ownership and less stale readback memory risk. Middle keeps diagnostics without runtime authority drift. High/Ultra keep the same visual telemetry and adaptive overkill paths after prewarm; `GlobalQualityWeight` behavior is unchanged.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is lifecycle safety and clearer native memory ownership, not steady-state frame-time reduction.

## Decision 057 - Static Save Write Buffers Must Serialize Instead Of Allocate Fallbacks

Problem: `SaveManager.StaticNativeBuffers.AcquireWriteBuffers` still had a contention path that could allocate a fresh 64 MB raw buffer plus a 71 MB compressed buffer per overlapping static save/repair route. The deferred dispose path also let `ReleaseWriteBuffers` drain `SaveLoadCandidateScratch` from inside the write-buffer monitor instead of preserving the existing candidate-scratch-first teardown order.
Solution: Serialize the single static raw/compressed write-buffer lease with `Monitor.Wait(Sync)` and `Monitor.PulseAll(Sync)`. Remove the per-contention fallback allocation route. When a static dispose is requested during an active write lease, `ReleaseWriteBuffers` now clears the lease, pulses waiters, and calls `Dispose()` outside the `Sync` monitor so disposal returns through the canonical `SaveLoadCandidateScratchSync -> Sync` order.
Rejected Alternatives: Keep the per-contention fallback because it avoids waiting; rejected because a 135 MB native spike per overlap is worse than serializing a cold static save/repair path. Hold `SaveLoadCandidateScratchSync` while already inside `Sync`; rejected because it reverses lock order. Add a pool of fallback buffers; rejected because it multiplies persistent native memory for a rare cold contention case.
Scalability potential: Low devices avoid catastrophic native memory spikes during static save/read repair contention. Middle devices get deterministic static IO memory. High/Ultra keep the same save format and can still run the same native compression route without changing gameplay truth, DTO layout, or `GlobalQualityWeight`.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is avoiding approximately 135 MB of extra native allocation per contended static write lease on i3/MX350-class hardware.

## Decision 057B - Tick-Driven Chunk Dehydration Must Not Warm Save Staging

Problem: `SaveManager.Tick` drains `ChunkDehydratedSignal` and calls `EnqueueChunkDehydrationPayloads`. That method still called `EnsureSaveStagingBuffer()`. Under normal cold boot the buffer can already exist, but failed cold init, scene-service reentry, or future lifecycle changes would turn a runtime tick into a 10 MB `Allocator.Persistent` allocation route.
Solution: Prewarm `SaveStagingBuffer` from `SaveManagerNativeBufferSet.EnsureInitial()`, which is called from cold initialization. Replace the tick-path ensure with a readiness check against `_saveStagingBuffer.IsCreated` and `SaveStagingBufferBytes`; if the buffer is absent or undersized, the dehydration payload path fails closed for that signal instead of allocating from the tick.
Rejected Alternatives: Leave the hot ensure because it usually no-ops; rejected because zero-GC proof must cover failure paths, not only happy boot. Allocate a smaller per-signal staging buffer; rejected because it would still allocate from the tick and fragment native memory. Move chunk dehydration payload staging to `GlobalDataVault`; rejected because this is SaveManager-owned transient staging, not cross-domain truth.
Scalability potential: Low devices avoid a 10 MB native allocation hitch in `Tick`. Middle devices fail closed instead of self-repairing during gameplay if cold init failed. High/Ultra keep the same world pager payload route and can still spend quality budget elsewhere; no save format, DTO layout, gameplay truth, or `GlobalQualityWeight` contract changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is eliminating a rare but severe runtime allocation spike on i3/MX350-class hardware.

## Decision 058 - Fluid GPU Readback Must Be Cold-Owned And Fault Dumps Must Avoid Managed Writers

Problem: `HectonFluidEngine` still carried GPU buoyancy readback targets as a direct `NativeArray<float4>[]` field on the MonoBehaviour, and the visual-sync dispatch path could grow those persistent readback targets from `LateFrameTick`. The same file also kept critical fluid/ocean/abyssal/maelstrom/advection black-box dumps on `FileStream`/`BinaryWriter`.
Solution: Move the readback targets into `GpuReadbackNativeRing`, a non-MonoBehaviour owner that registers each persistent slot with `NativeMemorySentinel`, unregisters before disposal, and is filled only from cold GPU buoyancy buffer warmup. `TryDispatchGpuBuoyancySampling` now only checks `HasGpuReadbackData` and fails closed if cold capacity is missing. Dump writers now stage fixed little-endian records into Temp `NativeArray<byte>` and call `AsyncWriteManager.WriteAll`; field order is preserved.
Rejected Alternatives: Keep the readback `Ensure` in `LateFrameTick` because GPU parity is currently dormant; rejected because phase safety must survive future enablement. Move readback targets to `GlobalDataVault`; rejected because they are presentation-owner readback slots, not cross-domain truth. Keep `BinaryWriter` because dumps are rare; rejected because fault paths must be deterministic and the project already has native save IO.
Scalability potential: Low devices avoid first-use visual-sync native allocation and managed fault-writer churn. Middle devices skip GPU buoyancy sampling if cold capacity is missing instead of stalling a frame. High and Ultra keep the same future GPU buoyancy overkill path once cold resources are resident; continuous `GlobalQualityWeight` behavior and fluid gameplay truth are unchanged.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit on i3/MX350 is removal of a possible `LateFrameTick` persistent native allocation spike and less managed allocation pressure during fluid fault export.

## Decision 057C - Do Not Retry A Timed-Out Build

Problem: After the SaveManager staging patch, the build gate opened and one full solution build was legitimate. The command exceeded the watchdog timeout without diagnostics and left a `dotnet.exe` child process.
Solution: Stop the orphaned build process, rescan compiler state, and do not retry. The source proof remains the active evidence until a clean compiler window exists and a build can finish with diagnostics.
Rejected Alternatives: Launch a second build immediately; rejected because the first attempt already consumed 184 seconds and CPU climbed above the throttle threshold after timeout. Treat timeout as compile success; rejected because no compiler verdict was produced. Leave the child process running; rejected because orphaned build processes violate the resource protocol.
Scalability potential: Runtime behavior unchanged. This is build-resource containment only.
Hardware Impact: Avoided additional compiler load after a timeout; post-cleanup compiler scan returned zero compiler processes.

## Decision 059 - Outpost Fault Dumps Need A Core Native Writer Facade

Problem: `MarauderOutpostGenerationService.DumpBlackBox` still used `FileStream` and `BinaryWriter` after the outpost scratch buffers had already been moved out of the MonoBehaviour. Replacing those calls directly with `AsyncWriteManager` inside the outpost file would have crossed two contracts: `Hecton8.World.Outposts.asmdef` has `allowUnsafeCode=false`, and the native writer implementation is assembly-internal to Core/Save infrastructure.
Solution: Keep the outpost assembly unsafe-free. Stage the existing dump layout into a local Temp `NativeArray<byte>`, write all fields as explicit little-endian bytes, and dispose the Temp array in `finally`. Add `Hecton8.Core.NativeFaultDumpWriter` as the only unsafe bridge; it validates the created array and byte count, obtains a read-only native pointer inside the unsafe-enabled Core assembly, and calls `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Flip `allowUnsafeCode` on `Hecton8.World.Outposts`; rejected because one fault writer should not widen the entire feature assembly. Make `AsyncWriteManager` public; rejected because storage internals should not become a direct dependency for every feature domain. Keep `BinaryWriter` because dumps are rare; rejected because fault paths still need deterministic native IO and no managed stream residue.
Scalability potential: Low devices avoid managed stream allocation during outpost postmortem dumps. Middle devices keep the same fixed binary payload with less crash-path pressure. High and Ultra keep the same telemetry density; no gameplay truth, generation math, save identity, DTO layout, or continuous `GlobalQualityWeight` contract changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is lower fault-path allocation pressure and a narrower unsafe surface on i3/MX350-class hardware.

## Decision 060 - Vegetation Tile Cache Validation Must Use The Hot Texture Cache Route

Problem: The narrow Core build found `VegetationTileCacheResidency.HasTileCacheSignatureChanged` calling removed method `RefreshTerrainTextureCaches`. The adjacent partial already split the route into `RefreshTerrainTextureCachesCold` and `TryRefreshTerrainTextureCachesHot`; using the cold route from validation would allocate `Texture2D[]` handles from a runtime validation path.
Solution: Replace the stale call with `TryRefreshTerrainTextureCachesHot`. If the cached texture array is absent or its length no longer matches `TerrainData.alphamapTextureCount`, the signature method returns true and the caller falls through to the existing cache rebuild path instead of hiding cold work in validation.
Rejected Alternatives: Recreate `RefreshTerrainTextureCaches` as an allocating helper; rejected because the old ambiguous name would erase the cold/hot contract. Call `RefreshTerrainTextureCachesCold` directly; rejected because signature validation is runtime cadence and must not allocate. Ignore the build error as out-of-domain; rejected because the fix was local, contract-preserving, and required no dependency invention.
Scalability potential: Low devices avoid texture-cache array allocation during validation. Middle devices detect terrain texture cache drift without runtime repair stalls. High and Ultra keep the same tile cache fidelity and chunk invalidation route; no terrain sampling math, cache buffer layout, save identity, or continuous `GlobalQualityWeight` behavior changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is compile restoration plus avoidance of a possible runtime allocation if the stale helper were recreated naively.

## Decision 061 - Ladder IK Fault Dumps Must Not Use Managed Worker Snapshots Or FileStream

Problem: `ProceduralLadderClimbRuntime` had fault-dump work routed through managed snapshot state and `FileStream`. That kept a managed array and thread-pool handoff attached to a MonoBehaviour fault path, and it wrote the final telemetry payload through managed stream IO.
Solution: Remove the managed snapshot/in-flight route and write the dump directly from the telemetry NativeArrays into a method-local Temp `NativeArray<byte>`. Preserve the existing 8-byte header and 128-byte telemetry record layout with explicit little-endian writers. Submit through `NativeFaultDumpWriter.TryWriteAll` and dispose the Temp payload in `finally`.
Rejected Alternatives: Keep `ThreadPool` because the dump is fault-only; rejected because it extends MonoBehaviour-owned managed state across a worker boundary. Keep `FileStream` because the payload is small; rejected because native save IO now exists and the fault path should not allocate managed stream infrastructure. Create a persistent dump buffer field on the MonoBehaviour; rejected because 1403's primary contract is removing direct lifecycle-sensitive storage from MonoBehaviour state.
Scalability potential: Low devices avoid managed fault-path stream allocation and worker state churn. Middle devices keep the same IK telemetry evidence without changing solve math. High and Ultra keep identical telemetry density; no IK target math, ladder AUP route, DataVault buffer layout, gameplay truth, or continuous `GlobalQualityWeight` behavior changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower fault-path allocation pressure and safer lifecycle behavior on i3/MX350-class hardware.

## Decision 062 - Leviathan IK Fault Dumps Must Use The Shared Native Writer Facade

Problem: `LeviathanTerrainIkBlackBox.TryDumpTelemetry` still used `System.IO`, `Directory.CreateDirectory`, `FileStream`, and `BinaryWriter` for a critical IK postmortem dump. The data was already fixed-size native telemetry, so managed stream infrastructure added avoidable fault-path allocation and another assembly-local writer pattern.
Solution: Replace the stream writer with a method-local Temp `NativeArray<byte>` payload: 20-byte fixed header plus 300 records at the existing 96-byte telemetry layout. All fields are written little-endian with explicit byte stores, including `double3` via `BitConverter.DoubleToInt64Bits`. Submission goes through `NativeFaultDumpWriter.TryWriteAll`, keeping the unsafe bridge inside Core.
Rejected Alternatives: Keep `BinaryWriter` because the path is fault-only; rejected because the project now has a native fault writer facade and IK crash paths must not allocate managed stream objects. Add a persistent dump buffer field to the IK owner; rejected because the payload is rare, bounded, and 1403 is removing lifecycle-sensitive storage from MonoBehaviour state. Change the record layout; rejected because external postmortem readers depend on the declared 96-byte record size.
Scalability potential: Low devices avoid managed stream allocation during Leviathan IK fault capture. Middle devices keep the same telemetry density and binary layout. High and Ultra keep identical IK solve math, terrain push data, AUP state, and continuous `GlobalQualityWeight` behavior.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is lower emergency-path allocation pressure on i3/MX350-class hardware.

## Decision 063 - GlobalTelemetryBus Blackbox Must Finish The Native-Only Cutover

Problem: The gated Core build exposed an incomplete blackbox/MMF refactor in `GlobalTelemetryBus`: file IO usings, fields, and constants had been removed, but methods still referenced `Path`, `Directory`, `FileStream`, `MemoryMappedFile`, `_blackboxMmfSignal`, `_blackboxMmfThread`, and related state. That left the Core assembly uncompilable and would have reintroduced managed file IO if fixed by simply restoring usings.
Solution: Complete the cutover instead of restoring managed stream dependencies. The memory-breach event now routes to `RequestBlackboxEmergencyDumpAsync`. The legacy MMF scratch lane is sized for a complete dump: `BlackboxDumpHeaderBytes + desiredFrameCount * BlackboxFrameStrideBytes`. `CommitBlackboxDumpInMemory` now stages the header plus oldest-to-newest frames into that existing native scratch and writes one fixed dump through `NativeFaultDumpWriter.TryWriteAll`.
Rejected Alternatives: Restore `System.IO` and `System.IO.MemoryMappedFiles`; rejected because it would compile by reversing the native-only direction. Leave memory-breach flush as a no-op; rejected because it would remove the crash proof path. Allocate a Temp dump buffer from the background watchdog thread; rejected because the existing crash scratch lane can be cold-sized once and reused without thread-local native allocation.
Scalability potential: Low devices avoid managed stream/MMF worker allocation and a background IO thread for blackbox flush. Middle devices retain a bounded native dump route. High and Ultra keep the same frame payload, watchdog, deterministic hash, source payload, and `GlobalQualityWeight` contracts.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit is compile restoration plus removal of managed blackbox writer residue under memory-breach/watchdog fault pressure.

## Decision 064 - Static Save Buffer Waiters Must Fail Closed On Dispose Faults

Problem: The serialized static save write-buffer lease removed the 135 MB contention fallback, but it introduced a remaining failure-mode risk. If `StaticNativeBuffers.DisposeIfRequestedAndIdle` hit an exception while disposing static native buffers, `s_disposeRequested` stayed true. Any thread inside `AcquireWriteBuffers` would keep waiting on `Monitor.Wait(Sync)` with no state transition that could ever make the predicate false.
Solution: Add a cold fault latch, `s_disposeException`. `AcquireWriteBuffers` checks that latch before entering the wait loop and after every wake. `DisposeIfRequestedAndIdle` stores the first dispose exception, pulses all waiters, and rethrows. A later successful dispose clears the latch and request flag. The failure mode becomes explicit fail-closed instead of a silent deadlock.
Rejected Alternatives: Clear `s_disposeRequested` even when disposal fails; rejected because callers could reuse partially disposed or untracked native buffers. Swallow the exception; rejected because it hides the actual native lifecycle fault. Add a timeout to `Monitor.Wait`; rejected because it converts a deterministic disposal fault into timing-dependent behavior.
Scalability potential: Low devices avoid a static save/read repair deadlock after a native dispose failure. Middle devices get deterministic fault propagation. High and Ultra keep the same save binary route and compression path; no save format, DTO layout, gameplay truth, or `GlobalQualityWeight` contract changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is deadlock removal under native disposal fault pressure.

## Decision 065 - Persistent Native Owners Must Assign Only After Sentinel Registration

Problem: The 1403 owner wrappers had removed direct `NativeArray` fields from MonoBehaviour class depth, but persistent cold allocations still followed the unsafe sequence `field = new NativeArray` then `NativeMemorySentinel.RegisterNativeArray(field, ...)`. If registration or allocation-adjacent sentinel code threw after the field assignment, the owner could retain a created buffer whose registration proof did not exist.
Solution: Add cold `CreatePersistentNativeArray` helpers in the three target files. The helper creates the array in a local variable, registers that local array with `NativeMemorySentinel`, returns it only after registration succeeds, and disposes the local allocation in `catch` before rethrow. `SaveManagerNativeBufferSet`, `SaveManager.StaticNativeBuffers`, `RigNativeBufferSet`, and `RuntimeNativeBufferSet` now assign persistent fields only from this helper. The load-time `GetComponent<Rigidbody>()` fallback was also replaced with `TryGetComponent` to remove the last direct non-Try component lookup from the target scan.
Rejected Alternatives: Leave the assignment/register ordering because registration rarely fails; rejected because the failure mode is exactly an invisible native allocation. Wrap each individual allocation site with bespoke try/catch; rejected because repeated local boilerplate is easier to drift. Move IK owner arrays into `GlobalDataVault`; rejected because these buffers are owner-local job/presentation state, not cross-domain sovereign truth.
Scalability potential: Low devices get deterministic fail-closed behavior under native allocation/sentinel pressure instead of leaked or invisible buffers. Middle devices keep the same save and IK behavior. High and Ultra keep identical binary save layout, IK job payloads, matrix math, telemetry DTOs, and continuous `GlobalQualityWeight` contracts.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is lifecycle integrity under cold allocation failure and scene-unload fault pressure on i3/MX350-class hardware.

## Decision 066 - Hot Phases Must Consume Cached Vault Views, Not Resolve Them

Problem: The previous tether and celestial fixes still left hot-reachable DataVault resolution. `TetherManager.FixedTick` could reach `TryResolveHandle` through the Shinobu143 AUP mock buffer opener, and `HectonCelestialEngine.LateFrameTick` could resolve the orbit output when completing the deferred orbit job. `HectonCelestialEngine.SlowTick` also still repaired the atmosphere LUT through an `Ensure*` path.
Solution: Cache the needed native views from cold bootstrap and hot-swap routes. `Shinobu143AupBufferViews` stores the resolved AUP mock buffers after `EnsureShinobu143AupBootstrap`; the physics phase only validates vault identity, vault generation, compaction fence, and fixed capacities under a single aggregate mutation guard. `CelestialPresentationBufferViews` stores blackbox, gradient, and orbit output views after cold presentation handle setup; `LateFrameTick` only reads the cached orbit output view. Slow LUT repair is now readiness-only and fails closed if the cold-owned texture is missing.
Rejected Alternatives: Keep `TryResolveHandle` in hot methods because the aggregate guard is held; rejected because the phase proof still contains a DataVault mutable resolve in physics/visual sync. Use multiple `TryAcquireWriteLock` calls for the AUP buffers; rejected because it would violate the one-lock proof. Recreate the celestial LUT from `SlowTick`; rejected because runtime repair cannot allocate presentation resources.
Scalability potential: Low devices avoid physics/visual DataVault resolve stalls and surprise LUT recreation. Middle devices fail closed on stale generation or missing cold resources. High and Ultra keep the same AUP mock solve, celestial orbit math, LUT fidelity, and continuous `GlobalQualityWeight` contracts once resources are cold-resident.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is lower hot-phase stall risk on i3/MX350-class hardware.

## Decision 067 - Native Fault Writers Must Not Be Stubs

Problem: `NativeFaultDumpWriter` had been reverted to a no-op success predicate, and `LeviathanTerrainIkBlackBox.TryDumpTelemetry` had become a loop that read `FrameIndex` and returned true. That meant blackbox callers could report success without a dump payload ever reaching the native IO route.
Solution: `NativeFaultDumpWriter.TryWriteAll` now bridges a validated `NativeArray<byte>` payload to `AsyncWriteManager.WriteAll` through `NativeArrayUnsafeUtility`. `TetherBlackBoxDumpWriter` stages a fixed 32-byte header plus oldest-to-newest raw unmanaged records into a Temp `NativeArray<byte>` and writes both primary and legacy paths through the shared writer. `LeviathanTerrainIkBlackBox` stages its declared 20-byte header and 300 fixed 96-byte little-endian telemetry records, disposes the Temp payload in `finally`, and writes through the same facade.
Rejected Alternatives: Leave the writer disabled because dumps are rare; rejected because a blackbox success without bytes is fake evidence. Restore `FileStream`/`BinaryWriter`; rejected because the project already has native save IO and managed stream residue is banned from fault paths. Add persistent dump buffers to the MonoBehaviours; rejected because fault payloads are bounded and local Temp native staging is enough.
Scalability potential: Low devices avoid managed stream allocation during crash/fault capture. Middle devices retain deterministic postmortem bytes. High and Ultra keep the same telemetry density and binary layouts; no gameplay truth, DTO layout, save identity, or `GlobalQualityWeight` behavior changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit is honest crash-path evidence with no managed writer allocation.

## Decision 068 - Sentinel Registration Return Code Must Be Fatal For Persistent Owners

Problem: The 1403 persistent owner helpers already allocated into a method-local `NativeArray` and disposed on thrown registration failures, but `NativeMemorySentinel.RegisterNativeArray` can fail without throwing. It returns `0` for an uncreated array and `RegisterPointer` returns `0` when the sentinel registry reaches `MaxTrackedAllocations=1024`. Ignoring that return value could still expose a persistent SaveManager or IK buffer with no sentinel proof.
Solution: Require `registrationId > 0` in `SaveManager.CreatePersistentNativeArray`, `ContextualPhysicalIkRig.CreatePersistentNativeArray`, and `ContextualPhysicalIkRuntime.CreatePersistentNativeArray`. A zero or negative id now throws a static `InvalidOperationException` on the cold allocation failure path; the existing `catch` disposes the method-local allocation before any owner field is assigned.
Rejected Alternatives: Keep ignoring the return value; rejected because it converts registry exhaustion into invisible persistent memory. Add logging only; rejected because the owner would still expose an untracked buffer. Change `NativeMemorySentinel.RegisterNativeArray` globally to throw; rejected because many out-of-domain callers treat `0` as a non-throwing failure code and this pass is scoped to the 1403 owners.
Scalability potential: Low devices fail closed under sentinel capacity pressure instead of leaking or hiding persistent native buffers. Middle devices keep identical save and IK behavior. High and Ultra keep existing binary save layout, IK matrix math, job payloads, blackbox DTOs, and continuous `GlobalQualityWeight` contracts.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit on i3/MX350 is bounded native-memory accounting during cold allocation pressure and cleaner scene-unload diagnostics.

## Decision 069 - Sentinel Restore And Transient Registration Must Fail Closed

Problem: The 1403 owners had one remaining lifecycle hole after registration-id hardening. If a disposal route unregistered a `NativeArray` from `NativeMemorySentinel` and then `Dispose` failed, the restore path re-registered without checking the returned id and swallowed restore failure. `ContextualPhysicalIkRuntime.RuntimeNativeBufferSet.Dispose()` could also throw from an inner dispose path before draining an already accumulated deferred disposal handle. Save/load transient arrays were still created and then registered as two separate operations at call sites, leaving registration failure handling dependent on every caller's surrounding `finally`.
Solution: Make restore explicit and fatal: `RestoreNativeSentinelRecordOrThrow` re-registers only when unregister succeeded and the array is still created, requires `registrationId > 0`, and reports both disposal and restore failures via `AggregateException`. Runtime no-arg disposal now drains the scheduled handle in `finally` before surfacing the first failure. SaveManager transient arrays now use `CreateTransientNativeArray`, which creates into a method-local variable, registers through the same positive-id check, and disposes the local array on failure before caller ownership is established.
Rejected Alternatives: Leave restore best-effort; rejected because a failed dispose would erase the only sentinel proof. Complete only the happy-path dispose handle; rejected because partial scheduling can leave native memory release work behind a thrown exception. Keep call-site `new NativeArray` plus `RegisterTransientNativeArray`; rejected because the safer helper removes repeated failure-order assumptions without changing save DTOs or serialization routes.
Scalability potential: Low devices fail closed under sentinel pressure or scene-unload disposal faults instead of hiding native memory. Middle devices keep the same save/load and IK behavior with cleaner crash diagnostics. High and Ultra retain the existing binary save format, IK job payloads, blackbox DTOs, and continuous `GlobalQualityWeight` behavior; no gameplay truth or authority route changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit on i3/MX350-class hardware is lower leak risk and deterministic fault propagation during cold allocation, save/load repair, and IK teardown pressure.

## Decision 070 - Blackbox Success Must Mean Bytes Were Submitted

Problem: Several critical dump routes were overwritten back to validator stubs during concurrent workspace activity. The methods returned success after reading one ring element (`Frame`, `FrameIndex`, or `PositionHash`) and never submitted bytes to the native IO route. This breaks the black-box contract worse than a managed writer because it creates false postmortem proof.
Solution: Restore real native payload submission. `NativeFaultDumpWriter` validates path/payload/count and calls `AsyncWriteManager.WriteAll`. Tether, Leviathan IK, VR hand IK, AUP precision, Homeostasis, ladder IK, marauder outpost, contextual physical IK, and topographical sonar now stage bounded `Allocator.Temp` byte payloads, write fixed little-endian headers/records or raw fixed-stride records, dispose in `finally`, and submit through the shared facade.
Rejected Alternatives: Leave stubs because dump paths are fault-only; rejected because success without bytes is fake evidence. Restore `FileStream`/`BinaryWriter`; rejected because native save IO already exists and managed stream allocation is banned in crash paths. Add persistent MonoBehaviour dump buffers; rejected because these payloads are bounded and local staging avoids new lifecycle ownership.
Scalability potential: Low devices avoid managed stream allocation and false crash reports under fault pressure. Middle devices retain deterministic postmortem bytes. High and Ultra keep the same telemetry density and layouts; no gameplay truth, save identity, DTO ownership, or continuous `GlobalQualityWeight` behavior changed.
Hardware Impact: Measured runtime microseconds saved: 0 in source-only verification. Expected benefit on i3/MX350 is lower fault-path allocation pressure and honest telemetry evidence instead of silent no-op dumps.

## Decision 071 - IK Blackbox Dumps Must Write Bytes And Own Temp Lifetime

Problem: `ContextualPhysicalIkRuntime.DumpTelemetry` had regressed from a real dump writer into a loop that only touched telemetry `Frame` values. That produced no `Dump_1403_CONTEXTUAL_PHYSICAL_IK.bin` payload on invalid IK telemetry. The same area also needed transient sentinel ownership if the Temp payload was restored as a real writer.
Solution: Restore the fixed little-endian dump path: 24-byte header plus the existing 300-entry, 96-byte telemetry ring, written oldest-to-newest into a method-local Temp `NativeArray<byte>`. The Temp payload is created through `CreateTransientNativeArray`, registered with `NativeMemorySentinel` under `NativeAllocationLifetime.TransientArena`, and disposed through `DisposeTransientNativeArray`; if unregister succeeds and dispose fails, restore re-registers with the same transient lifetime and requires a positive registration id.
Rejected Alternatives: Keep the fake frame-read loop; rejected because a blackbox success path without bytes is false evidence. Use `FileStream` or `BinaryWriter`; rejected because the project already has `NativeFaultDumpWriter` and managed stream residue is banned from fault paths. Add a persistent dump buffer to the MonoBehaviour; rejected because the payload is bounded, rare, and should not add lifecycle-sensitive physical storage to the component.
Scalability potential: Low devices get deterministic fault evidence without managed stream allocation. Middle devices keep the same contextual IK cadence and solve data. High and Ultra keep identical IK telemetry density, binary layout, job payloads, and continuous `GlobalQualityWeight` behavior; no gameplay truth, authority route, save identity, or DTO layout changed.
Hardware Impact: Measured runtime microseconds saved: 0. Expected benefit on i3/MX350-class hardware is honest postmortem output with bounded native Temp lifetime and no managed writer allocation.
