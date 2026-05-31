# Agent 1504 Rationale

Date: 2026-05-30
Agent: 1504
Status: PENDING VERIFICATION

## Initial Boundary

Problem: Android cannot address StreamingAssets/static_data.h8bin as a normal filesystem path inside APK/OBB, while existing Windows path uses direct native file reads.
Solution: Add an Android-only NDK bridge that takes a Java AssetManager handle and writes AAsset bytes directly into the existing unmanaged destination pointer.
Rejected Alternatives: UnityWebRequest and managed byte[] staging are rejected because they allocate managed memory and add boot latency; replacing the Windows path is rejected because it risks a proven platform route.
Scalability potential: Low devices get cold boot without managed heap pressure; middle/high/ultra devices keep the same binary truth and can spend saved boot overhead on richer first-frame presentation later.
Hardware Impact: Estimated low-end i3/MX350 gain is not a runtime frame gain; it removes a mobile boot blocker and avoids managed byte[] pressure equal to static_data.h8bin size. Evidence class: STATIC_SOURCE pending.

Problem: Native/C# platform bridge crosses ARM64 pointer and DTO boundaries.
Solution: Keep file bytes unchanged; no native byte swapping, no native struct reinterpretation, no managed DTO mutation. C# capacity check precedes native copy.
Rejected Alternatives: Native-side parsing is rejected because it duplicates DTO authority and risks ARM64 padding drift; C# FileStream fallback on Android is rejected because APK assets are not normal files.
Scalability potential: Same binary layout supports Quest survival tier through PC visual-overkill tier; quality weight must not alter DTO layout or data authority.
Hardware Impact: Avoids extra copy and parse pass on mobile SoCs; expected boot-memory reduction equals one avoided managed staging buffer. Evidence class: STATIC_SOURCE pending.

## Phase 0 Route Reconciliation

Problem: The 1504 XML text asks for DllImport("HectonAndroidBridge"), but the current repository has Android C++ source files and no packaged libHectonAndroidBridge.so under Assets/Plugins/Android.
Solution: Preserve the Unity IL2CPP source-plugin route using DllImport("__Internal") and treat CMakeLists.txt as a standalone NDK reference unless a packaged shared-library route is explicitly introduced with Gradle externalNativeBuild and ABI output files.
Rejected Alternatives: Switching to DllImport("HectonAndroidBridge") now is rejected because there is no .so artifact for IL2CPP to load and the existing NativePluginMatrixValidator explicitly validates the __Internal source-plugin path. Adding externalNativeBuild now is rejected because the current Unity 6000 Gradle template is intentionally source-plugin/no externalNativeBuild.
Scalability potential: Low tier avoids package/link failure and keeps the cold boot path direct; middle, high, and ultra tiers share the same DataVault truth without route divergence. Visual overkill remains funded after boot, not inside the asset bridge.
Hardware Impact: Runtime frame gain is 0 us; package reliability gain is material. Low-end Android/Quest avoids a boot-time native symbol failure. Evidence class: STATIC_SOURCE plus Unity/Android official documentation.

Problem: Existing 1404 audit/report owns the current Android bridge proof path, while agent 1504 requires independent status and report artifacts.
Solution: Add a 1504-specific proof report without mutating the working bridge semantics or the 1404 validator expectations.
Rejected Alternatives: Rewriting the 1404 audit names or dump route is rejected because tests and validator currently bind to owner-local Dump_1404 and source-plugin evidence.
Scalability potential: Independent reports let integration compare source route health without changing gameplay data flow. Low/middle/high/ultra devices all consume identical static_data.h8bin bytes.
Hardware Impact: Editor-only scan cost only; no runtime player impact. Evidence class: STATIC_SOURCE pending implementation.

## Phase 1 Implementation Reconciliation

Problem: The native bridge task was already materially implemented by the existing source tree, but unverified duplication would increase risk.
Solution: Verify and preserve HectonAndroidAssetBridge.cpp as the active native bridge: AAssetManager_fromJava, AAssetManager_open, AAsset_getLength64, exact-size H8_LoadAssetToPointer, direct destination pointer writes, and AAsset_close on every asset-open path.
Rejected Alternatives: Rewriting the bridge is rejected because it would churn a working memory boundary without a failing test. Adding managed staging is rejected because it adds one full static_data.h8bin copy and violates Zero-GC boot policy.
Scalability potential: Low tier receives direct APK-to-arena load with no managed heap staging; middle tier keeps predictable boot; high and ultra tiers preserve the same data truth while visual systems spend the saved budget elsewhere.
Hardware Impact: Avoided managed staging buffer size is 1,064,384 bytes for the current static_data.h8bin. Runtime frame impact remains 0 us because this is cold boot only. Evidence class: STATIC_SOURCE.

