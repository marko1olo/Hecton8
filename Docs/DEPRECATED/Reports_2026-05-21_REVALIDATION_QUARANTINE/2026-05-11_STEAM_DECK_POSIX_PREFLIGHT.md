<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Steam Deck POSIX Preflight

- Status: PENDING VERIFICATION
- Strict mode: True
- Project root: C:/hades/Hecton8
- Generated: 2026-05-11 18:32:56
- Proof boundary: static/editor scan only. No Linux player launch, Steam Deck device run, Vulkan RenderDoc capture, profiler, GCMonitor, thermals, or battery API proof.
- Blockers: 11
- Warnings: 294

## Mandates Applied

- `PROJECT_LTS_Compatibility_Layer.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## POSIX MMF Path Resolution Code

Current Win32 file-length probes were replaced with managed file metadata. This is not a full MMF proof; unsafe mapped pointers still require Linux player verification.

```csharp
private static bool TryGetFileLength(string path, out long bytes)
{
    bytes = -1L;
    if (string.IsNullOrEmpty(path)) return false;
    FileInfo info = new FileInfo(path);
    if (!info.Exists) return false;
    bytes = info.Length;
    return true;
}
```

## Case-Sensitive Path Audit Logic

```csharp
Dictionary<string,string> map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
// Store exact Assets/... path as value; probe string literals with OrdinalIgnoreCase lookup.
// If lookup succeeds but requested path != actual path by StringComparison.Ordinal, fail the audit.
```

## Unity Hub Cannot Fix

- Missing Linux/macOS native plugin binaries (`liblz4.so`, `HectonAudioKernel.so`, `libsteam_api.so`).
- Unsafe MMF pointer alignment and Linux `mmap` limits.
- Shader barrier/noise compatibility on older Vulkan drivers.
- Steam Deck gyro/trackpad/haptic integration without a SteamInput/PAL owner.
- Case-sensitive asset path defects.

## Blockers

| Severity | Location | Finding |
|---|---|---|
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:567 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:597 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:699 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:862 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:888 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:899 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:4901 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Scripts/SaveBinaryStorage.cs:4957 | Unsafe MMF pointer acquisition requires alignment and POSIX mmap verification. |
| BLOCKER | Assets/_Project/Plugins | WINDOWS-ONLY DLL BLOCKER: liblz4.dll exists but no liblz4.so was found for Linux/Steam Deck. |
| BLOCKER | Assets/Plugins | WINDOWS-ONLY DLL BLOCKER: HectonAudioKernel.dll exists but no HectonAudioKernel.so/libHectonAudioKernel.so was found. |
| BLOCKER | Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs | SteamManager present but libsteam_api.so evidence is missing. Steam Deck overlay/cloud/callbacks are not proven. |

## Warnings

| Severity | Location | Finding |
|---|---|---|
| WARN | Assets/_Project/Editor/HectonDevToolsMenu.cs:197 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/HectonDevToolsMenu.cs:214 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/HectonLodGroupConflictResolver.cs:18 | Asset path literal was not found exactly: `Assets/_Project/Prefabs/Nature/GOTOVYE PREFABY KAMNEY/ENV_ Skala2.prefab`. |
| WARN | Assets/_Project/Editor/HectonLodGroupConflictResolver.cs:19 | Asset path literal was not found exactly: `Assets/_Project/Prefabs/Nature/GOTOVYE PREFABY KAMNEY/ENV_ Bolder 2.prefab`. |
| WARN | Assets/_Project/Editor/HectonMeshGenerator.cs:91 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/HectonSkyAtlasGenerator.cs:372 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/HectonSkyTools.cs:62 | Asset path literal was not found exactly: `Assets/_Project/Art/Textures/Sky/HectonSkyAtlas_RGBA.png`. |
| WARN | Assets/_Project/Editor/SaveSlotManagerWindow.cs:395 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/SaveSlotManagerWindow.cs:409 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Editor/UnityReloadAuditReport.cs:86 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/BoidFishInstanced.shader:320 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Scripts/BoidSimulation.compute:27 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Scripts/BoidSimulation.compute:294 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Scripts/BoidSimulation.compute:385 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Scripts/BoidSimulation.compute:455 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:3 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:275 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:278 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:1602 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:1603 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:1677 | ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2220 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2224 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2227 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2272 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2276 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:2279 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/FieldToolRuntimeSmokeTester.cs:549 | Asset path literal was not found exactly: `Assets/_Project/Data/Items/Data_Titanium.asset`. |
| WARN | Assets/_Project/Scripts/OmegaSurvivalKinematicsSmokeTester.cs:124 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/OmegaSurvivalKinematicsSmokeTester.cs:125 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/OmegaSurvivalKinematicsSmokeTester.cs:127 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:80 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:84 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:4 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:25 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:78 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:559 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:565 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:566 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:846 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:860 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:861 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:3657 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:4889 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:4899 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorage.cs:4900 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveBinaryStorageNativeArrayExtensions.cs:2 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/SaveManager.cs:1792 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveManager.cs:3098 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveManager.cs:3110 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs:271 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs:298 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveRecoverySmokeTester.cs:208 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveRecoverySmokeTester.cs:209 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveRecoverySmokeTester.cs:210 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveSidecarStorage.cs:395 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs:158 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveThumbnailSystem.cs:93 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/SaveThumbnailSystem.cs:456 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/VisualOmegaSmokeTester.cs:201 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Art/Shaders/HectonFirmamentBake.compute:33 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/HectonHudFogLuminance.compute:35 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Art/Shaders/HectonHudFogLuminance.compute:43 | Compute barrier needs Vulkan/Deck validation; divergent early returns can deadlock groups. |
| WARN | Assets/_Project/Art/Shaders/Hecton_AbyssalSSDO.shader:90 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute:70 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute:222 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute:289 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute:358 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader:256 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader:258 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader:259 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hecton_ScannerPulseInstanced.shader:80 | Sine-based noise in shader should be replaced with LUT/hash/poly path for Deck/MX350. |
| WARN | Assets/_Project/Art/Shaders/Hecton_VoxelSSAO.compute:46 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/Hidden_Hecton_BiosDiagnostic.shader:72 | Sine-based noise in shader should be replaced with LUT/hash/poly path for Deck/MX350. |
| WARN | Assets/_Project/Art/Shaders/Hidden_Hecton_ScannerDepthProjection.shader:75 | Sine-based noise in shader should be replaced with LUT/hash/poly path for Deck/MX350. |
| WARN | Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute:349 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute:362 | Bitwise shader noise/hash path needs SPIR-V compiler validation on older Vulkan drivers. |
| WARN | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:1592 | ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits. |
| WARN | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3901 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3941 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3997 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4358 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Core/BlackBoxHeartbeatThread.cs:40 | ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits. |
| WARN | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:671 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:688 | ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits. |
| WARN | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:689 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:886 | ThreadPriority must be verified against Linux scheduler starvation and Steam Deck 4C/8T limits. |
| WARN | Assets/_Project/Scripts/Core/RebindingManager.cs:515 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Core/RebindingManager.cs:523 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/BotController.cs:215 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/BotController.cs:453 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/CelestialCataclysmSmokeTester.cs:526 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs:524 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/NarrativeProgressionSmokeTester.cs:118 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Dev/NarrativeProgressionSmokeTester.cs:119 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs:384 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/BiomeMatrixRuntimeVisualProfileAuthoring.cs:92 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/BiomeRegistryEditor.cs:14 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/BiomeRegistryEditor.cs:21 | Asset path literal was not found exactly: `Assets/_Project/Data/HectonBiomeRegistry.asset`. |
| WARN | Assets/_Project/Scripts/Editor/BlackBoxBinaryReader.cs:85 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs:659 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs:731 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:146 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:160 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:500 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:506 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:512 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:518 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs:524 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/DodReplayScrubberWindow.cs:48 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:228 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:230 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:231 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:232 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:233 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:234 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:235 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:319 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ErosionTestHarness.cs:322 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HectonAssetPipelineAudit.cs:42 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HectonAssetQuarantineUtility.cs:93 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HectonAssetQuarantineUtility.cs:127 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HectonGeneratedProjectReferencePruner.cs:114 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:153 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/DepthZones/DepthZone_TheDropDeep.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:162 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier1.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:167 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Oxygen_Tier1.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:172 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier2.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:177 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier3.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs:182 | Asset path literal was not found exactly: `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Hull_Tier4.asset`. |
| WARN | Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs:111 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs:113 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/LayerMaskSanitizer.cs:237 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/LifePodTactilePrologueSmokeTester.cs:706 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/LifePodTactilePrologueSmokeTester.cs:819 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/LoadingScreenPrefabCreator.cs:19 | Asset path literal was not found exactly: `Assets/_Project/Prefabs/UI/LoadingScreen.prefab`. |
| WARN | Assets/_Project/Scripts/Editor/LocalizationCjkCoverageValidator.cs:123 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/LocalizationCjkFontBootstrap.cs:367 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/NarrativeProgressionSmokeTestRunner.cs:38 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/NarrativeProgressionSmokeTestRunner.cs:39 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/NativeLeakScanner.cs:39 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/NativeLeakScanner.cs:98 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/NativeLeakScanner.cs:426 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/OmegaAutonomySmokeTestRunner.cs:32 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/OmegaAutonomySmokeTestRunner.cs:33 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/OrphanedComponentSweeper.cs:87 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:93 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:203 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:294 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:398 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:407 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs:458 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SignalCryptographySmokeTester.cs:751 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SignalCryptographySmokeTester.cs:752 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SignalCryptographySmokeTester.cs:765 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SignalCryptographySmokeTester.cs:766 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngine098TerrainSmokeTestRunner.cs:32 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngine098TerrainSmokeTestRunner.cs:33 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:94 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:102 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:186 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:210 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:242 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:266 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:267 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:268 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs:320 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTestAutoRunner.cs:66 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTester.cs:17 | Asset path literal was not found exactly: `Assets/_Project/Art/TEXTURES/Utility/TX_BlueNoise_256_R8.png`. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTester.cs:223 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTester.cs:224 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTester.cs:229 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/TechArtPipelineSmokeTester.cs:230 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/VolumetricBiomeSmokeTestRunner.cs:47 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/VolumetricBiomeSmokeTestRunner.cs:48 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFamilyContractValidator.cs:104 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFinalVariantAuthoring.cs:411 | Asset path literal was not found exactly: `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/{prefabPrefix}_{suffix}.prefab`. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:109 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:335 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:697 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:855 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:860 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:865 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs:870 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs:283 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs:351 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs:1183 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralGeologyStatusReport.cs:51 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralMatrixBiomeContentReport.cs:101 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralMatrixBiomeMemoryReport.cs:101 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscStatusReport.cs:41 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralPatternBalanceReport.cs:75 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralStructuralStatusReport.cs:61 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/WorldProceduralSupportStatusReport.cs:45 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ZeroGCComplianceScanner.cs:225 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:3 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1012 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1013 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1088 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1089 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1139 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1150 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:4 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:430 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:434 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:437 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:468 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:472 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:475 | MMF usage requires Linux mmap/player soak proof and per-process map-count budget. |
| WARN | Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:605 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:948 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:953 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:291 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:438 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:291 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:295 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:308 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:756 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:876 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Quest/QuestStateManager.cs:1233 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/UI/FontAssetRecovery.cs:18 | Asset path literal was not found exactly: `Assets/_Project/Art/Materials/Fonts/04420435043a04410442 SDF.asset`. |
| WARN | Assets/_Project/Scripts/UI/FontAssetRecovery.cs:19 | Asset path literal was not found exactly: `Assets/_Project/Art/Materials/Fonts/0446043804440440044b SDF.asset`. |
| WARN | Assets/_Project/Scripts/UI/PDALoadoutTab.cs:224 | Asset path literal was not found exactly: `Assets/_Project/Data/Tools/Presets/Preset_Exploration.asset`. |
| WARN | Assets/_Project/Scripts/UI/PDALoadoutTab.cs:225 | Asset path literal was not found exactly: `Assets/_Project/Data/Tools/Presets/Preset_Construction.asset`. |
| WARN | Assets/_Project/Scripts/UI/PDALoadoutTab.cs:226 | Asset path literal was not found exactly: `Assets/_Project/Data/Tools/Presets/Preset_FieldRecovery.asset`. |
| WARN | Assets/_Project/Scripts/UI/PDALoadoutTab.cs:227 | Asset path literal was not found exactly: `Assets/_Project/Data/Tools/Presets/Preset_Defense.asset`. |
| WARN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:1997 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3315 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3323 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Tests/PlayMode/InquisitionStabilityPlayModeTests.cs:552 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Tests/PlayMode/InquisitionStabilityPlayModeTests.cs:554 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:338 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Audio/Editor/AudioOmegaAutonomySmokeTester.cs:99 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Audio/Editor/DSPThreadSafetySmokeTester.cs:182 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:63 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/BuildLogPathScrubber.cs:67 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/BuildLogPathScrubber.cs:79 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/MachineCodePurityPrebuildScanner.cs:50 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:74 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:75 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:80 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:81 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:92 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThirdPartyStrippingGuard.cs:128 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThreadAffinityPrebuildScanner.cs:63 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/Build/ThreadAffinityPrebuildScanner.cs:82 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:226 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:227 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:246 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:255 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:276 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:295 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:314 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:363 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs:374 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Optimization/Editor/RenderTextureResolutionAnalyzer.cs:142 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/_Project/Scripts/Optimization/Editor/VRAMDiagnosticReport.cs:48 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/ES3.cs:685 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/ES3.cs:1333 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Referencing/ES3GlobalReferences.cs:40 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Settings/ES3Settings.cs:26 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Settings/ES3Settings.cs:309 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Settings/ES3Settings.cs:309 | Resources.Load key not found by static Resources path map: `ES3Defaults`. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Settings/ES3Settings.cs:309 | Resources.Load key not found by static Resources path map: `ES3 Default Settings`. |
| WARN | Assets/Plugins/Easy Save 3/Scripts/Streams/ES3ResourcesStream.cs:18 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DTGUIHelper.cs:1413 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DTGUIHelper.cs:1456 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicGroupVariationInspector.cs:85 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupCreatorInspector.cs:2535 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupCreatorInspector.cs:3498 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupCreatorInspector.cs:3652 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupInspector.cs:1300 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/DynamicSoundGroupInspector.cs:2127 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioGroupInspector.cs:133 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioGroupInspector.cs:1507 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioGroupInspector.cs:2481 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioInspector.cs:2809 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioInspector.cs:5384 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/MasterAudioInspector.cs:7416 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/SoundGroupOrganizerInspector.cs:1783 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/DarkTonic/MasterAudio/SoundGroupVariationInspector.cs:141 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/Editor/RelationsInspector/BackendUtils/ScriptableObjectBackendToolbar.cs:40 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/Plugins/Editor/RelationsInspector/BackendUtils/ScriptableObjectBackendToolbar.cs:77 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/Plugins/Editor/RelationsInspector/BackendUtils/ScriptableObjectBackendToolbar.cs:82 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/Plugins/Editor/RelationsInspector/BackendUtils/ScriptableObjectBackendToolbar.cs:115 | CTO path-neutrality prompt requested review of Path.Combine call sites; prefer a project PAL for persisted runtime paths. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/AudioResourceOptimizer.cs:127 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/AudioResourceOptimizer.cs:174 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/MasterAudio.cs:3791 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/MasterAudio.cs:9105 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/MasterAudio.cs:9128 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/MasterAudio.cs:9151 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/MasterAudio.cs:9173 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/Plugins/DarkTonic/MasterAudio/Scripts/Singleton/SingletonScriptable.cs:32 | Resources.Load requires case-sensitive path audit and Addressables migration proof. |
| WARN | Assets/_Project/Plugins | liblz4.dylib missing; macOS save compression path is not proven. |
| WARN | Assets/Plugins | HectonAudioKernel.dylib missing; macOS native audio path is not proven. |

## Info

| Severity | Location | Finding |
|---|---|---|
| INFO | - | none |

## Regression Model

- CPU: scanner/editor tooling adds no player hot-path work. Removing Win32 sparse hint is cold-path only and may affect disk allocation, not frame time.
- GC: scanner allocations are editor-only. Runtime GC proof remains absent until Play Mode/player profiling.
- Memory: no project settings, scenes, prefabs, URP assets, or Addressables groups are mutated.
- Correctness: Linux build must remain blocked while Windows-only native dependencies and unsafe MMF proof gaps exist.
- Failure modes: missing native plugins, shader compile/runtime Vulkan defect, file path case mismatch, MMF map-count exhaustion, Steam Deck input provider absence.

