using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
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
    internal static class PersistentWorldVaultMutationGuards
    {
        public const ulong CollectionMutationGuardMask = 1UL << 49;
    }

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
            return FromGridLocal(
                aligned.GridX,
                aligned.GridY,
                aligned.GridZ,
                new float3(aligned.Local.x, aligned.Local.y, aligned.Local.z));
        }

        public static AbsoluteUniversePosition FromGridLocal(long gridX, long gridY, long gridZ, float3 local)
        {
            if (!math.all(math.isfinite(local)))
                return Invalid();

            return FromGridLocalDouble(
                gridX,
                gridY,
                gridZ,
                new double3(local.x, local.y, local.z));
        }

        private static AbsoluteUniversePosition FromGridLocalDouble(long gridX, long gridY, long gridZ, double3 local)
        {
            if (!math.all(math.isfinite(local)))
                return Invalid();

            if (!TryCanonicalizeGridLocalAxis(ref gridX, ref local.x) ||
                !TryCanonicalizeGridLocalAxis(ref gridY, ref local.y) ||
                !TryCanonicalizeGridLocalAxis(ref gridZ, ref local.z))
                return Invalid();

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)local.x,
                LocalY = (float)local.y,
                LocalZ = (float)local.z
            };
        }

        public static AbsoluteUniversePosition FromRuntimePosition(Vector3 runtimePosition)
        {
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return Invalid();

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return Invalid();

            return OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
        }

        public static AbsoluteUniversePosition FromAbsolutePosition(double3 absolutePosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                return Invalid();

            double cellSize = CellSizeMeters;
            if (!TryResolveGridCoordinate(absolutePosition.x, cellSize, out long gridX) ||
                !TryResolveGridCoordinate(absolutePosition.y, cellSize, out long gridY) ||
                !TryResolveGridCoordinate(absolutePosition.z, cellSize, out long gridZ))
            {
                return Invalid();
            }

            double originX = (double)gridX * cellSize;
            double originY = (double)gridY * cellSize;
            double originZ = (double)gridZ * cellSize;

            double localX = absolutePosition.x - originX;
            double localY = absolutePosition.y - originY;
            double localZ = absolutePosition.z - originZ;
            if (!math.all(math.isfinite(new double3(localX, localY, localZ))))
                return Invalid();

            float localXFloat = (float)localX;
            float localYFloat = (float)localY;
            float localZFloat = (float)localZ;
            if (!TryCanonicalizeLocalAxis(ref gridX, ref localXFloat) ||
                !TryCanonicalizeLocalAxis(ref gridY, ref localYFloat) ||
                !TryCanonicalizeLocalAxis(ref gridZ, ref localZFloat))
            {
                return Invalid();
            }

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = localXFloat,
                LocalY = localYFloat,
                LocalZ = localZFloat
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCanonicalLocalOffset(float3 local)
        {
            return math.all(math.isfinite(local)) &&
                   local.x >= 0f &&
                   local.y >= 0f &&
                   local.z >= 0f &&
                   local.x < CellSizeMeters &&
                   local.y < CellSizeMeters &&
                   local.z < CellSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCanonicalizeGridLocalAxis(ref long grid, ref double local)
        {
            if (!math.isfinite(local))
                return false;

            if (local >= 0d && local < CellSizeMeters)
                return true;

            double cellSize = CellSizeMeters;
            double cellDeltaDouble = math.floor(local / cellSize);
            if (!math.isfinite(cellDeltaDouble) ||
                cellDeltaDouble < (double)long.MinValue ||
                cellDeltaDouble >= (double)long.MaxValue)
            {
                return false;
            }

            long cellDelta = (long)cellDeltaDouble;
            if ((cellDelta > 0L && grid > long.MaxValue - cellDelta) ||
                (cellDelta < 0L && grid < long.MinValue - cellDelta))
            {
                return false;
            }

            grid += cellDelta;
            local -= (double)cellDelta * cellSize;
            float localFloat = (float)local;
            if (!TryCanonicalizeLocalAxis(ref grid, ref localFloat))
                return false;

            local = localFloat;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCanonicalizeLocalAxis(ref long grid, ref float local)
        {
            if (!math.isfinite(local))
                return false;

            if (local >= CellSizeMeters)
            {
                if (grid == long.MaxValue)
                    return false;

                grid++;
                local -= CellSizeMeters;
            }
            else if (local < 0f)
            {
                if (grid == long.MinValue)
                    return false;

                grid--;
                local += CellSizeMeters;
            }

            return local >= 0f && local < CellSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveGridCoordinate(double absoluteAxisMeters, double cellSizeMeters, out long grid)
        {
            grid = 0L;
            if (!math.isfinite(absoluteAxisMeters) || cellSizeMeters <= 0d)
                return false;

            double gridDouble = math.floor(absoluteAxisMeters / cellSizeMeters);
            if (!math.isfinite(gridDouble) ||
                gridDouble < (double)long.MinValue ||
                gridDouble >= (double)long.MaxValue)
            {
                return false;
            }

            grid = (long)gridDouble;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition Invalid()
        {
            return new AbsoluteUniversePosition
            {
                LocalX = float.NaN,
                LocalY = float.NaN,
                LocalZ = float.NaN
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                   math.isfinite(value.LocalY) &&
                   math.isfinite(value.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFinite()
        {
            return IsFinite(in this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbsoluteUniversePosition Sanitize(
            in AbsoluteUniversePosition value,
            in AbsoluteUniversePosition fallback)
        {
            AbsoluteUniversePosition sanitized = CanonicalizeOrInvalid(in value);
            if (sanitized.IsFinite())
                return sanitized;

            return CanonicalizeOrInvalid(in fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AbsoluteUniversePosition CanonicalizeOrInvalid(in AbsoluteUniversePosition value)
        {
            if (!IsFinite(in value))
                return Invalid();

            float3 local = new float3(value.LocalX, value.LocalY, value.LocalZ);
            return IsCanonicalLocalOffset(local)
                ? value
                : FromGridLocal(value.GridX, value.GridY, value.GridZ, local);
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
            if (!IsFinite() || !math.all(math.isfinite(deltaMeters)))
                return Invalid();

            return FromGridLocalDouble(
                GridX,
                GridY,
                GridZ,
                new double3(
                    (double)LocalX + deltaMeters.x,
                    (double)LocalY + deltaMeters.y,
                    (double)LocalZ + deltaMeters.z));
        }

        public static double3 OffsetAbsoluteMeters(in AbsoluteUniversePosition origin, double3 deltaMeters)
        {
            return AUPMath.OffsetAbsoluteMeters(in origin, deltaMeters);
        }

        public static AbsoluteUniversePosition OffsetMeters(in AbsoluteUniversePosition origin, double3 deltaMeters)
        {
            if (!origin.IsFinite() || !math.all(math.isfinite(deltaMeters)))
                return Invalid();

            return FromGridLocalDouble(
                origin.GridX,
                origin.GridY,
                origin.GridZ,
                new double3(
                    (double)origin.LocalX + deltaMeters.x,
                    (double)origin.LocalY + deltaMeters.y,
                    (double)origin.LocalZ + deltaMeters.z));
        }

        public static AbsoluteUniversePosition WeightedAverage3(
            in AbsoluteUniversePosition a,
            in AbsoluteUniversePosition b,
            in AbsoluteUniversePosition c,
            double weight)
        {
            if (!a.IsFinite() || !b.IsFinite() || !c.IsFinite() || !math.isfinite(weight))
                return Invalid();

            double3 anchoredDelta = (AUPMath.AUPDeltaClamped(in b, in a) + AUPMath.AUPDeltaClamped(in c, in a)) * weight;
            return a.OffsetMeters(anchoredDelta);
        }

        public float3 ToRuntimeFloat3()
        {
            return TryToRuntimeFloat3(out float3 runtimePosition)
                ? runtimePosition
                : InvalidRuntimeFloat3();
        }

        public bool TryToRuntimeFloat3(out float3 runtimePosition)
        {
            runtimePosition = InvalidRuntimeFloat3();
            if (!IsFinite())
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            runtimePosition = AUPMath.ResolveCameraRelative(in this, in originAup);
            if (!math.all(math.isfinite(runtimePosition)))
            {
                runtimePosition = InvalidRuntimeFloat3();
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 InvalidRuntimeFloat3()
        {
            return new float3(float.NaN, float.NaN, float.NaN);
        }

        /// <summary>
        /// Converts an AUP into camera-relative view space without truncating sector deltas to float first.
        /// </summary>
        /// <param name="position">World entity AUP.</param>
        /// <param name="cameraPosition">Camera AUP used as the local origin.</param>
        /// <returns>Camera-relative float position for rendering and culling.</returns>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static float3 ToCameraRelativeFloat3(in AbsoluteUniversePosition position, in AbsoluteUniversePosition cameraPosition)
        {
            return AUPMath.ResolveCameraRelative(in position, in cameraPosition);
        }

        public static int3 ResolveChunkId(in AbsoluteUniversePosition position, int chunkSizeMeters)
        {
            if (!position.IsFinite() || chunkSizeMeters <= 0)
                return InvalidChunkId();

            double3 absolutePosition = position.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absolutePosition)))
                return InvalidChunkId();

            double chunkSize = chunkSizeMeters;
            if (!TryFloorToInt(absolutePosition.x / chunkSize, out int chunkX) ||
                !TryFloorToInt(absolutePosition.y / chunkSize, out int chunkY) ||
                !TryFloorToInt(absolutePosition.z / chunkSize, out int chunkZ))
            {
                return InvalidChunkId();
            }

            return new int3(chunkX, chunkY, chunkZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryFloorToInt(double value, out int result)
        {
            result = int.MinValue;
            if (!math.isfinite(value) ||
                value <= int.MinValue ||
                value >= int.MaxValue)
            {
                return false;
            }

            result = (int)math.floor(value);
            return result > int.MinValue && result < int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidChunkId(int3 chunkId)
        {
            return chunkId.x > int.MinValue &&
                   chunkId.y > int.MinValue &&
                   chunkId.z > int.MinValue &&
                   chunkId.x < int.MaxValue &&
                   chunkId.y < int.MaxValue &&
                   chunkId.z < int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 InvalidChunkId()
        {
            return new int3(int.MinValue, int.MinValue, int.MinValue);
        }

        internal static bool TryResolveSectorCoord(
            in AbsoluteUniversePosition position,
            int sectorEdgeMeters,
            out int2 sectorCoord)
        {
            sectorCoord = new int2(int.MinValue, 0);
            if (sectorEdgeMeters <= 0)
                return false;

            int safeSectorEdge = sectorEdgeMeters;
            AbsoluteUniversePosition canonical = CanonicalizeOrInvalid(in position);
            if (!canonical.IsFinite())
                return false;

            if (!TryQuantizeAxisToIntSector(canonical.GridX, canonical.LocalX, safeSectorEdge, out int sectorX) ||
                !TryQuantizeAxisToIntSector(canonical.GridZ, canonical.LocalZ, safeSectorEdge, out int sectorZ))
            {
                return false;
            }

            sectorCoord = new int2(sectorX, sectorZ);
            return true;
        }

        private static bool TryQuantizeAxisToIntSector(long grid, float local, int sectorEdgeMeters, out int sectorCoord)
        {
            sectorCoord = int.MinValue;
            if (!math.isfinite(local) || sectorEdgeMeters <= 0)
                return false;

            if (CellSizeMeters % sectorEdgeMeters == 0)
            {
                long sectorsPerCell = CellSizeMeters / sectorEdgeMeters;
                if (sectorsPerCell <= 0L ||
                    grid > long.MaxValue / sectorsPerCell ||
                    grid < long.MinValue / sectorsPerCell)
                {
                    return false;
                }

                double localSectorDouble = math.floor(local / (double)sectorEdgeMeters);
                if (!math.isfinite(localSectorDouble) ||
                    localSectorDouble < 0d ||
                    localSectorDouble >= sectorsPerCell)
                {
                    return false;
                }

                long baseSector = grid * sectorsPerCell;
                long localSector = (long)localSectorDouble;
                if (localSector > 0L && baseSector > long.MaxValue - localSector)
                    return false;

                long totalSector = baseSector + localSector;
                if (totalSector <= int.MinValue || totalSector >= int.MaxValue)
                    return false;

                sectorCoord = (int)totalSector;
                return true;
            }

            double absolute = ((double)grid * CellSizeMeters) + local;
            if (!math.isfinite(absolute) ||
                !TryFloorToInt(absolute / sectorEdgeMeters, out sectorCoord))
            {
                return false;
            }

            return true;
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

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct PersistentThermalVentRecord
    {
        [FieldOffset(0)]
        public long RuntimeKey;
        [FieldOffset(8)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(56)]
        public float RadiusWS;
        [FieldOffset(60)]
        public float HeightWS;
        [FieldOffset(64)]
        public float UpdraftVelocity;
        [FieldOffset(68)]
        public float HeatIntensity;
        [FieldOffset(72)]
        public float SmokeDensity;
        [FieldOffset(76)]
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
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    internal struct PoolSlotData
    {
        [FieldOffset(0)] public ulong BoundGuid;
        [FieldOffset(8)] public long GridX;
        [FieldOffset(16)] public long GridY;
        [FieldOffset(24)] public long GridZ;
        [FieldOffset(32)] public float3 LocalOffset;
        [FieldOffset(44)] public ushort HydrationFrame;
        [FieldOffset(46)] public byte RefCount;
        [FieldOffset(47)] public byte StateFlags;
        [FieldOffset(48)] public ushort StableFrames;
        [FieldOffset(50)] public ushort LastVisibleFrame;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private ulong _pad1;
        [FieldOffset(64)] private ulong _pad2;
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

    /// <summary>
    /// Fauna persistence route that lets AI cache hibernation/egg records without depending on the concrete registry owner.
    /// </summary>
    internal interface IFaunaPersistentWorldStateService : ISystem
    {
        bool TryCacheFaunaHibernationState(in EntityDataRecord faunaState);

        bool TryCacheFaunaEggState(in EntityDataRecord eggState);

        int ConsumeCachedFaunaHibernationStates(
            in AbsoluteUniversePosition playerAup,
            float restoreRadiusMeters,
            List<EntityDataRecord> destination);

        int MigrateApexFaunaHibernationStatesToward(
            in AbsoluteUniversePosition attractorAup,
            float searchRadiusMeters,
            float stepMeters);
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct ResourceNodeTombstoneRecord
    {
        [FieldOffset(0)]
        public ulong TombstoneId;
        [FieldOffset(8)]
        public AbsoluteUniversePosition Position;
        [FieldOffset(56)]
        public int3 ChunkId;
        [FieldOffset(68)]
        public uint InstanceUid;
        [FieldOffset(72)]
        public uint Reserved0;
        [FieldOffset(76)]
        public uint Reserved1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct PersistentWorldItemRecord
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition Position;
        [FieldOffset(48)]
        public ulong ItemPersistentIdHash;
        [FieldOffset(56)]
        public FixedString128Bytes ItemPersistentId;
        [FieldOffset(184)]
        public int3 ChunkId;
        [FieldOffset(196)]
        public int Quantity;
        [FieldOffset(200)]
        public uint InstanceUid;
        [FieldOffset(204)]
        public PersistentWorldItemFlags Flags;
        [FieldOffset(205)] private byte _padFlags0;
        [FieldOffset(206)] private byte _padFlags1;
        [FieldOffset(207)] private byte _padFlags2;
        [FieldOffset(208)] private ulong _pad1;
        [FieldOffset(216)] private ulong _pad2;
        [FieldOffset(224)] private ulong _pad3;
        [FieldOffset(232)] private ulong _pad4;
        [FieldOffset(240)] private ulong _pad5;
        [FieldOffset(248)] private ulong _pad6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlag(in PersistentWorldItemRecord record, PersistentWorldItemFlags flag)
        {
            return (record.Flags & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCollected(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.Collected);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraDestroyed(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraDestroyed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDeleted(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.Deleted);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraSeedPending(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraSeedPending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraSeedReady(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraSeedReady);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraStateOverride(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraStateOverride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsResourceNodeDestroyed(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.ResourceNodeDestroyed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsResourceNodeMetamorphosed(in PersistentWorldItemRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.ResourceNodeMetamorphosed);
        }

        public void MarkCollected()
        {
            Flags |= PersistentWorldItemFlags.Collected;
        }

        public void MarkDeleted()
        {
            Flags |= PersistentWorldItemFlags.Deleted;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PersistentWorldDeltaRecord
    {
        private const uint PackedAxisMask = 0x3FFu;
        private const float PackedAxisScale = 1023f;

        [FieldOffset(0)]
        public ulong ItemPersistentIdHash;
        [FieldOffset(8)]
        public int3 ChunkId;
        [FieldOffset(20)]
        public uint InstanceUid;
        [FieldOffset(24)]
        public uint PackedLocalPosition;
        [FieldOffset(28)]
        public ushort Quantity;
        [FieldOffset(30)]
        public byte ItemFlags;
        [FieldOffset(31)]
        public byte Reserved;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlag(in PersistentWorldDeltaRecord record, PersistentWorldItemFlags flag)
        {
            return ((PersistentWorldItemFlags)record.ItemFlags & flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDeleted(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.Deleted);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraSeedPending(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraSeedPending);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraSeedReady(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraSeedReady);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloraStateOverride(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.FloraStateOverride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsResourceNodeDestroyed(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.ResourceNodeDestroyed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsResourceNodeMetamorphosed(in PersistentWorldDeltaRecord record)
        {
            return HasFlag(in record, PersistentWorldItemFlags.ResourceNodeMetamorphosed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(in PersistentWorldDeltaRecord record)
        {
            return record.InstanceUid != 0u &&
                   AbsoluteUniversePosition.IsValidChunkId(record.ChunkId) &&
                   (IsDeleted(in record) || (record.ItemPersistentIdHash != 0UL && record.Quantity > 0));
        }

        public static PersistentWorldDeltaRecord FromRecord(in PersistentWorldItemRecord record, int chunkSizeMeters)
        {
            if (chunkSizeMeters <= 0 ||
                !record.Position.IsFinite() ||
                !AbsoluteUniversePosition.IsValidChunkId(record.ChunkId))
            {
                return default;
            }

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
            if (chunkSizeMeters <= 0)
                return AbsoluteUniversePosition.Invalid();

            UnpackLocalPosition(PackedLocalPosition, chunkSizeMeters, out float localX, out float localY, out float localZ);
            double3 chunkOrigin = new double3(
                ChunkId.x * (double)chunkSizeMeters,
                ChunkId.y * (double)chunkSizeMeters,
                ChunkId.z * (double)chunkSizeMeters);

            return AbsoluteUniversePosition.FromAbsolutePosition(chunkOrigin + new double3(localX, localY, localZ));
        }

        private static uint PackLocalPosition(in AbsoluteUniversePosition position, int3 chunkId, int chunkSizeMeters)
        {
            if (chunkSizeMeters <= 0 ||
                !position.IsFinite() ||
                !AbsoluteUniversePosition.IsValidChunkId(chunkId))
            {
                return 0u;
            }

            double3 absolute = position.ToAbsoluteDouble3();
            double3 chunkOrigin = new double3(
                chunkId.x * (double)chunkSizeMeters,
                chunkId.y * (double)chunkSizeMeters,
                chunkId.z * (double)chunkSizeMeters);

            double maxLocal = math.max(0d, (double)chunkSizeMeters);
            float localX = (float)math.clamp(absolute.x - chunkOrigin.x, 0d, maxLocal);
            float localY = (float)math.clamp(absolute.y - chunkOrigin.y, 0d, maxLocal);
            float localZ = (float)math.clamp(absolute.z - chunkOrigin.z, 0d, maxLocal);
            float inverseChunkSize = 1f / chunkSizeMeters;

            uint x = (uint)math.round(math.saturate(localX * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint y = (uint)math.round(math.saturate(localY * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            uint z = (uint)math.round(math.saturate(localZ * inverseChunkSize) * PackedAxisScale) & PackedAxisMask;
            return x | (y << 10) | (z << 20);
        }

        private static void UnpackLocalPosition(uint packed, int chunkSizeMeters, out float localX, out float localY, out float localZ)
        {
            if (chunkSizeMeters <= 0)
            {
                localX = float.NaN;
                localY = float.NaN;
                localZ = float.NaN;
                return;
            }

            float chunkSize = chunkSizeMeters;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasDeletedFlag(in PersistentWorldCompactDeltaRecord record)
        {
            return ((PersistentWorldItemFlags)record.ItemFlags & PersistentWorldItemFlags.Deleted) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidRecord(in PersistentWorldCompactDeltaRecord record)
        {
            return record.InstanceUid != 0u && (HasDeletedFlag(in record) || record.Quantity > 0);
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WorldTelemetryEntry
    {
        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint BufferId;
        [FieldOffset(12)] public uint Generation;
        [FieldOffset(16)] public uint SystemId;
        [FieldOffset(20)] public uint EventCode;
        [FieldOffset(24)] public int ActualLength;
        [FieldOffset(28)] public int ExpectedLength;
        [FieldOffset(32)] public int3 ChunkId;
        [FieldOffset(44)] public float Microseconds;
        [FieldOffset(48)] public uint InstanceUid;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    internal struct VaultBackedArray<T>
        where T : unmanaged
    {
        private IDataVault _vault;
        private VaultGenerationHandle<T> _handle;
        private BufferID _bufferId;
        private SystemID _owner;
        private int _length;

        public bool IsCreated => _vault != null && _handle.BufferID != 0u && _length > 0;
        public int Length => IsCreated ? _length : 0;
        public int Capacity => Length;
        public long EstimatedBytes => IsCreated ? (long)_length * UnsafeUtility.SizeOf<T>() : 0L;

        public void Initialize(IDataVault vault, BufferID bufferId, int length, SystemID owner, NativeArrayOptions options)
        {
            Dispose();
            _vault = vault;
            _bufferId = bufferId;
            _owner = owner;
            _length = math.max(0, length);
            _handle = vault != null && _length > 0
                ? vault.EnsureGenerationHandle<T>(bufferId, _length, owner, options)
                : default;
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault != null && _handle.BufferID != 0u)
                vault.ReleaseBuffer(in _handle);

            this = default;
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Length ||
                    _vault == null ||
                    !_vault.TryReadOnlyHandle(in _handle, out NativeArray<T>.ReadOnly values) ||
                    (uint)index >= (uint)values.Length)
                {
                    return default;
                }

                return values[index];
            }
            set
            {
                TryWrite(index, value);
            }
        }

        public bool TryWrite(int index, T value)
        {
            IDataVault vault = _vault;
            if ((uint)index >= (uint)Length || vault == null)
                return false;

            bool locked = vault.TryAcquireWriteLock(in _handle, _owner, out NativeArray<T> values);
            if (!locked)
                return false;

            try
            {
                if ((uint)index >= (uint)values.Length)
                    return false;

                values[index] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _handle, _owner);
            }
        }

        public bool Clear()
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool locked = vault.TryAcquireWriteLock(in _handle, _owner, out NativeArray<T> values);
            if (!locked)
                return false;

            try
            {
                int count = math.min(_length, values.Length);
                for (int i = 0; i < count; i++)
                    values[i] = default;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _handle, _owner);
            }
        }

        public bool TryCopyTo(T[] destination, int maxCount, out int copiedCount)
        {
            copiedCount = 0;
            if (destination == null || maxCount <= 0 || !IsCreated || _vault == null)
                return false;

            if (!_vault.TryReadOnlyHandle(in _handle, out NativeArray<T>.ReadOnly values))
                return false;

            int copyCount = math.min(math.min(maxCount, destination.Length), math.min(_length, values.Length));
            for (int i = 0; i < copyCount; i++)
                destination[i] = values[i];

            copiedCount = copyCount;
            return true;
        }

    }

    internal struct VaultBackedList<T>
        where T : unmanaged
    {
        private IDataVault _vault;
        private VaultGenerationHandle<T> _itemsHandle;
        private VaultGenerationHandle<int> _countHandle;
        private BufferID _itemsBufferId;
        private BufferID _countBufferId;
        private SystemID _owner;
        private int _capacity;

        public bool IsCreated => _vault != null && _itemsHandle.BufferID != 0u && _countHandle.BufferID != 0u && _capacity > 0;
        public int Capacity => IsCreated ? _capacity : 0;
        public int Length => ReadCount();
        public long EstimatedBytes => IsCreated ? ((long)_capacity * UnsafeUtility.SizeOf<T>()) + UnsafeUtility.SizeOf<int>() : 0L;

        public void Initialize(IDataVault vault, BufferID itemsBufferId, BufferID countBufferId, int capacity, SystemID owner, NativeArrayOptions itemOptions)
        {
            Dispose();
            _vault = vault;
            _itemsBufferId = itemsBufferId;
            _countBufferId = countBufferId;
            _owner = owner;
            _capacity = math.max(0, capacity);
            if (vault == null || _capacity <= 0)
                return;

            _itemsHandle = vault.EnsureGenerationHandle<T>(itemsBufferId, _capacity, owner, itemOptions);
            _countHandle = vault.EnsureGenerationHandle<int>(countBufferId, 1, owner, NativeArrayOptions.ClearMemory);
            Clear();
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                if (_itemsHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _itemsHandle);
                if (_countHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _countHandle);
            }

            this = default;
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Length ||
                    _vault == null ||
                    !_vault.TryReadOnlyHandle(in _itemsHandle, out NativeArray<T>.ReadOnly items) ||
                    (uint)index >= (uint)items.Length)
                {
                    return default;
                }

                return items[index];
            }
            set
            {
                TryWrite(index, value);
            }
        }

        public bool TryWrite(int index, T value)
        {
            IDataVault vault = _vault;
            if ((uint)index >= (uint)Length || vault == null)
                return false;

            bool itemsLocked = vault.TryAcquireWriteLock(in _itemsHandle, _owner, out NativeArray<T> items);
            if (!itemsLocked)
                return false;

            try
            {
                if ((uint)index >= (uint)items.Length)
                    return false;

                items[index] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _itemsHandle, _owner);
            }
        }

        public bool Clear()
        {
            IDataVault vault = _vault;
            if (vault == null || _countHandle.BufferID == 0u)
                return false;

            bool countLocked = vault.TryAcquireWriteLock(in _countHandle, _owner, out NativeArray<int> count);
            if (!countLocked)
                return false;

            try
            {
                if (count.Length > 0)
                {
                    count[0] = 0;
                    return true;
                }

                return false;
            }
            finally
            {
                vault.ReleaseWriteLock(in _countHandle, _owner);
            }
        }

        public bool AddNoResize(T value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _itemsHandle, out NativeArray<T> items) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int length = math.clamp(count[0], 0, math.min(_capacity, items.Length));
                if (length >= _capacity || length >= items.Length)
                    return false;

                items[length] = value;
                count[0] = length + 1;
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public void RemoveAtSwapBack(int index)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return;

            try
            {
                if (!vault.TryResolveHandle(in _itemsHandle, out NativeArray<T> items) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return;
                }

                int length = math.clamp(count[0], 0, math.min(_capacity, items.Length));
                if ((uint)index >= (uint)length)
                    return;

                int last = length - 1;
                items[index] = items[last];
                items[last] = default;
                count[0] = last;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool TryRead(int index, out T value)
        {
            value = default;
            if (_vault == null ||
                _countHandle.BufferID == 0u ||
                _itemsHandle.BufferID == 0u ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                !_vault.TryReadOnlyHandle(in _itemsHandle, out NativeArray<T>.ReadOnly items) ||
                count.Length <= 0)
            {
                return false;
            }

            int length = math.clamp(count[0], 0, math.min(_capacity, items.Length));
            if ((uint)index >= (uint)length)
                return false;

            value = items[index];
            return true;
        }

        public int CopyTo(NativeArray<T> destination, int maxCount)
        {
            return TryCopyTo(destination, maxCount, out int copiedCount) ? copiedCount : 0;
        }

        public bool TryCopyTo(NativeArray<T> destination, int maxCount, out int copiedCount)
        {
            if (!destination.IsCreated || maxCount <= 0 || !IsCreated || _vault == null)
            {
                copiedCount = 0;
                return false;
            }

            if (!_vault.TryReadOnlyHandle(in _itemsHandle, out NativeArray<T>.ReadOnly items) ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                count.Length <= 0)
            {
                copiedCount = 0;
                return false;
            }

            int length = math.clamp(count[0], 0, math.min(_capacity, items.Length));
            int copyLimit = math.min(destination.Length, math.min(maxCount, length));
            for (int i = 0; i < copyLimit; i++)
                destination[i] = items[i];

            copiedCount = copyLimit;
            return true;
        }

        private int ReadCount()
        {
            if (_vault == null ||
                _countHandle.BufferID == 0u ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                count.Length <= 0)
            {
                return 0;
            }

            return math.clamp(count[0], 0, _capacity);
        }
    }

    internal struct VaultBackedHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private IDataVault _vault;
        private VaultGenerationHandle<TKey> _keysHandle;
        private VaultGenerationHandle<TValue> _valuesHandle;
        private VaultGenerationHandle<byte> _statesHandle;
        private VaultGenerationHandle<int> _countHandle;
        private SystemID _owner;
        private int _capacity;

        public bool IsCreated => _vault != null && _keysHandle.BufferID != 0u && _valuesHandle.BufferID != 0u && _statesHandle.BufferID != 0u && _countHandle.BufferID != 0u && _capacity > 0;
        public int Capacity => IsCreated ? _capacity : 0;
        public int Count => ReadCount();
        public long EstimatedBytes => IsCreated ? (long)_capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>() + 1L) + UnsafeUtility.SizeOf<int>() : 0L;

        public void Initialize(IDataVault vault, BufferID keysBufferId, BufferID valuesBufferId, BufferID statesBufferId, BufferID countBufferId, int capacity, SystemID owner)
        {
            Dispose();
            _vault = vault;
            _owner = owner;
            _capacity = math.max(0, capacity);
            if (vault == null || _capacity <= 0)
                return;

            _keysHandle = vault.EnsureGenerationHandle<TKey>(keysBufferId, _capacity, owner, NativeArrayOptions.UninitializedMemory);
            _valuesHandle = vault.EnsureGenerationHandle<TValue>(valuesBufferId, _capacity, owner, NativeArrayOptions.UninitializedMemory);
            _statesHandle = vault.EnsureGenerationHandle<byte>(statesBufferId, _capacity, owner, NativeArrayOptions.ClearMemory);
            _countHandle = vault.EnsureGenerationHandle<int>(countBufferId, 1, owner, NativeArrayOptions.ClearMemory);
            Clear();
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                if (_keysHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _keysHandle);
                if (_valuesHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _valuesHandle);
                if (_statesHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _statesHandle);
                if (_countHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _countHandle);
            }

            this = default;
        }

        public bool Clear()
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _statesHandle, out NativeArray<byte> states) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int stateCount = math.min(_capacity, states.Length);
                for (int i = 0; i < stateCount; i++)
                    states[i] = 0;

                count[0] = 0;
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;
            if (!IsCreated || _vault == null)
                return false;

            if (!_vault.TryReadOnlyHandle(in _keysHandle, out NativeArray<TKey>.ReadOnly keys) ||
                !_vault.TryReadOnlyHandle(in _valuesHandle, out NativeArray<TValue>.ReadOnly values) ||
                !_vault.TryReadOnlyHandle(in _statesHandle, out NativeArray<byte>.ReadOnly states))
            {
                return false;
            }

            int capacity = math.min(_capacity, math.min(keys.Length, math.min(values.Length, states.Length)));
            if (capacity <= 0)
                return false;

            int slot = ResolveSlot(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (slot + probe) % capacity;
                byte state = states[index];
                if (state == 0)
                    return false;
                if (state == 1 && keys[index].Equals(key))
                {
                    value = values[index];
                    return true;
                }
            }

            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        private int ReadCount()
        {
            if (!IsCreated ||
                _vault == null ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                count.Length <= 0)
            {
                return 0;
            }

            return math.clamp(count[0], 0, _capacity);
        }

        public bool TryAdd(TKey key, TValue value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _valuesHandle, out NativeArray<TValue> values) ||
                    !vault.TryResolveHandle(in _statesHandle, out NativeArray<byte> states) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int capacity = math.min(_capacity, math.min(keys.Length, math.min(values.Length, states.Length)));
                if (capacity <= 0)
                    return false;

                int activeCount = math.clamp(count[0], 0, capacity);
                if (count[0] != activeCount)
                    count[0] = activeCount;

                if (activeCount >= capacity)
                    return false;

                int slot = ResolveSlot(key, capacity);
                int tombstoneIndex = -1;
                for (int probe = 0; probe < capacity; probe++)
                {
                    int index = (slot + probe) % capacity;
                    byte state = states[index];
                    if (state == 1)
                    {
                        if (keys[index].Equals(key))
                            return false;
                        continue;
                    }

                    if (state == 2)
                    {
                        if (tombstoneIndex < 0)
                            tombstoneIndex = index;
                        continue;
                    }

                    int writeIndex = tombstoneIndex >= 0 ? tombstoneIndex : index;
                    keys[writeIndex] = key;
                    values[writeIndex] = value;
                    states[writeIndex] = 1;
                    count[0] = activeCount + 1;
                    return true;
                }

                if (tombstoneIndex >= 0)
                {
                    keys[tombstoneIndex] = key;
                    values[tombstoneIndex] = value;
                    states[tombstoneIndex] = 1;
                    count[0] = activeCount + 1;
                    return true;
                }

                return false;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool TrySet(TKey key, TValue value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _valuesHandle, out NativeArray<TValue> values) ||
                    !vault.TryResolveHandle(in _statesHandle, out NativeArray<byte> states) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int capacity = math.min(_capacity, math.min(keys.Length, math.min(values.Length, states.Length)));
                if (capacity <= 0)
                    return false;

                int activeCount = math.clamp(count[0], 0, capacity);
                if (count[0] != activeCount)
                    count[0] = activeCount;

                int slot = ResolveSlot(key, capacity);
                int tombstoneIndex = -1;
                for (int probe = 0; probe < capacity; probe++)
                {
                    int index = (slot + probe) % capacity;
                    byte state = states[index];
                    if (state == 1)
                    {
                        if (keys[index].Equals(key))
                        {
                            values[index] = value;
                            return true;
                        }

                        continue;
                    }

                    if (state == 2)
                    {
                        if (tombstoneIndex < 0)
                            tombstoneIndex = index;
                        continue;
                    }

                    int writeIndex = tombstoneIndex >= 0 ? tombstoneIndex : index;
                    if (activeCount >= capacity && states[writeIndex] != 2)
                        return false;

                    keys[writeIndex] = key;
                    values[writeIndex] = value;
                    states[writeIndex] = 1;
                    count[0] = math.min(capacity, activeCount + 1);
                    return true;
                }

                if (tombstoneIndex >= 0)
                {
                    keys[tombstoneIndex] = key;
                    values[tombstoneIndex] = value;
                    states[tombstoneIndex] = 1;
                    count[0] = math.min(capacity, activeCount + 1);
                    return true;
                }

                return false;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool TrySetReplacing(TKey previousKey, TKey nextKey, TValue value)
        {
            if (previousKey.Equals(nextKey))
                return TrySet(nextKey, value);

            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _valuesHandle, out NativeArray<TValue> values) ||
                    !vault.TryResolveHandle(in _statesHandle, out NativeArray<byte> states) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int capacity = math.min(_capacity, math.min(keys.Length, math.min(values.Length, states.Length)));
                if (capacity <= 0)
                    return false;

                count[0] = math.clamp(count[0], 0, capacity);

                int nextIndex = FindActiveIndex(keys, states, nextKey, capacity);
                int previousIndex = FindActiveIndex(keys, states, previousKey, capacity);
                if (nextIndex >= 0)
                {
                    values[nextIndex] = value;
                    if (previousIndex >= 0 && previousIndex != nextIndex)
                    {
                        states[previousIndex] = 2;
                        count[0] = math.max(0, count[0] - 1);
                    }

                    return true;
                }

                bool removedPrevious = previousIndex >= 0;
                TKey previousStoredKey = default;
                TValue previousStoredValue = default;
                if (removedPrevious)
                {
                    previousStoredKey = keys[previousIndex];
                    previousStoredValue = values[previousIndex];
                    states[previousIndex] = 2;
                    count[0] = math.max(0, count[0] - 1);
                }

                int writeIndex = FindWritableIndex(states, nextKey, capacity);
                if (writeIndex < 0 ||
                    (!removedPrevious && count[0] >= capacity && states[writeIndex] != 2))
                {
                    if (removedPrevious)
                        RestoreActiveSlot(keys, values, states, count, previousIndex, previousStoredKey, previousStoredValue, capacity);
                    return false;
                }

                keys[writeIndex] = nextKey;
                values[writeIndex] = value;
                states[writeIndex] = 1;
                count[0] = math.min(capacity, count[0] + 1);
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool Remove(TKey key)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _statesHandle, out NativeArray<byte> states) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int capacity = math.min(_capacity, math.min(keys.Length, states.Length));
                if (capacity <= 0)
                    return false;

                int activeCount = math.clamp(count[0], 0, capacity);
                if (count[0] != activeCount)
                    count[0] = activeCount;

                int slot = ResolveSlot(key, capacity);
                for (int probe = 0; probe < capacity; probe++)
                {
                    int index = (slot + probe) % capacity;
                    byte state = states[index];
                    if (state == 0)
                        return false;
                    if (state == 1 && keys[index].Equals(key))
                    {
                        states[index] = 2;
                        count[0] = math.max(0, activeCount - 1);
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public void CopyValuesTo(List<TValue> destination)
        {
            if (destination == null || !IsCreated || _vault == null)
                return;

            if (!_vault.TryReadOnlyHandle(in _valuesHandle, out NativeArray<TValue>.ReadOnly values) ||
                !_vault.TryReadOnlyHandle(in _statesHandle, out NativeArray<byte>.ReadOnly states))
            {
                return;
            }

            int capacity = math.min(_capacity, math.min(values.Length, states.Length));
            for (int i = 0; i < capacity; i++)
            {
                if (destination.Count >= destination.Capacity)
                    break;

                if (states[i] == 1)
                    destination.Add(values[i]);
            }
        }

        private static int ResolveSlot(TKey key, int capacity)
        {
            return (int)((uint)key.GetHashCode() % (uint)math.max(1, capacity));
        }

        private static int FindActiveIndex(NativeArray<TKey> keys, NativeArray<byte> states, TKey key, int capacity)
        {
            int slot = ResolveSlot(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (slot + probe) % capacity;
                byte state = states[index];
                if (state == 0)
                    return -1;
                if (state == 1 && keys[index].Equals(key))
                    return index;
            }

            return -1;
        }

        private static int FindWritableIndex(NativeArray<byte> states, TKey key, int capacity)
        {
            int slot = ResolveSlot(key, capacity);
            int tombstoneIndex = -1;
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (slot + probe) % capacity;
                byte state = states[index];
                if (state == 1)
                    continue;

                if (state == 2)
                {
                    if (tombstoneIndex < 0)
                        tombstoneIndex = index;
                    continue;
                }

                return tombstoneIndex >= 0 ? tombstoneIndex : index;
            }

            return tombstoneIndex;
        }

        private static void RestoreActiveSlot(
            NativeArray<TKey> keys,
            NativeArray<TValue> values,
            NativeArray<byte> states,
            NativeArray<int> count,
            int index,
            TKey key,
            TValue value,
            int capacity)
        {
            keys[index] = key;
            values[index] = value;
            states[index] = 1;
            count[0] = math.min(capacity, count[0] + 1);
        }
    }

    internal struct VaultBackedHashSet<TKey>
        where TKey : unmanaged, IEquatable<TKey>
    {
        private VaultBackedHashMap<TKey, byte> _map;

        public bool IsCreated => _map.IsCreated;
        public int Capacity => _map.Capacity;
        public int Length => _map.Count;
        public long EstimatedBytes => _map.EstimatedBytes;

        public void Initialize(IDataVault vault, BufferID keysBufferId, BufferID valuesBufferId, BufferID statesBufferId, BufferID countBufferId, int capacity, SystemID owner)
        {
            _map.Initialize(vault, keysBufferId, valuesBufferId, statesBufferId, countBufferId, capacity, owner);
        }

        public void Dispose()
        {
            _map.Dispose();
        }

        public bool Clear()
        {
            return _map.Clear();
        }

        public bool Add(TKey key)
        {
            return _map.TryAdd(key, 1);
        }

        public bool Remove(TKey key)
        {
            return _map.Remove(key);
        }

        public bool Contains(TKey key)
        {
            return _map.ContainsKey(key);
        }
    }

    internal struct VaultBackedMultiHashMap<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private IDataVault _vault;
        private VaultGenerationHandle<TKey> _keysHandle;
        private VaultGenerationHandle<TValue> _valuesHandle;
        private VaultGenerationHandle<int> _countHandle;
        private SystemID _owner;
        private int _capacity;

        public bool IsCreated => _vault != null && _keysHandle.BufferID != 0u && _valuesHandle.BufferID != 0u && _countHandle.BufferID != 0u && _capacity > 0;
        public int Capacity => IsCreated ? _capacity : 0;
        public int Count => ReadCount();
        public long EstimatedBytes => IsCreated ? (long)_capacity * (UnsafeUtility.SizeOf<TKey>() + UnsafeUtility.SizeOf<TValue>()) + UnsafeUtility.SizeOf<int>() : 0L;

        public void Initialize(IDataVault vault, BufferID keysBufferId, BufferID valuesBufferId, BufferID countBufferId, int capacity, SystemID owner)
        {
            Dispose();
            _vault = vault;
            _owner = owner;
            _capacity = math.max(0, capacity);
            if (vault == null || _capacity <= 0)
                return;

            _keysHandle = vault.EnsureGenerationHandle<TKey>(keysBufferId, _capacity, owner, NativeArrayOptions.UninitializedMemory);
            _valuesHandle = vault.EnsureGenerationHandle<TValue>(valuesBufferId, _capacity, owner, NativeArrayOptions.UninitializedMemory);
            _countHandle = vault.EnsureGenerationHandle<int>(countBufferId, 1, owner, NativeArrayOptions.ClearMemory);
            Clear();
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                if (_keysHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _keysHandle);
                if (_valuesHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _valuesHandle);
                if (_countHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _countHandle);
            }

            this = default;
        }

        public bool Clear()
        {
            IDataVault vault = _vault;
            if (vault == null || _countHandle.BufferID == 0u)
                return false;

            bool countLocked = vault.TryAcquireWriteLock(in _countHandle, _owner, out NativeArray<int> count);
            if (!countLocked)
                return false;

            try
            {
                if (count.Length > 0)
                {
                    count[0] = 0;
                    return true;
                }

                return false;
            }
            finally
            {
                vault.ReleaseWriteLock(in _countHandle, _owner);
            }
        }

        private int ReadCount()
        {
            if (!IsCreated ||
                _vault == null ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                count.Length <= 0)
            {
                return 0;
            }

            return math.clamp(count[0], 0, _capacity);
        }

        public bool Add(TKey key, TValue value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _valuesHandle, out NativeArray<TValue> values) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int length = math.clamp(count[0], 0, math.min(_capacity, math.min(keys.Length, values.Length)));
                if (length >= _capacity || length >= keys.Length || length >= values.Length)
                    return false;

                keys[length] = key;
                values[length] = value;
                count[0] = length + 1;
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public void CopyValuesForKey(TKey key, List<TValue> destination)
        {
            if (destination == null || !IsCreated || _vault == null)
                return;

            if (!_vault.TryReadOnlyHandle(in _keysHandle, out NativeArray<TKey>.ReadOnly keys) ||
                !_vault.TryReadOnlyHandle(in _valuesHandle, out NativeArray<TValue>.ReadOnly values) ||
                !_vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly count) ||
                count.Length <= 0)
            {
                return;
            }

            int length = math.clamp(count[0], 0, math.min(_capacity, math.min(keys.Length, values.Length)));
            for (int i = 0; i < length; i++)
            {
                if (destination.Count >= destination.Capacity)
                    break;

                if (keys[i].Equals(key))
                    destination.Add(values[i]);
            }
        }

        public bool RemoveFirst(TKey key, TValue value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _keysHandle, out NativeArray<TKey> keys) ||
                    !vault.TryResolveHandle(in _valuesHandle, out NativeArray<TValue> values) ||
                    !vault.TryResolveHandle(in _countHandle, out NativeArray<int> count) ||
                    count.Length <= 0)
                {
                    return false;
                }

                int length = math.clamp(count[0], 0, math.min(_capacity, math.min(keys.Length, values.Length)));
                for (int i = 0; i < length; i++)
                {
                    if (!keys[i].Equals(key) || !values[i].Equals(value))
                        continue;

                    int last = length - 1;
                    keys[i] = keys[last];
                    values[i] = values[last];
                    keys[last] = default;
                    values[last] = default;
                    count[0] = last;
                    return true;
                }

                return false;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }
    }

    internal struct VaultBackedQueue<T>
        where T : unmanaged
    {
        private IDataVault _vault;
        private VaultGenerationHandle<T> _valuesHandle;
        private VaultGenerationHandle<int> _stateHandle;
        private SystemID _owner;
        private int _capacity;

        public bool IsCreated => _vault != null && _valuesHandle.BufferID != 0u && _stateHandle.BufferID != 0u && _capacity > 0;
        public int Capacity => IsCreated ? _capacity : 0;
        public int Count => ReadCount();
        public long EstimatedBytes => IsCreated ? (long)_capacity * UnsafeUtility.SizeOf<T>() + (2L * UnsafeUtility.SizeOf<int>()) : 0L;

        public void Initialize(IDataVault vault, BufferID valuesBufferId, BufferID stateBufferId, int capacity, SystemID owner)
        {
            Dispose();
            _vault = vault;
            _owner = owner;
            _capacity = math.max(0, capacity);
            if (vault == null || _capacity <= 0)
                return;

            _valuesHandle = vault.EnsureGenerationHandle<T>(valuesBufferId, _capacity, owner, NativeArrayOptions.UninitializedMemory);
            _stateHandle = vault.EnsureGenerationHandle<int>(stateBufferId, 2, owner, NativeArrayOptions.ClearMemory);
            Clear();
        }

        public void Dispose()
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                if (_valuesHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _valuesHandle);
                if (_stateHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in _stateHandle);
            }

            this = default;
        }

        public bool Clear()
        {
            IDataVault vault = _vault;
            if (vault == null || _stateHandle.BufferID == 0u)
                return false;

            bool stateLocked = vault.TryAcquireWriteLock(in _stateHandle, _owner, out NativeArray<int> state);
            if (!stateLocked)
                return false;

            try
            {
                if (state.Length >= 2)
                {
                    state[0] = 0;
                    state[1] = 0;
                    return true;
                }

                return false;
            }
            finally
            {
                vault.ReleaseWriteLock(in _stateHandle, _owner);
            }
        }

        public bool Enqueue(T value)
        {
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _valuesHandle, out NativeArray<T> values) ||
                    !vault.TryResolveHandle(in _stateHandle, out NativeArray<int> state) ||
                    state.Length < 2)
                {
                    return false;
                }

                int head = math.clamp(state[0], 0, _capacity - 1);
                int count = math.clamp(state[1], 0, _capacity);
                if (count >= _capacity || count >= values.Length)
                    return false;

                int tail = (head + count) % _capacity;
                values[tail] = value;
                state[0] = head;
                state[1] = count + 1;
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        public bool TryDequeue(out T value)
        {
            value = default;
            IDataVault vault = _vault;
            if (!IsCreated || vault == null)
                return false;

            bool mutationGuarded = vault.TryAcquireMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            if (!mutationGuarded)
                return false;

            try
            {
                if (!vault.TryResolveHandle(in _valuesHandle, out NativeArray<T> values) ||
                    !vault.TryResolveHandle(in _stateHandle, out NativeArray<int> state) ||
                    state.Length < 2)
                {
                    return false;
                }

                int head = math.clamp(state[0], 0, _capacity - 1);
                int count = math.clamp(state[1], 0, _capacity);
                if (count <= 0 || head >= values.Length)
                    return false;

                value = values[head];
                values[head] = default;
                state[0] = (head + 1) % _capacity;
                state[1] = count - 1;
                return true;
            }
            finally
            {
                if (mutationGuarded)
                    vault.ReleaseMutationGuard(PersistentWorldVaultMutationGuards.CollectionMutationGuardMask);
            }
        }

        private int ReadCount()
        {
            if (_vault == null ||
                _stateHandle.BufferID == 0u ||
                !_vault.TryReadOnlyHandle(in _stateHandle, out NativeArray<int>.ReadOnly state) ||
                state.Length < 2)
            {
                return 0;
            }

            return math.clamp(state[1], 0, _capacity);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    internal static class WorldMemorySovereigntyValidator1325
    {
        private const uint FailureLayout = 1u;

        static WorldMemorySovereigntyValidator1325()
        {
            ValidateLayoutsOrThrow();
        }

        [UnityEditor.MenuItem("Hecton8/World/Run Memory Sovereignty Validator 1325")]
        public static void RunMenu()
        {
            ValidateLayoutsOrThrow();
            H8Debug.Log("[1325] Persistent world memory sovereignty layout validator passed.");
        }

        private static void ValidateLayoutsOrThrow()
        {
            uint failureFlags = 0u;

            AssertExplicit<AbsoluteUniversePosition>(48, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridX), 0, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridY), 8, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridZ), 16, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalX), 24, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalY), 28, ref failureFlags);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalZ), 32, ref failureFlags);

            AssertExplicit<AbsoluteUniversePositionBlit128>(48, ref failureFlags);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.GridX), 0, ref failureFlags);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.GridY), 8, ref failureFlags);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.GridZ), 16, ref failureFlags);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Local), 24, ref failureFlags);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Reserved), 40, ref failureFlags);

            AssertExplicit<PoolSlotData>(72, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.BoundGuid), 0, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.GridX), 8, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.GridY), 16, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.GridZ), 24, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.LocalOffset), 32, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.HydrationFrame), 44, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.RefCount), 46, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.StateFlags), 47, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.StableFrames), 48, ref failureFlags);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.LastVisibleFrame), 50, ref failureFlags);

            AssertExplicit<EntityDataRecord>(64, ref failureFlags);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Position), 0, ref failureFlags);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Quantity), 48, ref failureFlags);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Integrity01), 52, ref failureFlags);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InventoryHash), 56, ref failureFlags);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InstanceUid), 60, ref failureFlags);

            AssertExplicit<PersistentThermalVentRecord>(80, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.RuntimeKey), 0, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.PositionAup), 8, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.RadiusWS), 56, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.HeightWS), 60, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.UpdraftVelocity), 64, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.HeatIntensity), 68, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.SmokeDensity), 72, ref failureFlags);
            AssertOffset<PersistentThermalVentRecord>(nameof(PersistentThermalVentRecord.CableRadiusWS), 76, ref failureFlags);

            AssertExplicit<ResourceNodeTombstoneRecord>(80, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.TombstoneId), 0, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.Position), 8, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.ChunkId), 56, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.InstanceUid), 68, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.Reserved0), 72, ref failureFlags);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.Reserved1), 76, ref failureFlags);

            AssertExplicit<PersistentWorldItemRecord>(256, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.Position), 0, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentIdHash), 48, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentId), 56, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ChunkId), 184, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.Quantity), 196, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.InstanceUid), 200, ref failureFlags);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.Flags), 204, ref failureFlags);

            AssertExplicit<PersistentWorldDeltaRecord>(64, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.ItemPersistentIdHash), 0, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.ChunkId), 8, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.InstanceUid), 20, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.PackedLocalPosition), 24, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.Quantity), 28, ref failureFlags);
            AssertOffset<PersistentWorldDeltaRecord>(nameof(PersistentWorldDeltaRecord.ItemFlags), 30, ref failureFlags);

            AssertExplicit<PersistentWorldCompactDeltaRecord>(16, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.PackedLocalPosition), 0, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.InstanceUid), 4, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.Quantity), 8, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.ItemFlags), 10, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.ChunkIndex), 12, ref failureFlags);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.ItemHashIndex), 14, ref failureFlags);

            AssertExplicit<WorldTelemetryEntry>(64, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.Sequence), 0, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.BufferId), 8, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.Generation), 12, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.SystemId), 16, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.EventCode), 20, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.ActualLength), 24, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.ExpectedLength), 28, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.ChunkId), 32, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.Microseconds), 44, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.InstanceUid), 48, ref failureFlags);
            AssertOffset<WorldTelemetryEntry>(nameof(WorldTelemetryEntry.Flags), 52, ref failureFlags);

            AssertPagedSectorHashWindow(ref failureFlags);

            if (failureFlags != 0u)
                throw new FatalArchitectureException("1325 persistent world DTO layout violation.");
        }

        private static void AssertExplicit<T>(int expectedSize, ref uint failureFlags)
            where T : unmanaged
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int size = UnsafeUtility.SizeOf<T>();
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != expectedSize ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : unmanaged
        {
            int offset = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }

        private static void AssertPagedSectorHashWindow(ref uint failureFlags)
        {
            Type type = typeof(PersistentWorldRegistry).GetNestedType(
                "PagedSectorHashWindow",
                System.Reflection.BindingFlags.NonPublic);
            if (type == null)
            {
                failureFlags |= FailureLayout;
                return;
            }

            StructLayoutAttribute layout = type.StructLayoutAttribute;
            int size = Marshal.SizeOf(type);
            if (layout == null ||
                layout.Value != LayoutKind.Explicit ||
                size != 72 ||
                (size & 7) != 0)
            {
                failureFlags |= FailureLayout;
            }

            AssertOffset(type, "Hash0", 0, ref failureFlags);
            AssertOffset(type, "Hash1", 8, ref failureFlags);
            AssertOffset(type, "Hash2", 16, ref failureFlags);
            AssertOffset(type, "Hash3", 24, ref failureFlags);
            AssertOffset(type, "Hash4", 32, ref failureFlags);
            AssertOffset(type, "Hash5", 40, ref failureFlags);
            AssertOffset(type, "Hash6", 48, ref failureFlags);
            AssertOffset(type, "Hash7", 56, ref failureFlags);
            AssertOffset(type, "Hash8", 64, ref failureFlags);
        }

        private static void AssertOffset(Type type, string fieldName, int expectedOffset, ref uint failureFlags)
        {
            int offset = Marshal.OffsetOf(type, fieldName).ToInt32();
            if (offset != expectedOffset)
                failureFlags |= FailureLayout;
        }
    }