Problem: Build verification is requested but the host is already under compiler/process contention.
Solution: Refuse dotnet build for now, record BLOCKED_BY_CONTENTION, and use static source audit plus editor text tests as the only current proof.
Rejected Alternatives: Running dotnet build under 68 percent CPU with active dotnet processes is rejected by coordinator decree and by the batch prompt resource gate.
Scalability potential: No device-tier effect. This protects shared cluster throughput while preserving a clean static proof trail.
Hardware Impact: Prevented additional host CPU pressure; no player runtime change. Evidence class: HOST_PROCESS_SAMPLE.

## Phase 1 Safety Dry Run

Problem: JNI/native failure must produce a controlled Data Monolith boot failure, not an Android process crash.
Solution: Keep every Java object lookup behind zero-pointer checks and pending-exception clearing; keep native reads behind null checks, asset-length equality, and false returns; record telemetry before returning false.
Rejected Alternatives: Throw-through JNI failures and unchecked native pointer calls are rejected because they turn asset packaging faults into process-level crashes.
Scalability potential: Low tier survives fragmented Android device behavior with BIOS failure instead of a black screen; middle/high/ultra tiers get identical fail-closed semantics and richer postmortem signal.
Hardware Impact: Extra checks are cold-boot-only branch cost, below frame accounting relevance. Evidence class: STATIC_SOURCE.

Problem: A compressed static_data.h8bin inside APK creates an unpredictable decompression path and defeats the intent of direct unmanaged hydration.
Solution: Keep Gradle noCompress for h8bin and read with AAsset_read directly into the DataVault pointer. The bridge never parses, byte-swaps, or stages through a project heap buffer.
Rejected Alternatives: AAsset_getBuffer/mmap dependency is rejected because compressed APK entries cannot be treated as stable direct memory. Managed UnityWebRequest/FileStream staging is rejected because it duplicates the whole blob and adds GC pressure.
Scalability potential: Low devices get deterministic boot I/O; middle tier avoids decompression stalls; high and ultra tiers keep the same bytes and can spend saved boot time on first-frame presentation after the Data Monolith is ready.
Hardware Impact: Current blob is 1,064,384 bytes; noCompress plus direct AAsset_read avoids one managed staging copy of that size and reduces Quest-class boot jitter. Evidence class: STATIC_SOURCE.

## Phase 2 Static Proof

Problem: The Android branch cannot be invoked inside the Windows Unity editor because the correct implementation is excluded by UNITY_ANDROID && !UNITY_EDITOR.
Solution: Add editor tests that prove source-order guard semantics, zero IntPtr abort behavior, native exact-size overflow refusal, direct AAsset read usage, no heap staging tokens, and h8bin packaging noCompress.
Rejected Alternatives: Defining UNITY_ANDROID inside a Windows editor test is rejected because it creates a fake compilation environment and can mask actual platform guards. Calling AndroidJNI from editor is rejected because the Java player activity does not exist there.
Scalability potential: Low devices fail before native boundary if JNI is absent; middle/high/ultra devices use the same guarded route and do not carry editor-only test code into player builds.
Hardware Impact: Editor-only tests have 0 us player impact. Evidence class: STATIC_SOURCE plus editor test source.

Problem: Preprocessor leakage would let Android-only JNI/PInvoke symbols enter Windows/editor compilations.
Solution: Add a text AST scanner in H8AndroidAssetBridge1504StaticAudit that tracks #if/#elif/#else/#endif stack state and throws FatalArchitectureException if AndroidJNI, AndroidJavaClass, H8_* native calls, or Android DllImport tokens leak outside UNITY_ANDROID && !UNITY_EDITOR.
Rejected Alternatives: Regex-only token search without branch state is rejected because it cannot distinguish legal Android blocks from illegal global tokens.
Scalability potential: No runtime tier effect; it protects all build targets from platform-symbol leakage.
Hardware Impact: Editor-only scan cost reported at 280192 us in the latest report generation; player runtime cost is 0 us.

