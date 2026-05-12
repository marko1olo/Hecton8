# Rationale_PLATFORM

Top = old, bottom = new.

## 2026-05-11T23:02:03+04:00 | Session Bootstrap

Problem: Platform work was requested without existing state files or build queue registration. Running compile/refresh blindly could collide with other agents on a 4C/8T host.

Solution: Created explicit platform status and build queue records before touching runtime code. Kept status as `PENDING VERIFICATION` because no compile or platform player build has run yet.

Rejected Alternatives: Running a quick compile first was rejected because it violates the prompt's build gate. Relying on chat history was rejected because AGENTS requires disk-backed anti-amnesia.

Scalability potential: Low-tier devices benefit indirectly by keeping platform policy explicit; high-tier devices are unaffected at runtime.

Hardware Impact: Runtime 0 us. Developer-machine impact: prevents avoidable MSBuild contention and editor refresh stalls on i5-1135G7.

Low / Middle / High / Ultra: This is process infrastructure, not a quality tier feature.

## 2026-05-11T23:12:51+04:00 | Native Bridge, PC Matrix, Steam Deck PAL

Problem: Windows-only native assumptions made save compression and audio DSP brittle on Linux/macOS. Compute culling also had no hard backend gate for legacy DX11, and Steam Deck input had no explicit PAL fields.

Solution: Added `HectonNativeBridge` as the process-level native availability mask. LZ4 now tries native first and falls back to managed Deflate in the same block container when the plugin is missing. Audio Kernel calls now fail closed through the same bridge. Added `HardwareTierDetector` to classify D3D11/D3D12/Vulkan/Metal and force CPU culling on DX11. Added Steam Deck gyro/trackpad-proxy fields and a no-atan radial sector solver.

Rejected Alternatives: Random public native binaries were rejected because official provenance is not guaranteed. A hard Steamworks/Steam Input dependency was rejected until the SDK is installed. Trigonometric radial selection was rejected because dominant-axis/diagonal thresholds preserve user-facing behavior with less math.

Scalability potential: Low uses CPU culling and shared-memory clamps. Middle keeps standard GPU paths except DX11. High/Ultra retain compute culling on D3D12/Vulkan/Metal and spend saved compatibility work on richer vegetation density rather than driver fights.

Hardware Impact: i3/MX350/DX11 avoids unstable compute indirect culling, expected 50-300 us saved in vegetation-heavy scenes by taking the existing CPU BRG path instead of failing GPU work. Steam Deck shared-memory policy caps runtime VRAM to 960 MB. Radial menu sectoring saves estimated 0.5-2 us per interaction burst versus atan/sqrt sectoring.

Low / Middle / High / Ultra: Low = DX11 CPU culling, Deck shared-memory budget, gyro optional. Middle = Vulkan/Metal compute allowed with standard budgets. High = compute culling plus richer LOD residency. Ultra = same PAL, higher visual payload after device proof.

REGRESSION MODEL: CPU improves on DX11 fallback but CPU culling can cost more than compute on modern GPUs; gate only disables DX11. GC hot path unchanged; managed Deflate allocates only on save/load fallback. Memory reduces on shared-memory devices. Correctness risk is mixed compression: native readers now try LZ4 then Deflate, but older builds without this fallback will not read Deflate fallback saves.

HOT PATH IMPACT: Steam Deck input adds fixed sensor/gamepad reads in `InputDispatcher.Tick`; no collections, no strings, no allocations. Radial solver uses multiply comparisons only.

FAILURE MODES: Official LZ4/Steamworks binaries still absent. Deflate fallback is slower and not byte-compatible with old native-only builds. Real Steam Deck trackpads need Steam Input SDK for native trackpad identity; current PAL is Input System proxy.

WHY KEPT/REJECTED: Kept because it prevents platform-total save failure and makes backend gates explicit. Rejected claiming verified platform support because no Unity import, player build, or device run has executed.

## 2026-05-11T23:18:26+04:00 | Build Validators, XR/Mobile Prep, Memory/Battery/Replay Policy

Problem: Mac/Metal, standalone VR, Linux case sensitivity, shared-memory VRAM, battery throttling, and replay timestamps had no hard platform gates. The project could export a player that fails later on device.

Solution: Added Apple shader precision prebuild audit, XR eye descriptor manager, Android manifest/Gradle templates, Android `MOBILE_VR` define preprocessor, case-sensitive asset path validator, platform blocker register, shared-memory VRAM clamps, haptic waveform library, battery watchdog, debug define stripper, and monotonic `PlatformPrecisionClock`.

