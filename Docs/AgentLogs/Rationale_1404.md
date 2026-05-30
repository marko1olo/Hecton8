# Rationale_1404

Status: PENDING ANDROID PLAYER BUILD / COMPILE GATE BLOCKED

## Initial Mandate Selection

Problem: Android cannot treat `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` as a normal disk file inside APK/OBB packaging, so a Windows-only file loader can soft-lock boot.

Solution: Use an Android-only native AAssetManager bridge that reads into caller-owned unmanaged memory and keep Windows/editor branches isolated behind compile-time gates.

Rejected Alternatives: UnityWebRequest was rejected because the task requires direct unmanaged hydration and AGENTS forbids UnityWebRequest without explicit task. Managed `byte[]` staging was rejected because it violates zero-copy hydration. Replacing the existing Win32 path was rejected because cross-platform parity requires preserving the optimized PC path.

Scalability potential: Low devices use the same single cold boot read with no gameplay hot-path cost. Middle/High/Ultra devices do not need a different gameplay truth route; saved boot allocations can be spent on richer presentation after Data Monolith boot succeeds.

Hardware Impact: Expected gain for i3/MX350-class and mobile ARM64 is removal of managed file fallback failure/GC pressure during boot. No measured microseconds yet; evidence class is STATIC_SOURCE until compile/player proof exists.

## Decisions

### Task 01-05 Archaeology And Plan

Problem: `GameBootstrapper.InitializeMemoryPreWarmPhaseAsync()` calls `InitializeBootstrapDataMonolithAsync()`, which calls `H8StaticDataArena.TryInitializeFromStreamingAssetsAsync(_globalDataVault, ...)`. The existing Windows player path reaches `CreateFileW`/`ReadFile`; the generic async URI path stages with `UnityWebRequest`/`DownloadHandlerFile`, which is wrong for the requested Android zero-copy bridge.

Solution: Preserve the Windows branch and insert an Android-only branch before WebGL/URI staging. The destination pointer remains the existing DataVault-backed arena from `TryAllocateArena()` and `NativeArrayUnsafeUtility.GetUnsafePtr(arena)`. The Android branch resolves the Java `AssetManager` during MemoryPreWarm after UnityPlayer activity exists, gets size through native code, allocates the existing DataVault buffer once, then passes the raw pointer to native `AAsset_read`.

Rejected Alternatives: Replacing the Windows path was rejected because parity requires keeping the proven Win32 route. `UnityWebRequest` cache staging was rejected because it adds a disk/cache copy and does not satisfy zero-copy unmanaged hydration. Passing a managed `string` through P/Invoke was rejected because marshaling can allocate conversion buffers; the asset name is written into stack memory as ASCII/UTF-8.

Scalability potential: Low/Middle/High/Ultra use the same cold boot route; quality scaling does not alter static-data truth. Low devices avoid APK URI staging. High/Ultra can spend saved boot memory pressure on presentation assets after boot.

Hardware Impact: i3/MX350 and Android ARM64 avoid one managed URL staging path and one file-cache readback. Measured runtime gain absent; estimate remains STATIC_SOURCE only.

### JNI And Native Pointer Boundary

Problem: C# cannot legally manufacture an `AAssetManager*`; Unity exposes Java objects, not the native NDK pointer.

Solution: C# uses raw `AndroidJNI.FindClass("com/unity3d/player/UnityPlayer")`, `GetStaticFieldID`, `GetStaticObjectField`, `GetMethodID`, and `CallObjectMethodUnsafe` to obtain the Java `AssetManager` jobject. C# also passes `AndroidJNI.GetJavaVM()` to the native bridge. C++ receives `JavaVM*` and the Java `AssetManager` jobject as explicit arguments, resolves `JNIEnv*` per call, and converts with `AAssetManager_fromJava` inside the native call. The UnityPlayer class, Activity, Activity class, and AssetManager local refs are deleted after native read completes.

Rejected Alternatives: `AndroidJNI.GetPlatformWindow()` was rejected because it returns the window, not `AssetManager`. `AndroidJavaClass` was rejected after APEX re-scan because it is a managed wrapper allocation. Caching a native `AAssetManager*` globally was rejected because activity/resource lifecycle ownership is Java-side and stale native pointers are unacceptable. JNI global refs and `JNI_OnLoad`-only `JavaVM*` cache were rejected because no cross-frame retention is needed and Unity P/Invoke load order is not a proof artifact.

Scalability potential: One JNI crossing for size and one for load during boot. No gameplay-frame cost on any tier.

Hardware Impact: JNI cost is cold boot only. No measurable frame microseconds claimed.

### Native AAsset Read Safety

Problem: Native code can corrupt the DataVault if the APK asset length exceeds the destination capacity, can leave stale tail bytes if the second asset open is shorter than the prior size query, or can leak if `AAsset_close` is skipped on failure.

Solution: `H8_GetAssetSize` returns signed error codes; `H8_LoadAssetToPointer` rejects null pointers, empty names, non-positive buffers, bad asset length, and `assetLength != bufferSize` before reading. The read loop advances a `uint8_t*` cursor in 1 MiB chunks and calls `AAsset_close` on success and every failure branch.

Rejected Alternatives: `AAsset_getBuffer`/mmap was rejected because compressed APK entries may not provide a stable direct buffer. `std::vector`, `malloc`, and managed `byte[]` staging were rejected because ownership must remain the DataVault arena.

Scalability potential: Low devices read the same bytes with bounded chunks. High/Ultra do not bloat static DTOs; visual overkill belongs above this truth layer.

Hardware Impact: Prevents native overflow and partial-tail validation crash classes on ARM64. Microsecond savings unmeasured; static proof shows removed heap staging tokens.

### Packaging And Compression

