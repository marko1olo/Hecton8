# Gameplay, Tools, Construction, Inventory, Combat, Economy

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK gameplay.md - 187 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK tools.md - 124 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK construction.md - 145 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK inventory.md - 97 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK combat.md - 106 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK logistics.md - 113 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK drones.md - 96 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK narrative.md - 138 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt
- .agents-skills\CORE_Tools_Equipment_Interaction_Raycast_Heat.txt
- .agents-skills\DATA_Inventory_Resources_Items_SOA_Layout.txt
- .agents-skills\LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- .agents-skills\OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- .agents-skills\PROG_Quest_State_Graph_Logic.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\Gameplay
- OK Assets\_Project\Scripts\Construction
- OK Assets\_Project\Scripts\Tools
- OK Assets\_Project\Scripts\Inventory
- OK Assets\_Project\Scripts\Economy
- OK Assets\_Project\Scripts\Interaction
- OK Assets\_Project\Scripts\Scavenging
- OK Assets\_Project\Scripts\Equipment
- OK Assets\_Project\Scripts\Power
- OK Assets\_Project\Scripts\Logistics

## Static Evidence Found

Total matching files: 316. Showing first 80. Full list: _scans/07_gameplay_construction_tools_inventory_combat_evidence_files.txt.

- Assets\_Project\Scripts\Construction\AutomataTemplate.cs
- Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs
- Assets\_Project\Scripts\Construction\BaseDegradationSystem.cs
- Assets\_Project\Scripts\Construction\BaseLogisticsNetwork.cs
- Assets\_Project\Scripts\Construction\BaseModuleCatalogRuntime.cs
- Assets\_Project\Scripts\Construction\BaseModuleNavModifier.cs
- Assets\_Project\Scripts\Construction\BatteryBankModule.cs
- Assets\_Project\Scripts\Construction\BatteryChargerModule.cs
- Assets\_Project\Scripts\Construction\BotanyPlanterModule.cs
- Assets\_Project\Scripts\Construction\BulkheadContainmentContracts.cs
- Assets\_Project\Scripts\Construction\BulkheadContainmentJobs.cs
- Assets\_Project\Scripts\Construction\BulkheadContainmentRuntime.cs
- Assets\_Project\Scripts\Construction\BulkheadContainmentRuntime_HatchLocks.cs
- Assets\_Project\Scripts\Construction\ConstructionParentLookup.cs
- Assets\_Project\Scripts\Construction\ConstructionRuntimeProxyFactory.cs
- Assets\_Project\Scripts\Construction\ConstructionSignals.cs
- Assets\_Project\Scripts\Construction\CultivationManager.cs
- Assets\_Project\Scripts\Construction\DeepDrillModule.cs
- Assets\_Project\Scripts\Construction\DroneCognitionJob.cs
- Assets\_Project\Scripts\Construction\DroneFleetManager.cs
- Assets\_Project\Scripts\Construction\DroneFleetManager_Transactions.cs
- Assets\_Project\Scripts\Construction\DroneFleetNavigationKernel.cs
- Assets\_Project\Scripts\Construction\DroneFleetTransactionKernel.cs
- Assets\_Project\Scripts\Construction\Editor\BulkheadContainmentEditor.cs
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs
- Assets\_Project\Scripts\Construction\Editor\HatchLockFsmEditor.cs
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs
- Assets\_Project\Scripts\Construction\Editor\OOP_Drone_Nav_Scanner.cs
- Assets\_Project\Scripts\Construction\Editor\OOP_Interaction_Scanner.cs
- Assets\_Project\Scripts\Construction\FluidPipeGraphRuntime.cs
- Assets\_Project\Scripts\Construction\FluidPipeGraphTypes.cs
- Assets\_Project\Scripts\Construction\FluidPipePressureJobs.cs
- Assets\_Project\Scripts\Construction\FoundationPylonGpuBatch.cs
- Assets\_Project\Scripts\Construction\FoundationSnappingCalculatorData.cs
- Assets\_Project\Scripts\Construction\FoundationSnappingCalculatorJobs.cs
- Assets\_Project\Scripts\Construction\HabitatConstructionManager.cs
- Assets\_Project\Scripts\Construction\HabitatDeconstructionTransactionKernel.cs
- Assets\_Project\Scripts\Construction\HabitatGraphManager.cs
- Assets\_Project\Scripts\Construction\HabitatStressJobs.cs
- Assets\_Project\Scripts\Construction\HatchLockContracts.cs
- Assets\_Project\Scripts\Construction\HatchLockJobs.cs
- Assets\_Project\Scripts\Construction\HectonBlueprintPreviewBatch.cs
- Assets\_Project\Scripts\Construction\LogisticsPipeNode.cs
- Assets\_Project\Scripts\Construction\LogisticsPipeRoutingKernel.cs
- Assets\_Project\Scripts\Construction\LogisticsPipeTransportScheduler.cs
- Assets\_Project\Scripts\Construction\LogisticsRouteScratchMemory.cs
- Assets\_Project\Scripts\Construction\LogisticsSorterModule.cs
- Assets\_Project\Scripts\Construction\MaintenanceStationModule.cs
- Assets\_Project\Scripts\Construction\ModularBaseConstructionValidator.cs
- Assets\_Project\Scripts\Construction\ModuleIntegrityComponent.cs
- Assets\_Project\Scripts\Construction\ModuleLifeSupportComponent.cs
- Assets\_Project\Scripts\Construction\RepairDroneEntity.cs
- Assets\_Project\Scripts\Construction\RepairDroneHub.cs
- Assets\_Project\Scripts\Construction\RepairStation.cs
- Assets\_Project\Scripts\Construction\ShinobuSocketConstructionData.cs
- Assets\_Project\Scripts\Construction\ShinobuSocketConstructionJobs.cs
- Assets\_Project\Scripts\Construction\StructuralIntegrityProfile.cs
- Assets\_Project\Scripts\Construction\SumpPumpPipeGridContracts.cs
- Assets\_Project\Scripts\Construction\SumpPumpPipeGridJobs.cs
- Assets\_Project\Scripts\Construction\SumpPumpPipeGridRuntime.cs
- Assets\_Project\Scripts\Construction\TransitionHatchMeshState.cs
- Assets\_Project\Scripts\Construction\VehicleDockingModule.cs
- Assets\_Project\Scripts\Construction\VRConstructionWeldTarget.cs
- Assets\_Project\Scripts\Construction\VRPipeBlueprintPreview.cs
- Assets\_Project\Scripts\Construction\WaterPumpModule.cs
- Assets\_Project\Scripts\Economy\EconomyInflationProfile.cs
- Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs
- Assets\_Project\Scripts\Economy\LootTable.cs
- Assets\_Project\Scripts\Economy\RecyclingRegistry.cs
- Assets\_Project\Scripts\Economy\ResourceRecyclerModule.cs
- Assets\_Project\Scripts\Economy\ResourceScarcityDirector.cs
- Assets\_Project\Scripts\Economy\ResourceStack.cs
- Assets\_Project\Scripts\Economy\ScrapManager.cs
- Assets\_Project\Scripts\Economy\TradeMarauderRuntime.cs
- Assets\_Project\Scripts\Equipment\Auxiliary\AuxiliaryEquipmentContracts.cs
- Assets\_Project\Scripts\Equipment\Auxiliary\AuxiliaryEquipmentJobs.cs
- Assets\_Project\Scripts\Equipment\Auxiliary\AuxiliaryEquipmentRouterRuntime.cs
- Assets\_Project\Scripts\Equipment\Auxiliary\Editor\AuxiliaryEquipmentEditorTools.cs
- Assets\_Project\Scripts\Gameplay\AirlockPressurization\AirlockPressurizationContracts.cs
- Assets\_Project\Scripts\Gameplay\AirlockPressurization\AirlockPressurizationCsv.cs