Rejected Alternatives: Silent shader acceptance was rejected because static scan found broad `half` usage. Project settings mutation outside build preprocessors was rejected. Per-frame battery polling was rejected in favor of 300-frame cadence. `sin/cos` haptic waves were rejected for triangle/square/saw approximations.

Scalability potential: Low uses shared-memory 960 MB VRAM budget, texture mip clamp 2, boid scale 0.4, and critical-battery quality 0. Middle keeps normal D3D12/Vulkan/Metal compute paths. High/Ultra retain full compute and can spend saved reliability on denser visuals after device proof.

Hardware Impact: Steam Deck UMA budget saves 640 MB against the 1.6 GB cap. Haptic waveform evaluator saves estimated 0.5-3 us during active rumble bursts. Battery watchdog is sub-us per sampled frame because it runs every 300 frames. XR descriptor policy prevents under-resolution eye RT setup without SDK hard dependency.

Low / Middle / High / Ultra: Low = compatibility clamps and no unverified shader precision. Middle = Vulkan/Metal allowed but build blocked on known shader precision debt. High/Ultra = visual overkill only after Metal/Vulkan device logs prove budget.

REGRESSION MODEL: CPU risk is low; prebuild validators run only in editor/build. GC hot-path risk is low; new runtime paths use static objects/structs and low-cadence sampling. Memory improves on UMA devices. Correctness risk: debug define stripper restores only on post-build success; if Unity aborts before postprocess, defines may need manual review.

HOT PATH IMPACT: `InputDispatcher.Tick` gets Steam Deck sensor reads only when Deck-like input is bound. Battery watchdog samples every 300 frames. Haptic triangle evaluation remains multiply/floor/abs only.

FAILURE MODES: Apple builds now fail on existing shader `half` usage until conversion/waiver. Android templates still require Unity Hub Android modules. Mobile VR manifest is generic and will need store/device-specific metadata after target SDK choice.

WHY KEPT/REJECTED: Kept because these are platform gates with explicit failure. Rejected claiming Mac/VR readiness because no actual Unity import, SDK install, or hardware run happened.

## 2026-05-11T23:20:03+04:00 | Final Compile Gate Result

Problem: The prompt requires `dotnet build <target> --no-restore -m:2 /nr:false`, but generated NuGet restore assets are missing under `Temp/obj/Hecton8.Editor/project.assets.json`.

Solution: Ran the gated command exactly against `Hecton8.Editor.csproj` so both Core and Editor platform changes would be covered if restore assets existed. Build failed before C# compilation with `NETSDK1004`. Immediately ran `dotnet build-server shutdown`.

Rejected Alternatives: Running `dotnet restore` or a build without `--no-restore` was rejected because it violates the strict build-gate rule. Unity refresh was rejected for the same queue/refresh constraint and because dependencies are explicitly deferred.

Scalability potential: No runtime effect.

Hardware Impact: No runtime effect. Developer-machine contention was contained by `-m:2 /nr:false` and build-server shutdown.

Low / Middle / High / Ultra: Compile gate only.

REGRESSION MODEL: C# compile diagnostics are absent because MSBuild stopped at missing restore assets. This means code-level compile status remains `PENDING VERIFICATION`.

HOT PATH IMPACT: None.

FAILURE MODES: After restore/Unity regeneration, real C# errors may still surface in the new runtime/editor files.

WHY KEPT/REJECTED: Kept status as `PENDING VERIFICATION`; rejected false green reporting.

## 2026-05-11T23:40:36+04:00 | Post-Completion Platform Hardening

Problem: The managed compression fallback initially depended on decoder trial order. That is survivable but not clean: a fallback block should be structurally identified, especially across Linux/macOS where native LZ4 may be absent. Native binary absence also needed an editor gate that does not require Unity Hub installs today.

Solution: Added an explicit managed-Deflate block marker in the compressed length high bit for both standard and protected save blocks. Added fail-fast encoding checks before writing block headers. Added `CaseSensitiveResourceLoadValidator` for first-party `Resources.Load` case mismatches. Added `NativePluginMatrixValidator` to warn by default and fail under `HECTON_STRICT_NATIVE_PLUGIN_BUILD` when target-native binaries are missing.

