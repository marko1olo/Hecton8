using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
#endif

namespace Hecton8.Core.Content
{
    internal static class ContentAssetBinaryLayout
    {
        public const int RecordStrideBytes = 32;
    }

    public enum ContentAssetKind : byte
    {
        Unknown = 0,
        Prefab = 1,
        Mesh = 2,
        Material = 3,
        Texture = 4,
        Audio = 5,
        Vfx = 6,
        LoreText = 7,
        Compute = 8
    }

    public enum ContentTier : byte
    {
        Core = 0,
        HighRes = 1,
        Overkill = 2
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = ContentAssetBinaryLayout.RecordStrideBytes)]
    public struct ContentAssetBinaryRecord
    {
        [FieldOffset(0)] public long EstimatedVramBytes;
        [FieldOffset(8)] public uint Hash;
        [FieldOffset(12)] public uint DependencyOffset;
        [FieldOffset(16)] public ushort DependencyCount;
        [FieldOffset(18)] public ContentAssetKind Kind;
        [FieldOffset(19)] public ContentTier Tier;
        [FieldOffset(20)] public byte BiomeId;
        [FieldOffset(21)] public byte LodLevel;
        [FieldOffset(22)] public byte Flags;
        [FieldOffset(23)] public byte Reserved0;
        [FieldOffset(24)] public uint Reserved1;
        [FieldOffset(28)] public uint Reserved2;
    }

    /// <summary>
    /// Authoring-only registry row. This type holds managed Unity references and must not be marshalled.
    /// </summary>
    /// <remarks>
    /// Cold export and validator paths may produce <see cref="ContentAssetBinaryRecord"/> via
    /// <see cref="ToBinaryRecord"/>. Runtime NativeArray, Burst, or hot ARM64 use requires an
    /// aligned runtime mirror, not the packed file/export record.
    /// </remarks>
    [Serializable]
    public struct ContentAssetEntry
    {
        [Tooltip("FNV1a-32 authority hash. Zero is invalid and fails validators.")]
        public uint Hash;

        [Tooltip("Human-readable key used only to recompute Hash in editor/import windows.")]
        public string StableKey;

        [Tooltip("Addressables address or GUID. Runtime callers must resolve by Hash first.")]
        public string Address;

#if UNITY_ADDRESSABLES_EXIST
        public AssetReference Asset;
#endif

        [Tooltip("Prefab or visual mesh binding required by build gates for economy/world items.")]
        public GameObject MeshPrefab;

        public Mesh Mesh;
        public Material FallbackMaterial;
        public ContentAssetKind Kind;
        public ContentTier Tier;
        public byte BiomeId;
        public byte LodLevel;
        public long EstimatedVramBytes;
        public bool RequiredInBuild;
        public bool IsBiomeCache;
        public uint[] DependencyHashes;

        public bool IsVisual3DKind()
        {
            return Kind == ContentAssetKind.Prefab ||
                   Kind == ContentAssetKind.Mesh ||
                   Kind == ContentAssetKind.Vfx;
        }

        public bool HasVisual3D()
        {
            return IsVisual3DKind() && (MeshPrefab != null || Mesh != null);
        }

        public ContentAssetBinaryRecord ToBinaryRecord(uint dependencyOffset)
        {
            ValidateBinaryExportShape();

            uint flags = 0u;
            if (RequiredInBuild)
                flags |= 1u;
            if (IsBiomeCache)
                flags |= 2u;
            if (HasVisual3D())
                flags |= 4u;

            int dependencyCount = DependencyHashes != null ? DependencyHashes.Length : 0;
            return new ContentAssetBinaryRecord
            {
                Hash = Hash,
                EstimatedVramBytes = EstimatedVramBytes,
                DependencyOffset = dependencyOffset,
                DependencyCount = (ushort)dependencyCount,
                Kind = Kind,
                Tier = Tier,
                BiomeId = BiomeId,
                LodLevel = LodLevel,
                Flags = (byte)flags
            };
        }

        private void ValidateBinaryExportShape()
        {
            if (Hash == 0u)
                throw new InvalidOperationException("Content asset binary export rejected zero hash.");
            if (Kind == ContentAssetKind.Unknown || Kind > ContentAssetKind.Compute)
                throw new InvalidOperationException("Content asset binary export rejected invalid kind.");
            if (Tier > ContentTier.Overkill)
                throw new InvalidOperationException("Content asset binary export rejected invalid tier.");
            if (LodLevel > 2)
                throw new InvalidOperationException("Content asset binary export rejected unsupported LOD level.");
            if (EstimatedVramBytes < 0L)
                throw new InvalidOperationException("Content asset binary export rejected negative VRAM estimate.");

            uint[] dependencies = DependencyHashes;
            int dependencyCount = dependencies != null ? dependencies.Length : 0;
            if (dependencyCount > ushort.MaxValue)
                throw new InvalidOperationException("Content asset binary export rejected dependency overflow.");

            for (int i = 0; i < dependencyCount; i++)
            {
                uint dependency = dependencies[i];
                if (dependency == 0u)
                    throw new InvalidOperationException("Content asset binary export rejected zero dependency.");
                if (dependency == Hash)
                    throw new InvalidOperationException("Content asset binary export rejected self dependency.");

                for (int j = i + 1; j < dependencyCount; j++)
                {
                    if (dependencies[j] == dependency)
                        throw new InvalidOperationException("Content asset binary export rejected duplicate dependency.");
                }
            }
        }
    }

    /// <summary>
    /// Authoritative binary-hash to Unity asset bridge for Addressables, build gates, and runtime proxy systems.
    /// </summary>
    [CreateAssetMenu(menuName = "HECTON-8/Content/Asset Hash Map", fileName = "ContentAssetHashMap")]
    public sealed class ContentAssetHashMap : ScriptableObject
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const byte SortStateUnknown = 0;
        private const byte SortStateSorted = 1;
        private const byte SortStateUnsorted = 2;

        [SerializeField] private ContentAssetEntry[] entries = Array.Empty<ContentAssetEntry>();

        [NonSerialized] private byte _sortState;

        public int Count => entries != null ? entries.Length : 0;

        public ContentAssetEntry GetEntryAt(int index)
        {
            return entries[index];
        }

        public bool TryGetEntry(uint hash, out ContentAssetEntry entry)
        {
            if (_sortState != SortStateSorted)
                return TryGetEntryLinear(hash, out entry);

            int lo = 0;
            int hi = entries != null ? entries.Length - 1 : -1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                uint midHash = entries[mid].Hash;
                if (midHash == hash)
                {
                    entry = entries[mid];
                    return true;
                }

                if (midHash < hash)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            entry = default;
            return false;
        }

        public bool Has3DMeshBinding(uint hash)
        {
            return TryGetEntry(hash, out ContentAssetEntry entry) && entry.HasVisual3D();
        }

        public int CopyRequiredHashes(uint[] destination)
        {
            int requiredCount = CountRequiredBuildHashes();
            if (requiredCount == 0)
                return 0;

            int destinationLength = destination != null ? destination.Length : 0;
            if (destinationLength < requiredCount)
            {
                LogRequiredHashDestinationTooSmall(requiredCount, destinationLength);
                return -1;
            }

            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].RequiredInBuild || entries[i].Hash == 0u)
                    continue;

                destination[count] = entries[i].Hash;
                count++;
            }

            return count;
        }

        public int CountRequiredBuildHashes()
        {
            int count = 0;
            int length = entries != null ? entries.Length : 0;
            for (int i = 0; i < length; i++)
            {
                if (entries[i].RequiredInBuild && entries[i].Hash != 0u)
                    count++;
            }

            return count;
        }

        public static uint ComputeFnv1a32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        public void ForceSort()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            SortEntries();