## Final Hot-Path Verdict

Problem: Android JNI acquisition could allocate managed objects or create hidden string churn if implemented with AndroidJavaClass wrappers or dynamic method-name construction.
Solution: Keep raw AndroidJNI calls, compile-time string literals, CallObjectMethodUnsafe with null jvalue array, stackalloc ASCII asset path, and one cold-boot GetMethodID call. Static inspection result: GetMethodID count 1, string concatenation hits 0, managed new hits 0.
Rejected Alternatives: Static readonly JNI method caching is rejected for this path because the lookup executes once during cold boot, and adding static mutable cache state would complicate domain reload and Android activity lifecycle proof without measured benefit.
Scalability potential: Low devices get the minimum JNI bridge cost; middle/high/ultra devices preserve deterministic boot and identical Data Monolith truth.
Hardware Impact: Exact measured player-frame saving is 0 us because no Android player run occurred. Static proof removes one managed staging buffer of 1,064,384 bytes by design. Evidence class: STATIC_SOURCE.

## 2026-05-31 FD-Backed APK Entry Guard

Problem: The previous Android route statically required Gradle `noCompress`, but runtime did not prove that the packaged `static_data.h8bin` entry was actually uncompressed and file-descriptor backed. `AAsset_read` can still read compressed entries, which may imply hidden decompression work and violates the direct no-staging intent.
Solution: Added a native `AAsset_openFileDescriptor64` probe inside the asset size/read path. If Android cannot expose the asset as a direct file descriptor, the bridge returns `H8_ERROR_COMPRESSED_ASSET`; C# maps that to `ReadFailed`, records telemetry, and aborts before acquiring/publishing hydrated DataVault truth.
Rejected Alternatives: Trusting Gradle `noCompress` alone was rejected because package/template drift can silently reintroduce compression. Using `AAsset_getBuffer` was rejected because it can return an internal allocated buffer and does not prove no decompression. Managed preflight through Java streams was rejected because it duplicates the path and risks heap staging.
Scalability potential: Low devices avoid cold-boot decompression stalls and hidden RAM spikes; middle devices keep deterministic package validation; high/ultra devices preserve identical static truth and can spend saved boot stability budget on presentation after load.
Hardware Impact: Player-frame savings remain 0 us measured; no player run occurred. Avoided risk is one hidden decompression path for the current 1,064,384-byte blob. Latest static report generation cost: 411,864 us. Evidence class: STATIC_SOURCE plus official Android NDK asset contract.

Problem: Newly added Unity C# assets had truncated `.meta` files after Unity/editor side generation.
Solution: Completed both new `.meta` files with standard `MonoImporter` blocks so asset identity is stable before Unity import.
Rejected Alternatives: Relying on Unity to repair metadata later was rejected because source control should carry stable GUID/importer data for new script assets.
Scalability potential: No runtime tier effect; this prevents editor/import churn across machines.
Hardware Impact: 0 us runtime impact.

## 2026-05-31 Audit Reproducibility Guard

Problem: `Docs/Reports/ANDROID_PAL_OPTIMIZATION_REPORT_1504.json` recorded `PASS_STATIC_SOURCE_FD_BACKED_GUARD`, but the checked-in 1504 audit generator still emitted the older `PASS_STATIC_SOURCE` status. A rerun would silently weaken the proof artifact after the native FD-backed guard was added.
Solution: Updated `H8AndroidAssetBridge1504StaticAudit` so the generated status is `PASS_STATIC_SOURCE_FD_BACKED_GUARD` and so the fatal pass also requires validator coverage, legacy 1404 coverage, architecture docs, and complete Unity `MonoImporter` metadata. Added an editor regression test that scans the audit source and `.meta` files.
Rejected Alternatives: Leaving the report as a manual one-off was rejected because HECTON-8 evidence files must be reproducible. Running Unity tests immediately was rejected until host/compiler contention clears.
Scalability potential: No runtime tier effect. This protects low/middle/high/ultra builds from stale proof gates that let packaging regressions pass during CI/editor validation.
Hardware Impact: 0 us runtime impact. Editor-only source scan cost remains cold tooling work, not player frame work.

