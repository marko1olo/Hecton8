# PLATFORM_STEAM_DECK Status

Agent ID: PLATFORM_STEAM_DECK  
Domain: ECHELON 1 Platform Abstraction / Core & Memory Infrastructure  
Task count: 25  
Status: PENDING VERIFICATION

## Mandates Read

- [x] PROJECT_LTS_Compatibility_Layer.txt | DOD practice: platform contact belongs in adapters/tools, not gameplay hot paths. Alternative rejected: broad runtime rewrite without owner proof. Estimate: 0 us runtime change until measured.
- [x] DATA_Save_Persistence_Binary_Delta_Checksum.txt | DOD practice: save owner is binary/MMF/LZ4, not JSON fallback. Alternative rejected: changing save format in compatibility pass. Estimate: 0 us runtime change.
- [x] STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt | DOD practice: native plugin/path audit is a build gate before asset streaming claims. Alternative rejected: player support claim from package presence. Estimate: 0 us runtime change.
- [x] CTRL_Device_Abstraction_Haptics.txt | DOD practice: Steam Deck input/haptics need PAL/SteamInput bridge, not direct gameplay API leaks. Alternative rejected: per-system controller reads. Estimate: PENDING MEASUREMENT.
- [x] GPU_Compute_Kernels_Kernels_Optimization_MX350.txt | DOD practice: Vulkan/Deck shader audit must flag barriers/bitwise/noise risk. Alternative rejected: assuming RTX shader path equals Deck path. Estimate: PENDING MEASUREMENT.
- [x] REND_URP_Graphics_HotPath_Optimization_HLOD.txt | DOD practice: VRAM and shader variant pressure are hard gates. Alternative rejected: visual feature readiness from editor compile. Estimate: PENDING MEASUREMENT.
- [x] OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt | DOD practice: Steam Deck treated as low-power shared-memory target. Alternative rejected: 2 GB dedicated VRAM assumption. Estimate: PENDING MEASUREMENT.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt | DOD practice: MMF/native pointer ownership must be audited before POSIX support. Alternative rejected: blind unsafe pointer edits. Estimate: PENDING MEASUREMENT.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt | DOD practice: black-box telemetry stays bounded and binary. Alternative rejected: managed JSON crash path. Estimate: PENDING MEASUREMENT.

## Tasks 1-5

- [x] 1. PATH NEUTRALIZATION | DOD practice: Roslyn analyzer scaffold + Unity preflight + two editor path fixes. Alternative rejected: mass replacing portable `Path.Combine`. Estimate: 0 us runtime; editor-only gate.
- [x] 2. POSIX MMF BRIDGE | DOD practice: removed first-party runtime `System.IO.MemoryMappedFiles`/`AcquirePointer` API usage from save, telemetry, replay, lore, options, and archaeology sidecars; retained binary formats through `FileStream` + `NativeArray<byte>`/fixed scratch buffers. Alternative rejected: keeping mmap behind platform guards. Estimate: 0 us hot-frame; save/replay IO cold path PENDING DEVICE MEASUREMENT.
- [x] 3. SAVE FOLDER MIGRATION | DOD practice: runtime persistence stays on `Application.persistentDataPath`; editor `LocalApplicationData` log path replaced with `Application.consoleLogPath`. Alternative rejected: AppData-specific runtime paths. Estimate: 0 us runtime.
- [BLOCKED BY NATIVE BINARIES] 4. NATIVE DLL WRAPPER | DOD practice: audio bridge now compiles platform attempts; scanner blocks missing `.so`/`.dylib`/Steam binary evidence. Alternative rejected: pretending Windows DLL satisfies Linux. Estimate: cold path only.
- [x] 5. VULKAN SHADER AUDIT | DOD practice: static shader preflight for barriers/noise/bitwise. Alternative rejected: assuming HLSL cross-compile success. Estimate: editor-only.

## Tasks 6-10

- [BLOCKED BY DECK/VULKAN CAPTURE] 6. COMPUTE SHADER BARRIER FIX | DOD practice: scanner flags `GroupMemoryBarrierWithGroupSync`/barrier risk. Alternative rejected: editing compute kernels without RenderDoc/Deck proof. Estimate: PENDING MEASUREMENT.
- [BLOCKED BY RUNTIME MONITOR OWNER] 7. VRAM FRAGMENTATION GUARD | DOD practice: report hard Deck cap at 1.6 GB shared-memory working ceiling. Alternative rejected: 2 GB dedicated assumption. Estimate: PENDING OWNER.
- [BLOCKED BY STEAMINPUT/OPENXR PAL] 8. GYRO-SENSITIVE KCC | DOD practice: PAL only. Alternative rejected: KCC direct Steam API dependency. Estimate: PENDING DEPENDENCY.
- [BLOCKED BY STEAMINPUT/UI OWNER] 9. TRACKPAD RADIAL MENU | DOD practice: input/PAL action map required before UI. Alternative rejected: new gameplay UI without owner. Estimate: PENDING DEPENDENCY.
- [BLOCKED BY BATTERY API PAL] 10. POWER-DRAIN DICTATOR | DOD practice: define audit gate first. Alternative rejected: Linux-only polling in gameplay. Estimate: PENDING DEPENDENCY.

