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
}