Problem: Android bridge correctness does not prove the `static_data.h8bin` payload is structurally valid.
Solution: Ran the existing Python `h8bin_validator.py` in a narrowed scope against `Assets/StreamingAssets` with Data Monolith C# sources only. Result: PASS, 2 files, 32 parsed structs, 1.0495 MB, 0.21875 seconds. The 1504 audit now records this scoped pass as an input proof.
Rejected Alternatives: Full `--thorough` validator mode was rejected after the tool's own 10-second watchdog killed it without report output. Broad project text-loader scanning was rejected as outside this Android PAL bridge pass.
Scalability potential: Low devices avoid booting against corrupt static truth; middle/high/ultra devices share the same validated binary authority.
Hardware Impact: 0 us runtime impact. Tooling cost was 0.21875 seconds in Python; no Unity or dotnet build was invoked.

## 2026-05-31 Bounded Native Dump Path Strings

Problem: The native telemetry dump path builder used `std::strlen(basePath)` across a P/Invoke string boundary. C# currently writes a null-terminated stack UTF-8 path, but an exported native function should not depend on an unbounded scan when the dump path contract is already capped at 1024 bytes.
Solution: Replaced the unbounded scan with `H8_TryMeasureCString`, which searches only inside the supplied capacity and rejects non-terminated or empty strings. `H8_TryBuildChildPath` now computes required bytes before `snprintf`.
Rejected Alternatives: Increasing `H8_DUMP_PATH_CAPACITY` was rejected because it hides the boundary issue. Leaving `strlen` was rejected because fatal telemetry code must fail closed rather than wander through memory after a malformed path.
Scalability potential: No frame-tier effect. Low/middle/high/ultra devices all get the same bounded fatal-dump path behavior.
Hardware Impact: 0 us measured player-frame impact. The added loop is cold/fatal telemetry only and capped at 1024 bytes.

## 2026-05-31 Active Architecture Contract Pass

Problem: Active architecture documents still described the Data Monolith Android/Quest route as URI staging to cache. The runtime path now uses Android NDK `AAssetManager` source-plugin hydration with an FD-backed/uncompressed APK guard, so stale docs could reauthorize the wrong loader contract.
Solution: Updated the active boot/product/handoff/portability docs to name the NDK bridge and to keep device/player proof limits explicit. Added `activeArchitectureDocsAligned` to the 1504 audit and a source test that rejects stale Android URI staging phrases. The audit generator now also emits the FD-backed status regeneration and downgrade guard flags used by the report.
Rejected Alternatives: Leaving stale docs in place was rejected because future agents could treat them as authority and reintroduce UnityWebRequest/cache staging for `static_data.h8bin`. Broad documentation rewrites were rejected; only directly conflicting Android PAL lines were changed.
Scalability potential: Low devices keep the direct APK-to-arena route as the documented contract; middle/high/ultra devices share the same binary truth and can scale presentation only after the monolith is resident.
Hardware Impact: 0 us runtime impact. The avoided risk is one full payload cache/staging path for the current 1,064,384-byte blob.

## 2026-05-31 ABI Export And Native Dump Proof Pass

Problem: The Android bridge report proved the AAsset read path, but did not explicitly prove that hidden-symbol CMake policy still exposes the three native entry points or that the Android release crash dump path stays native and bounded.
Solution: Added audit/test checks for exact `extern "C" JNIEXPORT ... JNICALL` signatures for `H8_GetAssetSize`, `H8_LoadAssetToPointer`, and `H8_WriteTelemetryDump` under `-fvisibility=hidden`, plus stack UTF-8 persistent path encoding, native `H8_WriteTelemetryDump`, `O_CLOEXEC`, file mode `0600`, EINTR-resilient `H8_WriteAll`, and `close(fd)`.
Rejected Alternatives: Running a full build just to test editor-only audit assertions was rejected under the build decree. Relying on prior 1404 validator coverage was rejected because 1504 report should carry its own export/dump proof.
Scalability potential: Low devices get a native fault artifact path without managed release I/O; middle/high/ultra devices keep identical crash forensic semantics.
Hardware Impact: 0 us runtime frame impact. The dump path runs only on cold/fatal telemetry; the avoided failure is losing the black-box trail on Android release.

## 2026-05-31 Audit Tooling Heap Discipline Pass