Problem: `.h8bin` compressed inside APK forces decompression work below the NDK API and prevents direct predictable asset streaming.

Solution: `Assets/Plugins/Android/mainTemplate.gradle` now preserves the Unity 6000.4 default no-compress expression and appends `.h8bin`: `noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ') + ['h8bin']`. `.gitignore` explicitly unignores this active custom template so the Gradle correction can be versioned.

Rejected Alternatives: Depending on default StreamingAssets compression was rejected because default APK packaging is not a proof artifact. Runtime decompression into an intermediate buffer was rejected because it violates zero-copy hydration. The earlier `noCompress += ['h8bin']` edit was rejected because it dropped Unity's built-in no-compress list.

Scalability potential: Weak devices avoid zlib work during boot; high-tier devices gain no different truth route.

Hardware Impact: Expected Android boot CPU and memory pressure reduction. Player build proof absent.

### Static Audit And Report

Problem: Platform claims from prose are rejected by `QA_Evidence_Text_Filter_Audit`.

Solution: Added `H8AndroidAssetBridgeStaticAudit.cs` to scan Android guard isolation, native AAsset API presence, overflow guard, no native heap staging tokens, CMake links, Gradle no-compress, and SHA-256 hashes.

Rejected Alternatives: Chat-only report was rejected. Claiming Android readiness from source scan was rejected; report status stays `PENDING_ANDROID_PLAYER_BUILD`.

Scalability potential: Audit has no runtime cost; it is editor-only.

Hardware Impact: No runtime hardware impact.

### Compile Gate

Problem: The final `dotnet build` gate would violate host-resource law because CPU load sampled at 100 twice. The first sample also found existing `dotnet` process PID 33312 active.

Solution: Did not launch build. Marked Task 15 as `BLOCKED_BY_CONTENTION`; retained static source evidence only.

Rejected Alternatives: Launching another compiler process was rejected because AGENTS and batch prompt explicitly forbid build under CPU >50 or active compiler/dotnet contention.

Scalability potential: No runtime effect.

Hardware Impact: Avoided additional host CPU contention. No compile proof produced.

### APEX JNI VM Ownership Correction

Problem: The native bridge depended on `JNI_OnLoad` to cache `JavaVM*`. That is not a valid proof for Unity P/Invoke packaging because a shared library can be loaded through the native loader path without the managed route proving that `g_javaVm` was populated before `H8_GetAssetSize`. Failure mode was fail-closed `H8_ERROR_JNI_ENVIRONMENT`, not a segfault, but Android Data Monolith would still not load.

Solution: C# now calls `AndroidJNI.GetJavaVM()` and passes the returned pointer to both `H8_GetAssetSize` and `H8_LoadAssetToPointer`. C++ resolves `JNIEnv*` from that explicit pointer per call, attaches only if the current thread is detached, and detaches only when this call attached it. `JNI_OnLoad` and `g_javaVm` are gone.

Rejected Alternatives: Keeping `JNI_OnLoad` as the sole route was rejected because it made runtime success depend on loader behavior outside this bridge. Caching a global `AAssetManager*` was rejected because the Java Activity/resource lifecycle remains the owner. Passing a P/Invoke string was rejected because filename marshaling is avoidable.

Scalability potential: Low/Middle/High/Ultra all use the same cold boot truth route. This is not visual fidelity code; quality scalar must not alter static DTO identity. Saved boot reliability budget can be spent above the data layer after boot.

Hardware Impact: Prevents Android boot false-negative on ARM64. Microseconds saved are unmeasured; this is correctness and crash-risk removal, not a frame optimization.

### APEX DataVault Writer Fence Correction

Problem: The previous Android and Win32/native write paths resolved the Data Monolith payload with `TryResolveHandle` and then passed a raw pointer to native/memcpy code. That violated the current Memory Sovereignty rule requiring mutable resolutions to be fenced and released in `finally`.

Solution: Added `TryAcquireArenaWriteView` and `ReleaseArenaWriteView`. Four payload writes now acquire `TryAcquireWriteLock(in _arenaHandle, SystemID.CoreDataVault, out arena)` and four releases occur in `finally`. Success is returned only after `ReleaseWriteLock` reports true. The old mutable `TryRefreshArenaView` helper was removed.

Rejected Alternatives: Locking only the Android branch was rejected because the same raw pointer hazard existed in existing Win32/native copy paths. Holding the lock through validation was rejected because the external raw pointer lifetime ends immediately after the read/copy, and validation uses the existing read-only route.

Scalability potential: No runtime frame cost; all writes occur during cold boot/import. Weak devices gain relocation safety during boot; high-tier devices get the same data truth and spend quality elsewhere.

Hardware Impact: Removes a relocation/deadlock class around external native pointer ownership. No frame microseconds claimed.

### APEX Unity 6000 Android Source Plugin Route Correction

Problem: Subagent review found the active custom `mainTemplate.gradle` was wrong for Unity 6000.4: it used `com.android.application`, legacy `**MINSDKVERSION**`/`**TARGETSDKVERSION**`/`**PACKAGING_OPTIONS**` tokens, replaced Unity's default no-compress list, and stacked a second `externalNativeBuild` CMake root beside Unity's own IL2CPP native build placeholders. That route could fail Gradle generation before the bridge could be linked.

Solution: `Assets/Plugins/Android/mainTemplate.gradle` is restored to the Unity 6000.4 `unityLibrary` shape: `apply plugin: 'com.android.library'`, `**MINSDK**`, `**TARGETSDK**`, `}**PACKAGING**`, `**IL_CPP_BUILD_SETUP**`, `**SOURCE_BUILD_SETUP**`, and `**EXTERNAL_SOURCES**`. The active bridge now uses Unity's Android IL2CPP source-plugin route, so the C# binding changed from `DllImport("HectonAndroidBridge")` to `DllImport("__Internal")`. `CMakeLists.txt` remains as a standalone NDK reference for the required native target, but it is not injected as a competing Gradle CMake root.

