# Signal Architecture Optimization Report X_001

Evidence class: STATIC_SOURCE_ROSLYN_AST
Runtime proof: False
Canonical hash: 829fa28be83fd53c0cbfc0b391212be841d239a4cfd59682c918353483634df2

## Counts

- Files scanned: 2380
- Parse failures: 0
- Signal payload definitions: 401
- Payloads inside GlobalSignals.cs: 153
- Hard payload violations: 0
- GlobalSignals call sites: 266
- GlobalSignals publish sites: 0
- GlobalSignals NativeQueue fields: 74
- FlushDirectSignalLane invocations: 141
- Signal lanes in ledger: 288

## Top Hotspots

- Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs | calls=13 publish=0 consume=0 read=1
- Assets/_Project/Scripts/Fauna/FaunaBrain.cs | calls=6 publish=0 consume=0 read=4
- Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs | calls=5 publish=0 consume=0 read=3
- Assets/_Project/Scripts/Construction/DroneFleetManager.cs | calls=4 publish=0 consume=0 read=3
- Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs | calls=3 publish=0 consume=0 read=2
- Assets/_Project/Scripts/ConstructionManager.cs | calls=3 publish=0 consume=0 read=3
- Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs | calls=3 publish=0 consume=0 read=3
- Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs | calls=3 publish=0 consume=0 read=1
- Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs | calls=3 publish=0 consume=0 read=0
- Assets/_Project/Scripts/World/PersistentWorldRegistry.cs | calls=3 publish=0 consume=0 read=3
- Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/BeaconNetworkSystem.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/BeaconRuntime.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs | calls=2 publish=0 consume=0 read=0
- Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/Core/SystemDispatcher.cs | calls=2 publish=0 consume=0 read=0
- Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs | calls=2 publish=0 consume=0 read=2
- Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs | calls=2 publish=0 consume=0 read=2

## Payload Violations

- WARN SIGNAL_LAYOUT_UNDECLARED Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1101 EntityAliveMaskSignalFilter | No StructLayout attribute found.
- INFO SIGNAL_LAYOUT_UNDECLARED Assets/_Project/Scripts/FaunaDirector.cs:211 AcousticPanicCommand | No StructLayout attribute found.
- WARN SIGNAL_LAYOUT_UNDECLARED Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1861 RespawnSignalResolvedTargetTransformer | No StructLayout attribute found.
- INFO SIGNAL_LAYOUT_UNDECLARED Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:34 InstanceCullingCameraPositionSignal | No StructLayout attribute found.
- INFO SIGNAL_LAYOUT_UNDECLARED Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:45 InstanceCullingCameraFrustumSignal | No StructLayout attribute found.

## Legacy Publish Sites


## Lane Capacity And Overflow Ledger

