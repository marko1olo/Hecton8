# PLATFORM BLOCKER REGISTER

Status: `PENDING VERIFICATION`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Native Binaries

| Area | Status | Blocker | Unity Hub Fix? |
|---|---:|---|---|
| LZ4 Windows x64 | Present | `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll` exists. | No |
| LZ4 Linux x64/ARM64 | Missing | No vendored `liblz4.so`; official source release must be built or a signed package must be supplied. Managed Deflate fallback now marks fallback blocks explicitly and prevents save-total failure, but it is slower and not old-build compatible. | No |
| LZ4 macOS Universal | Missing | No vendored `.dylib`; requires official build/sign pipeline. | No |
| Audio Kernel Linux/macOS | Missing | `HectonAudioKernel` has no verified `.so`/`.dylib`; bridge now fails closed. | No |
| Steamworks Linux/macOS | Missing | `libsteam_api.so`/`libsteam_api.dylib` must come from official Steamworks SDK `redistributable_bin`. | No |

## Build-Time Platform Guards

- `GraphicsApiMatrixValidator` checks Linux/Steam Deck Vulkan-first, macOS Metal-first, Windows D3D12+D3D11, and Android Vulkan-first/GLES3 fallback policy through Unity `PlayerSettings.GetGraphicsAPIs`. Linux violations are hard blockers; other target violations become hard blockers with `HECTON_STRICT_GRAPHICS_API_BUILD`.
- `XrPlatformReadinessValidator` blocks Android/mobile-VR and strict XR builds when XR packages, VR settings, Android identity, target SDK, or Android quality tiers are not ready. It warns from the menu item and hard-fails during matching player builds.
- `PlatformCompatibilityAudit` now reports runtime adaptation and XR/Android package/settings facts, including target SDK and mobile-VR manifest evidence.
- `SteamDeckPosixPreflightScanner` now classifies player runtime C# separately from editor-only C# so Linux-player blockers are not padded by audit/tooling implementation details.
- `ShaderPortabilityRiskValidator` scans first-party shaders for compute barriers, atomics/bitwise paths, and direct `sin/cos`. It warns by default and fails with `HECTON_STRICT_SHADER_PORTABILITY_BUILD`.
- `NativePluginMatrixValidator` reports missing per-target native binaries before player export. In normal mode it warns; with `HECTON_STRICT_NATIVE_PLUGIN_BUILD` it fails the build.
- `CaseSensitiveAssetCollisionValidator` fails builds when two assets differ only by case.
- `CaseSensitiveResourceLoadValidator` fails builds when first-party static `Resources.Load("...")` literals differ from the real `Resources/` asset casing. Unresolved literals are warnings because type-specific or generated Resources usage can be legitimate.
- `MetalShaderPrecisionAuditor` fails Apple-family builds on unwaived first-party `half` shader precision debt.

## Unity Hub Modules

Install now:

- Android Build Support
- OpenJDK
- Android SDK & NDK Tools
- Mac Build Support (Mono) if you want local Mac player export from this Windows machine

Install only when the matching test target is real:

- visionOS Build Support: requires Mac/Xcode/device or simulator validation; Windows export alone is not enough.
- iOS/tvOS Build Support: requires Mac signing/Xcode pipeline.
- Web Build Support: only if WebGL is a target.
- UWP Build Support: only if Microsoft Store/UWP is a target; not needed for Steam Deck.
- Dedicated Server modules: only for headless QA/server builds.

Already useful from screenshot:

- Windows Build Support (IL2CPP)
- Linux Build Support (IL2CPP)
- Linux Build Support (Mono)

## Mac / Metal Shader Blockers

Static scan found heavy first-party `half` usage under:

- `Assets/_Project/Art/Shaders/TerrainMaster.shader`
- `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`
- `Assets/_Project/Scripts/HectonBiolumMaster.shader`
- `Assets/_Project/Scripts/BoidFishInstanced.shader`
- `_Archive` shader files

`MetalShaderPrecisionAuditor` now fails Apple-family builds on unwaived first-party half precision. This is intentional: Mac is not verified until these shader precision paths are converted or explicitly waived.