Rejected Alternatives: Keeping `externalNativeBuild` was rejected because Unity already owns IL2CPP native source build wiring through template placeholders. Keeping `DllImport("HectonAndroidBridge")` was rejected because no packaged `.so` exists. Precompiled `.so` files were rejected because no local NDK build artifact exists in this batch.

Scalability potential: Packaging route is platform infrastructure, not quality-tier logic. It keeps low-tier Android and Quest from boot-failing before gameplay systems can scale.

Hardware Impact: No runtime frame cost. Prevents Gradle-template generation failure and missing-symbol boot failure pending Android IL2CPP build proof.

### APEX Zero-GC And Continuous Scalability Audit

Problem: A blanket "zero allocation" claim would have been false before the second APEX pass. The Android boot branch had one cold `new AndroidJavaClass(...)` wrapper, which was not a hot path but was still a real reference-type allocation token.

Solution: Replaced `AndroidJavaClass` with raw `AndroidJNI.FindClass` and explicit local-ref cleanup. Text scan result is recorded in `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json`: modified runtime hot-path method count is 0; Android guarded branch has `new` count 0, reference-type `new` count 0, and zero `string.Format`, `.ToString()`, LINQ tokens, `foreach`, `new jvalue`, or `AndroidJavaClass`; native bridge has zero heap tokens (`new`, `std::vector`, `std::string`, `malloc`, `free`, `delete`).

Rejected Alternatives: Pretending the AndroidJavaClass wrapper was zero allocation was rejected. Adding `HomeostasisBrain.GlobalQualityWeight` to static-data truth was rejected because project law says quality may not change DTO layout, save identity, or authority route.

Scalability potential: Data Monolith loading is a continuous-quality invariant. Low/Middle/High/Ultra may scale consumers and presentation after data readiness, but the byte payload and validation rules stay identical. No binary `isLowEnd` switch was introduced.

Hardware Impact: Android native loader adds no per-frame GC. Boot allocation proof remains STATIC_SOURCE until Unity player profiler/GCMonitor evidence exists.

### APEX Telemetry Sovereignty And Dump Route Correction

Problem: Telemetry ring and cursor writes still needed the same write-lock proof as payload hydration. A crash-path proof also had to name the agent-specific dump path, not only shared legacy dump filenames.

Solution: Added `TryReadTelemetry`, `TryAcquireTelemetryWriteViews`, and `ReleaseTelemetryWriteViews`. `EnsureTelemetry` and `DumpTelemetry` use read-only handles. `RecordTelemetry` acquires the telemetry ring and cursor write locks before mutation and releases through `finally`. Partial-acquisition cleanup also releases in `finally`. `DumpTelemetry` now writes `Docs/AgentLogs/Dump_1404.bin` in editor/development and Win32 diagnostic dump routes.

Rejected Alternatives: Leaving telemetry outside DataVault locks was rejected because black-box evidence is still mutable cross-domain native state. Keeping only shared dump filenames was rejected because the batch protocol requires a precise `Dump_1404` evidence route.

Scalability potential: Telemetry writes are cold boot/failure diagnostics, not gameplay hot path. Low/Middle/High/Ultra use the same black-box schema; quality scalar does not change fault evidence identity.

Hardware Impact: No frame-time claim. The correction removes a mutable handle discipline violation and improves crash forensics.

### APEX Final Proof Refresh 2026-05-28

Problem: The prior JSON report hash was stale after the Unity 6000 Gradle/source-plugin correction, exact native length guard, and `.gitignore` exception. Reporting the old hash would be evidence fraud.

Solution: Regenerated `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` after parsing the current source and refreshed the compile-throttle sample. Current report SHA-256 is `4f88ef3f8f3983eeef551ccc67a3cdd27d8d12702197de73bd3ed6feddf1d5d1`. Static scan recorded Android guarded `new` count 0, reference-type `new` count 0, native heap token counts 0, payload write-lock acquire/release/finally counts 4/4/4, and latest CPU gate 100 with active `dotnet` PID 55080. Build remained blocked by CPU > 50 and active compiler contention.

Rejected Alternatives: Running `dotnet build` under 100% CPU and active `dotnet` contention was rejected by Compilation Resource Throttling. Claiming `Dump_1404.bin` existence was rejected; the source route exists, but the dump file is absent until runtime telemetry dump executes.

Scalability potential: No `HomeostasisBrain.GlobalQualityWeight` logic was added because Data Monolith bytes, DTO layout, and authority route are gameplay truth and must not scale by quality. Low/Middle/High/Ultra differ only in downstream consumers and presentation budgets after static data readiness.

Hardware Impact: Host build was not launched; no player/device microseconds claimed. Runtime impact remains cold-boot-only and unmeasured until Android IL2CPP build, launch, profiler, and GC artifacts exist.

### APEX Repeat Audit Fixes 2026-05-28

Problem: A second audit found three real defects. `DumpTelemetry` was a no-op in Android release because only production Win32 and editor/development branches wrote files. The same dump method wrote unrelated agent-owned dump files (`Dump_SHINOBU_103`, `Dump_X_002`, `Dump_1313`, `Dump_1330`) and therefore violated one-owner/one-route telemetry ownership. The raw `AndroidJNI` path checked zero handles but did not explicitly consume pending Java exceptions after `FindClass`, field lookup, object lookup, method lookup, or `CallObjectMethodUnsafe`.