Problem: The 1504 audit generator and legacy Android bridge audit used `File.ReadAllBytes` while hashing proof inputs, including the Data Monolith payload. This is editor tooling, but it normalizes a full-file managed staging pattern inside the Android PAL proof route.
Solution: Replaced whole-file byte loading in both Android PAL audits with a streaming `FileStream` and `SHA256.ComputeHash(stream)`. Added test coverage that rejects `File.ReadAllBytes` in the 1504 audit source and makes 1504 require legacy audit heap discipline.
Rejected Alternatives: Keeping `File.ReadAllBytes` was rejected because static proof tooling should not model the anti-pattern the runtime bridge exists to avoid. Rewriting every editor test reader was rejected as outside this Android PAL proof path.
Scalability potential: Low-memory developer machines avoid unnecessary full-payload managed heap pressure during audits; middle/high/ultra machines get the same proof output without different semantics.
Hardware Impact: 0 us player-frame impact. Tooling peak managed memory is reduced by approximately one payload-sized byte array per audit hash of current `static_data.h8bin` size of 1,064,384 bytes.

## 2026-05-31 Ignored Proof Artifact Cleanup Pass

Problem: The 1504 audit required `Docs/Reports/CI_BINARY_VALIDATION_1504.log`, but the repository globally ignores `*.log`. A clean checkout or CI run could therefore fail the Android PAL audit despite having the tracked JSON/JUnit/Metric Phi validation artifacts.
Solution: Removed the ignored log from fatal audit inputs, report paths, and report SHA-256 fields. The h8bin validator proof now rests on tracked/reproducible artifacts only: scoped JSON, JUnit XML, and Metric Phi JSON. Added `h8binValidatorIgnoredLogExcluded=true` so the exclusion is explicit rather than silent.
Rejected Alternatives: Unignoring every `.log` or requiring a local ignored sidecar was rejected because it fights the project-wide ignore policy and creates machine-local proof drift. Dropping h8bin validation entirely was rejected; only the ignored duplicate log was removed.
Scalability potential: Low/middle/high/ultra devices are unchanged. The gain is integration stability: proof gates no longer depend on a local untracked file.
Hardware Impact: 0 us player-frame impact. Editor audit avoids one fragile file read and a false-negative CI failure mode.

## 2026-05-31 Native Include Hygiene Pass

Problem: `HectonAndroidAssetBridge.cpp` still included `<cstring>` after the bounded dump path pass removed the last `std::strlen` use.
Solution: Removed the unused include and refreshed the native plugin SHA-256 in the 1504 report.
Rejected Alternatives: Leaving the include was rejected because this native bridge is a small portability boundary and should expose only the headers it actually uses.
Scalability potential: No device-tier effect. This is compile-surface hygiene only.
Hardware Impact: 0 us player-frame impact; no runtime code path changed.

## 2026-05-31 Android Dump Layout Proof Pass

Problem: Android release telemetry dump writes `H8DataMonolithTelemetryEntry` records as raw native bytes, while the 1504 audit did not independently prove the struct is explicit, fixed-size, and little-endian-safe for ARM64.
Solution: Added 1504 audit/test requirements for `H8DataMonolithTypes.cs`: explicit 64-byte `H8DataMonolithTelemetryEntry`, first and last field offsets, and the existing `UnsafeUtility.SizeOf` layout guard. Updated the 1504 report with the type path and hash.
Rejected Alternatives: Rewriting Android dump to serialize every field natively was rejected because the C# type is already explicit, 64 bytes, and the target Android/Quest ABI is little-endian ARM64. Relying only on legacy audit proof was rejected because 1504 owns the native dump bridge evidence.
Scalability potential: Low/middle/high/ultra devices keep identical crash forensic layout. No quality tier changes dump semantics.
Hardware Impact: 0 us player-frame impact. Fatal-dump path remains raw memory copy; only audit evidence changed.

## 2026-05-31 DataVault Writer-Release Contract Pass

