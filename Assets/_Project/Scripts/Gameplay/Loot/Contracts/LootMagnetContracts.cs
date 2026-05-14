using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Loot.Contracts
{
    public static class LootMagnetConstants
    {
        public const int DefaultMaxEntities = 4096;
        public const int TelemetryFrameCount = 300;
        public const float AcquireDistanceMeters = 0.5f;
        public const float AcquireDistanceSq = AcquireDistanceMeters * AcquireDistanceMeters;
        public const float MinDistanceSq = 0.01f;
        public const float DefaultPullRadiusMeters = 8f;
        public const float DefaultPullStrength = 18f;
        public const float DefaultMaxVelocityMetersPerSecond = 12f;
        public const float MaxIntegrationDeltaTimeSeconds = 0.05f;
        public const int PresentationSignalStride = 64;
        public const int MaxAcquisitionsPerFrame = 64;
        public const double AupCellSizeMeters = 5000d;
        public const byte ItemSourceLootMagnet = 8;
        public const byte SignalFlagLootMagnet = 1;
        public const uint WakeSourceLootZip = 0x4C5A4950u;
    }

    public static class LootEntityFlags
    {
        public const uint Active = 1u << 0;
        public const uint IsLoot = 1u << 1;
        public const uint PullEnabled = 1u << 2;
        public const uint Pulling = 1u << 3;
        public const uint Acquired = 1u << 4;
        public const uint LowTierSnap = 1u << 5;
        public const uint NonFinite = 1u << 31;
    }

    public static class LootMagnetEventFlags
    {
        public const uint Acoustic = 1u << 0;
        public const uint Wake = 1u << 1;
        public const uint Acquired = 1u << 2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 80)]
    public struct LootMagnetSignalEvent
    {
        public AbsoluteUniversePosition PositionAup;
        public float3 Velocity;
        public uint ItemHash;
        public uint Quantity;
        public float DistanceSq;
        public uint Frame;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 120)]
    public struct LootMagnetTelemetryEntry
    {
        public AbsoluteUniversePosition PlayerAup;
        public AbsoluteUniversePosition SampleLootAup;
        public uint Frame;
        public uint ActiveCount;
        public uint AcquiredCount;
        public uint FlagsHash;
        public uint Flags;
    }
}
