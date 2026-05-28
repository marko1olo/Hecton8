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

Solution: Regenerated `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1404.json`. Current SHA-256 is `5aaabd5ad674dd5ba5a02a9bfa76ec4555b88e1d85472900f87b7a29f445029e`. The report records zero Android guarded `new`, zero reference-type `new`, zero `string.Format`, zero `.ToString()`, zero LINQ tokens, zero `foreach`, zero Android `Path.Combine(Application.persistentDataPath)`, zero Android FileStream/BinaryWriter tokens, native heap token counts all zero, payload lock acquire/release/finally counts 4/4/4, and latest build gate CPU 64 with active `dotnet` PID 32028.

Rejected Alternatives: Running `dotnet build` was rejected because CPU remained above 50 and an active `dotnet` process existed. Claiming runtime dump existence was rejected; `Docs/AgentLogs/Dump_1404.bin` is still absent until the runtime dump path executes.

Scalability potential: No `HomeostasisBrain.GlobalQualityWeight` logic was added to Data Monolith truth. Static DTO layout and authority route are invariant; downstream consumers own continuous fidelity scaling.

Hardware Impact: Host build was not launched. Runtime/device performance remains unmeasured.