Solution: Added `TryConsumePendingAndroidJniException`, using `AndroidJNI.ExceptionOccurred`, `ExceptionClear`, and `DeleteLocalRef` to convert pending JNI exceptions into deterministic fail-closed telemetry before continuing. Added an Android release dump route to `Application.persistentDataPath/Docs/AgentLogs/Dump_1404.bin`. Removed unrelated dump aliases from editor/development and production Win32 routes, leaving only owner-local `Dump_1404.bin`. Updated `NativePluginMatrixValidator` and `H8AndroidAssetBridgeStaticAudit` so future source gates reject missing JNI exception fences or reintroduced unrelated dump aliases.

Rejected Alternatives: Keeping pending JNI exceptions for outer `catch (AndroidJavaException)` was rejected because raw JNI can leave JVM exception state while returning zero handles. Writing all historical dump aliases was rejected because this Data Monolith owner must not overwrite unrelated agents' forensic files. Writing Android release dumps to workspace `Docs/AgentLogs` was rejected because packaged Android players do not have a writable project root; `persistentDataPath` is the only stable app-owned route.

Scalability potential: Diagnostics remain failure/cold-path only. No gameplay truth, DTO layout, or quality-tier behavior changes. Low/Middle/High/Ultra all get the same fail-closed Android bootstrap route; downstream presentation still owns visual overkill.

Hardware Impact: No frame-time or microsecond saving claimed. The patch reduces Android boot fault ambiguity and cross-agent forensic pollution; Android player proof is still absent.

### APEX Compile-Gate Sample Refresh 2026-05-28

Problem: The final JSON report needed to reflect the latest live compilation gate sample, not an older active-process CPU value.

Solution: Updated `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` build-throttle process CPU to the latest observed `dotnet` PID 55080 sample: host CPU load 100, process CPU 249.625, start `2026-05-28T04:58:33+04:00`. Parsed the JSON after patching and recorded SHA-256 `c81065228d5e90bd0b080e88e8aa8b23368251ef4cff76b969d4308443f5bf28`.

Rejected Alternatives: Running `dotnet build` was rejected because CPU remained above 50 and an active `dotnet` process was present. Leaving the prior JSON process CPU was rejected because the final proof artifact must match the last observed gate sample.

Scalability potential: No runtime behavior changes. This is proof hygiene only.

Hardware Impact: Host build was not launched; no compile artifact exists.

### APEX Android Native Dump And Release Ownership Fix 2026-05-28

Problem: The Android release dump route still used managed `Path.Combine`, `Directory.CreateDirectory`, `FileStream`, and `BinaryWriter` on a fault path. A subagent audit also proved a DataVault ownership flaw: if `ReleaseWriteLock` queued a deferred writer release and returned false, `ShutdownArenaOnly` could call `ReleaseBuffer`, receive false because `ActiveWriterSystemID` was still set, and then clear the generation handle anyway.

Solution: Added native `H8_WriteTelemetryDump` in `Assets/Plugins/Android/Native/HectonAndroidAssetBridge.cpp`. Android release now stack-encodes `Application.persistentDataPath`, passes a raw telemetry pointer, and native code creates `Docs/AgentLogs`, opens `Dump_1404.bin`, writes a 20-byte little-endian header plus raw 64-byte telemetry entries, and closes the descriptor. Added `ReleaseWriteLockWithRetry`, made telemetry and payload release paths use it, changed `ReleaseVaultHandle` to clear only after `ReleaseBuffer` succeeds, and made `TryAllocateArena` refuse to overwrite an unreleased payload handle.

Rejected Alternatives: Keeping managed Android diagnostic I/O was rejected because the user demanded proof against unmanaged/mobile fault-path instability. Clearing failed release handles was rejected because it hides an active writer and can orphan DataVault ownership. Blocking forever on a writer release was rejected because boot must fail closed under contention.

Scalability potential: This remains cold boot and crash-dump infrastructure. Low/Middle/High/Ultra devices get identical truth bytes and identical dump schema; visual quality scaling is deliberately outside this static-data authority route.

Hardware Impact: No frame-time saving claimed. The change removes Android release managed dump I/O and one handle-loss/orphan class; device microseconds remain unmeasured until Android player proof exists.

### APEX GameActivity Manifest Correction 2026-05-28

Problem: `ProjectSettings.asset` serialized `androidApplicationEntry: 2` while `Assets/Plugins/Android/AndroidManifest.xml` still declared `com.unity3d.player.UnityPlayerActivity`. That can produce a launch-route mismatch in Unity 6 GameActivity builds before the Data Monolith bridge is tested.

Solution: Updated the manifest activity to `com.unity3d.player.UnityPlayerGameActivity`, added `@style/BaseUnityGameActivityTheme`, and added `<meta-data android:name="android.app.lib_name" android:value="game" />`. Updated `NativePluginMatrixValidator` and `H8AndroidAssetBridgeStaticAudit` to reject the old mismatch.

Rejected Alternatives: Switching ProjectSettings back to Activity was rejected because the project already serialized GameActivity as the selected Android entry point and Unity documents GameActivity as the newer entry route. Leaving both routes ambiguous was rejected because release builds should have one entry point.

Scalability potential: GameActivity is platform launch plumbing, not gameplay-quality logic. It reduces boot ambiguity for low-tier Android and Quest while leaving downstream systems free to scale presentation with `GlobalQualityWeight`.

Hardware Impact: No measured runtime gain. Launch correctness proof still requires Android export/install/launch.

### APEX Report Refresh 2026-05-28 Native Dump Pass

Problem: The previous report hash `c81065228d5e90bd0b080e88e8aa8b23368251ef4cff76b969d4308443f5bf28` no longer described the source after native dump, writer-release retry, and GameActivity manifest fixes.