Rejected Alternatives: Silent decoder guessing was rejected because it hides save-format state. Hard-failing every normal Linux/macOS build for missing optional SDK binaries was rejected because the user is deferring dependency installs; strict define keeps the hard gate available without blocking all local work. Scanning third-party Resources calls as hard failures was rejected because vendor packages can use generated or type-specific Resources paths.

Scalability potential: Low-tier Linux/Deck gets deterministic save fallback identification and early binary warnings. Middle/high platforms keep native LZ4 when present. Ultra paths are unaffected except that missing SDK/binary proof becomes visible before player export.

Hardware Impact: Save/load fallback remains cold path and slower than native LZ4; runtime hot-path cost is 0 us. The new editor validators are build-time only. Explicit block markers save no frame time but remove a failure ambiguity that could otherwise become a non-Windows save-load blocker.

Low / Middle / High / Ultra: Low = managed fallback marked and readable without native plugin. Middle = native plugin if present, fallback if absent. High/Ultra = native plugin expected, strict matrix should be enabled for release candidates.

REGRESSION MODEL: Older native-only builds will not read Deflate fallback saves; this was already true and is now explicit. The high-bit marker limits encoded compressed block length to 1 GB, far above current block sizes. Validators can produce warnings during build; strict native matrix only fails when explicitly enabled.

HOT PATH IMPACT: None. Compression/decompression fallback is save/load only. Validators run editor/build only.

FAILURE MODES: If Unity/C# compilation later rejects `BuildTarget` or editor API usage, fix the editor validator syntax after restore/import. No final compile proof exists yet.

WHY KEPT/REJECTED: Kept because it increases platform determinism without installing modules. Rejected marking status verified because no compile/player/device run has executed.

## 2026-05-12T00:03:28+04:00 | POSIX Thread Priority Neutralization

Problem: Runtime threads used `AboveNormal`, `BelowNormal`, and `Lowest` directly. On Windows this is usually predictable enough, but on Linux/Steam Deck scheduler behavior can starve telemetry/replay exporters or over-prioritize audio producer work under 4C/8T load.

Solution: Added `HectonThreadPriorityPolicy` and routed the procedural audio producer, DOD replay writer, heartbeat monitor, telemetry export thread, and crash black-box export thread through it. POSIX/mobile targets return `ThreadPriority.Normal`; non-POSIX keeps the old role-specific intent.

Rejected Alternatives: Deleting priorities entirely was rejected because Windows builds currently encode useful scheduling intent. Adding per-call `#if` blocks was rejected because it spreads platform policy across unrelated systems.

Scalability potential: Low/Deck avoids scheduler starvation and background export stalls. Middle/high Windows keeps existing intent. Ultra can later tune via one policy point if device profiling proves a better native thread model.

Hardware Impact: Direct frame-time saving is 0 us. Risk reduction is scheduler-level: telemetry/replay/black-box writes are less likely to fall behind on Steam Deck/Linux under CPU load.

Low / Middle / High / Ultra: Low = normalized priorities for predictable Linux scheduling. Middle = same. High/Ultra Windows = previous differentiated priorities retained.

REGRESSION MODEL: Audio producer loses `AboveNormal` on macOS/Linux/mobile; if device audio underruns occur, the policy should be adjusted with measured evidence rather than direct per-system priority assignments. Compile proof is still pending.

HOT PATH IMPACT: None; priority is resolved once at thread creation.

FAILURE MODES: If Unity defines differ for future platforms, unknown platforms use the non-POSIX branch until added to the policy.

WHY KEPT/REJECTED: Kept because it directly closes a Linux scheduler risk in the platform prompt. Rejected claiming device proof without Steam Deck/Linux player run.

SCAN EVIDENCE: Fresh first-party non-editor runtime scan found no active MMF/Win32 markers for `MemoryMappedFile`, `SafeMemoryMappedViewHandle`, `kernel32.dll`, `Microsoft.Win32`, `System.Drawing`, or `Windows.Forms`. The older Steam Deck POSIX preflight report contains stale MMF blockers from before the FileStream/NativeArray conversion and must be regenerated after Unity import.

## 2026-05-12T00:11:31+04:00 | Graphics API Matrix Gate

Problem: `ProjectSettings.asset` showed Windows and Android graphics API data but no separate Linux entry. Manually editing Unity YAML is brittle and can corrupt the serialized player settings. Steam Deck readiness needs Vulkan-first proof from Unity's own API, not optimistic text.