- AcousticPingSignal | configure=2 | maxFrame=128,64 | lowTier=16,8 | legacyPublish=0 | typedPublish=36 | coalescing=Coalesces by channel and AUP meter cell; acoustic energy is merged in native snapshot memory.
- AcousticZoneChangedEvent | configure=1 | maxFrame=8 | lowTier=AcousticZoneChangedSignalCapacity | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AnomalyProximitySignal | configure=2 | maxFrame=16 | lowTier=4 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AtmosphericReentrySignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AudioEvent | configure=1 | maxFrame=16 | lowTier=16 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AupPreShiftSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AupShiftSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BaseModuleCompromisedSignal | configure=2 | maxFrame=DefaultMaxFrameSignals,64 | lowTier=DefaultSurvivalFrameSignals,16 | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BaseStructuralWarningSignal | configure=2 | maxFrame=64,BaseStructuralWarningConstants.MaxFrameSignals | lowTier=8,BaseStructuralWarningConstants.LowTierFrameSignals | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BatteryLevelSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BiomeChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BiomeGradientSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BrownoutSignal | configure=1 | maxFrame=BrownoutSignalCapacity | lowTier=16 | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BubbleSpawnSignal | configure=2 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraFrustumSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraJuiceImpactSignal | configure=2 | maxFrame=ImpactSignalCapacity,128 | lowTier=LowTierImpactSignalCapacity,32 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraPositionSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ChunkDehydratedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CombatDamageSignal | configure=1 | maxFrame=128 | lowTier=16 | legacyPublish=0 | typedPublish=17 | coalescing=Coalesces by TargetHash + DamageType + Channel inside the native frame snapshot; magnitude and integrity delta accumulate, flags OR, first nonzero source is retained.
- CompassCalibratedSignal | configure=2 | maxFrame=8 | lowTier=2 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CpuStarvationSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CraftingCompletedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CullingOverloadSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DataVaultUpdateSignal | configure=1 | maxFrame=DataVaultUpdateSignalCapacity | lowTier=16 | legacyPublish=0 | typedPublish=4 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DebrisSpawnSignal | configure=1 | maxFrame=DebrisSpawnSignalCapacity | lowTier=16 | legacyPublish=0 | typedPublish=32 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DebugSignal | configure=1 | maxFrame=64 | lowTier=8 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DeferredSubmarineImpactSignal | configure=1 | maxFrame=DeferredSubmarineImpactSignalCapacity | lowTier=DeferredSubmarineImpactSurvivalSignalCapacity | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DesyncDetectedSignal | configure=1 | maxFrame=DeterminismDesyncDetectedSignalCapacity | lowTier=DeterminismDesyncDetectedSignalCapacity | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DiegeticHudSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DirectorAIMusicSignal | configure=1 | maxFrame=DirectorAIMusicSignalCapacity | lowTier=8 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DockingCompleteSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DockingFailedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DockingRequestSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DropPodLandedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- DynamicMusicScalarSignal | configure=4 | maxFrame=64 | lowTier=64,8 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- EntityDeathSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=4 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- EntitySpawnSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- FaunaStateChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=6 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- FluidImpulseSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=10 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- FramePacingWarningSignal | configure=1 | maxFrame=16 | lowTier=4 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- FrameTimeSignal | configure=1 | maxFrame=64 | lowTier=16 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HUDNotificationSignal | configure=1 | maxFrame=HUDNotificationSignalCapacity | lowTier=64 | legacyPublish=0 | typedPublish=13 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HapticPulseSignal | configure=3 | maxFrame=HapticPulseSignalCapacity,256 | lowTier=1,8 | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HapticRequest | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=17 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HighSpeedImpactSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=4 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HullDeformedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=4 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- HullRepairedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ImpactSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=19 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InputSignal | configure=1 | maxFrame=DeterminismInputSignalCapacity | lowTier=DeterminismInputSignalCapacity | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InputStateSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InventoryChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InventoryCommandSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InventoryDeathLootCacheSignal | configure=1 | maxFrame=InventoryDeathLootCacheSignal.MaxFrameSignals | lowTier=InventoryDeathLootCacheSignal.LowTierFrameSignals | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InventoryRespawnDeathAupSignal | configure=2 | maxFrame=InventoryRespawnDeathAupSignal.MaxFrameSignals | lowTier=InventoryRespawnDeathAupSignal.LowTierFrameSignals | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- InventoryRespawnPenaltyResultSignal | configure=2 | maxFrame=InventoryRespawnPenaltyResultSignal.MaxFrameSignals | lowTier=InventoryRespawnPenaltyResultSignal.LowTierFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ItemAcquiredSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=12 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ItemDurabilityChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- KccVelocitySignal | configure=1 | maxFrame=DeterminismKccVelocitySignalCapacity | lowTier=DeterminismKccVelocitySignalCapacity | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- KillSwitchSignal | configure=1 | maxFrame=32 | lowTier=8 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- LaserCutterEventPayload | configure=1 | maxFrame=LaserCutterEventSignalCapacity | lowTier=LaserCutterEventSignalCapacity | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- LockstepSnapshotSignal | configure=1 | maxFrame=16 | lowTier=16 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- LoreFragmentScannedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MacroCollisionSignal | configure=1 | maxFrame=32 | lowTier=8 | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MacroDatabaseSectorHydrationSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ManualOverridePulledSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MemoryAddressShiftSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MemoryPressureSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MockPlayerFootstepSignal | configure=1 | maxFrame=32 | lowTier=8 | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MockRockCollisionSignal | configure=1 | maxFrame=128 | lowTier=16 | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- MovementAcousticSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PdaExchangeStateChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PhysicsEventPayload | configure=1 | maxFrame=PhysicsEventPayloadSignalCapacity | lowTier=PhysicsEventPayloadSurvivalSignalCapacity | legacyPublish=0 | typedPublish=4 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PhysiologyStateSignal | configure=2 | maxFrame=PhysiologyStateSignalCapacity,64 | lowTier=32 | legacyPublish=0 | typedPublish=9 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerActionCancelledSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerActionCompletedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerActionProgressSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerBaseEnterSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerBaseExitSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerExhaleSignal | configure=1 | maxFrame=32 | lowTier=8 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- PlayerFatalPressureSignal | configure=1 | maxFrame=16 | lowTier=4 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