Solution: Regenerated `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json`. Current SHA-256 is `6ec50ef7ed5736a5e640c3da6cf498f9869714bf7b2cac8c2b00f4bb8c0a0f82`. The report records zero Android guarded `new`, zero reference-type `new`, zero `string.Format`, zero `.ToString()`, zero LINQ tokens, zero `foreach`, zero Android `Path.Combine(Application.persistentDataPath)`, zero Android FileStream/BinaryWriter tokens, native heap token counts all zero, payload lock acquire/release/finally counts 4/4/4, and latest build gate CPU 96 with active `dotnet` PID 32028.

Rejected Alternatives: Running `dotnet build` was rejected because CPU remained above 50 and an active `dotnet` process existed. Claiming runtime dump existence was rejected; `Docs/AgentLogs/Dump_1404.bin` is still absent until the runtime dump path executes.

Scalability potential: No `HomeostasisBrain.GlobalQualityWeight` logic was added to Data Monolith truth. Static DTO layout and authority route are invariant; downstream consumers own continuous fidelity scaling.

Hardware Impact: Host build was not launched. Runtime/device performance remains unmeasured.

### APEX Deferred Release Truth / Telemetry Dump Ordering Pass 2026-05-28

Problem: A deeper audit found four remaining proof defects. `GlobalDataVault.ReleaseWriteLock` could queue a deferred writer release and return `true`, which let callers treat an active writer fence as released. `DumpTelemetry` called `EnsureTelemetry`, so a crash/fault dump could allocate or grow DataVault telemetry buffers. Dump files wrote raw ring storage order instead of chronological last-300 order. Native `H8_EnsureDirectory` accepted `EEXIST` without proving the existing path was a directory.

Solution: `ReleaseWriteLock` and `ReleaseWriterBlockLock` now enqueue deferred writer release but return `false`, preserving fail-closed ownership semantics. `DumpTelemetry` now uses only `TryReadTelemetry`; the black-box dump path no longer creates telemetry buffers. C# editor/dev and Win32 dump writers normalize the telemetry cursor and rotate writes from cursor to end, then zero to cursor. Android native `H8_WriteTelemetryDump` performs the same cursor rotation without heap allocation. `H8_EnsureDirectory` now validates `EEXIST` with `stat` and `S_ISDIR`. Static validators were updated to reject regressions in these exact properties.

Rejected Alternatives: Keeping queued writer-release as success was rejected because it makes `TryAcquireWriteLock`/`ReleaseWriteLock` evidence untrustworthy. Creating telemetry buffers inside `DumpTelemetry` was rejected because crash/postmortem paths must be read-only. Raw ring order was rejected because it forces every forensic reader to reconstruct chronology. Accepting `EEXIST` without `S_ISDIR` was rejected because a file collision would redirect dump failure into later open/write calls.

Scalability potential: Low/Middle/High/Ultra devices use identical Data Monolith bytes, DTO layouts, BufferIDs, and dump schema. `HomeostasisBrain.GlobalQualityWeight` remains deliberately absent; this route is gameplay truth and crash evidence, not visual fidelity. Presentation consumers may scale after data readiness, but the authority path does not.

Hardware Impact: No frame-time or microsecond saving claimed. The patch removes ownership ambiguity and fault-path mutation on Android/Win32 without adding hot-path work. Latest build gate remains blocked: CPU 23 but active `dotnet` PID 34436 and `VBCSCompiler` PID 44300 exist, so `dotnet build` was not launched.

### APEX False-Positive Proof Correction / Final Compile Attempt 2026-05-28

Problem: The report claimed `GlobalDataVault` deferred writer release returned false, but a direct line audit found the source still had `return QueueDeferredWriterRelease(...)` at the writer release contention sites. That was a false-positive proof condition and would let callers interpret a queued release as a completed writer fence. A second issue remained: `ReleaseWriteLockWithRetry` retried only on Android even though the deferred-release contract is global.

Solution: Changed both `GlobalDataVault` contention branches to `_ = QueueDeferredWriterRelease(...); return false;`. Moved `DataMonolithWriterReleaseRetryCount` outside Android guards and made `ReleaseWriteLockWithRetry` retry on every platform. Updated `H8AndroidAssetBridgeStaticAudit` and `NativePluginMatrixValidator` to reject both `return QueueDeferredWriterRelease(...)` and Android-only release retry proof. Regenerated `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` and sidecar hash.

Rejected Alternatives: Keeping the report-only claim was rejected as evidence fraud. Keeping Android-only retry was rejected because Windows/editor can hit the same DataVault mutation gate contention. Launching a second build after the first failed was rejected because post-failure CPU sampled at 99.

Scalability potential: Low/Middle/High/Ultra devices use the same writer-fence truth and Data Monolith bytes. `GlobalQualityWeight` remains absent because static data ownership and DTO identity must not scale.

Hardware Impact: No frame-time improvement claimed. One final compile attempt was made only after CPU 35 and zero active compiler processes; it failed with exit code 1 and empty captured output. Forensic record: `Docs/AgentLogs/Dump_1404_build_failure_20260528T1306_SAMARA.log`.

### APEX Contract Reversal / Deferred Queue Hardening 2026-05-28

Problem: The previous 1404 conclusion was wrong. Agent 1414 owns the core allocator contract and its test/rationale explicitly define `ReleaseWriteLock` success as either synchronous release or accepted transfer into the deferred release queue. Replacing `return QueueDeferredWriterRelease(...)` with `return false` would break shared caller semantics and the 1414 test contract. A real defect remained: `QueueDeferredRelease` de-duped all kinds in the current source but did not serialize scan plus slot reservation, so two contending callers could publish duplicate pending release records.

