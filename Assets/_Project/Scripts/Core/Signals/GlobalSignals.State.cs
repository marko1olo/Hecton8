using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core
{
    public static partial class GlobalSignals
    {
        private const int BaseModuleCompromisedSignalCapacity = 64;
        private const int BaseStructuralWarningSignalCapacity = 64;
        private const int PlayerBaseTransitionSignalCapacity = 32;
        private const int HapticRequestCapacity = 64;
        private const int HapticPulseSignalCapacity = 8;
        private const int PlayerStateSignalCapacity = 64;
        private const int PlayerRespawnSignalCapacity = PlayerRespawnSignal.ExpectedCapacity;
        private const int SurvivalVitalsChangedSignalCapacity = 64;
        private const int AupPreShiftSignalCapacity = 64;
        private const int AupShiftSignalCapacity = 64;
        private const int DropPodLandedSignalCapacity = 8;
        private const int BrownoutSignalCapacity = 64;
        private const int DebrisSpawnSignalCapacity = 128;
        private const int DeflectSignalCapacity = 128;
        private const int EntityDeathSignalCapacity = 64;
        private const int EntitySpawnSignalCapacity = 128;
        private const int InputStateSignalCapacity = 64;
        private const int PlayerInputSignalCapacity = 64;
        private const int PlayerLookTargetSignalCapacity = 64;
        private const int SolarFlareSignalCapacity = 16;
        private const int RebaseSignalCapacity = 64;
        private const int ControlSignalCapacity = 256;
        private const int AnomalySignalCapacity = 128;
        private const int TelemetryAnomalySignalCapacity = 128;
        private const int CrashTelemetrySignalCapacity = 64;
        private const int HabitatConstructionSignalCapacity = 64;
        private const int DeconstructRequestSignalCapacity = 64;
        private const int DeconstructResultSignalCapacity = 64;
        private const int ModuleDeconstructSignalCapacity = 64;
        private const int VitalWarningSignalCapacity = 32;
        private const int CrushWarningSignalCapacity = 32;
        private const int VocalWarningSignalCapacity = 64;
        private const int SubtitleSignalCapacity = 64;
        private const int DataReloadSignalCapacity = 32;
        private const int DataVaultUpdateSignalCapacity = 64;
        private const int PrefabAcousticSignatureSignalCapacity = 64;
        private const int PrefabLoreLinkSignalCapacity = 64;
        private const int MemoryPressureSignalCapacity = 16;
        private const int SignalTelemetryRingBufferCapacity = 300;

        public static AbsoluteUniversePosition CurrentRuntimeOriginAup()
        {
            return RuntimeOriginRoute.CurrentRuntimeOriginAup();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryRuntimePositionToAup(Vector3 runtimePosition, ref AbsoluteUniversePosition aup)
        {
            return RuntimeOriginRoute.TryRuntimePositionToAup(runtimePosition, ref aup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryRuntimePositionToAup(float3 runtimePosition, ref AbsoluteUniversePosition aup)
        {
            return RuntimeOriginRoute.TryRuntimePositionToAup(runtimePosition, ref aup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte EncodeSignalQualityWeightByte(float qualityWeight01)
        {
            float sanitized = math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 0f;
            return (byte)math.clamp((int)math.round(sanitized * byte.MaxValue), 0, byte.MaxValue);
        }
        private const int MemoryAddressShiftSignalCapacity = 64;
        private const int ResolutionChangedSignalCapacity = 16;
        private const int SystemHealthIndexSignalCapacity = 16;
        private const int ScalabilityChangedSignalCapacity = 4;
        private const int AcousticPingSignalCapacity = 64;
        private const int MovementAcousticSignalCapacity = 128;
        private const int AcousticZoneChangedSignalCapacity = 4;
        private const int DirectorAIMusicSignalCapacity = 32;
        private const int DynamicMusicScalarSignalCapacity = 32;
        private const int SwarmDispersedSignalCapacity = 64;
        private const int SonarPingSignalCapacity = 64;
        private const int HypoxiaSignalCapacity = 32;
        private const int OxygenCriticalSignalCapacity = 32;
        private const int InteractionUiSignalCapacity = 128;
        private const int UIRescaleRequestSignalCapacity = 64;
        private const int FluidIncursionSignalCapacity = 64;
        private const int FluidDensityChangedSignalCapacity = 64;
        private const int SubmarineFloodStateSignalCapacity = 64;
        private const int PipeRuptureSignalCapacity = 64;
        private const int SpectrumScanSignalCapacity = 128;
        private const int RigidbodySleepSignalCapacity = 128;
        private const int ScannerToolActiveSignalCapacity = 64;
        private const int ScanCompleteSignalCapacity = 128;
        private const int LoreFragmentScannedSignalCapacity = 128;
        private const int BlueprintUnlockedSignalCapacity = 128;
        private const int CraftingStartedSignalCapacity = 128;
        private const int CraftingCompletedSignalCapacity = 128;
        private const int ToolStateChangedSignalCapacity = 64;
        private const int ToolLoadoutChangedSignalCapacity = 64;
        private const int ToolAcousticSignalCapacity = 128;
        private const int PowerDrainSignalCapacity = 128;
        private const int ToolTriggerSignalCapacity = 128;
        private const int HUDNotificationSignalCapacity = 128;
        private const int ThermalStateChangedSignalCapacity = 32;
        private const int BatteryLevelSignalCapacity = 32;
        private const int ReconDataSignalCapacity = 128;
        private const int SaveLifecycleSignalCapacity = 16;
        private const int WfcOutpostGeneratedSignalCapacity = 16;
        private const int WfcOutpostDoorPowerSignalCapacity = 64;
        private const int ComplianceViolationSignalCapacity = 64;
        private const int GlobalTimeSyncSignalCapacity = 16;
        private const int SeismicSignalCapacity = 64;
        private const int TimeDilationSignalCapacity = 32;
        private const int SimulationPauseSignalCapacity = 32;
        private const int SimulationBucketSyncSignalCapacity = 8;
        private const int DeterminismInputSignalCapacity = 128;
        private const int DeterminismStateCorrectionSignalCapacity = 16;
        private const int DeterminismDesyncDetectedSignalCapacity = 16;
        private const int DeterminismSyncFenceSignalCapacity = 32;
        private const int DeterminismKccVelocitySignalCapacity = 32;
        private const int LaserCutterEventSignalCapacity = 16;
        private const int FramePacingWarningSignalCapacity = 8;
        private const int BulletTimeVisualSignalCapacity = 32;
        private const int WeatherStrengthSignalCapacity = 32;
        private const int CameraPositionSignalCapacity = 8;
        private const int CameraFrustumSignalCapacity = 8;
        private const int ChunkDehydratedSignalCapacity = 64;
        private const int ItemDecaySignalCapacity = 64;
        private const int InventoryCommandSignalCapacity = 16;
        private const int InventoryRespawnDeathAupSignalCapacity = InventoryRespawnDeathAupSignal.ExpectedCapacity;
        private const int InventoryDeathLootCacheSignalCapacity = InventoryDeathLootCacheSignal.ExpectedCapacity;
        private const int InventoryRespawnPenaltyResultSignalCapacity = InventoryRespawnPenaltyResultSignal.ExpectedCapacity;
        private const int InventoryChangedSignalCapacity = 64;
        private const int ItemDurabilityChangedSignalCapacity = 64;
        private const int ItemLifecycleSignalCapacity = 128;
        private const int ItemAcquiredSignalCapacity = 128;
        private const int RadiationDoseSignalCapacity = 64;
        private const int RadiationSourceSignalCapacity = 64;
        private const int TemperatureChangedSignalCapacity = 64;
        private const int ThermalSourceSignalCapacity = 128;
        private const int ResourceDepletionDeltaSignalCapacity = 64;
        private const int LightLevelSignalCapacity = 64;
        private const int SubmarineLightsChangedSignalCapacity = 64;
        private const int FaunaStateChangedSignalCapacity = 128;
        private const int PhysiologyStateSignalCapacity = 64;
        private const int PlayerStressSignalCapacity = 64;
        private const int TraumaSignalCapacity = 16;
        private const int WakeGeneratedSignalCapacity = 128;
        private const int FluidImpulseSignalCapacity = 32;
        private const int BubbleSpawnSignalCapacity = 64;
        private const int SplashEventSignalCapacity = 64;
        private const int SplashEventSurvivalSignalCapacity = 32;
        private const int PhysicsEventPayloadSignalCapacity = 128;
        private const int PhysicsEventPayloadSurvivalSignalCapacity = 64;
        private const int DeferredSubmarineImpactSignalCapacity = 32;
        private const int DeferredSubmarineImpactSurvivalSignalCapacity = 16;
        private const int ProgressionEventSignalCapacity = 128;
        private const int ProgressionMetaSignalCapacity = 64;
        private const int SessionLifecycleSignalCapacity = 16;
        private const int GlobalWorldStateSignalCapacity = 64;
        private const int BiomeChangedSignalCapacity = 64;
        private const int NarrativeFocusSignalCapacity = 64;
        private const int FocusBrokenSignalCapacity = 32;
        private const int MixerStateSignalCapacity = 32;
        private const int DiegeticHudSignalCapacity = 32;
        private const int NarrativeHudWaypointSignalCapacity = 64;
        private const int SoundscapeProfileSignalCapacity = 64;
        private const int NarrativePoiStateSignalCapacity = 64;
        private const int BiomeGradientSignalCapacity = 64;
        private const int StorageDebtSignalCapacity = 32;
        private const int StreamingTurbulenceSignalCapacity = 32;
        private const int AtmosphericReentrySignalCapacity = 32;
        private const int PrologueCompleteSignalCapacity = 8;
        private const int ManualOverridePulledSignalCapacity = 8;
        private const int CullingOverloadSignalCapacity = 16;
        private const int PlayerActionProgressSignalCapacity = 64;
        private const int PlayerActionCompletedSignalCapacity = 16;
        private const int PlayerActionCancelledSignalCapacity = 16;
        private const int ScanLogChangedSignalCapacity = 32;
        private const int PdaExchangeStateChangedSignalCapacity = 32;
        private const int VehicleUpgradesChangedSignalCapacity = 32;
        private const int SignalTelemetryLaneBudgetPerFrame = 4;
        private static bool _initialized;
        private static CombatDamageSignal _latestDamageSignal;
        private static AcousticPingSignal _latestAcousticPingSignal;
        private static FluidDensityChangedSignal _latestFluidDensityChangedSignal;
        private static LightLevelSignal _latestLightLevelSignal;
        private static PhysiologyStateSignal _latestPhysiologyStateSignal;
        private static PlayerStressSignal _latestPlayerStressSignal;
        private static PlayerStateSignal _latestPlayerStateSignal;
        private static SeismicSignal _latestSeismicSignal;
        private static ScannerToolActiveSignal _latestScannerToolActiveSignal;
        private static ToolStateChangedSignal _latestToolStateChangedSignal;
        private static int _latestStorageDebtMilli;
        private static int _latestStorageLatencyMilli;
        private static int _latestStorageDebtSequence;
        private static int _latestDamageSignalSequence;
        private static int _latestAcousticPingSignalSequence;
        private static int _latestFluidDensityChangedSignalSequence;
        private static int _latestLightLevelSignalSequence;
        private static int _latestPhysiologyStateSignalSequence;
        private static int _latestPlayerStressSignalSequence;
        private static int _latestPlayerStateSignalSequence;
        private static int _latestSeismicSignalSequence;
        private static int _latestScannerToolActiveSignalSequence;
        private static int _latestToolStateChangedSignalSequence;
        private static int _signalTelemetryCursor;
        private static int _signalTelemetryLastCorruptedTotal;
        private static int _signalTelemetryLastDropStormDumpFrame;
        private static int _signalTelemetryLastCorruptionDumpFrame;

        [global::System.Obsolete("Central GlobalSignals bridge-state read is retired. Use SimulationSignalRoute.TimeDilationScalar.", true)]
        public static float TimeDilationScalar => SignalBridgeState.TimeDilationScalar;

        [global::System.Obsolete("Central GlobalSignals bridge-state read is retired. Use SimulationSignalRoute.SimulationPaused.", true)]
        public static bool SimulationPaused => SignalBridgeState.SimulationPaused;

        public static float SystemStress01 => SignalBusRegistry.SystemStress01;

        [global::System.Obsolete("Central GlobalSignals bridge-state read is retired. Use SimulationSignalRoute.BulletTimeVisualIntensity01.", true)]
        public static float BulletTimeVisualIntensity01 => SignalBridgeState.BulletTimeVisualIntensity01;

        public static float LatestStorageDebt01 => math.saturate(Volatile.Read(ref _latestStorageDebtMilli) * 0.001f);

        public static float LatestStorageLatencyEwmaMs => math.max(0f, Volatile.Read(ref _latestStorageLatencyMilli));

        public static uint LatestStorageDebtSequence => unchecked((uint)Volatile.Read(ref _latestStorageDebtSequence));

        [global::System.Obsolete("Central GlobalSignals bridge-state read is retired. Use CraftingSignalRoute or SignalBus<CraftingCompletedSignal>.", true)]
        public static uint LatestCraftingCompletedSequence => SignalBridgeState.LatestCraftingCompletedSequence;

        [global::System.Obsolete("Central GlobalSignals bridge-state read is retired. Use CraftingSignalRoute.LatestCompletedUnitCount.", true)]
        public static uint LatestCraftingCompletedUnitCount => SignalBridgeState.LatestCraftingCompletedUnitCount;

        internal static void AdvanceSignalSequence(ref int sequence)
        {
            int next = unchecked(Volatile.Read(ref sequence) + 1);
            if (next == 0)
                next = 1;

            Volatile.Write(ref sequence, next);
        }
    }
}
