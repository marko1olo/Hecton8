using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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
        public bool HasAbsoluteUniversePosition;
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
        public Hecton8.World.AbsoluteUniversePosition AbsoluteUniverseAup;
        public Vector3 AbsoluteUniversePosition;
    }

    internal struct AssetDispatchTicket
    {
        public int RequestId;
        public uint AssetKey;
        public AssetPriorityTier Priority;
        public bool IsDistantHlod;
    }

    internal struct AssetDispatchRequest
    {
        public int RequestId;
        public uint AssetKey;
        public AssetPriorityTier Priority;
        public bool IsDistantHlod;
        public int AgeFrames;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AssetTrackerDTO
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public int ReferenceCount;
        [FieldOffset(8)] public ulong HandlePointer;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct AssetHandleMapEntryDTO
    {
        [FieldOffset(0)] public ulong HandlePointer;
        [FieldOffset(8)] public uint AssetHash;
        [FieldOffset(12)] public uint BundlePrefixHash;
        [FieldOffset(16)] public int Slot;
        [FieldOffset(20)] public int RefCount;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Generation;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct AssetCacheProfileDTO
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public float BaseTtlSeconds;
        [FieldOffset(8)] public float BundleTtlMultiplier;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal partial struct MockChunkLoadSignal
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public int RequestCount;
        [FieldOffset(8)] public uint FrameIndex;
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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct AssetTtlEvaluationJob : IJob
    {
        [NoAlias] public NativeArray<AssetTrackerDTO> Trackers;
        [NoAlias] public NativeArray<float> TimeToLiveSeconds;
        [NoAlias] public NativeArray<byte> Flags;
        public float DeltaSeconds;

        public void Execute()
        {
            float delta = DeltaSeconds > 0f ? DeltaSeconds : 1f;
            int count = Trackers.IsCreated ? Trackers.Length : 0;
            for (int i = 0; i < count; i++)
            {
                byte flags = Flags[i];
                if ((flags & AssetHandleFlags.Active) == 0 ||
                    (flags & AssetHandleFlags.PendingTtl) == 0 ||
                    (flags & AssetHandleFlags.Pinned) != 0)
                {
                    continue;
                }

                AssetTrackerDTO tracker = Trackers[i];
                if (tracker.ReferenceCount > 0)
                {
                    flags = (byte)(flags & ~(AssetHandleFlags.PendingTtl | AssetHandleFlags.Releasable));
                    Flags[i] = flags;
                    continue;
                }

                float ttl = TimeToLiveSeconds[i] - delta;
                TimeToLiveSeconds[i] = ttl;
                if (ttl <= 0f)
                    Flags[i] = (byte)(flags | AssetHandleFlags.Releasable);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct MockChunkLoadSpamJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public AssetTrackerDTO* Trackers;
        public int TrackerCount;
        public uint BaseAssetHash;
        public uint FrameIndex;
        [NoAlias] public NativeArray<MockChunkLoadSignal> OutputSignals;

        public void Execute(int index)
        {
            if (!OutputSignals.IsCreated || (uint)index >= (uint)OutputSignals.Length)
                return;

            int trackerCount = TrackerCount > 0 ? TrackerCount : 1;
            int slot = index % trackerCount;
            uint hash = BaseAssetHash + unchecked((uint)slot);
            int count = 1;
            if (Trackers != null && slot >= 0 && slot < TrackerCount)
            {
                Trackers[slot].AssetHash = hash;
                count = Interlocked.Increment(ref Trackers[slot].ReferenceCount);
            }

            OutputSignals[index] = new MockChunkLoadSignal
            {
                AssetHash = hash,
                RequestCount = count,
                FrameIndex = FrameIndex,
                Flags = 1u
            };
        }
    }
}
