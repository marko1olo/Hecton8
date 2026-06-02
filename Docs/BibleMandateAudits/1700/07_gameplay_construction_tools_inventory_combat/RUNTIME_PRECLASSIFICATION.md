# Runtime Preclassification - Gameplay, Tools, Construction, Inventory, Combat, Economy

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 193.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 96
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 40
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 19
- REVIEW_UNCLASSIFIED_STATIC_RISK: 17
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 12
- REVIEW_LOG_GUARD_REQUIRED: 3
- LIKELY_LEGAL_COLD_LOOKUP: 3
- LIKELY_LEGAL_COLD_OR_PRESENTATION_PATH: 2
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (96)

- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BeaconRegistry.cs:182:            Hecton8.Core.H8Debug.LogWarning("[BeaconRegistry] Fixed active beacon capacity exceeded.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:284:            Hecton8.Core.H8Debug.LogError($"[BaseAirlockEvents] {ownerName} was destroyed while still registered as an IBaseAirlockEventListener.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BatteryCharger.cs:873:            Hecton8.Core.H8Debug.LogError("BatteryCharger bridge rollback failed; Inventory-owner reservation route is required for a hard conservation proof.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:850:                Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:862:                Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Floater.cs:617:                Hecton8.Core.H8Debug.LogWarning("[Floater] Cannot attach to object without Rigidbody.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:392:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:1468:            H8Debug.Log($"[FirstHour] Milestone: {milestone} (t={sessionTime:F0}s)");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:345:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Ending already complete.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:351:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Choice UI opened. " +
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:443:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1294:            Hecton8.Core.H8Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1302:            H8Debug.Log($"[Ending] Choice executed: {choice}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1310:            H8Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:377:                    Hecton8.Core.H8Debug.LogWarning("[HarvestablePlant] ObjectPoolManager unavailable. Loot spawn skipped to avoid runtime Instantiate.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:740:                    Hecton8.Core.H8Debug.LogWarning($"[HarvestablePlant] Segment {i} has no mesh renderer assigned.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HazardZoneManager.cs:2670:            Hecton8.Core.H8Debug.LogWarning(OverflowLogText);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\OxygenPlant.cs:170:                    Hecton8.Core.H8Debug.LogWarning("[OxygenPlant] ObjectPoolManager unavailable. Bubble release skipped to avoid runtime Instantiate.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:562:            H8Debug.Log("[Eclipse] Night predators rising.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:728:            H8Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:736:            H8Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1169:            H8Debug.Log($"[PlayerExpression] Active profile: {profileId} ({displayName})");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1180:            H8Debug.Log($"[PlayerExpression] Suit applied: {profileId} -> {suitName}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:130:                H8Debug.Log($"[DirectorBridge] Mission triggered: {missionId} near {position}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:233:                H8Debug.Log($"[DirectorBridge] Profile mission triggered: {missionId} near {position}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1333:            H8Debug.Log("[RandomEvent] Started");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1342:            H8Debug.Log("[RandomEvent] Ended");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1722:                Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1729:                Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\StorageCrate.cs:587:                Hecton8.Core.H8Debug.LogWarning("[StorageCrate] PlayerInventory is null. Cannot transfer item.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\StorageCrate.cs:596:                H8Debug.Log($"[StorageCrate] Player inventory full. Cannot take {item.itemName}.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:226:                Hecton8.Core.H8Debug.LogError("[SuitUpgrade] baseStats not assigned. Disabling.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:757:            H8Debug.Log("[SuitUpgrade] Installed.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:765:            H8Debug.Log("[SuitUpgrade] Blueprint unlocked.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:773:            H8Debug.Log("[SuitUpgrade] Broken.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:781:            H8Debug.Log("[SuitUpgrade] Repaired.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\SuitUpgradeManager.cs:1401:                Hecton8.Core.H8Debug.LogException(exception, this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\VehicleMotor.cs:1597:                Hecton8.Core.H8Debug.LogError("VehicleMotor vault DTO layout drift detected.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:62:                Hecton8.Core.H8Debug.LogError("[BallisticsLayoutVerifier] Ballistic DTO layout mismatch. SHINOBU_127 cannot be trusted until offsets match the XML contract.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:67:                Hecton8.Core.H8Debug.Log("[BallisticsLayoutVerifier] BallisticTrajectoryDTO=64B, AABBPrimitiveDTO=96B, BallisticHitResultDTO=112B, ImpactVfx=80B, Tuning/Telemetry/Counters=64B.");
- Additional lines omitted here: 56. Use `../_scans/07_gameplay_construction_tools_inventory_combat_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (40)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:103:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRig.cs:3210:                array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:67:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:102:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\HectonSubmarineOS.cs:240:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\PlayerSignalEvents.cs:111:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:75:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:56:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:221:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:891:                        State = new NativeArray<PlayerKinematicState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:896:                        Sphere = new NativeArray<PlayerBoundingSphere>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:901:                        HandHistory = new NativeArray<SomaticHandStrokeSample>(HandHistoryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:906:                        Tuning = new NativeArray<SomaticKinematicsTuningData>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:911:                        DragLut = new NativeArray<float>(DragLutCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:916:                        SignalScratch = new NativeArray<SomaticKinematicSignalScratch>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:921:                        BlackBox = new NativeArray<SomaticKinematicBlackBoxEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:926:                        BlackBoxCursor = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1189:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1626:            NativeArray<SomaticComfortStateDTO> stateBuffer = new NativeArray<SomaticComfortStateDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1630:            NativeArray<SomaticDerivativeDTO> derivativeBuffer = new NativeArray<SomaticDerivativeDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1634:            NativeArray<VrComfortProfileDTO> profileBuffer = new NativeArray<VrComfortProfileDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VehicleCommandSignals.cs:55:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SuitMeshUpdateEvents.cs:43:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRuntime.cs:1886:                    array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRuntime.cs:2793:                array = new NativeArray<T>(length, allocator, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:140:                    JobInputs = new NativeArray<ExtractorJobInput>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:142:                    JobResults = new NativeArray<ExtractorJobResult>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:144:                    CycleTimers = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:146:                    BufferedItemHashIds = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:148:                    BufferedUnitCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:150:                    CompletedCycleCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\WfcLaserCutRuntime.cs:623:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\ToolKinematics\ToolKinematicsRuntime.cs:957:            NativeArray<byte> bytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Interaction\InteractionEvents.cs:69:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1056:                    Requests = new NativeArray<ScavengingHarvestRequestDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1058:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1061:                    ResolvedYields = new NativeArray<ScavengingResolvedYieldDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1063:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1066:                    TelemetryRing = new NativeArray<ScavengingTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1068:                        Allocator.Persistent,

## REVIEW_CACHE_OR_INJECTION_REQUIRED (19)

- Unity scene lookup | Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1712:            root.GetComponentsInChildren<MeshFilter>(true, _meshFilterScratch);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1731:            root.GetComponentsInChildren<Collider>(true, _colliderScratch);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:146:            GetComponentsInChildren<Renderer>(true, _cachedRenderers);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:148:            GetComponentsInChildren<Collider>(true, _cachedColliders);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\LifePodTactilePrologueController.cs:449:                    GetComponentsInChildren<LifePodSeatStrapLatch>(true, _seatStrapLatches);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\MountablePlayerTransport.cs:1587:            if (!TryGetComponent<SubmarineCoreDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1067:            TryGetComponent<IPlayerKinematicsMovementRuntime>(out _movement);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1068:            TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1575:                    TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1733:                TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1743:            prefab.GetComponentsInChildren(true, _meteorSplashValidationScratch);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\SubmarineCoreDirector.cs:397:                !TryGetComponent<SubmarineAutoLevelBallastController>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:23:            if (!runtimeRoot.TryGetComponent<ScrapManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:26:            if (!runtimeRoot.TryGetComponent<ResourceScarcityDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:29:            if (!runtimeRoot.TryGetComponent<TradeMarauderDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:32:            if (!runtimeRoot.TryGetComponent<Hecton8.World.EnvironmentalStrainManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Interaction\InteractableRegistry.cs:228:            owner.GetComponentsInChildren(true, s_invalidationColliders);
- Unity scene lookup | Assets\_Project\Scripts\Interaction\InteractableRegistry.cs:243:            owner.GetComponentsInChildren(true, s_invalidationColliders);
- Unity scene lookup | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1788:                    candidate.GetComponent<ScavengingLootOracleRuntime>() != null)

## REVIEW_UNCLASSIFIED_STATIC_RISK (17)

- Uncategorized | Assets\_Project\Scripts\Gameplay\SolarPanel.cs:527:                s_gizmoLabelContent.text = ResolveEditorGizmoLabel(watts, depth, angle, shadow);
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:244:                _telemetryStateLabel.text = "Telemetry: latest solved frame.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:255:                _telemetryStateLabel.text = "Telemetry: no solved frame yet.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:235:                _state.text = "Telemetry: runtime not initialized.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:239:            _state.text = (entry.Flags & 0x3u) != 0u ? "Telemetry: fault flag present." : "Telemetry: latest armor solve.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:211:                _state.text = "Telemetry: runtime not initialized.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:215:            _state.text = _lastTelemetry.AnomalyHash != 0u ? "Telemetry: anomaly present." : "Telemetry: latest status solve.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2439:            _layoutLabel.text = valid
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2455:            _auditLabel.text = applied
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2467:                _auditLabel.text = "Audit unavailable: Vault not created.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2475:            _auditLabel.text = $"10k: {c0}/{c1}/{c2}/{c3}";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2490:                _auditLabel.text = "CSV ingest failed: invalid byte length.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2513:                    _auditLabel.text = "CSV ingest failed: incomplete file read.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2517:                _auditLabel.text = ScavengingLootOracleRuntime.TryIngestLootDistributionCsvBytes(nativeBytes, out int entryCount)
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:84:                _statusLabel.text = "Runtime: offline";
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:88:            _statusLabel.text = "Runtime: online";
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:89:            _telemetryLabel.text =

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (12)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\RadiationHazardGrid.cs:2367:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:2488:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3074:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3136:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.cs:3080:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\LaserCutterDodRuntime.cs:1077:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Shinobu19EconomyLedger.cs:1585:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Shinobu19EconomyLedger.cs:1624:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Routing\InventoryRoutingNetwork.cs:1081:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2494:            using (NativeArray<byte> nativeBytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Power\SubmarineOsThermalGridRuntime.cs:1654:            NativeArray<byte> payload = new NativeArray<byte>((int)totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Power\ShinobuLogisticsRouter.cs:1747:                NativeArray<byte> payload = new NativeArray<byte>((int)totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## REVIEW_LOG_GUARD_REQUIRED (3)

- Runtime debug logging | Assets\_Project\Scripts\Interaction\PlayerInteraction.cs:38://     Debug.LogError only if Instance still null at Start.
- Runtime debug logging | Assets\_Project\Scripts\Interaction\PlayerInteraction.cs:216:        //   Debug.LogError ONLY if Instance still null.
- Runtime debug logging | Assets\_Project\Scripts\Power\LogisticsNetworkGraph.cs:301:                Debug.LogError("LogisticsNetworkGraph layout fault.");

## LIKELY_LEGAL_COLD_LOOKUP (3)

- Unity scene lookup | Assets\_Project\Scripts\Gameplay\SubmarineCompoundColliderAuthoring.cs:316:            generatedRoot.GetComponentsInChildren(true, _compoundColliderCache);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:757:                Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
- Unity scene lookup | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1782:            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>(); // COLD ALLOC: reload cleanup scan for HideAndDontSave orphan hosts.

## LIKELY_LEGAL_COLD_OR_PRESENTATION_PATH (2)

- Coroutine / managed timing | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:314:                "yield return new " + "WaitForSeconds",
- Coroutine / managed timing | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:389:            json.Append("float>\", \"yield return new Wait");

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Scavenging\ResourceNodeTemplate.cs:275:        [Tooltip("Primitive collider family used by runtime nodes. MeshCollider is forbidden.")]