## XR / Android Standalone Blockers

Current evidence before Unity Hub Android/XR installation:

- `Packages/manifest.json` does not contain `com.unity.xr.management`.
- `Packages/manifest.json` does not contain `com.unity.xr.openxr`.
- `ProjectSettings/ProjectSettings.asset` still has `m_BuildTargetVRSettings: []`.
- Android application id is still `com.UnityTechnologies.com.unity.template.urpblank`.
- `AndroidTargetSdkVersion` is `0` (automatic), not an explicit release target.
- Android is now included in quality tiers, with `Abyss (Low)` selected as Android's default quality level.
- `Assets/Plugins/Android/AndroidManifest.xml` does include `VIBRATE`, `android.hardware.vr.headtracking`, and the `hecton8.mobile_vr_template` marker, so the manifest template exists but the XR stack is not installed/proven.

Unity Hub can fix only the module side: Android Build Support, OpenJDK, Android SDK & NDK Tools. It will not automatically fix package manifest dependencies, app id, target SDK policy, loader settings, or device proof.

## POSIX Path Scan

No hardcoded first-party code path of the form `Assets\...`, `StreamingAssets\...`, `Plugins\...`, or `Application.persistentDataPath + ...` was found in `Assets/_Project/Scripts`. Existing backslash hits are normalizers, regex, JSON escaping, or char escaping.

## Win32 / MMF Runtime Scan

Fresh first-party non-editor runtime scan found no active hits for:

- `System.IO.MemoryMappedFiles`
- `MemoryMappedFile`
- `MemoryMappedViewAccessor`
- `SafeMemoryMappedViewHandle`
- `CreateFromFile`
- `AcquirePointer`
- `SafeFileHandle`
- `kernel32.dll`
- `Microsoft.Win32`
- `System.Drawing`
- `Windows.Forms`

The older `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md` still lists stale `SaveBinaryStorage` MMF blockers from before the FileStream/NativeArray conversion. Treat this blocker register and the current source scan as newer evidence until the Unity editor scanner is rerun after dependency import.

## Platform Runtime State

- D3D11: compute culling disabled; CPU culling fallback path used.
- D3D12/Vulkan/Metal: compute culling allowed when Unity reports compute support.
- Steam Deck/shared memory: runtime VRAM budget clamped to 960 MB and low scalability override applied.
- Critical battery `< 15%`: runtime quality level forced to 0 while discharging.
- Adaptive runtime governor: `PlatformAdaptiveBudgetGovernor` samples Deck/UMA, VRAM pressure, critical battery, and sustained frame pressure every 120 frames, then applies low scalability and a production dynamic-resolution pressure floor.
- Linux/macOS/mobile thread priorities: runtime background/audio/heartbeat worker priorities normalize to `ThreadPriority.Normal` via `HectonThreadPriorityPolicy`. Windows keeps role-specific priorities.
- Platform-core replay/telemetry paths: routed through `HectonPersistentPathPolicy`, still rooted at Unity `Application.persistentDataPath` for Steam/Proton/Linux/macOS compatibility.
- Save-adjacent thumbnails, sidecars, input rebinds, global profile, diagnostics, quest audit, save smoke tests, and Data Archaeology sidecar paths now route through `HectonPersistentPathPolicy` for file joins.
- Bootstrap boot-state/fatal-log files, bootstrap telemetry directory handshake, user options, and dev bot CSV path now route through `HectonPersistentPathPolicy`; remaining raw `Application.persistentDataPath` in `GameBootstrapper` is diagnostic text only.

## Remaining Verification

- `dotnet build Hecton8.Editor.csproj --no-restore -m:2 /nr:false` currently stops at `NETSDK1004`: missing `Temp/obj/Hecton8.Editor/project.assets.json`.
- Unity import/console after dependency install.
- `dotnet build` through build queue.
- Windows player smoke run.
- Linux/Vulkan player on Steam Deck or comparable AMD Vulkan Linux host.
- macOS Metal player on real Mac.
- Standalone VR Android build only after Android module and target SDK are installed.
