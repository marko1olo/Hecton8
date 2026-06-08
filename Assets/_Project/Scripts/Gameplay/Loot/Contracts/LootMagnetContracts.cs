using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Loot.Contracts
{
    public static class LootMagnetConstants
    {
        public const int DefaultMaxEntities = 4096;
        public const int MaxEntitiesHardCap = 8192;
        public const int TelemetryFrameCount = 300;
        public const int TelemetryEntrySizeBytes = 128;
        public const float AcquireDistanceMeters = 0.3f;
        public const float AcquireDistanceSq = AcquireDistanceMeters * AcquireDistanceMeters;
        public const float MinRsqrtDistanceSq = 0.0001f;
        public const float MinForceDistanceSq = 0.1f;
        public const float MinDistanceSq = MinRsqrtDistanceSq;
        public const float DefaultPullRadiusMeters = 8f;
        public const float DefaultPullStrength = 18f;
        public const float DefaultMaxVelocityMetersPerSecond = 12f;
        public const float MaxStablePullRadiusMeters = 64f;
        public const float MaxStablePullStrength = 256f;
        public const float MaxStableVelocityMetersPerSecond = 48f;
        public const float MaxIntegrationDeltaTimeSeconds = 0.05f;
        public const int PresentationSignalStride = 64;
        public const int MaxAcquisitionsPerFrame = 64;
        public const byte ScalabilityTierHysteresisSlowTicks = 4;
        public const int SurvivalAcousticSignalsPerFrame = 16;
        public const int StandardAcousticSignalsPerFrame = 48;
        public const int HighFidelityAcousticSignalsPerFrame = 56;
        public const int VisualOverkillAcousticSignalsPerFrame = 64;
        public const int LowTierAcousticSignalsPerFrame = SurvivalAcousticSignalsPerFrame;
        public const int DefaultAcousticSignalsPerFrame = StandardAcousticSignalsPerFrame;
        public const int HighTierAcousticSignalsPerFrame = HighFidelityAcousticSignalsPerFrame;
        public const int UltraTierAcousticSignalsPerFrame = VisualOverkillAcousticSignalsPerFrame;
        public const int SurvivalWakeSignalsPerFrame = 32;
        public const int StandardWakeSignalsPerFrame = 96;
        public const int HighFidelityWakeSignalsPerFrame = 112;
        public const int VisualOverkillWakeSignalsPerFrame = 128;
        public const int LowTierWakeSignalsPerFrame = SurvivalWakeSignalsPerFrame;
        public const int DefaultWakeSignalsPerFrame = StandardWakeSignalsPerFrame;
        public const int HighTierWakeSignalsPerFrame = HighFidelityWakeSignalsPerFrame;
        public const int UltraTierWakeSignalsPerFrame = VisualOverkillWakeSignalsPerFrame;
        public const float MotionVectorVelocityThresholdMetersPerSecond = 10f;
        public const float MotionVectorVelocityThresholdSq =
            MotionVectorVelocityThresholdMetersPerSecond * MotionVectorVelocityThresholdMetersPerSecond;
        public const float StressRadiusReductionThreshold01 = 0.8f;
        public const float StressRadiusMultiplier = 0.5f;
        public const double AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const double AupCellSizeSq = AupCellSizeMeters * AupCellSizeMeters;
        public const byte ItemSourceLootMagnet = ItemAcquiredSignalSourceKinds.LootMagnet;
        public const byte SignalFlagLootMagnet = 1;
        public const uint WakeSourceLootZip = 0x4C5A4950u;
        public const uint FluidImpulseSourceLootZip = 0x4C464C44u;
        public const float HighFidelityFluidImpulseRadiusMeters = 1.35f;
        public const float VisualOverkillFluidImpulseRadiusMeters = 2.25f;
        public const float HighTierFluidImpulseRadiusMeters = HighFidelityFluidImpulseRadiusMeters;
        public const float UltraTierFluidImpulseRadiusMeters = VisualOverkillFluidImpulseRadiusMeters;
        public const float HighFidelityFluidImpulseLifetimeSeconds = 0.35f;
        public const float VisualOverkillFluidImpulseLifetimeSeconds = 0.75f;
        public const float HighTierFluidImpulseLifetimeSeconds = HighFidelityFluidImpulseLifetimeSeconds;
        public const float UltraTierFluidImpulseLifetimeSeconds = VisualOverkillFluidImpulseLifetimeSeconds;
        public const uint ItemSnapSparkSpeciesHash = 0x4C53504Bu;
        public const byte ItemSnapSparkDebrisKind = 1;
        public const ushort ItemSnapSparkQuantity = 6;
    }

    public static class LootEntityFlags
    {
        public const uint Active = 1u << 0;
        public const uint IsLoot = 1u << 1;
        public const uint PullEnabled = 1u << 2;
        public const uint Pulling = 1u << 3;
        public const uint Acquired = 1u << 4;
        public const uint SurvivalPressureLerp = 1u << 5;
        public const uint LowTierLerp = SurvivalPressureLerp;
        public const uint DataOnlyDeathCache = 1u << 6;
        public const uint Bit_IsMagnetic = PullEnabled;
        public const uint Flag_Acquired = Acquired;
        public const uint NonFinite = 1u << 31;
    }

    public static class LootMagnetEventFlags
    {
        public const uint Acoustic = 1u << 0;
        public const uint Wake = 1u << 1;
        public const uint Acquired = 1u << 2;
    }

    public static class LootMagnetLayoutGuards
    {
        public static bool ValidateSignalEventLayout()
        {
            return UnsafeUtility.SizeOf<LootMagnetSignalEvent>() == 128 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.PositionAup)) == 0 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.Velocity)) == 48 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.ItemHash)) == 60 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.Quantity)) == 64 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.DistanceSq)) == 68 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.Frame)) == 72 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.Flags)) == 76 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.GeneticsMask)) == 80 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.QualityMilli)) == 88 &&
                   OffsetOf<LootMagnetSignalEvent>(nameof(LootMagnetSignalEvent.StateFlags)) == 90;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            var field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct LootMagnetSignalEvent
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public uint ItemHash;
        [FieldOffset(64)] public uint Quantity;
        [FieldOffset(68)] public float DistanceSq;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public ulong GeneticsMask;
        [FieldOffset(88)] public ushort QualityMilli;
        [FieldOffset(90)] public ushort StateFlags;
        [FieldOffset(92)] private uint _pad0;
        [FieldOffset(96)] private ulong _pad1;
        [FieldOffset(104)] private ulong _pad2;
        [FieldOffset(112)] private ulong _pad3;
        [FieldOffset(120)] private ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct LootMagnetTelemetryEntry
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PlayerAup;
        [FieldOffset(48)] public AbsoluteUniversePosition SampleLootAup;
        [FieldOffset(96)] public uint Frame;
        [FieldOffset(100)] public uint ActiveCount;
        [FieldOffset(104)] public uint ActiveLootPullsCount;
        [FieldOffset(108)] public uint AcquiredCount;
        [FieldOffset(112)] public uint FlagsHash;
        [FieldOffset(116)] public uint Flags;
        [FieldOffset(120)] public float PeakMagnetVelocity;
        [FieldOffset(124)] public uint Reserved;
    }

    public ref struct LootMagnetVaultViews
    {
        public NativeArray<AbsoluteUniversePosition> EntityAups;
        public NativeArray<uint> EntityFlags;
        public NativeArray<float3> EntityVelocities;
        public NativeArray<uint> EntityItemHashes;
        public NativeArray<ushort> EntityQuantities;
        public NativeArray<LootMagnetSignalEvent> SignalEvents;
        public NativeArray<LootMagnetTelemetryEntry> Telemetry;

        public static bool IsCreated(in LootMagnetVaultViews views)
        {
            return views.EntityAups.IsCreated &&
                   views.EntityFlags.IsCreated &&
                   views.EntityVelocities.IsCreated &&
                   views.EntityItemHashes.IsCreated &&
                   views.EntityQuantities.IsCreated &&
                   views.SignalEvents.IsCreated &&
                   views.Telemetry.IsCreated;
        }
    }
}