Solution: Preserve `return QueueDeferredWriterRelease(...)` in both writer-release contention branches. Add `_deferredReleaseEnqueueGate`, acquire it with `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)`, run pending scan and slot reservation under that gate, and release it with `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally`. Keep all-kind de-duplication via `pending->Kind == kind`. Update 1404 validators and JSON proof to require queue acceptance plus serialized all-kind de-duplication, not the false-return contract.

Rejected Alternatives: Continuing to force `return false` was rejected because it contradicts `ArenaAllocatorSentinel1414EditTests` and can make callers treat a valid deferred release request as failed. Leaving scan/reserve unlocked was rejected because slot-level `CompareExchange` protects only slot ownership, not semantic duplicate release identity. Blocking until the release mutation gate opens was rejected because this path exists to stay non-blocking under allocator pressure.

Scalability potential: Low-tier devices avoid retry-spin pressure under allocator contention. Middle/High/Ultra keep the same bounded native ring and deterministic drain; no DTO layout or authority route changes. `GlobalQualityWeight` remains out of this truth path.

Hardware Impact: No frame microseconds claimed. The fix adds one atomic enqueue gate only on deferred release paths, removes duplicate pending-release corruption risk, and preserves non-blocking release behavior for weak CPUs.

### APEX Concurrent Source Drift Blocker 2026-05-28

Problem: The `_deferredReleaseEnqueueGate` patch was applied and verified more than once, but the shared `GlobalDataVault.cs` changed again during verification. Current source retains `return QueueDeferredWriterRelease(...)` and all-kind `pending->Kind == kind`, but no longer contains `_deferredReleaseEnqueueGate`, `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)`, or `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)`. The current 1404 report must therefore fail the queue-contract proof.

Solution: Stop claiming the hardening is present. Refresh `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` to `PENDING_CONCURRENT_SOURCE_DRIFT`, record `globalDataVaultDeferredWriterReleaseQueueContract=false`, `deferredReleaseEnqueueGateLine=0`, and SHA-256 `05e4e1cbe799585f06251c0f419f1f7eed601c5236876f135f98be6679e44377`. Keep 1404 validators requiring the gate so a future build/import gate catches the missing serialized enqueue contract.

Rejected Alternatives: Reapplying the same `GlobalDataVault` patch indefinitely was rejected after repeated source drift. Shipping stale `queueContract=true` JSON was rejected as evidence fraud. Weakening the validator to accept unlocked scan/reserve was rejected because it leaves duplicate pending-release corruption under contention.

Scalability potential: Low-tier devices remain most exposed to allocator contention. The intended fix is one atomic gate only on deferred release enqueue; no quality scalar is applicable because this is memory authority, not presentation.

Hardware Impact: No runtime gain claimed because the gate is not present in current source. Build was not launched: latest CPU gate was 74 with zero compiler processes, which still violates the CPU > 50 rule.

### APEX Writer-Only Deferred Queue Reconciliation 2026-05-28

Problem: A deeper contract read showed two different truths had been conflated. 1414 loop 9 originally argued all-kind de-dup, but 1414 loop 11 later corrected that buffer-pin releases are counted and duplicate-looking pin releases can be legitimate. Therefore 1404's all-kind validator was wrong. The actual remaining defect was narrower: writer-release duplicate scan and slot reservation needed `_deferredReleaseEnqueueGate`.

Solution: Preserve writer queue acceptance with `return QueueDeferredWriterRelease(...)`. Restore `_deferredReleaseEnqueueGate`, reset it during initialize/dispose, wrap writer duplicate scan and queue slot reservation with `while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) != 0) Thread.SpinWait(8)`, and release via `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally`. Keep writer-only de-duplication with `pending->Kind == DeferredReleaseKindWriter`. Update 1404 validators to require writer-only queue semantics and reject stale generic `pending->Kind == kind`.

Rejected Alternatives: All-kind de-duplication was rejected because it can collapse legitimate counted buffer-pin releases. Returning false while the enqueue gate is held was rejected because release callers treat false as unaccepted release. Leaving scan/reserve unlocked was rejected because two writer-release callers can publish duplicate pending writer records.

Scalability potential: Low-tier CPUs get deterministic writer release acceptance under allocator contention without managed locks or blocking OS primitives. Middle/High/Ultra retain the same fixed-size native ring and do not change Data Monolith truth, DTO layout, or quality route.

Hardware Impact: No profiler microseconds claimed. The added atomic spin gate is only on deferred release enqueue, not steady arena allocation or Android data loading. Build was not launched: latest report gate sample was CPU 88 with one active compiler process.

### APEX Stale Proof Artifact Correction 2026-05-28

Problem: `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` recorded `GlobalDataVault.cs` SHA-256 `5d3cfe4c916fa9547a313920aba8ce6d7ef4275ed3e26dfc00145a3b5fc2c4f1`, but the current source hash is `259674db0e7fd9216c65bc6c4cb49d64cc86e27cbaa616a76af3878ab31dcc96`. The source contract remained valid, but the proof artifact was stale and therefore not admissible as final evidence.

Solution: Regenerated the JSON report and sidecar. Current report SHA-256 is `a06c414068e9bb5cf659a71da1872e6743f9127602d4445638ad8f97fa0a92d3`. The refreshed report records current queue-return lines `1949` and `1981`, enqueue gate line `2061`, `finally` gate release line `2114`, writer-only de-dup line `2080`, generic de-dup line `0`, CPU sample `97`, and active compiler processes `csc` PID `43496` plus `dotnet` PID `35512`.

Rejected Alternatives: Leaving the stale JSON hash was rejected as evidence drift. Rewriting `GlobalDataVault` again was rejected because current source matches the 1414 writer-only queue contract and the 1404 validators. Running `dotnet build` was rejected because CPU was above 50 and active compiler processes existed.