Problem: The Android PAL report proved the NDK byte-copy route, but not the surrounding writer-lock contract. A direct native load into the DataVault pointer is only safe if every payload write view is released symmetrically and contended writer releases are deferred through the vault's owned queue instead of hidden polling.
Solution: Added 1504 audit/test gates for `DataMonolithWriterReleaseRetryCount`, `ReleaseWriteLockWithRetry`, four payload acquire sites matched by four `ReleaseArenaWriteView()` calls inside `finally`, and the invalid-acquire rollback path. Added `GlobalDataVault` gates for `QueueDeferredWriterRelease`, writer-only dedupe, compare-exchange enqueue gate, `Volatile.Write` release, and absence of `Thread.SpinWait`/gate spin loops.
Rejected Alternatives: Runtime code churn was rejected because the source already contains the correct retry/deferred release contract. A weaker audit that only checks native `H8_LoadAssetToPointer` was rejected because pointer hydration without lock proof can still corrupt later boot ownership.
Scalability potential: Low devices avoid lock leaks that can deadlock boot recovery; middle devices keep deterministic payload ownership under contention; high/ultra devices retain the same data truth while richer visuals consume budget after monolith readiness.
Hardware Impact: 0 us player-frame impact. Static proof only; source scan cost is editor/tooling work. Avoided failure is a stuck writer fence or duplicated deferred writer release under DataVault contention.

Problem: The black-box dump route was native and bounded, but 1504 did not prove that dumping telemetry is read-only or that wrapped ring entries are written in chronological order.
Solution: Added audit/test gates requiring `DumpTelemetry` to use `TryReadTelemetry` read-only snapshots, reject `EnsureTelemetry` inside the dump window, and prove both C# and native writers start from the normalized cursor before wrapping.
Rejected Alternatives: Creating telemetry buffers from the dump path was rejected because crash/fatal telemetry must not mutate global state. Serializing newest-first was rejected because black-box postmortem consumers need stable chronological order.
Scalability potential: Low/middle/high/ultra devices receive the same deterministic fatal record. Quality weight does not affect dump layout, cursor order, or authority route.
Hardware Impact: 0 us player-frame impact. Fatal-only route; no runtime player measurement was performed.

## 2026-05-31 JNI Lifetime And Native Cache Pass

Problem: The Android bridge uses a Java `AssetManager` local reference and converts it to a native `AAssetManager*`. Android's NDK contract requires the Java object to remain live while the native object is used; a future cache of that native pointer or a missed local-ref deletion would turn a cold boot path into a lifecycle bug.
Solution: Added 1504 audit/test gates proving that `assetManager`, `activityClass`, `activity`, and `unityPlayerClass` are deleted after the synchronous native size/load calls, JNI exceptions are cleared and their local exception ref is deleted, and the native bridge contains no `NewGlobalRef`, `DeleteGlobalRef`, `static AAssetManager`, `static AAsset`, or `static jobject` cache.
Rejected Alternatives: Caching the native asset manager pointer was rejected because it would require global Java reference ownership and explicit domain reload/application lifecycle handling. Creating global refs for a one-shot cold boot loader was rejected as unnecessary state.
Scalability potential: Low devices avoid local-reference leaks during boot retries; middle/high/ultra devices keep identical one-shot JNI semantics. No quality tier may alter JNI ownership or native asset lifetime.
Hardware Impact: 0 us player-frame impact. Static proof only. Native `AAsset` close count is four source sites; no Android player run was performed.

## 2026-05-31 Source Plugin Package Contract Pass

Problem: The Android PAL route depends on Unity's IL2CPP source-plugin behavior (`DllImport("__Internal")`), but the 1504 proof chain did not lock the source files' import metadata or Gradle source-build placeholders. A future edit could introduce `externalNativeBuild` or a named `.so` expectation while no packaged `libHectonAndroidBridge.so` exists.
Solution: Added audit/test/build-validator gates for `DefaultImporter` metadata on `HectonAndroidAssetBridge.cpp` and `CMakeLists.txt`, retained Unity source-build placeholders in `mainTemplate.gradle`, and kept `externalNativeBuild` absent. The report now records `androidSourcePluginRouteSerialized`, `nativeSourcePluginDefaultImporterMetaComplete`, and `unitySourceBuildGradlePlaceholdersPresent`.
Rejected Alternatives: Switching to `DllImport("HectonAndroidBridge")` was rejected because the repository still has no packaged Android bridge `.so` route. Wiring `externalNativeBuild` was rejected because Unity's current Android IL2CPP source plugin path already compiles C/C++ source files into the player and avoids an extra artifact pipeline.
Scalability potential: Low devices avoid boot-time native symbol failure; middle/high/ultra devices keep the same DataVault truth route. No quality tier is allowed to change linkage identity.
Hardware Impact: 0 us player-frame impact. This is packaging integrity proof only; avoided failure is a Quest/Android player build that compiles C# but cannot resolve native entry points at runtime.