#endif

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5850)]
    public sealed class PersistentWorldRegistry : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, ISceneTransitionWorldResidencyBridge, IRuntimeWatchdogWorldHealthBridge, INutrientThermalVentReadModel, IFaunaPersistentWorldStateService, IPersistentDroppedItemRegistry, IGlobalRegistryHotSwapListener
    {
        private sealed class SectorOverrideState
        {
            public string TempPath;
            public string EntityStateTempPath;
            public float LastUnloadedTime;
            public bool IsResident;
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

        private struct SectorOverrideCommitWork
        {
            public long SectorHash;
            public string TempPath;
            public string EntityStateTempPath;
            public bool Committed;
            public bool EntityStateDeleted;
            public string Error;

            public SectorOverrideCommitWork(long sectorHash, string tempPath, string entityStateTempPath)
            {
                SectorHash = sectorHash;
                TempPath = tempPath;
                EntityStateTempPath = entityStateTempPath;
                Committed = false;
                EntityStateDeleted = false;
                Error = string.Empty;
            }
        }

        private readonly struct SectorOverrideReadWork
        {
            public readonly long SectorHash;
            public readonly string TempPath;
            public readonly string EntityStateTempPath;

            public SectorOverrideReadWork(long sectorHash, string tempPath, string entityStateTempPath)
            {
                SectorHash = sectorHash;
                TempPath = tempPath;
                EntityStateTempPath = entityStateTempPath;
            }
        }

        private readonly struct SectorOverrideReadWindow
        {
            public readonly int Count;
            private readonly SectorOverrideReadWork _work0;
            private readonly SectorOverrideReadWork _work1;
            private readonly SectorOverrideReadWork _work2;
            private readonly SectorOverrideReadWork _work3;
            private readonly SectorOverrideReadWork _work4;
            private readonly SectorOverrideReadWork _work5;
            private readonly SectorOverrideReadWork _work6;
            private readonly SectorOverrideReadWork _work7;
            private readonly SectorOverrideReadWork _work8;

            public SectorOverrideReadWindow(
                int count,
                in SectorOverrideReadWork work0,
                in SectorOverrideReadWork work1,
                in SectorOverrideReadWork work2,
                in SectorOverrideReadWork work3,
                in SectorOverrideReadWork work4,
                in SectorOverrideReadWork work5,
                in SectorOverrideReadWork work6,
                in SectorOverrideReadWork work7,
                in SectorOverrideReadWork work8)
            {
                Count = count;
                _work0 = work0;
                _work1 = work1;
                _work2 = work2;
                _work3 = work3;
                _work4 = work4;
                _work5 = work5;
                _work6 = work6;
                _work7 = work7;
                _work8 = work8;
            }

            public SectorOverrideReadWork this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0:
                            return _work0;
                        case 1:
                            return _work1;
                        case 2:
                            return _work2;
                        case 3:
                            return _work3;
                        case 4:
                            return _work4;
                        case 5:
                            return _work5;
                        case 6:
                            return _work6;
                        case 7:
                            return _work7;
                        case 8:
                            return _work8;
                        default:
                            return default;
                    }
                }
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
        private const int PagedSectorWindowWidth = 3;
        private const int PagedSectorHashCount = PagedSectorWindowWidth * PagedSectorWindowWidth;
        private const int PagedSectorEdgeLengthMeters = 1000;
        private const long InvalidPagedSectorHash = long.MinValue;
        private const float PagedSectorLoadRadiusMeters = 1000f;
        private const float PagedSectorLoadRadiusSq = PagedSectorLoadRadiusMeters * PagedSectorLoadRadiusMeters;
        private const float SectorEvictionDistanceMeters = 1500f;
        private const int MaxSectorOverrideCommitsPerPass = 16;
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
        private const long PersistentMemoryBudgetBytes = 67108864L;
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
        private const Allocator DataVaultExemptPersistentRecordAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptPersistentDeltaAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptPersistentTombstoneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptPersistentHydrationAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptPersistentStateAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptPersistentQueueAllocator = Allocator.Persistent;
        private const SystemID WorldRegistryVaultOwner = SystemID.WorldStreaming;
        private const BufferID WorldRegistryRecordsBuffer = BufferID.VegetationSurfaceAggregateBackSemanticTypes;
        private const BufferID WorldRegistryRecordsCountBuffer = BufferID.VegetationSurfaceAggregateBackBiomeLayers;
        private const BufferID WorldRegistryRecordsByChunkKeysBuffer = BufferID.VegetationSurfaceAggregateBackFlowDirections;
        private const BufferID WorldRegistryRecordsByChunkValuesBuffer = BufferID.VegetationSurfaceAggregateBackFlowVectors;
        private const BufferID WorldRegistryRecordsByChunkCountBuffer = BufferID.VegetationUnderwaterAggregateFrontMatrices;
        private const BufferID WorldRegistryDeltaRecordsBuffer = BufferID.VegetationUnderwaterAggregateFrontMetadata;
        private const BufferID WorldRegistryDeltaRecordsCountBuffer = BufferID.VegetationUnderwaterAggregateFrontTypes;
        private const BufferID WorldRegistryDeltaRecordIndexKeysBuffer = BufferID.VegetationUnderwaterAggregateFrontSemanticTypes;
        private const BufferID WorldRegistryDeltaRecordIndexValuesBuffer = BufferID.VegetationUnderwaterAggregateFrontBiomeLayers;
        private const BufferID WorldRegistryDeltaRecordIndexStatesBuffer = BufferID.VegetationUnderwaterAggregateFrontFlowDirections;
        private const BufferID WorldRegistryDeltaRecordIndexCountBuffer = BufferID.VegetationUnderwaterAggregateFrontFlowVectors;
        private const BufferID WorldRegistryDeletedInstanceKeysBuffer = BufferID.VegetationUnderwaterAggregateBackMatrices;
        private const BufferID WorldRegistryDeletedInstanceValuesBuffer = BufferID.VegetationUnderwaterAggregateBackMetadata;
        private const BufferID WorldRegistryDeletedInstanceStatesBuffer = BufferID.VegetationUnderwaterAggregateBackTypes;
        private const BufferID WorldRegistryDeletedInstanceCountBuffer = BufferID.VegetationUnderwaterAggregateBackSemanticTypes;
        private const BufferID WorldRegistryResourceTombstoneKeysBuffer = BufferID.VegetationUnderwaterAggregateBackBiomeLayers;
        private const BufferID WorldRegistryResourceTombstoneValuesBuffer = BufferID.VegetationUnderwaterAggregateBackFlowDirections;
        private const BufferID WorldRegistryResourceTombstoneStatesBuffer = BufferID.VegetationUnderwaterAggregateBackFlowVectors;
        private const BufferID WorldRegistryResourceTombstoneCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistryResourceTombstoneCountBuffer;
        private const BufferID WorldRegistryResourceMetamorphosedKeysBuffer = BufferID.PersistentWorldRegistry_WorldRegistryResourceMetamorphosedKeysBuffer;
        private const BufferID WorldRegistryResourceMetamorphosedValuesBuffer = BufferID.PersistentWorldRegistry_WorldRegistryResourceMetamorphosedValuesBuffer;
        private const BufferID WorldRegistryResourceMetamorphosedStatesBuffer = BufferID.PersistentWorldRegistry_WorldRegistryResourceMetamorphosedStatesBuffer;
        private const BufferID WorldRegistryResourceMetamorphosedCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistryResourceMetamorphosedCountBuffer;
        private const BufferID WorldRegistryDeltaChunkIndexKeysBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIndexKeysBuffer;
        private const BufferID WorldRegistryDeltaChunkIndexValuesBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIndexValuesBuffer;
        private const BufferID WorldRegistryDeltaChunkIndexStatesBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIndexStatesBuffer;
        private const BufferID WorldRegistryDeltaChunkIndexCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIndexCountBuffer;
        private const BufferID WorldRegistryDeltaChunkIdsBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIdsBuffer;
        private const BufferID WorldRegistryDeltaChunkIdsCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaChunkIdsCountBuffer;
        private const BufferID WorldRegistryDeltaItemIndexKeysBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDeltaItemIndexKeysBuffer;
        private const BufferID WorldRegistryDeltaItemIndexValuesBuffer = BufferID.VegetationSurfaceChunkPoolMatrices;
        private const BufferID WorldRegistryDeltaItemIndexStatesBuffer = BufferID.VegetationSurfaceChunkPoolMetadata;
        private const BufferID WorldRegistryDeltaItemIndexCountBuffer = BufferID.VegetationSurfaceChunkPoolTypes;
        private const BufferID WorldRegistryDeltaItemHashesBuffer = BufferID.VegetationSurfaceChunkPoolSemanticTypes;
        private const BufferID WorldRegistryDeltaItemHashesCountBuffer = BufferID.VegetationSurfaceChunkPoolBiomeLayers;
        private const BufferID WorldRegistryDeltaRecordsByChunkKeysBuffer = BufferID.VegetationSurfaceChunkPoolEdgeDistances;
        private const BufferID WorldRegistryDeltaRecordsByChunkValuesBuffer = BufferID.VegetationSurfaceChunkPoolFlowDirections;
        private const BufferID WorldRegistryDeltaRecordsByChunkCountBuffer = BufferID.VegetationSurfaceChunkPoolFlowVectors;
        private const BufferID WorldRegistryTombstoneDecayIndicesBuffer = BufferID.VegetationUnderwaterChunkPoolMatrices;
        private const BufferID WorldRegistryTombstoneDecayIndicesCountBuffer = BufferID.VegetationUnderwaterChunkPoolMetadata;
        private const BufferID WorldRegistrySaveSnapshotDeltasBuffer = BufferID.VegetationUnderwaterChunkPoolTypes;
        private const BufferID WorldRegistrySaveSnapshotDeltasCountBuffer = BufferID.VegetationUnderwaterChunkPoolSemanticTypes;
        private const BufferID WorldRegistryPoolSlotDataBuffer = BufferID.VegetationUnderwaterChunkPoolBiomeLayers;
        private const BufferID WorldRegistryGuidToPoolIndexKeysBuffer = BufferID.VegetationUnderwaterChunkPoolEdgeDistances;
        private const BufferID WorldRegistryGuidToPoolIndexValuesBuffer = BufferID.VegetationUnderwaterChunkPoolFlowDirections;
        private const BufferID WorldRegistryGuidToPoolIndexStatesBuffer = BufferID.VegetationUnderwaterChunkPoolFlowVectors;
        private const BufferID WorldRegistryGuidToPoolIndexCountBuffer = BufferID.VegetationSurfaceDefragScratchMatrices;
        private const BufferID WorldRegistryEntityStateKeysBuffer = BufferID.VegetationSurfaceDefragScratchMetadata;
        private const BufferID WorldRegistryEntityStateValuesBuffer = BufferID.VegetationSurfaceDefragScratchTypes;
        private const BufferID WorldRegistryEntityStateStatesBuffer = BufferID.VegetationSurfaceDefragScratchSemanticTypes;
        private const BufferID WorldRegistryEntityStateCountBuffer = BufferID.VegetationSurfaceDefragScratchBiomeLayers;
        private const BufferID WorldRegistryFloraSpawnStateKeysBuffer = BufferID.VegetationSurfaceDefragScratchEdgeDistances;
        private const BufferID WorldRegistryFloraSpawnStateValuesBuffer = BufferID.VegetationSurfaceDefragScratchFlowDirections;
        private const BufferID WorldRegistryFloraSpawnStateStatesBuffer = BufferID.VegetationSurfaceDefragScratchFlowVectors;
        private const BufferID WorldRegistryFloraSpawnStateCountBuffer = BufferID.VegetationUnderwaterDefragScratchMatrices;
        private const BufferID WorldRegistrySpawnImpulseKeysBuffer = BufferID.VegetationUnderwaterDefragScratchMetadata;
        private const BufferID WorldRegistrySpawnImpulseValuesBuffer = BufferID.VegetationUnderwaterDefragScratchTypes;
        private const BufferID WorldRegistrySpawnImpulseStatesBuffer = BufferID.VegetationUnderwaterDefragScratchSemanticTypes;
        private const BufferID WorldRegistrySpawnImpulseCountBuffer = BufferID.VegetationUnderwaterDefragScratchBiomeLayers;
        private const BufferID WorldRegistrySpawnVelocityKeysBuffer = BufferID.VegetationUnderwaterDefragScratchEdgeDistances;
        private const BufferID WorldRegistrySpawnVelocityValuesBuffer = BufferID.VegetationUnderwaterDefragScratchFlowDirections;
        private const BufferID WorldRegistrySpawnVelocityStatesBuffer = BufferID.VegetationUnderwaterDefragScratchFlowVectors;
        private const BufferID WorldRegistrySpawnVelocityCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistrySpawnVelocityCountBuffer;
        private const BufferID WorldRegistryDehydrateQueueValuesBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDehydrateQueueValuesBuffer;
        private const BufferID WorldRegistryDehydrateQueueStateBuffer = BufferID.PersistentWorldRegistry_WorldRegistryDehydrateQueueStateBuffer;
        private const BufferID WorldRegistryPendingHydrationRecordsBuffer = BufferID.PersistentWorldRegistry_WorldRegistryPendingHydrationRecordsBuffer;
        private const BufferID WorldRegistryPendingHydrationRecordsCountBuffer = BufferID.PersistentWorldRegistry_WorldRegistryPendingHydrationRecordsCountBuffer;
        private const BufferID WorldRegistryTelemetryRingBuffer = BufferID.PersistentWorldRegistry_WorldRegistryTelemetryRingBuffer;
        private const BufferID WorldRegistryTelemetryCursorBuffer = BufferID.PersistentWorldRegistry_WorldRegistryTelemetryCursorBuffer;
        private const int TombstoneDecayThreshold = 1024;
        private const int TombstoneTimeToLiveDays = 3;
        private const double TombstoneInGameDaySeconds = 86400d;
        private const float TombstoneDecayFrostTickSeconds = 5f;
        private const float WorldClockMaxSeconds = 16777215f;
        private const int MaxTombstoneDecayAppliesPerLateFrame = 128;
        private const int WorldTelemetryRingLength = 300;
        private const uint WorldTelemetryResolveSuccess = 1u;
        private const uint WorldTelemetryReadFailure = 2u;
        private const uint WorldTelemetryWriteLockContention = 3u;
        private const uint WorldTelemetryStaleGeneration = 4u;
        private const uint WorldTelemetryCapacityMismatch = 5u;
        private const uint WorldTelemetryInvalidAup = 6u;
        private const uint WorldTelemetryTombstoneDecaySkipped = 7u;

        // Save-snapshot capture failure discriminators.
        //
        // CaptureSaveSnapshot() has four distinct false-return branches. They previously collapsed into
        // one SaveManager reason string ("Persistent world save snapshot capture failed."), so a total
        // loss of player progress could only be attributed by byte-decoding the UTF-16 payload of the
        // slot_N.diag sidecar - and even then the string named none of the four.
        //
        // Two of the branches emitted no telemetry at all, and SaveSnapshotFailureStorageNotCreated
        // structurally CANNOT emit any: _worldTelemetryRing is allocated by InitializeVaultBackedStorage,
        // the very method that did not run, so WriteWorldTelemetry() no-ops at its IsCreated guard.
        // This managed byte is the only failure signal that escapes that branch.
        internal const byte SaveSnapshotFailureNone = 0;
        internal const byte SaveSnapshotFailureStorageNotCreated = 1;
        internal const byte SaveSnapshotFailureTombstoneStaging = 2;
        internal const byte SaveSnapshotFailureSnapshotClear = 3;
        internal const byte SaveSnapshotFailureCapacityOverflow = 4;

        [StructLayout(LayoutKind.Explicit, Size = 72)]
        private struct PagedSectorHashWindow
        {
            [FieldOffset(0)]
            public long Hash0;
            [FieldOffset(8)]
            public long Hash1;
            [FieldOffset(16)]
            public long Hash2;
            [FieldOffset(24)]
            public long Hash3;
            [FieldOffset(32)]
            public long Hash4;
            [FieldOffset(40)]
            public long Hash5;
            [FieldOffset(48)]
            public long Hash6;
            [FieldOffset(56)]
            public long Hash7;
            [FieldOffset(64)]
            public long Hash8;

            public long this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return index switch
                    {
                        0 => Hash0,
                        1 => Hash1,
                        2 => Hash2,
                        3 => Hash3,
                        4 => Hash4,
                        5 => Hash5,
                        6 => Hash6,
                        7 => Hash7,
                        8 => Hash8,
                        _ => InvalidPagedSectorHash
                    };
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    switch (index)
                    {
                        case 0:
                            Hash0 = value;
                            break;
                        case 1:
                            Hash1 = value;
                            break;
                        case 2:
                            Hash2 = value;
                            break;
                        case 3:
                            Hash3 = value;
                            break;
                        case 4:
                            Hash4 = value;
                            break;
                        case 5:
                            Hash5 = value;
                            break;
                        case 6:
                            Hash6 = value;
                            break;
                        case 7:
                            Hash7 = value;
                            break;
                        case 8:
                            Hash8 = value;
                            break;
                    }
                }
            }
        }

        private static readonly int3 ApexFaunaTombstoneChunkId = new int3(int.MinValue, 0, 0);
        private static readonly long HydrationFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks;
        private static readonly long HydrationPerformanceWarningBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 5000L);
        private static readonly uint _hydrationApplyBudgetWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.HydrationApplyBudget"));
        private static readonly uint _hydrationApplyContextHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.HydrationApply"));
        private static readonly uint _sectorEntityStateThrottleWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.EntityStateCompressionThrottle"));
        private static readonly uint _sectorEntityStateQueueOverflowWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.EntityStateCompressionQueueOverflow"));
        private static readonly uint _sectorCorruptionNotificationMissWarningHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.SectorCorruptionNotificationMiss"));
        private static readonly uint _sectorCorruptionNotificationContextHash = unchecked((uint)LocHash.Compute("PersistentWorldRegistry.SectorCorruptionNotification"));
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
        private int _sectorCorruptionNotificationMissCount;
        private PersistentThermalVentRecord[] _activeThermalVents;
        private int _activeThermalVentCount;
        private int _activeThermalVentRevision;

        internal int ChunkSizeMeters => chunkSizeMeters;
        public int ActiveThermalVentCount => _activeThermalVentCount;
        public int ActiveThermalVentRevision => _activeThermalVentRevision;
        public int SectorCorruptionNotificationMissCount => _sectorCorruptionNotificationMissCount;

        /// <summary>
        /// Reads the active thermal vent count through the nutrient-facing owner interface.
        /// </summary>
        public int ReadActiveNutrientThermalVentCount()
        {
            return _activeThermalVentCount;
        }

        /// <summary>
        /// Reads the active thermal vent revision through the nutrient-facing owner interface.
        /// </summary>
        public int ReadActiveNutrientThermalVentRevision()
        {
            return _activeThermalVentRevision;
        }

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

        private IDataVault _dataVault;
        private VaultBackedList<PersistentWorldItemRecord> _records;
        private VaultBackedMultiHashMap<int3, int> _recordsByChunk;
        private VaultBackedList<PersistentWorldCompactDeltaRecord> _deltaRecords;
        private VaultBackedHashMap<uint, int> _deltaRecordIndexByEntityId;
        private VaultBackedHashSet<uint> _deletedInstanceUids;
        private VaultBackedHashSet<ulong> _resourceNodeTombstoneIds;
        private VaultBackedHashSet<ulong> _resourceNodeMetamorphosedIds;
        private VaultBackedHashMap<int3, ushort> _deltaChunkIndexByChunkId;
        private VaultBackedList<int3> _deltaChunkIds;
        private VaultBackedHashMap<ulong, ushort> _deltaItemIndexByHash;
        private VaultBackedList<ulong> _deltaItemHashes;
        private VaultBackedMultiHashMap<uint, PersistentWorldCompactDeltaRecord> _deltaRecordsByChunk;
        private VaultBackedList<int> _tombstoneDecayExpiredIndices;
        private VaultBackedList<PersistentWorldDeltaRecord> _saveSnapshotDeltas;
        private byte _lastSaveSnapshotFailureCode;
        private VaultBackedArray<PoolSlotData> _poolSlotData;
        private VaultBackedHashMap<ulong, int> _guidToPoolIndex;
        private VaultBackedHashMap<uint, EntityDataRecord> _entityStateByInstanceUid;
        private VaultBackedHashMap<uint, EntityDataRecord> _floraSpawnStateByInstanceUid;
        private VaultBackedHashMap<uint, float3> _spawnImpulseByInstanceUid;
        private VaultBackedHashMap<uint, float3> _spawnVelocityChangeByInstanceUid;
        private VaultBackedQueue<int> _dehydrateQueue;
        private VaultBackedList<int> _pendingHydrationRecords;
        private VaultBackedArray<WorldTelemetryEntry> _worldTelemetryRing;
        private VaultBackedArray<int> _worldTelemetryCursor;
        private GameObject[] _hydratedInstancesBySlot;
        private Transform[] _poolSlotTransforms;
        private Rigidbody[] _poolSlotRigidbodies;
        private PickupItem[] _poolSlotPickupItems;
        private HectonItem[] _poolSlotHectonItems;
        private Dictionary<int, GameObject> _hydratedInstancesByRecordIndex;
        private Dictionary<ulong, ItemData> _itemLookupByHash;
        private List<ItemData> _itemCatalogScratch;
        private List<int> _worldPrefabPrewarmHashScratch;
        private List<int> _worldPrefabReleaseScratch;
        private List<int> _recordIndexScratch;
        private List<EntityDataRecord> _entityStateScratch;
        private List<EntityDataRecord> _entityStateSectorTargetScratch;
        private List<EntityDataRecord> _floraSpawnStateScratch;
        private SectorOverrideCommitWork[] _sectorOverrideCommitWorkBuffer;
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
        private bool _hotSwapRegistered;
        private bool _hydrationSessionRunning;
        private bool _hydrationSessionStartPending;
        private bool _playerChunkValid;
        private bool _hasLastHydrationScanAup;
        private ushort _hydrationFrameCounter;
        private int _pendingHydrationReadIndex;
        private int _hydrationSessionVersion;
        private int3 _currentPlayerChunk;
        private int2 _currentPlayerSector;
        private int2 _pendingIndexedSectorPagingCenter;
        private AbsoluteUniversePosition _lastHydrationScanAup;
        private bool _indexedSectorPagingEnabled;
        private bool _indexedSectorPagingInFlight;
        private bool _indexedSectorPagingStartPending;
        private int _indexedSectorAsyncGeneration;
        private int _indexedSectorPagingInFlightGeneration;
        private int _sectorOverrideCommitInFlightGeneration;
        private bool _playerSectorValid;
        private bool _sectorOverrideCommitInFlight;
        private float _nextSectorOverrideCommitTime;
        private float _worldClockSeconds;
        private string _indexedSectorSavePath;
        private string _indexedSectorOverrideDirectory;
        private List<SaveBinaryStorage.IndexedSectorEntryInfo> _indexedSectorDirectory;
        private Dictionary<long, SectorOverrideState> _sectorOverrideStates;
        private HashSet<int> _residentWorldPrefabHashes;
        private float _nextTombstoneDecaySweepTime;
        private int _tombstoneDecayApplyCursor;
        private int _tombstoneDecayCurrentDay;
        private bool _tombstoneDecayApplyPending;
        private ISaveService _saveService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerInventoryService _playerInventoryService;
        private IPhysicsService _physicsService;
        private IObjectPoolService _objectPoolService;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private ulong _worldTelemetrySequence;
        private int _worldTelemetryDumpQueued;
        private object _worldTelemetryDumpSnapshotLock;
        private WorldTelemetryEntry[] _worldTelemetryDumpSnapshot;
        private int _worldTelemetryDumpSnapshotCount;
        private static PersistentWorldRegistry s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _nextInstanceUidCounter = 0;
            s_activeRuntimeInstance = null;
        }

        public static PersistentWorldRegistry Instance => s_activeRuntimeInstance;

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
            AbsoluteUniversePosition invalidAup = AbsoluteUniversePosition.Invalid();
            AbsoluteUniversePosition safePosition = AbsoluteUniversePosition.Sanitize(in position, in invalidAup);
            if (!safePosition.IsFinite())
                return true;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return true;

            float3 runtimeFloat = AUPMath.ResolveCameraRelative(in safePosition, in originAup);
            if (!math.all(math.isfinite(runtimeFloat)))
                return true;

            Vector3 runtimePosition = new Vector3(runtimeFloat.x, runtimeFloat.y, runtimeFloat.z);
            if (IsInsideActiveModuleInterior(runtimePosition))
                return true;

            PersistentWorldRegistry registry = s_activeRuntimeInstance;
            ISubmarineRuntimeContext submarine = registry != null
                ? registry._submarineRuntimeContext
                : GlobalRegistry.Submarine;
            if (submarine != null && IsInsideSubmarineFallbackBounds(submarine, runtimePosition))
                return true;

            if (registry == null ||
                !registry.TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                return false;
            }

            return AbsoluteUniversePosition.DistanceSq(in position, in playerAup) <= ModCoreProtectionRadiusSq;
        }

        private bool TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            AbsoluteUniversePosition invalidAup = AbsoluteUniversePosition.Invalid();

            IPlayerRuntimeContext player = _playerRuntimeContext;
            if (player == null)
                return false;

            if (player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                AbsoluteUniversePosition snapshotAup = snapshot.Aup;
                playerAup = AbsoluteUniversePosition.Sanitize(in snapshotAup, in invalidAup);
                return playerAup.IsFinite();
            }

            if (player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                AbsoluteUniversePosition movementAup = movementState.PredictedAup;
                playerAup = AbsoluteUniversePosition.Sanitize(in movementAup, in invalidAup);
                return playerAup.IsFinite();
            }

            return false;
        }

        public bool AreResidentWorldPrefabPoolsReady()
        {
#if UNITY_EDITOR
            // Bypass prewarm in editor because Addressables are unbuilt/empty.
            //
            // This is a genuine editor/player behaviour split, not merely a warning: in the editor
            // this method never inspects pool state at all, so a missing or unprewarmed resident
            // world prefab pool cannot fail here and will only ever surface in a player build. The
            // bypass itself is kept (Addressables really are unbuilt in-editor), but the remaining
            // body is now #else rather than compiled-and-unreachable, so the split is explicit.
            return true;
#else
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

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;

            HashSet<int>.Enumerator residentEnumerator = _residentWorldPrefabHashes.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                int itemHashId = residentEnumerator.Current;
                if (itemHashId == 0)
                    continue;

                if (!_resolvedItemCatalog.PollLoadedWorldPrefab(itemHashId, out GameObject prefab) ||
                    prefab == null ||
                    !pool.HasPool(prefab))
                {
                    residentEnumerator.Dispose();
                    return false;
                }
            }

            residentEnumerator.Dispose();
            return true;