Scalability potential: No gameplay quality route changed. Low/Middle/High/Ultra devices keep identical Data Monolith bytes, DTO layout, BufferIDs, and authority path; `GlobalQualityWeight` remains outside this truth layer.

Hardware Impact: No runtime microseconds claimed. This pass is proof hygiene plus build-throttle compliance only.

### APEX Concurrent Drift Repair And Final Hash Refresh 2026-05-28

Problem: A later report refresh caught a real regression: `GlobalDataVault.cs` changed to SHA-256 `8930026d041307e5c93c490ec4bafec6eb00bc995a05939e3626a9bc3e964cd2` and no longer contained `_deferredReleaseEnqueueGate`. The report correctly dropped `globalDataVaultDeferredWriterReleaseQueueContract` to false. That state would permit duplicate pending writer-release enqueue records under concurrent scan/reserve.

Solution: Restored `_deferredReleaseEnqueueGate`, initialize/dispose resets, `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)` around writer duplicate scan and slot reservation, and `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally`. Kept writer-only `pending->Kind == DeferredReleaseKindWriter` to match 1414 tests. Refreshed report to SHA-256 `3fad2f15cbde499c6b4646877b3d6c6d22fcc59b075c0cf32eebbc53ffcdcca7`; a 10-second source stability check held `GlobalDataVault.cs` hash `259674db0e7fd9216c65bc6c4cb49d64cc86e27cbaa616a76af3878ab31dcc96`.

Rejected Alternatives: Accepting the false queue contract was rejected because the 1414 test already defines the required writer-only serialized queue. All-kind de-duplication was still rejected because counted buffer-pin releases can be legitimate. Running `dotnet build` was rejected because the report gate sampled CPU `81`.

Scalability potential: Low devices get deterministic writer-release queue behavior under allocator contention. Middle/High/Ultra keep the same DataVault truth route; `GlobalQualityWeight` does not apply to memory authority or static-data identity.

Hardware Impact: No measured frame or boot microseconds. The change affects only deferred release contention, not steady frame logic.

### APEX Final Source Drift Repair And Test Gate 2026-05-28

Problem: A repeat audit found the same writer enqueue regression again: current `QueueDeferredRelease` used `bool enqueueGateAcquired = Interlocked.CompareExchange(...) == 0`. When the gate was not acquired, writer releases could still proceed to slot reservation without serialized duplicate scan. That violates the intended writer-only deferred release queue proof and made the previous JSON report inadmissible.

Solution: Replaced the nonblocking boolean gate with a strict writer-only spin gate: `while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) != 0) Thread.SpinWait(8);`. The gate is released in `finally` through `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)`. Added `Assets/_Project/Tests/Editor/AndroidAssetBridge1404EditTests.cs` to assert the JNI fail-closed route, native exact-size guard, absence of managed JNI wrappers/native heap staging, and strict DataVault writer enqueue gate.

Rejected Alternatives: Keeping `bool enqueueGateAcquired` was rejected because it preserves the duplicate writer-release enqueue race. All-kind de-duplication was still rejected because 1414 documents counted buffer-pin releases as legitimate. Returning false from `ReleaseWriteLock` was rejected because 1414 owns that contract and accepts queued writer release as successful transfer into the deferred queue.

Scalability potential: Low devices gain deterministic memory-release behavior under allocator contention without OS locks. Middle, High, and Ultra keep the same fixed DataVault authority route; this is not a visual fidelity path. `HomeostasisBrain.GlobalQualityWeight` is intentionally not used because memory ownership, DTO layout, and static-data identity must not scale by quality.

Hardware Impact: No measured microseconds. The cost is one writer-only atomic spin gate on deferred release contention. Final compile gate stayed closed: CPU 82 with zero active compiler processes, so `dotnet build` was not launched.

### APEX Concurrent Source Drift Final Blocker 2026-05-28

Problem: The strict writer-only enqueue gate was repaired four times, but current `GlobalDataVault.cs` reverted again to `bool enqueueGateAcquired`. Current SHA-256 is `56addb112a437a669c4e0b628ffad68a177d7e3f19c9e1dd6338c8ad647ea67b`; strict spin gate text is absent. This makes `Assets/_Project/Tests/Editor/AndroidAssetBridge1404EditTests.cs` a legitimate failing guard rather than a passed proof.

Solution: Stop claiming the fix is present. Refresh `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json` to `PENDING_CONCURRENT_SOURCE_DRIFT_AND_BUILD_BLOCKED_BY_CPU` with `globalDataVaultDeferredWriterReleaseQueueContract=false`. Preserve the editor test and validators so the regression is caught by the next stable integration pass.

Rejected Alternatives: Reapplying the same patch indefinitely was rejected after repeated overwrite. Removing the test was rejected because it would hide the current allocator contract defect. Publishing a report with stale source hash was rejected as evidence fraud.

Scalability potential: No quality path changed. The desired fix remains one contention-only atomic writer gate. `GlobalQualityWeight` remains deliberately irrelevant to DataVault memory authority.

Hardware Impact: No measured microseconds. Build was not launched: immediate pre-build sample wrote CPU `93`, active compiler processes `0`, and gate skipped the command in `Docs/AgentLogs/Dump_1404_build_after_source_drift_20260528T213058_SAMARA.log`.

### APEX Integrator Reconciliation 2026-05-29

Problem: The prior 1404 rationale mixed three incompatible contracts: false-return deferred release, all-kind de-duplication, and strict spin-based writer enqueue. Current source and current 1404/1414 editor tests reject that chain. `ReleaseWriteLock` success means synchronous release or accepted deferred ownership transfer. Writer release de-duplication is advisory and writer-only; duplicate writer release requests drain safely because `DrainDeferredWriterReleaseLocked` no-ops after metadata owner mismatch or unlocked state. Buffer-pin releases are counted and must not be collapsed by generic `pending->Kind == kind`.

