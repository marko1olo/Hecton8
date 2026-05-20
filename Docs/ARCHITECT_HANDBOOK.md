# ARCHITECT HANDBOOK
Date: 2026-05-17
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
Generated from Core/Contracts. Edit constants in C# contracts, then regenerate this file.

| Contract | Constant | Type | Value |
| --- | --- | --- | --- |
| AcousticAup | CellSizeMeters | int | `HectonPhysicsContract.AupSectorSizeMetersInt` |
| ResolutionScaleStateFlags | LowTierEmergency | byte | `1 << 0` |
| ResolutionScaleStateFlags | FramePressure | byte | `1 << 1` |
| ResolutionScaleStateFlags | ThermalPressure | byte | `1 << 2` |
| ResolutionScaleStateFlags | AupLocked | byte | `1 << 3` |
| ResolutionScaleStateFlags | InvalidStateRecovered | byte | `1 << 4` |
| HectonContractVersion | HashLo | ulong | `<computed in static constructor>` |
| HectonContractVersion | HashHi | ulong | `<computed in static constructor>` |
| HectonDataSovereigntyContract | DataOwnerGlobalDataVault | byte | `1` |
| HectonDataSovereigntyContract | DataOwnerSignalBusTypedLane | byte | `2` |
| HectonDataSovereigntyContract | DataOwnerReadOnlySpanView | byte | `3` |
| HectonDataSovereigntyContract | LocalNativeArrayOwnershipForbidden | byte | `1` |
| HectonDataSovereigntyContract | SystemIdContracts | uint | `0x43545243u` |
| HectonDataSovereigntyContract | SystemIdGlobalDataVault | uint | `0x47445654u` |
| HectonDataSovereigntyContract | SystemIdSignalBus | uint | `0x53474C4Eu` |
| HectonDataSovereigntyContract | SystemIdMmfPaging | uint | `0x4D4D4650u` |
| HectonDataSovereigntyContract | SystemIdBlackBox | uint | `0x42424F58u` |
| HectonDataSovereigntyContract | VaultOverrideFloatStrideBytes | int | `4` |
| HectonDataSovereigntyContract | VaultOverrideDoubleStrideBytes | int | `8` |
| HectonDataSovereigntyContract | VaultOverrideUlongStrideBytes | int | `8` |
| HectonDataSovereigntyContract | VaultOverrideMaxContractEntries | int | `512` |
| HectonDataSovereigntyContract | TypedSignalLaneMaxCount | int | `255` |
| HectonDataSovereigntyContract | ReadOnlySpanMinBridgeBytes | int | `16` |
| HectonDataSovereigntyContract | BlackBoxFrameCapacity | int | `HectonPlatformContract.ContractBlackBoxFrameCapacity` |
| HectonDataSovereigntyContract | BlackBoxEntryBytes | int | `HectonPlatformContract.ContractHeartbeatStrideBytes` |
| HectonEcologyContract | PopulationTelemetryCapacity | int | `300` |
| HectonEcologyContract | PopulationCounterCapacity | int | `16` |
| HectonEcologyContract | PopulationCoefficientCapacity | int | `1` |
| HectonEcologyContract | DefaultMaxEntities | int | `8192` |
| HectonEcologyContract | DefaultMaxSectors | int | `256` |
| HectonEcologyContract | EntityDeathSignalLaneCapacity | int | `64` |
| HectonEcologyContract | DefaultCullEventCapacity | int | `EntityDeathSignalLaneCapacity` |
| HectonEcologyContract | DefaultFreeRingCapacity | int | `1024` |
| HectonEcologyContract | MaxCoefficientJsonBytes | int | `16 * 1024` |
| HectonEcologyContract | CoefficientFileReadBufferBytes | int | `2048` |
| HectonEcologyContract | ColdTickDeltaSeconds | float | `1f` |
| HectonEcologyContract | DefaultBiomassPerEntity | float | `128f` |
| HectonEcologyContract | DefaultMaxActivePreyPerSector | int | `64` |
| HectonEcologyContract | StressCullThreshold01 | float | `0.8f` |
| HectonEcologyContract | DefaultStressCullFraction01 | float | `0.25f` |
| HectonEcologyContract | LotkaBirthRate | float | `0.03f` |
| HectonEcologyContract | LotkaDeathRate | float | `0.018f` |
| HectonEcologyContract | LotkaDeltaTimeSeconds | float | `HectonPhysicsContract.FixedDeltaTimeSeconds` |
| HectonEcologyContract | LotkaFeedRate | float | `0.000006f` |
| HectonEcologyContract | LotkaPredatorConversion | float | `0.35f` |
| HectonEcologyContract | LotkaPreyCarryingCapacity | float | `10000f` |
| HectonEcologyContract | LotkaStablePredatorBiomass | float | `714.2857f` |
| HectonEcologyContract | LotkaStablePreyBiomass | float | `8571.4287f` |
| HectonEcologyContract | LotkaObservedPredatorMax | float | `714.2857f` |
| HectonEcologyContract | LotkaObservedPreyMax | float | `9020.165f` |
| HectonEcologyContract | LotkaIntegrationSteps | int | `1000000` |
| HectonEcologyContract | WorldPreyBirthRatePerSecond | float | `0.012f` |
| HectonEcologyContract | WorldPredationRatePerSecond | float | `0.00045f` |
| HectonEcologyContract | WorldPredatorGrowthRatePerSecond | float | `0.00014f` |
| HectonEcologyContract | WorldPredatorDeathRatePerSecond | float | `0.006f` |
| HectonEcologyContract | WorldReproductionFoodThreshold01 | float | `0.62f` |
| HectonEcologyContract | BiomassMacroCellSizeMeters | float | `50f` |
| HectonEcologyContract | ApexSpawnGateCacheCellSizeMeters | float | `10f` |
| HectonEcologyContract | MigrationCellSizeMeters | float | `100f` |
| HectonEcologyContract | SpawnReactivateDistanceLowMeters | float | `32f` |
| HectonEcologyContract | SpawnReactivateDistanceMiddleMeters | float | `64f` |
| HectonEcologyContract | SpawnReactivateDistanceHighMeters | float | `96f` |
| HectonEcologyContract | SpawnReactivateDistanceUltraMeters | float | `128f` |
| HectonEditorBreadcrumbContract | IconUnknown | ushort | `0` |
| HectonEditorBreadcrumbContract | IconPlayer | ushort | `1` |
| HectonEditorBreadcrumbContract | IconObjective | ushort | `2` |
| HectonEditorBreadcrumbContract | IconHazard | ushort | `3` |
| HectonEditorBreadcrumbContract | IconLore | ushort | `4` |
| HectonEditorBreadcrumbContract | IconResource | ushort | `5` |
| HectonEditorBreadcrumbContract | IconBase | ushort | `6` |
| HectonEditorBreadcrumbContract | IconVehicle | ushort | `7` |
| HectonEditorBreadcrumbContract | IconSignal | ushort | `8` |
| HectonEditorBreadcrumbContract | ColorUnknownRgba | uint | `0x9AA3ADFFu` |
| HectonEditorBreadcrumbContract | ColorPlayerRgba | uint | `0x4EC9FFFFu` |
| HectonEditorBreadcrumbContract | ColorObjectiveRgba | uint | `0xFFE066FFu` |
| HectonEditorBreadcrumbContract | ColorHazardRgba | uint | `0xFF4D4DFFu` |
| HectonEditorBreadcrumbContract | ColorLoreRgba | uint | `0xB58CFFFFu` |
| HectonEditorBreadcrumbContract | ColorResourceRgba | uint | `0x5DFFB1FFu` |
| HectonEditorBreadcrumbContract | ColorBaseRgba | uint | `0x7CD1FFFFu` |
| HectonEditorBreadcrumbContract | ColorVehicleRgba | uint | `0xC4D7E8FFu` |
| HectonEditorBreadcrumbContract | ColorSignalRgba | uint | `0xFFB86BFFu` |
| HectonEditorBreadcrumbContract | DefaultWorldMarkerRadiusMeters | float | `1.25f` |
| HectonEditorBreadcrumbContract | DefaultHudMarkerFadeInMeters | float | `16f` |
| HectonEditorBreadcrumbContract | DefaultHudMarkerFadeOutMeters | float | `128f` |
| HectonLoreContract | IndustrialShiftBoardA | uint | `0xeb76d1d6u` |
| HectonLoreContract | PumpStartCheck | uint | `0xc925ccafu` |
| HectonLoreContract | O2QuotaNotice | uint | `0x4a1945c4u` |
| HectonLoreContract | NightMaintenanceBrief | uint | `0xd4ae0066u` |
| HectonLoreContract | ChildDrawing | uint | `0xf9505818u` |
| HectonLoreContract | ChenMDatapad01 | uint | `0xf68e1cbfu` |
| HectonLoreContract | LiftCageDelay | uint | `0xf300640du` |
| HectonLoreContract | CurrentTurbineWarning | uint | `0x714c8efdu` |
| HectonLoreContract | FoodBrickComplaint | uint | `0xed174bc5u` |
| HectonLoreContract | ChenMDatapad02 | uint | `0xf78e1e52u` |
| HectonLoreContract | PumpTestRecord | uint | `0x7a58808cu` |
| HectonLoreContract | ScrubberFilterRot | uint | `0xef09eb22u` |
| HectonLoreContract | SalvageLedgerWeek31 | uint | `0x10f962a4u` |
| HectonLoreContract | BiologistSamples | uint | `0xa9a3a07fu` |
| HectonLoreContract | HallLeakTicket | uint | `0x368d5e59u` |
| HectonLoreContract | RelayNoiseReport | uint | `0xb98d5da6u` |
| HectonLoreContract | MedicDiary | uint | `0x30be8c1du` |
| HectonLoreContract | FloodDoorJam | uint | `0x0d332417u` |
| HectonLoreContract | ShiftRosterBRedline | uint | `0x101e1b1cu` |
| HectonLoreContract | SensorDriftNote | uint | `0x78186244u` |
| HectonLoreContract | ChenMBlueprint | uint | `0x110be7fdu` |
| HectonLoreContract | RelayCalibrationTape | uint | `0x73e281a2u` |
| HectonLoreContract | HullRibInspection | uint | `0x7be6057fu` |
| HectonLoreContract | EmergencyLightingOrder | uint | `0xf937ead8u` |
| HectonLoreContract | BrineSiphonTamper | uint | `0xd0114675u` |
| HectonLoreContract | ChildDrawingRecovery | uint | `0x773d6906u` |
| HectonLoreContract | O2QuotaLedger | uint | `0xb33ed0a9u` |
| HectonLoreContract | ServiceTunnelEcho | uint | `0x507547f5u` |
| HectonLoreContract | SecurityLockoutNotice | uint | `0x5e3e305eu` |
| HectonLoreContract | ChenMDatapad03 | uint | `0xf88e1fe5u` |
| HectonLoreContract | CaptainLastBroadcast | uint | `0x581103a8u` |
| HectonLoreContract | SealFailurePlacard | uint | `0xc75c7b37u` |
| HectonLoreContract | ReactorBaffleAlarm | uint | `0xe7b1798au` |
| HectonLoreContract | Atlas6TerminalSector3 | uint | `0x6f88a1c3u` |
| HectonLoreContract | EvacuationRouteCard | uint | `0x7666eaf3u` |
| HectonLoreContract | ForemanSealKitNote | uint | `0x1d927d51u` |
| HectonLoreContract | BlackoutStartLog | uint | `0xe9a15dd2u` |
| HectonLoreContract | PumpRoomBreach | uint | `0xf651aec5u` |
| HectonLoreContract | CoilGeneratorOverheat | uint | `0x585b7399u` |
| HectonLoreContract | AtlasHazardPlacard | uint | `0x73b8a497u` |
| HectonLoreContract | BlackBoxShiftB | uint | `0xbdc4a1e8u` |
| HectonLoreContract | DeadAirLocker | uint | `0x77061681u` |
| HectonLoreContract | GhostRelayPing | uint | `0xfb6e7073u` |
| HectonLoreContract | EmptyMedBay | uint | `0xad639378u` |
| HectonLoreContract | ScrubberBedAsh | uint | `0x116bd8e2u` |
| HectonLoreContract | CargoManifestEndline | uint | `0xa43a94c9u` |
| HectonLoreContract | RecoveryDroneAutopsy | uint | `0xe103fbe9u` |
| HectonLoreContract | ChenMSuit | uint | `0x08f52407u` |
| HectonLoreContract | FinalMaintenanceLedger | uint | `0x4b966021u` |
| HectonLoreContract | SurvivorRouteScratch | uint | `0x842c3decu` |
| HectonMmfPagingContract | BTreePageSizeBytes | int | `4096` |
| HectonMmfPagingContract | BTreePageAlignmentBytes | int | `64` |
| HectonMmfPagingContract | MacroDatabaseSectorSizeMeters | int | `512` |
| HectonMmfPagingContract | MacroDatabaseLowTierRadiusMeters | int | `1000` |
| HectonMmfPagingContract | MacroDatabaseMiddleTierRadiusMeters | int | `2000` |
| HectonMmfPagingContract | MacroDatabaseHighTierRadiusMeters | int | `3000` |
| HectonMmfPagingContract | MacroDatabaseUltraTierRadiusMeters | int | `4000` |
| HectonMmfPagingContract | MacroDatabaseDehydrateRadiusMeters | int | `3000` |
| HectonMmfPagingContract | MacroDatabaseMaxPayloadBytes | int | `256 * 1024` |
| HectonMmfPagingContract | MacroDatabaseNativeCacheCapacity | int | `2048` |
| HectonMmfPagingContract | MacroDatabaseMaxQuerySectors | int | `4096` |
| HectonMmfPagingContract | MacroDatabaseInitialFileBytes | long | `8L * 1024L * 1024L` |
| HectonMmfPagingContract | MacroDatabaseMaxFileBytes | long | `2L * 1024L * 1024L * 1024L` |
| HectonPhysicsContract | AupSectorSizeMetersDouble | double | `5000.0d` |
| HectonPhysicsContract | AupSectorSizeMetersInt | int | `(int)AupSectorSizeMetersDouble` |
| HectonPhysicsContract | AupSectorSizeMetersFloat | float | `(float)AupSectorSizeMetersDouble` |
| HectonPhysicsContract | WaterDensityKgPerCubicMeterConst | float | `1025f` |
| HectonPhysicsContract | GravityMetersPerSecondSquaredConst | float | `9.81f` |
| HectonPhysicsContract | HydrostaticPressureKPaPerMeter | float | `WaterDensityKgPerCubicMeterConst * GravityMetersPerSecondSquaredConst * 0.001f` |
| HectonPhysicsContract | FixedDeltaTimeSeconds | float | `0.020f` |
| HectonPhysicsContract | SoundSpeedWaterMetersPerSecondConst | float | `1480f` |
| HectonPhysicsContract | SoundSpeedAirMetersPerSecondConst | float | `343f` |
| HectonPhysicsContract | KinematicCcdSpeedGateMetersPerSecondSq | float | `25f` |
| HectonPhysicsContract | KinematicCcdRollbackFractionBias | float | `0.01f` |
| HectonPhysicsContract | KinematicCcdMinVectorMagnitudeSq | float | `0.000001f` |
| HectonPhysicsContract | KinematicCcdCornerNormalDotThreshold | float | `0.45f` |
| HectonPhysicsContract | MassiveLostKineticEnergyJoules | float | `1500f` |
| HectonPhysicsContract | FluidSqrtEpsilon | float | `0.000001f` |
| HectonPhysicsContract | FluidDistanceEpsilon | float | `0.0001f` |
| HectonPhysicsContract | FluidDischargeCoefficientMin | float | `0.05f` |
| HectonPhysicsContract | FluidMaximumIngressScaleMin | float | `0.01f` |
| HectonPhysicsContract | FluidCharacteristicHeightMinMeters | float | `0.1f` |
| HectonPhysicsContract | FluidMagnitudeMidAxisWeight | float | `0.375f` |
| HectonPhysicsContract | FluidMagnitudeMinAxisWeight | float | `0.125f` |
| HectonPhysicsContract | CubeRootMagicBias | int | `709921077` |
| HectonPhysicsContract | CubeRootNewtonOneThird | float | `0.33333334f` |
| HectonPhysicsContract | DeterministicMillimeterScale | float | `1000f` |
| HectonPhysicsContract | DeterministicInvMillimeterScale | float | `0.001f` |
| HectonPhysicsContract | DeterministicMaxQuantizedMillimeterFloat | float | `2147483000f` |
| HectonPhysicsContract | DeterministicMinQuantizedMillimeterFloat | float | `-2147483000f` |
| HectonPhysicsContract | DeterministicMaxQuantizedMillimeter | int | `2147483647` |
| HectonPhysicsContract | DeterministicMinQuantizedMillimeter | int | `-2147483647 - 1` |
| HectonPhysicsContract | DeterministicPi | float | `3.14159265358979323846f` |
| HectonPhysicsContract | DeterministicTwoPi | float | `6.28318530717958647692f` |
| HectonPhysicsContract | DeterministicInvTwoPi | float | `0.15915494309189533577f` |
| HectonPhysicsContract | DeterministicMaxWrapInput | float | `13493037000f` |
| HectonPhysicsContract | AupMaxFloatSafeMeters | double | `1000000000000.0d` |
| HectonPhysicsContract | AupMaxDistanceReturnMeters | double | `1000000000.0d` |
| HectonPlatformContract | AbiStructPackBytes | int | `1` |
| HectonPlatformContract | SimdAlignmentBytes | int | `16` |
| HectonPlatformContract | CacheLineBytes | int | `64` |
| HectonPlatformContract | NativePageAlignmentBytes | int | `4096` |
| HectonPlatformContract | UniversalMaxComputeThreadsPerGroup | int | `1024` |
| HectonPlatformContract | QuestSafeComputeThreadsPerGroup | int | `256` |
| HectonPlatformContract | AndroidSafeComputeThreadsPerGroup | int | `256` |
| HectonPlatformContract | SteamDeckSafeComputeThreadsPerGroup | int | `512` |
| HectonPlatformContract | MetalSafeComputeThreadsPerGroup | int | `512` |
| HectonPlatformContract | PcUltraComputeThreadsPerGroup | int | `1024` |
| HectonPlatformContract | QuestMaxThreadGroupZ | int | `64` |
| HectonPlatformContract | AndroidMaxThreadGroupZ | int | `64` |
| HectonPlatformContract | MetalMaxThreadGroupZ | int | `64` |
| HectonPlatformContract | SteamDeckMicroSdReadBudgetBytesPerFrameLow | int | `16 * 1024` |
| HectonPlatformContract | SteamDeckMicroSdReadBudgetBytesPerFrameMiddle | int | `32 * 1024` |
| HectonPlatformContract | SteamDeckMicroSdReadBudgetBytesPerFrameHigh | int | `64 * 1024` |
| HectonPlatformContract | SteamDeckMicroSdReadBudgetBytesPerFrameUltra | int | `128 * 1024` |
| HectonPlatformContract | SteamDeckMmfPrefetchPageBudgetLow | int | `1` |
| HectonPlatformContract | SteamDeckMmfPrefetchPageBudgetUltra | int | `8` |
| HectonPlatformContract | ContractBlackBoxFrameCapacity | int | `300` |
| HectonPlatformContract | ContractHeartbeatStrideBytes | int | `32` |
| HectonPlatformContract | ContractHeartbeatBufferBytes | int | `ContractBlackBoxFrameCapacity * ContractHeartbeatStrideBytes` |
| HectonSignalLaneContract | AcousticPingSignal | byte | `1` |
| HectonSignalLaneContract | AnomalyProximitySignal | byte | `2` |
| HectonSignalLaneContract | AtmosphericReentrySignal | byte | `3` |
| HectonSignalLaneContract | AupPreShiftSignal | byte | `4` |
| HectonSignalLaneContract | AupShiftSignal | byte | `5` |
| HectonSignalLaneContract | BaseModuleCompromisedSignal | byte | `6` |
| HectonSignalLaneContract | BatteryLevelSignal | byte | `7` |
| HectonSignalLaneContract | BiomeChangedSignal | byte | `8` |
| HectonSignalLaneContract | BiomeGradientSignal | byte | `9` |
| HectonSignalLaneContract | BrownoutSignal | byte | `10` |
| HectonSignalLaneContract | BubbleSpawnSignal | byte | `11` |
| HectonSignalLaneContract | CameraFrustumSignal | byte | `12` |
| HectonSignalLaneContract | CameraPositionSignal | byte | `13` |
| HectonSignalLaneContract | ChunkDehydratedSignal | byte | `14` |
| HectonSignalLaneContract | CombatDamageSignal | byte | `15` |
| HectonSignalLaneContract | CompassCalibratedSignal | byte | `16` |
| HectonSignalLaneContract | CpuStarvationSignal | byte | `17` |
| HectonSignalLaneContract | CraftingCompletedSignal | byte | `18` |
| HectonSignalLaneContract | CullingOverloadSignal | byte | `19` |
| HectonSignalLaneContract | DebrisSpawnSignal | byte | `20` |
| HectonSignalLaneContract | DebugSignal | byte | `21` |
| HectonSignalLaneContract | DeferredSubmarineImpactSignal | byte | `22` |
| HectonSignalLaneContract | DesyncDetectedSignal | byte | `23` |
| HectonSignalLaneContract | DiegeticHudSignal | byte | `24` |
| HectonSignalLaneContract | DockingCompleteSignal | byte | `25` |
| HectonSignalLaneContract | DockingFailedSignal | byte | `26` |
| HectonSignalLaneContract | DockingRequestSignal | byte | `27` |
| HectonSignalLaneContract | DropPodLandedSignal | byte | `28` |
| HectonSignalLaneContract | EntityDeathSignal | byte | `29` |
| HectonSignalLaneContract | EntitySpawnSignal | byte | `30` |
| HectonSignalLaneContract | FaunaStateChangedSignal | byte | `31` |
| HectonSignalLaneContract | FluidImpulseSignal | byte | `32` |
| HectonSignalLaneContract | FramePacingWarningSignal | byte | `33` |
| HectonSignalLaneContract | FrameTimeSignal | byte | `34` |
| HectonSignalLaneContract | HapticRequest | byte | `35` |
| HectonSignalLaneContract | HighSpeedImpactSignal | byte | `36` |
| HectonSignalLaneContract | HullDeformedSignal | byte | `37` |
| HectonSignalLaneContract | HullRepairedSignal | byte | `38` |
| HectonSignalLaneContract | ImpactSignal | byte | `39` |
| HectonSignalLaneContract | InputSignal | byte | `40` |
| HectonSignalLaneContract | InputStateSignal | byte | `41` |
| HectonSignalLaneContract | InventoryChangedSignal | byte | `42` |
| HectonSignalLaneContract | InventoryCommandSignal | byte | `43` |
| HectonSignalLaneContract | ItemAcquiredSignal | byte | `44` |
| HectonSignalLaneContract | ItemDurabilityChangedSignal | byte | `45` |
| HectonSignalLaneContract | KccVelocitySignal | byte | `46` |
| HectonSignalLaneContract | KillSwitchSignal | byte | `47` |
| HectonSignalLaneContract | LaserCutterEventPayload | byte | `48` |
| HectonSignalLaneContract | LockstepSnapshotSignal | byte | `49` |
| HectonSignalLaneContract | LoreFragmentScannedSignal | byte | `50` |
| HectonSignalLaneContract | MacroDatabaseSectorHydrationSignal | byte | `51` |
| HectonSignalLaneContract | ManualOverridePulledSignal | byte | `52` |
| HectonSignalLaneContract | MemoryAddressShiftSignal | byte | `53` |
| HectonSignalLaneContract | MemoryPressureSignal | byte | `54` |
| HectonSignalLaneContract | MovementAcousticSignal | byte | `55` |
| HectonSignalLaneContract | PdaExchangeStateChangedSignal | byte | `56` |
| HectonSignalLaneContract | PhysicsEventPayload | byte | `57` |
| HectonSignalLaneContract | PhysiologyStateSignal | byte | `58` |
| HectonSignalLaneContract | PlayerActionCancelledSignal | byte | `59` |
| HectonSignalLaneContract | PlayerActionCompletedSignal | byte | `60` |
| HectonSignalLaneContract | PlayerActionProgressSignal | byte | `61` |
| HectonSignalLaneContract | PlayerBaseEnterSignal | byte | `62` |
| HectonSignalLaneContract | PlayerBaseExitSignal | byte | `63` |
| HectonSignalLaneContract | PlayerInputSignal | byte | `64` |
| HectonSignalLaneContract | PlayerLookTargetSignal | byte | `65` |
| HectonSignalLaneContract | PlayerStateSignal | byte | `66` |
| HectonSignalLaneContract | PlayerStressSignal | byte | `67` |
| HectonSignalLaneContract | PrologueCompleteSignal | byte | `68` |
| HectonSignalLaneContract | RadiationDoseSignal | byte | `69` |
| HectonSignalLaneContract | RadiationSourceSignal | byte | `70` |
| HectonSignalLaneContract | ReentryVfxStateSignal | byte | `71` |
| HectonSignalLaneContract | ResolutionChangedSignal | byte | `72` |
| HectonSignalLaneContract | ResourceDepletionDeltaSignal | byte | `73` |
| HectonSignalLaneContract | SaveCompletedSignal | byte | `74` |
| HectonSignalLaneContract | SaveMetadataReadySignal | byte | `75` |
| HectonSignalLaneContract | SaveRequestSignal | byte | `76` |
| HectonSignalLaneContract | SaveStatusSignal | byte | `77` |
| HectonSignalLaneContract | ScanLogChangedSignal | byte | `78` |
| HectonSignalLaneContract | ScannerToolActiveSignal | byte | `79` |
| HectonSignalLaneContract | SectorDehydratedSignal | byte | `80` |
| HectonSignalLaneContract | SectorResidencyHydratedSignal | byte | `81` |
| HectonSignalLaneContract | SimulationBucketSyncSignal | byte | `82` |
| HectonSignalLaneContract | SplashEvent | byte | `83` |
| HectonSignalLaneContract | StateCorrectionSignal | byte | `84` |
| HectonSignalLaneContract | StorageDebtSignal | byte | `85` |
| HectonSignalLaneContract | StreamingTurbulenceSignal | byte | `86` |
| HectonSignalLaneContract | SubmarineFloodStateSignal | byte | `87` |
| HectonSignalLaneContract | SubmarineLightsChangedSignal | byte | `88` |
| HectonSignalLaneContract | SurvivalVitalsChangedSignal | byte | `89` |
| HectonSignalLaneContract | SwarmDispersedSignal | byte | `90` |
| HectonSignalLaneContract | SyncFenceSignal | byte | `91` |
| HectonSignalLaneContract | SystemGlitchSignal | byte | `92` |
| HectonSignalLaneContract | SystemHealthIndexSignal | byte | `93` |
| HectonSignalLaneContract | SystemHealthSignal | byte | `94` |
| HectonSignalLaneContract | SystemPauseSignal | byte | `95` |
| HectonSignalLaneContract | TemperatureChangedSignal | byte | `96` |
| HectonSignalLaneContract | TetherFiredSignal | byte | `97` |
| HectonSignalLaneContract | TetherSnappedSignal | byte | `98` |
| HectonSignalLaneContract | TetherTensionSignal | byte | `99` |
| HectonSignalLaneContract | ThermalStateChangedSignal | byte | `100` |
| HectonSignalLaneContract | ToolLoadoutChangedSignal | byte | `101` |
| HectonSignalLaneContract | VehicleUpgradesChangedSignal | byte | `102` |
| HectonSignalLaneContract | VisorDropletSignal | byte | `103` |
| HectonSignalLaneContract | VisualFlareSignal | byte | `104` |
| HectonSignalLaneContract | VoxelCarveEvent | byte | `105` |
| HectonSignalLaneContract | WakeGeneratedSignal | byte | `106` |
| HectonSignalLaneContract | WeatherChangedSignal | byte | `107` |
| HectonSignalLaneContract | WfcOutpostDoorPowerSignal | byte | `108` |
| HectonSignalLaneContract | WfcOutpostGeneratedSignal | byte | `109` |
| HectonSignalLaneContract | WfcOutpostStateChangedSignal | byte | `110` |
| HectonSignalLaneContract | AcousticZoneChangedEvent | byte | `111` |
| HectonSignalLaneContract | DataVaultUpdateSignal | byte | `112` |
| HectonSignalLaneContract | DirectorAIMusicSignal | byte | `113` |
| HectonSignalLaneContract | HUDNotificationSignal | byte | `114` |
| HectonSignalLaneContract | PlayerExhaleSignal | byte | `115` |
| HectonSignalLaneContract | PlayerFatalPressureSignal | byte | `116` |
| HectonSignalLaneContract | PlayerFootstepSignal | byte | `117` |
| HectonSignalLaneContract | PlayerSprintStateSignal | byte | `118` |
| HectonSignalLaneContract | PlayerTransportBailoutSignal | byte | `119` |
| HectonSignalLaneContract | PlayerWaterSplashSignal | byte | `120` |
| HectonSignalLaneContract | PrefabAcousticSignatureSignal | byte | `121` |
| HectonSignalLaneContract | PrefabLoreLinkSignal | byte | `122` |
| HectonSignalLaneContract | ScalabilityChangedEvent | byte | `123` |
| HectonSignalLaneContract | SeismicSignal | byte | `124` |
| HectonSignalLaneContract | ToolAcousticSignal | byte | `125` |
| HectonSignalLaneContract | WaterTransitionSignal | byte | `126` |
| HectonSignalLaneContract | CameraJuiceImpactSignal | byte | `127` |
| HectonSignalLaneContract | ScalabilityChangedEventStableHash | uint | `0x53434C54u` |
| HectonSignalLaneContract | SignalLaneRegistryHash | uint | `0x83E4FE14u` |
| HectonSurvivalContract | KPaPerAtmosphere | float | `101.325f` |
| HectonSurvivalContract | StandardOxygenKPa | float | `21.22f` |
| HectonSurvivalContract | StandardCarbonDioxideKPa | float | `0.04f` |
| HectonSurvivalContract | StandardNitrogenKPa | float | `80.065f` |
| HectonSurvivalContract | StandardOxygenFraction01 | float | `0.21f` |
| HectonSurvivalContract | MaxOxygenFraction01 | float | `1f` |
| HectonSurvivalContract | LowOxygenStressThreshold01 | float | `0.25f` |
| HectonSurvivalContract | CriticalOxygenStressThreshold01 | float | `0.05f` |
| HectonSurvivalContract | DefaultPlayerOxygenKPaPerSecond | float | `0.012f` |
| HectonSurvivalContract | DefaultPlayerCarbonDioxideKPaPerSecond | float | `0.010f` |
| HectonSurvivalContract | DefaultFireOxygenKPaPerSecond | float | `0.080f` |
| HectonSurvivalContract | DefaultScrubberKPaPerSecond | float | `0.055f` |
| HectonSurvivalContract | DefaultCo2ToxicityThresholdKPa | float | `1.0f` |
| HectonSurvivalContract | DefaultCo2FatalKPa | float | `7.0f` |
| HectonSurvivalContract | DefaultNarcosisThresholdAtm | float | `4.0f` |
| HectonSurvivalContract | DefaultNarcosisFullAtm | float | `7.0f` |
| HectonSurvivalContract | DefaultRoomTemperatureCelsius | float | `20f` |
| HectonSurvivalContract | FreezingScrubberEfficiencyScale | float | `0.5f` |
| HectonSurvivalContract | DefaultDiffusionConductancePerSecond | float | `0.45f` |
| HectonSurvivalContract | DefaultHibernationDistanceMeters | float | `500f` |
| HectonSurvivalContract | DefaultLowTierHibernationDistanceMeters | float | `150f` |
| HectonSurvivalContract | DefaultHibernationHysteresisMeters | float | `25f` |
| HectonSurvivalContract | DefaultBaseIdleDrawWatts | float | `45f` |
| HectonSurvivalContract | DefaultBaseBatteryWattSeconds | float | `720000f` |
| HectonSurvivalContract | DefaultHibernationLeakRatePerSecond | float | `0.00006f` |
| HectonSurvivalContract | MaxWakeCatchUpSeconds | float | `86400f` |
| HectonSurvivalContract | MaxDiffusionFractionPerStep | float | `0.45f` |
| HectonSurvivalContract | StressSubstepDeltaSeconds | float | `0.1f` |
| HectonSurvivalContract | StressSubstepsPerSlowTick | int | `5` |
| HectonSurvivalContract | DarknessLightThreshold01 | float | `0.2f` |
| HectonSurvivalContract | SafeLightThreshold01 | float | `0.8f` |
| HectonSurvivalContract | DarknessStressPerSecond | float | `0.05f` |
| HectonSurvivalContract | ApexStressPerSecond | float | `0.2f` |
| HectonSurvivalContract | RecoveryPerSecond | float | `0.1f` |
| HectonSurvivalContract | ApexThreatRadiusMeters | float | `50f` |
| HectonSurvivalContract | AcousticStressImpulseScale | float | `0.08f` |
| HectonSurvivalContract | DamageStressImpulseScale | float | `0.18f` |
| HectonSurvivalContract | SqueezeStressImpulseScale | float | `1.0f` |
| HectonSurvivalContract | SqueezeStressPerSecond | float | `0.1f` |
| HectonSurvivalContract | O2StressMultiplier | float | `1.5f` |
| HectonSurvivalContract | NeutralLightLevel01 | float | `0.5f` |
| HectonSurvivalContract | PanicAttackThreshold01 | float | `1f` |
| HectonSurvivalContract | HallucinationStressThreshold01 | float | `0.9f` |
| HectonSurvivalContract | HallucinationResetThreshold01 | float | `0.84f` |
| HectonSurvivalContract | HallucinationCooldownMinSlowTicks | int | `36` |
| HectonSurvivalContract | HallucinationCooldownRandomSlowTicks | int | `48` |
| HectonSurvivalContract | HallucinationForwardMeters | float | `36f` |
| HectonSurvivalContract | HallucinationSideMeters | float | `18f` |
| HectonSurvivalContract | HallucinationUpMeters | float | `1.25f` |
| HectonSurvivalContract | ClimbStaminaDrainPerMeter | float | `0.18f` |
| HectonSurvivalContract | ClimbStressOxygenDrainBonus | float | `0.28f` |
| HectonSurvivalContract | PressureDamageSafeHullRelief01 | float | `0.45f` |
| HectonSurvivalContract | PressureDamageReliefPerAtmosphere | float | `0.08f` |
| HectonVaultOffsetContract | PhysicsGravity | ushort | `0x0000` |
| HectonVaultOffsetContract | PhysicsWaterDensity | ushort | `0x0004` |
| HectonVaultOffsetContract | PhysicsSoundSpeedWater | ushort | `0x0008` |
| HectonVaultOffsetContract | PhysicsAupSectorSize | ushort | `0x0010` |
| HectonVaultOffsetContract | SurvivalStandardOxygenKPa | ushort | `0x0100` |
| HectonVaultOffsetContract | SurvivalPlayerOxygenDrainKPaPerSecond | ushort | `0x0104` |
| HectonVaultOffsetContract | SurvivalCo2ToxicityThresholdKPa | ushort | `0x0108` |
| HectonVaultOffsetContract | SurvivalNarcosisThresholdAtm | ushort | `0x010C` |
| HectonVaultOffsetContract | EcologyLotkaBirthRate | ushort | `0x0200` |
| HectonVaultOffsetContract | EcologyLotkaDeathRate | ushort | `0x0204` |
| HectonVaultOffsetContract | EcologyLotkaFeedRate | ushort | `0x0208` |
| HectonVaultOffsetContract | EcologyLotkaPredatorConversion | ushort | `0x020C` |
| HectonVaultOffsetContract | EcologyPreyCarryingCapacity | ushort | `0x0210` |
| HectonVaultOffsetContract | ScalabilityMaxBoidsLow | ushort | `0x0300` |
| HectonVaultOffsetContract | ScalabilityMaxBoidsUltra | ushort | `0x0304` |
| HectonVaultOffsetContract | HomeostasisLevel1ActivateShi | ushort | `0x0400` |
| HectonVaultOffsetContract | HomeostasisLevel2ActivateShi | ushort | `0x0404` |
| HectonVaultOffsetContract | HomeostasisLevel3ActivateShi | ushort | `0x0408` |
| HectonVaultOffsetContract | MmfBTreePageSizeBytes | ushort | `0x0500` |
| HectonVaultOffsetContract | MmfBTreePageAlignmentBytes | ushort | `0x0504` |
| HectonVaultOffsetContract | SignalLaneBase | ushort | `0x0600` |
| HectonVaultOffsetContract | LoreHashBase | ushort | `0x0700` |
| HectonVaultOffsetContract | EditorBreadcrumbBase | ushort | `0x0800` |
| HectonVaultOffsetContract | PlatformAbiStructPackBytes | ushort | `0x0900` |
| HectonVaultOffsetContract | PlatformUniversalMaxComputeThreadsPerGroup | ushort | `0x0904` |
| HectonVaultOffsetContract | PlatformSteamDeckMicroSdReadBudgetLow | ushort | `0x0908` |
| HectonVaultOffsetContract | DataSovereigntySystemIdContracts | ushort | `0x0A00` |
| HectonVaultOffsetContract | DataSovereigntyBlackBoxFrameCapacity | ushort | `0x0A04` |
| HectonVaultOffsetContract | DataSovereigntyTypedSignalLaneMaxCount | ushort | `0x0A08` |
| HectonVaultOffsetContract | VisualOverkillLowDearLieLutSamples | ushort | `0x0B00` |
| HectonVaultOffsetContract | VisualOverkillUltraRaymarchSteps | ushort | `0x0B04` |
| HectonVaultOffsetContract | VisualOverkillUltraPomTaps | ushort | `0x0B08` |
| HectonVaultOffsetContract | VisualOverkillUltraWakeSiltParticles | ushort | `0x0B0C` |
| HectonVisualOverkillContract | LowTierDearLieLutSamples | int | `64` |
| HectonVisualOverkillContract | LowTierTriangleNoiseOctaves | int | `1` |
| HectonVisualOverkillContract | LowTierDotProductVisionSamples | int | `1` |
| HectonVisualOverkillContract | LowTierRaymarchSteps | int | `0` |
| HectonVisualOverkillContract | LowTierPomTaps | int | `0` |
| HectonVisualOverkillContract | LowTierSssSamples | int | `0` |
| HectonVisualOverkillContract | LowTierWakeSiltParticles | int | `128` |
| HectonVisualOverkillContract | LowTierVisorSaltCrystalBudget | int | `0` |
| HectonVisualOverkillContract | LowTierHullDentDecalBudget | int | `16` |
| HectonVisualOverkillContract | MiddleTierDearLieLutSamples | int | `128` |
| HectonVisualOverkillContract | MiddleTierTriangleNoiseOctaves | int | `2` |
| HectonVisualOverkillContract | MiddleTierRaymarchSteps | int | `8` |
| HectonVisualOverkillContract | MiddleTierPomTaps | int | `4` |
| HectonVisualOverkillContract | MiddleTierSssSamples | int | `2` |
| HectonVisualOverkillContract | MiddleTierWakeSiltParticles | int | `512` |
| HectonVisualOverkillContract | MiddleTierVisorSaltCrystalBudget | int | `128` |
| HectonVisualOverkillContract | MiddleTierHullDentDecalBudget | int | `64` |
| HectonVisualOverkillContract | HighTierRaymarchSteps | int | `32` |
| HectonVisualOverkillContract | HighTierPomTaps | int | `12` |
| HectonVisualOverkillContract | HighTierSssSamples | int | `6` |
| HectonVisualOverkillContract | HighTierWakeSiltParticles | int | `4096` |
| HectonVisualOverkillContract | HighTierVisorSaltCrystalBudget | int | `1024` |
| HectonVisualOverkillContract | HighTierHullDentDecalBudget | int | `256` |
| HectonVisualOverkillContract | UltraTierRaymarchSteps | int | `64` |
| HectonVisualOverkillContract | UltraTierPomTaps | int | `16` |
| HectonVisualOverkillContract | UltraTierSssSamples | int | `8` |
| HectonVisualOverkillContract | UltraTierWakeSiltParticles | int | `8192` |
| HectonVisualOverkillContract | UltraTierVisorSaltCrystalBudget | int | `2048` |
| HectonVisualOverkillContract | UltraTierHullDentDecalBudget | int | `512` |
| HectonVisualOverkillContract | LowTierWakeSiltStepMeters | float | `4.0f` |
| HectonVisualOverkillContract | UltraTierWakeSiltStepMeters | float | `0.75f` |
| HectonVisualOverkillContract | LowTierSaltCrystalSpawnChance01 | float | `0.0f` |
| HectonVisualOverkillContract | UltraTierSaltCrystalSpawnChance01 | float | `0.85f` |
| HectonVisualOverkillContract | LowTierHullDentNormalBlend01 | float | `0.25f` |
| HectonVisualOverkillContract | UltraTierHullDentNormalBlend01 | float | `0.95f` |
| JobAdmissionLanes | Count | int | `6` |
| JobAdmissionLanes | Lane0Critical | int | `0` |
| JobAdmissionLanes | Lane1World | int | `1` |
| JobAdmissionLanes | Lane2Voxel | int | `2` |
| JobAdmissionLanes | Lane3AI | int | `3` |
| JobAdmissionLanes | Lane4VFX | int | `4` |
| JobAdmissionLanes | Lane5IO | int | `5` |
| MacroDatabaseContracts | CellSizeMeters | int | `HectonPhysicsContract.AupSectorSizeMetersInt` |
| MacroDatabaseCompactionFlags | MemoryPressurePaused | byte | `1 << 0` |
| MacroDatabaseCompactionFlags | PersistenceGate | byte | `1 << 1` |
| MacroDatabaseCompactionFlags | TempReady | byte | `1 << 2` |
| MacroDatabaseCompactionFlags | LastSwapExceededBudget | byte | `1 << 3` |
| MacroDatabasePayloadFlags | Dirty | byte | `1 << 0` |
| H8WorldPagePayloadTypes | VoxelDeltaRle | uint | `0x5658524Cu` |
| H8WorldPagePayloadTypes | InventoryState | uint | `0x494E5654u` |
| H8WorldPagePayloadTypes | ChunkDehydratedMetadata | uint | `0x43484452u` |
| H8WorldPagePayloadTypes | WfcOutpostState | uint | `0x5746434Fu` |
| WfcOutpostPersistenceConstants | GridSizeX | int | `10` |
| WfcOutpostPersistenceConstants | GridSizeY | int | `10` |
| WfcOutpostPersistenceConstants | GridSizeZ | int | `5` |
| WfcOutpostPersistenceConstants | CellCount | int | `GridSizeX * GridSizeY * GridSizeZ` |
| WfcOutpostPersistenceConstants | MutableBitPlaneCount | int | `4` |
| WfcOutpostPersistenceConstants | PackedBitCount | int | `CellCount * MutableBitPlaneCount` |
| WfcOutpostPersistenceConstants | PackedWordCount | int | `(PackedBitCount + 63) / 64` |
| WfcOutpostPersistenceConstants | PackedWordBytes | int | `PackedWordCount * sizeof(ulong)` |
| WfcOutpostPersistenceConstants | PayloadHeaderBytes | int | `32` |
| WfcOutpostPersistenceConstants | PayloadMaxBytes | int | `PayloadHeaderBytes + PackedWordBytes` |
| WfcOutpostPersistenceConstants | MutableFlagMask | byte | `(byte)( WfcOutpostCellStateFlags.DoorOpen \| WfcOutpostCellStateFlags.DoorUnlocked \| WfcOutpostCellStateFlags.PowerOn \| WfcOutpostCellStateFlags.DatapadLooted)` |
| PrologueCancelReasons | TokenCancelled | byte | `1` |
| PrologueCancelReasons | ExplicitCancel | byte | `2` |
| PrologueCancelReasons | DevSkip | byte | `3` |
| PrologueCancelReasons | NonFinite | byte | `4` |
| PrologueSignalSourceHashes | SequenceDirector | uint | `0x50524C47u` |
| PrologueSignalSourceHashes | ManualOverrideLever | uint | `0x4D4F5652u` |
| PrologueSignalSourceHashes | OrbitalRelativityDirector | uint | `0x4F524249u` |
| ScalabilityContract | MaxBoidsCount_Low | int | `256` |
| ScalabilityContract | MaxBoidsCount_Middle | int | `1000` |
| ScalabilityContract | MaxBoidsCount_High | int | `2000` |
| ScalabilityContract | MaxBoidsCount_Ultra | int | `5000` |
| ScalabilityContract | HomeostasisFrameTimeWindow | int | `120` |
| ScalabilityContract | HomeostasisBlackBoxCapacity | int | `300` |
| ScalabilityContract | HomeostasisTelemetryCadenceFrames | int | `60` |
| ScalabilityContract | HomeostasisRecoveryArmFrames | int | `3000` |
| ScalabilityContract | HomeostasisRecoveryStepFrames | int | `60` |
| ScalabilityContract | HomeostasisFrostPollSeconds | float | `5f` |
| ScalabilityContract | HomeostasisFpsEwmaAlpha | float | `0.1f` |
| ScalabilityContract | HomeostasisShiEwmaAlpha | float | `0.12f` |
| ScalabilityContract | HomeostasisJitterUnstableSigmaMs | float | `2.0f` |
| ScalabilityContract | HomeostasisLevel1ActivateShi | float | `0.60f` |
| ScalabilityContract | HomeostasisLevel1RestoreShi | float | `0.50f` |
| ScalabilityContract | HomeostasisLevel2ActivateShi | float | `0.80f` |
| ScalabilityContract | HomeostasisLevel2RestoreShi | float | `0.70f` |
| ScalabilityContract | HomeostasisLevel3ActivateShi | float | `0.95f` |
| ScalabilityContract | HomeostasisLevel3RestoreShi | float | `0.90f` |
| ScalabilityContract | HomeostasisSequentialRecoveryShi | float | `0.30f` |
| ScalabilityContract | HomeostasisPersistentNativeBudgetBytes | long | `8192L` |
| ScalabilityContract | TargetFrameMilliseconds | float | `16.667f` |
| ScalabilityContract | PreSimulationBudgetMilliseconds | float | `1.5f` |
| ScalabilityContract | Lod0ScreenRatio01 | float | `0.20f` |
| ScalabilityContract | Lod1ScreenRatio01 | float | `0.55f` |
| ScalabilityContract | Lod2ScreenRatio01 | float | `0.25f` |
| ScalabilityContract | LodFadeDistanceMeters | float | `2f` |
| ScalabilityContract | LowTierVramPressureRatio01 | float | `0.70f` |
| ScalabilityContract | MiddleTierVramPressureRatio01 | float | `0.78f` |
| ScalabilityContract | HighTierVramPressureRatio01 | float | `0.84f` |
| ScalabilityContract | UltraTierVramPressureRatio01 | float | `0.90f` |
| SimulationBucketConstants | FastBucketCount | int | `4` |
| SimulationBucketConstants | FastBucketMask | int | `FastBucketCount - 1` |
| SimulationBucketConstants | StandardSlowBucketCount | int | `64` |
| SimulationBucketConstants | StandardSlowBucketMask | int | `StandardSlowBucketCount - 1` |
| SimulationBucketConstants | LowSlowBucketCount | int | `128` |
| SimulationBucketConstants | LowSlowBucketMask | int | `LowSlowBucketCount - 1` |
| SimulationBucketConstants | ColdBucketCount | int | `512` |
| SimulationBucketConstants | ColdBucketMask | int | `ColdBucketCount - 1` |
| SimulationBucketConstants | DefaultEntityCapacity | int | `8192` |
| SimulationBucketConstants | MaxEntityCapacity | int | `1 << 20` |
| SimulationBucketConstants | HighTierActiveSlowBucketCount | byte | `2` |
| SimulationBucketConstants | MinimumActiveSlowBucketCount | byte | `1` |
| SimulationBucketConstants | TargetFrameMilliseconds | float | `Hecton8.Core.Contracts.ScalabilityContract.TargetFrameMilliseconds` |
| SimulationBucketConstants | PreSimulationBudgetMilliseconds | float | `Hecton8.Core.Contracts.ScalabilityContract.PreSimulationBudgetMilliseconds` |
| SimulationBucketConstants | RebalanceCadenceFrames | int | `60` |
| SimulationBucketPacingFlags | Impossible60Fps | uint | `1u << 0` |
| SimulationBucketPacingFlags | PreSimulationOverBudget | uint | `1u << 1` |
| SimulationBucketPacingFlags | NonFiniteCost | uint | `1u << 2` |
| SimulationBucketPacingFlags | RebalancePending | uint | `1u << 3` |
| SimulationBucketPacingFlags | LowTierStaticDistribution | uint | `1u << 4` |
| SimulationBucketPacingFlags | HomeostasisKillRequested | uint | `1u << 5` |
| SimulationBucketPacingFlags | VisualOverkillBudgetAvailable | uint | `1u << 6` |

RU: Eto karta zakonov dvizhka. Menyat fiziku, vyzhivanie, ekologiyu, LOD i ABI offsety nuzhno zdes, ne v Burst jobah.
EN: This is the law map for the engine. Change physics, survival, ecology, LOD, and ABI offsets here, not inside Burst jobs.
