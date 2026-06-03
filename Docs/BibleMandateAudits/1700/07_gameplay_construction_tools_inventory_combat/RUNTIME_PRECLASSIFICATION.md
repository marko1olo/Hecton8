# Runtime Preclassification - Gameplay, Tools, Construction, Inventory, Combat, Economy

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Raw generated runtime suspects: 198. Line-level reconciliation: 193 classified lines in `LINE_LEVEL_CLASSIFICATION.md`.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 99
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 40
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 19
- REVIEW_UNCLASSIFIED_STATIC_RISK: 17
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 12
- REVIEW_LOG_GUARD_REQUIRED: 5
- LIKELY_LEGAL_COLD_LOOKUP: 3
- LIKELY_LEGAL_COLD_OR_PRESENTATION_PATH: 2
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (99)

- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:284:            Hecton8.Core.H8Debug.LogError($"[BaseAirlockEvents] {ownerName} was destroyed while still registered as an IBaseAirlockEventListener.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BeaconRegistry.cs:182:            Hecton8.Core.H8Debug.LogWarning("[BeaconRegistry] Fixed active beacon capacity exceeded.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BatteryCharger.cs:873:            Hecton8.Core.H8Debug.LogError("BatteryCharger bridge rollback failed; Inventory-owner reservation route is required for a hard conservation proof.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:850:                Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\BaseAirlock.cs:862:                Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:62:                Hecton8.Core.H8Debug.LogError("[BallisticsLayoutVerifier] Ballistic DTO layout mismatch. SHINOBU_127 cannot be trusted until offsets match the XML contract.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:67:                Hecton8.Core.H8Debug.Log("[BallisticsLayoutVerifier] BallisticTrajectoryDTO=64B, AABBPrimitiveDTO=96B, BallisticHitResultDTO=112B, ImpactVfx=80B, Tuning/Telemetry/Counters=64B.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:55:                Hecton8.Core.H8Debug.LogError("[ArmorPenetrationLayoutVerifier] Armor LUT DTO layout mismatch. SHINOBU_318 output rejected until fixed.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:60:                Hecton8.Core.H8Debug.Log("[ArmorPenetrationLayoutVerifier] ArmorProfileDTO=64B with material-row x angle-step 8x6 LUT at offset 16; ShinobuArmorPenetrationTable=64B; resolved hit=128B; telemetry=64B; debug hit=96B.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:208:                Hecton8.Core.H8Debug.LogWarning("[ArmorPenetrationTorture] Runtime not ready; register at least one combat target before running 10k LUT torture.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:212:            Hecton8.Core.H8Debug.Log($"[ArmorPenetrationTorture] impacts={entry.ImpactCount} weak={entry.WeakPointHits} deflect={entry.DeflectCount} solveUs={entry.SolveMicroseconds} flags=0x{entry.Flags:X}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:219:                Hecton8.Core.H8Debug.LogWarning($"[ArmorPenetrationCasTorture] FAILED successes={successes}/100 finalHealth={finalHealth}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:223:            Hecton8.Core.H8Debug.Log($"[ArmorPenetrationCasTorture] PASS successes={successes}/100 finalHealth={finalHealth}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:413:                Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:443:                Hecton8.Core.H8Debug.Log("[ArmorPenetrationBatchProofRunner] PASS. Wrote " + ReportPath);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:445:                Hecton8.Core.H8Debug.LogError("[ArmorPenetrationBatchProofRunner] FAILED: " + failure + " Wrote " + ReportPath);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:655:            Hecton8.Core.H8Debug.Log($"[OOP_Hitbox_Scanner] Wrote {ReportPath}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HazardZoneManager.cs:2670:            Hecton8.Core.H8Debug.LogWarning(OverflowLogText);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:562:            H8Debug.Log("[Eclipse] Night predators rising.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:728:            H8Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:736:            H8Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:377:                    Hecton8.Core.H8Debug.LogWarning("[HarvestablePlant] ObjectPoolManager unavailable. Loot spawn skipped to avoid runtime Instantiate.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\HarvestablePlant.cs:740:                    Hecton8.Core.H8Debug.LogWarning($"[HarvestablePlant] Segment {i} has no mesh renderer assigned.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:53:                Hecton8.Core.H8Debug.LogError("[StatusEffectLayoutVerifier] Status FSM DTO layout mismatch. SHINOBU_319 output rejected until fixed.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:58:                Hecton8.Core.H8Debug.Log("[StatusEffectLayoutVerifier] StatusEffectState=64B; StatusEffectMask offset=0; timers at 8/24; telemetry/counter/vfx/damage lanes=64B.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:329:            Hecton8.Core.H8Debug.Log($"[OOP_Buff_Scanner] Wrote {ReportPath} key={SharedReportKey}; findings={findings}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:130:                H8Debug.Log($"[DirectorBridge] Mission triggered: {missionId} near {position}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\DirectorMissionBridge.cs:233:                H8Debug.Log($"[DirectorBridge] Profile mission triggered: {missionId} near {position}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:443:            Hecton8.Core.H8Debug.LogException(exception);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1294:            Hecton8.Core.H8Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1302:            H8Debug.Log($"[Ending] Choice executed: {choice}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:1310:            H8Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\Floater.cs:617:                Hecton8.Core.H8Debug.LogWarning("[Floater] Cannot attach to object without Rigidbody.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1169:            H8Debug.Log($"[PlayerExpression] Active profile: {profileId} ({displayName})");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:1180:            H8Debug.Log($"[PlayerExpression] Suit applied: {profileId} -> {suitName}");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:345:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Ending already complete.");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\EndingTerminalInteractable.cs:351:            Hecton8.Core.H8Debug.Log("[EndingTerminal] Choice UI opened. " +
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\OxygenPlant.cs:170:                    Hecton8.Core.H8Debug.LogWarning("[OxygenPlant] ObjectPoolManager unavailable. Bubble release skipped to avoid runtime Instantiate.", this);
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1333:            H8Debug.Log("[RandomEvent] Started");
- Runtime debug logging | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1342:            H8Debug.Log("[RandomEvent] Ended");
- Additional lines omitted here: 59. Use `../_scans/07_gameplay_construction_tools_inventory_combat_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (40)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\BaseAirlockEvents.cs:103:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRuntime.cs:1886:                    array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRuntime.cs:2793:                array = new NativeArray<T>(length, allocator, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\ContextualPhysicalIkRig.cs:3210:                array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\EclipseGameplaySystem.cs:75:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\EndingSystem.cs:102:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\PlayerSignalEvents.cs:111:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\PlayerExpressionManager.cs:56:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\HectonSubmarineOS.cs:240:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:221:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:891:                        State = new NativeArray<PlayerKinematicState>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:896:                        Sphere = new NativeArray<PlayerBoundingSphere>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:901:                        HandHistory = new NativeArray<SomaticHandStrokeSample>(HandHistoryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:906:                        Tuning = new NativeArray<SomaticKinematicsTuningData>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:911:                        DragLut = new NativeArray<float>(DragLutCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:916:                        SignalScratch = new NativeArray<SomaticKinematicSignalScratch>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:921:                        BlackBox = new NativeArray<SomaticKinematicBlackBoxEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:926:                        BlackBoxCursor = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SuitMeshUpdateEvents.cs:43:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\FirstHourDirector.cs:67:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VehicleCommandSignals.cs:55:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1189:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1626:            NativeArray<SomaticComfortStateDTO> stateBuffer = new NativeArray<SomaticComfortStateDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1630:            NativeArray<SomaticDerivativeDTO> derivativeBuffer = new NativeArray<SomaticDerivativeDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.Comfort.cs:1634:            NativeArray<VrComfortProfileDTO> profileBuffer = new NativeArray<VrComfortProfileDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:144:                    JobInputs = new NativeArray<ExtractorJobInput>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:146:                    JobResults = new NativeArray<ExtractorJobResult>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:148:                    CycleTimers = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:150:                    BufferedItemHashIds = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:152:                    BufferedUnitCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Construction\AutonomousExtractorSystem.cs:154:                    CompletedCycleCounts = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\ToolKinematics\ToolKinematicsRuntime.cs:1031:            NativeArray<byte> bytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\WfcLaserCutRuntime.cs:623:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Interaction\InteractionEvents.cs:69:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1057:                    Requests = new NativeArray<ScavengingHarvestRequestDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1059:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1062:                    ResolvedYields = new NativeArray<ScavengingResolvedYieldDTO>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1064:                        Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1067:                    TelemetryRing = new NativeArray<ScavengingTelemetryEntry>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1069:                        Allocator.Persistent,

## REVIEW_CACHE_OR_INJECTION_REQUIRED (19)

- Unity scene lookup | Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1712:            root.GetComponentsInChildren<MeshFilter>(true, _meshFilterScratch);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\DebrisManager.cs:1731:            root.GetComponentsInChildren<Collider>(true, _colliderScratch);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:146:            GetComponentsInChildren<Renderer>(true, _cachedRenderers);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\HarvestableOutcrop.cs:148:            GetComponentsInChildren<Collider>(true, _cachedColliders);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1067:            TryGetComponent<IPlayerKinematicsMovementRuntime>(out _movement);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1068:            TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1575:                    TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs:1733:                TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _motor);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\LifePodTactilePrologueController.cs:449:                    GetComponentsInChildren<LifePodSeatStrapLatch>(true, _seatStrapLatches);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\MountablePlayerTransport.cs:1587:            if (!TryGetComponent<SubmarineCoreDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\RandomEventSystem.cs:1743:            prefab.GetComponentsInChildren(true, _meteorSplashValidationScratch);
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:23:            if (!runtimeRoot.TryGetComponent<ScrapManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:26:            if (!runtimeRoot.TryGetComponent<ResourceScarcityDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:29:            if (!runtimeRoot.TryGetComponent<TradeMarauderDirector>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Economy\EconomyRuntimeInstaller.cs:32:            if (!runtimeRoot.TryGetComponent<Hecton8.World.EnvironmentalStrainManager>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Interaction\InteractableRegistry.cs:228:            owner.GetComponentsInChildren(true, s_invalidationColliders);
- Unity scene lookup | Assets\_Project\Scripts\Interaction\InteractableRegistry.cs:243:            owner.GetComponentsInChildren(true, s_invalidationColliders);
- Unity scene lookup | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1797:                    candidate.GetComponent<ScavengingLootOracleRuntime>() != null)
- Unity scene lookup | Assets\_Project\Scripts\Scavenging\ResourceNodeTemplate.cs:949:            if (runtimeNodePrefab != null && runtimeNodePrefab.GetComponent<ResourceNode>() == null)

## REVIEW_UNCLASSIFIED_STATIC_RISK (17)

- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:244:                _telemetryStateLabel.text = "Telemetry: latest solved frame.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\BallisticsEditorFacade.cs:255:                _telemetryStateLabel.text = "Telemetry: no solved frame yet.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:235:                _state.text = "Telemetry: runtime not initialized.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:239:            _state.text = (entry.Flags & 0x3u) != 0u ? "Telemetry: fault flag present." : "Telemetry: latest armor solve.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:211:                _state.text = "Telemetry: runtime not initialized.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:215:            _state.text = _lastTelemetry.AnomalyHash != 0u ? "Telemetry: anomaly present." : "Telemetry: latest status solve.";
- Uncategorized | Assets\_Project\Scripts\Gameplay\SolarPanel.cs:527:                s_gizmoLabelContent.text = ResolveEditorGizmoLabel(watts, depth, angle, shadow);
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2461:            _layoutLabel.text = valid
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2477:            _auditLabel.text = applied
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2489:                _auditLabel.text = "Audit unavailable: Vault not created.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2497:            _auditLabel.text = $"10k: {c0}/{c1}/{c2}/{c3}";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2512:                _auditLabel.text = "CSV ingest failed: invalid byte length.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2535:                    _auditLabel.text = "CSV ingest failed: incomplete file read.";
- Uncategorized | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2539:                _auditLabel.text = ScavengingLootOracleRuntime.TryIngestLootDistributionCsvBytes(nativeBytes, out int entryCount)
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:84:                _statusLabel.text = "Runtime: offline";
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:88:            _statusLabel.text = "Runtime: online";
- Uncategorized | Assets\_Project\Scripts\Power\SolverConvergenceXRayWindow.cs:89:            _telemetryLabel.text =

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (12)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\RadiationHazardGrid.cs:2367:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs:2488:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3106:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs:3168:                payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Gameplay\VRSomaticProvider.cs:3080:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Tools\LaserCutterDodRuntime.cs:1077:                NativeArray<byte> payload = new NativeArray<byte>(totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Routing\InventoryRoutingNetwork.cs:1081:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Shinobu19EconomyLedger.cs:1585:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Inventory\Shinobu19EconomyLedger.cs:1624:            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:2516:            using (NativeArray<byte> nativeBytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Power\ShinobuLogisticsRouter.cs:1747:                NativeArray<byte> payload = new NativeArray<byte>((int)totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Power\SubmarineOsThermalGridRuntime.cs:1693:            NativeArray<byte> payload = new NativeArray<byte>((int)totalBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## REVIEW_LOG_GUARD_REQUIRED (5)

- Runtime debug logging | Assets\_Project\Scripts\Construction\HectonBlueprintPreviewBatch.cs:1163:                Debug.LogError("[HectonBlueprintPreviewBatch] Missing authored preview material. Runtime material synthesis is forbidden.", this);
- Runtime debug logging | Assets\_Project\Scripts\Construction\VRPipeBlueprintPreview.cs:807:                Debug.LogError("[VRPipeBlueprintPreview] Missing authored preview material. Runtime material synthesis is forbidden.", this);
- Runtime debug logging | Assets\_Project\Scripts\Interaction\PlayerInteraction.cs:38://     Debug.LogError only if Instance still null at Start.
- Runtime debug logging | Assets\_Project\Scripts\Interaction\PlayerInteraction.cs:216:        //   Debug.LogError ONLY if Instance still null.
- Runtime debug logging | Assets\_Project\Scripts\Power\LogisticsNetworkGraph.cs:301:                Debug.LogError("LogisticsNetworkGraph layout fault.");

## LIKELY_LEGAL_COLD_LOOKUP (3)

- Unity scene lookup | Assets\_Project\Scripts\Gameplay\Combat\ArmorPenetrationEditorFacade.cs:757:                Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
- Unity scene lookup | Assets\_Project\Scripts\Gameplay\SubmarineCompoundColliderAuthoring.cs:316:            generatedRoot.GetComponentsInChildren(true, _compoundColliderCache);
- Unity scene lookup | Assets\_Project\Scripts\Scavenging\ScavengingLootOracle.cs:1791:            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>(); // COLD ALLOC: reload cleanup scan for HideAndDontSave orphan hosts.

## LIKELY_LEGAL_COLD_OR_PRESENTATION_PATH (2)

- Coroutine / managed timing | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:314:                "yield return new " + "WaitForSeconds",
- Coroutine / managed timing | Assets\_Project\Scripts\Gameplay\Combat\StatusEffectsEditorFacade.cs:389:            json.Append("float>\", \"yield return new Wait");

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Scavenging\ResourceNodeTemplate.cs:279:        [Tooltip("Primitive collider family used by runtime nodes. MeshCollider is forbidden.")]