## Tasks 11-15

- [BLOCKED BY STEAMINPUT PACKAGE] 11. HAPTIC FEEDBACK TRANSLATION | DOD practice: SteamInput package/native bridge required. Alternative rejected: raw Steam Deck LRA calls in gameplay. Estimate: PENDING DEPENDENCY.
- [x] 12. LINUX BUILD PRE-FLIGHT | DOD practice: Unity batch scanner writes `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md`. Alternative rejected: manual grep only. Estimate: editor-only.
- [x] 13. CASE-SENSITIVE PATH AUDIT | DOD practice: exact-case asset dictionary + Resources map. Alternative rejected: Windows filesystem trust. Estimate: editor-only.
- [BLOCKED BY SHADER WARMUP OWNER] 14. SHADER FALLBACK CI | DOD practice: scanner/report lists missing warmup proof. Alternative rejected: shader readiness claim without SVC/log. Estimate: PENDING OWNER.
- [BLOCKED BY THREAD OWNER/PROFILER] 15. THREAD PRIORITY NEUTRALIZATION | DOD practice: preflight flags Linux scheduler risk. Alternative rejected: changing audio critical thread in compatibility pass. Estimate: PENDING PROFILER.

## Tasks 16-20

- [x] 16. Replace `kernel32.dll` | DOD practice: removed runtime Win32 P/Invoke from watchdog, crash telemetry, and sparse-file hint. Alternative rejected: gated Win32 code staying in shared runtime. Estimate: cold path only.
- [x] 17. Linux mmap limit | DOD practice: current runtime static scan finds no `System.IO.MemoryMappedFiles`, `MemoryMappedFile`, `MemoryMappedViewAccessor`, `CreateFromFile`, `AcquirePointer`, or `ReleasePointer` under `Assets/_Project/Scripts` outside Editor. Alternative rejected: relying on Windows mmap semantics. Estimate: avoids mmap map-count pressure; device measurement pending.
- [BLOCKED BY IL2CPP/ARM ALIGNMENT AUDIT] 18. Unsafe alignment | DOD practice: MMF-acquired pointers are gone, but save/replay still use unsafe native copies and require IL2CPP/Linux/ARM alignment soak. Alternative rejected: declaring all unsafe code portable from Windows x64. Estimate: PENDING AUDIT.
- [BLOCKED BY HUB MODULE/BUILD PIPELINE] 19. Headless Linux target | Linux Dedicated Server module not installed in screenshot. Alternative rejected: fake headless support from desktop Linux module. Estimate: PENDING DEPENDENCY.
- [x] 20. Cyrillic filenames | DOD practice: path scan included in preflight. Alternative rejected: Windows-only path confidence. Estimate: editor-only.

## Tasks 21-25

- [x] 21. Windows.Forms strip | DOD practice: preflight blocks forbidden namespaces. Alternative rejected: runtime/editor assembly drift. Estimate: editor-only.
- [BLOCKED BY STEAMWORKS LINUX BINARY] 22. SteamManager Linux link | DOD practice: scanner checks `libsteam_api.so` evidence. Alternative rejected: SteamManager class presence as proof. Estimate: PENDING DEPENDENCY.
- [BLOCKED BY SYSTEMDISPATCHER OWNER] 23. SystemDispatcher 4C/8T | DOD practice: no blind scheduling rewrite. Alternative rejected: cross-domain job churn. Estimate: PENDING OWNER.
- [BLOCKED BY RENDER/UI OWNER] 24. Resolution reciprocal multiplies | DOD practice: requires render/UI owner pass. Alternative rejected: shotgun math edits. Estimate: PENDING OWNER.
- [x] 25. PLATFORM_BLOCKER_REGISTER | DOD practice: blocker register now separates stale preflight from current static continuation: runtime MMF API blockers reduced to 0; native binary blockers remain. Alternative rejected: optimistic green status without Deck/Linux player launch. Estimate: docs-only.

## 2026-05-11 Continuation Verification

- [x] Runtime MMF API purge static scan | Command: `rg "System\.IO\.MemoryMappedFiles|MemoryMappedFile|MemoryMappedViewAccessor|CreateFromFile|AcquirePointer|ReleasePointer" Assets/_Project/Scripts --glob '!Assets/_Project/Scripts/Editor/**'` returned no hits after final patch. Estimate: 0 us hot-frame; cold IO path changed.
- [x] Core compile check | `dotnet build Hecton8.Core.csproj -clp:ErrorsOnly` succeeded with 0 errors / 0 warnings after MMF removal. Estimate: verification only.
- [PENDING VERIFICATION] Full generated Unity preflight rerun | Unity batch process started but hung before scanner completion; stale `2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md` still shows old MMF rows and must be regenerated in a clean Editor/licensing pass. Alternative rejected: silently editing generated proof as if Unity reran it.
- [PENDING VERIFICATION] Full `Assembly-CSharp.csproj` check | timed out after 184 seconds with no final result. `Hecton8.Core.csproj` is the reliable compile evidence for this continuation.