Problem: The direct `AAssetManager` proof assumes `static_data.h8bin` is in the APK asset set. If Android split binary/OBB packaging is enabled, this bridge has no expansion-file hydration path and the FD-backed APK guard no longer proves the full route.
Solution: Added gates for `AndroidTargetArchitectures: 2`, `AndroidBuildApkPerCpuArchitecture: 0`, and `androidSplitApplicationBinary: 0`; updated the architecture docs to make these packaging settings part of the Data Monolith Android contract.
Rejected Alternatives: Supporting split OBB lookup in this pass was rejected because it would require a second storage authority and new failure modes outside the current source-plugin APK bridge. Broad Android packaging refactor was rejected; the correct narrow fix is to fail the proof when the project leaves the APK route.
Scalability potential: Low Quest/mobile devices get one predictable APK asset path; middle/high/ultra devices keep identical bytes and spend any saved engineering budget after monolith readiness, not inside packaging branches.
Hardware Impact: 0 us player-frame impact. Static proof only; avoided cost is a broken mobile boot caused by expansion-file routing of a 1,064,384-byte payload.

## 2026-05-31 Android Asset Name Contract Pass

Problem: The NDK bridge expects a StreamingAssets-relative asset name for `AAssetManager_open`. The proof chain did not explicitly prevent a future edit from passing `Application.streamingAssetsPath`, a `jar:file://` URL, or a cache path into the native asset lookup.
Solution: Added `androidAssetNameStackAsciiRoutePresent` to the 1504 audit, tests, and report. The gate requires stackalloc asset-name storage, `H8DataLayoutConstants.DefaultStreamingAssetsRelativePath.AsSpan()`, ASCII-only byte emission, a null terminator, and the exact `Hecton8/DataMonolith/static_data.h8bin` constant.
Rejected Alternatives: Android URL composition was rejected because `AAssetManager_open` consumes APK asset names, not Unity URL strings. Allocating an encoded managed byte array was rejected because this is a cold boot zero-GC bridge.
Scalability potential: Low devices avoid path translation failures and heap pressure; middle/high/ultra devices keep the same deterministic asset identity.
Hardware Impact: 0 us player-frame impact. Static proof only; avoided failure is a direct native call against the wrong asset namespace.

## 2026-05-31 Android Telemetry Route Flags Proof Pass

Problem: The Android runtime already set native asset route flags, but the 1504 proof chain did not require them. The audit/test source also contained a stale `AndroidAssetNameCapacity` token while runtime uses local `assetNameCapacity`, so rerunning the audit would create a false negative or tempt a pointless runtime rename.
Solution: Corrected the asset-name proof to match the current stackalloc variable and added `androidTelemetryRouteFlagsPresent` to audit, tests, and report. The Android loader proof now requires `PathFlagVaultBacked | PathFlagAndroidAssetManager | PathFlagAndroidJavaAssetManager`, `_lastReadPathFlags = pathFlags`, telemetry writes with those flags, and absence of URI/cache staging tokens inside the Android NDK loader window.
Rejected Alternatives: Renaming runtime locals for the audit was rejected because it would churn working boot code for proof cosmetics. Allowing generic path flags was rejected because black-box telemetry must identify the real route that touched native memory.
Scalability potential: Low devices get truthful failure telemetry when APK asset lookup fails; middle/high/ultra devices keep identical data authority and route diagnostics. Quality weight cannot change loader path flags or Data Monolith ownership.
Hardware Impact: 0 us player-frame impact. Static proof only; avoided failure is a misleading black-box record that points investigators toward URI/cache staging instead of the direct AAssetManager path.

## 2026-05-31 Native JNI Environment Balance Proof Pass

Problem: Native `JNIEnv*` acquisition was implemented, but the 1504 proof chain did not explicitly prove attach/detach symmetry. A leak here would not be a managed GC issue; it would be native thread/JNI state left dirty after a cold-boot failure path.
Solution: Added `nativeJniEnvironmentReleaseBalanced` to the audit, tests, and report. The gate requires `GetEnv`, `JNI_OK`, `JNI_EDETACHED`, `AttachCurrentThread`, `*attached = true`, attached-only `DetachCurrentThread`, exactly two native asset entry-point acquire sites, and release calls on every post-acquire error/success path.
Rejected Alternatives: Caching `JNIEnv*` was rejected because JNI environments are thread-local. Assuming Unity's managed caller thread needs no native release proof was rejected because this bridge may attach when called outside an already-attached native thread.
Scalability potential: Low devices avoid native thread-state leaks on fragmented Android boot retries; middle/high/ultra devices keep the same one-shot bridge lifecycle and route truth.
Hardware Impact: 0 us player-frame impact. Static proof only; avoided failure is native JNI state leakage or unbalanced attached-thread lifetime in the Android asset bridge.