Solution: Added `GraphicsApiMatrixValidator`. It reads `PlayerSettings.GetGraphicsAPIs` at build/menu time and validates Linux/Steam Deck Vulkan-first, macOS Metal-first, Windows D3D12 plus D3D11 fallback, and Android Vulkan-first with GLES3 fallback warning. Linux build blockers hard-fail because Steam Deck is tier-1; other targets can be made strict with `HECTON_STRICT_GRAPHICS_API_BUILD`.

Rejected Alternatives: Directly patching `ProjectSettings.asset` was rejected because the binary enum ordering and automatic flags are Unity-owned serialization. Assuming automatic graphics API selection is good enough was rejected because it can hide OpenGL-first Linux exports.

Scalability potential: Low/Deck gets Vulkan-first enforcement. Windows low-tier retains D3D11 fallback; modern Windows keeps D3D12. Mac uses Metal only. Android standalone VR starts Vulkan-first and leaves GLES3 as a measured fallback rather than the primary path.

Hardware Impact: Runtime 0 us. This is build-time validation. It prevents expensive invalid build attempts and avoids Steam Deck graphics API drift.

Low / Middle / High / Ultra: Low = D3D11/Deck Vulkan path exists. Middle = standard Vulkan/D3D12/Metal. High/Ultra = same API foundation with higher visual budgets after device proof.

REGRESSION MODEL: The validator depends on Unity editor APIs and cannot be compile-verified until restore/import works. It does not mutate settings, so failure mode is a warning/build failure, not project setting corruption.

HOT PATH IMPACT: None.

FAILURE MODES: If target modules are absent, Unity may not provide meaningful graphics API data for that target. This is expected until Hub modules are installed.

WHY KEPT/REJECTED: Kept because it adds evidence-based graphics API gates without requiring current installs. Rejected declaring Vulkan readiness until the validator runs inside Unity and a Linux player launches.

## 2026-05-12T00:13:24+04:00 | Shader Portability Risk Gate

Problem: Static scans already showed shader risks relevant to Vulkan/Steam Deck and standalone VR: group barriers in compute shaders, bitwise/atomic paths, and direct `sin/cos` in first-party visual shaders. Editing individual dirty/untracked shader assets without device proof would risk stepping on other agents and changing visuals blindly.

Solution: Added `ShaderPortabilityRiskValidator`. It scans first-party shader, compute, and HLSL sources for group barriers, compute returns, atomics/bitwise operators, and direct `sin/cos`. It reports warnings by default and becomes a build blocker with `HECTON_STRICT_SHADER_PORTABILITY_BUILD`.

Rejected Alternatives: Mass-replacing all shader `sin/cos` calls was rejected because some are non-hot bake paths or authored visual waves that need image validation. Hard-failing normal builds immediately was rejected because the current project has known shader debt and the user is deferring dependency/device validation.

Scalability potential: Low/Deck and standalone VR gain a visible queue of shader risks to convert to triangle/parabolic approximations or LUTs. High/Ultra can keep richer shader paths behind quality/device proof after the risk list is cleared.

Hardware Impact: Runtime 0 us for the validator. It identifies future savings; direct `sin/cos` replacements are not claimed until shader code is changed and visually checked.

Low / Middle / High / Ultra: Low = strict shader portability gate before Steam Deck/Android VR release. Middle = warnings during development. High/Ultra = opt into expensive shader math only after device validation.

REGRESSION MODEL: The scan is conservative and can flag non-hot or valid shader math. It is warning-only unless strict define is enabled.

HOT PATH IMPACT: None; editor/build-time scan only.

FAILURE MODES: It cannot prove SPIR-V correctness; it only prevents silent risk drift. Real proof still requires Vulkan player build and GPU capture.

WHY KEPT/REJECTED: Kept because it adds an evidence-based shader debt gate without touching unverified visuals. Rejected claiming shader compatibility until strict scan is run and target devices render correctly.

## 2026-05-12T00:16:01+04:00 | Persistent Path PAL

Problem: `Application.persistentDataPath` is correct for Steam/Proton/Linux/macOS, but replay and telemetry files assembled paths directly in multiple platform-core systems. That scatters path policy and makes future Steam Cloud or per-platform root changes more error-prone.

Solution: Added `HectonPersistentPathPolicy` with `RootPath`, `CombineFile`, `CombineDirectory`, and `EnsureParentDirectory`. Routed DOD replay, global telemetry export, and crash telemetry live/export files through the PAL. The helper normalizes slash direction and collapses traversal attempts to a file name.