#endif
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (!_serviceRegistered)
                return;

            maxTrackedItems = math.max(256, maxTrackedItems);
            chunkSizeMeters = math.max(16, chunkSizeMeters);
            hydrationRadiusInChunks = math.clamp(hydrationRadiusInChunks, 0, 2);
            _hydrationFrameCounter = 0;
            _worldClockSeconds = 0f;
            Volatile.Write(ref _indexedSectorAsyncGeneration, 1);

            _dataVault = GlobalRegistry.DataVault;
            InitializeVaultBackedStorage(_dataVault, maxTrackedItems);
            // COLD ALLOC: GameObject[maxTrackedItems] â€” hydrated proxy instances by slot â€” owner: PersistentWorldRegistry
            _hydratedInstancesBySlot = new GameObject[maxTrackedItems];
            // COLD ALLOC: Transform[maxTrackedItems] â€” hydrated proxy transforms by slot â€” owner: PersistentWorldRegistry
            _poolSlotTransforms = new Transform[maxTrackedItems];
            // COLD ALLOC: Rigidbody[maxTrackedItems] â€” hydrated proxy rigidbodies by slot â€” owner: PersistentWorldRegistry
            _poolSlotRigidbodies = new Rigidbody[maxTrackedItems];
            // COLD ALLOC: PickupItem[maxTrackedItems] - hydrated proxy pickup components by slot - owner: PersistentWorldRegistry
            _poolSlotPickupItems = new PickupItem[maxTrackedItems];
            // COLD ALLOC: HectonItem[maxTrackedItems] - hydrated proxy item components by slot - owner: PersistentWorldRegistry
            _poolSlotHectonItems = new HectonItem[maxTrackedItems];
            // COLD ALLOC: Dictionary<int,GameObject>[maxTrackedItems] - hydrated world-item proxy lookup - owner: PersistentWorldRegistry
            _hydratedInstancesByRecordIndex = new Dictionary<int, GameObject>(maxTrackedItems);
            // COLD ALLOC: Dictionary<ulong,ItemData>[1024] â€” persistent-id hash to ItemData lookup cache â€” owner: PersistentWorldRegistry
            _itemLookupByHash = new Dictionary<ulong, ItemData>(1024);
            // COLD ALLOC: List<ItemData>[1024] â€” item catalog scratch buffer for hash cache rebuilds â€” owner: PersistentWorldRegistry
            _itemCatalogScratch = new List<ItemData>(1024);
            // COLD ALLOC: List<int>[256] Ã¢â‚¬â€ unique addressable prefab prewarm hash buffer for paged sector hydration Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _worldPrefabPrewarmHashScratch = new List<int>(256);
            // COLD ALLOC: List<int>[256] Ã¢â‚¬â€ deferred addressable prefab release scratch buffer for paged sector eviction Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _worldPrefabReleaseScratch = new List<int>(256);
            // COLD ALLOC: List<int>[maxTrackedItems] - hydration/dehydration scratch pre-sized to prevent hot-path growth - owner: PersistentWorldRegistry
            _recordIndexScratch = new List<int>(maxTrackedItems);
            // COLD ALLOC: List<EntityDataRecord>[128] â€” sector entity-state rewrite scratch buffer for MMF fauna hibernation pages â€” owner: PersistentWorldRegistry
            _entityStateScratch = new List<EntityDataRecord>(128);
            // COLD ALLOC: List<EntityDataRecord>[128] — destination-sector entity-state rewrite scratch — owner: PersistentWorldRegistry
            _entityStateSectorTargetScratch = new List<EntityDataRecord>(128);
            _floraSpawnStateScratch = new List<EntityDataRecord>(maxTrackedItems); // COLD ALLOC: List<EntityDataRecord>[maxTrackedItems] - standalone flora spawn-state snapshot scratch - owner: PersistentWorldRegistry
            // COLD ALLOC: SectorOverrideCommitWork[16] - bounded due sector override commit queue - owner: PersistentWorldRegistry
            _sectorOverrideCommitWorkBuffer = new SectorOverrideCommitWork[MaxSectorOverrideCommitsPerPass];
            // COLD ALLOC: uint[64] — active whale-fall POI uid index for bounded influence queries — owner: PersistentWorldRegistry
            _whaleFallPoiInstanceUids = new uint[MaxWhaleFallInfluenceScan];
            // COLD ALLOC: uint[256] — per-pass apex migration uid de-duplication scratch — owner: PersistentWorldRegistry
            _apexMigrationVisitedUids = new uint[MaxApexMigrationVisitedUids];
            // COLD ALLOC: PersistentThermalVentRecord[16] - active hydrothermal vent snapshot for thermodynamics - owner: PersistentWorldRegistry
            _activeThermalVents = new PersistentThermalVentRecord[MaxPersistentThermalVentRecords];
            // COLD ALLOC: WorldTelemetryEntry[300] - owner-phase fault snapshot retained in memory - owner: PersistentWorldRegistry
            _worldTelemetryDumpSnapshot = new WorldTelemetryEntry[WorldTelemetryRingLength];
            // COLD ALLOC: object - pre-owned telemetry snapshot monitor - owner: PersistentWorldRegistry
            _worldTelemetryDumpSnapshotLock = new object();
            // COLD ALLOC: List<IndexedSectorEntryInfo>[256] Ã¢â‚¬â€ cached v8 sector directory entries for paged restore Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);
            // COLD ALLOC: Dictionary<long,SectorOverrideState>[32] Ã¢â‚¬â€ paged sector temp-override residency map Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _sectorOverrideStates = new Dictionary<long, SectorOverrideState>(32);
            // COLD ALLOC: HashSet<int>[256] Ã¢â‚¬â€ resident addressable world-prefab hash residency set Ã¢â‚¬â€ owner: PersistentWorldRegistry
            _residentWorldPrefabHashes = new HashSet<int>(256);
            RegisterNativeMemorySentinelAllocations();
            RegisterPersistentMemoryBudget();
            InitializeWorldTelemetryDumpWorkerCold();

            UpdateDiagnostics();
        }

        private void InitializeVaultBackedStorage(IDataVault dataVault, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            _records.Initialize(dataVault, WorldRegistryRecordsBuffer, WorldRegistryRecordsCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _recordsByChunk.Initialize(dataVault, WorldRegistryRecordsByChunkKeysBuffer, WorldRegistryRecordsByChunkValuesBuffer, WorldRegistryRecordsByChunkCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _deltaRecords.Initialize(dataVault, WorldRegistryDeltaRecordsBuffer, WorldRegistryDeltaRecordsCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _deltaRecordIndexByEntityId.Initialize(dataVault, WorldRegistryDeltaRecordIndexKeysBuffer, WorldRegistryDeltaRecordIndexValuesBuffer, WorldRegistryDeltaRecordIndexStatesBuffer, WorldRegistryDeltaRecordIndexCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _deletedInstanceUids.Initialize(dataVault, WorldRegistryDeletedInstanceKeysBuffer, WorldRegistryDeletedInstanceValuesBuffer, WorldRegistryDeletedInstanceStatesBuffer, WorldRegistryDeletedInstanceCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _resourceNodeTombstoneIds.Initialize(dataVault, WorldRegistryResourceTombstoneKeysBuffer, WorldRegistryResourceTombstoneValuesBuffer, WorldRegistryResourceTombstoneStatesBuffer, WorldRegistryResourceTombstoneCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _resourceNodeMetamorphosedIds.Initialize(dataVault, WorldRegistryResourceMetamorphosedKeysBuffer, WorldRegistryResourceMetamorphosedValuesBuffer, WorldRegistryResourceMetamorphosedStatesBuffer, WorldRegistryResourceMetamorphosedCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _deltaChunkIndexByChunkId.Initialize(dataVault, WorldRegistryDeltaChunkIndexKeysBuffer, WorldRegistryDeltaChunkIndexValuesBuffer, WorldRegistryDeltaChunkIndexStatesBuffer, WorldRegistryDeltaChunkIndexCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _deltaChunkIds.Initialize(dataVault, WorldRegistryDeltaChunkIdsBuffer, WorldRegistryDeltaChunkIdsCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _deltaItemIndexByHash.Initialize(dataVault, WorldRegistryDeltaItemIndexKeysBuffer, WorldRegistryDeltaItemIndexValuesBuffer, WorldRegistryDeltaItemIndexStatesBuffer, WorldRegistryDeltaItemIndexCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _deltaItemHashes.Initialize(dataVault, WorldRegistryDeltaItemHashesBuffer, WorldRegistryDeltaItemHashesCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _deltaRecordsByChunk.Initialize(dataVault, WorldRegistryDeltaRecordsByChunkKeysBuffer, WorldRegistryDeltaRecordsByChunkValuesBuffer, WorldRegistryDeltaRecordsByChunkCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _tombstoneDecayExpiredIndices.Initialize(dataVault, WorldRegistryTombstoneDecayIndicesBuffer, WorldRegistryTombstoneDecayIndicesCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _saveSnapshotDeltas.Initialize(dataVault, WorldRegistrySaveSnapshotDeltasBuffer, WorldRegistrySaveSnapshotDeltasCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _poolSlotData.Initialize(dataVault, WorldRegistryPoolSlotDataBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.ClearMemory);
            _guidToPoolIndex.Initialize(dataVault, WorldRegistryGuidToPoolIndexKeysBuffer, WorldRegistryGuidToPoolIndexValuesBuffer, WorldRegistryGuidToPoolIndexStatesBuffer, WorldRegistryGuidToPoolIndexCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _entityStateByInstanceUid.Initialize(dataVault, WorldRegistryEntityStateKeysBuffer, WorldRegistryEntityStateValuesBuffer, WorldRegistryEntityStateStatesBuffer, WorldRegistryEntityStateCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _floraSpawnStateByInstanceUid.Initialize(dataVault, WorldRegistryFloraSpawnStateKeysBuffer, WorldRegistryFloraSpawnStateValuesBuffer, WorldRegistryFloraSpawnStateStatesBuffer, WorldRegistryFloraSpawnStateCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _spawnImpulseByInstanceUid.Initialize(dataVault, WorldRegistrySpawnImpulseKeysBuffer, WorldRegistrySpawnImpulseValuesBuffer, WorldRegistrySpawnImpulseStatesBuffer, WorldRegistrySpawnImpulseCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _spawnVelocityChangeByInstanceUid.Initialize(dataVault, WorldRegistrySpawnVelocityKeysBuffer, WorldRegistrySpawnVelocityValuesBuffer, WorldRegistrySpawnVelocityStatesBuffer, WorldRegistrySpawnVelocityCountBuffer, safeCapacity, WorldRegistryVaultOwner);
            _dehydrateQueue.Initialize(dataVault, WorldRegistryDehydrateQueueValuesBuffer, WorldRegistryDehydrateQueueStateBuffer, safeCapacity, WorldRegistryVaultOwner);
            _pendingHydrationRecords.Initialize(dataVault, WorldRegistryPendingHydrationRecordsBuffer, WorldRegistryPendingHydrationRecordsCountBuffer, safeCapacity, WorldRegistryVaultOwner, NativeArrayOptions.UninitializedMemory);
            _worldTelemetryRing.Initialize(dataVault, WorldRegistryTelemetryRingBuffer, WorldTelemetryRingLength, WorldRegistryVaultOwner, NativeArrayOptions.ClearMemory);
            _worldTelemetryCursor.Initialize(dataVault, WorldRegistryTelemetryCursorBuffer, 1, WorldRegistryVaultOwner, NativeArrayOptions.ClearMemory);
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (!_serviceRegistered)
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntimeLoops();
        }

        private void Start()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

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

        /// <summary>
        /// Copies one active thermal vent row into a nutrient-specific unmanaged DTO.
        /// </summary>
        public bool TryGetActiveNutrientThermalVent(int index, out NutrientThermalVentSnapshotDTO record)
        {
            record = default;
            if (!TryGetActiveThermalVent(index, out PersistentThermalVentRecord source))
                return false;

            record = new NutrientThermalVentSnapshotDTO
            {
                RuntimeKey = source.RuntimeKey,
                PositionAup = source.PositionAup,
                RadiusWS = source.RadiusWS,
                HeightWS = source.HeightWS,
                UpdraftVelocity = source.UpdraftVelocity,
                HeatIntensity = source.HeatIntensity,
                SmokeDensity = source.SmokeDensity,
                CableRadiusWS = source.CableRadiusWS
            };
            return true;
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
            InvalidateIndexedSectorAsyncOperations();
            CompleteTombstoneDecayBeforeDeltaMutation();
            CancelHydrationSession(clearQueue: false);
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLoops();
            TryUnregisterService();
            DehydrateAll(syncTransformsBackToRecords: false);
            CaptureQueuedWorldTelemetryDumpSnapshotCold();
            ClearSectorCorruptionNotificationDiagnostics();
            _saveService = null;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
        }

        private void OnDestroy()
        {
            InvalidateIndexedSectorAsyncOperations();
            CancelHydrationSession(clearQueue: false);
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLoops();
            TryUnregisterService();
            DehydrateAll(syncTransformsBackToRecords: false);
            CompleteTombstoneDecayBeforeDeltaMutation();
            _saveService = null;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            ClearSectorCorruptionNotificationDiagnostics();
            ShutdownWorldTelemetryDumpWorkerCold();

            DisposeVaultBackedStorage();
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            _dataVault = null;
        }

        private void DisposeVaultBackedStorage()
        {
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

            if (_worldTelemetryRing.IsCreated)
                _worldTelemetryRing.Dispose();

            if (_worldTelemetryCursor.IsCreated)
                _worldTelemetryCursor.Dispose();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            PersistentWorldRegistry registered = GlobalRegistry.PersistentWorldRegistry;
            if (ReferenceEquals(registered, this))
            {
                _serviceRegistered = true;
                s_activeRuntimeInstance = this;
                return;
            }

            if (ReferenceEquals(registered, null))
                GlobalRegistry.RegisterPersistentWorldRegistry(this);

            _serviceRegistered = ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PersistentWorldRegistry registered = GlobalRegistry.PersistentWorldRegistry;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsPersistentWorldRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.UnregisterPersistentWorldRegistry(registered);
                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
            }

            PersistentWorldRegistry active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsPersistentWorldRuntimeUsable(active))
            {
                GlobalRegistry.RegisterPersistentWorldRegistry(active);
                s_activeRuntimeInstance = active;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterPersistentWorldRegistry(active);
            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;

            return false;
        }

        private static bool IsPersistentWorldRuntimeUsable(PersistentWorldRegistry registry)
        {
            return registry != null && registry._serviceRegistered && registry.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.PersistentWorldRegistry, this))
                GlobalRegistry.UnregisterPersistentWorldRegistry(this);

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            _serviceRegistered = false;
        }

        public void Tick(float dt)
        {
            AdvanceWorldClock(dt);
        }

        private void AdvanceWorldClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _worldClockSeconds = math.min(WorldClockMaxSeconds, _worldClockSeconds + deltaTime);
        }

        private float ResolveWorldClockSeconds()
        {
            return _worldClockSeconds;
        }

        public void LateFrameTick()
        {
            _resolvedItemCatalog?.DrainDeferredWorldPrefabReleases(4);
            DrainDehydrateQueue(MaxDehydrationsPerTick);
            TryStartPendingIndexedSectorPaging();
            TryRunPendingHydrationSessionLateFrame();
            if (_tombstoneDecayApplyPending)
                ApplyCollectedTombstoneDecay(MaxTombstoneDecayAppliesPerLateFrame);
            CaptureQueuedWorldTelemetryDumpSnapshotCold();
        }

        public void SlowTick()
        {
            float now = Time.unscaledTime;
            ScheduleTombstoneDecaySweepIfDue(now);

            if (!TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
                return;

            if (!TryQuantizeSector(in playerAup, out int2 nextSector))
                return;

            if (_indexedSectorPagingEnabled && (!_playerSectorValid || !math.all(nextSector == _currentPlayerSector)))
            {
                _currentPlayerSector = nextSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(nextSector);
            }

            TryScheduleSectorOverrideCommit();

            if (!TryResolveRegistryChunkId(in playerAup, 0u, out int3 nextChunk))
                return;

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

        internal bool CanRegisterDroppedItem(ItemData itemData, int quantity)
        {
            if (!CanRegisterDroppedItemData(itemData, quantity, out string persistentId))
                return false;

            return ComputePersistentIdHash(persistentId) != 0UL &&
                   CanAppendDroppedItemState();
        }

        internal bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)
        {
            if (!CanRegisterDroppedItemData(itemData, quantity, out string persistentId))
                return false;

            return ComputePersistentIdHash(persistentId) != 0UL &&
                   CanAppendDroppedItemState() &&
                   CanResolveDroppedItemRuntimePosition(runtimePosition);
        }

        internal bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity)
        {
            if (itemHashId == 0 || itemCatalog == null)
                return false;

            return CanRegisterDroppedItem(itemCatalog.FindByHash(itemHashId), quantity);
        }

        internal bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            if (itemHashId == 0 || itemCatalog == null)
                return false;

            return CanRegisterDroppedItem(itemCatalog.FindByHash(itemHashId), quantity, runtimePosition);
        }

        internal bool CanRegisterDroppedItemBatch(int recordCount)
        {
            return recordCount <= 0 || CanAppendDroppedItemState(recordCount);
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
            if (!CanRegisterDroppedItemData(itemData, quantity, out string persistentId) ||
                !CanAppendDroppedItemState())
            {
                return false;
            }

            ulong persistentIdHash = ComputePersistentIdHash(persistentId);
            if (persistentIdHash == 0UL || !TryGenerateInstanceUid(itemData, persistentIdHash, out uint instanceUid))
                return false;

            Vector3 scatteredRuntimePosition = ApplyDeterministicDropScatter(runtimePosition, instanceUid);
            if (!TryResolveAupFromRuntimeOrigin(scatteredRuntimePosition, out AbsoluteUniversePosition position))
                return false;

            if (!TryResolveRegistryChunkId(in position, instanceUid, out int3 chunkId))
                return false;

            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = position,
                ChunkId = chunkId,
                ItemPersistentIdHash = persistentIdHash,
                ItemPersistentId = new FixedString128Bytes(persistentId),
                Quantity = quantity,
                Flags = PersistentWorldItemFlags.None,
                InstanceUid = instanceUid
            };

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeltaRecord(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

            if (!RegisterOrUpdatePoolSlot(recordIndex, in record) ||
                !RegisterOrUpdateEntityState(in record, CreateEntityStateFromRecord(in record, geneticsMask, qualityMilli)))
            {
                RemoveDeltaRecord(record.InstanceUid);
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

            RegisterSpawnImpulse(record.InstanceUid, initialImpulse);
            RegisterSpawnVelocityChange(record.InstanceUid, inheritedVelocityChange);

            if (_hasLastHydrationScanAup && ShouldHydrateDehydratedRecord(in record, in _lastHydrationScanAup))
                QueueRecordForHydration(recordIndex, in record, in _lastHydrationScanAup);

            UpdateDiagnostics();
            return true;
        }

        private static bool CanRegisterDroppedItemData(ItemData itemData, int quantity, out string persistentId)
        {
            persistentId = null;
            if (itemData == null || quantity <= 0)
                return false;

            persistentId = itemData.PersistentId;
            return !string.IsNullOrWhiteSpace(persistentId) &&
                   itemData.worldPrefab != null;
        }

        private bool CanAppendDroppedItemState()
        {
            return CanAppendDroppedItemState(1);
        }

        private bool CanAppendDroppedItemState(int recordCount)
        {
            if (!_records.IsCreated ||
                !_recordsByChunk.IsCreated ||
                !_deltaRecords.IsCreated ||
                !_deltaRecordIndexByEntityId.IsCreated ||
                !_poolSlotData.IsCreated ||
                !_guidToPoolIndex.IsCreated ||
                !_entityStateByInstanceUid.IsCreated)
            {
                return false;
            }

            if (recordCount <= 0)
                return true;

            long nextRecordIndex = _records.Length;
            long requiredRecordCount = nextRecordIndex + recordCount;
            return nextRecordIndex >= 0 &&
                   CanGenerateDroppedItemInstanceUidBatch(recordCount) &&
                   requiredRecordCount <= _records.Capacity &&
                   (long)_recordsByChunk.Count + recordCount <= _recordsByChunk.Capacity &&
                   requiredRecordCount <= _poolSlotData.Length &&
                   (long)_deltaRecords.Length + recordCount <= _deltaRecords.Capacity &&
                   (long)_deltaRecordIndexByEntityId.Count + recordCount <= _deltaRecordIndexByEntityId.Capacity &&
                   (long)_guidToPoolIndex.Count + recordCount <= _guidToPoolIndex.Capacity &&
                   (long)_entityStateByInstanceUid.Count + recordCount <= _entityStateByInstanceUid.Capacity;
        }

        private static bool CanGenerateDroppedItemInstanceUidBatch(int recordCount)
        {
            if (recordCount <= 0)
                return true;

            int counterSnapshot = Volatile.Read(ref _nextInstanceUidCounter);
            if (counterSnapshot < 0)
                return false;

            long requiredSequence = (long)counterSnapshot + recordCount;
            return requiredSequence > 0L &&
                   requiredSequence <= InstanceUidCounterMask;
        }

        private bool CanResolveDroppedItemRuntimePosition(Vector3 runtimePosition)
        {
            if (!CanResolveDroppedItemRuntimePositionSample(runtimePosition))
                return false;

            return CanResolveDroppedItemScatterEnvelope(runtimePosition);
        }

        private bool CanResolveDroppedItemScatterEnvelope(Vector3 runtimePosition)
        {
            if (!CanResolveDroppedItemLiftedSample(runtimePosition, 0f, 0f, DropScatterMinLiftMeters) ||
                !CanResolveDroppedItemLiftedSample(runtimePosition, 0f, 0f, DropScatterMaxLiftMeters))
            {
                return false;
            }

            for (uint directionIndex = 0u; directionIndex < 8u; directionIndex++)
            {
                float2 direction = ResolveScatterPlanarDirection(directionIndex << 29);
                if (!CanResolveDroppedItemLiftedSample(runtimePosition, direction.x, direction.y, DropScatterMinLiftMeters) ||
                    !CanResolveDroppedItemLiftedSample(runtimePosition, direction.x, direction.y, DropScatterMaxLiftMeters))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanResolveDroppedItemLiftedSample(Vector3 runtimePosition, float directionX, float directionZ, float liftMeters)
        {
            Vector3 sample = runtimePosition;
            sample.x += directionX * DropScatterRadiusMeters;
            sample.y += liftMeters;
            sample.z += directionZ * DropScatterRadiusMeters;
            return CanResolveDroppedItemRuntimePositionSample(sample);
        }

        private bool CanResolveDroppedItemRuntimePositionSample(Vector3 runtimePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            return AbsoluteUniversePosition.IsValidChunkId(chunkId);
        }

        internal bool TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            if (itemHashId == 0 || itemCatalog == null)
                return false;

            ItemData itemData = itemCatalog.FindByHash(itemHashId);
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition);
        }

        bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)
        {
            return CanRegisterDroppedItem(itemData, quantity, runtimePosition);
        }

        bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            return CanRegisterDroppedItem(itemHashId, itemCatalog, quantity, runtimePosition);
        }

        bool IPersistentDroppedItemRegistry.TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)
        {
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition);
        }

        bool IPersistentDroppedItemRegistry.TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse)
        {
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition, initialImpulse);
        }

        bool IPersistentDroppedItemRegistry.TryRegisterDroppedItem(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            Vector3 initialImpulse,
            Vector3 inheritedVelocityChange)
        {
            return TryRegisterDroppedItem(itemData, quantity, runtimePosition, initialImpulse, inheritedVelocityChange);
        }

        bool IPersistentDroppedItemRegistry.TryRegisterDroppedItemWithState(
            ItemData itemData,
            int quantity,
            Vector3 runtimePosition,
            ulong geneticsMask,
            ushort qualityMilli)
        {
            return TryRegisterDroppedItemWithState(itemData, quantity, runtimePosition, geneticsMask, qualityMilli);
        }

        bool IPersistentDroppedItemRegistry.TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)
        {
            return TryRegisterDroppedItem(itemHashId, itemCatalog, quantity, runtimePosition);
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

            if (!TryResolveRegistryChunkId(in position, instanceUid, out int3 chunkId))
                return false;

            if (TryFindRecordIndexByInstanceUid(instanceUid, out int existingRecordIndex))
            {
                PersistentWorldItemRecord existing = _records[existingRecordIndex];
                PersistentWorldItemRecord previousRecord = existing;
                int3 previousChunkId = existing.ChunkId;
                if (!TryMoveRecordIndexToChunk(existing.ChunkId, chunkId, existingRecordIndex, instanceUid))
                    return false;

                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.ItemPersistentId = default;
                existing.Quantity = 1;
                existing.Flags = PersistentWorldItemFlags.FloraDestroyed;
                if (!UpsertDeltaRecord(in existing))
                {
                    RollbackRecordChunkMove(chunkId, previousChunkId, existingRecordIndex, instanceUid);
                    return false;
                }

                if (!TryWriteRecordAt(existingRecordIndex, in existing))
                {
                    UpsertDeltaRecord(in previousRecord);
                    RollbackRecordChunkMove(chunkId, previousChunkId, existingRecordIndex, instanceUid);
                    return false;
                }

                RemoveEntityState(in existing);
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

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeltaRecord(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

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

            if (!TryResolveRegistryChunkId(in position, instanceUid, out int3 chunkId))
                return false;

            if (TryFindRecordIndexByInstanceUid(instanceUid, out int existingRecordIndex))
            {
                PersistentWorldItemRecord existing = _records[existingRecordIndex];
                PersistentWorldItemRecord previousRecord = existing;
                int3 previousChunkId = existing.ChunkId;
                if (!TryMoveRecordIndexToChunk(existing.ChunkId, chunkId, existingRecordIndex, instanceUid))
                    return false;

                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.ItemPersistentId = default;
                existing.Quantity = packedState;
                existing.Flags = PersistentWorldItemFlags.FloraStateOverride;
                if (!UpsertDeltaRecord(in existing))
                {
                    RollbackRecordChunkMove(chunkId, previousChunkId, existingRecordIndex, instanceUid);
                    return false;
                }

                if (!TryWriteRecordAt(existingRecordIndex, in existing))
                {
                    UpsertDeltaRecord(in previousRecord);
                    RollbackRecordChunkMove(chunkId, previousChunkId, existingRecordIndex, instanceUid);
                    return false;
                }

                RemoveEntityState(in existing);
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

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeltaRecord(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

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

            if (!TryResolveRegistryChunkId(in position, instanceUid, out int3 chunkId))
                return false;

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

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeletedTombstone(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

            if (!RegisterResourceNodeTombstone(tombstoneId))
            {
                RemoveDeltaRecord(record.InstanceUid);
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

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
            AbsoluteUniversePosition invalidAup = AbsoluteUniversePosition.Invalid();
            AbsoluteUniversePosition safePosition = AbsoluteUniversePosition.Sanitize(in position, in invalidAup);
            if (tombstoneId == 0UL ||
                !_records.IsCreated ||
                _records.Length >= _records.Capacity ||
                !safePosition.IsFinite())
            {
                return false;
            }

            if (IsResourceNodeMetamorphosed(tombstoneId))
                return true;

            if (!TryGenerateResourceNodeMetamorphosisInstanceUid(tombstoneId, out uint instanceUid))
                return false;

            if (!TryResolveRegistryChunkId(in safePosition, instanceUid, out int3 chunkId))
                return false;

            PersistentWorldItemRecord record = new PersistentWorldItemRecord
            {
                Position = safePosition,
                ChunkId = chunkId,
                ItemPersistentIdHash = tombstoneId,
                ItemPersistentId = default,
                Quantity = 1,
                Flags = PersistentWorldItemFlags.ResourceNodeMetamorphosed,
                InstanceUid = instanceUid
            };

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeltaRecord(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

            if (!RegisterResourceNodeMetamorphosis(tombstoneId))
            {
                RemoveDeltaRecord(record.InstanceUid);
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

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

            if (!TryResolveRegistryChunkId(in position, instanceUid, out int3 chunkId))
                return false;

            for (int pendingSeedRecordIndex = 0; pendingSeedRecordIndex < _records.Length; pendingSeedRecordIndex++)
            {
                PersistentWorldItemRecord existing = _records[pendingSeedRecordIndex];
                if (existing.InstanceUid != instanceUid)
                    continue;

                PersistentWorldItemRecord previousRecord = existing;
                int3 previousChunkId = existing.ChunkId;
                if (!TryMoveRecordIndexToChunk(existing.ChunkId, chunkId, pendingSeedRecordIndex, instanceUid))
                    return false;

                existing.Position = position;
                existing.ChunkId = chunkId;
                existing.ItemPersistentIdHash = floraPersistentIdHash;
                existing.Quantity = remainingSeconds;
                existing.Flags = PersistentWorldItemFlags.FloraSeedPending;
                if (!UpsertDeltaRecord(in existing))
                {
                    RollbackRecordChunkMove(chunkId, previousChunkId, pendingSeedRecordIndex, instanceUid);
                    return false;
                }

                if (!TryWriteRecordAt(pendingSeedRecordIndex, in existing))
                {
                    UpsertDeltaRecord(in previousRecord);
                    RollbackRecordChunkMove(chunkId, previousChunkId, pendingSeedRecordIndex, instanceUid);
                    return false;
                }

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

            if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                return false;

            if (!UpsertDeltaRecord(in record))
            {
                RollbackAppendedRecord(recordIndex, in record);
                return false;
            }

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
                if (record.InstanceUid != instanceUid || !PersistentWorldItemRecord.IsFloraSeedPending(in record))
                    continue;

                PersistentWorldItemRecord previousRecord = record;
                record.Quantity = remainingSeconds;
                if (!UpsertDeltaRecord(in record))
                    return false;

                if (!TryWriteRecordAt(recordIndex, in record))
                {
                    UpsertDeltaRecord(in previousRecord);
                    return false;
                }

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
                if (record.InstanceUid != instanceUid || !PersistentWorldItemRecord.IsFloraSeedPending(in record))
                    continue;

                PersistentWorldItemRecord previousRecord = record;
                record.Quantity = 1;
                record.Flags = PersistentWorldItemFlags.FloraSeedReady;
                if (!UpsertDeltaRecord(in record))
                    return false;

                if (!TryWriteRecordAt(recordIndex, in record))
                {
                    UpsertDeltaRecord(in previousRecord);
                    return false;
                }

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
            if (_floraSpawnStateByInstanceUid.TrySet(instanceUid, state))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryFloraSpawnStateKeysBuffer,
                0u,
                unchecked((int)instanceUid),
                _floraSpawnStateByInstanceUid.Capacity,
                TryResolveRegistryChunkId(in position, instanceUid, out int3 telemetryChunk)
                    ? telemetryChunk
                    : AbsoluteUniversePosition.InvalidChunkId(),
                instanceUid);
            return false;
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

            bool wasDeletedRegistered = IsDeletedInstanceUid(instanceUid);
            if (!RegisterDeletedInstanceUid(instanceUid))
                return false;

            if (_deltaRecordIndexByEntityId.TryGetValue(instanceUid, out int existingIndex))
            {
                if (!TryWriteCompactDeltaRecordAt(existingIndex, in compactRecord))
                {
                    if (!wasDeletedRegistered)
                        UnregisterDeletedInstanceUid(instanceUid);
                    return false;
                }

                RebuildDeltaChunkLookup();
                return true;
            }

            if (!TryAppendCompactDeltaRecord(instanceUid, in compactRecord))
            {
                if (!wasDeletedRegistered)
                    UnregisterDeletedInstanceUid(instanceUid);
                return false;
            }

            RebuildDeltaChunkLookup();
            return true;
        }

        internal int3 ResolveRuntimeChunkId(Vector3 runtimePosition)
        {
            return TryResolveRuntimeChunkId(runtimePosition, out int3 chunkId)
                ? chunkId
                : InvalidRuntimeChunkId();
        }

        internal bool TryResolveRuntimeChunkId(Vector3 runtimePosition, out int3 chunkId)
        {
            chunkId = InvalidRuntimeChunkId();
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition position))
            {
                WriteWorldTelemetry(
                    WorldTelemetryInvalidAup,
                    default,
                    0u,
                    0,
                    0,
                    chunkId,
                    0u);
                return false;
            }

            return TryResolveRegistryChunkId(in position, 0u, out chunkId);
        }

        private static int3 InvalidRuntimeChunkId()
        {
            return new int3(int.MinValue, int.MinValue, int.MinValue);
        }

        private static bool IsInvalidRuntimeChunkId(int3 chunkId)
        {
            return chunkId.x == int.MinValue &&
                   chunkId.y == int.MinValue &&
                   chunkId.z == int.MinValue;
        }

        internal int CopyResourceNodeTombstonesInChunk(int3 chunkId, List<ResourceNodeTombstoneRecord> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            if (!_records.IsCreated || IsInvalidRuntimeChunkId(chunkId))
                return 0;

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (!PersistentWorldItemRecord.IsResourceNodeDestroyed(in record) ||
                    record.ItemPersistentIdHash == 0UL ||
                    !record.Position.IsFinite() ||
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
                if (record.ItemPersistentIdHash != tombstoneId || !PersistentWorldItemRecord.IsResourceNodeDestroyed(in record))
                    continue;

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                if (!TryWriteRecordAt(recordIndex, in record))
                    return false;

                if (_resourceNodeTombstoneIds.IsCreated)
                    _resourceNodeTombstoneIds.Remove(tombstoneId);

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

            for (int recordIndex = 0; recordIndex < _records.Length; recordIndex++)
            {
                PersistentWorldItemRecord record = _records[recordIndex];
                if (record.InstanceUid != instanceUid || !PersistentWorldItemRecord.IsFloraDestroyed(in record))
                    continue;

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                if (!TryWriteRecordAt(recordIndex, in record))
                    return false;

                UnregisterDeletedInstanceUid(instanceUid);
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
                if (record.InstanceUid != instanceUid || !PersistentWorldItemRecord.IsFloraStateOverride(in record))
                    continue;

                record.Flags = PersistentWorldItemFlags.Collected;
                record.Quantity = 0;
                if (!TryWriteRecordAt(recordIndex, in record))
                    return false;

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
            if (PersistentWorldItemRecord.IsCollected(in record))
                return;

            PersistentWorldItemRecord previousRecord = record;
            record.MarkCollected();
            record.MarkDeleted();
            record.Quantity = 0;
            if (!UpsertDeletedTombstone(in record))
                return;

            if (!TryWriteRecordAt(recordIndex, in record))
            {
                UpsertDeltaRecord(in previousRecord);
                return;
            }

            DehydrateRecord(recordIndex, syncTransformBackToRecord: false);
            RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
            RemoveEntityState(in record);
            UpdateDiagnostics();
        }

        internal bool CaptureSaveSnapshot()
        {
            _lastSaveSnapshotFailureCode = SaveSnapshotFailureNone;

            if (!_saveSnapshotDeltas.IsCreated || !_deltaRecords.IsCreated)
            {
                // Vault-backed storage was never allocated. Awake() returns before
                // InitializeVaultBackedStorage() whenever TryRegisterService() leaves
                // _serviceRegistered false, which is what a ready-locked GlobalRegistry causes.
                // WriteWorldTelemetry() cannot report this: the ring is allocated by the same
                // method that did not run. The failure code is the only carrier out of here.
                _lastSaveSnapshotFailureCode = SaveSnapshotFailureStorageNotCreated;
                return false;
            }

            SyncAllHydratedRecords();
            if (!StageResourceNodeTombstonesForSave())
            {
                _lastSaveSnapshotFailureCode = SaveSnapshotFailureTombstoneStaging;
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryResourceTombstoneKeysBuffer,
                    0u,
                    0,
                    _resourceNodeTombstoneIds.Capacity,
                    _currentPlayerChunk,
                    0u);
                TryClearSaveSnapshotDeltas();
                UpdateDiagnostics();
                return false;
            }

            if (!TryClearSaveSnapshotDeltas())
            {
                // TryClearSaveSnapshotDeltas() already wrote its own capacity-mismatch telemetry.
                _lastSaveSnapshotFailureCode = SaveSnapshotFailureSnapshotClear;
                UpdateDiagnostics();
                return false;
            }

            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                if (!_saveSnapshotDeltas.IsCreated)
                {
                    _lastSaveSnapshotFailureCode = SaveSnapshotFailureStorageNotCreated;
                    return false;
                }

                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (_saveSnapshotDeltas.Length >= _saveSnapshotDeltas.Capacity ||
                    !_saveSnapshotDeltas.AddNoResize(expandedRecord))
                {
                    _lastSaveSnapshotFailureCode = SaveSnapshotFailureCapacityOverflow;
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistrySaveSnapshotDeltasBuffer,
                        0u,
                        _saveSnapshotDeltas.Length,
                        _saveSnapshotDeltas.Capacity,
                        _currentPlayerChunk,
                        expandedRecord.InstanceUid);
                    TryClearSaveSnapshotDeltas();
                    UpdateDiagnostics();
                    return false;
                }
            }

            UpdateDiagnostics();
            return true;
        }

        /// <summary>
        /// Discriminates which <see cref="CaptureSaveSnapshot"/> branch rejected the last capture.
        /// <see cref="SaveSnapshotFailureNone"/> after a successful capture. Read by SaveManager to
        /// attribute a save-snapshot failure to an exact branch instead of one opaque reason string.
        /// </summary>
        internal byte LastSaveSnapshotFailureCode => _lastSaveSnapshotFailureCode;

        internal int SaveSnapshotCount => _saveSnapshotDeltas.IsCreated ? _saveSnapshotDeltas.Length : 0;

        internal int SaveSnapshotCapacity => _saveSnapshotDeltas.IsCreated ? _saveSnapshotDeltas.Capacity : 0;

        internal bool TryReadSaveSnapshotDelta(int index, out PersistentWorldDeltaRecord record)
        {
            return _saveSnapshotDeltas.TryRead(index, out record);
        }

        internal int CopySaveSnapshotDeltas(NativeArray<PersistentWorldDeltaRecord> destination, int maxCount)
        {
            return _saveSnapshotDeltas.CopyTo(destination, maxCount);
        }

        internal bool TryCopySaveSnapshotDeltas(NativeArray<PersistentWorldDeltaRecord> destination, int maxCount, out int copiedCount)
        {
            return _saveSnapshotDeltas.TryCopyTo(destination, maxCount, out copiedCount);
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
            if (!ClearRestoredRuntimeState())
            {
                UpdateDiagnostics();
                return;
            }

            if (loadedRecords != null)
            {
                uint maxObservedInstanceSequence = 0u;
                int restoreCount = math.min(loadedRecords.Length, _records.Capacity);
                if (!TryPreRegisterLoadedRecordTombstones(loadedRecords, restoreCount))
                {
                    AbortRestoreAfterFailure(WorldRegistryDeletedInstanceKeysBuffer, 0u);
                    return;
                }

                for (int i = 0; i < restoreCount; i++)
                {
                    PersistentWorldDeltaRecord deltaRecord = loadedRecords[i];
                    if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                    {
                        AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, deltaRecord.InstanceUid);
                        return;
                    }

                    uint observedSequence = deltaRecord.InstanceUid & InstanceUidCounterMask;
                    if (observedSequence > maxObservedInstanceSequence)
                        maxObservedInstanceSequence = observedSequence;

                    if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord))
                    {
                        if (!TryBuildCompactDeltaRecord(deltaRecord, out PersistentWorldCompactDeltaRecord deletedCompactRecord))
                        {
                            AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        bool wasDeletedRegistered = IsDeletedInstanceUid(deltaRecord.InstanceUid);
                        if (!RegisterDeletedInstanceUid(deltaRecord.InstanceUid))
                        {
                            AbortRestoreAfterFailure(WorldRegistryDeletedInstanceKeysBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        if (!TryAppendCompactDeltaRecord(deltaRecord.InstanceUid, in deletedCompactRecord))
                        {
                            if (!wasDeletedRegistered)
                                UnregisterDeletedInstanceUid(deltaRecord.InstanceUid);
                            AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        if (PersistentWorldDeltaRecord.IsResourceNodeDestroyed(in deltaRecord))
                        {
                            ulong tombstoneId = ResolveResourceNodeTombstoneId(in deltaRecord);
                            deltaRecord.ItemPersistentIdHash = tombstoneId;
                            if (!RegisterResourceNodeTombstone(tombstoneId) ||
                                !StageLoadedResourceNodeTombstoneRecord(in deltaRecord, tombstoneId))
                            {
                                AbortRestoreAfterFailure(WorldRegistryResourceTombstoneKeysBuffer, deltaRecord.InstanceUid);
                                return;
                            }
                        }

                        continue;
                    }

                    if (PersistentWorldDeltaRecord.IsResourceNodeMetamorphosed(in deltaRecord))
                    {
                        if (!TryBuildCompactDeltaRecord(deltaRecord, out PersistentWorldCompactDeltaRecord metamorphosisCompactRecord))
                        {
                            AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        if (!TryAppendCompactDeltaRecord(deltaRecord.InstanceUid, in metamorphosisCompactRecord))
                        {
                            AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        if (!RegisterResourceNodeMetamorphosis(deltaRecord.ItemPersistentIdHash))
                        {
                            AbortRestoreAfterFailure(WorldRegistryResourceMetamorphosedKeysBuffer, deltaRecord.InstanceUid);
                            return;
                        }

                        continue;
                    }

                    PersistentWorldItemRecord record = deltaRecord.ToRecord(chunkSizeMeters);
                    if (PersistentWorldItemRecord.IsCollected(in record) || IsDeletedInstanceUid(record.InstanceUid))
                        continue;

                    if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int existingDeltaIndex))
                    {
                        if (TryFindRecordIndexByInstanceUid(record.InstanceUid, out int existingRecordIndex))
                        {
                            PersistentWorldItemRecord existingRecord = _records[existingRecordIndex];
                            if (!TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord replacementCompactRecord))
                            {
                                AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, record.InstanceUid);
                                return;
                            }

                            if (!TryMoveRecordIndexToChunk(existingRecord.ChunkId, record.ChunkId, existingRecordIndex, record.InstanceUid))
                            {
                                AbortRestoreAfterFailure(WorldRegistryRecordsByChunkKeysBuffer, record.InstanceUid);
                                return;
                            }

                            if (!TryWriteRecordAt(existingRecordIndex, in record) ||
                                !TryWriteCompactDeltaRecordAt(existingDeltaIndex, in replacementCompactRecord))
                            {
                                AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, record.InstanceUid);
                                return;
                            }

                            if (!PersistentWorldItemRecord.IsFloraDestroyed(in record) &&
                                !PersistentWorldItemRecord.IsFloraSeedPending(in record) &&
                                !PersistentWorldItemRecord.IsFloraSeedReady(in record) &&
                                !PersistentWorldItemRecord.IsFloraStateOverride(in record) &&
                                !PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record))
                            {
                                if (!RegisterOrUpdatePoolSlot(existingRecordIndex, in record) ||
                                    !RegisterOrUpdateEntityState(in record))
                                {
                                    AbortRestoreAfterFailure(WorldRegistryGuidToPoolIndexKeysBuffer, record.InstanceUid);
                                    return;
                                }
                            }
                            else
                            {
                                RemoveEntityState(in record);
                            }
                        }

                        continue;
                    }

                    if (!TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                    {
                        AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, record.InstanceUid);
                        return;
                    }

                    if (!TryAppendRecordWithChunk(in record, out int recordIndex))
                    {
                        AbortRestoreAfterFailure(WorldRegistryRecordsBuffer, record.InstanceUid);
                        return;
                    }

                    if (!TryAppendCompactDeltaRecord(record.InstanceUid, in compactRecord))
                    {
                        AbortRestoreAfterFailure(WorldRegistryDeltaRecordsBuffer, record.InstanceUid);
                        return;
                    }

                    if (!PersistentWorldItemRecord.IsFloraDestroyed(in record) &&
                        !PersistentWorldItemRecord.IsFloraSeedPending(in record) &&
                        !PersistentWorldItemRecord.IsFloraSeedReady(in record) &&
                        !PersistentWorldItemRecord.IsFloraStateOverride(in record) &&
                        !PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record))
                    {
                        if (!RegisterOrUpdatePoolSlot(recordIndex, in record) ||
                            !RegisterOrUpdateEntityState(in record))
                        {
                            AbortRestoreAfterFailure(WorldRegistryGuidToPoolIndexKeysBuffer, record.InstanceUid);
                            return;
                        }
                    }
                }

                RebuildDeltaChunkLookup();
                RebaseInstanceUidCounter(maxObservedInstanceSequence);
            }

            if (scheduleHydration &&
                TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                if (!TryResolveRegistryChunkId(in playerAup, 0u, out _currentPlayerChunk))
                    return;

                _playerChunkValid = true;
                _lastHydrationScanAup = playerAup;
                _hasLastHydrationScanAup = true;
                _hydrationFrameCounter++;
                RefreshHydrationWindow(in playerAup);
                EnsureHydrationSessionScheduled();
            }

            UpdateDiagnostics();
        }

        private bool ClearRestoredRuntimeState()
        {
            bool cleared = true;
            cleared &= !_records.IsCreated || _records.Clear();
            cleared &= !_recordsByChunk.IsCreated || _recordsByChunk.Clear();
            cleared &= !_deltaRecords.IsCreated || _deltaRecords.Clear();
            cleared &= !_deltaRecordIndexByEntityId.IsCreated || _deltaRecordIndexByEntityId.Clear();
            cleared &= !_deletedInstanceUids.IsCreated || _deletedInstanceUids.Clear();
            cleared &= !_resourceNodeTombstoneIds.IsCreated || _resourceNodeTombstoneIds.Clear();
            cleared &= !_resourceNodeMetamorphosedIds.IsCreated || _resourceNodeMetamorphosedIds.Clear();
            cleared &= !_deltaChunkIndexByChunkId.IsCreated || _deltaChunkIndexByChunkId.Clear();
            cleared &= !_deltaChunkIds.IsCreated || _deltaChunkIds.Clear();
            cleared &= !_deltaItemIndexByHash.IsCreated || _deltaItemIndexByHash.Clear();
            cleared &= !_deltaItemHashes.IsCreated || _deltaItemHashes.Clear();
            cleared &= !_deltaRecordsByChunk.IsCreated || _deltaRecordsByChunk.Clear();
            cleared &= !_tombstoneDecayExpiredIndices.IsCreated || _tombstoneDecayExpiredIndices.Clear();
            cleared &= !_saveSnapshotDeltas.IsCreated || _saveSnapshotDeltas.Clear();
            _tombstoneDecayApplyCursor = 0;
            _tombstoneDecayApplyPending = false;
            _playerChunkValid = false;
            _hasLastHydrationScanAup = false;
            _lastHydrationScanAup = default;
            _currentPlayerChunk = default;
            _hydrationFrameCounter = 0;
            cleared &= ResetPoolSlots();
            if (!cleared)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryRecordsBuffer,
                    0u,
                    _records.IsCreated ? _records.Length : 0,
                    _records.IsCreated ? _records.Capacity : 0,
                    _currentPlayerChunk,
                    0u);
            }

            return cleared;
        }

        private void AbortRestoreAfterFailure(BufferID bufferId, uint instanceUid)
        {
            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                bufferId,
                0u,
                _records.IsCreated ? _records.Length : 0,
                _records.IsCreated ? _records.Capacity : 0,
                _currentPlayerChunk,
                instanceUid);
            ClearRestoredRuntimeState();
            UpdateDiagnostics();
        }

        internal void PreloadTombstonesFromLoadedRecords(PersistentWorldDeltaRecord[] loadedRecords)
        {
            if (!_deletedInstanceUids.IsCreated)
                return;

            bool cleared = _deletedInstanceUids.Clear();
            if (_resourceNodeTombstoneIds.IsCreated)
                cleared &= _resourceNodeTombstoneIds.Clear();
            if (_resourceNodeMetamorphosedIds.IsCreated)
                cleared &= _resourceNodeMetamorphosedIds.Clear();

            if (!cleared)
                return;

            if (loadedRecords == null || loadedRecords.Length <= 0)
                return;

            if (!TryPreRegisterLoadedRecordTombstones(loadedRecords, math.min(loadedRecords.Length, maxTrackedItems)))
            {
                bool clearedRollback = _deletedInstanceUids.Clear();
                if (_resourceNodeTombstoneIds.IsCreated)
                    clearedRollback &= _resourceNodeTombstoneIds.Clear();
                if (_resourceNodeMetamorphosedIds.IsCreated)
                    clearedRollback &= _resourceNodeMetamorphosedIds.Clear();
                if (!clearedRollback)
                {
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistryDeletedInstanceKeysBuffer,
                        0u,
                        _deletedInstanceUids.IsCreated ? _deletedInstanceUids.Length : 0,
                        _deletedInstanceUids.IsCreated ? _deletedInstanceUids.Capacity : 0,
                        _currentPlayerChunk,
                        0u);
                }
            }
        }

        private bool TryPreRegisterLoadedRecordTombstones(PersistentWorldDeltaRecord[] loadedRecords, int restoreCount)
        {
            if (loadedRecords == null || restoreCount <= 0)
                return true;

            for (int i = 0; i < restoreCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = loadedRecords[i];
                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                    return false;

                if (PersistentWorldDeltaRecord.IsDeleted(in deltaRecord))
                {
                    if (!RegisterDeletedInstanceUid(deltaRecord.InstanceUid))
                        return false;

                    if (PersistentWorldDeltaRecord.IsResourceNodeDestroyed(in deltaRecord))
                    {
                        if (!RegisterResourceNodeTombstone(ResolveResourceNodeTombstoneId(in deltaRecord)))
                            return false;
                    }
                }
                else if (PersistentWorldDeltaRecord.IsResourceNodeMetamorphosed(in deltaRecord))
                {
                    if (!RegisterResourceNodeMetamorphosis(deltaRecord.ItemPersistentIdHash))
                        return false;
                }
            }

            return true;
        }

        internal void RestoreFromIndexedSave(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            InvalidateIndexedSectorAsyncOperations();
            if (_indexedSectorDirectory == null)
                _indexedSectorDirectory = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(256);

            if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(absolutePath, _indexedSectorDirectory, out _, out _))
            {
                _indexedSectorPagingEnabled = false;
                _indexedSectorSavePath = string.Empty;
                _indexedSectorOverrideDirectory = string.Empty;
                _indexedSectorDirectory.Clear();
                _sectorOverrideStates?.Clear();
                return;
            }

            _indexedSectorSavePath = absolutePath;
            _indexedSectorOverrideDirectory = Path.Combine(
                Path.GetDirectoryName(absolutePath) ?? string.Empty,
                CreateSectorOverrideDirectoryName(absolutePath));
            _indexedSectorPagingEnabled = _indexedSectorDirectory.Count > 0;
            _playerSectorValid = false;
            _nextSectorOverrideCommitTime = 0f;
            _sectorOverrideStates?.Clear();

            if (!string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                Directory.CreateDirectory(_indexedSectorOverrideDirectory);

            if (TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
            {
                if (!TryQuantizeSector(in playerAup, out int2 playerSector))
                    return;

                _currentPlayerSector = playerSector;
                _playerSectorValid = true;
                EnsureIndexedSectorPagingScheduled(playerSector);
            }
        }

        internal void DisableIndexedSavePaging()
        {
            InvalidateIndexedSectorAsyncOperations();
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
            _indexedSectorPagingStartPending = false;
            _playerSectorValid = false;
            _indexedSectorSavePath = string.Empty;
            _indexedSectorOverrideDirectory = string.Empty;
            _nextSectorOverrideCommitTime = 0f;
            _indexedSectorDirectory?.Clear();
            _sectorOverrideStates?.Clear();
        }

        private int InvalidateIndexedSectorAsyncOperations()
        {
            int generation = AdvanceIndexedSectorAsyncGeneration();
            _indexedSectorPagingInFlight = false;
            _indexedSectorPagingInFlightGeneration = 0;
            _indexedSectorPagingStartPending = false;
            _sectorOverrideCommitInFlight = false;
            _sectorOverrideCommitInFlightGeneration = 0;
            return generation;
        }

        private int AdvanceIndexedSectorAsyncGeneration()
        {
            unchecked
            {
                int nextGeneration = _indexedSectorAsyncGeneration + 1;
                if (nextGeneration == 0)
                    nextGeneration = 1;

                Volatile.Write(ref _indexedSectorAsyncGeneration, nextGeneration);
                return nextGeneration;
            }
        }

        private bool IsIndexedSectorAsyncGenerationCurrent(int generation)
        {
            return generation != 0 && Volatile.Read(ref _indexedSectorAsyncGeneration) == generation;
        }

        private void EnsureIndexedSectorPagingScheduled(int2 centerSector)
        {
            if (!_indexedSectorPagingEnabled || _indexedSectorPagingInFlight || string.IsNullOrEmpty(_indexedSectorSavePath))
                return;

            _pendingIndexedSectorPagingCenter = centerSector;
            _indexedSectorPagingStartPending = true;
        }

        private void TryStartPendingIndexedSectorPaging()
        {
            if (!_indexedSectorPagingStartPending ||
                !_indexedSectorPagingEnabled ||
                _indexedSectorPagingInFlight ||
                string.IsNullOrEmpty(_indexedSectorSavePath))
            {
                return;
            }

            _indexedSectorPagingStartPending = false;
            int asyncGeneration = _indexedSectorAsyncGeneration != 0
                ? _indexedSectorAsyncGeneration
                : AdvanceIndexedSectorAsyncGeneration();
            _indexedSectorPagingInFlight = true;
            _indexedSectorPagingInFlightGeneration = asyncGeneration;
            _ = RunIndexedSectorPagingAsync(_pendingIndexedSectorPagingCenter, asyncGeneration);
        }

        private async Awaitable RunIndexedSectorPagingAsync(int2 centerSector, int asyncGeneration)
        {
            PagedSectorHashWindow desiredSectorHashes = default;
            PersistentWorldDeltaRecord[] stagedRecords = null;
            Dictionary<uint, EntityDataRecord> stagedEntityStates = null;
            bool quarantinedSectorResetApplied = false;
            bool backupRecoveredSectorRepairApplied = false;

            try
            {
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

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

                if (!await SnapshotResidentSectorOverridesAsync(desiredSectorHashes, asyncGeneration))
                    return;

                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                SectorOverrideReadWindow sectorOverrideReadWork = CaptureSectorOverrideReadWork(in desiredSectorHashes);
                string indexedSectorSavePath = _indexedSectorSavePath;
                int indexedChunkSizeMeters = chunkSizeMeters;
                int indexedMaxTrackedItems = maxTrackedItems;
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                await Awaitable.BackgroundThreadAsync();
                if (!TryLoadIndexedSectorRecordsSnapshot(
                        indexedSectorSavePath,
                        indexedMaxTrackedItems,
                        in desiredSectorHashes,
                        out stagedRecords,
                        out string error))
                {
                    await Awaitable.MainThreadAsync();
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Indexed sector paging failed: ", error));
#endif
                    return;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    await Awaitable.MainThreadAsync();
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[PersistentWorldRegistry] Indexed sector paging recovered with quarantine: ", error));
#endif
                    await Awaitable.BackgroundThreadAsync();
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    {
                        await Awaitable.MainThreadAsync();
                        return;
                    }

                    // COLD ALLOC: long[16] - per-operation quarantine reset scratch, never shared across async sessions.
                    long[] quarantineResetScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];
                    quarantinedSectorResetApplied = ResetQuarantinedIndexedSectorsToPristine(
                        indexedSectorSavePath,
                        indexedChunkSizeMeters,
                        quarantineResetScratch);
                }

                if (SaveBinaryStorage.ConsumeIndexedSectorBackupRecoveryFlag())
                {
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    {
                        await Awaitable.MainThreadAsync();
                        return;
                    }

                    // COLD ALLOC: long[16] - per-operation backup recovery repair scratch, only allocated after a recovery flag.
                    long[] backupRecoveryScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];
                    backupRecoveredSectorRepairApplied = RestoreBackupRecoveredIndexedSectorsFromBackup(
                        indexedSectorSavePath,
                        indexedChunkSizeMeters,
                        backupRecoveryScratch);
                }

                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                {
                    await Awaitable.MainThreadAsync();
                    return;
                }

                if (!ApplySectorOverrides(
                        stagedRecords,
                        in sectorOverrideReadWork,
                        indexedChunkSizeMeters,
                        out stagedRecords,
                        out string overrideError))
                {
                    await Awaitable.MainThreadAsync();
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector override merge failed: ", overrideError));
#endif
                    return;
                }

                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                {
                    await Awaitable.MainThreadAsync();
                    return;
                }

                if (!TryLoadSectorEntityStateOverrides(in sectorOverrideReadWork, out stagedEntityStates, out string entityStateError))
                {
                    await Awaitable.MainThreadAsync();
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError(string.Concat("[PersistentWorldRegistry] Sector entity-state restore failed: ", entityStateError));
#endif
                    return;
                }

                await Awaitable.MainThreadAsync();
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                if (quarantinedSectorResetApplied)
                    TryPushSectorCorruptionNotification();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (backupRecoveredSectorRepairApplied)
                    Hecton8.Core.H8Debug.LogWarning("[PersistentWorldRegistry] Indexed sector paging repaired primary sectors from backup.");
#endif

                await AwaitSectorPrefabPrewarmAsync(stagedRecords);
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                RestoreFromLoadedRecords(stagedRecords, scheduleHydration: false);
                int suppressedResourceNodes = Hecton8.Scavenging.ResourceNode.ApplyPersistentWorldRegistryStateToRegisteredNodes(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (suppressedResourceNodes > 0)
                    H8Debug.Log($"[PersistentWorldRegistry] Suppressed {suppressedResourceNodes} resource nodes after indexed sector restore.");
#endif
                ApplyStagedEntityStates(stagedEntityStates);
                if (TryResolvePlayerAupSnapshot(out AbsoluteUniversePosition playerAup))
                {
                    if (!TryResolveRegistryChunkId(in playerAup, 0u, out _currentPlayerChunk))
                        return;

                    _playerChunkValid = true;
                    _lastHydrationScanAup = playerAup;
                    _hasLastHydrationScanAup = true;
                    _hydrationFrameCounter++;
                    RefreshHydrationWindow(in playerAup);
                    EnsureHydrationSessionScheduled();
                }

                UpdateResidentWorldPrefabResidency(_worldPrefabPrewarmHashScratch);
                MarkResidentSectorOverrides(in desiredSectorHashes);
            }
            finally
            {
                if (_indexedSectorPagingInFlightGeneration == asyncGeneration)
                {
                    _indexedSectorPagingInFlight = false;
                    _indexedSectorPagingInFlightGeneration = 0;
                }
            }
        }

        private bool TryLoadIndexedSectorRecordsSnapshot(
            string indexedSectorSavePath,
            int loadedRecordCapacity,
            in PagedSectorHashWindow desiredSectorHashes,
            out PersistentWorldDeltaRecord[] loadedRecords,
            out string error)
        {
            loadedRecords = Array.Empty<PersistentWorldDeltaRecord>();
            error = string.Empty;

            NativeArray<long> desiredSectorHashView = default;
            NativeList<PersistentWorldDeltaRecord> loadedSectorRecords = default;
            int loadedSectorRecordsSentinelId = 0;
            try
            {
                desiredSectorHashView = new NativeArray<long>(PagedSectorHashCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTrackedTransientArray(desiredSectorHashView, IndexedSectorPagingDesiredHashesLabel, NativeAllocationLifetime.TempJob);

                for (int i = 0; i < PagedSectorHashCount; i++)
                    desiredSectorHashView[i] = desiredSectorHashes[i];

                loadedSectorRecords = new NativeList<PersistentWorldDeltaRecord>(math.max(16, loadedRecordCapacity), Allocator.TempJob);
                RegisterTrackedTransientNativeList(
                    loadedSectorRecords,
                    IndexedSectorPagingLoadedRecordsLabel,
                    NativeAllocationLifetime.TempJob,
                    out loadedSectorRecordsSentinelId);

                if (!SaveBinaryStorage.TryLoadIndexedPersistentWorldSectors(
                        indexedSectorSavePath,
                        desiredSectorHashView,
                        loadedSectorRecords,
                        out error))
                {
                    return false;
                }

                int loadedCount = loadedSectorRecords.Length;
                if (loadedCount <= 0)
                    return true;

                loadedRecords = new PersistentWorldDeltaRecord[loadedCount];
                for (int i = 0; i < loadedCount; i++)
                    loadedRecords[i] = loadedSectorRecords[i];

                return true;
            }
            finally
            {
                Exception cleanupException = null;

                if (loadedSectorRecords.IsCreated || loadedSectorRecordsSentinelId > 0)
                {
                    try
                    {
                        DisposeTrackedTransientNativeList(ref loadedSectorRecords, ref loadedSectorRecordsSentinelId);
                    }
                    catch (Exception exception)
                    {
                        if (cleanupException == null)
                            cleanupException = exception;
                    }
                }
                else
                {
                    loadedSectorRecords = default;
                }

                if (desiredSectorHashView.IsCreated)
                {
                    try
                    {
                        DisposeTrackedTransientArray(ref desiredSectorHashView);
                    }
                    catch (Exception exception)
                    {
                        if (cleanupException == null)
                            cleanupException = exception;
                    }
                }
                else
                {
                    desiredSectorHashView = default;
                }

                if (cleanupException != null)
                    throw cleanupException;
            }
        }

        private static bool ResetQuarantinedIndexedSectorsToPristine(
            string indexedSectorSavePath,
            int indexedChunkSizeMeters,
            long[] quarantineResetScratch)
        {
            if (quarantineResetScratch == null || quarantineResetScratch.Length <= 0)
                return false;

            int resetCount = SaveBinaryStorage.CopyAndClearIndexedSectorQuarantineHashes(quarantineResetScratch);
            if (resetCount <= 0 || string.IsNullOrEmpty(indexedSectorSavePath))
                return false;

            bool resetApplied = false;
            for (int i = 0; i < resetCount; i++)
            {
                long sectorHash = quarantineResetScratch[i];
                if (sectorHash == long.MinValue)
                    continue;

                if (!SaveBinaryStorage.TryResetIndexedPersistentWorldSectorToPristine(
                        indexedSectorSavePath,
                        sectorHash,
                        indexedChunkSizeMeters,
                        out _))
                {
                    continue;
                }

                resetApplied = true;
                quarantineResetScratch[i] = long.MinValue;
            }

            return resetApplied;
        }

        private static bool RestoreBackupRecoveredIndexedSectorsFromBackup(
            string indexedSectorSavePath,
            int indexedChunkSizeMeters,
            long[] backupRecoveryScratch)
        {
            if (backupRecoveryScratch == null || backupRecoveryScratch.Length <= 0)
                return false;

            int recoveryCount = SaveBinaryStorage.CopyAndClearIndexedSectorBackupRecoveryHashes(backupRecoveryScratch);
            if (recoveryCount <= 0 || string.IsNullOrEmpty(indexedSectorSavePath))
                return false;

            bool repairApplied = false;
            for (int i = 0; i < recoveryCount; i++)
            {
                long sectorHash = backupRecoveryScratch[i];
                if (sectorHash == long.MinValue)
                    continue;

                if (!SaveBinaryStorage.TryRestoreIndexedPersistentWorldSectorFromBackup(
                        indexedSectorSavePath,
                        sectorHash,
                        indexedChunkSizeMeters,
                        out string restoreError))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[PersistentWorldRegistry] Backup-recovered indexed sector repair failed for 0x{sectorHash:X16}: {restoreError}");
#endif
                    continue;
                }

                repairApplied = true;
                backupRecoveryScratch[i] = long.MinValue;
            }

            return repairApplied;
        }

        private async Awaitable AwaitSectorPrefabPrewarmAsync(PersistentWorldDeltaRecord[] stagedRecords)
        {
            if (stagedRecords == null || stagedRecords.Length <= 0 || !TryEnsureItemLookup() || _resolvedItemCatalog == null)
                return;

            _worldPrefabPrewarmHashScratch.Clear();
            for (int i = 0; i < stagedRecords.Length; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = stagedRecords[i];
                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord) || !TryResolveItemData(deltaRecord.ItemPersistentIdHash, out ItemData itemData) || itemData == null)
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
                   ReferenceEquals(s_activeRuntimeInstance, this) &&
                   !_resolvedItemCatalog.PollWorldPrefabsReadyNonAlloc(_worldPrefabPrewarmHashScratch))
            {
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
                _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_slowTickRegistered)
            {
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultCold(previousService as IDataVault, currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                _saveService = currentService as ISaveService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                TryEnsureItemLookup();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PlayerInventory)
            {
                _playerInventoryService = currentService as IPlayerInventoryService;
                TryEnsureItemLookup();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
            {
                CacheObjectPoolService(currentService as ObjectPoolManager);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
            {
                _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            TryUnregisterRuntimeLoops();
            if (currentService == null ||
                !_serviceRegistered ||
                !isActiveAndEnabled)
            {
                return;
            }

            TryRegisterRuntimeLoops();
        }

        private void RebindDataVaultCold(IDataVault previousVault, IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault) && _records.IsCreated)
                return;

            CancelHydrationSession(clearQueue: true);
            if (_records.IsCreated)
                DehydrateAll(syncTransformsBackToRecords: false);

            if (_dataVault != null || previousVault != null)
            {
                DisposeVaultBackedStorage();
                MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            }

            _dataVault = currentVault;
            if (_dataVault == null)
                return;

            InitializeVaultBackedStorage(_dataVault, maxTrackedItems);
            RegisterNativeMemorySentinelAllocations();
            RegisterPersistentMemoryBudget();
            UpdateDiagnostics();
        }

        private void CacheRegistryServicesCold()
        {
            if (_dataVault == null)
            {
                _dataVault = GlobalRegistry.DataVault;
                if (!_records.IsCreated)
                {
                    InitializeVaultBackedStorage(_dataVault, maxTrackedItems);
                    RegisterNativeMemorySentinelAllocations();
                    RegisterPersistentMemoryBudget();
                }
            }

            _saveService = GlobalRegistry.Save;
            _playerRuntimeContext = GlobalRegistry.Player;
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _physicsService = GlobalRegistry.Physics;
            CacheObjectPoolService(null);
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            TryEnsureItemLookup();
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _objectPoolService = candidate;
                return;
            }

            ObjectPoolManager pool = null;
            _objectPoolService = ObjectPoolManager.TryResolveActiveRuntime(ref pool)
                ? pool
                : null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = null;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolService = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolService = null;
            pool = null;
            return false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !_serviceRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
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
                if (PersistentWorldItemRecord.IsCollected(in record) ||
                    PersistentWorldItemRecord.IsFloraDestroyed(in record) ||
                    PersistentWorldItemRecord.IsFloraSeedPending(in record) ||
                    PersistentWorldItemRecord.IsFloraSeedReady(in record) ||
                    PersistentWorldItemRecord.IsFloraStateOverride(in record) ||
                    PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record) ||
                    !ShouldKeepHydratedRecord(in record, in playerAup))
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
                        _recordIndexScratch.Clear();
                        _recordsByChunk.CopyValuesForKey(chunkId, _recordIndexScratch);
                        if (_recordIndexScratch.Count <= 0)
                            continue;

                        for (int scratchIndex = 0; scratchIndex < _recordIndexScratch.Count; scratchIndex++)
                        {
                            int recordIndex = _recordIndexScratch[scratchIndex];
                            if (!IsValidRecordIndex(recordIndex))
                                continue;

                            PersistentWorldItemRecord record = _records[recordIndex];
                            if (PersistentWorldItemRecord.IsCollected(in record) ||
                                PersistentWorldItemRecord.IsFloraDestroyed(in record) ||
                                PersistentWorldItemRecord.IsFloraSeedPending(in record) ||
                                PersistentWorldItemRecord.IsFloraSeedReady(in record) ||
                                PersistentWorldItemRecord.IsFloraStateOverride(in record) ||
                                PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record) ||
                                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex) ||
                                !ShouldHydrateDehydratedRecord(in record, in playerAup))
                            {
                                continue;
                            }

                            QueueRecordForHydration(recordIndex, in record, in playerAup);
                        }
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
            if (PersistentWorldItemRecord.IsCollected(in record) ||
                IsDeletedInstanceUid(record.InstanceUid) ||
                PersistentWorldItemRecord.IsFloraDestroyed(in record) ||
                PersistentWorldItemRecord.IsFloraSeedPending(in record) ||
                PersistentWorldItemRecord.IsFloraSeedReady(in record) ||
                PersistentWorldItemRecord.IsFloraStateOverride(in record) ||
                PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record) ||
                _hydratedInstancesByRecordIndex.ContainsKey(recordIndex))
            {
                return false;
            }

            if (!TryGetPoolIndex(in record, out int poolIndex))
                return false;

            if (!TryResolveCachedItemData(in record, out ItemData itemData) || itemData == null)
                return false;

            if (_resolvedItemCatalog == null)
                return false;

            int itemHashId = ComputeCatalogItemHash(itemData);
            if (itemHashId == 0)
                return false;

            if (!_resolvedItemCatalog.PollLoadedWorldPrefab(itemHashId, out GameObject prefab) || prefab == null)
            {
                _resolvedItemCatalog.QueueWorldPrefabPrewarm(itemHashId);
                return false;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;

            if (!pool.HasPool(prefab))
                return false;

            EntityDataRecord state = ResolveEntityState(in record);
            AbsoluteUniversePosition hydratedPosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
            if (!hydratedPosition.IsFinite())
            {
                WriteWorldTelemetry(
                    WorldTelemetryInvalidAup,
                    WorldRegistryEntityStateValuesBuffer,
                    0u,
                    0,
                    _entityStateByInstanceUid.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
                return false;
            }

            int hydratedQuantity = math.max(1, state.Quantity);
            ulong itemGeneticsMask = ResolveItemGeneticsMask(in state);
            ushort itemQualityMilli = ResolveItemQualityMilli(in state);
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            float3 runtimePosition = AUPMath.ResolveCameraRelative(in hydratedPosition, in originAup);
            if (!math.all(math.isfinite(runtimePosition)))
            {
                WriteWorldTelemetry(
                    WorldTelemetryInvalidAup,
                    WorldRegistryEntityStateValuesBuffer,
                    0u,
                    0,
                    _entityStateByInstanceUid.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
                return false;
            }

            GameObject instance = pool.Spawn(prefab, new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), Quaternion.identity, allowExpand: false);
            if (instance == null)
                return false;

            PickupItem pickupItem = null;
            HectonItem hectonItem = null;
            if (pool.TryGetPooledComponent(instance, out pickupItem))
            {
                pickupItem.Configure(itemData, hydratedQuantity, itemGeneticsMask, itemQualityMilli);
                pickupItem.BindPersistentWorldRecord(this, recordIndex);
            }
            else if (pool.TryGetPooledComponent(instance, out hectonItem))
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
            _poolSlotPickupItems[poolIndex] = pickupItem;
            _poolSlotHectonItems[poolIndex] = hectonItem;

            if (pool.TryGetPooledRootRigidbody(instance, out Rigidbody pooledRigidbody))
            {
                pooledRigidbody.mass = itemData.MassKg;
                pooledRigidbody.isKinematic = false;
                IPhysicsService physicsService = _physicsService;
                physicsService?.QueueLinearVelocitySet(pooledRigidbody, Vector3.zero, wake: false);
                physicsService?.QueueAngularVelocitySet(pooledRigidbody, Vector3.zero, wake: false);
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
                    physicsService?.QueueLinearVelocitySet(pooledRigidbody, resolvedSpawnVelocity);

                if (TryConsumeSpawnImpulse(record.InstanceUid, out float3 spawnImpulse))
                    physicsService?.QueueForce(pooledRigidbody, new Vector3(spawnImpulse.x, spawnImpulse.y, spawnImpulse.z), ForceMode.Impulse);
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
            if (!TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid))
            {
                _hydratedInstancesByRecordIndex.Remove(recordIndex);
                _hydratedInstancesBySlot[poolIndex] = null;
                _poolSlotTransforms[poolIndex] = null;
                _poolSlotRigidbodies[poolIndex] = null;
                _poolSlotPickupItems[poolIndex] = null;
                _poolSlotHectonItems[poolIndex] = null;
                pool.Despawn(instance);
                return false;
            }

            return true;
        }

        private void DehydrateRecord(int recordIndex, bool syncTransformBackToRecord)
        {
            if (!IsValidRecordIndex(recordIndex))
                return;

            PersistentWorldItemRecord record = _records[recordIndex];
            bool hasPoolIndex = TryGetPoolIndex(in record, out int poolIndex);
            if (!_hydratedInstancesByRecordIndex.TryGetValue(recordIndex, out GameObject instance) && hasPoolIndex)
                instance = _hydratedInstancesBySlot[poolIndex];

            if (instance == null)
            {
                ClearHydratedSlot(recordIndex);
                return;
            }

            if (syncTransformBackToRecord)
                SyncRecordFromLiveInstance(recordIndex, instance, instance.transform);

            Rigidbody pooledRigidbody = hasPoolIndex ? _poolSlotRigidbodies[poolIndex] : null;
            PickupItem pickupItem = hasPoolIndex ? _poolSlotPickupItems[poolIndex] : null;
            HectonItem hectonItem = hasPoolIndex ? _poolSlotHectonItems[poolIndex] : null;

            if (!ClearHydratedSlot(recordIndex))
                return;

            if (pickupItem != null)
                pickupItem.ClearPersistentWorldRecord();

            if (hectonItem != null)
                hectonItem.ClearPersistentWorldRecord();

            if (pooledRigidbody != null)
            {
                IPhysicsService physicsService = _physicsService;
                physicsService?.QueueLinearVelocitySet(pooledRigidbody, Vector3.zero, wake: false);
                physicsService?.QueueAngularVelocitySet(pooledRigidbody, Vector3.zero, wake: false);
                pooledRigidbody.isKinematic = true;
                pooledRigidbody.Sleep();
            }

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            if (pool != null)
            {
                pool.Despawn(instance);
            }
            else
            {
                instance.SetActive(false);
            }
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
            if (PersistentWorldItemRecord.IsCollected(in record))
                return;

            PersistentWorldItemRecord previousRecord = record;
            if (!TryResolveLiveInstanceAup(sourceTransform, out AbsoluteUniversePosition position))
                return;

            if (!TryResolveRegistryChunkId(in position, record.InstanceUid, out int3 nextChunkId))
                return;

            int3 previousChunkId = record.ChunkId;
            if (!math.all(nextChunkId == record.ChunkId))
            {
                if (!TryMoveRecordIndexToChunk(record.ChunkId, nextChunkId, recordIndex, record.InstanceUid))
                    return;

                record.ChunkId = nextChunkId;
            }

            record.Position = position;
            EntityDataRecord state = CaptureEntityStateFromLiveInstance(recordIndex, in record, instance, in position);
            record.Quantity = state.Quantity;
            if (!UpsertDeltaRecord(in record))
            {
                if (!math.all(nextChunkId == previousChunkId))
                    RollbackRecordChunkMove(nextChunkId, previousChunkId, recordIndex, record.InstanceUid);
                return;
            }

            if (!TryWriteRecordAt(recordIndex, in record) ||
                !RegisterOrUpdatePoolSlot(recordIndex, in record) ||
                !RegisterOrUpdateEntityState(in record, in state))
            {
                UpsertDeltaRecord(in previousRecord);
                TryWriteRecordAt(recordIndex, in previousRecord);
                if (!math.all(nextChunkId == previousChunkId))
                    RollbackRecordChunkMove(nextChunkId, previousChunkId, recordIndex, record.InstanceUid);
            }
        }

        private void RemoveRecordIndexFromChunk(int3 chunkId, int recordIndex)
        {
            _recordsByChunk.RemoveFirst(chunkId, recordIndex);
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
            if (PersistentWorldItemRecord.IsCollected(in record) ||
                PersistentWorldItemRecord.IsFloraDestroyed(in record) ||
                PersistentWorldItemRecord.IsFloraSeedPending(in record) ||
                PersistentWorldItemRecord.IsFloraSeedReady(in record) ||
                PersistentWorldItemRecord.IsFloraStateOverride(in record) ||
                PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record))
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= HydrateRadiusSq;
        }

        private bool ShouldKeepHydratedRecord(in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (PersistentWorldItemRecord.IsCollected(in record) ||
                PersistentWorldItemRecord.IsFloraDestroyed(in record) ||
                PersistentWorldItemRecord.IsFloraSeedPending(in record) ||
                PersistentWorldItemRecord.IsFloraSeedReady(in record) ||
                PersistentWorldItemRecord.IsFloraStateOverride(in record) ||
                PersistentWorldItemRecord.IsResourceNodeMetamorphosed(in record))
                return false;

            AbsoluteUniversePosition recordAup = ResolveResidencyPosition(in record);
            return AbsoluteUniversePosition.DistanceSq(in recordAup, in playerAup) <= DehydrateRadiusSq;
        }

        private bool TryResolveItemData(in PersistentWorldItemRecord record, out ItemData itemData)
        {
            itemData = null;
            if (!TryEnsureItemLookup())
                return false;

            return TryResolveCachedItemData(in record, out itemData);
        }

        private bool TryResolveCachedItemData(in PersistentWorldItemRecord record, out ItemData itemData)
        {
            itemData = null;
            if (_resolvedItemCatalog == null || _itemLookupByHash == null || _itemLookupByHash.Count <= 0)
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

        private void ClearItemLookupCold()
        {
            _resolvedItemCatalog = null;
            _itemLookupByHash?.Clear();
            _itemCatalogScratch?.Clear();
        }

        private bool TryEnsureItemLookup()
        {
            if (_itemLookupByHash == null || _itemCatalogScratch == null)
                return false;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory playerInventory = inventoryService != null
                ? inventoryService.Inventory
                : _playerRuntimeContext != null
                    ? _playerRuntimeContext.Inventory
                    : null;
            ItemCatalog currentCatalog = playerInventory != null
                ? playerInventory.ItemCatalog
                : null;

            if (currentCatalog == null)
            {
                ClearItemLookupCold();
                return false;
            }

            if (ReferenceEquals(_resolvedItemCatalog, currentCatalog) && _itemLookupByHash.Count > 0)
                return true;

            _itemCatalogScratch.Clear();

            if (!currentCatalog.TryCopyAllItemsNonAlloc(_itemCatalogScratch, out int itemCount))
            {
                _itemCatalogScratch.Clear();
                _resolvedItemCatalog = null;
                _itemLookupByHash.Clear();
                return false;
            }

            _resolvedItemCatalog = currentCatalog;
            _itemLookupByHash.Clear();
            for (int i = 0; i < itemCount; i++)
            {
                ItemData itemData = _itemCatalogScratch[i];
                if (itemData == null)
                    continue;

                string persistentId = itemData.PersistentId;
                ulong itemHash = ComputePersistentIdHash(persistentId);
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
            if (chunkSizeMeters <= 0)
                return hydrationRadiusInChunks;

            int distanceRadius = (int)math.ceil(DehydrateRadiusMeters / chunkSizeMeters);
            return math.max(hydrationRadiusInChunks, distanceRadius);
        }

        private static int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            return TryQuantizeSector(in position, out int2 sectorCoord)
                ? sectorCoord
                : InvalidPagedSectorCoord();
        }

        private static bool TryQuantizeSector(in AbsoluteUniversePosition position, out int2 sectorCoord)
        {
            return AbsoluteUniversePosition.TryResolveSectorCoord(
                in position,
                PagedSectorEdgeLengthMeters,
                out sectorCoord);
        }

        private static int2 InvalidPagedSectorCoord()
        {
            return new int2(int.MinValue, 0);
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static bool IsDesiredPagedSector(in PagedSectorHashWindow desiredSectorHashes, long sectorHash)
        {
            for (int i = 0; i < PagedSectorHashCount; i++)
            {
                if (desiredSectorHashes[i] == InvalidPagedSectorHash)
                    continue;

                if (desiredSectorHashes[i] == sectorHash)
                    return true;
            }

            return false;
        }

        private async Awaitable<bool> SnapshotResidentSectorOverridesAsync(PagedSectorHashWindow desiredSectorHashes, int asyncGeneration)
        {
            if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                return false;

            if (!_indexedSectorPagingEnabled ||
                string.IsNullOrEmpty(_indexedSectorOverrideDirectory) ||
                !_records.IsCreated)
            {
                return true;
            }

            string sectorOverrideDirectory = _indexedSectorOverrideDirectory;
            int snapshotChunkSizeMeters = chunkSizeMeters;
            if (string.IsNullOrEmpty(sectorOverrideDirectory) || snapshotChunkSizeMeters <= 0)
                return false;

            SyncAllHydratedRecords();

            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] Ã¢â‚¬â€ resident sector snapshot buckets during page-out Ã¢â‚¬â€ owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectors = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            Dictionary<long, List<EntityDataRecord>> sectorEntityStates = new Dictionary<long, List<EntityDataRecord>>(16);
            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (PersistentWorldItemRecord.IsCollected(in record))
                    continue;

                long sectorHash = ComputeSectorHash(in record.Position);
                if (sectorHash == InvalidPagedSectorHash)
                {
                    WriteWorldTelemetry(
                        WorldTelemetryInvalidAup,
                        WorldRegistryRecordsBuffer,
                        0u,
                        i,
                        _records.Capacity,
                        record.ChunkId,
                        record.InstanceUid);
                    continue;
                }

                if (IsDesiredPagedSector(in desiredSectorHashes, sectorHash))
                    continue;

                if (!sectors.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] Ã¢â‚¬â€ one resident sector override record bucket Ã¢â‚¬â€ owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectors.Add(sectorHash, bucket);
                }

                PersistentWorldDeltaRecord deltaRecord = PersistentWorldDeltaRecord.FromRecord(in record, snapshotChunkSizeMeters);
                if (!PersistentWorldDeltaRecord.IsValid(in deltaRecord))
                {
                    WriteWorldTelemetry(
                        WorldTelemetryInvalidAup,
                        WorldRegistryRecordsBuffer,
                        0u,
                        i,
                        _records.Capacity,
                        record.ChunkId,
                        record.InstanceUid);
                    return false;
                }

                AbsoluteUniversePosition deltaPosition = deltaRecord.UnpackPosition(snapshotChunkSizeMeters);
                long deltaSectorHash = ComputeSectorHash(in deltaPosition);
                if (deltaSectorHash == InvalidPagedSectorHash || deltaSectorHash != sectorHash)
                {
                    WriteWorldTelemetry(
                        WorldTelemetryInvalidAup,
                        WorldRegistryRecordsBuffer,
                        0u,
                        i,
                        _records.Capacity,
                        deltaRecord.ChunkId,
                        deltaRecord.InstanceUid);
                    return false;
                }

                bucket.Add(deltaRecord);

                if (!sectorEntityStates.TryGetValue(sectorHash, out List<EntityDataRecord> entityStateBucket))
                {
                    entityStateBucket = new List<EntityDataRecord>(16);
                    sectorEntityStates.Add(sectorHash, entityStateBucket);
                }

                entityStateBucket.Add(ResolveEntityState(in record));
            }

            if (_floraSpawnStateByInstanceUid.IsCreated)
            {
                _floraSpawnStateScratch.Clear();
                _floraSpawnStateByInstanceUid.CopyValuesTo(_floraSpawnStateScratch);
                for (int floraIndex = 0; floraIndex < _floraSpawnStateScratch.Count; floraIndex++)
                {
                    EntityDataRecord state = _floraSpawnStateScratch[floraIndex];
                    if (!IsFloraSpawnTimestampState(in state))
                        continue;

                    AbsoluteUniversePosition floraPosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                    long sectorHash = ComputeSectorHash(in floraPosition);
                    if (sectorHash == InvalidPagedSectorHash)
                    {
                        WriteWorldTelemetry(
                            WorldTelemetryInvalidAup,
                            WorldRegistryFloraSpawnStateValuesBuffer,
                            0u,
                            floraIndex,
                            _floraSpawnStateByInstanceUid.Capacity,
                            _currentPlayerChunk,
                            state.InstanceUid);
                        continue;
                    }

                    if (IsDesiredPagedSector(in desiredSectorHashes, sectorHash))
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
            }

            if (sectors.Count <= 0)
                return true;

            float now = Time.unscaledTime;
            List<SectorOverrideWriteResult> writeResults = new List<SectorOverrideWriteResult>(sectors.Count);
            string failureMessage = string.Empty;
            bool wroteSnapshots = false;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                wroteSnapshots = TryWriteResidentSectorOverrideSnapshots(
                    sectors,
                    sectorEntityStates,
                    writeResults,
                    sectorOverrideDirectory,
                    snapshotChunkSizeMeters,
                    asyncGeneration,
                    out failureMessage);

                await Awaitable.MainThreadAsync();
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!wroteSnapshots)
            {
                if (string.IsNullOrEmpty(failureMessage))
                    failureMessage = "Resident sector override snapshot writer failed without details.";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(failureMessage);
#endif
                return false;
            }

            if (!string.IsNullOrEmpty(failureMessage))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(failureMessage);
