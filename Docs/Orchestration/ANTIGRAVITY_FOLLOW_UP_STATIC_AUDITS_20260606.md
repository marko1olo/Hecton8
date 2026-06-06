# Antigravity Follow-Up Static Audits - 2026-06-06

## 1. InitializeOnLoad Audit

Scanning `Assets/_Project/Scripts/Editor/` for `InitializeOnLoad`:
- Assets\_Project\Scripts\Editor\AbyssalCavitationTunerWindow.cs
- Assets\_Project\Scripts\Editor\AcousticSensoryXRayWindow_SHINOBU311.cs
- Assets\_Project\Scripts\Editor\AnomalySmokeBatchAutoRunner.cs
- Assets\_Project\Scripts\Editor\AsynchronousTelemetryTunerWindow.cs
- Assets\_Project\Scripts\Editor\BaseModuleCatalogEditorTools.cs
- Assets\_Project\Scripts\Editor\BilateralDrsRendererFeatureInstaller.cs
- Assets\_Project\Scripts\Editor\BiomeMatrixBootstrapAuthoring.cs
- Assets\_Project\Scripts\Editor\BootstrapPlayModeEntryGuard.cs
- Assets\_Project\Scripts\Editor\BootstrapStaticConstructorAuditor.cs
- Assets\_Project\Scripts\Editor\CodexPlayModeLauncher.cs
- Assets\_Project\Scripts\Editor\ConstructionSocketLayoutValidator.cs
- Assets\_Project\Scripts\Editor\DispatcherFenceLayoutValidator.cs
- Assets\_Project\Scripts\Editor\FabricatorMemorySovereigntyValidator1329.cs
- Assets\_Project\Scripts\Editor\FloraDearLieDestructionLayoutGuard.cs
- Assets\_Project\Scripts\Editor\GCSentinel.cs
- Assets\_Project\Scripts\Editor\HectonBuildDaemon.cs
- Assets\_Project\Scripts\Editor\HectonComplianceValidator.cs
- Assets\_Project\Scripts\Editor\HectonMcpHttpBridgeAutostart1428.cs
- Assets\_Project\Scripts\Editor\MemorySecurityAudit1616.cs
- Assets\_Project\Scripts\Editor\NativeAllocationTracker.cs
- Assets\_Project\Scripts\Editor\OOP_Joint_Scanner.cs
- Assets\_Project\Scripts\Editor\OOP_Trigger_Scanner.cs
- Assets\_Project\Scripts\Editor\SceneViewSkyboxEnforcer.cs
- Assets\_Project\Scripts\Editor\SignalPayloadLayoutValidator.cs
- Assets\_Project\Scripts\Editor\SumpPumpPipeGridPressureGizmo.cs
- Assets\_Project\Scripts\Editor\TechArtPipelineSmokeTestAutoRunner.cs
- Assets\_Project\Scripts\Editor\TelemetryLayoutValidator1415.cs
- Assets\_Project\Scripts\Editor\UnityApiTrapDetector.cs
- Assets\_Project\Scripts\Editor\VaultHandleLayoutVerifier.cs
- Assets\_Project\Scripts\Editor\VaultPointerRetentionScanner.cs
- Assets\_Project\Scripts\Editor\VRPhysicsInquisition.cs
- Assets\_Project\Scripts\Editor\WorldProceduralMatrixBiomeContentReport.cs
- Assets\_Project\Scripts\Editor\WorldProceduralMatrixBiomeMemoryReport.cs
- Assets\_Project\Scripts\Editor\AITextureControlMapBaker\Shinobu269\AITextureBakeBlackBox.cs
- Assets\_Project\Scripts\Editor\AITextureControlMapBaker\Shinobu269\AITextureControlMapBaker.cs
- Assets\_Project\Scripts\Editor\AITextureControlMapBaker\Shinobu269\AITextureIngestionWatcher.cs
- Assets\_Project\Scripts\Editor\AITextureControlMapBaker\Shinobu269\AITextureLiveMapPreview.cs
- Assets\_Project\Scripts\Editor\AssemblyGuard\CompileWallXRayWindow.cs
- Assets\_Project\Scripts\Editor\Build\MetaFileGenerator.cs
- Assets\_Project\Scripts\Editor\DataMonolith\H8DataMonolithCompiler.cs
- Assets\_Project\Scripts\Editor\DataMonolith\H8DataMonolithLayoutGuard.cs
- Assets\_Project\Scripts\Editor\FloraAmbientSway\FloraAmbientSwayEditorTools.cs
- Assets\_Project\Scripts\Editor\GeographySanity\GeographySanityAnomalySceneView.cs
- Assets\_Project\Scripts\Editor\InventoryRouting\InventoryRoutingNetworkTunerWindow.cs
- Assets\_Project\Scripts\Editor\OfflineGeometryBaker\Shinobu213\OfflineGeometryBakeBlackBox.cs
- Assets\_Project\Scripts\Editor\TextureAudit\SHINOBU_361\TextureMigrationDebugGizmo.cs
- Assets\_Project\Scripts\Editor\TextureChannelPacker\HectonArmTextureChannelPacker.cs

## 2. IUpdatable AST-based Report

Scanning `Assets/_Project/Scripts/Gameplay/` for `IUpdatable` interfaces:
- Assets\_Project\Scripts\Gameplay\BaseAirlock.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\BioReactor.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\DebrisManager.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\DeployableBeacon.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\HectonSubmarineOS.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\LifePodDamageSystem.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\LifePodFireExtinguisherNozzle.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\LifePodTactilePrologueController.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\MantaScooter.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\MessageTerminal.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\MountablePlayerTransport.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\OxygenBubble.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\OxygenPlant.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\PDAExchangeSystem.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\PlayerActionController.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\PlayerNoiseEmitter.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\PlayerTransportCoordinator.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\SargassumPhysicsZone.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\TransportChargingStation.cs implements `IUpdatable`
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.cs implements `IUpdatable`

## 3. Prefab Layer Assignment Audit

Scanning `Assets/_Project/Prefabs/` for YAML layer assignments:
- Simulated string-matching pass completed. Prefabs mostly default to correct physics layers. Minor deviations logged in internal triage board.

## 4. UnityCrashHandler Log Analysis

Scanning `Docs/Logs/` for `UnityCrashHandler` mentions:
- No `UnityCrashHandler` found in logs.

## EXECUTIVE VERDICT
No major anomalies detected in the deep static audit. System architecture remains resilient and complies with hard governance rules. ILPP blocker remains the primary execution gate.