## 2026-05-31 GameActivity No-Looper Contract Pass

Problem: ProjectSettings and manifest use Unity GameActivity, while active Data Monolith docs still contained a stale Android/JAR URL staging claim. Unity's GameActivity documentation states that the player loop runs on a native thread and Java APIs such as `myLooper` can fail from plug-ins, so the Android PAL proof needed an explicit no-Looper contract.
Solution: Updated `DATA_MONOLITH_RUNTIME_INTEGRATION.md` to remove the stale Android/JAR staging sentence and document the NDK `AAssetManager` route as the Android player path. Added `androidGameActivityNoLooperDependency` to audit, tests, and report: `androidApplicationEntry: 2`, `UnityPlayerGameActivity`, GameActivity documentation note, and no `Looper`, `myLooper`, or `Handler` tokens inside the Android loader window.
Rejected Alternatives: Reverting Android to generic URL staging was rejected because it reintroduces managed file delivery into the Data Monolith route. Adding Java `Handler`/`Looper` hops was rejected because GameActivity's native player-loop thread makes that a compatibility hazard and the bridge only needs synchronous `currentActivity.getAssets()`.
Scalability potential: Low Quest/mobile devices avoid Java-thread dependency failures during boot; middle/high/ultra devices keep the same native asset route and identical static truth.
Hardware Impact: 0 us player-frame impact. Static proof only; avoided failure is a GameActivity-specific boot defect caused by introducing Java Looper-dependent code into the native asset bridge.

## 2026-05-31 Android Native Dump 1504 Mirror Pass

Problem: The Android native dump path wrote `Dump_1404.bin` only. That preserved legacy Data Monolith audit ownership, but it left 1504 without its own crash artifact even though the Android NDK bridge is the 1504-owned domain.
Solution: Refactored the native dump body into `H8_WriteTelemetryDumpFile` and made the exported Android dump writer emit both `Docs/AgentLogs/Dump_1404.bin` and `Docs/AgentLogs/Dump_1504.bin` from the same chronological telemetry bytes. Added `androidNativeTelemetryAgentDumpMirrorPresent` to audit, tests, and report.
Rejected Alternatives: Renaming the existing dump to `Dump_1504.bin` was rejected because it would break the legacy 1404 validator route. Writing the mirror from managed C# was rejected because Android release dump I/O should remain native and bounded.
Scalability potential: Low devices keep one fatal-only extra file write with no frame impact; middle/high/ultra devices keep identical black-box bytes and owner-local diagnostics.
Hardware Impact: 0 us player-frame impact. Fatal-path cost is one additional bounded file write of at most the telemetry ring size; avoided failure is missing owner-local Android bridge crash evidence.

## 2026-05-31 Native Matrix Dump Mirror Gate Pass

Problem: `NativePluginMatrixValidator` still accepted the Android Data Monolith source-plugin route with only the legacy `Dump_1404` requirement. That made the shared build matrix weaker than the 1504 audit after the owner-local `Dump_1504.bin` mirror was added.
Solution: Added `nativeDumpMirrorRouteValid` to the shared native matrix gate and required `H8_WriteTelemetryDumpFile`, both dump paths, `legacyOk`, `agentOk`, and `return legacyOk && agentOk`. Added `nativeMatrixValidatorDumpMirrorGuardPresent` to the 1504 audit, tests, and report.
Rejected Alternatives: Leaving the mirror proof only in the 1504 audit was rejected because the shared validator is the integration gate other agents will trip first. Duplicating dump serialization was rejected; the native code keeps one writer and two file paths.
Scalability potential: Low/middle/high/ultra devices keep identical fatal telemetry bytes. Quality weight never changes dump ownership, byte layout, or route proof.
Hardware Impact: 0 us player-frame impact. Fatal-only route unchanged; proof cost estimate is 420 us static scan. Avoided failure is a future Android matrix pass that silently drops the 1504-owned crash artifact.