Rejected Alternatives: Mass-refactoring every save path was rejected because save system behavior is high-risk and already dirty. Replacing `Application.persistentDataPath` with platform-specific AppData/XDG paths was rejected because Unity's persistent path is the correct cross-platform root and is compatible with Steam/Proton expectations.

Scalability potential: Low/Deck gets a single durable persistent-root policy. High/Ultra unaffected. Future Steam Cloud root changes can route through one helper for platform-core artifacts.

Hardware Impact: Runtime hot-path 0 us; paths are resolved during cold init/setup only.

Low / Middle / High / Ultra: Same behavior across tiers; this is reliability infrastructure.

REGRESSION MODEL: If callers pass nested relative paths with `..`, the helper collapses to file name. Current migrated callers use constants only.

HOT PATH IMPACT: None.

FAILURE MODES: Existing save system paths are not fully centralized yet; this pass intentionally touched only platform-core replay/telemetry files.

WHY KEPT/REJECTED: Kept because it reduces POSIX path drift without changing save semantics. Rejected broad save refactor until compile/device verification is available.

## 2026-05-12T00:43:40+04:00 | Adaptive Platform Budget Governor

Problem: Compatibility was still mostly static: build gates, backend detection, and one-time low-VRAM clamps existed, but there was no unified runtime pressure signal for Steam Deck UMA, weak PCs, critical battery, near-budget VRAM, and sustained frame pressure. Existing world systems already react to `GlobalRegistry.ScalabilityTier` and `DynamicResolutionScaler.CurrentRenderScale`; without a platform governor, those adaptive fronts only respond after local subsystems notice their own stress.

Solution: Added `PlatformAdaptiveBudgetGovernor` as a low-cadence core updatable. It packs pressure causes into a `uint` bitmask, samples every 120 frames, applies the existing low scalability tier override, and drives a production `DynamicResolutionScaler.SetPlatformPressureRenderScale(...)` hook. Deck/UMA uses a 0.78 render-scale target, VRAM pressure uses 0.72, sustained frame pressure uses 0.70, and critical battery uses 0.62. The dynamic-resolution hook lowers the quality preset floor without using debug override state, so fauna/underwater/impostor/audio systems that already read render scale or scalability tier automatically shed load.

Rejected Alternatives: A new quality manager was rejected because `GlobalRegistry` and `DynamicResolutionScaler` already form the project contract. Per-frame battery/VRAM sampling was rejected; compatibility pressure does not need 60 Hz decisions. Directly touching individual fauna, ocean, UI, and audio systems was rejected because it would duplicate pressure policy and increase cross-domain conflict risk.

Scalability potential: Low = Deck/UMA/weak PC immediately drops to low scalability and lower render-scale floor. Middle = normal dynamic resolution remains in control until sustained frame or VRAM pressure appears. High = no pressure flags, full preset floor remains. Ultra = same stable path, with platform governor silent unless the device actually breaches measured pressure.

Hardware Impact: Governor sampled frames are sub-0.1 ms by design: fixed bitmask checks, one VRAM threshold multiply/divide, no strings, no collections, no allocations. Direct microsecond saving is scene-dependent; expected gain is from earlier activation of already-existing adaptive consumers, roughly 100-800 us under Deck/low-VRAM visual pressure once fauna/underwater/impostor systems react to lower render scale. Runtime idle cost is effectively 0 us on 119 of every 120 frames.

Low / Middle / High / Ultra: Low = 0.62-0.78 render-scale pressure floor and low math/scalability tier. Middle = standard dynamic resolution. High/Ultra = no clamp unless pressure appears; saved budget can remain visual overkill after device proof.

REGRESSION MODEL: The governor can make visuals softer on Deck/UMA even before hard frame collapse; this is intended. It does not clear the low scalability override after pressure because oscillating global math precision is worse than conservative degradation. Compile proof is pending.

HOT PATH IMPACT: Registered through `GlobalRegistry.TryRegisterUpdatable`; work is skipped until the sample frame. No managed allocations in `Tick`. Uses a bitmask rather than strings/enums in mutable state.

FAILURE MODES: `DynamicResolutionScaler` production hook is uncompiled until restore/import. If runtime pressure clears, the render-scale floor returns to the quality preset, but global low tier remains until a higher-level settings flow clears it.

