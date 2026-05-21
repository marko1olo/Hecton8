# Illegal Singletons Audit

Date: `2026-04-29`
Status: `PENDING VERIFICATION`

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `STRM_Persistent_Object_Registry.txt`

## Scope

- Targeted ownership surface: first-party runtime code under `Assets/_Project/Scripts/`.
- Match rule: any class declaring `public static ... Instance`.
- Coverage rule: class name must be represented inside `Assets/_Project/Scripts/Core/GlobalRegistry.cs` directly or via the existing proxy accessors already used by `SaveManager` and `ObjectPoolManager`.
- External/package singleton noise was sampled, but excluded from the PASS/FAIL verdict because `GlobalRegistry` is a first-party service locator, not a normalization target for Crest, Shapes, Feel, Easy Save 3, or package code.

## Audit Verdict

`FAIL`

Current first-party coverage:

- `97` first-party `public static Instance` declarations found.
- `7` are represented in `GlobalRegistry.cs`.
- `90` are not represented in `GlobalRegistry.cs`.

Covered names verified in `GlobalRegistry.cs`:

- `AbyssalThermalManager`
- `HectonFluidEngine`
- `HectonNarrativeDirector`
- `ObjectPoolManager`
- `PlayerInventory`
- `QuestManager`
- `SaveManager`

## Offending First-Party Files

- `Assets/_Project/Scripts/AcousticZoneController.cs:121`
- `Assets/_Project/Scripts/AmbientWaterMotionManager.cs:74`
- `Assets/_Project/Scripts/AsyncLoadHelper.cs:80`
- `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs:273`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs:61`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:85`
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:325`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:328`
- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:53`
- `Assets/_Project/Scripts/BeaconNetworkSystem.cs:44`
- `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:71`
- `Assets/_Project/Scripts/ConstructionManager.cs:54`
- `Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs:111`
- `Assets/_Project/Scripts/Economy/ScrapManager.cs:28`
- `Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs:33`
- `Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs:25`
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:23`
- `Assets/_Project/Scripts/EntityChangeDetector.cs:281`
- `Assets/_Project/Scripts/FieldOperationLogSystem.cs:42`
- `Assets/_Project/Scripts/FlowFieldVisualizer.cs:65`
- `Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs:93`
- `Assets/_Project/Scripts/Gameplay/EndingSystem.cs:107`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:134`
- `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:218`
- `Assets/_Project/Scripts/Gameplay/MissionManager.cs:28`
- `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:63`
- `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:41`
- `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs:83`
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:129`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:56`
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs:140`
- `Assets/_Project/Scripts/HectonDiscoveryManager.cs:50`
- `Assets/_Project/Scripts/HectonFloatingOrigin.cs:118`
- `Assets/_Project/Scripts/HectonRockManager.cs:36`
- `Assets/_Project/Scripts/Input/InputManager.cs:107`
- `Assets/_Project/Scripts/Input/RebindingManager.cs:44`
- `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs:29`
- `Assets/_Project/Scripts/LocalizationManager.cs:94`
- `Assets/_Project/Scripts/MapMagicBridge.cs:68`
- `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs:55`
- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:135`
- `Assets/_Project/Scripts/Meta/RunModifierController.cs:28`
- `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs:48`
- `Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs:49`
- `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:234`
- `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs:13`
- `Assets/_Project/Scripts/Optimization/CameraRTManager.cs:23`
- `Assets/_Project/Scripts/Optimization/PostFXRTManager.cs:23`
- `Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs:23`
- `Assets/_Project/Scripts/Optimization/RenderTexturePool.cs:22`
- `Assets/_Project/Scripts/Optimization/UIRTManager.cs:23`
- `Assets/_Project/Scripts/Optimization/VisorRTManager.cs:23`
- `Assets/_Project/Scripts/Optimization/VRAMMonitor.cs:37`
- `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:78`
- `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:76`
- `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:38`
- `Assets/_Project/Scripts/PerformanceMonitor.cs:137`
- `Assets/_Project/Scripts/PowerGridManager.cs:28`
- `Assets/_Project/Scripts/PrefabRegistry.cs:70`
- `Assets/_Project/Scripts/RaycastBatchHelper.cs:63`
- `Assets/_Project/Scripts/ScanLogSystem.cs:52`
- `Assets/_Project/Scripts/ScavengePopulator.cs:90`
- `Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs:35`
- `Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs:62`
- `Assets/_Project/Scripts/Tools/PerformanceMonitor.cs:72`
- `Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs:30`
- `Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs:45`
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:29`
- `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs:66`
- `Assets/_Project/Scripts/UI/SettingsManager.cs:64`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs:167`
- `Assets/_Project/Scripts/UI/UITooltip.cs:25`
- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:35`
- `Assets/_Project/Scripts/Visor/SpectrumSystem.cs:177`
- `Assets/_Project/Scripts/World/AbyssalFluidDecalManager.cs:114`
- `Assets/_Project/Scripts/World/BasePollutionManager.cs:83`
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:35`
- `Assets/_Project/Scripts/World/CullingManager.cs:63`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs:76`
- `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:65`
- `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs:24`
- `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs:46`
- `Assets/_Project/Scripts/World/HectonBiolumController.cs:68`
- `Assets/_Project/Scripts/World/ImpostorSystem.cs:155`
- `Assets/_Project/Scripts/World/LODSystemManager.cs:89`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:512`
- `Assets/_Project/Scripts/World/SargassumCutManager.cs:251`
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:651`
- `Assets/_Project/Scripts/World/SoundscapeSystem.cs:76`
- `Assets/_Project/Scripts/WorldStateManager.cs:59`

## External Noise Sampled But Excluded From Verdict

- `Packages/com.waveharmonic.crest/...`
- `Assets/Crest/...`
- `Assets/Shapes/...`
- `Assets/Feel/...`
- `Assets/Plugins/Easy Save 3/...`
- `Assets/Plugins/DarkTonic/MasterAudio/...`

Reason for exclusion:

- these paths are not first-party ownership surfaces
- some are known third-party singleton templates
- forcing them into `GlobalRegistry` would violate third-party asset integrity and ownership boundaries

## Regression Model

- CPU: none; audit-only document pass.
- GC: none introduced; no runtime code changed.
- Memory: none introduced outside markdown storage.
- Cadence: ownership drift remains active until runtime managers stop declaring unmanaged static accessors.
- Correctness: false-positive risk is low for the first-party list because the rule is a direct text match on `public static ... Instance`.

## Hot Path Impact

- none from this change set
- the underlying architecture risk remains high because unmanaged singleton access preserves ambiguous init order and bypasses explicit registry ownership

## Failure Modes

- `GlobalRegistry.cs` may add new proxies later; this file will go stale if not re-audited.
- Some offender files may be editor/test-only despite living under `Assets/_Project/Scripts/`; this audit did not whitelist by runtime scene reachability.
- If a class uses `Instance` only as a legacy facade over `GlobalRegistry`, it is still listed unless the class name is represented in `GlobalRegistry.cs`.

## Why Kept

- The report is literal, low-assumption, and tied to code search results.
- The verdict is restricted to first-party ownership where `GlobalRegistry` policy is actually enforceable.
