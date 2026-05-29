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
