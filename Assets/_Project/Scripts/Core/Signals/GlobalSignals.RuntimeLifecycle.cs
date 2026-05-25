using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Hecton8.Core.Contracts.Signals.SignalPayloadSanitizer;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core
{
    public static partial class GlobalSignals
    {
        public static void InitializeAllQueues()
        {
            if (_initialized)
                return;

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            IDataVault dataVault = GlobalRegistry.DataVault;
            SignalBusRegistry.SetGlobalQualityWeight01(qualityWeight);
            SignalBusRegistry.ClearSimulationHalt();
            SignalPriorityTable.InitializeFromDisk();
            SignalTuningTable.Initialize(dataVault);
#if UNITY_EDITOR
            SignalTuningCsvHotSwap.TryLoadDefault();
#endif
            SignalTelemetryRingBuffer.Initialize();
            SignalThreadLocalScratchpad.Initialize(
                dataVault,
                qualityWeight,
                dataVault != null ? dataVault.CapacityPressure01 : 0f);
#if UNITY_EDITOR
            SignalThreadContentionCsvHotSwap.TryLoadDefault();
#endif
            RegisterLegacyLane<ImpactSignal>(ImpactSignal.ExpectedCapacity, nameof(ImpactSignal));
            RegisterLegacyLane<AupPreShiftSignal>(AupPreShiftSignalCapacity, nameof(AupPreShiftSignal));
            RegisterLegacyLane<AupShiftSignal>(AupShiftSignalCapacity, nameof(AupShiftSignal));
            RegisterLegacyLane<BrownoutSignal>(BrownoutSignalCapacity, nameof(BrownoutSignal));
            RegisterLegacyLane<DebrisSpawnSignal>(DebrisSpawnSignalCapacity, nameof(DebrisSpawnSignal));
            RegisterLegacyLane<DeflectSignal>(DeflectSignalCapacity, nameof(DeflectSignal));
            RegisterLegacyLane<EntityDeathSignal>(EntityDeathSignalCapacity, nameof(EntityDeathSignal));
            RegisterLegacyLane<RebaseSignal>(RebaseSignalCapacity, nameof(RebaseSignal));
            RegisterLegacyLane<ControlSignal>(ControlSignalCapacity, nameof(ControlSignal));
            RegisterLegacyLane<AnomalySignal>(AnomalySignalCapacity, nameof(AnomalySignal));
            RegisterLegacyLane<TelemetryAnomalySignal>(TelemetryAnomalySignalCapacity, nameof(TelemetryAnomalySignal));
            RegisterLegacyLane<CrashTelemetrySignal>(CrashTelemetrySignalCapacity, nameof(CrashTelemetrySignal));
            RegisterLegacyLane<HabitatConstructionSignal>(HabitatConstructionSignalCapacity, nameof(HabitatConstructionSignal));
            RegisterLegacyLane<DeconstructRequestSignal>(DeconstructRequestSignalCapacity, nameof(DeconstructRequestSignal));
            RegisterLegacyLane<DeconstructResultSignal>(DeconstructResultSignalCapacity, nameof(DeconstructResultSignal));
            RegisterLegacyLane<ModuleDeconstructSignal>(ModuleDeconstructSignalCapacity, nameof(ModuleDeconstructSignal));
            RegisterLegacyLane<VitalWarningSignal>(VitalWarningSignalCapacity, nameof(VitalWarningSignal));
            RegisterLegacyLane<CrushWarningSignal>(CrushWarningSignalCapacity, nameof(CrushWarningSignal));
            RegisterLegacyLane<VocalWarningSignal>(VocalWarningSignalCapacity, nameof(VocalWarningSignal));
            RegisterLegacyLane<SubtitleSignal>(SubtitleSignalCapacity, nameof(SubtitleSignal));
            RegisterLegacyLane<MemoryPressureSignal>(MemoryPressureSignalCapacity, nameof(MemoryPressureSignal));
            RegisterLegacyLane<MovementAcousticSignal>(MovementAcousticSignalCapacity, nameof(MovementAcousticSignal));
            RegisterLegacyLane<SonarPingSignal>(SonarPingSignalCapacity, nameof(SonarPingSignal));
            RegisterLegacyLane<HypoxiaSignal>(HypoxiaSignalCapacity, nameof(HypoxiaSignal));
            RegisterLegacyLane<OxygenCriticalSignal>(OxygenCriticalSignalCapacity, nameof(OxygenCriticalSignal));
            RegisterLegacyLane<InteractionUiSignal>(InteractionUiSignalCapacity, nameof(InteractionUiSignal));
            RegisterLegacyLane<UIRescaleRequestSignal>(UIRescaleRequestSignalCapacity, nameof(UIRescaleRequestSignal));
            RegisterLegacyLane<PipeRuptureSignal>(PipeRuptureSignalCapacity, nameof(PipeRuptureSignal));
            RegisterLegacyLane<RigidbodySleepSignal>(RigidbodySleepSignalCapacity, nameof(RigidbodySleepSignal));
            RegisterLegacyLane<ScannerToolActiveSignal>(ScannerToolActiveSignalCapacity, nameof(ScannerToolActiveSignal));
            RegisterLegacyLane<ScanCompleteSignal>(ScanCompleteSignalCapacity, nameof(ScanCompleteSignal));
            RegisterLegacyLane<BlueprintUnlockedSignal>(BlueprintUnlockedSignalCapacity, nameof(BlueprintUnlockedSignal));
            RegisterLegacyLane<EncyclopediaUnlockSignal>(64, nameof(EncyclopediaUnlockSignal));
            RegisterLegacyLane<EntityDepletedSignal>(64, nameof(EntityDepletedSignal));
            RegisterLegacyLane<CraftingStartedSignal>(CraftingStartedSignalCapacity, nameof(CraftingStartedSignal));
            RegisterLegacyLane<CraftingCompletedSignal>(CraftingCompletedSignalCapacity, nameof(CraftingCompletedSignal));
            RegisterLegacyLane<ToolStateChangedSignal>(ToolStateChangedSignalCapacity, nameof(ToolStateChangedSignal));
            RegisterLegacyLane<PowerDrainSignal>(PowerDrainSignalCapacity, nameof(PowerDrainSignal));
            RegisterLegacyLane<ToolTriggerSignal>(ToolTriggerSignalCapacity, nameof(ToolTriggerSignal));
            RegisterLegacyLane<HUDNotificationSignal>(HUDNotificationSignalCapacity, nameof(HUDNotificationSignal));
            RegisterLegacyLane<SaveLifecycleSignal>(SaveLifecycleSignalCapacity, nameof(SaveLifecycleSignal));
            RegisterLegacyLane<ComplianceViolationSignal>(ComplianceViolationSignalCapacity, nameof(ComplianceViolationSignal));
            RegisterLegacyLane<GlobalTimeSyncSignal>(GlobalTimeSyncSignalCapacity, nameof(GlobalTimeSyncSignal));
            RegisterLegacyLane<TimeDilationSignal>(TimeDilationSignalCapacity, nameof(TimeDilationSignal));
            RegisterLegacyLane<SimulationPauseSignal>(SimulationPauseSignalCapacity, nameof(SimulationPauseSignal));
            RegisterLegacyLane<BulletTimeVisualSignal>(BulletTimeVisualSignalCapacity, nameof(BulletTimeVisualSignal));
            RegisterLegacyLane<ItemAcquiredSignal>(ItemAcquiredSignalCapacity, nameof(ItemAcquiredSignal));
            RegisterLegacyLane<RadiationDoseSignal>(RadiationDoseSignalCapacity, nameof(RadiationDoseSignal));
            RegisterLegacyLane<ResourceDepletionDeltaSignal>(ResourceDepletionDeltaSignalCapacity, nameof(ResourceDepletionDeltaSignal));
            RegisterLegacyLane<LightLevelSignal>(LightLevelSignalCapacity, nameof(LightLevelSignal));
            RegisterLegacyLane<FaunaStateChangedSignal>(FaunaStateChangedSignalCapacity, nameof(FaunaStateChangedSignal));
            RegisterLegacyLane<PlayerStressSignal>(PlayerStressSignalCapacity, nameof(PlayerStressSignal));
            RegisterLegacyLane<TraumaSignal>(TraumaSignalCapacity, nameof(TraumaSignal));
            RegisterLegacyLane<GlobalWorldStateSignal>(GlobalWorldStateSignalCapacity, nameof(GlobalWorldStateSignal));
            RegisterLegacyLane<BiomeChangedSignal>(BiomeChangedSignalCapacity, nameof(BiomeChangedSignal));
            RegisterLegacyLane<NarrativeFocusSignal>(NarrativeFocusSignalCapacity, nameof(NarrativeFocusSignal));
            RegisterLegacyLane<FocusBrokenSignal>(FocusBrokenSignalCapacity, nameof(FocusBrokenSignal));
            RegisterLegacyLane<MixerStateSignal>(MixerStateSignalCapacity, nameof(MixerStateSignal));
            RegisterLegacyLane<NarrativeHudWaypointSignal>(NarrativeHudWaypointSignalCapacity, nameof(NarrativeHudWaypointSignal));
            RegisterLegacyLane<SoundscapeProfileSignal>(SoundscapeProfileSignalCapacity, nameof(SoundscapeProfileSignal));
            RegisterLegacyLane<NarrativePoiStateSignal>(NarrativePoiStateSignalCapacity, nameof(NarrativePoiStateSignal));
            InitializeCategorySignalLanes();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateSignalPayload<ImpactSignal>(64);
            ValidateSignalSize<HighSpeedImpactSignal>(128);
            ValidateSignalSize<HapticRequest>(32);
            ValidateSignalSize<HapticPulseSignal>(16);
            ValidateSignalSize<PlayerStateSignal>(64);
            ValidateSignalSize<PlayerRespawnSignal>(128);
            ValidateSignalSize<InventoryRespawnDeathAupSignal>(64);
            ValidateSignalSize<InventoryRespawnPenaltyResultSignal>(32);
            ValidateSignalSize<SurvivalVitalsChangedSignal>(32);
            ValidateSignalPayload<AupPreShiftSignal>(32);
            ValidateSignalPayload<AupShiftSignal>(32);
            ValidateSignalSize<DropPodLandedSignal>(64);
            ValidateSignalSize<PlayerLookTargetSignal>(128);
            ValidateSignalSize<BrownoutSignal>(32);
            ValidateSignalSize<DebrisSpawnSignal>(64);
            ValidateSignalSize<DeflectSignal>(32);
            ValidateSignalSize<EntityDeathSignal>(64);
            ValidateSignalSize<EntitySpawnSignal>(64);
            ValidateSignalSize<SolarFlareSignal>(32);
            ValidateSignalSize<RebaseSignal>(32);
            ValidateSignalSize<ControlSignal>(32);
            ValidateSignalPayload<AnomalySignal>(32);
            ValidateSignalSize<TelemetryAnomalySignal>(32);
            ValidateSignalSize<CrashTelemetrySignal>(32);
            ValidateSignalSize<HabitatConstructionSignal>(64);
            ValidateSignalSize<DeconstructRequestSignal>(128);
            ValidateSignalSize<DeconstructResultSignal>(64);
            ValidateSignalSize<ModuleDeconstructSignal>(64);
            ValidateSignalSize<VitalWarningSignal>(32);
            ValidateSignalSize<CrushWarningSignal>(32);
            ValidateSignalSize<VocalWarningSignal>(32);
            ValidateSignalSize<VocalCueSignal>(64);
            ValidateSignalSize<SubtitleSignal>(32);
            ValidateSignalSize<DataReloadSignal>(32);
            ValidateSignalSize<DataVaultUpdateSignal>(32);
            ValidateSignalSize<PrefabAcousticSignatureSignal>(32);
            ValidateSignalSize<PrefabLoreLinkSignal>(32);
            ValidateSignalSize<MemoryPressureSignal>(32);
            ValidateSignalSize<MemoryAddressShiftSignal>(64);
            ValidateSignalSize<ResolutionChangedSignal>(32);
            ValidateSignalSize<SystemHealthIndexSignal>(32);
            ValidateSignalSize<ScalabilityChangedEvent>(16);
            ValidateSignalPayload<AcousticPingSignal>(64);
            ValidateSignalPayload<MovementAcousticSignal>(64);
            ValidateSignalSize<AcousticZoneChangedEvent>(16);
            ValidateSignalSize<DirectorAIMusicSignal>(32);
            ValidateSignalSize<DynamicMusicScalarSignal>(64);
            ValidateSignalSize<global::Hecton8.Core.Contracts.Signals.AudioEvent>(128);
            ValidateSignalPayload<SwarmDispersedSignal>(64);
            ValidateSignalSize<MacroDatabaseSectorHydrationSignal>(32);
            ValidateSignalSize<WfcOutpostGeneratedSignal>(128);
            ValidateSignalSize<WfcOutpostStateChangedSignal>(32);
            ValidateSignalSize<WfcOutpostDoorPowerSignal>(128);
            ValidateSignalSize<SectorResidencyHydratedSignal>(64);
            ValidateSignalSize<SectorDehydratedSignal>(64);
            ValidateSignalSize<ChunkDehydratedSignal>(64);
            ValidateSignalSize<SonarPingSignal>(64);
            ValidateSignalPayload<HypoxiaSignal>(32);
            ValidateSignalSize<OxygenCriticalSignal>(32);
            ValidateSignalSize<InteractionUiSignal>(64);
            ValidateSignalSize<UIRescaleRequestSignal>(32);
            ValidateSignalSize<FluidIncursionSignal>(64);
            ValidateSignalSize<SubmarineFloodStateSignal>(64);
            ValidateSignalSize<FluidDensityChangedSignal>(64);
            ValidateSignalSize<PipeRuptureSignal>(64);
            ValidateSignalSize<SpectrumScanSignal>(32);
            ValidateSignalSize<RigidbodySleepSignal>(64);
            ValidateSignalSize<ScannerToolActiveSignal>(32);
            ValidateSignalPayload<ScanCompleteSignal>(64);
            ValidateSignalSize<LoreFragmentScannedSignal>(32);
            ValidateSignalSize<BlueprintUnlockedSignal>(32);
            ValidateSignalSize<CraftingStartedSignal>(32);
            ValidateSignalSize<CraftingCompletedSignal>(32);
            ValidateSignalSize<ToolStateChangedSignal>(32);
            ValidateSignalSize<ToolLoadoutChangedSignal>(32);
            ValidateSignalSize<ToolAcousticSignal>(32);
            ValidateSignalSize<PowerDrainSignal>(32);
            ValidateSignalSize<ToolTriggerSignal>(32);
            ValidateSignalSize<HUDNotificationSignal>(32);
            ValidateSignalSize<ReconDataSignal>(64);
            ValidateSignalSize<SaveLifecycleSignal>(32);
            ValidateSignalSize<ComplianceViolationSignal>(32);
            ValidateSignalSize<GlobalTimeSyncSignal>(32);
            ValidateSignalSize<InputStateSignal>(32);
            ValidateSignalSize<InputSignal>(64);
            ValidateSignalSize<StateCorrectionSignal>(128);
            ValidateSignalSize<DesyncDetectedSignal>(32);
            ValidateSignalSize<SyncFenceSignal>(128);
            ValidateSignalSize<KccVelocitySignal>(128);
            ValidateSignalSize<LockstepSnapshotSignal>(32);
            ValidateSignalSize<SystemGlitchSignal>(32);
            ValidateSignalSize<LaserCutterEventPayload>(16);
            ValidateSignalSize<SplashEvent>(64);
            ValidateSignalSize<PhysicsEventPayload>(128);
            ValidateSignalSize<DeferredSubmarineImpactSignal>(64);
            ValidateSignalSize<DebugSignal>(64);
            ValidateSignalSize<SeismicSignal>(96);
            ValidateSignalSize<TimeDilationSignal>(32);
            ValidateSignalSize<SimulationPauseSignal>(32);
            ValidateSignalSize<BulletTimeVisualSignal>(32);
            ValidateSignalSize<WeatherStrengthSignal>(32);
            ValidateSignalSize<ItemDecaySignal>(64);
            ValidateSignalSize<ItemDurabilityChangedSignal>(32);
            ValidateSignalSize<ItemLifecycleSignal>(64);
            ValidateSignalSize<ItemAcquiredSignal>(64);
            ValidateSignalSize<InventoryDeathLootCacheSignal>(128);
            ValidateSignalSize<RadiationDoseSignal>(64);
            ValidateSignalSize<TemperatureChangedSignal>(64);
            ValidateSignalSize<ThermalSourceSignal>(64);
            ValidateSignalSize<ResourceDepletionDeltaSignal>(32);
            ValidateSignalSize<LightLevelSignal>(32);
            ValidateSignalSize<SubmarineLightsChangedSignal>(128);
            ValidateSignalSize<FaunaStateChangedSignal>(64);
            ValidateSignalSize<PhysiologyStateSignal>(64);
            ValidateSignalSize<PlayerStressSignal>(32);
            ValidateSignalSize<TraumaSignal>(32);
            ValidateSignalSize<WakeGeneratedSignal>(64);
            ValidateSignalSize<ProgressionEventSignal>(64);
            ValidateSignalSize<ProgressionMetaSignal>(32);
            ValidateSignalSize<SessionLifecycleSignal>(64);
            ValidateSignalSize<GlobalWorldStateSignal>(64);
            ValidateSignalSize<BiomeChangedSignal>(64);
            ValidateSignalSize<NarrativeFocusSignal>(128);
            ValidateSignalSize<FocusBrokenSignal>(32);
            ValidateSignalSize<MixerStateSignal>(32);
            ValidateSignalSize<DiegeticHudSignal>(32);
            ValidateSignalSize<NarrativeHudWaypointSignal>(64);
            ValidateSignalSize<SoundscapeProfileSignal>(64);
            ValidateSignalSize<NarrativePoiStateSignal>(32);
            ValidateSignalSize<CombatDamageSignal>(64);
            ValidateSignalSize<HullDeformedSignal>(64);
            ValidateSignalSize<BaseModuleCompromisedSignal>(64);
            ValidateSignalSize<BaseStructuralWarningSignal>(64);
            ValidateSignalSize<PlayerBaseEnterSignal>(64);
            ValidateSignalSize<PlayerBaseExitSignal>(64);
            ValidateSignalSize<CameraPositionSignal>(32);
            ValidateSignalSize<CameraFrustumSignal>(64);
            ValidateSignalSize<WeatherChangedSignal>(32);
            ValidateSignalSize<SystemPauseSignal>(32);
            ValidateSignalSize<SimulationBucketSyncSignal>(32);
            ValidateSignalSize<FramePacingWarningSignal>(64);
            ValidateSignalSize<SaveRequestSignal>(32);
            ValidateSignalSize<SaveCompletedSignal>(32);
            ValidateSignalSize<SaveStatusSignal>(32);
            ValidateSignalSize<SaveMetadataReadySignal>(32);
            ValidateSignalSize<CpuStarvationSignal>(32);
            ValidateSignalSize<StorageDebtSignal>(32);
            ValidateSignalSize<StreamingTurbulenceSignal>(32);
            ValidateSignalSize<AtmosphericReentrySignal>(64);
            ValidateSignalSize<PrologueCompleteSignal>(64);
            ValidateSignalSize<ManualOverridePulledSignal>(64);
            ValidateSignalSize<CullingOverloadSignal>(32);
            ValidateSignalSize<PlayerActionProgressSignal>(32);
            ValidateSignalSize<PlayerActionCompletedSignal>(32);
            ValidateSignalSize<PlayerActionCancelledSignal>(32);
            ValidateSignalSize<ScanLogChangedSignal>(32);
            ValidateSignalSize<PdaExchangeStateChangedSignal>(32);
            ValidateSignalSize<VehicleUpgradesChangedSignal>(32);
            ValidateSignalSize<SystemHealthSignal>(64);
            ValidateSignalSize<FrameTimeSignal>(32);
            ValidateSignalSize<KillSwitchSignal>(32);
            ValidateSignalSize<SystemKillSwitchBitsSignal>(32);
            ValidateSignalSize<ReentryVfxStateSignal>(64);
            ValidateSignalSize<VisorDropletSignal>(64);
            ValidateSignalSize<CameraJuiceImpactSignal>(128);
            ValidateSignalSize<PlayerFootstepSignal>(32);
            ValidateSignalSize<PlayerWaterSplashSignal>(32);
            ValidateSignalSize<WaterTransitionSignal>(128);
            ValidateSignalSize<PlayerExhaleSignal>(16);
            ValidateSignalSize<PlayerSprintStateSignal>(16);
            ValidateSignalSize<PlayerFatalPressureSignal>(16);
            ValidateSignalSize<PlayerTransportBailoutSignal>(32);
            ValidateSignalSize<VisualFlareSignal>(32);
            ValidateSignalSize<TetherTensionSignal>(192);
            ValidateSignalSize<TetherSnappedSignal>(128);
            ValidateSignalSize<TetherFiredSignal>(64);
            ValidateSignalSize<VoxelCarveEvent>(128);
            ValidateSignalSize<DockingRequestSignal>(128);
            ValidateSignalSize<DockingCompleteSignal>(128);
            ValidateSignalSize<DockingFailedSignal>(128);
            ValidateSignalSize<AnomalyProximitySignal>(128);
            ValidateSignalSize<CompassCalibratedSignal>(32);
            ValidateSignalSize<SignalWardenMockDamageSignal>(64);
            ValidateSignalSize<MockPlayerFootstepSignal>(128);
            ValidateSignalSize<MockRockCollisionSignal>(64);
            ValidateSignalSize<MacroCollisionSignal>(64);
            ValidateSignalSize<SignalThreadLocalHeader64>(64);
            ValidateSignalSize<SignalThreadContentionTelemetryEntry>(64);
            ValidateSignalSize<SignalThreadContentionTuning64>(64);
            ValidateSignalSize<SignalThreadOverflowHeader64>(64);
            SignalThreadContentionLayoutGuard.Validate();
            ValidateSignalSize<WakeRequestSignal>(64);
#endif

            _initialized = true;
        }

        /// <summary>Initializes only the diagnostics visual lane without waking gameplay signal queues.</summary>
        public static void EnsureDebugSignalLaneInitialized()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            SignalBusRegistry.SetGlobalQualityWeight01(qualityWeight);
            ConfigureDebugSignalLane();
        }

        /// <summary>Initializes only the haptic proof lane without waking unrelated gameplay signal queues.</summary>
        public static void EnsureHapticPulseSignalLaneInitialized()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            SignalBusRegistry.SetGlobalQualityWeight01(qualityWeight);
            SignalBus<HapticPulseSignal>.Configure(
                HapticPulseSignalCapacity,
                maxFrameSignals: HapticPulseSignalCapacity,
                lowTierFrameSignals: 1,
                laneHash: HapticPulseSignal.LaneHash);
            SignalBus<HapticPulseSignal>.EnsureInitialized();
        }

        /// <summary>Disposes every native signal lane. Call during clean application or session shutdown.</summary>
        public static void DisposeAllQueues()
        {
            SignalBusRegistry.DisposeAll();
            SignalTelemetryRingBuffer.ReleaseHandlesOnly();
            SignalThreadLocalScratchpad.ReleaseHandlesOnly();
            ClearLatestSignals();
            _initialized = false;
        }

        /// <summary>Refreshes scalar lane controls before simulation without draining the signal rings.</summary>
        public static void PreSimulationHeartbeat()
        {
            EnsureInitialized();
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            SignalBusRegistry.SetGlobalQualityWeight01(qualityWeight);
            SignalBusRegistry.SetSystemStress01(global::Hecton8.Core.HomeostasisBrain.SystemHealthIndex01);
        }

        /// <summary>Flushes typed signal rings into next-frame snapshots at the POST_SIMULATION boundary.</summary>
        public static void FlushPostSimulation()
        {
            EnsureInitialized();
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            SignalBusRegistry.SetGlobalQualityWeight01(qualityWeight);
            SignalBusRegistry.SetSystemStress01(global::Hecton8.Core.HomeostasisBrain.SystemHealthIndex01);
            SignalBusRegistry.FlushPostSimulation();
            ApplyAupShiftSafety();
            ReportSignalLaneTelemetry();
        }

        /// <summary>Resets static signal state on domain reload or subsystem registration.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAllQueues();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAllQueues;
            Application.quitting += DisposeAllQueues;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAllQueues();
        }

        private static void ApplyAupShiftSafety()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 shiftMeters = shifts[i].ShiftMeters;
                if (!math.all(math.isfinite(shiftMeters)))
                    continue;

                CombatDamageSignalAupShiftTransformer transformer = default;
                transformer.SetShift(shiftMeters);
                SignalBus<CombatDamageSignal>.TransformSnapshot(transformer);
            }
        }

        private static void ReportSignalLaneTelemetry()
        {
            int laneCount = SignalBusRegistry.LaneCount;
            if (laneCount <= 0)
                return;

            if (SignalBusRegistry.RegistrationOverflow)
            {
                CrashTelemetryBuffer.ReportSignalLaneStats(
                    ComputeStableSignalLaneHash(nameof(SignalBusRegistry)),
                    laneCount,
                    0,
                    1);
            }

            int startIndex = Volatile.Read(ref _signalTelemetryCursor);
            if ((uint)startIndex >= (uint)laneCount)
                startIndex = 0;

            int sampledNonCritical = 0;
            int pushedSignals = 0;
            int peakSignals = 0;
            int coalescedSignals = 0;
            int droppedSignals = 0;
            int corruptedSignals = 0;
            for (int pass = 0; pass < laneCount; pass++)
            {
                int laneIndex = startIndex + pass;
                if (laneIndex >= laneCount)
                    laneIndex -= laneCount;

                if (!SignalBusRegistry.TryCopyTelemetryAt(laneIndex, out SignalLaneTelemetry telemetry))
                    continue;

                int snapshotCount = telemetry.SnapshotCount;
                int droppedCount = telemetry.DroppedCount;
                int coalescedCount = telemetry.CoalescedCount;
                int pushedCount = DecodeSignalLaneTelemetryPushed(in telemetry);
                int corruptedCount = DecodeSignalLaneTelemetryCorrupted(in telemetry);
                int queuedBeforeFlush = telemetry.QueuedBeforeFlush;
                if (queuedBeforeFlush > peakSignals)
                    peakSignals = queuedBeforeFlush;
                if (pushedCount > 0)
                    pushedSignals += pushedCount;
                if (coalescedCount > 0)
                    coalescedSignals += coalescedCount;
                if (droppedCount > 0)
                    droppedSignals += droppedCount;
                if (corruptedCount > 0)
                    corruptedSignals += corruptedCount;

                if (snapshotCount <= 0 && droppedCount <= 0 && corruptedCount <= 0)
                    continue;

                bool stormDetected = (telemetry.Flags & 1) != 0;
                bool critical = droppedCount > 0 || corruptedCount > 0 || stormDetected;
                if (!critical && sampledNonCritical >= SignalTelemetryLaneBudgetPerFrame)
                    continue;

                int droppedOrCorruptedCount = DecodeSignalLaneTelemetryDroppedOrCorrupted(droppedCount, corruptedCount);
                CrashTelemetryBuffer.ReportSignalLaneStats(
                    telemetry.LaneHash,
                    queuedBeforeFlush,
                    snapshotCount,
                    droppedOrCorruptedCount);

                if (!critical)
                    sampledNonCritical++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (stormDetected)
                    Hecton8.Core.H8Debug.LogWarning("[SIGNAL STORM DETECTED]");
#endif
            }

            int nextIndex = startIndex + SignalTelemetryLaneBudgetPerFrame;
            if (nextIndex >= laneCount)
                nextIndex %= laneCount;

            Volatile.Write(ref _signalTelemetryCursor, nextIndex);
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            SignalTelemetryRingBuffer.ReportFrame(
                frame,
                pushedSignals,
                peakSignals,
                coalescedSignals,
                droppedSignals,
                corruptedSignals,
                laneCount,
                SignalBusRegistry.GlobalQualityMilli,
                SignalBusRegistry.SystemStressMilli);
            if (pushedSignals > 0 &&
                droppedSignals > (pushedSignals >> 1) &&
                ShouldDumpSignalDropStorm(frame))
            {
                SignalTelemetryRingBuffer.RequestDumpToDiskAsync();
            }

            int previousCorrupted = Volatile.Read(ref _signalTelemetryLastCorruptedTotal);
            if (corruptedSignals > previousCorrupted)
            {
                if (ShouldDumpSignalCorruption(frame))
                    SignalTelemetryRingBuffer.RequestDumpToDiskAsync();

                Volatile.Write(ref _signalTelemetryLastCorruptedTotal, corruptedSignals);
            }
        }

        private static int DecodeSignalLaneTelemetryPushed(in SignalLaneTelemetry telemetry)
        {
            uint packed = (uint)(telemetry.Reserved2 & uint.MaxValue);
            if (packed != 0u)
                return packed > int.MaxValue ? int.MaxValue : (int)packed;

            return math.max(0, telemetry.SnapshotCount + telemetry.DroppedCount + telemetry.CoalescedCount);
        }

        private static int DecodeSignalLaneTelemetryCorrupted(in SignalLaneTelemetry telemetry)
        {
            uint packed = (uint)(telemetry.Reserved2 >> 32);
            return packed > int.MaxValue ? int.MaxValue : (int)packed;
        }

        private static int DecodeSignalLaneTelemetryDroppedOrCorrupted(int droppedCount, int corruptedCount)
        {
            if (droppedCount < 0)
                droppedCount = 0;
            if (corruptedCount < 0)
                corruptedCount = 0;

            int headroom = int.MaxValue - droppedCount;
            return corruptedCount > headroom ? int.MaxValue : droppedCount + corruptedCount;
        }

        private static bool ShouldDumpSignalDropStorm(int frame)
        {
            return ShouldDumpSignalBlackBox(ref _signalTelemetryLastDropStormDumpFrame, frame);
        }

        private static bool ShouldDumpSignalCorruption(int frame)
        {
            return ShouldDumpSignalBlackBox(ref _signalTelemetryLastCorruptionDumpFrame, frame);
        }

        private static bool ShouldDumpSignalBlackBox(ref int lastDumpFrame, int frame)
        {
            int lastFrame = Volatile.Read(ref lastDumpFrame);
            if (lastFrame > 0 &&
                unchecked((uint)(frame - lastFrame)) < SignalTelemetryRingBufferCapacity)
            {
                return false;
            }

            Volatile.Write(ref lastDumpFrame, frame);
            return true;
        }

        private static uint ComputeStableSignalLaneHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldEntityIdToSourceId(ulong entityId)
        {
            return RuntimeOriginRoute.FoldEntityIdToSourceId(entityId);
        }

        private static void InitializeCategorySignalLanes()
        {
            SignalBus<InputStateSignal>.Configure(InputStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InputStateSignal)));
            SignalBus<InputStateSignal>.EnsureInitialized();
            SignalBus<PlayerInputSignal>.Configure(PlayerInputSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerInputSignal)));
            SignalBus<PlayerInputSignal>.EnsureInitialized();
            SignalBus<PlayerLookTargetSignal>.Configure(PlayerLookTargetSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerLookTargetSignal)));
            SignalBus<PlayerLookTargetSignal>.EnsureInitialized();
            SignalBus<CombatDamageSignal>.Configure(
                CombatDamageSignal.ExpectedCapacity,
                maxFrameSignals: CombatDamageSignal.MaxFrameSignals,
                lowTierFrameSignals: CombatDamageSignal.LowTierFrameSignals,
                laneHash: CombatDamageSignal.LaneHash);
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<ImpactSignal>.Configure(
                ImpactSignal.ExpectedCapacity,
                maxFrameSignals: ImpactSignal.MaxFrameSignals,
                lowTierFrameSignals: ImpactSignal.LowTierFrameSignals,
                laneHash: ImpactSignal.LaneHash);
            SignalBus<ImpactSignal>.EnsureInitialized();
            SignalBus<HullDeformedSignal>.Configure(
                HullDeformedSignal.ExpectedCapacity,
                maxFrameSignals: HullDeformedSignal.MaxFrameSignals,
                lowTierFrameSignals: HullDeformedSignal.LowTierFrameSignals,
                laneHash: HullDeformedSignal.LaneHash);
            SignalBus<HullDeformedSignal>.EnsureInitialized();
            SignalBus<HullRepairedSignal>.Configure(
                HullRepairedSignal.ExpectedCapacity,
                maxFrameSignals: HullRepairedSignal.MaxFrameSignals,
                lowTierFrameSignals: HullRepairedSignal.LowTierFrameSignals,
                laneHash: HullRepairedSignal.LaneHash);
            SignalBus<HullRepairedSignal>.EnsureInitialized();
            SignalBus<PlayerBaseEnterSignal>.Configure(PlayerBaseTransitionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerBaseEnterSignal)));
            SignalBus<PlayerBaseEnterSignal>.EnsureInitialized();
            SignalBus<PlayerBaseExitSignal>.Configure(PlayerBaseTransitionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerBaseExitSignal)));
            SignalBus<PlayerBaseExitSignal>.EnsureInitialized();
            SignalBus<HighSpeedImpactSignal>.Configure(
                HighSpeedImpactSignal.ExpectedCapacity,
                maxFrameSignals: HighSpeedImpactSignal.MaxFrameSignals,
                lowTierFrameSignals: HighSpeedImpactSignal.LowTierFrameSignals,
                laneHash: HighSpeedImpactSignal.LaneHash);
            SignalBus<HighSpeedImpactSignal>.EnsureInitialized();
            SignalBus<AupPreShiftSignal>.Configure(AupPreShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AupPreShiftSignal)));
            SignalBus<AupPreShiftSignal>.EnsureInitialized();
            SignalBus<AupShiftSignal>.Configure(AupShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AupShiftSignal)));
            SignalBus<AupShiftSignal>.EnsureInitialized();
            SignalBus<RebaseSignal>.Configure(RebaseSignalCapacity, maxFrameSignals: RebaseSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(RebaseSignal)));
            SignalBus<RebaseSignal>.EnsureInitialized();
            SignalBus<EntityDeathSignal>.Configure(EntityDeathSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(EntityDeathSignal)));
            SignalBus<EntityDeathSignal>.EnsureInitialized();
            SignalBus<EntitySpawnSignal>.Configure(EntitySpawnSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(EntitySpawnSignal)));
            SignalBus<EntitySpawnSignal>.EnsureInitialized();
            SignalBus<FaunaStateChangedSignal>.Configure(FaunaStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(FaunaStateChangedSignal)));
            SignalBus<FaunaStateChangedSignal>.EnsureInitialized();
            SignalBus<WakeGeneratedSignal>.Configure(WakeGeneratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WakeGeneratedSignal)));
            SignalBus<WakeGeneratedSignal>.EnsureInitialized();
            SignalBus<MemoryPressureSignal>.Configure(MemoryPressureSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MemoryPressureSignal)));
            SignalBus<MemoryPressureSignal>.EnsureInitialized();
            SignalBus<HapticRequest>.Configure(HapticRequestCapacity, laneHash: ComputeStableSignalLaneHash(nameof(HapticRequest)));
            SignalBus<HapticRequest>.EnsureInitialized();
            SignalBus<HapticPulseSignal>.Configure(HapticPulseSignalCapacity, maxFrameSignals: HapticPulseSignalCapacity, lowTierFrameSignals: 1, laneHash: HapticPulseSignal.LaneHash);
            SignalBus<HapticPulseSignal>.EnsureInitialized();
            SignalBus<ThermalStateChangedSignal>.Configure(ThermalStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ThermalStateChangedSignal)));
            SignalBus<ThermalStateChangedSignal>.EnsureInitialized();
            SignalBus<BatteryLevelSignal>.Configure(BatteryLevelSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BatteryLevelSignal)));
            SignalBus<BatteryLevelSignal>.EnsureInitialized();
            SignalBus<PlayerStateSignal>.Configure(PlayerStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerStateSignal)));
            SignalBus<PlayerStateSignal>.EnsureInitialized();
            SignalBus<SurvivalVitalsChangedSignal>.Configure(SurvivalVitalsChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SurvivalVitalsChangedSignal)));
            SignalBus<SurvivalVitalsChangedSignal>.EnsureInitialized();
            SignalBus<PlayerStressSignal>.Configure(PlayerStressSignalCapacity, maxFrameSignals: PlayerStressSignalCapacity, lowTierFrameSignals: 32, laneHash: ComputeStableSignalLaneHash(nameof(PlayerStressSignal)));
            SignalBus<PlayerStressSignal>.EnsureInitialized();
            SignalBus<DropPodLandedSignal>.Configure(DropPodLandedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(DropPodLandedSignal)));
            SignalBus<DropPodLandedSignal>.EnsureInitialized();
            SignalBus<CameraPositionSignal>.Configure(CameraPositionSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CameraPositionSignal)));
            SignalBus<CameraPositionSignal>.EnsureInitialized();
            SignalBus<CameraFrustumSignal>.Configure(CameraFrustumSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CameraFrustumSignal)));
            SignalBus<CameraFrustumSignal>.EnsureInitialized();
            SignalBus<WeatherChangedSignal>.Configure(WeatherStrengthSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WeatherChangedSignal)));
            SignalBus<WeatherChangedSignal>.EnsureInitialized();
            SignalBus<SystemPauseSignal>.Configure(SimulationPauseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SystemPauseSignal)));
            SignalBus<SystemPauseSignal>.EnsureInitialized();
            SignalBus<SimulationBucketSyncSignal>.Configure(SimulationBucketSyncSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SimulationBucketSyncSignal)));
            SignalBus<SimulationBucketSyncSignal>.EnsureInitialized();
            SignalBus<InputSignal>.Configure(DeterminismInputSignalCapacity, maxFrameSignals: DeterminismInputSignalCapacity, lowTierFrameSignals: DeterminismInputSignalCapacity, laneHash: 0x5048494Eu);
            SignalBus<InputSignal>.EnsureInitialized();
            SignalBus<StateCorrectionSignal>.Configure(DeterminismStateCorrectionSignalCapacity, maxFrameSignals: DeterminismStateCorrectionSignalCapacity, lowTierFrameSignals: DeterminismStateCorrectionSignalCapacity, laneHash: 0x50485343u);
            SignalBus<StateCorrectionSignal>.EnsureInitialized();
            SignalBus<DesyncDetectedSignal>.Configure(DeterminismDesyncDetectedSignalCapacity, maxFrameSignals: DeterminismDesyncDetectedSignalCapacity, lowTierFrameSignals: DeterminismDesyncDetectedSignalCapacity, laneHash: 0x50484453u);
            SignalBus<DesyncDetectedSignal>.EnsureInitialized();
            SignalBus<SyncFenceSignal>.Configure(DeterminismSyncFenceSignalCapacity, maxFrameSignals: DeterminismSyncFenceSignalCapacity, lowTierFrameSignals: DeterminismSyncFenceSignalCapacity, laneHash: 0x50485346u);
            SignalBus<SyncFenceSignal>.EnsureInitialized();
            SignalBus<KccVelocitySignal>.Configure(DeterminismKccVelocitySignalCapacity, maxFrameSignals: DeterminismKccVelocitySignalCapacity, lowTierFrameSignals: DeterminismKccVelocitySignalCapacity, laneHash: 0x50484B56u);
            SignalBus<KccVelocitySignal>.EnsureInitialized();
            SignalBus<LockstepSnapshotSignal>.Configure(16, maxFrameSignals: 16, lowTierFrameSignals: 16, laneHash: 0x4C535348u);
            SignalBus<LockstepSnapshotSignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.Configure(8, maxFrameSignals: 8, lowTierFrameSignals: 8, laneHash: 0x5359474Cu);
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
            SignalBus<LaserCutterEventPayload>.Configure(LaserCutterEventSignalCapacity, maxFrameSignals: LaserCutterEventSignalCapacity, lowTierFrameSignals: LaserCutterEventSignalCapacity, laneHash: 0x4C435554u);
            SignalBus<LaserCutterEventPayload>.EnsureInitialized();
            SignalBus<FramePacingWarningSignal>.Configure(FramePacingWarningSignalCapacity, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: ComputeStableSignalLaneHash(nameof(FramePacingWarningSignal)));
            SignalBus<FramePacingWarningSignal>.EnsureInitialized();
            SignalBus<MovementAcousticSignal>.Configure(MovementAcousticSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MovementAcousticSignal)));
            SignalBus<MovementAcousticSignal>.EnsureInitialized();
            SignalBus<AcousticZoneChangedEvent>.Configure(
                AcousticZoneChangedSignalCapacity,
                maxFrameSignals: 8,
                lowTierFrameSignals: AcousticZoneChangedSignalCapacity,
                laneHash: ComputeStableSignalLaneHash(nameof(AcousticZoneChangedEvent)));
            SignalBus<AcousticZoneChangedEvent>.EnsureInitialized();
            SignalBus<DirectorAIMusicSignal>.Configure(
                DirectorAIMusicSignalCapacity,
                maxFrameSignals: DirectorAIMusicSignalCapacity,
                lowTierFrameSignals: 8,
                laneHash: ComputeStableSignalLaneHash(nameof(DirectorAIMusicSignal)));
            SignalBus<DirectorAIMusicSignal>.EnsureInitialized();
            SignalBus<global::Hecton8.Core.Contracts.Signals.AudioEvent>.Configure(
                16,
                maxFrameSignals: 16,
                lowTierFrameSignals: 16,
                laneHash: 0x41554445u);
            SignalBus<global::Hecton8.Core.Contracts.Signals.AudioEvent>.EnsureInitialized();
            SignalBus<BiomeChangedSignal>.Configure(BiomeChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BiomeChangedSignal)));
            SignalBus<BiomeChangedSignal>.EnsureInitialized();
            SignalBus<BiomeGradientSignal>.Configure(BiomeGradientSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(BiomeGradientSignal)));
            SignalBus<BiomeGradientSignal>.EnsureInitialized();
            SignalBus<DiegeticHudSignal>.Configure(DiegeticHudSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(DiegeticHudSignal)));
            SignalBus<DiegeticHudSignal>.EnsureInitialized();
            SignalBus<HUDNotificationSignal>.Configure(HUDNotificationSignalCapacity, maxFrameSignals: HUDNotificationSignalCapacity, lowTierFrameSignals: 64, laneHash: ComputeStableSignalLaneHash(nameof(HUDNotificationSignal)));
            SignalBus<HUDNotificationSignal>.EnsureInitialized();
            SignalBus<SaveCompletedSignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveCompletedSignal)));
            SignalBus<SaveCompletedSignal>.EnsureInitialized();
            SignalBus<SaveStatusSignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveStatusSignal)));
            SignalBus<SaveStatusSignal>.EnsureInitialized();
            SignalBus<SaveMetadataReadySignal>.Configure(SaveLifecycleSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SaveMetadataReadySignal)));
            SignalBus<SaveMetadataReadySignal>.EnsureInitialized();
            SignalBus<CpuStarvationSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(CpuStarvationSignal)));
            SignalBus<CpuStarvationSignal>.EnsureInitialized();
            SignalBus<LoreFragmentScannedSignal>.Configure(LoreFragmentScannedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(LoreFragmentScannedSignal)));
            SignalBus<LoreFragmentScannedSignal>.EnsureInitialized();
            SignalBus<ScannerToolActiveSignal>.Configure(ScannerToolActiveSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ScannerToolActiveSignal)));
            SignalBus<ScannerToolActiveSignal>.EnsureInitialized();
            SignalBus<MemoryAddressShiftSignal>.Configure(MemoryAddressShiftSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(MemoryAddressShiftSignal)));
            SignalBus<MemoryAddressShiftSignal>.EnsureInitialized();
            SignalBus<DataVaultUpdateSignal>.Configure(DataVaultUpdateSignalCapacity, maxFrameSignals: DataVaultUpdateSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(DataVaultUpdateSignal)));
            SignalBus<DataVaultUpdateSignal>.EnsureInitialized();
            SignalBus<PrefabAcousticSignatureSignal>.Configure(PrefabAcousticSignatureSignalCapacity, maxFrameSignals: PrefabAcousticSignatureSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(PrefabAcousticSignatureSignal)));
            SignalBus<PrefabAcousticSignatureSignal>.EnsureInitialized();
            SignalBus<PrefabLoreLinkSignal>.Configure(PrefabLoreLinkSignalCapacity, maxFrameSignals: PrefabLoreLinkSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(PrefabLoreLinkSignal)));
            SignalBus<PrefabLoreLinkSignal>.EnsureInitialized();
            SignalBus<ResolutionChangedSignal>.Configure(ResolutionChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ResolutionChangedSignal)));
            SignalBus<ResolutionChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthIndexSignal>.Configure(SystemHealthIndexSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SystemHealthIndexSignal)));
            SignalBus<SystemHealthIndexSignal>.EnsureInitialized();
            SignalBus<StorageDebtSignal>.Configure(StorageDebtSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(StorageDebtSignal)));
            SignalBus<StorageDebtSignal>.EnsureInitialized();
            SignalBus<StreamingTurbulenceSignal>.Configure(StreamingTurbulenceSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(StreamingTurbulenceSignal)));
            SignalBus<StreamingTurbulenceSignal>.EnsureInitialized();
            SignalBus<AtmosphericReentrySignal>.Configure(AtmosphericReentrySignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(AtmosphericReentrySignal)));
            SignalBus<AtmosphericReentrySignal>.EnsureInitialized();
            SignalBus<PrologueCompleteSignal>.Configure(PrologueCompleteSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PrologueCompleteSignal)));
            SignalBus<PrologueCompleteSignal>.EnsureInitialized();
            SignalBus<ManualOverridePulledSignal>.Configure(ManualOverridePulledSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ManualOverridePulledSignal)));
            SignalBus<ManualOverridePulledSignal>.EnsureInitialized();
            SignalBus<SwarmDispersedSignal>.Configure(SwarmDispersedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SwarmDispersedSignal)));
            SignalBus<SwarmDispersedSignal>.EnsureInitialized();
            SignalBus<FluidImpulseSignal>.Configure(FluidImpulseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(FluidImpulseSignal)));
            SignalBus<FluidImpulseSignal>.EnsureInitialized();
            SignalBus<SplashEvent>.Configure(SplashEventSignalCapacity, maxFrameSignals: SplashEventSignalCapacity, lowTierFrameSignals: SplashEventSurvivalSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SplashEvent)));
            SignalBus<SplashEvent>.EnsureInitialized();
            SignalBus<PhysicsEventPayload>.Configure(PhysicsEventPayloadSignalCapacity, maxFrameSignals: PhysicsEventPayloadSignalCapacity, lowTierFrameSignals: PhysicsEventPayloadSurvivalSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PhysicsEventPayload)));
            SignalBus<PhysicsEventPayload>.EnsureInitialized();
            SignalBus<DeferredSubmarineImpactSignal>.Configure(DeferredSubmarineImpactSignalCapacity, maxFrameSignals: DeferredSubmarineImpactSignalCapacity, lowTierFrameSignals: DeferredSubmarineImpactSurvivalSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(DeferredSubmarineImpactSignal)));
            SignalBus<DeferredSubmarineImpactSignal>.EnsureInitialized();
            SignalBus<SubmarineFloodStateSignal>.Configure(SubmarineFloodStateSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(SubmarineFloodStateSignal)));
            SignalBus<SubmarineFloodStateSignal>.EnsureInitialized();
            SignalBus<MacroDatabaseSectorHydrationSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(MacroDatabaseSectorHydrationSignal)));
            SignalBus<MacroDatabaseSectorHydrationSignal>.EnsureInitialized();
            SignalBus<WfcOutpostGeneratedSignal>.Configure(WfcOutpostGeneratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostGeneratedSignal)));
            SignalBus<WfcOutpostGeneratedSignal>.EnsureInitialized();
            SignalBus<WfcOutpostStateChangedSignal>.Configure(128, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostStateChangedSignal)));
            SignalBus<WfcOutpostStateChangedSignal>.EnsureInitialized();
            SignalBus<WfcOutpostDoorPowerSignal>.Configure(WfcOutpostDoorPowerSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(WfcOutpostDoorPowerSignal)));
            SignalBus<WfcOutpostDoorPowerSignal>.EnsureInitialized();
            SignalBus<SectorResidencyHydratedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(SectorResidencyHydratedSignal)));
            SignalBus<SectorResidencyHydratedSignal>.EnsureInitialized();
            SignalBus<SectorDehydratedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(SectorDehydratedSignal)));
            SignalBus<SectorDehydratedSignal>.EnsureInitialized();
            SignalBus<ChunkDehydratedSignal>.Configure(ChunkDehydratedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ChunkDehydratedSignal)));
            SignalBus<ChunkDehydratedSignal>.EnsureInitialized();
            SignalBus<InventoryCommandSignal>.Configure(InventoryCommandSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InventoryCommandSignal)));
            SignalBus<InventoryCommandSignal>.EnsureInitialized();
            SignalBus<InventoryDeathLootCacheSignal>.Configure(
                InventoryDeathLootCacheSignalCapacity,
                maxFrameSignals: InventoryDeathLootCacheSignal.MaxFrameSignals,
                lowTierFrameSignals: InventoryDeathLootCacheSignal.LowTierFrameSignals,
                laneHash: InventoryDeathLootCacheSignal.LaneHash);
            SignalBus<InventoryDeathLootCacheSignal>.EnsureInitialized();
            SignalBus<InventoryChangedSignal>.Configure(InventoryChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(InventoryChangedSignal)));
            SignalBus<InventoryChangedSignal>.EnsureInitialized();
            SignalBus<ItemDurabilityChangedSignal>.Configure(ItemDurabilityChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ItemDurabilityChangedSignal)));
            SignalBus<ItemDurabilityChangedSignal>.EnsureInitialized();
            SignalBus<ItemLifecycleSignal>.Configure(ItemLifecycleSignalCapacity, maxFrameSignals: ItemLifecycleSignalCapacity, lowTierFrameSignals: 32, laneHash: ComputeStableSignalLaneHash(nameof(ItemLifecycleSignal)));
            SignalBus<ItemLifecycleSignal>.EnsureInitialized();
            SignalBus<ItemAcquiredSignal>.Configure(ItemAcquiredSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ItemAcquiredSignal)));
            SignalBus<ItemAcquiredSignal>.EnsureInitialized();
            SignalBus<RadiationDoseSignal>.Configure(RadiationDoseSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(RadiationDoseSignal)));
            SignalBus<RadiationDoseSignal>.EnsureInitialized();
            SignalBus<RadiationSourceSignal>.Configure(RadiationSourceSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(RadiationSourceSignal)));
            SignalBus<RadiationSourceSignal>.EnsureInitialized();
            SignalBus<ResourceDepletionDeltaSignal>.Configure(ResourceDepletionDeltaSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ResourceDepletionDeltaSignal)));
            SignalBus<ResourceDepletionDeltaSignal>.EnsureInitialized();
            SignalBus<TemperatureChangedSignal>.Configure(TemperatureChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(TemperatureChangedSignal)));
            SignalBus<TemperatureChangedSignal>.EnsureInitialized();
            SignalBus<ThermalSourceSignal>.Configure(ThermalSourceSignalCapacity, maxFrameSignals: ThermalSourceSignalCapacity, lowTierFrameSignals: 32, laneHash: ComputeStableSignalLaneHash(nameof(ThermalSourceSignal)));
            SignalBus<ThermalSourceSignal>.EnsureInitialized();
            SignalBus<CullingOverloadSignal>.Configure(CullingOverloadSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CullingOverloadSignal)));
            SignalBus<CullingOverloadSignal>.EnsureInitialized();
            SignalBus<CraftingCompletedSignal>.Configure(CraftingCompletedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(CraftingCompletedSignal)));
            SignalBus<CraftingCompletedSignal>.EnsureInitialized();
            SignalBus<ProgressionMetaSignal>.Configure(ProgressionMetaSignalCapacity, maxFrameSignals: ProgressionMetaSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(ProgressionMetaSignal)));
            SignalBus<ProgressionMetaSignal>.EnsureInitialized();
            SignalBus<SessionLifecycleSignal>.Configure(SessionLifecycleSignalCapacity, maxFrameSignals: SessionLifecycleSignalCapacity, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(SessionLifecycleSignal)));
            SignalBus<SessionLifecycleSignal>.EnsureInitialized();
            SignalBus<ToolLoadoutChangedSignal>.Configure(ToolLoadoutChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ToolLoadoutChangedSignal)));
            SignalBus<ToolLoadoutChangedSignal>.EnsureInitialized();
            SignalBus<PlayerActionProgressSignal>.Configure(PlayerActionProgressSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionProgressSignal)));
            SignalBus<PlayerActionProgressSignal>.EnsureInitialized();
            SignalBus<PlayerActionCompletedSignal>.Configure(PlayerActionCompletedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionCompletedSignal)));
            SignalBus<PlayerActionCompletedSignal>.EnsureInitialized();
            SignalBus<PlayerActionCancelledSignal>.Configure(PlayerActionCancelledSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PlayerActionCancelledSignal)));
            SignalBus<PlayerActionCancelledSignal>.EnsureInitialized();
            SignalBus<ScanLogChangedSignal>.Configure(ScanLogChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(ScanLogChangedSignal)));
            SignalBus<ScanLogChangedSignal>.EnsureInitialized();
            SignalBus<PdaExchangeStateChangedSignal>.Configure(PdaExchangeStateChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(PdaExchangeStateChangedSignal)));
            SignalBus<PdaExchangeStateChangedSignal>.EnsureInitialized();
            SignalBus<VehicleUpgradesChangedSignal>.Configure(VehicleUpgradesChangedSignalCapacity, laneHash: ComputeStableSignalLaneHash(nameof(VehicleUpgradesChangedSignal)));
            SignalBus<VehicleUpgradesChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthSignal>.Configure(SystemHealthSignal.ExpectedCapacity, maxFrameSignals: SystemHealthSignal.MaxFrameSignals, lowTierFrameSignals: SystemHealthSignal.LowTierFrameSignals, laneHash: SystemHealthSignal.LaneHash);
            SignalBus<SystemHealthSignal>.EnsureInitialized();
            SignalBus<FrameTimeSignal>.Configure(FrameTimeSignal.ExpectedCapacity, maxFrameSignals: FrameTimeSignal.MaxFrameSignals, lowTierFrameSignals: FrameTimeSignal.LowTierFrameSignals, laneHash: FrameTimeSignal.LaneHash);
            SignalBus<FrameTimeSignal>.EnsureInitialized();
            SignalBus<KillSwitchSignal>.Configure(KillSwitchSignal.ExpectedCapacity, maxFrameSignals: KillSwitchSignal.MaxFrameSignals, lowTierFrameSignals: KillSwitchSignal.LowTierFrameSignals, laneHash: KillSwitchSignal.LaneHash);
            SignalBus<KillSwitchSignal>.EnsureInitialized();
            SignalBus<SystemKillSwitchBitsSignal>.Configure(SystemKillSwitchBitsSignal.ExpectedCapacity, maxFrameSignals: SystemKillSwitchBitsSignal.MaxFrameSignals, lowTierFrameSignals: SystemKillSwitchBitsSignal.LowTierFrameSignals, laneHash: SystemKillSwitchBitsSignal.LaneHash);
            SignalBus<SystemKillSwitchBitsSignal>.EnsureInitialized();
            SignalBus<ReentryVfxStateSignal>.Configure(ReentryVfxStateSignal.ExpectedCapacity, ReentryVfxStateSignal.MaxFrameSignals, ReentryVfxStateSignal.LowTierFrameSignals, ReentryVfxStateSignal.LaneHash);
            SignalBus<ReentryVfxStateSignal>.EnsureInitialized();
            SignalBus<VisorDropletSignal>.Configure(VisorDropletSignal.ExpectedCapacity, VisorDropletSignal.MaxFrameSignals, VisorDropletSignal.LowTierFrameSignals, VisorDropletSignal.LaneHash);
            SignalBus<VisorDropletSignal>.EnsureInitialized();
            SignalBus<PlayerFootstepSignal>.Configure(PlayerFootstepSignal.ExpectedCapacity, PlayerFootstepSignal.MaxFrameSignals, PlayerFootstepSignal.LowTierFrameSignals, PlayerFootstepSignal.LaneHash);
            SignalBus<PlayerFootstepSignal>.EnsureInitialized();
            SignalBus<PlayerWaterSplashSignal>.Configure(PlayerWaterSplashSignal.ExpectedCapacity, PlayerWaterSplashSignal.MaxFrameSignals, PlayerWaterSplashSignal.LowTierFrameSignals, PlayerWaterSplashSignal.LaneHash);
            SignalBus<PlayerWaterSplashSignal>.EnsureInitialized();
            SignalBus<WaterTransitionSignal>.Configure(WaterTransitionSignal.ExpectedCapacity, WaterTransitionSignal.MaxFrameSignals, WaterTransitionSignal.LowTierFrameSignals, WaterTransitionSignal.LaneHash);
            SignalBus<WaterTransitionSignal>.EnsureInitialized();
            SignalBus<PlayerExhaleSignal>.Configure(PlayerExhaleSignal.ExpectedCapacity, PlayerExhaleSignal.MaxFrameSignals, PlayerExhaleSignal.LowTierFrameSignals, PlayerExhaleSignal.LaneHash);
            SignalBus<PlayerExhaleSignal>.EnsureInitialized();
            SignalBus<PlayerSprintStateSignal>.Configure(PlayerSprintStateSignal.ExpectedCapacity, PlayerSprintStateSignal.MaxFrameSignals, PlayerSprintStateSignal.LowTierFrameSignals, PlayerSprintStateSignal.LaneHash);
            SignalBus<PlayerSprintStateSignal>.EnsureInitialized();
            SignalBus<PlayerFatalPressureSignal>.Configure(PlayerFatalPressureSignal.ExpectedCapacity, PlayerFatalPressureSignal.MaxFrameSignals, PlayerFatalPressureSignal.LowTierFrameSignals, PlayerFatalPressureSignal.LaneHash);
            SignalBus<PlayerFatalPressureSignal>.EnsureInitialized();
            SignalBus<PlayerTransportBailoutSignal>.Configure(PlayerTransportBailoutSignal.ExpectedCapacity, PlayerTransportBailoutSignal.MaxFrameSignals, PlayerTransportBailoutSignal.LowTierFrameSignals, PlayerTransportBailoutSignal.LaneHash);
            SignalBus<PlayerTransportBailoutSignal>.EnsureInitialized();
            SignalBus<VisualFlareSignal>.Configure(VisualFlareSignal.ExpectedCapacity, VisualFlareSignal.MaxFrameSignals, VisualFlareSignal.LowTierFrameSignals, VisualFlareSignal.LaneHash);
            SignalBus<VisualFlareSignal>.EnsureInitialized();
            SignalBus<BrownoutSignal>.Configure(BrownoutSignalCapacity, maxFrameSignals: BrownoutSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(BrownoutSignal)));
            SignalBus<BrownoutSignal>.EnsureInitialized();
            SignalBus<DebrisSpawnSignal>.Configure(DebrisSpawnSignalCapacity, maxFrameSignals: DebrisSpawnSignalCapacity, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(DebrisSpawnSignal)));
            SignalBus<DebrisSpawnSignal>.EnsureInitialized();
            ConfigureDebugSignalLane();
            SignalBus<SignalWardenMockDamageSignal>.Configure(16, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(SignalWardenMockDamageSignal)));
            SignalBus<SignalWardenMockDamageSignal>.EnsureInitialized();
            SignalBus<MockPlayerFootstepSignal>.Configure(16, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(MockPlayerFootstepSignal)));
            SignalBus<MockPlayerFootstepSignal>.EnsureInitialized();
            SignalBus<MockRockCollisionSignal>.Configure(64, maxFrameSignals: 128, lowTierFrameSignals: 16, laneHash: ComputeStableSignalLaneHash(nameof(MockRockCollisionSignal)));
            SignalBus<MockRockCollisionSignal>.EnsureInitialized();
            SignalBus<MacroCollisionSignal>.Configure(16, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(MacroCollisionSignal)));
            SignalBus<MacroCollisionSignal>.EnsureInitialized();
            SignalBus<WakeRequestSignal>.Configure(16, maxFrameSignals: 16, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(WakeRequestSignal)));
            SignalBus<WakeRequestSignal>.EnsureInitialized();
            SignalBus<TetherTensionSignal>.ConfigureCacheLineCritical(128, laneHash: ComputeStableSignalLaneHash(nameof(TetherTensionSignal)));
            SignalBus<TetherTensionSignal>.EnsureInitialized();
            SignalBus<TetherSnappedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(TetherSnappedSignal)));
            SignalBus<TetherSnappedSignal>.EnsureInitialized();
            SignalBus<TetherFiredSignal>.Configure(16, maxFrameSignals: 16, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(TetherFiredSignal)));
            SignalBus<TetherFiredSignal>.EnsureInitialized();
            SignalBus<VoxelCarveEvent>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(VoxelCarveEvent)));
            SignalBus<VoxelCarveEvent>.EnsureInitialized();
            SignalBus<DockingRequestSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingRequestSignal)));
            SignalBus<DockingRequestSignal>.EnsureInitialized();
            SignalBus<DockingCompleteSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingCompleteSignal)));
            SignalBus<DockingCompleteSignal>.EnsureInitialized();
            SignalBus<DockingFailedSignal>.Configure(64, laneHash: ComputeStableSignalLaneHash(nameof(DockingFailedSignal)));
            SignalBus<DockingFailedSignal>.EnsureInitialized();
        }

        private static void ConfigureDebugSignalLane()
        {
            SignalBus<DebugSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 8, laneHash: ComputeStableSignalLaneHash(nameof(DebugSignal)));
            SignalBus<DebugSignal>.EnsureInitialized();
        }

        private static void RegisterLegacyLane<T>(int expectedCapacity, string label)
            where T : unmanaged, ISignal
        {
            int maxFrameSignals = Math.Max(1, expectedCapacity);
            int lowTierFrameSignals = ResolveLegacyLowTierFrameSignals(maxFrameSignals);
            SignalBus<T>.Configure(
                expectedCapacity,
                maxFrameSignals: maxFrameSignals,
                lowTierFrameSignals: lowTierFrameSignals,
                laneHash: ComputeStableSignalLaneHash(label));
            SignalBus<T>.EnsureInitialized();
        }

        private static int ResolveLegacyLowTierFrameSignals(int maxFrameSignals)
        {
            return Math.Max(1, Math.Min(maxFrameSignals, maxFrameSignals >> 2));
        }

        private static void ClearLatestSignals()
        {
            _latestDamageSignal = default;
            _latestAcousticPingSignal = default;
            _latestFluidDensityChangedSignal = default;
            _latestLightLevelSignal = default;
            _latestPhysiologyStateSignal = default;
            _latestPlayerStressSignal = default;
            _latestPlayerStateSignal = default;
            _latestSeismicSignal = default;
            _latestScannerToolActiveSignal = default;
            _latestToolStateChangedSignal = default;
            SignalBridgeState.Reset();
            Volatile.Write(ref _latestStorageDebtMilli, 0);
            Volatile.Write(ref _latestStorageLatencyMilli, 0);
            Volatile.Write(ref _latestStorageDebtSequence, 0);
            Volatile.Write(ref _latestDamageSignalSequence, 0);
            Volatile.Write(ref _latestAcousticPingSignalSequence, 0);
            Volatile.Write(ref _latestFluidDensityChangedSignalSequence, 0);
            Volatile.Write(ref _latestLightLevelSignalSequence, 0);
            Volatile.Write(ref _latestPhysiologyStateSignalSequence, 0);
            Volatile.Write(ref _latestPlayerStressSignalSequence, 0);
            Volatile.Write(ref _latestPlayerStateSignalSequence, 0);
            Volatile.Write(ref _latestSeismicSignalSequence, 0);
            Volatile.Write(ref _latestScannerToolActiveSignalSequence, 0);
            Volatile.Write(ref _latestToolStateChangedSignalSequence, 0);
            Volatile.Write(ref _signalTelemetryCursor, 0);
            Volatile.Write(ref _signalTelemetryLastCorruptedTotal, 0);
            Volatile.Write(ref _signalTelemetryLastDropStormDumpFrame, 0);
            Volatile.Write(ref _signalTelemetryLastCorruptionDumpFrame, 0);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ValidateSignalPayload<T>(int expectedBytes)
            where T : unmanaged
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Hecton8.Core.H8Debug.LogError("[GlobalSignals] signal managed-reference violation.");

            ValidateSignalSize<T>(expectedBytes);
        }

        private static void ValidateSignalSize<T>(int expectedBytes)
            where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size != expectedBytes)
                Hecton8.Core.H8Debug.LogError("[GlobalSignals] signal size violation.");
        }
#endif
    }
}
