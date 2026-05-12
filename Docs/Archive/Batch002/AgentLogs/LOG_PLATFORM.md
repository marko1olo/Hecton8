# LOG_PLATFORM

Top = old, bottom = new.

## 2026-05-11T23:02:03+04:00 | PLATFORM_COMMAND Start

What was wrong: No platform task status, no rationale file, and no build queue file existed for this agent.

What was done: Registered the session and marked platform work `PENDING VERIFICATION`.

Cinematic Cheats used: none.

Exact Microseconds saved: 0 runtime us; build contention avoidance is process-level, not frame-time.

## 2026-05-11T23:24:00+04:00 | PLATFORM_COMMAND Platform Agnosticism Pass

What was wrong:
- Save compression still assumed native `liblz4` availability; Linux/macOS had no verified `.so`/`.dylib`.
- Audio Kernel native bridge only handled Windows/editor Windows cleanly and did not share a central safe-load policy.
- D3D11 legacy PCs could enter compute culling despite driver/backend risk.
- Steam Deck gyro/trackpads had no PAL fields in the frame input snapshot.
- Mac/Metal shader precision risk was not a build blocker.
- Android standalone VR templates and `MOBILE_VR` define path were absent.
- Linux/macOS case-sensitive asset collisions were not a standalone build gate.
- Steam Deck shared-memory VRAM budget was not enforced as UMA.
- Battery critical mode and monotonic replay timestamp policy were absent.

What was done:
- Added `Assets/_Project/Scripts/Core/HectonNativeBridge.cs`.
- Routed Audio Kernel native calls through safe availability checks.
- Added native-first LZ4 and managed Deflate fallback in `SaveBinaryStorage`.
- Added `HardwareTierDetector`, D3D11 compute-culling block, Steam Deck/UMA detection, and 960 MB shared-memory VRAM budget.
- Added Steam Deck gyro/trackpad proxy capture and zero-trig radial sector resolution.
- Added `MetalShaderPrecisionAuditor`, `CaseSensitiveAssetCollisionValidator`, `MobileVrBuildDefinePreprocessor`, and `PlatformDebugMetadataStripper`.
- Added `HectonXRManager` 2048x2048 baseline eye descriptor.
- Added `Assets/Plugins/Android/AndroidManifest.xml` and `mainTemplate.gradle`.
- Added `HapticWaveformLibrary`, `PlatformBatteryWatchdog`, and `PlatformPrecisionClock`.
- Added `Docs/Reports/PLATFORM_BLOCKER_REGISTER.md` and `Docs/Reports/STEAM_PUBLISHER_METADATA_TEMPLATE.md`.

Cinematic Cheats used:
- Steam Deck radial menu sectoring uses dominant-axis and diagonal-boundary comparisons instead of `atan2`.
- Haptic waveform generation uses triangle/square/saw phase math instead of `sin/cos`.
- D3D11 vegetation avoids compute culling and falls back to existing CPU culling path instead of attempting honest GPU indirect work on weak/legacy drivers.

Exact Microseconds saved:
- Radial menu: estimated 0.5-2 us per interaction burst versus atan/sqrt sectoring.
- Haptic waveform: estimated 0.5-3 us during active command sets versus trig waveform evaluation.
- D3D11 vegetation: estimated 50-300 us avoided in vegetation-heavy scenes by not attempting unstable compute/indirect dispatch paths; actual CPU cost depends on visible instance count.
- Battery watchdog: sub-us per sampled frame; sampled every 300 frames.
- Native safe-load/fallback: hot-path runtime 0 us; save/load fallback is cold path and slower than native LZ4.

Binary hunt result:
- LZ4 Linux x64/ARM64 and macOS Universal: not downloaded. Official LZ4 releases are source-focused; no trusted project-ready universal binaries were vendored.
- Steamworks `libsteam_api.so`/`.dylib`: not downloaded. Must come from official Steamworks SDK `redistributable_bin`, not mirrors.
- Audio Kernel Linux/macOS: not downloaded. No verified official binary source exists in workspace.