Solution: Treat the other agent's rollback as correct for this contract. Keep `return QueueDeferredWriterRelease(...)`. Keep nonblocking `enqueueGateAcquired = Interlocked.CompareExchange(...) == 0`, because the release path must not spin under allocator pressure. Keep `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally` only when the gate was acquired. Do not regenerate JSON or binary dumps; the integrator mandate deprecates those artifacts as proof.

Rejected Alternatives: Reapplying `Thread.SpinWait` was rejected because active tests explicitly forbid it and it burns CPU in a release contention path. Reapplying `_ = QueueDeferredWriterRelease(...); return false;` was rejected because it lies to callers after the queue has accepted release ownership. All-kind de-duplication was rejected because counted buffer-pin release records can be legitimate.

Scalability potential: Low devices avoid CPU spin and keep release contention bounded. Middle, High, and Ultra keep the same DataVault authority route; no `GlobalQualityWeight` scaling applies to memory ownership, static DTO layout, save identity, or Android data truth.

Hardware Impact: No measured runtime microseconds. Static scan only. Latest sampled host CPU was `66`, active compiler process count `0`; build remained blocked by CPU > 50 and by the current no-spam static-validation mandate.

### APEX Integrator Source Patch 2026-05-29

Problem: `H8StaticDataArena` set `_residentBlobBytes` before read/copy success in four loader routes. On a failed file/native/memory/Android read, the failure telemetry path could briefly describe the payload as resident even though the bytes were untrusted and the arena was about to be torn down.

Solution: Move `_residentBlobBytes` assignment to the commit point after read/copy success and write-lock release, before `TryValidateResidentArena`, in file, memory, Android AAssetManager, and Windows native StreamingAssets loaders. Add an editor static guard that asserts this ordering.

Rejected Alternatives: Leaving the early assignment was rejected because resident byte count is data truth, not requested capacity. Changing DataVault deferred writer-release semantics was rejected again because current 1404/1414 tests define queued writer release as accepted ownership transfer.

Scalability potential: Low/Middle/High/Ultra use identical static-data truth. No `GlobalQualityWeight` route applies because resident byte count, DTO layout, BufferID ownership, and authority route must not vary with hardware.

Hardware Impact: No frame microseconds claimed. The fix removes failure-path state drift without adding allocations, locks, jobs, registry lookups, or per-frame work. Build not launched: CPU `80` with active `csc` PID `49632` and `dotnet` PID `15800`.

### APEX Integrator Source Cleanup 2026-05-29

Problem: Private cold helpers used `Get/Read` names while either probing the filesystem through `FileInfo` or mutating the DataVault arena. That was not a runtime bug, but it created a source-contract ambiguity against the rule that `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors must be pure.

Solution: Renamed the helpers to command/probe names: `TryProbeExistingBlobLength`, `TryLoadWholeFileIntoArena`, and `TryLoadWholeNativeFileIntoArena`. Hardened `TryAcquireArenaWriteView` so the invalid-view post-acquire path releases inside `finally`; successful locks are explicitly transferred to caller-owned `finally` blocks. Extended the 1404 editor source guard to assert payload write-lock release ordering.

Rejected Alternatives: Reapplying the old DataVault spin-gate fix was rejected because current 1404/1414 tests define nonblocking deferred writer-release enqueue as the active contract. Keeping `Read` names on arena-mutating methods was rejected because it weakens source audits. Running `dotnet build` was rejected because CPU sampled at `70` with active `dotnet` PID `24736`.

Scalability potential: Low/Middle/High/Ultra devices use the same static-data truth. No `GlobalQualityWeight` path applies because this code owns payload identity, BufferID authority, DTO layout, and boot hydration, not visual fidelity.

Hardware Impact: No measured microseconds. The changes add no hot-path work and no allocations; they remove failure-path lock ambiguity and audit drift.

### APEX Integrator LocData Fail-Closed Patch 2026-05-29

Problem: Offset-only localization readers were documented as null-terminated, but accepted the entire remaining LocData block when no zero terminator was found. That is fail-open behavior on a corrupt static_data blob.

Solution: Require an observed zero terminator in both offset-only runtime accessors before decoding or returning a span. Add a 1404 editor source guard that asserts `foundTerminator` exists in both methods.

Rejected Alternatives: Leaving the behavior unchanged was rejected because section bounds are not a substitute for a string terminator. Adding a new allocation or copying into scratch storage was rejected because the caller already owns the decode buffer and the span path is read-only.

Scalability potential: Low/Middle/High/Ultra all use the same static-data truth. `GlobalQualityWeight` does not apply to corrupt-data acceptance, DTO identity, or Android boot authority.

Hardware Impact: No measured microseconds. The change is a bounded pointer loop over an existing read-only span, adds no heap allocation, no registry lookup, no write lock, and no presentation work.

### APEX Integrator Deferred Release Dispute Resolution 2026-05-29

Problem: The previous 1404 spin-gate/false-return idea contradicted the current core-memory tests and would burn CPU under release contention.

Solution: Do not change `GlobalDataVault`. Current source/test contract is accepted: queued writer release is a successful ownership transfer; duplicate scan is writer-only and nonblocking; the enqueue gate is released in `finally`.

Rejected Alternatives: `Thread.SpinWait` was rejected because active tests forbid it and weak CPUs should not spin in a release path. Returning false after queue acceptance was rejected because callers would see a failed release even though ownership had transferred to the deferred queue.

Scalability potential: Low devices avoid a contention spin. Strong devices get no different memory truth path; this is authority plumbing, not visual fidelity.

Hardware Impact: Static verification only. Build skipped because CPU was `100` with active `dotnet` and `VBCSCompiler`.
