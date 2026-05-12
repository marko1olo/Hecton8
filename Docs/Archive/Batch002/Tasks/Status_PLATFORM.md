# Status_PLATFORM

Agent: `PLATFORM_COMMAND`  
Domain: Echelon 1 Platform Abstraction / Deployment / Native SDK / PAL  
Prompt task count: 15  
Status: `PENDING VERIFICATION`

## Task Matrix

- [x] 1. BUILD QUEUE INIT  
  DOD: Created `Docs/AgentLogs/BUILD_QUEUE.md` and registered this session before any build/refresh.  
  Alternative rejected: unregistered compile, because the host is a 4C/8T i5-1135G7 and build contention is explicitly forbidden.  
  Estimate: saves unbounded contention spikes; direct runtime microseconds: 0.

- [x] 2. NATIVE BRIDGE REFACTOR  
  DOD: Added `HectonNativeBridge`, gated LZ4 and Audio Kernel native calls, and added a managed Deflate fallback for non-Windows save compression/decompression when `liblz4` is unavailable.  
  Alternative rejected: downloading random `.so`/`.dylib` binaries; supply-chain risk is worse than a cold-path fallback.  
  Estimate: runtime hot-path savings 0 us; failure-path save/load recovery avoids total non-Windows save failure.

- [x] 3. PC GENERATION MATRIX  
  DOD: Added `HardwareTierDetector` for D3D11/D3D12/Vulkan/Metal, shared-memory mode, Steam Deck-like signatures, and disabled compute culling on D3D11 so CPU culling is used.  
  Alternative rejected: trusting `SystemInfo.supportsComputeShaders` alone; DX11 legacy drivers can report support while being unstable under indirect/compute culling load.  
  Estimate: saves crash/stall risk; direct frame saving on DX11 is scene-dependent, expected 50-300 us under vegetation pressure by avoiding failing GPU dispatch paths.

- [x] 4. STEAM DECK PAL  
  DOD: Mapped Steam Deck gyro and trackpad-proxy axes into `PlayerInputState`, added zero-trig radial sector resolver, and created a publisher metadata template with a legal boundary against placeholder entities.  
  Alternative rejected: Steam Input SDK hard dependency before binaries/packages are present; PAL now degrades to Input System gamepad/sensor data.  
  Estimate: radial menu avoids atan/sqrt, estimated 0.5-2 us per interaction burst vs trigonometric sectoring.
- [x] 5. MAC/METAL SHADER AUDIT  
  DOD: Added `MetalShaderPrecisionAuditor` prebuild/menu audit. Static scan found heavy `half` usage, so Apple builds are intentionally blocked until conversion or explicit waiver.  
  Alternative rejected: silently allowing Metal builds with known precision-risk shader logic.  
  Estimate: runtime savings 0 us; prevents undefined visual artifacts on Apple Silicon.

- [x] 6. OPENXR FOUNDATION  
  DOD: Added `HectonXRManager` and 2048x2048 baseline eye render texture descriptor policy with Unity XR descriptor pickup when available.  
  Alternative rejected: hard OpenXR package dependency before packages are installed.  
  Estimate: runtime hot-path savings 0 us; prevents undersized eye RT defaults.

- [x] 7. STANDALONE VR PREP  
  DOD: Added Android manifest and Gradle template under `Assets/Plugins/Android`, plus Android prebuild `MOBILE_VR` define injection.  
  Alternative rejected: installing/assuming Android SDK locally inside the project.  
  Estimate: runtime 0 us; build-prep only.