Managed LZ4 fallback evidence:
- `SaveBinaryStorage.TryCompressBlock` tries native `LZ4_compress_default`, marks `HectonNativeLibrary.Lz4` unavailable on load/bind failure, then calls `DeflateBlockCompressManaged`.
- `SaveBinaryStorage.TryDecompressBlock` tries native `LZ4_decompress_safe`; if unavailable or wrong length, it tries `DeflateBlockDecompressManaged`.
- Fallback is intentionally called "managed fallback", not true managed LZ4. It prevents non-Windows save-total failure, but is slower and older native-only builds will not read Deflate fallback saves.

Final Git Diff:
- Tracked platform diff stat: 8 tracked files, 876 insertions, 377 deletions. This includes pre-existing uncommitted MMF-removal changes in `SaveBinaryStorage`; unrelated dirty workspace changes were not reverted.
- New files created by PLATFORM_COMMAND: `HectonNativeBridge.cs`, `HardwareTierDetector.cs`, `SteamDeckInputPal.cs`, `SteamDeckRadialMenu.cs`, `HectonXRManager.cs`, `PlatformBatteryWatchdog.cs`, `PlatformPrecisionClock.cs`, `MetalShaderPrecisionAuditor.cs`, `CaseSensitiveAssetCollisionValidator.cs`, `MobileVrBuildDefinePreprocessor.cs`, `PlatformDebugMetadataStripper.cs`, `HapticWaveformLibrary.cs`, Android manifest/Gradle templates, platform blocker/publisher docs.