WHY KEPT/REJECTED: Kept because this advances real multiplaform runtime behavior without Unity Hub modules or native SDK installs. Rejected claiming Steam Deck readiness because no Linux/Vulkan player run has executed.

## 2026-05-12T00:47:10+04:00 | Platform Audit Noise Reduction

Problem: The Steam Deck POSIX preflight scanner still treated editor-only C# as player-relevant source, which can inflate Linux-player blockers with build/audit tool implementation details. Its generated report also retained stale MMF-oriented explanation after the current storage standard moved to FileStream plus NativeArray scratch buffers.

Solution: Updated `SteamDeckPosixPreflightScanner` to skip `/Editor/` C# files during player portability scanning while still scanning runtime C# and shaders. Replaced the report's MMF path section with POSIX storage path text that describes `Application.persistentDataPath`/PAL-rooted FileStream use. Updated `PlatformCompatibilityAudit` with a runtime adaptation matrix for native bridge, hardware tier detector, persistent path PAL, adaptive governor, battery watchdog, and thread-priority policy.

Rejected Alternatives: Keeping editor tool warnings in the player preflight was rejected because it hides real runtime blockers behind false noise. Running Unity batch audit now was rejected because dependency install/import is deferred and the build gate still lacks generated restore assets.

Scalability potential: Low/Deck gets cleaner blocker evidence before Linux-player export. Middle/high unaffected at runtime. Audit output now separates runtime adaptation presence from native binary/device proof.

Hardware Impact: Runtime 0 us; editor audit only. Developer-machine impact is lower report noise and less wasted triage time.

Low / Middle / High / Ultra: Audit tooling only; it supports all tiers by making proof gaps clearer.

REGRESSION MODEL: Skipping editor-only C# means editor-tool Windows-specific issues are no longer reported by the Steam Deck player preflight; those should be covered by dedicated editor/tooling scans if needed. Compile proof is pending.

HOT PATH IMPACT: None.

FAILURE MODES: The next generated preflight report must be rerun in Unity to replace stale report artifacts. Current source change is static only.

WHY KEPT/REJECTED: Kept because it makes the compatibility evidence more honest. Rejected claiming a clean Steam Deck scan until the Unity menu/batch audit actually runs.

## 2026-05-12T01:10:19+04:00 | Persistent Path PAL Expansion

Problem: Several first-party cold persistence helpers still assembled files directly from `Application.persistentDataPath`. Unity's root is valid, but scattered path construction makes Steam/Proton/Linux/macOS policy drift harder to audit and leaves POSIX scanner findings noisy.

Solution: Routed save thumbnails, save sidecar absolute path conversion, save manager cold size/timestamp helpers, input rebinding override files, global profile files, runtime diagnostics directory, quest audit log, save smoke tests, and Data Archaeology sidecar path resolution through `HectonPersistentPathPolicy`. Updated `PersistenceUxSmokeTester` away from stale MMF expectations to FileStream/NativeArray/UnsafeMemoryCopyGuard checks.

Rejected Alternatives: A full save-system path refactor was rejected because save format, recovery ordering, backup naming, and checksum flow are high-risk. This pass only changed cold absolute-path construction and did not alter relative save names, extensions, binary format, backup semantics, or file contents.

Scalability potential: Low/Deck benefits from one path policy for save-adjacent files and cleaner POSIX audit output. Middle/high unaffected except easier support triage.

Hardware Impact: Runtime hot-path 0 us. File path construction remains cold IO setup. No frame-time saving claimed.

Low / Middle / High / Ultra: Same behavior across tiers; this is portability infrastructure.

REGRESSION MODEL: `HectonPersistentPathPolicy` collapses traversal attempts containing `..` to a file name. Current migrated callers pass project-owned safe filenames or existing relative save names. Compile/player proof is pending.

HOT PATH IMPACT: None; path resolution runs around save/load/diagnostics/dev smoke flows.

FAILURE MODES: Some remaining systems intentionally still read/log raw `Application.persistentDataPath` or assign it as a root. Bootstrap/UserOptions/DataArchaeology root capture should be reviewed after compile because they may be service-level roots, not file joins.

WHY KEPT/REJECTED: Kept because it reduces POSIX path drift with small local changes. Rejected broad rewrite of core save serialization until Unity import and save smoke tests can run.

## 2026-05-12T01:43:28+04:00 | XR Platform Readiness Gate