- [x] 8. POSIX PATH SANITIZATION  
  DOD: Ran project scan for hardcoded code-driven Windows paths; no `Assets\`, `StreamingAssets\`, `Plugins\`, or `persistentDataPath +` style offenders found in first-party scripts.  
  Alternative rejected: rewriting legitimate regex/JSON/backslash-normalizer code.  
  Estimate: runtime 0 us; prevents false churn.

- [x] 9. CASE-SENSITIVE VALIDATOR  
  DOD: Added `CaseSensitiveAssetCollisionValidator` prebuild/menu validator to fail on asset paths that collide by casing on Linux/macOS.  
  Alternative rejected: relying on Windows filesystem behavior to reveal Linux path bugs.  
  Estimate: runtime 0 us; build gate only.

- [x] 10. MEMORY PRESSURE DICTATOR  
  DOD: Steam Deck/shared-memory detection now clamps runtime VRAM warning/budget to 960 MB and applies stronger texture/boid pressure reductions.  
  Alternative rejected: treating Steam Deck UMA as 2 GB dedicated VRAM.  
  Estimate: saves 640 MB against the 1.6 GB Deck cap target; frame effect scene-dependent.

- [x] 11. HAPTIC WAVEFORM GEN  
  DOD: Added `HapticWaveformLibrary` with triangle/square/saw LRA/ERM waveforms and routed existing haptic triangle pulse through it.  
  Alternative rejected: sin/cos haptic modulation.  
  Estimate: 0.5-3 us saved per active haptic command set versus trig waveform evaluation.

- [x] 12. BATTERY LIFE WATCHDOG  
  DOD: Added `PlatformBatteryWatchdog`; while discharging below 15%, quality level is forced to 0 and low scalability override is registered.  
  Alternative rejected: per-frame battery polling; sampler uses 300-frame cadence.  
  Estimate: battery/runtime budget gain device-dependent; watchdog itself is sub-us per sampled frame.

- [x] 13. STRIP EDITOR SYMBOLS  
  DOD: Added Linux/macOS prebuild debug define stripper for project debug symbols, with post-build restore.  
  Alternative rejected: shipping debug-heavy Linux/macOS defines by default.  
  Estimate: runtime hot-path savings unmeasured; binary/metadata reduction depends on build defines.

- [x] 14. REPLAY DETERMINISM  
  DOD: Added `PlatformPrecisionClock` based on `Stopwatch.GetTimestamp()` and routed replay snapshot/input `PrecisionTimestamp` through it.  
  Alternative rejected: Unity realtime clock for deterministic replay headers.  
  Estimate: runtime delta negligible; monotonic timestamp stability improves across Windows/POSIX.
- [x] 15. FINAL COMPILE  
  DOD: Build gate checked, `dotnet build Hecton8.Editor.csproj --no-restore -m:2 /nr:false` attempted, and `dotnet build-server shutdown` executed.  
  Alternative rejected: running restore despite the strict no-restore build rule.  
  Estimate: runtime 0 us; compile verification remains blocked by missing `Temp/obj/Hecton8.Editor/project.assets.json`.

## Re-Ingestion Notes

- Current IO rule: MemoryMappedFile removed; FileStream + NativeArray scratch buffers are the standard.
- Build gate: check `Docs/AgentLogs/BUILD_QUEUE.md` before every build or Unity refresh.
- Final compile command must use `--no-restore -m:2 /nr:false`, followed by `dotnet build-server shutdown`.

## Post-Completion Hardening Notes

- 2026-05-11T23:40:36+04:00: Managed Deflate fallback blocks are now explicitly marked in the compressed block length high bit, so a missing native LZ4 library no longer causes ambiguous "try LZ4 then guess Deflate" decoding. Runtime status remains `PENDING VERIFICATION` because compile/player proof is still blocked by missing restore assets.
- 2026-05-11T23:40:36+04:00: Added `CaseSensitiveResourceLoadValidator` for first-party static `Resources.Load("...")` case mismatches. It fails only real case mismatches and logs unresolved literals as warnings to avoid breaking legitimate generated/type-specific Resources usage.
- 2026-05-11T23:40:36+04:00: Added `NativePluginMatrixValidator`. Missing `.so`/`.dylib`/Steamworks binaries are warnings by default and hard failures when `HECTON_STRICT_NATIVE_PLUGIN_BUILD` is defined.
- 2026-05-12T00:03:28+04:00: Added `HectonThreadPriorityPolicy` and routed runtime thread priority assignments through it. Linux/macOS/mobile targets now normalize background/audio/heartbeat threads to `ThreadPriority.Normal`; Windows keeps the prior intent.
- 2026-05-12T00:03:28+04:00: Fresh first-party non-editor runtime scan found no active MMF/Win32 namespace/PInvoke markers (`MemoryMappedFile`, `SafeMemoryMappedViewHandle`, `kernel32.dll`, `Microsoft.Win32`, `System.Drawing`, `Windows.Forms`).
- 2026-05-12T00:11:31+04:00: Added `GraphicsApiMatrixValidator`. It checks Linux/Steam Deck Vulkan-first, macOS Metal-first, Windows D3D12+D3D11, and Android Vulkan-first/GLES3 fallback policy through Unity `PlayerSettings.GetGraphicsAPIs` instead of hand-editing `ProjectSettings.asset`.
- 2026-05-12T00:13:24+04:00: Added `ShaderPortabilityRiskValidator`. It scans first-party shader/compute/HLSL sources for group barriers, atomics/bitwise paths, and direct `sin/cos`; default mode warns, `HECTON_STRICT_SHADER_PORTABILITY_BUILD` makes findings build blockers.
- 2026-05-12T00:16:01+04:00: Added `HectonPersistentPathPolicy` and routed platform-core replay/telemetry/crash telemetry files through it. The helper keeps `Application.persistentDataPath` as the root but normalizes relative segments and rejects traversal in those cold-path filenames.
- 2026-05-12T00:43:40+04:00: Added `PlatformAdaptiveBudgetGovernor` and a production platform-pressure hook in `DynamicResolutionScaler`. Deck/UMA, low battery, VRAM pressure, and sustained frame pressure now feed existing scalability/render-scale consumers without adding SDK dependencies. Runtime status remains `PENDING VERIFICATION`.
- 2026-05-12T00:47:10+04:00: Tightened platform audit tooling. `SteamDeckPosixPreflightScanner` now skips editor-only C# for Linux-player blocker classification and its generated report text reflects the current FileStream/NativeArray storage path instead of stale MMF wording. `PlatformCompatibilityAudit` now reports runtime adaptation guards.
- 2026-05-12T01:10:19+04:00: Extended persistent path PAL routing into save thumbnails, save sidecars, save manager cold helpers, input rebinds, global profile, runtime diagnostics, quest audit logs, dev save smoke tests, and Data Archaeology sidecar path resolution. Updated persistence smoke expectations away from MMF-era checks.
- 2026-05-12T01:43:28+04:00: Added `XrPlatformReadinessValidator` and extended `PlatformCompatibilityAudit` with Android target SDK and mobile-VR manifest evidence. Android/mobile-VR and strict XR builds now fail when XR Management/OpenXR packages, VR build settings, Android app id, explicit target SDK, Android quality tiers, or mobile-VR manifest requirements are missing. Removed Android from quality-tier exclusions and set Android default quality to `Abyss (Low)`. Current source evidence still shows missing XR packages, empty VR settings, template Android app id, and automatic target SDK, so XR/Android status remains blocked until Unity Hub/package/settings work is done.
- 2026-05-12T01:56:23+04:00: Routed bootstrap boot-state/fatal-log files, bootstrap telemetry directory handshake, user options file, and dev bot CSV path through `HectonPersistentPathPolicy`. Remaining direct `Application.persistentDataPath` in `GameBootstrapper` is diagnostic text only.
