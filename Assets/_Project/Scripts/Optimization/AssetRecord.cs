using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.Optimization
{
    internal enum AssetPriorityTier : byte
    {
        Tier0PlayerCritical = 0x00,
        Tier1Equipped = 0x01,
        Tier2Proximity = 0x10,
        Tier3Ambient = 0x20,
        Tier4MidRange = 0x30,
        Tier5DistantHlod = 0x40,
        Tier6Speculative = 0xFF
    }

    internal enum AssetResidencyKind : byte
    {
        Unknown = 0,
        Addressable = 1,
        SceneOwned = 2,
        StreamingTexture = 3,
        StreamingMesh = 4,
        AudioBank = 5,
        Misc = 6
    }

    internal enum AddressableAssetGroupKind : byte
    {
        Unknown = 0,
        UIIcons = 1
    }

    internal struct AssetRecord
    {
        public uint Key;
        public string AssetGuid;
        public string Address;
        public Object Asset;
        public Component Owner;
        public int RefCount;
        public AssetPriorityTier Priority;
        public AssetResidencyKind ResidencyKind;
        public bool PendingRelease;
        public bool IsFallback;
        public bool OwnsAssetInstance;
        public bool IsChunkAsset;
#if UNITY_ADDRESSABLES_EXIST
        public bool HasAddressableHandle;
        public AsyncOperationHandle AddressableHandle;
#endif
        public byte RetryCount;
        public byte BiomeId;
        public byte LodLevel;
        public long LastAccessFrame;
        public long SizeBytes;
        public int ActiveRequestId;
        public float NextRetryTime;
    }

    internal struct AssetDispatchTicket
    {
        public int RequestId;
        public uint AssetKey;
        public AssetPriorityTier Priority;
        public byte IsDistantHlod;
    }

    internal struct AssetDispatchRequest
    {
        public int RequestId;
        public uint AssetKey;
        public AssetPriorityTier Priority;
        public byte IsDistantHlod;
        public int AgeFrames;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AssetTrackerDTO
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public int ReferenceCount;
        [FieldOffset(8)] public ulong HandlePointer;
        [FieldOffset(16)] public long AssetSectorX;
        [FieldOffset(24)] public long AssetSectorY;
        [FieldOffset(32)] public long AssetSectorZ;
        [FieldOffset(40)] public float AssetLocalX;
        [FieldOffset(44)] public float AssetLocalY;
        [FieldOffset(48)] public float AssetLocalZ;
        [FieldOffset(52)] public float MaxResidencyRadiusSq;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint AupShiftGeneration;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct AssetHandleMapEntryDTO
    {
        [FieldOffset(0)] public ulong AssetHash;
        [FieldOffset(8)] public ulong BundlePrefixHash;
        [FieldOffset(16)] public int PoolSlotIndex;
        [FieldOffset(20)] public int RefCount;
        [FieldOffset(24)] public float TimeToLive;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint Generation;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private uint _pad1;
        [FieldOffset(44)] private uint _pad2;
        [FieldOffset(48)] private uint _pad3;
        [FieldOffset(52)] private uint _pad4;
        [FieldOffset(56)] private uint _pad5;
        [FieldOffset(60)] private uint _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct AssetCacheProfileDTO
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public float BaseTtlSeconds;
        [FieldOffset(8)] public float BundleTtlMultiplier;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct AssetHeapTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint ActiveHandles;
        [FieldOffset(8)] public uint OrphanedHandlesReleased;
        [FieldOffset(12)] public uint CacheHits;
        [FieldOffset(16)] public uint CacheMisses;
        [FieldOffset(20)] public uint PendingTtlReleases;
        [FieldOffset(24)] public uint ForcedVramReleases;
        [FieldOffset(28)] public uint LeakSuspectHash;
        [FieldOffset(32)] public float CacheHitRatio;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float VramPressure;
        [FieldOffset(44)] public float LongestTtlSeconds;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint ResultHash;
        [FieldOffset(56)] public uint Pad0;
        [FieldOffset(60)] public uint Pad1;
    }

    internal static class AssetHandleFlags
    {
        public const byte Active = 1 << 0;
        public const byte PendingTtl = 1 << 1;
        public const byte Releasable = 1 << 2;
        public const byte Pinned = 1 << 3;
        public const byte BundleShared = 1 << 4;
        public const byte LeakSuspect = 1 << 5;
        public const byte Loading = 1 << 6;
    }

    internal static class AssetTrackerMetaFlags
    {
        public const uint UnknownAup = 1u << 8;
    }

    internal static class AssetHandleMapFlags
    {
        public const uint Occupied = 1u << 0;
        public const uint Tombstone = 1u << 1;
        public const uint BundleShared = 1u << 2;
    }

    internal static unsafe class AssetTrackerAtomic
    {
        public static int Increment(NativeArray<AssetTrackerDTO> trackers, int slot)
        {
            if (!trackers.IsCreated || (uint)slot >= (uint)trackers.Length)
                return 0;

            AssetTrackerDTO* ptr = (AssetTrackerDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(trackers);
            return Interlocked.Increment(ref ptr[slot].ReferenceCount);
        }

        public static int Decrement(NativeArray<AssetTrackerDTO> trackers, int slot)
        {
            if (!trackers.IsCreated || (uint)slot >= (uint)trackers.Length)
                return 0;

            AssetTrackerDTO* ptr = (AssetTrackerDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(trackers);
            return Interlocked.Decrement(ref ptr[slot].ReferenceCount);
        }

        public static bool IsRefCountZero(NativeArray<AssetTrackerDTO> trackers, int slot)
        {
            if (!trackers.IsCreated || (uint)slot >= (uint)trackers.Length)
                return false;

            AssetTrackerDTO* ptr = (AssetTrackerDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(trackers);
            return Interlocked.CompareExchange(ref ptr[slot].ReferenceCount, 0, 0) == 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct AssetTtlEvaluationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AssetTrackerDTO> Trackers;
        [NoAlias] public NativeArray<AssetHandleMapEntryDTO> HandleMap;
        public double3 PlayerAup;
        public float MaxResidencyRadiusSq;
        public float DeltaSeconds;
        public float QualityTtlDecayMultiplier;
        public byte ForceVramPanic;

        public void Execute(int index)
        {
            float delta = DeltaSeconds > 0f ? DeltaSeconds : 1f;
            if (!HandleMap.IsCreated ||
                !Trackers.IsCreated ||
                (uint)index >= (uint)HandleMap.Length)
            {
                return;
            }

            AssetHandleMapEntryDTO entry = HandleMap[index];
            if ((entry.Flags & AssetHandleMapFlags.Occupied) == 0u)
                return;

            int slot = entry.PoolSlotIndex;
            if ((uint)slot >= (uint)Trackers.Length)
            {
                return;
            }

            AssetTrackerDTO tracker = Trackers[slot];
            if (tracker.AssetHash != unchecked((uint)entry.AssetHash))
                return;

            uint flags = tracker.Flags;
            if ((flags & AssetHandleFlags.Active) == 0 ||
                (flags & AssetHandleFlags.PendingTtl) == 0 ||
                (flags & AssetHandleFlags.Pinned) != 0)
            {
                return;
            }

            if (tracker.ReferenceCount > 0)
            {
                tracker.Flags = flags & ~(uint)(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable);
                Trackers[slot] = tracker;
                entry.RefCount = tracker.ReferenceCount;
                entry.TimeToLive = 0f;
                HandleMap[index] = entry;
                return;
            }

            float distancePenalty = 1f;
            if ((flags & AssetTrackerMetaFlags.UnknownAup) == 0u)
            {
                double3 assetAup = AssetTrackerAupMath.ToAbsoluteDouble3(in tracker);
                double3 aupDelta = assetAup - PlayerAup;
                float distanceSq = float.MaxValue;
                if (math.all(math.isfinite(aupDelta)))
                {
                    float3 localDelta = new float3((float)aupDelta.x, (float)aupDelta.y, (float)aupDelta.z);
                    float sampledDistanceSq = math.lengthsq(localDelta);
                    distanceSq = math.isfinite(sampledDistanceSq) ? sampledDistanceSq : float.MaxValue;
                }

                float safeRadiusSq = MaxResidencyRadiusSq > 0f && math.isfinite(MaxResidencyRadiusSq)
                    ? MaxResidencyRadiusSq
                    : tracker.MaxResidencyRadiusSq;
                distancePenalty = distanceSq > safeRadiusSq ? 5f : 1f;
            }

            float pressurePenalty = ForceVramPanic != 0 ? 3f : 1f;
            float qualityDecayPenalty = math.isfinite(QualityTtlDecayMultiplier)
                ? math.max(0.0001f, QualityTtlDecayMultiplier)
                : 1f;
            float ttl = entry.TimeToLive - (delta * distancePenalty * pressurePenalty * qualityDecayPenalty);
            ttl = math.isfinite(ttl) ? ttl : 0f;
            entry.RefCount = tracker.ReferenceCount;
            entry.TimeToLive = ttl;

            if (ttl <= 0f)
            {
                tracker.Flags = flags | AssetHandleFlags.Releasable;
                Trackers[slot] = tracker;
            }

            HandleMap[index] = entry;
        }
    }

    internal static class AssetTrackerAupMath
    {
        public static double3 ToAbsoluteDouble3(in AssetTrackerDTO tracker)
        {
            const double sectorSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                (tracker.AssetSectorX * sectorSize) + tracker.AssetLocalX,
                (tracker.AssetSectorY * sectorSize) + tracker.AssetLocalY,
                (tracker.AssetSectorZ * sectorSize) + tracker.AssetLocalZ);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HeapSanitizerMemClearJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AssetTrackerDTO> Trackers;
        [NoAlias] public NativeArray<float> TimeToLiveSeconds;
        [NoAlias] public NativeArray<AssetHandleMapEntryDTO> HandleMap;

        public void Execute(int index)
        {
            if (Trackers.IsCreated && (uint)index < (uint)Trackers.Length)
                Trackers[index] = default;
            if (TimeToLiveSeconds.IsCreated && (uint)index < (uint)TimeToLiveSeconds.Length)
                TimeToLiveSeconds[index] = 0f;
            if (HandleMap.IsCreated && (uint)index < (uint)HandleMap.Length)
                HandleMap[index] = default;
        }
    }

}