Problem: Android standalone VR had a manifest/Gradle template and an automatic `MOBILE_VR` define, but the project still lacked hard evidence for XR package installation, loader settings, Android identity, explicit target SDK, and Android quality tier inclusion. Without a gate, a developer could export an Android player that looks "prepared" but is not a valid XR candidate.

Solution: Added `XrPlatformReadinessValidator`. It runs as an editor menu audit and as a prebuild gate. Android/mobile-VR and `HECTON_STRICT_XR_BUILD` builds now fail on missing `com.unity.xr.management`, missing `com.unity.xr.openxr`, empty build-target VR settings, missing mobile-VR manifest requirements, Unity template Android app id, automatic Android target SDK, or quality tiers that exclude Android. Extended `PlatformCompatibilityAudit` so the general report also exposes Android target SDK and mobile-VR manifest evidence. Removed Android from quality-tier exclusions and set Android's default quality to `Abyss (Low)`.

Rejected Alternatives: Silently relying on Unity Hub module installation was rejected because modules do not add project packages, app identity, loader settings, or quality policy. Auto-mutating `ProjectSettings.asset` was rejected because Unity-owned serialized platform settings are too easy to corrupt without Editor API execution and device proof.

Scalability potential: Low/standalone VR gets `Abyss (Low)` as the default mobile quality and an honest hard stop before invalid headset builds. Middle/high PC VR can opt into the same strict gate with `HECTON_STRICT_XR_BUILD`. Ultra visual paths remain blocked until XR packages and real device frame timing prove the eye render budget.

Hardware Impact: Runtime 0 us. Editor/build only. It prevents invalid Android/VR export attempts and avoids wasting device test cycles on missing SDK/package/settings prerequisites.

Low / Middle / High / Ultra: Low = Android defaults to `Abyss (Low)` and no headset build until packages/settings are explicit. Middle = PC/non-XR builds unaffected unless strict define is active. High/Ultra = same gate, then higher eye render scale only after hardware proof.

REGRESSION MODEL: The validator hard-fails Android builds while the current manifest advertises mobile VR and `MOBILE_VR` is injected. If a flat non-VR Android SKU is needed, the Android preprocessor/manifest route must be split into a separate build profile before this gate is relaxed.

HOT PATH IMPACT: None.

FAILURE MODES: It uses Unity editor build APIs and is not compile-verified until restore/import runs. Current static evidence shows the gate would fail today, which is correct.

WHY KEPT/REJECTED: Kept because it makes XR/Android blockers explicit without pretending Hub modules alone solve them. Rejected claiming standalone VR readiness until package install, project settings, player build, and headset run are complete.

## 2026-05-12T01:56:23+04:00 | Bootstrap And Options Path PAL

Problem: After the first persistent path pass, several cold runtime files still assembled paths directly from `Application.persistentDataPath`: bootstrap boot-state recovery, fatal boot crash log, user options, and dev bot CSV output. The root was correct, but scattered direct joins make POSIX/Steam Cloud policy harder to enforce.

Solution: Routed those file paths through `HectonPersistentPathPolicy`. `GameBootstrapper` now resolves the telemetry directory on the main thread before starting its background directory handshake, so the background thread does not need to call Unity `Application` APIs. Android quality tiers were already fixed in the same hardening window.

Rejected Alternatives: Rewriting indexed save sector override paths was rejected because those are intentionally derived from the active save file path, not from the global persistent root. Replacing diagnostic text that prints `Application.persistentDataPath` was rejected because it is not a file join and can help compare raw Unity path against PAL behavior.

Scalability potential: Low/Deck/Linux/macOS get more centralized path policy. Middle/high unaffected. Future Steam Cloud or per-platform root adjustments now cover bootstrap/options/dev CSV paths through one helper.

Hardware Impact: Runtime hot-path 0 us. These paths are resolved during boot, options load/save, fatal log write, or dev expedition start only.

Low / Middle / High / Ultra: Same behavior across tiers; this is portability infrastructure.

REGRESSION MODEL: `HectonPersistentPathPolicy.RootPath` falls back to `"."` if Unity returns an empty persistent path. That is safer than failing path construction, but device proof must confirm no unexpected cwd writes on constrained targets.

HOT PATH IMPACT: None.

FAILURE MODES: No compile proof yet. If Unity later rejects any dependency in the touched files, fix after restore/import.

WHY KEPT/REJECTED: Kept because it closes concrete PAL drift with small, local edits. Rejected broad persistence rewrite while build verification is blocked.