#endif
                return false;
            }

            for (int i = 0; i < writeResults.Count; i++)
            {
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return false;

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
                    if (!TryDeleteFileIfExists(state.EntityStateTempPath))
                        return false;

                    state.EntityStateTempPath = string.Empty;
                }

                state.LastUnloadedTime = now;
                state.IsResident = false;
            }

            return true;
        }

        private bool TryValidateResidentSectorDeltaBucket(
            long sectorHash,
            List<PersistentWorldDeltaRecord> bucket,
            int snapshotChunkSizeMeters,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (bucket == null)
            {
                failureMessage = CreateHexMessage("Sector override snapshot bucket is null for 0x", sectorHash, ".");
                return false;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                PersistentWorldDeltaRecord record = bucket[i];
                if (!PersistentWorldDeltaRecord.IsValid(in record))
                {
                    failureMessage = CreateHexMessage("Sector override snapshot contains invalid delta for 0x", sectorHash, ".");
                    return false;
                }

                AbsoluteUniversePosition unpackedPosition = record.UnpackPosition(snapshotChunkSizeMeters);
                long recordSectorHash = ComputeSectorHash(in unpackedPosition);
                if (recordSectorHash == InvalidPagedSectorHash || recordSectorHash != sectorHash)
                {
                    failureMessage = CreateHexMessage("Sector override snapshot contains cross-sector delta for 0x", sectorHash, ".");
                    return false;
                }
            }

            return true;
        }

        private bool TryValidateResidentSectorEntityStateBucket(
            long sectorHash,
            List<EntityDataRecord> entityStateBucket,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (entityStateBucket == null || entityStateBucket.Count <= 0)
                return true;

            for (int i = 0; i < entityStateBucket.Count; i++)
            {
                EntityDataRecord state = entityStateBucket[i];
                AbsoluteUniversePosition statePosition = AbsoluteUniversePosition.FromAlignedBlit(in state.Position);
                long stateSectorHash = ComputeSectorHash(in statePosition);
                if (stateSectorHash == InvalidPagedSectorHash || stateSectorHash != sectorHash)
                {
                    failureMessage = CreateHexMessage("Sector entity-state snapshot contains cross-sector AUP for 0x", sectorHash, ".");
                    return false;
                }
            }

            return true;
        }

        private bool TryWriteResidentSectorOverrideSnapshots(
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectors,
            Dictionary<long, List<EntityDataRecord>> sectorEntityStates,
            List<SectorOverrideWriteResult> writeResults,
            string sectorOverrideDirectory,
            int snapshotChunkSizeMeters,
            int asyncGeneration,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (sectors == null || sectors.Count <= 0 || writeResults == null)
                return true;

            Dictionary<long, List<PersistentWorldDeltaRecord>>.Enumerator sectorEnumerator = sectors.GetEnumerator();
            try
            {
                while (sectorEnumerator.MoveNext())
                {
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        return false;

                    KeyValuePair<long, List<PersistentWorldDeltaRecord>> pair = sectorEnumerator.Current;
                    List<PersistentWorldDeltaRecord> bucket = pair.Value;
                    if (!TryValidateResidentSectorDeltaBucket(pair.Key, bucket, snapshotChunkSizeMeters, out failureMessage))
                        return false;

                    int bucketCount = bucket.Count;
                    NativeArray<PersistentWorldDeltaRecord> sectorRecords = CreateTrackedTransientArray<PersistentWorldDeltaRecord>(
                        bucketCount,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory,
                        SectorOverrideSnapshotRecordsLabel,
                        NativeAllocationLifetime.TempJob);
                    try
                    {
                        for (int i = 0; i < bucketCount; i++)
                        {
                            sectorRecords[i] = bucket[i];
                        }

                        string tempPath = ResolveSectorOverrideTempPath(sectorOverrideDirectory, pair.Key);
                        if (!SaveBinaryStorage.TryWriteIndexedPersistentWorldSectorOverride(tempPath, pair.Key, sectorRecords, snapshotChunkSizeMeters, out string error))
                        {
                            failureMessage = CreateHexErrorMessage("[PersistentWorldRegistry] Sector override snapshot failed for 0x", pair.Key, error);
                            return false;
                        }

                        string entityStateTempPath = string.Empty;
                        if (sectorEntityStates != null &&
                            sectorEntityStates.TryGetValue(pair.Key, out List<EntityDataRecord> entityStateBucket) &&
                            entityStateBucket != null &&
                            entityStateBucket.Count > 0)
                        {
                            if (!TryWriteResidentSectorEntityStateSnapshot(
                                    pair.Key,
                                    entityStateBucket,
                                    sectorOverrideDirectory,
                                    snapshotChunkSizeMeters,
                                    asyncGeneration,
                                    out entityStateTempPath,
                                    out failureMessage))
                                return false;
                        }

                        writeResults.Add(new SectorOverrideWriteResult(pair.Key, tempPath, entityStateTempPath));
                    }
                    finally
                    {
                        if (sectorRecords.IsCreated)
                        {
                            DisposeTrackedTransientArray(ref sectorRecords);
                        }
                    }
                }
            }
            finally
            {
                sectorEnumerator.Dispose();
            }

            return true;
        }

        private bool TryWriteResidentSectorEntityStateSnapshot(
            long sectorHash,
            List<EntityDataRecord> entityStateBucket,
            string sectorOverrideDirectory,
            int snapshotChunkSizeMeters,
            int asyncGeneration,
            out string entityStateTempPath,
            out string failureMessage)
        {
            entityStateTempPath = string.Empty;
            failureMessage = string.Empty;
            int stateCount = entityStateBucket != null ? entityStateBucket.Count : 0;
            if (stateCount <= 0)
                return true;

            if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                return false;

            if (!TryValidateResidentSectorEntityStateBucket(sectorHash, entityStateBucket, out failureMessage))
                return false;

            NativeArray<EntityDataRecord> sectorStates = CreateTrackedTransientArray<EntityDataRecord>(
                stateCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                SectorOverrideEntityStatesLabel,
                NativeAllocationLifetime.TempJob);
            try
            {
                for (int stateIndex = 0; stateIndex < stateCount; stateIndex++)
                    sectorStates[stateIndex] = entityStateBucket[stateIndex];

                entityStateTempPath = ResolveSectorEntityStateTempPath(sectorOverrideDirectory, sectorHash);
                if (SaveBinaryStorage.TryWriteIndexedSectorEntityStateOverride(
                        entityStateTempPath,
                        sectorHash,
                        sectorStates,
                        snapshotChunkSizeMeters,
                        out string entityStateError))
                {
                    return true;
                }

                failureMessage = CreateHexErrorMessage("[PersistentWorldRegistry] Sector entity-state snapshot failed for 0x", sectorHash, entityStateError);
                entityStateTempPath = string.Empty;
                return false;
            }
            finally
            {
                if (sectorStates.IsCreated)
                {
                    DisposeTrackedTransientArray(ref sectorStates);
                }
            }
        }

        private SectorOverrideReadWindow CaptureSectorOverrideReadWork(in PagedSectorHashWindow desiredSectorHashes)
        {
            SectorOverrideReadWork work0 = default;
            SectorOverrideReadWork work1 = default;
            SectorOverrideReadWork work2 = default;
            SectorOverrideReadWork work3 = default;
            SectorOverrideReadWork work4 = default;
            SectorOverrideReadWork work5 = default;
            SectorOverrideReadWork work6 = default;
            SectorOverrideReadWork work7 = default;
            SectorOverrideReadWork work8 = default;
            int workCount = 0;

            if (!_indexedSectorPagingEnabled ||
                _sectorOverrideStates == null ||
                _sectorOverrideStates.Count <= 0)
            {
                return default;
            }

            for (int i = 0; i < PagedSectorHashCount; i++)
            {
                long sectorHash = desiredSectorHashes[i];
                if (sectorHash == InvalidPagedSectorHash)
                    continue;

                if (!_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) ||
                    state == null)
                {
                    continue;
                }

                SectorOverrideReadWork readWork = new SectorOverrideReadWork(
                    sectorHash,
                    state.TempPath,
                    state.EntityStateTempPath);
                StoreSectorOverrideReadWork(
                    ref workCount,
                    in readWork,
                    ref work0,
                    ref work1,
                    ref work2,
                    ref work3,
                    ref work4,
                    ref work5,
                    ref work6,
                    ref work7,
                    ref work8);
            }

            return new SectorOverrideReadWindow(
                workCount,
                in work0,
                in work1,
                in work2,
                in work3,
                in work4,
                in work5,
                in work6,
                in work7,
                in work8);
        }

        private static void StoreSectorOverrideReadWork(
            ref int workCount,
            in SectorOverrideReadWork readWork,
            ref SectorOverrideReadWork work0,
            ref SectorOverrideReadWork work1,
            ref SectorOverrideReadWork work2,
            ref SectorOverrideReadWork work3,
            ref SectorOverrideReadWork work4,
            ref SectorOverrideReadWork work5,
            ref SectorOverrideReadWork work6,
            ref SectorOverrideReadWork work7,
            ref SectorOverrideReadWork work8)
        {
            switch (workCount)
            {
                case 0:
                    work0 = readWork;
                    break;
                case 1:
                    work1 = readWork;
                    break;
                case 2:
                    work2 = readWork;
                    break;
                case 3:
                    work3 = readWork;
                    break;
                case 4:
                    work4 = readWork;
                    break;
                case 5:
                    work5 = readWork;
                    break;
                case 6:
                    work6 = readWork;
                    break;
                case 7:
                    work7 = readWork;
                    break;
                case 8:
                    work8 = readWork;
                    break;
                default:
                    return;
            }

            workCount++;
        }

        private bool ApplySectorOverrides(
            PersistentWorldDeltaRecord[] loadedSectorRecords,
            in SectorOverrideReadWindow sectorOverrideReadWork,
            int indexedChunkSizeMeters,
            out PersistentWorldDeltaRecord[] mergedRecords,
            out string error)
        {
            mergedRecords = loadedSectorRecords ?? Array.Empty<PersistentWorldDeltaRecord>();
            error = string.Empty;
            // COLD ALLOC: Dictionary<long,List<PersistentWorldDeltaRecord>>[16] Ã¢â‚¬â€ paged sector merge map during override resolution Ã¢â‚¬â€ owner: PersistentWorldRegistry
            Dictionary<long, List<PersistentWorldDeltaRecord>> sectorBuckets = new Dictionary<long, List<PersistentWorldDeltaRecord>>(16);
            int loadedCount = loadedSectorRecords != null ? loadedSectorRecords.Length : 0;
            for (int i = 0; i < loadedCount; i++)
            {
                PersistentWorldDeltaRecord record = loadedSectorRecords[i];
                if (!PersistentWorldDeltaRecord.IsValid(in record))
                {
                    error = "Indexed persistent-world sector merge contains an invalid record.";
                    return false;
                }

                AbsoluteUniversePosition unpackedPosition = record.UnpackPosition(indexedChunkSizeMeters);
                long sectorHash = ComputeSectorHash(in unpackedPosition);
                if (sectorHash == InvalidPagedSectorHash)
                {
                    error = "Indexed persistent-world sector merge contains an invalid AUP sector.";
                    return false;
                }

                if (!sectorBuckets.TryGetValue(sectorHash, out List<PersistentWorldDeltaRecord> bucket))
                {
                    // COLD ALLOC: List<PersistentWorldDeltaRecord>[16] Ã¢â‚¬â€ one paged sector merge bucket Ã¢â‚¬â€ owner: PersistentWorldRegistry
                    bucket = new List<PersistentWorldDeltaRecord>(16);
                    sectorBuckets.Add(sectorHash, bucket);
                }

                bucket.Add(record);
            }

            int readWorkCount = sectorOverrideReadWork.Count;
            for (int i = 0; i < readWorkCount; i++)
            {
                SectorOverrideReadWork readWork = sectorOverrideReadWork[i];
                long sectorHash = readWork.SectorHash;
                if (sectorHash == InvalidPagedSectorHash)
                    continue;

                if (string.IsNullOrEmpty(readWork.TempPath) ||
                    !File.Exists(readWork.TempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedPersistentWorldSectorOverride(readWork.TempPath, out long loadedSectorHash, out PersistentWorldDeltaRecord[] overrideRecords, out error))
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
                    PersistentWorldDeltaRecord overrideRecord = overrideRecords[recordIndex];
                    if (!PersistentWorldDeltaRecord.IsValid(in overrideRecord))
                    {
                        error = "Sector override contains an invalid persistent-world record.";
                        return false;
                    }

                    AbsoluteUniversePosition overridePosition = overrideRecord.UnpackPosition(indexedChunkSizeMeters);
                    long overrideSectorHash = ComputeSectorHash(in overridePosition);
                    if (overrideSectorHash == InvalidPagedSectorHash || overrideSectorHash != sectorHash)
                    {
                        error = "Sector override contains a cross-sector persistent-world record.";
                        return false;
                    }

                    replacement.Add(overrideRecord);
                }

                sectorBuckets[sectorHash] = replacement;
            }

            List<PersistentWorldDeltaRecord> merged = new List<PersistentWorldDeltaRecord>(math.max(loadedCount, 16));
            Dictionary<long, List<PersistentWorldDeltaRecord>>.Enumerator enumerator = sectorBuckets.GetEnumerator();
            while (enumerator.MoveNext())
            {
                List<PersistentWorldDeltaRecord> bucket = enumerator.Current.Value;
                for (int i = 0; i < bucket.Count; i++)
                    merged.Add(bucket[i]);
            }

            enumerator.Dispose();
            mergedRecords = merged.Count > 0 ? merged.ToArray() : Array.Empty<PersistentWorldDeltaRecord>();
            return true;
        }

        private bool TryLoadSectorEntityStateOverrides(
            in SectorOverrideReadWindow sectorOverrideReadWork,
            out Dictionary<uint, EntityDataRecord> stagedEntityStates,
            out string error)
        {
            stagedEntityStates = null;
            error = string.Empty;

            int readWorkCount = sectorOverrideReadWork.Count;
            if (readWorkCount <= 0)
            {
                return true;
            }

            // COLD ALLOC: Dictionary<uint,EntityDataRecord>[64] Ã¢â‚¬â€ staged sector entity-state restore map during indexed paging Ã¢â‚¬â€ owner: PersistentWorldRegistry
            stagedEntityStates = new Dictionary<uint, EntityDataRecord>(64);
            for (int i = 0; i < readWorkCount; i++)
            {
                SectorOverrideReadWork readWork = sectorOverrideReadWork[i];
                long sectorHash = readWork.SectorHash;
                if (sectorHash == InvalidPagedSectorHash)
                    continue;

                if (string.IsNullOrEmpty(readWork.EntityStateTempPath) ||
                    !File.Exists(readWork.EntityStateTempPath))
                {
                    continue;
                }

                if (!SaveBinaryStorage.TryReadIndexedSectorEntityStateOverride(
                        readWork.EntityStateTempPath,
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
                    if (!_floraSpawnStateByInstanceUid.TrySet(pair.Key, pairValue))
                    {
                        WriteWorldTelemetry(
                            WorldTelemetryCapacityMismatch,
                            WorldRegistryFloraSpawnStateKeysBuffer,
                            0u,
                            unchecked((int)pair.Key),
                            _floraSpawnStateByInstanceUid.Capacity,
                            _currentPlayerChunk,
                            pair.Key);
                    }
                    continue;
                }

                if (!_entityStateByInstanceUid.TrySet(pair.Key, pairValue))
                {
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistryEntityStateKeysBuffer,
                        0u,
                        unchecked((int)pair.Key),
                        _entityStateByInstanceUid.Capacity,
                        _currentPlayerChunk,
                        pair.Key);
                }
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

        private void MarkResidentSectorOverrides(in PagedSectorHashWindow desiredSectorHashes)
        {
            if (_sectorOverrideStates == null || _sectorOverrideStates.Count <= 0)
                return;

            Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.IsResident = false;
            enumerator.Dispose();

            for (int i = 0; i < PagedSectorHashCount; i++)
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
            int asyncGeneration = _indexedSectorAsyncGeneration != 0
                ? _indexedSectorAsyncGeneration
                : AdvanceIndexedSectorAsyncGeneration();
            _sectorOverrideCommitInFlight = true;
            _sectorOverrideCommitInFlightGeneration = asyncGeneration;
            _ = RunSectorOverrideCommitAsync(asyncGeneration);
        }

        private async Awaitable RunSectorOverrideCommitAsync(int asyncGeneration)
        {
            SectorOverrideCommitWork[] commitWork = _sectorOverrideCommitWorkBuffer;
            int commitWorkCount = 0;
            try
            {
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                float now = Time.unscaledTime;

                Dictionary<long, SectorOverrideState>.Enumerator enumerator = _sectorOverrideStates.GetEnumerator();
                while (enumerator.MoveNext() && commitWorkCount < MaxSectorOverrideCommitsPerPass)
                {
                    KeyValuePair<long, SectorOverrideState> pair = enumerator.Current;
                    SectorOverrideState state = pair.Value;
                    if (state == null || state.IsResident || string.IsNullOrEmpty(state.TempPath))
                        continue;

                    if (now - state.LastUnloadedTime >= SectorOverrideCommitDelaySeconds)
                    {
                        commitWork[commitWorkCount] = new SectorOverrideCommitWork(pair.Key, state.TempPath, state.EntityStateTempPath);
                        commitWorkCount++;
                    }
                }
                enumerator.Dispose();

                if (commitWorkCount <= 0)
                    return;

                string indexedSectorSavePath = _indexedSectorSavePath;
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                await Awaitable.BackgroundThreadAsync();
                for (int i = 0; i < commitWorkCount; i++)
                {
                    if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                        break;

                    SectorOverrideCommitWork work = commitWork[i];
                    if (string.IsNullOrEmpty(work.TempPath) || !File.Exists(work.TempPath))
                    {
                        work.Error = CreateHexMessage("[PersistentWorldRegistry] Sector override temp block vanished for 0x", work.SectorHash, ".");
                        commitWork[i] = work;
                        continue;
                    }

                    if (!SaveBinaryStorage.TryCommitIndexedPersistentWorldSectorOverride(indexedSectorSavePath, work.TempPath, out string error))
                    {
                        work.Error = CreateHexErrorMessage("[PersistentWorldRegistry] Sector override commit failed for 0x", work.SectorHash, error);
                        commitWork[i] = work;
                        continue;
                    }

                    work.Committed = true;
                    if (!string.IsNullOrEmpty(work.EntityStateTempPath))
                    {
                        work.EntityStateDeleted = TryDeleteFileIfExists(work.EntityStateTempPath);
                        if (!work.EntityStateDeleted)
                            work.Error = CreateHexMessage("[PersistentWorldRegistry] Sector entity-state temp delete failed for 0x", work.SectorHash, ".");
                    }

                    commitWork[i] = work;
                }

                await Awaitable.MainThreadAsync();
                if (!IsIndexedSectorAsyncGenerationCurrent(asyncGeneration))
                    return;

                for (int i = 0; i < commitWorkCount; i++)
                {
                    SectorOverrideCommitWork work = commitWork[i];
                    if (!string.IsNullOrEmpty(work.Error))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Hecton8.Core.H8Debug.LogError(work.Error);
#endif
                    }

                    if (!work.Committed)
                        continue;

                    if (_sectorOverrideStates.TryGetValue(work.SectorHash, out SectorOverrideState state) &&
                        state != null)
                    {
                        if (state.TempPath == work.TempPath)
                            state.TempPath = string.Empty;

                        if (state.EntityStateTempPath == work.EntityStateTempPath && work.EntityStateDeleted)
                            state.EntityStateTempPath = string.Empty;
                    }
                }

                for (int i = 0; i < commitWorkCount; i++)
                {
                    long sectorHash = commitWork[i].SectorHash;
                    if (_sectorOverrideStates.TryGetValue(sectorHash, out SectorOverrideState state) &&
                        state != null &&
                        !state.IsResident &&
                        string.IsNullOrEmpty(state.TempPath) &&
                        string.IsNullOrEmpty(state.EntityStateTempPath))
                    {
                        _sectorOverrideStates.Remove(sectorHash);
                    }
                }
            }
            finally
            {
                if (_sectorOverrideCommitInFlightGeneration == asyncGeneration)
                {
                    _sectorOverrideCommitInFlight = false;
                    _sectorOverrideCommitInFlightGeneration = 0;
                }
            }
        }

        private static bool TryDeleteFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            AsyncWriteManager.InvalidateCachedReadWindows(path);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(path);
            }
        }

        private string ResolveSectorOverrideTempPath(long sectorHash)
        {
            return ResolveSectorOverrideTempPath(_indexedSectorOverrideDirectory, sectorHash);
        }

        private static string ResolveSectorOverrideTempPath(string sectorOverrideDirectory, long sectorHash)
        {
            if (string.IsNullOrEmpty(sectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(sectorOverrideDirectory, CreateSectorTempFileName(sectorHash, SectorOverrideTempFileSuffix));
        }

        private string ResolveSectorEntityStateTempPath(long sectorHash)
        {
            return ResolveSectorEntityStateTempPath(_indexedSectorOverrideDirectory, sectorHash);
        }

        private static string ResolveSectorEntityStateTempPath(string sectorOverrideDirectory, long sectorHash)
        {
            if (string.IsNullOrEmpty(sectorOverrideDirectory))
                return string.Empty;

            return Path.Combine(sectorOverrideDirectory, CreateSectorTempFileName(sectorHash, SectorEntityStateTempFileSuffix));
        }

        bool IFaunaPersistentWorldStateService.TryCacheFaunaHibernationState(in EntityDataRecord faunaState)
        {
            return TryCacheFaunaHibernationState(in faunaState);
        }

        bool IFaunaPersistentWorldStateService.TryCacheFaunaEggState(in EntityDataRecord eggState)
        {
            return TryCacheFaunaEggState(in eggState);
        }

        int IFaunaPersistentWorldStateService.ConsumeCachedFaunaHibernationStates(
            in AbsoluteUniversePosition playerAup,
            float restoreRadiusMeters,
            List<EntityDataRecord> destination)
        {
            return ConsumeCachedFaunaHibernationStates(in playerAup, restoreRadiusMeters, destination);
        }

        int IFaunaPersistentWorldStateService.MigrateApexFaunaHibernationStatesToward(
            in AbsoluteUniversePosition attractorAup,
            float searchRadiusMeters,
            float stepMeters)
        {
            return MigrateApexFaunaHibernationStatesToward(in attractorAup, searchRadiusMeters, stepMeters);
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

        internal float UpdateWhaleFallSpawnInfluence01(Vector3 worldPosition, float currentTimeSeconds, float radiusMeters)
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

            if (!_indexedSectorPagingEnabled || string.IsNullOrEmpty(_indexedSectorOverrideDirectory))
                return false;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in entityState.Position);
            long sectorHash = ComputeSectorHash(in position);
            if (sectorHash == InvalidPagedSectorHash)
                return false;

            if (!deferMemoryRegistrationUntilWrite)
                RegisterSpecialEntityStateInMemory(in entityState);

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

            if (!TryQuantizeSector(in playerAup, out int2 playerSector))
                return 0;

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
                                GetFaunaEggHatchTimeSeconds(in entityState) > ResolveWorldClockSeconds())
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
                                ResolveWorldClockSeconds(),
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
                        if (!TryDeleteFileIfExists(entityStateTempPath))
                            continue;

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
                    if (TombstoneHibernatedFaunaVictim(in state))
                    {
                        changedRecords++;
                    }
                    else
                    {
                        _entityStateScratch.Add(state);
                    }

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
                if (!TryDeleteFileIfExists(entityStateTempPath))
                    return 0;
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

            if (!TryQuantizeSector(in attractorAup, out int2 centerSector))
                return 0;

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

                        double3 toAttractor = AUPMath.AUPDeltaClamped(in attractorAup, in currentAup);
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

                        AbsoluteUniversePosition migratedAup = currentAup.OffsetMeters(moveDelta);
                        if (!migratedAup.IsFinite())
                        {
                            WriteWorldTelemetry(
                                WorldTelemetryInvalidAup,
                                WorldRegistryEntityStateValuesBuffer,
                                0u,
                                0,
                                _entityStateByInstanceUid.Capacity,
                                _currentPlayerChunk,
                                state.InstanceUid);
                            _entityStateScratch.Add(state);
                            continue;
                        }

                        EntityDataRecord migratedState = state;
                        migratedState.Position = migratedAup.ToAlignedBlit();
                        long migratedSectorHash = ComputeSectorHash(in migratedAup);
                        if (migratedSectorHash == InvalidPagedSectorHash)
                        {
                            _entityStateScratch.Add(state);
                            continue;
                        }

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

            if (!TryDeleteFileIfExists(entityStateTempPath))
                return false;

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

            if (!_entityStateByInstanceUid.TrySet(entityState.InstanceUid, entityState))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryEntityStateKeysBuffer,
                    0u,
                    unchecked((int)entityState.InstanceUid),
                    _entityStateByInstanceUid.Capacity,
                    _currentPlayerChunk,
                    entityState.InstanceUid);
                return;
            }

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

        private bool TombstoneHibernatedFaunaVictim(in EntityDataRecord entityState)
        {
            if (!TryRegisterFaunaTombstone(entityState.InstanceUid))
                return false;

            if (_entityStateByInstanceUid.IsCreated)
                _entityStateByInstanceUid.Remove(entityState.InstanceUid);

            return true;
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
                if (!seededAup.IsFinite())
                {
                    WriteWorldTelemetry(
                        WorldTelemetryInvalidAup,
                        WorldRegistryEntityStateValuesBuffer,
                        0u,
                        0,
                        _entityStateByInstanceUid.Capacity,
                        _currentPlayerChunk,
                        instanceUid);
                    continue;
                }

                EntityDataRecord seededState = CreateFaunaHibernationState(
                    instanceUid,
                    GetFaunaHibernationSpeciesId(in template),
                    GetFaunaHibernationHealth(in template),
                    in seededAup,
                    false,
                    predator,
                    ResolveWorldClockSeconds(),
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

            if (chunkSizeMeters <= 0)
            {
                WriteWorldTelemetry(
                    WorldTelemetryInvalidAup,
                    WorldRegistryEntityStateValuesBuffer,
                    0u,
                    0,
                    _entityStateByInstanceUid.Capacity,
                    _currentPlayerChunk,
                    0u);
                return false;
            }

            NativeArray<EntityDataRecord> sectorStates = CreateTrackedTransientArray<EntityDataRecord>(
                entityStates.Count,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory,
                SectorEntityStateAsyncWriteStatesLabel,
                NativeAllocationLifetime.Temp);
            try
            {
                for (int i = 0; i < entityStates.Count; i++)
                    sectorStates[i] = entityStates[i];

                if (!SaveBinaryStorage.TryWriteIndexedSectorEntityStateOverride(
                        entityStateTempPath,
                        sectorHash,
                        sectorStates,
                        chunkSizeMeters,
                        out string error))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError(CreateHexErrorMessage("[PersistentWorldRegistry] Entity-state temp write failed for 0x", sectorHash, error));
#endif
                    return false;
                }

                return true;
            }
            finally
            {
                if (sectorStates.IsCreated)
                {
                    DisposeTrackedTransientArray(ref sectorStates);
                }
            }
        }

        private static unsafe void DisposeTrackedTransientArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
            {
                array = default;
                return;
            }

            Exception firstException = null;
            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);

            if (trackedPointer != null)
            {
                try
                {
                    NativeMemorySentinel.UnregisterPointer(trackedPointer);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            try
            {
                array.Dispose();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }
            finally
            {
                array = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void DisposeTrackedTransientNativeList<T>(ref NativeList<T> list, ref int sentinelId) where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (list.IsCreated)
            {
                try
                {
                    list.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    list = default;
                }
            }
            else
            {
                list = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static NativeArray<T> CreateTrackedTransientArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            try
            {
                RegisterTrackedTransientArray(array, label, lifetime);
                return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();
                throw;
            }
        }

        private static void RegisterTrackedTransientArray<T>(
            NativeArray<T> array,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, MemoryBudgetOwnerName, label, lifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"NativeMemorySentinel rejected persistent world transient array registration for {label}.");
        }

        private static void RegisterTrackedTransientNativeList<T>(
            NativeList<T> list,
            string label,
            NativeAllocationLifetime lifetime,
            out int sentinelId) where T : unmanaged
        {
            sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, MemoryBudgetOwnerName, label, lifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"NativeMemorySentinel rejected persistent world transient list registration for {label}.");
        }

        private static void PublishSectorCompressionPerformanceWarning(
            uint warningHash,
            uint contextHash,
            float scalarValue,
            ref int lastTelemetryFrame)
        {
            if (!Application.isPlaying)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (lastTelemetryFrame == frame)
                return;

            lastTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, scalarValue);
        }

        private void TryPushSectorCorruptionNotification()
        {
            if (Hecton8.UI.NotificationEvents.TryPushCritical(LocalizedSectorCorruptionMessage.AsSpan()))
                return;

            ReportSectorCorruptionNotificationMiss();
        }

        private void ReportSectorCorruptionNotificationMiss()
        {
            _sectorCorruptionNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _sectorCorruptionNotificationMissWarningHash,
                _sectorCorruptionNotificationContextHash,
                math.max(1, _sectorCorruptionNotificationMissCount));
        }

        private void ClearSectorCorruptionNotificationDiagnostics()
        {
            _sectorCorruptionNotificationMissCount = 0;
        }

        private static float StopwatchTicksToMilliseconds(long elapsedTicks)
        {
            long clampedTicks = elapsedTicks > 0L ? elapsedTicks : 0L;
            return (float)((double)clampedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private static long ComputeSectorHash(in AbsoluteUniversePosition position)
        {
            return TryQuantizeSector(in position, out int2 sectorCoord)
                ? PackSectorHash(sectorCoord)
                : InvalidPagedSectorHash;
        }

        private bool RegisterOrUpdatePoolSlot(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (!IsValidPoolIndex(recordIndex) || !_guidToPoolIndex.IsCreated)
                return false;

            PoolSlotData slotData = _poolSlotData[recordIndex];
            ulong nextGuid = ComputePoolGuid(in record);
            if (nextGuid == 0UL)
                return false;

            ulong previousGuid = slotData.BoundGuid;
            bool hadPreviousGuid = previousGuid != 0UL &&
                                   previousGuid != nextGuid &&
                                   _guidToPoolIndex.TryGetValue(previousGuid, out int previousIndex) &&
                                   previousIndex == recordIndex;
            bool hasNextGuid = _guidToPoolIndex.TryGetValue(nextGuid, out int nextIndex);
            if (hasNextGuid && nextIndex != recordIndex)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryGuidToPoolIndexKeysBuffer,
                    0u,
                    nextIndex,
                    _guidToPoolIndex.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
                return false;
            }

            bool guidPublished = hadPreviousGuid
                ? _guidToPoolIndex.TrySetReplacing(previousGuid, nextGuid, recordIndex)
                : _guidToPoolIndex.TrySet(nextGuid, recordIndex);
            if (!guidPublished)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryGuidToPoolIndexKeysBuffer,
                    0u,
                    recordIndex,
                    _guidToPoolIndex.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
                return false;
            }

            slotData.BoundGuid = nextGuid;
            WritePoolSlotPosition(ref slotData, in record.Position);
            if (TryWritePoolSlotDataAt(recordIndex, in slotData, record.InstanceUid))
                return true;

            bool rollbackSucceeded = true;
            if (hadPreviousGuid)
                rollbackSucceeded = _guidToPoolIndex.TrySetReplacing(nextGuid, previousGuid, recordIndex);
            else if (!hasNextGuid)
                rollbackSucceeded = _guidToPoolIndex.Remove(nextGuid);

            if (!rollbackSucceeded)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryGuidToPoolIndexKeysBuffer,
                    0u,
                    -1,
                    _guidToPoolIndex.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
            }

            return false;
        }

        private bool RegisterOrUpdateEntityState(in PersistentWorldItemRecord record)
        {
            EntityDataRecord state = CreateEntityStateFromRecord(in record);
            return RegisterOrUpdateEntityState(in record, in state);
        }

        private bool RegisterOrUpdateEntityState(in PersistentWorldItemRecord record, in EntityDataRecord state)
        {
            if (record.InstanceUid == 0u || !_entityStateByInstanceUid.IsCreated)
                return false;

            if (!_entityStateByInstanceUid.TrySet(record.InstanceUid, state))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryEntityStateKeysBuffer,
                    0u,
                    unchecked((int)record.InstanceUid),
                    _entityStateByInstanceUid.Capacity,
                    record.ChunkId,
                    record.InstanceUid);
                return false;
            }

            return true;
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

        private bool TryResolvePlatformInheritedVelocity(Vector3 runtimePosition, out Vector3 inheritedVelocity)
        {
            inheritedVelocity = Vector3.zero;
            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            position = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return position.IsFinite();
        }

        private bool TryResolveRegistryChunkId(in AbsoluteUniversePosition position, uint instanceUid, out int3 chunkId)
        {
            chunkId = AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters);
            if (AbsoluteUniversePosition.IsValidChunkId(chunkId))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryInvalidAup,
                WorldRegistryRecordsBuffer,
                0u,
                0,
                0,
                chunkId,
                instanceUid);
            return false;
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
            if (!_spawnImpulseByInstanceUid.TryAdd(instanceUid, impulse))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistrySpawnImpulseKeysBuffer,
                    0u,
                    unchecked((int)instanceUid),
                    _spawnImpulseByInstanceUid.Capacity,
                    _currentPlayerChunk,
                    instanceUid);
            }
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
            if (!_spawnVelocityChangeByInstanceUid.TryAdd(instanceUid, velocityChange))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistrySpawnVelocityKeysBuffer,
                    0u,
                    unchecked((int)instanceUid),
                    _spawnVelocityChangeByInstanceUid.Capacity,
                    _currentPlayerChunk,
                    instanceUid);
            }
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
            int recordIndex,
            in PersistentWorldItemRecord record,
            GameObject instance,
            in AbsoluteUniversePosition position)
        {
            EntityDataRecord state = ResolveEntityState(in record);
            state.Position = position.ToAlignedBlit();
            state.InstanceUid = record.InstanceUid;
            state.Quantity = math.max(1, record.Quantity);

            if (instance != null && IsValidRecordIndex(recordIndex) && TryGetPoolIndex(in record, out int poolIndex))
            {
                PickupItem pickupItem = _poolSlotPickupItems[poolIndex];
                HectonItem hectonItem = _poolSlotHectonItems[poolIndex];
                if (pickupItem != null)
                {
                    state.Quantity = math.max(1, pickupItem.Quantity);
                    state.Position.Reserved = pickupItem.GeneticsMask;
                    state.InventoryHash = unchecked((int)pickupItem.GeneticsMask);
                    state.Integrity01 = ResolveItemQuality01(pickupItem.QualityMilli);
                }
                else if (hectonItem != null)
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
            slotData.GridX = position.GridX;
            slotData.GridY = position.GridY;
            slotData.GridZ = position.GridZ;
            slotData.LocalOffset = new float3(position.LocalX, position.LocalY, position.LocalZ);
        }

        private static AbsoluteUniversePosition ReadPoolSlotPosition(in PoolSlotData slotData)
        {
            return AbsoluteUniversePosition.FromGridLocal(
                slotData.GridX,
                slotData.GridY,
                slotData.GridZ,
                slotData.LocalOffset);
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

            if (_dehydrateQueue.Count >= _dehydrateQueue.Capacity)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDehydrateQueueValuesBuffer,
                    0u,
                    _dehydrateQueue.Count,
                    _dehydrateQueue.Capacity,
                    _currentPlayerChunk,
                    record.InstanceUid);
                return;
            }

            slotData.StateFlags |= queuedMask;
            if (!TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid))
                return;

            if (!_dehydrateQueue.Enqueue(recordIndex))
            {
                slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.DehydrationQueued);
                TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid);
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDehydrateQueueValuesBuffer,
                    0u,
                    _dehydrateQueue.Count,
                    _dehydrateQueue.Capacity,
                    _currentPlayerChunk,
                    record.InstanceUid);
            }
        }

        private void QueueRecordForHydration(int recordIndex, in PersistentWorldItemRecord record, in AbsoluteUniversePosition playerAup)
        {
            if (!_pendingHydrationRecords.IsCreated ||
                !IsValidRecordIndex(recordIndex) ||
                PersistentWorldItemRecord.IsCollected(in record) ||
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
            if (!TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid))
                return;

            if (!_pendingHydrationRecords.AddNoResize(recordIndex))
            {
                slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.HydrationQueued);
                TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid);
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryPendingHydrationRecordsBuffer,
                    0u,
                    _pendingHydrationRecords.Length,
                    _pendingHydrationRecords.Capacity,
                    _currentPlayerChunk,
                    record.InstanceUid);
                return;
            }

            QueueWorldPrefabPrewarmForRecord(in record);
            EnsureHydrationSessionScheduled();
        }

        private void QueueWorldPrefabPrewarmForRecord(in PersistentWorldItemRecord record)
        {
            if (!TryResolveCachedItemData(in record, out ItemData itemData) || itemData == null || _resolvedItemCatalog == null)
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

            _hydrationSessionStartPending = true;
        }

        private void TryRunPendingHydrationSessionLateFrame()
        {
            if (!_pendingHydrationRecords.IsCreated)
                return;

            if (!_hydrationSessionRunning && _hydrationSessionStartPending)
            {
                CompactPendingHydrationQueueIfDrained();
                if (_pendingHydrationReadIndex >= _pendingHydrationRecords.Length)
                {
                    _hydrationSessionStartPending = false;
                    return;
                }

                _hydrationSessionStartPending = false;
                _hydrationSessionRunning = true;
                _hydrationSessionVersion++;
            }

            if (!_hydrationSessionRunning)
                return;

            if (TryProcessHydrationBurst())
                return;

            _hydrationSessionRunning = false;
            CompactPendingHydrationQueueIfDrained();
            if (_pendingHydrationRecords.IsCreated && _pendingHydrationReadIndex < _pendingHydrationRecords.Length)
                _hydrationSessionStartPending = true;
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

                int recordIndex = _pendingHydrationRecords[_pendingHydrationReadIndex];

                if (!IsValidRecordIndex(recordIndex))
                {
                    _pendingHydrationReadIndex++;
                    processedCount++;
                    continue;
                }

                PersistentWorldItemRecord record = _records[recordIndex];
                if (!ClearHydrationQueuedFlag(in record))
                    break;

                _pendingHydrationReadIndex++;
                processedCount++;
                if (PersistentWorldItemRecord.IsCollected(in record) || IsDeletedInstanceUid(record.InstanceUid))
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

            if (TryClearPendingHydrationRecords())
                _pendingHydrationReadIndex = 0;
        }

        private void CancelHydrationSession(bool clearQueue)
        {
            _hydrationSessionVersion++;
            _hydrationSessionRunning = false;
            _hydrationSessionStartPending = false;

            if (!clearQueue || !_pendingHydrationRecords.IsCreated)
            {
                _pendingHydrationReadIndex = 0;
                return;
            }

            if (TryClearPendingHydrationRecords())
            {
                _pendingHydrationReadIndex = 0;
                return;
            }

            _pendingHydrationReadIndex = _pendingHydrationRecords.Length;
        }

        private bool ClearHydrationQueuedFlag(in PersistentWorldItemRecord record)
        {
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return true;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.HydrationQueued);
            return TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid);
        }

        private void DrainDehydrateQueue(int maxDequeueCount)
        {
            if (!_dehydrateQueue.IsCreated)
                return;

            int dequeueBudget = math.max(1, maxDequeueCount);
            while (dequeueBudget-- > 0 && _dehydrateQueue.TryDequeue(out int recordIndex))
            {
                if (!ClearDehydrationQueuedFlag(recordIndex))
                {
                    if (!_dehydrateQueue.Enqueue(recordIndex))
                    {
                        WriteWorldTelemetry(
                            WorldTelemetryCapacityMismatch,
                            WorldRegistryDehydrateQueueValuesBuffer,
                            0u,
                            _dehydrateQueue.Count,
                            _dehydrateQueue.Capacity,
                            _currentPlayerChunk,
                            0u);
                    }

                    break;
                }

                DehydrateRecord(recordIndex, syncTransformBackToRecord: true);
            }
        }

        private bool ClearDehydrationQueuedFlag(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
                return true;

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
                return true;

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.StateFlags &= unchecked((byte)~(byte)PoolSlotStateFlags.DehydrationQueued);
            return TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid);
        }

        private bool ClearHydratedSlot(int recordIndex)
        {
            if (!IsValidRecordIndex(recordIndex))
            {
                _hydratedInstancesByRecordIndex.Remove(recordIndex);
                return true;
            }

            PersistentWorldItemRecord record = _records[recordIndex];
            if (!TryGetPoolIndex(in record, out int poolIndex))
            {
                _hydratedInstancesByRecordIndex.Remove(recordIndex);
                return true;
            }

            PoolSlotData slotData = _poolSlotData[poolIndex];
            slotData.RefCount = 0;
            slotData.StableFrames = 0;
            slotData.StateFlags &= unchecked((byte)~((byte)PoolSlotStateFlags.Hydrated |
                                                     (byte)PoolSlotStateFlags.Dirty |
                                                     (byte)PoolSlotStateFlags.Settled |
                                                     (byte)PoolSlotStateFlags.HydrationQueued |
                                                     (byte)PoolSlotStateFlags.DehydrationQueued));
            if (!TryWritePoolSlotDataAt(poolIndex, in slotData, record.InstanceUid))
                return false;

            _hydratedInstancesByRecordIndex.Remove(recordIndex);
            _hydratedInstancesBySlot[poolIndex] = null;
            _poolSlotTransforms[poolIndex] = null;
            _poolSlotRigidbodies[poolIndex] = null;
            _poolSlotPickupItems[poolIndex] = null;
            _poolSlotHectonItems[poolIndex] = null;
            return true;
        }

        private bool ResetPoolSlots()
        {
            bool cleared = true;
            if (_guidToPoolIndex.IsCreated)
                cleared &= _guidToPoolIndex.Clear();

            if (_entityStateByInstanceUid.IsCreated)
                cleared &= _entityStateByInstanceUid.Clear();

            ClearWhaleFallPoiInfluenceUidIndex();
            ClearApexMigrationVisitedScratch();

            if (_floraSpawnStateByInstanceUid.IsCreated)
                cleared &= _floraSpawnStateByInstanceUid.Clear();

            if (_deltaRecordIndexByEntityId.IsCreated)
                cleared &= _deltaRecordIndexByEntityId.Clear();

            if (_deletedInstanceUids.IsCreated)
                cleared &= _deletedInstanceUids.Clear();

            if (_resourceNodeTombstoneIds.IsCreated)
                cleared &= _resourceNodeTombstoneIds.Clear();

            if (_resourceNodeMetamorphosedIds.IsCreated)
                cleared &= _resourceNodeMetamorphosedIds.Clear();

            if (_deltaChunkIndexByChunkId.IsCreated)
                cleared &= _deltaChunkIndexByChunkId.Clear();

            if (_deltaChunkIds.IsCreated)
                cleared &= _deltaChunkIds.Clear();

            if (_deltaItemIndexByHash.IsCreated)
                cleared &= _deltaItemIndexByHash.Clear();

            if (_deltaItemHashes.IsCreated)
                cleared &= _deltaItemHashes.Clear();

            if (_deltaRecordsByChunk.IsCreated)
                cleared &= _deltaRecordsByChunk.Clear();

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
                    Hecton8.Core.H8Debug.LogError(
                        CreateRecordCountWatchdogMessage(_records.Length));
#endif

                    while (_dehydrateQueue.TryDequeue(out _))
                    {
                    }
                }
            }

            if (_pendingHydrationRecords.IsCreated)
            {
                cleared &= _pendingHydrationRecords.Clear();
                _pendingHydrationReadIndex = 0;
            }

            if (_poolSlotData.IsCreated)
                cleared &= _poolSlotData.Clear();

            if (_hydratedInstancesBySlot != null)
                Array.Clear(_hydratedInstancesBySlot, 0, _hydratedInstancesBySlot.Length);

            if (_poolSlotTransforms != null)
                Array.Clear(_poolSlotTransforms, 0, _poolSlotTransforms.Length);

            if (_poolSlotRigidbodies != null)
                Array.Clear(_poolSlotRigidbodies, 0, _poolSlotRigidbodies.Length);

            if (_poolSlotPickupItems != null)
                Array.Clear(_poolSlotPickupItems, 0, _poolSlotPickupItems.Length);

            if (_poolSlotHectonItems != null)
                Array.Clear(_poolSlotHectonItems, 0, _poolSlotHectonItems.Length);

            _hydratedInstancesByRecordIndex?.Clear();
            return cleared;
        }

        private void RegisterNativeMemorySentinelAllocations()
        {
            PrewarmDehydrateQueue();
        }

        private void PrewarmDehydrateQueue()
        {
            if (!_dehydrateQueue.IsCreated)
                return;

            int safeCapacity = math.max(0, maxTrackedItems);
            for (int i = 0; i < safeCapacity; i++)
            {
                if (!_dehydrateQueue.Enqueue(default))
                    break;
            }

            while (_dehydrateQueue.TryDequeue(out _))
            {
            }
        }

        private void RegisterPersistentMemoryBudget()
        {
            long totalBytes =
                _records.EstimatedBytes +
                _recordsByChunk.EstimatedBytes +
                _deltaRecords.EstimatedBytes +
                _deltaRecordIndexByEntityId.EstimatedBytes +
                _deletedInstanceUids.EstimatedBytes +
                _resourceNodeTombstoneIds.EstimatedBytes +
                _resourceNodeMetamorphosedIds.EstimatedBytes +
                _deltaChunkIndexByChunkId.EstimatedBytes +
                _deltaChunkIds.EstimatedBytes +
                _deltaItemIndexByHash.EstimatedBytes +
                _deltaItemHashes.EstimatedBytes +
                _deltaRecordsByChunk.EstimatedBytes +
                _tombstoneDecayExpiredIndices.EstimatedBytes +
                _saveSnapshotDeltas.EstimatedBytes +
                _poolSlotData.EstimatedBytes +
                _guidToPoolIndex.EstimatedBytes +
                _entityStateByInstanceUid.EstimatedBytes +
                _floraSpawnStateByInstanceUid.EstimatedBytes +
                _spawnImpulseByInstanceUid.EstimatedBytes +
                _spawnVelocityChangeByInstanceUid.EstimatedBytes +
                _dehydrateQueue.EstimatedBytes +
                _pendingHydrationRecords.EstimatedBytes +
                _worldTelemetryRing.EstimatedBytes +
                _worldTelemetryCursor.EstimatedBytes;
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentMemoryBudgetBytes);
        }

        private void WriteWorldTelemetry(
            uint eventCode,
            BufferID bufferId,
            uint generation,
            int actualLength,
            int expectedLength,
            int3 chunkId,
            uint instanceUid,
            uint flags = 0u,
            float microseconds = 0f)
        {
            if (!_worldTelemetryRing.IsCreated || !_worldTelemetryCursor.IsCreated)
                return;

            int cursor = _worldTelemetryCursor[0];
            if ((uint)cursor >= WorldTelemetryRingLength)
                cursor = 0;

            WorldTelemetryEntry entry = new WorldTelemetryEntry
            {
                Sequence = ++_worldTelemetrySequence,
                BufferId = (uint)bufferId,
                Generation = generation,
                SystemId = (uint)WorldRegistryVaultOwner,
                EventCode = eventCode,
                ActualLength = actualLength,
                ExpectedLength = expectedLength,
                ChunkId = chunkId,
                Microseconds = microseconds,
                InstanceUid = instanceUid,
                Flags = flags,
                Reserved0 = 0u,
                Reserved1 = 0u
            };

            if (!_worldTelemetryRing.TryWrite(cursor, entry))
                return;

            _worldTelemetryCursor.TryWrite(0, (cursor + 1) % WorldTelemetryRingLength);

            if (eventCode == WorldTelemetryStaleGeneration ||
                eventCode == WorldTelemetryCapacityMismatch ||
                eventCode == WorldTelemetryInvalidAup)
            {
                RequestWorldTelemetryDump();
            }
        }

        private void RequestWorldTelemetryDump()
        {
            if (Interlocked.CompareExchange(ref _worldTelemetryDumpQueued, 1, 0) != 0)
                return;
        }

        private void CaptureQueuedWorldTelemetryDumpSnapshotCold()
        {
            if (Interlocked.CompareExchange(ref _worldTelemetryDumpQueued, 2, 1) != 1)
                return;

            if (!_worldTelemetryRing.IsCreated ||
                _worldTelemetryDumpSnapshot == null ||
                _worldTelemetryDumpSnapshotLock == null)
            {
                Interlocked.Exchange(ref _worldTelemetryDumpQueued, 0);
                return;
            }

            lock (_worldTelemetryDumpSnapshotLock)
            {
                if (!_worldTelemetryRing.TryCopyTo(
                        _worldTelemetryDumpSnapshot,
                        WorldTelemetryRingLength,
                        out int count))
                {
                    Interlocked.Exchange(ref _worldTelemetryDumpQueued, 0);
                    return;
                }

                _worldTelemetryDumpSnapshotCount = count;
            }

            Interlocked.Exchange(ref _worldTelemetryDumpQueued, 0);
        }

        private void InitializeWorldTelemetryDumpWorkerCold()
        {
            if (_worldTelemetryDumpSnapshot == null)
                _worldTelemetryDumpSnapshot = new WorldTelemetryEntry[WorldTelemetryRingLength];

            if (_worldTelemetryDumpSnapshotLock == null)
                _worldTelemetryDumpSnapshotLock = new object();
        }

        private void ShutdownWorldTelemetryDumpWorkerCold()
        {
            CaptureQueuedWorldTelemetryDumpSnapshotCold();
            _worldTelemetryDumpSnapshot = null;
            _worldTelemetryDumpSnapshotLock = null;
            _worldTelemetryDumpSnapshotCount = 0;
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

        private bool TryClearSaveSnapshotDeltas()
        {
            if (!_saveSnapshotDeltas.IsCreated)
                return false;

            if (_saveSnapshotDeltas.Clear())
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistrySaveSnapshotDeltasBuffer,
                0u,
                _saveSnapshotDeltas.Length,
                _saveSnapshotDeltas.Capacity,
                _currentPlayerChunk,
                0u);
            return false;
        }

        private bool TryClearPendingHydrationRecords()
        {
            if (!_pendingHydrationRecords.IsCreated)
                return false;

            if (_pendingHydrationRecords.Clear())
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryPendingHydrationRecordsBuffer,
                0u,
                _pendingHydrationRecords.Length,
                _pendingHydrationRecords.Capacity,
                _currentPlayerChunk,
                0u);
            return false;
        }

        private bool TryWriteRecordAt(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (_records.TryWrite(recordIndex, record))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryRecordsBuffer,
                0u,
                recordIndex,
                _records.IsCreated ? _records.Length : 0,
                record.ChunkId,
                record.InstanceUid);
            return false;
        }

        private bool TryWriteCompactDeltaRecordAt(int deltaIndex, in PersistentWorldCompactDeltaRecord compactRecord)
        {
            if (_deltaRecords.TryWrite(deltaIndex, compactRecord))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryDeltaRecordsBuffer,
                0u,
                deltaIndex,
                _deltaRecords.IsCreated ? _deltaRecords.Length : 0,
                _currentPlayerChunk,
                compactRecord.InstanceUid);
            return false;
        }

        private bool TryWritePoolSlotDataAt(int poolIndex, in PoolSlotData slotData, uint instanceUid)
        {
            if (_poolSlotData.TryWrite(poolIndex, slotData))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryPoolSlotDataBuffer,
                0u,
                poolIndex,
                _poolSlotData.IsCreated ? _poolSlotData.Length : 0,
                _currentPlayerChunk,
                instanceUid);
            return false;
        }

        private bool UpsertDeltaRecord(in PersistentWorldItemRecord record)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();
            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || record.InstanceUid == 0u)
                return false;

            if (PersistentWorldItemRecord.IsDeleted(in record))
                return UpsertDeletedTombstone(in record);

            if (!TryBuildCompactDeltaRecord(in record, out PersistentWorldCompactDeltaRecord compactRecord))
                return false;

            if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int deltaIndex))
            {
                if (!TryWriteCompactDeltaRecordAt(deltaIndex, in compactRecord))
                    return false;
            }
            else if (!TryAppendCompactDeltaRecord(record.InstanceUid, in compactRecord))
            {
                return false;
            }

            UnregisterDeletedInstanceUid(record.InstanceUid);
            RebuildDeltaChunkLookup();
            return true;
        }

        private bool TryAppendRecordWithChunk(in PersistentWorldItemRecord record, out int recordIndex)
        {
            recordIndex = -1;
            if (!_records.IsCreated || !_recordsByChunk.IsCreated || _records.Length >= _records.Capacity)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryRecordsBuffer,
                    0u,
                    _records.IsCreated ? _records.Length : 0,
                    _records.IsCreated ? _records.Capacity : 0,
                    _currentPlayerChunk,
                    record.InstanceUid);
                return false;
            }

            int appendIndex = _records.Length;
            if (!_records.AddNoResize(record))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryRecordsBuffer,
                    0u,
                    _records.Length,
                    _records.Capacity,
                    _currentPlayerChunk,
                    record.InstanceUid);
                return false;
            }

            if (TryAddRecordChunkIndex(record.ChunkId, appendIndex, record.InstanceUid))
            {
                recordIndex = appendIndex;
                return true;
            }

            _records.RemoveAtSwapBack(appendIndex);
            return false;
        }

        private void RollbackAppendedRecord(int recordIndex, in PersistentWorldItemRecord record)
        {
            if (!_records.IsCreated || recordIndex < 0 || recordIndex >= _records.Length)
                return;

            RemoveRecordIndexFromChunk(record.ChunkId, recordIndex);
            if (recordIndex == _records.Length - 1)
            {
                _records.RemoveAtSwapBack(recordIndex);
                return;
            }

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryRecordsBuffer,
                0u,
                recordIndex,
                _records.Length,
                record.ChunkId,
                record.InstanceUid);
        }

        private bool TryAddRecordChunkIndex(int3 chunkId, int recordIndex, uint instanceUid)
        {
            if (!_recordsByChunk.IsCreated || !_recordsByChunk.Add(chunkId, recordIndex))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryRecordsByChunkKeysBuffer,
                    0u,
                    recordIndex,
                    _recordsByChunk.IsCreated ? _recordsByChunk.Capacity : 0,
                    chunkId,
                    instanceUid);
                return false;
            }

            return true;
        }

        private bool TryMoveRecordIndexToChunk(int3 previousChunkId, int3 nextChunkId, int recordIndex, uint instanceUid)
        {
            if (math.all(previousChunkId == nextChunkId))
                return true;

            if (!TryAddRecordChunkIndex(nextChunkId, recordIndex, instanceUid))
                return false;

            RemoveRecordIndexFromChunk(previousChunkId, recordIndex);
            return true;
        }

        private void RollbackRecordChunkMove(int3 currentChunkId, int3 previousChunkId, int recordIndex, uint instanceUid)
        {
            if (math.all(currentChunkId == previousChunkId))
                return;

            RemoveRecordIndexFromChunk(currentChunkId, recordIndex);
            TryAddRecordChunkIndex(previousChunkId, recordIndex, instanceUid);
        }

        private bool TryAppendCompactDeltaRecord(uint instanceUid, in PersistentWorldCompactDeltaRecord compactRecord)
        {
            if (!_deltaRecords.IsCreated ||
                !_deltaRecordIndexByEntityId.IsCreated ||
                instanceUid == 0u ||
                _deltaRecords.Length >= _deltaRecords.Capacity)
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDeltaRecordsBuffer,
                    0u,
                    _deltaRecords.IsCreated ? _deltaRecords.Length : 0,
                    _deltaRecords.IsCreated ? _deltaRecords.Capacity : 0,
                    _currentPlayerChunk,
                    instanceUid);
                return false;
            }

            int deltaIndex = _deltaRecords.Length;
            if (!_deltaRecords.AddNoResize(compactRecord))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDeltaRecordsBuffer,
                    0u,
                    _deltaRecords.Length,
                    _deltaRecords.Capacity,
                    _currentPlayerChunk,
                    instanceUid);
                return false;
            }

            if (_deltaRecordIndexByEntityId.TryAdd(instanceUid, deltaIndex))
                return true;

            _deltaRecords.RemoveAtSwapBack(deltaIndex);
            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryDeltaRecordIndexKeysBuffer,
                0u,
                deltaIndex,
                _deltaRecordIndexByEntityId.Capacity,
                _currentPlayerChunk,
                instanceUid);
            return false;
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
                if (!_deltaRecordIndexByEntityId.TryAdd(lastRecord.InstanceUid, deltaIndex))
                {
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistryDeltaRecordIndexKeysBuffer,
                        0u,
                        deltaIndex,
                        _deltaRecordIndexByEntityId.Capacity,
                        _currentPlayerChunk,
                        lastRecord.InstanceUid);
                }
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
            return ItemData.ResolvePersistentHashId(itemData);
        }

        internal int CopyChunkDeltas(int3 chunkId, NativeList<PersistentWorldDeltaRecord> destination)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated)
                return 0;

            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length; i++)
            {
                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[i];
                if (!TryResolveDeltaRecord(compactRecord, out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!math.all(expandedRecord.ChunkId == chunkId))
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

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

        internal int CopyDestroyedFloraDeltas(NativeArray<PersistentWorldDeltaRecord> destination, int maxCount)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated || maxCount <= 0)
                return 0;

            int copyLimit = math.min(destination.Length, maxCount);
            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length && copiedCount < copyLimit; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (((PersistentWorldItemFlags)expandedRecord.ItemFlags & PersistentWorldItemFlags.FloraDestroyed) == 0)
                    continue;

                destination[copiedCount] = expandedRecord;
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyDestroyedFloraDeltas(PersistentWorldDeltaRecord[] destination, int maxCount)
        {
            if (destination == null || !_deltaRecords.IsCreated || maxCount <= 0)
                return 0;

            int copyLimit = math.min(destination.Length, maxCount);
            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length && copiedCount < copyLimit; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (((PersistentWorldItemFlags)expandedRecord.ItemFlags & PersistentWorldItemFlags.FloraDestroyed) == 0)
                    continue;

                destination[copiedCount] = expandedRecord;
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

                if (!PersistentWorldDeltaRecord.IsFloraStateOverride(in expandedRecord))
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyFloraStateOverrideDeltas(NativeArray<PersistentWorldDeltaRecord> destination, int maxCount)
        {
            if (!destination.IsCreated || !_deltaRecords.IsCreated || maxCount <= 0)
                return 0;

            int copyLimit = math.min(destination.Length, maxCount);
            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length && copiedCount < copyLimit; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!PersistentWorldDeltaRecord.IsFloraStateOverride(in expandedRecord))
                    continue;

                destination[copiedCount] = expandedRecord;
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyFloraStateOverrideDeltas(PersistentWorldDeltaRecord[] destination, int maxCount)
        {
            if (destination == null || !_deltaRecords.IsCreated || maxCount <= 0)
                return 0;

            int copyLimit = math.min(destination.Length, maxCount);
            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length && copiedCount < copyLimit; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!PersistentWorldDeltaRecord.IsFloraStateOverride(in expandedRecord))
                    continue;

                destination[copiedCount] = expandedRecord;
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

                if (!PersistentWorldDeltaRecord.IsFloraSeedPending(in expandedRecord))
                    continue;

                if (destination.Length >= destination.Capacity)
                    break;

                destination.AddNoResize(expandedRecord);
                copiedCount++;
            }

            return copiedCount;
        }

        internal int CopyPendingFloraSeedDeltas(PersistentWorldDeltaRecord[] destination, int maxCount)
        {
            if (destination == null || !_deltaRecords.IsCreated || maxCount <= 0)
                return 0;

            int copyLimit = math.min(destination.Length, maxCount);
            int copiedCount = 0;
            for (int i = 0; i < _deltaRecords.Length && copiedCount < copyLimit; i++)
            {
                if (!TryResolveDeltaRecord(_deltaRecords[i], out PersistentWorldDeltaRecord expandedRecord))
                    continue;

                if (!PersistentWorldDeltaRecord.IsFloraSeedPending(in expandedRecord))
                    continue;

                destination[copiedCount] = expandedRecord;
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
            PersistentWorldDeltaRecord expandedRecord = PersistentWorldItemRecord.IsDeleted(in record)
                ? PersistentWorldDeltaRecord.CreateDeletedTombstone(in record, chunkSizeMeters)
                : PersistentWorldDeltaRecord.FromRecord(in record, chunkSizeMeters);
            return TryBuildCompactDeltaRecord(expandedRecord, out compactRecord);
        }

        private bool TryBuildCompactDeltaRecord(PersistentWorldDeltaRecord expandedRecord, out PersistentWorldCompactDeltaRecord compactRecord)
        {
            compactRecord = default;
            if (!PersistentWorldDeltaRecord.IsValid(in expandedRecord))
                return false;

            if (!TryEnsureDeltaChunkIndex(expandedRecord.ChunkId, out ushort chunkIndex))
            {
                return false;
            }

            ushort itemHashIndex = ushort.MaxValue;
            if (!PersistentWorldDeltaRecord.IsDeleted(in expandedRecord) &&
                !TryEnsureDeltaItemHashIndex(expandedRecord.ItemPersistentIdHash, out itemHashIndex))
            {
                return false;
            }

            compactRecord = new PersistentWorldCompactDeltaRecord
            {
                PackedLocalPosition = expandedRecord.PackedLocalPosition,
                InstanceUid = expandedRecord.InstanceUid,
                Quantity = PersistentWorldDeltaRecord.IsDeleted(in expandedRecord) ? (ushort)1 : expandedRecord.Quantity,
                ItemFlags = expandedRecord.ItemFlags,
                Reserved = 0,
                ChunkIndex = chunkIndex,
                ItemHashIndex = itemHashIndex
            };
            return PersistentWorldCompactDeltaRecord.IsValidRecord(in compactRecord);
        }

        private bool TryResolveDeltaRecord(PersistentWorldCompactDeltaRecord compactRecord, out PersistentWorldDeltaRecord expandedRecord)
        {
            expandedRecord = default;
            if (!PersistentWorldCompactDeltaRecord.IsValidRecord(in compactRecord) ||
                !TryGetCompactDeltaChunkId(compactRecord, out int3 chunkId))
            {
                return false;
            }

            ulong itemHash = 0UL;
            bool compactDeleted = PersistentWorldCompactDeltaRecord.HasDeletedFlag(in compactRecord);
            if (!compactDeleted && !TryGetCompactDeltaItemHash(compactRecord, out itemHash))
                return false;

            expandedRecord = new PersistentWorldDeltaRecord
            {
                ChunkId = chunkId,
                ItemPersistentIdHash = itemHash,
                InstanceUid = compactRecord.InstanceUid,
                PackedLocalPosition = compactRecord.PackedLocalPosition,
                Quantity = compactDeleted ? (ushort)1 : compactRecord.Quantity,
                ItemFlags = compactRecord.ItemFlags,
                Reserved = compactRecord.Reserved
            };
            return PersistentWorldDeltaRecord.IsValid(in expandedRecord);
        }

        private bool TryEnsureDeltaChunkIndex(int3 chunkId, out ushort chunkIndex)
        {
            chunkIndex = 0;
            if (!_deltaChunkIndexByChunkId.IsCreated || !_deltaChunkIds.IsCreated)
                return false;

            if (!AbsoluteUniversePosition.IsValidChunkId(chunkId))
                return false;

            if (_deltaChunkIndexByChunkId.TryGetValue(chunkId, out chunkIndex))
                return true;

            if (_deltaChunkIds.Length >= _deltaChunkIds.Capacity || _deltaChunkIds.Length >= ushort.MaxValue)
                return false;

            chunkIndex = (ushort)_deltaChunkIds.Length;
            if (!_deltaChunkIds.AddNoResize(chunkId))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDeltaChunkIdsBuffer,
                    0u,
                    _deltaChunkIds.Length,
                    _deltaChunkIds.Capacity,
                    chunkId,
                    0u);
                return false;
            }

            if (_deltaChunkIndexByChunkId.TryAdd(chunkId, chunkIndex))
                return true;

            _deltaChunkIds.RemoveAtSwapBack(chunkIndex);
            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryDeltaChunkIndexKeysBuffer,
                0u,
                chunkIndex,
                _deltaChunkIndexByChunkId.Capacity,
                chunkId,
                0u);
            return false;
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
            if (!_deltaItemHashes.AddNoResize(itemHash))
            {
                WriteWorldTelemetry(
                    WorldTelemetryCapacityMismatch,
                    WorldRegistryDeltaItemHashesBuffer,
                    0u,
                    _deltaItemHashes.Length,
                    _deltaItemHashes.Capacity,
                    _currentPlayerChunk,
                    0u);
                return false;
            }

            if (_deltaItemIndexByHash.TryAdd(itemHash, itemHashIndex))
                return true;

            _deltaItemHashes.RemoveAtSwapBack(itemHashIndex);
            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryDeltaItemIndexKeysBuffer,
                0u,
                itemHashIndex,
                _deltaItemIndexByHash.Capacity,
                _currentPlayerChunk,
                0u);
            return false;
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
                PersistentWorldItemRecord record = _records[i];
                if (!PersistentWorldItemRecord.IsCollected(in record) && !PersistentWorldItemRecord.IsDeleted(in record))
                    count++;
            }

            return count;
        }

        private bool IsDeletedInstanceUid(uint instanceUid)
        {
            return instanceUid != 0u && _deletedInstanceUids.IsCreated && _deletedInstanceUids.Contains(instanceUid);
        }

        private bool RegisterDeletedInstanceUid(uint instanceUid)
        {
            if (instanceUid == 0u || !_deletedInstanceUids.IsCreated)
                return false;

            if (_deletedInstanceUids.Contains(instanceUid))
                return true;

            if (_deletedInstanceUids.Add(instanceUid))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryDeletedInstanceKeysBuffer,
                0u,
                0,
                _deletedInstanceUids.Capacity,
                _currentPlayerChunk,
                instanceUid);
            return false;
        }

        private bool RegisterResourceNodeTombstone(ulong tombstoneId)
        {
            if (tombstoneId == 0UL || !_resourceNodeTombstoneIds.IsCreated)
                return false;

            if (_resourceNodeTombstoneIds.Contains(tombstoneId))
                return true;

            if (_resourceNodeTombstoneIds.Add(tombstoneId))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryResourceTombstoneKeysBuffer,
                0u,
                0,
                _resourceNodeTombstoneIds.Capacity,
                _currentPlayerChunk,
                0u);
            return false;
        }

        private ulong ResolveResourceNodeTombstoneId(in PersistentWorldDeltaRecord deltaRecord)
        {
            return deltaRecord.ItemPersistentIdHash != 0UL
                ? deltaRecord.ItemPersistentIdHash
                : ComputeResourceNodeTombstoneId(deltaRecord.UnpackPosition(chunkSizeMeters));
        }

        private bool StageLoadedResourceNodeTombstoneRecord(in PersistentWorldDeltaRecord deltaRecord, ulong tombstoneId)
        {
            if (tombstoneId == 0UL ||
                !_records.IsCreated ||
                !_recordsByChunk.IsCreated ||
                deltaRecord.InstanceUid == 0u)
                return false;

            if (TryFindRecordIndexByInstanceUid(deltaRecord.InstanceUid, out _))
                return true;

            if (_records.Length >= _records.Capacity)
                return false;

            PersistentWorldItemRecord record = deltaRecord.ToRecord(chunkSizeMeters);
            record.ItemPersistentIdHash = tombstoneId;
            record.Quantity = 0;
            record.Flags = PersistentWorldItemFlags.Deleted | PersistentWorldItemFlags.ResourceNodeDestroyed;
            return TryAppendRecordWithChunk(in record, out _);
        }

        private bool StageResourceNodeTombstonesForSave()
        {
            if (!_records.IsCreated)
                return false;

            for (int i = 0; i < _records.Length; i++)
            {
                PersistentWorldItemRecord record = _records[i];
                if (!PersistentWorldItemRecord.IsResourceNodeDestroyed(in record) || record.ItemPersistentIdHash == 0UL)
                    continue;

                if (!UpsertDeletedTombstone(in record))
                    return false;

                if (!RegisterResourceNodeTombstone(record.ItemPersistentIdHash))
                    return false;
            }

            return true;
        }

        private bool RegisterResourceNodeMetamorphosis(ulong tombstoneId)
        {
            if (tombstoneId == 0UL || !_resourceNodeMetamorphosedIds.IsCreated)
                return false;

            if (_resourceNodeMetamorphosedIds.Contains(tombstoneId))
                return true;

            if (_resourceNodeMetamorphosedIds.Add(tombstoneId))
                return true;

            WriteWorldTelemetry(
                WorldTelemetryCapacityMismatch,
                WorldRegistryResourceMetamorphosedKeysBuffer,
                0u,
                0,
                _resourceNodeMetamorphosedIds.Capacity,
                _currentPlayerChunk,
                0u);
            return false;
        }

        private void UnregisterDeletedInstanceUid(uint instanceUid)
        {
            if (instanceUid == 0u || !_deletedInstanceUids.IsCreated)
                return;

            _deletedInstanceUids.Remove(instanceUid);
        }

        private bool UpsertDeletedTombstone(in PersistentWorldItemRecord record)
        {
            CompleteTombstoneDecayBeforeDeltaMutation();

            if (!_deltaRecords.IsCreated || !_deltaRecordIndexByEntityId.IsCreated || record.InstanceUid == 0u)
                return false;

            PersistentWorldItemRecord tombstoneRecord = record;
            tombstoneRecord.Quantity = tombstoneRecord.Quantity > 1 ? tombstoneRecord.Quantity : ResolveTombstoneDayIndex();
            if (!TryBuildCompactDeltaRecord(in tombstoneRecord, out PersistentWorldCompactDeltaRecord compactRecord))
                return false;

            bool wasDeletedRegistered = IsDeletedInstanceUid(record.InstanceUid);
            if (!RegisterDeletedInstanceUid(record.InstanceUid))
                return false;

            if (_deltaRecordIndexByEntityId.TryGetValue(record.InstanceUid, out int deltaIndex))
            {
                if (!TryWriteCompactDeltaRecordAt(deltaIndex, in compactRecord))
                {
                    if (!wasDeletedRegistered)
                        UnregisterDeletedInstanceUid(record.InstanceUid);
                    return false;
                }

                RebuildDeltaChunkLookup();
                return true;
            }

            if (!TryAppendCompactDeltaRecord(record.InstanceUid, in compactRecord))
            {
                if (!wasDeletedRegistered)
                    UnregisterDeletedInstanceUid(record.InstanceUid);
                return false;
            }

            RebuildDeltaChunkLookup();
            return true;
        }

        private void ScheduleTombstoneDecaySweepIfDue(float now)
        {
            if (_tombstoneDecayApplyPending ||
                !_deltaRecords.IsCreated ||
                !_tombstoneDecayExpiredIndices.IsCreated ||
                now < _nextTombstoneDecaySweepTime)
            {
                return;
            }

            _nextTombstoneDecaySweepTime = now + TombstoneDecayFrostTickSeconds;
            _tombstoneDecayApplyCursor = 0;
            _tombstoneDecayCurrentDay = ResolveTombstoneDayIndex();
            if (CollectExpiredTombstoneDecayIndices())
                _tombstoneDecayApplyPending = true;
        }

        private void CompleteTombstoneDecayBeforeDeltaMutation()
        {
            if (_tombstoneDecayApplyPending)
                ApplyCollectedTombstoneDecay(int.MaxValue);
        }

        private bool CollectExpiredTombstoneDecayIndices()
        {
            _tombstoneDecayExpiredIndices.Clear();
            int tombstoneCount = 0;
            int deltaCount = _deltaRecords.Length;
            for (int i = 0; i < deltaCount; i++)
            {
                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[i];
                if (PersistentWorldCompactDeltaRecord.HasDeletedFlag(in compactRecord))
                    tombstoneCount++;
            }

            if (tombstoneCount <= TombstoneDecayThreshold)
                return false;

            for (int i = deltaCount - 1; i >= 0 && tombstoneCount > TombstoneDecayThreshold; i--)
            {
                PersistentWorldCompactDeltaRecord compactRecord = _deltaRecords[i];
                if (!PersistentWorldCompactDeltaRecord.HasDeletedFlag(in compactRecord))
                    continue;

                int tombstoneDay = compactRecord.Quantity > 0 ? compactRecord.Quantity : 1;
                if (_tombstoneDecayCurrentDay - tombstoneDay <= TombstoneTimeToLiveDays)
                    continue;

                if (!_tombstoneDecayExpiredIndices.AddNoResize(i))
                {
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistryTombstoneDecayIndicesBuffer,
                        0u,
                        _tombstoneDecayExpiredIndices.Length,
                        maxTrackedItems,
                        _currentPlayerChunk,
                        0u);
                    return _tombstoneDecayExpiredIndices.Length > 0;
                }

                tombstoneCount--;
            }

            return _tombstoneDecayExpiredIndices.Length > 0;
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
                if (!PersistentWorldCompactDeltaRecord.HasDeletedFlag(in compactRecord))
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
                PersistentWorldDeltaRecord.IsResourceNodeDestroyed(in expandedRecord) &&
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
                if (!_deltaRecordIndexByEntityId.TryAdd(lastRecord.InstanceUid, deltaIndex))
                {
                    WriteWorldTelemetry(
                        WorldTelemetryCapacityMismatch,
                        WorldRegistryDeltaRecordIndexKeysBuffer,
                        0u,
                        deltaIndex,
                        _deltaRecordIndexByEntityId.Capacity,
                        _currentPlayerChunk,
                        lastRecord.InstanceUid);
                }
            }
        }

        private int ResolveTombstoneDayIndex()
        {
            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            double playSeconds = IsSaveServiceUsable(saveService)
                ? saveService.CurrentPlayTimeSeconds
                : Time.timeAsDouble;
            int day = (int)math.floor(math.max(0d, playSeconds) / TombstoneInGameDaySeconds);
            return math.clamp(day, 1, ushort.MaxValue);
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
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
            if (string.IsNullOrWhiteSpace(value))
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
            bool hasNonWhiteSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte current = value[i];
                hasNonWhiteSpace |= !IsAsciiWhiteSpace(current);
                hash ^= current;
                hash *= FnvPrime64;
                hash ^= 0UL;
                hash *= FnvPrime64;
            }

            return hasNonWhiteSpace ? hash : 0UL;
        }

        private static bool IsAsciiWhiteSpace(byte value)
        {
            return value == 32 || (value >= 9 && value <= 13);
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
                Hecton8.Core.H8Debug.LogError("[PersistentWorldRegistry] Exhausted 24-bit persistent item instance UID counter.");
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
            Hecton8.Core.H8Debug.LogError(CreateHexMessage("[PersistentWorldRegistry] Failed to reserve resource-node tombstone UID. tombstoneId=", tombstoneId));
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
            Hecton8.Core.H8Debug.LogError(CreateHexMessage("[PersistentWorldRegistry] Failed to reserve resource-node metamorphosis UID. tombstoneId=", tombstoneId));
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