#endif
        }

        private void OnEnable()
        {
            RefreshSortStateCold();
        }

        private void RefreshSortStateCold()
        {
            _sortState = IsSortedAscending() ? SortStateSorted : SortStateUnsorted;
        }

#if UNITY_EDITOR
        private void SortEntries()
        {
            if (entries == null)
            {
                _sortState = SortStateSorted;
                return;
            }

            for (int i = 1; i < entries.Length; i++)
            {
                ContentAssetEntry current = entries[i];
                int j = i - 1;
                while (j >= 0 && entries[j].Hash > current.Hash)
                {
                    entries[j + 1] = entries[j];
                    j--;
                }

                entries[j + 1] = current;
            }

            _sortState = SortStateSorted;
        }
#endif

        private bool IsSortedAscending()
        {
            if (entries == null || entries.Length < 2)
                return true;

            uint previous = entries[0].Hash;
            for (int i = 1; i < entries.Length; i++)
            {
                uint current = entries[i].Hash;
                if (current < previous)
                    return false;

                previous = current;
            }

            return true;
        }

        private bool TryGetEntryLinear(uint hash, out ContentAssetEntry entry)
        {
            int count = entries != null ? entries.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].Hash != hash)
                    continue;

                entry = entries[i];
                return true;
            }

            entry = default;
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogRequiredHashDestinationTooSmall(int requiredCount, int destinationLength)
        {
            Hecton8.Core.H8Debug.LogError("[ContentAssetHashMap] Required-hash copy rejected destinationLength=" +
                           destinationLength + " required=" + requiredCount + ".");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Hash != 0u || string.IsNullOrEmpty(entries[i].StableKey))
                    continue;

                entries[i].Hash = ComputeFnv1a32(entries[i].StableKey);
            }

            SortEntries();
        }
#endif
    }
}
