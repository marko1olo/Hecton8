using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory.Layout;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct AbsoluteUniversePosition
    {
        public const int CellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;

        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float LocalX;
        [FieldOffset(28)]
        public float LocalY;
        [FieldOffset(32)]
        public float LocalZ;
        [FieldOffset(36)]
        private float _pad0;
        [FieldOffset(40)]
        private ulong _pad1;

        /// <summary>
        /// Converts the compact save-layout position into a 16-byte-aligned transfer payload for memcpy/blit lanes.
        /// </summary>
        /// <returns>Aligned AUP transfer payload.</returns>
        public AbsoluteUniversePositionBlit128 ToAlignedBlit()
        {
            return new AbsoluteUniversePositionBlit128
            {
                GridX = GridX,
                GridY = GridY,
                GridZ = GridZ,
                Local = new float4(LocalX, LocalY, LocalZ, 0f),
                Reserved = 0UL
            };
        }

        /// <summary>
        /// Reconstructs the compact save-layout AUP from an aligned transfer payload.
        /// </summary>
        /// <param name="aligned">Aligned transfer payload.</param>
        /// <returns>Compact AUP.</returns>
        public static AbsoluteUniversePosition FromAlignedBlit(in AbsoluteUniversePositionBlit128 aligned)
        {
            return new AbsoluteUniversePosition
            {
                GridX = aligned.GridX,
                GridY = aligned.GridY,
                GridZ = aligned.GridZ,
                LocalX = aligned.Local.x,
                LocalY = aligned.Local.y,
                LocalZ = aligned.Local.z
            };
        }

        public static AbsoluteUniversePosition FromRuntimePosition(Vector3 runtimePosition)
        {
            return FromAbsolutePosition(HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition));
        }

        public static AbsoluteUniversePosition FromAbsolutePosition(double3 absolutePosition)
        {
            double cellSize = CellSizeMeters;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);

            double originX = gridX * cellSize;
            double originY = gridY * cellSize;
            double originZ = gridZ * cellSize;

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolutePosition.x - originX),
                LocalY = (float)(absolutePosition.y - originY),
                LocalZ = (float)(absolutePosition.z - originZ)
            };
        }

        public double3 ToAbsoluteDouble3()
        {
            return AUPMath.ToAbsoluteDouble3(in this);
        }

        public double3 OffsetAbsoluteMeters(double3 deltaMeters)
        {
            return AUPMath.OffsetAbsoluteMeters(in this, deltaMeters);
        }

        public AbsoluteUniversePosition OffsetMeters(double3 deltaMeters)
        {
            return FromAbsolutePosition(OffsetAbsoluteMeters(deltaMeters));
        }

        public static double3 OffsetAbsoluteMeters(in AbsoluteUniversePosition origin, double3 deltaMeters)
        {
            return AUPMath.OffsetAbsoluteMeters(in origin, deltaMeters);
        }

        public static AbsoluteUniversePosition OffsetMeters(in AbsoluteUniversePosition origin, double3 deltaMeters)
        {
            return FromAbsolutePosition(OffsetAbsoluteMeters(in origin, deltaMeters));
        }

        public static AbsoluteUniversePosition WeightedAverage3(
            in AbsoluteUniversePosition a,
            in AbsoluteUniversePosition b,
            in AbsoluteUniversePosition c,
            double weight)
        {
            return FromAbsolutePosition(AUPMath.WeightedAbsoluteAverage3(in a, in b, in c, weight));
        }

        public float3 ToRuntimeFloat3()
        {
            double3 committedOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return AUPMath.ToRuntimeFloat3(
                in this,
                committedOffset);
        }

        /// <summary>
        /// Converts an AUP into camera-relative view space without truncating sector deltas to float first.
        /// </summary>
        /// <param name="position">World entity AUP.</param>
        /// <param name="cameraPosition">Camera AUP used as the local origin.</param>
        /// <returns>Camera-relative float position for rendering and culling.</returns>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static float3 ToCameraRelativeFloat3(in AbsoluteUniversePosition position, in AbsoluteUniversePosition cameraPosition)
        {
            return AUPMath.ResolveCameraRelative(in position, in cameraPosition);
        }

        public static int3 ResolveChunkId(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            double chunkSize = math.max(1, chunkSizeMeters);
            return new int3(
                (int)math.floor(absolutePosition.x / chunkSize),
                (int)math.floor(absolutePosition.y / chunkSize),
                (int)math.floor(absolutePosition.z / chunkSize));
        }

        public static double DistanceSq(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return AUPMath.AUPDistanceSq(in a, in b);
        }

        /// <summary>
        /// Resolves the AUP delta in meters with guard rails for impossible sector separation.
        /// </summary>
        /// <param name="a">Target AUP.</param>
        /// <param name="b">Origin AUP.</param>
        /// <returns>Meter delta from <paramref name="b"/> to <paramref name="a"/>.</returns>
        public static double3 DeltaMetersClamped(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return AUPMath.AUPDeltaClamped(in a, in b);
        }

        /// <summary>
        /// Computes a cheap AUP distance approximation without reducing grid-local deltas to float first.
        /// </summary>
        /// <param name="a">First AUP.</param>
        /// <param name="b">Second AUP.</param>
        /// <returns>Approximate distance in meters.</returns>
        public static double ApproximateDistanceMetersClamped(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return AUPMath.ApproximateAUPDistanceMetersClamped(in a, in b);
        }
    }

    /// <summary>
    /// 16-byte-aligned AUP transfer payload for network or memcpy lanes that require float4-friendly packing.
    /// </summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct AbsoluteUniversePositionBlit128
    {
        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float4 Local;
        [FieldOffset(40)]
        public ulong Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PersistentThermalVentRecord
    {
        public long RuntimeKey;
        public AbsoluteUniversePosition PositionAup;
        public float RadiusWS;
        public float HeightWS;
        public float UpdraftVelocity;
        public float HeatIntensity;
        public float SmokeDensity;
        public float CableRadiusWS;
    }

    [Flags]
    internal enum PersistentWorldItemFlags : byte
    {
        None = 0,
        Collected = 1 << 0,
        FloraDestroyed = 1 << 1,
        Deleted = 1 << 2,
        FloraSeedPending = 1 << 3,
        FloraSeedReady = 1 << 4,
        FloraStateOverride = 1 << 5,
        ResourceNodeDestroyed = 1 << 6,
        ResourceNodeMetamorphosed = 1 << 7
    }

    [Flags]
    internal enum PoolSlotStateFlags : byte
    {
        None = 0,
        Hydrated = 1 << 0,
        Settled = 1 << 1,
        Dirty = 1 << 2,
        Reserved = 1 << 3,
        HydrationQueued = 1 << 4,
        DehydrationQueued = 1 << 5,
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PoolSlotData
    {
        [FieldOffset(0)] public ulong BoundGuid;
        [FieldOffset(8)] public int3 AupCell;
        [FieldOffset(20)] public float3 LocalOffset;
        [FieldOffset(32)] public ushort HydrationFrame;
        [FieldOffset(34)] public byte RefCount;
        [FieldOffset(35)] public byte StateFlags;
        [FieldOffset(36)] public ushort StableFrames;
        [FieldOffset(38)] public ushort LastVisibleFrame;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EntityDataRecord
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 Position;
        [FieldOffset(48)] public int Quantity;
        [FieldOffset(52)] public float Integrity01;
        [FieldOffset(56)] public int InventoryHash;
        [FieldOffset(60)] public uint InstanceUid;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct ResourceNodeTombstoneRecord
    {
        [FieldOffset(0)]
        public ulong TombstoneId;
        [FieldOffset(8)]
        public uint InstanceUid;
        [FieldOffset(12)]
        public uint Reserved0;
        [FieldOffset(16)]
        public AbsoluteUniversePosition Position;
        [FieldOffset(64)]
        public int3 ChunkId;
        [FieldOffset(76)]
        public uint Reserved1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct PersistentWorldItemRecord
    {
        private const uint QuantityMask = 0x00FFFFFFu;
        private const int FlagsShift = 24;

        [FieldOffset(0)]
        public AbsoluteUniversePosition Position;
        [FieldOffset(48)]
        public ulong ItemPersistentIdHash;
        [FieldOffset(56)]
        public FixedString128Bytes ItemPersistentId;
        [FieldOffset(184)]
        public int3 ChunkId;
        [FieldOffset(196)]
        private uint _packedQuantityAndFlags;
        [FieldOffset(200)]
        public uint InstanceUid;
        [FieldOffset(204)] private uint _pad0;
        [FieldOffset(208)] private ulong _pad1;
        [FieldOffset(216)] private ulong _pad2;
        [FieldOffset(224)] private ulong _pad3;
        [FieldOffset(232)] private ulong _pad4;
        [FieldOffset(240)] private ulong _pad5;
        [FieldOffset(248)] private ulong _pad6;

        public int Quantity
        {
            get => (int)(_packedQuantityAndFlags & QuantityMask);
            set
            {
                uint clampedQuantity = value <= 0
                    ? 0u
                    : (uint)math.min(value, (int)QuantityMask);
                _packedQuantityAndFlags = (_packedQuantityAndFlags & ~QuantityMask) | clampedQuantity;
            }
        }

        public PersistentWorldItemFlags Flags
        {
            get => (PersistentWorldItemFlags)((_packedQuantityAndFlags >> FlagsShift) & 0xFFu);
            set => _packedQuantityAndFlags = (_packedQuantityAndFlags & QuantityMask) | ((uint)value << FlagsShift);
        }

        public bool IsCollected => (Flags & PersistentWorldItemFlags.Collected) != 0;

        public bool IsFloraDestroyed => (Flags & PersistentWorldItemFlags.FloraDestroyed) != 0;

        public bool IsDeleted => (Flags & PersistentWorldItemFlags.Deleted) != 0;

        public bool IsFloraSeedPending => (Flags & PersistentWorldItemFlags.FloraSeedPending) != 0;

        public bool IsFloraSeedReady => (Flags & PersistentWorldItemFlags.FloraSeedReady) != 0;

        public bool IsFloraStateOverride => (Flags & PersistentWorldItemFlags.FloraStateOverride) != 0;

        public bool IsResourceNodeDestroyed => (Flags & PersistentWorldItemFlags.ResourceNodeDestroyed) != 0;

        public bool IsResourceNodeMetamorphosed => (Flags & PersistentWorldItemFlags.ResourceNodeMetamorphosed) != 0;

        public void MarkCollected()
        {
            Flags |= PersistentWorldItemFlags.Collected;
        }

        public void MarkDeleted()
        {
            Flags |= PersistentWorldItemFlags.Deleted;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PersistentWorldDeltaRecord
    {
        private const uint PackedAxisMask = 0x3FFu;
        private const float PackedAxisScale = 1023f;

        [FieldOffset(0)]
        public int3 ChunkId;
        [FieldOffset(12)] private uint _pad0;
        [FieldOffset(16)]
        public ulong ItemPersistentIdHash;
        [FieldOffset(24)]
        public uint InstanceUid;
        [FieldOffset(28)]
        public uint PackedLocalPosition;
        [FieldOffset(32)]
        public ushort Quantity;
        [FieldOffset(34)]
        public byte ItemFlags;
        [FieldOffset(35)]
        public byte Reserved;
        [FieldOffset(36)] private uint _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;

        public bool IsDeleted => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.Deleted) != 0;

        public bool IsFloraSeedPending => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.FloraSeedPending) != 0;

        public bool IsFloraSeedReady => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.FloraSeedReady) != 0;

        public bool IsFloraStateOverride => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.FloraStateOverride) != 0;

        public bool IsResourceNodeDestroyed => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.ResourceNodeDestroyed) != 0;

        public bool IsResourceNodeMetamorphosed => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.ResourceNodeMetamorphosed) != 0;

        public bool IsValid => InstanceUid != 0u && (IsDeleted || (ItemPersistentIdHash != 0UL && Quantity > 0));

        public static PersistentWorldDeltaRecord FromRecord(in PersistentWorldItemRecord record, int chunkSizeMeters)
        {
            return new PersistentWorldDeltaRecord
            {
                ChunkId = record.ChunkId,
                ItemPersistentIdHash = record.ItemPersistentIdHash,
                InstanceUid = record.InstanceUid,
                PackedLocalPosition = PackLocalPosition(in record.Position, record.ChunkId, chunkSizeMeters),
                Quantity = (ushort)math.clamp(record.Quantity, 1, ushort.MaxValue),
                ItemFlags = (byte)record.Flags,
                Reserved = 0
            };
        }

        public PersistentWorldItemRecord ToRecord(int chunkSizeMeters)
        {
            AbsoluteUniversePosition position = UnpackPosition(chunkSizeMeters);
            return new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = ChunkId,
                ItemPersistentIdHash = ItemPersistentIdHash,
                ItemPersistentId = default,
                Quantity = math.max(1, Quantity),
                Flags = (PersistentWorldItemFlags)ItemFlags,
                InstanceUid = InstanceUid
            };
        }

        public static PersistentWorldDeltaRecord CreateDeletedTombstone(in PersistentWorldItemRecord record, int chunkSizeMeters)
        {
            PersistentWorldDeltaRecord tombstone = FromRecord(in record, chunkSizeMeters);
            if ((record.Flags & PersistentWorldItemFlags.ResourceNodeDestroyed) == 0)
                tombstone.ItemPersistentIdHash = 0UL;
            tombstone.Quantity = (ushort)math.clamp(record.Quantity > 0 ? record.Quantity : 1, 1, ushort.MaxValue);
            tombstone.ItemFlags = (byte)(record.Flags | PersistentWorldItemFlags.Deleted);
            return tombstone;
        }

        public AbsoluteUniversePosition UnpackPosition(int chunkSizeMeters)
        {
            UnpackLocalPosition(PackedLocalPosition, chunkSizeMeters, out float localX, out float localY, out float localZ);
            double3 chunkOrigin = new double3(
                ChunkId.x * (double)chunkSizeMeters,
                ChunkId.y * (double)chunkSizeMeters,
                ChunkId.z * (double)chunkSizeMeters);

            return AbsoluteUniversePosition.FromAbsolutePosition(chunkOrigin + new double3(localX, localY, localZ));
        }

        private static uint PackLocalPosition(in AbsoluteUniversePosition position, int3 chunkId, int chunkSizeMeters)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            double3 chunkOrigin = new double3(
                chunkId.x * (double)chunkSizeMeters,
                chunkId.y * (double)chunkSizeMeters,
                chunkId.z * (double)chunkSizeMeters);

            double maxLocal = math.max(0d, (double)chunkSizeMeters);
            float localX = (float)math.clamp(absolute.x - chunkOrigin.x, 0d, maxLocal);
            float localY = (float)math.clamp(absolute.y - chunkOrigin.y, 0d, maxLocal);
            float localZ = (float)math.clamp(absolute.z - chunkOrigin.z, 0d, maxLocal);
            float inverseChunkSize = 1f / math.max(1f, chunkSizeMeters);

            uint x = (uint)math.round(math.saturate(localX * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint y = (uint)math.round(math.saturate(localY * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint z = (uint)math.round(math.saturate(localZ * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            return x | (y << 10) | (z << 20);
        }

        private static void UnpackLocalPosition(uint packed, int chunkSizeMeters, out float localX, out float localY, out float localZ)
        {
            float chunkSize = math.max(1f, chunkSizeMeters);
            localX = ((packed & PackedAxisMask) / PackedAxisScale) * chunkSize;
            localY = (((packed >> 10) & PackedAxisMask) / PackedAxisScale) * chunkSize;
            localZ = (((packed >> 20) & PackedAxisMask) / PackedAxisScale) * chunkSize;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct PersistentWorldCompactDeltaRecord
    {
        [FieldOffset(0)] public uint PackedLocalPosition;
        [FieldOffset(4)] public uint InstanceUid;
        [FieldOffset(8)] public ushort Quantity;
        [FieldOffset(10)] public byte ItemFlags;
        [FieldOffset(11)] public byte Reserved;
        [FieldOffset(12)] public ushort ChunkIndex;
        [FieldOffset(14)] public ushort ItemHashIndex;

        public bool IsDeleted => ((PersistentWorldItemFlags)ItemFlags & PersistentWorldItemFlags.Deleted) != 0;

        public bool IsValid => InstanceUid != 0u && (IsDeleted || Quantity > 0);
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct TombstoneDecayCollectJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<PersistentWorldCompactDeltaRecord> DeltaRecords;
        [NoAlias] public NativeList<int> ExpiredDeltaIndices;
        public int CurrentDay;
        public int Threshold;
        public int TimeToLiveDays;

        public void Execute()
        {
            ExpiredDeltaIndices.Clear();
            int tombstoneCount = 0;
            for (int i = 0; i < DeltaRecords.Length; i++)
            {
                if (DeltaRecords[i].IsDeleted)
                    tombstoneCount++;
            }

            if (tombstoneCount <= Threshold)
                return;

            for (int i = DeltaRecords.Length - 1; i >= 0 && tombstoneCount > Threshold; i--)
            {
                PersistentWorldCompactDeltaRecord compactRecord = DeltaRecords[i];
                if (!compactRecord.IsDeleted)
                    continue;

                int tombstoneDay = compactRecord.Quantity > 0 ? compactRecord.Quantity : 1;
                if (CurrentDay - tombstoneDay <= TimeToLiveDays)
                    continue;

                if (ExpiredDeltaIndices.Length >= ExpiredDeltaIndices.Capacity)
                    return;

                ExpiredDeltaIndices.AddNoResize(i);
                tombstoneCount--;
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5850)]
    public sealed class PersistentWorldRegistry : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, ISceneTransitionWorldResidencyBridge, IRuntimeWatchdogWorldHealthBridge
    {
        private sealed class SectorOverrideState
        {
            public string TempPath;
            public string EntityStateTempPath;
            public float LastUnloadedTime;
            public bool IsResident;
        }

        private sealed class SectorEntityStateWriteWork
        {
            public long SectorHash;
            public string TempPath;
            public bool IsCompleted;
            public SaveBinaryStorage.IndexedSectorEntityStateWriteHandle WriteHandle;
        }

        private readonly struct SectorOverrideWriteResult
        {
            public readonly long SectorHash;
            public readonly string TempPath;
            public readonly string EntityStateTempPath;

            public SectorOverrideWriteResult(long sectorHash, string tempPath, string entityStateTempPath)
            {
                SectorHash = sectorHash;
                TempPath = tempPath;
                EntityStateTempPath = entityStateTempPath;
            }
        }

        private const int DefaultMaxTrackedItems = 16384;
        private const int DefaultChunkSizeMeters = 64;
        private const int DefaultHydrationRadius = 1;
        private const float DropScatterRadiusMeters = 0.55f;
        private const float ScatterDiagonal2 = 0.70710677f;
        private const float DropScatterMinLiftMeters = 0.06f;
        private const float DropScatterMaxLiftMeters = 0.22f;
        private const float PlatformVelocityInheritanceFallbackHalfX = 18f;
        private const float PlatformVelocityInheritanceFallbackHalfY = 12f;
        private const float PlatformVelocityInheritanceFallbackHalfZ = 45f;
        private const ushort DefaultItemQualityMilli = 1000;
        private const float DefaultItemQuality01 = 1f;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private const int InstanceUidTypeShift = 24;
        private const uint InstanceUidCounterMask = 0x00FFFFFFu;
        private const float HydrateRadiusMeters = 150f;
        private const uint FloraSpawnTimestampStateTypeMask = 0xFA000000u;
        private const float FloraSpawnTimestampQuantizationSeconds = 60f;
        private const float ModCoreProtectionRadiusMeters = 8f;
        private const float ModCoreProtectionRadiusSq = ModCoreProtectionRadiusMeters * ModCoreProtectionRadiusMeters;
        private const double HydrateRadiusSq = HydrateRadiusMeters * HydrateRadiusMeters;
        private const float DehydrateRadiusMeters = 160f;
        private const double DehydrateRadiusSq = DehydrateRadiusMeters * DehydrateRadiusMeters;
        private const float HydrationRescanDistanceMeters = 16f;
        private const double HydrationRescanDistanceSq = HydrationRescanDistanceMeters * HydrationRescanDistanceMeters;
        private const int MaxHydrationsPerFrame = 30;
        private const int MaxDehydrationsPerTick = 8;
        private const int MaxPendingEntityStateTempWrites = 4;
        private const int MaxEntityStateTempWriteCompletionsPerTick = 4;
        private const int PagedSectorWindowWidth = 3;
        private const int PagedSectorHashCount = PagedSectorWindowWidth * PagedSectorWindowWidth;
        private const int PagedSectorEdgeLengthMeters = 1000;
        private const long InvalidPagedSectorHash = long.MinValue;
        private const float PagedSectorLoadRadiusMeters = 1000f;
        private const float PagedSectorLoadRadiusSq = PagedSectorLoadRadiusMeters * PagedSectorLoadRadiusMeters;
        private const float SectorEvictionDistanceMeters = 1500f;
        private const float SectorOverrideCommitIntervalSeconds = 10f;
        private const float SectorOverrideCommitDelaySeconds = 300f;
        private const float FloraStateQuantizationScale = 255f;
        private const uint FaunaHibernationStateTypeMask = 0xF9000000u;
        private const uint WhaleFallStateTypeMask = 0xF8000000u;
        private const uint FaunaEggStateTypeMask = 0xF7000000u;
        private const int FaunaHibernationStateValueMask = 0x00FFFFFF;
        private const int FaunaStateFlagLargeThreat = 1 << 0;
        private const int FaunaStateFlagPredator = 1 << 1;
        private const int FaunaStateFlagsMask = FaunaStateFlagLargeThreat | FaunaStateFlagPredator;
        private const int FaunaSleepStartShift = 2;
        private const int FaunaSleepStartMaxEncoded = (1 << 22) - 1;
        private const float FaunaSleepStartQuantumSeconds = 0.25f;
        private const float WhaleFallDurationSeconds = 7200f;
        private const int MaxWhaleFallInfluenceScan = 64;
        private const int MaxApexMigrationVisitedUids = 256;
        private const int MaxPersistentThermalVentRecords = 16;
        private const int EcosystemFaunaRecordBirthLimitPerSectorPass = 4;
        private const float EcosystemFaunaCloneJitterRadiusMeters = 180f;
        private const float HibernatedApexPredationBasePower = 0.65f;
        private const float HibernatedPreyEscapeBasePower = 0.15f;
        private const ulong PoolGuidMixSalt = 11400714819323198485UL;
        private const long PersistentMemoryBudgetBytes = 10485760L;
        private const string MemoryBudgetOwnerName = "PersistentWorldRegistry";
        private const string LocalizedSectorCorruptionMessage = "CRITICAL ERROR: LOCALIZED DATA CORRUPTION. TERRAIN RE-INITIALIZED.";
        private const string IndexedSectorPagingDesiredHashesLabel = "indexedSectorPagingDesiredSectorHashes";
        private const string IndexedSectorPagingLoadedRecordsLabel = "indexedSectorPagingLoadedSectorRecords";
        private const string SectorOverrideSnapshotRecordsLabel = "sectorOverrideSnapshotRecords";
        private const string SectorOverrideEntityStatesLabel = "sectorOverrideEntityStates";
        private const string SectorEntityStateAsyncWriteStatesLabel = "sectorEntityStateAsyncWriteStates";
        private const string ResourceNodeTombstonePrefix = "resource_node_";
        private const string SectorOverridesDirectorySuffix = "_sector_overrides";
        private const string SectorOverrideTempFileSuffix = ".sectmp";
        private const string SectorEntityStateTempFileSuffix = ".estatmp";
        private const string UpperHexDigits = "0123456789ABCDEF";
        private const int TombstoneDecayThreshold = 1024;
        private const int TombstoneTimeToLiveDays = 3;
        private const double TombstoneInGameDaySeconds = 86400d;
        private const float TombstoneDecayFrostTickSeconds = 5f;
        private const int MaxTombstoneDecayAppliesPerLateFrame = 128;
        private static readonly int3 ApexFaunaTombstoneChunkId = new int3(int.MinValue, 0, 0);
        private static readonly long HydrationFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks;
        private static readonly long HydrationPerformanceWarningBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 5000L);
        private static readonly uint _hydrationApplyBudgetWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.HydrationApplyBudget"));
        private static readonly uint _hydrationApplyContextHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.HydrationApply"));
        private static readonly uint _sectorEntityStateThrottleWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.EntityStateCompressionThrottle"));
        private static readonly uint _sectorEntityStateQueueOverflowWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.EntityStateCompressionQueueOverflow"));
        private static int _nextInstanceUidCounter;

        [Header("Settings")]
        [SerializeField, Min(256)]
        [Tooltip("Hard ceiling for tracked dropped-item records. Native containers are pre-allocated to this count and never resized at runtime.")]
        private int maxTrackedItems = DefaultMaxTrackedItems;

        [SerializeField, Min(16)]
        [Tooltip("Chunk edge length in meters used by the dropped-item spatial hash.")]
        private int chunkSizeMeters = DefaultChunkSizeMeters;

        [SerializeField, Range(0, 2)]
        [Tooltip("How many chunk rings around the player stay hydrated as live pooled proxies.")]
        private int hydrationRadiusInChunks = DefaultHydrationRadius;

        [Header("Diagnostics")]
        [SerializeField] private int _debugTrackedRecordCount;
        [SerializeField] private int _debugHydratedRecordCount;
        [SerializeField] private int _debugSnapshotRecordCount;
        [SerializeField] private Vector3Int _debugPlayerChunk;
        private int _lastHydrationBudgetTelemetryFrame = int.MinValue;
        private int _lastEntityStateThrottleTelemetryFrame = int.MinValue;
        private int _lastEntityStateQueueOverflowTelemetryFrame = int.MinValue;
        private PersistentThermalVentRecord[] _activeThermalVents;
        private int _activeThermalVentCount;
        private int _activeThermalVentRevision;

        internal int ChunkSizeMeters => chunkSizeMeters;
        public int ActiveThermalVentCount => _activeThermalVentCount;
        public int ActiveThermalVentRevision => _activeThermalVentRevision;

        internal static ushort PackFloraStateOverride(float normalizedHealth, byte harvestState)
        {
            byte packedHealth = QuantizeFloraStateChannel(normalizedHealth);
            return (ushort)(packedHealth | (harvestState << 8));
        }

        internal static bool IsPristineFloraState(float normalizedHealth, float normalizedHeightScale)
        {
            return math.saturate(normalizedHealth) >= 0.9999f && math.saturate(normalizedHeightScale) >= 0.9999f;
        }

        internal static void UnpackFloraStateOverride(ushort packedState, out float normalizedHealth, out byte harvestState)
        {
            normalizedHealth = ((packedState & 0xFF) / FloraStateQuantizationScale);
            harvestState = (byte)((packedState >> 8) & 0xFF);
        }

        internal static int PackFloraSpawnTimestampMinutes(float spawnPlayTimeSeconds)
        {
            float clampedSeconds = math.max(0f, spawnPlayTimeSeconds);
            int quantizedMinutes = math.clamp((int)math.floor(clampedSeconds / FloraSpawnTimestampQuantizationSeconds), 0, ushort.MaxValue - 1);
            return quantizedMinutes + 1;
        }

        internal static float UnpackFloraSpawnTimestampSeconds(int packedMinutes)
        {
            int quantizedMinutes = math.max(0, packedMinutes - 1);
            return quantizedMinutes * FloraSpawnTimestampQuantizationSeconds;
        }

        private static byte QuantizeFloraStateChannel(float value)
        {
            return (byte)math.clamp(math.round(math.saturate(value) * FloraStateQuantizationScale), 0f, FloraStateQuantizationScale);
        }

        private NativeList<PersistentWorldItemRecord> _records;
        private NativeParallelMultiHashMap<int3, int> _recordsByChunk;
        private NativeList<PersistentWorldCompactDeltaRecord> _deltaRecords;
        private NativeHashMap<uint, int> _deltaRecordIndexByEntityId;
        private NativeParallelHashSet<uint> _deletedInstanceUids;
        private NativeParallelHashSet<ulong> _resourceNodeTombstoneIds;
        private NativeParallelHashSet<ulong> _resourceNodeMetamorphosedIds;
        private NativeHashMap<int3, ushort> _deltaChunkIndexByChunkId;
        private NativeList<int3> _deltaChunkIds;
        private NativeHashMap<ulong, ushort> _deltaItemIndexByHash;
        private NativeList<ulong> _deltaItemHashes;
        private NativeParallelMultiHashMap<uint, PersistentWorldCompactDeltaRecord> _deltaRecordsByChunk;
        private NativeList<int> _tombstoneDecayExpiredIndices;
        private NativeList<PersistentWorldDeltaRecord> _saveSnapshotDeltas;
        private NativeArray<PoolSlotData> _poolSlotData;
        private NativeHashMap<ulong, int> _guidToPoolIndex;
        private NativeHashMap<uint, EntityDataRecord> _entityStateByInstanceUid;
        private NativeHashMap<uint, EntityDataRecord> _floraSpawnStateByInstanceUid;
        private NativeHashMap<uint, float3> _spawnImpulseByInstanceUid;
        private NativeHashMap<uint, float3> _spawnVelocityChangeByInstanceUid;
        private NativeQueue<int> _dehydrateQueue;
        private NativeList<int> _pendingHydrationRecords;
        private GameObject[] _hydratedInstancesBySlot;
        private Transform[] _poolSlotTransforms;
        private Rigidbody[] _poolSlotRigidbodies;
        private Dictionary<int, GameObject> _hydratedInstancesByRecordIndex;
        private Dictionary<ulong, ItemData> _itemLookupByHash;
        private List<ItemData> _itemCatalogScratch;
        private List<int> _worldPrefabPrewarmHashScratch;
        private List<int> _worldPrefabReleaseScratch;
        private List<int> _recordIndexScratch;
        private List<EntityDataRecord> _entityStateScratch;
        private List<EntityDataRecord> _entityStateSectorTargetScratch;
        private List<EntityDataRecord> _floraSpawnStateScratch;
        private List<SectorEntityStateWriteWork> _pendingEntityStateTempWrites;
        private List<long> _dueSectorOverrideCommitHashes;
        private long[] _quarantinedSectorResetScratch;
        private uint[] _whaleFallPoiInstanceUids;
        private int _whaleFallPoiInstanceUidCount;
        private int _whaleFallPoiInstanceUidWriteCursor;
        private uint[] _apexMigrationVisitedUids;
        private int _apexMigrationVisitedUidCount;
        private Transform _playerTransform;
        private ItemCatalog _resolvedItemCatalog;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _hydrationSessionRunning;
        private bool _playerChunkValid;
        private bool _hasLastHydrationScanAup;
        private ushort _hydrationFrameCounter;
        private int _pendingHydrationReadIndex;
        private int _hydrationSessionVersion;
        private int3 _currentPlayerChunk;
        private int2 _currentPlayerSector;
        private AbsoluteUniversePosition _lastHydrationScanAup;
        private bool _indexedSectorPagingEnabled;
        private bool _indexedSectorPagingInFlight;
        private bool _playerSectorValid;
        private bool _sectorOverrideCommitInFlight;
        private float _nextSectorOverrideCommitTime;
        private string _indexedSectorSavePath;
        private string _indexedSectorOverrideDirectory;
        private List<SaveBinaryStorage.IndexedSectorEntryInfo> _indexedSectorDirectory;
        private Dictionary<long, SectorOverrideState> _sectorOverrideStates;
        private HashSet<int> _residentWorldPrefabHashes;
        private JobHandle _tombstoneDecaySweepHandle;
        private float _nextTombstoneDecaySweepTime;
        private int _tombstoneDecayApplyCursor;
        private int _tombstoneDecayCurrentDay;
        private bool _tombstoneDecaySweepScheduled;
        private bool _tombstoneDecayApplyPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _nextInstanceUidCounter = 0;
        }

        public static PersistentWorldRegistry Instance => GlobalRegistry.PersistentWorldRegistry;

        internal bool TryGetIndexedSaveHealth(out string absolutePath, out long currentSectorHash)
        {
            absolutePath = _indexedSectorSavePath;
            currentSectorHash = _playerSectorValid
                ? PackSectorHash(_currentPlayerSector)
                : InvalidPagedSectorHash;
            return _indexedSectorPagingEnabled && !string.IsNullOrEmpty(absolutePath);
        }

        bool IRuntimeWatchdogWorldHealthBridge.TryGetIndexedSaveHealth(out string absolutePath, out long currentSectorHash)
        {
            return TryGetIndexedSaveHealth(out absolutePath, out currentSectorHash);
        }

        /// <summary>
        /// Returns true when a sandboxed mod command targets protected runtime space near the active player core.
        /// </summary>
        /// <param name="runtimePosition">Frame-space command center.</param>
        /// <returns>True when the command must be rejected by the mod security gate.</returns>
        public static bool IsModProtectedCoreRuntimePosition(Vector3 runtimePosition)
        {
            float3 position = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(position)))
                return true;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aupPosition))
                return true;

            return IsModProtectedCoreAup(in aupPosition);
        }

        /// <summary>
        /// Returns true when a sandboxed mod command targets protected absolute space near the active player core.
        /// </summary>
        /// <param name="position">Absolute Universe Position command center.</param>
        /// <returns>True when the command must be rejected by the mod security gate.</returns>
        internal static bool IsModProtectedCoreAup(in AbsoluteUniversePosition position)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            if (!IsFinite(absolutePosition))
                return true;

            float3 runtimeFloat = position.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimeFloat)))
                return true;

            Vector3 runtimePosition = new Vector3(runtimeFloat.x, runtimeFloat.y, runtimeFloat.z);
            if (IsInsideActiveModuleInterior(runtimePosition))
                return true;

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            if (submarine != null && IsInsideSubmarineFallbackBounds(submarine, runtimePosition))
                return true;

            if (!TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
                return false;

            return AbsoluteUniversePosition.DistanceSq(in position, in playerAup) <= ModCoreProtectionRadiusSq;
        }

        private static bool TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player == null)
                return false;

            if (player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return MathGuard.IsFinite(in playerAup);
            }

            HectonPlayerMovement playerMovement = player.PlayerMovement;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return MathGuard.IsFinite(in playerAup);
        }

        public bool AreResidentWorldPrefabPoolsReady()
        {
            if (!Application.isPlaying)
                return true;

            if (_indexedSectorPagingInFlight)
                return false;

            if (_residentWorldPrefabHashes == null ||
                _residentWorldPrefabHashes.Count <= 0)
            {
                return true;
            }

            if (!TryEnsureItemLookup() || _resolvedItemCatalog == null)
                return false;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return false;

            _resolvedItemCatalog.PumpWorldPrefabDispatchTickets();
            HashSet<int>.Enumerator residentEnumerator = _residentWorldPrefabHashes.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                int itemHashId = residentEnumerator.Current;
                if (itemHashId == 0)
                    continue;

                if (!_resolvedItemCatalog.TryGetLoadedWorldPrefab(itemHashId, out GameObject prefab) ||
                    prefab == null ||
                    !pool.HasPool(prefab))
                {
                    residentEnumerator.Dispose();
                    return false;
                }
            }

            residentEnumerator.Dispose();
            return true;
        }

        private void Awake()
        {
            PersistentWorldRegistry registeredRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (registeredRegistry != null && registeredRegistry != this)
            {
                Destroy(gameObject);
                return;
            }

            TryRegisterService();

            maxTrackedItems = math.max(256, maxTrackedItems);
            chunkSizeMeters = math.max(16, chunkSizeMeters);
            hydrationRadiusInChunks = math.clamp(hydrationRadiusInChunks, 0, 2);
            _hydrationFrameCounter = 0;

            // COLD ALLOC: NativeList<PersistentWorldItemRecord>[maxTrackedItems] â€” persistent dropped-item record store â€” owner: PersistentWorldRegistry
            _records = new NativeList<PersistentWorldItemRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<int3,int>[maxTrackedItems] â€” dropped-item chunk lookup table â€” owner: PersistentWorldRegistry
            _recordsByChunk = new NativeParallelMultiHashMap<int3, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldCompactDeltaRecord>[maxTrackedItems] â€” authoritative 16-byte dropped-item delta store â€” owner: PersistentWorldRegistry
            _deltaRecords = new NativeList<PersistentWorldCompactDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,int>[maxTrackedItems] â€” delta entity-to-index lookup keyed by InstanceUid â€” owner: PersistentWorldRegistry
            _deltaRecordIndexByEntityId = new NativeHashMap<uint, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<uint>[maxTrackedItems] â€” tombstoned persistent instance UIDs preventing scene-authored respawn â€” owner: PersistentWorldRegistry
            _deletedInstanceUids = new NativeParallelHashSet<uint>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<ulong>[maxTrackedItems] â€” AUP-derived resource-node tombstones preventing procedural respawn â€” owner: PersistentWorldRegistry
            _resourceNodeTombstoneIds = new NativeParallelHashSet<ulong>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<ulong>[maxTrackedItems] â€” AUP-derived resource-node metamorphosis overrides â€” owner: PersistentWorldRegistry
            _resourceNodeMetamorphosedIds = new NativeParallelHashSet<ulong>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<int3,ushort>[maxTrackedItems] â€” chunk-id to compact delta table index â€” owner: PersistentWorldRegistry
            _deltaChunkIndexByChunkId = new NativeHashMap<int3, ushort>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<int3>[maxTrackedItems] â€” compact delta chunk table â€” owner: PersistentWorldRegistry
            _deltaChunkIds = new NativeList<int3>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<ulong,ushort>[maxTrackedItems] â€” item-hash to compact delta table index â€” owner: PersistentWorldRegistry
            _deltaItemIndexByHash = new NativeHashMap<ulong, ushort>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<ulong>[maxTrackedItems] â€” compact delta item-hash table â€” owner: PersistentWorldRegistry
            _deltaItemHashes = new NativeList<ulong>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,PersistentWorldCompactDeltaRecord>[maxTrackedItems] â€” chunk-hash to compact delta lookup â€” owner: PersistentWorldRegistry
            _deltaRecordsByChunk = new NativeParallelMultiHashMap<uint, PersistentWorldCompactDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[maxTrackedItems] - FrostTick tombstone decay candidate indices - owner: PersistentWorldRegistry
            _tombstoneDecayExpiredIndices = new NativeList<int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeList<PersistentWorldDeltaRecord>[maxTrackedItems] â€” immutable save snapshot for background binary writes â€” owner: PersistentWorldRegistry
            _saveSnapshotDeltas = new NativeList<PersistentWorldDeltaRecord>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeArray<PoolSlotData>[maxTrackedItems] â€” persistent hydration slot state store â€” owner: PersistentWorldRegistry
            _poolSlotData = new NativeArray<PoolSlotData>(maxTrackedItems, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeHashMap<ulong,int>[maxTrackedItems] â€” hydration GUID to slot lookup â€” owner: PersistentWorldRegistry
            _guidToPoolIndex = new NativeHashMap<ulong, int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,EntityDataRecord>[maxTrackedItems] â€” authoritative dehydration payload store keyed by InstanceUid â€” owner: PersistentWorldRegistry
            _entityStateByInstanceUid = new NativeHashMap<uint, EntityDataRecord>(maxTrackedItems, Allocator.Persistent);
            _floraSpawnStateByInstanceUid = new NativeHashMap<uint, EntityDataRecord>(maxTrackedItems, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,EntityDataRecord>[maxTrackedItems] â€” standalone flora spawn-timestamp payload store keyed by deterministic flora uid â€” owner: PersistentWorldRegistry
            // COLD ALLOC: NativeHashMap<uint,float3>[maxTrackedItems] Ã¢â‚¬â€ deferred spawn impulse staging keyed by InstanceUid for persistent debris hydration Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _spawnImpulseByInstanceUid = new NativeHashMap<uint, float3>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeHashMap<uint,float3>[maxTrackedItems] â€” deferred spawn velocity-change staging keyed by InstanceUid for transport-relative dropped-item inheritance â€” owner: PersistentWorldRegistry
            _spawnVelocityChangeByInstanceUid = new NativeHashMap<uint, float3>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: NativeQueue<int>(Persistent) â€” deferred dehydration queue â€” owner: PersistentWorldRegistry
            _dehydrateQueue = new NativeQueue<int>(Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[maxTrackedItems] â€” time-sliced hydration backlog keyed by persistent record index â€” owner: PersistentWorldRegistry
            _pendingHydrationRecords = new NativeList<int>(maxTrackedItems, Allocator.Persistent);
            // COLD ALLOC: GameObject[maxTrackedItems] â€” hydrated proxy instances by slot â€” owner: PersistentWorldRegistry
            _hydratedInstancesBySlot = new GameObject[maxTrackedItems];
            // COLD ALLOC: Transform[maxTrackedItems] â€” hydrated proxy transforms by slot â€” owner: PersistentWorldRegistry
            _poolSlotTransforms = new Transform[maxTrackedItems];
            // COLD ALLOC: Rigidbody[maxTrackedItems] â€” hydrated proxy rigidbodies by slot â€” owner: PersistentWorldRegistry
            _poolSlotRigidbodies = new Rigidbody[maxTrackedItems];
            // COLD ALLOC: Dictionary<int,GameObject>[128] â€” hydrated world-item proxy lookup â€” owner: PersistentWorldRegistry
            _hydratedInstancesByRecordIndex = new Dictionary<int, GameObject>(128);
            // COLD ALLOC: Dictionary<ulong,ItemData>[1024] â€” persistent-id hash to ItemData lookup cache â€” owner: PersistentWorldRegistry
            _itemLookupByHash = new Dictionary<ulong, ItemData>(1024);
            // COLD ALLOC: List<ItemData>[1024] â€” item catalog scratch buffer for hash cache rebuilds â€” owner: PersistentWorldRegistry
            _itemCatalogScratch = new List<ItemData>(1024);
            // COLD ALLOC: List<int>[256] Ã¢â‚¬â€ unique addressable prefab prewarm hash buffer for paged sector hydration Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _worldPrefabPrewarmHashScratch = new List<int>(256);
            // COLD ALLOC: List<int>[256] Ã¢â‚¬â€ deferred addressable prefab release scratch buffer for paged sector eviction Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _worldPrefabReleaseScratch = new List<int>(256);
            // COLD ALLOC: List<int>[128] â€” hydrated record scratch buffer for sync/dehydrate passes â€” owner: PersistentWorldRegistry
            _recordIndexScratch = new List<int>(128);
            // COLD ALLOC: List<EntityDataRecord>[128] â€” sector entity-state rewrite scratch buffer for MMF fauna hibernation pages â€” owner: PersistentWorldRegistry
            _entityStateScratch = new List<EntityDataRecord>(128);
            // COLD ALLOC: List<EntityDataRecord>[128] — destination-sector entity-state rewrite scratch — owner: PersistentWorldRegistry
            _entityStateSectorTargetScratch = new List<EntityDataRecord>(128);
            _floraSpawnStateScratch = new List<EntityDataRecord>(128); // COLD ALLOC: List<EntityDataRecord>[128] â€” standalone flora spawn-state snapshot scratch â€” owner: PersistentWorldRegistry
            // COLD ALLOC: List<SectorEntityStateWriteWork>[64] â€” async sector entity-state temp write handles â€” owner: PersistentWorldRegistry
            _pendingEntityStateTempWrites = new List<SectorEntityStateWriteWork>(MaxPendingEntityStateTempWrites);
            // COLD ALLOC: List<long>[16] - due sector override commit queue - owner: PersistentWorldRegistry
            _dueSectorOverrideCommitHashes = new List<long>(16);
            // COLD ALLOC: long[16] - indexed sector quarantine reset scratch - owner: PersistentWorldRegistry
            _quarantinedSectorResetScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];
            // COLD ALLOC: uint[64] — active whale-fall POI uid index for bounded influence queries — owner: PersistentWorldRegistry
            _whaleFallPoiInstanceUids = new uint[MaxWhaleFallInfluenceScan];
            // COLD ALLOC: uint[256] — per-pass apex migration uid de-duplication scratch — owner: PersistentWorldRegistry
            _apexMigrationVisitedUids = new uint[MaxApexMigrationVisitedUids];
            // COLD ALLOC: PersistentThermalVentRecord[16] - active hydrothermal vent snapshot for thermodynamics - owner: PersistentWorldRegistry
            _activeThermalVents = new PersistentThermalVentRecord[MaxPersistentThermalVentRecords];
            // COLD ALLOC: List<IndexedSectorEntryInfo>[256] Ã¢â‚¬â€ cached v8 sector directory entries for paged restore Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);
            // COLD ALLOC: Dictionary<long,SectorOverrideState>[32] Ã¢â‚¬â€ paged sector temp-override residency map Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _sectorOverrideStates = new Dictionary<long, SectorOverrideState>(32);
            // COLD ALLOC: HashSet<int>[256] Ã¢â‚¬â€ resident addressable world-prefab hash residency set Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _residentWorldPrefabHashes = new HashSet<int>(256);
            RegisterNativeMemorySentinelAllocations();
            RegisterPersistentMemoryBudget();

            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegisterRuntimeLoops();
        }

        private void Start()
        {
            TryRegisterRuntimeLoops();
        }

        public bool TryGetActiveThermalVent(int index, out PersistentThermalVentRecord record)
        {
            record = default;
            if (_activeThermalVents == null ||
                index < 0 ||
                index >= _activeThermalVentCount ||
                index >= _activeThermalVents.Length)
            {
                return false;
            }

            record = _activeThermalVents[index];
            return record.RuntimeKey != 0L;
        }

        public bool RegisterActiveThermalVent(
            long runtimeKey,
            Vector3 positionWS,
            float radiusWS,
            float heightWS,
            float updraftVelocity,
            float heatIntensity,
            float smokeDensity,
            float cableRadiusWS)
        {
            if (runtimeKey == 0L || _activeThermalVents == null)
                return false;

            if (!TryResolveAupFromRuntimeOrigin(positionWS, out AbsoluteUniversePosition ventAup))
                return false;

            PersistentThermalVentRecord record = new PersistentThermalVentRecord
            {
                RuntimeKey = runtimeKey,
                PositionAup = ventAup,
                RadiusWS = math.max(2f, radiusWS),
                HeightWS = math.max(4f, heightWS),
                UpdraftVelocity = math.max(0.5f, updraftVelocity),
                HeatIntensity = math.max(0.5f, heatIntensity),
                SmokeDensity = math.max(0.1f, smokeDensity),
                CableRadiusWS = math.max(2f, cableRadiusWS)
            };

            for (int i = 0; i < _activeThermalVentCount; i++)
            {
                if (_activeThermalVents[i].RuntimeKey != runtimeKey)
                    continue;

                _activeThermalVents[i] = record;
                unchecked { _activeThermalVentRevision++; }
                return true;
            }

            if (_activeThermalVentCount >= _activeThermalVents.Length)
                return false;

            _activeThermalVents[_activeThermalVentCount++] = record;
            unchecked { _activeThermalVentRevision++; }
            return true;
        }

        public bool UnregisterActiveThermalVent(long runtimeKey)
        {
            if (runtimeKey == 0L || _activeThermalVents == null || _activeThermalVentCount <= 0)
                return false;

            for (int i = 0; i < _activeThermalVentCount; i++)
            {
                if (_activeThermalVents[i].RuntimeKey != runtimeKey)
                    continue;

                int lastIndex = _activeThermalVentCount - 1;
                _activeThermalVents[i] = _activeThermalVents[lastIndex];
                _activeThermalVents[lastIndex] = default;
                _activeThermalVentCount = lastIndex;
                unchecked { _activeThermalVentRevision++; }
                return true;
            }

            return false;
        }

        private void OnDisable()
        {
            CompleteTombstoneDecayBeforeDeltaMutation();
            CancelHydrationSession(clearQueue: false);
            TryUnregisterRuntimeLoops();
            TryUnregisterService();
            DehydrateAll(syncTransformsBackToRecords: false);
            DrainPendingEntityStateTempWrites(int.MaxValue);
            DisposePendingEntityStateTempWritesDeferred();
        }

        private void OnDestroy()
        {
            CancelHydrationSession(clearQueue: false);
            TryUnregisterRuntimeLoops();
            TryUnregisterService();
            DehydrateAll(syncTransformsBackToRecords: false);
            DrainPendingEntityStateTempWrites(int.MaxValue);
            DisposePendingEntityStateTempWritesDeferred();
            CompleteTombstoneDecayBeforeDeltaMutation();
            UnregisterNativeMemorySentinelAllocations();

            if (_records.IsCreated)
                _records.Dispose();

            if (_recordsByChunk.IsCreated)
                _recordsByChunk.Dispose();

            if (_deltaRecords.IsCreated)
                _deltaRecords.Dispose();

            if (_deltaRecordIndexByEntityId.IsCreated)
                _deltaRecordIndexByEntityId.Dispose();

            if (_deletedInstanceUids.IsCreated)
            {
                _deletedInstanceUids.Dispose();
            }

            if (_resourceNodeTombstoneIds.IsCreated)
            {
                _resourceNodeTombstoneIds.Dispose();
            }

            if (_resourceNodeMetamorphosedIds.IsCreated)
            {
                _resourceNodeMetamorphosedIds.Dispose();
            }

            if (_deltaChunkIndexByChunkId.IsCreated)
                _deltaChunkIndexByChunkId.Dispose();

            if (_deltaChunkIds.IsCreated)
                _deltaChunkIds.Dispose();

            if (_deltaItemIndexByHash.IsCreated)
                _deltaItemIndexByHash.Dispose();

            if (_deltaItemHashes.IsCreated)
                _deltaItemHashes.Dispose();

            if (_deltaRecordsByChunk.IsCreated)
                _deltaRecordsByChunk.Dispose();

            if (_tombstoneDecayExpiredIndices.IsCreated)
                _tombstoneDecayExpiredIndices.Dispose();

            if (_saveSnapshotDeltas.IsCreated)
                _saveSnapshotDeltas.Dispose();

            if (_poolSlotData.IsCreated)
                _poolSlotData.Dispose();

            if (_guidToPoolIndex.IsCreated)
                _guidToPoolIndex.Dispose();

            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Dispose();

            if (_floraSpawnStateByInstanceUid.IsCreated)
                _floraSpawnStateByInstanceUid.Dispose();

            if (_spawnImpulseByInstanceUid.IsCreated)
                _spawnImpulseByInstanceUid.Dispose();

            if (_spawnVelocityChangeByInstanceUid.IsCreated)
                _spawnVelocityChangeByInstanceUid.Dispose();

            if (_dehydrateQueue.IsCreated)
            {
                _dehydrateQueue.Dispose();
            }

            if (_pendingHydrationRecords.IsCreated)
                _pendingHydrationRecords.Dispose();

            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            if (GlobalRegistry.PersistentWorldRegistry != null &&
                !ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[PersistentWorldRegistry] Duplicate registry owner detected. Disabling duplicate.");
#endif
                enabled = false;
                return;
            }

            GlobalRegistry.RegisterPersistentWorldRegistry(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this))
                GlobalRegistry.UnregisterPersistentWorldRegistry(this);

            _serviceRegistered = false;
        }

        public void Tick(float dt)
        {
            _resolvedItemCatalog?.DrainDeferredWorldPrefabReleases(4);
            DrainDehydrateQueue(MaxDehydrationsPerTick);
        }

        public void LateFrameTick()
        {
            DrainPendingEntityStateTempWrites(MaxEntityStateTempWriteCompletionsPerTick);
            TryFinalizeTombstoneDecaySweepNoWait(MaxTombstoneDecayAppliesPerLateFrame);
            if (_tombstoneDecayApplyPending)
                ApplyCollectedTombstoneDecay(MaxTombstoneDecayAppliesPerLateFrame);
        }

        public void SlowTick()
        {
            float now = Time.unscaledTime;
            ScheduleTombstoneDecaySweepIfDue(now);

            if (!TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
                return;

            int2 nextSector = QuantizeSector(in playerAup);
            if (_indexedSectorPagingEnabled && (!_playerSectorValid || !math.all(nextSector == _currentPlayerSector)))
            {
                _currentPlayerSector = nextSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(nextSector);
            }

            TryScheduleSectorOverrideCommit();

            int3 nextChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
            bool requiresRescan = !_playerChunkValid || !math.all(nextChunk == _currentPlayerChunk);
            if (!requiresRescan && _hasLastHydrationScanAup)
                requiresRescan = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastHydrationScanAup) >= HydrationRescanDistanceSq;

            if (!requiresRescan)
                return;

            SyncAllHydratedRecords();
            _currentPlayerChunk = nextChunk;
            _playerChunkValid = true;
            _lastHydrationScanAup = playerAup;
            _hasLastHydrationScanAup = true;
            _hydrationFrameCounter++;
            RefreshHydrationWindow(in playerAup);
            EnsureHydrationSessionScheduled();
            UpdateDiagnostics();
        }

        internal bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)
        {
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition, Vector3.zero);
        }

        internal bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse)
        {
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition, initialImpulse, Vector3.zero);
        }

        internal bool TryRegisterDroppedItemWithState(ItemData itemData, int quantity, Vector3 runtimePosition, ulong geneticsMask, ushort qualityMilli)
        {
            return TryRegisterDroppedItemStateful(itemData, quantity, runtimePosition, Vector3.zero, Vector3.zero, geneticsMask, qualityMilli);
        }

        internal bool TryRegisterDroppedItem(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            Vector3 initialImpulse,
            Vector3 inheritedVelocityChange)
        {
            return TryRegisterDroppedItemStateful(itemData, quantity, runtimePosition, initialImpulse, inheritedVelocityChange, 0u, DefaultItemQualityMilli);
        }

        private bool TryRegisterDroppedItemStateful(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            Vector3 initialImpulse,
            Vector3 inheritedVelocityChange,
            ulong geneticsMask,
            ushort qualityMilli)
        {
            if (itemData == null || quantity <= 0 || !_records.IsCreated || _records.Length >= _records.Capacity)
                return false;

            if (string.IsNullOrWhiteSpace(itemData.PersistentId))
                return false;

            if (itemData.worldPrefab == null)
                return false;

            if (!TryGenerateInstanceUid(itemData, ComputePersistentIdHash(itemData.PersistentId), out uint instanceUid))
                return false;

            Vector3 scatteredRuntimePosition = ApplyDeterministicDropScatter(runtimePosition, instanceUid);
            if (!TryResolveAupFromRuntimeOrigin(scatteredRuntimePosition, out AbsoluteUniversePosition position))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = ComputePersistentIdHash(itemData.PersistentId),
                ItemPersistentId = new FixedString128Bytes(itemData.PersistentId),
                Quantity = quantity,
                Flags = PersistentWorldItemFlags.None,
                InstanceUid = instanceUid
            };

            int recordIndex = _records.Length;
            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, recordIndex);
            RegisterOrUpdatePoolSlot(recordIndex, in record);
            RegisterOrUpdateEntityState(in record, CreateEntityStateFromRecord(in record, geneticsMask, qualityMilli));
            RegisterSpawnImpulse(record.InstanceUid, initialImpulse);
            RegisterSpawnVelocityChange(record.InstanceUid, inheritedVelocityChange);
            UpsertDeltaRecord(in record);

            if (_hasLastHydrationScanAup && ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                QueueRecordForHydration(recordIndex, in record, in _lastHydrationScanAup);

            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            if (itemHashId == 0 || itemCatalog == null)
                return false;

            ItemData itemData = itemCatalog.FindByHash(itemHashId);
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition);
        }

        internal bool TryRegisterDestroyedFlora(ulong floraPersistentIdHash, uint instanceUid, Vector3 runtimePosition)
        {
            if (floraPersistentIdHash == 0UL || instanceUid == 0u || !_records.IsCreated)
            {
                return false;
            }

            if (IsDeletedInstanceUid(instanceUid))
                return true;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            if (TryFindRecordIndexByInstanceUid(instanceUid, out int existingRecordIndex))
            {
                PersistentWorldItemRecord existing = _records[existingRecordIndex];
                RemoveRecordIndexFromChunk(existing.ChunkId, existingRecordIndex);
                _recordsByChunk.Add(chunkId, existingRecordIndex);

                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.ItemPersistentId = default;
                existing.Quantity = 1;
                existing.Flags = PersistentWorldItemFlags.FloraDestroyed;
                _records[existingRecordIndex] = existing;
                RemoveEntityState(in existing);
                UpsertDeltaRecord(in existing);
                UpdateDiagnostics();
                return true;
            }

            if (_records.Length >= _records.Capacity)
                return false;

            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = floraPersistentIdHash,
                ItemPersistentId = default,
                Quantity = 1,
                Flags = PersistentWorldItemFlags.FloraDestroyed,
                InstanceUid = instanceUid
            };

            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, _records.Length - 1);
            UpsertDeltaRecord(in record);
            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterFloraStateOverride(
            ulong floraPersistentIdHash,
            uint instanceUid,
            Vector3 runtimePosition,
            float normalizedHealth,
            byte harvestState)
        {
            if (floraPersistentIdHash == 0UL || instanceUid == 0u || !_records.IsCreated)
                return false;

            ushort packedState = PackFloraStateOverride(normalizedHealth, harvestState);
            if (packedState == 0)
                return TryClearFloraStateOverride(instanceUid);

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            if (TryFindRecordIndexByInstanceUid(instanceUid, out int existingRecordIndex))
            {
                PersistentWorldItemRecord existing = _records[existingRecordIndex];
                RemoveRecordIndexFromChunk(existing.ChunkId, existingRecordIndex);
                _recordsByChunk.Add(chunkId, existingRecordIndex);

                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.ItemPersistentId = default;
                existing.Quantity = packedState;
                existing.Flags = PersistentWorldItemFlags.FloraStateOverride;
                _records[existingRecordIndex] = existing;
                RemoveEntityState(in existing);
                UpsertDeltaRecord(in existing);
                UpdateDiagnostics();
                return true;
            }

            if (_records.Length >= _records.Capacity)
                return false;

            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = floraPersistentIdHash,
                ItemPersistentId = default,
                Quantity = packedState,
                Flags = PersistentWorldItemFlags.FloraStateOverride,
                InstanceUid = instanceUid
            };

            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, _records.Length - 1);
            UpsertDeltaRecord(in record);
            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterDestroyedResourceNode(ulong tombstoneId, Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            return TryRegisterDestroyedResourceNode(tombstoneId, in position);
        }

        internal bool TryRegisterDestroyedResourceNode(ulong tombstoneId, in AbsoluteUniversePosition position)
        {
            if (tombstoneId == 0UL ||
                !_records.IsCreated ||
                _records.Length >= _records.Capacity)
            {
                return false;
            }

            if (IsResourceNodeTombstoned(tombstoneId))
                return true;

            if (!TryGenerateResourceNodeTombstoneInstanceUid(tombstoneId, out uint instanceUid))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = tombstoneId,
                ItemPersistentId = default,
                Quantity = 0,
                Flags = PersistentWorldItemFlags.Deleted | PersistentWorldItemFlags.ResourceNodeDestroyed,
                InstanceUid = instanceUid
            };

            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, _records.Length - 1);
            RegisterResourceNodeTombstone(tombstoneId);
            UpsertDeletedTombstone(in record);
            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterResourceNodeMetamorphosis(ulong tombstoneId, Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            return TryRegisterResourceNodeMetamorphosis(tombstoneId, in position);
        }

        internal bool TryRegisterResourceNodeMetamorphosis(ulong tombstoneId, in AbsoluteUniversePosition position)
        {
            if (tombstoneId == 0UL ||
                !_records.IsCreated ||
                _records.Length >= _records.Capacity ||
                !IsFinite(position.ToAbsoluteDouble3()))
            {
                return false;
            }

            if (IsResourceNodeMetamorphosed(tombstoneId))
                return true;

            if (!TryGenerateResourceNodeMetamorphosisInstanceUid(tombstoneId, out uint instanceUid))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = tombstoneId,
                ItemPersistentId = default,
                Quantity = 1,
                Flags = PersistentWorldItemFlags.ResourceNodeMetamorphosed,
                InstanceUid = instanceUid
            };

            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, _records.Length - 1);
            RegisterResourceNodeMetamorphosis(tombstoneId);
            UpsertDeltaRecord(in record);
            UpdateDiagnostics();
            return true;
        }

        internal bool TryRegisterPendingFloraSeed(ulong floraPersistentIdHash, uint instanceUid, Vector3 runtimePosition, ushort remainingSeconds)
        {
            if (floraPersistentIdHash == 0UL ||
                instanceUid == 0u ||
                remainingSeconds == 0 ||
                !_records.IsCreated)
            {
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord existing = _records[recordIndex];
                if (existing.InstanceUid != instanceUid)
                    continue;

                RemoveRecordIndexFromChunk(existing.ChunkId, recordIndex);
                _recordsByChunk.Add(chunkId, recordIndex);
                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.Quantity = remainingSeconds;
                existing.Flags = PersistentWorldItemFlags.FloraSeedPending;
                _records[recordIndex] = existing;
                UpsertDeltaRecord(in existing);
                UpdateDiagnostics();
                return true;
            }

            if (_records.Length >= _records.Capacity)
                return false;

            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = floraPersistentIdHash,
                ItemPersistentId = default,
                Quantity = remainingSeconds,
                Flags = PersistentWorldItemFlags.FloraSeedPending,
                InstanceUid = instanceUid
            };

            _records.AddNoResize(record);
            _recordsByChunk.Add(chunkId, _records.Length - 1);
            UpsertDeltaRecord(in record);
            UpdateDiagnostics();
            return true;
        }

        internal bool TryUpdatePendingFloraSeed(uint instanceUid, ushort remainingSeconds)
        {
            if (instanceUid == 0u || remainingSeconds == 0 || !_records.IsCreated)
                return false;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.InstanceUid != instanceUid || !record.IsFloraSeedPending)
                    continue;

                record.Quantity = remainingSeconds;
                _records[recordIndex] = record;
                UpsertDeltaRecord(in record);
                UpdateDiagnostics();
                return true;
            }

            return false;
        }

        internal bool TryMarkPendingFloraSeedReady(uint instanceUid)
        {
            if (instanceUid == 0u || !_records.IsCreated)
                return false;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.InstanceUid != instanceUid || !record.IsFloraSeedPending)
                    continue;

                record.Quantity = 1;
                record.Flags = PersistentWorldItemFlags.FloraSeedReady;
                _records[recordIndex] = record;
                UpsertDeltaRecord(in record);
                UpdateDiagnostics();
                return true;
            }

            return false;
        }

        internal bool TryRegisterFloraSpawnTimestamp(uint instanceUid, Vector3 runtimePosition, float spawnPlayTimeSeconds)
        {
            if (instanceUid == 0u || !_floraSpawnStateByInstanceUid.IsCreated)
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            EntityDataRecord state = CreateFloraSpawnTimestampState(instanceUid, spawnPlayTimeSeconds, in position);
            _floraSpawnStateByInstanceUid.Remove(instanceUid);
            _floraSpawnStateByInstanceUid.TryAdd(instanceUid, state);
            return true;
        }

        internal bool TryGetFloraSpawnTimestamp(uint instanceUid, out float spawnPlayTimeSeconds)
        {
            spawnPlayTimeSeconds = 0f;
            if (instanceUid == 0u ||
                !_floraSpawnStateByInstanceUid.IsCreated ||
                !_floraSpawnStateByInstanceUid.TryGetValue(instanceUid, out EntityDataRecord state) ||
                !IsFloraSpawnTimestampState(in state))
            {
                return false;
            }

            spawnPlayTimeSeconds = GetFloraSpawnTimestampSeconds(in state);
            return true;
        }

        internal bool TryClearFloraSpawnTimestamp(uint instanceUid)
        {
            if (instanceUid == 0u || !_floraSpawnStateByInstanceUid.IsCreated)
                return false;

            return _floraSpawnStateByInstanceUid.Remove(instanceUid);
        }

        internal bool IsTombstoned(uint instanceUid)
        {
            return IsDeletedInstanceUid(instanceUid);
        }

        internal bool IsResourceNodeTombstoned(ulong tombstoneId)
        {
            return tombstoneId != 0UL &&
                   _resourceNodeTombstoneIds.IsCreated &&
                   _resourceNodeTombstoneIds.Contains(tombstoneId);
        }

        internal bool IsResourceNodeMetamorphosed(ulong tombstoneId)
        {
            return tombstoneId != 0UL &&
                   _resourceNodeMetamorphosedIds.IsCreated &&
                   _resourceNodeMetamorphosedIds.Contains(tombstoneId);
        }

        internal bool TryRegisterFaunaTombstone(uint instanceUid)
        {
            if (instanceUid == 0u || !_deletedInstanceUids.IsCreated || !_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated)
                return false;

            RegisterDeletedInstanceUid(instanceUid);

            var tombstone = new PersistentWorldDeltaRecord
            {
                ChunkId = ApexFaunaTombstoneChunkId,
                ItemPersistentIdHash = 0UL,
                InstanceUid = instanceUid,
                PackedLocalPosition = 0u,
                Quantity = (ushort)ResolveTombstoneDayIndex(),
                ItemFlags = (byte)PersistentWorldItemFlags.Deleted,
                Reserved = 0
            };

            if (!TryBuildCompactDeltaRecord(tombstone, out PersistentWorldCompactDeltaRecord compactRecord))
                return false;

            if (_deltaRecordIndexByEntityId.TryGetValue(instanceUid, out int existingIndex))
            {
                _deltaRecords[existingIndex] = compactRecord;
                return true;
            }

            if (_deltaRecords.Length >= _deltaRecords.Capacity)
                return false;

            _deltaRecordIndexByEntityId.TryAdd(instanceUid, _deltaRecords.Length);
            _deltaRecords.AddNoResize(compactRecord);
            return true;
        }

        internal int3 ResolveRuntimeChunkId(Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return default;

            return AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
        }

        internal int CopyResourceNodeTombstonesInChunk(int3 chunkId, List<ResourceNodeTombstoneRecord> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            if (!_records.IsCreated)
                return 0;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (!record.IsResourceNodeDestroyed ||
                    record.ItemPersistentIdHash == 0UL ||
                    !record.ChunkId.Equals(chunkId))
                {
                    continue;
                }

                destination.Add(new ResourceNodeTombstoneRecord
                {
                    TombstoneId = record.ItemPersistentIdHash,
                    InstanceUid = record.InstanceUid,
                    Position = record.Position,
                    ChunkId = record.ChunkId
                });
            }

            return destination.Count;
        }

        internal bool TryReinstateDestroyedResourceNode(ulong tombstoneId)
        {
            if (tombstoneId == 0UL || !_records.IsCreated)
                return false;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.ItemPersistentIdHash != tombstoneId || !record.IsResourceNodeDestroyed)
                    continue;

                if (_resourceNodeTombstoneIds.IsCreated)
                    _resourceNodeTombstoneIds.Remove(tombstoneId);

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                _records[recordIndex] = record;
                RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
                RemoveEntityState(in record);
                UnregisterDeletedInstanceUid(record.InstanceUid);
                RemoveDeltaRecord(record.InstanceUid);
                UpdateDiagnostics();
                return true;
            }

            return false;
        }

        internal bool TryClearDestroyedFlora(uint instanceUid)
        {
            if (instanceUid == 0u || !_records.IsCreated)
                return false;

            UnregisterDeletedInstanceUid(instanceUid);

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.InstanceUid != instanceUid || !record.IsFloraDestroyed)
                    continue;

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                _records[recordIndex] = record;
                RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
                RemoveEntityState(in record);
                RemoveDeltaRecord(instanceUid);
                UpdateDiagnostics();
                return true;
            }

            return false;
        }

        internal bool TryClearFloraStateOverride(uint instanceUid)
        {
            if (instanceUid == 0u || !_records.IsCreated)
                return false;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.InstanceUid != instanceUid || !record.IsFloraStateOverride)
                    continue;

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                _records[recordIndex] = record;
                RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
                RemoveEntityState(in record);
                RemoveDeltaRecord(instanceUid);
                UpdateDiagnostics();
                return true;
            }

            return false;
        }

        private static Vector3 ApplyDeterministicDropScatter(Vector3 runtimePosition, uint instanceUid)
        {
            uint state = instanceUid != 0u ? instanceUid : 0xA511E9B3u;
            float2 direction = NextScatterPlanarDirection(ref state);
            float radius = NextScatter01(ref state) * DropScatterRadiusMeters;
            float lift = math.lerp(DropScatterMinLiftMeters, DropScatterMaxLiftMeters, NextScatter01(ref state));

            Vector3 offset;
            offset.x = direction.x * radius;
            offset.y = lift;
            offset.z = direction.y * radius;
            return runtimePosition + offset;
        }

        private static float NextScatter01(ref uint state)
        {
            return (NextScatterBits(ref state) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint NextScatterBits(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static float2 NextScatterPlanarDirection(ref uint state)
        {
            return ResolveScatterPlanarDirection(NextScatterBits(ref state));
        }

        private static float2 ResolveScatterPlanarDirection(uint seed)
        {
            switch ((seed >> 29) & 7u)
            {
                case 0u: return new float2(1f, 0f);
                case 1u: return new float2(-1f, 0f);
                case 2u: return new float2(0f, 1f);
                case 3u: return new float2(0f, -1f);
                case 4u: return new float2(ScatterDiagonal2, ScatterDiagonal2);
                case 5u: return new float2(-ScatterDiagonal2, ScatterDiagonal2);
                case 6u: return new float2(ScatterDiagonal2, -ScatterDiagonal2);
                default: return new float2(-ScatterDiagonal2, -ScatterDiagonal2);
            }
        }

        internal static ulong ComputeResourceNodeTombstoneId(Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return 0UL;

            return ComputeResourceNodeTombstoneId(in position);
        }

        internal static ulong ComputeResourceNodeTombstoneId(in AbsoluteUniversePosition position)
        {
            ulong hash = FnvOffsetBasis64;
            FoldResourceNodeTombstoneField(ref hash, 0x484543544F4E3852UL);
            FoldResourceNodeTombstoneField(ref hash, unchecked((ulong)position.GridX));
            FoldResourceNodeTombstoneField(ref hash, unchecked((ulong)position.GridY));
            FoldResourceNodeTombstoneField(ref hash, unchecked((ulong)position.GridZ));

            ulong localX = (ulong)math.max(0L, (long)math.round(position.LocalX * 1000f));
            ulong localY = (ulong)math.max(0L, (long)math.round(position.LocalY * 1000f));
            ulong localZ = (ulong)math.max(0L, (long)math.round(position.LocalZ * 1000f));
            FoldResourceNodeTombstoneField(ref hash, localX);
            FoldResourceNodeTombstoneField(ref hash, localY);
            FoldResourceNodeTombstoneField(ref hash, localZ);
            return hash;
        }

        internal static string FormatResourceNodeTombstoneId(ulong tombstoneId)
        {
            return tombstoneId == 0UL
                ? string.Empty
                : CreateResourceNodeTombstoneLabel(tombstoneId);
        }

        private static string CreateResourceNodeTombstoneLabel(ulong tombstoneId)
        {
            return string.Create(ResourceNodeTombstonePrefix.Length + 16, tombstoneId, static (buffer, value) =>
            {
                ResourceNodeTombstonePrefix.AsSpan().CopyTo(buffer);
                WriteHex16(buffer.Slice(ResourceNodeTombstonePrefix.Length, 16), value);
            });
        }

        private static string CreateSectorOverrideDirectoryName(string absolutePath)
        {
            return string.Concat(Path.GetFileNameWithoutExtension(absolutePath), SectorOverridesDirectorySuffix);
        }

        private static string CreateSectorTempFileName(long sectorHash, string suffix)
        {
            return string.Create(16 + suffix.Length, (sectorHash, suffix), static (buffer, state) =>
            {
                WriteHex16(buffer.Slice(0, 16), state.sectorHash);
                state.suffix.AsSpan().CopyTo(buffer.Slice(16));
            });
        }

        private static string CreateHexMessage(string prefix, long value, string suffix)
        {
            return string.Create(prefix.Length + 16 + suffix.Length, (prefix, value, suffix), static (buffer, state) =>
            {
                int cursor = 0;
                state.prefix.AsSpan().CopyTo(buffer);
                cursor += state.prefix.Length;
                WriteHex16(buffer.Slice(cursor, 16), state.value);
                cursor += 16;
                state.suffix.AsSpan().CopyTo(buffer.Slice(cursor));
            });
        }

        private static string CreateHexMessage(string prefix, ulong value)
        {
            return string.Create(prefix.Length + 16, (prefix, value), static (buffer, state) =>
            {
                state.prefix.AsSpan().CopyTo(buffer);
                WriteHex16(buffer.Slice(state.prefix.Length, 16), state.value);
            });
        }

        private static string CreateHexErrorMessage(string prefix, long value, string error)
        {
            string safeError = error ?? string.Empty;
            return string.Create(prefix.Length + 18 + safeError.Length, (prefix, value, safeError), static (buffer, state) =>
            {
                int cursor = 0;
                state.prefix.AsSpan().CopyTo(buffer);
                cursor += state.prefix.Length;
                WriteHex16(buffer.Slice(cursor, 16), state.value);
                cursor += 16;
                buffer[cursor++] = ':';
                buffer[cursor++] = ' ';
                state.safeError.AsSpan().CopyTo(buffer.Slice(cursor));
            });
        }

        private static string CreateRecordCountWatchdogMessage(int recordCount)
        {
            const string prefix = "[PersistentWorldRegistry] ResetPoolSlots dehydrate queue drain exceeded watchdog. recordCount=";
            int digitCount = CountSignedDecimalDigits(recordCount);
            return string.Create(prefix.Length + digitCount, recordCount, static (buffer, value) =>
            {
                prefix.AsSpan().CopyTo(buffer);
                value.TryFormat(buffer.Slice(prefix.Length), out int _);
            });
        }

        private static int CountSignedDecimalDigits(int value)
        {
            long remaining = value;
            int digits = remaining < 0L ? 1 : 0;
            if (remaining < 0L)
                remaining = -remaining;

            do
            {
                digits++;
                remaining /= 10L;
            }
            while (remaining != 0L);

            return digits;
        }

        private static void WriteHex16(Span<char> destination, long value)
        {
            WriteHex16(destination, unchecked((ulong)value));
        }

        private static void WriteHex16(Span<char> destination, ulong value)
        {
            for (int i = 15; i >= 0; i--)
            {
                destination[i] = UpperHexDigits[(int)(value & 0xFUL)];
                value >>= 4;
            }
        }

        private static void FoldResourceNodeTombstoneField(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(value & 0xFFUL);
                hash *= FnvPrime64;
                value >>= 8;
            }
        }

        internal void MarkRecordCollected(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (record.IsCollected)
                return;

            record.MarkCollected();
            record.MarkDeleted();
            record.Quantity = 0;
            _records[recordIndex] = record;
            UpsertDeletedTombstone(in record);
            DehydrateRecord(recordIndex, syncTransformBackToRecord: false);
            RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
            RemoveEntityState(in record);
            UpdateDiagnostics();
        }

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotDeltas.IsCreated)
                return;

            SyncAllHydratedRecords();
            StageResourceNodeTombstonesForSave();
            _saveSnapshotDeltas.Clear();
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!_saveSnapshotDeltas.IsCreated || _saveSnapshotDeltas.Length >= _saveSnapshotDeltas.Capacity)
                    break;

                if (TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    _saveSnapshotDeltas.AddNoResize(expandedRecord);
            }

            UpdateDiagnostics();
        }

        internal NativeArray<PersistentWorldDeltaRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotDeltas.IsCreated
                ? _saveSnapshotDeltas.AsArray()
                : default;
        }

        internal void RestoreFromLoadedRecords(PersistentWorldDeltaRecord[] loadedRecords, bool scheduleHydration = true)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();
            if (_tombstoneDecayExpiredIndices.IsCreated)
                _tombstoneDecayExpiredIndices.Clear();
            _tombstoneDecayApplyCursor = 0;
            _tombstoneDecayApplyPending = false;
            CancelHydrationSession(clearQueue: true);
            DehydrateAll(syncTransformsBackToRecords: false);
            if (!_indexedSectorPagingEnabled)
                _resolvedItemCatalog?.ReleaseAllWorldPrefabHandles();
            _records.Clear();
            _recordsByChunk.Clear();
            _deltaRecords.Clear();
            _deltaRecordIndexByEntityId.Clear();
            _deletedInstanceUids.Clear();
            _resourceNodeTombstoneIds.Clear();
            _resourceNodeMetamorphosedIds.Clear();
            _deltaChunkIndexByChunkId.Clear();
            _deltaChunkIds.Clear();
            _deltaItemIndexByHash.Clear();
            _deltaItemHashes.Clear();
            _deltaRecordsByChunk.Clear();
            _tombstoneDecayExpiredIndices.Clear();
            _saveSnapshotDeltas.Clear();
            _playerChunkValid = false;
            _hasLastHydrationScanAup = false;
            _lastHydrationScanAup = default;
            _currentPlayerChunk = default;
            _hydrationFrameCounter = 0;
            ResetPoolSlots();

            if (loadedRecords != null)
            {
                uint maxObservedInstanceSequence = 0u;
                int restoreCount = math.min(loadedRecords.Length, _records.Capacity);
                PreRegisterLoadedRecordTombstones(loadedRecords, restoreCount);
                for (int i = 0; i < restoreCount; i++)
                {
                    PersistentWorldDeltaRecord deltaRecord = loadedRecords[i];
                    if (!deltaRecord.IsValid)
                        continue;

                    uint observedSequence = deltaRecord.InstanceUid & InstanceUidCounterMask;
                    if (observedSequence > maxObservedInstanceSequence)
                        maxObservedInstanceSequence = observedSequence;

                    if (deltaRecord.IsDeleted)
                    {
                        RegisterDeletedInstanceUid(deltaRecord.InstanceUid);
                        if (deltaRecord.IsResourceNodeDestroyed)
                        {
                            ulong tombstoneId = ResolveResourceNodeTombstoneId(in deltaRecord);
                            deltaRecord.ItemPersistentIdHash = tombstoneId;
                            RegisterResourceNodeTombstone(tombstoneId);
                            StageLoadedResourceNodeTombstoneRecord(in deltaRecord, tombstoneId);
                        }

                        if (TryBuildCompactDeltaRecord(deltaRecord, out PersistentWorldCompactDeltaRecord deletedCompactRecord))
                        {
                            _deltaRecordIndexByEntityId.TryAdd(deltaRecord.InstanceUid, _deltaRecords.Length);
                            _deltaRecords.AddNoResize(deletedCompactRecord);
                        }

                        continue;
                    }

                    if (deltaRecord.IsResourceNodeMetamorphosed)
                    {
                        RegisterResourceNodeMetamorphosis(deltaRecord.ItemPersistentIdHash);
                        if (TryBuildCompactDeltaRecord(deltaRecord, out PersistentWorldCompactDeltaRecord metamorphosisCompactRecord))
                        {
                            _deltaRecordIndexByEntityId.TryAdd(deltaRecord.InstanceUid, _deltaRecords.Length);
                            _deltaRecords.AddNoResize(metamorphosisCompactRecord);
                        }

                        continue;
                    }

                    PersistentWorldItemRecord record = deltaRecord.ToRecord(chunkSizeMeters);
                    if (record.IsCollected || IsDeletedInstanceUid(record.InstanceUid))
                        continue;

                    if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int existingDeltaIndex))
                    {
                        if (TryFindRecordIndexByInstanceUid(record.InstanceUid, out int existingRecordIndex))
                        {
                            PersistentWorldItemRecord existingRecord = _records[existingRecordIndex];
                            RemoveRecordIndexFromChunk(existingRecord.ChunkId, existingRecordIndex);
                            _records[existingRecordIndex] = record;
                            _recordsByChunk.Add(record.ChunkId, existingRecordIndex);
                            if (!record.IsFloraDestroyed && !record.IsFloraSeedPending && !record.IsFloraSeedReady && !record.IsFloraStateOverride && !record.IsResourceNodeMetamorphosed)
                            {
                                RegisterOrUpdatePoolSlot(existingRecordIndex, in record);
                                RegisterOrUpdateEntityState(in record);
                            }
                            else
                            {
                                RemoveEntityState(in record);
                            }
                        }

                        if (TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord replacementCompactRecord))
                            _deltaRecords[existingDeltaIndex] = replacementCompactRecord;

                        continue;
                    }

                    _records.AddNoResize(record);
                    int recordIndex = _records.Length - 1;
                    _recordsByChunk.Add(record.ChunkId, recordIndex);
                    if (!record.IsFloraDestroyed && !record.IsFloraSeedPending && !record.IsFloraSeedReady && !record.IsFloraStateOverride && !record.IsResourceNodeMetamorphosed)
                    {
                        RegisterOrUpdatePoolSlot(recordIndex, in record);
                        RegisterOrUpdateEntityState(in record);
                    }
                    if (TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                    {
                        _deltaRecordIndexByEntityId.TryAdd(record.InstanceUid, _deltaRecords.Length);
                        _deltaRecords.AddNoResize(compactRecord);
                    }
                }

                RebuildDeltaChunkLookup();
                RebaseInstanceUidCounter(maxObservedInstanceSequence);
            }

            if (scheduleHydration &&
                TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                _currentPlayerChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
                _playerChunkValid = true;
                _lastHydrationScanAup = playerAup;
                _hasLastHydrationScanAup = true;
                _hydrationFrameCounter++;
                RefreshHydrationWindow(in playerAup);
                EnsureHydrationSessionScheduled();
            }

            UpdateDiagnostics();
        }

        internal void PreloadTombstonesFromLoadedRecords(PersistentWorldDeltaRecord[] loadedRecords)
        {
            if (!_deletedInstanceUids.IsCreated)
                return;

            _deletedInstanceUids.Clear();
            if (_resourceNodeTombstoneIds.IsCreated)
                _resourceNodeTombstoneIds.Clear();
            if (_resourceNodeMetamorphosedIds.IsCreated)
                _resourceNodeMetamorphosedIds.Clear();

            if (loadedRecords == null || loadedRecords.Length <= 0)
                return;

            PreRegisterLoadedRecordTombstones(loadedRecords, math.min(loadedRecords.Length, maxTrackedItems));
        }

        private void PreRegisterLoadedRecordTombstones(PersistentWorldDeltaRecord[] loadedRecords, int restoreCount)
        {
            if (loadedRecords == null || restoreCount <= 0)
                return;

            for (int i = 0; i < restoreCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = loadedRecords[i];
                if (!deltaRecord.IsValid)
                    continue;

                if (deltaRecord.IsDeleted)
                {
                    RegisterDeletedInstanceUid(deltaRecord.InstanceUid);
                    if (deltaRecord.IsResourceNodeDestroyed)
                        RegisterResourceNodeTombstone(ResolveResourceNodeTombstoneId(in deltaRecord));
                }
                else if (deltaRecord.IsResourceNodeMetamorphosed)
                {
                    RegisterResourceNodeMetamorphosis(deltaRecord.ItemPersistentIdHash);
                }
            }
        }

        internal void RestoreFromIndexedSave(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            if (_indexedSectorDirectory == null)
                _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);

            if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(absolutePath, _indexedSectorDirectory, out _, out _))
            {
                _indexedSectorPagingEnabled = false;
                _indexedSectorSavePath = string.Empty;
                return;
            }

            _indexedSectorSavePath = absolutePath;
            _indexedSectorOverrideDirectory = Path.Combine(
                Path.GetDirectoryName(absolutePath) ?? string.Empty,
                CreateSectorOverrideDirectoryName(absolutePath));
            _indexedSectorPagingEnabled = _indexedSectorDirectory.Count > 0;
            _indexedSectorPagingInFlight = false;
            _playerSectorValid = false;
            _sectorOverrideCommitInFlight = false;
            _nextSectorOverrideCommitTime = 0f;
            _sectorOverrideStates?.Clear();

            if (!string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                Directory.CreateDirectory(_indexedSectorOverrideDirectory);

            if (TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                int2 playerSector = QuantizeSector(in playerAup);
                _currentPlayerSector = playerSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(playerSector);
            }
        }

        internal void DisableIndexedSavePaging()
        {
            if (_residentWorldPrefabHashes != null && _residentWorldPrefabHashes.Count > 0 && _resolvedItemCatalog != null)
            {
                _worldPrefabReleaseScratch.Clear();
                HashSet<int>.Enumerator releaseEnumerator = _residentWorldPrefabHashes.GetEnumerator();
                while (releaseEnumerator.MoveNext())
                    _worldPrefabReleaseScratch.Add(releaseEnumerator.Current);
                releaseEnumerator.Dispose();
                _resolvedItemCatalog.QueueWorldPrefabReleaseNonAlloc(_worldPrefabReleaseScratch);
                _residentWorldPrefabHashes.Clear();
            }

            _indexedSectorPagingEnabled = false;
            _indexedSectorPagingInFlight = false;
            _playerSectorValid = false;
            _indexedSectorSavePath = string.Empty;
            _indexedSectorOverrideDirectory = string.Empty;
            _nextSectorOverrideCommitTime = 0f;
            _indexedSectorDirectory?.Clear();
            _sectorOverrideStates?.Clear();
        }

        private void EnsureIndexedSectorPagingScheduled(int2 centerSector)
        {
            if (!_indexedSectorPagingEnabled || _indexedSectorPagingInFlight || string.IsNullOrEmpty(_indexedSectorSavePath))
                return;

            _indexedSectorPagingInFlight = true;
            _ = RunIndexedSectorPagingAsync(centerSector);
        }

        private async Awaitable RunIndexedSectorPagingAsync(int2 centerSector)
        {
            NativeArray<long> desiredSectorHashes = default;
            NativeList<PersistentWorldDeltaRecord> loadedSectorRecords = default;
            PersistentWorldDeltaRecord[] stagedRecords = null;
            Dictionary<uint, EntityDataRecord> stagedEntityStates = null;
            bool quarantinedSectorResetApplied = false;

            try
            {
                desiredSectorHashes = new NativeArray<long>(PagedSectorHashCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    desiredSectorHashes,
                    MemoryBudgetOwnerName,
                    IndexedSectorPagingDesiredHashesLabel,
                    NativeAllocationLifetime.TransientArena);
                int hashCursor = 0;
                for (int z = -1; z <= 1; z++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float planarDistanceSq = ((x * PagedSectorEdgeLengthMeters) * (x * PagedSectorEdgeLengthMeters)) +
                                                 ((z * PagedSectorEdgeLengthMeters) * (z * PagedSectorEdgeLengthMeters));
                        desiredSectorHashes[hashCursor++] = planarDistanceSq <= PagedSectorLoadRadiusSq
                            ? PackSectorHash(centerSector + new int2(x, z))
                            : InvalidPagedSectorHash;
                    }
                }

                if (!await SnapshotResidentSectorOverridesAsync(desiredSectorHashes))
                    return;
                loadedSectorRecords = new NativeList<PersistentWorldDeltaRecord>(math.max(16, maxTrackedItems), Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeList(
                    loadedSectorRecords,
                    MemoryBudgetOwnerName,
                    IndexedSectorPagingLoadedRecordsLabel,
                    NativeAllocationLifetime.TransientArena);

                await Awaitable.BackgroundThreadAsync();
                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(_indexedSectorSavePath, desiredSectorHashes, loadedSectorRecords, out string error))
                {
                    await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(string.Concat("[PersistentWorldRegistry] Indexed sector paging failed: ", error));
#endif
                    return;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(string.Concat("[PersistentWorldRegistry] Indexed sector paging recovered with quarantine: ", error));
#endif
                    await Awaitable.BackgroundThreadAsync();
                    quarantinedSectorResetApplied = ResetQuarantinedIndexedSectorsToPristine();
                }

                if (!ApplySectorOverrides(desiredSectorHashes, loadedSectorRecords, out string overrideError))
                {
                    await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector override merge failed: ", overrideError));
#endif
                    return;
                }

                if (!TryLoadSectorEntityStateOverrides(desiredSectorHashes, out stagedEntityStates, out string entityStateError))
                {
                    await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector entity-state restore failed: ", entityStateError));
#endif
                    return;
                }

                int loadedCount = loadedSectorRecords.Length;
                stagedRecords = loadedCount > 0 ? new PersistentWorldDeltaRecord[loadedCount] : Array.Empty<PersistentWorldDeltaRecord>();
                for (int i = 0; i < loadedCount; i++)
                    stagedRecords[i] = loadedSectorRecords[i];

                await Awaitable.MainThreadAsync();
                if (quarantinedSectorResetApplied)
                    Hecton8.UI.NotificationEvents.PushCritical(LocalizedSectorCorruptionMessage);

                await AwaitSectorPrefabPrewarmAsync(stagedRecords);
                RestoreFromLoadedRecords(stagedRecords, scheduleHydration: false);
                ApplyStagedEntityStates(stagedEntityStates);
                if (TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
                {
                    _currentPlayerChunk = AbsoluteUniversePosition.ResolveChunkId(in playerAup, chunkSizeMeters);
                    _playerChunkValid = true;
                    _lastHydrationScanAup = playerAup;
                    _hasLastHydrationScanAup = true;
                    _hydrationFrameCounter++;
                    RefreshHydrationWindow(in playerAup);
                    EnsureHydrationSessionScheduled();
                }

                UpdateResidentWorldPrefabResidency(_worldPrefabPrewarmHashScratch);
                MarkResidentSectorOverrides(desiredSectorHashes);
            }
            finally
            {
                if (loadedSectorRecords.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, IndexedSectorPagingLoadedRecordsLabel);
                    loadedSectorRecords.Dispose();
                }

                if (desiredSectorHashes.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(desiredSectorHashes);
                    desiredSectorHashes.Dispose();
                }

                _indexedSectorPagingInFlight = false;
            }
        }

        private bool ResetQuarantinedIndexedSectorsToPristine()
        {
            int resetCount = SaveBinaryStorage.CopyAndClearIndexedSectorQuarantineHashes(_quarantinedSectorResetScratch);
            if (resetCount <= 0 || string.IsNullOrEmpty(_indexedSectorSavePath))
                return false;

            bool resetApplied = false;
            for (int i = 0; i < resetCount; i++)
            {
                long sectorHash = _quarantinedSectorResetScratch[i];
                if (sectorHash == long.MinValue)
                    continue;

                if (!SaveBinaryStorage.TryResetIndexedPersistentWorldSectorToPristine(
                        _indexedSectorSavePath,
                        sectorHash,
                        chunkSizeMeters,
                        out string resetError))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(CreateHexErrorMessage("[PersistentWorldRegistry] Pristine sector reset failed for 0x", sectorHash, resetError));
#endif
                    continue;
                }

                resetApplied = true;
                _quarantinedSectorResetScratch[i] = long.MinValue;
            }

            return resetApplied;
        }

        private async Awaitable AwaitSectorPrefabPrewarmAsync(PersistentWorldDeltaRecord[] stagedRecords)
        {
            if (stagedRecords == null || stagedRecords.Length <= 0 || !TryEnsureItemLookup() || _resolvedItemCatalog == null)
                return;

            _worldPrefabPrewarmHashScratch.Clear();
            for (int i = 0; i < stagedRecords.Length; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = stagedRecords[i];
                if (!deltaRecord.IsValid || !TryResolveItemData(deltaRecord.ItemPersistentIdHash, out ItemData itemData) || itemData == null)
                    continue;

                int itemHashId = ComputeCatalogItemHash(itemData);
                if (itemHashId == 0 || _worldPrefabPrewarmHashScratch.Contains(itemHashId))
                    continue;

                _worldPrefabPrewarmHashScratch.Add(itemHashId);
            }

            if (_worldPrefabPrewarmHashScratch.Count <= 0)
                return;

            _resolvedItemCatalog.QueueWorldPrefabPrewarmNonAlloc(_worldPrefabPrewarmHashScratch);
            while (Application.isPlaying &&
                   ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this) &&
                   !_resolvedItemCatalog.AreWorldPrefabsReadyNonAlloc(_worldPrefabPrewarmHashScratch))
            {
                _resolvedItemCatalog.PumpWorldPrefabDispatchTickets();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }
        }

        private void TryRegisterRuntimeLoops()
        {
            if (_tickRegistered && _slowTickRegistered && _lateFrameRegistered)
                return;

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_slowTickRegistered)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }
        }

        private void TryUnregisterRuntimeLoops()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }
        }

        private void RefreshHydrationWindow(in AbsoluteUniversePosition playerAup)
        {
            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
            {
                int recordIndex = hydratedEnumerator.Current.Key;
                if (!IsValidRecordIndex(recordIndex))
                {
                    _recordIndexScratch.Add(recordIndex);
                    continue;
                }

                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.IsCollected || record.IsFloraDestroyed || record.IsFloraSeedPending || record.IsFloraSeedReady || record.IsFloraStateOverride || record.IsResourceNodeMetamorphosed || !ShouldKeepHydratedRecord(in record, in playerAup))
                    QueueRecordForDehydration(recordIndex);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                ClearHydratedSlot(_recordIndexScratch[i]);

            _recordIndexScratch.Clear();

            int radius = ResolveHydrationScanChunkRadius();
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        int3 chunkId = _currentPlayerChunk + new int3(x, y, z);
                        if (!_recordsByChunk.TryGetFirstValue(chunkId, out int recordIndex, out NativeParallelMultiHashMapIterator<int3> iterator))
                            continue;

                        do
                        {
                            if (!IsValidRecordIndex(recordIndex))
                                continue;

                            PersistentWorldItemRecord record = _records[recordIndex];
                            if (record.IsCollected ||
                                record.IsFloraDestroyed ||
                                record.IsFloraSeedPending ||
                                record.IsFloraSeedReady ||
                                record.IsFloraStateOverride ||
                                record.IsResourceNodeMetamorphosed ||
                                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex) ||
                                !ShouldHydrateDehydratedRecord(in record, in playerAup))
                            {
                                continue;
                            }

                            QueueRecordForHydration(recordIndex, in record, in playerAup);
                        }
                        while (_recordsByChunk.TryGetNextValue(out recordIndex, ref iterator));
                    }
                }
            }
        }

        private void SyncAllHydratedRecords()
        {
            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
            {
                int recordIndex = hydratedEnumerator.Current.Key;
                GameObject instance = hydratedEnumerator.Current.Value;
                if (instance == null)
                {
                    _recordIndexScratch.Add(recordIndex);
                    continue;
                }

                SyncRecordFromLiveInstance(recordIndex, instance, instance.transform);
            }

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                ClearHydratedSlot(_recordIndexScratch[i]);

            _recordIndexScratch.Clear();
        }

        private bool HydrateRecord(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (record.IsCollected ||
                IsDeletedInstanceUid(record.InstanceUid) ||
                record.IsFloraDestroyed ||
                record.IsFloraSeedPending ||
                record.IsFloraSeedReady ||
                record.IsFloraStateOverride ||
                record.IsResourceNodeMetamorphosed ||
                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex))
            {
                return false;
            }

            if (!TryGetPoolIndex(in record, out int poolIndex))
                return false;

            if (!TryResolveItemData(in record, out ItemData itemData) || itemData == null)
                return false;

            if (_resolvedItemCatalog == null)
                return false;

            int itemHashId = ComputeCatalogItemHash(itemData);
            if (itemHashId == 0)
                return false;

            if (!_resolvedItemCatalog.TryGetLoadedWorldPrefab(itemHashId, out GameObject prefab) || prefab == null)
            {
                _resolvedItemCatalog.QueueWorldPrefabPrewarm(itemHashId);
                return false;
            }

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return false;

            if (!pool.HasPool(prefab))
                pool.Warmup(prefab, 1);

            EntityDataRecord state = ResolveEntityState(in record);
            AbsoluteUniversePosition hydratedPosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
            int hydratedQuantity = math.max(1, state.Quantity);
            ulong itemGeneticsMask = ResolveItemGeneticsMask(in state);
            ushort itemQualityMilli = ResolveItemQualityMilli(in state);
            float3 runtimePosition = hydratedPosition.ToRuntimeFloat3();
            GameObject instance = pool.Spawn(prefab, new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), Quaternion.identity, allowExpand: false);
            if (instance == null)
                return false;

            if (instance.TryGetComponent(out PickupItem pickupItem))
            {
                pickupItem.Configure(itemData, hydratedQuantity, itemGeneticsMask, itemQualityMilli);
                pickupItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else if (instance.TryGetComponent(out HectonItem hectonItem))
            {
                hectonItem.SetItemData(itemData, hydratedQuantity, itemGeneticsMask, itemQualityMilli);
                hectonItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else
            {
                pool.Despawn(instance);
                return false;
            }

            _hydratedInstancesByRecordIndex[recordIndex] = instance;
            _hydratedInstancesBySlot[poolIndex] = instance;
            _poolSlotTransforms[poolIndex] = instance.transform;

            if (instance.TryGetComponent(out Rigidbody pooledRigidbody))
            {
                pooledRigidbody.mass = itemData.MassKg;
                pooledRigidbody.isKinematic = false;
                pooledRigidbody.linearVelocity = Vector3.zero;
                pooledRigidbody.angularVelocity = Vector3.zero;
                _poolSlotRigidbodies[poolIndex] = pooledRigidbody;
                Vector3 resolvedSpawnVelocity = Vector3.zero;
                bool hasResolvedSpawnVelocity = false;
                if (TryConsumeSpawnVelocityChange(record.InstanceUid, out float3 spawnVelocityChange))
                {
                    resolvedSpawnVelocity = new Vector3(spawnVelocityChange.x, spawnVelocityChange.y, spawnVelocityChange.z);
                    hasResolvedSpawnVelocity = IsFiniteNonZero(resolvedSpawnVelocity);
                }

                if (TryResolvePlatformInheritedVelocity(pooledRigidbody.position, out Vector3 platformVelocity))
                {
                    resolvedSpawnVelocity = platformVelocity;
                    hasResolvedSpawnVelocity = true;
                }

                if (hasResolvedSpawnVelocity)
                    pooledRigidbody.linearVelocity = resolvedSpawnVelocity;

                if (TryConsumeSpawnImpulse(record.InstanceUid, out float3 spawnImpulse))
                    PhysicsForceRouter.QueueForce(pooledRigidbody, new Vector3(spawnImpulse.x, spawnImpulse.y, spawnImpulse.z), ForceMode.Impulse);
            }
            else
            {
                _poolSlotRigidbodies[poolIndex] = null;
            }

            PoolSlotData slotData = _poolSlotData[poolIndex];
            WritePoolSlotPosition(ref slotData, in hydratedPosition);
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.Dirty);
            slotData.StateFlags |= (byte)PoolSlotStateFlags.Hydrated;
            slotData.RefCount = 1;
            slotData.HydrationFrame = _hydrationFrameCounter;
            slotData.LastVisibleFrame = _hydrationFrameCounter;
            slotData.StableFrames = 0;
            _poolSlotData[poolIndex] = slotData;
            return true;
        }

        private void DehydrateRecord(int recordIndex, bool syncTransformBackToRecord)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            if (!_hydratedInstancesByRecordIndex.TryGetValue(recordIndex, out GameObject instance))
                instance = _hydratedInstancesBySlot[recordIndex];

            if (instance == null)
            {
                ClearHydratedSlot(recordIndex);
                return;
            }

            _hydratedInstancesByRecordIndex.Remove(recordIndex);

            if (syncTransformBackToRecord)
                SyncRecordFromLiveInstance(recordIndex, instance, instance.transform);

            if (instance.TryGetComponent(out PickupItem pickupItem))
                pickupItem.ClearPersistentWorldRecord();

            if (instance.TryGetComponent(out HectonItem hectonItem))
                hectonItem.ClearPersistentWorldRecord();

            Rigidbody pooledRigidbody = _poolSlotRigidbodies[recordIndex];
            if (pooledRigidbody == null)
                instance.TryGetComponent(out pooledRigidbody);

            if (pooledRigidbody != null)
            {
                pooledRigidbody.linearVelocity = Vector3.zero;
                pooledRigidbody.angularVelocity = Vector3.zero;
                pooledRigidbody.isKinematic = true;
                pooledRigidbody.Sleep();
            }

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null)
            {
                pool.Despawn(instance);
            }
            else
            {
                instance.SetActive(false);
            }

            ClearHydratedSlot(recordIndex);
        }

        private void DehydrateAll(bool syncTransformsBackToRecords)
        {
            if (_hydratedInstancesByRecordIndex == null || _hydratedInstancesByRecordIndex.Count <= 0)
                return;

            _recordIndexScratch.Clear();

            Dictionary<int, GameObject>.Enumerator hydratedEnumerator = _hydratedInstancesByRecordIndex.GetEnumerator();
            while (hydratedEnumerator.MoveNext())
                _recordIndexScratch.Add(hydratedEnumerator.Current.Key);

            hydratedEnumerator.Dispose();

            for (int i = 0; i < _recordIndexScratch.Count; i++)
                DehydrateRecord(_recordIndexScratch[i], syncTransformsBackToRecords);

            _recordIndexScratch.Clear();
        }

        private void SyncRecordFromLiveInstance(int recordIndex, GameObject instance, Transform sourceTransform)
        {
            if (!IsValidRecordIndex(recordIndex) || sourceTransform == null)
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (record.IsCollected)
                return;

            if (!TryResolveLiveInstanceAup(sourceTransform, out AbsoluteUniversePosition position))
                return;

            int3 nextChunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            if (!math.all(nextChunkId == record.ChunkId))
            {
                RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
                _recordsByChunk.Add(nextChunkId, recordIndex);
                record.ChunkId = nextChunkId;
            }

            record.Position = position;
            EntityDataRecord state = CaptureEntityStateFromLiveInstance(in record, instance, in position);
            record.Quantity = state.Quantity;
            _records[recordIndex] = record;
            RegisterOrUpdatePoolSlot(recordIndex, in record);
            RegisterOrUpdateEntityState(in record, in state);
            UpsertDeltaRecord(in record);
        }

        private void RemoveRecordIndexFromChunk(int3 chunkId, int recordIndex)
        {
            if (!_recordsByChunk.TryGetFirstValue(chunkId, out int value, out NativeParallelMultiHashMapIterator<int3> iterator))
                return;

            do
            {
                if (value != recordIndex)
                    continue;

                _recordsByChunk.Remove(iterator);
                return;
            }
            while (_recordsByChunk.TryGetNextValue(out value, ref iterator));
        }

        private bool ShouldHydrateChunk(int3 chunkId)
        {
            if (!_playerChunkValid)
                return false;

            int radius = ResolveHydrationScanChunkRadius();
            int3 delta = chunkId - _currentPlayerChunk;
            return math.abs(delta.x) <= radius &&
                   math.abs(delta.y) <= radius &&
                   math.abs(delta.z) <= radius;
        }

        private bool ShouldHydrateDehydratedRecord(in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (record.IsCollected || record.IsFloraDestroyed || record.IsFloraSeedPending || record.IsFloraSeedReady || record.IsFloraStateOverride || record.IsResourceNodeMetamorphosed)
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= HydrateRadiusSq;
        }

        private bool ShouldKeepHydratedRecord(in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (record.IsCollected || record.IsFloraDestroyed || record.IsFloraSeedPending || record.IsFloraSeedReady || record.IsFloraStateOverride || record.IsResourceNodeMetamorphosed)
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= DehydrateRadiusSq;
        }

        private bool TryResolveItemData(in PersistentWorldItemRecord record, out ItemData itemData)
        {
            itemData = null;
            if (!TryEnsureItemLookup())
                return false;

            ulong itemPersistentIdHash = record.ItemPersistentIdHash != 0UL
                ? record.ItemPersistentIdHash
                : ComputePersistentIdHash(in record.ItemPersistentId);

            if (itemPersistentIdHash != 0UL &&
                _itemLookupByHash.TryGetValue(itemPersistentIdHash, out ItemData resolvedItem) &&
                resolvedItem != null)
            {
                itemData = resolvedItem;
                return true;
            }

            return false;
        }

        private bool TryResolveItemData(ulong itemPersistentIdHash, out ItemData itemData)
        {
            itemData = null;
            if (itemPersistentIdHash == 0UL || !TryEnsureItemLookup())
                return false;

            return _itemLookupByHash.TryGetValue(itemPersistentIdHash, out itemData) && itemData != null;
        }

        private bool TryEnsureItemLookup()
        {
            PlayerInventory playerInventory = GlobalRegistry.PlayerInventory != null
                ? GlobalRegistry.PlayerInventory.Inventory
                : null;
            ItemCatalog currentCatalog = playerInventory != null
                ? playerInventory.ItemCatalog
                : null;

            if (currentCatalog == null)
                return false;

            if (ReferenceEquals(_resolvedItemCatalog, currentCatalog) && _itemLookupByHash.Count > 0)
                return true;

            _resolvedItemCatalog = currentCatalog;
            _itemLookupByHash.Clear();
            _itemCatalogScratch.Clear();

            int itemCount = currentCatalog.GetAllItemsNonAlloc(_itemCatalogScratch);
            for (int i = 0; i < itemCount; i++)
            {
                ItemData itemData = _itemCatalogScratch[i];
                if (itemData == null)
                    continue;

                ulong itemHash = ComputePersistentIdHash(itemData.PersistentId);
                if (itemHash == 0UL)
                    continue;

                if (_itemLookupByHash.TryGetValue(itemHash, out ItemData existing) &&
                    existing != null &&
                    !ReferenceEquals(existing, itemData))
                {
                    continue;
                }

                _itemLookupByHash[itemHash] = itemData;
            }

            return _itemLookupByHash.Count > 0;
        }

        private bool IsValidRecordIndex(int recordIndex)
        {
            return recordIndex >= 0 && recordIndex < _records.Length;
        }

        private bool IsValidPoolIndex(int poolIndex)
        {
            return _poolSlotData.IsCreated && poolIndex >= 0 && poolIndex < _poolSlotData.Length;
        }

        private int ResolveHydrationScanChunkRadius()
        {
            int distanceRadius = (int)math.ceil(DehydrateRadiusMeters / math.max(1f, chunkSizeMeters));
            return math.max(hydrationRadiusInChunks, distanceRadius);
        }

        private static int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolute.x / PagedSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PagedSectorEdgeLengthMeters));
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static bool IsDesiredPagedSector(NativeArray<long> desiredSectorHashes, long sectorHash)
        {
            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                if (desiredSectorHashes[i] == InvalidPagedSectorHash)
                    continue;

                if (desiredSectorHashes[i] == sectorHash)
                    return true;
            }

            return false;
        }

        private async Awaitable<bool> SnapshotResidentSectorOverridesAsync(NativeArray<long> desiredSectorHashes)
        {
            if (!_indexedSectorPagingEnabled ||
                string.IsNullOrEmpty(_indexedSectorOverrideDirectory) ||
                !_records.IsCreated)
            {
                return true;
            }

            SyncAllHydratedRecords();

            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] Ã¢â‚¬â€ resident sector snapshot buckets during page-out Ã¢â‚¬â€ owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectors = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            Dictionary<long, List<EntityDataRecord>> sectorEntityStates = new Dictionary<long, List<EntityDataRecord>>(16);
            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (record.IsCollected)
                    continue;

                long sectorHash = ComputeSectorHash(in record.Position);
                if (IsDesiredPagedSector(desiredSectorHashes, sectorHash))
                    continue;

                if (!sectors.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] Ã¢â‚¬â€ one resident sector override record bucket Ã¢â‚¬â€ owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectors.Add(sectorHash, bucket);
                }

                bucket.Add(PersistentWorldDeltaRecord.FromRecord(in record, chunkSizeMeters));

                if (!sectorEntityStates.TryGetValue(sectorHash, out List<EntityDataRecord> entityStateBucket))
                {
                    entityStateBucket = new List<EntityDataRecord>(16);
                    sectorEntityStates.Add(sectorHash, entityStateBucket);
                }

                entityStateBucket.Add(ResolveEntityState(in record));
            }

            if (_floraSpawnStateByInstanceUid.IsCreated)
            {
                NativeHashMap<uint, EntityDataRecord>.Enumerator floraEnumerator = _floraSpawnStateByInstanceUid.GetEnumerator();
                while (floraEnumerator.MoveNext())
                {
                    EntityDataRecord state = floraEnumerator.Current.Value;
                    if (!IsFloraSpawnTimestampState(in state))
                        continue;

                    AbsoluteUniversePosition floraPosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                    long sectorHash = ComputeSectorHash(in floraPosition);
                    if (IsDesiredPagedSector(desiredSectorHashes, sectorHash))
                        continue;

                    if (!sectors.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> floraBucket))
                    {
                        floraBucket = new List<PersistentWorldDeltaRecord>(0);
                        sectors.Add(sectorHash, floraBucket);
                    }

                    if (!sectorEntityStates.TryGetValue(sectorHash, out List<EntityDataRecord> floraStateBucket))
                    {
                        floraStateBucket = new List<EntityDataRecord>(4);
                        sectorEntityStates.Add(sectorHash, floraStateBucket);
                    }

                    floraStateBucket.Add(state);
                }

                floraEnumerator.Dispose();
            }

            if (sectors.Count <= 0)
                return true;

            float now = Time.unscaledTime;
            List<SectorOverrideWriteResult> writeResults = new List<SectorOverrideWriteResult>(sectors.Count);
            List<SectorEntityStateWriteWork> entityStateWriteWork = new List<SectorEntityStateWriteWork>(sectorEntityStates.Count);
            string failureMessage = string.Empty;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                Dictionary<long, List<PersistentWorldDeltaRecord>>.Enumerator sectorEnumerator = sectors.GetEnumerator();
                int pendingEntityStateWrites = 0;
                try
                {
                    while (sectorEnumerator.MoveNext())
                    {
                        KeyValuePair<long, List<PersistentWorldDeltaRecord>> pair = sectorEnumerator.Current;
                        List<PersistentWorldDeltaRecord> bucket = pair.Value;
                        NativeArray<PersistentWorldDeltaRecord> sectorRecords = new NativeArray<PersistentWorldDeltaRecord>(bucket.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                        NativeMemorySentinel.RegisterNativeArray(
                            sectorRecords,
                            MemoryBudgetOwnerName,
                            SectorOverrideSnapshotRecordsLabel,
                            NativeAllocationLifetime.TempJob);
                        try
                        {
                            for (int i = 0; i < bucket.Count; i++)
                                sectorRecords[i] = bucket[i];

                            string tempPath = ResolveSectorOverrideTempPath(pair.Key);
                            if (!SaveBinaryStorage.TryWriteIndexedPersistentWorldSectorOverride(tempPath, pair.Key, sectorRecords, chunkSizeMeters, out string error))
                            {
                                failureMessage = CreateHexErrorMessage("[PersistentWorldRegistry] Sector override snapshot failed for 0x", pair.Key, error);
                                break;
                            }

                            string entityStateTempPath = string.Empty;
                            if (sectorEntityStates.TryGetValue(pair.Key, out List<EntityDataRecord> entityStateBucket) &&
                                entityStateBucket != null &&
                                entityStateBucket.Count > 0)
                            {
                                while (pendingEntityStateWrites >= MaxPendingEntityStateTempWrites && string.IsNullOrEmpty(failureMessage))
                                {
                                    if (!TryDrainCompletedSectorEntityStateWriteWork(entityStateWriteWork, ref pendingEntityStateWrites, out failureMessage))
                                        break;

                                    if (pendingEntityStateWrites >= MaxPendingEntityStateTempWrites)
                                    {
                                        await Awaitable.MainThreadAsync();
                                        PublishSectorCompressionPerformanceWarning(
                                            _sectorEntityStateThrottleWarningHash,
                                            unchecked((uint)pendingEntityStateWrites),
                                            MaxPendingEntityStateTempWrites,
                                            ref _lastEntityStateThrottleTelemetryFrame);
                                        await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
                                        await Awaitable.BackgroundThreadAsync();
                                    }
                                }

                                if (!string.IsNullOrEmpty(failureMessage))
                                    break;

                                NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(entityStateBucket.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                                NativeMemorySentinel.RegisterNativeArray(
                                    sectorStates,
                                    MemoryBudgetOwnerName,
                                    SectorOverrideEntityStatesLabel,
                                    NativeAllocationLifetime.TempJob);
                                try
                                {
                                    for (int stateIndex = 0; stateIndex < entityStateBucket.Count; stateIndex++)
                                        sectorStates[stateIndex] = entityStateBucket[stateIndex];

                                    entityStateTempPath = ResolveSectorEntityStateTempPath(pair.Key);
                                    if (!SaveBinaryStorage.TryScheduleIndexedSectorEntityStateOverrideWrite(
                                            entityStateTempPath,
                                            pair.Key,
                                            sectorStates,
                                            chunkSizeMeters,
                                            out SaveBinaryStorage.IndexedSectorEntityStateWriteHandle writeHandle,
                                            out string entityStateError))
                                    {
                                        failureMessage = CreateHexErrorMessage("[PersistentWorldRegistry] Sector entity-state snapshot failed for 0x", pair.Key, entityStateError);
                                        break;
                                    }

                                    entityStateWriteWork.Add(new SectorEntityStateWriteWork
                                    {
                                        SectorHash = pair.Key,
                                        TempPath = entityStateTempPath,
                                        WriteHandle = writeHandle
                                    });
                                    pendingEntityStateWrites++;
                                }
                                finally
                                {
                                    if (sectorStates.IsCreated)
                                    {
                                        NativeMemorySentinel.UnregisterNativeArray(sectorStates);
                                        sectorStates.Dispose();
                                    }
                                }
                            }

                            writeResults.Add(new SectorOverrideWriteResult(pair.Key, tempPath, entityStateTempPath));
                        }
                        finally
                        {
                            if (sectorRecords.IsCreated)
                            {
                                NativeMemorySentinel.UnregisterNativeArray(sectorRecords);
                                sectorRecords.Dispose();
                            }
                        }

                        if (!string.IsNullOrEmpty(failureMessage))
                            break;
                    }
                }
                finally
                {
                    sectorEnumerator.Dispose();
                }

                while (pendingEntityStateWrites > 0 && string.IsNullOrEmpty(failureMessage))
                {
                    TryDrainCompletedSectorEntityStateWriteWork(entityStateWriteWork, ref pendingEntityStateWrites, out failureMessage);

                    if (pendingEntityStateWrites > 0 && string.IsNullOrEmpty(failureMessage))
                    {
                        await Awaitable.MainThreadAsync();
                        await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);
                        await Awaitable.BackgroundThreadAsync();
                    }
                }

                await Awaitable.MainThreadAsync();
            }
            catch (OperationCanceledException)
            {
                DisposeEntityStateWriteWorkDeferred(entityStateWriteWork);
                return false;
            }

            if (!string.IsNullOrEmpty(failureMessage))
            {
                DisposeEntityStateWriteWorkDeferred(entityStateWriteWork);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(failureMessage);
#endif
                return false;
            }

            for (int i = 0; i < writeResults.Count; i++)
            {
                SectorOverrideWriteResult result = writeResults[i];
                if (!_sectorOverrideStates.TryGetValue(result.SectorHash, out SectorOverrideState state))
                {
                    state = new SectorOverrideState();
                    _sectorOverrideStates.Add(result.SectorHash, state);
                }

                state.TempPath = result.TempPath;
                if (!string.IsNullOrEmpty(result.EntityStateTempPath))
                {
                    state.EntityStateTempPath = result.EntityStateTempPath;
                }
                else if (!string.IsNullOrEmpty(state.EntityStateTempPath) && File.Exists(state.EntityStateTempPath))
                {
                    File.Delete(state.EntityStateTempPath);
                    state.EntityStateTempPath = string.Empty;
                }

                state.LastUnloadedTime = now;
                state.IsResident = false;
            }

            return true;
        }

        private bool ApplySectorOverrides(
            NativeArray<long> desiredSectorHashes,
            NativeList<PersistentWorldDeltaRecord> loadedSectorRecords,
            out string error)
        {
            error = string.Empty;
            if (!_indexedSectorPagingEnabled || _sectorOverrideStates == null || _sectorOverrideStates.Count <= 0)
                return true;

            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] Ã¢â‚¬â€ paged sector merge map during override resolution Ã¢â‚¬â€ owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectorBuckets = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            for (int i = 0; i < loadedSectorRecords.Length; i++)
            {
                PersistentWorldDeltaRecord record = loadedSectorRecords[i];
                if (!record.IsValid)
                    continue;

                AbsoluteUniversePosition unpackedPosition = record.UnpackPosition(chunkSizeMeters);
                long sectorHash = ComputeSectorHash(in unpackedPosition);
                if (!sectorBuckets.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] Ã¢â‚¬â€ one paged sector merge bucket Ã¢â‚¬â€ owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectorBuckets.Add(sectorHash, bucket);
                }

                bucket.Add(record);
            }

            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                long sectorHash = desiredSectorHashes[i];
                if (sectorHash == InvalidPagedSectorHash)
                    continue;

                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                    string.IsNullOrEmpty(state.TempPath) ||
                    !File.Exists(state.TempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldSectorOverride(state.TempPath, out long loadedSectorHash, out PersistentWorldDeltaRecord[] overrideRecords, out error))
                    return false;

                if (loadedSectorHash != sectorHash)
                {
                    error = CreateHexMessage("Sector override hash mismatch for temp block 0x", sectorHash, ".");
                    return false;
                }

                // COLD ALLOC: List<PersistentWorldDeltaRecord>[N] Ã¢â‚¬â€ override-resolved sector records loaded from temp block Ã¢â‚¬â€ owner: PersistentWorldRegistry
                List<PersistentWorldDeltaRecord> replacement = new List<PersistentWorldDeltaRecord>(overrideRecords.Length);
                for (int recordIndex = 0; recordIndex < overrideRecords.Length; recordIndex++)
                {
                    if (overrideRecords[recordIndex].IsValid)
                        replacement.Add(overrideRecords[recordIndex]);
                }

                sectorBuckets[sectorHash] = replacement;
            }

            loadedSectorRecords.Clear();
            Dictionary<long, List<PersistentWorldDeltaRecord>>.Enumerator enumerator = sectorBuckets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                List<PersistentWorldDeltaRecord> bucket = enumerator.Current.Value;
                for (int i = 0; i < bucket.Count; i++)
                    loadedSectorRecords.Add(bucket[i]);
            }

            enumerator.Dispose();
            return true;
        }

        private bool TryLoadSectorEntityStateOverrides(
            NativeArray<long> desiredSectorHashes,
            out Dictionary<uint, EntityDataRecord> stagedEntityStates,
            out string error)
        {
            stagedEntityStates = null;
            error = string.Empty;

            if (!_indexedSectorPagingEnabled ||
                _sectorOverrideStates == null ||
                _sectorOverrideStates.Count <= 0)
            {
                return true;
            }

            // COLD ALLOC: Dictionary<uint,EntityDataRecord>[64] Ã¢â‚¬â€ staged sector entity-state restore map during indexed paging Ã¢â‚¬â€ owner: PersistentWorldRegistry
            stagedEntityStates = new Dictionary<uint, EntityDataRecord>(64);
            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                long sectorHash = desiredSectorHashes[i];
                if (sectorHash == InvalidPagedSectorHash)
                    continue;

                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                    state == null ||
                    string.IsNullOrEmpty(state.EntityStateTempPath) ||
                    !File.Exists(state.EntityStateTempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(
                        state.EntityStateTempPath,
                        out long loadedSectorHash,
                        out EntityDataRecord[] entityStates,
                        out error))
                {
                    return false;
                }

                if (loadedSectorHash != sectorHash)
                {
                    error = CreateHexMessage("Sector entity-state override hash mismatch for temp block 0x", sectorHash, ".");
                    return false;
                }

                for (int stateIndex = 0; stateIndex < entityStates.Length; stateIndex++)
                {
                    EntityDataRecord entityState = entityStates[stateIndex];
                    if (entityState.InstanceUid == 0u)
                        continue;

                    stagedEntityStates[entityState.InstanceUid] = entityState;
                }
            }

            return true;
        }

        private void ApplyStagedEntityStates(Dictionary<uint, EntityDataRecord> stagedEntityStates)
        {
            if (stagedEntityStates == null ||
                stagedEntityStates.Count <= 0 ||
                !_entityStateByInstanceUid.IsCreated ||
                !_floraSpawnStateByInstanceUid.IsCreated)
            {
                return;
            }

            Dictionary<uint, EntityDataRecord>.Enumerator enumerator = stagedEntityStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<uint, EntityDataRecord> pair = enumerator.Current;
                EntityDataRecord pairValue = pair.Value;
                if (IsFloraSpawnTimestampState(in pairValue))
                {
                    _floraSpawnStateByInstanceUid.Remove(pair.Key);
                    _floraSpawnStateByInstanceUid.TryAdd(pair.Key, pairValue);
                    continue;
                }

                _entityStateByInstanceUid.Remove(pair.Key);
                _entityStateByInstanceUid.TryAdd(pair.Key, pairValue);
            }

            enumerator.Dispose();
        }

        private void UpdateResidentWorldPrefabResidency(List<int> nextResidentHashes)
        {
            if (_residentWorldPrefabHashes == null || _resolvedItemCatalog == null)
                return;

            _worldPrefabReleaseScratch.Clear();
            HashSet<int>.Enumerator residentEnumerator = _residentWorldPrefabHashes.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                int residentHash = residentEnumerator.Current;
                bool stillResident = false;
                if (nextResidentHashes != null)
                {
                    for (int i = 0; i < nextResidentHashes.Count; i++)
                    {
                        if (nextResidentHashes[i] != residentHash)
                            continue;

                        stillResident = true;
                        break;
                    }
                }

                if (!stillResident)
                    _worldPrefabReleaseScratch.Add(residentHash);
            }

            residentEnumerator.Dispose();

            if (_worldPrefabReleaseScratch.Count > 0)
                _resolvedItemCatalog.QueueWorldPrefabReleaseNonAlloc(_worldPrefabReleaseScratch);

            _residentWorldPrefabHashes.Clear();
            if (nextResidentHashes == null)
                return;

            for (int i = 0; i < nextResidentHashes.Count; i++)
                _residentWorldPrefabHashes.Add(nextResidentHashes[i]);
        }

        private void MarkResidentSectorOverrides(NativeArray<long> desiredSectorHashes)
        {
            if (_sectorOverrideStates == null || _sectorOverrideStates.Count <= 0)
                return;

            Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.IsResident = false;
            enumerator.Dispose();

            for (int i = 0; i < desiredSectorHashes.Length; i++)
            {
                if (desiredSectorHashes[i] == InvalidPagedSectorHash)
                    continue;

                if (_sectorOverrideStates.TryGetValue(desiredSectorHashes[i], out SectorOverrideState state))
                    state.IsResident = true;
            }
        }

        private void TryScheduleSectorOverrideCommit()
        {
            if (!_indexedSectorPagingEnabled ||
                _sectorOverrideCommitInFlight ||
                _sectorOverrideStates == null ||
                _sectorOverrideStates.Count <= 0 ||
                Time.unscaledTime < _nextSectorOverrideCommitTime)
            {
                return;
            }

            _nextSectorOverrideCommitTime = Time.unscaledTime + SectorOverrideCommitIntervalSeconds;
            _sectorOverrideCommitInFlight = true;
            _ = RunSectorOverrideCommitAsync();
        }

        private async Awaitable RunSectorOverrideCommitAsync()
        {
            try
            {
                _dueSectorOverrideCommitHashes.Clear();
                float now = Time.unscaledTime;

                Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<long, SectorOverrideState> pair = enumerator.Current;
                    SectorOverrideState state = pair.Value;
                    if (state == null || state.IsResident || string.IsNullOrEmpty(state.TempPath) || !File.Exists(state.TempPath))
                        continue;

                    if (now - state.LastUnloadedTime >= SectorOverrideCommitDelaySeconds)
                        _dueSectorOverrideCommitHashes.Add(pair.Key);
                }
                enumerator.Dispose();

                if (_dueSectorOverrideCommitHashes.Count <= 0)
                    return;

                await Awaitable.BackgroundThreadAsync();
                for (int i = 0; i < _dueSectorOverrideCommitHashes.Count; i++)
                {
                    long sectorHash = _dueSectorOverrideCommitHashes[i];
                    if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                        state == null ||
                        string.IsNullOrEmpty(state.TempPath) ||
                        !File.Exists(state.TempPath))
                    {
                        continue;
                    }

                    if (!SaveBinaryStorage.TryCommitIndexedPersistentWorldSectorOverride(_indexedSectorSavePath, state.TempPath, out string error))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        await Awaitable.MainThreadAsync();
                        Debug.LogError(CreateHexErrorMessage("[PersistentWorldRegistry] Sector override commit failed for 0x", sectorHash, error));
                        await Awaitable.BackgroundThreadAsync();
#endif
                    }
                    else if (!string.IsNullOrEmpty(state.EntityStateTempPath) && File.Exists(state.EntityStateTempPath))
                    {
                        File.Delete(state.EntityStateTempPath);
                        state.EntityStateTempPath = string.Empty;
                    }
                }

                await Awaitable.MainThreadAsync();
                for (int i = 0; i < _dueSectorOverrideCommitHashes.Count; i++)
                {
                    long sectorHash = _dueSectorOverrideCommitHashes[i];
                    if (_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) &&
                        state != null &&
                        !state.IsResident &&
                        !string.IsNullOrEmpty(state.TempPath) &&
                        !File.Exists(state.TempPath))
                    {
                        _sectorOverrideStates.Remove(sectorHash);
                    }
                }
            }
            finally
            {
                _sectorOverrideCommitInFlight = false;
            }
        }

        private string ResolveSectorOverrideTempPath(long sectorHash)
        {
            if (string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(_indexedSectorOverrideDirectory, CreateSectorTempFileName(sectorHash, SectorOverrideTempFileSuffix));
        }

        private string ResolveSectorEntityStateTempPath(long sectorHash)
        {
            if (string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(_indexedSectorOverrideDirectory, CreateSectorTempFileName(sectorHash, SectorEntityStateTempFileSuffix));
        }

        internal bool TryCacheFaunaHibernationState(in EntityDataRecord faunaState)
        {
            if (!IsFaunaHibernationState(in faunaState))
                return false;

            return TryCacheSpecialEntityState(in faunaState);
        }

        internal bool TryCacheFaunaEggState(in EntityDataRecord eggState)
        {
            if (!IsFaunaEggState(in eggState))
                return false;

            return TryCacheSpecialEntityState(in eggState);
        }

        internal bool TryCacheWhaleFallPoiState(uint instanceUid, int speciesId, in AbsoluteUniversePosition position, float currentTimeSeconds)
        {
            EntityDataRecord whaleFallState = CreateWhaleFallPoiState(instanceUid, speciesId, in position, currentTimeSeconds);
            return TryCacheSpecialEntityState(in whaleFallState);
        }

        internal float ResolveWhaleFallSpawnInfluence01(Vector3 worldPosition, float currentTimeSeconds, float radiusMeters)
        {
            if (!_entityStateByInstanceUid.IsCreated || radiusMeters <= 0f || _whaleFallPoiInstanceUidCount <= 0)
                return 0f;

            double radiusSq = (double)radiusMeters * radiusMeters;
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
                return 0f;

            float bestInfluence01 = 0f;
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _whaleFallPoiInstanceUidCount; readIndex++)
            {
                uint instanceUid = _whaleFallPoiInstanceUids[readIndex];
                if (instanceUid == 0u ||
                    !_entityStateByInstanceUid.TryGetValue(instanceUid, out EntityDataRecord state) ||
                    !IsWhaleFallPoiState(in state))
                {
                    continue;
                }

                float expireTimeSeconds = GetWhaleFallExpireTimeSeconds(in state);
                if (expireTimeSeconds <= currentTimeSeconds)
                {
                    _entityStateByInstanceUid.Remove(instanceUid);
                    continue;
                }

                _whaleFallPoiInstanceUids[writeIndex++] = instanceUid;

                AbsoluteUniversePosition whaleFallAup = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in whaleFallAup, in queryAup);
                if (distanceSq > radiusSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / math.max(0.001d, radiusSq)));
                float life01 = math.saturate((expireTimeSeconds - currentTimeSeconds) / WhaleFallDurationSeconds);
                bestInfluence01 = math.max(bestInfluence01, distance01 * math.max(0.25f, life01));
            }

            for (int i = writeIndex; i < _whaleFallPoiInstanceUidCount; i++)
                _whaleFallPoiInstanceUids[i] = 0u;

            _whaleFallPoiInstanceUidCount = writeIndex;
            if (_whaleFallPoiInstanceUidWriteCursor >= _whaleFallPoiInstanceUids.Length)
                _whaleFallPoiInstanceUidWriteCursor = 0;

            return bestInfluence01;
        }

        private bool TryCacheSpecialEntityState(in EntityDataRecord entityState)
        {
            return TryCacheSpecialEntityState(in entityState, _entityStateScratch);
        }

        private bool TryCacheSpecialEntityState(in EntityDataRecord entityState, List<EntityDataRecord> scratch)
        {
            return TryCacheSpecialEntityState(in entityState, scratch, deferMemoryRegistrationUntilWrite: false);
        }

        private bool TryCacheSpecialEntityState(
            in EntityDataRecord entityState,
            List<EntityDataRecord> scratch,
            bool deferMemoryRegistrationUntilWrite)
        {
            if (!IsFaunaHibernationState(in entityState) && !IsWhaleFallPoiState(in entityState) && !IsFaunaEggState(in entityState))
                return false;

            if (scratch == null)
                return false;

            if (!deferMemoryRegistrationUntilWrite)
                RegisterSpecialEntityStateInMemory(in entityState);

            if (!_indexedSectorPagingEnabled || string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return false;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in entityState.Position);
            long sectorHash = ComputeSectorHash(in position);
            string entityStateTempPath = ResolveSectorEntityStateTempPath(sectorHash);
            if (string.IsNullOrEmpty(entityStateTempPath))
                return false;

            scratch.Clear();
            if (File.Exists(entityStateTempPath) &&
                SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(entityStateTempPath, out long loadedSectorHash, out EntityDataRecord[] entityStates, out string readError))
            {
                if (loadedSectorHash != sectorHash)
                    return false;

                for (int i = 0; i < entityStates.Length; i++)
                {
                    EntityDataRecord existingState = entityStates[i];
                    if (existingState.InstanceUid == entityState.InstanceUid)
                        continue;

                    scratch.Add(existingState);
                }
            }

            scratch.Add(entityState);
            if (!TryWriteEntityStateTempBlock(sectorHash, entityStateTempPath, scratch, chunkSizeMeters))
                return false;

            if (deferMemoryRegistrationUntilWrite)
                RegisterSpecialEntityStateInMemory(in entityState);

            if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state))
            {
                state = new SectorOverrideState();
                _sectorOverrideStates.Add(sectorHash, state);
            }

            state.EntityStateTempPath = entityStateTempPath;
            state.LastUnloadedTime = Time.unscaledTime;
            return true;
        }

        internal int ConsumeCachedFaunaHibernationStates(Vector3 playerPosition, float restoreRadiusMeters, List<EntityDataRecord> destination)
        {
            if (!TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup))
                return 0;

            return ConsumeCachedFaunaHibernationStates(in playerAup, restoreRadiusMeters, destination);
        }

        internal int ConsumeCachedFaunaHibernationStates(in AbsoluteUniversePosition playerAup, float restoreRadiusMeters, List<EntityDataRecord> destination)
        {
            if (destination == null || restoreRadiusMeters <= 0f || !_indexedSectorPagingEnabled || string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return 0;

            int2 playerSector = QuantizeSector(in playerAup);
            double restoreRadiusSq = restoreRadiusMeters * restoreRadiusMeters;
            int restoredCount = 0;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    long sectorHash = PackSectorHash(playerSector + new int2(dx, dz));
                    string entityStateTempPath = ResolveSectorEntityStateTempPath(sectorHash);
                    if (string.IsNullOrEmpty(entityStateTempPath) || !File.Exists(entityStateTempPath))
                        continue;

                    if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(entityStateTempPath, out long loadedSectorHash, out EntityDataRecord[] entityStates, out _))
                        continue;

                    if (loadedSectorHash != sectorHash)
                        continue;

                    _entityStateScratch.Clear();
                    bool consumedAnyFauna = false;
                    for (int i = 0; i < entityStates.Length; i++)
                    {
                        EntityDataRecord entityState = entityStates[i];
                        if (IsFaunaEggState(in entityState))
                        {
                            AbsoluteUniversePosition eggAup = AbsoluteUniversePosition.FromAlignedBlit(in entityState.Position);
                            if (AbsoluteUniversePosition.DistanceSq(in eggAup, in playerAup) > restoreRadiusSq ||
                                GetFaunaEggHatchTimeSeconds(in entityState) > Time.time)
                            {
                                _entityStateScratch.Add(entityState);
                                continue;
                            }

                            destination.Add(CreateFaunaHibernationState(
                                entityState.InstanceUid,
                                GetFaunaEggSpeciesId(in entityState),
                                1f,
                                in eggAup,
                                false,
                                false,
                                Time.time,
                                0.15f));
                            _entityStateByInstanceUid.Remove(entityState.InstanceUid);
                            restoredCount++;
                            consumedAnyFauna = true;
                            continue;
                        }

                        if (!IsFaunaHibernationState(in entityState))
                        {
                            _entityStateScratch.Add(entityState);
                            continue;
                        }

                        AbsoluteUniversePosition faunaAup = AbsoluteUniversePosition.FromAlignedBlit(in entityState.Position);
                        if (AbsoluteUniversePosition.DistanceSq(in faunaAup, in playerAup) > restoreRadiusSq)
                        {
                            _entityStateScratch.Add(entityState);
                            continue;
                        }

                        destination.Add(CreateFaunaHibernationState(
                            entityState.InstanceUid,
                            GetFaunaHibernationSpeciesId(in entityState),
                            GetFaunaHibernationHealth(in entityState),
                            in faunaAup,
                            GetFaunaHibernationLargeThreatFlag(in entityState),
                            GetFaunaHibernationPredatorFlag(in entityState),
                            GetFaunaHibernationSleepStartTimeSeconds(in entityState),
                            GetFaunaHibernationHunger01(in entityState)));
                        _entityStateByInstanceUid.Remove(entityState.InstanceUid);
                        restoredCount++;
                        consumedAnyFauna = true;
                    }

                    if (!consumedAnyFauna)
                        continue;

                    if (_entityStateScratch.Count > 0)
                    {
                        TryWriteEntityStateTempBlock(sectorHash, entityStateTempPath, _entityStateScratch, chunkSizeMeters);
                    }
                    else
                    {
                        File.Delete(entityStateTempPath);
                        if (_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state))
                            state.EntityStateTempPath = string.Empty;
                    }
                }
            }

            return restoredCount;
        }

        internal int ReconcileFaunaHibernationSectorPopulation(
            int2 sectorCoord,
            int preyPopulation,
            int predatorPopulation,
            int maxPreyPopulation,
            int maxPredatorPopulation)
        {
            if (!_indexedSectorPagingEnabled || string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return 0;

            long sectorHash = PackSectorHash(sectorCoord);
            string entityStateTempPath = ResolveSectorEntityStateTempPath(sectorHash);
            if (string.IsNullOrEmpty(entityStateTempPath) || !File.Exists(entityStateTempPath))
                return 0;

            if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(entityStateTempPath, out long loadedSectorHash, out EntityDataRecord[] entityStates, out _) ||
                loadedSectorHash != sectorHash)
            {
                return 0;
            }

            int preyRecordCount = 0;
            int predatorRecordCount = 0;
            EntityDataRecord preyTemplate = default;
            EntityDataRecord predatorTemplate = default;
            EntityDataRecord apexPredatorCandidate = default;
            EntityDataRecord preyVictimCandidate = default;
            bool hasPreyTemplate = false;
            bool hasPredatorTemplate = false;
            bool hasApexPredatorCandidate = false;
            bool hasPreyVictimCandidate = false;

            for (int i = 0; i < entityStates.Length; i++)
            {
                EntityDataRecord state = entityStates[i];
                if (!IsFaunaHibernationState(in state))
                    continue;

                bool largeThreat = GetFaunaHibernationLargeThreatFlag(in state);
                bool predator = GetFaunaHibernationPredatorFlag(in state);
                if (largeThreat)
                {
                    if (predator && !hasApexPredatorCandidate)
                    {
                        apexPredatorCandidate = state;
                        hasApexPredatorCandidate = true;
                    }

                    continue;
                }

                if (predator)
                {
                    predatorRecordCount++;
                    if (!hasPredatorTemplate)
                    {
                        predatorTemplate = state;
                        hasPredatorTemplate = true;
                    }
                }
                else
                {
                    preyRecordCount++;
                    if (!hasPreyVictimCandidate)
                    {
                        preyVictimCandidate = state;
                        hasPreyVictimCandidate = true;
                    }

                    if (!hasPreyTemplate)
                    {
                        preyTemplate = state;
                        hasPreyTemplate = true;
                    }
                }
            }

            int preyTarget = ResolveEquilibriumRecordTarget(preyRecordCount, preyPopulation, maxPreyPopulation);
            int predatorTarget = ResolveEquilibriumRecordTarget(predatorRecordCount, predatorPopulation, maxPredatorPopulation);
            uint hibernatedPredationVictimUid = ResolveHibernatedPredationVictimUid(
                sectorHash,
                preyPopulation,
                predatorPopulation,
                in apexPredatorCandidate,
                hasApexPredatorCandidate,
                in preyVictimCandidate,
                hasPreyVictimCandidate);
            if (hibernatedPredationVictimUid != 0u && preyTarget > 0)
                preyTarget--;

            int keptPrey = 0;
            int keptPredators = 0;
            int changedRecords = 0;

            _entityStateScratch.Clear();
            for (int i = 0; i < entityStates.Length; i++)
            {
                EntityDataRecord state = entityStates[i];
                if (!IsFaunaHibernationState(in state) || GetFaunaHibernationLargeThreatFlag(in state))
                {
                    _entityStateScratch.Add(state);
                    continue;
                }

                if (state.InstanceUid == hibernatedPredationVictimUid)
                {
                    TombstoneHibernatedFaunaVictim(in state);
                    changedRecords++;
                    continue;
                }

                if (GetFaunaHibernationPredatorFlag(in state))
                {
                    if (keptPredators < predatorTarget)
                    {
                        _entityStateScratch.Add(state);
                        keptPredators++;
                    }
                    else
                    {
                        _entityStateByInstanceUid.Remove(state.InstanceUid);
                        changedRecords++;
                    }
                }
                else
                {
                    if (keptPrey < preyTarget)
                    {
                        _entityStateScratch.Add(state);
                        keptPrey++;
                    }
                    else
                    {
                        _entityStateByInstanceUid.Remove(state.InstanceUid);
                        changedRecords++;
                    }
                }
            }

            changedRecords += SeedEquilibriumFaunaRecords(
                sectorHash,
                in preyTemplate,
                hasPreyTemplate,
                preyTarget - keptPrey,
                false);

            changedRecords += SeedEquilibriumFaunaRecords(
                sectorHash,
                in predatorTemplate,
                hasPredatorTemplate,
                predatorTarget - keptPredators,
                true);

            if (changedRecords <= 0)
                return 0;

            if (_entityStateScratch.Count > 0)
            {
                if (!TryWriteEntityStateTempBlock(sectorHash, entityStateTempPath, _entityStateScratch, chunkSizeMeters))
                    return 0;
            }
            else
            {
                File.Delete(entityStateTempPath);
            }

            if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState sectorState))
            {
                sectorState = new SectorOverrideState();
                _sectorOverrideStates.Add(sectorHash, sectorState);
            }

            sectorState.EntityStateTempPath = _entityStateScratch.Count > 0 ? entityStateTempPath : string.Empty;
            sectorState.LastUnloadedTime = Time.unscaledTime;
            return changedRecords;
        }

        internal int MigrateApexFaunaHibernationStatesToward(Vector3 attractorPosition, float searchRadiusMeters, float stepMeters)
        {
            if (!TryResolveAupFromRuntimeOrigin(attractorPosition, out AbsoluteUniversePosition attractorAup))
                return 0;

            return MigrateApexFaunaHibernationStatesToward(in attractorAup, searchRadiusMeters, stepMeters);
        }

        internal int MigrateApexFaunaHibernationStatesToward(in AbsoluteUniversePosition attractorAup, float searchRadiusMeters, float stepMeters)
        {
            if (!_indexedSectorPagingEnabled ||
                string.IsNullOrEmpty(_indexedSectorOverrideDirectory) ||
                searchRadiusMeters <= 0f ||
                stepMeters <= 0f)
            {
                return 0;
            }

            double3 attractorAbsolute = attractorAup.ToAbsoluteDouble3();
            int2 centerSector = QuantizeSector(in attractorAup);
            int sectorRadius = math.max(1, (int)math.ceil(searchRadiusMeters / PagedSectorEdgeLengthMeters));
            double searchRadiusSq = searchRadiusMeters * searchRadiusMeters;
            int migratedCount = 0;
            ClearApexMigrationVisitedScratch();

            for (int dz = -sectorRadius; dz <= sectorRadius; dz++)
            {
                for (int dx = -sectorRadius; dx <= sectorRadius; dx++)
                {
                    long sectorHash = PackSectorHash(centerSector + new int2(dx, dz));
                    string entityStateTempPath = ResolveSectorEntityStateTempPath(sectorHash);
                    if (string.IsNullOrEmpty(entityStateTempPath) || !File.Exists(entityStateTempPath))
                        continue;

                    if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(entityStateTempPath, out long loadedSectorHash, out EntityDataRecord[] entityStates, out _) ||
                        loadedSectorHash != sectorHash)
                    {
                        continue;
                    }

                    _entityStateScratch.Clear();
                    bool changedSector = false;
                    int sectorMigratedCount = 0;
                    for (int i = 0; i < entityStates.Length; i++)
                    {
                        EntityDataRecord state = entityStates[i];
                        if (!IsFaunaHibernationState(in state) ||
                            !GetFaunaHibernationLargeThreatFlag(in state) ||
                            !GetFaunaHibernationPredatorFlag(in state))
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        AbsoluteUniversePosition currentAup = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                        if (AbsoluteUniversePosition.DistanceSq(in currentAup, in attractorAup) > searchRadiusSq)
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        if (!TryReserveApexMigrationVisitedUid(state.InstanceUid))
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        double3 currentAbsolute = currentAup.ToAbsoluteDouble3();
                        double3 toAttractor = attractorAbsolute - currentAbsolute;
                        double ax = math.abs(toAttractor.x);
                        double ay = math.abs(toAttractor.y);
                        double az = math.abs(toAttractor.z);
                        double dominantDistance = math.max(ax, math.max(ay, az));
                        if (dominantDistance <= 0.01d)
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        double moveMeters = math.min((double)stepMeters, dominantDistance);
                        double3 moveDelta;
                        if (ax >= ay && ax >= az)
                            moveDelta = new double3(toAttractor.x < 0d ? -moveMeters : moveMeters, 0d, 0d);
                        else if (ay >= az)
                            moveDelta = new double3(0d, toAttractor.y < 0d ? -moveMeters : moveMeters, 0d);
                        else
                            moveDelta = new double3(0d, 0d, toAttractor.z < 0d ? -moveMeters : moveMeters);

                        AbsoluteUniversePosition migratedAup = AbsoluteUniversePosition.FromAbsolutePosition(currentAbsolute + moveDelta);
                        EntityDataRecord migratedState = state;
                        migratedState.Position = migratedAup.ToAlignedBlit();
                        long migratedSectorHash = ComputeSectorHash(in migratedAup);
                        if (migratedSectorHash == sectorHash)
                        {
                            _entityStateScratch.Add(migratedState);
                            RegisterSpecialEntityStateInMemory(in migratedState);
                        }
                        else if (!TryCacheSpecialEntityState(
                                     in migratedState,
                                     _entityStateSectorTargetScratch,
                                     deferMemoryRegistrationUntilWrite: true))
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        changedSector = true;
                        sectorMigratedCount++;
                    }

                    if (!changedSector)
                        continue;

                    if (!TryCommitEntityStateTempBlockOrDelete(sectorHash, entityStateTempPath, _entityStateScratch))
                        continue;

                    migratedCount += sectorMigratedCount;
                }
            }

            return migratedCount;
        }

        private bool TryCommitEntityStateTempBlockOrDelete(long sectorHash, string entityStateTempPath, List<EntityDataRecord> entityStates)
        {
            if (entityStates == null)
                return false;

            if (entityStates.Count > 0)
            {
                if (!TryWriteEntityStateTempBlock(sectorHash, entityStateTempPath, entityStates, chunkSizeMeters))
                    return false;

                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState sectorState))
                {
                    sectorState = new SectorOverrideState();
                    _sectorOverrideStates.Add(sectorHash, sectorState);
                }

                sectorState.EntityStateTempPath = entityStateTempPath;
                sectorState.LastUnloadedTime = Time.unscaledTime;
                return true;
            }

            if (File.Exists(entityStateTempPath))
                File.Delete(entityStateTempPath);

            if (_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState emptySectorState))
            {
                emptySectorState.EntityStateTempPath = string.Empty;
                emptySectorState.LastUnloadedTime = Time.unscaledTime;
            }

            return true;
        }

        private void RegisterSpecialEntityStateInMemory(in EntityDataRecord entityState)
        {
            if (!_entityStateByInstanceUid.IsCreated || entityState.InstanceUid == 0u)
                return;

            _entityStateByInstanceUid.Remove(entityState.InstanceUid);
            _entityStateByInstanceUid.TryAdd(entityState.InstanceUid, entityState);
            if (IsWhaleFallPoiState(in entityState))
                RegisterWhaleFallPoiInfluenceUid(entityState.InstanceUid);
        }

        private void RegisterWhaleFallPoiInfluenceUid(uint instanceUid)
        {
            if (instanceUid == 0u || _whaleFallPoiInstanceUids == null || _whaleFallPoiInstanceUids.Length == 0)
                return;

            for (int i = 0; i < _whaleFallPoiInstanceUidCount; i++)
            {
                if (_whaleFallPoiInstanceUids[i] == instanceUid)
                    return;
            }

            if (_whaleFallPoiInstanceUidCount < _whaleFallPoiInstanceUids.Length)
            {
                _whaleFallPoiInstanceUids[_whaleFallPoiInstanceUidCount++] = instanceUid;
                return;
            }

            int slot = _whaleFallPoiInstanceUidWriteCursor;
            _whaleFallPoiInstanceUids[slot] = instanceUid;
            _whaleFallPoiInstanceUidWriteCursor = (slot + 1) % _whaleFallPoiInstanceUids.Length;
        }

        private void ClearWhaleFallPoiInfluenceUidIndex()
        {
            if (_whaleFallPoiInstanceUids != null)
            {
                for (int i = 0; i < _whaleFallPoiInstanceUidCount; i++)
                    _whaleFallPoiInstanceUids[i] = 0u;
            }

            _whaleFallPoiInstanceUidCount = 0;
            _whaleFallPoiInstanceUidWriteCursor = 0;
        }

        private void ClearApexMigrationVisitedScratch()
        {
            _apexMigrationVisitedUidCount = 0;
        }

        private bool TryReserveApexMigrationVisitedUid(uint instanceUid)
        {
            if (instanceUid == 0u)
                return true;

            if (_apexMigrationVisitedUids == null || _apexMigrationVisitedUids.Length == 0)
                return false;

            for (int i = 0; i < _apexMigrationVisitedUidCount; i++)
            {
                if (_apexMigrationVisitedUids[i] == instanceUid)
                    return false;
            }

            if (_apexMigrationVisitedUidCount >= _apexMigrationVisitedUids.Length)
                return false;

            _apexMigrationVisitedUids[_apexMigrationVisitedUidCount++] = instanceUid;
            return true;
        }

        private void TombstoneHibernatedFaunaVictim(in EntityDataRecord entityState)
        {
            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Remove(entityState.InstanceUid);

            TryRegisterFaunaTombstone(entityState.InstanceUid);
            RegisterResourceNodeTombstone(entityState.InstanceUid);
        }

        private static uint ResolveHibernatedPredationVictimUid(
            long sectorHash,
            int preyPopulation,
            int predatorPopulation,
            in EntityDataRecord apexPredatorCandidate,
            bool hasApexPredatorCandidate,
            in EntityDataRecord preyVictimCandidate,
            bool hasPreyVictimCandidate)
        {
            if (!hasApexPredatorCandidate ||
                !hasPreyVictimCandidate ||
                apexPredatorCandidate.InstanceUid == 0u ||
                preyVictimCandidate.InstanceUid == 0u)
            {
                return 0u;
            }

            uint sectorLow = (uint)sectorHash;
            uint sectorHigh = (uint)((ulong)sectorHash >> 32);
            uint rollHash = math.hash(new uint4(
                sectorLow,
                sectorHigh,
                apexPredatorCandidate.InstanceUid,
                preyVictimCandidate.InstanceUid));

            float roll01 = (rollHash & 0xFFFFu) * (1f / 65535f);
            int safePreyPopulation = math.max(0, preyPopulation);
            int safePredatorPopulation = math.max(1, predatorPopulation);
            float pressureDenominator = math.max(1f, safePreyPopulation + safePredatorPopulation);
            float predatorPressure01 = math.saturate(safePredatorPopulation / pressureDenominator);
            float apexPower = HibernatedApexPredationBasePower + predatorPressure01 * 0.25f + roll01 * 0.1f;
            float preyEscapePower = HibernatedPreyEscapeBasePower + (1f - predatorPressure01) * 0.2f;
            return apexPower >= preyEscapePower ? preyVictimCandidate.InstanceUid : 0u;
        }

        private int SeedEquilibriumFaunaRecords(long sectorHash, in EntityDataRecord template, bool hasTemplate, int missingCount, bool predator)
        {
            if (!hasTemplate || missingCount <= 0)
                return 0;

            int birthCount = math.min(missingCount, EcosystemFaunaRecordBirthLimitPerSectorPass);
            int seededCount = 0;
            for (int i = 0; i < birthCount && _entityStateScratch.Count < _entityStateScratch.Capacity; i++)
            {
                uint instanceUid = BuildEquilibriumFaunaInstanceUid(sectorHash, template.InstanceUid, i, predator);
                if (instanceUid == 0u ||
                    (_entityStateByInstanceUid.IsCreated && _entityStateByInstanceUid.ContainsKey(instanceUid)))
                {
                    continue;
                }

                uint jitterHash = instanceUid ^ (uint)(i * 747796405);
                float2 jitterDirection = ResolveScatterPlanarDirection(jitterHash);
                float radius = (((jitterHash >> 8) & 0xFFFFu) * (EcosystemFaunaCloneJitterRadiusMeters / 65535f));
                AbsoluteUniversePosition templateAup = AbsoluteUniversePosition.FromAlignedBlit(in template.Position);
                AbsoluteUniversePosition seededAup = AbsoluteUniversePosition.OffsetMeters(
                    in templateAup,
                    new double3(
                        (double)jitterDirection.x * radius,
                        0d,
                        (double)jitterDirection.y * radius));
                EntityDataRecord seededState = CreateFaunaHibernationState(
                    instanceUid,
                    GetFaunaHibernationSpeciesId(in template),
                    GetFaunaHibernationHealth(in template),
                    in seededAup,
                    false,
                    predator,
                    Time.time,
                    GetFaunaHibernationHunger01(in template));

                _entityStateScratch.Add(seededState);
                RegisterSpecialEntityStateInMemory(in seededState);
                seededCount++;
            }

            return seededCount;
        }

        private static int ResolveEquilibriumRecordTarget(int currentRecordCount, int population, int maxPopulation)
        {
            if (currentRecordCount <= 0 || population <= 0 || maxPopulation <= 0)
                return 0;

            float normalizedPopulation = math.saturate((float)population / maxPopulation);
            int target = (int)math.ceil(currentRecordCount * normalizedPopulation);
            if (normalizedPopulation >= 0.85f)
                target = math.min(currentRecordCount + EcosystemFaunaRecordBirthLimitPerSectorPass, target + 1);

            return math.max(0, target);
        }

        private static uint BuildEquilibriumFaunaInstanceUid(long sectorHash, uint templateUid, int birthIndex, bool predator)
        {
            unchecked
            {
                uint hash = (uint)sectorHash ^ (uint)(sectorHash >> 32);
                hash ^= templateUid * 16777619u;
                hash ^= (uint)(birthIndex + 1) * 2166136261u;
                hash ^= predator ? 0xA711E5u : 0x51EDC0DEu;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash == 0u ? 1u : hash;
            }
        }

        private bool TryWriteEntityStateTempBlock(long sectorHash, string entityStateTempPath, List<EntityDataRecord> entityStates, int chunkSizeMeters)
        {
            if (string.IsNullOrEmpty(entityStateTempPath) || entityStates == null || entityStates.Count <= 0)
                return false;

            if (_pendingEntityStateTempWrites == null)
                return false;

            DrainPendingEntityStateTempWrites(MaxEntityStateTempWriteCompletionsPerTick);
            if (_pendingEntityStateTempWrites.Count >= MaxPendingEntityStateTempWrites)
            {
                PublishSectorCompressionPerformanceWarning(
                    _sectorEntityStateQueueOverflowWarningHash,
                    unchecked((uint)sectorHash),
                    _pendingEntityStateTempWrites.Count,
                    ref _lastEntityStateQueueOverflowTelemetryFrame);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PersistentWorldRegistry] Async entity-state temp write queue is full; dropped non-critical terrain/flora state.");
#endif
                return false;
            }

            NativeArray<EntityDataRecord> sectorStates = new NativeArray<EntityDataRecord>(entityStates.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(
                sectorStates,
                MemoryBudgetOwnerName,
                SectorEntityStateAsyncWriteStatesLabel,
                NativeAllocationLifetime.TempJob);
            try
            {
                for (int i = 0; i < entityStates.Count; i++)
                    sectorStates[i] = entityStates[i];

                if (!SaveBinaryStorage.TryScheduleIndexedSectorEntityStateOverrideWrite(
                        entityStateTempPath,
                        sectorHash,
                        sectorStates,
                        math.max(1, chunkSizeMeters),
                        out SaveBinaryStorage.IndexedSectorEntityStateWriteHandle writeHandle,
                        out string error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(CreateHexErrorMessage("[PersistentWorldRegistry] Async entity-state temp write schedule failed for 0x", sectorHash, error));
#endif
                    return false;
                }

                _pendingEntityStateTempWrites.Add(new SectorEntityStateWriteWork
                {
                    SectorHash = sectorHash,
                    TempPath = entityStateTempPath,
                    WriteHandle = writeHandle
                });
                return true;
            }
            finally
            {
                if (sectorStates.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(sectorStates);
                    sectorStates.Dispose();
                }
            }
        }

        private void DrainPendingEntityStateTempWrites(int maxCompletions)
        {
            if (_pendingEntityStateTempWrites == null || _pendingEntityStateTempWrites.Count <= 0 || maxCompletions <= 0)
                return;

            int completedCount = 0;
            for (int i = 0; i < _pendingEntityStateTempWrites.Count && completedCount < maxCompletions;)
            {
                SectorEntityStateWriteWork work = _pendingEntityStateTempWrites[i];
                if (work == null || !work.WriteHandle.IsCreated)
                {
                    _pendingEntityStateTempWrites.RemoveAt(i);
                    continue;
                }

                if (!work.WriteHandle.IsCompleted || HasEarlierPendingEntityStateTempWrite(i, work.SectorHash))
                {
                    i++;
                    continue;
                }

                if (!SaveBinaryStorage.TryCompleteIndexedSectorEntityStateOverrideWrite(ref work.WriteHandle, out string error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(CreateHexErrorMessage("[PersistentWorldRegistry] Async entity-state temp write failed for 0x", work.SectorHash, error));
#endif
                }

                work.IsCompleted = true;
                _pendingEntityStateTempWrites.RemoveAt(i);
                completedCount++;
            }
        }

        private bool HasEarlierPendingEntityStateTempWrite(int index, long sectorHash)
        {
            for (int i = 0; i < index; i++)
            {
                SectorEntityStateWriteWork earlier = _pendingEntityStateTempWrites[i];
                if (earlier != null && earlier.SectorHash == sectorHash && earlier.WriteHandle.IsCreated)
                    return true;
            }

            return false;
        }

        private static void PublishSectorCompressionPerformanceWarning(
            uint warningHash,
            uint contextHash,
            float scalarValue,
            ref int lastTelemetryFrame)
        {
            if (!Application.isPlaying)
                return;

            int frame = Time.frameCount;
            if (lastTelemetryFrame == frame)
                return;

            lastTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, scalarValue);
        }

        private static float StopwatchTicksToMilliseconds(long elapsedTicks)
        {
            long clampedTicks = elapsedTicks > 0L ? elapsedTicks : 0L;
            return (float)((double)clampedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private void DisposePendingEntityStateTempWritesDeferred()
        {
            if (_pendingEntityStateTempWrites == null || _pendingEntityStateTempWrites.Count <= 0)
                return;

            JobHandle disposeHandle = default;
            for (int i = 0; i < _pendingEntityStateTempWrites.Count; i++)
            {
                SectorEntityStateWriteWork work = _pendingEntityStateTempWrites[i];
                if (work == null)
                    continue;

                disposeHandle = SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref work.WriteHandle, disposeHandle);
            }

            _pendingEntityStateTempWrites.Clear();
            JobHandle.ScheduleBatchedJobs();
        }

        private static bool TryDrainCompletedSectorEntityStateWriteWork(
            List<SectorEntityStateWriteWork> writeWork,
            ref int pendingEntityStateWrites,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (writeWork == null || writeWork.Count <= 0)
                return true;

            for (int i = 0; i < writeWork.Count; i++)
            {
                SectorEntityStateWriteWork work = writeWork[i];
                if (work == null || work.IsCompleted || !work.WriteHandle.IsCreated || !work.WriteHandle.IsCompleted)
                    continue;

                if (!SaveBinaryStorage.TryCompleteIndexedSectorEntityStateOverrideWrite(ref work.WriteHandle, out string error))
                {
                    failureMessage = CreateHexErrorMessage("[PersistentWorldRegistry] Sector entity-state snapshot failed for 0x", work.SectorHash, error);
                    return false;
                }

                work.IsCompleted = true;
                pendingEntityStateWrites = math.max(0, pendingEntityStateWrites - 1);
            }

            return true;
        }

        private static void DisposeEntityStateWriteWorkDeferred(List<SectorEntityStateWriteWork> writeWork)
        {
            if (writeWork == null || writeWork.Count <= 0)
                return;

            JobHandle disposeHandle = default;
            for (int i = 0; i < writeWork.Count; i++)
            {
                SectorEntityStateWriteWork work = writeWork[i];
                if (work == null || work.IsCompleted)
                    continue;

                disposeHandle = SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref work.WriteHandle, disposeHandle);
            }

            JobHandle.ScheduleBatchedJobs();
        }

        private static long ComputeSectorHash(in AbsoluteUniversePosition position)
        {
            return PackSectorHash(QuantizeSector(in position));
        }

        private void RegisterOrUpdatePoolSlot(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (!IsValidPoolIndex(recordIndex) || !_guidToPoolIndex.IsCreated)
                return;

            PoolSlotData slotData = _poolSlotData[recordIndex];
            ulong nextGuid = ComputePoolGuid(in record);
            ulong previousGuid = slotData.BoundGuid;
            if (previousGuid != 0UL &&
                previousGuid != nextGuid &&
                _guidToPoolIndex.TryGetValue(previousGuid, out int previousIndex) &&
                previousIndex == recordIndex)
            {
                _guidToPoolIndex.Remove(previousGuid);
            }

            _guidToPoolIndex.Remove(nextGuid);
            _guidToPoolIndex.TryAdd(nextGuid, recordIndex);

            slotData.BoundGuid = nextGuid;
            WritePoolSlotPosition(ref slotData, in record.Position);
            _poolSlotData[recordIndex] = slotData;
        }

        private void RegisterOrUpdateEntityState(in PersistentWorldItemRecord record)
        {
            EntityDataRecord state = CreateEntityStateFromRecord(in record);
            RegisterOrUpdateEntityState(in record, in state);
        }

        private void RegisterOrUpdateEntityState(in PersistentWorldItemRecord record, in EntityDataRecord state)
        {
            if (record.InstanceUid == 0u || !_entityStateByInstanceUid.IsCreated)
                return;

            _entityStateByInstanceUid.Remove(record.InstanceUid);
            _entityStateByInstanceUid.TryAdd(record.InstanceUid, state);
        }

        private void RemoveEntityState(in PersistentWorldItemRecord record)
        {
            if (!_entityStateByInstanceUid.IsCreated || record.InstanceUid == 0u)
                return;

            _entityStateByInstanceUid.Remove(record.InstanceUid);
            if (_spawnImpulseByInstanceUid.IsCreated)
                _spawnImpulseByInstanceUid.Remove(record.InstanceUid);
        }

        private EntityDataRecord ResolveEntityState(in PersistentWorldItemRecord record)
        {
            if (_entityStateByInstanceUid.IsCreated &&
                record.InstanceUid != 0u &&
                _entityStateByInstanceUid.TryGetValue(record.InstanceUid, out EntityDataRecord state))
            {
                return state;
            }

            return CreateEntityStateFromRecord(in record);
        }

        private static EntityDataRecord CreateEntityStateFromRecord(in PersistentWorldItemRecord record)
        {
            return CreateEntityStateFromRecord(in record, 0UL, DefaultItemQualityMilli);
        }

        private static EntityDataRecord CreateEntityStateFromRecord(in PersistentWorldItemRecord record, ulong geneticsMask, ushort qualityMilli)
        {
            AbsoluteUniversePositionBlit128 position = record.Position.ToAlignedBlit();
            position.Reserved = geneticsMask;
            return new EntityDataRecord
            {
                Position = position,
                Quantity = math.max(1, record.Quantity),
                Integrity01 = ResolveItemQuality01(qualityMilli),
                InventoryHash = unchecked((int)geneticsMask),
                InstanceUid = record.InstanceUid
            };
        }

        private static bool IsSpecialEntityState(in EntityDataRecord state)
        {
            return IsFaunaHibernationState(in state) || IsFaunaEggState(in state) || IsFloraSpawnTimestampState(in state) || IsWhaleFallPoiState(in state);
        }

        private static ulong ResolveItemGeneticsMask(in EntityDataRecord state)
        {
            return IsSpecialEntityState(in state)
                ? 0UL
                : (state.Position.Reserved != 0UL ? state.Position.Reserved : (uint)state.InventoryHash);
        }

        private static ushort ResolveItemQualityMilli(in EntityDataRecord state)
        {
            if (IsSpecialEntityState(in state) || !float.IsFinite(state.Integrity01))
                return DefaultItemQualityMilli;

            return (ushort)math.clamp((int)math.round(math.saturate(state.Integrity01) * DefaultItemQualityMilli), 0, DefaultItemQualityMilli);
        }

        private static float ResolveItemQuality01(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return DefaultItemQuality01;

            return math.saturate((float)math.min((int)qualityMilli, (int)DefaultItemQualityMilli) / DefaultItemQualityMilli);
        }

        internal static EntityDataRecord CreateFaunaHibernationState(
            uint instanceUid,
            int speciesId,
            float health,
            in AbsoluteUniversePosition position,
            bool isLargeThreat,
            bool isPredator,
            float sleepStartTimeSeconds,
            float hunger01 = 0f)
        {
            int flags = 0;
            if (isLargeThreat)
                flags |= FaunaStateFlagLargeThreat;
            if (isPredator)
                flags |= FaunaStateFlagPredator;

            uint packedSleepStart = PackFaunaSleepStartTimeSeconds(sleepStartTimeSeconds);
            uint packedState = FaunaHibernationStateTypeMask |
                               ((packedSleepStart & (uint)FaunaSleepStartMaxEncoded) << FaunaSleepStartShift) |
                               (uint)(flags & FaunaStateFlagsMask);
            AbsoluteUniversePositionBlit128 packedPosition = position.ToAlignedBlit();
            packedPosition.Reserved = PackFaunaVitals(health, hunger01);

            return new EntityDataRecord
            {
                Position = packedPosition,
                Quantity = math.max(1, speciesId),
                Integrity01 = health,
                InventoryHash = unchecked((int)packedState),
                InstanceUid = instanceUid
            };
        }

        internal static bool IsFaunaHibernationState(in EntityDataRecord state)
        {
            return state.InstanceUid != 0u &&
                   state.Quantity > 0 &&
                   (((uint)state.InventoryHash & 0xFF000000u) == FaunaHibernationStateTypeMask);
        }

        internal static EntityDataRecord CreateFaunaEggState(
            uint instanceUid,
            int speciesId,
            in AbsoluteUniversePosition position,
            float laidTimeSeconds,
            float incubationSeconds)
        {
            float hatchTimeSeconds = math.max(0f, laidTimeSeconds) + math.max(1f, incubationSeconds);
            return new EntityDataRecord
            {
                Position = position.ToAlignedBlit(),
                Quantity = math.max(1, speciesId),
                Integrity01 = hatchTimeSeconds,
                InventoryHash = unchecked((int)FaunaEggStateTypeMask),
                InstanceUid = instanceUid
            };
        }

        internal static bool IsFaunaEggState(in EntityDataRecord state)
        {
            return state.InstanceUid != 0u &&
                   state.Quantity > 0 &&
                   (((uint)state.InventoryHash & 0xFF000000u) == FaunaEggStateTypeMask);
        }

        internal static EntityDataRecord CreateWhaleFallPoiState(
            uint instanceUid,
            int speciesId,
            in AbsoluteUniversePosition position,
            float currentTimeSeconds)
        {
            return new EntityDataRecord
            {
                Position = position.ToAlignedBlit(),
                Quantity = math.max(1, speciesId),
                Integrity01 = math.max(0f, currentTimeSeconds) + WhaleFallDurationSeconds,
                InventoryHash = unchecked((int)WhaleFallStateTypeMask),
                InstanceUid = instanceUid
            };
        }

        internal static bool IsWhaleFallPoiState(in EntityDataRecord state)
        {
            return state.InstanceUid != 0u &&
                   state.Quantity > 0 &&
                   (((uint)state.InventoryHash & 0xFF000000u) == WhaleFallStateTypeMask);
        }

        internal static EntityDataRecord CreateFloraSpawnTimestampState(
            uint instanceUid,
            float spawnPlayTimeSeconds,
            in AbsoluteUniversePosition position)
        {
            return new EntityDataRecord
            {
                Position = position.ToAlignedBlit(),
                Quantity = PackFloraSpawnTimestampMinutes(spawnPlayTimeSeconds),
                Integrity01 = 1f,
                InventoryHash = unchecked((int)FloraSpawnTimestampStateTypeMask),
                InstanceUid = instanceUid
            };
        }

        internal static bool IsFloraSpawnTimestampState(in EntityDataRecord state)
        {
            return state.InstanceUid != 0u &&
                   state.Quantity > 0 &&
                   (((uint)state.InventoryHash & 0xFF000000u) == FloraSpawnTimestampStateTypeMask);
        }

        internal static float GetFloraSpawnTimestampSeconds(in EntityDataRecord state)
        {
            return UnpackFloraSpawnTimestampSeconds(state.Quantity);
        }

        internal static int GetFaunaHibernationSpeciesId(in EntityDataRecord state)
        {
            return state.Quantity;
        }

        internal static int GetFaunaEggSpeciesId(in EntityDataRecord state)
        {
            return state.Quantity;
        }

        internal static float GetFaunaEggHatchTimeSeconds(in EntityDataRecord state)
        {
            return IsFaunaEggState(in state) && math.isfinite(state.Integrity01)
                ? math.max(0f, state.Integrity01)
                : 0f;
        }

        internal static float GetFaunaHibernationHealth(in EntityDataRecord state)
        {
            return TryUnpackFaunaVitals(state.Position.Reserved, out float health, out _)
                ? health
                : state.Integrity01;
        }

        internal static float GetFaunaHibernationHunger01(in EntityDataRecord state)
        {
            return TryUnpackFaunaVitals(state.Position.Reserved, out _, out float hunger01)
                ? hunger01
                : 0f;
        }

        internal static float StableRandom01(uint uid)
        {
            uint value = uid != 0u ? uid : 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        internal static bool GetFaunaHibernationLargeThreatFlag(in EntityDataRecord state)
        {
            return (state.InventoryHash & FaunaStateFlagLargeThreat) != 0;
        }

        internal static bool GetFaunaHibernationPredatorFlag(in EntityDataRecord state)
        {
            return (state.InventoryHash & FaunaStateFlagPredator) != 0;
        }

        internal static float GetFaunaHibernationSleepStartTimeSeconds(in EntityDataRecord state)
        {
            uint encoded = ((uint)state.InventoryHash & FaunaHibernationStateValueMask) >> FaunaSleepStartShift;
            return encoded <= 0u ? 0f : encoded * FaunaSleepStartQuantumSeconds;
        }

        internal static float GetWhaleFallExpireTimeSeconds(in EntityDataRecord state)
        {
            return IsWhaleFallPoiState(in state) && math.isfinite(state.Integrity01)
                ? state.Integrity01
                : 0f;
        }

        private static ulong PackFaunaVitals(float health, float hunger01)
        {
            uint packedHealth = math.asuint(math.max(0f, health));
            uint packedHunger = (uint)math.clamp((int)(math.saturate(hunger01) * 255f + 0.5f), 0, 255);
            return ((ulong)packedHealth << 32) | packedHunger;
        }

        private static bool TryUnpackFaunaVitals(ulong packedVitals, out float health, out float hunger01)
        {
            if (packedVitals == 0UL)
            {
                health = 0f;
                hunger01 = 0f;
                return false;
            }

            uint packedHealth = (uint)(packedVitals >> 32);
            uint packedHunger = (uint)(packedVitals & 0xFFFFFFFFUL);
            health = math.max(0f, math.asfloat(packedHealth));
            hunger01 = (packedHunger & 0xFFFFFF00u) == 0u
                ? packedHunger * (1f / 255f)
                : math.saturate(math.asfloat(packedHunger));
            if (!math.isfinite(health))
                health = 0f;
            if (!math.isfinite(hunger01))
                hunger01 = 0f;

            return true;
        }

        private static bool TryResolvePlatformInheritedVelocity(Vector3 runtimePosition, out Vector3 inheritedVelocity)
        {
            inheritedVelocity = Vector3.zero;
            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            if (submarine == null || !submarine.IsTransportPlatformActive)
                return false;

            if (!IsInsideActiveModuleInterior(runtimePosition) &&
                !IsInsideSubmarineFallbackBounds(submarine, runtimePosition))
            {
                return false;
            }

            inheritedVelocity = submarine.GetPlatformPointVelocity(runtimePosition);
            return IsFiniteNonZero(inheritedVelocity);
        }

        private static bool IsInsideActiveModuleInterior(Vector3 runtimePosition)
        {
            int moduleCount = BaseModule.ActiveModuleCount;
            if (moduleCount <= 0)
                return false;

            for (int i = 0; i < moduleCount; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null)
                    continue;

                if (!module.TryGetInteriorHazardBounds(out Vector3 worldCenter, out float radius))
                    continue;

                if ((runtimePosition - worldCenter).sqrMagnitude <= radius * radius)
                    return true;
            }

            return false;
        }

        private static bool IsInsideSubmarineFallbackBounds(ISubmarineRuntimeContext submarine, Vector3 runtimePosition)
        {
            Transform platformTransform = submarine.PlatformTransform;
            if (platformTransform == null)
                return false;

            Vector3 localPosition = platformTransform.InverseTransformPoint(runtimePosition);
            return math.abs(localPosition.x) <= PlatformVelocityInheritanceFallbackHalfX &&
                   math.abs(localPosition.y) <= PlatformVelocityInheritanceFallbackHalfY &&
                   math.abs(localPosition.z) <= PlatformVelocityInheritanceFallbackHalfZ;
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > 0.000001f;
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition position)
        {
            position = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            position = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return MathGuard.IsFinite(in position);
        }

        private static bool TryResolveLiveInstanceAup(Transform sourceTransform, out AbsoluteUniversePosition position)
        {
            position = default;
            if (sourceTransform == null)
                return false;

            return TryResolveAupFromRuntimeOrigin(sourceTransform.position, out position);
        }

        private void RegisterSpawnImpulse(uint instanceUid, Vector3 initialImpulse)
        {
            if (!_spawnImpulseByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            float3 impulse = new float3(initialImpulse.x, initialImpulse.y, initialImpulse.z);
            if (!math.all(math.isfinite(impulse)) || math.lengthsq(impulse) <= 0.000001f)
                return;

            _spawnImpulseByInstanceUid.Remove(instanceUid);
            _spawnImpulseByInstanceUid.TryAdd(instanceUid, impulse);
        }

        private static uint PackFaunaSleepStartTimeSeconds(float sleepStartTimeSeconds)
        {
            if (!float.IsFinite(sleepStartTimeSeconds) || sleepStartTimeSeconds <= 0f)
                return 0u;

            return (uint)math.min(FaunaSleepStartMaxEncoded, (int)math.round(sleepStartTimeSeconds / FaunaSleepStartQuantumSeconds));
        }

        private void RegisterSpawnVelocityChange(uint instanceUid, Vector3 inheritedVelocityChange)
        {
            if (!_spawnVelocityChangeByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            float3 velocityChange = new float3(inheritedVelocityChange.x, inheritedVelocityChange.y, inheritedVelocityChange.z);
            if (!math.all(math.isfinite(velocityChange)) || math.lengthsq(velocityChange) <= 0.000001f)
                return;

            _spawnVelocityChangeByInstanceUid.Remove(instanceUid);
            _spawnVelocityChangeByInstanceUid.TryAdd(instanceUid, velocityChange);
        }

        private bool TryConsumeSpawnImpulse(uint instanceUid, out float3 impulse)
        {
            impulse = default;
            if (!_spawnImpulseByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            if (!_spawnImpulseByInstanceUid.TryGetValue(instanceUid, out impulse))
                return false;

            _spawnImpulseByInstanceUid.Remove(instanceUid);
            return true;
        }

        private bool TryConsumeSpawnVelocityChange(uint instanceUid, out float3 velocityChange)
        {
            velocityChange = default;
            if (!_spawnVelocityChangeByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            if (!_spawnVelocityChangeByInstanceUid.TryGetValue(instanceUid, out velocityChange))
                return false;

            _spawnVelocityChangeByInstanceUid.Remove(instanceUid);
            return true;
        }

        private EntityDataRecord CaptureEntityStateFromLiveInstance(
            in PersistentWorldItemRecord record,
            GameObject instance,
            in AbsoluteUniversePosition position)
        {
            EntityDataRecord state = ResolveEntityState(in record);
            state.Position = position.ToAlignedBlit();
            state.InstanceUid = record.InstanceUid;
            state.Quantity = math.max(1, record.Quantity);

            if (instance != null)
            {
                if (instance.TryGetComponent(out PickupItem pickupItem))
                {
                    state.Quantity = math.max(1, pickupItem.Quantity);
                    state.Position.Reserved = pickupItem.GeneticsMask;
                    state.InventoryHash = unchecked((int)pickupItem.GeneticsMask);
                    state.Integrity01 = ResolveItemQuality01(pickupItem.QualityMilli);
                }
                else if (instance.TryGetComponent(out HectonItem hectonItem))
                {
                    state.Quantity = math.max(1, hectonItem.Quantity);
                    state.Position.Reserved = hectonItem.GeneticsMask;
                    state.InventoryHash = unchecked((int)hectonItem.GeneticsMask);
                    state.Integrity01 = ResolveItemQuality01(hectonItem.QualityMilli);
                }
            }

            if (IsSpecialEntityState(in state) && state.Integrity01 <= 0f)
                state.Integrity01 = 1f;
            else if (!float.IsFinite(state.Integrity01))
                state.Integrity01 = DefaultItemQuality01;
            else
                state.Integrity01 = math.saturate(state.Integrity01);

            return state;
        }

        private AbsoluteUniversePosition ResolveResidencyPosition(in PersistentWorldItemRecord record)
        {
            EntityDataRecord state = ResolveEntityState(in record);
            return AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
        }

        private bool TryGetPoolIndex(in PersistentWorldItemRecord record, out int poolIndex)
        {
            poolIndex = -1;
            if (!_guidToPoolIndex.IsCreated)
                return false;

            return _guidToPoolIndex.TryGetValue(ComputePoolGuid(in record), out poolIndex) &&
                   IsValidPoolIndex(poolIndex);
        }

        private static ulong ComputePoolGuid(in PersistentWorldItemRecord record)
        {
            ulong guid = record.ItemPersistentIdHash ^ ((ulong)record.InstanceUid * PoolGuidMixSalt);
            return guid != 0UL ? guid : (PoolGuidMixSalt ^ 1UL);
        }

        private static void WritePoolSlotPosition(ref PoolSlotData slotData, in AbsoluteUniversePosition position)
        {
            slotData.AupCell = new int3((int)position.GridX, (int)position.GridY, (int)position.GridZ);
            slotData.LocalOffset = new float3(position.LocalX, position.LocalY, position.LocalZ);
        }

        private static AbsoluteUniversePosition ReadPoolSlotPosition(in PoolSlotData slotData)
        {
            return new AbsoluteUniversePosition
            {
                GridX = slotData.AupCell.x,
                GridY = slotData.AupCell.y,
                GridZ = slotData.AupCell.z,
                LocalX = slotData.LocalOffset.x,
                LocalY = slotData.LocalOffset.y,
                LocalZ = slotData.LocalOffset.z
            };
        }

        private void QueueRecordForDehydration(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex) || !_dehydrateQueue.IsCreated)
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            byte hydratedMask = (byte)PoolSlotStateFlags.Hydrated;
            byte queuedMask = (byte)PoolSlotStateFlags.DehydrationQueued;
            if ((slotData.StateFlags & hydratedMask) == 0 || (slotData.StateFlags & queuedMask) != 0)
                return;

            slotData.StateFlags |= queuedMask;
            _poolSlotData[poolIndex] = slotData;
            _dehydrateQueue.Enqueue(recordIndex);
        }

        private void QueueRecordForHydration(int recordIndex, in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (!_pendingHydrationRecords.IsCreated ||
                !IsValidRecordIndex(recordIndex) ||
                record.IsCollected ||
                IsDeletedInstanceUid(record.InstanceUid) ||
                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex) ||
                !ShouldHydrateDehydratedRecord(in record, in playerAup))
            {
                return;
            }

            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            CompactPendingHydrationQueueIfDrained();
            if (_pendingHydrationRecords.Length >= _pendingHydrationRecords.Capacity)
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            byte hydrationQueuedMask = (byte)PoolSlotStateFlags.HydrationQueued;
            byte hydratedMask = (byte)PoolSlotStateFlags.Hydrated;
            if ((slotData.StateFlags & hydratedMask) != 0 || (slotData.StateFlags & hydrationQueuedMask) != 0)
                return;

            slotData.StateFlags |= hydrationQueuedMask;
            _poolSlotData[poolIndex] = slotData;
            QueueWorldPrefabPrewarmForRecord(in record);
            _pendingHydrationRecords.AddNoResize(recordIndex);
            EnsureHydrationSessionScheduled();
        }

        private void QueueWorldPrefabPrewarmForRecord(in PersistentWorldItemRecord record)
        {
            if (!TryResolveItemData(in record, out ItemData itemData) || itemData == null || _resolvedItemCatalog == null)
                return;

            int itemHashId = ComputeCatalogItemHash(itemData);
            if (itemHashId == 0)
                return;

            _resolvedItemCatalog.QueueWorldPrefabPrewarm(itemHashId);
        }

        private void EnsureHydrationSessionScheduled()
        {
            if (_hydrationSessionRunning ||
                !Application.isPlaying ||
                !_pendingHydrationRecords.IsCreated)
            {
                return;
            }

            CompactPendingHydrationQueueIfDrained();
            if (_pendingHydrationReadIndex >= _pendingHydrationRecords.Length)
                return;

            _hydrationSessionRunning = true;
            int sessionVersion = ++_hydrationSessionVersion;
            _ = RunHydrationSessionAsync(sessionVersion);
        }

        private async Awaitable RunHydrationSessionAsync(int sessionVersion)
        {
            try
            {
                while (Application.isPlaying &&
                       ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this) &&
                       _hydrationSessionVersion == sessionVersion)
                {
                    if (!TryProcessHydrationBurst())
                        break;

                    await HydrationScheduler.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_hydrationSessionVersion == sessionVersion)
                    _hydrationSessionRunning = false;

                CompactPendingHydrationQueueIfDrained();
                if (!_hydrationSessionRunning)
                    EnsureHydrationSessionScheduled();
            }
        }

        private bool TryProcessHydrationBurst()
        {
            if (!_pendingHydrationRecords.IsCreated || _pendingHydrationReadIndex >= _pendingHydrationRecords.Length)
            {
                CompactPendingHydrationQueueIfDrained();
                return false;
            }

            long burstStartTicks = Stopwatch.GetTimestamp();
            long budgetDeadline = burstStartTicks + HydrationFrameBudgetTicks;
            int processedCount = 0;
            while (processedCount < MaxHydrationsPerFrame && _pendingHydrationReadIndex < _pendingHydrationRecords.Length)
            {
                if (processedCount > 0 && Stopwatch.GetTimestamp() >= budgetDeadline)
                    break;

                int recordIndex = _pendingHydrationRecords[_pendingHydrationReadIndex++];
                processedCount++;

                if (!IsValidRecordIndex(recordIndex))
                    continue;

                PersistentWorldItemRecord record = _records[recordIndex];
                ClearHydrationQueuedFlag(in record);
                if (record.IsCollected || IsDeletedInstanceUid(record.InstanceUid))
                    continue;

                if (_hasLastHydrationScanAup && !ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                    continue;

                if (!HydrateRecord(recordIndex, in record) &&
                    _hasLastHydrationScanAup &&
                    ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                {
                    QueueRecordForHydration(recordIndex, in record, in _lastHydrationScanAup);
                }
            }

            if (processedCount > 0)
                PublishHydrationBudgetWarningIfNeeded(Stopwatch.GetTimestamp() - burstStartTicks);

            CompactPendingHydrationQueueIfDrained();
            return _pendingHydrationRecords.IsCreated && _pendingHydrationReadIndex < _pendingHydrationRecords.Length;
        }

        private void PublishHydrationBudgetWarningIfNeeded(long elapsedTicks)
        {
            if (elapsedTicks <= HydrationPerformanceWarningBudgetTicks)
                return;

            PublishSectorCompressionPerformanceWarning(
                _hydrationApplyBudgetWarningHash,
                _hydrationApplyContextHash,
                StopwatchTicksToMilliseconds(elapsedTicks),
                ref _lastHydrationBudgetTelemetryFrame);
        }

        private void CompactPendingHydrationQueueIfDrained()
        {
            if (!_pendingHydrationRecords.IsCreated || _pendingHydrationReadIndex < _pendingHydrationRecords.Length)
                return;

            _pendingHydrationRecords.Clear();
            _pendingHydrationReadIndex = 0;
        }

        private void CancelHydrationSession(bool clearQueue)
        {
            _hydrationSessionVersion++;
            _hydrationSessionRunning = false;
            _pendingHydrationReadIndex = 0;

            if (clearQueue && _pendingHydrationRecords.IsCreated)
                _pendingHydrationRecords.Clear();
        }

        private void ClearHydrationQueuedFlag(in PersistentWorldItemRecord record)
        {
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.HydrationQueued);
            _poolSlotData[poolIndex] = slotData;
        }

        private void DrainDehydrateQueue(int maxDequeueCount)
        {
            if (!_dehydrateQueue.IsCreated)
                return;

            int dequeueBudget = math.max(1, maxDequeueCount);
            while (dequeueBudget-- > 0 && _dehydrateQueue.TryDequeue(out int recordIndex))
            {
                ClearDehydrationQueuedFlag(recordIndex);
                DehydrateRecord(recordIndex, syncTransformBackToRecord: true);
            }
        }

        private void ClearDehydrationQueuedFlag(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.DehydrationQueued);
            _poolSlotData[poolIndex] = slotData;
        }

        private void ClearHydratedSlot(int poolIndex)
        {
            _hydratedInstancesByRecordIndex.Remove(poolIndex);

            if (!IsValidPoolIndex(poolIndex))
                return;

            _hydratedInstancesBySlot[poolIndex] = null;
            _poolSlotTransforms[poolIndex] = null;
            _poolSlotRigidbodies[poolIndex] = null;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.RefCount = 0;
            slotData.StableFrames = 0;
            slotData.StateFlags &= unchecked((byte)~((byte)PoolSlotStateFlags.Hydrated |
                                                     (byte)PoolSlotStateFlags.Dirty |
                                                     (byte)PoolSlotStateFlags.Settled |
                                                     (byte)PoolSlotStateFlags.HydrationQueued |
                                                     (byte)PoolSlotStateFlags.DehydrationQueued));
            _poolSlotData[poolIndex] = slotData;
        }

        private void ResetPoolSlots()
        {
            if (_guidToPoolIndex.IsCreated)
                _guidToPoolIndex.Clear();

            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Clear();

            ClearWhaleFallPoiInfluenceUidIndex();
            ClearApexMigrationVisitedScratch();

            if (_floraSpawnStateByInstanceUid.IsCreated)
                _floraSpawnStateByInstanceUid.Clear();

            if (_deltaRecordIndexByEntityId.IsCreated)
                _deltaRecordIndexByEntityId.Clear();

            if (_deletedInstanceUids.IsCreated)
                _deletedInstanceUids.Clear();

            if (_resourceNodeTombstoneIds.IsCreated)
                _resourceNodeTombstoneIds.Clear();

            if (_resourceNodeMetamorphosedIds.IsCreated)
                _resourceNodeMetamorphosedIds.Clear();

            if (_deltaChunkIndexByChunkId.IsCreated)
                _deltaChunkIndexByChunkId.Clear();

            if (_deltaChunkIds.IsCreated)
                _deltaChunkIds.Clear();

            if (_deltaItemIndexByHash.IsCreated)
                _deltaItemIndexByHash.Clear();

            if (_deltaItemHashes.IsCreated)
                _deltaItemHashes.Clear();

            if (_deltaRecordsByChunk.IsCreated)
                _deltaRecordsByChunk.Clear();

            if (_dehydrateQueue.IsCreated)
            {
                int maxDrainCount = math.max(1, _records.Length + 1);
                int drainedCount = 0;
                while (drainedCount < maxDrainCount && _dehydrateQueue.TryDequeue(out _))
                {
                    drainedCount++;
                }

                if (drainedCount >= maxDrainCount && _dehydrateQueue.TryDequeue(out _))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        CreateRecordCountWatchdogMessage(_records.Length));
#endif

                    while (_dehydrateQueue.TryDequeue(out _))
                    {
                    }
                }
            }

            if (_pendingHydrationRecords.IsCreated)
            {
                _pendingHydrationRecords.Clear();
                _pendingHydrationReadIndex = 0;
            }

            if (_poolSlotData.IsCreated)
            {
                for (int i = 0; i < _poolSlotData.Length; i++)
                    _poolSlotData[i] = default;
            }

            if (_hydratedInstancesBySlot != null)
                Array.Clear(_hydratedInstancesBySlot, 0, _hydratedInstancesBySlot.Length);

            if (_poolSlotTransforms != null)
                Array.Clear(_poolSlotTransforms, 0, _poolSlotTransforms.Length);

            if (_poolSlotRigidbodies != null)
                Array.Clear(_poolSlotRigidbodies, 0, _poolSlotRigidbodies.Length);

            _hydratedInstancesByRecordIndex?.Clear();
        }

        private void RegisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.RegisterNativeList(_records, MemoryBudgetOwnerName, nameof(_records), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_recordsByChunk, MemoryBudgetOwnerName, nameof(_recordsByChunk), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_deltaRecords, MemoryBudgetOwnerName, nameof(_deltaRecords), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_deltaRecordIndexByEntityId, MemoryBudgetOwnerName, nameof(_deltaRecordIndexByEntityId), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_deletedInstanceUids, MemoryBudgetOwnerName, nameof(_deletedInstanceUids), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_resourceNodeTombstoneIds, MemoryBudgetOwnerName, nameof(_resourceNodeTombstoneIds), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_resourceNodeMetamorphosedIds, MemoryBudgetOwnerName, nameof(_resourceNodeMetamorphosedIds), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_deltaChunkIndexByChunkId, MemoryBudgetOwnerName, nameof(_deltaChunkIndexByChunkId), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_deltaChunkIds, MemoryBudgetOwnerName, nameof(_deltaChunkIds), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_deltaItemIndexByHash, MemoryBudgetOwnerName, nameof(_deltaItemIndexByHash), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_deltaItemHashes, MemoryBudgetOwnerName, nameof(_deltaItemHashes), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_deltaRecordsByChunk, MemoryBudgetOwnerName, nameof(_deltaRecordsByChunk), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_tombstoneDecayExpiredIndices, MemoryBudgetOwnerName, nameof(_tombstoneDecayExpiredIndices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_saveSnapshotDeltas, MemoryBudgetOwnerName, nameof(_saveSnapshotDeltas), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_poolSlotData, MemoryBudgetOwnerName, nameof(_poolSlotData), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_guidToPoolIndex, MemoryBudgetOwnerName, nameof(_guidToPoolIndex), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_entityStateByInstanceUid, MemoryBudgetOwnerName, nameof(_entityStateByInstanceUid), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_floraSpawnStateByInstanceUid, MemoryBudgetOwnerName, nameof(_floraSpawnStateByInstanceUid), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_spawnImpulseByInstanceUid, MemoryBudgetOwnerName, nameof(_spawnImpulseByInstanceUid), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeHashMap(_spawnVelocityChangeByInstanceUid, MemoryBudgetOwnerName, nameof(_spawnVelocityChangeByInstanceUid), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeQueue(_dehydrateQueue, maxTrackedItems, MemoryBudgetOwnerName, nameof(_dehydrateQueue), NativeAllocationLifetime.Session);
            PrewarmDehydrateQueue();
            NativeMemorySentinel.RegisterNativeList(_pendingHydrationRecords, MemoryBudgetOwnerName, nameof(_pendingHydrationRecords), NativeAllocationLifetime.Session);
        }

        private void PrewarmDehydrateQueue()
        {
            if (!_dehydrateQueue.IsCreated)
                return;

            int safeCapacity = math.max(0, maxTrackedItems);
            for (int i = 0; i < safeCapacity; i++)
                _dehydrateQueue.Enqueue(default);

            while (_dehydrateQueue.TryDequeue(out _))
            {
            }
        }

        private void UnregisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_records));
            NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(MemoryBudgetOwnerName, nameof(_recordsByChunk));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_deltaRecords));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_deltaRecordIndexByEntityId));
            NativeMemorySentinel.UnregisterNativeParallelHashSet(MemoryBudgetOwnerName, nameof(_deletedInstanceUids));
            NativeMemorySentinel.UnregisterNativeParallelHashSet(MemoryBudgetOwnerName, nameof(_resourceNodeTombstoneIds));
            NativeMemorySentinel.UnregisterNativeParallelHashSet(MemoryBudgetOwnerName, nameof(_resourceNodeMetamorphosedIds));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_deltaChunkIndexByChunkId));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_deltaChunkIds));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_deltaItemIndexByHash));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_deltaItemHashes));
            NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(MemoryBudgetOwnerName, nameof(_deltaRecordsByChunk));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_tombstoneDecayExpiredIndices));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_saveSnapshotDeltas));
            NativeMemorySentinel.UnregisterNativeArray(_poolSlotData);
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_guidToPoolIndex));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_entityStateByInstanceUid));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_floraSpawnStateByInstanceUid));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_spawnImpulseByInstanceUid));
            NativeMemorySentinel.UnregisterNativeHashMap(MemoryBudgetOwnerName, nameof(_spawnVelocityChangeByInstanceUid));
            NativeMemorySentinel.UnregisterNativeQueue(MemoryBudgetOwnerName, nameof(_dehydrateQueue));
            NativeMemorySentinel.UnregisterNativeList(MemoryBudgetOwnerName, nameof(_pendingHydrationRecords));
        }

        private void RegisterPersistentMemoryBudget()
        {
            long totalBytes =
                GetNativeListBytes(_records) +
                GetNativeParallelMultiHashMapBytes(_recordsByChunk) +
                GetNativeListBytes(_deltaRecords) +
                GetNativeHashMapBytes(_deltaRecordIndexByEntityId) +
                GetNativeParallelHashSetBytes(_deletedInstanceUids) +
                GetNativeParallelHashSetBytes(_resourceNodeTombstoneIds) +
                GetNativeParallelHashSetBytes(_resourceNodeMetamorphosedIds) +
                GetNativeHashMapBytes(_deltaChunkIndexByChunkId) +
                GetNativeListBytes(_deltaChunkIds) +
                GetNativeHashMapBytes(_deltaItemIndexByHash) +
                GetNativeListBytes(_deltaItemHashes) +
                GetNativeParallelMultiHashMapBytes(_deltaRecordsByChunk) +
                GetNativeListBytes(_tombstoneDecayExpiredIndices) +
                GetNativeListBytes(_saveSnapshotDeltas) +
                GetNativeArrayBytes(_poolSlotData) +
                GetNativeHashMapBytes(_guidToPoolIndex) +
                GetNativeHashMapBytes(_entityStateByInstanceUid);
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentMemoryBudgetBytes);
        }

        private static long GetNativeArrayBytes<T>(NativeArray<T> array) where T : unmanaged
        {
            return array.IsCreated ? (long)array.Length * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private static long GetNativeListBytes<T>(NativeList<T> list) where T : unmanaged
        {
            return list.IsCreated ? (long)list.Capacity * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private static long GetNativeHashMapBytes<TKey, TValue>(NativeHashMap<TKey, TValue> map)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return map.IsCreated
                ? (long)map.Capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>())
                : 0L;
        }

        private static long GetNativeParallelMultiHashMapBytes<TKey, TValue>(NativeParallelMultiHashMap<TKey, TValue> map)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return map.IsCreated
                ? (long)map.Capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>())
                : 0L;
        }

        private static long GetNativeParallelHashSetBytes<TKey>(NativeParallelHashSet<TKey> set)
            where TKey : unmanaged, IEquatable<TKey>
        {
            return set.IsCreated
                ? (long)set.Capacity * UnsafeUtility.SizeOf<TKey>()
                : 0L;
        }

        private void UpdateDiagnostics()
        {
            _debugTrackedRecordCount = _records.IsCreated ? CountActiveRecords() : 0;
            _debugHydratedRecordCount = _hydratedInstancesByRecordIndex != null ? _hydratedInstancesByRecordIndex.Count : 0;
            _debugSnapshotRecordCount = _saveSnapshotDeltas.IsCreated ? _saveSnapshotDeltas.Length : 0;
            _debugPlayerChunk = _playerChunkValid
                ? new Vector3Int(_currentPlayerChunk.x, _currentPlayerChunk.y, _currentPlayerChunk.z)
                : default;
        }

        private void UpsertDeltaRecord(in PersistentWorldItemRecord record)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || record.InstanceUid == 0u)
                return;

            if (record.IsDeleted)
                RegisterDeletedInstanceUid(record.InstanceUid);
            else
                UnregisterDeletedInstanceUid(record.InstanceUid);

            if (!TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                return;

            if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int deltaIndex))
            {
                _deltaRecords[deltaIndex] = compactRecord;
            }
            else if (_deltaRecords.Length < _deltaRecords.Capacity)
            {
                _deltaRecordIndexByEntityId.TryAdd(record.InstanceUid, _deltaRecords.Length);
                _deltaRecords.AddNoResize(compactRecord);
            }
            else
            {
                return;
            }

            RebuildDeltaChunkLookup();
        }

        private void RemoveDeltaRecord(uint instanceUid)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || instanceUid == 0u)
                return;

            if (!_deltaRecordIndexByEntityId.TryGetValue(instanceUid, out int deltaIndex) ||
                deltaIndex < 0 ||
                deltaIndex >= _deltaRecords.Length)
            {
                return;
            }

            int lastIndex = _deltaRecords.Length - 1;
            PersistentWorldCompactDeltaRecord lastRecord = _deltaRecords[lastIndex];
            _deltaRecords.RemoveAtSwapBack(deltaIndex);
            _deltaRecordIndexByEntityId.Remove(instanceUid);

            if (deltaIndex < lastIndex)
            {
                _deltaRecordIndexByEntityId.Remove(lastRecord.InstanceUid);
                _deltaRecordIndexByEntityId.TryAdd(lastRecord.InstanceUid, deltaIndex);
            }

            RebuildDeltaChunkLookup();
        }

        private void RebuildDeltaChunkLookup()
        {
            if (!_deltaRecordsByChunk.IsCreated)
                return;

            _deltaRecordsByChunk.Clear();
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[i];
                if (!TryGetCompactDeltaChunkId(compactRecord, out int3 chunkId))
                    continue;

                _deltaRecordsByChunk.Add(ComputeChunkDeltaKey(chunkId), compactRecord);
            }
        }

        private static uint ComputeChunkDeltaKey(int3 chunkId)
        {
            return math.hash(chunkId);
        }

        private static int ComputeCatalogItemHash(ItemData itemData)
        {
            if (itemData == null || string.IsNullOrWhiteSpace(itemData.PersistentId))
                return 0;

            return Hecton.Localization.LocHash.Compute(itemData.PersistentId);
        }

        internal int CopyChunkDeltas(int3 chunkId, NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecordsByChunk.IsCreated)
                return 0;

            uint chunkKey = ComputeChunkDeltaKey(chunkId);
            if (!_deltaRecordsByChunk.TryGetFirstValue(chunkKey, out PersistentWorldCompactDeltaRecord compactRecord, out var iterator))
                return 0;

            int copiedCount = 0;
            do
            {
                if (!TryResolveDeltaRecord(compactRecord, out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!math.all(expandedRecord.ChunkId == chunkId))
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }
            while (_deltaRecordsByChunk.TryGetNextValue(out compactRecord, ref iterator));

            return copiedCount;
        }

        internal int CopyDestroyedFloraDeltas(NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated)
                return 0;

            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (((PersistentWorldItemFlags)expandedRecord.ItemFlags & PersistentWorldItemFlags.FloraDestroyed) == 0)
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyFloraStateOverrideDeltas(NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated)
                return 0;

            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!expandedRecord.IsFloraStateOverride)
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyPendingFloraSeedDeltas(NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated)
                return 0;

            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!expandedRecord.IsFloraSeedPending)
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

            return copiedCount;
        }

        private bool ContainsRecordInstanceUid(uint instanceUid)
        {
            if (instanceUid == 0u || !_records.IsCreated)
                return false;

            if (IsDeletedInstanceUid(instanceUid))
                return true;

            for (int i = 0; i < _records.Length; i++)
            {
                if (_records[i].InstanceUid == instanceUid)
                    return true;
            }

            return false;
        }

        private bool TryBuildCompactDeltaRecord(in PersistentWorldItemRecord record, out PersistentWorldCompactDeltaRecord compactRecord)
        {
            compactRecord = default;
            PersistentWorldDeltaRecord expandedRecord = record.IsDeleted
                ? PersistentWorldDeltaRecord.CreateDeletedTombstone(in record, chunkSizeMeters)
                : PersistentWorldDeltaRecord.FromRecord(in record, chunkSizeMeters);
            return TryBuildCompactDeltaRecord(expandedRecord, out compactRecord);
        }

        private bool TryBuildCompactDeltaRecord(PersistentWorldDeltaRecord expandedRecord, out PersistentWorldCompactDeltaRecord compactRecord)
        {
            compactRecord = default;
            if (!expandedRecord.IsValid)
                return false;

            if (!TryEnsureDeltaChunkIndex(expandedRecord.ChunkId, out ushort chunkIndex))
            {
                return false;
            }

            ushort itemHashIndex = ushort.MaxValue;
            if (!expandedRecord.IsDeleted &&
                !TryEnsureDeltaItemHashIndex(expandedRecord.ItemPersistentIdHash, out itemHashIndex))
            {
                return false;
            }

            compactRecord = new PersistentWorldCompactDeltaRecord
            {
                PackedLocalPosition = expandedRecord.PackedLocalPosition,
                InstanceUid = expandedRecord.InstanceUid,
                Quantity = expandedRecord.IsDeleted ? (ushort)1 : expandedRecord.Quantity,
                ItemFlags = expandedRecord.ItemFlags,
                Reserved = 0,
                ChunkIndex = chunkIndex,
                ItemHashIndex = itemHashIndex
            };
            return compactRecord.IsValid;
        }

        private bool TryResolveDeltaRecord(PersistentWorldCompactDeltaRecord compactRecord, out PersistentWorldDeltaRecord expandedRecord)
        {
            expandedRecord = default;
            if (!compactRecord.IsValid || !TryGetCompactDeltaChunkId(compactRecord, out int3 chunkId))
            {
                return false;
            }

            ulong itemHash = 0UL;
            if (!compactRecord.IsDeleted && !TryGetCompactDeltaItemHash(compactRecord, out itemHash))
                return false;

            expandedRecord = new PersistentWorldDeltaRecord
            {
                ChunkId = chunkId,
                ItemPersistentIdHash = itemHash,
                InstanceUid = compactRecord.InstanceUid,
                PackedLocalPosition = compactRecord.PackedLocalPosition,
                Quantity = compactRecord.IsDeleted ? (ushort)1 : compactRecord.Quantity,
                ItemFlags = compactRecord.ItemFlags,
                Reserved = compactRecord.Reserved
            };
            return expandedRecord.IsValid;
        }

        private bool TryEnsureDeltaChunkIndex(int3 chunkId, out ushort chunkIndex)
        {
            chunkIndex = 0;
            if (!_deltaChunkIndexByChunkId.IsCreated || !_deltaChunkIds.IsCreated)
                return false;

            if (_deltaChunkIndexByChunkId.TryGetValue(chunkId, out chunkIndex))
                return true;

            if (_deltaChunkIds.Length >= _deltaChunkIds.Capacity || _deltaChunkIds.Length >= ushort.MaxValue)
                return false;

            chunkIndex = (ushort)_deltaChunkIds.Length;
            _deltaChunkIds.AddNoResize(chunkId);
            _deltaChunkIndexByChunkId.TryAdd(chunkId, chunkIndex);
            return true;
        }

        private bool TryEnsureDeltaItemHashIndex(ulong itemHash, out ushort itemHashIndex)
        {
            itemHashIndex = 0;
            if (!_deltaItemIndexByHash.IsCreated || !_deltaItemHashes.IsCreated || itemHash == 0UL)
                return false;

            if (_deltaItemIndexByHash.TryGetValue(itemHash, out itemHashIndex))
                return true;

            if (_deltaItemHashes.Length >= _deltaItemHashes.Capacity || _deltaItemHashes.Length >= ushort.MaxValue)
                return false;

            itemHashIndex = (ushort)_deltaItemHashes.Length;
            _deltaItemHashes.AddNoResize(itemHash);
            _deltaItemIndexByHash.TryAdd(itemHash, itemHashIndex);
            return true;
        }

        private bool TryGetCompactDeltaChunkId(PersistentWorldCompactDeltaRecord compactRecord, out int3 chunkId)
        {
            chunkId = default;
            int chunkIndex = compactRecord.ChunkIndex;
            if (!_deltaChunkIds.IsCreated || chunkIndex < 0 || chunkIndex >= _deltaChunkIds.Length)
                return false;

            chunkId = _deltaChunkIds[chunkIndex];
            return true;
        }

        private bool TryGetCompactDeltaItemHash(PersistentWorldCompactDeltaRecord compactRecord, out ulong itemHash)
        {
            itemHash = 0UL;
            int itemHashIndex = compactRecord.ItemHashIndex;
            if (!_deltaItemHashes.IsCreated || itemHashIndex < 0 || itemHashIndex >= _deltaItemHashes.Length)
                return false;

            itemHash = _deltaItemHashes[itemHashIndex];
            return itemHash != 0UL;
        }

        private int CountActiveRecords()
        {
            int count = 0;
            for (int i = 0; i < _records.Length; i++)
            {
                if (!_records[i].IsCollected && !_records[i].IsDeleted)
                    count++;
            }

            return count;
        }

        private bool IsDeletedInstanceUid(uint instanceUid)
        {
            return instanceUid != 0u && _deletedInstanceUids.IsCreated && _deletedInstanceUids.Contains(instanceUid);
        }

        private void RegisterDeletedInstanceUid(uint instanceUid)
        {
            if (instanceUid == 0u || !_deletedInstanceUids.IsCreated)
                return;

            _deletedInstanceUids.Add(instanceUid);
        }

        private void RegisterResourceNodeTombstone(ulong tombstoneId)
        {
            if (tombstoneId == 0UL || !_resourceNodeTombstoneIds.IsCreated)
                return;

            _resourceNodeTombstoneIds.Add(tombstoneId);
        }

        private ulong ResolveResourceNodeTombstoneId(in PersistentWorldDeltaRecord deltaRecord)
        {
            return deltaRecord.ItemPersistentIdHash != 0UL
                ? deltaRecord.ItemPersistentIdHash
                : ComputeResourceNodeTombstoneId(deltaRecord.UnpackPosition(chunkSizeMeters));
        }

        private void StageLoadedResourceNodeTombstoneRecord(in PersistentWorldDeltaRecord deltaRecord, ulong tombstoneId)
        {
            if (tombstoneId == 0UL ||
                !_records.IsCreated ||
                !_recordsByChunk.IsCreated ||
                _records.Length >= _records.Capacity ||
                deltaRecord.InstanceUid == 0u ||
                TryFindRecordIndexByInstanceUid(deltaRecord.InstanceUid, out _))
            {
                return;
            }

            PersistentWorldItemRecord record = deltaRecord.ToRecord(chunkSizeMeters);
            record.ItemPersistentIdHash = tombstoneId;
            record.Quantity = 0;
            record.Flags = PersistentWorldItemFlags.Deleted | PersistentWorldItemFlags.ResourceNodeDestroyed;
            _records.AddNoResize(record);
            _recordsByChunk.Add(record.ChunkId, _records.Length - 1);
        }

        private void StageResourceNodeTombstonesForSave()
        {
            if (!_records.IsCreated)
                return;

            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (!record.IsResourceNodeDestroyed || record.ItemPersistentIdHash == 0UL)
                    continue;

                RegisterResourceNodeTombstone(record.ItemPersistentIdHash);
                UpsertDeletedTombstone(in record);
            }
        }

        private void RegisterResourceNodeMetamorphosis(ulong tombstoneId)
        {
            if (tombstoneId == 0UL || !_resourceNodeMetamorphosedIds.IsCreated)
                return;

            _resourceNodeMetamorphosedIds.Add(tombstoneId);
        }

        private void UnregisterDeletedInstanceUid(uint instanceUid)
        {
            if (instanceUid == 0u || !_deletedInstanceUids.IsCreated)
                return;

            _deletedInstanceUids.Remove(instanceUid);
        }

        private void UpsertDeletedTombstone(in PersistentWorldItemRecord record)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();

            RegisterDeletedInstanceUid(record.InstanceUid);
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || record.InstanceUid == 0u)
                return;

            PersistentWorldItemRecord tombstoneRecord = record;
            tombstoneRecord.Quantity = tombstoneRecord.Quantity > 1 ? tombstoneRecord.Quantity : ResolveTombstoneDayIndex();
            if (!TryBuildCompactDeltaRecord(in tombstoneRecord, out PersistentWorldCompactDeltaRecord compactRecord))
                return;

            if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int deltaIndex))
            {
                _deltaRecords[deltaIndex] = compactRecord;
                return;
            }

            if (_deltaRecords.Length >= _deltaRecords.Capacity)
                return;

            _deltaRecordIndexByEntityId.TryAdd(record.InstanceUid, _deltaRecords.Length);
            _deltaRecords.AddNoResize(compactRecord);
        }

        private void ScheduleTombstoneDecaySweepIfDue(float now)
        {
            if (_tombstoneDecaySweepScheduled ||
                _tombstoneDecayApplyPending ||
                !_deltaRecords.IsCreated ||
                !_tombstoneDecayExpiredIndices.IsCreated ||
                now < _nextTombstoneDecaySweepTime)
            {
                return;
            }

            _nextTombstoneDecaySweepTime = now + TombstoneDecayFrostTickSeconds;
            _tombstoneDecayApplyCursor = 0;
            _tombstoneDecayCurrentDay = ResolveTombstoneDayIndex();
            TombstoneDecayCollectJob decayJob = new TombstoneDecayCollectJob
            {
                DeltaRecords = _deltaRecords.AsArray(),
                ExpiredDeltaIndices = _tombstoneDecayExpiredIndices,
                CurrentDay = _tombstoneDecayCurrentDay,
                Threshold = TombstoneDecayThreshold,
                TimeToLiveDays = TombstoneTimeToLiveDays
            };
            _tombstoneDecaySweepHandle = decayJob.Schedule();
            _tombstoneDecaySweepScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void CompleteTombstoneDecayBeforeDeltaMutation()
        {
            CompleteTombstoneDecaySweepForDeltaMutationBarrier(int.MaxValue);
            if (_tombstoneDecayApplyPending)
                ApplyCollectedTombstoneDecay(int.MaxValue);
        }

        private void TryFinalizeTombstoneDecaySweepNoWait(int maxApplies)
        {
            if (!_tombstoneDecaySweepScheduled)
                return;

            if (!_tombstoneDecaySweepHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _tombstoneDecaySweepHandle))
                return;

            CommitCompletedTombstoneDecaySweep(maxApplies);
        }

        private void CompleteTombstoneDecaySweepForDeltaMutationBarrier(int maxApplies)
        {
            if (!_tombstoneDecaySweepScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _tombstoneDecaySweepHandle, forceComplete: true);
            CommitCompletedTombstoneDecaySweep(maxApplies);
        }

        private void CommitCompletedTombstoneDecaySweep(int maxApplies)
        {
            _tombstoneDecaySweepScheduled = false;
            _tombstoneDecayApplyPending = true;
            ApplyCollectedTombstoneDecay(math.max(1, maxApplies));
        }

        private void ApplyCollectedTombstoneDecay(int maxApplies)
        {
            if (!_tombstoneDecayExpiredIndices.IsCreated ||
                !_deltaRecords.IsCreated ||
                !_deltaRecordIndexByEntityId.IsCreated ||
                _tombstoneDecayExpiredIndices.Length <= 0)
            {
                _tombstoneDecayApplyCursor = 0;
                _tombstoneDecayApplyPending = false;
                return;
            }

            int appliedCount = 0;
            while (_tombstoneDecayApplyCursor < _tombstoneDecayExpiredIndices.Length && appliedCount < maxApplies)
            {
                int deltaIndex = _tombstoneDecayExpiredIndices[_tombstoneDecayApplyCursor++];
                if ((uint)deltaIndex >= (uint)_deltaRecords.Length)
                    continue;

                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[deltaIndex];
                if (!compactRecord.IsDeleted)
                    continue;

                int tombstoneDay = compactRecord.Quantity > 0 ? compactRecord.Quantity : 1;
                if (_tombstoneDecayCurrentDay - tombstoneDay <= TombstoneTimeToLiveDays)
                    continue;

                RemoveDeltaRecordAtSwapBack(deltaIndex, in compactRecord);
                appliedCount++;
            }

            if (appliedCount > 0)
                RebuildDeltaChunkLookup();

            if (_tombstoneDecayApplyCursor < _tombstoneDecayExpiredIndices.Length)
                return;

            _tombstoneDecayExpiredIndices.Clear();
            _tombstoneDecayApplyCursor = 0;
            _tombstoneDecayApplyPending = false;
        }

        private void RemoveDeltaRecordAtSwapBack(int deltaIndex, in PersistentWorldCompactDeltaRecord compactRecord)
        {
            if (deltaIndex < 0 || deltaIndex >= _deltaRecords.Length)
                return;

            if (TryResolveDeltaRecord(compactRecord, out PersistentWorldDeltaRecord expandedRecord) &&
                expandedRecord.IsResourceNodeDestroyed &&
                expandedRecord.ItemPersistentIdHash != 0UL &&
                _resourceNodeTombstoneIds.IsCreated)
            {
                _resourceNodeTombstoneIds.Remove(expandedRecord.ItemPersistentIdHash);
            }

            UnregisterDeletedInstanceUid(compactRecord.InstanceUid);
            _deltaRecordIndexByEntityId.Remove(compactRecord.InstanceUid);

            int lastIndex = _deltaRecords.Length - 1;
            PersistentWorldCompactDeltaRecord lastRecord = _deltaRecords[lastIndex];
            _deltaRecords.RemoveAtSwapBack(deltaIndex);
            if (deltaIndex < lastIndex)
            {
                _deltaRecordIndexByEntityId.Remove(lastRecord.InstanceUid);
                _deltaRecordIndexByEntityId.TryAdd(lastRecord.InstanceUid, deltaIndex);
            }
        }

        private static int ResolveTombstoneDayIndex()
        {
            double playSeconds = GlobalRegistry.Save != null
                ? GlobalRegistry.Save.CurrentPlayTimeSeconds
                : Time.timeAsDouble;
            int day = (int)math.floor(math.max(0d, playSeconds) / TombstoneInGameDaySeconds);
            return math.clamp(day, 1, ushort.MaxValue);
        }

        private bool TryFindRecordIndexByInstanceUid(uint instanceUid, out int recordIndex)
        {
            recordIndex = -1;
            if (instanceUid == 0u || !_records.IsCreated)
                return false;

            for (int i = 0; i < _records.Length; i++)
            {
                if (_records[i].InstanceUid != instanceUid)
                    continue;

                recordIndex = i;
                return true;
            }

            return false;
        }

        internal static ulong ComputePersistentIdHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0UL;

            ulong hash = FnvOffsetBasis64;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                hash ^= (byte)current;
                hash *= FnvPrime64;
                hash ^= (byte)(current >> 8);
                hash *= FnvPrime64;
            }

            return hash;
        }

        internal static ulong ComputePersistentIdHash(in FixedString128Bytes value)
        {
            if (value.Length <= 0)
                return 0UL;

            ulong hash = FnvOffsetBasis64;
            for (int i = 0; i < value.Length; i++)
            {
                byte current = value[i];
                hash ^= current;
                hash *= FnvPrime64;
                hash ^= 0UL;
                hash *= FnvPrime64;
            }

            return hash;
        }

        private static bool UID_VALIDATE(in PersistentWorldItemRecord record)
        {
            if (record.ItemPersistentIdHash == 0UL)
                return false;

            ulong computedPersistentIdHash = ComputePersistentIdHash(in record.ItemPersistentId);
            if (computedPersistentIdHash == 0UL)
                return false;

            if (computedPersistentIdHash != record.ItemPersistentIdHash)
                return false;

            if (record.InstanceUid == 0u)
                return true;

            uint expectedTypeId = ResolveInstanceUidTypeId(null, record.ItemPersistentIdHash);
            uint actualTypeId = record.InstanceUid >> InstanceUidTypeShift;
            return actualTypeId == expectedTypeId;
        }

        private bool TryEnsureInstanceUid(ref PersistentWorldItemRecord record)
        {
            if (record.InstanceUid != 0u)
                return true;

            ItemData itemData = null;
            TryResolveItemData(in record, out itemData);
            return TryGenerateInstanceUid(itemData, record.ItemPersistentIdHash, out record.InstanceUid);
        }

        private static bool TryGenerateInstanceUid(ItemData itemData, ulong persistentIdHash, out uint instanceUid)
        {
            instanceUid = 0u;

            uint sequence = unchecked((uint)Interlocked.Increment(ref _nextInstanceUidCounter));
            if (sequence == 0u || sequence > InstanceUidCounterMask)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[PersistentWorldRegistry] Exhausted 24-bit persistent item instance UID counter.");
#endif
                return false;
            }

            uint typeId = ResolveInstanceUidTypeId(itemData, persistentIdHash);
            instanceUid = (typeId << InstanceUidTypeShift) | sequence;
            return true;
        }

        private bool TryGenerateResourceNodeTombstoneInstanceUid(ulong tombstoneId, out uint instanceUid)
        {
            instanceUid = 0u;
            if (tombstoneId == 0UL || !_records.IsCreated)
                return false;

            const uint resourceNodeTombstoneTypeId = 0xFEu;
            uint sequence = (((uint)tombstoneId) ^ ((uint)(tombstoneId >> 32)) ^ 0x5D588B65u) & InstanceUidCounterMask;
            if (sequence == 0u)
                sequence = 1u;

            int probeBudget = math.max(1, maxTrackedItems);
            for (int i = 0; i < probeBudget; i++)
            {
                uint candidate = (resourceNodeTombstoneTypeId << InstanceUidTypeShift) | sequence;
                if (!ContainsRecordInstanceUid(candidate))
                {
                    instanceUid = candidate;
                    return true;
                }

                sequence++;
                if (sequence > InstanceUidCounterMask)
                    sequence = 1u;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(CreateHexMessage("[PersistentWorldRegistry] Failed to reserve resource-node tombstone UID. tombstoneId=", tombstoneId));
#endif
            return false;
        }

        private bool TryGenerateResourceNodeMetamorphosisInstanceUid(ulong tombstoneId, out uint instanceUid)
        {
            instanceUid = 0u;
            if (tombstoneId == 0UL || !_records.IsCreated)
                return false;

            const uint resourceNodeMetamorphosisTypeId = 0xFDu;
            uint sequence = (((uint)tombstoneId) ^ ((uint)(tombstoneId >> 32)) ^ 0x7F4A7C15u) & InstanceUidCounterMask;
            if (sequence == 0u)
                sequence = 1u;

            int probeBudget = math.max(1, maxTrackedItems);
            for (int i = 0; i < probeBudget; i++)
            {
                uint candidate = (resourceNodeMetamorphosisTypeId << InstanceUidTypeShift) | sequence;
                if (!ContainsRecordInstanceUid(candidate))
                {
                    instanceUid = candidate;
                    return true;
                }

                sequence++;
                if (sequence > InstanceUidCounterMask)
                    sequence = 1u;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(CreateHexMessage("[PersistentWorldRegistry] Failed to reserve resource-node metamorphosis UID. tombstoneId=", tombstoneId));
#endif
            return false;
        }

        private static uint ResolveInstanceUidTypeId(ItemData itemData, ulong persistentIdHash)
        {
            if (itemData != null)
                return ((uint)itemData.category) & 0xFFu;

            return persistentIdHash != 0UL
                ? (uint)((persistentIdHash >> 56) & 0xFFUL)
                : 0u;
        }

        private static void RebaseInstanceUidCounter(uint maxObservedSequence)
        {
            int target = (int)math.min(maxObservedSequence, InstanceUidCounterMask);
            int snapshot = Volatile.Read(ref _nextInstanceUidCounter);
            int compareExchangeWatchdog = 16;
            while (snapshot < target && compareExchangeWatchdog-- > 0)
            {
                int prior = Interlocked.CompareExchange(ref _nextInstanceUidCounter, target, snapshot);
                if (prior == snapshot)
                    return;

                snapshot = prior;
            }
        }
    }
}
