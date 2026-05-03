using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Sandboxed resource resolver exposed to mods.
    /// Returns hash identifiers only; engine owners resolve actual Unity assets.
    /// </summary>
    public interface IModResourceProxy
    {
        /// <summary>
        /// Resolves a prefab asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolvePrefab(string assetName, out uint hashId);

        /// <summary>
        /// Resolves an audio clip asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolveAudioClip(string assetName, out uint hashId);

        /// <summary>
        /// Resolves a texture asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolveTexture(string assetName, out uint hashId);
    }

    internal enum ModResourceKind : byte
    {
        Prefab = 1,
        AudioClip = 2,
        Texture = 3
    }

    internal sealed class ModResourceProxy : IModResourceProxy
    {
        internal static readonly ModResourceProxy Instance = new ModResourceProxy();

        private ModResourceProxy()
        {
        }

        public bool TryResolvePrefab(string assetName, out uint hashId)
        {
            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.Prefab, out hashId);
        }

        public bool TryResolveAudioClip(string assetName, out uint hashId)
        {
            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.AudioClip, out hashId);
        }

        public bool TryResolveTexture(string assetName, out uint hashId)
        {
            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.Texture, out hashId);
        }
    }

    internal static class ModResourceRegistry
    {
        private const int ResourceCapacity = 256;

        private struct ResourceRecord
        {
            public string ModId;
            public string AssetName;
            public ModResourceKind Kind;
        }

        // COLD ALLOC: ResourceRecord[256] - managed sidecar for engine-only resource resolution - owner: ModResourceRegistry
        private static readonly ResourceRecord[] _records = new ResourceRecord[ResourceCapacity];
        private static NativeHashMap<uint, int> _resourceIndexByHash;
        private static int _recordCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Initialize()
        {
            if (!_resourceIndexByHash.IsCreated)
            {
                _resourceIndexByHash = new NativeHashMap<uint, int>(ResourceCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[256] - O(1) resource hash to sidecar index - owner: ModResourceRegistry
                NativeMemorySentinel.RegisterNativeHashMap(_resourceIndexByHash, nameof(ModResourceRegistry), nameof(_resourceIndexByHash), NativeAllocationLifetime.Session);
            }
        }

        internal static void Shutdown()
        {
            if (_resourceIndexByHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModResourceRegistry), nameof(_resourceIndexByHash));
                _resourceIndexByHash.Dispose();
                _resourceIndexByHash = default;
            }

            for (int i = 0; i < _recordCount; i++)
                _records[i] = default;

            _recordCount = 0;
        }

        internal static bool TryRegister(
            string modId,
            string assetName,
            ModResourceKind kind,
            out uint hashId)
        {
            hashId = 0u;

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Resource proxy calls must originate from an active mod execution scope.");

            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName) || kind == 0)
                return false;

            Initialize();
            hashId = ComputeResourceHash(modId, assetName, kind);
            if (hashId == 0u)
                return false;

            if (_resourceIndexByHash.ContainsKey(hashId))
                return true;

            if (_recordCount >= ResourceCapacity)
                return false;

            _records[_recordCount] = new ResourceRecord
            {
                ModId = modId,
                AssetName = assetName,
                Kind = kind
            };
            _resourceIndexByHash.Add(hashId, _recordCount);
            _recordCount++;
            return true;
        }

        internal static bool TryResolvePrefab(uint hashId, out GameObject prefab)
        {
            prefab = null;
            if (!TryResolve(hashId, ModResourceKind.Prefab, out ResourceRecord record))
                return false;

            prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);
            return prefab != null;
        }

        internal static bool TryResolveAudioClip(uint hashId, out AudioClip clip)
        {
            clip = null;
            if (!TryResolve(hashId, ModResourceKind.AudioClip, out ResourceRecord record))
                return false;

            clip = ModAssetManager.LoadAudioClip(record.ModId, record.AssetName);
            return clip != null;
        }

        internal static bool TryResolveTexture(uint hashId, out Texture2D texture)
        {
            texture = null;
            if (!TryResolve(hashId, ModResourceKind.Texture, out ResourceRecord record))
                return false;

            texture = ModAssetManager.LoadTexture(record.ModId, record.AssetName);
            return texture != null;
        }

        private static bool TryResolve(uint hashId, ModResourceKind expectedKind, out ResourceRecord record)
        {
            record = default;
            if (hashId == 0u || !_resourceIndexByHash.IsCreated)
                return false;

            if (!_resourceIndexByHash.TryGetValue(hashId, out int index) ||
                (uint)index >= (uint)_recordCount)
            {
                return false;
            }

            record = _records[index];
            return record.Kind == expectedKind;
        }

        private static uint ComputeResourceHash(string modId, string assetName, ModResourceKind kind)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixHash(hash, (uint)LocHash.Compute(modId));
                hash = MixHash(hash, (uint)LocHash.Compute(assetName));
                hash = MixHash(hash, (uint)kind);
                return hash == 0u ? 1u : hash;
            }
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }
}