Verification:
- `git diff --check` on touched tracked runtime files: no whitespace errors; line-ending warnings only.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:2 /nr:false`: failed before C# compilation with `NETSDK1004` because `Temp/obj/Hecton8.Editor/project.assets.json` is missing.
- `dotnet build-server shutdown`: completed.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T01:10:19+04:00 | PLATFORM_COMMAND Persistent Path PAL Expansion

What was wrong:
- Multiple save-adjacent cold paths still joined files directly under `Application.persistentDataPath`.
- Persistence smoke tooling still asserted an MMF-era save write path even though the current standard is FileStream plus NativeArray scratch buffers.

What was done:
- Routed save thumbnails, save sidecar absolute path conversion, save manager cold helpers, input rebind files, global profile files, runtime diagnostics directory, quest audit log, save runtime smoke tests, and Data Archaeology sidecar path through `HectonPersistentPathPolicy`.
- Updated `PersistenceUxSmokeTester` to expect FileStream/NativeArray/UnsafeMemoryCopyGuard rather than `MemoryMappedFile.CreateFromFile`.
- Narrowed Steam Deck preflight path warnings to direct `Path.Combine(Application.persistentDataPath, ...)` instead of every valid `Path.Combine`.

Cinematic Cheats used:
- None. This is path policy consolidation, not simulation or visual math.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Save/load cold path: no performance claim. The value is platform path determinism and cleaner POSIX audit output.

Verification:
- `rg` shows no first-party runtime direct `Path.Combine(Application.persistentDataPath, ...)` outside editor/dev scan text.
- `rg` shows first-party runtime MMF markers are absent outside editor scanner/assert text.
- `git diff --check` passed for the touched path files; CRLF warnings only.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:47:10+04:00 | PLATFORM_COMMAND Audit Tooling Honesty Pass

What was wrong:
- Steam Deck/POSIX preflight still scanned editor-only C# as player-relevant code, which could inflate Linux-player blockers with audit/tool implementation details.
- The generated report text still described an MMF-oriented storage proof section even though the current IO rule is FileStream plus NativeArray scratch buffers.
- The general platform audit did not expose the new runtime adaptation guards as a matrix.

What was done:
- Updated `SteamDeckPosixPreflightScanner` to skip `/Editor/` C# during player portability classification.
- Replaced the stale MMF report section with POSIX storage text rooted in `Application.persistentDataPath` / project PAL usage.
- Updated `PlatformCompatibilityAudit` with a runtime adaptation matrix covering native bridge, hardware tier, path PAL, adaptive pressure governor, battery watchdog, and thread-priority policy.

Cinematic Cheats used:
- None. This is evidence hygiene and scanner correctness.

Exact Microseconds saved:
- Runtime: 0 us.
- Editor audit time saved: unmeasured; expected reduction is triage noise, not player frame time.

Verification:
- `rg` confirms new report sections and scanner skip hook.
- `git diff --check` passed for the modified audit files; CRLF warning only.
- No Unity audit was executed; generated reports remain stale until import/batch audit runs.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:43:40+04:00 | PLATFORM_COMMAND Adaptive Platform Runtime Governor

What was wrong:
- Platform compatibility work had hard build gates and one-time hardware clamps, but no unified runtime pressure path for Deck/UMA, weak PC frame pressure, critical battery, and near-budget VRAM.
- Existing adaptive systems already read `GlobalRegistry.ScalabilityTier` and `DynamicResolutionScaler.CurrentRenderScale`; they needed a single platform signal instead of per-system platform branches.

What was done:
- Added `Assets/_Project/Scripts/Core/PlatformAdaptiveBudgetGovernor.cs`.
- Added production `DynamicResolutionScaler.SetPlatformPressureRenderScale(...)` support without using debug override state.
- Governor samples every 120 frames and packs pressure reasons into `PlatformAdaptivePressureFlags`.
- Deck/shared memory targets 0.78 render scale; VRAM pressure targets 0.72; sustained frame pressure targets 0.70; critical battery targets 0.62.
- Pressure applies the existing low scalability tier so math LOD, fauna, underwater, impostor, audio, and world residency consumers get one coherent downshift.

Cinematic Cheats used:
- Replaced broad per-system platform logic with one bitmask pressure signal.
- Used render-scale floor pressure instead of honest subsystem-by-subsystem simulation throttling.
- Used low-cadence sampling rather than per-frame battery/VRAM polling.

Exact Microseconds saved:
- Idle runtime: 0 us on 119/120 frames; sampled frame remains intended sub-0.1 ms.
- Direct code saving: no honest microsecond claim without player profiling.
- Expected downstream saving: roughly 100-800 us under Deck/UMA or low-VRAM visual pressure once existing adaptive consumers react to lower render scale.

Verification:
- `rg` confirms the production pressure hook and governor entry points exist.
- `git diff --check` passed for `DynamicResolutionScaler.cs`; CRLF warning only.
- No Unity refresh/import/build was run because dependency installation is deferred and compile verification is still blocked by missing generated restore assets.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:16:01+04:00 | PLATFORM_COMMAND Persistent Path PAL

What was wrong:
- Replay and telemetry files used direct `Path.Combine(Application.persistentDataPath, ...)` in several platform-core systems.
- Path policy was correct but scattered.

What was done:
- Added `Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs`.
- Routed `DodReplayRecorder`, `GlobalTelemetryBus`, and `CrashTelemetryBuffer` persistent file/directory construction through the policy.
- Kept Unity `Application.persistentDataPath` as the root; normalized relative segments and guarded against traversal in cold-path filenames.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Cold path only; this is portability hardening, not performance work.

Verification:
- `rg` confirms platform-core replay/telemetry persistent paths now go through `HectonPersistentPathPolicy`.
- `git diff --check` passed for the helper and migrated files; CRLF warnings only.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:13:24+04:00 | PLATFORM_COMMAND Shader Portability Gate

What was wrong:
- Vulkan/Deck/standalone-VR shader risk markers exist, but were not isolated in a dedicated build gate.
- Directly editing dirty/untracked shader assets would change visuals without device proof.

What was done:
- Added `Assets/_Project/Scripts/Editor/Build/ShaderPortabilityRiskValidator.cs`.
- The validator scans first-party `.compute`, `.shader`, and `.hlsl` files for group barriers, compute returns, atomics/bitwise paths, and direct `sin/cos`.
- Default mode warns; `HECTON_STRICT_SHADER_PORTABILITY_BUILD` makes findings hard build blockers.

Cinematic Cheats used:
- None implemented in shader code in this pass. The validator identifies where triangle/parabolic/LUT approximations should replace expensive math after visual proof.

Exact Microseconds saved:
- Runtime: 0 us.
- Future savings are not claimed until specific shader replacements are made and profiled.

Verification:
- `git diff --check` passed for the new shader validator.
- No Unity compile/import was run.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:11:31+04:00 | PLATFORM_COMMAND Graphics API Matrix Gate

What was wrong:
- Linux/Steam Deck graphics API order was not proven through Unity API.
- Manual `ProjectSettings.asset` edits would be fragile and could corrupt platform serialization.

What was done:
- Added `Assets/_Project/Scripts/Editor/Build/GraphicsApiMatrixValidator.cs`.
- Linux/Steam Deck now requires Vulkan-first and hard-fails build when violated.
- macOS checks Metal-first.
- Windows checks D3D12 plus D3D11 fallback for modern and legacy PC coverage.
- Android checks Vulkan-first and warns if GLES3 fallback is missing.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: 0 us.
- Build waste avoided: unmeasured; prevents wrong graphics API player attempts before device testing.

Verification:
- `git diff --check` passed for the new validator.
- No Unity API execution yet; status remains pending until Unity import/compile is available.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T00:03:28+04:00 | PLATFORM_COMMAND Thread Priority Neutralization

What was wrong:
- Runtime code directly assigned `ThreadPriority.AboveNormal`, `BelowNormal`, and `Lowest`.
- Linux/Steam Deck scheduler starvation risk remained for replay, telemetry, and crash black-box workers.

What was done:
- Added `Assets/_Project/Scripts/Core/HectonThreadPriorityPolicy.cs`.
- Routed procedural audio producer, DOD replay writer, heartbeat monitor, global telemetry export, and crash telemetry export thread priorities through the policy.
- POSIX/mobile targets now normalize these threads to `ThreadPriority.Normal`; Windows keeps the previous role-specific priorities.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Thread creation only: one switch per thread creation.
- Risk reduction: avoids Linux scheduler starvation class; no honest microsecond number claimed without device profiling.

Verification:
- `rg` over first-party non-editor runtime now finds direct priority constants only inside `HectonThreadPriorityPolicy`.
- `rg` over first-party non-editor runtime found no active MMF/Win32 markers: `MemoryMappedFile`, `SafeMemoryMappedViewHandle`, `kernel32.dll`, `Microsoft.Win32`, `System.Drawing`, `Windows.Forms`.
- `git diff --check` passed for the touched thread-priority files; CRLF warnings only.

Status:
- `PENDING VERIFICATION`.
- Unity import, restore/project regeneration, player builds, Steam Deck run, Mac Metal run, Android standalone VR run are still required.

## 2026-05-11T23:40:36+04:00 | PLATFORM_COMMAND Post-Completion Hardening

What was wrong:
- Managed Deflate fallback blocks were recoverable but not explicitly marked in the compressed block header.
- Missing Linux/macOS/Android native binaries were documented, but there was no lightweight build-time matrix gate.
- Static first-party `Resources.Load("...")` literals did not have a dedicated case-sensitive build guard.

What was done:
- Added a managed-Deflate high-bit marker to compressed block lengths in `SaveBinaryStorage` standard and protected block streams.
- Added fail-fast checks so an unencodable compressed block length cannot silently write `0` into the header.
- Added `Assets/_Project/Scripts/Editor/Build/CaseSensitiveResourceLoadValidator.cs`.
- Added `Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs`.
- Updated `Docs/Reports/PLATFORM_BLOCKER_REGISTER.md` with the new guards and stricter fallback wording.

Cinematic Cheats used:
- None. This pass is platform determinism and build gating, not simulation math.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Save/load failure avoidance: unmeasured; prevents ambiguous fallback decode on platforms without native LZ4.
- Build-time native matrix: 0 runtime us; catches missing `.so`/`.dylib`/Steamworks binaries before export.

Verification:
- `git diff --check` passed for the modified save file and new editor validators; CRLF warnings only where applicable.
- No Unity refresh and no compile were run in this pass because dependency installation/restore is deferred and the previous no-restore build is blocked by missing `Temp/obj/Hecton8.Editor/project.assets.json`.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T01:43:28+04:00 | PLATFORM_COMMAND XR Platform Readiness Gate

What was wrong:
- Android/mobile-VR had a manifest template but no hard gate for missing XR Management/OpenXR packages, empty VR build settings, template Android app id, automatic target SDK, or Android-excluded quality tiers.
- Unity Hub modules can supply toolchains, but they do not prove project package/settings/device readiness.

What was done:
- Added `Assets/_Project/Scripts/Editor/Build/XrPlatformReadinessValidator.cs`.
- Added `Assets/_Project/Scripts/Editor/Build/XrPlatformReadinessValidator.cs.meta`.
- Extended `Assets/_Project/Scripts/Editor/Build/PlatformCompatibilityAudit.cs` with Android target SDK and mobile-VR manifest checks.
- Removed Android from quality-tier exclusions in `ProjectSettings/QualitySettings.asset` and set Android default quality to `Abyss (Low)`.
- Updated `Docs/Reports/PLATFORM_BLOCKER_REGISTER.md` with exact XR/Android blockers.
- Android/mobile-VR and `HECTON_STRICT_XR_BUILD` builds now fail on missing XR packages/settings/identity/quality requirements. The menu audit logs the same state without requiring a player build.

Cinematic Cheats used:
- None. This is build gating and compatibility evidence, not simulation math.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Avoided invalid build/device-test time is unmeasured; no frame-time claim.

Verification:
- Static evidence: `Packages/manifest.json` currently lacks `com.unity.xr.management` and `com.unity.xr.openxr`.
- Static evidence: `ProjectSettings/ProjectSettings.asset` has `m_BuildTargetVRSettings: []`, template Android app id, and `AndroidTargetSdkVersion: 0`.
- Static evidence: `ProjectSettings/QualitySettings.asset` now includes Android quality tiers and maps Android to low-tier quality index 1.
- Static evidence: `Assets/Plugins/Android/AndroidManifest.xml` contains `VIBRATE`, `android.hardware.vr.headtracking`, and `hecton8.mobile_vr_template`.
- No Unity refresh/import/build was run.

Status:
- `PENDING VERIFICATION`.

## 2026-05-12T01:56:23+04:00 | PLATFORM_COMMAND Bootstrap And Options Path PAL

What was wrong:
- Bootstrap boot-state/fatal-log paths, user options path, and dev bot CSV path still used direct `Application.persistentDataPath` file joins.
- These were cold paths, but they kept POSIX/Steam Cloud path policy scattered.

What was done:
- Routed `GameBootstrapper` boot-state, fatal boot crash log, and telemetry directory handshake through `HectonPersistentPathPolicy`.
- Routed `UserOptionsPersistence` options file through `HectonPersistentPathPolicy`.
- Routed `BotController` expedition CSV path through `HectonPersistentPathPolicy`.
- Left bootstrap diagnostic raw-path text intact because it is not a file join.

Cinematic Cheats used:
- None. This is file path PAL hardening.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Cold IO path: no speed claim; value is platform determinism.

Verification:
- `rg` now finds no direct `Application.persistentDataPath` file joins in `UserOptionsPersistence` or `BotController`.
- `GameBootstrapper` direct persistent path hit is diagnostic raw-path logging only.
- No Unity refresh/import/build was run.

Status:
- `PENDING VERIFICATION`.