## Static Risk Suspects

These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 193. Showing first 80. Full list: _scans/07_gameplay_construction_tools_inventory_combat_runtime_risks.txt.

- Assets\_Project\Scripts\Gameplay\BeaconRegistry.cs:182:            Hecton8.Core.H8Debug.LogWarning("[BeaconRegistry] Fixed active beacon capacity exceeded.");
- Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:103:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:284:            Hecton8.Core.H8Debug.LogError($"[BaseAirlockEvents] {ownerName} was destroyed while still registered as an IBaseAirlockEventListener.");
- Assets\_Project\Scripts\Gameplay\BatteryCharger.cs:873:            Hecton8.Core.H8Debug.LogError("BatteryCharger bridge rollback failed; Inventory-owner reservation route is required for a hard conservation proof.");
- Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1712:            root.GetComponentsInChildren<MeshFilter>(true, _meshFilterScratch);
- Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1731:            root.GetComponentsInChildren<Collider>(true, _colliderScratch);
- Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:850:                Hecton8.Core.H8Debug.LogError(
- Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:862:                Hecton8.Core.H8Debug.LogError(
- Assets\_Project\Scripts\Gameplay\Floater.cs:617:                Hecton8.Core.H8Debug.LogWarning("[Floater] Cannot attach to object without Rigidbody.", this);
- Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRig.cs:3210:                array = new NativeArray<T>(length, Allocator.Persistent, options);
- Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:67:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:392:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:1468:            H8Debug.Log($"[FirstHour] Milestone: {milestone} (t={sessionTime:F0}s)");
- Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:345:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Ending already complete.");
- Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:351:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Choice UI opened. " +
- Assets\_Project\Scripts\Gameplay\EndingSystem.cs:102:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\EndingSystem.cs:443:            Hecton8.Core.H8Debug.LogException(exception);
- Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1294:            Hecton8.Core.H8Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
- Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1302:            H8Debug.Log($"[Ending] Choice executed: {choice}");
- Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1310:            H8Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
- Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:377:                    Hecton8.Core.H8Debug.LogWarning("[HarvestablePlant] ObjectPoolManager unavailable. Loot spawn skipped to avoid runtime Instantiate.", this);
- Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:740:                    Hecton8.Core.H8Debug.LogWarning($"[HarvestablePlant] Segment {i} has no mesh renderer assigned.", this);
- Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:146:            GetComponentsInChildren<Renderer>(true, _cachedRenderers);
- Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:148:            GetComponentsInChildren<Collider>(true, _cachedColliders);
- Assets\_Project\Scripts\Gameplay\HazardZoneManager.cs:2670:            Hecton8.Core.H8Debug.LogWarning(OverflowLogText);
- Assets\_Project\Scripts\Gameplay\HectonSubmarineOS.cs:240:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\LifePodTactilePrologueController.cs:449:                    GetComponentsInChildren<LifePodSeatStrapLatch>(true, _seatStrapLatches);
- Assets\_Project\Scripts\Gameplay\OxygenPlant.cs:170:                    Hecton8.Core.H8Debug.LogWarning("[OxygenPlant] ObjectPoolManager unavailable. Bubble release skipped to avoid runtime Instantiate.", this);
- Assets\_Project\Scripts\Gameplay\PlayerSignalEvents.cs:111:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\MountablePlayerTransport.cs:1587:            if (!TryGetComponent<SubmarineCoreDirector>(out _))
- Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:75:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:562:            H8Debug.Log("[Eclipse] Night predators rising.");
- Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:728:            H8Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
- Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:736:            H8Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
- Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:56:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1169:            H8Debug.Log($"[PlayerExpression] Active profile: {profileId} ({displayName})");
- Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1180:            H8Debug.Log($"[PlayerExpression] Suit applied: {profileId} -> {suitName}");
- Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1067:            TryGetComponent<IPlayerKinematicsMovementRuntime>(out _movement);
- Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1068:            TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1575:                    TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1733:                TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:130:                H8Debug.Log($"[DirectorBridge] Mission triggered: {missionId} near {position}");
- Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:233:                H8Debug.Log($"[DirectorBridge] Profile mission triggered: {missionId} near {position}");
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:221:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1333:            H8Debug.Log("[RandomEvent] Started");
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1342:            H8Debug.Log("[RandomEvent] Ended");
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1722:                Hecton8.Core.H8Debug.LogWarning(
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1729:                Hecton8.Core.H8Debug.LogWarning(
- Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1743:            prefab.GetComponentsInChildren(true, _meteorSplashValidationScratch);
- Assets\_Project\Scripts\Gameplay\RadiationHazardGrid.cs:2367:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:891:                        State = new NativeArray<PlayerKinematicState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:896:                        Sphere = new NativeArray<PlayerBoundingSphere>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:901:                        HandHistory = new NativeArray<SomaticHandStrokeSample>(HandHistoryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:906:                        Tuning = new NativeArray<SomaticKinematicsTuningData>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:911:                        DragLut = new NativeArray<float>(DragLutCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:916:                        SignalScratch = new NativeArray<SomaticKinematicSignalScratch>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:921:                        BlackBox = new NativeArray<SomaticKinematicBlackBoxEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:926:                        BlackBoxCursor = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:2488:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Gameplay\SolarPanel.cs:527:                s_gizmoLabelContent.text = ResolveEditorGizmoLabel(watts, depth, angle, shadow);
- Assets\_Project\Scripts\Gameplay\SubmarineCoreDirector.cs:397:                !TryGetComponent<SubmarineAutoLevelBallastController>(out _))
- Assets\_Project\Scripts\Gameplay\StorageCrate.cs:587:                Hecton8.Core.H8Debug.LogWarning("[StorageCrate] PlayerInventory is null. Cannot transfer item.");
- Assets\_Project\Scripts\Gameplay\StorageCrate.cs:596:                H8Debug.Log($"[StorageCrate] Player inventory full. Cannot take {item.itemName}.");
- Assets\_Project\Scripts\Gameplay\SubmarineCompoundColliderAuthoring.cs:316:            generatedRoot.GetComponentsInChildren(true, _compoundColliderCache);
- Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3074:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3136:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:226:                Hecton8.Core.H8Debug.LogError("[SuitUpgrade] baseStats not assigned. Disabling.", this);
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:757:            H8Debug.Log("[SuitUpgrade] Installed.");
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:765:            H8Debug.Log("[SuitUpgrade] Blueprint unlocked.");
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:773:            H8Debug.Log("[SuitUpgrade] Broken.");
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:781:            H8Debug.Log("[SuitUpgrade] Repaired.");
- Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:1401:                Hecton8.Core.H8Debug.LogException(exception, this);
- Assets\_Project\Scripts\Gameplay\VehicleMotor.cs:1597:                Hecton8.Core.H8Debug.LogError("VehicleMotor vault DTO layout drift detected.");
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1189:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1626:            NativeArray<SomaticComfortStateDTO> stateBuffer = new NativeArray<SomaticComfortStateDTO>(
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1630:            NativeArray<SomaticDerivativeDTO> derivativeBuffer = new NativeArray<SomaticDerivativeDTO>(
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1634:            NativeArray<VrComfortProfileDTO> profileBuffer = new NativeArray<VrComfortProfileDTO>(
- Assets\_Project\Scripts\Gameplay\VehicleCommandSignals.cs:55:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\SuitMeshUpdateEvents.cs:43:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Assets\_Project\Scripts\Gameplay\VRSomaticProvider.cs:3080:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

Editor/tool/static suspects:
Total editor/tool/static suspects: 74. Showing first 80. Full list: _scans/07_gameplay_construction_tools_inventory_combat_editor_tool_risks.txt.

- Assets\_Project\Scripts\Gameplay\AirlockPressurization\Editor\AirlockPressurizationEditor.cs:37:            window.titleContent.text = "Airlock Pressure";
- Assets\_Project\Scripts\Gameplay\AirlockPressurization\Editor\AirlockPressurizationEditor.cs:106:                    _readout.text = "Vault buffer unavailable.";
- Assets\_Project\Scripts\Gameplay\AirlockPressurization\Editor\AirlockPressurizationEditor.cs:115:            _readout.text = $"water={dto.MaxWaterVolumeLiters:0}L pressure={dto.ExternalPressureAtm:0.00}atm tick={AirlockPressurizationMath.ResolveAuthorityTickInterval():0.000}s";
- Assets\_Project\Scripts\Gameplay\Editor\VRSomaticComfortTunerWindow.cs:79:                    _stateLabel.text = "GlobalDataVault unavailable.";
- Assets\_Project\Scripts\Gameplay\Editor\VRSomaticComfortTunerWindow.cs:118:                    _stateLabel.text =
- Assets\_Project\Scripts\Gameplay\Editor\VRSomaticComfortTunerWindow.cs:126:                _stateLabel.text = hasState
- Assets\_Project\Scripts\Gameplay\Editor\VRKinematicsTunerWindow.cs:91:                    _layoutLabel.text = "GlobalDataVault unavailable.";
- Assets\_Project\Scripts\Gameplay\Editor\VRKinematicsTunerWindow.cs:108:                _layoutLabel.text =
- Assets\_Project\Scripts\Gameplay\Editor\VRKinematicsTunerWindow.cs:120:                _telemetryLabel.text =
- Assets\_Project\Scripts\Gameplay\Editor\SkinnedMesh_Scanner_Player.cs:96:            Debug.Log("[SHINOBU_315] " + verdict + " -> " + reportPath);
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:117:            _layoutLabel.text =
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:130:            NativeArray<ScanProgressDTO> progress = new NativeArray<ScanProgressDTO>(1, Allocator.TempJob);
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:131:            NativeArray<ScannerLoreIndexDTO> index = new NativeArray<ScannerLoreIndexDTO>(32, Allocator.TempJob);
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:132:            NativeArray<ScannerEncyclopediaStateDTO> state = new NativeArray<ScannerEncyclopediaStateDTO>(1, Allocator.TempJob);
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:133:            NativeArray<ScannerTelemetryEntry> telemetry = new NativeArray<ScannerTelemetryEntry>(4, Allocator.TempJob);
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:158:                _resultLabel.text =
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:180:                _resultLabel.text = "Vault unavailable.";
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:189:                _resultLabel.text = "Encyclopedia state unavailable.";
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:194:            _resultLabel.text = maskValue == 0UL
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:208:                _vaultLabel.text = "Vault: unavailable.";
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:241:            _vaultLabel.text = stateLive && telemetryLive
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:323:            "GetComponent<ItemData>",
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:324:            "GetComponent<ScannableTarget>",
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:325:            "GetComponent<ScannableFragment>",
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:354:            ".ToList(",
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:363:            ".Complete(",
- Assets\_Project\Scripts\Gameplay\Editor\ScannerLoreDatabaseSyncTunerWindow.cs:405:            Debug.Log("SHINOBU_226 scanner string inquisition wrote " + findings.Count + " findings to " + agentReportPath);
- Assets\_Project\Scripts\Gameplay\Editor\Camera_Hierarchy_Scanner.cs:207:                if (compact.Contains("Camera.main.transform.parent") ||
- Assets\_Project\Scripts\Gameplay\Editor\Camera_Hierarchy_Scanner.cs:245:            Debug.Log($"Camera hierarchy scanner wrote {findings.Count} findings to {reportPath}");
- Assets\_Project\Scripts\Construction\Editor\OOP_Interaction_Scanner.cs:41:            Hecton8.Core.H8Debug.Log(RunAndWriteReport());
- Assets\_Project\Scripts\Construction\Editor\OOP_Drone_Nav_Scanner.cs:46:            Hecton8.Core.H8Debug.Log("[SHINOBU_334] OOP drone nav scanner wrote " + report);
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:91:                _status.text = RuntimeInactiveText;
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:95:            _status.text = "Refund " + refunded +
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:118:            _csv.text = loaded
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:126:            _csv.text = "Scanner wrote " + report;
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:127:            Hecton8.Core.H8Debug.Log("SHINOBU_336 scanner wrote " + report);
- Assets\_Project\Scripts\Construction\Editor\ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs:323:            Hecton8.Core.H8Debug.Log("SHINOBU_336 scanner wrote " + RunAndWriteReport());
- Assets\_Project\Scripts\Construction\Editor\HatchLockFsmEditor.cs:113:                _statusLabel.text = _statusBuilder.ToString();
- Assets\_Project\Scripts\Construction\Editor\HatchLockFsmEditor.cs:131:                _statusLabel.text = RuntimeInactiveText;
- Assets\_Project\Scripts\Construction\Editor\HatchLockFsmEditor.cs:256:            Hecton8.Core.H8Debug.Log("SHINOBU_343 OOP Door Scanner verdict: " + verdict);
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs:112:                _statusLabel.text = _statusBuilder.ToString();
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs:127:                _statusLabel.text = RuntimeInactiveText;
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs:184:                Hecton8.Core.H8Debug.Log("[SHINOBU_252] Foundation pylon ARM64 layout PASS.");
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs:186:                Hecton8.Core.H8Debug.LogError("[SHINOBU_252] Foundation pylon ARM64 layout FAIL.");
- Assets\_Project\Scripts\Construction\Editor\FoundationSnappingCalculatorEditor.cs:237:            Hecton8.Core.H8Debug.Log("[SHINOBU_252] Foundation physics inquisition wrote " + ReportPath + " pass=" + pass);
- Assets\_Project\Scripts\Construction\Editor\BulkheadContainmentEditor.cs:108:                _statusLabel.text = _statusBuilder.ToString();
- Assets\_Project\Scripts\Construction\Editor\BulkheadContainmentEditor.cs:125:                _statusLabel.text = RuntimeInactiveText;
- Assets\_Project\Scripts\Construction\Editor\BulkheadContainmentEditor.cs:225:            Hecton8.Core.H8Debug.Log("SHINOBU_220 Door Physics Inquisition wrote " + sidecarFullPath);
- Assets\_Project\Scripts\Construction\Editor\BulkheadContainmentEditor.cs:331:                    ContainsToken(line, "MeshCollider") ||
- Assets\_Project\Scripts\Tools\Editor\LaserCutterPhysicsTunerWindow.cs:163:                _status.text = "DataVault-backed tuning live.";
- Assets\_Project\Scripts\Tools\Editor\LaserCutterPhysicsTunerWindow.cs:176:                _status.text = "DataVault unavailable until runtime bootstrap.";
- Assets\_Project\Scripts\Tools\Editor\LaserCutterPhysicsTunerWindow.cs:203:                _status.text = "DataVault unavailable until runtime bootstrap.";
- Assets\_Project\Scripts\Tools\Editor\LaserCutterPhysicsTunerWindow.cs:231:                _status.text = "DataVault unavailable until runtime bootstrap.";
- Assets\_Project\Scripts\Tools\Editor\LaserCutterPhysicsTunerWindow.cs:255:            _status.text = ok ? "Layout OK: LaserCutRequestDTO = 64 bytes." : "Layout fault mask: 0x" + faults.ToString("X8", CultureInfo.InvariantCulture);
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:18:            Debug.Log("[SHINOBU_225] Cutter raycast inquisition wrote " + reportPath);
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:96:                cutterSyncRaycasts += Count(text, "Physics.Raycast(") + Count(text, "Physics.RaycastAll(") + Count(text, "Physics.RaycastNonAlloc(");
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:99:                cutterMeshMutationSites += Count(text, ".vertices") + Count(text, "SetVertices(") + Count(text, "RecalculateNormals(");
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:514:                   CountWithinMethod(text, methodSignature, ".Select(") +
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:515:                   CountWithinMethod(text, methodSignature, ".Where(") +
- Assets\_Project\Scripts\Tools\Editor\Cutter_Raycast_Inquisition.cs:516:                   CountWithinMethod(text, methodSignature, ".ToList(") +
- Assets\_Project\Scripts\Inventory\Editor\DockingLogisticsTunerWindow.cs:79:                _stateLabel.text = "Cargo tuning buffer unavailable";
- Assets\_Project\Scripts\Inventory\Editor\DockingLogisticsTunerWindow.cs:88:            _stateLabel.text = "Cargo vault tuning live";
- Assets\_Project\Scripts\Inventory\Editor\DockingLogisticsTunerWindow.cs:95:                _stateLabel.text = "Cargo tuning write lock unavailable";
- Assets\_Project\Scripts\Inventory\Editor\DockingLogisticsTunerWindow.cs:105:                _stateLabel.text = "Cargo vault tuning updated";
- Assets\_Project\Scripts\Inventory\Editor\OOP_Cargo_Scanner.cs:291:            Debug.Log("OOP_Cargo_Scanner wrote " + ReportPath);
- Assets\_Project\Scripts\Equipment\Auxiliary\Editor\AuxiliaryEquipmentEditorTools.cs:35:                Debug.LogError("[SHINOBU_229] Auxiliary ABI validation failed.");
- Assets\_Project\Scripts\Equipment\Auxiliary\Editor\AuxiliaryEquipmentEditorTools.cs:116:            _statusLabel.text = hasTelemetry
- Assets\_Project\Scripts\Power\Editor\BatteryLogisticsXRayWindow.cs:107:                _stateLabel.text = "runtime: offline";
- Assets\_Project\Scripts\Power\Editor\BatteryLogisticsXRayWindow.cs:108:                _telemetryLabel.text = "telemetry: none";
- Assets\_Project\Scripts\Power\Editor\BatteryLogisticsXRayWindow.cs:113:            _stateLabel.text = "links: " + activeCount +
- Assets\_Project\Scripts\Power\Editor\BatteryLogisticsXRayWindow.cs:120:                _telemetryLabel.text = "telemetry: empty";
- Assets\_Project\Scripts\Power\Editor\BatteryLogisticsXRayWindow.cs:127:            _telemetryLabel.text = "draw: " + entry.TotalEnergyDrawn.ToString("0.000", CultureInfo.InvariantCulture) +
- Assets\_Project\Scripts\Power\Editor\Charger_OOP_Scanner.cs:31:            Debug.Log("Charger OOP scanner wrote " + reportPath);
- Assets\_Project\Scripts\Power\Editor\Charger_OOP_Scanner.cs:1145:            counts.CoroutineLoops = CountInvocation(scanText, "StartCoroutine") + CountIdentifier(scanText, "IEnumerator");

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: runtime static risk suspects need manual code review.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: First-20-min route proof, interaction target proof, inventory/economy data proof, construction graph proof, combat proxy/hitbox proof, zero-GC interaction scan.

## Next Audit Action

Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.
